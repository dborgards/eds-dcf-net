namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;

public class DcfReaderTests
{
    private readonly DcfReader _reader = new();

    #region Helper

    private static string BuildMinimalDcf(
        string? deviceCommissioningSection = null,
        string? extraSections = null)
    {
        var dc = deviceCommissioningSection ?? @"
[DeviceCommissioning]
NodeID=5
NodeName=TestNode
Baudrate=500
NetNumber=1
NetworkName=TestNetwork
CANopenManager=0";

        return $@"
[FileInfo]
FileName=test.dcf
FileVersion=1
FileRevision=0
EDSVersion=4.0
Description=Test DCF

[DeviceInfo]
VendorName=Test Vendor
VendorNumber=0x100
ProductName=Test Product
ProductNumber=0x1001
RevisionNumber=0x00010000
OrderCode=TEST-001
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
{dc}

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
{extraSections}";
    }

    #endregion

    #region ReadFile Tests

    [Fact]
    public void ReadFile_MinimalDcf_ReturnsDeviceConfigurationFile()
    {
        // Arrange
        var filePath = "Fixtures/minimal.dcf";

        // Act
        var result = _reader.ReadFile(filePath);

        // Assert
        result.Should().NotBeNull();
        result.FileInfo.FileName.Should().Be("minimal.dcf");
        result.DeviceCommissioning.NodeId.Should().Be(5);
        result.DeviceCommissioning.Baudrate.Should().Be(500);
    }

    [Fact]
    public void ReadFile_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Act
        var act = () => _reader.ReadFile("NonExistent.dcf");

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void ReadFile_FullFeaturesDcf_ParsesAllSections()
    {
        // Arrange
        var filePath = "Fixtures/full_features.dcf";

        // Act
        var result = _reader.ReadFile(filePath);

        // Assert
        result.FileInfo.FileName.Should().Be("full_features.dcf");
        result.DeviceCommissioning.NodeId.Should().Be(10);
        result.DeviceInfo.VendorName.Should().Be("Full Feature Vendor");
        result.ObjectDictionary.MandatoryObjects.Should().HaveCount(2);
        result.ObjectDictionary.OptionalObjects.Should().HaveCount(3);
        result.ObjectDictionary.ManufacturerObjects.Should().HaveCount(2);
        result.Comments.Should().NotBeNull();
    }

    [Fact]
    public void ReadFile_ModularDeviceDcf_ParsesModules()
    {
        // Arrange
        var filePath = "Fixtures/modular_device.dcf";

        // Act
        var result = _reader.ReadFile(filePath);

        // Assert
        result.SupportedModules.Should().HaveCount(2);
        result.ConnectedModules.Should().HaveCount(3);
        result.ConnectedModules.Should().ContainInOrder(1, 2, 1);
    }

    #endregion

    #region ReadString Tests

    [Fact]
    public void ReadString_MinimalContent_ParsesSuccessfully()
    {
        // Arrange
        var content = BuildMinimalDcf();

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.Should().NotBeNull();
        result.FileInfo.FileName.Should().Be("test.dcf");
    }

    [Fact]
    public void ReadString_MissingDeviceInfo_ThrowsEdsParseException()
    {
        // Arrange – no [DeviceInfo] section
        var content = @"
[FileInfo]
FileName=test.dcf

[DeviceCommissioning]
NodeID=5

[MandatoryObjects]
SupportedObjects=0
";

        // Act
        var act = () => _reader.ReadString(content);

        // Assert
        act.Should().Throw<EdsParseException>()
            .WithMessage("*DeviceInfo*");
    }

    #endregion

    #region ParseFileInfo Tests

