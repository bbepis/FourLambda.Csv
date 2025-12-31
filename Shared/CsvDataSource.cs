using System.Text;
using ZstdSharp;

public class CsvDataSource
{
	public string Filename { get; }

	public readonly bool HasHeader;
	public readonly int MaxFieldCount;
	public readonly int IntegerColumn;

	public byte[] Utf8Data;
	public ReadOnlyMemory<char> Utf16Data;

	//public CsvDataSource() { }

	public CsvDataSource(string filename, bool hasHeader, int maxFieldCount, int integerColumn)
	{
		Filename = filename;
		HasHeader = hasHeader;
		MaxFieldCount = maxFieldCount;
		IntegerColumn = integerColumn;
	}

	private static readonly string SampleDirectory =
		$"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}SampleData";

	public static string GetSampleFilename(string filename) => Path.Combine(SampleDirectory, filename);

	public void Load()
	{
		if (Utf8Data != null)
			return;

		using var fileStream = new FileStream(GetSampleFilename(Filename), FileMode.Open);
		using var decompressStream = new DecompressionStream(fileStream);
		using var memoryStream = new MemoryStream();

		decompressStream.CopyTo(memoryStream);

		Utf8Data = memoryStream.ToArray();
		Utf16Data = Encoding.UTF8.GetString(Utf8Data).AsMemory();
	}

	public static CsvDataSource[] BenchmarkSources =>
	[
		new ("reddit_subset.csv.zst", true, 24, 6),
		new ("mixed_unicode.csv.zst", true, 4, 0),
		new ("65K_Records_Data.csv.zst", true, 16, 8),
		new ("synthetic_thin_numeric.csv.zst", true, 4, 0),
		new ("synthetic_short_numeric.csv.zst", true, 4, 0),
		new ("synthetic_quoted_numeric.csv.zst", true, 4, 0),
		new ("synthetic_complex.csv.zst", true, 4, 0),
	];

	public static CsvDataSource[] TestSources =>
	[
		new ("compliance_no_cr.csv.zst", true, 4, 0),
		new ("compliance_no_end_crlf.csv.zst", true, 4, 0),
		new ("compliance_utf8_bom.csv.zst", true, 4, 0),
		new ("compliance_utf16_surrogate.csv.zst", true, 4, 0),
	];

	public static IEnumerable<CsvDataSource> BenchmarkSourcesLoaded()
	{
		foreach (var source in BenchmarkSources)
		{
			source.Load();
			yield return source;
		}
	}

	public override string ToString() => Filename;
}