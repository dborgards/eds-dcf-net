namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using AwesomeAssertions;
using Xunit;

public partial class EdsReaderTests
{
    #region DynamicChannels Tests

    [Fact]
    public void ReadString_DynamicChannels_ParsesSegments()
    {
        // Arrange
        var content = @"
[FileInfo]
FileName=test.eds
[DeviceInfo]
VendorName=Test
VendorNumber=1
ProductName=Test
ProductNumber=1
RevisionNumber=1
OrderCode=Test
BaudRate_10=0
BaudRate_20=0
BaudRate_50=0
BaudRate_125=1
BaudRate_250=1
BaudRate_500=1
BaudRate_800=0
BaudRate_1000=0
SimpleBootUpMaster=0
SimpleBootUpSlave=1
Granularity=8
DynamicChannelsSupported=2
GroupMessaging=0
NrOfRXPDO=0
NrOfTXPDO=0
LSS_Supported=0
[MandatoryObjects]
SupportedObjects=2
1=0x1000
2=0x1001
[1000]
ParameterName=Device Type
ObjectType=0x7
DataType=0x0007
AccessType=ro
[1001]
ParameterName=Error Register
ObjectType=0x7
DataType=0x0005
AccessType=ro
[DynamicChannels]
NrOfSeg=2
Type1=0x0007
Dir1=ro
Range1=0xA080-0xA0BF
PPOffset1=0
Type2=0x0005
Dir2=rww
Range2=0xA0C0-0xA0FF
PPOffset2=64
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.DynamicChannels.Should().NotBeNull();
        result.DynamicChannels!.Segments.Should().HaveCount(2);

        result.DynamicChannels.Segments[0].Type.Should().Be(0x0007);
        result.DynamicChannels.Segments[0].Dir.Should().Be(AccessType.ReadOnly);
        result.DynamicChannels.Segments[0].Range.Should().Be("0xA080-0xA0BF");
        result.DynamicChannels.Segments[0].PPOffset.Should().Be(0);

        result.DynamicChannels.Segments[1].Type.Should().Be(0x0005);
        result.DynamicChannels.Segments[1].Dir.Should().Be(AccessType.ReadWriteOutput);
        result.DynamicChannels.Segments[1].Range.Should().Be("0xA0C0-0xA0FF");
        result.DynamicChannels.Segments[1].PPOffset.Should().Be(64);
    }

    [Fact]
    public void ReadString_NoDynamicChannels_ReturnsNull()
    {
        // Arrange
        var content = @"
[FileInfo]
FileName=test.eds
[DeviceInfo]
VendorName=Test
VendorNumber=1
ProductName=Test
ProductNumber=1
RevisionNumber=1
OrderCode=Test
BaudRate_10=0
BaudRate_20=0
BaudRate_50=0
BaudRate_125=1
BaudRate_250=1
BaudRate_500=1
BaudRate_800=0
BaudRate_1000=0
SimpleBootUpMaster=0
SimpleBootUpSlave=1
Granularity=8
DynamicChannelsSupported=0
GroupMessaging=0
NrOfRXPDO=0
NrOfTXPDO=0
LSS_Supported=0
[MandatoryObjects]
SupportedObjects=2
1=0x1000
2=0x1001
[1000]
ParameterName=Device Type
ObjectType=0x7
DataType=0x0007
AccessType=ro
[1001]
ParameterName=Error Register
ObjectType=0x7
DataType=0x0005
AccessType=ro
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.DynamicChannels.Should().BeNull();
    }

    [Fact]
    public void ReadString_DynamicChannelsZeroSegments_ReturnsNull()
    {
        // Arrange
        var content = @"
[FileInfo]
FileName=test.eds
[DeviceInfo]
VendorName=Test
VendorNumber=1
ProductName=Test
ProductNumber=1
RevisionNumber=1
OrderCode=Test
BaudRate_10=0
BaudRate_20=0
BaudRate_50=0
BaudRate_125=1
BaudRate_250=1
BaudRate_500=1
BaudRate_800=0
BaudRate_1000=0
SimpleBootUpMaster=0
SimpleBootUpSlave=1
Granularity=8
DynamicChannelsSupported=0
GroupMessaging=0
NrOfRXPDO=0
NrOfTXPDO=0
LSS_Supported=0
[MandatoryObjects]
SupportedObjects=2
1=0x1000
2=0x1001
[1000]
ParameterName=Device Type
ObjectType=0x7
DataType=0x0007
AccessType=ro
[1001]
ParameterName=Error Register
ObjectType=0x7
DataType=0x0005
AccessType=ro
[DynamicChannels]
NrOfSeg=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.DynamicChannels.Should().BeNull();
    }

    #endregion

    #region Object Type Tests

