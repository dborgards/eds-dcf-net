namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;

public class XddReaderTests
{
    private readonly XddReader _reader = new();

    private const string MinimalXdd = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileHeader>
      <ProfileClassID>Device</ProfileClassID>
    </ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen""
                 fileName=""test.xdd"" fileCreator=""TestCreator""
                 fileCreationDate=""2025-01-15"" fileVersion=""1"">
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
                 fileCreationDate=""2025-01-15"" fileVersion=""1"">
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
            <supportedBaudRate value=""500 Kbps""/>
          </baudRate>
        </PhysicalLayer>
      </TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""2"" nrOfTxPDO=""2""
                                bootUpSlave=""true"" layerSettingServiceSlave=""false""
                                groupMessaging=""false"" dynamicChannels=""0""/>
        <CANopenMasterFeatures bootUpMaster=""false""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

    #region ReadFile Tests

    [Fact]
    public void ReadFile_ValidXddFile_ParsesSuccessfully()
    {
        // Act
        var result = _reader.ReadFile("Fixtures/sample_device.xdd");

        // Assert
        result.Should().NotBeNull();
        result.FileInfo.Should().NotBeNull();
        result.DeviceInfo.Should().NotBeNull();
        result.ObjectDictionary.Should().NotBeNull();
    }

    [Fact]
    public void ReadFile_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Act
        var act = () => _reader.ReadFile("NonExistent.xdd");

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    #endregion

    #region ReadString Tests

    [Fact]
    public void ReadString_ValidXddContent_ParsesSuccessfully()
    {
        // Act
        var result = _reader.ReadString(MinimalXdd);

        // Assert
        result.Should().NotBeNull();
        result.FileInfo.FileName.Should().Be("test.xdd");
        result.DeviceInfo.VendorName.Should().Be("Test Vendor");
    }

    [Fact]
    public void ReadString_MissingCommunicationNetworkProfile_ThrowsException()
    {
        // Arrange — only Device profile, no CommunicationNetwork profile
        const string xddNoCommNet = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""test.xdd"" fileVersion=""1"">
      <DeviceIdentity>
        <vendorName>Test</vendorName>
        <vendorID>0x00000001</vendorID>
        <productName>Test</productName>
        <productID>0x00000001</productID>
      </DeviceIdentity>
      <DeviceManager/>
      <DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        // Act
        var act = () => _reader.ReadString(xddNoCommNet);

        // Assert
        act.Should().Throw<EdsParseException>();
    }

    #endregion

    #region FileInfo Tests

    [Fact]
    public void FileInfo_ParsesAllFields()
    {
        // Act
        var result = _reader.ReadString(MinimalXdd);

        // Assert
        result.FileInfo.FileName.Should().Be("test.xdd");
        result.FileInfo.CreatedBy.Should().Be("TestCreator");
        result.FileInfo.CreationDate.Should().Be("01-15-2025");
        result.FileInfo.FileVersion.Should().Be(1);
    }

    #endregion

    #region DeviceInfo Tests

    [Fact]
    public void DeviceInfo_ParsesVendorAndProduct()
    {
        // Act
        var result = _reader.ReadString(MinimalXdd);

        // Assert
        result.DeviceInfo.VendorName.Should().Be("Test Vendor");
        result.DeviceInfo.VendorNumber.Should().Be(0x00000100u);
        result.DeviceInfo.ProductName.Should().Be("Test Product");
        result.DeviceInfo.ProductNumber.Should().Be(0x00001001u);
    }

    [Fact]
    public void DeviceInfo_ParsesGeneralFeatures()
    {
        // Act
        var result = _reader.ReadString(MinimalXdd);

        // Assert
        result.DeviceInfo.Granularity.Should().Be(8);
        result.DeviceInfo.NrOfRxPdo.Should().Be(2);
        result.DeviceInfo.NrOfTxPdo.Should().Be(2);
        result.DeviceInfo.SimpleBootUpSlave.Should().BeTrue();
        result.DeviceInfo.LssSupported.Should().BeFalse();
        result.DeviceInfo.GroupMessaging.Should().BeFalse();
        result.DeviceInfo.SimpleBootUpMaster.Should().BeFalse();
    }

    [Fact]
    public void DeviceInfo_ParsesSupportedBaudRates()
    {
        // Act
        var result = _reader.ReadString(MinimalXdd);

        // Assert
        result.DeviceInfo.SupportedBaudRates.BaudRate250.Should().BeTrue();
        result.DeviceInfo.SupportedBaudRates.BaudRate500.Should().BeTrue();
        result.DeviceInfo.SupportedBaudRates.BaudRate125.Should().BeFalse();
    }

    #endregion

    #region ObjectDictionary Tests

    [Fact]
    public void ObjectDictionary_ParsesMandatoryObjects()
    {
        // Act
        var result = _reader.ReadString(MinimalXdd);

        // Assert
        result.ObjectDictionary.MandatoryObjects.Should().Contain(0x1000);
        result.ObjectDictionary.Objects.Should().ContainKey(0x1000);
    }

