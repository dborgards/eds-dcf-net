# Performance baseline (Phase 3)

Run all benchmarks from a clean checkout at the repository root:

```bash
dotnet run --configuration Release --project benchmarks/EdsDcfNet.Benchmarks -- --filter "*"
```

Avoid nested benchmark project copies (for example, in agent worktrees below the
checkout): BenchmarkDotNet rejects duplicate project names.

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

| Date (UTC) | Commit | Hardware / OS / Runtime | ParserBenchmarks.EdsParseFromString (Mean / Allocated) | WriterBenchmarks.EdsWriteToString (Mean / Allocated) | RoundTripBenchmarks.EdsRoundTrip (Mean / Allocated) |
| --- | --- | --- | --- | --- | --- |
| 2026-10-24 | `a07615a` | Apple M5 / macOS 27.0.1 arm64 / .NET 10.0.13, SDK 10.0.402 | 114.655 μs / 371.38 KB | 17,715.9 ns / 62.37 KB | 136.443 μs / 433.82 KB |

The 2026-10-24 row is the initial reference baseline. It came from a complete
12-scenario BenchmarkDotNet 0.15.8 run using the repository job settings (one
launch, three warmups, five iterations). The OS did not permit BenchmarkDotNet
to raise process priority. Values are hardware- and runtime-specific reference
points, not portable pass/fail thresholds; compare repeated runs on equivalent
systems and investigate sustained trends rather than single-run noise.

