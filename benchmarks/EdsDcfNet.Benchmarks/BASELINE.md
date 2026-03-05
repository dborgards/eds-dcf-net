# Performance baseline (Phase 3)

Run benchmarks from repository root:

```bash
dotnet run -c Release -p benchmarks/EdsDcfNet.Benchmarks -- --filter "*"
```

BenchmarkDotNet writes reports to:

- `benchmarks/EdsDcfNet.Benchmarks/bin/Release/net10.0/BenchmarkDotNet.Artifacts/results/*.md`
- `benchmarks/EdsDcfNet.Benchmarks/bin/Release/net10.0/BenchmarkDotNet.Artifacts/results/*.csv`

## Baseline scenarios

The following scenarios are marked as `Baseline = true` in the benchmark classes:

- `ParserBenchmarks.EdsParseFromString`
- `WriterBenchmarks.EdsWriteToString`
- `RoundTripBenchmarks.EdsRoundTrip`

Core covered scenarios include EDS/DCF/CPJ parse, write, and round-trip operations.

