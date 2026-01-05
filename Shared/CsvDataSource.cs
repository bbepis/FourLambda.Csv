using System.Text;
using ZstdSharp;

public class CsvDataSource
{
	public string Filename { get; }

	public readonly bool HasHeader;
	public readonly int MaxFieldCount;
	public readonly int IntegerColumn;
	public readonly string? FormatDescription;
	public readonly string? Description;

	public byte[] Utf8Data;
	public ReadOnlyMemory<char> Utf16Data;

	//public CsvDataSource() { }

	public CsvDataSource(string filename, bool hasHeader, int maxFieldCount, int integerColumn, string formatDescription = null, string description = null)
	{
		Filename = filename;
		HasHeader = hasHeader;
		MaxFieldCount = maxFieldCount;
		IntegerColumn = integerColumn;
		FormatDescription = formatDescription;
		Description = description;
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

		var byteOrderMark = Utf16Data.Span[0];

		if (byteOrderMark == '\uFEFF' || byteOrderMark == '\uFFFE')
		{
			// Although we explicitly handle UTF-8 BOM, I don't think we should test for UTF-16 BOM.
			// - StreamReader automatically strips it if it encounters it
			// - Sylvan.Data.Csv passes it through as-is and doesn't throw exceptions on it

			Utf16Data = Utf16Data.Slice(1);
		}
	}

	public static CsvDataSource[] BenchmarkSources =>
	[
		new ("reddit_subset.csv.zst", true, 24, 6, "22 columns, 16,781 rows", "'RC_2008-01.ndjson' from the Arctic Shift reddit dump converted to CSV format. Meant to represent real-world data processing, and contains a very diverse range of long, complex text."),
		new ("mixed_unicode.csv.zst", true, 4, 0, "4 columns, 52,524 rows", "'item_flavor_text.csv' from the Veekun/pokedex GitHub repo. Contains medium-length text in many different languages & scripts, with many newlines."),
		new ("65K_Records_Data.csv.zst", true, 16, 8, "14 columns, 65,535 rows", "Example CSV dataset used by MarkPflug/Benchmarks to benchmark .NET CSV libraries. Contains sample financial record data."),
		new ("synthetic_thin_numeric.csv.zst", true, 4, 0, "4 columns, 50,000 rows", "An artificially generated dataset consisting of 4 columns containing integers ranging from 0 - 2000."),
		new ("synthetic_short_numeric.csv.zst", true, 4, 0, "4 columns, 50 rows", "An artificially generated dataset consisting of 4 columns containing integers ranging from 0 - 2000. Short to show overall library overhead."),
		new ("synthetic_quoted_numeric.csv.zst", true, 4, 0, "4 columns, 50,000 rows", "An artificially generated dataset consisting of 4 columns containing integers ranging from 0 - 2000, with each column wrapped in quotation marks for escaping."),
		new ("synthetic_complex.csv.zst", true, 4, 0, "4 columns, 50,000 rows", "An artificially generated dataset consisting of 1 column with a number, and 3 containing very large escaped text designed to be extremely challenging for a parser to scan through."),
	];

	public static CsvDataSource[] TestSources =>
	[
		new ("compliance_no_cr.csv.zst", true, 4, 0),
		new ("compliance_no_end_crlf.csv.zst", true, 4, 0),
		new ("compliance_no_headers.csv.zst", false, 4, 0),
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