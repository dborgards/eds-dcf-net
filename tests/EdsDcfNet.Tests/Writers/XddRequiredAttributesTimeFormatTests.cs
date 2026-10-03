namespace EdsDcfNet.Tests.Writers;

using System.Xml.Linq;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Tests.Infrastructure;
using EdsDcfNet.Tests.Utilities;
using EdsDcfNet.Validation;

/// <summary>
/// CiA 311 v1.1.0 <c>ag_formatAndFile</c> (CommonElements.xsd) and
/// <c>deviceCommissioning</c> (ProfileBody_Network_CANopen.xsd): required attributes are
/// always written, invalid optional values are omitted, <c>xsd:time</c> is written from the
/// EDS <c>hh:mmAM/PM</c> form, and schema-valid values the model cannot hold are preserved.
/// </summary>
public class XddRequiredAttributesTimeFormatTests
{
    private static readonly CanOpenFileOptions Strict = new() { StrictParsing = true };

    // ── deviceCommissioning: preserved values (Rule 15) ──────────────────────

    [Fact]
    public void WriteToString_XdcAutoBaudRate_RoundTripsUnchangedIncludingValidatedAndStrictRead()
    {
        // Arrange
        var dcf = CanOpenFile.Xdc.ReadString(Xdc(actualBaudRate: "auto-baudRate"), Strict);

        // Act
        var plain = CanOpenFile.Xdc.WriteToString(dcf);
        var validated = CanOpenFile.Xdc.WriteToString(dcf, CanOpenWriteOptions.Validated);
        var again = CanOpenFile.Xdc.ReadString(validated, Strict);

        // Assert
        dcf.DeviceCommissioning.Baudrate.Should().Be(0);
        Attribute(plain, "deviceCommissioning", "actualBaudRate").Should().Be("auto-baudRate");
        Attribute(validated, "deviceCommissioning", "actualBaudRate").Should().Be("auto-baudRate");
        again.DeviceCommissioning.Baudrate.Should().Be(0);
    }

    [Fact]
    public void WriteToString_XdcNetworkNumberAboveUInt32_RoundTripsUnchangedIncludingValidatedAndStrictRead()
    {
        // Arrange
        var dcf = CanOpenFile.Xdc.ReadString(Xdc(networkNumber: "4294967296"), Strict);

        // Act
        var plain = CanOpenFile.Xdc.WriteToString(dcf);
        var validated = CanOpenFile.Xdc.WriteToString(dcf, CanOpenWriteOptions.Validated);

        // Assert
        dcf.DeviceCommissioning.NetNumber.Should().Be(0u);
        Attribute(plain, "deviceCommissioning", "networkNumber").Should().Be("4294967296");
        Attribute(validated, "deviceCommissioning", "networkNumber").Should().Be("4294967296");
    }

    [Fact]
    public void ReadStringWithDiagnostics_XdcUnmappableCommissioningValues_ReportsDiagnosticsWithoutThrowingInStrict()
    {
        // Arrange
        var xml = Xdc(actualBaudRate: "auto-baudRate", networkNumber: "4294967296");

        // Act
        var lenient = CanOpenFile.Xdc.ReadStringWithDiagnostics(xml);
        var strict = CanOpenFile.Xdc.ReadStringWithDiagnostics(xml, Strict);

        // Assert
        foreach (var result in new[] { lenient, strict })
        {
            result.Diagnostics.Should().Contain(d =>
                d.Code == ParseDiagnosticCodes.XddUnknownBaudRate && d.RawValue == "auto-baudRate");
            result.Diagnostics.Should().Contain(d =>
                d.Code == ParseDiagnosticCodes.XddNetworkNumberExceedsUInt32 && d.RawValue == "4294967296");
        }
    }

    [Fact]
    public void ReadString_XdcMalformedNetworkNumber_StrictStillThrows()
    {
        // Arrange — not an xsd:unsignedLong, so this is invalid input rather than a model limit.
        var xml = Xdc(networkNumber: "banana");

        // Act
        var act = () => CanOpenFile.Xdc.ReadString(xml, Strict);

        // Assert
        act.Should().Throw<EdsParseException>().Which.Code.Should().Be(ParseDiagnosticCodes.XddInvalidNumericAttribute);
    }

