namespace EdsDcfNet.Tests.Writers;

using System.Xml.Linq;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Validation;

/// <summary>
/// CiA 311 v1.1.0 value ranges the model could not carry (review findings X4, X5, X9):
/// the <c>100 Kbps</c> and <c>auto-baudRate</c> baud rates with <c>baudRate/@defaultValue</c>,
/// the free-text <c>fileVersion</c> (<c>xsd:string</c>) and the mandatory object 1018h.
/// </summary>
public class XddValueRangesMandatoryObjectsTests
{
    private static readonly CanOpenFileOptions Strict = new() { StrictParsing = true };

    // ── X4: baud rates ───────────────────────────────────────────────────────

    [Fact]
    public void WriteToString_XddWithHundredKbpsAutoBaudRateAndDefault_RoundTripsXddToXdd()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Document(
            baudRate: BaudRate("100 Kbps", "100 Kbps", "250 Kbps", "auto-baudRate")));

        // Act
        var plain = CanOpenFile.Xdd.WriteToString(eds);
        var validated = CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);
        var again = CanOpenFile.Xdd.ReadString(plain, Strict);

        // Assert
        eds.DeviceInfo.SupportedBaudRates.BaudRate100.Should().BeTrue();
        eds.DeviceInfo.SupportedBaudRates.AutoBaudRate.Should().BeTrue();
        eds.DeviceInfo.SupportedBaudRates.BaudRate250.Should().BeTrue();
        foreach (var xml in new[] { plain, validated })
        {
            DefaultBaudRate(xml).Should().Be("100 Kbps");
            SupportedBaudRates(xml).Should().Equal("100 Kbps", "250 Kbps", "auto-baudRate");
        }

        again.DeviceInfo.SupportedBaudRates.BaudRate100.Should().BeTrue();
        again.DeviceInfo.SupportedBaudRates.AutoBaudRate.Should().BeTrue();
    }

    [Fact]
    public void WriteToString_XddWithAutoBaudRateDefault_RoundTripsXddToXdc()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Document(baudRate: BaudRate("auto-baudRate", "auto-baudRate")));
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, 5, new DateTime(2027, 3, 4, 14, 30, 0, DateTimeKind.Unspecified), 250);

        // Act
        var written = CanOpenFile.Xdc.WriteToString(dcf);

        // Assert
        DefaultBaudRate(written).Should().Be("auto-baudRate");
        SupportedBaudRates(written).Should().Equal("auto-baudRate");
    }

    [Fact]
    public void WriteToString_XddDefaultDiffersFromDerivedDefault_KeepsReadDefault()
    {
        // Arrange — 250 Kbps would be derived; the file says 500 Kbps.
        var eds = CanOpenFile.Xdd.ReadString(Document(baudRate: BaudRate("500 Kbps", "250 Kbps", "500 Kbps")));

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        DefaultBaudRate(written).Should().Be("500 Kbps");
    }

    [Fact]
    public void WriteToString_XddWithoutDefaultValue_DerivesDefaultFromFlags()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Document(baudRate: BaudRate(null, "125 Kbps", "500 Kbps")));

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        DefaultBaudRate(written).Should().Be("500 Kbps");
    }

    [Fact]
    public void WriteToString_XddSupportedFlagsChangedAfterRead_DefaultFollowsFlags()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Document(baudRate: BaudRate("500 Kbps", "250 Kbps", "500 Kbps")));
        eds.DeviceInfo.SupportedBaudRates.BaudRate500 = false;

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert — the read default is stale now, the derived one wins.
        DefaultBaudRate(written).Should().Be("250 Kbps");
        SupportedBaudRates(written).Should().Equal("250 Kbps");
    }

    [Fact]
    public void WriteToString_XddSupportedFlagsRestoredToBaselineAfterRead_KeepsReadDefault()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Document(baudRate: BaudRate("500 Kbps", "250 Kbps", "500 Kbps")));
        eds.DeviceInfo.SupportedBaudRates.BaudRate500 = false;
        eds.DeviceInfo.SupportedBaudRates.BaudRate500 = true;

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        DefaultBaudRate(written).Should().Be("500 Kbps");
    }

    [Fact]
    public void WriteToString_ModelWithHundredAndAutoFlagsOnly_DerivesAutoAsLastResortDefault()
    {
        // Arrange
        var eds = ValidEds(withoutBaudRates: true);
        eds.DeviceInfo.SupportedBaudRates.BaudRate100 = true;
        var onlyAuto = ValidEds(withoutBaudRates: true);
        onlyAuto.DeviceInfo.SupportedBaudRates.AutoBaudRate = true;

        // Act
        var withHundred = CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);
        var withAuto = CanOpenFile.Xdd.WriteToString(onlyAuto, CanOpenWriteOptions.Validated);

        // Assert
        DefaultBaudRate(withHundred).Should().Be("100 Kbps");
        SupportedBaudRates(withHundred).Should().Equal("100 Kbps");
        DefaultBaudRate(withAuto).Should().Be("auto-baudRate");
        SupportedBaudRates(withAuto).Should().Equal("auto-baudRate");
    }

    [Fact]
    public void ReadString_XddHundredKbpsAndAutoBaudRate_StrictParsingDoesNotThrow()
    {
        // Act
        var eds = CanOpenFile.Xdd.ReadString(
            Document(baudRate: BaudRate("auto-baudRate", "100 Kbps", "auto-baudRate")), Strict);

        // Assert
        eds.DeviceInfo.SupportedBaudRates.BaudRate100.Should().BeTrue();
        eds.DeviceInfo.SupportedBaudRates.AutoBaudRate.Should().BeTrue();
    }

    [Theory]
    [InlineData("777 Kbps", "250 Kbps")]
    [InlineData("250 Kbps", "banana")]
    public void ReadString_XddUnknownBaudRateText_StrictParsingStillThrows(string defaultValue, string supported)
    {
        // Act
        var act = () => CanOpenFile.Xdd.ReadString(Document(baudRate: BaudRate(defaultValue, supported)), Strict);

        // Assert
        act.Should().Throw<EdsParseException>().Which.Code.Should().Be(ParseDiagnosticCodes.XddUnknownBaudRate);
    }

    [Fact]
    public void ReadString_XddUnknownDefaultValue_LenientDerivesDefaultAndReports()
    {
        // Act
        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(Document(baudRate: BaudRate("777 Kbps", "125 Kbps")));
        var written = CanOpenFile.Xdd.WriteToString(result.Model);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Code == ParseDiagnosticCodes.XddUnknownBaudRate && d.RawValue == "777 Kbps");
        DefaultBaudRate(written).Should().Be("125 Kbps");
    }

    [Fact]
    public void ConvertToDcf_XddWithHundredAndAutoBaudRatesAndDefault_CarriesThemIntoXdc()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Document(
            baudRate: BaudRate("100 Kbps", "100 Kbps", "250 Kbps", "auto-baudRate")));

        // Act
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, 5, new DateTime(2027, 3, 4, 14, 30, 0, DateTimeKind.Unspecified), 250);
        var written = CanOpenFile.Xdc.WriteToString(dcf);

        // Assert
        dcf.DeviceInfo.SupportedBaudRates.BaudRate100.Should().BeTrue();
        dcf.DeviceInfo.SupportedBaudRates.AutoBaudRate.Should().BeTrue();
        DefaultBaudRate(written).Should().Be("100 Kbps");
        SupportedBaudRates(written).Should().Equal("100 Kbps", "250 Kbps", "auto-baudRate");
    }

    [Fact]
    public void WriteToString_EdsAndDcfFromXddWithNewBaudRates_DoNotEmitThem()
    {
        // Arrange — EDS/DCF know only the eight CiA 306 rates; the notice is WP-42.
        var eds = CanOpenFile.Xdd.ReadString(Document(baudRate: BaudRate("100 Kbps", "100 Kbps", "250 Kbps", "auto-baudRate")));

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        written.Should().Contain("BaudRate_250=1");
        written.Should().NotContain("BaudRate_100=");
        written.Should().NotContain("auto-baudRate");
    }

    // ── X5: fileVersion ──────────────────────────────────────────────────────

    [Fact]
    public void WriteToString_XddFreeTextFileVersion_RoundTripsXddToXddIncludingValidatedAndStrictRead()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Document(fileVersion: "vendor-r7"), Strict);

        // Act
        var plain = CanOpenFile.Xdd.WriteToString(eds);
        var validated = CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);
        var again = CanOpenFile.Xdd.ReadString(validated, Strict);

        // Assert
        eds.FileInfo.FileVersionText.Should().Be("vendor-r7");
        eds.FileInfo.FileVersion.Should().Be(1);
        Attributes(plain, "fileVersion").Should().Equal("vendor-r7", "vendor-r7");
        Attributes(validated, "fileVersion").Should().Equal("vendor-r7", "vendor-r7");
        again.FileInfo.FileVersionText.Should().Be("vendor-r7");
    }

    [Fact]
    public void WriteToString_XddFreeTextFileVersion_RoundTripsXddToXdc()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Document(fileVersion: "vendor-r7"));
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, 5, new DateTime(2027, 3, 4, 14, 30, 0, DateTimeKind.Unspecified), 250);

        // Act
        var xdc = CanOpenFile.Xdc.WriteToString(dcf);
        var dcfText = CanOpenFile.Dcf.WriteToString(dcf);

        // Assert
        dcf.FileInfo.FileVersionText.Should().Be("vendor-r7");
        Attributes(xdc, "fileVersion").Should().Equal("vendor-r7", "vendor-r7");
        dcfText.Should().Contain("FileVersion=1");
    }

    [Fact]
    public void ReadStringWithDiagnostics_XddFreeTextFileVersion_ReportsDiagnosticInBothModes()
    {
        // Arrange
        var xml = Document(fileVersion: "vendor-r7");

        // Act
        var lenient = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml);
        var strict = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml, Strict);

        // Assert
        foreach (var result in new[] { lenient, strict })
        {
            result.Diagnostics.Should().ContainSingle(d =>
                d.Code == ParseDiagnosticCodes.XddFileVersionNotNumeric && d.RawValue == "vendor-r7");
        }
    }

    [Theory]
    [InlineData("256")]
    [InlineData("-1")]
    [InlineData("1e3")]
    public void ReadString_XddFileVersionOutsideByte_KeepsTextAndDefaultVersion(string text)
    {
        // Act
        var eds = CanOpenFile.Xdd.ReadString(Document(fileVersion: text), Strict);
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        eds.FileInfo.FileVersion.Should().Be(1);
        Attributes(written, "fileVersion").Should().Equal(text, text);
    }

    [Fact]
    public void ReadStringWithDiagnostics_XddMajorMinorFileVersionWithMajorAboveByte_KeepsTextWithoutThrowing()
    {
        // Act
        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(Document(fileVersion: "256.0"));

        // Assert
        result.Model.FileInfo.FileVersion.Should().Be(1);
        result.Model.FileInfo.FileVersionText.Should().Be("256.0");
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.XddFileVersionNotNumeric);
    }

    [Fact]
    public void WriteToString_XddFileVersionChangedAfterRead_FollowsProperty()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Document(fileVersion: "vendor-r7"));
        eds.FileInfo.FileVersion = 9;

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        Attributes(written, "fileVersion").Should().Equal("9", "9");
    }

    [Fact]
    public void WriteToString_XddFileVersionRestoredToBaselineAfterRead_KeepsText()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Document(fileVersion: "vendor-r7"));
        eds.FileInfo.FileVersion = 9;
        eds.FileInfo.FileVersion = 1;

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        Attributes(written, "fileVersion").Should().Equal("vendor-r7", "vendor-r7");
    }

    [Fact]
    public void WriteToString_XddFileVersionTextAssignedByCaller_IsWritten()
    {
        // Arrange — a model that never came from a reader has no baseline to compare with.
        var eds = ValidEds();
        eds.FileInfo.FileVersionText = "build-42";

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        Attributes(written, "fileVersion").Should().Equal("build-42", "build-42");
    }

    [Theory]
    [InlineData("3", (byte)3)]
    [InlineData("010", (byte)10)]
    [InlineData("255", (byte)255)]
    public void ReadString_XddNumericFileVersion_SetsFileVersionAndWritesCanonicalNumber(string text, byte expected)
    {
        // Act
        var eds = CanOpenFile.Xdd.ReadString(Document(fileVersion: text), Strict);
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        eds.FileInfo.FileVersion.Should().Be(expected);
        Attributes(written, "fileVersion").Should().Equal(
            expected.ToString(System.Globalization.CultureInfo.InvariantCulture),
            expected.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ReadString_XddMajorMinorFileVersion_StrictStillThrowsAndLenientUsesMajor()
    {
        // Act
        var act = () => CanOpenFile.Xdd.ReadString(Document(fileVersion: "1.0"), Strict);
        var lenient = CanOpenFile.Xdd.ReadString(Document(fileVersion: "2.1"));

        // Assert
        act.Should().Throw<EdsParseException>().Which.Code.Should().Be(ParseDiagnosticCodes.XddFileVersionMajorMinor);
        lenient.FileInfo.FileVersion.Should().Be(2);
    }

    // ── X9: 1018h is a mandatory object ──────────────────────────────────────

    [Fact]
    public void ReadString_XddWithIdentityObject_ListsIndex1018AsMandatory()
    {
        // Act
        var eds = CanOpenFile.Xdd.ReadString(Document());

        // Assert
        eds.ObjectDictionary.MandatoryObjects.Should().Equal(0x1000, 0x1001, 0x1018);
        eds.ObjectDictionary.OptionalObjects.Should().NotContain(0x1018);
    }

    [Fact]
    public void Validate_XddReadWithMandatoryObjects_ReportsNoMissingMandatoryListing()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Document());

        // Act
        var issues = CanOpenFile.Validate(eds, new CanOpenValidationOptions { RequireMandatoryEntries = true });

        // Assert
        issues.Should().NotContain(issue => issue.Message.Contains("0x1018", StringComparison.Ordinal));
    }

    [Fact]
    public void WriteToString_XddReadWithIdentityObject_WritesMandatoryObjectsCountOfThree()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Document());

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);
        var list = XDocument.Parse(written).Descendants().First(e => e.Name.LocalName == "CANopenObjectList");

        // Assert
        list.Attribute("mandatoryObjects")!.Value.Should().Be("3");
        list.Attribute("optionalObjects")!.Value.Should().Be("0");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ElectronicDataSheet ValidEds(bool withoutBaudRates = false) =>
        CanOpenFile.Xdd.ReadString(Document(baudRate: withoutBaudRates ? BaudRate(null) : null));

    private static string BaudRate(string? defaultValue, params string[] supported) =>
        "<baudRate" + (defaultValue == null ? string.Empty : " defaultValue=\"" + defaultValue + "\"") + ">" +
        string.Concat(supported.Select(value => "<supportedBaudRate value=\"" + value + "\"/>")) +
        "</baudRate>";

    private static string DefaultBaudRate(string xml) =>
        XDocument.Parse(xml).Descendants().Single(e => e.Name.LocalName == "baudRate").Attribute("defaultValue")!.Value;

    private static List<string> SupportedBaudRates(string xml) =>
        XDocument.Parse(xml).Descendants()
            .Where(e => e.Name.LocalName == "supportedBaudRate")
            .Select(e => e.Attribute("value")!.Value)
            .ToList();

    private static List<string> Attributes(string xml, string attribute) =>
        XDocument.Parse(xml).Descendants()
            .Where(e => e.Name.LocalName == "ProfileBody")
            .Select(e => e.Attribute(attribute)!.Value)
            .ToList();

    private static string Document(string fileVersion = "1", string? baudRate = null)
    {
        var file = " fileName=\"t.xdd\" fileCreator=\"me\" fileCreationDate=\"2026-01-01\" fileVersion=\"" + fileVersion + "\"";
        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
               "<ISO15745ProfileContainer xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
               "<ISO15745Profile><ProfileBody xsi:type=\"ProfileBody_Device_CANopen\"" + file + ">" +
               "<DeviceIdentity><vendorName>T</vendorName><vendorID>0x1</vendorID><productName>T</productName><productID>0x1</productID></DeviceIdentity>" +
               "<DeviceManager/><DeviceFunction/></ProfileBody></ISO15745Profile>" +
               "<ISO15745Profile><ProfileBody xsi:type=\"ProfileBody_CommunicationNetwork_CANopen\"" + file + ">" +
               "<ApplicationLayers><CANopenObjectList mandatoryObjects=\"3\" optionalObjects=\"0\" manufacturerObjects=\"0\">" +
               "<CANopenObject index=\"1000\" name=\"Device Type\" objectType=\"7\" dataType=\"0007\" accessType=\"ro\" PDOmapping=\"no\"/>" +
               "<CANopenObject index=\"1001\" name=\"Error register\" objectType=\"7\" dataType=\"0005\" accessType=\"ro\" PDOmapping=\"no\"/>" +
               "<CANopenObject index=\"1018\" name=\"Identity\" objectType=\"9\" subNumber=\"1\">" +
               "<CANopenSubObject subIndex=\"00\" name=\"Highest sub-index\" objectType=\"7\" dataType=\"0005\" accessType=\"ro\" PDOmapping=\"no\"/>" +
               "</CANopenObject>" +
               "</CANopenObjectList></ApplicationLayers>" +
               "<TransportLayers><PhysicalLayer>" + (baudRate ?? BaudRate("250 Kbps", "250 Kbps")) + "</PhysicalLayer></TransportLayers>" +
               "<NetworkManagement><CANopenGeneralFeatures granularity=\"8\" nrOfRxPDO=\"0\" nrOfTxPDO=\"0\"/></NetworkManagement>" +
               "</ProfileBody></ISO15745Profile></ISO15745ProfileContainer>";
    }
}
