# 5. Building Block View

## 5.1 Level 1: Overall System

```mermaid
graph LR
    subgraph EdsDcfNet ["EdsDcfNet (NuGet Package)"]
        API["CanOpenFile<br/><i>Static Facade</i>"]
        Parsers["Parsers<br/><i>IniParser, EdsReader, DcfReader</i>"]
        Writers["Writers<br/><i>DcfWriter</i>"]
        Models["Models<br/><i>Domain Models</i>"]
        Utilities["Utilities<br/><i>ValueConverter</i>"]
        Extensions["Extensions<br/><i>ObjectDictionaryExtensions</i>"]
        Exceptions["Exceptions<br/><i>EdsParseException, DcfWriteException</i>"]
    end

    EDS["EDS File"] --> API
    DCF_In["DCF File"] --> API
    API --> DCF_Out["DCF File"]
    API --> Parsers
    API --> Writers
    Parsers --> Models
    Writers --> Models
    Parsers --> Utilities
    Writers --> Utilities
    Extensions --> Models

    style API fill:#4A90D9,color:#fff
    style Parsers fill:#7AB648,color:#fff
    style Writers fill:#7AB648,color:#fff
    style Models fill:#E74C3C,color:#fff
    style Utilities fill:#9B59B6,color:#fff
    style Extensions fill:#F39C12,color:#fff
    style Exceptions fill:#95A5A6,color:#fff
```

### Building Block Overview

| Building Block        | Responsibility                                                            |
|-----------------------|---------------------------------------------------------------------------|
| `CanOpenFile`         | Public API facade; coordinates parsers and writers                        |
| `Parsers/`            | Reading and interpreting EDS/DCF files                                    |
| `Writers/`            | Serializing models back to DCF file format                                |
| `Models/`             | Domain models representing the structure of EDS/DCF files                 |
| `Utilities/`          | Helper functions for type conversion (numbers, booleans, formulas)        |
| `Extensions/`         | Extension methods for convenient ObjectDictionary access                  |
| `Exceptions/`         | Specific exception types for parse and write errors                       |

## 5.2 Level 2: Detailed Building Blocks

### 5.2.1 Parsers

```mermaid
classDiagram
    class IniParser {
        -Dictionary~string, Dictionary~string, string~~ _sections
        +IniParser(string content)
        +GetSections() IEnumerable~string~
        +HasSection(string name) bool
        +GetKeys(string section) IEnumerable~string~
        +GetValue(string section, string key) string?
    }

    class EdsReader {
        +Read(string content) ElectronicDataSheet
        -ParseFileInfo(IniParser parser) EdsFileInfo
        -ParseDeviceInfo(IniParser parser) DeviceInfo
        -ParseObjectDictionary(IniParser parser) ObjectDictionary
        -ParseObject(IniParser parser, ushort index) CanOpenObject
        -ParseSubObject(IniParser parser, ushort index, byte subIndex) CanOpenSubObject
        -ParseComments(IniParser parser) Comments?
        -ParseSupportedModules(IniParser parser) List~ModuleInfo~
        -ParseDynamicChannels(IniParser parser) DynamicChannels?
        -ParseTools(IniParser parser) List~ToolInfo~
    }

    class DcfReader {
        +Read(string content) DeviceConfigurationFile
        -ParseDeviceCommissioning(IniParser parser) DeviceCommissioning
        -ParseConnectedModules(IniParser parser) List~ushort~
    }

    IniParser <-- EdsReader : uses
    IniParser <-- DcfReader : uses
    EdsReader <|-- DcfReader : extends parsing logic
```

**IniParser** is the base component that transforms raw INI text into a searchable data structure (sections with key-value pairs). Lookups are **case-insensitive**.

**EdsReader** uses the IniParser to build a complete `ElectronicDataSheet` model. It processes all specified sections and preserves unknown sections in `AdditionalSections`.

**DcfReader** extends the EdsReader logic with DCF-specific sections (`DeviceCommissioning`, `ConnectedModules`) and fields (`ParameterValue`, `Denotation`).

### 5.2.2 Writers

```mermaid
classDiagram
    class DcfWriter {
        -DeviceConfigurationFile _dcf
        +DcfWriter(DeviceConfigurationFile dcf)
        +WriteFile(string filePath) void
        +GenerateString() string
        -WriteFileInfo(StringBuilder sb) void
        -WriteDeviceInfo(StringBuilder sb) void
        -WriteDeviceCommissioning(StringBuilder sb) void
        -WriteDummyUsage(StringBuilder sb) void
        -WriteComments(StringBuilder sb) void
        -WriteObjectDictionary(StringBuilder sb) void
        -WriteObject(StringBuilder sb, CanOpenObject obj) void
        -WriteSubObject(StringBuilder sb, CanOpenObject obj, CanOpenSubObject sub) void
        -WriteSupportedModules(StringBuilder sb) void
        -WriteConnectedModules(StringBuilder sb) void
        -WriteDynamicChannels(StringBuilder sb) void
        -WriteTools(StringBuilder sb) void
        -WriteAdditionalSections(StringBuilder sb) void
    }
```

**DcfWriter** serializes a `DeviceConfigurationFile` model back into the INI-based DCF format. Output can be written either to a file (ASCII encoding) or returned as a string.

### 5.2.3 Models

