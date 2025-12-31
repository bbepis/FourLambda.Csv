using BenchmarkDotNet.Attributes;
using FourLambda.Csv;
using nietras.SeparatedValues;

namespace Benchmark;

[BenchmarkKey("skipToEnd")]
public class CsvSkipToEndBenchmark : BenchmarkBase
{
	[Benchmark, Library("FourLambda.Csv"), BenchmarkCategory("UTF8")]
	public (int records, int fields) SkipToEnd_FourLambda_Utf8()
	{
		using var dr = new CsvReaderUtf8(Utf8Stream, lineBufferSize: BufferSize, maxFieldCount: dataSource.MaxFieldCount);

		if (dataSource.HasHeader)
			dr.ReadNext(); // strip the header row

		int records = 0, fields = 0;

		while (dr.ReadNext())
		{
			records++;
			fields += dr.FieldCount;
		}

		return (records, fields);
	}

	[Benchmark, Library("FourLambda.Csv"), BenchmarkCategory("UTF16")]
	public (int records, int fields) SkipToEnd_FourLambda_Utf16()
	{
		using var dr = new CsvReaderUtf16(Utf16Stream, lineBufferSize: BufferSize, maxFieldCount: dataSource.MaxFieldCount);

		if (dataSource.HasHeader)
			dr.ReadNext(); // strip the header row

		int records = 0, fields = 0;

		while (dr.ReadNext())
		{
			records++;
			fields += dr.FieldCount;
		}

		return (records, fields);
	}

	[Benchmark, Library("Sylvan.Data.Csv"), BenchmarkCategory("UTF8")]
	public (int records, int fields) SkipToEnd_Sylvan_Utf8()
	{
		using var tr = new StreamReader(Utf8Stream);
		using var dr = Sylvan.Data.Csv.CsvDataReader.Create(tr, new Sylvan.Data.Csv.CsvDataReaderOptions
		{
			HasHeaders = dataSource.HasHeader,
			BufferSize = BufferSize
		});

		int records = 0, fields = 0;

		while (dr.Read())
		{
			records++;
			fields += dr.FieldCount;
		}
		return (records, fields);
	}

	[Benchmark, Library("Sylvan.Data.Csv"), BenchmarkCategory("UTF16")]
	public (int records, int fields) SkipToEnd_Sylvan_Utf16()
	{
		using var dr = Sylvan.Data.Csv.CsvDataReader.Create(Utf16Stream, new Sylvan.Data.Csv.CsvDataReaderOptions
		{
			HasHeaders = dataSource.HasHeader,
			BufferSize = BufferSize
		});

		int records = 0, fields = 0;

		while (dr.Read())
		{
			records++;
			fields += dr.FieldCount;
		}
		return (records, fields);
	}

	[Benchmark, Library("Sep"), BenchmarkCategory("UTF8")]
	public (int records, int fields) SkipToEnd_Sep_Utf8()
	{
		using var dr = Sep.Reader(_ => new SepReaderOptions
		{
			DisableQuotesParsing = false,
			Unescape = true,
			HasHeader = dataSource.HasHeader,
			InitialBufferLength = BufferSize
		}).From(Utf8Stream);

		int records = 0, fields = 0;

		foreach (var record in dr)
		{
			records++;
			fields += record.ColCount;
		}

		return (records, fields);
	}

	[Benchmark, Library("Sep"), BenchmarkCategory("UTF16")]
	public (int records, int fields) SkipToEnd_Sep_Utf16()
	{
		using var dr = Sep.Reader(_ => new SepReaderOptions
		{
			DisableQuotesParsing = false,
			Unescape = true,
			HasHeader = dataSource.HasHeader,
			InitialBufferLength = BufferSize
		}).From(Utf16Stream);

		int records = 0, fields = 0;

		foreach (var record in dr)
		{
			records++;
			fields += record.ColCount;
		}

		return (records, fields);
	}

	[Benchmark, Library("NReco"), BenchmarkCategory("UTF16")]
	public (int records, int fields) SkipToEnd_NReco_Utf16()
	{
		var reader = new NReco.Csv.CsvReader(Utf16Stream);

		if (dataSource.HasHeader)
			reader.Read();

		int records = 0, fields = 0;

		while (reader.Read())
		{
			records++;
			fields += reader.FieldsCount;
		}

		return (records, fields);
	}
}