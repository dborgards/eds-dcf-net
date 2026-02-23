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
}
