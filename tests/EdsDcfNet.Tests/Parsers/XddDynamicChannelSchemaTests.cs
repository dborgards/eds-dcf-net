namespace EdsDcfNet.Tests.Parsers;

using System.Xml.Linq;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Tests.Infrastructure;
using EdsDcfNet.Tests.Utilities;
using EdsDcfNet.Writers;

/// <summary>
/// CiA 311 <c>dynamicChannel</c> attributes from
/// <c>ProfileBody_Network_CANopen.xsd</c>: required <c>dataType</c>,
/// <c>accessType</c>, <c>startIndex</c>, <c>endIndex</c>, <c>maxNumber</c>,
/// and <c>addressOffset</c>; optional <c>bitAlignment</c>.
/// </summary>
public class XddDynamicChannelSchemaTests
{
    private static readonly CanOpenFileOptions Strict = new() { StrictParsing = true };

    private const string MinimalXdd = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>Device</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <DeviceIdentity><vendorName>V</vendorName></DeviceIdentity>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>CommunicationNetwork</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007"" accessType=""ro""/>
        </CANopenObjectList>
        <!--channels-->
      </ApplicationLayers>
      <TransportLayers>
        <PhysicalLayer>
          <baudRate defaultValue=""250 Kbps""><supportedBaudRate value=""250 Kbps""/></baudRate>
        </PhysicalLayer>
      </TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""0"" nrOfTxPDO=""0""
                                bootUpSlave=""false"" layerSettingServiceSlave=""false""
                                groupMessaging=""false"" dynamicChannels=""0""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

    private const string EdsWithDynamicChannel = @"
[FileInfo]
FileName=dyn.eds
FileVersion=1
FileRevision=1
[DeviceInfo]
VendorName=Vendor
VendorNumber=1
ProductName=Product
ProductNumber=1
RevisionNumber=1
OrderCode=OC
BaudRate_250=1
[MandatoryObjects]
SupportedObjects=1
1=0x1000
[1000]
ParameterName=Device Type
ObjectType=0x7
DataType=0x0007
AccessType=ro
[DynamicChannels]
NrOfSeg=2
Type1=0x0007
Dir1=rww
Range1=0xA080-0xA0BF
PPOffset1=0
Type2=0x0005
Dir2=ro
Range2=0x2000
PPOffset2=16
";

    [Fact]
    public void FormatDataType_ObjectFormStaysFourDigits_DynamicChannelUsesTwo()
    {
        // Arrange — CANopenObject dataType is four digits. The dynamicChannel
        // annotation specifies two. The compiled hexBinary type accepts both.

        // Act
        var objectForm = XddFormatHelper.FormatDataType(0x0007);
        var channelForm = XddFormatHelper.FormatDynamicChannelDataType(0x0007);
        var wideChannel = XddFormatHelper.FormatDynamicChannelDataType(0x0107);

        // Assert
        objectForm.Should().Be("0007");
        channelForm.Should().Be("07");
        wideChannel.Should().Be("0107");
    }

