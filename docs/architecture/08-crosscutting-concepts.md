# 8. Crosscutting Concepts

## 8.1 Error Handling

### Strategy

The library uses **exceptions** as its primary error mechanism:

| Exception               | Use Case                                                    | Additional Information       |
|-------------------------|-------------------------------------------------------------|------------------------------|
| `EdsParseException`     | Errors during EDS/DCF/CPJ/XDD/XDC parsing                   | `LineNumber`, `SectionName`  |
| `EdsWriteException`     | Errors during EDS writing                                   | `SectionName`                |
| `DcfWriteException`     | Errors during DCF writing                                   | `SectionName`                |
| `CpjWriteException`     | Errors during CPJ writing                                   | `SectionName`                |
| `XddWriteException`     | Errors during XDD writing                                   | `SectionName`                |
| `XdcWriteException`     | Errors during XDC writing (including commissioning validation) | `SectionName`             |
| `ArgumentException`     | Invalid input parameters where validation is performed by the API | Standard .NET          |

> **Note:** `CanOpenFile.EdsToDcf`, DCF parsing, and XDC writing enforce CANopen Node-ID constraints for explicit commissioning data. `EdsToDcf` and DCF parsing require `1..127`; XDC writing emits commissioning only when a configured NodeId is present and valid and throws `XdcWriteException` for out-of-range values.

> **Compatibility note (AccessType):** Parsing of invalid or unknown `AccessType` values is intentionally tolerant and falls back to `ReadOnly` instead of failing. This is a deliberate trade-off to maximize interoperability with non-compliant manufacturer EDS/DCF files.

### Error Tolerance

```mermaid
flowchart TD
    A["Read CANopen file (INI/XML)"] --> B{Required structure present?}
    B -->|No| C["EdsParseException"]
    B -->|Yes| D{Optional section present?}
    D -->|No| E["Use default value / null"]
    D -->|Yes| F{Value parseable?}
    F -->|No| G["EdsParseException with context"]
    F -->|Yes| H["Store value in model"]
    E --> H
```

- **Required fields**: Missing required sections result in an `EdsParseException`.
- **Optional fields**: Missing optional values result in `null` or default values.
- **Unknown INI sections**: Preserved in `AdditionalSections` (no warning, no error).
- **CiA 311 XML**: Parsed against supported profile structures; unsupported XML nodes are not represented as generic passthrough data.

### Input Size Limits

To mitigate memory-pressure and oversized-input scenarios, all read APIs enforce a default
maximum input size of `IniParser.DefaultMaxInputSize` (10 MB).

The limit is configurable per read operation (`Read*`, `Read*Async`, `Read*FromString`)
for EDS/DCF/CPJ/XDD/XDC, including the `CanOpenFile` facade.

Guideline: keep the default for untrusted inputs and raise limits only as needed for
trusted, known-large payloads.

## 8.2 Culture Independence (InvariantCulture)

CANopen INI/XML files are culture-independent. Numeric values use deterministic formats and must not depend on OS locale.

### Rule

Every numeric or date-related parse/format operation **must** use `CultureInfo.InvariantCulture`:

```csharp
// Correct
int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result);
value.ToString(CultureInfo.InvariantCulture);

// Wrong -- depends on system culture
int.TryParse(value, out var result);
value.ToString();
```

## 8.3 Number Format Processing

The `ValueConverter` supports three number formats specified in CiA DS 306:

```mermaid
flowchart TD
    A["Input string"] --> B{"Starts with '0x' or '0X'?"}
    B -->|Yes| C["Parse as hexadecimal<br/>(e.g., 0x1A00)"]
    B -->|No| D{"Starts with '0' and length > 1?"}
    D -->|Yes| E["Parse as octal<br/>(e.g., 0177)"]
    D -->|No| F{"Starts with '$NODEID'?"}
    F -->|Yes| G["Evaluate $NODEID formula<br/>(e.g., $NODEID+0x200)"]
    F -->|No| H["Parse as decimal<br/>(e.g., 42)"]
```

### $NODEID Formula

DCF files can contain values computed relative to the node ID:

| Example                 | Node ID = 5 | Result   |
|------------------------|-------------|----------|
| `$NODEID`              | 5           | 5        |
| `$NODEID+0x600`        | 5           | 1541     |
| `$NODEID+0x200`        | 5           | 517      |

