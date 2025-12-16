# EdsDcfNet

A comprehensive, easy-to-use C# .NET library for CiA DS 306 - Electronic Data Sheet (EDS) and Device Configuration File (DCF) for CANopen devices.

## Features

✨ **Simple API** - Intuitive, fluent API style for quick integration
📖 **Read EDS** - Complete parsing of Electronic Data Sheets
📝 **Read & Write DCF** - Process and create Device Configuration Files
🔄 **EDS to DCF Conversion** - Easy conversion with configuration parameters
🎯 **Type-Safe** - Fully typed models for all CANopen objects
📦 **Modular** - Support for modular devices (bus couplers + modules)
✅ **CiA DS 306 v1.3 Compliant** - Implemented according to official specification

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

### Reading a DCF File

```csharp
using EdsDcfNet;

// Read DCF file
var dcf = CanOpenFile.ReadDcf("configured_device.dcf");

Console.WriteLine($"Node ID: {dcf.DeviceCommissioning.NodeId}");
Console.WriteLine($"Baudrate: {dcf.DeviceCommissioning.Baudrate} kbit/s");
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

### Working with Object Dictionary

```csharp
using EdsDcfNet.Extensions;

var dcf = CanOpenFile.ReadDcf("device.dcf");

// Get object
var deviceType = dcf.ObjectDictionary.GetObject(0x1000);

// Set value
dcf.ObjectDictionary.SetParameterValue(0x1000, "0x00000191");

// Browse PDO objects
var tpdos = dcf.ObjectDictionary.GetPdoCommunicationParameters(transmit: true);
```

## API Overview

### Main Class: `CanOpenFile`

```csharp
// Read EDS
ElectronicDataSheet ReadEds(string filePath)
ElectronicDataSheet ReadEdsFromString(string content)

// Read DCF
DeviceConfigurationFile ReadDcf(string filePath)
DeviceConfigurationFile ReadDcfFromString(string content)

// Write DCF
void WriteDcf(DeviceConfigurationFile dcf, string filePath)
string WriteDcfToString(DeviceConfigurationFile dcf)

// Convert EDS to DCF
DeviceConfigurationFile EdsToDcf(ElectronicDataSheet eds, byte nodeId,
                                  ushort baudrate = 250, string? nodeName = null)
```

## Supported Features

- ✅ Complete EDS parsing
- ✅ Complete DCF parsing and writing
- ✅ All Object Types (VAR, ARRAY, RECORD)
- ✅ Sub-objects and sub-indexes
- ✅ Compact Storage (CompactSubObj, CompactPDO)
- ✅ Object Links
- ✅ Modular device concept
- ✅ Hexadecimal, decimal, and octal numbers
- ✅ $NODEID formula evaluation (e.g., $NODEID+0x200)
- ✅ Comments and additional sections

## Examples

Complete examples can be found in the `examples/EdsDcfNet.Examples` project.

## Project Structure

```
eds-dcf-net/
├── src/
│   └── EdsDcfNet/              # Main library
│       ├── Models/             # Data models
│       ├── Parsers/            # EDS/DCF parsers
│       ├── Writers/            # DCF writer
│       ├── Utilities/          # Helper classes
│       ├── Exceptions/         # Custom exceptions
│       └── Extensions/         # Extension methods
├── examples/
│   └── EdsDcfNet.Examples/     # Example application
└── docs/
    └── cia/                    # CiA DS 306 specification
```

## Requirements

- .NET 10.0 or higher
- C# 12.0

## License

MIT License - see [LICENSE](LICENSE) file

## Specification

Based on **CiA DS 306 Version 1.3** (January 1, 2005)
"Electronic data sheet specification for CANopen"

## Support

For questions or issues:
- GitHub Issues: https://github.com/dborgards/eds-dcf-net/issues

---

**EdsDcfNet** - Professional CANopen EDS/DCF processing in C# .NET
