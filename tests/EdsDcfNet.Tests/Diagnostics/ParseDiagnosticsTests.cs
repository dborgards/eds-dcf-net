namespace EdsDcfNet.Tests.Diagnostics;

using System.Text;
using System.Text.RegularExpressions;
using EdsDcfNet;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using FluentAssertions;
using Xunit;
using AccessType = EdsDcfNet.Models.AccessType;

/// <summary>
/// Guards the parse-diagnostics channel (#523): lenient reads through the
/// <c>Read*WithDiagnostics</c> facade methods report every coercion as a
/// <see cref="ParseDiagnostic"/> with a stable code, and the same condition in
/// strict mode throws an <see cref="EdsParseException"/> carrying the same code.
/// There is one test per instrumented strict-mode throw site.
/// </summary>
public class ParseDiagnosticsTests
{
    private static readonly CanOpenFileOptions Strict = new() { StrictParsing = true };

    // --- EDS: diagnostics on lenient repairs -------------------------------

    [Fact]
    public void CleanEds_ReportsNoDiagnostics()
    {
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(
            File.ReadAllText("Fixtures/sample_device.eds"));

        result.HasDiagnostics.Should().BeFalse();
        result.Diagnostics.Should().BeEmpty();
        result.Model.DeviceInfo.ProductName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void DuplicateIniKey_LenientReports_StrictThrowsSameCode()
    {
        var content = EdsWithDuplicateKey();

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.IniDuplicateKey);
        diagnostic.Severity.Should().Be(ParseSeverity.Warning);
        diagnostic.Path.Should().Be("FileInfo.FileName");
        diagnostic.Line.Should().NotBeNull("INI diagnostics carry the source line");
        diagnostic.CoercedTo.Should().Be("duplicate-b.eds");
        result.Model.FileInfo.FileName.Should().Be("duplicate-b.eds");

        AssertStrictThrowsWithCode(content, ParseDiagnosticCodes.IniDuplicateKey);
    }

    [Fact]
    public void UnknownBooleanToken_LenientReports_StrictThrowsSameCode()
    {
        var content = File.ReadAllText("Fixtures/sample_device.eds")
            .Replace("SimpleBootUpMaster=0", "SimpleBootUpMaster=maybe");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.UnknownBooleanToken);
        diagnostic.RawValue.Should().Be("maybe");
        diagnostic.CoercedTo.Should().Be("false");
        result.Model.DeviceInfo.SimpleBootUpMaster.Should().BeFalse();

