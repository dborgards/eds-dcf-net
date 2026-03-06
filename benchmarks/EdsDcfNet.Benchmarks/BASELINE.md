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

## Baseline tracking table

Record each baseline capture with commit and environment metadata:

| Date (UTC) | Commit | OS / Runtime | ParserBenchmarks.EdsParseFromString | WriterBenchmarks.EdsWriteToString | RoundTripBenchmarks.EdsRoundTrip |
| --- | --- | --- | --- | --- | --- |
| 2026-03-06 | d74b4a6 | Linux (CI), .NET 10.0 | _pending first capture_ | _pending first capture_ | _pending first capture_ |

