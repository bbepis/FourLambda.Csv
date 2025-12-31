using BenchmarkDotNet.Attributes;
using FourLambda.Csv;
using nietras.SeparatedValues;

namespace Benchmark;

[BenchmarkKey("readAll")]
public class CsvReadAllBenchmark : BenchmarkBase
{
	[Benchmark, Library("FourLambda.Csv"), BenchmarkCategory("string", "UTF8")]
	public string ReadAll_FourLambda_String_Utf8()
	{
		using var dr = new CsvReaderUtf8(Utf8Stream, lineBufferSize: BufferSize, maxFieldCount: dataSource.MaxFieldCount);

		if (dataSource.HasHeader)
			dr.ReadNext(); // strip the header row

		string str = string.Empty;

		while (dr.ReadNext())
		{
			for (int i = 0; i < dr.FieldCount; i++)
				str = dr.GetString(i);
		}

		return str;
	}

	[Benchmark, Library("FourLambda.Csv"), BenchmarkCategory("span-raw", "UTF8")]
	public void ReadAll_FourLambda_SpanRaw_Utf8()
	{
		using var dr = new CsvReaderUtf8(Utf8Stream, lineBufferSize: BufferSize, maxFieldCount: dataSource.MaxFieldCount);

		if (dataSource.HasHeader)
			dr.ReadNext(); // strip the header row

		while (dr.ReadNext())
		{
			for (int i = 0; i < dr.FieldCount; i++)
				dr.GetSpanRaw(i);
		}
	}

	[Benchmark, Library("FourLambda.Csv"), BenchmarkCategory("string", "UTF16")]
	public string ReadAll_FourLambda_String_Utf16()
	{
		using var dr = new CsvReaderUtf16(Utf16Stream, lineBufferSize: BufferSize, maxFieldCount: dataSource.MaxFieldCount);

		if (dataSource.HasHeader)
			dr.ReadNext(); // strip the header row

		string str = string.Empty;

		while (dr.ReadNext())
		{
			for (int i = 0; i < dr.FieldCount; i++)
				str = dr.GetString(i);
		}

		return str;
	}

	[Benchmark, Library("FourLambda.Csv"), BenchmarkCategory("span", "UTF16")]
	public void ReadAll_FourLambda_Span_Utf16()
	{
		using var dr = new CsvReaderUtf16(Utf16Stream, lineBufferSize: BufferSize, maxFieldCount: dataSource.MaxFieldCount);

		if (dataSource.HasHeader)
			dr.ReadNext(); // strip the header row

		while (dr.ReadNext())
		{
			for (int i = 0; i < dr.FieldCount; i++)
				dr.GetSpan(i);
		}
	}

	[Benchmark, Library("FourLambda.Csv"), BenchmarkCategory("span-raw", "UTF16")]
	public void ReadAll_FourLambda_SpanRaw_Utf16()
	{
		using var dr = new CsvReaderUtf16(Utf16Stream, lineBufferSize: BufferSize, maxFieldCount: dataSource.MaxFieldCount);

		if (dataSource.HasHeader)
			dr.ReadNext(); // strip the header row

		while (dr.ReadNext())
		{
			for (int i = 0; i < dr.FieldCount; i++)
				dr.GetRawSpan(i);
		}
	}

	[Benchmark, Library("Sylvan.Data.Csv"), BenchmarkCategory("string", "UTF8")]
	public string ReadAll_Sylvan_String_Utf8()
	{
		using var tr = new StreamReader(Utf8Stream);
		using var dr = Sylvan.Data.Csv.CsvDataReader.Create(tr, new Sylvan.Data.Csv.CsvDataReaderOptions
		{
			HasHeaders = dataSource.HasHeader,
			BufferSize = BufferSize
		});

		string str = string.Empty;

		while (dr.Read())
		{
			for (int i = 0; i < dr.FieldCount; i++)
				str = dr.GetString(i);
		}

		return str;
	}

	[Benchmark, Library("Sylvan.Data.Csv"), BenchmarkCategory("span", "UTF8")]
	public void ReadAll_Sylvan_Span_Utf8()
	{
		using var tr = new StreamReader(Utf8Stream);
		using var dr = Sylvan.Data.Csv.CsvDataReader.Create(tr, new Sylvan.Data.Csv.CsvDataReaderOptions
		{
			HasHeaders = dataSource.HasHeader,
			BufferSize = BufferSize
		});

		while (dr.Read())
		{
			for (int i = 0; i < dr.FieldCount; i++)
				dr.GetFieldSpan(i);
		}
	}

	[Benchmark, Library("Sylvan.Data.Csv"), BenchmarkCategory("string", "UTF16")]
	public string ReadAll_Sylvan_String_Utf16()
	{
		using var dr = Sylvan.Data.Csv.CsvDataReader.Create(Utf16Stream, new Sylvan.Data.Csv.CsvDataReaderOptions
		{
			HasHeaders = dataSource.HasHeader,
			BufferSize = BufferSize
		});

		string str = string.Empty;

		while (dr.Read())
		{
			for (int i = 0; i < dr.FieldCount; i++)
				str = dr.GetString(i);
		}

		return str;
	}

