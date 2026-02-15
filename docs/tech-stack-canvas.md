# Tech Stack Canvas — EdsDcfNet

> Based on [Tech Stack Canvas](https://techstackcanvas.io) by Jörg Müller (INNOQ)

| | Context | Layers | | Support |
|---|---|---|---|---|
| **Business Goals** | **Frontend Technologies** | **API & Integrations** | **Security & Compliance** | |
| Provide a zero-dependency C# library for reading/writing CiA DS 306 EDS and DCF files | _Not applicable_ (library, no UI) | CANopen Object Dictionary (0x1000–0x5FFF) | MIT License | |
| Support broad .NET ecosystem (netstandard2.0 + net10.0) | | PDO / SRDO mapping | Nullable reference types enabled | |
| Enable round-trip fidelity for industrial automation device configuration | | `$NODEID` formula evaluation | Deterministic builds | |
| | | Modular device support (bus couplers + modules) | SourceLink for supply-chain transparency | |
| | | Compact storage modes (CompactPDO, CompactSubObj) | | |

---

## Context

### Business Goals

- Provide a comprehensive, **zero-dependency** C# library for reading and writing CiA DS 306 EDS/DCF files
- Support the broadest possible .NET ecosystem via **netstandard2.0** (.NET Framework 4.6.1+, .NET Core 2.0+, Unity, Xamarin) and **net10.0**
- Enable **round-trip fidelity** — unknown/vendor-specific sections are preserved during read-modify-write cycles
- Distribute as a NuGet package with automated semantic versioning and publishing

### Sizing Numbers

- **Target platforms:** netstandard2.0 + net10.0 (covers .NET Framework 4.6.1 through .NET 10)
- **External dependencies:** 0 (core library)
- **Object Dictionary address space:** 0x1000–0x5FFF (CANopen standard)
- **Current version:** 1.3.1
- **File formats:** 2 (EDS read, DCF read/write)

### Major Quality Attributes

- **Compatibility** — Must work on all .NET platforms via netstandard2.0; all string/number parsing uses `CultureInfo.InvariantCulture`
- **Correctness** — Full compliance with CiA DS 306 v1.4.0 specification
- **Round-trip fidelity** — Unknown sections preserved in `AdditionalSections` dictionary
- **Simplicity** — Zero external dependencies, single static facade API (`CanOpenFile`)
- **Maintainability** — Comprehensive test suite, architecture documentation (ARC42), XML doc comments on all public members

---

## Core Technology Layers

### Frontend Technologies

_Not applicable_ — EdsDcfNet is a library without a user interface.

An **example console application** (`examples/EdsDcfNet.Examples/`) demonstrates API usage.

### Backend Technologies

| Category | Technology |
|---|---|
| **Language** | C# 13.0 (`LangVersion latest`) |
| **Frameworks** | .NET Standard 2.0, .NET 10.0 |
| **Build system** | MSBuild via .NET SDK |
| **Architecture** | Facade → Parsers → Models → Writers |
| **Key patterns** | Facade (`CanOpenFile`), Strategy (`ValueConverter`), DTOs (domain models), Round-trip fidelity |
| **Nullable refs** | Enabled (`<Nullable>enable</Nullable>`) |
| **Implicit usings** | Enabled |
| **File-scoped namespaces** | Yes |

### Data Storage & Management

| Category | Technology |
|---|---|
| **File formats** | EDS / DCF (INI-style text files, CiA DS 306 v1.4.0) |
| **In-memory model** | Strongly-typed C# objects (`ElectronicDataSheet`, `DeviceConfigurationFile`, `ObjectDictionary`) |
| **Persistence** | File-based — read from and write to `.eds` / `.dcf` files |
| **Unknown data** | Preserved in `Dictionary<string, Dictionary<string, string>> AdditionalSections` |

### API & Integrations

**Public API** (static facade `CanOpenFile`):

```
ReadEds(filePath) / ReadEdsFromString(content) → ElectronicDataSheet
ReadDcf(filePath) / ReadDcfFromString(content) → DeviceConfigurationFile
WriteDcf(dcf, filePath) / WriteDcfToString(dcf) → DCF output
EdsToDcf(eds, nodeId, baudrate, nodeName) → DeviceConfigurationFile
```

**CANopen protocol elements:** Object Dictionary, PDO/SRDO mapping, `$NODEID` formulas, modular devices, compact storage modes.

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
| **Assertions** | FluentAssertions 7.0+ |
| **Code coverage** | Coverlet 6.0.4 (XPlat Code Coverage, cobertura format) |
| **Coverage reporting** | Codecov |
| **Naming convention** | `MethodName_Scenario_ExpectedBehavior` |
| **Test pattern** | Arrange-Act-Assert (AAA) |
| **Fixture data** | `tests/EdsDcfNet.Tests/Fixtures/` (sample EDS/DCF files) |

### Infrastructure & Deployment

| Category | Technology |
|---|---|
| **Hosting** | NuGet.org (package distribution) |
| **Source code** | GitHub ([dborgards/eds-dcf-net](https://github.com/dborgards/eds-dcf-net)) |
| **CI/CD** | GitHub Actions |
| **Build workflow** | Build + test on ubuntu-latest with .NET 8.0 and 10.0 |
| **Release workflow** | semantic-release v23 (Node.js 20) → NuGet publish |
| **Versioning** | Semantic Versioning (automated via conventional commits) |
| **Dependency updates** | Dependabot (NuGet ecosystem, weekly) |

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
| **Branching strategy** | `main` (stable) · `develop` (beta) · `alpha` (experimental) · feature branches |
| **Release automation** | semantic-release with commit-analyzer, changelog, exec, git, github plugins |
| **Documentation** | ARC42 architecture docs (12 chapters), CiA DS 306 specification PDF |
| **IDE support** | Visual Studio 2017+ (.sln), VS Code |
| **AI assistants** | CLAUDE.md, `.github/copilot-instructions.md` |
