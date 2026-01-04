using System.Globalization;
using BenchmarkDotNet.Attributes;
using FourLambda.Csv;
using nietras.SeparatedValues;

namespace Benchmark;

[BenchmarkKey("sum")]
public class CsvSumBenchmark : BenchmarkBase
{
	[Benchmark, Library("FourLambda.Csv"), BenchmarkCategory("UTF8")]
	public int Sum_FourLambda_Utf8()
	{
		using var dr = new CsvReaderUtf8(Utf8Stream, lineBufferSize: BufferSize, maxFieldCount: dataSource.MaxFieldCount);

		if (dataSource.HasHeader)
			dr.ReadNext(); // strip the header row

		int sum = 0;

		while (dr.ReadNext())
		{
			sum += dr.GetInt32(dataSource.IntegerColumn);
		}

		return sum;
	}

	[Benchmark, Library("FourLambda.Csv"), BenchmarkCategory("UTF16")]
	public int Sum_FourLambda_Utf16()
	{
		using var dr = new CsvReaderUtf16(Utf16Stream, lineBufferSize: BufferSize, maxFieldCount: dataSource.MaxFieldCount);

		if (dataSource.HasHeader)
			dr.ReadNext(); // strip the header row

		int sum = 0;

		while (dr.ReadNext())
		{
			sum += dr.GetInt32(dataSource.IntegerColumn);
		}

		return sum;
	}

	[Benchmark, Library("Sylvan.Data.Csv"), BenchmarkCategory("UTF8")]
	public int Sum_Sylvan_Utf8()
	{
		using var tr = new StreamReader(Utf8Stream);
		using var dr = Sylvan.Data.Csv.CsvDataReader.Create(tr, new Sylvan.Data.Csv.CsvDataReaderOptions
		{
			HasHeaders = dataSource.HasHeader,
			BufferSize = BufferSize
		});

		int sum = 0;

		while (dr.Read())
		{
			sum += dr.GetInt32(dataSource.IntegerColumn);
		}

		return sum;
	}

	[Benchmark, Library("Sylvan.Data.Csv"), BenchmarkCategory("UTF16")]
	public int Sum_Sylvan_Utf16()
	{
		using var dr = Sylvan.Data.Csv.CsvDataReader.Create(Utf16Stream, new Sylvan.Data.Csv.CsvDataReaderOptions
		{
			HasHeaders = dataSource.HasHeader,
			BufferSize = BufferSize
		});

		int sum = 0;

		while (dr.Read())
		{
			sum += dr.GetInt32(dataSource.IntegerColumn);
		}

		return sum;
	}

	[Benchmark, Library("Sep"), BenchmarkCategory("UTF8")]
	public int Sum_Sep_Utf8()
	{
		using var dr = Sep.Reader(_ => new SepReaderOptions
		{
			DisableQuotesParsing = false,
			Unescape = true,
			HasHeader = dataSource.HasHeader,
			InitialBufferLength = BufferSize
		}).From(Utf8Stream);

		int sum = 0;

		foreach (var record in dr)
		{
			sum += record[dataSource.IntegerColumn].Parse<int>();
		}

		return sum;
	}

	[Benchmark, Library("Sep"), BenchmarkCategory("UTF16")]
	public int Sum_Sep_Utf16()
	{
		using var dr = Sep.Reader(_ => new SepReaderOptions
		{
			DisableQuotesParsing = false,
			Unescape = true,
			HasHeader = dataSource.HasHeader,
			InitialBufferLength = BufferSize
		}).From(Utf16Stream);

		int sum = 0;

		foreach (var record in dr)
		{
			sum += record[dataSource.IntegerColumn].Parse<int>();
		}

		return sum;
	}

	[Benchmark, Library("NReco"), BenchmarkCategory("UTF8")]
	public int Sum_NReco_Utf8()
	{
		var reader = new NReco.Csv.CsvReader(new StreamReader(Utf8Stream));

		if (dataSource.HasHeader)
			reader.Read();

		int sum = 0;

		while (reader.Read())
		{
			sum += int.Parse(reader[dataSource.IntegerColumn]);
		}

		return sum;
	}

	[Benchmark, Library("NReco"), BenchmarkCategory("UTF16")]
	public int Sum_NReco_Utf16()
	{
		var reader = new NReco.Csv.CsvReader(Utf16Stream);

		if (dataSource.HasHeader)
			reader.Read();

		int sum = 0;

		while (reader.Read())
		{
			sum += int.Parse(reader[dataSource.IntegerColumn]);
		}

		return sum;
	}

	[Benchmark, Library("CsvHelper"), BenchmarkCategory("UTF8")]
	public int Sum_CsvHelper_Utf8()
	{
		using var reader = new CsvHelper.CsvReader(new StreamReader(Utf8Stream), CultureInfo.InvariantCulture);

		if (dataSource.HasHeader)
			reader.Read();

		int sum = 0;

		while (reader.Read())
		{
			sum += reader.GetField<int>(dataSource.IntegerColumn);
		}

		return sum;
	}

	[Benchmark, Library("CsvHelper"), BenchmarkCategory("UTF16")]
	public int Sum_CsvHelper_Utf16()
	{
		using var reader = new CsvHelper.CsvReader(Utf16Stream, CultureInfo.InvariantCulture);

		if (dataSource.HasHeader)
			reader.Read();

		int sum = 0;

		while (reader.Read())
		{
			sum += reader.GetField<int>(dataSource.IntegerColumn);
		}

		return sum;
	}
}