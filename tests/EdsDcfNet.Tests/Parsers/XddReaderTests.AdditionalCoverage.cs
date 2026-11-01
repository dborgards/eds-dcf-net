namespace EdsDcfNet.Tests.Parsers;

using System.Text;
using EdsDcfNet;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;

public partial class XddReaderTests
{
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
        // ProfileBody_Device_CANopen without DeviceIdentity → DeviceInfo defaults.
        // Use a verbatim needle so CRLF checkouts (Windows CI) still match MinimalXdd.
        var xdd = MinimalXdd.Replace(
            @"<DeviceIdentity>
        <vendorName>Test Vendor</vendorName>
        <vendorID>0x00000100</vendorID>
        <productName>Test Product</productName>
        <productID>0x00001001</productID>
      </DeviceIdentity>",
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
    public void ParseBaudRates_UnknownBaudRate_StrictParsing_ThrowsEdsParseException()
    {
        var xdd = MinimalXdd.Replace(
            "<supportedBaudRate value=\"500 Kbps\"/>",
            "<supportedBaudRate value=\"999 Kbps\"/>");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*Unknown baud-rate string '999 Kbps'*");
    }

    [Fact]
    public void ParseBaudRates_UnknownDefaultValue_StrictParsing_ThrowsEdsParseException()
    {
        // defaultValue is not mapped into DeviceInfo, but StrictParsing still validates vocabulary
        var xdd = MinimalXdd.Replace(
            @"defaultValue=""250 Kbps""",
            @"defaultValue=""999 Kbps""");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*Unknown baud-rate string '999 Kbps'*");
    }

    [Fact]
    public void ParseBaudRates_UnknownDefaultValue_Lenient_Succeeds()
    {
        var xdd = MinimalXdd.Replace(
            @"defaultValue=""250 Kbps""",
            @"defaultValue=""999 Kbps""");

        var result = _reader.ReadString(xdd);

        result.DeviceInfo.SupportedBaudRates.BaudRate250.Should().BeTrue();
        result.DeviceInfo.SupportedBaudRates.BaudRate500.Should().BeTrue();
    }

    [Fact]
    public void ParseBaudRates_MissingDefaultValueAttribute_StillParsesSupportedRates()
    {
        // Covers Attribute("defaultValue")?.Value null path and Length == 0 skip
        var xdd = MinimalXdd.Replace(
            @"<baudRate defaultValue=""250 Kbps"">",
            "<baudRate>");

        var result = _reader.ReadString(xdd);

        result.DeviceInfo.SupportedBaudRates.BaudRate250.Should().BeTrue();
        result.DeviceInfo.SupportedBaudRates.BaudRate500.Should().BeTrue();
    }

    [Fact]
    public void ParseBaudRates_EmptyDefaultValue_StrictParsing_DoesNotThrow()
    {
        var xdd = MinimalXdd.Replace(
            @"defaultValue=""250 Kbps""",
            @"defaultValue=""""");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().NotThrow();
    }

    [Fact]
    public void ParseBaudRateString_WhitespaceOnly_IsTreatedAsEmpty()
    {
        var xdd = MinimalXdd.Replace(
            "<supportedBaudRate value=\"500 Kbps\"/>",
            "<supportedBaudRate value=\"   \"/>");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().NotThrow();
    }

    [Fact]
    public void ParseDeviceCommissioning_UnknownActualBaudRate_StrictParsing_ReportsAndPreservesWithoutThrowing()
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
        <deviceCommissioning nodeID=""1"" networkNumber=""0"" CANopenManager=""false"" actualBaudRate=""777 Kbps""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        // actualBaudRate is a free xsd:string: valid input the model cannot hold is reported, not rejected.
        var result = CanOpenFile.Xdc.ReadStringWithDiagnostics(xdc, new CanOpenFileOptions { StrictParsing = true });

        result.Model.DeviceCommissioning.Baudrate.Should().Be(0);
        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == ParseDiagnosticCodes.XddUnknownBaudRate && d.RawValue == "777 Kbps");
        CanOpenFile.Xdc.WriteToString(result.Model).Should().Contain("actualBaudRate=\"777 Kbps\"");
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
    public void FileInfo_FileVersionWithCommaMinorPart_UsesMajorComponent()
    {
        var xdd = MinimalXdd.Replace(
            @"fileVersion=""1""",
            @"fileVersion=""4,2""");

        var result = _reader.ReadString(xdd);

        result.FileInfo.FileVersion.Should().Be(4);
    }

    [Theory]
    [InlineData("07.3", (byte)7)]
    [InlineData("012.5", (byte)12)]
    [InlineData("010.0", (byte)10)]
    public void FileInfo_FileVersionWithLeadingZeroMajor_UsesMajorComponent(string fileVersion, byte expected)
    {
        var xdd = MinimalXdd.Replace(
            @"fileVersion=""1""",
            $@"fileVersion=""{fileVersion}""");

        var result = _reader.ReadString(xdd);

        result.FileInfo.FileVersion.Should().Be(expected);
    }

    [Theory]
    [InlineData("7.3")]
    [InlineData("1,0")]
    public void FileInfo_FileVersionMajorMinor_StrictParsing_UsesMajorAndKeepsText(string fileVersion)
    {
        var xdd = MinimalXdd.Replace(
            @"fileVersion=""1""",
            $@"fileVersion=""{fileVersion}""");

        var result = CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        result.FileInfo.FileVersion.Should().Be(byte.Parse(fileVersion.Substring(0, 1)));
        result.FileInfo.FileVersionText.Should().Be(fileVersion);
    }

    [Theory]
    [InlineData("010", (byte)10)]
    [InlineData("08", (byte)8)]
    [InlineData("012", (byte)12)]
    public void FileInfo_FileVersionZeroPadded_StrictParsing_StaysDecimal(string fileVersion, byte expected)
    {
        // Strict mode must reject major/minor without switching plain XDD decimals onto
        // ParseByte/CiA octal ("010" → 10, not 8; "08" remains a valid decimal integer).
        var xdd = MinimalXdd.Replace(
            @"fileVersion=""1""",
            $@"fileVersion=""{fileVersion}""");

        var result = CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        result.FileInfo.FileVersion.Should().Be(expected);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("1.x")]
    [InlineData("abc")]
    public void FileInfo_NonNumericFileVersion_KeepsTextAndDefaultVersionWithoutThrowing(string fileVersion)
    {
        // fileVersion is an xsd:string (CiA 311): valid text the Unsigned8 property cannot hold is kept.
        var xdd = MinimalXdd.Replace(
            @"fileVersion=""1""",
            $@"fileVersion=""{fileVersion}""");

        var result = _reader.ReadString(xdd);

        result.FileInfo.FileVersion.Should().Be(1);
        result.FileInfo.FileVersionText.Should().Be(fileVersion);
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

    [Theory]
    [InlineData("granularity", "invalid")]
    [InlineData("nrOfRxPDO", "oops")]
    [InlineData("nrOfTxPDO", "nope")]
    [InlineData("dynamicChannels", "bad")]
    public void ParseNetworkManagement_InvalidNumeric_StrictParsing_ThrowsEdsParseException(
        string attribute, string badValue)
    {
        var xdd = MinimalXdd.Replace(
            $@"{attribute}=""{attribute switch
            {
                "granularity" => "8",
                "nrOfRxPDO" => "2",
                "nrOfTxPDO" => "2",
                _ => "0"
            }}""",
            $@"{attribute}=""{badValue}""");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage($"*{attribute}*{badValue}*");
    }

    [Fact]
    public void ParseCanOpenObject_InvalidObjFlags_StrictParsing_ThrowsEdsParseException()
    {
        var xdd = MinimalXdd.Replace(
            @"PDOmapping=""no""",
            @"PDOmapping=""no"" objFlags=""not-uint""");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*objFlags*not-uint*");
    }

    [Fact]
    public void ParseCanOpenObject_ObjFlagsWithSurroundingWhitespace_StrictParsing_Parses()
    {
        var xdd = MinimalXdd.Replace(
            @"PDOmapping=""no""",
            @"PDOmapping=""no"" objFlags="" 0001 """);

        var result = CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        result.ObjectDictionary.Objects[0x1000].ObjFlags.Should().Be(1u);
    }

    [Fact]
    public void ParseCanOpenObject_ObjFlagsWithLeadingPlus_StrictParsing_ThrowsEdsParseException()
    {
        // objFlags is xsd:hexBinary (CiA 311 Annex A.1.4). A leading sign is not hexadecimal.
        var xdd = MinimalXdd.Replace(
            @"PDOmapping=""no""",
            @"PDOmapping=""no"" objFlags=""+1""");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*objFlags*+1*");
    }

    [Fact]
    public void ParseCanOpenObject_SubNumberNegativeZero_StrictParsing_ParsesAsZero()
    {
        var xdd = MinimalXdd.Replace(
            @"PDOmapping=""no""",
            @"PDOmapping=""no"" subNumber=""-0""");

        var result = CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        result.ObjectDictionary.Objects[0x1000].SubNumber.Should().Be(0);
    }

    [Fact]
    public void ParseCanOpenObject_SubNumberNegativeOutOfRange_StrictParsing_ThrowsEdsParseException()
    {
        var xdd = MinimalXdd.Replace(
            @"PDOmapping=""no""",
            @"PDOmapping=""no"" subNumber=""-1""");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*subNumber*-1*");
    }

    [Fact]
    public void ParseNetworkManagement_NrOfRxPdoWithTrailingWhitespace_StrictParsing_Parses()
    {
        var xdd = MinimalXdd.Replace(@"nrOfRxPDO=""2""", @"nrOfRxPDO=""2 """);

        var result = CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        result.DeviceInfo.NrOfRxPdo.Should().Be(2);
    }

    [Fact]
    public void ParseNetworkManagement_NrOfRxPdoWithLeadingPlus_StrictParsing_Parses()
    {
        var xdd = MinimalXdd.Replace(@"nrOfRxPDO=""2""", @"nrOfRxPDO=""+2""");

        var result = CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        result.DeviceInfo.NrOfRxPdo.Should().Be(2);
    }

    [Fact]
    public void GetTrimmedAttributeValue_Absent_ReturnsNull()
    {
        var elem = new System.Xml.Linq.XElement("CANopenObject");
        XddParsingPrimitives.GetTrimmedAttributeValue(elem, "objFlags").Should().BeNull();
    }

    [Fact]
    public void GetTrimmedAttributeValue_WhitespaceOnly_ReturnsEmpty()
    {
        var elem = new System.Xml.Linq.XElement("CANopenObject",
            new System.Xml.Linq.XAttribute("objFlags", "   "));
        XddParsingPrimitives.GetTrimmedAttributeValue(elem, "objFlags").Should().BeEmpty();
    }

    [Fact]
    public void RejectFailedNumericAttribute_LenientUnknown_DoesNotThrow()
    {
        var act = () => XddParsingPrimitives.RejectFailedNumericAttribute("nope", parsed: false, "objFlags");
        act.Should().NotThrow();
    }

    [Fact]
    public void RejectFailedNumericAttribute_EmptyOrParsed_DoesNotThrowEvenWhenStrict()
    {
        using (StrictParsingScope.Enter(true))
        {
            XddParsingPrimitives.RejectFailedNumericAttribute(null, parsed: false, "objFlags");
            XddParsingPrimitives.RejectFailedNumericAttribute("", parsed: false, "objFlags");
            XddParsingPrimitives.RejectFailedNumericAttribute("1", parsed: true, "objFlags");
        }
    }

    [Fact]
    public void RejectFailedSignedNumericAttribute_StrictParsing_MentionsSignedInteger()
    {
        using (StrictParsingScope.Enter(true))
        {
            var act = () => XddParsingPrimitives.RejectFailedSignedNumericAttribute("oops", parsed: false, "lowerLimit");
            act.Should().Throw<EdsParseException>()
                .WithMessage("*lowerLimit*oops*signed integer*");
        }
    }

    [Fact]
    public void ParseCanOpenObject_InvalidSubNumber_StrictParsing_ThrowsEdsParseException()
    {
        var xdd = MinimalXdd.Replace(
            @"PDOmapping=""no""",
            @"PDOmapping=""no"" subNumber=""xx""");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*subNumber*xx*");
    }

    [Fact]
    public void ParseDynamicChannels_InvalidPPOffset_StrictParsing_ThrowsEdsParseException()
    {
        var xdd = MinimalXdd.Replace(
            "</ApplicationLayers>",
            @"  <dynamicChannels>
            <dynamicChannel dataType=""0007"" accessType=""readOnly"" startIndex=""1600"" endIndex=""17FF"" pDOmappingIndex=""not-a-number""/>
          </dynamicChannels>
        </ApplicationLayers>");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*pDOmappingIndex*not-a-number*");
    }

    [Fact]
    public void ParseDeviceCommissioning_InvalidNetworkNumber_StrictParsing_ThrowsEdsParseException()
    {
        var xdc = MinimalXdd.Replace(
            "</NetworkManagement>",
            @"  <deviceCommissioning nodeID=""5"" nodeName=""N1"" actualBaudRate=""250 Kbps""
                                 networkNumber=""bad"" networkName=""Net"" CANopenManager=""false""/>
      </NetworkManagement>");

        var act = () => CanOpenFile.Xdc.ReadString(xdc, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*networkNumber*bad*");
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

    [Theory]
    [InlineData("rwr", AccessType.ReadWriteInput)]
    [InlineData("RWW", AccessType.ReadWriteOutput)]
    [InlineData("const", AccessType.Constant)]
    [InlineData("wo", AccessType.WriteOnly)]
    [InlineData("rw", AccessType.ReadWrite)]
    public void ParseXddAccessType_KnownTokens_IncludingRwrRww_Parses(string accessType, AccessType expected)
    {
        var xdd = MinimalXdd.Replace(
            @"accessType=""ro""",
            $@"accessType=""{accessType}""");

        var result = _reader.ReadString(xdd);

        result.ObjectDictionary.Objects[0x1000].AccessType.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseXddAccessType_EmptyOrWhitespace_ReturnsReadOnly(string accessType)
    {
        XddParsingPrimitives.ParseXddAccessType(accessType).Should().Be(AccessType.ReadOnly);

        using (StrictParsingScope.Enter(true))
        {
            XddParsingPrimitives.ParseXddAccessType(accessType).Should().Be(AccessType.ReadOnly);
        }
    }

    [Fact]
    public void ParseXddAccessType_Unknown_StrictParsing_ThrowsEdsParseException()
    {
        var xdd = MinimalXdd.Replace(
            @"accessType=""ro""",
            @"accessType=""custom""");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*access type*custom*");
    }

    [Theory]
    [InlineData("maybe")]
    [InlineData("yes")]
    [InlineData("2")]
    public void ParseXmlBool_Unknown_StrictParsing_ThrowsEdsParseException(string token)
    {
        var xdd = MinimalXdd.Replace(
            @"bootUpSlave=""true""",
            $@"bootUpSlave=""{token}""");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*XML boolean*" + token + "*");
    }

    [Theory]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("true", true)]
    [InlineData("1", true)]
    [InlineData("FALSE", false)]
    [InlineData("TRUE", true)]
    public void ParseXmlBool_KnownTokens_StrictParsing_Parses(string token, bool expected)
    {
        var xdd = MinimalXdd.Replace(
            @"bootUpSlave=""true""",
            $@"bootUpSlave=""{token}""");

        var result = CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        result.DeviceInfo.SimpleBootUpSlave.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseXmlBool_EmptyOrWhitespace_ReturnsFalse(string token)
    {
        XddParsingPrimitives.ParseXmlBool(token).Should().BeFalse();

        using (StrictParsingScope.Enter(true))
        {
            XddParsingPrimitives.ParseXmlBool(token).Should().BeFalse();
        }
    }

    [Fact]
    public void ParseXmlBool_Unknown_Lenient_ReturnsFalse()
    {
        XddParsingPrimitives.ParseXmlBool("maybe").Should().BeFalse();
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
    public void ParseDummyUsage_InvalidHexSuffix_StrictParsing_ThrowsEdsParseException()
    {
        var xdd = File.ReadAllText("Fixtures/sample_device.xdd")
            .Replace(@"<dummy entry=""Dummy0005=1""/>", @"<dummy entry=""DummyGGGG=1""/>");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*dummyUsage*DummyGGGG=1*");
    }

    [Fact]
    public void ParseDummyUsage_EntryWithoutEquals_StrictParsing_ThrowsEdsParseException()
    {
        var xdd = MinimalXdd.Replace(
            "</ApplicationLayers>",
            @"  <dummyUsage>
            <dummy entry=""InvalidEntryNoEquals""/>
          </dummyUsage>
        </ApplicationLayers>");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*dummyUsage*InvalidEntryNoEquals*");
    }

    [Fact]
    public void ParseDummyUsage_ShortDummyKey_StrictParsing_ThrowsEdsParseException()
    {
        var xdd = MinimalXdd.Replace(
            "</ApplicationLayers>",
            @"  <dummyUsage>
            <dummy entry=""Dummy1=1""/>
          </dummyUsage>
        </ApplicationLayers>");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*dummyUsage*Dummy1=1*");
    }

    [Fact]
    public void ParseDummyUsage_OverlongDummyKey_StrictParsing_ThrowsEdsParseException()
    {
        var xdd = MinimalXdd.Replace(
            "</ApplicationLayers>",
            @"  <dummyUsage>
            <dummy entry=""Dummy00001=1""/>
          </dummyUsage>
        </ApplicationLayers>");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*dummyUsage*Dummy00001=1*");
    }

    [Fact]
    public void ParseDummyUsage_OverlongDummyKey_Lenient_ParsesHexSuffix()
    {
        // Pre-existing lenient behavior: Dummy00001=1 is accepted as index 1.
        var xdd = MinimalXdd.Replace(
            "</ApplicationLayers>",
            @"  <dummyUsage>
            <dummy entry=""Dummy00001=1""/>
          </dummyUsage>
        </ApplicationLayers>");

        var result = _reader.ReadString(xdd);

        result.ObjectDictionary.DummyUsage.Should().ContainKey((ushort)0x0001);
        result.ObjectDictionary.DummyUsage[0x0001].Should().BeTrue();
    }

    [Theory]
    [InlineData("2")]
    [InlineData("true")]
    public void ParseDummyUsage_InvalidValue_StrictParsing_ThrowsEdsParseException(string badValue)
    {
        var xdd = MinimalXdd.Replace(
            "</ApplicationLayers>",
            $@"  <dummyUsage>
            <dummy entry=""Dummy0002={badValue}""/>
          </dummyUsage>
        </ApplicationLayers>");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage($"*dummyUsage*Dummy0002={badValue}*");
    }

    [Fact]
    public void ParseDummyUsage_InvalidValue_Lenient_CoercesToFalse()
    {
        var xdd = MinimalXdd.Replace(
            "</ApplicationLayers>",
            @"  <dummyUsage>
            <dummy entry=""Dummy0002=2""/>
          </dummyUsage>
        </ApplicationLayers>");

        var result = _reader.ReadString(xdd);

        result.ObjectDictionary.DummyUsage.Should().ContainKey((ushort)0x0002);
        result.ObjectDictionary.DummyUsage[0x0002].Should().BeFalse();
    }

    [Fact]
    public void ParseHexDataType_WithPrefix_ParsedCorrectly()
    {
        var xdd = MinimalXdd.Replace(@"dataType=""0007""", @"dataType=""0x0007""");

        var result = _reader.ReadString(xdd);

        result.ObjectDictionary.Objects[0x1000].DataType.Should().Be(0x0007);
    }

    [Fact]
    public void ParseHexDataType_InvalidValue_ThrowsEdsParseException()
    {
        var xdd = MinimalXdd.Replace(@"dataType=""0007""", @"dataType=""nope""");

        var act = () => _reader.ReadString(xdd);

        act.Should().Throw<EdsParseException>()
            .WithMessage("*dataType*nope*");
    }

    [Fact]
    public void ParseHexId_InvalidVendorId_ThrowsEdsParseException()
    {
        var xdd = MinimalXdd.Replace(@"<vendorID>0x00000100</vendorID>", @"<vendorID>nothex</vendorID>");

        var act = () => _reader.ReadString(xdd);

        act.Should().Throw<EdsParseException>()
            .WithMessage("*identifier*nothex*");
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
    public void ParseCanOpenObject_MissingIndex_StrictParsing_ThrowsEdsParseException()
    {
        var xdd = MinimalXdd.Replace(
            @"<CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007""
                         accessType=""ro"" defaultValue=""0x00000000"" PDOmapping=""no""/>",
            @"<CANopenObject name=""Device Type"" objectType=""7"" dataType=""0007""
                         accessType=""ro"" defaultValue=""0x00000000"" PDOmapping=""no""/>");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*missing required attribute 'index'*");
    }

    [Fact]
    public void ParseCanOpenObject_MissingObjectType_StrictParsing_ThrowsEdsParseException()
    {
        var xdd = MinimalXdd.Replace(
            @"objectType=""7""",
            @"");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*missing required attribute 'objectType'*");
    }

    [Fact]
    public void ParseCanOpenObject_InvalidObjectType_StrictParsing_ThrowsEdsParseException()
    {
        var xdd = MinimalXdd.Replace(
            @"objectType=""7""",
            @"objectType=""not-a-byte""");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*Invalid objectType*not-a-byte*");
    }

    [Fact]
    public void ParseCanOpenObject_ObjectTypeWithSurroundingWhitespace_StrictParsing_Parses()
    {
        var xdd = MinimalXdd.Replace(
            @"objectType=""7""",
            @"objectType="" 9 """);

        var result = CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        result.ObjectDictionary.Objects[0x1000].ObjectType.Should().Be(9);
    }

    [Fact]
    public void ParseCanOpenObject_ObjectTypeWithLeadingPlus_StrictParsing_Parses()
    {
        var xdd = MinimalXdd.Replace(
            @"objectType=""7""",
            @"objectType=""+9""");

        var result = CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        result.ObjectDictionary.Objects[0x1000].ObjectType.Should().Be(9);
    }

    [Fact]
    public void ParseCanOpenObject_ObjectTypeNegativeZero_StrictParsing_ParsesAsZero()
    {
        var xdd = MinimalXdd.Replace(
            @"objectType=""7""",
            @"objectType=""-0""");

        var result = CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        result.ObjectDictionary.Objects[0x1000].ObjectType.Should().Be(0);
    }

    [Fact]
    public void ParseCanOpenObject_ObjectTypeNegativeOutOfRange_StrictParsing_ThrowsEdsParseException()
    {
        var xdd = MinimalXdd.Replace(
            @"objectType=""7""",
            @"objectType=""-1""");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*Invalid objectType*-1*");
    }

    [Fact]
    public void ParseCanOpenObject_WhitespaceOnlyObjectType_StrictParsing_ThrowsInvalidNotMissing()
    {
        var xdd = MinimalXdd.Replace(
            @"objectType=""7""",
            @"objectType=""   """);

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*Invalid objectType*");
    }

    [Fact]
    public void ParseCanOpenSubObject_InvalidObjectType_StrictParsing_ThrowsEdsParseException()
    {
        var xdd = MinimalXdd.Replace(
            @"<CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007""
                         accessType=""ro"" defaultValue=""0x00000000"" PDOmapping=""no""/>",
            @"<CANopenObjectList mandatoryObjects=""0"" optionalObjects=""1"" manufacturerObjects=""0"">
          <CANopenObject index=""1018"" name=""Identity"" objectType=""9"" subNumber=""1"">
            <CANopenSubObject subIndex=""00"" name=""Count"" objectType=""invalid"" dataType=""0005""
                              accessType=""ro"" defaultValue=""1"" PDOmapping=""no""/>
          </CANopenObject>");

        var act = () => CanOpenFile.Xdd.ReadString(xdd, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*Invalid objectType*invalid*CANopenSubObject*");
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
}
