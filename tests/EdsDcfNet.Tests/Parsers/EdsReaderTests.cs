namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using FluentAssertions;
using Xunit;

public class EdsReaderTests
{
    private readonly EdsReader _reader = new();

    #region ReadFile Tests

    [Fact]
    public void ReadFile_ValidEdsFile_ParsesSuccessfully()
    {
        // Arrange
        var filePath = "Fixtures/sample_device.eds";

        // Act
        var result = _reader.ReadFile(filePath);

        // Assert
        result.Should().NotBeNull();
        result.FileInfo.Should().NotBeNull();
        result.DeviceInfo.Should().NotBeNull();
        result.ObjectDictionary.Should().NotBeNull();
    }

    [Fact]
    public void ReadFile_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var filePath = "NonExistent.eds";

        // Act
        var act = () => _reader.ReadFile(filePath);

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    #endregion

    #region ReadString Tests

    [Fact]
    public void ReadString_ValidEdsContent_ParsesSuccessfully()
    {
        // Arrange
        var content = @"
[FileInfo]
FileName=test.eds
FileVersion=1
FileRevision=0
EDSVersion=4.0

[DeviceInfo]
VendorName=Test Vendor
VendorNumber=0x100
ProductName=Test Product
ProductNumber=0x1001

[DummyUsage]
Dummy0002=1

[MandatoryObjects]
SupportedObjects=1
1=0x1000

[1000]
ParameterName=Device Type
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0x00000191
PDOMapping=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.Should().NotBeNull();
        result.FileInfo.FileName.Should().Be("test.eds");
        result.DeviceInfo.VendorName.Should().Be("Test Vendor");
    }

    [Fact]
    public void ReadString_MissingDeviceInfo_ThrowsException()
    {
        // Arrange
        var content = @"
[FileInfo]
FileName=test.eds
";

        // Act
        var act = () => _reader.ReadString(content);

        // Assert
        act.Should().Throw<EdsParseException>()
            .WithMessage("*DeviceInfo*");
    }

    #endregion

    #region FileInfo Parsing Tests

    [Fact]
    public void ReadString_FileInfo_ParsesAllFields()
    {
        // Arrange
        var content = @"
[FileInfo]
FileName=test.eds
FileVersion=2
FileRevision=5
EDSVersion=4.0
Description=Test Description
CreationTime=10:30AM
CreationDate=01-15-2025
CreatedBy=Test User
ModificationTime=11:00AM
ModificationDate=01-16-2025
ModifiedBy=Another User

[DeviceInfo]
VendorName=Test

[DummyUsage]
Dummy0002=1

[MandatoryObjects]
SupportedObjects=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.FileInfo.FileName.Should().Be("test.eds");
        result.FileInfo.FileVersion.Should().Be(2);
        result.FileInfo.FileRevision.Should().Be(5);
        result.FileInfo.EdsVersion.Should().Be("4.0");
        result.FileInfo.Description.Should().Be("Test Description");
        result.FileInfo.CreationTime.Should().Be("10:30AM");
        result.FileInfo.CreationDate.Should().Be("01-15-2025");
        result.FileInfo.CreatedBy.Should().Be("Test User");
        result.FileInfo.ModificationTime.Should().Be("11:00AM");
        result.FileInfo.ModificationDate.Should().Be("01-16-2025");
        result.FileInfo.ModifiedBy.Should().Be("Another User");
    }

    [Fact]
    public void ReadString_MissingFileInfo_UsesDefaults()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test

[DummyUsage]
Dummy0002=1

[MandatoryObjects]
SupportedObjects=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.FileInfo.Should().NotBeNull();
        result.FileInfo.FileVersion.Should().Be(1);
        result.FileInfo.FileRevision.Should().Be(0);
        result.FileInfo.EdsVersion.Should().Be("4.0");
    }

    #endregion

    #region DeviceInfo Parsing Tests

    [Fact]
    public void ReadString_DeviceInfo_ParsesAllFields()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Example Automation Inc.
VendorNumber=0x00000100
ProductName=IO-Module 16x16
ProductNumber=0x00001001
RevisionNumber=0x00010000
OrderCode=IO-16X16-001
BaudRate_10=1
BaudRate_20=1
BaudRate_50=1
BaudRate_125=1
BaudRate_250=1
BaudRate_500=1
BaudRate_800=0
BaudRate_1000=1
SimpleBootUpMaster=0
SimpleBootUpSlave=1
Granularity=8
DynamicChannelsSupported=0
GroupMessaging=0
NrOfRXPDO=4
NrOfTXPDO=4
LSS_Supported=0

