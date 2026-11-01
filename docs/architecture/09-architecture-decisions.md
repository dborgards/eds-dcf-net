# 9. Architecture Decisions

## ADR-1: Static Facade as API Entry Point

### Context

The library needs a clearly defined entry point for consumers.

### Decision

`CanOpenFile` is a **static class** with format-specific entry points (`Eds`, `Dcf`, `Cpj`,
`Xdd`, `Xdc`) for read/write operations, including synchronous and asynchronous file-I/O
variants, plus `Eds.ConvertToDcf` for EDS-to-DCF conversion. Legacy static `Read*` /
`Write*` / `EdsToDcf` facade methods remain for backward compatibility.

### Rationale

- The library holds **no state** between calls -- every call is self-contained.
- Static methods are idiomatic in C# for stateless operations.
- A simple call like `CanOpenFile.Eds.ReadFile("file.eds")` requires no instantiation and minimizes onboarding effort.

### Consequences

- (+) Minimal API surface, easy to discover and use.
- (+) No dependency injection setup needed for simple use cases.
- (-) Harder to mock in consumer unit tests (workaround: use `ReadString`/`WriteToString` on the format entry points).

---

## ADR-2: Custom INI Parser Instead of External Library

### Context

EDS/DCF/CPJ files are based on the INI format. Numerous INI parser libraries exist for .NET.

### Decision

A **custom, minimal INI parser** (`IniParser`) is implemented.

### Rationale

- **Zero-dependency principle**: No external NuGet dependencies.
- EDS/DCF/CPJ use only a subset of the INI format (sections, key-value pairs, comments with `;`).
- A tailored parser can natively support case-insensitive section names.

### Consequences

- (+) No dependency conflicts, lean package.
- (+) Full control over INI parsing behavior.
- (-) Maintenance overhead for the custom parser.

For CiA 311 XML formats (XDD/XDC), the implementation uses built-in `.NET` XML APIs (`System.Xml.Linq`) rather than a third-party XML dependency.

---

## ADR-3: Separate Models for EDS and DCF

### Context

EDS and DCF share most sections, but DCF has additional fields (`DeviceCommissioning`, `ParameterValue`, `ConnectedModules`).

### Decision

`ElectronicDataSheet` and `DeviceConfigurationFile` are **separate classes** (no inheritance).

### Rationale

- Clear semantic separation: EDS is a template, DCF is a configured instance.
- Avoids confusion about optional vs. required fields.
- The `CanOpenFile.Eds.ConvertToDcf` conversion makes the transformation explicit.

### Consequences

- (+) Type safety: DCF always has `DeviceCommissioning`, EDS never does.
- (+) Clear API semantics: `CanOpenFile.Eds.ReadFile` returns `ElectronicDataSheet`, `CanOpenFile.Dcf.ReadFile` returns `DeviceConfigurationFile`.
- (-) Duplication of shared properties between the classes.

---

## ADR-4: Dual Target netstandard2.0 + net10.0

### Context

The library should support as many .NET platforms as possible while benefiting from modern APIs.

### Decision

**Multi-targeting** with `netstandard2.0` and `net10.0`.

### Rationale

- `netstandard2.0` covers .NET Framework 4.6.1+, .NET Core 2.0+, Mono, Xamarin, and Unity.
- `net10.0` enables use of new APIs and compiler optimizations.
- Industrial applications frequently still use older .NET versions.

### Consequences

- (+) Maximum reach across all .NET platforms.
- (-) Some modern APIs are not available on `netstandard2.0` (e.g., `string.Replace(string, string, StringComparison)`). Most others (e.g., `string.Contains(char)`, `string.Contains(string, StringComparison)`) are provided by the `Polyfill` package (see § 2.1).
- (-) Increased testing effort for both targets.

---

## ADR-5: Round-Trip Fidelity via AdditionalSections

### Context

INI-based formats (EDS/DCF/CPJ) can contain vendor-specific or future sections not represented in the model.

### Decision

Unknown sections are stored in a `Dictionary<string, Dictionary<string, string>> AdditionalSections` and written back during output.

### Rationale

- Prevents data loss during round-trips (read -> modify -> write).
- Forward compatibility with future CiA specification versions.
- Tolerance for vendor-specific extensions.

### Consequences

- (+) No unintentional removal of information.
- (+) Compatibility with extended EDS/DCF/CPJ files.
- (-) No typed validation for unknown sections.

