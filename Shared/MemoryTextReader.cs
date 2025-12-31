public sealed class MemoryTextReader(ReadOnlyMemory<char> buffer) : TextReader
{
	private int position = 0;
	private readonly int length = buffer.Length;

	public override int Peek()
	{
		if (position >= length)
			return -1;

		return buffer.Span[position];
	}

	public override int Read()
	{
		if (position >= length)
			return -1;

		return buffer.Span[position++];
	}

	public override int Read(char[] buffer, int index, int count)
		=> Read(buffer.AsSpan(index, count));

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