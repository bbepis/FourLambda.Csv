using System.Text;
using ZstdSharp;

namespace Tests;

internal class GenerateSamples
{
	private static StreamWriter GetStreamWriter(string filename, Encoding? encoding = null)
	{
		var fileStream = new FileStream(CsvDataSource.GetSampleFilename(filename), FileMode.Create);
		var zstdStream = new CompressionStream(fileStream, 22, leaveOpen: false);
		return new StreamWriter(zstdStream, encoding ?? new UTF8Encoding(false));
	}

	[Test, Explicit]
	public void GenerateSyntheticThinNumeric()
	{
		using var streamWriter = GetStreamWriter("synthetic_thin_numeric.csv.zst");

		var random = new Random(3475984);

		streamWriter.Write($"column_1,column_2,column_3,column_4\r\n");

		for (int i = 0; i < 50_000; i++)
		{
			streamWriter.Write($"{random.Next(0, 2000)},{random.Next(0, 2000)},{random.Next(0, 2000)},{random.Next(0, 2000)}\r\n");
		}
	}

	[Test, Explicit]
	public void GenerateSyntheticShortNumeric()
	{
		using var streamWriter = GetStreamWriter("synthetic_short_numeric.csv.zst");

		var random = new Random(61872367);

		streamWriter.Write($"column_1,column_2,column_3,column_4\r\n");

		for (int i = 0; i < 50; i++)
		{
			streamWriter.Write($"{random.Next(0, 2000)},{random.Next(0, 2000)},{random.Next(0, 2000)},{random.Next(0, 2000)}\r\n");
		}
	}

	[Test, Explicit]
	public void GenerateSyntheticQuotedNumeric()
	{
		using var streamWriter = GetStreamWriter("synthetic_quoted_numeric.csv.zst");

		var random = new Random(7584634);

		streamWriter.Write($"column_1,column_2,column_3,column_4\r\n");

		for (int i = 0; i < 50_000; i++)
		{
			streamWriter.Write($"\"{random.Next(0, 2000)}\",\"{random.Next(0, 2000)}\",\"{random.Next(0, 2000)}\",\"{random.Next(0, 2000)}\"\r\n");
		}
	}

	[Test, Explicit]
	public void GenerateSyntheticComplex()
	{
		using var streamWriter = GetStreamWriter("synthetic_complex.csv.zst");

		var random = new Random(9304854);

		streamWriter.Write($"column_1,column_2,column_3,column_4\r\n");

		void WriteComplexString()
		{
			streamWriter.Write('\"');
			for (int i = 0; i < random.Next(1, 50); i++)
			{
				switch (random.Next(4))
				{
					case 0:
						for (int j = 0; j < random.Next(1, 20); j++)
							streamWriter.Write("Grzegorz Brzęczyszczykiewicz ");
						break;

					case 1:
						for (int j = 0; j < random.Next(1, 20); j++)
							streamWriter.Write("Spärde\r\n");
						break;

					case 2:
						for (int j = 0; j < random.Next(5, 20); j++)
							streamWriter.Write("\"\"");
						break;

					case 3:
						for (int j = 0; j < random.Next(10, 20); j++)
							streamWriter.Write(',');
						break;
				}
			}
			streamWriter.Write('\"');
		}

		for (int i = 0; i < 50_000; i++)
		{
			streamWriter.Write($"{random.Next(-500, 500)},");

			for (int j = 0; j < 3; j++)
			{
				WriteComplexString();
				streamWriter.Write(j == 2 ? "\r\n" : ",");
			}
		}
	}

	[Test, Explicit]
	public void GenerateCompliance()
	{
		var random = new Random(1298371);

		using (var streamWriter = GetStreamWriter("compliance_no_cr.csv.zst"))
		{
			streamWriter.Write("column_1,column_2,column_3,column_4\n");
			streamWriter.Write("1,2,3,4\n");
		}

		using (var streamWriter = GetStreamWriter("compliance_no_end_crlf.csv.zst"))
		{
			streamWriter.Write($"column_1,column_2,column_3,column_4\r\n");
			streamWriter.Write($"1,2,3,4");
		}

		using (var streamWriter = GetStreamWriter("compliance_no_headers.csv.zst"))
		{
			streamWriter.Write($"1,2,3,4\r\n");
			streamWriter.Write($"1,2,3,4\r\n");
			streamWriter.Write($"1,2,3,4\r\n");
			streamWriter.Write($"1,2,3,4\r\n");
			streamWriter.Write($"1,2,3,4\r\n");
			streamWriter.Write($"1,2,3,4\r\n");
		}

		using (var streamWriter = GetStreamWriter("compliance_utf8_bom.csv.zst", new UTF8Encoding(true)))
		{
			streamWriter.Write($"column_1,column_2,column_3,column_4\r\n");
			streamWriter.Write($"1,2,3,4");
		}

		using (var streamWriter = GetStreamWriter("compliance_utf16_surrogate.csv.zst"))
		{
			streamWriter.Write($"column_1,column_2,column_3,column_4\r\n");
			streamWriter.Write($"1,2,3,😎");
		}
	}
}
