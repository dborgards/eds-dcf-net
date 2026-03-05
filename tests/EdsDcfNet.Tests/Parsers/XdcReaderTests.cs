namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;

public class XdcReaderTests
{
    private readonly XdcReader _reader = new();

    private const string MinimalXdc = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>Device</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen""
                 fileName=""test.xdc"" fileCreator=""TestCreator""
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
    <ProfileHeader><ProfileClassID>CommunicationNetwork</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen""
                 fileName=""test.xdc"" fileCreator=""TestCreator""
                 fileCreationDate=""2025-01-15"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007""
                         accessType=""ro"" defaultValue=""0x00000000""
                         PDOmapping=""no"" actualValue=""0x00000191"" denotation=""MyDevice""/>
        </CANopenObjectList>
      </ApplicationLayers>
      <TransportLayers>
        <PhysicalLayer>
          <baudRate defaultValue=""500 Kbps"">
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
        <deviceCommissioning nodeID=""3"" nodeName=""MyDevice""
                             actualBaudRate=""500 Kbps"" networkNumber=""1""
                             networkName=""CANopen Network"" CANopenManager=""false""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

    /// <summary>
    /// Minimal XDC with an unknown ProfileBody child so that AdditionalSections is non-empty
    /// and XdcReader's clone loop is exercised (Codecov patch coverage for PR #168).
    /// </summary>
    private const string MinimalXdcWithAdditionalSection = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>Device</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""test.xdc"" fileVersion=""1"">
      <DeviceIdentity><vendorName>V</vendorName><vendorID>0x1</vendorID><productName>P</productName><productID>0x1</productID></DeviceIdentity>
      <DeviceManager/><DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>CommunicationNetwork</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""test.xdc"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""0"" optionalObjects=""0"" manufacturerObjects=""0"">
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
        <deviceCommissioning nodeID=""1"" networkNumber=""0"" CANopenManager=""false""/>
      </NetworkManagement>
      <VendorExtension customKey=""customValue"" AnotherAttr=""other""/>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

    #region ReadFile Tests

    [Fact]
    public void ReadFile_ValidXdcFile_ParsesSuccessfully()
    {
        // Act
        var result = _reader.ReadFile("Fixtures/minimal.xdc");

        // Assert
        result.Should().NotBeNull();
        result.FileInfo.Should().NotBeNull();
        result.DeviceInfo.Should().NotBeNull();
        result.ObjectDictionary.Should().NotBeNull();
        result.DeviceCommissioning.Should().NotBeNull();
    }

    [Fact]
    public void ReadFile_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Act
        var act = () => _reader.ReadFile("NonExistent.xdc");

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public async Task ReadFileAsync_ValidXdcFile_ParsesSuccessfully()
    {
        // Act
        var result = await _reader.ReadFileAsync("Fixtures/minimal.xdc");

        // Assert
        result.Should().NotBeNull();
        result.FileInfo.Should().NotBeNull();
        result.DeviceInfo.Should().NotBeNull();
        result.ObjectDictionary.Should().NotBeNull();
        result.DeviceCommissioning.Should().NotBeNull();
    }

    [Fact]
    public async Task ReadFileAsync_Utf16EncodedXdc_ParsesSuccessfully()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(tempFile, MinimalXdc, System.Text.Encoding.Unicode);

            var syncResult = _reader.ReadFile(tempFile);
            var asyncResult = await _reader.ReadFileAsync(tempFile);

            asyncResult.FileInfo.FileName.Should().Be(syncResult.FileInfo.FileName);
            asyncResult.DeviceCommissioning.NodeId.Should().Be(3);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadFileAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        const string filePath = "NonExistent.xdc";