    [Fact]
    public void ReadString_FileInfo_ParsesAllFields()
    {
        // Arrange
        var content = @"
[FileInfo]
FileName=complete.dcf
FileVersion=3
FileRevision=7
EDSVersion=4.0
Description=Complete test
CreationTime=10:00AM
CreationDate=01-01-2026
CreatedBy=Author
ModificationTime=02:30PM
ModificationDate=02-15-2026
ModifiedBy=Editor
LastEDS=source.eds

[DeviceInfo]
VendorName=V
ProductName=P
VendorNumber=0x1

[DeviceCommissioning]
NodeID=1

[MandatoryObjects]
SupportedObjects=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.FileInfo.FileName.Should().Be("complete.dcf");
        result.FileInfo.FileVersion.Should().Be(3);
        result.FileInfo.FileRevision.Should().Be(7);
        result.FileInfo.EdsVersion.Should().Be("4.0");
        result.FileInfo.Description.Should().Be("Complete test");
        result.FileInfo.CreationTime.Should().Be("10:00AM");
        result.FileInfo.CreationDate.Should().Be("01-01-2026");
        result.FileInfo.CreatedBy.Should().Be("Author");
        result.FileInfo.ModificationTime.Should().Be("02:30PM");
        result.FileInfo.ModificationDate.Should().Be("02-15-2026");
        result.FileInfo.ModifiedBy.Should().Be("Editor");
        result.FileInfo.LastEds.Should().Be("source.eds");
    }

    [Fact]
    public void ReadString_MissingFileInfo_ReturnsDefaults()
    {
        // Arrange – no [FileInfo] section but DeviceInfo is present
        var content = @"
[DeviceInfo]
VendorName=V
ProductName=P
VendorNumber=0x1

[DeviceCommissioning]
NodeID=1

[MandatoryObjects]
SupportedObjects=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.FileInfo.FileName.Should().BeEmpty();
        result.FileInfo.FileVersion.Should().Be(1);
        result.FileInfo.FileRevision.Should().Be(0);
        result.FileInfo.EdsVersion.Should().Be("4.0");
    }

    [Fact]
    public void ReadString_FileInfo_InvalidNumericFields_ThrowsFormatException()
    {
        // Arrange – invalid numeric values in FileInfo
        var content = @"
[FileInfo]
FileName=invalid.dcf
FileVersion=NaN
FileRevision=NotANumber

[DeviceInfo]
VendorName=V
ProductName=P
VendorNumber=0x1

[DeviceCommissioning]
NodeID=1

[MandatoryObjects]
SupportedObjects=0
";

        // Act
        var act = () => _reader.ReadString(content);

        // Assert
        act.Should().Throw<FormatException>();
    }

    #endregion

    #region ParseDeviceCommissioning Tests

    [Fact]
    public void ReadString_DeviceCommissioning_ParsesAllFields()
    {
        // Arrange
        var content = BuildMinimalDcf(@"
[DeviceCommissioning]
NodeID=10
NodeName=FullNode
Baudrate=250
NetNumber=2
NetworkName=ProdNet
CANopenManager=1
LSS_SerialNumber=12345678");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.DeviceCommissioning.NodeId.Should().Be(10);
        result.DeviceCommissioning.NodeName.Should().Be("FullNode");
        result.DeviceCommissioning.Baudrate.Should().Be(250);
        result.DeviceCommissioning.NetNumber.Should().Be(2u);
        result.DeviceCommissioning.NetworkName.Should().Be("ProdNet");
        result.DeviceCommissioning.CANopenManager.Should().BeTrue();
        result.DeviceCommissioning.LssSerialNumber.Should().Be(12345678u);
    }

    [Fact]
    public void ReadString_DeviceCommissioning_MissingSection_ReturnsDefaults()
    {
        // Arrange
        var content = BuildMinimalDcf("");

        // Act
        var result = _reader.ReadString(content);

        // Assert — byte default is 0, ushort default is 0
        result.DeviceCommissioning.NodeId.Should().Be(0);
        result.DeviceCommissioning.Baudrate.Should().Be(0);
        result.DeviceCommissioning.NetNumber.Should().Be(0u);
        result.DeviceCommissioning.CANopenManager.Should().BeFalse();
        result.DeviceCommissioning.LssSerialNumber.Should().BeNull();
    }

    [Fact]
    public void ReadString_DeviceCommissioning_NoLssSerialNumber_PropertyIsNull()
    {
        // Arrange
        var content = BuildMinimalDcf(@"
[DeviceCommissioning]
NodeID=5
Baudrate=500");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.DeviceCommissioning.LssSerialNumber.Should().BeNull();
    }

    #endregion

    #region ParseObjectDictionary Tests

    [Fact]
    public void ReadString_MandatoryObjects_ParsesObjectList()
    {
        // Arrange
        var content = BuildMinimalDcf();

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ObjectDictionary.MandatoryObjects.Should().Contain((ushort)0x1000);
        result.ObjectDictionary.Objects.Should().ContainKey((ushort)0x1000);
    }

    [Fact]
    public void ReadString_OptionalAndManufacturerObjects_ParsesAll()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=1
1=0x1008

[1008]
ParameterName=Manufacturer Device Name
ObjectType=0x7
DataType=0x0009
AccessType=ro
DefaultValue=TestDevice
PDOMapping=0

[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Custom Object
ObjectType=0x7
DataType=0x0007
AccessType=rw
DefaultValue=0
PDOMapping=1
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ObjectDictionary.OptionalObjects.Should().Contain((ushort)0x1008);
        result.ObjectDictionary.ManufacturerObjects.Should().Contain((ushort)0x2000);
        result.ObjectDictionary.Objects.Should().ContainKey((ushort)0x1008);
        result.ObjectDictionary.Objects.Should().ContainKey((ushort)0x2000);
    }

    [Fact]
    public void ReadString_ObjectProperties_ParsedCorrectly()
    {
        // Arrange
        var content = BuildMinimalDcf();

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x1000];
        obj.Index.Should().Be(0x1000);
        obj.ParameterName.Should().Be("Device Type");
        obj.ObjectType.Should().Be(0x7);
        obj.DataType.Should().Be(0x0007);
        obj.AccessType.Should().Be(AccessType.ReadOnly);
        obj.DefaultValue.Should().Be("0x191");
        obj.PdoMapping.Should().BeFalse();
    }

    [Fact]
    public void ReadString_ObjectWithLimitsAndFlags_ParsedCorrectly()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Custom Object
ObjectType=0x7
DataType=0x0007
AccessType=rw
DefaultValue=100
LowLimit=0
HighLimit=1000
PDOMapping=1
ObjFlags=3
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2000];
        obj.LowLimit.Should().Be("0");
        obj.HighLimit.Should().Be("1000");
        obj.PdoMapping.Should().BeTrue();
        obj.ObjFlags.Should().Be(3u);
    }

    [Fact]
    public void ReadString_DcfSpecificFields_ParsedCorrectly()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=DCF Object
