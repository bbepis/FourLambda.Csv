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
public sealed unsafe class CsvReaderUtf8 : ICsvReader
{
	private readonly Stream stream;
	private int currentBufferSize;
	private int currentBufferOffset;

	private readonly (int offset, int length, bool isEscaped)[] fieldInfo;
	private int fieldCount = 0;

	private readonly char separatorChar;

	private readonly Vector256<byte> separatorVector;
	private readonly Vector256<byte> escapeVector;
	private readonly Vector256<byte> newlineVector;

	private int crPadding = 0;

	private byte* bufferPtr;
	private int bufferSize;
	private readonly int maxBufferSize;

	private bool needsToCheckBOM = true;

	private Span<byte> BufferSpan => new Span<byte>(bufferPtr, bufferSize);

	/// <inheritdoc/>
	public int FieldCount => fieldCount;

	/// <inheritdoc/>
	public IReadOnlyDictionary<string, int>? Headers { get; private set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="CsvReaderUtf8"/> class. Uses Avx2 hardware instructions.
	/// </summary>
	/// <param name="stream">The stream containing the CSV data.</param>
	/// <param name="hasHeaders">Whether the supplied CSV file has headers; if true, they'll be loaded into the <see cref="Headers"/> property.</param>s
	/// <param name="lineBufferSize">The size of the buffer used to store lines of text as they get processed. Lines will be read incorrectly if they are larger than this buffer.</param>
	/// <param name="maxFieldCount">The maximum number of fields expected in a line.</param>
	/// <param name="separatorChar">The character that separates fields for the supplied CSV file.</param>
	public CsvReaderUtf8(Stream stream, bool hasHeaders = false, int lineBufferSize = 32 * 1024, int maxFieldCount = 256, char separatorChar = ',')
	{
		this.stream = stream;

		maxBufferSize = bufferSize = currentBufferOffset = lineBufferSize;
		bufferPtr = (byte*)NativeMemory.AlignedAlloc((nuint)maxBufferSize * 3, 64); // x1 for byte buffer, x2 for char unescape buffer = x3

		fieldInfo = new (int offset, int length, bool isEscaped)[maxFieldCount];

		if ((ushort)separatorChar > 127)
			throw new ArgumentOutOfRangeException(nameof(separatorChar), "Separator character must be within ASCII range.");

		this.separatorChar = separatorChar;

		if (Avx2.IsSupported)
		{
			separatorVector = Vector256.Create((byte)separatorChar);
			newlineVector = Vector256.Create((byte)'\n');
			escapeVector = Vector256.Create((byte)'\"');
		}

		//if (lineEndChar == '\n')
		crPadding = -1;

		if (hasHeaders && ReadNext())
		{
			var headerDictionary = new Dictionary<string, int>(FieldCount);

			for (int i = 0; i < FieldCount; i++)
				headerDictionary[GetString(i)] = i;

			Headers = headerDictionary;
		}
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

	/// <inheritdoc/>
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

		if (currentBufferOffset == 0)
		{
			throw new InternalBufferOverflowException("Supplied line buffer is too small to handle this CSV file. Try increasing lineBufferSize in the constructor");
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

	/// <inheritdoc/>
	public T Parse<T>(int field) where T : ISpanParsable<T>
	{
		FieldCheck(field);
		var info = fieldInfo[field];

		Span<char> buffer = new Span<char>(bufferPtr + maxBufferSize + info.offset * 2, info.length);
		var length = UnescapeField(buffer, info);

		return T.Parse(buffer.Slice(0, length), null);
	}

	/// <summary>
	/// Parses the specified field to the specified type. Type must implement <see cref="IUtf8SpanParsable{T}"/> and <see cref="ISpanParsable{T}"/>. Prefer this over the regular <see cref="Parse"/> where possible, as this doesn't require expansion to UTF-16 first if the field doesn't need to be escaped.
	/// </summary>
	/// <typeparam name="T">The type to parse as.</typeparam>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The parsed value of the specified field.</returns>
	public T ParseUtf8<T>(int field) where T : IUtf8SpanParsable<T>, ISpanParsable<T>
	{
		FieldCheck(field);
		var info = fieldInfo[field];

		if (!info.isEscaped)
			return T.Parse(new Span<byte>(bufferPtr + info.offset, info.length), null);

		Span<char> buffer = new Span<char>(bufferPtr + maxBufferSize + info.offset * 2, info.length);
		var length = UnescapeField(buffer, info);

		return T.Parse(buffer.Slice(0, length), null);
	}

	/// <inheritdoc/>
	public bool NeedsEscape(int field, out int rawLength)
	{
		FieldCheck(field);
		var info = fieldInfo[field];
		rawLength = info.length;
		return info.isEscaped;
	}

	/// <inheritdoc/>
	public int WriteToSpan(int field, Span<char> destination)
	{
		FieldCheck(field);
		var info = fieldInfo[field];

		return UnescapeField(destination, info);
	}

	/// <summary>
	/// Retrieves the specified field as a <see cref="Span{char}"/>, unescaping if necessary.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The unescaped text data of the specified field.</returns>
	public ReadOnlySpan<char> GetSpan(int field)
	{
		FieldCheck(field);
		var info = fieldInfo[field];

		Span<char> buffer = new Span<char>(bufferPtr + maxBufferSize + info.offset * 2, info.length);
		var length = UnescapeField(buffer, info);

		return buffer.Slice(0, length);
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

	/// <inheritdoc/>
	public string GetString(int field)
	{
		FieldCheck(field);
		var info = fieldInfo[field];

		if (!info.isEscaped)
			return Encoding.UTF8.GetString(new Span<byte>(bufferPtr + info.offset, info.length));

		Span<char> buffer = new Span<char>(bufferPtr + maxBufferSize + info.offset * 2, info.length);
		var length = UnescapeField(buffer, info);

		return new string(buffer.Slice(0, length));
	}

	/// <inheritdoc/>
	public int GetInt32(int field) => ParseUtf8<int>(field);

	/// <inheritdoc/>
	public uint GetUInt32(int field) => ParseUtf8<uint>(field);

	/// <inheritdoc/>
	public long GetInt64(int field) => ParseUtf8<long>(field);

	/// <inheritdoc/>
	public ulong GetUInt64(int field) => ParseUtf8<ulong>(field);

	/// <inheritdoc/>
	public float GetFloat(int field) => ParseUtf8<float>(field);

	/// <inheritdoc/>
	public double GetDouble(int field) => ParseUtf8<double>(field);

	/// <inheritdoc/>
	public decimal GetDecimal(int field) => ParseUtf8<decimal>(field);

	/// <inheritdoc/>
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

		Span<char> buffer = new Span<char>(bufferPtr + maxBufferSize + info.offset * 2, info.length);
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

				nuint separatorMask = (uint)Avx2.MoveMask(Avx2.CompareEqual(dataVector, separatorVector));
				nuint escapeMask = (uint)Avx2.MoveMask(Avx2.CompareEqual(dataVector, escapeVector));
				nuint newlineMask = (uint)Avx2.MoveMask(Avx2.CompareEqual(dataVector, newlineVector));

				nuint combinedMask = separatorMask | escapeMask | newlineMask;

				while (combinedMask != 0)
				{
					int index = BitOperations.TrailingZeroCount(combinedMask);
					nuint bit = (nuint)1 << index;

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

			if (c == (byte)separatorChar)
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