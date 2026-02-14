# Copilot Instructions for EdsDcfNet

## Project Overview

EdsDcfNet is a C# library for reading and writing CiA DS 306 EDS (Electronic Data Sheet) and DCF (Device Configuration File) files for CANopen devices. Zero external dependencies. Dual-targets **netstandard2.0** and **net10.0**.

## Critical Constraints

### .NET Standard 2.0 Compatibility

This library must compile against netstandard2.0. The following APIs are **not available** and must be avoided:

- `string.Replace(string, string, StringComparison)` — use `IndexOf` + `Substring` for case-insensitive replacement
- `string.Contains(string, StringComparison)` — use `IndexOf(string, StringComparison) >= 0`
- `string.Contains(char)` — use `IndexOf(char) >= 0`
- `string.StartsWith(char)` / `string.EndsWith(char)` — use the `string` overload instead
- `[NotNullWhen]`, `[MemberNotNull]` attributes — not available without polyfill

### Invariant Culture

All numeric and date formatting/parsing **must** use `CultureInfo.InvariantCulture`. EDS/DCF files are culture-independent INI-style files. This applies to:

- `int.TryParse`, `uint.Parse`, `ushort.TryParse`, `byte.Parse` — always pass `CultureInfo.InvariantCulture`
- `.ToString()` on numeric types — always pass `CultureInfo.InvariantCulture`
- String interpolation with numbers in section headers (e.g., `[M{n}ModuleInfo]`) — use `string.Format(CultureInfo.InvariantCulture, ...)` instead of `$""` interpolation

## Architecture

```
EDS/DCF file → IniParser → EdsReader/DcfReader → Models → DcfWriter → DCF file
```

- **`CanOpenFile`** — static facade, the public API entry point (`ReadEds`, `ReadDcf`, `WriteDcf`, `EdsToDcf`)
- **`IniParser`** — low-level INI section/key-value parsing (case-insensitive)
- **`EdsReader`** / **`DcfReader`** — domain-specific parsers producing `ElectronicDataSheet` / `DeviceConfigurationFile`
- **`DcfWriter`** — serializes `DeviceConfigurationFile` back to DCF format
- **`ValueConverter`** — parses integers (decimal/hex `0x`/octal), booleans, `$NODEID` formulas, AccessType enum

### Key Models

- `ElectronicDataSheet` — EDS template (no configured node)
- `DeviceConfigurationFile` — DCF instance (includes `DeviceCommissioning` with nodeId/baudrate)
- `ObjectDictionary` — `CanOpenObject` entries indexed by `ushort`, each with optional `CanOpenSubObject` entries
- Unknown sections are preserved as `Dictionary<string, Dictionary<string, string>>` for round-trip fidelity

## Testing Conventions

- **Framework:** XUnit + FluentAssertions
- **Naming:** `MethodName_Scenario_ExpectedBehavior`
- **Pattern:** Arrange-Act-Assert (AAA)
- **Fixture data:** `tests/EdsDcfNet.Tests/Fixtures/sample_device.eds`

## Code Style

- XML doc comments on all public members
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- File-scoped namespaces (`namespace Foo;`)
- No external dependencies in the main library