[DummyUsage]
Dummy0002=1

[MandatoryObjects]
SupportedObjects=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.DeviceInfo.VendorName.Should().Be("Example Automation Inc.");
        result.DeviceInfo.VendorNumber.Should().Be(0x00000100);
        result.DeviceInfo.ProductName.Should().Be("IO-Module 16x16");
        result.DeviceInfo.ProductNumber.Should().Be(0x00001001);
        result.DeviceInfo.RevisionNumber.Should().Be(0x00010000);
        result.DeviceInfo.OrderCode.Should().Be("IO-16X16-001");
        result.DeviceInfo.SimpleBootUpMaster.Should().BeFalse();
        result.DeviceInfo.SimpleBootUpSlave.Should().BeTrue();
        result.DeviceInfo.Granularity.Should().Be(8);
        result.DeviceInfo.DynamicChannelsSupported.Should().Be(0);
        result.DeviceInfo.GroupMessaging.Should().BeFalse();
        result.DeviceInfo.NrOfRxPdo.Should().Be(4);
        result.DeviceInfo.NrOfTxPdo.Should().Be(4);
        result.DeviceInfo.LssSupported.Should().BeFalse();
    }

    [Fact]
    public void ReadString_DeviceInfo_ParsesBaudRates()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test
BaudRate_10=1
BaudRate_20=0
BaudRate_50=1
BaudRate_125=1
BaudRate_250=1
BaudRate_500=1
BaudRate_800=0
BaudRate_1000=1

[DummyUsage]
Dummy0002=1

[MandatoryObjects]
SupportedObjects=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.DeviceInfo.SupportedBaudRates.BaudRate10.Should().BeTrue();
        result.DeviceInfo.SupportedBaudRates.BaudRate20.Should().BeFalse();
        result.DeviceInfo.SupportedBaudRates.BaudRate50.Should().BeTrue();
        result.DeviceInfo.SupportedBaudRates.BaudRate125.Should().BeTrue();
        result.DeviceInfo.SupportedBaudRates.BaudRate250.Should().BeTrue();
        result.DeviceInfo.SupportedBaudRates.BaudRate500.Should().BeTrue();
        result.DeviceInfo.SupportedBaudRates.BaudRate800.Should().BeFalse();
        result.DeviceInfo.SupportedBaudRates.BaudRate1000.Should().BeTrue();
    }

    [Fact]
    public void ReadString_DeviceInfo_CANopenSafetySupported_ParsedCorrectly()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test
CANopenSafetySupported=1

[MandatoryObjects]
SupportedObjects=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.DeviceInfo.CANopenSafetySupported.Should().BeTrue();
    }

    [Fact]
    public void ReadString_DeviceInfo_CANopenSafetySupported_DefaultsFalse()
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
        result.DeviceInfo.CANopenSafetySupported.Should().BeFalse();
    }

    #endregion

    #region Object Dictionary Parsing Tests

    [Fact]
    public void ReadString_ObjectDictionary_ParsesMandatoryObjects()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test

[DummyUsage]
Dummy0002=1

[MandatoryObjects]
SupportedObjects=2
1=0x1000
2=0x1001

[1000]
ParameterName=Device Type
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0x00000191
PDOMapping=0

[1001]
ParameterName=Error Register
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=1
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ObjectDictionary.MandatoryObjects.Should().HaveCount(2);
        result.ObjectDictionary.MandatoryObjects.Should().Contain(0x1000);
        result.ObjectDictionary.MandatoryObjects.Should().Contain(0x1001);
        result.ObjectDictionary.Objects.Should().ContainKey(0x1000);
        result.ObjectDictionary.Objects.Should().ContainKey(0x1001);
    }

    [Fact]
    public void ReadString_ObjectDictionary_ParsesOptionalObjects()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test

[DummyUsage]
Dummy0002=1

[MandatoryObjects]
SupportedObjects=0

[OptionalObjects]
SupportedObjects=2
1=0x1008
2=0x1009

[1008]
ParameterName=Manufacturer Device Name
ObjectType=0x7
DataType=0x0009
AccessType=ro
DefaultValue=Test Device
PDOMapping=0

[1009]
ParameterName=Manufacturer Hardware Version
ObjectType=0x7
DataType=0x0009
AccessType=ro
DefaultValue=1.0
PDOMapping=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ObjectDictionary.OptionalObjects.Should().HaveCount(2);
        result.ObjectDictionary.OptionalObjects.Should().Contain(0x1008);
        result.ObjectDictionary.OptionalObjects.Should().Contain(0x1009);
    }

    [Fact]
    public void ReadString_Object_ParsesAllProperties()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test

