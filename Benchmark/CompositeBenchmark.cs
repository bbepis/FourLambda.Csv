using BenchmarkDotNet.Attributes;
using FourLambda.Csv;
using nietras.SeparatedValues;

namespace Benchmark;

[MemoryDiagnoser]
[BenchmarkKey("composite")]
public class CsvCompositeBenchmark
{
	private readonly CsvDataSource DataSource;

	public CsvCompositeBenchmark()
	{
		DataSource = CsvDataSource.BenchmarkSources.First(x => x.Filename == "65K_Records_Data.csv.zst");
		DataSource.Load();
	}

	protected MemoryStream Utf8Stream => new MemoryStream(DataSource.Utf8Data);
	protected TextReader Utf16Stream => new MemoryTextReader(DataSource.Utf16Data);

	protected readonly int BufferSize = 32 * 1024;

	[Benchmark, Library("FourLambda.Csv"), BenchmarkCategory("UTF8")]
	public void Composite_FourLambda_Utf8()
	{
		using var dr = new CsvReaderUtf8(Utf8Stream, lineBufferSize: BufferSize, maxFieldCount: DataSource.MaxFieldCount);

		if (DataSource.HasHeader)
			dr.ReadNext(); // strip the header row

		while (dr.ReadNext())
		{
			dr.GetDateTime(7);		// Ship Date
			dr.GetInt32(8);		// Units Sold
			dr.GetDecimal(13);		// Total Profit
		}
	}

	[Benchmark, Library("FourLambda.Csv"), BenchmarkCategory("UTF16")]
	public void Composite_FourLambda_Utf16()
	{
		using var dr = new CsvReaderUtf16(Utf16Stream, lineBufferSize: BufferSize, maxFieldCount: DataSource.MaxFieldCount);

		if (DataSource.HasHeader)
			dr.ReadNext(); // strip the header row

		while (dr.ReadNext())
		{
			dr.GetDateTime(7);		// Ship Date
			dr.GetInt32(8);		// Units Sold
			dr.GetDecimal(13);		// Total Profit
		}
	}

	[Benchmark, Library("Sylvan.Data.Csv"), BenchmarkCategory("UTF8")]
	public void Composite_Sylvan_Utf8()
	{
		using var tr = new StreamReader(Utf8Stream);
		using var dr = Sylvan.Data.Csv.CsvDataReader.Create(tr, new Sylvan.Data.Csv.CsvDataReaderOptions
		{
			HasHeaders = DataSource.HasHeader,
			BufferSize = BufferSize
		});

		while (dr.Read())
		{
			dr.GetDateTime(7);	// Ship Date
			dr.GetInt32(8);		// Units Sold
			dr.GetDecimal(13);	// Total Profit
		}
	}

	[Benchmark, Library("Sylvan.Data.Csv"), BenchmarkCategory("UTF16")]
	public void Composite_Sylvan_Utf16()
	{
		using var dr = Sylvan.Data.Csv.CsvDataReader.Create(Utf16Stream, new Sylvan.Data.Csv.CsvDataReaderOptions
		{
			HasHeaders = DataSource.HasHeader,
			BufferSize = BufferSize
		});

		while (dr.Read())
		{
			dr.GetDateTime(7);	// Ship Date
			dr.GetInt32(8);		// Units Sold
			dr.GetDecimal(13);	// Total Profit
		}
	}

	[Benchmark, Library("Sep"), BenchmarkCategory("UTF8")]
	public void Composite_Sep_Utf8()
	{
		using var dr = Sep.Reader(_ => new SepReaderOptions
		{
			DisableQuotesParsing = false,
			Unescape = true,
			HasHeader = DataSource.HasHeader,
			InitialBufferLength = BufferSize
		}).From(Utf8Stream);

		foreach (var record in dr)
		{
			record[7].Parse<DateTime>();    // Ship Date
			record[8].Parse<int>();			// Units Sold
			record[13].Parse<decimal>();    // Total Profit
		}
	}

	[Benchmark, Library("Sep"), BenchmarkCategory("UTF16")]
	public void Composite_Sep_Utf16()
	{
		using var dr = Sep.Reader(_ => new SepReaderOptions
		{
			DisableQuotesParsing = false,
			Unescape = true,
			HasHeader = DataSource.HasHeader,
			InitialBufferLength = BufferSize
		}).From(Utf16Stream);

		foreach (var record in dr)
		{
			record[7].Parse<DateTime>();	// Ship Date
			record[8].Parse<int>();			// Units Sold
			record[13].Parse<decimal>();	// Total Profit
		}
	}
}