	[Benchmark, Library("Sylvan.Data.Csv"), BenchmarkCategory("span", "UTF16")]
	public void ReadAll_Sylvan_Span_Utf16()
	{
		using var dr = Sylvan.Data.Csv.CsvDataReader.Create(Utf16Stream, new Sylvan.Data.Csv.CsvDataReaderOptions
		{
			HasHeaders = dataSource.HasHeader,
			BufferSize = BufferSize
		});

		while (dr.Read())
		{
			for (int i = 0; i < dr.FieldCount; i++)
				dr.GetFieldSpan(i);
		}
	}

	[Benchmark, Library("Sep"), BenchmarkCategory("string", "UTF8")]
	public string ReadAll_Sep_String_Utf8()
	{
		using var dr = Sep.Reader(_ => new SepReaderOptions
		{
			DisableQuotesParsing = false,
			Unescape = true,
			HasHeader = dataSource.HasHeader,
			InitialBufferLength = BufferSize
		}).From(Utf8Stream);

		string str = string.Empty;

		foreach (var record in dr)
		{
			for (int i = 0; i < record.ColCount; i++)
				str = record[i].Parse<string>();
		}

		return str;
	}

	[Benchmark, Library("Sep"), BenchmarkCategory("span", "UTF8")]
	public void ReadAll_Sep_Span_Utf8()
	{
		using var dr = Sep.Reader(_ => new SepReaderOptions
		{
			DisableQuotesParsing = false,
			Unescape = true,
			HasHeader = dataSource.HasHeader,
			InitialBufferLength = BufferSize
		}).From(Utf8Stream);

		ReadOnlySpan<char> span;

		foreach (var record in dr)
		{
			for (int i = 0; i < record.ColCount; i++)
				span = record[i].Span;
		}
	}

	[Benchmark, Library("Sep"), BenchmarkCategory("span-raw", "UTF8")]
	public void ReadAll_Sep_SpanRaw_Utf8()
	{
		using var dr = Sep.Reader(_ => new SepReaderOptions
		{
			DisableQuotesParsing = false,
			Unescape = false,
			HasHeader = dataSource.HasHeader,
			InitialBufferLength = BufferSize
		}).From(Utf8Stream);

		ReadOnlySpan<char> span;

		foreach (var record in dr)
		{
			for (int i = 0; i < record.ColCount; i++)
				span = record[i].Span;
		}
	}

	[Benchmark, Library("Sep"), BenchmarkCategory("string", "UTF16")]
	public string ReadAll_Sep_String_Utf16()
	{
		using var dr = Sep.Reader(_ => new SepReaderOptions
		{
			DisableQuotesParsing = false,
			Unescape = true,
			HasHeader = dataSource.HasHeader,
			InitialBufferLength = BufferSize
		}).From(Utf16Stream);

		string str = string.Empty;

		foreach (var record in dr)
		{
			for (int i = 0; i < record.ColCount; i++)
				str = record[i].Parse<string>();
		}
		
		return str;
	}

	[Benchmark, Library("Sep"), BenchmarkCategory("span", "UTF16")]
	public void ReadAll_Sep_Span_Utf16()
	{
		using var dr = Sep.Reader(_ => new SepReaderOptions
		{
			DisableQuotesParsing = false,
			Unescape = true,
			HasHeader = dataSource.HasHeader,
			InitialBufferLength = BufferSize
		}).From(Utf16Stream);

		ReadOnlySpan<char> span;

		foreach (var record in dr)
		{
			for (int i = 0; i < record.ColCount; i++)
				span = record[i].Span;
		}
	}

	[Benchmark, Library("Sep"), BenchmarkCategory("span-raw", "UTF16")]
	public void ReadAll_Sep_SpanRaw_Utf16()
	{
		using var dr = Sep.Reader(_ => new SepReaderOptions
		{
			DisableQuotesParsing = false,
			Unescape = false,
			HasHeader = dataSource.HasHeader,
			InitialBufferLength = BufferSize
		}).From(Utf16Stream);

		ReadOnlySpan<char> span;

		foreach (var record in dr)
		{
			for (int i = 0; i < record.ColCount; i++)
				span = record[i].Span;
		}
	}

	[Benchmark, Library("NReco"), BenchmarkCategory("string", "UTF16")]
	public string ReadAll_NReco_String_Utf16()
	{
		var reader = new NReco.Csv.CsvReader(Utf16Stream);

		if (dataSource.HasHeader)
			reader.Read();

		string str = string.Empty;

		while (reader.Read())
		{
			for (int i = 0; i < reader.FieldsCount; i++)
				str = reader[i];
		}

		return str;
	}
}