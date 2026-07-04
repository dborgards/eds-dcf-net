# EdsDcfNet

[![Build Status](https://github.com/dborgards/eds-dcf-net/actions/workflows/build.yml/badge.svg)](https://github.com/dborgards/eds-dcf-net/actions/workflows/build.yml)
[![Semantic Release](https://img.shields.io/badge/semantic--release-conventionalcommits-e10079?logo=semantic-release)](https://github.com/semantic-release/semantic-release)
[![NuGet Version](https://img.shields.io/nuget/v/EdsDcfNet)](https://www.nuget.org/packages/EdsDcfNet)
[![NuGet Downloads](https://img.shields.io/nuget/dt/EdsDcfNet)](https://www.nuget.org/packages/EdsDcfNet)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![codecov](https://codecov.io/gh/dborgards/eds-dcf-net/branch/main/graph/badge.svg)](https://codecov.io/gh/dborgards/eds-dcf-net)

A comprehensive, easy-to-use C# .NET library for CANopen file formats:
CiA DS 306 (EDS, DCF, CPJ) and CiA 311 (XDD, XDC).

## Features

✨ **Simple API** - Intuitive, fluent API style for quick integration

📖 **Read & Write EDS** - Parse and generate Electronic Data Sheets

📝 **Read & Write DCF** - Process and create Device Configuration Files

🌐 **Read & Write CPJ** - Parse and create Nodelist Project files (CiA 306-3 network topologies)

🧩 **Read & Write XDD/XDC** - Parse and generate CiA 311 XML device descriptions/configurations

🔄 **EDS to DCF Conversion** - Easy conversion with configuration parameters

🎯 **Type-Safe** - Fully typed models for all CANopen objects

📦 **Modular** - Support for modular devices (bus couplers + modules)

✅ **CiA DS 306 v1.4 / CiA 311 v1.1 Compliant** - Implemented according to official specification

## Quick Start

### Reading an EDS File

```csharp
using EdsDcfNet;

// Read EDS file
var eds = CanOpenFile.Eds.ReadFile("device.eds");

// Display device information
Console.WriteLine($"Device: {eds.DeviceInfo.ProductName}");
Console.WriteLine($"Vendor: {eds.DeviceInfo.VendorName}");
Console.WriteLine($"Product Number: 0x{eds.DeviceInfo.ProductNumber:X}");
```

### Writing an EDS File

```csharp
using EdsDcfNet;

var eds = CanOpenFile.Eds.ReadFile("device.eds");
eds.FileInfo.FileRevision++;
CanOpenFile.Eds.WriteFile(eds, "device_updated.eds");
```

### Async File I/O (`async`/`await`)

```csharp
using EdsDcfNet;
using System.Threading;

using var cts = new CancellationTokenSource();

var eds = await CanOpenFile.Eds.ReadFileAsync("device.eds", cancellationToken: cts.Token);
eds.FileInfo.FileRevision++;
await CanOpenFile.Eds.WriteFileAsync(eds, "device_updated.eds", cancellationToken: cts.Token);
```

### Stream-based I/O

```csharp
using EdsDcfNet;
using System.IO;

using var stream = File.OpenRead("device.eds");
var eds = CanOpenFile.Eds.ReadStream(stream);

using var outStream = new MemoryStream();
CanOpenFile.Eds.WriteStream(eds, outStream);
```

> Stream ownership: stream overloads do **not** dispose input/output streams.  
> The caller remains responsible for stream lifetime.

## Canonical API (format entry points)

For new code, use the format-specific entry points on `CanOpenFile` instead of the
legacy static `Read*` / `Write*` overloads:

| Format | Entry point | Example |
|--------|-------------|---------|
| EDS | `CanOpenFile.Eds` | `CanOpenFile.Eds.ReadFile("device.eds")` |
| DCF | `CanOpenFile.Dcf` | `CanOpenFile.Dcf.WriteFile(dcf, "out.dcf")` |
| CPJ | `CanOpenFile.Cpj` | `CanOpenFile.Cpj.ReadFile("network.cpj")` |
| XDD | `CanOpenFile.Xdd` | `CanOpenFile.Xdd.ReadFile("device.xdd")` |
| XDC | `CanOpenFile.Xdc` | `CanOpenFile.Xdc.ReadFile("device.xdc")` |

These entry points accept `CanOpenFileOptions` (read limits) and `CanOpenWriteOptions`
(pre-write validation) in one place. Legacy facade overloads remain for backward
compatibility and delegate to the same operations; overloads that only supply default
parameters are marked `[Obsolete]` (advisory) and will be removed in a future major release.

EDS-to-DCF conversion lives on the EDS entry point: `CanOpenFile.Eds.ConvertToDcf(...)`.
The legacy `CanOpenFile.EdsToDcf(...)` methods delegate there.

```csharp
using EdsDcfNet;

var eds = CanOpenFile.Eds.ReadFile("device.eds");
var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 2, baudrate: 500);
CanOpenFile.Dcf.WriteFile(dcf, "device_node2.dcf", CanOpenWriteOptions.Validated);
```

## Migration Guide

If your code still calls the legacy `CanOpenFile.Read*` / `Write*` / `EdsToDcf` static
methods, move to the format entry points in the table above. Default-parameter facade
overloads are marked `[Obsolete]` (advisory) and delegate to the same implementation;
they remain available until a future major release.

### Facade → format entry point

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
| `EdsToDcf(...)` | `Eds.ConvertToDcf(...)` |

`CanOpenFile.Validate(...)` is unchanged.

### Input size limits

Pass `CanOpenFileOptions` instead of a bare `maxInputSize` parameter:

```csharp
// Before
var xdd = CanOpenFile.ReadXdd("device.xdd", maxInputSize: 50L * 1024 * 1024);

// After
var xdd = CanOpenFile.Xdd.ReadFile(
    "device.xdd",
    new CanOpenFileOptions { MaxInputSize = 50L * 1024 * 1024 });
```

### Pre-write validation

Use `CanOpenWriteOptions.Validated` on the format entry point write methods (see
[Validating models before write operations](#validating-models-before-write-operations)).

### EDS-to-DCF conversion

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

### Example migration

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

## Output Encoding Policy

All writer APIs that persist text (`CanOpenFile.Eds`, `.Dcf`, `.Cpj`, `.Xdd`, and `.Xdc`
write methods) write **UTF-8 without BOM** by default for file and stream output.

This is an intentional interoperability choice:
- CiA DS 306 is historically ASCII-oriented.
- UTF-8 keeps full ASCII compatibility for 7-bit content.
- UTF-8 also preserves non-ASCII characters in names/comments instead of replacing them.

### Guidance for strict ASCII toolchains

If a downstream tool only accepts strict ASCII, keep model text in 7-bit ASCII characters,
or transcode explicitly to strict ASCII at your boundary and fail fast on non-ASCII content.

```csharp
using EdsDcfNet;
using System.IO;
using System.Text;

var asciiStrict = Encoding.GetEncoding(
    "us-ascii",
    EncoderFallback.ExceptionFallback,
    DecoderFallback.ExceptionFallback);

var dcf = CanOpenFile.Dcf.ReadFile("device.dcf");
var text = CanOpenFile.Dcf.WriteToString(dcf);
File.WriteAllText("device_ascii.dcf", text, asciiStrict);
```

### Reading an XDD File (CiA 311 XML)

```csharp
using EdsDcfNet;

// Read XDD file
var xdd = CanOpenFile.Xdd.ReadFile("device.xdd");

Console.WriteLine($"Device: {xdd.DeviceInfo.ProductName}");
Console.WriteLine($"Vendor: {xdd.DeviceInfo.VendorName}");
```

### Reading a DCF File

```csharp
using EdsDcfNet;

// Read DCF file
var dcf = CanOpenFile.Dcf.ReadFile("configured_device.dcf");

Console.WriteLine($"Node ID: {dcf.DeviceCommissioning.NodeId}");
Console.WriteLine($"Baudrate: {dcf.DeviceCommissioning.Baudrate} kbit/s");
```

### Reading an XDC File (CiA 311 XML)

```csharp
using EdsDcfNet;

// Read XDC file
var xdc = CanOpenFile.Xdc.ReadFile("configured_device.xdc");

Console.WriteLine($"Node ID: {xdc.DeviceCommissioning.NodeId}");
Console.WriteLine($"Baudrate: {xdc.DeviceCommissioning.Baudrate} kbit/s");
```

### Working with ApplicationProcess (CiA 311 §6.4.5)

XDD/XDC files may include an `ApplicationProcess` element describing device parameters
at the application level. The typed model gives full programmatic access to all
sub-constructs.

```csharp
using EdsDcfNet;

var xdd = CanOpenFile.Xdd.ReadFile("device.xdd");

if (xdd.ApplicationProcess is { } ap)
{
    // Iterate parameters
    foreach (var param in ap.ParameterList)
    {
        var displayName = param.LabelGroup.GetDisplayName() ?? param.UniqueId;
        Console.WriteLine($"Parameter: {displayName}");
    }

    // Inspect data type definitions
    if (ap.DataTypeList is { } dtl)
    {
        foreach (var enumType in dtl.Enums)
            Console.WriteLine($"Enum type: {enumType.Name}");
    }
}
```

### Converting EDS to DCF

```csharp
using EdsDcfNet;

// Read EDS
var eds = CanOpenFile.Eds.ReadFile("device.eds");

// Convert to DCF with node ID and baudrate
var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 2, baudrate: 500, nodeName: "MyDevice");

// Save DCF
CanOpenFile.Dcf.WriteFile(dcf, "device_node2.dcf");
```

### Validating models before write operations

Use the validation API to detect invalid commissioning values and inconsistent
object-list definitions before serializing files.

```csharp
using EdsDcfNet;
using EdsDcfNet.Validation;

var dcf = CanOpenFile.Dcf.ReadFile("configured_device.dcf");

IReadOnlyList<ValidationIssue> issues = CanOpenFile.Validate(dcf);
if (issues.Count > 0)
{
    foreach (var issue in issues)
        Console.WriteLine(issue);
}
```

`CanOpenFile.Validate(...)` is the recommended entry point and routes to the
full model validator, returning path-based `ValidationIssue` entries.
Current checks include:

- commissioning constraints (Node-ID range `1..127` for commissioned nodes; `NodeId == 0` is accepted only when commissioning is omitted, baudrate range with `0` accepted for that omitted state, key string limits)
- device info constraints (name/order-code length, granularity limit)
- object dictionary consistency (list membership, duplicates, missing entries)
- object-level constraints (object type validity, parameter-name length, SubNumber mismatch)

The CiA 306 Node-ID range used by these checks is exposed publicly via
`CanOpenNodeId`, so consumers can validate or document node IDs without
duplicating the `1..127` literals:

```csharp
using EdsDcfNet;

bool valid = CanOpenNodeId.IsInRange(nodeId);          // true for 1..127
byte min = CanOpenNodeId.MinValue;                     // 1
byte max = CanOpenNodeId.MaxValue;                     // 127
string range = CanOpenNodeId.RangeDescription;         // "1..127"
```

To validate automatically before writing, pass `CanOpenWriteOptions.Validated` to the
format-specific entry points:

```csharp
using EdsDcfNet;

var dcf = CanOpenFile.Dcf.ReadFile("configured_device.dcf");

// Throws ModelValidationException when the model has validation issues.
CanOpenFile.Dcf.WriteFile(dcf, "updated.dcf", CanOpenWriteOptions.Validated);
```

The same option works on `CanOpenFile.Eds`, `.Cpj`, `.Xdd`, and `.Xdc` write methods.
Legacy `CanOpenFile.WriteDcf(...)` overloads delegate to these entry points.

### Working with Nodelist Projects (CPJ)

```csharp
using EdsDcfNet;
using EdsDcfNet.Models;

// Read a CPJ file describing the network topology
var cpj = CanOpenFile.Cpj.ReadFile("nodelist.cpj");

foreach (var network in cpj.Networks)
{
    Console.WriteLine($"Network: {network.NetName}");
    foreach (var node in network.Nodes.Values)
    {
        Console.WriteLine($"  Node {node.NodeId}: {node.Name} ({node.DcfFileName})");
    }
}

// Create a new CPJ
var project = new NodelistProject();
project.Networks.Add(new NetworkTopology
{
    NetName = "Production Line 1",
    Nodes =
    {
        [2] = new NetworkNode { NodeId = 2, Present = true, Name = "PLC", DcfFileName = "plc.dcf" },
        [3] = new NetworkNode { NodeId = 3, Present = true, Name = "IO Module", DcfFileName = "io.dcf" }
    }
});
CanOpenFile.Cpj.WriteFile(project, "network.cpj");
```

### Working with Object Dictionary

```csharp
using EdsDcfNet.Extensions;

var dcf = CanOpenFile.Dcf.ReadFile("device.dcf");

// Get object
var deviceType = dcf.ObjectDictionary.GetObject(0x1000);

// Set value (returns true if object exists, false if not found)
bool set = dcf.ObjectDictionary.SetParameterValue(0x1000, "0x00000191");

// Browse PDO objects
var tpdos = dcf.ObjectDictionary.GetPdoCommunicationParameters(transmit: true);
```

## API Overview

### Main Class: `CanOpenFile`

Writer encoding note: all file/stream write methods on the format entry points use UTF-8 without BOM.

Each format exposes read/write operations via a static property (`Eds`, `Dcf`, `Cpj`, `Xdd`, `Xdc`).
The shared surface on every format entry point includes:

```csharp
// Read (file, string, stream; sync and async)
TModel ReadFile(string filePath, CanOpenFileOptions? options = null)
Task<TModel> ReadFileAsync(string filePath, CanOpenFileOptions? options = null, CancellationToken cancellationToken = default)
TModel ReadString(string content, CanOpenFileOptions? options = null)
TModel ReadStream(Stream stream, CanOpenFileOptions? options = null)
Task<TModel> ReadStreamAsync(Stream stream, CanOpenFileOptions? options = null, CancellationToken cancellationToken = default)

// Write (file, stream, string; sync and async; optional CanOpenWriteOptions)
void WriteFile(TModel model, string filePath)
void WriteFile(TModel model, string filePath, CanOpenWriteOptions? options)
void WriteStream(TModel model, Stream stream)
void WriteStream(TModel model, Stream stream, CanOpenWriteOptions? options)
Task WriteFileAsync(TModel model, string filePath, CancellationToken cancellationToken = default)
Task WriteFileAsync(TModel model, string filePath, CanOpenWriteOptions? options, CancellationToken cancellationToken = default)
Task WriteStreamAsync(TModel model, Stream stream, CancellationToken cancellationToken = default)
Task WriteStreamAsync(TModel model, Stream stream, CanOpenWriteOptions? options, CancellationToken cancellationToken = default)
string WriteToString(TModel model)
```

Format-specific model types:

| Entry point | Read/write model |
|-------------|------------------|
| `CanOpenFile.Eds` | `ElectronicDataSheet` |
| `CanOpenFile.Dcf` | `DeviceConfigurationFile` |
| `CanOpenFile.Cpj` | `NodelistProject` |
| `CanOpenFile.Xdd` | `ElectronicDataSheet` |
| `CanOpenFile.Xdc` | `DeviceConfigurationFile` |

EDS-to-DCF conversion:

```csharp
DeviceConfigurationFile ConvertToDcf(ElectronicDataSheet eds, byte nodeId,
                                     ushort baudrate = 250, string? nodeName = null)
```

Model validation:

```csharp
IReadOnlyList<ValidationIssue> Validate(ElectronicDataSheet eds)
IReadOnlyList<ValidationIssue> Validate(DeviceConfigurationFile dcf)
```

Legacy static `Read*` / `Write*` / `EdsToDcf` facade methods remain for backward compatibility
and delegate to these entry points; default-parameter-only overloads are marked `[Obsolete]`.

### Input Size Limits and Tuning

All read APIs apply a safe default input-size limit of **10 MB**
(`IniParser.DefaultMaxInputSize`) to reduce denial-of-service risk from
unexpectedly large payloads.

You can override this limit per operation when you need to process larger files:

```csharp
var xdd = CanOpenFile.Xdd.ReadFile(
    "large-device.xdd",
    new CanOpenFileOptions { MaxInputSize = 50L * 1024 * 1024 });
```

Guidance:
- Keep the default whenever possible.
- Increase limits only for trusted sources and known use cases.
- Set the limit just high enough for your expected maximum file size.

## Supported Features

- ✅ Complete EDS parsing and writing
- ✅ Complete DCF parsing and writing
- ✅ CPJ nodelist project parsing and writing (CiA 306-3 network topologies)
- ✅ XDD parsing and writing (CiA 311 XML device description)
- ✅ XDC parsing and writing (CiA 311 XML device configuration)
- ✅ All Object Types (NULL, DOMAIN, DEFTYPE, DEFSTRUCT, VAR, ARRAY, RECORD)
- ✅ Sub-objects and sub-indexes
- ✅ Compact Storage (CompactSubObj, CompactPDO)
- ✅ Object Links
- ✅ Modular device concept
- ✅ Hexadecimal, decimal, and octal numbers
- ✅ $NODEID formula evaluation (e.g., $NODEID+0x200)
- ✅ CANopen Safety (EN 50325-5) - SRDOMapping, InvertedSRAD
- ✅ Comments and additional sections

## Error Handling

Writer APIs expose format-specific exceptions with context:

- `EdsWriter` / `CanOpenFile.Eds` write methods: `EdsWriteException`
- `DcfWriter` / `CanOpenFile.Dcf` write methods: `DcfWriteException`
- `CpjWriter` / `CanOpenFile.Cpj` write methods: `CpjWriteException`
- `XddWriter` / `CanOpenFile.Xdd` write methods: `XddWriteException`
- `XdcWriter` / `CanOpenFile.Xdc` write methods: `XdcWriteException`

When a failure can be attributed to a concrete generated section/element,
the exception contains a `SectionName` value (for example `DeviceInfo`,
`Topology`, `DeviceProfile`, or `deviceCommissioning`).

## Examples

Complete examples can be found in the `examples/EdsDcfNet.Examples` project.

## Performance Benchmarks

A dedicated BenchmarkDotNet project is available at:

- `benchmarks/EdsDcfNet.Benchmarks`

Run all benchmarks:

```bash
dotnet run -c Release -p benchmarks/EdsDcfNet.Benchmarks -- --filter "*"
```

Baseline scenario definitions and artifact locations are documented in:

- `benchmarks/EdsDcfNet.Benchmarks/BASELINE.md`

## Project Structure

```
eds-dcf-net/
├── src/
│   └── EdsDcfNet/              # Main library
│       ├── Models/             # Data models
│       ├── Parsers/            # EDS/DCF/CPJ/XDD/XDC parsers
│       ├── Writers/            # EDS/DCF/CPJ/XDD/XDC writers
│       ├── Utilities/          # Helper classes
│       ├── Exceptions/         # Custom exceptions
│       └── Extensions/         # Extension methods
├── benchmarks/
│   └── EdsDcfNet.Benchmarks/   # BenchmarkDotNet throughput/memory benchmarks
├── examples/
│   └── EdsDcfNet.Examples/     # Example application
└── docs/
    ├── architecture/           # ARC42 software architecture
    └── cia/                    # CiA DS 306 specification
```

## Requirements

**For consuming the NuGet package:**

- Any .NET implementation compatible with .NET Standard 2.0
  (e.g., .NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+, Unity, Xamarin)

**For building this repository (library, tests, examples):**

- .NET SDK 10.0 or higher
- C# 13.0 (as provided by the .NET 10 SDK)

## License

MIT License - see [LICENSE](LICENSE) file

## Specification

Based on:
- **CiA DS 306 Version 1.4.0** (December 15, 2021)
- **CiA 311** XML device description/configuration concepts (XDD/XDC)

## Support

For questions or issues:
- GitHub Issues: https://github.com/dborgards/eds-dcf-net/issues

---

**EdsDcfNet** - Professional CANopen EDS/DCF/CPJ/XDD/XDC processing in C# .NET
