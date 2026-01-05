using System.Reflection;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Newtonsoft.Json.Linq;

namespace Benchmark;

public static class Program
{
	static void Main(string[] args)
	{
		var results = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new CsvConfig());

		JObject compiledData = new();

		compiledData["lastrun"] = DateTime.Now.ToString("dd/MM/yyyy");

		var datasetObject = compiledData["datasets"] = new JObject();

		foreach (var dataset in CsvDataSource.BenchmarkSources)
		{
			dataset.Load();

			datasetObject[dataset.Filename.Replace(".zst", "")] = new JObject
			{
				["utf8size"] = dataset.Utf8Data.Length,
				["utf16size"] = dataset.Utf16Data.Length * sizeof(char),
				["formatDesc"] = dataset.FormatDescription,
				["description"] = dataset.Description
			};
		}

		var resultObject = compiledData["results"] = new JObject();

		foreach (var result in results)
		{
			var testSuiteKey = result.Reports[0].BenchmarkCase.Descriptor.Type.GetCustomAttribute<BenchmarkKeyAttribute>()?.BenchmarkKey ?? throw new Exception("Unknown key");

			var keyedResults = (JObject)(resultObject[testSuiteKey] = new JObject());

			foreach (var report in result.Reports)
			{
				string datasetName = ((CsvDataSource)report.BenchmarkCase.Parameters[0].Value).Filename.Replace(".zst", "");

				if (keyedResults[datasetName] is not JArray datasetArray)
				{
					datasetArray = new JArray();
					keyedResults[datasetName] = datasetArray;
				}

				var libraryName = report.BenchmarkCase.Descriptor.WorkloadMethod.GetCustomAttribute<LibraryAttribute>()?.LibraryName ?? "<??>";
				
				var reportObject = new JObject
				{
					["library"] = libraryName,
					["properties"] = new JArray(report.BenchmarkCase.Descriptor.Categories),
					["meanNs"] = report.ResultStatistics.Mean,
					["errorNs"] = report.ResultStatistics.StandardError,
					["allocated"] = (ulong)report.Metrics["Allocated Memory"].Value
				};

				datasetArray.Add(reportObject);
			}
		}

		File.WriteAllText("compiled_resultset.json", compiledData.ToString(Newtonsoft.Json.Formatting.None));
	}

	class CsvConfig : ManualConfig
	{
		public CsvConfig()
		{
			AddJob(Job.InProcess
#if DEBUG
				.WithWarmupCount(0)
				.WithUnrollFactor(2)
				.WithInvocationCount(2)
				.WithIterationCount(1));
#else
				.WithWarmupCount(2)
				.WithMinIterationCount(1)
				.WithMaxIterationCount(20));
#endif

			AddLogger(ConsoleLogger.Default)
				.AddLogicalGroupRules(BenchmarkLogicalGroupRule.ByParams, BenchmarkLogicalGroupRule.ByCategory)
				.WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest));

			AddColumnProvider(CustomColumnProviders.Instance);
		}
	}

	public class CustomColumnProviders : IColumnProvider
	{
		public static readonly IColumnProvider Instance = new CustomColumnProviders();

		public IEnumerable<IColumn> GetColumns(Summary summary)
		{
			if (summary.BenchmarksCases.Select(b => b.Descriptor.Categories).Distinct().Count() > 1)
				yield return CategoriesColumn.Default;

			if (summary.BenchmarksCases.Select(b => b.Descriptor.Type.Namespace).Distinct().Count() > 1)
				yield return TargetMethodColumn.Namespace;

			if (summary.BenchmarksCases.Select(b => b.Descriptor.Type.Name).Distinct().Count() > 1)
				yield return TargetMethodColumn.Type;

			yield return TargetMethodColumn.Method;

			foreach (var provider in DefaultColumnProviders.Job.GetColumns(summary))
				yield return provider;

			yield return StatisticColumn.Mean;
			yield return StatisticColumn.Error;

			foreach (var provider in DefaultColumnProviders.Params.GetColumns(summary))
				yield return provider;

			foreach (var provider in DefaultColumnProviders.Metrics.GetColumns(summary))
				yield return provider;
		}
	}
}