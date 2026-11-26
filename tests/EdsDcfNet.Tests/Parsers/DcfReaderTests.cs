namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Utilities;

public partial class DcfReaderTests
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
        result.FileInfo.EdsVersion.Should().Be("3.0");
    }

    [Fact]
    public void ReadString_FileInfo_InvalidNumericFields_ThrowsEdsParseException()
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

        // Assert – failure is attributed to the first bad FileInfo field
        var ex = act.Should().Throw<EdsParseException>().Which;
        ex.SectionName.Should().Be("FileInfo");
        ex.Message.Should().Contain("[FileInfo] FileVersion:");
        ex.Message.Should().Contain("NaN");
    }

    [Theory]
    [InlineData("1.0", (byte)1)]
    [InlineData("1,0", (byte)1)]
    [InlineData("7.3", (byte)7)]
    [InlineData("07.3", (byte)7)]
    [InlineData("012.5", (byte)12)]
    [InlineData("010.0", (byte)10)]
    [InlineData("12,5", (byte)12)]
    [InlineData("255.0", (byte)255)]
    public void ReadString_FileInfo_MajorMinorFileVersion_UsesMajorComponent(string fileVersion, byte expected)
    {
        var content = BuildMinimalDcf().Replace("FileVersion=1", $"FileVersion={fileVersion}");

        var result = _reader.ReadString(content);

        result.FileInfo.FileVersion.Should().Be(expected);
    }

    [Theory]
    [InlineData("2.1", (byte)2)]
    [InlineData("3,0", (byte)3)]
    public void ReadString_FileInfo_MajorMinorFileRevision_UsesMajorComponent(string fileRevision, byte expected)
    {
        var content = BuildMinimalDcf().Replace("FileRevision=0", $"FileRevision={fileRevision}");

        var result = _reader.ReadString(content);

        result.FileInfo.FileRevision.Should().Be(expected);
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1,0")]
    public void ReadString_FileInfo_MajorMinorFileVersion_StrictParsing_ThrowsWithAttribution(string fileVersion)
    {
        var content = BuildMinimalDcf().Replace("FileVersion=1", $"FileVersion={fileVersion}");

        var act = () => CanOpenFile.Dcf.ReadString(content, new CanOpenFileOptions { StrictParsing = true });

        var ex = act.Should().Throw<EdsParseException>().Which;
        ex.SectionName.Should().Be("FileInfo");
        ex.Message.Should().Contain("[FileInfo] FileVersion:");
        ex.Message.Should().Contain(fileVersion);
    }

    [Theory]
    [InlineData("010", (byte)10)]
    [InlineData("08", (byte)8)]
    [InlineData("012", (byte)12)]
    public void ReadString_FileInfo_FileVersionZeroPadded_StaysDecimal_CrossFormatWithXdd(
        string fileVersion, byte expected)
    {
        // EDS/DCF FileVersion must match XDD fileVersion decimal policy (#467):
        // "010" → 10, not CiA octal 8 via ParseByte.
        var dcf = BuildMinimalDcf().Replace("FileVersion=1", $"FileVersion={fileVersion}");
        var dcfResult = CanOpenFile.Dcf.ReadString(dcf);
        dcfResult.FileInfo.FileVersion.Should().Be(expected);

        var xdd = MinimalXddForFileVersion(fileVersion);
        var xddResult = CanOpenFile.Xdd.ReadString(xdd);
        xddResult.FileInfo.FileVersion.Should().Be(expected);
    }

    [Theory]
    [InlineData("010", (byte)10)]
    [InlineData("08", (byte)8)]
    public void ReadString_FileInfo_FileVersionZeroPadded_StrictParsing_StaysDecimal(
        string fileVersion, byte expected)
    {
        var content = BuildMinimalDcf().Replace("FileVersion=1", $"FileVersion={fileVersion}");

        var result = CanOpenFile.Dcf.ReadString(content, new CanOpenFileOptions { StrictParsing = true });

        result.FileInfo.FileVersion.Should().Be(expected);
    }

    private static string MinimalXddForFileVersion(string fileVersion) =>
        $@"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileHeader>
      <ProfileClassID>Device</ProfileClassID>
    </ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen""
                 fileName=""test.xdd"" fileCreator=""TestCreator""
                 fileCreationDate=""2025-01-15"" fileVersion=""{fileVersion}"">
      <DeviceIdentity>
        <vendorName>Test Vendor</vendorName>
        <vendorID>0x00000100</vendorID>
        <productName>Test Product</productName>
        <productID>0x00001001</productID>
      </DeviceIdentity>
      <DeviceManager/>
      <DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileHeader>
      <ProfileClassID>CommunicationNetwork</ProfileClassID>
    </ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen""
                 fileName=""test.xdd"" fileCreator=""TestCreator""
                 fileCreationDate=""2025-01-15"" fileVersion=""{fileVersion}"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007""
                         accessType=""ro"" defaultValue=""0x00000000"" PDOmapping=""no""/>
        </CANopenObjectList>
      </ApplicationLayers>
      <TransportLayers>
        <PhysicalLayer>
          <baudRate defaultValue=""250 Kbps"">
            <supportedBaudRate value=""250 Kbps""/>
          </baudRate>
        </PhysicalLayer>
      </TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""0"" nrOfTxPDO=""0""
                                bootUpSlave=""true"" layerSettingServiceSlave=""false""
                                groupMessaging=""false"" dynamicChannels=""0""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

    [Fact]
    public void ReadString_FileInfo_FileVersionAtMaxValue_Parses255()
    {
        var content = BuildMinimalDcf().Replace("FileVersion=1", "FileVersion=255");

        var result = _reader.ReadString(content);

        result.FileInfo.FileVersion.Should().Be(byte.MaxValue);
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

    [Theory]
    [InlineData(0)]
    [InlineData(128)]
    public void ReadString_DeviceCommissioning_InvalidNodeIdStrict_ThrowsEdsParseException(int nodeId)
    {
        // Arrange
        var content = BuildMinimalDcf($@"
[DeviceCommissioning]
NodeID={nodeId}
NodeName=InvalidNode
Baudrate=500
NetNumber=1
NetworkName=TestNetwork
CANopenManager=0");

        // Act
        var act = () => CanOpenFile.Dcf.ReadString(content, new CanOpenFileOptions { StrictParsing = true });

        // Assert
        act.Should().Throw<EdsParseException>()
            .WithMessage("*Invalid NodeID*")
            .Which.SectionName.Should().BeOneOf("DeviceCommissioning", "DeviceComissioning");
    }

    [Fact]
    public void ReadString_DeviceCommissioning_ReferenceDesignators_ParsedCorrectly()
    {
        // Arrange
        var content = BuildMinimalDcf(@"
[DeviceCommissioning]
NodeID=5
NodeName=TestNode
NodeRefd=N1.A2
Baudrate=500
NetNumber=1
NetworkName=TestNetwork
NetRefd=CAN-Bus 1
CANopenManager=0");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.DeviceCommissioning.NodeRefd.Should().Be("N1.A2");
        result.DeviceCommissioning.NetRefd.Should().Be("CAN-Bus 1");
    }

    [Fact]
    public void ReadString_DeviceCommissioning_ReferenceDesignators_DefaultNull()
    {
        // Arrange
        var content = BuildMinimalDcf();

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.DeviceCommissioning.NodeRefd.Should().BeNullOrEmpty();
        result.DeviceCommissioning.NetRefd.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ReadString_DeviceComissioning_SingleM_ParsedCorrectly()
    {
        // Arrange — spec spelling with single 'm'
        var content = @"
[FileInfo]
FileName=test.dcf
FileVersion=1
FileRevision=0
EDSVersion=4.0

[DeviceInfo]
VendorName=Test Vendor
VendorNumber=0x100
ProductName=Test Product
ProductNumber=0x1001

[DeviceComissioning]
NodeID=7
NodeName=SpecNode
NodeRefd=N2.B3
Baudrate=250
NetNumber=1
NetworkName=SpecNet
NetRefd=CAN-Bus 2
CANopenManager=0

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
        result.DeviceCommissioning.NodeId.Should().Be(7);
        result.DeviceCommissioning.NodeName.Should().Be("SpecNode");
        result.DeviceCommissioning.NodeRefd.Should().Be("N2.B3");
        result.DeviceCommissioning.NetworkName.Should().Be("SpecNet");
        result.DeviceCommissioning.NetRefd.Should().Be("CAN-Bus 2");
    }

    [Fact]
    public void ReadString_Object_ParamRefd_ParsedCorrectly()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Custom Object
ObjectType=0x9
DataType=0x0007
AccessType=rw
DefaultValue=0
PDOMapping=0
ParamRefd=X1.Channel1
SubNumber=1

[2000sub0]
ParameterName=Number of Entries
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0

[2000sub1]
ParameterName=Sub Entry
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0
ParamRefd=X1.Channel1.Sub1
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2000];
        obj.ParamRefd.Should().Be("X1.Channel1");
        obj.SubObjects[1].ParamRefd.Should().Be("X1.Channel1.Sub1");
        obj.SubObjects[0].ParamRefd.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ReadString_Object_ParamRefd_DefaultNull()
    {
        // Arrange
        var content = BuildMinimalDcf();

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ObjectDictionary.Objects[0x1000].ParamRefd.Should().BeNullOrEmpty();
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
}
