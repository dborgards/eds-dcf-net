# EdsDcfNet.Tests

Comprehensive unit and integration tests for the EdsDcfNet library using XUnit and FluentAssertions.

## Test Structure

### Unit Tests

#### Utilities/ValueConverterTests.cs
Tests for the `ValueConverter` utility class:
- Parsing integers (decimal, hexadecimal, octal formats)
- Parsing $NODEID formulas with arithmetic operations
- Parsing booleans (1/0, true/false, yes/no)
- Parsing bytes and ushort values
- Parsing and converting AccessType enum values
- Formatting integers and booleans for output
- Round-trip conversion tests

#### Parsers/IniParserTests.cs
Tests for the `IniParser` class:
- Parsing INI sections and key-value pairs
- Handling comments and empty lines
- Case-insensitive section and key names
- Parsing whitespace and special characters
- Error handling for malformed content
- Helper methods (GetValue, HasSection, GetKeys)

#### Parsers/EdsReaderTests.cs
Tests for the `EdsReader` class:
- Reading EDS files from disk and strings
- Parsing FileInfo section
- Parsing DeviceInfo section with baud rates
- Parsing ObjectDictionary (mandatory, optional, manufacturer objects)
- Parsing objects with sub-objects
- Parsing all AccessType values
- Parsing Comments section
- Integration test with sample_device.eds

#### Extensions/ObjectDictionaryExtensionsTests.cs
Tests for `ObjectDictionary` extension methods:
- GetObject and GetSubObject
- SetParameterValue for objects and sub-objects
- GetParameterValue (configured vs default values)
- GetObjectsByType (filtering by category)
- GetPdoCommunicationParameters (RPDO/TPDO)
- GetPdoMappingParameters (RPDO/TPDO)

#### Writers/DcfWriterTests.cs
Tests for the `DcfWriter` class:
- Generating DCF content as strings
- Writing FileInfo, DeviceInfo, DeviceCommissioning sections
- Writing object lists and objects
- Writing objects with sub-objects
- Formatting hexadecimal values
- Formatting AccessType values
- Writing Comments section
- Writing to files with error handling

### Integration Tests

#### Integration/CanOpenFileTests.cs
Tests for the `CanOpenFile` API:
- ReadEds and ReadEdsFromString
- ReadDcf and ReadDcfFromString
- WriteDcf and WriteDcfToString
- EdsToDcf conversion with commissioning parameters
- Round-trip tests (EDS → DCF → String → DCF)
- Data preservation through conversions

## Running Tests

### Using .NET CLI

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity normal

# Run specific test class
dotnet test --filter "FullyQualifiedName~ValueConverterTests"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Using Visual Studio
1. Open the solution in Visual Studio
2. Open Test Explorer (Test → Test Explorer)
3. Click "Run All" to execute all tests

### Using Visual Studio Code
1. Install the .NET Core Test Explorer extension
2. Tests will appear in the Test Explorer sidebar
3. Click the play button to run tests

## Test Coverage

The test suite provides comprehensive coverage of:
- ✅ All value parsing and formatting functions
- ✅ INI file parsing with various edge cases
- ✅ EDS file reading and structure parsing
- ✅ DCF file writing and formatting
- ✅ Object dictionary manipulation
- ✅ Main API entry points
- ✅ Round-trip conversion scenarios
- ✅ Error handling and validation

## Test Fixtures

The `Fixtures/` directory contains:
- `sample_device.eds` - Sample CANopen Electronic Data Sheet used for integration tests

## Dependencies

- **xunit** (v2.9.3) - Test framework
- **FluentAssertions** (v7.2.1) - Fluent assertion library
- **Microsoft.NET.Test.Sdk** (v18.0.1) - Test platform
- **coverlet.collector** (v8.0.0) - Code coverage collector

## Conventions

- Test class names end with `Tests` (e.g., `ValueConverterTests`)
- Test method names follow the pattern: `MethodName_Scenario_ExpectedBehavior`
- FluentAssertions is used for all assertions for better readability
- Arrange-Act-Assert (AAA) pattern is used consistently
- Each test is independent and doesn't rely on test execution order

## Examples

```csharp
// Example test structure
[Fact]
public void ParseInteger_HexadecimalValue_ParsesCorrectly()
{
    // Arrange
    var input = "0xFF";

    // Act
    var result = ValueConverter.ParseInteger(input);

    // Assert
    result.Should().Be(255);
}
```

## Contributing

When adding new features to the library:
1. Add corresponding unit tests for new functionality
2. Update integration tests if the API changes
3. Ensure all tests pass before submitting changes
4. Aim for high test coverage of new code