        // Act
        var act = () => _reader.ReadFileAsync(filePath);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>()
            .Where(ex => ex.FileName == filePath);
    }

    [Fact]
    public async Task ReadFileAsync_ContentExceedsCustomMaximumSize_ThrowsEdsParseException()
    {
        const string path = "Fixtures/minimal.xdc";
        var fileLength = new FileInfo(path).Length;
        var act = () => _reader.ReadFileAsync(path, maxInputSize: fileLength - 1);

        await act.Should().ThrowAsync<EdsParseException>()
            .WithMessage("*too large*");
    }

    #endregion

    #region ReadString Tests

    [Fact]
    public void ReadString_ValidXdcContent_ParsesSuccessfully()
    {
        // Act
        var result = _reader.ReadString(MinimalXdc);

        // Assert
        result.Should().NotBeNull();
        result.FileInfo.FileName.Should().Be("test.xdc");
        result.DeviceInfo.VendorName.Should().Be("Test Vendor");
    }

    [Fact]
    public void ReadString_MissingCommunicationNetworkProfile_ThrowsException()
    {
        // Arrange
        const string xdcNoCommNet = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdc"" fileVersion=""1"">
      <DeviceIdentity>
        <vendorName>T</vendorName><vendorID>0x1</vendorID>
        <productName>T</productName><productID>0x1</productID>
      </DeviceIdentity>
      <DeviceManager/><DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        // Act
        var act = () => _reader.ReadString(xdcNoCommNet);

        // Assert
        act.Should().Throw<EdsParseException>();
    }

    #endregion

    #region Security Hardening Tests

    [Fact]
    public void ReadString_WithDoctype_ThrowsEdsParseException()
    {
        var xdc = MinimalXdc.Replace(
            @"<?xml version=""1.0"" encoding=""utf-8""?>",
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<!DOCTYPE ISO15745ProfileContainer [<!ENTITY test ""x"">]>");

        var act = () => _reader.ReadString(xdc);

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
    public void ReadString_ContentExceedsCustomMaximumSize_ThrowsEdsParseException()
    {
        var contentLength = MinimalXdc.Length;
        var act = () => _reader.ReadString(MinimalXdc, maxInputSize: contentLength - 1);

        act.Should().Throw<EdsParseException>()
            .WithMessage("*too large*");
    }

    [Fact]
    public void ReadString_ContentWithinCustomMaximumSize_ParsesSuccessfully()
    {
        var result = _reader.ReadString(MinimalXdc, maxInputSize: MinimalXdc.Length + 10);

        result.FileInfo.FileName.Should().Be("test.xdc");
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

    [Fact]
    public void ReadFile_ContentExceedsCustomMaximumSize_ThrowsEdsParseException()
    {
        const string path = "Fixtures/minimal.xdc";
        var fileLength = new FileInfo(path).Length;
        var act = () => _reader.ReadFile(path, maxInputSize: fileLength - 1);

        act.Should().Throw<EdsParseException>()
            .WithMessage("*too large*");
    }

    #endregion

    #region ActualValue / Denotation Tests

    [Fact]
    public void ActualValue_MappedToParameterValue()
    {
        // Act
        var result = _reader.ReadString(MinimalXdc);

        // Assert
        result.ObjectDictionary.Objects[0x1000].ParameterValue.Should().Be("0x00000191");
    }

    [Fact]
    public void Denotation_MappedCorrectly()
    {
        // Act
        var result = _reader.ReadString(MinimalXdc);

        // Assert
        result.ObjectDictionary.Objects[0x1000].Denotation.Should().Be("MyDevice");
    }

    #endregion

    #region DeviceCommissioning Tests

    [Fact]
    public void DeviceCommissioning_ParsesAllFields()
    {
        // Act
        var result = _reader.ReadString(MinimalXdc);

        // Assert
        result.DeviceCommissioning.Should().NotBeNull();
        result.DeviceCommissioning.NodeId.Should().Be(3);
        result.DeviceCommissioning.NodeName.Should().Be("MyDevice");
        result.DeviceCommissioning.Baudrate.Should().Be(500);
        result.DeviceCommissioning.NetNumber.Should().Be(1u);
        result.DeviceCommissioning.NetworkName.Should().Be("CANopen Network");
        result.DeviceCommissioning.CANopenManager.Should().BeFalse();
    }

    [Fact]
    public void DeviceCommissioning_MissingElement_ReturnsDefaultCommissioning()
    {
        // Arrange — XDC without deviceCommissioning element
        const string xdcNoCommissioning = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdc"" fileVersion=""1"">
      <DeviceIdentity><vendorName>T</vendorName><vendorID>0x1</vendorID><productName>T</productName><productID>0x1</productID></DeviceIdentity>
      <DeviceManager/><DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdc"" fileVersion=""1"">
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
        var result = _reader.ReadString(xdcNoCommissioning);

        // Assert — should return a default DeviceCommissioning (NodeId = 0)
        result.DeviceCommissioning.Should().NotBeNull();
        result.DeviceCommissioning.NodeId.Should().Be(0);
    }

    #endregion

    #region Sample File Integration Test

    [Fact]
    public void MinimalXdc_ParsesSuccessfully()
    {
        // Act
        var result = _reader.ReadFile("Fixtures/minimal.xdc");

        // Assert — commissioning
        result.DeviceCommissioning.NodeId.Should().Be(5);
        result.DeviceCommissioning.Baudrate.Should().Be(500);
        result.DeviceCommissioning.NodeName.Should().Be("MinimalDevice");

        // Assert — actual values
        result.ObjectDictionary.Objects[0x1000].ParameterValue.Should().Be("0x00000191");
        result.ObjectDictionary.Objects[0x1001].ParameterValue.Should().Be("0");
    }

    #endregion

    #region Additional Coverage Tests

    [Fact]
    public void ReadString_XdcWithUnknownProfileBodyChild_CopiesAdditionalSectionsWithCaseInsensitiveClone()
    {
        // XddReader puts unknown ProfileBody children into AdditionalSections; XdcReader clones them
        // via AdditionalSectionsCloner.CloneSectionEntriesCaseInsensitive (patch coverage for PR #168).
        var result = _reader.ReadString(MinimalXdcWithAdditionalSection);

        result.AdditionalSections.Should().ContainKey("VendorExtension");
        var section = result.AdditionalSections["VendorExtension"];
        section.Should().ContainKey("customKey");
        section["customKey"].Should().Be("customValue");
        section.Should().ContainKey("AnotherAttr");
        section["AnotherAttr"].Should().Be("other");
        section.Should().ContainKey("anotherattr"); // case-insensitive clone
        section["anotherattr"].Should().Be("other");
    }

    [Fact]
    public void ReadString_InvalidXml_ThrowsEdsParseException()
    {
        var act = () => _reader.ReadString("<not valid xml<<");

        act.Should().Throw<EdsParseException>();
    }

    [Fact]
    public void ParseDeviceCommissioning_DocumentWithoutRoot_ReturnsNull()
    {
        var method = typeof(XdcReader).GetMethod(
            "ParseDeviceCommissioning",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.Should().NotBeNull();

        var result = method!.Invoke(null, new object[] { new System.Xml.Linq.XDocument() });

        result.Should().BeNull();
    }

    [Fact]
    public void ParseDeviceCommissioning_NoNetworkManagementElement_ReturnsDefault()
    {
        // XDC without a deviceCommissioning element → ParseDeviceCommissioning returns null → default
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
        <!-- no deviceCommissioning element -->
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        var result = _reader.ReadString(xdc);

        // No deviceCommissioning element → ParseDeviceCommissioning returns null → default (NodeId=0)
        result.DeviceCommissioning.NodeId.Should().Be(0);
    }

    [Fact]
    public void ParseDeviceCommissioning_ProfileBodyWithoutTypeAttribute_IsSkipped()
    {
        // A ProfileBody without xsi:type attribute is not recognized as CommNet → commissioning defaults
        const string xdc = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer>
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>Device</ProfileClassID></ProfileHeader>
    <ProfileBody fileName=""t.xdc"" fileVersion=""1"">
      <DeviceIdentity><vendorName>V</vendorName></DeviceIdentity>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>CommunicationNetwork</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen""
                 xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
                 fileName=""t.xdc"" fileVersion=""1"">
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
        <deviceCommissioning nodeID=""7"" networkNumber=""0"" CANopenManager=""false""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        var result = _reader.ReadString(xdc);

        // CommNet profile body was found and commissioning parsed
        result.DeviceCommissioning.NodeId.Should().Be(7);
    }

    [Fact]
    public void ParseSubObjectActualValueAndDenotation_ViaXdc()
    {
        // XDC sub-object with actualValue AND denotation
        var xdc = MinimalXdc.Replace(
            @"<CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007""
                         accessType=""ro"" defaultValue=""0x00000000""
                         PDOmapping=""no"" actualValue=""0x00000191"" denotation=""MyDevice""/>
        </CANopenObjectList>",
            @"<CANopenObjectList mandatoryObjects=""0"" optionalObjects=""1"" manufacturerObjects=""0"">
          <CANopenObject index=""1018"" name=""Identity"" objectType=""9"" subNumber=""2"">
            <CANopenSubObject subIndex=""00"" name=""Count"" objectType=""7"" dataType=""0005""
                              accessType=""ro"" defaultValue=""1""
                              PDOmapping=""no"" actualValue=""1"" denotation=""SubDenotation""/>
          </CANopenObject>
        </CANopenObjectList>");

        var result = _reader.ReadString(xdc);

        result.ObjectDictionary.Objects[0x1018].SubObjects[0].ParameterValue.Should().Be("1");
        result.ObjectDictionary.Objects[0x1018].SubObjects[0].Denotation.Should().Be("SubDenotation");
    }

    [Fact]
    public void ParseDeviceCommissioning_MissingNodeIdAttribute_DefaultsToZero()
    {
        var xdc = MinimalXdc.Replace(@"nodeID=""3"" ", string.Empty);

        var result = _reader.ReadString(xdc);

        result.DeviceCommissioning.NodeId.Should().Be(0);
        result.DeviceCommissioning.NodeName.Should().Be("MyDevice");
    }

    [Fact]
    public void ParseDeviceCommissioning_ProfileWithoutProfileBody_IsSkipped()
    {
        const string xdc = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>Ignored</ProfileClassID></ProfileHeader>
    <!-- intentionally no ProfileBody -->
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>Device</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdc"" fileVersion=""1"">
      <DeviceIdentity><vendorName>V</vendorName><vendorID>0x1</vendorID><productName>P</productName><productID>0x1</productID></DeviceIdentity>
      <DeviceManager/><DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>CommunicationNetwork</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdc"" fileVersion=""1"">
      <ApplicationLayers><CANopenObjectList mandatoryObjects=""0"" optionalObjects=""0"" manufacturerObjects=""0""/></ApplicationLayers>
      <TransportLayers><PhysicalLayer><baudRate defaultValue=""250 Kbps""><supportedBaudRate value=""250 Kbps""/></baudRate></PhysicalLayer></TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""0"" nrOfTxPDO=""0"" bootUpSlave=""false"" layerSettingServiceSlave=""false"" groupMessaging=""false"" dynamicChannels=""0""/>
        <CANopenMasterFeatures bootUpMaster=""false""/>
        <deviceCommissioning nodeID=""9"" networkNumber=""1"" CANopenManager=""false""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        var result = _reader.ReadString(xdc);

        result.DeviceCommissioning.NodeId.Should().Be(9);
    }

    [Fact]
    public void ParseDeviceCommissioning_NetworkManagementMissing_ReturnsDefaultCommissioning()
    {
        const string xdc = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>Device</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdc"" fileVersion=""1"">
      <DeviceIdentity><vendorName>V</vendorName><vendorID>0x1</vendorID><productName>P</productName><productID>0x1</productID></DeviceIdentity>
      <DeviceManager/><DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>CommunicationNetwork</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdc"" fileVersion=""1"">
      <ApplicationLayers><CANopenObjectList mandatoryObjects=""0"" optionalObjects=""0"" manufacturerObjects=""0""/></ApplicationLayers>
      <TransportLayers><PhysicalLayer><baudRate defaultValue=""250 Kbps""><supportedBaudRate value=""250 Kbps""/></baudRate></PhysicalLayer></TransportLayers>
      <!-- intentionally no NetworkManagement -->
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        var result = _reader.ReadString(xdc);

        result.DeviceCommissioning.NodeId.Should().Be(0);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("128")]
    [InlineData("255")]
    public void ParseDeviceCommissioning_OutOfRangeNodeId_ThrowsEdsParseException(string nodeId)
    {
        var xdc = MinimalXdc.Replace(
            @"<deviceCommissioning nodeID=""3""",
            $@"<deviceCommissioning nodeID=""{nodeId}""");

        var act = () => _reader.ReadString(xdc);

        act.Should().Throw<EdsParseException>()
            .WithMessage("*nodeID*");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0x100")]
    [InlineData("not-a-number")]
    public void ParseDeviceCommissioning_UnparseableNodeId_ThrowsEdsParseException(string nodeId)
    {
        var xdc = MinimalXdc.Replace(
            @"<deviceCommissioning nodeID=""3""",
            $@"<deviceCommissioning nodeID=""{nodeId}""");

        var act = () => _reader.ReadString(xdc);

        act.Should().Throw<EdsParseException>()
            .WithMessage("*nodeID*");
    }

    #endregion
}
