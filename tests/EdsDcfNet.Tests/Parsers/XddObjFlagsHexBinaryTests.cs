namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Tests.Utilities;
using EdsDcfNet.Validation;

/// <summary>
/// CiA 311 v1.1.0 Annex A.1.4, <c>ProfileBody_Network_CANopen.xsd</c> attribute
/// <c>objFlags</c> (<c>xsd:hexBinary</c>). The annotation specifies four hex digits,
/// defines bits 0, 1 and 2 (bit 2: change of value takes effect after reset), and
/// reserves bits 3..31.
/// </summary>
public class XddObjFlagsHexBinaryTests
{
    private static readonly CanOpenFileOptions Strict = new() { StrictParsing = true };

    [Theory]
    [InlineData("Xdd")]
    [InlineData("Xdc")]
    public void ReadString_ObjFlags000A_ParsesAsHexTen(string format)
    {
        // Arrange — "000A" is valid hexBinary and is not a decimal integer.
        // 0xA sets bit 1 and reserved bit 3.

        // Act
        var flags = ReadObjFlags(format, "000A", options: null, out var diagnostics);

        // Assert
        flags.Should().Be(0x0Au);
        diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.XddObjFlagsReservedBits &&
            diagnostic.Path == "CANopenObject[@index='1000']/objFlags" &&
            diagnostic.RawValue == "000A");
        diagnostics.Single().Message.Should().Contain("decimal");
    }

    [Fact]
    public void ReadString_ObjFlags0004_StrictParsing_ParsesWithoutDiagnostic()
    {
        // Arrange — bit 2 is defined. Four digits are the canonical lexical form.

        // Act
        var flags = ReadObjFlags("Xdd", "0004", Strict, out var diagnostics);

        // Assert
        flags.Should().Be(0x4u);
        diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("1", 0x1u)]
    [InlineData("2", 0x2u)]
    [InlineData("3", 0x3u)]
    [InlineData("7", 0x7u)]
    public void ReadString_ObjFlagsSingleDefinedDigit_ParsesWithOddLengthDiagnostic(string raw, uint expected)
    {
        // Arrange — values 0..7 are identical in hex and decimal, so older decimal
        // output such as objFlags="3" stays readable. The length is still not valid hexBinary.

        // Act
        var flags = ReadObjFlags("Xdd", raw, options: null, out var diagnostics);

        // Assert
        flags.Should().Be(expected);
        diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.XddObjFlagsOddHexLength &&
            diagnostic.RawValue == raw);
        diagnostics.Should().NotContain(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.XddObjFlagsReservedBits);
    }

    [Fact]
    public void ReadString_ObjFlagsOddLength_StrictParsing_Throws()
    {
        // Act
        var act = () => CanOpenFile.Xdd.ReadString(ProfileWithObjFlags("3"), Strict);

        // Assert
        act.Should().Throw<EdsParseException>()
            .Which.Code.Should().Be(ParseDiagnosticCodes.XddObjFlagsOddHexLength);
    }

    [Fact]
    public void ReadString_ObjFlags10_ParsesAsHexSixteenAndReportsReservedBits()
    {
        // Arrange — decimal 10 and hex 0x10 differ. The diagnostic records that
        // a pre-fix decimal emission would change meaning.

        // Act
        var flags = ReadObjFlags("Xdc", "10", Strict, out var diagnostics);

        // Assert
        flags.Should().Be(0x10u);
        diagnostics.Should().ContainSingle();
        diagnostics[0].Code.Should().Be(ParseDiagnosticCodes.XddObjFlagsReservedBits);
        diagnostics[0].Message.Should().Contain("decimal");
    }

    [Fact]
    public void ReadString_ObjFlagsOddAndReserved_LenientReportsBothStrictThrowsOddLength()
    {
        // Arrange — "A" is 0xA (bits 1 and 3) and has an odd length.

        // Act
        var flags = ReadObjFlags("Xdd", "A", options: null, out var diagnostics);
        var strict = () => CanOpenFile.Xdd.ReadString(ProfileWithObjFlags("A"), Strict);

        // Assert
        flags.Should().Be(0x0Au);
        diagnostics.Select(diagnostic => diagnostic.Code).Should().Equal(
            ParseDiagnosticCodes.XddObjFlagsOddHexLength,
            ParseDiagnosticCodes.XddObjFlagsReservedBits);
        strict.Should().Throw<EdsParseException>()
            .Which.Code.Should().Be(ParseDiagnosticCodes.XddObjFlagsOddHexLength);
    }

    [Theory]
    [InlineData("+1")]
    [InlineData("0x0004")]
    [InlineData("not-uint")]
    public void ReadString_ObjFlagsNotHexBinary_LenientLeavesUnsetStrictThrows(string raw)
    {
        // Act
        var flags = ReadObjFlags("Xdd", raw, options: null, out var diagnostics);
        var strict = () => CanOpenFile.Xdc.ReadString(ProfileWithObjFlags(raw), Strict);

        // Assert
        flags.Should().Be(0u);
        diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.XddInvalidNumericAttribute &&
            diagnostic.RawValue == raw);
        strict.Should().Throw<EdsParseException>()
            .Which.Code.Should().Be(ParseDiagnosticCodes.XddInvalidNumericAttribute);
    }

    [Fact]
    public void ReadString_ObjFlagsSurroundingWhitespace_ParsesCanonicalValue()
    {
        // Act
        var flags = ReadObjFlags("Xdd", " 0004 ", Strict, out var diagnostics);

        // Assert
        flags.Should().Be(0x4u);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_ObjFlagsLowerCase_ParsesHexValue()
    {
        // Act
        var flags = ReadObjFlags("Xdd", "000a", options: null, out var diagnostics);

        // Assert
        flags.Should().Be(0x0Au);
        diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.XddObjFlagsReservedBits);
    }

    [Fact]
    public void ReadString_ObjFlagsLeadingZerosWithinUint_WritesCanonicalDigits()
    {
        // Arrange — extra leading zeros are valid hexBinary and still fit. objFlags has a
        // specified digit count, so the writer emits the canonical form.

        // Act
        var read = CanOpenFile.Xdd.ReadStringWithDiagnostics(ProfileWithObjFlags("00000004"));
        var written = CanOpenFile.Xdd.WriteToString(read.Model);

        // Assert
        read.Diagnostics.Should().BeEmpty();
        read.Model.ObjectDictionary.Objects[0x1000].ObjFlags.Should().Be(0x4u);
        written.Should().Contain("objFlags=\"0004\"");
        written.Should().NotContain("00000004");
    }

    [Fact]
    public void ReadString_ObjFlagsBeyondUInt32_PreservesLexicalTextInBothModes()
    {
        // Arrange — "0100000000" is even-length hexBinary for 2^32, which does not fit.

        // Act
        var lenient = CanOpenFile.Xdd.ReadStringWithDiagnostics(ProfileWithObjFlags("0100000000"));
        var strict = CanOpenFile.Xdc.ReadStringWithDiagnostics(ProfileWithObjFlags("0100000000"), Strict);

        // Assert
        lenient.Model.ObjectDictionary.Objects[0x1000].ObjFlags.Should().Be(0u);
        lenient.Model.ObjectDictionary.Objects[0x1000].ObjFlagsLexical.Should().Be("0100000000");
        lenient.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.XddObjFlagsExceedsUInt32);
        strict.Model.ObjectDictionary.Objects[0x1000].ObjFlags.Should().Be(0u);
        strict.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.XddObjFlagsExceedsUInt32);

        var preserved = CanOpenFile.Xdd.WriteToString(lenient.Model);
        preserved.Should().Contain("objFlags=\"0100000000\"");

        lenient.Model.ObjectDictionary.Objects[0x1000].ObjFlags = 0x4;
        var replaced = CanOpenFile.Xdd.WriteToString(lenient.Model);
        replaced.Should().Contain("objFlags=\"0004\"");
        replaced.Should().NotContain("0100000000");
    }

    [Fact]
    public void ReadString_ObjFlagsBeyondUInt32_SurvivesConvertToDcf()
    {
        // Act
        var eds = CanOpenFile.Xdd.ReadString(ProfileWithObjFlags("0100000000"));
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 5, timestamp: new DateTime(2026, 1, 2), baudrate: 250);
        var written = CanOpenFile.Xdc.WriteToString(dcf);

        // Assert
        dcf.ObjectDictionary.Objects[0x1000].ObjFlagsLexical.Should().Be("0100000000");
        written.Should().Contain("objFlags=\"0100000000\"");
    }

    [Fact]
    public void ReadString_ObjFlagsOddOverflow_LenientIgnoresStrictThrows()
    {
        // Arrange — nine digits is not valid hexBinary, and the magnitude does not fit.

        // Act
        var flags = ReadObjFlags("Xdd", "100000000", options: null, out var diagnostics);
        var strict = () => CanOpenFile.Xdd.ReadString(ProfileWithObjFlags("100000000"), Strict);

        // Assert
        flags.Should().Be(0u);
        diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.XddInvalidNumericAttribute);
        strict.Should().Throw<EdsParseException>()
            .Which.Code.Should().Be(ParseDiagnosticCodes.XddInvalidNumericAttribute);
    }

    [Theory]
    [InlineData(0x1u, "0001")]
    [InlineData(0x2u, "0002")]
    [InlineData(0x4u, "0004")]
    [InlineData(0x7u, "0007")]
    public void WriteToString_DefinedObjFlags_WritesFourDigitHex(uint flags, string expected)
    {
        // Arrange
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.ObjectDictionary.Objects[0x1000].ObjFlags = flags;

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        written.Should().Contain("objFlags=\"" + expected + "\"");
    }

    [Fact]
    public void WriteToString_ObjFlagsZero_OmitsAttribute()
    {
        // Arrange
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.ObjectDictionary.Objects[0x1000].ObjFlags = 0;

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        written.Should().NotContain("objFlags");
    }

    [Fact]
    public void WriteToString_ObjFlags0004_ValidatedRoundTrip_PreservesBit2()
    {
        // Arrange
        var xdd = ValidCanOpenModelBuilder.CreateValidEds();
        xdd.ObjectDictionary.Objects[0x1000].ObjFlags = 0x4;
        var xdc = ValidCanOpenModelBuilder.CreateValidDcf();
        xdc.ObjectDictionary.Objects[0x1000].ObjFlags = 0x4;

        // Act
        var writtenXdd = CanOpenFile.Xdd.WriteToString(xdd, CanOpenWriteOptions.Validated);
        var writtenXdc = CanOpenFile.Xdc.WriteToString(xdc, CanOpenWriteOptions.Validated);
        var againXdd = CanOpenFile.Xdd.ReadStringWithDiagnostics(writtenXdd);
        var againXdc = CanOpenFile.Xdc.ReadStringWithDiagnostics(writtenXdc);

        // Assert
        writtenXdd.Should().Contain("objFlags=\"0004\"");
        writtenXdc.Should().Contain("objFlags=\"0004\"");
        againXdd.Model.ObjectDictionary.Objects[0x1000].ObjFlags.Should().Be(0x4u);
        againXdc.Model.ObjectDictionary.Objects[0x1000].ObjFlags.Should().Be(0x4u);
        againXdd.Diagnostics.Should().BeEmpty();
        againXdc.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void WriteToString_ObjFlagsEditedAfterRead_FollowsProperty()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(ProfileWithObjFlags("0004"));
        eds.ObjectDictionary.Objects[0x1000].ObjFlags = 0x1;

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        written.Should().Contain("objFlags=\"0001\"");
        written.Should().NotContain("objFlags=\"0004\"");
    }

    [Theory]
    [InlineData("Xdd", 0x8u, "0008")]
    [InlineData("Xdc", 0x8u, "0008")]
    [InlineData("Xdd", 0x80000000u, "80000000")]
    [InlineData("Xdc", uint.MaxValue, "FFFFFFFF")]
    public void WriteToString_ObjFlagsReservedBits_UnvalidatedEmitsHexValidatedRejects(
        string format,
        uint flags,
        string expected)
    {
        // Act
        var unvalidated = Write(format, flags, validated: false);
        var validated = () => Write(format, flags, validated: true);

        // Assert
        unvalidated.Should().Contain("objFlags=\"" + expected + "\"");
        var issues = validated.Should().Throw<ModelValidationException>().Which.Issues;
        issues.Should().ContainSingle(issue =>
            issue.Code == ValidationIssueCodes.XddObjFlagsReservedBits &&
            issue.Path == "ObjectDictionary.Objects[0x1000].ObjFlags");
    }

    [Fact]
    public void WriteToString_ObjFlagsBeyondUInt32_ValidatedWriteRejectsPreservedText()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(ProfileWithObjFlags("0100000000"));

        // Act
        var act = () => CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        act.Should().Throw<ModelValidationException>().Which.Issues.Should().ContainSingle(issue =>
            issue.Code == ValidationIssueCodes.XddObjFlagsExceedsUInt32 &&
            issue.Path == "ObjectDictionary.Objects[0x1000].ObjFlags");
    }

    [Theory]
    [InlineData("Eds")]
    [InlineData("Dcf")]
    public void WriteToString_ObjFlagsReservedBit_ValidatedIniWrite_IsNotRejected(string format)
    {
        // The EDS/DCF reserved-bit limit (bits 2..31) belongs to WP-15 and is not applied here.
        // Bit 3 is reserved for XDD/XDC only in this package.

        // Act
        string written = format == "Eds"
            ? CanOpenFile.Eds.WriteToString(EdsWithFlags(0x8), CanOpenWriteOptions.Validated)
            : CanOpenFile.Dcf.WriteToString(DcfWithFlags(0x8), CanOpenWriteOptions.Validated);

        // Assert
        written.Should().Contain("ObjFlags=0x8");
    }

    private static uint ReadObjFlags(
        string format,
        string raw,
        CanOpenFileOptions? options,
        out IReadOnlyList<ParseDiagnostic> diagnostics)
    {
        var xml = ProfileWithObjFlags(raw);
        if (format == "Xdd")
        {
            var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml, options);
            diagnostics = result.Diagnostics;
            return result.Model.ObjectDictionary.Objects[0x1000].ObjFlags;
        }

        var xdc = CanOpenFile.Xdc.ReadStringWithDiagnostics(xml, options);
        diagnostics = xdc.Diagnostics;
        return xdc.Model.ObjectDictionary.Objects[0x1000].ObjFlags;
    }

    private static string Write(string format, uint flags, bool validated)
    {
        var options = validated ? CanOpenWriteOptions.Validated : null;
        if (format == "Xdd")
            return CanOpenFile.Xdd.WriteToString(EdsWithFlags(flags), options);

        return CanOpenFile.Xdc.WriteToString(DcfWithFlags(flags), options);
    }

    private static ElectronicDataSheet EdsWithFlags(uint flags)
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.ObjectDictionary.Objects[0x1000].ObjFlags = flags;
        return eds;
    }

    private static DeviceConfigurationFile DcfWithFlags(uint flags)
    {
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        dcf.ObjectDictionary.Objects[0x1000].ObjFlags = flags;
        return dcf;
    }

    private static string ProfileWithObjFlags(string objFlags) =>
        @"<?xml version=""1.0"" encoding=""utf-8""?>
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
                         accessType=""ro"" PDOmapping=""no"" objFlags=""" + objFlags + @"""/>
        </CANopenObjectList>
      </ApplicationLayers>
      <TransportLayers><PhysicalLayer><baudRate defaultValue=""250 Kbps""/></PhysicalLayer></TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""0"" nrOfTxPDO=""0""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";
}