ObjectType=0x7
DataType=0x0007
AccessType=rw
DefaultValue=0
PDOMapping=0
ParameterValue=42
Denotation=MyDenotation
UploadFile=upload.bin
DownloadFile=download.bin
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2000];
        obj.ParameterValue.Should().Be("42");
        obj.Denotation.Should().Be("MyDenotation");
        obj.UploadFile.Should().Be("upload.bin");
        obj.DownloadFile.Should().Be("download.bin");
    }

    #endregion

    #region SubObjects Tests

    [Fact]
    public void ReadString_SubObjects_ParsedViaSubNumber()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
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
DefaultValue=0x100
PDOMapping=0
ParameterValue=0x100
Denotation=VendorID

[1018sub2]
ParameterName=Product Code
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0x1001
PDOMapping=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x1018];
        obj.SubNumber.Should().Be(2);
        obj.SubObjects.Should().HaveCount(3); // sub0, sub1, sub2
        obj.SubObjects[0].ParameterName.Should().Be("Number of Entries");
        obj.SubObjects[1].ParameterName.Should().Be("Vendor ID");
        obj.SubObjects[1].ParameterValue.Should().Be("0x100");
        obj.SubObjects[1].Denotation.Should().Be("VendorID");
        obj.SubObjects[2].ParameterName.Should().Be("Product Code");
    }

    [Fact]
    public void ReadString_SubObjects_ParsedViaObjectType_Array()
    {
        // Arrange – ObjectType 0x8 (ARRAY) triggers sub-object parsing
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=1
1=0x6000

[6000]
ParameterName=Digital Input
ObjectType=0x8
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=1
SubNumber=2

[6000sub0]
ParameterName=Number of Elements
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=2
PDOMapping=0

[6000sub1]
ParameterName=DI Channel 1
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=1

[6000sub2]
ParameterName=DI Channel 2
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=1
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x6000];
        obj.ObjectType.Should().Be(0x8);
        obj.SubObjects.Should().HaveCount(3);
    }

    [Fact]
    public void ReadString_CompactSubObj_ValueSection_AppliesParameterValues()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=Config Array
