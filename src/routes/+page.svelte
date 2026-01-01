<script lang="ts">
    import { arraysEqual, ByteUnits, formatBytes, formatNs, TimeUnits } from "$lib/utils";
    import benchmarkData from "../benchmark-data.json";

    let selectedIndex: number = $state(0);

    type BenchmarkResult = {
        library: string;
        properties: string[];
        meanNs: number;
        errorNs: number;
        allocated: number;
    };

    type FilteredDataset = { [testName: string]: BenchmarkResult[] }

    let selectedDataset = $derived.by(() => {
        const entries = Object.entries(benchmarkData.datasets)[selectedIndex];
        return {
            name: entries[0],
            ...entries[1]
        }
    });

    let displayDataset: FilteredDataset = $derived.by(() => {
        const benchmarkTypes = Object.entries(benchmarkData.results);

        return Object.fromEntries(
            benchmarkTypes.map(x => [x[0], (<FilteredDataset>x[1])[selectedDataset.name]?.sort((a, b) => a.meanNs - b.meanNs)])
        );
    });

    export function getPropertyPermutations(results: BenchmarkResult[]): string[][] {
        const seen = new Set<string>();
        const permutations: string[][] = [];

        for (const result of results) {
            const key = result.properties.join("|");

            if (!seen.has(key)) {
                seen.add(key);
                permutations.push(result.properties);
            }
        }

        return permutations.sort((a, b) =>
            b.join("|").localeCompare(a.join("|"))
        );
    }
</script>