## 8.4 Round-Trip Fidelity

A core design principle is **round-trip fidelity**: EDS/DCF/CPJ files that are read and written back unchanged should not lose any information.

```mermaid
flowchart LR
    A["INI File<br/>(EDS/DCF/CPJ)"] -->|Read*| B["Model<br/>(Typed objects)"]
    B -->|Write*| C["INI File<br/>(Output)"]

    B --- D["AdditionalSections<br/>preserves unknown sections"]

    style D fill:#F5A623,color:#fff
```

Mechanisms (INI formats):
- **`AdditionalSections`**: All sections not mapped by the model are stored as raw key-value pairs and written back during output.
- **`LastEds`**: DCF files store the filename of the source EDS.

For CiA 311 XML, round-trip behavior is guaranteed for the currently mapped schema subset used by `XddReader`/`XdcReader` and `XddWriter`/`XdcWriter`.

## 8.5 CiA 311 XML Mapping

CiA 311 support is implemented through explicit mapping of ISO 15745 profile elements to shared domain models:

- `CANopenObject` / `CANopenSubObject` attributes map to Object Dictionary objects/sub-objects.
- XDC `actualValue` and `denotation` map to `ParameterValue` and `Denotation`.
- `deviceCommissioning` maps to `DeviceCommissioning`.

XDC writer behavior:
- NodeId `0` means "commissioning not configured" and omits the XML `deviceCommissioning` element.
- NodeId `1..127` emits a valid `deviceCommissioning` element.
- Out-of-range NodeId values cause an `XdcWriteException`.

## 8.6 Modular Devices (CiA DS 306)

CANopen supports modular devices (e.g., bus couplers with pluggable I/O modules). EdsDcfNet fully represents this concept:

```mermaid
graph TD
    EDS["ElectronicDataSheet"]
    SM["SupportedModules<br/><i>List of available modules</i>"]
    MI["ModuleInfo<br/><i>Per module: name, version, objects</i>"]
    FO["FixedObjects<br/><i>OD entries provided by the module</i>"]
    SE["SubExtensions<br/><i>Dynamic sub-index extensions</i>"]

    DCF["DeviceConfigurationFile"]
    CM["ConnectedModules<br/><i>Actually plugged-in modules</i>"]

    EDS --> SM
    SM --> MI
    MI --> FO
    MI --> SE
    DCF --> CM

    style EDS fill:#4A90D9,color:#fff
    style DCF fill:#E74C3C,color:#fff
```

## 8.7 CANopen Object Dictionary Structure

The Object Dictionary is the heart of every CANopen device:

```mermaid
graph TD
    OD["ObjectDictionary"]
    MO["MandatoryObjects<br/><i>0x1000-0x1FFF</i>"]
    OO["OptionalObjects<br/><i>0x1000-0x1FFF</i>"]
    MF["ManufacturerObjects<br/><i>0x2000-0x5FFF</i>"]

    OBJ["CanOpenObject<br/><i>Index, Name, Type, DataType</i>"]
    SUB["CanOpenSubObject<br/><i>SubIndex, Name, Type, DataType</i>"]

    OD --> MO
    OD --> OO
    OD --> MF
    MO --> OBJ
    OO --> OBJ
    MF --> OBJ
    OBJ --> SUB

    style OD fill:#4A90D9,color:#fff
    style OBJ fill:#7AB648,color:#fff
    style SUB fill:#F5A623,color:#fff
```

### Object Types

| ObjectType | Value | Description                                     |
|------------|-------|-------------------------------------------------|
| VAR        | 0x07  | Single variable                                 |
| ARRAY      | 0x08  | Array with homogeneous sub-objects              |
| RECORD     | 0x09  | Structure with heterogeneous sub-objects        |

### Access Types

| Enum Value           | Abbreviation | Meaning                        |
|----------------------|--------------|--------------------------------|
| `ReadOnly`           | `ro`         | Read only                      |
| `WriteOnly`          | `wo`         | Write only                     |
| `ReadWrite`          | `rw`         | Read and write                 |
| `ReadWriteInput`     | `rwr`        | Read/write (process input)     |
| `ReadWriteOutput`    | `rww`        | Read/write (process output)    |
| `Constant`           | `const`      | Constant, not modifiable       |