ObjectType=0x8
DataType=0x0005
AccessType=rw
SubNumber=3
CompactSubObj=3

[2100sub0]
ParameterName=Number of Elements
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=3
PDOMapping=0

[2100sub1]
ParameterName=Element 1
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0

[2100sub2]
ParameterName=Element 2
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0

[2100sub3]
ParameterName=Element 3
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0

[2100Value]
NrOfEntries=3
1=10
2=20
3=30
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2100];
        obj.CompactSubObj.Should().Be(3);
        obj.SubObjects[1].ParameterValue.Should().Be("10");
        obj.SubObjects[2].ParameterValue.Should().Be("20");
        obj.SubObjects[3].ParameterValue.Should().Be("30");
    }

    [Fact]
    public void ReadString_CompactSubObj_DenotationSection_AppliesDenotations()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=Config Array
ObjectType=0x8
DataType=0x0005
AccessType=rw
SubNumber=2
CompactSubObj=2

[2100sub0]
ParameterName=Number of Elements
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=2
PDOMapping=0

[2100sub1]
ParameterName=Element 1
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0

[2100sub2]
ParameterName=Element 2
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0

[2100Denotation]
NrOfEntries=2
1=Label A
2=Label B
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2100];
        obj.SubObjects[1].Denotation.Should().Be("Label A");
        obj.SubObjects[2].Denotation.Should().Be("Label B");
    }

    [Fact]
    public void ReadString_SubObject_AllFieldsParsed()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=1
1=0x1018

[1018]
ParameterName=Record
SubNumber=1
ObjectType=0x9

[1018sub0]
ParameterName=Sub Zero
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=42
LowLimit=0
HighLimit=255
PDOMapping=1
ParameterValue=99
Denotation=SubDenotation
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var sub = result.ObjectDictionary.Objects[0x1018].SubObjects[0];
        sub.SubIndex.Should().Be(0);
        sub.ParameterName.Should().Be("Sub Zero");
        sub.ObjectType.Should().Be(0x7);
        sub.DataType.Should().Be(0x0005);
        sub.AccessType.Should().Be(AccessType.ReadWrite);
        sub.DefaultValue.Should().Be("42");
        sub.LowLimit.Should().Be("0");
        sub.HighLimit.Should().Be("255");
        sub.PdoMapping.Should().BeTrue();
        sub.ParameterValue.Should().Be("99");
        sub.Denotation.Should().Be("SubDenotation");
    }

    [Fact]
    public void ReadString_DeviceInfo_CANopenSafetySupported_ParsedCorrectly()
    {
        // Arrange
        var content = BuildMinimalDcf().Replace(
            "LSS_Supported=0",
            "LSS_Supported=0\nCANopenSafetySupported=1");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.DeviceInfo.CANopenSafetySupported.Should().BeTrue();
    }

    [Fact]
    public void ReadString_DeviceInfo_CANopenSafetySupported_DefaultsFalse()
    {
        // Arrange
        var content = BuildMinimalDcf();

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.DeviceInfo.CANopenSafetySupported.Should().BeFalse();
    }

    [Fact]
    public void ReadString_Object_SafetyProperties_ParsedCorrectly()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
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
");

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
        var content = BuildMinimalDcf();

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x1000];
        obj.SrdoMapping.Should().BeFalse();
        obj.InvertedSrad.Should().BeNullOrEmpty();
    }

    #endregion

    #region DummyUsage Tests

    [Fact]
    public void ReadString_DummyUsage_ParsesHexKeysAndBoolValues()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[DummyUsage]
Dummy0002=1
Dummy0003=0
Dummy0005=1
Dummy0007=1
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ObjectDictionary.DummyUsage.Should().ContainKey(2);
        result.ObjectDictionary.DummyUsage[2].Should().BeTrue();
        result.ObjectDictionary.DummyUsage[3].Should().BeFalse();
        result.ObjectDictionary.DummyUsage[5].Should().BeTrue();
        result.ObjectDictionary.DummyUsage[7].Should().BeTrue();
    }

    [Fact]
    public void ReadString_NoDummyUsage_EmptyDictionary()
    {
        // Arrange
        var content = BuildMinimalDcf();

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ObjectDictionary.DummyUsage.Should().BeEmpty();
    }

    #endregion

    #region ObjectLinks Tests

    [Fact]
    public void ReadString_ObjectLinks_ParsedCorrectly()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Linked Object
