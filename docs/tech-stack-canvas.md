# Tech Stack Canvas — EdsDcfNet

> Based on [Tech Stack Canvas](https://techstackcanvas.io) by Jörg Müller (INNOQ)

| | Context | Layers | | Support |
|---|---|---|---|---|
| **Business Goals** | **Frontend Technologies** | **API & Integrations** | **Security & Compliance** | |
| Provide a zero-dependency C# library for reading/writing CiA DS 306 and CiA 311 CANopen files | _Not applicable_ (library, no UI) | CANopen Object Dictionary (0x1000–0x5FFF) | MIT License | |
| Support broad .NET ecosystem (netstandard2.0 + net10.0) | | PDO / SRDO mapping | Nullable reference types enabled | |
| Enable round-trip fidelity for industrial automation device configuration | | `$NODEID` formula evaluation | Deterministic builds | |
| | | Modular device support (bus couplers + modules) | SourceLink for supply-chain transparency | |
| | | Compact storage modes (CompactPDO, CompactSubObj) | | |

---

## Context

### Business Goals

- Provide a comprehensive, **zero-dependency** C# library for reading and writing CiA DS 306 (EDS/DCF/CPJ) and CiA 311 (XDD/XDC) files
- Support the broadest possible .NET ecosystem via **netstandard2.0** (.NET Framework 4.6.1+, .NET Core 2.0+, Unity, Xamarin) and **net10.0**
- Enable **round-trip fidelity** — unknown/vendor-specific sections are preserved during read-modify-write cycles
- Distribute as a NuGet package with automated semantic versioning and publishing

### Sizing Numbers

- **Target platforms:** netstandard2.0 + net10.0 (covers .NET Framework 4.6.1 through .NET 10)
- **External dependencies:** 0 (core library)
- **Object Dictionary address space:** 0x1000–0x5FFF (CANopen standard)
- **Current version:** Branch-specific; see `src/EdsDcfNet/EdsDcfNet.csproj` (managed by semantic-release)
- **File formats:** 5 (`.eds`, `.dcf`, `.cpj`, `.xdd`, `.xdc`)

### Major Quality Attributes

- **Compatibility** — Must work on all .NET platforms via netstandard2.0; all string/number parsing uses `CultureInfo.InvariantCulture`
- **Correctness** — Correct parsing/writing of implemented CiA DS 306 and CiA 311 feature sets
- **Round-trip fidelity** — Unknown sections preserved in `AdditionalSections` dictionary
- **Simplicity** — Zero runtime dependencies, one static entry class (`CanOpenFile`) with per-format entry points (`CanOpenFile.Eds` / `Dcf` / `Cpj` / `Xdd` / `Xdc`)
- **Maintainability** — Comprehensive test suite, architecture documentation (ARC42), XML doc comments on all public members

---

## Core Technology Layers

### Frontend Technologies

_Not applicable_ — EdsDcfNet is a library without a user interface.

An **example console application** (`examples/EdsDcfNet.Examples/`) demonstrates API usage.

### Backend Technologies

| Category | Technology |
|---|---|
| **Language** | C# 14 (`LangVersion latest` in `Directory.Build.props`, SDK pinned to 10.0.x by `global.json`) |
| **Frameworks** | .NET Standard 2.0, .NET 10.0 |
| **Build system** | MSBuild via .NET SDK |
| **Architecture** | Facade (format entry points) → Parsers → Models → Writers |
| **Key patterns** | Facade (`CanOpenFile` + format entry points), static value conversion (`ValueConverter`), DTOs (domain models), Round-trip fidelity |
| **Nullable refs** | Enabled (`<Nullable>enable</Nullable>`) |
| **Implicit usings** | Enabled |
| **File-scoped namespaces** | Yes |

### Data Storage & Management

| Category | Technology |
|---|---|
| **File formats** | EDS / DCF / CPJ (INI-style) and XDD / XDC (XML, CiA 311) |
| **File encoding (I/O)** | UTF-8 text processing; file writes use UTF-8 without BOM |
| **In-memory model** | Strongly-typed C# objects (`ElectronicDataSheet`, `DeviceConfigurationFile`, `NodelistProject`, `ObjectDictionary`) |
| **Persistence** | File-based — read from and write to `.eds` / `.dcf` / `.cpj` / `.xdd` / `.xdc` files |
| **Unknown data** | Preserved in `Dictionary<string, Dictionary<string, string>> AdditionalSections` |

### API & Integrations

**Canonical API** (format entry points on the static class `CanOpenFile`; `TModel` is `ElectronicDataSheet` for `Eds`/`Xdd`, `DeviceConfigurationFile` for `Dcf`/`Xdc`, `NodelistProject` for `Cpj`):

```
CanOpenFile.Eds | .Dcf | .Cpj | .Xdd | .Xdc
  ReadFile(filePath, CanOpenFileOptions? = null) / ReadString(content, options) / ReadStream(stream, options) → TModel
  ReadFileAsync(...) / ReadStreamAsync(...) → Task<TModel>
  Read*WithDiagnostics(...) / Read*WithDiagnosticsAsync(...) → CanOpenReadResult<TModel>
  WriteFile(model, filePath[, CanOpenWriteOptions?]) / WriteStream(model, stream[, options])
  WriteFileAsync(...) / WriteStreamAsync(...) → Task
  WriteToString(model[, options]) → string
CanOpenFile.Eds.ConvertToDcf(eds, nodeId, baudrate, nodeName) → DeviceConfigurationFile
CanOpenFile.Validate(...) / ValidateAsync(...) / EnsureValid(...) / EnsureValidAsync(...)
```

The legacy static `Read*` / `Write*` / `EdsToDcf` methods directly on `CanOpenFile` (`ReadEds`, `WriteDcfToString`, ...) are marked
`[Obsolete]` and delegate to these entry points; see the README "Migration Guide". `EdsToDcf(..., DateTime timestamp, ...)` is a
retained non-obsolete shim, and `Validate*` / `EnsureValid*` live directly on `CanOpenFile`.

**CANopen protocol elements:** Object Dictionary, PDO/SRDO mapping, `$NODEID` formulas, modular devices, compact storage modes, network topologies (CiA 306-3).

### Security & Compliance

| Category | Detail |
|---|---|
| **License** | MIT (Copyright 2025 Dietmar Borgards) |
| **Deterministic builds** | Enabled (`<Deterministic>true</Deterministic>`) |
| **SourceLink** | Microsoft.SourceLink.GitHub for symbol debugging and supply-chain transparency |
| **CI build flag** | `<ContinuousIntegrationBuild>` enabled in CI |
| **Nullable analysis** | Compile-time null safety via `<Nullable>enable</Nullable>` |

---

## Support

### Testing & QA

| Category | Technology |
|---|---|
| **Test framework** | XUnit 2.9.3 |
| **Assertions** | AwesomeAssertions 9.6.0 |
| **Code coverage** | coverlet.collector 10.0.1 (XPlat Code Coverage, cobertura format) |
| **Coverage reporting** | Codecov |
| **Naming convention** | `MethodName_Scenario_ExpectedBehavior` |
| **Test pattern** | Arrange-Act-Assert (AAA) |
| **Fixture data** | `tests/EdsDcfNet.Tests/Fixtures/` (sample EDS/DCF/XDD/XDC/CPJ files) |

### Infrastructure & Deployment

| Category | Technology |
|---|---|
| **Hosting** | NuGet.org (package distribution) |
| **Source code** | GitHub ([dborgards/eds-dcf-net](https://github.com/dborgards/eds-dcf-net)) |
| **CI/CD** | GitHub Actions |
| **Build workflow** | Build + test on `windows-latest` (SDK from `global.json`; tests run on `net10.0` and `net48`, the latter binds the `netstandard2.0` asset); coverage gate `coverage/threshold` (95 % lines); ApiCompat, breaking-change-intent and npm-lockfile jobs on `ubuntu-latest` |
| **Release workflow** | semantic-release v25 (Node.js 24, `windows-latest`) → NuGet publish |
| **Versioning** | Semantic Versioning (automated via conventional commits) |
| **Dependency updates** | Renovate (NuGet / npm / GitHub Actions → `develop`) |

### Analytics & Monitoring

| Category | Technology |
|---|---|
| **Code coverage tracking** | Codecov (integrated in CI pipeline) |
| **Release notes** | Auto-generated CHANGELOG.md via `@semantic-release/changelog` |

### Development Workflow & Collaboration

| Category | Technology |
|---|---|
| **Version control** | Git |
| **Commit convention** | Conventional Commits |
| **Branching strategy** | `main` (stable) · `develop` (beta) · feature branches |
| **Release automation** | semantic-release with commit-analyzer, changelog, exec, git, github plugins |
| **Documentation** | ARC42 architecture docs (12 chapters), CiA format notes in README |
| **IDE support** | Visual Studio 2017+ (.sln), VS Code |
| **AI assistants** | CLAUDE.md, `.github/copilot-instructions.md` |
