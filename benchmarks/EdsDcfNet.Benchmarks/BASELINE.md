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

After each baseline run, copy the `Mean` and `Allocated` values from the
BenchmarkDotNet markdown report into the table below.

| Date (UTC) | Commit | OS / Runtime | ParserBenchmarks.EdsParseFromString (Mean / Allocated) | WriterBenchmarks.EdsWriteToString (Mean / Allocated) | RoundTripBenchmarks.EdsRoundTrip (Mean / Allocated) |
| --- | --- | --- | --- | --- | --- |
| 2026-03-06 | d74b4a6 | Linux (CI), .NET 10.0 | _capture pending (see artifacts)_ | _capture pending (see artifacts)_ | _capture pending (see artifacts)_ |

The first completed row with concrete values is the reference baseline for
future regression comparisons.