ObjectType=0x7
DataType=0x0007
AccessType=rw
DefaultValue=0
PDOMapping=0

[2000ObjectLinks]
ObjectLinks=2
1=0x2100
2=0x1000
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2000];
        obj.ObjectLinks.Should().HaveCount(2);
        obj.ObjectLinks.Should().Contain((ushort)0x2100);
        obj.ObjectLinks.Should().Contain((ushort)0x1000);
    }

    #endregion

    #region Comments Tests

    [Fact]
    public void ReadString_Comments_ParsedCorrectly()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[Comments]
Lines=3
Line1=First line
Line2=Second line
Line3=Third line
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.Comments.Should().NotBeNull();
        result.Comments!.Lines.Should().Be(3);
        result.Comments.CommentLines.Should().HaveCount(3);
        result.Comments.CommentLines[1].Should().Be("First line");
        result.Comments.CommentLines[2].Should().Be("Second line");
        result.Comments.CommentLines[3].Should().Be("Third line");
    }

    [Fact]
    public void ReadString_NoComments_ReturnsNull()
    {
        // Arrange
        var content = BuildMinimalDcf();

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.Comments.Should().BeNull();
    }

    #endregion

    #region SupportedModules Tests

    [Fact]
    public void ReadString_SupportedModules_ParsedCorrectly()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[SupportedModules]
NrOfEntries=2

[M1ModuleInfo]
ProductName=Input Module
ProductVersion=1
ProductRevision=0
OrderCode=MOD-IN-8

[M1FixedObjects]
NrOfEntries=1
1=0x6000

[M2ModuleInfo]
ProductName=Output Module
ProductVersion=2
ProductRevision=1
OrderCode=MOD-OUT-8
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.SupportedModules.Should().HaveCount(2);

        var m1 = result.SupportedModules[0];
        m1.ModuleNumber.Should().Be(1);
        m1.ProductName.Should().Be("Input Module");
        m1.ProductVersion.Should().Be(1);
        m1.ProductRevision.Should().Be(0);
        m1.OrderCode.Should().Be("MOD-IN-8");
        m1.FixedObjects.Should().Contain((ushort)0x6000);

        var m2 = result.SupportedModules[1];
        m2.ModuleNumber.Should().Be(2);
        m2.ProductName.Should().Be("Output Module");
        m2.ProductVersion.Should().Be(2);
        m2.ProductRevision.Should().Be(1);
        m2.OrderCode.Should().Be("MOD-OUT-8");
        m2.FixedObjects.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_NoSupportedModules_EmptyList()
    {
        // Arrange
        var content = BuildMinimalDcf();

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.SupportedModules.Should().BeEmpty();
    }

    #endregion

    #region ConnectedModules Tests

    [Fact]
    public void ReadString_ConnectedModules_ParsedCorrectly()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[ConnectedModules]
NrOfEntries=3
1=1
2=2
3=1
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ConnectedModules.Should().HaveCount(3);
        result.ConnectedModules.Should().ContainInOrder(1, 2, 1);
    }

    [Fact]
    public void ReadString_NoConnectedModules_EmptyList()
    {
        // Arrange
        var content = BuildMinimalDcf();

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ConnectedModules.Should().BeEmpty();
    }

    #endregion

    #region AdditionalSections Tests

    [Fact]
    public void ReadString_UnknownSection_PreservedInAdditionalSections()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[VendorSpecific]