---

## ADR-6: UTF-8 (without BOM) for INI/XML File Output

### Context

CiA DS 306 historically describes EDS/DCF as ASCII-oriented text files, while
real-world files often contain extended characters from vendor/device names.

### Decision

All writers (`EdsWriter`, `DcfWriter`, `CpjWriter`, `XddWriter`, `XdcWriter`) use **UTF-8 without BOM** when writing files.

### Rationale

- UTF-8 is ASCII-compatible for the 7-bit subset required by classic tooling.
- Non-ASCII characters are preserved instead of being replaced during write.
- Reader and writer behavior stays symmetric across INI and XML formats (UTF-8-based text processing).

### Consequences

- (+) Preserves Unicode content in names/comments for modern toolchains.
- (+) Keeps ASCII compatibility for standard-compliant content.
- (-) A very strict ASCII-only consumer may reject UTF-8 files with non-ASCII characters.

---

## ADR-7: Typed ApplicationProcess Model Instead of Raw XML Passthrough

### Context

CiA 311 XDD/XDC files can contain an `ApplicationProcess` element (§6.4.5) that describes the device's services and parameters at the application level, independently of communication technology. An initial implementation stored this element as a raw XML string (`ApplicationProcessXml`) to achieve round-trip fidelity without the complexity of full type modelling.

### Decision

Replace the raw `string? ApplicationProcessXml` property on `ElectronicDataSheet` and `DeviceConfigurationFile` with a fully typed `ApplicationProcess? ApplicationProcess` object graph covering all sub-constructs defined by CiA 311 §6.4.5 (`dataTypeList`, `functionTypeList`, `functionInstanceList`, `templateList`, `parameterList`, `parameterGroupList`).

### Rationale

- A typed model enables consumers to read and write individual parameters, function types, and data type definitions programmatically — something the raw string approach did not support.
- Type safety and XML doc comments make the API self-describing and discoverable.
- The writer regenerates well-formed XML from the typed model, so round-trip fidelity is preserved without storing an internal raw string.
- Consistent with ADR-3 (separate, fully typed models for each domain concept).

### Consequences

- (+) Full programmatic access to all ApplicationProcess sub-constructs.
- (+) Consistent model-first design: no internal raw XML state.
- (-) Breaking change: consumers who used `ApplicationProcessXml` directly must migrate to the typed API (`ApplicationProcess.ParameterList`, etc.).
- (-) Increased model surface area (several new `Ap*` classes).

---

## ADR-8: Interface-Based Reader Abstraction Across File Formats

### Context

`EdsReader` and `DcfReader` share internal INI parsing through `CanOpenReaderBase`, while
`CpjReader` intentionally does not inherit from that base class because CPJ has a different structure.
This created an API inconsistency: consumers had no common contract to reference when handling
multiple reader types uniformly.

### Decision

Introduce a lightweight generic interface `IFileReader<TModel>` with shared read operations
(`ReadFile`, `ReadStream`, `ReadString`, and async variants with explicit `maxInputSize`).
`EdsReader`, `DcfReader`, `CpjReader`, `XddReader`, and `XdcReader` implement this interface.

### Rationale

- Provides a consistent public reader contract without forcing incorrect inheritance.
- Preserves existing parser internals (`CanOpenReaderBase` remains only for shared EDS/DCF logic).
- Improves discoverability and enables polymorphic reader handling in consumer code.

### Consequences

- (+) Unified reader API surface across INI and XML formats.
- (+) No behavior change and no forced coupling of CPJ parsing to EDS/DCF internals.
- (-) Introduces one additional public abstraction to maintain.
- (-) Writer abstraction is intentionally left unchanged for now and can be addressed separately if needed.

---

## ADR-9: Validation Rules Stay in the Core Package; the Checker Ships as a dotnet Tool

Status: **Accepted** (2026-11-01), implemented in #682–#688. Refs #644 (review findings E-1, E-2, T5;
plan decision E6).

### Context

The rule engine already lived in the core assembly `EdsDcfNet` under `EdsDcfNet.Validation`
(`CanOpenModelValidator` for model rules, `CanOpenIniValidator` for raw EDS/DCF source rules,
`IniWriteRules` / `XmlWriteRules` for the write path). `edsdcf-check` was a thin CLI in `examples/`,
excluded from coverage, with its tests inside the library test project. Open points were duplicated
INI helpers (rest of E-2), no rule catalog, library codes hidden behind `LIB001`/`LIB003`, and a
release pipeline that published exactly one package.

