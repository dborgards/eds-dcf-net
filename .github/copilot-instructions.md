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

## Commit Convention

This project uses **Conventional Commits** and **semantic-release** for automated versioning and NuGet publishing. All commit messages **must** follow this format:

```
<type>(<optional scope>): <description>
```

### Types and their effect on versioning

| Type | Release | Description |
|---|---|---|
| `feat` | **minor** | A new feature |
| `fix` | **patch** | A bug fix |
| `perf` | **patch** | A performance improvement |
| `revert` | **patch** | Reverts a previous commit |
| `docs` | none | Documentation only |
| `style` | none | Formatting, missing semicolons, etc. |
| `refactor` | none | Code change that neither fixes a bug nor adds a feature |
| `test` | none | Adding or correcting tests |
| `build` | none | Changes to the build system or dependencies |
| `ci` | none | Changes to CI configuration |
| `chore` | none | Other changes that don't modify src or test files |

### Breaking changes

For a **major** release, add `BREAKING CHANGE:` in the commit body or footer:

```
feat: redesign public API

BREAKING CHANGE: CanOpenFile.ReadEds now returns a Result type
```

### Examples

```
feat: add support for CompactPDO mapping
fix: correct hex parsing for negative values
docs: update README with new API examples
build: bump FluentAssertions to 7.x
ci: add codecov upload to build workflow
refactor(parser): simplify IniParser section lookup
test: add round-trip tests for modular devices
```

## Branching Strategy

This project uses a **develop → main** integration model:

| Branch | Purpose | Release |
|---|---|---|
| `main` | Stable, production-ready code | Stable NuGet release (e.g., `1.5.0`) |
| `develop` | Integration branch for ongoing work | Pre-release NuGet (e.g., `1.5.0-beta.1`) |
| `feat/*`, `fix/*`, etc. | Short-lived feature/fix branches | None |

### Workflow

1. Branch off `develop`:
   ```
   git checkout -b feat/my-feature develop
   ```
2. Open a PR from the feature branch **into `develop`** (never directly into `main`).
3. On merge to `develop`, semantic-release publishes a `beta` pre-release to NuGet automatically.
4. When ready for a stable release, open a PR from `develop` → `main`.
5. On merge to `main`, semantic-release publishes the stable release to NuGet.

### CI Behaviour

- **Push to a feature branch** → `build.yml` runs (build + test).
- **PR targeting `develop` or `main`** → `build.yml` runs (build + test as gate).
- **Merge to `develop`** → `semantic-release.yml` runs (build + test + beta pre-release).
- **Merge to `main`** → `semantic-release.yml` runs (build + test + stable release).

Direct commits to `main` or `develop` are not allowed; all changes go through PRs.

## Code Style

- XML doc comments on all public members
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- File-scoped namespaces (`namespace Foo;`)
- No external dependencies in the main library
