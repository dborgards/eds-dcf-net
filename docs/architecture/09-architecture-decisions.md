# 9. Architecture Decisions

## ADR-1: Static Facade as API Entry Point

### Context

The library needs a clearly defined entry point for consumers.

### Decision

`CanOpenFile` is a **static class** with static methods for all supported formats,
including synchronous and asynchronous file-I/O variants
(`ReadEds`/`ReadEdsAsync`, `ReadDcf`/`ReadDcfAsync`, `ReadCpj`/`ReadCpjAsync`,
`ReadXdd`/`ReadXddAsync`, `ReadXdc`/`ReadXdcAsync`, corresponding write methods, and `EdsToDcf`).

### Rationale

- The library holds **no state** between calls -- every call is self-contained.
- Static methods are idiomatic in C# for stateless operations.
- A simple call like `CanOpenFile.ReadEds("file.eds")` requires no instantiation and minimizes onboarding effort.

### Consequences

- (+) Minimal API surface, easy to discover and use.
- (+) No dependency injection setup needed for simple use cases.
- (-) Harder to mock in consumer unit tests (workaround: use `Read*FromString`/`Write*ToString` methods).

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
- The `EdsToDcf` conversion makes the transformation explicit.

### Consequences

- (+) Type safety: DCF always has `DeviceCommissioning`, EDS never does.
- (+) Clear API semantics: `ReadEds` returns `ElectronicDataSheet`, `ReadDcf` returns `DeviceConfigurationFile`.
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
- (-) Certain modern APIs (e.g., `string.Contains(StringComparison)`) cannot be used in shared code.
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
