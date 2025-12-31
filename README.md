# FourLambda.Csv

Fastest .NET CSV library in the universe. Extremely lean, simple to use, and above all else, high-performance.

Relies on AVX2 extensions for speed (95% of all CPUs currently used, all CPUs made after 2013), otherwise has a slower software fallback.

Install via `Install-Package FourLambda.Csv`

## Benchmarks

Under ideal workloads, it is faster than Sep by up to **3.5x**.

| Library         | Speed         | Mean         | Error    | Allocations | Library Size |
|-----------------|---------------|--------------|----------|-------------|--------------|
| FourLambda.Csv  | 6,837.67 MB/s | 804.582 µs   | 0.786 µs | 0.33 KB     | 13 KB        |
| Sep             | 2,095.79 MB/s | 2,625.006 µs | 7.538 µs | 4.74 KB     | 163 KB       |
| Sylvan.Data.Csv | 958.68 MB/s   | 5,738.593 µs | 9.306 µs | 70.35 KB    | 96 KB        |

View extended benchmarks for multiple workloads and conditions [here](https://bepis.io/csv-benchmark/).

## Usage

```cs
// If you have binary UTF-8 data via a Stream, use this class:
Stream Utf8Stream = ...;
using var reader = new CsvReaderUtf8(Utf8Stream);

// If you have UTF-16 data via a TextReader, use this class:
TextReader Utf16Stream = ...;
using var reader = new CsvReaderUtf16(Utf16Stream);

int sum = 0;

while (dr.ReadNext())
{
    sum += dr.GetInt32(0);
    var text = dr.GetString(1);
    // other columns
}
```

Notes:

- Ensure that the reader is correctly configured via the constructor for your dataset; `lineBufferSize` should be as large as the longest line in the CSV file, and `maxFieldCount` should be set to at least the largest number of fields a row could have.
- Headers are not handled by this library; if your CSV has a header, make sure to record the indices of the values of the first row, or skip it.