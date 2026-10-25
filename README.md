# EdsDcfNet

[![Build Status](https://github.com/dborgards/eds-dcf-net/actions/workflows/build.yml/badge.svg)](https://github.com/dborgards/eds-dcf-net/actions/workflows/build.yml)
[![Semantic Release](https://img.shields.io/badge/semantic--release-conventionalcommits-e10079?logo=semantic-release)](https://github.com/semantic-release/semantic-release)
[![NuGet Version](https://img.shields.io/nuget/v/EdsDcfNet)](https://www.nuget.org/packages/EdsDcfNet)
[![NuGet Downloads](https://img.shields.io/nuget/dt/EdsDcfNet)](https://www.nuget.org/packages/EdsDcfNet)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![codecov](https://codecov.io/gh/dborgards/eds-dcf-net/branch/main/graph/badge.svg)](https://codecov.io/gh/dborgards/eds-dcf-net)

A comprehensive, easy-to-use C# .NET library for CANopen file formats:
CiA DS 306 (EDS, DCF, CPJ) and CiA 311 (XDD, XDC).

EdsDcfNet exposes the CANopen **Object Dictionary as a fully typed, editable API**.
Applications can inspect, query, create, and configure objects and sub-objects directly;
the library is not limited to file conversion or lossless round-trips.

## Features

✨ **Simple API** - Intuitive, fluent API style for quick integration

🗂️ **First-Class Object Dictionary API** - Query, create, and modify CANopen objects,
sub-objects, object lists, data types, access rights, defaults, and configured values

📖 **Read & Write EDS** - Parse and generate Electronic Data Sheets

📝 **Read & Write DCF** - Process and create Device Configuration Files

🌐 **Read & Write CPJ** - Parse and create Nodelist Project files (CiA 306-3 network topologies)

🧩 **Read & Write XDD/XDC** - Parse and generate CiA 311 XML device descriptions/configurations

🔄 **EDS to DCF Conversion** - Easy conversion with configuration parameters

🎯 **Type-Safe** - Fully typed models for all CANopen objects

📦 **Modular** - Support for modular devices (bus couplers + modules)

✅ **Documented CiA 306 / CiA 311 Subset** - EDS, DCF and CPJ follow CiA 306-1 v1.4.0 and CiA 306-3
(lenient or strict reader, writer, validator). XDD and XDC follow CiA 311 v1.1.0: output is checked in the
test suite against the CiA 311 XML schema, and, within the two profiles the library reads, XML elements
and attributes the model does not cover are preserved on round-trip. This is not a full-conformance claim.
The exact scope and the known limitations (additional profiles, comments and processing instructions
between modelled elements) are listed in
[arc42 section 8.4](docs/architecture/08-crosscutting-concepts.md#84-round-trip-fidelity).

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

### Working with the CANopen Object Dictionary

Every EDS, DCF, XDD, and XDC model exposes its parsed object dictionary through
`ObjectDictionary`. It is an application-facing model, not merely parser state: use it
to inspect device capabilities, find objects and sub-objects by index, evaluate default
or configured values, update a DCF configuration, or build an object dictionary in code.

```csharp
using EdsDcfNet;
using EdsDcfNet.Extensions;
using EdsDcfNet.Models;

var dcf = CanOpenFile.Dcf.ReadFile("configured_device.dcf");
var dictionary = dcf.ObjectDictionary;

// Query objects and sub-objects by their CANopen index.
var deviceType = dictionary.GetObject(0x1000);
var mappingEntry = dictionary.GetSubObject(0x1A00, 0x01);

Console.WriteLine(deviceType?.ParameterName);

// Read values as object or as a strongly typed value. The .NET type is derived
// automatically from DataType in the Object Dictionary.
object? rawDeviceType = dictionary.GetParameterValueAsObject(0x1000); // uint for UNSIGNED32
uint mapping = dictionary.GetParameterValue<uint>(0x1A00, 0x01);

// Pass a .NET value directly. It is validated against the OD data type and converted
// to the textual ParameterValue representation persisted by the writer.
if (!dictionary.SetParameterValue(0x1A00, 0x01, 0x60000108U))
    throw new InvalidOperationException("TPDO mapping entry 0x1A00:01 is missing.");

// Create a manufacturer-specific object programmatically.
// ObjectType/DataType take the CiA 301 constants instead of magic numbers.
dictionary.ManufacturerObjects.Add(0x2000);
dictionary.Objects[0x2000] = new CanOpenObject
{
    Index = 0x2000,
    ParameterName = "Application mode",
    ObjectType = CanOpenObjectType.Var,
    DataType = CanOpenDataType.Unsigned8,
    AccessType = AccessType.ReadWrite,
    DefaultValue = "0",
    ParameterValue = "1",
    PdoMapping = true
};

CanOpenFile.Dcf.WriteFile(dcf, "configured_device_updated.dcf");
```

The constants also carry metadata (bit length, signedness, display name) — see
[Data-type metadata](#data-type-metadata-canopendatatype) below.

The model distinguishes mandatory, optional, and manufacturer-specific object lists and
represents ARRAY and RECORD entries through typed `CanOpenSubObject` instances. Convenience
extensions also provide category queries and access to RPDO/TPDO communication and mapping
parameter ranges.

Typed value conversion covers BOOLEAN, signed and unsigned integers (including the CANopen
24/40/48/56-bit types), REAL32/REAL64, VISIBLE_STRING, UNICODE_STRING, and OCTET_STRING.
Numeric ranges are validated when setting values, and REAL32/REAL64 values must be finite:
`NaN` and values that overflow the target range (for example `double.MaxValue` on a REAL32
entry) are rejected instead of being written as `Infinity`. DOMAIN entries are excluded
because DCF files reference their payload through `UploadFile`/`DownloadFile` instead of an
inline value; use those properties directly. The original string overloads
remain available when an application needs exact control of the serialized representation.

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

### File replacement and atomicity

On `net10.0`, `WriteFile` and `WriteFileAsync` write to a temporary file beside the
final target before replacing it. Existing targets therefore remain in place until
the new content is complete. Symbolic links are followed and remain links.

The `netstandard2.0` asset has an intentional **non-atomic in-place fallback** for
Windows reparse points and for existing Unix files on hosts without managed
Unix-permission APIs. Content is serialized before truncation, but an I/O failure
during the subsequent copy can leave partial output. Async cancellation is observed
before truncation; once truncation has occurred, copying and flushing finish before
cancellation can surface again.

Maintainers can find the platform/target matrix and repeatable verification steps in
[the file-write contract](docs/file-write-contract.md).

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

These entry points accept `CanOpenFileOptions` (`MaxInputSize`, `StrictParsing`)
and `CanOpenWriteOptions` (pre-write validation) in one place. Legacy facade
`Read*` / `Write*` overloads (path, stream, string, sync, async, and options-taking
variants) remain for backward compatibility and are marked `[Obsolete]` (advisory);
they will be removed in a future major release.

EDS-to-DCF conversion lives on the EDS entry point: `CanOpenFile.Eds.ConvertToDcf(...)`.
The no-timestamp `CanOpenFile.EdsToDcf(...)` overload is obsolete and delegates
there; the `EdsToDcf(..., DateTime timestamp, ...)` overload is intentionally
**not** obsolete (thin retained shim for deterministic timestamps) but new code
should still call `Eds.ConvertToDcf`.

```csharp
using EdsDcfNet;

var eds = CanOpenFile.Eds.ReadFile("device.eds");
var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 2, baudrate: 500);
CanOpenFile.Dcf.WriteFile(dcf, "device_node2.dcf", CanOpenWriteOptions.Validated);
```

## Migration

The format entry points above replace the legacy static `Read*` / `Write*` / `EdsToDcf`
methods. The legacy facade remains available but is marked `[Obsolete]` and will be
removed in a future major release. See the [migration guide](docs/migration.md) for the
complete method mapping, input-option and conversion examples, and strong-naming notes.

## Write options

All formats write **UTF-8 without BOM** by default. `CanOpenWriteOptions.Encoding` selects
another byte encoding, and `NewLine` selects `"\n"` or `"\r\n"` instead of the platform
default:

```csharp
var options = new CanOpenWriteOptions { NewLine = "\n" };
CanOpenFile.Eds.WriteFile(eds, "device.eds", options);
CanOpenFile.Xdd.WriteFile(xdd, "device.xdd", options);
```

See [read, write, and validation options](docs/options.md) for encoding behavior,
validated writes, and all option details.

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

### Working with Device Profiles and Module Management

XDD/XDC device functions, device-manager information, and both device-side and
network-side module management are available as live typed models. Changes are
included the next time the document is written; unknown XML content is preserved.

```csharp
using EdsDcfNet.Models;

var xdd = CanOpenFile.Xdd.ReadFile("device.xdd");
var function = xdd.DeviceFunctions.Single();

Console.WriteLine($"Characteristic groups: {function.CharacteristicsLists.Count}");
Console.WriteLine($"Device module interfaces: {xdd.DeviceManager?.ModuleManagement?.Interfaces.Count}");
Console.WriteLine($"Network module interfaces: {xdd.NetworkModuleManagement?.Interfaces.Count}");
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

### Validation

Use `CanOpenFile.Validate(...)` to inspect path-based `ValidationIssue` entries, or
`CanOpenWriteOptions.Validated` to reject an invalid model before writing:

```csharp
using EdsDcfNet.Validation;

IReadOnlyList<ValidationIssue> issues = CanOpenFile.Validate(dcf);
CanOpenFile.Dcf.WriteFile(dcf, "updated.dcf", CanOpenWriteOptions.Validated);

// Opt in to stricter CiA 306 conformance checks explicitly.
var strictIssues = CanOpenFile.Validate(dcf, CanOpenValidationOptions.Strict);
```

Validated writes work for every format. Async validation and async validated writes
support cooperative cancellation. The [options guide](docs/options.md#validation-options)
lists all default and opt-in rules.

For line-aware EDS/DCF source validation, use `CanOpenIniValidator`. It reports malformed
structure, duplicates, non-canonical section names and raw value errors without stopping
at the first reader error. This is the same rule engine used by `edsdcf-check`:

```csharp
IReadOnlyList<IniValidationIssue> sourceIssues = CanOpenIniValidator.ValidateFile(
    "device.dcf",
    CanOpenIniFormat.Dcf,
    cancellationToken);

var memoryIssues = CanOpenIniValidator.ValidateString(
    iniText,
    CanOpenIniFormat.Eds,
    sourceName: "generated.eds");
```

Each issue contains its stable rule code, severity, source name, optional line, section,
key and raw value. `ToDisplayString()` produces a compiler-style diagnostic.

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
using EdsDcfNet;
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

Legacy static `Read*` / `Write*` facade methods remain for backward compatibility
and are all marked `[Obsolete]` (advisory). The no-timestamp `EdsToDcf` overload is
obsolete; `EdsToDcf(..., DateTime timestamp, ...)` is retained without `[Obsolete]`
as a thin shim (prefer `Eds.ConvertToDcf`). `Validate*` is unchanged.

### Read options

Reads are limited to **10 MiB** by default. `CanOpenFileOptions` lets trusted callers
raise `MaxInputSize` or enable `StrictParsing` to throw on malformed tokens instead of
coercing them:

```csharp
var options = new CanOpenFileOptions
{
    MaxInputSize = 50L * 1024 * 1024,
    StrictParsing = true
};
var xdd = CanOpenFile.Xdd.ReadFile("large-device.xdd", options);
```

See the [options guide](docs/options.md#read-options) for the complete strict-parsing
scope and guidance.

### Parse diagnostics (report repairs, keep the model)

Between lenient (silently coerce) and strict (throw on the first deviation)
there is a third path: the `Read*WithDiagnostics` methods on the format entry
points return the parsed model **and** report every repair as a
`ParseDiagnostic` — for import UIs and device-file validators that need to
answer "what did the parser silently fix?" without giving up the model.

```csharp
var result = CanOpenFile.Eds.ReadFileWithDiagnostics("device.eds");

foreach (var diagnostic in result.Diagnostics)
    Console.WriteLine(diagnostic);   // [Warning] INI_DUPLICATE_KEY at FileInfo.FileName:42: ...

var model = result.Model;            // fully parsed, coercions applied
```

Each `ParseDiagnostic` carries:

- `Code` — stable machine-readable identifier (`ParseDiagnosticCodes`, e.g.
  `INI_DUPLICATE_KEY`, `XDD_MISSING_INDEX`). In strict mode the same condition
  throws an `EdsParseException` whose `Code` property carries the same value.
- `Severity` (`Info` / `Warning` / `Error`), `Message`, `Path` (section/key for
  INI formats, XPath-like for XML formats), `Line` (INI formats), `RawValue`,
  and `CoercedTo` (what lenient mode substituted).

Diagnostics are collected through an `AsyncLocal` sink scoped to the call, so
concurrent reads do not interfere. Direct `*Reader` APIs stay lenient and
silent — collection happens only through the `CanOpenFile` entry points.

### Thread safety

- **Entry points are safe for concurrent use.** `CanOpenFile.Eds` / `.Dcf` /
  `.Cpj` / `.Xdd` / `.Xdc` and their `Read*` / `Write*` / `Validate` operations
  may be called from multiple threads or async flows simultaneously. The
  operation objects behind them are stateless singletons whose delegates
  construct a fresh reader/writer per call, and `StrictParsing` state is scoped
  per call via `AsyncLocal`, so concurrent calls with different options do not
  interfere. This contract is guarded by a concurrency test
  (`tests/EdsDcfNet.Tests/Integration/ThreadSafetyTests.cs`).
- **Models are not thread-safe.** `ElectronicDataSheet`, `DeviceConfigurationFile`,
  `NodelistProject`, `ObjectDictionary`, etc. are plain mutable objects. Do not
  mutate a model while it is being written, validated, or converted
  (`EdsToDcf` / `ConvertToDcf`); give each thread its own model instance.
- **Caller-owned streams and files are not synchronized.** The `ReadStream*` /
  `WriteStream*` overloads operate directly on the `Stream` you pass, and the
  file-based overloads contend on the external file system. Concurrent calls must
  each use their own stream and target distinct paths — sharing one stream races
  its position, and concurrent writes to the same path (or a read overlapping a
  write) can throw a sharing `IOException` or expose truncated content.
- **Options may be shared.** `CanOpenFileOptions` and `CanOpenWriteOptions` are
  immutable (`init`-only); a single instance can be reused across threads.

### Options extension pattern (format-specific options)

`CanOpenFileOptions` (read) and `CanOpenWriteOptions` (write) are intentionally
small, shared across all formats, and hold only cross-format concerns
(`MaxInputSize`, `StrictParsing`, and pre-write `ValidateBeforeWrite`).

When a genuinely **format-specific** option becomes necessary (for example XDD
XML formatting, INI section ordering, or CPJ network defaults), the agreed
extension pattern is **derived per-format option types**, not new properties on
the shared types:

```csharp
// Pattern (illustrative — implemented only when a concrete option exists):
public class XddWriteOptions : CanOpenWriteOptions
{
    public bool IndentXml { get; init; } = true;
}

CanOpenFile.Xdd.WriteFile(xdd, "device.xdd", new XddWriteOptions { IndentXml = false });
```

Rules for adding such an option:

- The shared base types stay limited to cross-format concerns; unrelated
  format-specific properties must not accumulate on them (IntelliSense on
  `CanOpenFile.Eds` should never show XDD-only options).
- The base types are unsealed on demand in the same PR that introduces the
  first derived type (unsealing is a non-breaking, additive change).
- The derived type flows through the existing `CanOpenWriteOptions?` /
  `CanOpenFileOptions?` parameters; the format-specific writer/reader checks
  for its own derived type. Existing signatures, overload shapes, and
  parameter names are untouched (see the Public API compatibility checklist
  in CONTRIBUTING.md).
- No format-specific option type is added before a concrete requirement
  exists.

### Data-type metadata (`CanOpenDataType`)

`CanOpenDataType` exposes CiA 301 (§7.4.7) data-type index constants and
lookup helpers for the raw `ushort` values stored in
`CanOpenObject.DataType` / `CanOpenSubObject.DataType`:

```csharp
using EdsDcfNet;
using EdsDcfNet.Extensions;

var eds = CanOpenFile.Eds.ReadFile("device.eds");
var dictionary = eds.ObjectDictionary;

ushort dataType = dictionary.GetObject(0x1000)?.DataType ?? 0; // Device Type: UNSIGNED32

CanOpenDataType.IsStandardType(dataType);  // true
CanOpenDataType.TryGetBitLength(dataType); // 32; null for variable-length types
CanOpenDataType.IsSigned(dataType);        // false
CanOpenDataType.IsUnsigned(dataType);      // true
CanOpenDataType.GetName(dataType);         // "UNSIGNED32", or null when unknown
```

`TryGetBitLength` is the single source of truth for fixed bit widths and backs
`CanOpenValueConverter`'s own conversion widths, so consumers get the same
answer the library uses internally. It returns `null` for variable-length
types (`VISIBLE_STRING`, `OCTET_STRING`, `UNICODE_STRING`, `DOMAIN`), reserved
codes, and manufacturer-specific or unknown types (0x0040 and above) — do not
assume a fixed width when the result is `null`.

## Supported Features

- ✅ First-class, editable CANopen Object Dictionary model for EDS, DCF, XDD, and XDC
- ✅ Indexed lookup, creation, and modification of objects and sub-objects
- ✅ Mandatory, optional, and manufacturer-specific object lists
- ✅ Default values and DCF/XDC configured parameter values
- ✅ Automatic conversion between OD data types and .NET values, with range validation
- ✅ CiA 301 data-type metadata lookup (bit length, signedness, display name via `CanOpenDataType`)
- ✅ Helpers for RPDO/TPDO communication and mapping parameters
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
- ✅ Hexadecimal (`0x`), decimal, and octal (`0`+digit, e.g. `010` → 8 — not padded decimal)
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
dotnet run --configuration Release --project benchmarks/EdsDcfNet.Benchmarks -- --filter "*"
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

The assembly is strong-named. .NET Framework consumers upgrading from the previously
unsigned assembly should see the [1.13.0 migration note](docs/migration.md#strong-naming-since-1130).

**For building this repository (library, tests, examples):**

- .NET SDK 10.0 or higher
- C# 14 (`LangVersion latest`, as provided by the .NET 10 SDK)

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