    [Fact]
    public void ReadString_ObjectType_Null_ParsedCorrectly()
    {
        // Arrange - ObjectType 0x0 (NULL) has no data type or access type
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=1
1=0x1000

[1000]
ParameterName=Null Object
ObjectType=0x0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x1000];
        obj.ObjectType.Should().Be(0x0);
        obj.ParameterName.Should().Be("Null Object");
        obj.SubObjects.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_ObjectType_Domain_ParsedCorrectly()
    {
        // Arrange - ObjectType 0x2 (DOMAIN) for arbitrary binary data
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=1
1=0x1F50

[1F50]
ParameterName=Program Data
ObjectType=0x2
DataType=0x000F
AccessType=rw
PDOMapping=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x1F50];
        obj.ObjectType.Should().Be(0x2);
        obj.ParameterName.Should().Be("Program Data");
        obj.SubObjects.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_ObjectType_DefType_ParsedCorrectly()
    {
        // Arrange - ObjectType 0x5 (DEFTYPE) for data type definitions
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=1
1=0x0005

[5]
ParameterName=UNSIGNED8
ObjectType=0x5
DataType=0x0005
AccessType=ro
PDOMapping=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x0005];
        obj.ObjectType.Should().Be(0x5);
        obj.ParameterName.Should().Be("UNSIGNED8");
        obj.SubObjects.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_ObjectType_DefStruct_WithSubObjects_ParsedCorrectly()
    {
        // Arrange - ObjectType 0x6 (DEFSTRUCT) can have sub-objects
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=1
1=0x0020

[20]
ParameterName=PDO Communication Parameter
ObjectType=0x6
SubNumber=2

[20sub0]
ParameterName=Number of Entries
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=2
PDOMapping=0

[20sub1]
ParameterName=COB-ID
ObjectType=0x7
DataType=0x0007
AccessType=rw
PDOMapping=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x0020];
        obj.ObjectType.Should().Be(0x6);
        obj.SubObjects.Should().HaveCount(2);
        obj.SubObjects[0].ParameterName.Should().Be("Number of Entries");
        obj.SubObjects[1].ParameterName.Should().Be("COB-ID");
    }

    [Fact]
    public void ReadString_ObjectType_DefStruct_WithoutSubNumber_ParsesSubObjects()
    {
        // Arrange - DEFSTRUCT (0x6) should trigger sub-object parsing even without SubNumber
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=1
1=0x0020

[20]
ParameterName=PDO Comm Param
ObjectType=0x6

[20sub0]
ParameterName=Number of Entries
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x0020];
        obj.ObjectType.Should().Be(0x6);
        // SubNumber not set, but sub-objects should still be found because ObjectType triggers parsing
        // Note: without SubNumber the parser scans from 0 to maxSubIndex (0),
        // so only sub0 can be discovered
        obj.SubObjects.Should().ContainKey(0);
    }

    #endregion

    #region SupportedModules Tests

    [Fact]
    public void ReadString_SupportedModules_ParsesModulesCorrectly()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=0

[SupportedModules]
NrOfEntries=2

[M1ModuleInfo]
ProductName=Input Module
ProductVersion=1
ProductRevision=0
OrderCode=MOD-IN-8

[M2ModuleInfo]
ProductName=Output Module
ProductVersion=2
ProductRevision=1
OrderCode=MOD-OUT-4
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.SupportedModules.Should().HaveCount(2);

        result.SupportedModules[0].ModuleNumber.Should().Be(1);
        result.SupportedModules[0].ProductName.Should().Be("Input Module");
        result.SupportedModules[0].ProductVersion.Should().Be(1);
        result.SupportedModules[0].ProductRevision.Should().Be(0);
        result.SupportedModules[0].OrderCode.Should().Be("MOD-IN-8");

        result.SupportedModules[1].ModuleNumber.Should().Be(2);
        result.SupportedModules[1].ProductName.Should().Be("Output Module");
        result.SupportedModules[1].ProductVersion.Should().Be(2);
        result.SupportedModules[1].ProductRevision.Should().Be(1);
    }

    [Fact]
    public void ReadString_SupportedModules_WithFixedObjects_ParsedCorrectly()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=0

[SupportedModules]
NrOfEntries=1

[M1ModuleInfo]
ProductName=IO Module
ProductVersion=1
ProductRevision=0
OrderCode=MOD-IO

[M1FixedObjects]
NrOfEntries=2
1=0x6000
2=0x6200
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.SupportedModules.Should().HaveCount(1);
        result.SupportedModules[0].FixedObjects.Should().HaveCount(2);
        result.SupportedModules[0].FixedObjects.Should().Contain((ushort)0x6000);
        result.SupportedModules[0].FixedObjects.Should().Contain((ushort)0x6200);
    }

    [Fact]
    public void ReadString_SupportedModules_MissingModuleSection_SkipsEntry()
    {
        // Arrange — NrOfEntries says 2 but M2ModuleInfo is missing
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=0

[SupportedModules]
NrOfEntries=2

[M1ModuleInfo]
ProductName=Only Module
ProductVersion=1
ProductRevision=0
OrderCode=ONLY
";

        // Act
        var result = _reader.ReadString(content);

        // Assert — only M1 parsed, M2 skipped
        result.SupportedModules.Should().HaveCount(1);
        result.SupportedModules[0].ProductName.Should().Be("Only Module");
    }

    [Fact]
    public void ReadString_SupportedModules_SectionsNotInAdditionalSections()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=0

[SupportedModules]
NrOfEntries=1

[M1ModuleInfo]
ProductName=Test Module
ProductVersion=1
ProductRevision=0
OrderCode=TEST

[M1FixedObjects]
NrOfEntries=1
1=0x6000
";

        // Act
        var result = _reader.ReadString(content);

        // Assert — module sections should NOT be in AdditionalSections
        result.AdditionalSections.Should().NotContainKey("SupportedModules");
        result.AdditionalSections.Should().NotContainKey("M1ModuleInfo");
        result.AdditionalSections.Should().NotContainKey("M1FixedObjects");
    }

    [Fact]
    public void ReadString_NoSupportedModules_ReturnsEmptyList()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.SupportedModules.Should().BeEmpty();
    }

    #endregion
}
