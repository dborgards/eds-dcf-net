# 6. Runtime View

## 6.1 Reading an EDS File

The following sequence diagram shows the flow when reading an EDS file:

```mermaid
sequenceDiagram
    participant App as Application
    participant CF as CanOpenFile
    participant IP as IniParser
    participant ER as EdsReader
    participant VC as ValueConverter

    App->>CF: ReadEds(filePath)
    CF->>CF: File.ReadAllText(filePath)
    CF->>IP: new IniParser(content)
    IP->>IP: Parse sections and key-value pairs
    CF->>ER: Read(content)
    ER->>IP: GetValue("FileInfo", ...)
    ER->>ER: ParseFileInfo()
    ER->>IP: GetValue("DeviceInfo", ...)
    ER->>ER: ParseDeviceInfo()

    loop For each object in the ObjectDictionary
        ER->>IP: GetValue("[XXXX]", ...)
        ER->>VC: ParseInteger(), ParseBoolean(), ParseAccessType()
        VC-->>ER: Typed values
        ER->>ER: ParseObject() / ParseSubObject()
    end

    ER->>ER: ParseComments(), ParseSupportedModules(), ...
    ER-->>CF: ElectronicDataSheet
    CF-->>App: ElectronicDataSheet
```

## 6.2 Converting EDS to DCF

```mermaid
sequenceDiagram
    participant App as Application
    participant CF as CanOpenFile
    participant ER as EdsReader
    participant Model as DeviceConfigurationFile

    App->>CF: EdsToDcf(eds, nodeId: 2, baudrate: 500)
    CF->>CF: Transfer EDS data into new DCF model
    CF->>Model: Copy FileInfo, DeviceInfo, ObjectDictionary
    CF->>Model: Create DeviceCommissioning (NodeId=2, Baudrate=500)

    loop For each object with DefaultValue
        CF->>Model: Set ParameterValue = DefaultValue
    end

    CF-->>App: DeviceConfigurationFile
```

## 6.3 Writing a DCF File

```mermaid
sequenceDiagram
    participant App as Application
    participant CF as CanOpenFile
    participant DW as DcfWriter
    participant VC as ValueConverter

    App->>CF: WriteDcf(dcf, filePath)
    CF->>DW: new DcfWriter(dcf)
    CF->>DW: WriteFile(filePath)
    DW->>DW: GenerateString()

    DW->>DW: WriteFileInfo()
    DW->>DW: WriteDeviceInfo()
    DW->>DW: WriteDeviceCommissioning()
    DW->>DW: WriteDummyUsage()
    DW->>DW: WriteComments()

    loop For each object
        DW->>VC: FormatInteger(), FormatBoolean()
        VC-->>DW: Formatted string
        DW->>DW: WriteObject() / WriteSubObject()
    end

    DW->>DW: WriteSupportedModules()
    DW->>DW: WriteAdditionalSections()
    DW->>DW: File.WriteAllText(filePath, content, ASCII)

    DW-->>CF: void
    CF-->>App: void
```

## 6.4 DCF Round-Trip (Read and Write Back)

```mermaid
sequenceDiagram
    participant App as Application
    participant CF as CanOpenFile

    App->>CF: ReadDcf("device.dcf")
    CF-->>App: DeviceConfigurationFile

    Note over App: Modify values, e.g.<br/>SetParameterValue(0x1017, "500")

    App->>CF: WriteDcf(dcf, "device_modified.dcf")
    CF-->>App: void

    Note over App: Unknown sections are preserved<br/>(round-trip fidelity)
```

## 6.5 Error Handling During Parsing

```mermaid
sequenceDiagram
    participant App as Application
    participant CF as CanOpenFile
    participant ER as EdsReader

    App->>CF: ReadEds("invalid.eds")
    CF->>ER: Read(content)
    ER->>ER: Required section missing or invalid value

    ER--xCF: EdsParseException (LineNumber, SectionName)
    CF--xApp: EdsParseException

    Note over App: Application can use LineNumber and<br/>SectionName for diagnostics
```
