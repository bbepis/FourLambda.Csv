using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace FourLambda.Csv;

/// <summary>
/// High-performance CSV reader that accepts UTF-16 data.
/// </summary>
public sealed unsafe class CsvReaderUtf16 : IDisposable
{
	private readonly TextReader reader;
	private int currentBufferSize;
	private int currentBufferOffset;

	private readonly (int offset, int length, byte escapedCount)[] fieldInfo;
	private int fieldCount = 0;

	private readonly Vector256<byte> separatorVector;
	private readonly Vector256<byte> escapeVector;
	private readonly Vector256<byte> newlineVector;
	private int crPadding = 0;

	private char* bufferPtr;
	private int bufferSize;
	private readonly int maxBufferSize;
	private Span<char> BufferSpan => new(bufferPtr, bufferSize);

	/// <summary>
	/// Gets the count of fields in the current line.
	/// </summary>
	public int FieldCount => fieldCount;

	/// <summary>
	/// Initializes a new instance of the <see cref="CsvReaderUtf16"/> class. Uses Avx2 hardware instructions if available.
	/// </summary>
	/// <param name="reader">The text reader containing the CSV data.</param>
	/// <param name="lineBufferSize">The size of the buffer used to store lines of text as they get processed. Lines will be read incorrectly if they are larger than this buffer.</param>
	/// <param name="maxFieldCount">The maximum number of fields expected in a line.</param>
	public CsvReaderUtf16(TextReader reader, int lineBufferSize = 32 * 1024, int maxFieldCount = 256)
	{
		this.reader = reader;

		maxBufferSize = bufferSize = currentBufferOffset = lineBufferSize;
		bufferPtr = (char*)NativeMemory.AlignedAlloc((nuint)maxBufferSize * sizeof(char), 64);
		fieldInfo = new (int offset, int length, byte escapedCount)[maxFieldCount];

		if (Avx2.IsSupported)
		{
			separatorVector = Vector256.Create((byte)',');
			newlineVector = Vector256.Create((byte)'\n');
			escapeVector = Vector256.Create((byte)'\"');
		}

		//if (lineEndChar == '\n')
		crPadding = -1;
	}

