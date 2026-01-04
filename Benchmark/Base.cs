using BenchmarkDotNet.Attributes;
using FourLambda.Csv;

namespace Benchmark;

public class LibraryAttribute(string libraryName) : Attribute
{
	public string LibraryName { get; } = libraryName;
}

public class BenchmarkKeyAttribute(string benchmarkKey) : Attribute
{
	public string BenchmarkKey { get; } = benchmarkKey;
}

[MemoryDiagnoser]
public abstract class BenchmarkBase
{
	[ParamsSource(typeof(CsvDataSource), nameof(CsvDataSource.BenchmarkSourcesLoaded))]
	public CsvDataSource dataSource;

	protected MemoryStream Utf8Stream => new MemoryStream(dataSource.Utf8Data);
	protected TextReader Utf16Stream => new MemoryTextReader(dataSource.Utf16Data);

	protected readonly int BufferSize = 32 * 1024;
}