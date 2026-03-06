using BenchmarkDotNet.Running;
using EdsDcfNet.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(ParserBenchmarks).Assembly).Run(args);
