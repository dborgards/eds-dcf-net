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
}
