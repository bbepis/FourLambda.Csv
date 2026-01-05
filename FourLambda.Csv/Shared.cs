using System.Text;

namespace FourLambda.Csv;

/// <summary>
/// A generic interface over <see cref="CsvReaderUtf8"/> and <see cref="CsvReaderUtf16"/>. Does not include some higher performance methods as they're encoding specific; you should cast to / create the concrete types if you want access to them.
/// </summary>
public interface ICsvReader : IDisposable
{
	/// <summary>
	/// Gets the count of fields in the current line.
	/// </summary>
	int FieldCount { get; }

	/// <summary>
	/// A dictionary containing the headers of the CSV file. Only populated if enabled via the constructor; otherwise is null.
	/// </summary>
	IReadOnlyDictionary<string, int>? Headers { get; }

	/// <summary>
	/// Reads the next line of the CSV stream and updates the internal state.
	/// </summary>
	/// <returns>True if a line was successfully read; otherwise, false.</returns>
	bool ReadNext();

	/// <summary>
	/// Parses the specified field to the specified type. Type must implement <see cref="ISpanParsable{T}"/>.
	/// </summary>
	/// <typeparam name="T">The type to parse as.</typeparam>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The parsed value of the specified field.</returns>
	T Parse<T>(int field) where T : ISpanParsable<T>;

	/// <summary>
	/// Returns a value specifying if the field needs to be escaped, and the raw length of the field. If the field is escaped, <see cref="rawLength"/> can be considered the upper bound of the length of the unescaped value. Otherwise, <see cref="rawLength"/> is the length of the unescaped value.
	/// </summary>
	/// <param name="field">The index of the field to retrieve information about.</param>
	/// <param name="rawLength">If the field is escaped, <see cref="rawLength"/> can be considered the upper bound of the length of the unescaped value. Otherwise, <see cref="rawLength"/> is the length of the unescaped value.</param>
	/// <returns>If the field needs to be unescaped.</returns>
	bool NeedsEscape(int field, out int rawLength);

	/// <summary>
	/// Copies the data from the specified field directly to the destination <see cref="Span{char}"/>, unescaping if necessary. Returns the written amount of chars.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <param name="destination">The destination <see cref="Span{char}"/> to copy chars to.</param>
	/// <returns>The written amount of chars.</returns>
	int WriteToSpan(int field, Span<char> destination);

	/// <summary>
	/// Retrieves the specified field as a <see cref="Span{char}"/>, unescaping if necessary.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The unescaped text data of the specified field.</returns>
	ReadOnlySpan<char> GetSpan(int field);

	/// <summary>
	/// Retrieves the specified field as a string, unescaping if necessary.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The string value of the specified field.</returns>
	string GetString(int field);

	/// <summary>
	/// Retrieves the specified field as a 32-bit integer, unescaping if necessary.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The integer value of the specified field.</returns>
	int GetInt32(int field);

	/// <summary>
	/// Retrieves the specified field as an unsigned 32-bit integer, unescaping if necessary.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The unsigned integer value of the specified field.</returns>
	uint GetUInt32(int field);

	/// <summary>
	/// Retrieves the specified field as a long, unescaping if necessary.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The long value of the specified field.</returns>
	long GetInt64(int field);

	/// <summary>
	/// Retrieves the specified field as an unsigned long, unescaping if necessary.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The unsigned long value of the specified field.</returns>
	ulong GetUInt64(int field);

	/// <summary>
	/// Retrieves the specified field as a float, unescaping if necessary.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The float value of the specified field.</returns>
	float GetFloat(int field);

	/// <summary>
	/// Retrieves the specified field as a double, unescaping if necessary.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The double value of the specified field.</returns>
	double GetDouble(int field);

	/// <summary>
	/// Retrieves the specified field as a decimal, unescaping if necessary.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The decimal value of the specified field.</returns>
	decimal GetDecimal(int field);

	/// <summary>
	/// Retrieves the specified field as a DateTime, unescaping if necessary.
	/// </summary>
	/// <param name="field">The index of the field to retrieve.</param>
	/// <returns>The DateTime value of the specified field.</returns>
	DateTime GetDateTime(int field);
}

/// <summary>
/// Generic methods relating to creating CsvReader instances.
/// </summary>
public static class CsvReader
{
	/// <summary>
	/// Creates a <see cref="CsvReaderUtf8"/> for the supplied <see cref="Stream"/>. Assumes the contents are UTF-8.
	/// </summary>
	/// <param name="stream">The stream containing the CSV data.</param>
	/// <param name="hasHeaders">Whether the supplied CSV file has headers; if true, they'll be loaded into the <see cref="ICsvReader.Headers"/> property.</param>
	/// <param name="lineBufferSize">The size of the buffer used to store lines of text as they get processed. Lines will be read incorrectly if they are larger than this buffer.</param>
	/// <param name="maxFieldCount">The maximum number of fields expected in a line.</param>
	/// <param name="separatorChar">The character that separates fields for the supplied CSV file.</param>
	public static CsvReaderUtf8 Create(Stream stream, bool hasHeaders = false, int lineBufferSize = 32 * 1024, int maxFieldCount = 256, char separatorChar = ',')
	{
		return new CsvReaderUtf8(stream, hasHeaders, lineBufferSize, maxFieldCount, separatorChar);
	}