<div class="lg:flex h-dvh">
    <div class="flex flex-col lg:w-[48rem] p-4 overflow-y-auto bg-[#222]">
        <h2>.NET CSV benchmark</h2>

        <div class="pb-1">Test last run 01/01/2026</div>
        <div>
            Libraries compared:
            <ul>
                <li><a href="https://github.com/MarkPflug/Sylvan/blob/main/docs/Csv/Sylvan.Data.Csv.md">Sylvan.Data.Csv v1.4.3</a></li>
                <li><a href="https://github.com/nietras/Sep/">Sep v0.12.1</a></li>
                <li><a href="https://github.com/bbepis/FourLambda.Csv">FourLambda.Csv v1.0</a></li>
            </ul>
        </div>

        <div class="flex flex-col overflow-y-auto bg-[#181818]">
            {#each Object.entries(benchmarkData.datasets) as dataset, i}
                <div
                    class="py-2 px-2 user-select-none cursor-pointer {selectedIndex === i ? "bg-selected" : ""}"
                    onclick={() => selectedIndex = i}
                >
                    <b class="">{dataset[0]}</b>
                    <div class="flex justify-between"><span>{formatBytes(dataset[1].utf8size)} (UTF-8), {formatBytes(dataset[1].utf16size)} (UTF-16)</span><span>[{dataset[1].formatDesc}]</span></div>
                    <span class="subtext">{dataset[1].description}</span>
                </div>
            {/each}
        </div>
    </div>
    <div class="flex grow flex-col pt-12 pb-16 px-16 overflow-y-auto">
        {#snippet printDataset(results: BenchmarkResult[])}
            {#if !results}
                N/A
            {:else}
                {@const uniqueCategories = getPropertyPermutations(results)}
                {#each uniqueCategories as permutation, i}
                    {@const filteredResults = results.filter(x => arraysEqual(permutation, x.properties))}
                    {@const timeUnit = TimeUnits[Math.floor(Math.log(0.1 * filteredResults.map(x => x.meanNs).reduce((a, b) => Math.min(a, b))) / Math.log(1000))]}
                    {@const allocationUnit = ByteUnits[Math.max(1, Math.floor(Math.log(filteredResults.map(x => x.allocated).reduce((a, b) => Math.min(a, b))) / Math.log(1024)))]}
                    <b class="block my-2" class:mt-4={i > 0}>{permutation.join(" / ")}</b>
                    <table class="result-table">
                        <colgroup>
                            <col style="width: 3%;">
                            <col style="width: 17%;">
                            <col style="width: auto;">
                            <col style="width: 10%;">
                            <col style="width: 19%;">
                            <col style="width: 19%;">
                            <col style="width: 19%;">
                        </colgroup>
                        <thead>
                            <tr>
                                <th colspan="2">Library</th>
                            <th colspan="2">Speed</th>
                            <th>Mean</th>
                            <th>Error</th>
                            <th>Allocations</th>
                        </tr>
                    </thead>
                    <tbody>
                        {#each filteredResults as result, i}
                            {@const dataSize = permutation.includes("UTF16") ? selectedDataset.utf16size : selectedDataset.utf8size}
                            {@const speed = dataSize / (result.meanNs / 1_000_000_000)}
                            <tr>
                                <td class="border-r-transparent!">#{i + 1}</td>
                                <td>{result.library}</td>
                                <td class="border-r-transparent!">{formatBytes(speed, { unit: "MB", decimals: 2 })}/s</td>
                                <td class="text-right">±{formatBytes(speed * (result.errorNs / result.meanNs), { unit: "MB", decimals: 2 })}/s</td>
                                <td>{formatNs(result.meanNs, { decimals: 3, unit: timeUnit })}</td>
                                <td>{formatNs(result.errorNs, { decimals: 3, unit: timeUnit })}</td>
                                <td>{formatBytes(result.allocated, { decimals: 2, unit: allocationUnit })}</td>
                            </tr>
                        {/each}
                    </tbody>
                </table>
            {/each}
            {/if}
        {/snippet}
        <div class="w-full">
            <div>
                Benchmark environment:

                <pre>
BenchmarkDotNet v0.15.8, Windows 10 (10.0.19044.6575/21H2/November2021Update)
AMD Ryzen 7 9700X 3.80GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.101
  [Host] : .NET 10.0.1 (10.0.1, 10.0.125.57005), X64 RyuJIT x86-64-v4
                </pre>
            </div>
            <div class="mt-8">
                Tests come in two variants:
                <ul class="leading-5">
                    <li class="mb-2"><b>UTF-8</b>: Loaded binary CSV as UTF-8 data read from a Stream. Sylvan.Data.Csv does not directly support this, and is emulated by passing it through a StreamReader.</li>
                    <li><b>UTF-16</b>: Loaded CSV as UTF-16 data read from a TextReader.<br/>If you parse a CSV with the source as a string / Span&lt;char&gt;, you will be using this version.<br/>Note that while libraries using this seem to have faster raw MB/s speed, it's not necessarily processing more data faster; on average UTF-16 versions of UTF-8 data is double the physical byte size. Use Mean as an unbiased reference if you want to measure actual parse time</li>
                </ul>
            </div>
        </div>
        <hr/>
        <div class="w-full">
            <h2>Test suite #1 - Skip to end</h2>
            <div>
                Measures how fast a parser can read from start to finish, without extracting any values and only interpreting structure. Mimics workloads like:
                <ul>
                    <li>Counting the amount of lines and fields in a CSV</li>
                    <li>Validating column count</li>
                </ul>
            </div>
            {@render printDataset(displayDataset["skipToEnd"])}
        </div>
        <hr/>
        <div class="w-full">
            <h2>Test suite #2 - Column sum</h2>
            <div>
                Measures how fast a single column can be parsed as an int32 and compute a sum. Mimics workloads like:
                <ul>
                    <li>Calculating a sum / min / max across transactions</li>
                    <li>Determining the maximum ID of a dataset of records</li>
                </ul>
            </div>
            {@render printDataset(displayDataset["sum"])}
        </div>
        <hr/>
        <div class="w-full">
            <h2>Test suite #3 - Read all columns</h2>
            <div>
                Measures how fast a parser can get the text content of every column. Has 3 variants:
                <ul>
                    <li>"string": Reads the column value as a string object</li>
                    <li>"span": Reads the column value as a Span&lt;char&gt; object. Not available in the UTF-8 version of FourLambda.Csv</li>
                    <li>"span-raw": Reads the column value as a Span&lt;char&gt; / Span&lt;byte&gt; object, without performing any unescaping. Not available in Sylvan.Data.Csv</li>
                </ul>
            </div>
            {@render printDataset(displayDataset["readAll"])}
        </div>
        <hr/>
        <div class="w-full">
            <h2>Test suite #4 - Composite workload</h2>
            <div>
                Measures how fast a parser can extract strongly-typed values from rows, e.g. extracting a decimal from a decimal formatted column, and a DateTime from a datetime formatted column.
            </div>
            {#if displayDataset["composite"]}
                {@render printDataset(displayDataset["composite"])}
            {:else}
                <i class="block mt-8">Not available for this dataset</i>
            {/if}
        </div>
    </div>
</div>

<style lang="scss">
    .subtext {
        color: #999;
    }

    .result-table {
        width: 100%;
    }

    .result-table tbody tr:first-child {
        font-weight: bold;
        background-color: var(--selected-color);
    }
</style>