[DummyUsage]
Dummy0002=1

[MandatoryObjects]
SupportedObjects=1
1=0x1000

[1000]
ParameterName=Device Type
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0x00000191
PDOMapping=0
LowLimit=0
HighLimit=0xFFFFFFFF
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x1000];
        obj.Index.Should().Be(0x1000);
        obj.ParameterName.Should().Be("Device Type");
        obj.ObjectType.Should().Be(0x7);
        obj.DataType.Should().Be(0x0007);
        obj.AccessType.Should().Be(AccessType.ReadOnly);
        obj.DefaultValue.Should().Be("0x00000191");
        obj.PdoMapping.Should().BeFalse();
        obj.LowLimit.Should().Be("0");
        obj.HighLimit.Should().Be("0xFFFFFFFF");
    }

    [Fact]
    public void ReadString_ObjectWithSubObjects_ParsesCorrectly()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test

[DummyUsage]
Dummy0002=1
Dummy0005=1

[MandatoryObjects]
SupportedObjects=1
1=0x1018

[1018]
ParameterName=Identity Object
SubNumber=2
ObjectType=0x9

[1018sub0]
ParameterName=Number of Entries
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=2
PDOMapping=0

[1018sub1]
ParameterName=Vendor ID
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0x00000100
PDOMapping=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x1018];
        obj.SubNumber.Should().Be(2);
        obj.SubObjects.Should().ContainKey(0);
        obj.SubObjects.Should().ContainKey(1);
        obj.SubObjects[0].ParameterName.Should().Be("Number of Entries");
        obj.SubObjects[1].ParameterName.Should().Be("Vendor ID");
    }

    [Fact]
    public void ReadString_AccessTypes_ParsesAllTypes()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test

[DummyUsage]
Dummy0002=1
Dummy0003=1
Dummy0004=1
Dummy0005=1
Dummy0006=1
Dummy0007=1

[MandatoryObjects]
SupportedObjects=6
1=0x2000
2=0x2001
3=0x2002
4=0x2003
5=0x2004
6=0x2005

[2000]
ParameterName=ReadOnly Object
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=0

[2001]
ParameterName=WriteOnly Object
ObjectType=0x7
DataType=0x0005
AccessType=wo
DefaultValue=0
PDOMapping=0

[2002]
ParameterName=ReadWrite Object
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0

[2003]
ParameterName=ReadWriteInput Object
ObjectType=0x7
DataType=0x0005
AccessType=rwr
DefaultValue=0
PDOMapping=0

[2004]
ParameterName=ReadWriteOutput Object
ObjectType=0x7
DataType=0x0005
AccessType=rww
DefaultValue=0
PDOMapping=0

[2005]
ParameterName=Constant Object
ObjectType=0x7
DataType=0x0005
AccessType=const
DefaultValue=0
PDOMapping=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ObjectDictionary.Objects[0x2000].AccessType.Should().Be(AccessType.ReadOnly);
        result.ObjectDictionary.Objects[0x2001].AccessType.Should().Be(AccessType.WriteOnly);
        result.ObjectDictionary.Objects[0x2002].AccessType.Should().Be(AccessType.ReadWrite);
        result.ObjectDictionary.Objects[0x2003].AccessType.Should().Be(AccessType.ReadWriteInput);
        result.ObjectDictionary.Objects[0x2004].AccessType.Should().Be(AccessType.ReadWriteOutput);
        result.ObjectDictionary.Objects[0x2005].AccessType.Should().Be(AccessType.Constant);
    }

    [Fact]
    public void ReadString_Object_SafetyProperties_ParsedCorrectly()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=0

[OptionalObjects]
SupportedObjects=1
1=0x6100

[6100]
ParameterName=SRDO Input
ObjectType=0x8
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=1
SRDOMapping=1
InvertedSRAD=0x610101
SubNumber=1

[6100sub0]
ParameterName=Number of Entries
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0
SRDOMapping=0