    [Fact]
    public void WriteToString_XdcBaudrateChangedAfterRead_FollowsProperty()
    {
        // Arrange
        var dcf = CanOpenFile.Xdc.ReadString(Xdc(actualBaudRate: "auto-baudRate", networkNumber: "4294967296"));
        dcf.DeviceCommissioning.Baudrate = 125;
        dcf.DeviceCommissioning.NetNumber = 7;

        // Act
        var written = CanOpenFile.Xdc.WriteToString(dcf, CanOpenWriteOptions.Validated);

        // Assert
        Attribute(written, "deviceCommissioning", "actualBaudRate").Should().Be("125 Kbps");
        Attribute(written, "deviceCommissioning", "networkNumber").Should().Be("7");
    }

    [Fact]
    public void WriteToString_XdcBaudrateRestoredToBaselineAfterRead_KeepsPreservedText()
    {
        // Arrange
        var dcf = CanOpenFile.Xdc.ReadString(Xdc(actualBaudRate: "auto-baudRate"));
        dcf.DeviceCommissioning.Baudrate = 250;
        dcf.DeviceCommissioning.Baudrate = 0;

        // Act — the value equals the baseline again, so the preserved text is still current.
        var written = CanOpenFile.Xdc.WriteToString(dcf, CanOpenWriteOptions.Validated);

        // Assert
        Attribute(written, "deviceCommissioning", "actualBaudRate").Should().Be("auto-baudRate");
    }

    [Fact]
    public void ConvertToDcf_FromXddWithTimeZoneDates_WritesFreshDatesWithoutOldZone()
    {
        // Arrange — the conversion builds new FileInfo/commissioning; nothing stale may carry over.
        var eds = CanOpenFile.Xdd.ReadString(Xdd(creationDate: "2026-01-01+05:30", creationTime: "19:31:59+01:00"));
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, 5, new DateTime(2027, 3, 4, 14, 30, 0, DateTimeKind.Unspecified), 250);

        // Act
        var written = CanOpenFile.Xdc.WriteToString(dcf, CanOpenWriteOptions.Validated);

