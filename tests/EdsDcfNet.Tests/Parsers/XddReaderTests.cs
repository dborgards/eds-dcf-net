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

    [Fact]
    public async Task ReadFileAsync_ValidXddFile_ParsesSuccessfully()
    {
        // Act
        var result = await _reader.ReadFileAsync("Fixtures/sample_device.xdd");

        // Assert
        result.Should().NotBeNull();
        result.FileInfo.Should().NotBeNull();
        result.DeviceInfo.Should().NotBeNull();
        result.ObjectDictionary.Should().NotBeNull();
    }

    [Fact]
    public async Task ReadFileAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Act
        var act = () => _reader.ReadFileAsync("NonExistent.xdd");

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
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

    #region Security Hardening Tests

    [Fact]
    public void ReadString_WithDoctype_ThrowsEdsParseException()
    {
        var xdd = MinimalXdd.Replace(
            @"<?xml version=""1.0"" encoding=""utf-8""?>",
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<!DOCTYPE ISO15745ProfileContainer [<!ENTITY test ""x"">]>");

        var act = () => _reader.ReadString(xdd);

        act.Should().Throw<EdsParseException>();
    }

    [Fact]
    public void ReadString_ContentExceedsMaximumSize_ThrowsEdsParseException()
    {
        var oversizedContent = new string('A', checked((int)IniParser.DefaultMaxInputSize + 1));

        var act = () => _reader.ReadString(oversizedContent);

        act.Should().Throw<EdsParseException>()
            .WithMessage("*too large*");
    }

    [Fact]
    public void ReadFile_ContentExceedsMaximumSize_ThrowsEdsParseException()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            using (var stream = new FileStream(tempFile, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                stream.SetLength(IniParser.DefaultMaxInputSize + 1);
            }

            var act = () => _reader.ReadFile(tempFile);

            act.Should().Throw<EdsParseException>()
                .WithMessage("*too large*");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
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
    public void ApplicationProcess_ParsedToTypedModel()
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
        <dataTypeList>
          <struct name=""MyStruct"" uniqueID=""uid_s1"">
            <varDeclaration name=""Field1"" uniqueID=""uid_v1""><BOOL/></varDeclaration>
          </struct>
          <enum name=""MyEnum"" uniqueID=""uid_e1"">
            <enumValue value=""0""/>
            <enumValue value=""1""/>
          </enum>
        </dataTypeList>
        <parameterList>
          <parameter uniqueID=""uid_p1"" access=""read"">
            <UINT/>
          </parameter>
        </parameterList>
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
        result.ApplicationProcess.Should().NotBeNull();
        result.ApplicationProcess!.DataTypeList.Should().NotBeNull();
        result.ApplicationProcess.DataTypeList!.Structs.Should().HaveCount(1);
        result.ApplicationProcess.DataTypeList.Structs[0].Name.Should().Be("MyStruct");
        result.ApplicationProcess.DataTypeList.Structs[0].UniqueId.Should().Be("uid_s1");
        result.ApplicationProcess.DataTypeList.Structs[0].VarDeclarations.Should().HaveCount(1);
        result.ApplicationProcess.DataTypeList.Structs[0].VarDeclarations[0].Name.Should().Be("Field1");
        result.ApplicationProcess.DataTypeList.Enums.Should().HaveCount(1);
        result.ApplicationProcess.DataTypeList.Enums[0].Name.Should().Be("MyEnum");
        result.ApplicationProcess.DataTypeList.Enums[0].EnumValues.Should().HaveCount(2);
        result.ApplicationProcess.ParameterList.Should().HaveCount(1);
        result.ApplicationProcess.ParameterList[0].UniqueId.Should().Be("uid_p1");
        result.ApplicationProcess.ParameterList[0].Access.Should().Be("read");
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

        // Assert — ApplicationProcess (typed model)
        result.ApplicationProcess.Should().NotBeNull();
        var ap = result.ApplicationProcess!;
        ap.DataTypeList.Should().NotBeNull();
        ap.DataTypeList!.Structs.Should().ContainSingle(s => s.Name == "IoStatus");
        ap.DataTypeList.Enums.Should().ContainSingle(e => e.Name == "ControlMode");
        ap.DataTypeList.Enums[0].EnumValues.Should().HaveCount(3);
        ap.ParameterList.Should().HaveCount(2);
        ap.ParameterList.Should().Contain(p => p.UniqueId == "uid_p_mode");
        ap.ParameterList.Should().Contain(p => p.UniqueId == "uid_p_status");
        ap.ParameterGroupList.Should().HaveCount(1);
        ap.ParameterGroupList[0].UniqueId.Should().Be("uid_pg_config");
        ap.ParameterGroupList[0].SubGroups.Should().HaveCount(1);
    }

    #endregion

    #region Additional Coverage Tests

    [Fact]
    public void ReadString_InvalidXml_ThrowsEdsParseException()
    {
        var act = () => _reader.ReadString("<not valid xml<<");

        act.Should().Throw<EdsParseException>();
    }

    [Fact]
    public void ParseDocument_ProfileWithNoProfileBodyChild_IsSkipped()
    {
        // A profile with no ProfileBody child should be silently skipped (line 82 continue).
        var xdd = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <!-- No ProfileBody child here — should be skipped -->
    <ProfileHeader><ProfileClassID>Device</ProfileClassID></ProfileHeader>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>CommunicationNetwork</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen""
                 fileName=""test.xdd"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""0"" optionalObjects=""0"" manufacturerObjects=""0""/>
      </ApplicationLayers>
      <TransportLayers>
        <PhysicalLayer>
          <baudRate defaultValue=""250 Kbps"">
            <supportedBaudRate value=""250 Kbps""/>
          </baudRate>
        </PhysicalLayer>
      </TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""0"" nrOfRxPDO=""0"" nrOfTxPDO=""0""
                                bootUpSlave=""false"" layerSettingServiceSlave=""false""
                                groupMessaging=""false"" dynamicChannels=""0""/>
        <CANopenMasterFeatures bootUpMaster=""false""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        var result = _reader.ReadString(xdd);

        // Should parse without error; device info will be default since device profile body was skipped
        result.Should().NotBeNull();
        result.DeviceInfo.VendorName.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ParseDeviceIdentity_NoDeviceIdentityElement_ReturnsEmptyDeviceInfo()
    {
        // ProfileBody_Device_CANopen without DeviceIdentity → DeviceInfo defaults
        var xdd = MinimalXdd.Replace(
            "<DeviceIdentity>" +
            "\n        <vendorName>Test Vendor</vendorName>" +
            "\n        <vendorID>0x00000100</vendorID>" +
            "\n        <productName>Test Product</productName>" +
            "\n        <productID>0x00001001</productID>" +
            "\n      </DeviceIdentity>",
            "<!-- no DeviceIdentity -->");

        var result = _reader.ReadString(xdd);

        result.DeviceInfo.VendorName.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ParseCanOpenObject_NoObjectType_DefaultsTo7()
    {
        // CANopenObject without objectType attribute → defaults to 0x7
        var xdd = MinimalXdd.Replace(
            @"<CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007""
                         accessType=""ro"" defaultValue=""0x00000000"" PDOmapping=""no""/>",
            @"<CANopenObject index=""1000"" name=""Device Type"" dataType=""0007""
                         accessType=""ro"" defaultValue=""0x00000000"" PDOmapping=""no""/>");

        var result = _reader.ReadString(xdd);

        result.ObjectDictionary.Objects[0x1000].ObjectType.Should().Be(0x7);
    }

    [Fact]
    public void ClassifyObject_ManufacturerRange_Classified()
    {
        // Object in manufacturer-specific range 0x2000-0x5FFF
        var xdd = MinimalXdd.Replace(
            @"<CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007""
                         accessType=""ro"" defaultValue=""0x00000000"" PDOmapping=""no""/>
        </CANopenObjectList>",
            @"<CANopenObjectList mandatoryObjects=""0"" optionalObjects=""0"" manufacturerObjects=""1"">
          <CANopenObject index=""2001"" name=""Mfr Object"" objectType=""7"" dataType=""0007""
                         accessType=""rw"" defaultValue=""0"" PDOmapping=""no""/>
        </CANopenObjectList>");

        var result = _reader.ReadString(xdd);

        result.ObjectDictionary.ManufacturerObjects.Should().Contain((ushort)0x2001);
    }

    [Fact]
    public void ParseDummyUsage_EntryWithoutEquals_IsSkipped()
    {
        // An entry element without '=' should be silently skipped
        var xdd = MinimalXdd.Replace(
            "</ApplicationLayers>",
            @"  <dummyUsage>
            <dummy entry=""InvalidEntryNoEquals""/>
            <dummy entry=""Dummy0002=1""/>
          </dummyUsage>
        </ApplicationLayers>");

        var result = _reader.ReadString(xdd);

        // Dummy0002 with valid format should still be parsed; invalid entry skipped
        result.ObjectDictionary.DummyUsage.Should().ContainKey((ushort)0x0002);
    }

    [Fact]
    public void ParseBaudRates_NoPhysicalLayer_NoException()
    {
        // TransportLayers with no PhysicalLayer child → returns silently
        var xdd = MinimalXdd.Replace(
            @"<TransportLayers>
        <PhysicalLayer>
          <baudRate defaultValue=""250 Kbps"">
            <supportedBaudRate value=""250 Kbps""/>
            <supportedBaudRate value=""500 Kbps""/>
          </baudRate>
        </PhysicalLayer>
      </TransportLayers>",
            "<TransportLayers/>");

        var result = _reader.ReadString(xdd);

        // Should parse without error; baud rates remain default
        result.DeviceInfo.SupportedBaudRates.BaudRate250.Should().BeFalse();
    }

    [Fact]
    public void ParseBaudRates_NoBaudRateElement_NoException()
    {
        // PhysicalLayer without baudRate child → returns silently
        var xdd = MinimalXdd.Replace(
            @"<PhysicalLayer>
          <baudRate defaultValue=""250 Kbps"">
            <supportedBaudRate value=""250 Kbps""/>
            <supportedBaudRate value=""500 Kbps""/>
          </baudRate>
        </PhysicalLayer>",
            "<PhysicalLayer/>");

        var result = _reader.ReadString(xdd);

        result.DeviceInfo.SupportedBaudRates.BaudRate250.Should().BeFalse();
    }

    [Fact]
    public void ParseBaudRates_800Kbps_Parsed()
    {
        // supportedBaudRate with value "800 Kbps"
        var xdd = MinimalXdd.Replace(
            "<supportedBaudRate value=\"500 Kbps\"/>",
            "<supportedBaudRate value=\"800 Kbps\"/>");

        var result = _reader.ReadString(xdd);

        result.DeviceInfo.SupportedBaudRates.BaudRate800.Should().BeTrue();
    }

    [Fact]
    public void ParseBaudRates_AllKnownRates_AreParsed()
    {
        var xdd = MinimalXdd
            .Replace(
                "<supportedBaudRate value=\"250 Kbps\"/>",
                "<supportedBaudRate value=\"10 Kbps\"/>\n            <supportedBaudRate value=\"20 Kbps\"/>\n            <supportedBaudRate value=\"50 Kbps\"/>\n            <supportedBaudRate value=\"125 Kbps\"/>\n            <supportedBaudRate value=\"250 Kbps\"/>")
            .Replace(
                "<supportedBaudRate value=\"500 Kbps\"/>",
                "<supportedBaudRate value=\"500 Kbps\"/>\n            <supportedBaudRate value=\"800 Kbps\"/>\n            <supportedBaudRate value=\"1000 Kbps\"/>");

        var result = _reader.ReadString(xdd);
        var baudRates = result.DeviceInfo.SupportedBaudRates;

        baudRates.BaudRate10.Should().BeTrue();
        baudRates.BaudRate20.Should().BeTrue();
        baudRates.BaudRate50.Should().BeTrue();
        baudRates.BaudRate125.Should().BeTrue();
        baudRates.BaudRate250.Should().BeTrue();
        baudRates.BaudRate500.Should().BeTrue();
        baudRates.BaudRate800.Should().BeTrue();
        baudRates.BaudRate1000.Should().BeTrue();
    }

    [Fact]
    public void ParseBaudRates_UnknownBaudRate_IsIgnored()
    {
        // An unknown baud rate string should not throw; kbps=0 is ignored by SetBaudRate
        var xdd = MinimalXdd.Replace(
            "<supportedBaudRate value=\"500 Kbps\"/>",
            "<supportedBaudRate value=\"999 Kbps\"/>");

        var result = _reader.ReadString(xdd);

        // BaudRate500 should be false since it was replaced with unknown value
        result.DeviceInfo.SupportedBaudRates.BaudRate500.Should().BeFalse();
    }

    [Fact]
    public void ParseDeviceCommissioning_HexNodeId_ParsedViaXdcReader()
    {
        // XDC with nodeID="0x05" (hex prefix) — covers the hex-parse branch in ParseDeviceCommissioning
        const string xdc = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>Device</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdc"" fileVersion=""1"">
      <DeviceIdentity><vendorName>V</vendorName></DeviceIdentity>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>CommunicationNetwork</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdc"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""0"" optionalObjects=""0"" manufacturerObjects=""0""/>
      </ApplicationLayers>
      <TransportLayers>
        <PhysicalLayer>
          <baudRate defaultValue=""250 Kbps""><supportedBaudRate value=""250 Kbps""/></baudRate>
        </PhysicalLayer>
      </TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""0"" nrOfRxPDO=""0"" nrOfTxPDO=""0""
                                bootUpSlave=""false"" layerSettingServiceSlave=""false""
                                groupMessaging=""false"" dynamicChannels=""0""/>
        <CANopenMasterFeatures bootUpMaster=""false""/>
        <deviceCommissioning nodeID=""0x05"" networkNumber=""0"" CANopenManager=""false""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        var dcf = new EdsDcfNet.Parsers.XdcReader().ReadString(xdc);

        dcf.DeviceCommissioning.NodeId.Should().Be(5);
    }

    [Fact]
    public void ParseDynamicChannels_WithChannels_Parsed()
    {
        // XDD with dynamicChannels in ApplicationLayers
        var xdd = MinimalXdd.Replace(
            "</ApplicationLayers>",
            @"  <dynamicChannels>
            <dynamicChannel dataType=""0007"" accessType=""ro"" startIndex=""1600"" endIndex=""17FF"" pDOmappingIndex=""2""/>
            <dynamicChannel dataType=""0004"" accessType=""rw"" startIndex=""2000""/>
          </dynamicChannels>
        </ApplicationLayers>");

        var result = _reader.ReadString(xdd);

        result.DynamicChannels.Should().NotBeNull();
        result.DynamicChannels!.Segments.Should().HaveCount(2);
        result.DynamicChannels.Segments[0].Range.Should().Be("1600-17FF");
        result.DynamicChannels.Segments[0].PPOffset.Should().Be(2);
    }

    [Fact]
    public void ReadString_XddActualValueAndDenotation_AreIgnored()
    {
        // XDD reader parses with includeActualValues=false, so actualValue/denotation must not be mapped.
        var xdd = MinimalXdd.Replace(
            @"<CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007""
                         accessType=""ro"" defaultValue=""0x00000000"" PDOmapping=""no""/>",
            @"<CANopenObjectList mandatoryObjects=""0"" optionalObjects=""1"" manufacturerObjects=""0"">
          <CANopenObject index=""1018"" name=""Identity"" objectType=""9"" subNumber=""1""
                         actualValue=""ignored"" denotation=""ignored-too"">
            <CANopenSubObject subIndex=""00"" name=""Count"" objectType=""7"" dataType=""0005""
                              accessType=""ro"" defaultValue=""1"" PDOmapping=""no""
                              actualValue=""7"" denotation=""SubText""/>
          </CANopenObject>");

        var result = _reader.ReadString(xdd);

        var obj = result.ObjectDictionary.Objects[0x1018];
        obj.ParameterValue.Should().BeNull();
        obj.Denotation.Should().BeNull();
        obj.SubObjects[0].ParameterValue.Should().BeNull();
        obj.SubObjects[0].Denotation.Should().BeNull();
    }

    [Fact]
    public void FileInfo_FileVersionWithMinorPart_UsesMajorAndParsesModificationMetadata()
    {
        var xdd = MinimalXdd.Replace(
            @"fileCreationDate=""2025-01-15"" fileVersion=""1""",
            @"fileCreationDate=""2025-01-15"" fileVersion=""7.3"" fileModifiedBy=""Editor"" fileModificationDate=""2025-02-20"" fileModificationTime=""16:45""");

        var result = _reader.ReadString(xdd);

        result.FileInfo.FileVersion.Should().Be(7);
        result.FileInfo.ModifiedBy.Should().Be("Editor");
        result.FileInfo.ModificationDate.Should().Be("02-20-2025");
        result.FileInfo.ModificationTime.Should().Be("16:45");
    }

    [Fact]
    public void ParseNetworkManagement_InvalidNumericValues_AreIgnored_BooleanOnesAreAccepted()
    {
        var xdd = MinimalXdd
            .Replace(@"granularity=""8""", @"granularity=""invalid""")
            .Replace(@"nrOfRxPDO=""2""", @"nrOfRxPDO=""oops""")
            .Replace(@"nrOfTxPDO=""2""", @"nrOfTxPDO=""nope""")
            .Replace(@"dynamicChannels=""0""", @"dynamicChannels=""bad""")
            .Replace(@"bootUpSlave=""true""", @"bootUpSlave=""1""")
            .Replace(@"layerSettingServiceSlave=""false""", @"layerSettingServiceSlave=""1""")
            .Replace(@"groupMessaging=""false""", @"groupMessaging=""1""")
            .Replace(@"bootUpMaster=""false""", @"bootUpMaster=""1""");

        var result = _reader.ReadString(xdd);

        result.DeviceInfo.Granularity.Should().Be(8);
        result.DeviceInfo.NrOfRxPdo.Should().Be(0);
        result.DeviceInfo.NrOfTxPdo.Should().Be(0);
        result.DeviceInfo.DynamicChannelsSupported.Should().Be(0);
        result.DeviceInfo.SimpleBootUpSlave.Should().BeTrue();
        result.DeviceInfo.LssSupported.Should().BeTrue();
        result.DeviceInfo.GroupMessaging.Should().BeTrue();
        result.DeviceInfo.SimpleBootUpMaster.Should().BeTrue();
    }

    [Fact]
    public void ParseDynamicChannels_EndIndexWithoutStartIndex_DoesNotBuildRangeAndInvalidOffsetIgnored()
    {
        var xdd = MinimalXdd.Replace(
            "</ApplicationLayers>",
            @"  <dynamicChannels>
            <dynamicChannel dataType=""0007"" accessType=""ro"" endIndex=""17FF"" pDOmappingIndex=""not-a-number""/>
          </dynamicChannels>
        </ApplicationLayers>");

        var result = _reader.ReadString(xdd);

        result.DynamicChannels.Should().NotBeNull();
        result.DynamicChannels!.Segments.Should().HaveCount(1);
        result.DynamicChannels.Segments[0].Type.Should().Be(0x0007);
        result.DynamicChannels.Segments[0].Dir.Should().Be(AccessType.ReadOnly);
        result.DynamicChannels.Segments[0].Range.Should().BeEmpty();
        result.DynamicChannels.Segments[0].PPOffset.Should().Be(0);
    }

    [Fact]
    public void ParseXddAccessType_Unknown_DefaultsToReadOnly()
    {
        // An unrecognized accessType should fall through to the default (ReadOnly)
        var xdd = MinimalXdd.Replace(
            @"accessType=""ro""",
            @"accessType=""custom""");

        var result = _reader.ReadString(xdd);

        result.ObjectDictionary.Objects[0x1000].AccessType.Should().Be(AccessType.ReadOnly);
    }

    [Fact]
    public void ParseHexIndex_WithPrefix_ParsedCorrectly()
    {
        // index="0x1000" (with 0x prefix) should parse to 0x1000
        var xdd = MinimalXdd.Replace(
            @"index=""1000""",
            @"index=""0x1000""");

        var result = _reader.ReadString(xdd);

        result.ObjectDictionary.Objects.Should().ContainKey((ushort)0x1000);
    }

    [Fact]
    public void ConvertXsdDateToEds_NonIsoFormat_ReturnedAsIs()
    {
        // fileCreationDate not matching "YYYY-MM-DD" pattern → passed through unchanged
        var xdd = MinimalXdd.Replace(
            @"fileCreationDate=""2025-01-15""",
            @"fileCreationDate=""15/01/2025""");

        var result = _reader.ReadString(xdd);

        // The non-ISO date is passed through as-is
        result.FileInfo.CreationDate.Should().Be("15/01/2025");
    }

    [Fact]
    public void ParseDocument_WithoutRoot_ThrowsEdsParseException()
    {
        var emptyDoc = new System.Xml.Linq.XDocument();
        var method = typeof(XddReader).GetMethod(
            "ParseDocument",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.Should().NotBeNull();

        var act = () => method!.Invoke(null, new object[] { emptyDoc, false });

        act.Should().Throw<System.Reflection.TargetInvocationException>()
            .WithInnerException<EdsParseException>()
            .WithMessage("*no root element*");
    }

    [Fact]
    public void ParseCanOpenSubObject_InvalidObjectType_DefaultsToVar()
    {
        // Invalid objectType should fall back to 0x7 in sub-object parsing.
        var xdd = MinimalXdd.Replace(
            @"<CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007""
                         accessType=""ro"" defaultValue=""0x00000000"" PDOmapping=""no""/>",
            @"<CANopenObjectList mandatoryObjects=""0"" optionalObjects=""1"" manufacturerObjects=""0"">
          <CANopenObject index=""1018"" name=""Identity"" objectType=""9"" subNumber=""1"">
            <CANopenSubObject subIndex=""00"" name=""Count"" objectType=""invalid"" dataType=""0005""
                              accessType=""ro"" defaultValue=""1"" PDOmapping=""no""/>
          </CANopenObject>");

        var result = _reader.ReadString(xdd);

        result.ObjectDictionary.Objects[0x1018].SubObjects[0].ObjectType.Should().Be(0x7);
    }

    [Fact]
    public void ParseDummyUsage_InvalidKeyFormat_IsSkipped()
    {
        var xdd = File.ReadAllText("Fixtures/sample_device.xdd")
            .Replace(@"<dummy entry=""Dummy0005=1""/>", @"<dummy entry=""Bad0005=1""/>");

        var result = _reader.ReadString(xdd);

        result.ObjectDictionary.DummyUsage.Should().ContainKey(0x0002);
        result.ObjectDictionary.DummyUsage.Should().NotContainKey(0x0005);
    }

    [Fact]
    public void ParseDummyUsage_InvalidHexSuffix_IsSkipped()
    {
        var xdd = File.ReadAllText("Fixtures/sample_device.xdd")
            .Replace(@"<dummy entry=""Dummy0005=1""/>", @"<dummy entry=""DummyGGGG=1""/>");

        var result = _reader.ReadString(xdd);

        result.ObjectDictionary.DummyUsage.Should().ContainKey(0x0002);
        result.ObjectDictionary.DummyUsage.Should().NotContainKey(0x0005);
    }

    [Fact]
    public void ParseHexDataType_WithPrefix_ParsedCorrectly()
    {
        var xdd = MinimalXdd.Replace(@"dataType=""0007""", @"dataType=""0x0007""");

        var result = _reader.ReadString(xdd);

        result.ObjectDictionary.Objects[0x1000].DataType.Should().Be(0x0007);
    }

    [Fact]
    public void ParseHexDataType_InvalidValue_DefaultsToZero()
    {
        var xdd = MinimalXdd.Replace(@"dataType=""0007""", @"dataType=""nope""");

        var result = _reader.ReadString(xdd);

        result.ObjectDictionary.Objects[0x1000].DataType.Should().Be(0);
    }

    [Fact]
    public void ParseHexId_InvalidVendorId_DefaultsToZero()
    {
        var xdd = MinimalXdd.Replace(@"<vendorID>0x00000100</vendorID>", @"<vendorID>nothex</vendorID>");

        var result = _reader.ReadString(xdd);

        result.DeviceInfo.VendorNumber.Should().Be(0);
    }

    [Fact]
    public void FileInfo_MissingAttributes_DefaultsAreUsed()
    {
        var xdd = MinimalXdd.Replace(
            @"<ProfileBody xsi:type=""ProfileBody_Device_CANopen""
                 fileName=""test.xdd"" fileCreator=""TestCreator""
                 fileCreationDate=""2025-01-15"" fileVersion=""1"">",
            @"<ProfileBody xsi:type=""ProfileBody_Device_CANopen"">");

        var result = _reader.ReadString(xdd);

        result.FileInfo.FileName.Should().BeEmpty();
        result.FileInfo.CreatedBy.Should().BeEmpty();
        result.FileInfo.FileVersion.Should().Be(1);
    }

    [Fact]
    public void ParseDeviceIdentity_MissingChildren_DefaultsToEmptyOrZero()
    {
        var xdd = MinimalXdd.Replace(
            @"<DeviceIdentity>
        <vendorName>Test Vendor</vendorName>
        <vendorID>0x00000100</vendorID>
        <productName>Test Product</productName>
        <productID>0x00001001</productID>
      </DeviceIdentity>",
            @"<DeviceIdentity>
        <vendorName>Only Vendor Name</vendorName>
      </DeviceIdentity>");

        var result = _reader.ReadString(xdd);

        result.DeviceInfo.VendorName.Should().Be("Only Vendor Name");
        result.DeviceInfo.VendorNumber.Should().Be(0);
        result.DeviceInfo.ProductName.Should().BeEmpty();
        result.DeviceInfo.ProductNumber.Should().Be(0);
    }

    [Fact]
    public void ParseCanOpenObject_MissingIndexAndName_DefaultsAreApplied()
    {
        var xdd = MinimalXdd.Replace(
            @"<CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007""
                         accessType=""ro"" defaultValue=""0x00000000"" PDOmapping=""no""/>
        </CANopenObjectList>",
            @"<CANopenObjectList mandatoryObjects=""0"" optionalObjects=""1"" manufacturerObjects=""0"">
          <CANopenObject objectType=""9"">
            <CANopenSubObject/>
          </CANopenObject>
        </CANopenObjectList>");

        var result = _reader.ReadString(xdd);

        result.ObjectDictionary.Objects.Should().ContainKey(0);
        var obj = result.ObjectDictionary.Objects[0];
        obj.ParameterName.Should().BeEmpty();
        obj.ObjectType.Should().Be(9);
        obj.SubObjects.Should().ContainKey(0);

        var sub = obj.SubObjects[0];
        sub.SubIndex.Should().Be(0);
        sub.ParameterName.Should().BeEmpty();
        sub.ObjectType.Should().Be(0x7);
        sub.DataType.Should().Be(0);
        sub.AccessType.Should().Be(AccessType.ReadOnly);
        sub.DefaultValue.Should().BeNull();
        sub.LowLimit.Should().BeNull();
        sub.HighLimit.Should().BeNull();
        sub.PdoMapping.Should().BeFalse();
    }

    [Fact]
    public void ParseCanOpenSubObject_WithHexPrefixedSubIndex_ParsesCorrectly()
    {
        var xdd = MinimalXdd.Replace(
            @"<CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007""
                         accessType=""ro"" defaultValue=""0x00000000"" PDOmapping=""no""/>
        </CANopenObjectList>",
            @"<CANopenObjectList mandatoryObjects=""0"" optionalObjects=""1"" manufacturerObjects=""0"">
          <CANopenObject index=""1018"" name=""Identity"" objectType=""9"" subNumber=""2"">
            <CANopenSubObject subIndex=""0x01"" name=""Vendor ID"" objectType=""7"" dataType=""0007"" accessType=""ro"" PDOmapping=""no""/>
          </CANopenObject>
        </CANopenObjectList>");

        var result = _reader.ReadString(xdd);

        result.ObjectDictionary.Objects[0x1018].SubObjects.Should().ContainKey(1);
    }

    [Fact]
    public void ParseCanOpenSubObject_IncludeActualValuesWithoutAttributes_LeavesValuesNull()
    {
        const string xdc = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>Device</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdc"" fileVersion=""1"">
      <DeviceIdentity><vendorName>V</vendorName></DeviceIdentity>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>CommunicationNetwork</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdc"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""0"" optionalObjects=""1"" manufacturerObjects=""0"">
          <CANopenObject index=""1018"" name=""Identity"" objectType=""9"" subNumber=""1"">
            <CANopenSubObject subIndex=""00"" name=""Count"" objectType=""7"" dataType=""0005"" accessType=""ro"" PDOmapping=""no""/>
          </CANopenObject>
        </CANopenObjectList>
      </ApplicationLayers>
      <TransportLayers><PhysicalLayer><baudRate defaultValue=""250 Kbps""/></PhysicalLayer></TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""0"" nrOfTxPDO=""0"" bootUpSlave=""false"" layerSettingServiceSlave=""false"" groupMessaging=""false"" dynamicChannels=""0""/>
        <CANopenMasterFeatures bootUpMaster=""false""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        var dcf = new XdcReader().ReadString(xdc);

        var sub = dcf.ObjectDictionary.Objects[0x1018].SubObjects[0];
        sub.ParameterValue.Should().BeNull();
        sub.Denotation.Should().BeNull();
    }

    [Fact]
    public void ParseCanOpenSubObject_LowAndHighLimits_AreParsed()
    {
        var xdd = MinimalXdd.Replace(
            @"<CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007""
                         accessType=""ro"" defaultValue=""0x00000000"" PDOmapping=""no""/>
        </CANopenObjectList>",
            @"<CANopenObjectList mandatoryObjects=""0"" optionalObjects=""1"" manufacturerObjects=""0"">
          <CANopenObject index=""1018"" name=""Identity"" objectType=""9"" subNumber=""1"">
            <CANopenSubObject subIndex=""00"" name=""Count"" objectType=""7"" dataType=""0005""
                              accessType=""ro"" lowLimit=""1"" highLimit=""9"" PDOmapping=""no""/>
          </CANopenObject>
        </CANopenObjectList>");

        var result = _reader.ReadString(xdd);

        var sub = result.ObjectDictionary.Objects[0x1018].SubObjects[0];
        sub.LowLimit.Should().Be("1");
        sub.HighLimit.Should().Be("9");
    }

    [Fact]
    public void ParseDummyUsage_EntryAttributeMissing_IsSkipped()
    {
        var xdd = MinimalXdd.Replace(
            "</ApplicationLayers>",
            @"  <dummyUsage>
            <dummy/>
            <dummy entry=""Dummy0002=1""/>
          </dummyUsage>
        </ApplicationLayers>");

        var result = _reader.ReadString(xdd);

        result.ObjectDictionary.DummyUsage.Should().ContainKey((ushort)0x0002);
        result.ObjectDictionary.DummyUsage.Should().HaveCount(1);
    }

    [Fact]
    public void ParseDynamicChannels_EmptyElement_ResultsInNull()
    {
        var xdd = MinimalXdd.Replace(
            "</ApplicationLayers>",
            @"  <dynamicChannels/>
        </ApplicationLayers>");

        var result = _reader.ReadString(xdd);

        result.DynamicChannels.Should().BeNull();
    }

    [Fact]
    public void ParseDynamicChannels_ChannelWithoutTypeAndAccess_UsesDefaults()
    {
        var xdd = MinimalXdd.Replace(
            "</ApplicationLayers>",
            @"  <dynamicChannels>
            <dynamicChannel startIndex=""2000""/>
          </dynamicChannels>
        </ApplicationLayers>");

        var result = _reader.ReadString(xdd);

        result.DynamicChannels.Should().NotBeNull();
        result.DynamicChannels!.Segments.Should().ContainSingle();
        result.DynamicChannels.Segments[0].Type.Should().Be(0);
        result.DynamicChannels.Segments[0].Dir.Should().Be(AccessType.ReadOnly);
        result.DynamicChannels.Segments[0].Range.Should().Be("2000");
    }

    [Fact]
    public void ParseBaudRates_MissingValueIsIgnored_AndTenKbpsIsParsed()
    {
        var xdd = MinimalXdd.Replace(
            @"<baudRate defaultValue=""250 Kbps"">
            <supportedBaudRate value=""250 Kbps""/>
            <supportedBaudRate value=""500 Kbps""/>
          </baudRate>",
            @"<baudRate defaultValue=""250 Kbps"">
            <supportedBaudRate/>
            <supportedBaudRate value=""10 Kbps""/>
          </baudRate>");

        var result = _reader.ReadString(xdd);

        result.DeviceInfo.SupportedBaudRates.BaudRate10.Should().BeTrue();
        result.DeviceInfo.SupportedBaudRates.BaudRate250.Should().BeFalse();
        result.DeviceInfo.SupportedBaudRates.BaudRate500.Should().BeFalse();
    }

    [Fact]
    public void ParseBaudRates_AllSupportedValues_AreMapped()
    {
        var xdd = MinimalXdd.Replace(
            @"<baudRate defaultValue=""250 Kbps"">
            <supportedBaudRate value=""250 Kbps""/>
            <supportedBaudRate value=""500 Kbps""/>
          </baudRate>",
            @"<baudRate defaultValue=""250 Kbps"">
            <supportedBaudRate value=""10 Kbps""/>
            <supportedBaudRate value=""20 Kbps""/>
            <supportedBaudRate value=""50 Kbps""/>
            <supportedBaudRate value=""125 Kbps""/>
            <supportedBaudRate value=""250 Kbps""/>
            <supportedBaudRate value=""500 Kbps""/>
            <supportedBaudRate value=""800 Kbps""/>
            <supportedBaudRate value=""1000 Kbps""/>
          </baudRate>");

        var result = _reader.ReadString(xdd);

        result.DeviceInfo.SupportedBaudRates.BaudRate10.Should().BeTrue();
        result.DeviceInfo.SupportedBaudRates.BaudRate20.Should().BeTrue();
        result.DeviceInfo.SupportedBaudRates.BaudRate50.Should().BeTrue();
        result.DeviceInfo.SupportedBaudRates.BaudRate125.Should().BeTrue();
        result.DeviceInfo.SupportedBaudRates.BaudRate250.Should().BeTrue();
        result.DeviceInfo.SupportedBaudRates.BaudRate500.Should().BeTrue();
        result.DeviceInfo.SupportedBaudRates.BaudRate800.Should().BeTrue();
        result.DeviceInfo.SupportedBaudRates.BaudRate1000.Should().BeTrue();
    }

    [Fact]
    public void ParseNetworkManagement_MissingAttributes_KeepDeviceDefaults()
    {
        var xdd = MinimalXdd.Replace(
            @"<CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""2"" nrOfTxPDO=""2""
                                bootUpSlave=""true"" layerSettingServiceSlave=""false""
                                groupMessaging=""false"" dynamicChannels=""0""/>",
            @"<CANopenGeneralFeatures/>")
            .Replace(@"<CANopenMasterFeatures bootUpMaster=""false""/>", @"<CANopenMasterFeatures/>");

        var result = _reader.ReadString(xdd);

        result.DeviceInfo.Granularity.Should().Be(8);
        result.DeviceInfo.NrOfRxPdo.Should().Be(0);
        result.DeviceInfo.NrOfTxPdo.Should().Be(0);
        result.DeviceInfo.SimpleBootUpSlave.Should().BeFalse();
        result.DeviceInfo.GroupMessaging.Should().BeFalse();
        result.DeviceInfo.LssSupported.Should().BeFalse();
        result.DeviceInfo.DynamicChannelsSupported.Should().Be(0);
        result.DeviceInfo.SimpleBootUpMaster.Should().BeFalse();
    }

    [Fact]
    public void ParseDeviceCommissioning_OptionalNetworkNumberAndManager_MissingUseDefaults()
    {
        const string xdc = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdc"" fileVersion=""1"">
      <DeviceIdentity><vendorName>V</vendorName></DeviceIdentity>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdc"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""0"" optionalObjects=""0"" manufacturerObjects=""0""/>
      </ApplicationLayers>
      <TransportLayers><PhysicalLayer><baudRate defaultValue=""250 Kbps""/></PhysicalLayer></TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""0"" nrOfTxPDO=""0"" bootUpSlave=""false"" layerSettingServiceSlave=""false"" groupMessaging=""false"" dynamicChannels=""0""/>
        <CANopenMasterFeatures bootUpMaster=""false""/>
        <deviceCommissioning nodeID=""5""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        var dcf = new XdcReader().ReadString(xdc);

        dcf.DeviceCommissioning.NodeId.Should().Be(5);
        dcf.DeviceCommissioning.NetNumber.Should().Be(0u);
        dcf.DeviceCommissioning.CANopenManager.Should().BeFalse();
    }

    #endregion

    #region Malformed required hex fields

    [Theory]
    [InlineData("ZZZZ")]
    [InlineData("GGGG")]
    [InlineData("not-hex")]
    public void ParseCanOpenObject_MalformedIndex_ThrowsEdsParseException(string badIndex)
    {
        var xdd = MinimalXdd.Replace(
            @"<CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000""",
            $@"<CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""{badIndex}""");

        var act = () => _reader.ReadString(xdd);

        act.Should().Throw<EdsParseException>()
            .WithMessage("*index*");
    }

    [Theory]
    [InlineData("ZZ")]
    [InlineData("GG")]
    [InlineData("not-hex")]
    public void ParseCanOpenSubObject_MalformedSubIndex_ThrowsEdsParseException(string badSubIndex)
    {
        var xdd = MinimalXdd.Replace(
            @"<CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007""
                         accessType=""ro"" defaultValue=""0x00000000"" PDOmapping=""no""/>",
            $@"<CANopenObjectList mandatoryObjects=""0"" optionalObjects=""1"" manufacturerObjects=""0"">
          <CANopenObject index=""1018"" name=""Identity"" objectType=""9"" subNumber=""1"">
            <CANopenSubObject subIndex=""{badSubIndex}"" name=""Count"" objectType=""7"" dataType=""0005""
                              accessType=""ro"" defaultValue=""0"" PDOmapping=""no""/>
          </CANopenObject>");

        var act = () => _reader.ReadString(xdd);

        act.Should().Throw<EdsParseException>()
            .WithMessage("*subIndex*");
    }

    #endregion
}