[6100sub1]
ParameterName=SRDO Input 1
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=1
SRDOMapping=1
InvertedSRAD=0x610101
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x6100];
        obj.SrdoMapping.Should().BeTrue();
        obj.InvertedSrad.Should().Be("0x610101");

        obj.SubObjects[0].SrdoMapping.Should().BeFalse();
        obj.SubObjects[0].InvertedSrad.Should().BeNullOrEmpty();

        obj.SubObjects[1].SrdoMapping.Should().BeTrue();
        obj.SubObjects[1].InvertedSrad.Should().Be("0x610101");
    }

    [Fact]
    public void ReadString_Object_SafetyProperties_DefaultValues()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=1
1=0x1000

[1000]
ParameterName=Device Type
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0x191
PDOMapping=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x1000];
        obj.SrdoMapping.Should().BeFalse();
        obj.InvertedSrad.Should().BeNullOrEmpty();
    }

    #endregion

    #region Comments Parsing Tests

    [Fact]
    public void ReadString_Comments_ParsesCorrectly()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test

[DummyUsage]
Dummy0002=1

[MandatoryObjects]
SupportedObjects=0

[Comments]
Lines=3
Line1=First comment line
Line2=Second comment line
Line3=Third comment line
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.Comments.Should().NotBeNull();
        result.Comments!.Lines.Should().Be(3);
        result.Comments.CommentLines.Should().HaveCount(3);
        result.Comments.CommentLines[1].Should().Be("First comment line");
        result.Comments.CommentLines[2].Should().Be("Second comment line");
        result.Comments.CommentLines[3].Should().Be("Third comment line");
    }

    #endregion

    #region Sample File Integration Test

    [Fact]
    public void ReadFile_SampleDeviceEds_ParsesCompletely()
    {
        // Arrange
        var filePath = "Fixtures/sample_device.eds";

        // Act
        var result = _reader.ReadFile(filePath);

        // Assert - FileInfo
        result.FileInfo.FileName.Should().Be("sample_device.eds");
        result.FileInfo.FileVersion.Should().Be(1);
        result.FileInfo.FileRevision.Should().Be(0);
        result.FileInfo.EdsVersion.Should().Be("4.0");

        // Assert - DeviceInfo
        result.DeviceInfo.VendorName.Should().Be("Example Automation Inc.");
        result.DeviceInfo.ProductName.Should().Be("IO-Module 16x16");
        result.DeviceInfo.VendorNumber.Should().Be(0x00000100);
        result.DeviceInfo.ProductNumber.Should().Be(0x00001001);

        // Assert - ObjectDictionary
        result.ObjectDictionary.MandatoryObjects.Should().Contain(0x1000);
        result.ObjectDictionary.MandatoryObjects.Should().Contain(0x1001);
        result.ObjectDictionary.OptionalObjects.Should().Contain(0x1008);
        result.ObjectDictionary.OptionalObjects.Should().Contain(0x1018);

        // Assert - Specific Objects
        result.ObjectDictionary.Objects[0x1000].ParameterName.Should().Be("Device Type");
        result.ObjectDictionary.Objects[0x1001].ParameterName.Should().Be("Error Register");

        // Assert - Comments
        result.Comments.Should().NotBeNull();
        result.Comments!.Lines.Should().Be(3);
        result.Comments.CommentLines[1].Should().Contain("Sample EDS file");
    }

    #endregion

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

    #region Tools Tests

    [Fact]
    public void ReadString_ToolsSections_ParsesTools()
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
[Tools]
Items=2
[Tool1]
Name=EDS Checker
Command=checker.exe $EDS
[Tool2]
Name=Configurator
Command=config.exe $DCF $NODEID
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.Tools.Should().HaveCount(2);
        result.Tools[0].Name.Should().Be("EDS Checker");
        result.Tools[0].Command.Should().Be("checker.exe $EDS");
        result.Tools[1].Name.Should().Be("Configurator");
        result.Tools[1].Command.Should().Be("config.exe $DCF $NODEID");
    }

    [Fact]
    public void ReadString_NoToolsSection_ReturnsEmptyList()
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
        result.Tools.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_Tools_MissingToolSection_SkipsEntry()
    {
        // Arrange — Tools says Items=2 but Tool2 section is missing
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
[Tools]
Items=2
[Tool1]
Name=Checker
Command=check.exe
";

        // Act
        var result = _reader.ReadString(content);

        // Assert — only Tool1 parsed, Tool2 skipped
        result.Tools.Should().HaveCount(1);
        result.Tools[0].Name.Should().Be("Checker");
    }

    [Fact]
    public void ReadString_ToolsSections_NotInAdditionalSections()
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
[Tools]
Items=1
[Tool1]
Name=Checker
Command=check.exe
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.AdditionalSections.Should().NotContainKey("Tools");
        result.AdditionalSections.Should().NotContainKey("Tool1");
    }

    [Fact]
    public void ReadString_ToolSectionWithNonNumericSuffix_PreservedInAdditionalSections()
    {
        // Arrange — "ToolABC" is not a valid ToolX section
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
[Tools]
Items=1
[Tool1]
Name=Checker
Command=check.exe
[ToolABC]
Name=Invalid
Command=invalid.exe
";

        // Act
        var result = _reader.ReadString(content);

        // Assert — ToolABC preserved in AdditionalSections
        result.Tools.Should().HaveCount(1);
        result.AdditionalSections.Should().ContainKey("ToolABC");
    }

    [Fact]
    public void ReadString_SectionNamedTool_PreservedInAdditionalSections()
    {
        // Arrange — section named exactly "Tool" (length 4, no numeric suffix)
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
[Tool]
SomeKey=SomeValue
";

        // Act
        var result = _reader.ReadString(content);

        // Assert — "Tool" is not a known section and not a valid ToolX
        result.AdditionalSections.Should().ContainKey("Tool");
    }

    [Fact]
    public void ReadString_ToolZeroSection_PreservedInAdditionalSections()
    {
        // Arrange — Tool0 has toolNumber < 1
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
[Tools]
Items=1
[Tool1]
Name=Checker
Command=check.exe
[Tool0]
Name=Zero
Command=zero.exe
";

        // Act
        var result = _reader.ReadString(content);

        // Assert — Tool0 (toolNumber < 1) preserved in AdditionalSections
        result.Tools.Should().HaveCount(1);
        result.AdditionalSections.Should().ContainKey("Tool0");
    }

    [Fact]
    public void ReadString_OrphanToolSection_PreservedInAdditionalSections()
    {
        // Arrange — Tool1 exists but no [Tools] section, so it's orphaned
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
[Tool1]
Name=Orphan
Command=orphan.exe
";

        // Act
        var result = _reader.ReadString(content);

        // Assert — orphan Tool1 preserved in AdditionalSections
        result.Tools.Should().BeEmpty();
        result.AdditionalSections.Should().ContainKey("Tool1");
    }

    [Fact]
    public void ReadString_ToolSectionBeyondItems_PreservedInAdditionalSections()
    {
        // Arrange — Items=1 but Tool2 also exists
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
[Tools]
Items=1
[Tool1]
Name=Checker
Command=check.exe
[Tool2]
Name=Extra
Command=extra.exe
";

        // Act
        var result = _reader.ReadString(content);

        // Assert — Tool1 parsed, Tool2 beyond Items preserved in AdditionalSections
        result.Tools.Should().HaveCount(1);
        result.AdditionalSections.Should().NotContainKey("Tool1");
        result.AdditionalSections.Should().ContainKey("Tool2");
    }

    #endregion

    #region Vendor Section Preservation Tests

    [Fact]
    public void ReadString_SectionContainingSub_NotSubObject_PreservedInAdditionalSections()
    {
        // Arrange - "SubSystem" contains "sub" but is NOT a sub-object section (no hex prefix)
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=1
1=0x1000

[1000]
ParameterName=Device Type
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0x191
PDOMapping=0

[SubSystem]
VendorKey=VendorData
";

        // Act
        var result = _reader.ReadString(content);

        // Assert - SubSystem should NOT be swallowed as a known section
        result.AdditionalSections.Should().ContainKey("SubSystem");
        result.AdditionalSections["SubSystem"]["VendorKey"].Should().Be("VendorData");
    }

    [Fact]
    public void ReadString_SectionStartingWithM_NotModule_PreservedInAdditionalSections()
    {
        // Arrange - "Manufacturing" starts with "M" but is NOT a module section
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=1
1=0x1000

[1000]
ParameterName=Device Type
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0x191
PDOMapping=0

[Manufacturing]
SerialFormat=12345
";

        // Act
        var result = _reader.ReadString(content);

        // Assert - Manufacturing should NOT be swallowed as a known section
        result.AdditionalSections.Should().ContainKey("Manufacturing");
        result.AdditionalSections["Manufacturing"]["SerialFormat"].Should().Be("12345");
    }

    [Fact]
    public void ReadString_ValidSubObjectSection_StillRecognized()
    {
        // Arrange - "1018sub1" is a valid sub-object section (hex prefix + "sub" + hex suffix)
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=1
1=0x1018

[1018]
ParameterName=Identity
ObjectType=0x9
SubNumber=2

[1018sub0]
ParameterName=Number of Entries
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0

[1018sub1]
ParameterName=Vendor ID
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0x100
PDOMapping=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert - valid sub-object sections should still be parsed, not in AdditionalSections
        result.ObjectDictionary.Objects[0x1018].SubObjects.Should().ContainKey(1);
        result.AdditionalSections.Should().NotContainKey("1018sub1");
    }

    [Fact]
    public void ReadString_ModuleSectionWithSubExtendSuffix_RecognizedAsKnown()
    {
        // Arrange - "M1SubExtension" has suffix "SubExtension" which StartsWith("SubExtend")
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=0

[SupportedModules]
NrOfEntries=1

[M1ModuleInfo]
ProductName=Module
ProductVersion=1
ProductRevision=0
OrderCode=MOD-1

[M1SubExtension1]
SomeKey=SomeValue
";

        // Act
        var result = _reader.ReadString(content);

        // Assert - M1SubExtension1 is a known module section, NOT in AdditionalSections
        result.AdditionalSections.Should().NotContainKey("M1SubExtension1");
    }

    [Fact]
    public void ReadString_ModuleSectionWithSubExtSuffix_RecognizedAsKnown()
    {
        // Arrange - "M1SubExt1" has suffix "SubExt1" which StartsWith("SubExt") but NOT "SubExtend"
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=0

[SupportedModules]
NrOfEntries=1

[M1ModuleInfo]
ProductName=Module
ProductVersion=1
ProductRevision=0
OrderCode=MOD-1

[M1SubExt1]
SomeKey=SomeValue
";

        // Act
        var result = _reader.ReadString(content);

        // Assert - M1SubExt1 is a known module section, NOT in AdditionalSections
        result.AdditionalSections.Should().NotContainKey("M1SubExt1");
    }

    [Fact]
    public void ReadString_ModuleSectionWithCommentsSuffix_RecognizedAsKnown()
    {
        // Arrange - "M1Comments" has suffix "Comments"
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=0

[SupportedModules]
NrOfEntries=1

[M1ModuleInfo]
ProductName=Module
ProductVersion=1
ProductRevision=0
OrderCode=MOD-1

[M1Comments]
Lines=1
Line1=Module comment
";

        // Act
        var result = _reader.ReadString(content);

        // Assert - M1Comments is a known module section, NOT in AdditionalSections
        result.AdditionalSections.Should().NotContainKey("M1Comments");
    }

    [Fact]
    public void ReadString_ModuleSectionWithUnknownSuffix_PreservedInAdditionalSections()
    {
        // Arrange - "M1UnknownSuffix" has suffix "UnknownSuffix" which matches none of the known
        // module suffixes (ModuleInfo, FixedObjects, SubExtend*, SubExt*, Comments), so
        // IsModuleSection evaluates the Equals("Comments") branch as false and returns false overall.
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=0

[M1UnknownSuffix]
Key=Value
";

        // Act
        var result = _reader.ReadString(content);

        // Assert - M1UnknownSuffix is NOT a known module section, so it lands in AdditionalSections
        result.AdditionalSections.Should().ContainKey("M1UnknownSuffix");
    }

    [Fact]
    public void ReadString_SupportedModules_MissingModuleSection_SkipsEntry()
    {
        // Arrange - NrOfEntries=2 but only M1ModuleInfo exists, not M2ModuleInfo.
        // ParseModuleInfo(sections, 2) hits the HasSection=false path and returns null.
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=0

[SupportedModules]
NrOfEntries=2

[M1ModuleInfo]
ProductName=Module1
ProductVersion=1
ProductRevision=0
OrderCode=MOD-1
";

        // Act
        var result = _reader.ReadString(content);

        // Assert - only module 1 was parsed; module 2 has no section so it is skipped
        result.SupportedModules.Should().HaveCount(1);
        result.SupportedModules[0].ModuleNumber.Should().Be(1);
    }

    [Fact]
    public void ReadString_DummyUsage_NonDummyKey_IsIgnored()
    {
        // Arrange - DummyUsage section contains a key that does NOT start with "Dummy".
        // This covers the StartsWith("Dummy")=false branch in ParseObjectDictionary.
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=0

[DummyUsage]
Dummy0002=1
SupportedObjects=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert - only the valid DummyXXXX entry is in DummyUsage; the other key is ignored
        result.ObjectDictionary.DummyUsage.Should().ContainKey(0x0002);
        result.ObjectDictionary.DummyUsage.Should().HaveCount(1);
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
}