```mermaid
classDiagram
    class ElectronicDataSheet {
        +EdsFileInfo FileInfo
        +DeviceInfo DeviceInfo
        +ObjectDictionary ObjectDictionary
        +Comments? Comments
        +List~ModuleInfo~ SupportedModules
        +DynamicChannels? DynamicChannels
        +List~ToolInfo~ Tools
        +Dictionary~string, Dictionary~string, string~~ AdditionalSections
    }

    class DeviceConfigurationFile {
        +EdsFileInfo FileInfo
        +DeviceInfo DeviceInfo
        +ObjectDictionary ObjectDictionary
        +DeviceCommissioning DeviceCommissioning
        +List~ushort~ ConnectedModules
        +Comments? Comments
        +List~ModuleInfo~ SupportedModules
        +DynamicChannels? DynamicChannels
        +List~ToolInfo~ Tools
        +Dictionary~string, Dictionary~string, string~~ AdditionalSections
    }

    class ObjectDictionary {
        +List~ushort~ MandatoryObjects
        +List~ushort~ OptionalObjects
        +List~ushort~ ManufacturerObjects
        +Dictionary~ushort, CanOpenObject~ Objects
        +Dictionary~ushort, bool~ DummyUsage
    }

    class CanOpenObject {
        +ushort Index
        +string ParameterName
        +byte ObjectType
        +ushort? DataType
        +AccessType? AccessType
        +string? DefaultValue
        +string? ParameterValue
        +string? Denotation
        +string? LowLimit
        +string? HighLimit
        +bool? PdoMapping
        +byte? CompactSubObj
        +Dictionary~byte, CanOpenSubObject~ SubObjects
        +List~ushort~ ObjectLinks
    }

    class CanOpenSubObject {
        +byte SubIndex
        +string ParameterName
        +byte? ObjectType
        +ushort? DataType
        +AccessType? AccessType
        +string? DefaultValue
        +string? ParameterValue
        +string? Denotation
        +string? LowLimit
        +string? HighLimit
        +bool? PdoMapping
    }

    class DeviceInfo {
        +string? VendorName
        +uint? VendorNumber
        +string? ProductName
        +uint? ProductNumber
        +uint? RevisionNumber
        +string? OrderCode
        +BaudRates BaudRate
        +bool SimpleBootUpMaster
        +bool SimpleBootUpSlave
        +byte? Granularity
        +bool? DynamicChannelsSupported
        +ushort? NrOfRxPdo
        +ushort? NrOfTxPdo
        +bool? LssSupported
    }

    class DeviceCommissioning {
        +byte NodeId
        +string? NodeName
        +ushort Baudrate
        +uint? NetNumber
        +string? NetworkName
        +bool? CANopenManager
        +string? LssSerialNumber
    }

    class EdsFileInfo {
        +string? FileName
        +string? FileVersion
        +string? FileRevision
        +string? EdsVersion
        +string? Description
        +string? CreationTime
        +string? CreationDate
        +string? CreatedBy
        +string? ModificationTime
        +string? ModificationDate
        +string? ModifiedBy
        +string? LastEds
    }

    ElectronicDataSheet *-- EdsFileInfo
    ElectronicDataSheet *-- DeviceInfo
    ElectronicDataSheet *-- ObjectDictionary
    ElectronicDataSheet *-- Comments
    ElectronicDataSheet *-- ModuleInfo
    ElectronicDataSheet *-- DynamicChannels
    ElectronicDataSheet *-- ToolInfo

    DeviceConfigurationFile *-- EdsFileInfo
    DeviceConfigurationFile *-- DeviceInfo
    DeviceConfigurationFile *-- ObjectDictionary
    DeviceConfigurationFile *-- DeviceCommissioning

    ObjectDictionary *-- CanOpenObject
    CanOpenObject *-- CanOpenSubObject
```

### 5.2.4 Utilities

```mermaid
classDiagram
    class ValueConverter {
        +ParseInteger(string? value) int?$
        +ParseBoolean(string? value) bool$
        +ParseByte(string? value) byte?$
        +ParseUInt16(string? value) ushort?$
        +ParseAccessType(string? value) AccessType?$
        +AccessTypeToString(AccessType type) string$
        +FormatInteger(int value) string$
        +FormatBoolean(bool value) string$
        +EvaluateNodeIdFormula(string formula, byte nodeId) int$
    }
```

**ValueConverter** is a static utility class that encapsulates all type-specific conversions:

- **Number formats**: Decimal (`123`), hexadecimal (`0x7B`), octal (`0173`)
- **Boolean values**: `"1"`, `"true"`, `"yes"` are interpreted as `true`
- **`$NODEID` formulas**: Expressions like `$NODEID+0x200` are evaluated at runtime
- **AccessType mapping**: String-to-enum conversion (`"rw"` -> `ReadWrite`)

### 5.2.5 Extensions

```mermaid
classDiagram
    class ObjectDictionaryExtensions {
        +GetObject(ushort index) CanOpenObject?$
        +GetSubObject(ushort index, byte subIndex) CanOpenSubObject?$
        +SetParameterValue(ushort index, string value) void$
        +SetParameterValue(ushort index, byte subIndex, string value) void$
        +GetParameterValue(ushort index) string?$
        +GetParameterValue(ushort index, byte subIndex) string?$
        +GetObjectsByType(ObjectCategory category) IEnumerable~CanOpenObject~$
        +GetPdoCommunicationParameters(bool transmit) IEnumerable~CanOpenObject~$
        +GetPdoMappingParameters(bool transmit) IEnumerable~CanOpenObject~$
    }

    class ObjectCategory {
        <<enumeration>>
        Mandatory
        Optional
        Manufacturer
    }
```

### 5.2.6 Exceptions

```mermaid
classDiagram
    Exception <|-- EdsParseException
    Exception <|-- DcfWriteException

    class EdsParseException {
        +int? LineNumber
        +string? SectionName
    }

    class DcfWriteException {
        +string? SectionName
    }
```

**EdsParseException** is thrown on parsing errors and optionally contains line number and section name for diagnostics.

**DcfWriteException** is thrown on errors during DCF generation.
