using FourLambda.Csv;
using Sylvan.Data.Csv;
using System;
using System.Buffers;

namespace Tests;

public class CompareTests
{
	private static List<CsvDataSource> LoadedTestCases;

	public static IEnumerable<TestCaseData> CsvDataSourceCases()
	{
		if (LoadedTestCases == null)
		{
			LoadedTestCases = new();

			foreach (var source in CsvDataSource.TestSources.Concat(CsvDataSource.BenchmarkSources))
			{
				source.Load();
				LoadedTestCases.Add(source);
			}
		}

		foreach (var source in LoadedTestCases)
		{
			yield return new TestCaseData(source);
		}
	}

	private void FullStringComparisonTestShared(ICsvReader lambdaReader, CsvDataSource dataSource)
	{
		// funnily enough, the easiest way to test is use another library to test against

		using var tr = new StreamReader(new MemoryStream(dataSource.Utf8Data));
		using var sylvanReader = Sylvan.Data.Csv.CsvDataReader.Create(tr, new Sylvan.Data.Csv.CsvDataReaderOptions
		{
			HasHeaders = dataSource.HasHeader
		});

		int rowCount = 0;

		if (dataSource.HasHeader)
		{
			var columnSchema = sylvanReader.GetColumnSchema();

			Assert.That(lambdaReader.Headers.Count, Is.EqualTo(columnSchema.Count));

			for (int i = 0; i < columnSchema.Count; i++)
			{
				Assert.That(lambdaReader.Headers.First(x => x.Value == i).Key, Is.EqualTo(columnSchema[i].ColumnName));
			}
		}

		while (sylvanReader.Read())
		{
			Assert.That(lambdaReader.ReadNext(), Is.True);
			Assert.That(lambdaReader.FieldCount, Is.EqualTo(sylvanReader.FieldCount));

			for (int i = 0; i < sylvanReader.FieldCount; i++)
			{
				var expected = sylvanReader.GetString(i);

				var actual = lambdaReader.GetString(i);
				Assert.That(actual, Is.EqualTo(expected));

				actual = lambdaReader.GetSpan(i).ToString();
				Assert.That(actual, Is.EqualTo(expected));

				lambdaReader.NeedsEscape(i, out var rawLength);

				using var charBuffer = MemoryPool<char>.Shared.Rent(rawLength);
				var written = lambdaReader.WriteToSpan(i, charBuffer.Memory.Span);
				actual = charBuffer.Memory.Span.Slice(0, written).ToString();
				Assert.That(actual, Is.EqualTo(expected));
			}

			// can't think of a better way to test this
			// make sure that spans don't clobber each other after generating each
			var span1 = lambdaReader.GetSpan(0);
			var span2 = lambdaReader.GetSpan(1);
			var span3 = lambdaReader.GetSpan(2);
			var span4 = lambdaReader.GetSpan(3);

			Assert.That(span1.ToString(), Is.EqualTo(sylvanReader.GetString(0)));
			Assert.That(span2.ToString(), Is.EqualTo(sylvanReader.GetString(1)));
			Assert.That(span3.ToString(), Is.EqualTo(sylvanReader.GetString(2)));
			Assert.That(span4.ToString(), Is.EqualTo(sylvanReader.GetString(3)));

			rowCount++;
		}

		Assert.That(lambdaReader.ReadNext(), Is.False);
		Console.WriteLine($"Row count: {rowCount:N0}");
	}

	[TestCaseSource(nameof(CsvDataSourceCases))]
	public void FullStringComparisonTestUtf8(CsvDataSource dataSource)
	{
		using var lambdaReader = new CsvReaderUtf8(new MemoryStream(dataSource.Utf8Data), dataSource.HasHeader);
		FullStringComparisonTestShared(lambdaReader, dataSource);
	}

	[TestCaseSource(nameof(CsvDataSourceCases))]
	public void FullStringComparisonTestUtf16(CsvDataSource dataSource)
	{
		using var lambdaReader = new CsvReaderUtf16(new MemoryTextReader(dataSource.Utf16Data), dataSource.HasHeader);
		FullStringComparisonTestShared(lambdaReader, dataSource);
	}
}