        // Assert
        Attribute(written, "ProfileBody", "fileCreationDate").Should().Be("2027-03-04");
        Attribute(written, "ProfileBody", "fileCreationTime").Should().Be("14:30:00");
        Attribute(written, "deviceCommissioning", "actualBaudRate").Should().Be("250 Kbps");
        written.Should().NotContain("05:30");
    }

    [Fact]
    public void WriteToString_XdcDefaultCommissioningAttributes_AreAllWrittenAsRequired()
    {
        // Arrange
        var dcf = ValidXmlDcf();
        dcf.DeviceCommissioning = new DeviceCommissioning { NodeId = 5, Baudrate = 0, NetNumber = 7, CANopenManager = true };

        // Act
        var written = CanOpenFile.Xdc.WriteToString(dcf);
        var element = XDocument.Parse(written).Descendants().Single(e => e.Name.LocalName == "deviceCommissioning");

        // Assert
        element.Attributes().Select(a => a.Name.LocalName).Should().BeEquivalentTo(
            new[] { "nodeID", "nodeName", "actualBaudRate", "networkNumber", "networkName", "CANopenManager" });
        element.Attribute("nodeName")!.Value.Should().BeEmpty();
        element.Attribute("actualBaudRate")!.Value.Should().BeEmpty();
        element.Attribute("networkName")!.Value.Should().BeEmpty();
    }

    [Fact]
    public void WriteToString_XdcBaudrateZeroWithoutPreservedValue_ValidatedRejects()
    {
        // Arrange
        var dcf = ValidXmlDcf();
        dcf.DeviceCommissioning.Baudrate = 0;

        // Act
        var act = () => CanOpenFile.Xdc.WriteToString(dcf, CanOpenWriteOptions.Validated);

        // Assert
        act.Should().Throw<ModelValidationException>().Which.Issues.Should().ContainSingle(issue =>
            issue.Code == ValidationIssueCodes.XddActualBaudRateNotSet &&
            issue.Path == "DeviceCommissioning.Baudrate");
    }

    // ── ag_formatAndFile: required strings ───────────────────────────────────

    [Fact]
    public void WriteToString_EmptyFileNameAndCreator_WritesEmptyRequiredAttributes()
    {
        // Arrange
        var eds = ValidXmlEds();
        eds.FileInfo.FileName = string.Empty;
        eds.FileInfo.CreatedBy = string.Empty;

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        Attributes(written, "ProfileBody", "fileName").Should().Equal(string.Empty, string.Empty);
        Attributes(written, "ProfileBody", "fileCreator").Should().Equal(string.Empty, string.Empty);
    }

    [Theory]
    [InlineData("Xdd", "FileInfo.FileName", ValidationIssueCodes.XddFileTextEmpty)]
    [InlineData("Xdc", "FileInfo.FileName", ValidationIssueCodes.XddFileTextEmpty)]
    [InlineData("Xdd", "FileInfo.CreatedBy", ValidationIssueCodes.XddFileTextEmpty)]
    [InlineData("Xdc", "FileInfo.CreatedBy", ValidationIssueCodes.XddFileTextEmpty)]
    public void WriteToString_EmptyRequiredFileText_ValidatedRejects(string format, string path, string code)
    {
        // Arrange
        var model = path.EndsWith("FileName", StringComparison.Ordinal)
            ? WithFileInfo(format, fi => fi.FileName = string.Empty)
            : WithFileInfo(format, fi => fi.CreatedBy = string.Empty);

        // Act
        var act = () => WriteValidated(format, model);

        // Assert
        act.Should().Throw<ModelValidationException>().Which.Issues.Should().ContainSingle(issue =>
            issue.Code == code && issue.Path == path);
    }

    [Fact]
    public void WriteToString_NullRequiredTexts_WritesEmptyStringsInsteadOfThrowing()
    {
        // Arrange — a caller may assign null to a non-nullable property.
        var dcf = ValidXmlDcf();
        dcf.FileInfo.FileName = null!;
        dcf.FileInfo.CreatedBy = null!;
        dcf.DeviceCommissioning.NodeName = null!;
        dcf.DeviceCommissioning.NetworkName = null!;

        // Act
        var written = CanOpenFile.Xdc.WriteToString(dcf);

        // Assert
        Attributes(written, "ProfileBody", "fileName").Should().Equal(string.Empty, string.Empty);
        Attributes(written, "ProfileBody", "fileCreator").Should().Equal(string.Empty, string.Empty);
        Attribute(written, "deviceCommissioning", "nodeName").Should().BeEmpty();
        Attribute(written, "deviceCommissioning", "networkName").Should().BeEmpty();
    }

    [Fact]
    public void Apply_UnrelatedModelOrOmittedCommissioning_ReportsNothingForCommissioning()
    {
        // Arrange
        var issues = new List<ValidationIssue>();
        var withoutCommissioning = ValidXmlDcf();
        withoutCommissioning.DeviceCommissioning = null!;
        var defaultCommissioning = ValidXmlDcf();
        defaultCommissioning.DeviceCommissioning = new DeviceCommissioning();

        // Act
        XmlWriteRules.Apply(new NodelistProject(), issues);
        XmlWriteRules.Apply(withoutCommissioning, issues);
        XmlWriteRules.Apply(defaultCommissioning, issues);

        // Assert
        issues.Should().BeEmpty();
    }

    [Fact]
    public void WriteToString_ModifiedByEmpty_OmitsAttribute()
    {
        // Arrange
        var eds = ValidXmlEds();
        eds.FileInfo.ModifiedBy = string.Empty;

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        written.Should().NotContain("fileModifiedBy");
    }

    // ── ag_formatAndFile: dates ──────────────────────────────────────────────

    [Theory]
    [InlineData("Xdd", "02-29-2024", "2024-02-29")]
    [InlineData("Xdc", "12-31-2026", "2026-12-31")]
    [InlineData("Xdd", "01-01-0001", "0001-01-01")]
    public void WriteToString_ValidCreationDate_WritesXsdDate(string format, string edsDate, string expected)
    {
        // Arrange
        var model = WithFileInfo(format, fi => fi.CreationDate = edsDate);

        // Act
        var plain = Write(format, model);
        var validated = WriteValidated(format, model);

        // Assert
        Attributes(plain, "ProfileBody", "fileCreationDate").Should().Equal(expected, expected);
        Attributes(validated, "ProfileBody", "fileCreationDate").Should().Equal(expected, expected);
    }

    [Theory]
    [InlineData("Xdd", "02-30-2026")]
    [InlineData("Xdd", "02-29-2026")]
    [InlineData("Xdd", "04-31-2026")]
    [InlineData("Xdd", "13-01-2026")]
    [InlineData("Xdd", "00-10-2026")]
    [InlineData("Xdd", "banana")]
    [InlineData("Xdc", "02-30-2026")]
    [InlineData("Xdc", "banana")]
    [InlineData("Xdd", "2026-02-30")]
    public void WriteToString_InvalidCreationDate_OmitsAttributeUnvalidatedAndRejectsValidated(string format, string value)
    {
        // Arrange
        var model = WithFileInfo(format, fi => fi.CreationDate = value);

        // Act
        var plain = Write(format, model);
        var validated = () => WriteValidated(format, model);

        // Assert
        plain.Should().NotContain("fileCreationDate");
        validated.Should().Throw<ModelValidationException>().Which.Issues.Should().ContainSingle(issue =>
            issue.Code == ValidationIssueCodes.XddFileDateInvalid &&
            issue.Path == "FileInfo.CreationDate");
    }

    [Theory]
    [InlineData("Xdd")]
    [InlineData("Xdc")]
    public void WriteToString_MissingCreationDate_OmitsAttributeUnvalidatedAndRejectsValidated(string format)
    {
        // Arrange
        var model = WithFileInfo(format, fi => fi.CreationDate = string.Empty);

        // Act
        var plain = Write(format, model);
        var validated = () => WriteValidated(format, model);

        // Assert
        plain.Should().NotContain("fileCreationDate");
        validated.Should().Throw<ModelValidationException>().Which.Issues.Should().ContainSingle(issue =>
            issue.Code == ValidationIssueCodes.XddFileCreationDateMissing &&
            issue.Path == "FileInfo.CreationDate");
    }

    [Theory]
    [InlineData("02-30-2026")]
    [InlineData("banana")]
    public void WriteToString_InvalidModificationDate_OmitsAttributeUnvalidatedAndRejectsValidated(string value)
    {
        // Arrange
        var eds = ValidXmlEds();
        eds.FileInfo.ModificationDate = value;

        // Act
        var plain = CanOpenFile.Xdd.WriteToString(eds);
        var validated = () => CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        plain.Should().NotContain("fileModificationDate");
        validated.Should().Throw<ModelValidationException>().Which.Issues.Should().ContainSingle(issue =>
            issue.Code == ValidationIssueCodes.XddFileDateInvalid &&
            issue.Path == "FileInfo.ModificationDate");
    }

    [Fact]
    public void WriteToString_EmptyAndValidModificationDate_AreValidated()
    {
        // Arrange
        var empty = ValidXmlEds();
        empty.FileInfo.ModificationDate = string.Empty;
        var set = ValidXmlEds();
        set.FileInfo.ModificationDate = "03-04-2027";

        // Act
        var writtenEmpty = CanOpenFile.Xdd.WriteToString(empty, CanOpenWriteOptions.Validated);
        var writtenSet = CanOpenFile.Xdd.WriteToString(set, CanOpenWriteOptions.Validated);

        // Assert
        writtenEmpty.Should().NotContain("fileModificationDate");
        Attributes(writtenSet, "ProfileBody", "fileModificationDate").Should().Equal("2027-03-04", "2027-03-04");
    }

    [Theory]
    [InlineData("2026-01-01+05:30")]
    [InlineData("2026-01-01Z")]
    [InlineData("2026-01-01-14:00")]
    public void WriteToString_XddDateWithTimeZone_RoundTripsXddToXddCharacterExact(string lexical)
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Xdd(creationDate: lexical, modificationDate: lexical));

        // Act
        var plain = CanOpenFile.Xdd.WriteToString(eds);
        var validated = CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        eds.FileInfo.CreationDate.Should().Be("01-01-2026");
        Attributes(plain, "ProfileBody", "fileCreationDate").Should().Equal(lexical, lexical);
        Attributes(plain, "ProfileBody", "fileModificationDate").Should().Equal(lexical, lexical);
        Attributes(validated, "ProfileBody", "fileCreationDate").Should().Equal(lexical, lexical);
    }

    [Fact]
    public void WriteToString_XdcDateWithTimeZone_RoundTripsXdcToXdc()
    {
        // Arrange
        var dcf = CanOpenFile.Xdc.ReadString(Xdc(creationDate: "2026-01-01+05:30"));

        // Act
        var written = CanOpenFile.Xdc.WriteToString(dcf, CanOpenWriteOptions.Validated);

        // Assert
        Attributes(written, "ProfileBody", "fileCreationDate").Should().Equal("2026-01-01+05:30", "2026-01-01+05:30");
    }

    [Fact]
    public void WriteToString_CreationDateChangedAfterRead_FollowsPropertyWithoutOldZone()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Xdd(creationDate: "2026-01-01+05:30", modificationDate: "2026-01-02Z"));
        eds.FileInfo.CreationDate = "02-03-2026";
        eds.FileInfo.ModificationDate = "04-05-2026";

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        Attributes(written, "ProfileBody", "fileCreationDate").Should().Equal("2026-02-03", "2026-02-03");
        Attributes(written, "ProfileBody", "fileModificationDate").Should().Equal("2026-04-05", "2026-04-05");
        written.Should().NotContain("05:30");
    }

    [Fact]
    public void WriteToString_CreationDateReassignedSameValueAfterRead_KeepsTimeZone()
    {
        // Arrange — the calendar date is unchanged, so the preserved spelling still applies.
        var eds = CanOpenFile.Xdd.ReadString(Xdd(creationDate: "2026-01-01+05:30"));
        eds.FileInfo.CreationDate = "01-01-2026";

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        Attributes(written, "ProfileBody", "fileCreationDate").Should().Equal("2026-01-01+05:30", "2026-01-01+05:30");
    }

    [Fact]
    public void ReadString_XddDateWithSurroundingWhitespace_IsTrimmedAndConverted()
    {
        // Arrange — xsd:date collapses whitespace.
        var eds = CanOpenFile.Xdd.ReadString(Xdd(creationDate: "  2026-01-01  "));

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        eds.FileInfo.CreationDate.Should().Be("01-01-2026");
        Attributes(written, "ProfileBody", "fileCreationDate").Should().Equal("2026-01-01", "2026-01-01");
    }

    [Fact]
    public void WriteToString_CreationDateAlreadyXsdForm_IsKept()
    {
        // Arrange — a model filled with an xsd:date (including a year beyond four digits).
        var eds = ValidXmlEds();
        eds.FileInfo.CreationDate = "12345-01-01";

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        Attributes(written, "ProfileBody", "fileCreationDate").Should().Equal("12345-01-01", "12345-01-01");
    }

    // ── ag_formatAndFile: times ──────────────────────────────────────────────

    [Theory]
    [InlineData("02:30PM", "14:30:00")]
    [InlineData("2:07AM", "02:07:00")]
    [InlineData("12:00AM", "00:00:00")]
    [InlineData("12:34AM", "00:34:00")]
    [InlineData("12:00PM", "12:00:00")]
    [InlineData("11:59PM", "23:59:00")]
    [InlineData("10:00am", "10:00:00")]
    [InlineData("19:31:59.7179280+01:00", "19:31:59.7179280+01:00")]
    [InlineData("12:37:54Z", "12:37:54Z")]
    [InlineData("00:00:00", "00:00:00")]
    [InlineData("24:00:00", "24:00:00")]
    [InlineData("10:00:00-14:00", "10:00:00-14:00")]
    [InlineData(" 10:00:00 ", "10:00:00")]
    public void WriteToString_ValidFileTimes_WritesXsdTime(string value, string expected)
    {
        // Arrange
        var eds = ValidXmlEds();
        eds.FileInfo.CreationTime = value;
        eds.FileInfo.ModificationTime = value;

        // Act
        var plain = CanOpenFile.Xdd.WriteToString(eds);
        var validated = CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        Attributes(plain, "ProfileBody", "fileCreationTime").Should().Equal(expected, expected);
        Attributes(validated, "ProfileBody", "fileModificationTime").Should().Equal(expected, expected);
    }

    [Theory]
    [InlineData("banana")]
    [InlineData("13:00PM")]
    [InlineData("00:30AM")]
    [InlineData("12:60PM")]
    [InlineData("25:00:00")]
    [InlineData("24:00:01")]
    [InlineData("10:00")]
    [InlineData("10:00:60")]
    [InlineData("10:00:00+15:00")]
    [InlineData("10:00:00+14:30")]
    [InlineData("10:00:00+01:60")]
    [InlineData("10:00:00.")]
    [InlineData("10:00:00 PM")]
    [InlineData("1:00:00")]
    public void WriteToString_InvalidFileTimes_OmitsAttributeUnvalidatedAndRejectsValidated(string value)
    {
        // Arrange
        var eds = ValidXmlEds();
        eds.FileInfo.CreationTime = value;
        eds.FileInfo.ModificationTime = value;

        // Act
        var plain = CanOpenFile.Xdd.WriteToString(eds);
        var validated = () => CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        plain.Should().NotContain("fileCreationTime");
        plain.Should().NotContain("fileModificationTime");
        var issues = validated.Should().Throw<ModelValidationException>().Which.Issues;
        issues.Should().Contain(issue =>
            issue.Code == ValidationIssueCodes.XddFileTimeInvalid && issue.Path == "FileInfo.CreationTime");
        issues.Should().Contain(issue =>
            issue.Code == ValidationIssueCodes.XddFileTimeInvalid && issue.Path == "FileInfo.ModificationTime");
    }

    [Fact]
    public void WriteToString_EmptyFileTimes_AreOmittedAndValidated()
    {
        // Arrange
        var eds = ValidXmlEds();
        eds.FileInfo.CreationTime = string.Empty;
        eds.FileInfo.ModificationTime = "  ";

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        written.Should().NotContain("fileCreationTime");
        written.Should().NotContain("fileModificationTime");
    }

    [Fact]
    public void WriteToString_EdsWithTwelveHourTime_WritesXsdTimeAndRoundTripsThroughXdd()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(
            "[FileInfo]\r\nFileName=a.eds\r\nCreatedBy=me\r\nCreationDate=05-24-2024\r\nCreationTime=02:30PM\r\n" +
            "ModificationDate=05-25-2024\r\nModificationTime=12:00AM\r\n" +
            "[DeviceInfo]\r\nVendorName=V\r\nProductName=P\r\n" +
            "[MandatoryObjects]\r\nSupportedObjects=1\r\n1=0x1000\r\n" +
            "[1000]\r\nParameterName=Device Type\r\nObjectType=0x7\r\nDataType=0x0007\r\nAccessType=ro\r\n");

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);
        var again = CanOpenFile.Xdd.ReadString(written);

        // Assert
        Attributes(written, "ProfileBody", "fileCreationTime").Should().Equal("14:30:00", "14:30:00");
        Attributes(written, "ProfileBody", "fileModificationTime").Should().Equal("00:00:00", "00:00:00");
        again.FileInfo.CreationTime.Should().Be("14:30:00");
    }

    [Fact]
    public void WriteToString_CorpusBasicDeviceXdd_KeepsBothTimeValuesCharacterExact()
    {
        // Arrange
        var path = "Fixtures/Corpus/canopen-node/basicDevice.xdd";
        var eds = CanOpenFile.Xdd.ReadFile(path);

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        Attributes(written, "ProfileBody", "fileCreationTime").Should().Equal("12:37:54.0000000+01:00", "12:37:54.0000000+01:00");
        Attributes(written, "ProfileBody", "fileModificationTime").Should().Equal("19:31:59.7179280+01:00", "19:31:59.7179280+01:00");
    }

    [Fact]
    public void ReadString_XddTime_StaysVerbatimInModel()
    {
        // Arrange — the reader never converts; the writer converts per target format.
        var eds = CanOpenFile.Xdd.ReadString(Xdd(creationTime: "19:31:59.7179280+01:00"));

        // Assert
        eds.FileInfo.CreationTime.Should().Be("19:31:59.7179280+01:00");
    }

    // ── Empty object directory ───────────────────────────────────────────────

    [Fact]
    public void WriteToString_EmptyObjectDictionaryXdd_ValidatedRejectsUnvalidatedWrites()
    {
        // Arrange
        var eds = ValidXmlEds();
        eds.ObjectDictionary.Objects.Clear();
        eds.ObjectDictionary.MandatoryObjects.Clear();

        // Act
        var plain = CanOpenFile.Xdd.WriteToString(eds);
        var validated = () => CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        plain.Should().Contain("CANopenObjectList");
        validated.Should().Throw<ModelValidationException>().Which.Issues.Should().Contain(issue =>
            issue.Code == ValidationIssueCodes.XddObjectDictionaryEmpty &&
            issue.Path == "ObjectDictionary.Objects");
    }

    [Fact]
    public void WriteToString_EmptyObjectDictionaryXdc_ValidatedRejectsUnvalidatedWrites()
    {
        // Arrange
        var dcf = ValidXmlDcf();
        dcf.ObjectDictionary.Objects.Clear();
        dcf.ObjectDictionary.MandatoryObjects.Clear();

        // Act
        var plain = CanOpenFile.Xdc.WriteToString(dcf);
        var validated = () => CanOpenFile.Xdc.WriteToString(dcf, CanOpenWriteOptions.Validated);

        // Assert
        plain.Should().Contain("CANopenObjectList");
        validated.Should().Throw<ModelValidationException>().Which.Issues.Should().Contain(issue =>
            issue.Code == ValidationIssueCodes.XddObjectDictionaryEmpty);
    }

    [Fact]
    public async Task WriteStreamAsync_DefaultXmlModels_ValidatedRejectsAsynchronously()
    {
        // Arrange
        var eds = new ElectronicDataSheet();
        var dcf = new DeviceConfigurationFile();
        using var xddStream = new MemoryStream();
        using var xdcStream = new MemoryStream();

        // Act
        var xdd = () => CanOpenFile.Xdd.WriteStreamAsync(eds, xddStream, CanOpenWriteOptions.Validated);
        var xdc = () => CanOpenFile.Xdc.WriteStreamAsync(dcf, xdcStream, CanOpenWriteOptions.Validated);

        // Assert
        (await xdd.Should().ThrowAsync<ModelValidationException>()).Which.Issues.Should().Contain(issue =>
            issue.Code == ValidationIssueCodes.XddFileCreationDateMissing);
        (await xdc.Should().ThrowAsync<ModelValidationException>()).Which.Issues.Should().Contain(issue =>
            issue.Code == ValidationIssueCodes.XddFileCreationDateMissing);
    }

    [Fact]
    public async Task WriteStreamAsync_InvalidDateXdd_UnvalidatedWritesAsBefore()
    {
        // Arrange
        var eds = ValidXmlEds();
        eds.FileInfo.CreationDate = "banana";
        using var stream = new MemoryStream();

        // Act
        await CanOpenFile.Xdd.WriteStreamAsync(eds, stream);

        // Assert
        stream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void WriteToString_SameModelAsEdsAndDcfValidated_IsNotAffectedByXmlRules()
    {
        // Arrange — the XML-only rules must not touch the INI formats.
        var eds = ValidXmlEds();
        eds.FileInfo.CreationDate = "banana";
        eds.FileInfo.CreationTime = "banana";
        eds.FileInfo.FileName = "a.eds";
        var dcf = ValidXmlDcf();
        dcf.FileInfo.CreationDate = "banana";
        dcf.FileInfo.FileName = "a.dcf";

        // Act
        var edsAct = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);
        var dcfAct = () => CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);
        var xddAct = () => CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        edsAct.Should().NotThrow();
        dcfAct.Should().NotThrow();
        xddAct.Should().Throw<ModelValidationException>();
    }

    // ── schema conformance ───────────────────────────────────────────────────

    [Fact]
    public void WriterOutput_XddWithCompleteMetadata_ValidatesAgainstSchema()
    {
        // Arrange
        var eds = ValidXmlEds();
        eds.FileInfo.CreationTime = "02:30PM";
        eds.FileInfo.ModificationDate = "03-04-2027";
        eds.FileInfo.ModificationTime = "12:00AM";
        eds.FileInfo.ModifiedBy = "someone";

        // Act
        var problems = Cia311Schema.Validate(CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated));

        // Assert
        problems.Should().BeEmpty();
    }

    [Fact]
    public void WriterOutput_XdcWithPreservedValues_ValidatesAgainstSchema()
    {
        // Arrange
        var dcf = CanOpenFile.Xdc.ReadString(
            Xdc(creationDate: "2026-01-01+05:30", actualBaudRate: "auto-baudRate", networkNumber: "4294967296"), Strict);

        // Act
        var problems = Cia311Schema.Validate(CanOpenFile.Xdc.WriteToString(dcf, CanOpenWriteOptions.Validated));

        // Assert
        problems.Should().BeEmpty();
    }

    [Fact]
    public void WriterOutput_DefaultXdcMetadata_ReportsTheMissingRequiredAttributesOnly()
    {
        // Arrange — the unvalidated path stays tolerant and is documented as not schema-valid.
        var dcf = ValidXmlDcf();
        dcf.FileInfo.CreationDate = string.Empty;
        dcf.DeviceCommissioning = new DeviceCommissioning { NodeId = 5 };

        // Act
        var problems = Cia311Schema.Validate(CanOpenFile.Xdc.WriteToString(dcf));

        // Assert
        problems.Should().Contain(problem => problem.Contains("fileCreationDate"));
        problems.Should().NotContain(problem =>
            problem.Contains("nodeName") || problem.Contains("actualBaudRate") || problem.Contains("networkName"));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static ElectronicDataSheet ValidXmlEds()
    {
        return ValidCanOpenModelBuilder.CreateValidEds();
    }

    private static DeviceConfigurationFile ValidXmlDcf()
    {
        return ValidCanOpenModelBuilder.CreateValidDcf();
    }

    private static object WithFileInfo(string format, Action<EdsFileInfo> mutate)
    {
        if (format == "Xdd")
        {
            var eds = ValidXmlEds();
            mutate(eds.FileInfo);
            return eds;
        }

        var dcf = ValidXmlDcf();
        mutate(dcf.FileInfo);
        return dcf;
    }

    private static string Write(string format, object model) => format == "Xdd"
        ? CanOpenFile.Xdd.WriteToString((ElectronicDataSheet)model)
        : CanOpenFile.Xdc.WriteToString((DeviceConfigurationFile)model);

    private static string WriteValidated(string format, object model) => format == "Xdd"
        ? CanOpenFile.Xdd.WriteToString((ElectronicDataSheet)model, CanOpenWriteOptions.Validated)
        : CanOpenFile.Xdc.WriteToString((DeviceConfigurationFile)model, CanOpenWriteOptions.Validated);

    private static string? Attribute(string xml, string element, string attribute)
        => XDocument.Parse(xml).Descendants()
            .First(e => e.Name.LocalName == element && e.Attribute(attribute) != null)
            .Attribute(attribute)?.Value;

    private static List<string> Attributes(string xml, string element, string attribute)
        => XDocument.Parse(xml).Descendants()
            .Where(e => e.Name.LocalName == element)
            .Select(e => e.Attribute(attribute)?.Value ?? "<absent>")
            .ToList();

    private static string Xdd(
        string creationDate = "2026-01-01",
        string creationTime = "10:00:00",
        string? modificationDate = null)
        => Document(creationDate, creationTime, modificationDate, deviceCommissioning: null);

    private static string Xdc(
        string creationDate = "2026-01-01",
        string actualBaudRate = "250 Kbps",
        string networkNumber = "1")
        => Document(
            creationDate,
            "10:00:00",
            modificationDate: null,
            deviceCommissioning: "<deviceCommissioning nodeID=\"5\" nodeName=\"n\" actualBaudRate=\"" + actualBaudRate +
                                 "\" networkNumber=\"" + networkNumber + "\" networkName=\"net\" CANopenManager=\"false\"/>");

    private static string Document(string creationDate, string creationTime, string? modificationDate, string? deviceCommissioning)
    {
        var file = " fileName=\"t.xdd\" fileCreator=\"me\" fileCreationDate=\"" + creationDate +
                   "\" fileCreationTime=\"" + creationTime + "\"" +
                   (modificationDate == null ? string.Empty : " fileModificationDate=\"" + modificationDate + "\"") +
                   " fileVersion=\"1\"";
        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
               "<ISO15745ProfileContainer xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
               "<ISO15745Profile><ProfileBody xsi:type=\"ProfileBody_Device_CANopen\"" + file + ">" +
               "<DeviceIdentity><vendorName>T</vendorName><vendorID>0x1</vendorID><productName>T</productName><productID>0x1</productID></DeviceIdentity>" +
               "<DeviceManager/><DeviceFunction/></ProfileBody></ISO15745Profile>" +
               "<ISO15745Profile><ProfileBody xsi:type=\"ProfileBody_CommunicationNetwork_CANopen\"" + file + ">" +
               "<ApplicationLayers><CANopenObjectList mandatoryObjects=\"1\" optionalObjects=\"0\" manufacturerObjects=\"0\">" +
               "<CANopenObject index=\"1000\" name=\"Device Type\" objectType=\"7\" dataType=\"0007\" accessType=\"ro\" PDOmapping=\"no\"/>" +
               "</CANopenObjectList></ApplicationLayers>" +
               "<TransportLayers><PhysicalLayer><baudRate defaultValue=\"250 Kbps\"/></PhysicalLayer></TransportLayers>" +
               "<NetworkManagement><CANopenGeneralFeatures granularity=\"8\" nrOfRxPDO=\"0\" nrOfTxPDO=\"0\"/>" +
               (deviceCommissioning ?? string.Empty) + "</NetworkManagement>" +
               "</ProfileBody></ISO15745Profile></ISO15745ProfileContainer>";
    }
}
