# Migration guide

## Facade to format entry points

For new code, use the format-specific entry points on `CanOpenFile` instead of the
legacy static `Read*` / `Write*` / `EdsToDcf` methods. All legacy `Read*` / `Write*`
facade overloads are marked `[Obsolete]` (advisory), not only default-parameter
variants. They remain available until a future major release.

Each format uses the same method names on its entry point (`Eds`, `Dcf`, `Cpj`, `Xdd`,
`Xdc`). Replace the legacy facade prefix with the matching entry point:

| Legacy facade method | Canonical replacement |
|----------------------|------------------------|
| `ReadEds(...)`, `ReadDcf(...)`, … | `Eds.ReadFile(...)`, `Dcf.ReadFile(...)`, … |
| `ReadEdsFromString(...)`, … | `Eds.ReadString(...)`, `Dcf.ReadString(...)`, … |
| `ReadEds(stream, ...)`, … | `Eds.ReadStream(stream, ...)`, `Dcf.ReadStream(stream, ...)`, … |
| `ReadEdsAsync(path, ...)`, … | `Eds.ReadFileAsync(path, ...)`, `Dcf.ReadFileAsync(path, ...)`, … |
| `ReadEdsAsync(stream, ...)`, … | `Eds.ReadStreamAsync(stream, ...)`, … |
| `WriteEds(...)`, … | `Eds.WriteFile(...)`, `Dcf.WriteFile(...)`, … |
| `WriteEds(model, stream)`, … | `Eds.WriteStream(model, stream)`, … |
| `WriteEdsAsync(...)`, … | `Eds.WriteFileAsync(...)`, `Eds.WriteStreamAsync(...)`, … |
| `WriteEdsToString(...)`, … | `Eds.WriteToString(...)`, `Dcf.WriteToString(...)`, … |
| `EdsToDcf(...)` (no timestamp) | `Eds.ConvertToDcf(...)` — obsolete |
| `EdsToDcf(..., DateTime timestamp, ...)` | Prefer `Eds.ConvertToDcf(..., timestamp, ...)`; facade overload kept (not obsolete) |

These `CanOpenFile` static members are **not** marked `[Obsolete]`:

| Member | Notes |
|--------|-------|
| `Validate(...)` / `ValidateAsync(...)` | Unchanged validation entry points |
| `EnsureValid(...)` / `EnsureValidAsync(...)` | Throw-on-invalid helpers for EDS/DCF/CPJ |
| `EdsToDcf(..., DateTime timestamp, ...)` | Intentional retained shim; prefer `Eds.ConvertToDcf` for new code |
| Format entry points (`Eds`, `Dcf`, `Cpj`, `Xdd`, `Xdc`) | Canonical API |

## Input size limits

Pass `CanOpenFileOptions` instead of a bare `maxInputSize` parameter:

```csharp
// Before
var xdd = CanOpenFile.ReadXdd("device.xdd", maxInputSize: 50L * 1024 * 1024);

// After
var xdd = CanOpenFile.Xdd.ReadFile(
    "device.xdd",
    new CanOpenFileOptions { MaxInputSize = 50L * 1024 * 1024 });
```

## Pre-write validation

Use `CanOpenWriteOptions.Validated` on the format entry point write methods. See
[Read, write, and validation options](options.md#validated-writes).

## EDS-to-DCF conversion

```csharp
// Before
var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 2, baudrate: 500);

// After
var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 2, baudrate: 500);
```

For deterministic generated timestamps (recommended in tests and reproducible builds),
pass an explicit `DateTime` to `ConvertToDcf`:

```csharp
var dcf = CanOpenFile.Eds.ConvertToDcf(
    eds, nodeId: 2, timestamp: DateTime.UtcNow, baudrate: 500);
```

## Example migration

```csharp
// Before
var eds = CanOpenFile.ReadEds("device.eds");
var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 2, baudrate: 500);
CanOpenFile.WriteDcf(dcf, "device_node2.dcf");

// After
var eds = CanOpenFile.Eds.ReadFile("device.eds");
var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 2, baudrate: 500);
CanOpenFile.Dcf.WriteFile(dcf, "device_node2.dcf");
```

## Strong naming since 1.13.0

`EdsDcfNet.dll` is strong-named as of **1.13.0**. The key
(`src/EdsDcfNet/EdsDcfNet.snk`) is committed and public: it provides assembly identity
only, not authenticity. On .NET (Core) 5+ nothing changes. .NET Framework consumers that
referenced the previously unsigned assembly must **rebuild** against 1.13.0; no source
changes are required.