    [Theory]
    [InlineData(0u, "0000")]
    [InlineData(0x10u, "0010")]
    [InlineData(0x10000u, "010000")]
    [InlineData(0xFFFFFFFFu, "FFFFFFFF")]
    public void FormatHexBinary_EvenUppercaseAtLeastFourDigits(uint value, string expected)
    {
        XddFormatHelper.FormatHexBinary(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("Xdd")]
    [InlineData("Xdc")]
    public void ReadString_AddressOffsetDigitCount_RoundTrips(string format)
    {
        // Arrange — 0010 is two bytes; 00000010 is four bytes. Both are 16.

        // Act
        var shortForm = RoundTrip(format, Channel(
            "dataType=\"07\" accessType=\"readOnly\" startIndex=\"1600\" endIndex=\"17FF\" maxNumber=\"3\" addressOffset=\"0010\" bitAlignment=\"8\""));
        var longForm = RoundTrip(format, Channel(
            "dataType=\"07\" accessType=\"writeOnly\" startIndex=\"2000\" endIndex=\"200F\" maxNumber=\"1\" addressOffset=\"00000010\" bitAlignment=\"0\""));

        // Assert
        shortForm.Segment.PPOffset.Should().Be(0x10);
        shortForm.Segment.MaxNumber.Should().Be(3u);
        shortForm.Segment.BitAlignment.Should().Be(8);
        shortForm.Segment.Type.Should().Be(0x0007);
        shortForm.Segment.Dir.Should().Be(AccessType.ReadOnly);
        shortForm.Segment.Range.Should().Be("1600-17FF");
        Attribute(shortForm.Written, "addressOffset").Should().Be("0010");
        Attribute(shortForm.Written, "maxNumber").Should().Be("3");
        Attribute(shortForm.Written, "bitAlignment").Should().Be("8");
        Attribute(shortForm.Written, "dataType").Should().Be("07");
        shortForm.Written.Should().NotContain("pDOmappingIndex");

        longForm.Segment.PPOffset.Should().Be(0x10);
        longForm.Segment.BitAlignment.Should().Be(0);
        Attribute(longForm.Written, "addressOffset").Should().Be("00000010");
        Attribute(longForm.Written, "bitAlignment").Should().Be("0");
        Attribute(longForm.Written, "accessType").Should().Be("writeOnly");
    }

    [Fact]
    public void ReadString_MaxNumberSmallerThanIndexSpan_IsNotReplaced()
    {
        // Arrange — 1600-17FF spans 512 indexes. maxNumber stays 3.

        // Act
        var result = RoundTrip("Xdd", Channel(
            "dataType=\"07\" accessType=\"readWriteOutput\" startIndex=\"1600\" endIndex=\"17FF\" maxNumber=\"3\" addressOffset=\"0000\""));

        // Assert
        result.Segment.MaxNumber.Should().Be(3u);
        result.Segment.Dir.Should().Be(AccessType.ReadWriteOutput);
        Attribute(result.Written, "maxNumber").Should().Be("3");
        Attribute(result.Written, "maxNumber").Should().NotBe("512");
    }

    [Fact]
    public void ReadString_AddressOffsetOneByte_KeepsShorterSpelling()
    {
        // Act
        var result = RoundTrip("Xdd", Channel(
            "dataType=\"07\" accessType=\"readOnly\" startIndex=\"1000\" endIndex=\"1000\" maxNumber=\"1\" addressOffset=\"10\""));

        // Assert
        result.Segment.PPOffset.Should().Be(0x10);
        Attribute(result.Written, "addressOffset").Should().Be("10");
    }

    [Fact]
    public void ReadString_AddressOffsetLowerCase_KeepsCaseUntilOffsetChanges()
    {
        // Act
        var result = RoundTrip("Xdc", Channel(
            "dataType=\"07\" accessType=\"readOnly\" startIndex=\"1000\" endIndex=\"1000\" maxNumber=\"1\" addressOffset=\"00aa\""));

        // Assert
        result.Segment.PPOffset.Should().Be(0xAA);
        Attribute(result.Written, "addressOffset").Should().Be("00aa");
    }

    [Theory]
    [InlineData("Xdd")]
    [InlineData("Xdc")]
    public void WriteToString_PPOffsetChanged_FormatsEvenUppercaseHex(string format)
    {
        // Arrange
        var read = Read(format, Channel(
            "dataType=\"07\" accessType=\"readOnly\" startIndex=\"1000\" endIndex=\"1000\" maxNumber=\"1\" addressOffset=\"0010\""));
        read.ModelSegment.PPOffset = 0x20;

        // Act
        var written = Write(format, read.Model, validated: false);
        read.ModelSegment.PPOffset = 0x10000;
        var wide = Write(format, read.Model, validated: false);

        // Assert
        Attribute(written, "addressOffset").Should().Be("0020");
        Attribute(wide, "addressOffset").Should().Be("010000");
        written.Should().NotContain("addressOffset=\"0010\"");
    }

    [Fact]
    public void WriteToString_ValidatedRoundTrip_KeepsChannelAttributes()
    {
        // Arrange
        var read = CanOpenFile.Xdd.ReadString(WithChannels(Channel(
            "dataType=\"07\" accessType=\"readOnly\" startIndex=\"1600\" endIndex=\"1603\" maxNumber=\"2\" addressOffset=\"0010\" bitAlignment=\"16\"")));
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.DynamicChannels = read.DynamicChannels;

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);
        var again = CanOpenFile.Xdd.ReadString(written);

        // Assert
        var segment = again.DynamicChannels!.Segments.Should().ContainSingle().Subject;
        segment.MaxNumber.Should().Be(2u);
        segment.BitAlignment.Should().Be(16);
        segment.PPOffset.Should().Be(0x10);
        Attribute(written, "addressOffset").Should().Be("0010");
        Attribute(written, "maxNumber").Should().Be("2");
        Attribute(written, "bitAlignment").Should().Be("16");
    }

