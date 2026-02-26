# 10. Quality Requirements

## 10.1 Quality Tree

```mermaid
mindmap
  root((Quality))
    Correctness
      Specification compliance (CiA DS 306 + CiA 311 subset)
      Number format processing
      Round-trip fidelity
      INI/XML format interoperability
    Portability
      netstandard2.0
      net10.0
      Platform independence
    Usability
      Simple API
      XML documentation
      Meaningful error messages
    Reliability
      Robust parsing
      Fault tolerance for optional fields
      Deterministic output
    Maintainability
      Clear layered architecture
      Test coverage
      Conventions
    Independence
      Zero dependencies
      No platform-specific dependencies
```

## 10.2 Quality Scenarios

### Scenario 1: Specification Compliance

| Aspect           | Description                                                                |
|------------------|----------------------------------------------------------------------------|
| **Stimulus**     | A valid DS 306 (EDS/DCF/CPJ) or supported CiA 311 (XDD/XDC) file is read. |
| **Environment**  | Regular library operation.                                                 |
| **Response**     | Mapped sections and fields are correctly interpreted into typed models.    |
| **Metric**       | All currently implemented sections/features pass automated parser/writer and integration tests. |

### Scenario 2: Platform Compatibility

| Aspect           | Description                                                                |
|------------------|----------------------------------------------------------------------------|
| **Stimulus**     | The library is referenced in a .NET Framework 4.6.1 project.               |
| **Environment**  | Windows development machine with Visual Studio.                            |
| **Response**     | Compilation and execution without errors.                                  |
| **Metric**       | Successful compilation against `netstandard2.0` without warnings.          |

### Scenario 3: Culture Independence

| Aspect           | Description                                                                |
|------------------|----------------------------------------------------------------------------|
| **Stimulus**     | A CANopen INI/XML file with numeric values is read on a system with German culture. |
| **Environment**  | Operating system with `de-DE` as default culture.                          |
| **Response**     | Values are parsed correctly, no culture-related errors.                    |
| **Metric**       | Identical results regardless of system culture.                            |

### Scenario 4: Round-Trip Fidelity

| Aspect           | Description                                                                |
|------------------|----------------------------------------------------------------------------|
| **Stimulus**     | A DCF file with vendor-specific sections is read and written back unchanged. |
| **Environment**  | Regular library operation.                                                 |
| **Response**     | Vendor-specific sections are preserved in the output file.                 |
| **Metric**       | No information loss in `AdditionalSections`.                               |

### Scenario 5: Meaningful Error Messages

| Aspect           | Description                                                                |
|------------------|----------------------------------------------------------------------------|
| **Stimulus**     | An EDS file with an invalid value in section `[1000]` is read.             |
| **Environment**  | Regular library operation.                                                 |
| **Response**     | `EdsParseException` is thrown with `SectionName = "1000"` and optionally `LineNumber`. |
| **Metric**       | Errors can be localized within 30 seconds.                                 |

### Scenario 6: XDC Commissioning Validation

| Aspect           | Description                                                                |
|------------------|----------------------------------------------------------------------------|
| **Stimulus**     | An XDC is generated with an invalid commissioning NodeId (e.g., 128).     |
| **Environment**  | Regular library operation.                                                 |
| **Response**     | Writing fails fast with a clear exception before output is persisted.      |
| **Metric**       | Invalid NodeId is rejected in all writer test cases.                       |

## 10.3 Test Coverage

Quality is ensured through automated tests:

| Test Category            | Test Class                       | Focus                                             |
|--------------------------|----------------------------------|---------------------------------------------------|
| **Unit tests**           | `ValueConverterTests`            | Number formats, booleans, AccessType conversion   |
| **Unit tests**           | `IniParserTests`                 | INI parsing, sections, comments                   |
| **Unit tests**           | `EdsReaderTests`                 | EDS sections, ObjectDictionary                    |
| **Unit tests**           | `DcfReaderTests`                 | DCF-specific sections                             |
| **Unit tests**           | `DcfWriterTests`                 | DCF output and formatting                         |
| **Unit tests**           | `ModelInitializationTests`       | Model defaults and instantiation                  |
| **Unit tests**           | `ObjectDictionaryExtensionsTests`| Extension method behavior                         |
| **Unit tests**           | `EdsParseExceptionTests`         | Exception constructors and properties             |
| **Unit tests**           | `DcfWriteExceptionTests`         | Exception constructors and properties             |
| **Unit tests**           | `CpjReaderTests`                 | CPJ topology parsing, multi-network, node IDs     |
| **Unit tests**           | `CpjWriterTests`                 | CPJ output, round-trip fidelity                   |
| **Unit tests**           | `XddReaderTests`                 | XDD XML profile parsing                            |
| **Unit tests**           | `XdcReaderTests`                 | XDC XML parsing incl. commissioning/actual values |
| **Unit tests**           | `XddWriterTests`                 | XDD XML generation                                 |
| **Unit tests**           | `XdcWriterTests`                 | XDC XML generation and NodeId validation           |
| **Integration tests**    | `CanOpenFileTests`               | End-to-end: read file, verify model               |
| **Integration tests**    | `RoundTripDcfTests`              | Read -> write -> read again                       |
| **Integration tests**    | `CpjIntegrationTests`            | CPJ end-to-end via CanOpenFile facade              |
| **Integration tests**    | `XddXdcIntegrationTests`         | XDD/XDC round-trip and cross-format conversion     |
