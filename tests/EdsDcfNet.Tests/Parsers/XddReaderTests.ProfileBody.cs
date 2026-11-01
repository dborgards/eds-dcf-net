namespace EdsDcfNet.Tests.Parsers;

using System.Text;
using EdsDcfNet;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;

public partial class XddReaderTests
{
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

    #region Unknown CommunicationNetwork ProfileBody AdditionalSections contract (#415)

    [Fact]
    public void ReadString_UnknownCommNetProfileBodyChild_StoresAttributesOnly_DropsNestedContent()
    {
        // Arrange — nested element content must not appear in INI-shaped AdditionalSections
        const string xdd = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>Device</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <DeviceIdentity><vendorName>V</vendorName><vendorID>0x1</vendorID><productName>P</productName><productID>0x1</productID></DeviceIdentity>
      <DeviceManager/><DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>CommunicationNetwork</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
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
      </NetworkManagement>
      <VendorExtension customKey=""attrOnly"">
        <Nested>should-not-be-preserved</Nested>
      </VendorExtension>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        // Act
        var result = _reader.ReadString(xdd);

        // Assert
        result.AdditionalSections.Should().ContainKey("VendorExtension");
        result.AdditionalSections["VendorExtension"].Should().ContainKey("customKey");
        result.AdditionalSections["VendorExtension"]["customKey"].Should().Be("attrOnly");
        result.AdditionalSections["VendorExtension"].Keys.Should().NotContain(k =>
            k.Contains("Nested", StringComparison.OrdinalIgnoreCase) ||
            k.Contains("should-not-be-preserved", StringComparison.OrdinalIgnoreCase));
        result.AdditionalSections["VendorExtension"].Values.Should().NotContain("should-not-be-preserved");
    }

    [Fact]
    public void ReadString_UnknownDeviceProfileBodyChild_IsNotCapturedInAdditionalSections()
    {
        // Device ProfileBody unknowns are mapped selectively; they are not attribute-captured
        const string xdd = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>Device</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <DeviceIdentity><vendorName>V</vendorName><vendorID>0x1</vendorID><productName>P</productName><productID>0x1</productID></DeviceIdentity>
      <DeviceManager/><DeviceFunction/>
      <VendorDeviceExtension customKey=""deviceOnly""/>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>CommunicationNetwork</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
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
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        var result = _reader.ReadString(xdd);

        result.AdditionalSections.Should().NotContainKey("VendorDeviceExtension");
    }

    #endregion

    #region Duplicate ProfileBody StrictParsing (#486)

    [Fact]
    public void ReadString_DuplicateDeviceProfileBody_StrictParsing_ThrowsEdsParseException()
    {
        var xdd = MinimalXdd.Replace(
            "</ISO15745ProfileContainer>",
            @"  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""dup.xdd"" fileVersion=""2"">
      <DeviceIdentity>
        <vendorName>Other</vendorName>
        <vendorID>0x2</vendorID>
        <productName>Other</productName>
        <productID>0x2</productID>
      </DeviceIdentity>
      <DeviceManager/><DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*more than one*ProfileBody_Device_CANopen*");
    }

    [Fact]
    public void ReadString_SiblingDeviceProfileBodiesInSameProfile_StrictParsing_ThrowsEdsParseException()
    {
        // Two ProfileBody siblings under one ISO15745Profile must also be rejected.
        const string xdd = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""a.xdd"" fileVersion=""1"">
      <DeviceIdentity>
        <vendorName>First</vendorName><vendorID>0x1</vendorID>
        <productName>First</productName><productID>0x1</productID>
      </DeviceIdentity>
      <DeviceManager/><DeviceFunction/>
    </ProfileBody>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""b.xdd"" fileVersion=""2"">
      <DeviceIdentity>
        <vendorName>Second</vendorName><vendorID>0x2</vendorID>
        <productName>Second</productName><productID>0x2</productID>
      </DeviceIdentity>
      <DeviceManager/><DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
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
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*more than one*ProfileBody_Device_CANopen*");
    }

    [Fact]
    public void ReadString_DuplicateCommNetProfileBody_StrictParsing_ThrowsEdsParseException()
    {
        var xdd = MinimalXdd.Replace(
            "</ISO15745ProfileContainer>",
            @"  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""dup.xdd"" fileVersion=""2"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""0"" optionalObjects=""0"" manufacturerObjects=""0""/>
      </ApplicationLayers>
      <TransportLayers><PhysicalLayer><baudRate defaultValue=""125 Kbps""/></PhysicalLayer></TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""0"" nrOfTxPDO=""0""
                                bootUpSlave=""false"" layerSettingServiceSlave=""false""
                                groupMessaging=""false"" dynamicChannels=""0""/>
        <CANopenMasterFeatures bootUpMaster=""false""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*more than one*ProfileBody_CommunicationNetwork_CANopen*");
    }

    [Fact]
    public void ReadString_DuplicateDeviceProfileBody_Lenient_LastWins()
    {
        var xdd = MinimalXdd.Replace(
            "</ISO15745ProfileContainer>",
            @"  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""dup.xdd"" fileVersion=""2"">
      <DeviceIdentity>
        <vendorName>Other Vendor</vendorName>
        <vendorID>0x2</vendorID>
        <productName>Other Product</productName>
        <productID>0x2</productID>
      </DeviceIdentity>
      <DeviceManager/><DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>");

        var result = _reader.ReadString(xdd);

        result.DeviceInfo.VendorName.Should().Be("Other Vendor");
        result.DeviceInfo.ProductName.Should().Be("Other Product");
    }

    #endregion
}