    [Fact]
    public void ObjectDictionary_ParsesOptionalObjects()
    {
        // Arrange — XDD with optional object 1008h
        const string xdd = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <DeviceIdentity><vendorName>T</vendorName><vendorID>0x1</vendorID><productName>T</productName><productID>0x1</productID></DeviceIdentity>
      <DeviceManager/><DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""0"" optionalObjects=""1"" manufacturerObjects=""0"">
          <CANopenObject index=""1008"" name=""Device Name"" objectType=""7"" dataType=""0009""
                         accessType=""ro"" defaultValue=""Test"" PDOmapping=""no""/>
        </CANopenObjectList>
      </ApplicationLayers>
      <TransportLayers><PhysicalLayer><baudRate defaultValue=""250 Kbps""/></PhysicalLayer></TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""0"" nrOfTxPDO=""0""
                                bootUpSlave=""false"" layerSettingServiceSlave=""false""
                                groupMessaging=""false"" dynamicChannels=""0""/>
        <CANopenMasterFeatures bootUpMaster=""false""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        // Act
        var result = _reader.ReadString(xdd);

        // Assert
        result.ObjectDictionary.OptionalObjects.Should().Contain(0x1008);
    }

    [Fact]
    public void ObjectDictionary_ParsesObjectAllProperties()
    {
        // Arrange — XDD with object having all standard properties
        const string xdd = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <DeviceIdentity><vendorName>T</vendorName><vendorID>0x1</vendorID><productName>T</productName><productID>0x1</productID></DeviceIdentity>
      <DeviceManager/><DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007""
                         accessType=""ro"" defaultValue=""0x00000191""
                         lowLimit=""0"" highLimit=""0xFFFFFFFF""
                         PDOmapping=""no"" objFlags=""1"" subNumber=""0""/>
        </CANopenObjectList>
      </ApplicationLayers>
      <TransportLayers><PhysicalLayer><baudRate defaultValue=""250 Kbps""/></PhysicalLayer></TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""0"" nrOfTxPDO=""0""
                                bootUpSlave=""false"" layerSettingServiceSlave=""false""
                                groupMessaging=""false"" dynamicChannels=""0""/>
        <CANopenMasterFeatures bootUpMaster=""false""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        // Act
        var result = _reader.ReadString(xdd);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x1000];
        obj.Index.Should().Be(0x1000);
        obj.ParameterName.Should().Be("Device Type");
        obj.ObjectType.Should().Be(7);
        obj.DataType.Should().Be(0x0007);
        obj.AccessType.Should().Be(AccessType.ReadOnly);
        obj.DefaultValue.Should().Be("0x00000191");
        obj.LowLimit.Should().Be("0");
        obj.HighLimit.Should().Be("0xFFFFFFFF");
        obj.PdoMapping.Should().BeFalse();
        obj.ObjFlags.Should().Be(1u);
        obj.SubNumber.Should().Be(0);
    }

    [Fact]
    public void ObjectDictionary_ParsesSubObjects()
    {
        // Act — sample_device.xdd has Identity Object with sub-objects
        var result = _reader.ReadFile("Fixtures/sample_device.xdd");

        // Assert
        var obj = result.ObjectDictionary.Objects[0x1018];
        obj.SubObjects.Should().ContainKey(0);
        obj.SubObjects.Should().ContainKey(1);
        obj.SubObjects[0].ParameterName.Should().Be("Number of Entries");
        obj.SubObjects[1].ParameterName.Should().Be("Vendor ID");
    }

    [Fact]
    public void ObjectDictionary_ParsesDummyUsage()
    {
        // Act
        var result = _reader.ReadFile("Fixtures/sample_device.xdd");

        // Assert
        result.ObjectDictionary.DummyUsage.Should().ContainKey(0x0002);
        result.ObjectDictionary.DummyUsage[0x0002].Should().BeTrue();
        result.ObjectDictionary.DummyUsage.Should().ContainKey(0x0005);
        result.ObjectDictionary.DummyUsage[0x0005].Should().BeTrue();
    }

    #endregion

    #region AccessType Tests

    [Theory]
    [InlineData("const", AccessType.Constant)]
    [InlineData("ro", AccessType.ReadOnly)]
    [InlineData("wo", AccessType.WriteOnly)]
    [InlineData("rw", AccessType.ReadWrite)]
    public void AccessTypes_AllMappedCorrectly(string xddType, AccessType expected)
    {
        // Arrange
        var xdd = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <DeviceIdentity><vendorName>T</vendorName><vendorID>0x1</vendorID><productName>T</productName><productID>0x1</productID></DeviceIdentity>
      <DeviceManager/><DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Test"" objectType=""7"" dataType=""0007""
                         accessType=""{xddType}"" PDOmapping=""no""/>
        </CANopenObjectList>
      </ApplicationLayers>
      <TransportLayers><PhysicalLayer><baudRate defaultValue=""250 Kbps""/></PhysicalLayer></TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""0"" nrOfTxPDO=""0""
                                bootUpSlave=""false"" layerSettingServiceSlave=""false""
                                groupMessaging=""false"" dynamicChannels=""0""/>
        <CANopenMasterFeatures bootUpMaster=""false""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        // Act
        var result = _reader.ReadString(xdd);

        // Assert
        result.ObjectDictionary.Objects[0x1000].AccessType.Should().Be(expected);
    }

    [Fact]
    public void PdoMapping_no_MappedToFalse()
    {
        // Act
        var result = _reader.ReadString(MinimalXdd);

        // Assert
        result.ObjectDictionary.Objects[0x1000].PdoMapping.Should().BeFalse();
    }

    [Fact]
    public void PdoMapping_optional_MappedToTrue()
    {
        // Arrange
        const string xdd = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <DeviceIdentity><vendorName>T</vendorName><vendorID>0x1</vendorID><productName>T</productName><productID>0x1</productID></DeviceIdentity>
      <DeviceManager/><DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""0"" optionalObjects=""1"" manufacturerObjects=""0"">
          <CANopenObject index=""1001"" name=""Error Register"" objectType=""7"" dataType=""0005""
                         accessType=""ro"" PDOmapping=""optional""/>
        </CANopenObjectList>
      </ApplicationLayers>
      <TransportLayers><PhysicalLayer><baudRate defaultValue=""250 Kbps""/></PhysicalLayer></TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""0"" nrOfTxPDO=""0""
                                bootUpSlave=""false"" layerSettingServiceSlave=""false""
                                groupMessaging=""false"" dynamicChannels=""0""/>
        <CANopenMasterFeatures bootUpMaster=""false""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        // Act
        var result = _reader.ReadString(xdd);

        // Assert
        result.ObjectDictionary.Objects[0x1001].PdoMapping.Should().BeTrue();
    }

    #endregion

    #region ApplicationProcess Tests

    [Fact]
    public void ApplicationProcess_PreservedAsOpaqueXml()
    {
        // Arrange
        const string xdd = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <DeviceIdentity><vendorName>T</vendorName><vendorID>0x1</vendorID><productName>T</productName><productID>0x1</productID></DeviceIdentity>
      <DeviceManager/>
      <DeviceFunction/>
      <ApplicationProcess>
        <dataTypeList><dataType index=""1"" name=""BOOLEAN""/></dataTypeList>
      </ApplicationProcess>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007""
                         accessType=""ro"" PDOmapping=""no""/>
        </CANopenObjectList>
      </ApplicationLayers>
      <TransportLayers><PhysicalLayer><baudRate defaultValue=""250 Kbps""/></PhysicalLayer></TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""0"" nrOfTxPDO=""0""
                                bootUpSlave=""false"" layerSettingServiceSlave=""false""
                                groupMessaging=""false"" dynamicChannels=""0""/>
        <CANopenMasterFeatures bootUpMaster=""false""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        // Act
        var result = _reader.ReadString(xdd);

        // Assert
        result.ApplicationProcessXml.Should().NotBeNullOrEmpty();
        result.ApplicationProcessXml.Should().Contain("ApplicationProcess");
        result.ApplicationProcessXml.Should().Contain("BOOLEAN");
    }

    #endregion

    #region Sample File Integration Test

    [Fact]
    public void SampleDeviceXdd_ParsesSuccessfully()
    {
        // Act
        var result = _reader.ReadFile("Fixtures/sample_device.xdd");

        // Assert — FileInfo
        result.FileInfo.FileName.Should().Be("sample_device.xdd");
        result.FileInfo.FileVersion.Should().Be(1);

        // Assert — DeviceInfo
        result.DeviceInfo.VendorName.Should().Be("Example Automation Inc.");
        result.DeviceInfo.ProductName.Should().Be("IO-Module 16x16");
        result.DeviceInfo.VendorNumber.Should().Be(0x00000100u);
        result.DeviceInfo.ProductNumber.Should().Be(0x00001001u);

        // Assert — ObjectDictionary
        result.ObjectDictionary.MandatoryObjects.Should().Contain(0x1000);
        result.ObjectDictionary.MandatoryObjects.Should().Contain(0x1001);
        result.ObjectDictionary.Objects[0x1000].ParameterName.Should().Be("Device Type");
        result.ObjectDictionary.Objects[0x1001].ParameterName.Should().Be("Error Register");

        // Assert — DummyUsage
        result.ObjectDictionary.DummyUsage.Should().ContainKey(0x0002);

        // Assert — General Features
        result.DeviceInfo.Granularity.Should().Be(8);
        result.DeviceInfo.NrOfRxPdo.Should().Be(4);
        result.DeviceInfo.NrOfTxPdo.Should().Be(4);
        result.DeviceInfo.SimpleBootUpSlave.Should().BeTrue();

        // Assert — BaudRates
        result.DeviceInfo.SupportedBaudRates.BaudRate250.Should().BeTrue();
        result.DeviceInfo.SupportedBaudRates.BaudRate500.Should().BeTrue();
    }

    #endregion
}