	~CsvReaderUtf16()
	{
		if (bufferPtr != (char*)0)
			NativeMemory.AlignedFree(bufferPtr);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (bufferPtr != (char*)0)
		{
			NativeMemory.AlignedFree(bufferPtr);
			bufferPtr = (char*)0;
		}

		fieldCount = 0;
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Reads the next line of the CSV stream and updates the internal state.
	/// </summary>
	/// <returns>True if a line was successfully read; otherwise, false.</returns>
	public bool ReadNext()
	{
		if (bufferPtr == (char*)0)
			return false;

		int endByte = 0;

		var bufferSpan = BufferSpan;

		if (currentBufferOffset + currentBufferSize >= bufferSize)
			goto renew;


		currentBufferOffset += currentBufferSize + 1;

		cycle:

		endByte = DetermineFields(currentBufferOffset);

		if (endByte > 0)
		{
			currentBufferSize = endByte - currentBufferOffset;
			return true;
		}

		if (bufferSize < maxBufferSize)
		{
			// reached the end without encountering a newline
			currentBufferSize = bufferSize - currentBufferOffset;
			return currentBufferSize != 0;
		}

		endByte = bufferSize - currentBufferOffset;
		new Span<char>(bufferPtr + currentBufferOffset, endByte).CopyTo(bufferSpan);



		renew:

		currentBufferOffset = 0;
		currentBufferSize = 0;

		int availableBytes = endByte;

		while (availableBytes < maxBufferSize)
		{
			int read = reader.Read(bufferSpan.Slice(availableBytes));

			if (read == 0)
				break;

			availableBytes += read;
		}

		bufferSize = availableBytes;

		if (availableBytes == 0)
		{
			fieldCount = 0;
			return false;
		}

		goto cycle;
	}

	private void FieldCheck(int field)
	{
		if (field > fieldCount)
			throw new ArgumentOutOfRangeException(nameof(field), $"Field {field} is greater than the actual range of fields for the line ({fieldCount})");
	}

	/// <summary>
	/// Retrieves the memory span of the specified field, unescaping if necessary. An allocation is required for unescaping; otherwise the returned <see cref="ReadOnlyMemory{char}"/> is invalid after <see cref="ReadNext"/> is called again.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The memory span of the specified field.</returns>
	public string GetString(int field)
	{
		return new string(GetSpan(field));
	}

	/// <summary>
	/// Parses the specified field to the specified type. Type must implement <see cref="ISpanParsable{T}"/>.
	/// </summary>
	/// <typeparam name="T">The type to parse as.</typeparam>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The parsed value of the specified field.</returns>
	public T Parse<T>(int field) where T : ISpanParsable<T>
	{
		// TODO: add branch to check if field is escaped and use stackalloc instead
		return T.Parse(GetSpan(field), null);
	}

	/// <summary>
	/// Returns a value specifying if the field needs to be escaped, and the raw length of the field. If the field is escaped, <see cref="rawLength"/> can be considered the upper bound of the length of the unescaped value. Otherwise, <see cref="rawLength"/> is the length of the unescaped value.
	/// </summary>
	/// <param name="field">The index of the field to retrieve information about.</param>
	/// <param name="rawLength">If the field is escaped, <see cref="rawLength"/> can be considered the upper bound of the length of the unescaped value. Otherwise, <see cref="rawLength"/> is the length of the unescaped value.</param>
	/// <returns>If the field needs to be unescaped.</returns>
	public bool NeedsEscape(int field, out int rawLength)
	{
		FieldCheck(field);
		var info = fieldInfo[field];
		rawLength = info.length;
		return info.escapedCount > 3;
	}

	// TODO: create hybrid public Span<char> WriteToSpan(int field, Span<char> buffer, out int written)

	/// <summary>
	/// Copies the data from the specified field directly to the destination <see cref="Span{char}"/>, unescaping if necessary. Returns the written amount of chars. If you need to copy data from this field into another span, this method is always faster than using <see cref="GetSpan"/>.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <param name="destination">The destination <see cref="Span{char}"/> to copy chars to.</param>
	/// <returns>The written amount of chars.</returns>
	public int WriteToSpan(int field, Span<char> destination)
	{
		FieldCheck(field);
		var info = fieldInfo[field];

		if (info.escapedCount == 0)
		{
			new Span<char>(bufferPtr + info.offset, info.length).CopyTo(destination);
			return info.length;
		}

		// since we don't need to worry about UTF conversion, we can just do a quick shortcut to find out if this column only has a simple escape, and then just return the contents between them
		if (info.escapedCount == 3 && bufferPtr[info.offset] == '\"' && bufferPtr[info.offset + info.length - 1] == '\"')
		{
			new Span<char>(bufferPtr + info.offset + 1, info.length - 2).CopyTo(destination);
			return info.length - 2;
		}

		return UnescapeField(destination, info);
	}

	/// <summary>
	/// Retrieves the specified field as a <see cref="Span{char}"/>, unescaping if necessary. If the field doesn't need unescaping, then this method is extremely fast and requires no allocations.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The unescaped text data of the specified field.</returns>
	public ReadOnlySpan<char> GetSpan(int field)
	{
		FieldCheck(field);
		var info = fieldInfo[field];

		if (info.escapedCount == 0)
		{
			return new Span<char>(bufferPtr + info.offset, info.length);
		}

		// since we don't need to worry about UTF conversion, we can just do a quick shortcut to find out if this column only has a simple escape, and then just return the contents between them
		if (info.escapedCount == 3 && bufferPtr[info.offset] == '\"' && bufferPtr[info.offset + info.length - 1] == '\"')
		{
			return new Span<char>(bufferPtr + info.offset + 1, info.length - 2);
		}

		Span<char> buffer = new char[info.length];
		var length = UnescapeField(buffer, info);

		return buffer.Slice(0, length);
	}

	/// <summary>
	/// Retrieves the specified field as a <see cref="Span{char}"/>, without performing any unescaping regardless of if the field needs it.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The text data of the specified field.</returns>
	public ReadOnlySpan<char> GetRawSpan(int field)
	{
		FieldCheck(field);
		var info = fieldInfo[field];

		return new Span<char>(bufferPtr + info.offset, info.length);
	}

	/// <summary>
	/// Retrieves the specified field as an integer, unescaping if necessary.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The integer value of the specified field.</returns>
	public int GetInt32(int field) => Parse<int>(field);

	/// <summary>
	/// Retrieves the specified field as an unsigned integer, unescaping if necessary.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The unsigned integer value of the specified field.</returns>
	public uint GetUInt32(int field) => Parse<uint>(field);

	/// <summary>
	/// Retrieves the specified field as a long, unescaping if necessary.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The long value of the specified field.</returns>
	public long GetInt64(int field) => Parse<long>(field);

	/// <summary>
	/// Retrieves the specified field as an unsigned long, unescaping if necessary.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The unsigned long value of the specified field.</returns>
	public ulong GetUInt64(int field) => Parse<ulong>(field);

	/// <summary>
	/// Retrieves the specified field as a float, unescaping if necessary.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The float value of the specified field.</returns>
	public float GetFloat(int field) => Parse<float>(field);

	/// <summary>
	/// Retrieves the specified field as a double, unescaping if necessary.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The double value of the specified field.</returns>
	public double GetDouble(int field) => Parse<double>(field);

	/// <summary>
	/// Retrieves the specified field as a decimal, unescaping if necessary.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The decimal value of the specified field.</returns>
	public decimal GetDecimal(int field) => Parse<decimal>(field);

	/// <summary>
	/// Retrieves the specified field as a DateTime, unescaping if necessary.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The DateTime value of the specified field.</returns>
	public DateTime GetDateTime(int field) => Parse<DateTime>(field);

	private int DetermineFields(int bufferOffset)
	{
		int endChar = -1;
		int fieldStart = bufferOffset;
		byte wasOnceEscaped = 0;
		bool isEscaped = false;
		fieldCount = 0;

		int i = bufferOffset;

		if (Avx2.IsSupported)
			for (; i + 31 < bufferSize; i += 32)
			{
				var dataVector1 = Avx.LoadVector256((short*)bufferPtr + i);
				var dataVector2 = Avx.LoadVector256((short*)bufferPtr + i + 16);

				// values 256 -> 32767 will be cast correctly and saturated to 255.
				// 32768 -> 65535 will be interpreted as negative and saturate to 0. perfect for us, since ASCII remains untouched
				var packedData = Avx2.PackUnsignedSaturate(dataVector1, dataVector2);

				var dataVector = Avx2.Permute4x64(packedData.AsUInt64(), 0b11_01_10_00).AsByte();

				uint separatorMask = (uint)Avx2.MoveMask(Avx2.CompareEqual(dataVector, separatorVector));
				uint escapeMask = (uint)Avx2.MoveMask(Avx2.CompareEqual(dataVector, escapeVector));
				uint newlineMask = (uint)Avx2.MoveMask(Avx2.CompareEqual(dataVector, newlineVector));

				uint combinedMask = separatorMask | escapeMask | newlineMask;

				while (combinedMask != 0)
				{
					int index = BitOperations.TrailingZeroCount(combinedMask);
					uint bit = (1u << index);

					if ((escapeMask & bit) != 0)
					{
						isEscaped = !isEscaped;
						wasOnceEscaped = (byte)((wasOnceEscaped << 1) | 1);

						goto continueLoop;
					}

					if (isEscaped)
						goto continueLoop;

					if ((separatorMask & bit) != 0)
					{
						fieldInfo[fieldCount++] = (fieldStart, i + index - fieldStart, wasOnceEscaped);

						fieldStart = i + index + 1;
						wasOnceEscaped = 0;
					}
					else if ((newlineMask & bit) != 0)
					{
						endChar = i + index;
						goto exit;
					}

					continueLoop:
					combinedMask &= ~bit; // clear this bit
				}
			}

		for (; i < bufferSize; i++)
		{
			char c = bufferPtr[i];

			if (c == '"')
			{
				isEscaped = !isEscaped;
				wasOnceEscaped = (byte)((wasOnceEscaped << 1) | 1);

				continue;
			}

			if (isEscaped)
				continue;

			if (c == ',')
			{
				fieldInfo[fieldCount++] = (fieldStart, i - fieldStart, wasOnceEscaped);

				fieldStart = i + 1;
				wasOnceEscaped = 0;
			}

			if (c == '\n')
			{
				endChar = i;
				break;
			}
		}

		exit:

		if (crPadding == -1)
		{
			crPadding = endChar > 0 && bufferPtr[endChar - 1] == '\r' ? 1 : 0;
		}

		fieldInfo[fieldCount++] = (fieldStart, (endChar < 0 ? bufferSize : endChar - crPadding) - fieldStart, wasOnceEscaped);

		return endChar;
	}

	//private static readonly Vector256<ushort> utf16QuoteVector = Vector256.Create((ushort)'\"');
	private int UnescapeField(Span<char> destination, (int offset, int length, byte escapedCount) info)
	{
		int rawLength = info.offset + info.length;

		int idx = 0;
		int i = info.offset;

		//Span<ushort> ushortDest = MemoryMarshal.Cast<char, ushort>(destination);

		//for (; i + 15 < rawLength; i += 16)
		//{
		//	var dataVector = Avx.LoadVector256((ushort*)bufferPtr + i);
		//	var mask = (uint)Avx2.MoveMask(Avx2.CompareEqual(dataVector, utf16QuoteVector).AsByte());

		//	dataVector.CopyTo(ushortDest.Slice(idx));
		//	idx += 16;

		//	if (mask == 0)
		//	{
		//		continue;
		//	}

		//	int trailing;

		//	while ((trailing = BitOperations.TrailingZeroCount(mask)) < 32)
		//	{
		//		mask ^= (3u << trailing);
		//	}
		//}

		for (; i < rawLength; i++)
		{
			char c = bufferPtr[i];
			if (c == '"')
			{
				if (i != info.offset && i + 1 < rawLength)
				{
					destination[idx++] = bufferPtr[i + 1];
					i++;
				}
			}
			else
				destination[idx++] = c;
		}

		return idx;
	}
}