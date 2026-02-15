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
        +ParseFile(string filePath) Dictionary~string, Dictionary~string, string~~
        +ParseString(string content) Dictionary~string, Dictionary~string, string~~
        +GetValue(sections, sectionName, key, defaultValue) string$
        +HasSection(sections, sectionName) bool$
        +GetKeys(sections, sectionName) IEnumerable~string~$
    }

    class EdsReader {
        -IniParser _iniParser
        +ReadFile(string filePath) ElectronicDataSheet
        +ReadString(string content) ElectronicDataSheet
        -ParseEds(sections) ElectronicDataSheet
        -ParseFileInfo(sections) EdsFileInfo
        -ParseDeviceInfo(sections) DeviceInfo
        -ParseObjectDictionary(sections) ObjectDictionary
        -ParseObject(sections, ushort index) CanOpenObject?
        -ParseComments(sections) Comments?
        -ParseSupportedModules(sections) List~ModuleInfo~
        -ParseDynamicChannels(sections) DynamicChannels?
        -ParseTools(sections) List~ToolInfo~
    }

    class DcfReader {
        -IniParser _iniParser
        -EdsReader _edsReader
        +ReadFile(string filePath) DeviceConfigurationFile
        +ReadString(string content) DeviceConfigurationFile
        -ParseDcf(sections) DeviceConfigurationFile
        -ParseDeviceCommissioning(sections) DeviceCommissioning
        -ParseConnectedModules(sections) List~int~
    }

    IniParser <-- EdsReader : uses
    IniParser <-- DcfReader : uses
    EdsReader <-- DcfReader : delegates shared parsing
```

**IniParser** is the base component that transforms raw INI text into a sections dictionary (`Dictionary<string, Dictionary<string, string>>`) that preserves section names as they appear in the file while using a case-insensitive comparer (`StringComparer.OrdinalIgnoreCase`) for lookups. It provides instance methods (`ParseFile`, `ParseString`) for parsing and static helper methods (`GetValue`, `HasSection`, `GetKeys`) for querying the resulting dictionary.

**EdsReader** holds an internal `IniParser` instance. Its public methods (`ReadFile`, `ReadString`) parse the input into a sections dictionary and then build a complete `ElectronicDataSheet` model from it. Unknown sections are preserved in `AdditionalSections`.

**DcfReader** holds its own `IniParser` and an `EdsReader` instance (for delegating shared parsing like `ParseDeviceInfo` and `ParseDynamicChannels`). It adds DCF-specific sections (`DeviceCommissioning`, `ConnectedModules`) and fields (`ParameterValue`, `Denotation`).

### 5.2.2 Writers

```mermaid
classDiagram
    class DcfWriter {
        +WriteFile(DeviceConfigurationFile dcf, string filePath) void
        +GenerateString(DeviceConfigurationFile dcf) string
        -GenerateDcfContent(DeviceConfigurationFile dcf) string
        -WriteFileInfo(StringBuilder sb, EdsFileInfo fileInfo) void
        -WriteDeviceInfo(StringBuilder sb, DeviceInfo deviceInfo) void
        -WriteDeviceCommissioning(StringBuilder sb, DeviceCommissioning dc) void
        -WriteDummyUsage(StringBuilder sb, ObjectDictionary objDict) void
        -WriteComments(StringBuilder sb, Comments comments) void
        -WriteObjectLists(StringBuilder sb, ObjectDictionary objDict) void
        -WriteObjects(StringBuilder sb, ObjectDictionary objDict) void
        -WriteObject(StringBuilder sb, CanOpenObject obj) void
        -WriteSubObject(StringBuilder sb, ushort index, CanOpenSubObject subObj) void
        -WriteSupportedModules(StringBuilder sb, List~ModuleInfo~ modules) void
        -WriteConnectedModules(StringBuilder sb, List~int~ modules) void
        -WriteDynamicChannels(StringBuilder sb, DynamicChannels dc) void
        -WriteTools(StringBuilder sb, List~ToolInfo~ tools) void
    }
```

**DcfWriter** is a stateless class that serializes a `DeviceConfigurationFile` model back into the INI-based DCF format. The `DeviceConfigurationFile` is passed as a parameter to each public method rather than stored in the writer. Output can be written either to a file (ASCII encoding) or returned as a string.

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
        +List~int~ ConnectedModules
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
        +AccessType AccessType
        +string? DefaultValue
        +string? ParameterValue
        +string? Denotation
        +string? LowLimit
        +string? HighLimit
        +bool PdoMapping
        +bool SrdoMapping
        +string? InvertedSrad
        +uint ObjFlags
        +byte? SubNumber
        +byte? CompactSubObj
        +Dictionary~byte, CanOpenSubObject~ SubObjects
        +List~ushort~ ObjectLinks
        +string? UploadFile
        +string? DownloadFile
        +string? ParamRefd
    }

    class CanOpenSubObject {
        +byte SubIndex
        +string ParameterName
        +byte ObjectType
        +ushort DataType
        +AccessType AccessType
        +string? DefaultValue
        +string? ParameterValue
        +string? Denotation
        +string? LowLimit
        +string? HighLimit
        +bool PdoMapping
        +bool SrdoMapping
        +string? InvertedSrad
        +string? ParamRefd
    }

    class DeviceInfo {
        +string VendorName
        +uint VendorNumber
        +string ProductName
        +uint ProductNumber
        +uint RevisionNumber
        +string OrderCode
        +BaudRates SupportedBaudRates
        +bool SimpleBootUpMaster
        +bool SimpleBootUpSlave
        +byte Granularity
        +byte DynamicChannelsSupported
        +bool GroupMessaging
        +ushort NrOfRxPdo
        +ushort NrOfTxPdo
        +bool LssSupported
        +byte CompactPdo
        +bool CANopenSafetySupported
    }

    class DeviceCommissioning {
        +byte NodeId
        +string NodeName
        +ushort Baudrate
        +uint NetNumber
        +string NetworkName
        +bool CANopenManager
        +uint? LssSerialNumber
        +string? NodeRefd
        +string? NetRefd
    }

    class EdsFileInfo {
        +string FileName
        +byte FileVersion
        +byte FileRevision
        +string EdsVersion
        +string Description
        +string CreationTime
        +string CreationDate
        +string CreatedBy
        +string ModificationTime
        +string ModificationDate
        +string ModifiedBy
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
        +ParseInteger(string value, byte? nodeId) uint$
        +ParseBoolean(string value) bool$
        +ParseByte(string value) byte$
        +ParseUInt16(string value) ushort$
        +ParseAccessType(string value) AccessType$
        +AccessTypeToString(AccessType accessType) string$
        +FormatInteger(uint value, bool useHex) string$
        +FormatBoolean(bool value) string$
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
