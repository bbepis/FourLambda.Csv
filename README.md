# FourLambda.Csv v1.1.0

Fastest .NET CSV library in the universe. Extremely lean, simple to use, and above all else, high-performance.

Relies on AVX2 extensions for speed (95% of all CPUs currently used, all CPUs made after 2013), otherwise has a slower software fallback.

Install via `Install-Package FourLambda.Csv` / https://www.nuget.org/packages/FourLambda.Csv

## Benchmarks

Under ideal workloads, it is faster than Sep by up to **3.5x**.

| Library         | Speed         | Mean         | Error     | Allocations | Library Size |
|-----------------|---------------|--------------|-----------|-------------|--------------|
| FourLambda.Csv  | 6,782.58 MB/s | 811.117 µs   | 0.636 µs  | 0.33 KB     | 16 KB        |
| Sep             | 2,154.41 MB/s | 2,553.586 µs | 3.747 µs  | 4.75 KB     | 163 KB       |
| NReco.Csv       | 1,334.07 MB/s | 4,123.819 µs | 3.106 µs  | 83.74 KB    | 11 KB        |
| Sylvan.Data.Csv | 1,025.93 MB/s | 5,362.444 µs | 9.404 µs  | 70.37 KB    | 96 KB        |
| CsvHelper       | 595.75 MB/s   | 9,234.509 µs | 21.846 µs | 24.23 KB    | 221 KB       |

View extended benchmarks for multiple workloads and conditions [here](https://bepis.io/csv-benchmark/).

## Usage

```cs
// Easiest way to create. Accepts Streams, TextReaders and raw strings.
using var reader = FourLambda.Csv.CsvReader.Create(data);

int sum = 0;

while (reader.ReadNext())
{
    sum += reader.GetInt32(0);
    var text = reader.GetString(1);

    // If the CSV file has headers and you've enabled them via the static method / constructor, you can access them like this:
    var totalSum = reader.GetString(reader.Headers["total_sum"]);

    // other columns
}
```

Notes:

- Ensure that the reader is correctly configured via the constructor for your dataset; `lineBufferSize` should be as large as the longest line in the CSV file, and `maxFieldCount` should be set to at least the largest number of fields a row could have. An exception will be thrown if either of these limits are exceeded.