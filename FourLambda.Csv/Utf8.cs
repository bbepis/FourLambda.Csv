using System.Buffers.Text;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace FourLambda.Csv;

/// <summary>
/// High-performance CSV reader that accepts UTF-8 data.
/// </summary>
public sealed unsafe class CsvReaderUtf8 : IDisposable
{
	private readonly Stream stream;
	private int currentBufferSize;
	private int currentBufferOffset;

	private readonly (int offset, int length, bool isEscaped)[] fieldInfo;
	private int fieldCount = 0;

	private readonly Vector256<byte> separatorVector;
	private readonly Vector256<byte> escapeVector;
	private readonly Vector256<byte> newlineVector;

	private int crPadding = 0;

	private byte* bufferPtr;
	private int bufferSize;
	private readonly int maxBufferSize;

	private bool needsToCheckBOM = true;

	private Span<byte> BufferSpan => new Span<byte>(bufferPtr, bufferSize);

	/// <summary>
	/// Gets the count of fields in the current line.
	/// </summary>
	public int FieldCount => fieldCount;

	/// <summary>
	/// Initializes a new instance of the <see cref="CsvReaderUtf8"/> class. Uses Avx2 hardware instructions.
	/// </summary>
	/// <param name="stream">The stream containing the DSV data.</param>
	/// <param name="lineBufferSize">The size of the buffer used to store lines of text as they get processed. Lines will be read incorrectly if they are larger than this buffer.</param>
	/// <param name="maxFieldCount">The maximum number of fields expected in a line.</param>
	public CsvReaderUtf8(Stream stream, int lineBufferSize = 32 * 1024, int maxFieldCount = 256)
	{
		this.stream = stream;

		maxBufferSize = bufferSize = currentBufferOffset = lineBufferSize;
		bufferPtr = (byte*)NativeMemory.AlignedAlloc((nuint)maxBufferSize, 64);

		fieldInfo = new (int offset, int length, bool isEscaped)[maxFieldCount];

		if (Avx2.IsSupported)
		{
			separatorVector = Vector256.Create((byte)',');
			newlineVector = Vector256.Create((byte)'\n');
			escapeVector = Vector256.Create((byte)'\"');
		}

		//if (lineEndChar == '\n')
		crPadding = -1;
	}

