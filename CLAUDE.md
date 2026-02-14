# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

EdsDcfNet is a C# library for reading and writing CiA DS 306 EDS (Electronic Data Sheet) and DCF (Device Configuration File) files for CANopen devices. It has zero external dependencies and targets both **netstandard2.0** and **net10.0**.

## Build & Test Commands

```bash
# Build all projects
dotnet build

# Run all tests
dotnet test

# Run a single test by name
dotnet test --filter "FullyQualifiedName~ValueConverterTests.ParseInteger_HexadecimalValues_ParsesCorrectly"

# Run tests in a specific class
dotnet test --filter "FullyQualifiedName~DcfWriterTests"

# Run examples
dotnet run --project examples/EdsDcfNet.Examples
```

Test framework: XUnit + FluentAssertions. Tests follow the `MethodName_Scenario_ExpectedBehavior` naming convention with AAA pattern.

## Architecture

### Entry Point

`CanOpenFile` (static facade) provides the public API: `ReadEds`, `ReadDcf`, `WriteDcf`, `EdsToDcf`.

### Data Flow

```
EDS/DCF file → IniParser → EdsReader/DcfReader → Models → DcfWriter → DCF file
```

- **IniParser** handles low-level INI section/key-value parsing (case-insensitive sections)
- **EdsReader** parses EDS-specific sections into `ElectronicDataSheet`
- **DcfReader** extends EDS parsing with DCF-specific fields (DeviceCommissioning, ConnectedModules) into `DeviceConfigurationFile`
- **DcfWriter** serializes `DeviceConfigurationFile` back to DCF format
- **ValueConverter** handles integer parsing (decimal/hex/octal), `$NODEID` formula evaluation, boolean/AccessType conversion

### Key Models

- `ElectronicDataSheet` — EDS template (no configured node)
- `DeviceConfigurationFile` — DCF instance (has DeviceCommissioning with nodeId/baudrate)
- `ObjectDictionary` — contains `CanOpenObject` entries indexed by ushort, each with optional `CanOpenSubObject` entries
- Unknown/additional sections are preserved as `Dictionary<string, Dictionary<string, string>>`

## Critical Constraints

- **Dual-target netstandard2.0 + net10.0**: Do not use APIs unavailable in netstandard2.0. Key restrictions:
  - No `string.Replace(string, string, StringComparison)` — use `IndexOf` + `Substring`
  - No `string.Contains(string, StringComparison)` — use `IndexOf(...) >= 0`
  - No `string.Contains(char)` — use `IndexOf(char) >= 0`
  - `string.StartsWith(char)` / `EndsWith(char)` unavailable — use the string overload
- **Invariant culture**: All numeric/date formatting and parsing must use `CultureInfo.InvariantCulture`. EDS/DCF files are culture-independent.
- **String interpolation in section headers** (e.g., `[M{n}ModuleInfo]`): Use `string.Format(CultureInfo.InvariantCulture, ...)` instead of `$""` interpolation to avoid culture-dependent number formatting.