    [Fact]
    public void ReadString_AddressOffsetBeyondUInt32_PreservesTextInBothModes()
    {
        // Arrange — 0100000000 is 2^32, even-length hexBinary, and does not fit.
        var xml = WithChannels(Channel(
            "dataType=\"07\" accessType=\"readOnly\" startIndex=\"1000\" endIndex=\"1000\" maxNumber=\"1\" addressOffset=\"0100000000\""));

        // Act
        var lenient = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml);
        var strict = CanOpenFile.Xdc.ReadStringWithDiagnostics(xml, Strict);

        // Assert
        lenient.Model.DynamicChannels!.Segments[0].PPOffset.Should().Be(0u);
        lenient.Model.DynamicChannels.Segments[0].AddressOffsetLexical.Should().Be("0100000000");
        lenient.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.XddAddressOffsetExceedsUInt32 &&
            diagnostic.RawValue == "0100000000");
        strict.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.XddAddressOffsetExceedsUInt32);
        strict.Model.DynamicChannels!.Segments[0].AddressOffsetLexical.Should().Be("0100000000");
        var preserved = CanOpenFile.Xdd.WriteToString(lenient.Model);
        Attribute(preserved, "addressOffset").Should().Be("0100000000");
        lenient.Model.DynamicChannels.Segments[0].PPOffset = 1;
        var replaced = CanOpenFile.Xdd.WriteToString(lenient.Model);
        Attribute(replaced, "addressOffset").Should().Be("0001");
    }

    [Theory]
    [InlineData("001")]
    [InlineData("0x10")]
    [InlineData("zz")]
    [InlineData("+10")]
    public void ReadString_AddressOffsetNotHexBinary_LenientIgnoresStrictThrows(string raw)
    {
        // Act
        var xml = WithChannels(Channel(
            "dataType=\"07\" accessType=\"readOnly\" startIndex=\"1000\" endIndex=\"1000\" maxNumber=\"1\" addressOffset=\"" + raw + "\""));
        var lenient = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml);
        var strict = () => CanOpenFile.Xdd.ReadString(xml, Strict);

        // Assert
        lenient.Model.DynamicChannels!.Segments[0].PPOffset.Should().Be(0u);
        lenient.Model.DynamicChannels.Segments[0].AddressOffsetLexical.Should().BeNull();
        lenient.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.XddInvalidNumericAttribute &&
            diagnostic.RawValue == raw);
        strict.Should().Throw<EdsParseException>()
            .Which.Code.Should().Be(ParseDiagnosticCodes.XddInvalidNumericAttribute);

        var rewritten = CanOpenFile.Xdd.WriteToString(lenient.Model);
        Attribute(rewritten, "addressOffset").Should().Be("0000");
    }

    [Theory]
    [InlineData("00 10")]
    [InlineData("0 0 1 0")]
    [InlineData("00&#9;10")]
    [InlineData("00&#10;10")]
    [InlineData("00&#13;10")]
    public void ReadString_AddressOffsetInteriorWhitespace_LenientIgnoresStrictThrows(string raw)
    {
        // Arrange — xsd:hexBinary has whiteSpace=collapse; interior whitespace is not
        // part of the lexical space and must not be silently stripped.
        var xml = WithChannels(Channel(
            "dataType=\"07\" accessType=\"readOnly\" startIndex=\"1000\" endIndex=\"1000\" maxNumber=\"1\" addressOffset=\"" + raw + "\""));
        var expectedRaw = XDocument.Parse(xml).Descendants()
            .First(e => e.Name.LocalName == "dynamicChannel").Attribute("addressOffset")!.Value;

        // Act
        var lenient = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml);
        var strict = () => CanOpenFile.Xdd.ReadString(xml, Strict);

        // Assert
        lenient.Model.DynamicChannels!.Segments[0].PPOffset.Should().Be(0u);
        lenient.Model.DynamicChannels.Segments[0].AddressOffsetLexical.Should().BeNull();
        lenient.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.XddInvalidNumericAttribute &&
            diagnostic.RawValue == expectedRaw);
        strict.Should().Throw<EdsParseException>()
            .Which.Code.Should().Be(ParseDiagnosticCodes.XddInvalidNumericAttribute);

        var rewritten = CanOpenFile.Xdd.WriteToString(lenient.Model);
        Attribute(rewritten, "addressOffset").Should().Be("0000");
    }

    [Theory]
    [InlineData(" 0010")]
    [InlineData("0010 ")]
    [InlineData("  0010  ")]
    [InlineData("&#9;0010&#10;&#13;")]
    public void ReadString_AddressOffsetSurroundingWhitespace_AcceptedInBothModes(string raw)
    {
        // Arrange — leading/trailing whitespace is removed by whiteSpace=collapse.
        var xml = WithChannels(Channel(
            "dataType=\"07\" accessType=\"readOnly\" startIndex=\"1000\" endIndex=\"1000\" maxNumber=\"1\" addressOffset=\"" + raw + "\""));

        // Act
        var lenient = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml);
        var strict = CanOpenFile.Xdd.ReadString(xml, Strict);
        var rewritten = CanOpenFile.Xdd.WriteToString(strict);

        // Assert
        lenient.Diagnostics.Should().BeEmpty();
        lenient.Model.DynamicChannels!.Segments[0].PPOffset.Should().Be(0x10u);
        strict.DynamicChannels!.Segments[0].PPOffset.Should().Be(0x10u);
        Attribute(rewritten, "addressOffset").Trim().Should().Be("0010");
        CanOpenFile.Xdd.ReadString(rewritten, Strict).DynamicChannels!.Segments[0].PPOffset.Should().Be(0x10u);
    }

    [Fact]
    public void ReadString_LegacyMappingIndex_LenientKeepsOffsetStrictThrows()
    {
        // Arrange — older library builds wrote PPOffset as pDOmappingIndex.
        var xml = WithChannels(Channel(
            "dataType=\"0007\" accessType=\"readOnly\" startIndex=\"1600\" endIndex=\"17FF\" pDOmappingIndex=\"2\""));

        // Act
        var lenient = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml);
        var strict = () => CanOpenFile.Xdc.ReadString(xml, Strict);
        var written = CanOpenFile.Xdd.WriteToString(lenient.Model);

        // Assert
        lenient.Model.DynamicChannels!.Segments[0].PPOffset.Should().Be(2u);
        lenient.Model.DynamicChannels.Segments[0].Dir.Should().Be(AccessType.ReadOnly);
        lenient.Model.DynamicChannels.Segments[0].Type.Should().Be(0x0007);
        lenient.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.XddLegacyAttribute &&
            diagnostic.Message.Contains("legacy attribute") &&
            diagnostic.RawValue == "2");
        strict.Should().Throw<EdsParseException>()
            .Which.Code.Should().Be(ParseDiagnosticCodes.XddLegacyAttribute);
        Attribute(written, "addressOffset").Should().Be("0002");
        written.Should().NotContain("pDOmappingIndex");
        Attribute(written, "dataType").Should().Be("07");
        Attribute(written, "accessType").Should().Be("readOnly");
    }

    [Fact]
    public void ReadString_AddressOffsetPresent_IgnoresLegacyMappingIndex()
    {
        // Act
        var read = CanOpenFile.Xdd.ReadStringWithDiagnostics(WithChannels(Channel(
            "dataType=\"07\" accessType=\"readOnly\" startIndex=\"1000\" endIndex=\"1000\" maxNumber=\"1\" addressOffset=\"0011\" pDOmappingIndex=\"2\"")));

        // Assert
        read.Model.DynamicChannels!.Segments[0].PPOffset.Should().Be(0x11);
        read.Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("readOnly", AccessType.ReadOnly)]
    [InlineData("writeOnly", AccessType.WriteOnly)]
    [InlineData("readWriteOutput", AccessType.ReadWriteOutput)]
    [InlineData("READONLY", AccessType.ReadOnly)]
    public void ReadString_SchemaAccessType_RoundTripsCanonicalToken(string token, AccessType expected)
    {
        // Act
        var read = CanOpenFile.Xdd.ReadStringWithDiagnostics(WithChannels(Channel(
            "dataType=\"07\" accessType=\"" + token + "\" startIndex=\"1000\" endIndex=\"1000\" maxNumber=\"1\" addressOffset=\"0000\"")), Strict);
        var written = CanOpenFile.Xdd.WriteToString(read.Model);

        // Assert
        read.Diagnostics.Should().BeEmpty();
        read.Model.DynamicChannels!.Segments[0].Dir.Should().Be(expected);
        Attribute(written, "accessType").Should().Be(
            token.Equals("writeOnly", StringComparison.OrdinalIgnoreCase) ? "writeOnly" :
            token.Equals("readWriteOutput", StringComparison.OrdinalIgnoreCase) ? "readWriteOutput" :
            "readOnly");
    }

    [Theory]
    [InlineData("ro", AccessType.ReadOnly)]
    [InlineData("wo", AccessType.WriteOnly)]
    [InlineData("rw", AccessType.ReadWrite)]
    [InlineData("rwr", AccessType.ReadWriteInput)]
    [InlineData("rww", AccessType.ReadWriteOutput)]
    [InlineData("const", AccessType.Constant)]
    public void ReadString_ShortAccessType_LenientAcceptsStrictThrows(string token, AccessType expected)
    {
        // Act
        var xml = WithChannels(Channel(
            "dataType=\"07\" accessType=\"" + token + "\" startIndex=\"1000\" endIndex=\"1000\" maxNumber=\"1\" addressOffset=\"0000\""));
        var lenient = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml);
        var strict = () => CanOpenFile.Xdd.ReadString(xml, Strict);

        // Assert
        lenient.Diagnostics.Should().BeEmpty();
        lenient.Model.DynamicChannels!.Segments[0].Dir.Should().Be(expected);
        strict.Should().Throw<EdsParseException>()
            .Which.Code.Should().Be(ParseDiagnosticCodes.XddUnknownAccessType);
    }

    [Fact]
    public void ReadString_UnknownAccessType_LenientMapsToReadOnly()
    {
        // Act
        var read = CanOpenFile.Xdd.ReadStringWithDiagnostics(WithChannels(Channel(
            "dataType=\"07\" accessType=\"banana\" startIndex=\"1000\" endIndex=\"1000\" maxNumber=\"1\" addressOffset=\"0000\"")));

        // Assert
        read.Model.DynamicChannels!.Segments[0].Dir.Should().Be(AccessType.ReadOnly);
        read.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.XddUnknownAccessType &&
            diagnostic.Path == "dynamicChannel/accessType");
    }

    [Fact]
    public void WriteToString_EdsDynamicChannels_AreSchemaValid()
    {
        // Arrange — EDS stores Type, Dir, Range, and PPOffset. maxNumber is derived.
        var eds = CanOpenFile.Eds.ReadString(EdsWithDynamicChannel);

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);
        var fragment = XDocument.Parse(written)
            .Descendants()
            .Single(element => element.Name.LocalName == "dynamicChannels");
        var problems = Cia311Schema.Validate(SpliceIntoBasicDevice(fragment.ToString()));
        var channels = fragment.Elements().Where(element => element.Name.LocalName == "dynamicChannel").ToList();

        // Assert
        eds.DynamicChannels!.Segments[0].MaxNumber.Should().BeNull();
        eds.DynamicChannels.Segments[0].BitAlignment.Should().BeNull();
        problems.Should().BeEmpty();
        channels.Should().HaveCount(2);
        channels[0].Attribute("dataType")!.Value.Should().Be("07");
        channels[0].Attribute("accessType")!.Value.Should().Be("readWriteOutput");
        channels[0].Attribute("startIndex")!.Value.Should().Be("A080");
        channels[0].Attribute("endIndex")!.Value.Should().Be("A0BF");
        channels[0].Attribute("maxNumber")!.Value.Should().Be("64");
        channels[0].Attribute("addressOffset")!.Value.Should().Be("0000");
        channels[0].Attribute("bitAlignment").Should().BeNull();
        channels[1].Attribute("startIndex")!.Value.Should().Be("2000");
        channels[1].Attribute("endIndex")!.Value.Should().Be("2000");
        channels[1].Attribute("maxNumber")!.Value.Should().Be("1");
        channels[1].Attribute("addressOffset")!.Value.Should().Be("0010");
        channels[1].Attribute("accessType")!.Value.Should().Be("readOnly");
        fragment.ToString().Should().NotContain("pDOmappingIndex");
        eds.DynamicChannels.Segments[0].MaxNumber.Should().BeNull();
    }

    [Fact]
    public void Cia311Schema_ShortAccessTypeOnDynamicChannel_IsRejected()
    {
        // Arrange — the compiled enumeration does not include the EDS short form.
        var xml = SpliceIntoBasicDevice(
            "<dynamicChannels><dynamicChannel dataType=\"07\" accessType=\"ro\" startIndex=\"1600\" endIndex=\"17FF\" maxNumber=\"1\" addressOffset=\"0000\"/></dynamicChannels>");

        // Act
        var problems = Cia311Schema.Validate(xml);

        // Assert
        problems.Should().Contain(problem => problem.Contains("accessType"));
    }

    [Fact]
    public void Cia311Schema_PreservedAddressOffsetForms_AreAccepted()
    {
        // Arrange
        var channels = new DynamicChannels();
        channels.Segments.Add(new DynamicChannelSegment
        {
            Type = 0x0007,
            Dir = AccessType.ReadOnly,
            Range = "1600-17FF",
            PPOffset = 0x10,
            MaxNumber = 3,
            BitAlignment = 255,
            AddressOffsetLexical = "0010",
            AddressOffsetLexicalBaseline = 0x10
        });
        channels.Segments.Add(new DynamicChannelSegment
        {
            Type = 0x0107,
            Dir = AccessType.WriteOnly,
            Range = "2000-2000",
            PPOffset = 0x10,
            MaxNumber = uint.MaxValue,
            AddressOffsetLexical = "00000010",
            AddressOffsetLexicalBaseline = 0x10
        });
        channels.Segments.Add(new DynamicChannelSegment
        {
            Type = 0x0005,
            Dir = AccessType.ReadOnly,
            Range = "0000-FFFF"
        });
        var fragment = XddProfileBuilder.BuildDynamicChannels(channels).ToString();

        // Act
        var problems = Cia311Schema.Validate(SpliceIntoBasicDevice(fragment));

        // Assert
        problems.Should().BeEmpty();
        fragment.Should().Contain("addressOffset=\"0010\"");
        fragment.Should().Contain("addressOffset=\"00000010\"");
        fragment.Should().Contain("maxNumber=\"3\"");
        fragment.Should().Contain("maxNumber=\"4294967295\"");
        fragment.Should().Contain("bitAlignment=\"255\"");
        fragment.Should().Contain("dataType=\"0107\"");
        fragment.Should().Contain("maxNumber=\"65536\"");
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("255", 255)]
    [InlineData("+8", 8)]
    public void ReadString_BitAlignmentBoundary_RoundTripsDecimal(string raw, int expected)
    {
        // Act
        var result = RoundTrip("Xdd", Channel(
            "dataType=\"07\" accessType=\"readOnly\" startIndex=\"1000\" endIndex=\"1000\" maxNumber=\"1\" addressOffset=\"0000\" bitAlignment=\"" + raw + "\""));

        // Assert
        result.Segment.BitAlignment.Should().Be((byte)expected);
        Attribute(result.Written, "bitAlignment").Should().Be(expected.ToString());
    }

    [Fact]
    public void ReadString_BitAlignmentAboveUnsignedByte_LenientLeavesUnsetStrictThrows()
    {
        // Act
        var xml = WithChannels(Channel(
            "dataType=\"07\" accessType=\"readOnly\" startIndex=\"1000\" endIndex=\"1000\" maxNumber=\"1\" addressOffset=\"0000\" bitAlignment=\"256\""));
        var lenient = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml);
        var strict = () => CanOpenFile.Xdd.ReadString(xml, Strict);
        var written = CanOpenFile.Xdd.WriteToString(lenient.Model);

        // Assert
        lenient.Model.DynamicChannels!.Segments[0].BitAlignment.Should().BeNull();
        lenient.Diagnostics.Should().Contain(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.XddInvalidNumericAttribute &&
            diagnostic.RawValue == "256");
        strict.Should().Throw<EdsParseException>()
            .Which.Code.Should().Be(ParseDiagnosticCodes.XddInvalidNumericAttribute);
        written.Should().NotContain("bitAlignment");
    }

    [Fact]
    public void ReadString_MaxNumberAtMaxValue_RoundTrips()
    {
        // Act
        var result = RoundTrip("Xdd", Channel(
            "dataType=\"07\" accessType=\"readOnly\" startIndex=\"1000\" endIndex=\"1000\" maxNumber=\"4294967295\" addressOffset=\"0000\""));

        // Assert
        result.Segment.MaxNumber.Should().Be(uint.MaxValue);
        Attribute(result.Written, "maxNumber").Should().Be("4294967295");
    }

    [Fact]
    public void ReadString_MaxNumberAboveUnsignedInt_LenientDerivesOnWrite()
    {
        // Act
        var xml = WithChannels(Channel(
            "dataType=\"07\" accessType=\"readOnly\" startIndex=\"1600\" endIndex=\"1601\" maxNumber=\"4294967296\" addressOffset=\"0000\""));
        var lenient = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml);
        var strict = () => CanOpenFile.Xdc.ReadString(xml, Strict);
        var written = CanOpenFile.Xdd.WriteToString(lenient.Model);

        // Assert
        lenient.Model.DynamicChannels!.Segments[0].MaxNumber.Should().BeNull();
        lenient.Diagnostics.Should().Contain(diagnostic => diagnostic.RawValue == "4294967296");
        strict.Should().Throw<EdsParseException>();
        Attribute(written, "maxNumber").Should().Be("2");
    }

    [Fact]
    public void WriteToString_MissingMaxNumber_DerivesWithoutMutatingTheModel()
    {
        // Arrange
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.DynamicChannels = new DynamicChannels();
        eds.DynamicChannels.Segments.Add(new DynamicChannelSegment
        {
            Type = 0x0007,
            Dir = AccessType.ReadOnly,
            Range = "1600-17FF",
            PPOffset = 0
        });
        eds.DynamicChannels.Segments.Add(new DynamicChannelSegment
        {
            Type = 0x0001,
            Dir = AccessType.Constant,
            Range = "17FF-1600"
        });

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);
        var channels = XDocument.Parse(written)
            .Descendants()
            .Where(element => element.Name.LocalName == "dynamicChannel")
            .ToList();

        // Assert
        eds.DynamicChannels.Segments[0].MaxNumber.Should().BeNull();
        channels[0].Attribute("maxNumber")!.Value.Should().Be("512");
        channels[1].Attribute("accessType")!.Value.Should().Be("readOnly");
        channels[1].Attribute("maxNumber")!.Value.Should().Be("0");
        channels[1].Attribute("startIndex")!.Value.Should().Be("17FF");
        channels[1].Attribute("endIndex")!.Value.Should().Be("1600");
    }

    [Fact]
    public void ConvertToDcf_KeepsMaxNumberBitAlignmentAndAddressOffsetSpelling()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(WithChannels(Channel(
            "dataType=\"07\" accessType=\"readWriteOutput\" startIndex=\"1600\" endIndex=\"17FF\" maxNumber=\"3\" addressOffset=\"00000010\" bitAlignment=\"32\"")));

        // Act
        var dcf = CanOpenFile.Eds.ConvertToDcf(
            eds,
            nodeId: 5,
            timestamp: new DateTime(2026, 1, 2),
            baudrate: 250);
        var written = CanOpenFile.Xdc.WriteToString(dcf);
        var again = CanOpenFile.Xdc.ReadString(written);

        // Assert
        var cloned = dcf.DynamicChannels!.Segments.Should().ContainSingle().Subject;
        cloned.MaxNumber.Should().Be(3u);
        cloned.BitAlignment.Should().Be(32);
        cloned.AddressOffsetLexical.Should().Be("00000010");
        cloned.PPOffset.Should().Be(0x10);
        Attribute(written, "addressOffset").Should().Be("00000010");
        Attribute(written, "maxNumber").Should().Be("3");
        Attribute(written, "bitAlignment").Should().Be("32");
        again.DynamicChannels!.Segments[0].AddressOffsetLexical.Should().Be("00000010");
        again.DynamicChannels.Segments[0].MaxNumber.Should().Be(3u);
    }

    [Theory]
    [InlineData(AccessType.ReadOnly, "readOnly")]
    [InlineData(AccessType.WriteOnly, "writeOnly")]
    [InlineData(AccessType.ReadWriteOutput, "readWriteOutput")]
    [InlineData(AccessType.ReadWrite, "readWriteOutput")]
    [InlineData(AccessType.ReadWriteInput, "readWriteOutput")]
    [InlineData(AccessType.Constant, "readOnly")]
    public void WriteToString_AccessType_UsesSchemaToken(AccessType accessType, string expected)
    {
        // Arrange
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.DynamicChannels = new DynamicChannels();
        eds.DynamicChannels.Segments.Add(new DynamicChannelSegment
        {
            Type = 1,
            Dir = accessType,
            Range = "2000-2000"
        });

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        Attribute(written, "accessType").Should().Be(expected);
    }

    private static string Channel(string attributes) =>
        "<dynamicChannel " + attributes + "/>";

    private static string WithChannels(string channels) =>
        MinimalXdd.Replace("<!--channels-->", "<dynamicChannels>" + channels + "</dynamicChannels>");

    private static RoundTripResult RoundTrip(string format, string channel, bool validated = false)
    {
        var read = Read(format, channel);
        var written = Write(format, read.Model, validated);
        return new RoundTripResult(read.ModelSegment, written);
    }

    private static ReadResult Read(string format, string channel, CanOpenFileOptions? options = null)
    {
        var xml = WithChannels(channel);
        if (format == "Xdd")
        {
            var read = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml, options);
            return new ReadResult(read.Model, read.Model.DynamicChannels!.Segments[0]);
        }

        var xdc = CanOpenFile.Xdc.ReadStringWithDiagnostics(xml, options);
        return new ReadResult(xdc.Model, xdc.Model.DynamicChannels!.Segments[0]);
    }

    private static string Write(string format, object model, bool validated)
    {
        var options = validated ? CanOpenWriteOptions.Validated : null;
        if (format == "Xdd")
            return CanOpenFile.Xdd.WriteToString((ElectronicDataSheet)model, options);

        return CanOpenFile.Xdc.WriteToString((DeviceConfigurationFile)model, options);
    }

    private static string Attribute(string xml, string name) =>
        XDocument.Parse(xml)
            .Descendants()
            .First(element => element.Name.LocalName == "dynamicChannel")
            .Attribute(name)!
            .Value;

    private static string SpliceIntoBasicDevice(string dynamicChannels)
    {
        var xml = File.ReadAllText(Path.Combine("Fixtures", "Corpus", "canopen-node", "basicDevice.xdd"));
        const string marker = "</ApplicationLayers>";
        var index = xml.IndexOf(marker, StringComparison.Ordinal);
        index.Should().BeGreaterThan(0);
        return xml.Insert(index, dynamicChannels);
    }

    private sealed class RoundTripResult
    {
        internal RoundTripResult(DynamicChannelSegment segment, string written)
        {
            Segment = segment;
            Written = written;
        }

        internal DynamicChannelSegment Segment { get; }

        internal string Written { get; }
    }

    private sealed class ReadResult
    {
        internal ReadResult(object model, DynamicChannelSegment segment)
        {
            Model = model;
            ModelSegment = segment;
        }

        internal object Model { get; }

        internal DynamicChannelSegment ModelSegment { get; }
    }
}
