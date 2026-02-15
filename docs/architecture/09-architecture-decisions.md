# 9. Architecture Decisions

## ADR-1: Static Facade as API Entry Point

### Context

The library needs a clearly defined entry point for consumers.

### Decision

`CanOpenFile` is a **static class** with static methods (`ReadEds`, `ReadDcf`, `WriteDcf`, `EdsToDcf`).

### Rationale

- The library holds **no state** between calls -- every call is self-contained.
- Static methods are idiomatic in C# for stateless operations.
- A simple call like `CanOpenFile.ReadEds("file.eds")` requires no instantiation and minimizes onboarding effort.

### Consequences

- (+) Minimal API surface, easy to discover and use.
- (+) No dependency injection setup needed for simple use cases.
- (-) Harder to mock in consumer unit tests (workaround: use `ReadEdsFromString`/`WriteDcfToString`).

---

## ADR-2: Custom INI Parser Instead of External Library

### Context

EDS/DCF files are based on the INI format. Numerous INI parser libraries exist for .NET.

### Decision

A **custom, minimal INI parser** (`IniParser`) is implemented.

### Rationale

- **Zero-dependency principle**: No external NuGet dependencies.
- EDS/DCF uses only a subset of the INI format (sections, key-value pairs, comments with `;`).
- A tailored parser can natively support case-insensitive section names.

### Consequences

- (+) No dependency conflicts, lean package.
- (+) Full control over parsing behavior.
- (-) Maintenance overhead for the custom parser.

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

EDS/DCF files can contain vendor-specific or future sections not represented in the model.

### Decision

Unknown sections are stored in a `Dictionary<string, Dictionary<string, string>> AdditionalSections` and written back during output.

### Rationale

- Prevents data loss during round-trips (read -> modify -> write).
- Forward compatibility with future CiA specification versions.
- Tolerance for vendor-specific extensions.

### Consequences

- (+) No unintentional removal of information.
- (+) Compatibility with extended EDS/DCF files.
- (-) No typed validation for unknown sections.

---

## ADR-6: ASCII Encoding for DCF Output

### Context

The CiA DS 306 specification defines EDS/DCF files as ASCII-encoded text files.

### Decision

The `DcfWriter` uses **ASCII encoding** when writing files.

### Rationale

- Specification compliance.
- Maximum compatibility with existing CANopen tools.

### Consequences

- (+) Interoperability with all common CANopen configuration tools.
- (-) No support for Unicode characters in parameter names or comments.