CustomKey=CustomValue
Version=1.0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.AdditionalSections.Should().ContainKey("VendorSpecific");
        result.AdditionalSections["VendorSpecific"]["CustomKey"].Should().Be("CustomValue");
        result.AdditionalSections["VendorSpecific"]["Version"].Should().Be("1.0");
    }

    [Fact]
    public void ReadString_KnownSections_NotInAdditionalSections()
    {
        // Arrange
        var content = BuildMinimalDcf();

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.AdditionalSections.Should().NotContainKey("FileInfo");
        result.AdditionalSections.Should().NotContainKey("DeviceInfo");
        result.AdditionalSections.Should().NotContainKey("DeviceCommissioning");
        result.AdditionalSections.Should().NotContainKey("MandatoryObjects");
        result.AdditionalSections.Should().NotContainKey("1000");
    }

    #endregion

    #region Case-Insensitivity Tests

    [Fact]
    public void ReadString_CaseInsensitiveSections_ParsesCorrectly()
    {
        // Arrange – mixed-case section names
        var content = @"
[fileinfo]
FileName=case_test.dcf
FileVersion=1

[deviceinfo]
VendorName=Test
ProductName=CaseTest
VendorNumber=0x1

[devicecommissioning]
NodeID=7
Baudrate=250

[mandatoryobjects]
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
        result.FileInfo.FileName.Should().Be("case_test.dcf");
        result.DeviceInfo.VendorName.Should().Be("Test");
        result.DeviceCommissioning.NodeId.Should().Be(7);
        result.ObjectDictionary.MandatoryObjects.Should().Contain((ushort)0x1000);
    }

    #endregion

    #region AccessType Parsing in Objects

    [Theory]
    [InlineData("ro", AccessType.ReadOnly)]
    [InlineData("wo", AccessType.WriteOnly)]
    [InlineData("rw", AccessType.ReadWrite)]
    [InlineData("rwr", AccessType.ReadWriteInput)]
    [InlineData("rww", AccessType.ReadWriteOutput)]
    [InlineData("const", AccessType.Constant)]
    public void ReadString_AccessType_ParsedCorrectly(string accessTypeStr, AccessType expected)
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: $@"
[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=AccessTest
ObjectType=0x7
DataType=0x0007
AccessType={accessTypeStr}
DefaultValue=0
PDOMapping=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ObjectDictionary.Objects[0x2000].AccessType.Should().Be(expected);
    }

    #endregion

    #region Full Feature Fixture Tests

    [Fact]
    public void ReadFile_FullFeatures_CommissioningWithLssSerial()
    {
        // Act
        var result = _reader.ReadFile("Fixtures/full_features.dcf");

        // Assert
        result.DeviceCommissioning.CANopenManager.Should().BeTrue();
        result.DeviceCommissioning.LssSerialNumber.Should().Be(12345678u);
    }

    [Fact]
    public void ReadFile_FullFeatures_DcfSpecificFieldsOnObjects()
    {
        // Act
        var result = _reader.ReadFile("Fixtures/full_features.dcf");

        // Assert
        var obj2000 = result.ObjectDictionary.Objects[0x2000];
        obj2000.ParameterValue.Should().Be("42");
        obj2000.UploadFile.Should().Be("status_upload.bin");
        obj2000.DownloadFile.Should().Be("status_download.bin");
        obj2000.ObjFlags.Should().Be(3u);
    }

    [Fact]
    public void ReadFile_FullFeatures_ObjectLinksOnObject()
    {
        // Act
        var result = _reader.ReadFile("Fixtures/full_features.dcf");

        // Assert
        var obj2000 = result.ObjectDictionary.Objects[0x2000];
        obj2000.ObjectLinks.Should().HaveCount(2);
        obj2000.ObjectLinks.Should().Contain((ushort)0x2100);
        obj2000.ObjectLinks.Should().Contain((ushort)0x1000);
        
        // ObjectLinks section is now in AdditionalSections since IsKnownSection no longer marks it as known
        result.AdditionalSections.Should().ContainKey("2000ObjectLinks");
    }

    [Fact]
    public void ReadFile_FullFeatures_CompactValueAndDenotation()
    {
        // Act
        var result = _reader.ReadFile("Fixtures/full_features.dcf");

        // Assert
        var obj2100 = result.ObjectDictionary.Objects[0x2100];
        obj2100.CompactSubObj.Should().Be(3);

        // Value section applied
        obj2100.SubObjects[1].ParameterValue.Should().Be("10");
        obj2100.SubObjects[2].ParameterValue.Should().Be("20");
        obj2100.SubObjects[3].ParameterValue.Should().Be("30");

        // Denotation section applied
        obj2100.SubObjects[1].Denotation.Should().Be("Config A");
        obj2100.SubObjects[2].Denotation.Should().Be("Config B");
        obj2100.SubObjects[3].Denotation.Should().Be("Config C");
    }

    [Fact]
    public void ReadFile_FullFeatures_UnknownSectionPreserved()
    {
        // Act
        var result = _reader.ReadFile("Fixtures/full_features.dcf");

        // Assert
        result.AdditionalSections.Should().ContainKey("VendorSpecificSection");
        result.AdditionalSections["VendorSpecificSection"]["CustomKey1"].Should().Be("CustomValue1");
        result.AdditionalSections["VendorSpecificSection"]["VersionInfo"].Should().Be("1.2.3");
    }

    [Fact]
    public void ReadFile_FullFeatures_DummyUsage()
    {
        // Act
        var result = _reader.ReadFile("Fixtures/full_features.dcf");

        // Assert
        result.ObjectDictionary.DummyUsage[2].Should().BeTrue();
        result.ObjectDictionary.DummyUsage[3].Should().BeFalse();
        result.ObjectDictionary.DummyUsage[5].Should().BeTrue();
        result.ObjectDictionary.DummyUsage[7].Should().BeTrue();
    }

    [Fact]
    public void ReadFile_FullFeatures_Comments()
    {
        // Act
        var result = _reader.ReadFile("Fixtures/full_features.dcf");

        // Assert
        result.Comments.Should().NotBeNull();
        result.Comments!.Lines.Should().Be(2);
        result.Comments.CommentLines[1].Should().Be("Full-featured DCF test file");
        result.Comments.CommentLines[2].Should().Be("Contains all supported DCF extensions");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ReadString_ObjectSectionMissing_ObjectNotAdded()
    {
        // Arrange – Object listed but has no definition section
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=1
1=0x9999
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ObjectDictionary.OptionalObjects.Should().Contain((ushort)0x9999);
        result.ObjectDictionary.Objects.Should().NotContainKey((ushort)0x9999);
    }

    [Fact]
    public void ReadString_EmptyObjectDictionary_ParsesWithoutError()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: "").Replace(@"
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
", @"
[MandatoryObjects]
SupportedObjects=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ObjectDictionary.MandatoryObjects.Should().BeEmpty();
        result.ObjectDictionary.Objects.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_DuplicateIndexInMultipleLists_ObjectOnlyOnce()
    {
        // Arrange – same index in both mandatory and optional (unusual but possible)
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=1
1=0x1000
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        // Distinct() in DcfReader ensures only one object entry
        result.ObjectDictionary.Objects.Should().ContainKey((ushort)0x1000);
    }

    #endregion

    #region Negative/Error-Path Tests

    [Fact]
    public void ReadString_EmptyContent_RequiresMandatoryDeviceInfo()
    {
        // Arrange
        var content = "";

        // Act
        var act = () => _reader.ReadString(content);

        // Assert - empty content lacks [DeviceInfo] which is mandatory
        act.Should().Throw<EdsParseException>()
            .WithMessage("*DeviceInfo*");
    }

    [Fact]
    public void ReadString_WhitespaceOnlyContent_RequiresMandatoryDeviceInfo()
    {
        // Arrange
        var content = "   \n\n  \t  ";

        // Act
        var act = () => _reader.ReadString(content);

        // Assert - whitespace-only content lacks [DeviceInfo]
        act.Should().Throw<EdsParseException>()
            .WithMessage("*DeviceInfo*");
    }

    [Fact]
    public void ReadString_InvalidAccessType_ParsesAsDefault()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Invalid AccessType
ObjectType=0x7
DataType=0x0007
AccessType=invalid
DefaultValue=0
PDOMapping=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        // Parser uses ValueConverter.ParseAccessType which handles unknown values
        var obj = result.ObjectDictionary.Objects[0x2000];
        obj.AccessType.Should().Be(AccessType.ReadOnly); // default fallback
    }

    [Fact]
    public void ReadString_NonHexObjectIndex_ThrowsFormatException()
    {
        // Arrange – Non-hex index in object list
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=1
1=NotHex
");

        // Act
        var act = () => _reader.ReadString(content);

        // Assert
        // ValueConverter.ParseUInt16 throws FormatException for non-hex
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void ReadString_OverflowObjectIndex_ThrowsOverflowException()
    {
        // Arrange – Value exceeds ushort range
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=1
1=0x10000
");

        // Act
        var act = () => _reader.ReadString(content);

        // Assert
        // ValueConverter.ParseUInt16 throws OverflowException
        act.Should().Throw<OverflowException>();
    }

    [Fact]
    public void ReadString_SupportedObjectsCountMismatch_IgnoresExtra()
    {
        // Arrange – SupportedObjects says 2 but list 3
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=2
1=0x1008
2=0x1009
3=0x100A

[1008]
ParameterName=Object 1008
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0
PDOMapping=0

[1009]
ParameterName=Object 1009
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0
PDOMapping=0

[100A]
ParameterName=Object 100A
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0
PDOMapping=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        // Parser respects SupportedObjects count, only loads first 2
        result.ObjectDictionary.OptionalObjects.Should().HaveCount(2);
        result.ObjectDictionary.OptionalObjects.Should().Contain(0x1008);
        result.ObjectDictionary.OptionalObjects.Should().Contain(0x1009);
        result.ObjectDictionary.OptionalObjects.Should().NotContain(0x100A);
    }

    [Fact]
    public void ReadString_SupportedObjectsCountExceedsAvailable_ParsesAvailable()
    {
        // Arrange – SupportedObjects says 5 but only 2 defined
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=5
1=0x1008
2=0x1009

[1008]
ParameterName=Object 1008
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0
PDOMapping=0

[1009]
ParameterName=Object 1009
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0
PDOMapping=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        // Parser loops over SupportedObjects range, gets what's available
        result.ObjectDictionary.OptionalObjects.Should().Contain(0x1008);
        result.ObjectDictionary.OptionalObjects.Should().Contain(0x1009);
    }

    [Fact]
    public void ReadString_OrphanObjectLinksSection_PreservedInAdditionalSections()
    {
        // Arrange – ObjectLinks section for object that doesn't exist in any list
        var content = BuildMinimalDcf(extraSections: @"
[9999ObjectLinks]
ObjectLinks=2
1=0x1000
2=0x2000
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        // Orphan ObjectLinks sections are preserved in AdditionalSections
        // since IsKnownSection no longer marks them as known
        result.AdditionalSections.Should().ContainKey("9999ObjectLinks");
        result.AdditionalSections["9999ObjectLinks"]["ObjectLinks"].Should().Be("2");
        result.AdditionalSections["9999ObjectLinks"]["1"].Should().Be("0x1000");
    }

    [Fact]
    public void ReadString_ObjectLinksForExistingObject_InAdditionalSections()
    {
        // Arrange – ObjectLinks section for an object that DOES exist
        // Since ObjectLinks are no longer marked as "known" in IsKnownSection,
        // they go to AdditionalSections regardless
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Custom Object
ObjectType=0x7
DataType=0x0007
AccessType=rw
DefaultValue=0
PDOMapping=0

[2000ObjectLinks]
ObjectLinks=1
1=0x1000
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        // ObjectLinks for existing object are parsed and attached to object
        var obj = result.ObjectDictionary.Objects[0x2000];
        obj.ObjectLinks.Should().Contain(0x1000);
        
        // ObjectLinks section is also in AdditionalSections since it's not marked as known
        result.AdditionalSections.Should().ContainKey("2000ObjectLinks");
    }

    [Fact]
    public void ReadString_InvalidSubNumberCount_StillParsesAvailableSubObjects()
    {
        // Arrange – SubNumber says 3 but only 2 sub-sections exist
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=1
1=0x1018

[1018]
ParameterName=Record
SubNumber=3
ObjectType=0x9

[1018sub0]
ParameterName=Number of Entries
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=3
PDOMapping=0

[1018sub1]
ParameterName=Entry 1
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        // Parser creates sub-objects for index range 0..SubNumber
        var obj = result.ObjectDictionary.Objects[0x1018];
        obj.SubObjects.Should().HaveCount(2); // has sub0 and sub1 sections
    }

    [Fact]
    public void ReadString_NegativeOrZeroHexValue_ParsesCorrectly()
    {
        // Arrange – Negative hex (two's complement)
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Negative Value
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0xFFFFFFFF
PDOMapping=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2000];
        obj.DefaultValue.Should().Be("0xFFFFFFFF");
    }

    [Fact]
    public void ReadString_OctalNotation_ParsedCorrectly()
    {
        // Arrange – Octal notation (0755 style)
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Octal Value
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0755
PDOMapping=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2000];
        obj.DefaultValue.Should().Be("0755");
    }

    #endregion
}

