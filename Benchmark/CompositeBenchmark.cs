using System.Globalization;
using BenchmarkDotNet.Attributes;
using FourLambda.Csv;
using nietras.SeparatedValues;

namespace Benchmark;

[MemoryDiagnoser]
[BenchmarkKey("composite")]
public class CsvCompositeBenchmark
{
	public static IEnumerable<CsvDataSource> BenchmarkSourcesLoaded()
	{
		var dataSource = CsvDataSource.BenchmarkSources.First(x => x.Filename == "65K_Records_Data.csv.zst");
		dataSource.Load();

		yield return dataSource;
	}

	[ParamsSource(nameof(BenchmarkSourcesLoaded))]
	public CsvDataSource dataSource;

	protected MemoryStream Utf8Stream => new MemoryStream(dataSource.Utf8Data);
	protected TextReader Utf16Stream => new MemoryTextReader(dataSource.Utf16Data);

	protected readonly int BufferSize = 32 * 1024;

	[Benchmark, Library("FourLambda.Csv"), BenchmarkCategory("UTF8")]
	public void Composite_FourLambda_Utf8()
	{
		using var dr = new CsvReaderUtf8(Utf8Stream, dataSource.HasHeader, lineBufferSize: BufferSize, maxFieldCount: dataSource.MaxFieldCount);
		
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
		using var dr = new CsvReaderUtf16(Utf16Stream, dataSource.HasHeader, lineBufferSize: BufferSize, maxFieldCount: dataSource.MaxFieldCount);
		
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
			HasHeaders = dataSource.HasHeader,
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
			HasHeaders = dataSource.HasHeader,
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
			HasHeader = dataSource.HasHeader,
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
			HasHeader = dataSource.HasHeader,
			InitialBufferLength = BufferSize
		}).From(Utf16Stream);

		foreach (var record in dr)
		{
			record[7].Parse<DateTime>();	// Ship Date
			record[8].Parse<int>();			// Units Sold
			record[13].Parse<decimal>();	// Total Profit
		}
	}

	[Benchmark, Library("NReco"), BenchmarkCategory("UTF8")]
	public void Composite_NReco_Utf8()
	{
		var reader = new NReco.Csv.CsvReader(new StreamReader(Utf8Stream));

		if (dataSource.HasHeader)
			reader.Read();

		while (reader.Read())
		{
			DateTime.Parse(reader[7]);
			int.Parse(reader[8]);
			decimal.Parse(reader[13]);
		}
	}

	[Benchmark, Library("NReco"), BenchmarkCategory("UTF16")]
	public void Composite_NReco_Utf16()
	{
		var reader = new NReco.Csv.CsvReader(Utf16Stream);

		if (dataSource.HasHeader)
			reader.Read();

		while (reader.Read())
		{
			DateTime.Parse(reader[7]);
			int.Parse(reader[8]);
			decimal.Parse(reader[13]);
		}
	}

	[Benchmark, Library("CsvHelper"), BenchmarkCategory("UTF8")]
	public void Composite_CsvHelper_Utf8()
	{
		using var reader = new CsvHelper.CsvReader(new StreamReader(Utf8Stream), CultureInfo.InvariantCulture);

		if (dataSource.HasHeader)
			reader.Read();

		while (reader.Read())
		{
			reader.GetField<DateTime>(7);
			reader.GetField<int>(8);
			reader.GetField<decimal>(13);
		}
	}

	[Benchmark, Library("CsvHelper"), BenchmarkCategory("UTF16")]
	public void Composite_CsvHelper_Utf16()
	{
		using var reader = new CsvHelper.CsvReader(Utf16Stream, CultureInfo.InvariantCulture);

		if (dataSource.HasHeader)
			reader.Read();

		while (reader.Read())
		{
			reader.GetField<DateTime>(7);
			reader.GetField<int>(8);
			reader.GetField<decimal>(13);
		}
	}
}