### Decision

1. The shared rules stay in the core package `EdsDcfNet`, namespace `EdsDcfNet.Validation`. There is no
   separate validation package.
2. Core has a public rule catalog (`CanOpenValidationRuleCatalog`: id, default severity, title, CiA
   reference) and per-rule severity overrides in `CanOpenIniValidationOptions`. Both are additive.
3. The checker lives in `src/EdsDcfNet.Checker` and is packed as a dotnet tool from that project
   (`PackAsTool`, `net10.0`, `RollForward=Major`). It uses only the public `EdsDcfNet.Validation` API.
4. The tool is released in lockstep with the library: same version, packed and pushed in the same
   release run, bundling the core assembly of the same build.
5. `StrictParsing` stays a reader mode. Reader diagnostics keep their `ParseDiagnostic.Code` as the
   rule id in INI validation results; `LIB` codes are only fallbacks.

Dependency direction: `EdsDcfNet.Checker` → `EdsDcfNet`, never the reverse.

### Rationale

- The public types in `EdsDcfNet.Validation` shipped in 1.16.0. Moving them to another assembly would
  be breaking, and type forwarding cannot help because core would have to reference the new package.
- The writers and `ValidateBeforeWrite` need the model and write rules in core anyway.
- The raw rules mirror reader behaviour and use internals; one assembly avoids `InternalsVisibleTo`
  between packages and new public helpers, which would recreate E-2.
- The rules add no dependencies, so a separate package would isolate nothing.

### Consequences

- (+) No breaking change; one rule set for library users, the write guard and the CLI.
- (+) CI users install the tool with one command and get text, JSON or SARIF output.
- (-) The release scripts pack and push two packages (CONTRIBUTING "Two packages, one version").
- (-) `fix(checker)` commits bump the shared version.
- (-) The checker code is under the coverage gate (its own test project).
- (-) Three severity enums remain (TD-10); unifying them is binary-breaking and waits for 2.0 (#691).

### Alternatives Considered

- **Separate validation package:** rejected for the reasons above. Revisit only if the rules ever need
  heavy dependencies.
- **Rules only in the CLI:** rejected; that was the state behind E-1/E-2.
- **Tool with its own engine (variant A alone):** superseded by #675.

### Layout

```
src/EdsDcfNet/Validation/              rules, catalog, options (public API)
src/EdsDcfNet/Validation/Ini/          raw INI rules (internal)
src/EdsDcfNet.Checker/                 CLI, packed as the edsdcf-check dotnet tool (net10.0)
tests/EdsDcfNet.Tests/Validation/      rule tests (net10.0 + net48)
tests/EdsDcfNet.Checker.Tests/         CLI tests: args, exit codes, output snapshots, tool package
```

There is no public rule registration interface. The rules work on the internal `RawIniDocument`, and a
plugin surface would freeze it.

### CLI Contract

- **Formats:** `--format text|json|sarif`, `--json` kept as an alias; `--output <file>`.
- **Rules:** `--severity <ID>=error|warning|none`, `--config <file>` (severity map, `exclude` globs,
  per-glob `overrides`), `--list-rules`. Unknown rule ids are a usage error.
- **Exit codes:** 0 = clean, 1 = error-level findings after overrides, 2 = usage/I/O problem or nothing
  checked. 2 wins over 1.
- **SARIF 2.1.0:** every catalog rule as a rule descriptor; each finding with file and line as the
  physical location and `[Section]Key` as the logical location.

### Settled Questions

- **Package id and command:** `EdsDcfNet.Checker` / `edsdcf-check`.
- **Runtime:** `net10.0` with `RollForward=Major`.
- **Model issues:** `ObjectDictionary.*` model issues stay suppressed in the INI validation results,
  because the raw rules report the same problems with line numbers.
- **XDD/XDC:** out of scope for the tool; the CiA 311 XSD must not ship in a package (plan E8).
- **SBOM:** the tool has no SBOM of its own; its only non-framework content is the bundled
  `EdsDcfNet.dll`, which the release SBOM describes.
- **`IncludeReaderDiagnostics` / `IncludeModelValidation`:** default `false` in the library, so
  `CanOpenIniValidationOptions.Default` equals the behaviour without options; the CLI turns both on
  unless `--no-library` is given.