	/// <summary>
	/// Creates a <see cref="CsvReaderUtf8"/> for the supplied <see cref="Stream"/>. Uses the specified encoding.
	/// </summary>
	/// <param name="stream">The stream containing the CSV data.</param>
	/// <param name="encoding">The encoding to use.</param>
	/// <param name="hasHeaders">Whether the supplied CSV file has headers; if true, they'll be loaded into the <see cref="ICsvReader.Headers"/> property.</param>
	/// <param name="lineBufferSize">The size of the buffer used to store lines of text as they get processed. Lines will be read incorrectly if they are larger than this buffer.</param>
	/// <param name="maxFieldCount">The maximum number of fields expected in a line.</param>
	/// <param name="separatorChar">The character that separates fields for the supplied CSV file.</param>
	public static ICsvReader Create(Stream stream, Encoding encoding, bool hasHeaders = false, int lineBufferSize = 32 * 1024, int maxFieldCount = 256, char separatorChar = ',')
	{
		if (encoding is UTF8Encoding)
			return new CsvReaderUtf8(stream, hasHeaders, lineBufferSize, maxFieldCount, separatorChar);

		return new CsvReaderUtf16(new StreamReader(stream, encoding), hasHeaders, lineBufferSize, maxFieldCount, separatorChar);
	}

	/// <summary>
	/// Creates a <see cref="CsvReaderUtf16"/> for the supplied <see cref="TextReader"/>.
	/// </summary>
	/// <param name="reader">The TextReader containing the CSV data.</param>
	/// <param name="hasHeaders">Whether the supplied CSV file has headers; if true, they'll be loaded into the <see cref="ICsvReader.Headers"/> property.</param>
	/// <param name="lineBufferSize">The size of the buffer used to store lines of text as they get processed. Lines will be read incorrectly if they are larger than this buffer.</param>
	/// <param name="maxFieldCount">The maximum number of fields expected in a line.</param>
	/// <param name="separatorChar">The character that separates fields for the supplied CSV file.</param>
	public static CsvReaderUtf16 Create(TextReader reader, bool hasHeaders = false, int lineBufferSize = 32 * 1024, int maxFieldCount = 256, char separatorChar = ',')
	{
		return new CsvReaderUtf16(reader, hasHeaders, lineBufferSize, maxFieldCount, separatorChar);
	}

	/// <summary>
	/// Creates a <see cref="CsvReaderUtf16"/> for the supplied <see cref="string"/>.
	/// </summary>
	/// <param name="text">The string containing the CSV data.</param>
	/// <param name="hasHeaders">Whether the supplied CSV file has headers; if true, they'll be loaded into the <see cref="ICsvReader.Headers"/> property.</param>
	/// <param name="lineBufferSize">The size of the buffer used to store lines of text as they get processed. Lines will be read incorrectly if they are larger than this buffer.</param>
	/// <param name="maxFieldCount">The maximum number of fields expected in a line.</param>
	/// <param name="separatorChar">The character that separates fields for the supplied CSV file.</param>
	public static CsvReaderUtf16 Create(string text, bool hasHeaders = false, int lineBufferSize = 32 * 1024, int maxFieldCount = 256, char separatorChar = ',')
	{
		return new CsvReaderUtf16(new MemoryTextReader(text.AsMemory()), hasHeaders, lineBufferSize, maxFieldCount, separatorChar);
	}

	/// <summary>
	/// Creates a <see cref="CsvReaderUtf16"/> for the supplied <see cref="ReadOnlyMemory{char}"/>.
	/// </summary>
	/// <param name="text">The string containing the CSV data.</param>
	/// <param name="hasHeaders">Whether the supplied CSV file has headers; if true, they'll be loaded into the <see cref="ICsvReader.Headers"/> property.</param>
	/// <param name="lineBufferSize">The size of the buffer used to store lines of text as they get processed. Lines will be read incorrectly if they are larger than this buffer.</param>
	/// <param name="maxFieldCount">The maximum number of fields expected in a line.</param>
	/// <param name="separatorChar">The character that separates fields for the supplied CSV file.</param>
	public static CsvReaderUtf16 Create(ReadOnlyMemory<char> text, bool hasHeaders = false, int lineBufferSize = 32 * 1024, int maxFieldCount = 256, char separatorChar = ',')
	{
		return new CsvReaderUtf16(new MemoryTextReader(text), hasHeaders, lineBufferSize, maxFieldCount, separatorChar);
	}
}

/// <summary>
/// Implements a <see cref="TextReader"/> over a <see cref="string"/> or <see cref="ReadOnlyMemory{char}"/>.
/// </summary>
/// <param name="buffer">The text data to wrap.</param>
public sealed class MemoryTextReader(ReadOnlyMemory<char> buffer) : TextReader
{
	private int position = 0;
	private readonly int length = buffer.Length;

	/// <inheritdoc/>
	public override int Peek()
	{
		if (position >= length)
			return -1;

		return buffer.Span[position];
	}

	/// <inheritdoc/>
	public override int Read()
	{
		if (position >= length)
			return -1;

		return buffer.Span[position++];
	}

	/// <inheritdoc/>
	public override int Read(char[] buffer, int index, int count)
		=> Read(buffer.AsSpan(index, count));

	/// <inheritdoc/>
	public override int Read(Span<char> destination)
	{
		int remaining = length - position;
		if (remaining <= 0)
			return 0;

		int toCopy = Math.Min(destination.Length, remaining);
		buffer.Span.Slice(position, toCopy).CopyTo(destination);
		position += toCopy;

		return toCopy;
	}
}