	~CsvReaderUtf8()
	{
		if (bufferPtr != (byte*)0)
			NativeMemory.AlignedFree(bufferPtr);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (bufferPtr != (byte*)0)
		{
			NativeMemory.AlignedFree(bufferPtr);
			bufferPtr = (byte*)0;
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
		if (bufferPtr == (byte*)IntPtr.Zero)
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
		new Span<byte>(bufferPtr + currentBufferOffset, endByte).CopyTo(bufferSpan);


		renew:

		currentBufferOffset = 0;
		currentBufferSize = 0;

		int availableBytes = endByte;

		while (availableBytes < maxBufferSize)
		{
			int read = stream.Read(bufferSpan.Slice(availableBytes));

			if (read == 0)
				break;

			availableBytes += read;
		}

		bufferSize = availableBytes;

		if (availableBytes == 0)
		{
			Dispose();
			return false;
		}

		if (needsToCheckBOM)
		{
			if (availableBytes >= 3 &&
			    bufferSpan[0] == 0xEF && bufferSpan[1] == 0xBB && bufferSpan[2] == 0xBF)
			{
				currentBufferOffset += 3;
			}
		}

		needsToCheckBOM = false;

		goto cycle;
	}

	private void FieldCheck(int field)
	{
		if (field > fieldCount)
			throw new ArgumentOutOfRangeException(nameof(field), $"Field {field} is greater than the actual range of fields for the line ({fieldCount})");
	}

	/// <summary>
	/// Parses the specified field to the specified type. Type must implement <see cref="IUtf8SpanParsable{T}"/> and <see cref="ISpanParsable{T}"/>.
	/// </summary>
	/// <typeparam name="T">The type to parse as.</typeparam>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The parsed value of the specified field.</returns>
	public T Parse<T>(int field) where T : IUtf8SpanParsable<T>, ISpanParsable<T>
	{
		FieldCheck(field);
		var info = fieldInfo[field];

		if (!info.isEscaped)
			return T.Parse(new Span<byte>(bufferPtr + info.offset, info.length), null);

		Span<char> buffer = stackalloc char[info.length];
		var length = UnescapeField(buffer, info);

		return T.Parse(buffer.Slice(0, length), null);
	}

	/// <summary>
	/// Copies the data from the specified field directly to the destination <see cref="Span{char}"/>, unescaping if necessary. Returns the written amount of chars.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <param name="destination">The destination <see cref="Span{char}"/> to copy chars to.</param>
	/// <returns>The written amount of chars.</returns>
	public int WriteToSpan(int field, Span<char> destination)
	{
		FieldCheck(field);
		var info = fieldInfo[field];

		return UnescapeField(destination, info);
	}

	/// <summary>
	/// Retrieves the specified field as a <see cref="Span{byte}"/> containing UTF-8 data, without performing any unescaping regardless of if the field needs it.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The UTF-8 data of the specified field.</returns>
	public ReadOnlySpan<byte> GetSpanRaw(int field)
	{
		FieldCheck(field);
		var info = fieldInfo[field];

		return new Span<byte>(bufferPtr + info.offset, info.length);
	}

	/// <summary>
	/// Retrieves the specified field as a string, unescaping if necessary.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The string value of the specified field.</returns>
	public string GetString(int field)
	{
		FieldCheck(field);
		var info = fieldInfo[field];

		if (!info.isEscaped)
			return Encoding.UTF8.GetString(new Span<byte>(bufferPtr + info.offset, info.length));

		Span<char> buffer = stackalloc char[info.length];
		var length = UnescapeField(buffer, info);

		return new string(buffer.Slice(0, length));
	}

	/// <summary>
	/// Retrieves the specified field as a 32-bit integer, unescaping if necessary.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The integer value of the specified field.</returns>
	public int GetInt32(int field) => Parse<int>(field);

	/// <summary>
	/// Retrieves the specified field as an unsigned 32-bit integer, unescaping if necessary.
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
	public DateTime GetDateTime(int field)
	{
		var info = fieldInfo[field];

		if (!info.isEscaped)
		{
			var span = GetSpanRaw(field);
			if (Utf8Parser.TryParse(span, out DateTime value, out _))
				return value;

			//throw new Exception($"Could not parse field as DateTime: {Encoding.UTF8.GetString(span)}");
		}

		Span<char> buffer = stackalloc char[info.length];
		var bufferLength = UnescapeField(buffer, info);
		return DateTime.Parse(buffer.Slice(0, bufferLength));
	}

	private int DetermineFields(int bufferOffset)
	{
		int endChar = -1;
		int fieldStart = bufferOffset;
		bool wasOnceEscaped = false;
		bool isEscaped = false;
		fieldCount = 0;

		int i = bufferOffset;

		if (Avx2.IsSupported)
			for (; i + 31 < bufferSize; i += 32)
			{
				var dataVector = Avx.LoadVector256(bufferPtr + i);


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
						wasOnceEscaped = true;

						goto continueLoop;
					}

					if (isEscaped)
						goto continueLoop;

					if ((separatorMask & bit) != 0)
					{
						fieldInfo[fieldCount++] = (fieldStart, i + index - fieldStart, wasOnceEscaped);

						fieldStart = i + index + 1;
						wasOnceEscaped = false;
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
			byte c = bufferPtr[i];

			if (c == (byte)'"')
			{
				isEscaped = !isEscaped;
				wasOnceEscaped = true;

				continue;
			}

			if (isEscaped)
				continue;

			if (c == (byte)',')
			{
				fieldInfo[fieldCount++] = (fieldStart, i - fieldStart, wasOnceEscaped);

				fieldStart = i + 1;
				wasOnceEscaped = false;
			}

			if (c == (byte)'\n')
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

	private int UnescapeField(Span<char> destination, (int offset, int length, bool isEscaped) info)
	{
		int idx = 0;
		int rawLength = info.offset + info.length;

		for (int i = info.offset; i < rawLength; i++)
		{
			byte c = bufferPtr[i];
			if (c == (byte)'"')
			{
				if (i != info.offset && i + 1 < rawLength)
				{
					var nextC = bufferPtr[i + 1];
					if ((nextC & 0x80) == 0)
					{
						destination[idx++] = (char)nextC;
						i++;
					}
				}
			}
			else if ((c & 0x80) != 0) // multi-byte sequence
			{
				var encodedLength = BitOperations.LeadingZeroCount((uint)(~c & 0xFF)) - 24;

				if (encodedLength == 2) // 2 bytes long - always 1 utf16 char
				{
					destination[idx++] = (char)((c & 0b0001_1111) << 6 | (bufferPtr[i + 1] & 0b0011_1111));
					i += 1;
				}
				else if (encodedLength == 3) // 3 bytes long - always 1 utf16 char
				{
					destination[idx++] = (char)((c & 0b0000_1111) << 12 |
									   (bufferPtr[i + 1] & 0b0011_1111) << 6 |
									   (bufferPtr[i + 2] & 0b0011_1111));
					i += 2;
				}
				else // 4 bytes long - always 2 utf16 chars as a surrogate pair. convert UTF8 -> UTF32 -> UTF16
				{
					uint codePoint = (uint)((c & 0b0000_0111) << 18 |
									   (bufferPtr[i + 1] & 0b0011_1111) << 12 |
									   (bufferPtr[i + 2] & 0b0011_1111) << 6 |
									   (bufferPtr[i + 3] & 0b0011_1111));

					codePoint -= 0x10000;
					destination[idx++] = (char)(ushort)((codePoint >> 10) + 0xD800);  // high surrogate
					destination[idx++] = (char)(ushort)((codePoint & 0x3FF) + 0xDC00); // low surrogate

					i += 4;
				}
			}
			else
			{
				destination[idx++] = (char)c;
			}
		}

		return idx;
	}
}