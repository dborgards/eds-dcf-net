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
var eds = CanOpenFile.ReadEds("device.eds");

// Display device information
Console.WriteLine($"Device: {eds.DeviceInfo.ProductName}");
Console.WriteLine($"Vendor: {eds.DeviceInfo.VendorName}");
Console.WriteLine($"Product Number: 0x{eds.DeviceInfo.ProductNumber:X}");
```

### Writing an EDS File

```csharp
using EdsDcfNet;

var eds = CanOpenFile.ReadEds("device.eds");
eds.FileInfo.FileRevision++;
CanOpenFile.WriteEds(eds, "device_updated.eds");
```

### Async File I/O (`async`/`await`)

```csharp
using EdsDcfNet;
using System.Threading;

using var cts = new CancellationTokenSource();

var eds = await CanOpenFile.ReadEdsAsync("device.eds", cts.Token);
eds.FileInfo.FileRevision++;
await CanOpenFile.WriteEdsAsync(eds, "device_updated.eds", cts.Token);
```

### Reading an XDD File (CiA 311 XML)

```csharp
using EdsDcfNet;

// Read XDD file
var xdd = CanOpenFile.ReadXdd("device.xdd");

Console.WriteLine($"Device: {xdd.DeviceInfo.ProductName}");
Console.WriteLine($"Vendor: {xdd.DeviceInfo.VendorName}");
```

### Reading a DCF File

```csharp
using EdsDcfNet;

// Read DCF file
var dcf = CanOpenFile.ReadDcf("configured_device.dcf");

Console.WriteLine($"Node ID: {dcf.DeviceCommissioning.NodeId}");
Console.WriteLine($"Baudrate: {dcf.DeviceCommissioning.Baudrate} kbit/s");
```

### Reading an XDC File (CiA 311 XML)

```csharp
using EdsDcfNet;

// Read XDC file
var xdc = CanOpenFile.ReadXdc("configured_device.xdc");

Console.WriteLine($"Node ID: {xdc.DeviceCommissioning.NodeId}");
Console.WriteLine($"Baudrate: {xdc.DeviceCommissioning.Baudrate} kbit/s");
```

### Working with ApplicationProcess (CiA 311 §6.4.5)

XDD/XDC files may include an `ApplicationProcess` element describing device parameters
at the application level. The typed model gives full programmatic access to all
sub-constructs.

```csharp
using EdsDcfNet;

var xdd = CanOpenFile.ReadXdd("device.xdd");

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
var eds = CanOpenFile.ReadEds("device.eds");

// Convert to DCF with node ID and baudrate
var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 2, baudrate: 500, nodeName: "MyDevice");

// Save DCF
CanOpenFile.WriteDcf(dcf, "device_node2.dcf");
```

### Working with Nodelist Projects (CPJ)

```csharp
using EdsDcfNet;
using EdsDcfNet.Models;

// Read a CPJ file describing the network topology
var cpj = CanOpenFile.ReadCpj("nodelist.cpj");

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
CanOpenFile.WriteCpj(project, "network.cpj");
```

### Working with Object Dictionary

```csharp
using EdsDcfNet.Extensions;

var dcf = CanOpenFile.ReadDcf("device.dcf");

// Get object
var deviceType = dcf.ObjectDictionary.GetObject(0x1000);

// Set value (returns true if object exists, false if not found)
bool set = dcf.ObjectDictionary.SetParameterValue(0x1000, "0x00000191");

// Browse PDO objects
var tpdos = dcf.ObjectDictionary.GetPdoCommunicationParameters(transmit: true);
```

## API Overview

### Main Class: `CanOpenFile`

```csharp
// Read EDS
ElectronicDataSheet ReadEds(string filePath)
Task<ElectronicDataSheet> ReadEdsAsync(string filePath, CancellationToken cancellationToken = default)
ElectronicDataSheet ReadEdsFromString(string content)

// Write EDS
void WriteEds(ElectronicDataSheet eds, string filePath)
Task WriteEdsAsync(ElectronicDataSheet eds, string filePath, CancellationToken cancellationToken = default)
string WriteEdsToString(ElectronicDataSheet eds)

// Read DCF
DeviceConfigurationFile ReadDcf(string filePath)
Task<DeviceConfigurationFile> ReadDcfAsync(string filePath, CancellationToken cancellationToken = default)
DeviceConfigurationFile ReadDcfFromString(string content)

// Write DCF
void WriteDcf(DeviceConfigurationFile dcf, string filePath)
Task WriteDcfAsync(DeviceConfigurationFile dcf, string filePath, CancellationToken cancellationToken = default)
string WriteDcfToString(DeviceConfigurationFile dcf)

// Read CPJ (CiA 306-3 Nodelist Project)
NodelistProject ReadCpj(string filePath)
Task<NodelistProject> ReadCpjAsync(string filePath, CancellationToken cancellationToken = default)
NodelistProject ReadCpjFromString(string content)

// Write CPJ
void WriteCpj(NodelistProject cpj, string filePath)
Task WriteCpjAsync(NodelistProject cpj, string filePath, CancellationToken cancellationToken = default)
string WriteCpjToString(NodelistProject cpj)

// Read XDD (CiA 311 XML Device Description)
ElectronicDataSheet ReadXdd(string filePath)
Task<ElectronicDataSheet> ReadXddAsync(string filePath, CancellationToken cancellationToken = default)
ElectronicDataSheet ReadXddFromString(string content)

// Write XDD
void WriteXdd(ElectronicDataSheet xdd, string filePath)
Task WriteXddAsync(ElectronicDataSheet xdd, string filePath, CancellationToken cancellationToken = default)
string WriteXddToString(ElectronicDataSheet xdd)

// Read XDC (CiA 311 XML Device Configuration)
DeviceConfigurationFile ReadXdc(string filePath)
Task<DeviceConfigurationFile> ReadXdcAsync(string filePath, CancellationToken cancellationToken = default)
DeviceConfigurationFile ReadXdcFromString(string content)

// Write XDC
void WriteXdc(DeviceConfigurationFile xdc, string filePath)
Task WriteXdcAsync(DeviceConfigurationFile xdc, string filePath, CancellationToken cancellationToken = default)
string WriteXdcToString(DeviceConfigurationFile xdc)

// Convert EDS to DCF
DeviceConfigurationFile EdsToDcf(ElectronicDataSheet eds, byte nodeId,
                                  ushort baudrate = 250, string? nodeName = null)
```

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

## Examples

Complete examples can be found in the `examples/EdsDcfNet.Examples` project.

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