        AssertStrictThrowsWithCode(content, ParseDiagnosticCodes.UnknownBooleanToken);
    }

    [Fact]
    public void UnknownAccessType_LenientReports_StrictThrowsSameCode()
    {
        var content = new Regex("(?m)^AccessType=ro(\r?)$")
            .Replace(File.ReadAllText("Fixtures/sample_device.eds"), "AccessType=nope$1", 1);

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.UnknownAccessTypeToken);
        diagnostic.RawValue.Should().Be("nope");
        diagnostic.CoercedTo.Should().Be("ro");
        result.Model.ObjectDictionary?.Objects[0x1000].AccessType.Should().Be(AccessType.ReadOnly);

        AssertStrictThrowsWithCode(content, ParseDiagnosticCodes.UnknownAccessTypeToken);
    }

    [Fact]
    public void InvalidDummyUsageKey_LenientReports_StrictThrowsSameCode()
    {
        var content = File.ReadAllText("Fixtures/sample_device.eds")
            .Replace("Dummy0002=1", "DummyXYZ=1");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.IniInvalidDummyUsageKey);
        diagnostic.Path.Should().Be("DummyUsage.DummyXYZ");
        diagnostic.RawValue.Should().Be("DummyXYZ");
        result.Model.ObjectDictionary?.DummyUsage.Should().NotContainKey(0x0002);

        AssertStrictThrowsWithCode(content, ParseDiagnosticCodes.IniInvalidDummyUsageKey);
    }

    [Fact]
    public void MajorMinorFileVersion_LenientReports_StrictThrowsSameCode()
    {
        var content = new Regex("(?m)^FileVersion=1(\r?)$")
            .Replace(File.ReadAllText("Fixtures/sample_device.eds"), "FileVersion=1.0$1", 1);

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.IniVersionMajorMinor);
        diagnostic.Path.Should().Be("FileInfo.FileVersion");
        diagnostic.RawValue.Should().Be("1.0");
        diagnostic.CoercedTo.Should().Be("1");
        result.Model.FileInfo.FileVersion.Should().Be((byte)1);

        AssertStrictThrowsWithCode(content, ParseDiagnosticCodes.IniVersionMajorMinor);
    }

    [Fact]
    public async Task ReadStreamWithDiagnosticsAsync_ReportsDiagnostics()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(EdsWithDuplicateKey()));

        var result = await CanOpenFile.Eds.ReadStreamWithDiagnosticsAsync(stream);

        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == ParseDiagnosticCodes.IniDuplicateKey);
        result.Model.FileInfo.FileName.Should().Be("duplicate-b.eds");
    }

    [Fact]
    public async Task ConcurrentReads_DiagnosticsStayIsolatedPerCall()
    {
        var duplicateKeyRead = Task.Run(() =>
            CanOpenFile.Eds.ReadStringWithDiagnostics(EdsWithDuplicateKey()));
        var booleanRead = Task.Run(() =>
            CanOpenFile.Eds.ReadStringWithDiagnostics(
                File.ReadAllText("Fixtures/sample_device.eds")
                    .Replace("SimpleBootUpMaster=0", "SimpleBootUpMaster=maybe")));

        var duplicateKeyResult = await duplicateKeyRead;
        var booleanResult = await booleanRead;

        duplicateKeyResult.Diagnostics.Should().OnlyContain(d =>
            d.Code == ParseDiagnosticCodes.IniDuplicateKey);
        booleanResult.Diagnostics.Should().OnlyContain(d =>
            d.Code == ParseDiagnosticCodes.UnknownBooleanToken);
    }

    // --- XDD: diagnostics on lenient repairs --------------------------------

    [Fact]
    public void CleanXdd_ReportsNoDiagnostics()
    {
        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(MinimalXdd);

        result.HasDiagnostics.Should().BeFalse();
    }

    [Fact]
    public void UnknownAccessType_Xdd_LenientReports_StrictThrowsSameCode()
    {
        var content = MinimalXdd.Replace("accessType=\"ro\"", "accessType=\"banana\"");

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.XddUnknownAccessType);
        diagnostic.RawValue.Should().Be("banana");
        diagnostic.CoercedTo.Should().Be("ro");

        AssertStrictThrowsWithCode(content, ParseDiagnosticCodes.XddUnknownAccessType, xdd: true);
    }

    [Fact]
    public void UnknownXmlBool_Xdd_LenientReports_StrictThrowsSameCode()
    {
        var content = MinimalXdd.Replace("bootUpSlave=\"true\"", "bootUpSlave=\"perhaps\"");

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.XddUnknownXmlBool);
        diagnostic.RawValue.Should().Be("perhaps");
        diagnostic.CoercedTo.Should().Be("false");

        AssertStrictThrowsWithCode(content, ParseDiagnosticCodes.XddUnknownXmlBool, xdd: true);
    }

    [Fact]
    public void UnknownBaudRate_Xdd_LenientReports_StrictThrowsSameCode()
    {
        var content = MinimalXdd.Replace("value=\"250 Kbps\"", "value=\"123 Kbps\"");

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.XddUnknownBaudRate);
        diagnostic.RawValue.Should().Be("123 Kbps");
        diagnostic.CoercedTo.Should().Be("0");

        AssertStrictThrowsWithCode(content, ParseDiagnosticCodes.XddUnknownBaudRate, xdd: true);
    }

    [Fact]
    public void InvalidNumericAttribute_Xdd_LenientReports_StrictThrowsSameCode()
    {
        var content = MinimalXdd.Replace("granularity=\"8\"", "granularity=\"eight\"");

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.XddInvalidNumericAttribute);
        diagnostic.Path.Should().Be("granularity");
        diagnostic.RawValue.Should().Be("eight");

        AssertStrictThrowsWithCode(content, ParseDiagnosticCodes.XddInvalidNumericAttribute, xdd: true);
    }

    [Fact]
    public void MissingIndex_Xdd_LenientReports_StrictThrowsSameCode()
    {
        var content = MinimalXdd.Replace(" index=\"1000\"", string.Empty);

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.XddMissingIndex);
        diagnostic.Path.Should().Be("CANopenObject");
        diagnostic.CoercedTo.Should().Be("0x0000");

        AssertStrictThrowsWithCode(content, ParseDiagnosticCodes.XddMissingIndex, xdd: true);
    }

    [Fact]
    public void MissingObjectType_Xdd_LenientReports_StrictThrowsSameCode()
    {
        var content = MinimalXdd.Replace(" objectType=\"7\"", string.Empty);

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.XddMissingObjectType);
        diagnostic.Path.Should().Be("CANopenObject");
        diagnostic.CoercedTo.Should().Be("0x7");

        AssertStrictThrowsWithCode(content, ParseDiagnosticCodes.XddMissingObjectType, xdd: true);
    }

    [Fact]
    public void InvalidObjectType_Xdd_LenientReports_StrictThrowsSameCode()
    {
        var content = MinimalXdd.Replace("objectType=\"7\"", "objectType=\"bogus\"");

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.XddInvalidObjectType);
        diagnostic.Path.Should().Be("CANopenObject");
        diagnostic.RawValue.Should().Be("bogus");
        diagnostic.CoercedTo.Should().Be("0x7");

        AssertStrictThrowsWithCode(content, ParseDiagnosticCodes.XddInvalidObjectType, xdd: true);
    }

    [Fact]
    public void InvalidDummyUsage_Xdd_LenientReports_StrictThrowsSameCode()
    {
        var content = MinimalXdd.Replace(
            "</CANopenObjectList>",
            "</CANopenObjectList>\n        <dummyUsage><dummy entry=\"bogus\"/></dummyUsage>");

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.XddInvalidDummyUsage);
        diagnostic.Path.Should().Be("dummyUsage/dummy");
        diagnostic.RawValue.Should().Be("bogus");

        AssertStrictThrowsWithCode(content, ParseDiagnosticCodes.XddInvalidDummyUsage, xdd: true);
    }

    [Fact]
    public void MajorMinorFileVersion_Xdd_LenientReports_StrictThrowsSameCode()
    {
        var content = MinimalXdd.Replace("fileVersion=\"1\"", "fileVersion=\"1.0\"");

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == ParseDiagnosticCodes.XddFileVersionMajorMinor &&
            d.CoercedTo == "1");

        AssertStrictThrowsWithCode(content, ParseDiagnosticCodes.XddFileVersionMajorMinor, xdd: true);
    }

    [Fact]
    public void DuplicateDeviceProfile_Xdd_LenientReports_StrictThrowsSameCode()
    {
        var content = MinimalXdd.Insert(
            MinimalXdd.IndexOf("</ISO15745Profile>", StringComparison.Ordinal)
                + "</ISO15745Profile>".Length,
            "\n  " + MinimalXdd.Substring(
                MinimalXdd.IndexOf("<ISO15745Profile>", StringComparison.Ordinal),
                MinimalXdd.IndexOf("</ISO15745Profile>", StringComparison.Ordinal)
                    + "</ISO15745Profile>".Length
                    - MinimalXdd.IndexOf("<ISO15745Profile>", StringComparison.Ordinal)));

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == ParseDiagnosticCodes.XddDuplicateDeviceProfile);

        AssertStrictThrowsWithCode(content, ParseDiagnosticCodes.XddDuplicateDeviceProfile, xdd: true);
    }

    [Fact]
    public void DuplicateCommNetProfile_Xdd_LenientReports_StrictThrowsSameCode()
    {
        var firstProfileStart = MinimalXdd.IndexOf("<ISO15745Profile>", StringComparison.Ordinal);
        var secondProfileStart = MinimalXdd.IndexOf(
            "<ISO15745Profile>", firstProfileStart + 1, StringComparison.Ordinal);
        var commNetProfile = MinimalXdd.Substring(secondProfileStart,
            MinimalXdd.IndexOf("</ISO15745Profile>", secondProfileStart, StringComparison.Ordinal)
                + "</ISO15745Profile>".Length - secondProfileStart);
        var content = MinimalXdd.Replace(
            "</ISO15745ProfileContainer>",
            commNetProfile + "\n</ISO15745ProfileContainer>");

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == ParseDiagnosticCodes.XddDuplicateCommNetProfile);

        AssertStrictThrowsWithCode(content, ParseDiagnosticCodes.XddDuplicateCommNetProfile, xdd: true);
    }

    [Fact]
    public void InvalidDummyUsageValue_Xdd_LenientReportsCoercion_StrictThrowsSameCode()
    {
        var content = MinimalXdd.Replace(
            "</CANopenObjectList>",
            "</CANopenObjectList>\n        <dummyUsage><dummy entry=\"Dummy0001=2\"/></dummyUsage>");

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.XddInvalidDummyUsage);
        diagnostic.Path.Should().Be("dummyUsage/dummy");
        diagnostic.RawValue.Should().Be("Dummy0001=2");
        diagnostic.CoercedTo.Should().Be("false");
        result.Model.ObjectDictionary?.DummyUsage[0x0001].Should().BeFalse(
            "lenient mode stores the invalid value as false");

        AssertStrictThrowsWithCode(content, ParseDiagnosticCodes.XddInvalidDummyUsage, xdd: true);
    }

    [Fact]
    public void OverlongDummyUsageKey_Xdd_LenientReports_StrictThrowsSameCode()
    {
        var content = MinimalXdd.Replace(
            "</CANopenObjectList>",
            "</CANopenObjectList>\n        <dummyUsage><dummy entry=\"Dummy00001=1\"/></dummyUsage>");

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.XddInvalidDummyUsage);
        diagnostic.Path.Should().Be("dummyUsage/dummy");
        diagnostic.RawValue.Should().Be("Dummy00001=1");
        result.Model.ObjectDictionary?.DummyUsage.Should().ContainKey(0x0001,
            "lenient mode accepts the overlong key");

        AssertStrictThrowsWithCode(content, ParseDiagnosticCodes.XddInvalidDummyUsage, xdd: true);
    }

    // --- value object -------------------------------------------------------

    [Fact]
    public void ParseDiagnostic_ToString_IncludesLineWhenKnown()
    {
        var withLine = new ParseDiagnostic(
            ParseSeverity.Warning, "CODE", "Section.Key", "message", line: 12);
        var withoutLine = new ParseDiagnostic(
            ParseSeverity.Info, "CODE", "Section.Key", "message");

        withLine.ToString().Should().Be("[Warning] CODE at Section.Key:12: message");
        withoutLine.ToString().Should().Be("[Info] CODE at Section.Key: message");
    }

    [Fact]
    public void ParseDiagnostic_ToString_OmitsLocationWhenPathIsEmpty()
    {
        // Shared token converters report without location context.
        var emptyPath = new ParseDiagnostic(
            ParseSeverity.Warning, "CODE", string.Empty, "message");
        var emptyPathWithLine = new ParseDiagnostic(
            ParseSeverity.Warning, "CODE", string.Empty, "message", line: 7);

        emptyPath.ToString().Should().Be("[Warning] CODE: message");
        emptyPathWithLine.ToString().Should().Be("[Warning] CODE at line 7: message");
    }

    // --- helpers ------------------------------------------------------------

    private static void AssertStrictThrowsWithCode(string content, string code, bool xdd = false)
    {
        var act = () => xdd
            ? CanOpenFile.Xdd.ReadStringWithDiagnostics(content, Strict)
            : CanOpenFile.Eds.ReadStringWithDiagnostics(content, Strict);

        act.Should().Throw<EdsParseException>()
            .Which.Code.Should().Be(code, "strict mode must carry the same diagnostic code");
    }

    /// <summary>
    /// Loads the well-formed EDS fixture and injects a duplicate <c>FileName</c> key
    /// into <c>[FileInfo]</c>: lenient mode keeps the last value, strict mode throws.
    /// </summary>
    private static string EdsWithDuplicateKey()
    {
        var content = File.ReadAllText("Fixtures/sample_device.eds");
        var match = Regex.Match(content, "(?m)^FileName=.+$");
        match.Success.Should().BeTrue("the fixture must contain a FileName key");
        return content.Insert(
            match.Index + match.Length,
            "\nFileName=duplicate-b.eds");
    }

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
}
