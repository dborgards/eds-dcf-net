namespace EdsDcfNet.Tests.Writers;

using System.Globalization;
using System.Xml.Linq;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Tests.Infrastructure;
using EdsDcfNet.Tests.Utilities;
using EdsDcfNet.Validation;

/// <summary>
/// XDD/XDC round-trip of the device identity and features (review finding X8): typed versions,
/// order numbers, <c>Comments</c> as marked XML comments before the root element, and the
/// <c>CANopenGeneralFeatures</c> / <c>CANopenMasterFeatures</c> flags.
/// </summary>
public class XddDeviceIdentityRoundTripTests
{
    private const string CorpusXdd = "Fixtures/Corpus/canopen-node/basicDevice.xdd";

    private static readonly CanOpenFileOptions Strict = new() { StrictParsing = true };

    private static readonly DateTime Stamp = new(2027, 3, 4, 14, 30, 0, DateTimeKind.Unspecified);

    // ── Versions ─────────────────────────────────────────────────────────────

    [Fact]
    public void WriteToString_CorpusBasicDevice_KeepsAllThreeVersionsXddToXdd()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadFile(CorpusXdd);

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);
        var again = CanOpenFile.Xdd.ReadString(written, Strict);

        // Assert
        eds.DeviceInfo.Versions.Select(v => (v.Type, v.Value, v.ReadOnly)).Should().Equal(
            (DeviceVersionType.Software, "0", true),
            (DeviceVersionType.Firmware, "0", true),
            (DeviceVersionType.Hardware, "0", true));
        Versions(written).Should().Equal(("SW", "0", null), ("FW", "0", null), ("HW", "0", null));
        again.DeviceInfo.Versions.Select(v => v.Type).Should().Equal(
            DeviceVersionType.Software, DeviceVersionType.Firmware, DeviceVersionType.Hardware);
    }

    [Fact]
    public void WriteToString_VersionWithReadOnlyFalse_KeepsAttributeAndOrderValidatedRoundTrip()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Xdd(identity:
            "<version versionType=\"HW\" readOnly=\"false\">B2</version>" +
            "<version versionType=\"SW\" readOnly=\"true\"> 1.2.3 </version>" +
            "<version versionType=\"FW\">7</version>"));

        // Act
        var plain = CanOpenFile.Xdd.WriteToString(eds);
        var validated = CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);
        var again = CanOpenFile.Xdd.ReadString(plain, Strict);

        // Assert
        foreach (var xml in new[] { plain, validated })
            Versions(xml).Should().Equal(("HW", "B2", "false"), ("SW", " 1.2.3 ", null), ("FW", "7", null));

        again.DeviceInfo.Versions[0].ReadOnly.Should().BeFalse();
        again.DeviceInfo.Versions[1].ReadOnly.Should().BeTrue();
        again.DeviceInfo.Versions[1].Value.Should().Be(" 1.2.3 ");
    }

    [Fact]
    public void WriteToString_OrderNumbersAndVersions_FollowSchemaElementOrder()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Xdd(identity:
            "<orderNumber>A-1</orderNumber><version versionType=\"FW\">1</version>"));

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert — schema order: productID, orderNumber, version.
        Identity(written).Elements().Select(e => e.Name.LocalName).Should().Equal(
            "vendorName", "vendorID", "productName", "productID", "orderNumber", "version");
    }

    [Fact]
    public void WriteToString_XddRevisionNumberChangedAfterRead_VersionsStayUnchanged()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Xdd(identity: "<version versionType=\"SW\">1.0</version>"));
        eds.DeviceInfo.RevisionNumber = 99;

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        eds.DeviceInfo.RevisionNumber.Should().Be(99);
        Versions(written).Should().Equal(("SW", "1.0", null));
    }

    [Fact]
    public void WriteToString_XddVersionsListChangedAfterRead_OutputFollowsList()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Xdd(identity: "<version versionType=\"SW\">1.0</version>"));
        eds.DeviceInfo.Versions[0].Value = "2.0";
        eds.DeviceInfo.Versions[0].ReadOnly = false;
        eds.DeviceInfo.Versions.Add(new DeviceVersion { Type = DeviceVersionType.Hardware, Value = "C" });

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        Versions(written).Should().Equal(("SW", "2.0", "false"), ("HW", "C", null));
    }

    [Theory]
    [InlineData("<version versionType=\"FW\">1.2.3</version>", 0u)]
    [InlineData("<version versionType=\"FW\">12</version>", 12u)]
    [InlineData("<version versionType=\"FW\">4294967295</version>", 4294967295u)]
    [InlineData("<version versionType=\"FW\">4294967296</version>", 0u)]
    [InlineData("<version versionType=\"FW\"></version>", 0u)]
    [InlineData("<version versionType=\"FW\">+5</version>", 0u)]
    [InlineData("<version versionType=\"SW\">12</version>", 0u)]
    [InlineData("<version versionType=\"FW\">1</version><version versionType=\"FW\">2</version>", 0u)]
    [InlineData("<version versionType=\"SW\">9</version><version versionType=\"FW\">7</version>", 7u)]
    public void ReadString_XddWithVersions_DerivesRevisionNumberOnlyFromSingleNumericFirmware(string versions, uint expected)
    {
        // Act
        var eds = CanOpenFile.Xdd.ReadString(Xdd(identity: versions));

        // Assert
        eds.DeviceInfo.RevisionNumber.Should().Be(expected);
    }

    [Fact]
    public void WriteToString_EdsRevisionNumberThroughXdd_SurvivesEdsToXddToEds()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadFile("Fixtures/sample_device.eds");
        eds.DeviceInfo.RevisionNumber = 3;

        // Act
        var xdd = CanOpenFile.Xdd.WriteToString(eds);
        var back = CanOpenFile.Eds.ReadString(CanOpenFile.Eds.WriteToString(CanOpenFile.Xdd.ReadString(xdd, Strict)));

        // Assert
        back.DeviceInfo.RevisionNumber.Should().Be(3);
    }

    [Fact]
    public void WriteToString_RevisionNumberZeroWithoutVersions_WritesNoVersion()
    {
        // Arrange
        var eds = ValidXmlEds();

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        Versions(written).Should().BeEmpty();
    }

    [Fact]
    public void WriteToString_ModelWithoutVersionList_WritesRevisionNumberAsFirmwareVersion()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadFile("Fixtures/sample_device.eds");
        eds.DeviceInfo.RevisionNumber = 4294967295;

        // Act
        var xdd = CanOpenFile.Xdd.WriteToString(eds);
        var edsAgain = CanOpenFile.Eds.ReadString(CanOpenFile.Eds.WriteToString(eds));

        // Assert
        eds.DeviceInfo.Versions.Should().BeEmpty();
        Versions(xdd).Should().Equal(("FW", "4294967295", null));
        edsAgain.DeviceInfo.RevisionNumber.Should().Be(4294967295);
    }

    [Fact]
    public void ReadString_VersionWithMissingOrUnknownType_LenientIgnoresAndReports()
    {
        // Arrange
        var xml = Xdd(identity:
            "<version versionType=\"XX\">1</version><version>2</version><version versionType=\" fw \">3</version>" +
            "<version versionType=\"HW\">4</version>");

        // Act
        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml);

        // Assert
        result.Model.DeviceInfo.Versions.Select(v => v.Value).Should().Equal("4");
        result.Diagnostics.Where(d => d.Code == ParseDiagnosticCodes.XddUnknownVersionType)
            .Select(d => d.RawValue).Should().Equal("XX", null, " fw ");
    }

    [Fact]
    public void ReadString_VersionWithUnknownType_StrictThrows()
    {
        // Act
        var act = () => CanOpenFile.Xdd.ReadString(Xdd(identity: "<version versionType=\"XX\">1</version>"), Strict);

        // Assert
        act.Should().Throw<EdsParseException>().Which.Code.Should().Be(ParseDiagnosticCodes.XddUnknownVersionType);
    }

    [Fact]
    public void ReadString_VersionWithUnknownReadOnlyToken_StrictThrowsLenientTreatsAsFalse()
    {
        // Arrange
        var xml = Xdd(identity: "<version versionType=\"SW\" readOnly=\"maybe\">1</version>");

        // Act
        var lenient = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml);
        var strict = () => CanOpenFile.Xdd.ReadString(xml, Strict);

        // Assert
        lenient.Model.DeviceInfo.Versions[0].ReadOnly.Should().BeFalse();
        lenient.Diagnostics.Should().Contain(d => d.Code == ParseDiagnosticCodes.XddUnknownXmlBool);
        strict.Should().Throw<EdsParseException>();
    }

    [Fact]
    public void WriteToString_VersionTypeOutsideEnum_ThrowsXddWriteException()
    {
        // Arrange
        var eds = ValidXmlEds();
        eds.DeviceInfo.Versions.Add(new DeviceVersion { Type = (DeviceVersionType)99, Value = "1" });

        // Act
        var act = () => CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        act.Should().Throw<XddWriteException>().WithInnerException<ArgumentOutOfRangeException>();
    }

    // ── Order numbers ────────────────────────────────────────────────────────

    [Fact]
    public void WriteToString_TwoOrderNumbersOneReadOnlyFalse_SurvivesXddToXddAndSetsOrderCode()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Xdd(identity:
            "<orderNumber>ABC-1</orderNumber><orderNumber readOnly=\"false\">ABC-2</orderNumber>"));

        // Act
        var plain = CanOpenFile.Xdd.WriteToString(eds);
        var validated = CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);
        var again = CanOpenFile.Xdd.ReadString(plain, Strict);

        // Assert
        eds.DeviceInfo.OrderCode.Should().Be("ABC-1");
        foreach (var xml in new[] { plain, validated })
            OrderNumbers(xml).Should().Equal(("ABC-1", null), ("ABC-2", "false"));

        again.DeviceInfo.OrderNumbers.Select(o => (o.Value, o.ReadOnly)).Should().Equal(("ABC-1", true), ("ABC-2", false));
    }

    [Fact]
    public void WriteToString_OrderCodeChangedAfterXddRead_XddUnchangedEdsFollowsOrderCode()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Xdd(identity:
            "<orderNumber>ABC-1</orderNumber><orderNumber readOnly=\"false\">ABC-2</orderNumber>"));
        eds.DeviceInfo.OrderCode = "CHANGED";

        // Act
        var xdd = CanOpenFile.Xdd.WriteToString(eds);
        var edsText = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        OrderNumbers(xdd).Should().Equal(("ABC-1", null), ("ABC-2", "false"));
        CanOpenFile.Eds.ReadString(edsText).DeviceInfo.OrderCode.Should().Be("CHANGED");
    }

    [Fact]
    public void WriteToString_OrderNumberListChangedAfterRead_OutputFollowsList()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Xdd(identity: "<orderNumber>ABC-1</orderNumber>"));
        eds.DeviceInfo.OrderNumbers.Clear();
        eds.DeviceInfo.OrderNumbers.Add(new DeviceOrderNumber { Value = "NEW", ReadOnly = false });

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        OrderNumbers(written).Should().Equal(("NEW", "false"));
    }

    [Fact]
    public void WriteToString_EdsOrderCodeWithoutList_IsWrittenAsSingleOrderNumber()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadFile("Fixtures/sample_device.eds");
        eds.DeviceInfo.OrderCode = "EDS-ORDER";

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);
        var back = CanOpenFile.Xdd.ReadString(written);

        // Assert
        eds.DeviceInfo.OrderNumbers.Should().BeEmpty();
        OrderNumbers(written).Should().Equal(("EDS-ORDER", null));
        back.DeviceInfo.OrderCode.Should().Be("EDS-ORDER");
    }

    [Fact]
    public void WriteToString_EmptyOrderCodeWithoutList_WritesNoOrderNumber()
    {
        // Arrange
        var eds = ValidXmlEds();

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        OrderNumbers(written).Should().BeEmpty();
    }

    // ── Comments ─────────────────────────────────────────────────────────────

    [Fact]
    public void WriteToString_EdsWithCommentsIncludingDoubleHyphen_RoundTripsEdsToXddToEds()
    {
        // Arrange
        var text = File.ReadAllText("Fixtures/sample_device.eds")
            .Replace("Line2=This device is a 16x16 digital I/O module", "Line2=ready -- set --- go-");
        var eds = CanOpenFile.Eds.ReadString(text);

        // Act
        var xdd = CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);
        var back = CanOpenFile.Eds.ReadString(CanOpenFile.Eds.WriteToString(CanOpenFile.Xdd.ReadString(xdd, Strict)));

        // Assert
        eds.Comments!.CommentLines[2].Should().Be("ready -- set --- go-");
        back.Comments!.Lines.Should().Be(3);
        back.Comments.CommentLines.Should().Equal(eds.Comments.CommentLines);
        RootComments(xdd).Should().HaveCount(3).And.OnlyContain(c => !c.Contains("--", StringComparison.Ordinal) && !c.EndsWith("-", StringComparison.Ordinal));
        xdd.IndexOf("<!--", StringComparison.Ordinal).Should().BeLessThan(xdd.IndexOf("<co:ISO15745ProfileContainer", StringComparison.Ordinal));
    }

    [Fact]
    public void WriteToString_DcfWithCommentsIncludingDoubleHyphen_RoundTripsDcfToXdcToDcf()
    {
        // Arrange
        var dcf = CanOpenFile.Dcf.ReadFile("Fixtures/full_features.dcf");
        dcf.Comments!.CommentLines[2] = "--start-- and -";

        // Act
        var xdc = CanOpenFile.Xdc.WriteToString(dcf, CanOpenWriteOptions.Validated);
        var back = CanOpenFile.Dcf.ReadString(CanOpenFile.Dcf.WriteToString(CanOpenFile.Xdc.ReadString(xdc, Strict)));

        // Assert
        back.Comments!.Lines.Should().Be(2);
        back.Comments.CommentLines.Should().Equal(dcf.Comments.CommentLines);
        back.Comments.CommentLines[2].Should().Be("--start-- and -");
    }

    [Fact]
    public void ReadFile_CorpusBasicDevice_GeneratorCommentsAreNotDeviceComments()
    {
        // Act
        var eds = CanOpenFile.Xdd.ReadFile(CorpusXdd);
        var edsText = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        eds.Comments.Should().BeNull();
        edsText.Should().NotContain("[Comments]").And.NotContain("libedssharp");
    }

    [Fact]
    public void WriteToString_CommentsWithEscapesLineBreaksAndGaps_RoundTripXddToXdd()
    {
        // Arrange
        var eds = ValidXmlEds();
        eds.Comments = new Comments { Lines = 7 };
        eds.Comments.CommentLines[1] = "-a";
        eds.Comments.CommentLines[2] = "a-b";
        eds.Comments.CommentLines[3] = "back\\slash \\h \\x";
        eds.Comments.CommentLines[5] = "two\r\nlines\nand\rmore  ";
        eds.Comments.CommentLines[6] = "  leading space; <tag> & \"quote\" ünï ☃ \U0001F600";

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);
        var again = CanOpenFile.Xdd.ReadString(written, Strict);

        // Assert
        again.Comments!.Lines.Should().Be(7);
        again.Comments.CommentLines.Should().Equal(eds.Comments.CommentLines);
        RootComments(written).Should().HaveCount(6, "five carried lines plus an empty line that keeps Lines=7");
    }

    [Fact]
    public void ReadString_ForeignAndMalformedCommentsAroundRoot_AreNotComments()
    {
        // Arrange
        var xml = Xdd(prolog:
                "<!--generated by a tool-->" +
                "<!--EdsDcfNet.Comment x: text-->" +
                "<!--EdsDcfNet.Comment 3 without separator-->" +
                "<!--EdsDcfNet.Comment 0: zero-->" +
                "<!--EdsDcfNet.Comment 65536: too large-->" +
                "<!--EdsDcfNet.Comment : empty number-->" +
                "<!--EdsDcfNet.Comment 4: kept\\x and trailing\\-->",
            epilog: "<!--EdsDcfNet.Comment 9: after the root-->");

        // Act
        var eds = CanOpenFile.Xdd.ReadString(xml);

        // Assert
        eds.Comments!.Lines.Should().Be(4);
        eds.Comments.CommentLines.Should().Equal(new Dictionary<int, string> { [4] = "kept\\x and trailing\\" });
    }

    [Fact]
    public void ReadString_MarkedCommentsInDescendingOrder_KeepsHighestLineAsCount()
    {
        // Act
        var eds = CanOpenFile.Xdd.ReadString(Xdd(prolog:
            "<!--EdsDcfNet.Comment 3: third--><!--EdsDcfNet.Comment 1: first-->"));

        // Assert
        eds.Comments!.Lines.Should().Be(3);
        eds.Comments.CommentLines.Should().Equal(new Dictionary<int, string> { [3] = "third", [1] = "first" });
    }

    [Fact]
    public void ReadString_XddWithoutMarkedComments_HasNoComments()
    {
        // Act
        var eds = CanOpenFile.Xdd.ReadString(Xdd(prolog: "<!--just a note-->"));

        // Assert
        eds.Comments.Should().BeNull();
    }

    [Fact]
    public void ReadString_MarkedCommentWithEmptyTextAndHighNumber_KeepsLineCountOnly()
    {
        // Act
        var eds = CanOpenFile.Xdd.ReadString(Xdd(prolog: "<!--EdsDcfNet.Comment 65535: -->"));

        // Assert
        eds.Comments!.Lines.Should().Be(65535);
        eds.Comments.CommentLines.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Xdd")]
    [InlineData("Xdc")]
    public void WriteToString_CommentLineThatXmlCannotCarry_OmittedUnvalidatedRejectedValidated(string format)
    {
        // Arrange
        var comments = new Comments { Lines = 3 };
        comments.CommentLines[1] = "fine";
        comments.CommentLines[2] = "bell\u0007";
        comments.CommentLines[3] = "lone surrogate \uD800";
        comments.CommentLines[0] = "line zero";
        comments.CommentLines[70000] = "too large";
        comments.CommentLines[4] = "bad low \uDC00 first";
        object model = format == "Xdd" ? WithComments(ValidXmlEds(), comments) : WithComments(ValidXmlDcf(), comments);

        // Act
        var plain = Write(format, model);
        var validated = () => Write(format, model, validated: true);

        // Assert
        // The carried line plus the empty marker that keeps Lines=3.
        RootComments(plain).Should().Equal("EdsDcfNet.Comment 1: fine", "EdsDcfNet.Comment 3: ");
        var issues = validated.Should().Throw<ModelValidationException>().Which.Issues;
        issues.Select(i => i.Path).Should().BeEquivalentTo(new[]
        {
            "Comments.CommentLines[2]", "Comments.CommentLines[3]", "Comments.CommentLines[0]",
            "Comments.CommentLines[70000]", "Comments.CommentLines[4]"
        });
        issues.Should().OnlyContain(i => i.Code == ValidationIssueCodes.XddCommentLineNotRepresentable);
    }

    [Fact]
    public void WriteToString_CommentLineWithNullText_IsWrittenAsEmptyLine()
    {
        // Arrange
        var eds = ValidXmlEds();
        eds.Comments = new Comments { Lines = 1 };
        eds.Comments.CommentLines[1] = null!;

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);
        var again = CanOpenFile.Xdd.ReadString(written);

        // Assert
        RootComments(written).Should().ContainSingle();
        again.Comments!.Lines.Should().Be(1);
        again.Comments.CommentLines.Should().BeEmpty();
    }

    [Fact]
    public void WriteToString_CommentsWithoutLines_WriteNoComments()
    {
        // Arrange
        var eds = ValidXmlEds();
        eds.Comments = new Comments();

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        RootComments(written).Should().BeEmpty();
    }

    // ── CANopen features ─────────────────────────────────────────────────────

    [Fact]
    public void WriteToString_FeatureFlagsFromXdd_RoundTripXddToXddAndXdc()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Xdd(
            general: " selfStartingDevice=\"true\" SDORequestingDevice=\"1\"",
            master: " bootUpMaster=\"true\" flyingMaster=\"true\" SDOManager=\"true\" configurationManager=\"true\" layerSettingServiceMaster=\"true\""));
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, 5, Stamp, 250);

        // Act
        var xdd = CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);
        var xdc = CanOpenFile.Xdc.WriteToString(dcf);
        var again = CanOpenFile.Xdd.ReadString(xdd, Strict);

        // Assert
        var info = eds.DeviceInfo;
        new[] { info.SelfStartingDevice, info.SdoRequestingDevice, info.FlyingMaster, info.SdoManager, info.ConfigurationManager, info.LayerSettingServiceMaster }
            .Should().OnlyContain(flag => flag);
        foreach (var xml in new[] { xdd, xdc })
        {
            Attributes(xml, "CANopenGeneralFeatures").Should().Contain("selfStartingDevice", "true").And.Contain("SDORequestingDevice", "true");
            Attributes(xml, "CANopenMasterFeatures").Should()
                .Contain("flyingMaster", "true").And.Contain("SDOManager", "true")
                .And.Contain("configurationManager", "true").And.Contain("layerSettingServiceMaster", "true");
        }

        again.DeviceInfo.FlyingMaster.Should().BeTrue();
        again.DeviceInfo.SelfStartingDevice.Should().BeTrue();
    }

    [Fact]
    public void WriteToString_FeatureFlagsAbsentOrFalse_AreNotWrittenAndReadBackFalse()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Xdd(
            general: " selfStartingDevice=\"false\" SDORequestingDevice=\"0\"",
            master: " flyingMaster=\"false\""));

        // Act
        var written = CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        Attributes(written, "CANopenGeneralFeatures").Keys.Should().NotContain(new[] { "selfStartingDevice", "SDORequestingDevice" });
        Attributes(written, "CANopenMasterFeatures").Keys.Should()
            .NotContain(new[] { "flyingMaster", "SDOManager", "configurationManager", "layerSettingServiceMaster" });
        eds.DeviceInfo.FlyingMaster.Should().BeFalse();
    }

    [Fact]
    public void ReadString_FeatureFlagWithUnknownToken_StrictThrows()
    {
        // Act
        var act = () => CanOpenFile.Xdd.ReadString(Xdd(master: " SDOManager=\"yes\""), Strict);

        // Assert
        act.Should().Throw<EdsParseException>().Which.Code.Should().Be(ParseDiagnosticCodes.XddUnknownXmlBool);
    }

    [Fact]
    public void WriteToString_FeatureFlagsSetOnEds_DoNotReachEdsOrDcf()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadFile("Fixtures/sample_device.eds");
        eds.DeviceInfo.FlyingMaster = true;
        eds.DeviceInfo.SelfStartingDevice = true;

        // Act
        var edsText = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        edsText.Should().NotContainEquivalentOf("flyingMaster").And.NotContainEquivalentOf("SelfStarting");
    }

    // ── ModelCloner / ConvertToDcf (rule 17) ─────────────────────────────────

    [Fact]
    public void ConvertToDcf_XddWithIdentityListsFlagsAndComments_CarriesThemIntoXdcOutput()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadString(Xdd(
            prolog: "<!--EdsDcfNet.Comment 1: from the \\h\\h file-->",
            identity: "<orderNumber>ABC-1</orderNumber><orderNumber readOnly=\"false\">ABC-2</orderNumber>" +
                      "<version versionType=\"SW\" readOnly=\"false\">1.5</version><version versionType=\"HW\">B</version>",
            general: " selfStartingDevice=\"true\" SDORequestingDevice=\"true\"",
            master: " flyingMaster=\"true\" SDOManager=\"true\" configurationManager=\"true\" layerSettingServiceMaster=\"true\""));

        // Act
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, 5, Stamp, 250);
        eds.DeviceInfo.Versions[0].Value = "mutated";
        eds.DeviceInfo.OrderNumbers[0].Value = "mutated";
        var xdc = CanOpenFile.Xdc.WriteToString(dcf, CanOpenWriteOptions.Validated);

        // Assert
        dcf.DeviceInfo.Versions.Select(v => (v.Type, v.Value, v.ReadOnly)).Should().Equal(
            (DeviceVersionType.Software, "1.5", false), (DeviceVersionType.Hardware, "B", true));
        dcf.DeviceInfo.OrderNumbers.Select(o => (o.Value, o.ReadOnly)).Should().Equal(("ABC-1", true), ("ABC-2", false));
        Versions(xdc).Should().Equal(("SW", "1.5", "false"), ("HW", "B", null));
        OrderNumbers(xdc).Should().Equal(("ABC-1", null), ("ABC-2", "false"));
        Attributes(xdc, "CANopenGeneralFeatures").Should().Contain("selfStartingDevice", "true");
        Attributes(xdc, "CANopenMasterFeatures").Should().Contain("layerSettingServiceMaster", "true");
        RootComments(xdc).Should().ContainSingle().Which.Should().Contain("from the \\h\\h file");
        CanOpenFile.Xdc.ReadString(xdc).Comments!.CommentLines[1].Should().Be("from the -- file");
    }

    // ── Schema ───────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WriterOutputWithListsFlagsAndComments_ReportsNoIdentityOrFeatureProblem()
    {
        // Arrange — the document as a whole is still a known gap (WP-43 DeviceFunction); the
        // identity, the features and the comments before the root are pinned here.
        var eds = CanOpenFile.Xdd.ReadString(Xdd(
            prolog: "<!--EdsDcfNet.Comment 1: note-->",
            identity: "<orderNumber>A</orderNumber><orderNumber readOnly=\"false\">B</orderNumber>" +
                      "<version versionType=\"SW\" readOnly=\"false\">1</version><version versionType=\"FW\">2</version><version versionType=\"HW\">3</version>",
            general: " selfStartingDevice=\"true\" SDORequestingDevice=\"true\"",
            master: " flyingMaster=\"true\" SDOManager=\"true\" configurationManager=\"true\" layerSettingServiceMaster=\"true\""));
        var xml = CanOpenFile.Xdd.WriteToString(eds);

        // Act
        var problems = Cia311Schema.Validate(xml);
        var probe = Cia311Schema.Validate(xml.Replace("versionType=\"SW\"", "versionType=\"ZZ\""));

        // Assert
        problems.Should().NotContain(p => p.Contains("version", StringComparison.OrdinalIgnoreCase)
                                          || p.Contains("orderNumber", StringComparison.Ordinal)
                                          || p.Contains("DeviceIdentity", StringComparison.Ordinal)
                                          || p.Contains("Features", StringComparison.Ordinal)
                                          || p.Contains("selfStartingDevice", StringComparison.Ordinal)
                                          || p.Contains("flyingMaster", StringComparison.Ordinal));
        probe.Should().Contain(p => p.Contains("ZZ", StringComparison.Ordinal));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static ElectronicDataSheet ValidXmlEds() => ValidCanOpenModelBuilder.CreateValidEds();

    private static DeviceConfigurationFile ValidXmlDcf() => ValidCanOpenModelBuilder.CreateValidDcf();

    private static ElectronicDataSheet WithComments(ElectronicDataSheet eds, Comments comments)
    {
        eds.Comments = comments;
        return eds;
    }

    private static DeviceConfigurationFile WithComments(DeviceConfigurationFile dcf, Comments comments)
    {
        dcf.Comments = comments;
        return dcf;
    }

    private static string Write(string format, object model, bool validated = false)
    {
        var options = validated ? CanOpenWriteOptions.Validated : null;
        return format == "Xdd"
            ? CanOpenFile.Xdd.WriteToString((ElectronicDataSheet)model, options)
            : CanOpenFile.Xdc.WriteToString((DeviceConfigurationFile)model, options);
    }

    private static XElement Identity(string xml)
        => XDocument.Parse(xml).Descendants().Single(e => e.Name.LocalName == "DeviceIdentity");

    private static List<(string Type, string Value, string? ReadOnly)> Versions(string xml)
        => Identity(xml).Elements().Where(e => e.Name.LocalName == "version")
            .Select(e => (e.Attribute("versionType")!.Value, e.Value, e.Attribute("readOnly")?.Value))
            .ToList();

    private static List<(string Value, string? ReadOnly)> OrderNumbers(string xml)
        => Identity(xml).Elements().Where(e => e.Name.LocalName == "orderNumber")
            .Select(e => (e.Value, e.Attribute("readOnly")?.Value))
            .ToList();

    private static Dictionary<string, string> Attributes(string xml, string elementName)
        => XDocument.Parse(xml).Descendants().Single(e => e.Name.LocalName == elementName)
            .Attributes().ToDictionary(a => a.Name.LocalName, a => a.Value);

    private static List<string> RootComments(string xml)
        => XDocument.Parse(xml).Nodes().OfType<XComment>().Select(c => c.Value).ToList();

    private static string Xdd(
        string identity = "",
        string general = "",
        string master = " bootUpMaster=\"false\"",
        string prolog = "",
        string epilog = "")
    {
        var file = " fileName=\"t.xdd\" fileCreator=\"me\" fileCreationDate=\"2026-01-01\" fileVersion=\"1\"";
        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + prolog +
               "<ISO15745ProfileContainer xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
               "<ISO15745Profile><ProfileBody xsi:type=\"ProfileBody_Device_CANopen\"" + file + ">" +
               "<DeviceIdentity><vendorName>T</vendorName><vendorID>0x1</vendorID><productName>T</productName><productID>0x1</productID>" +
               identity + "</DeviceIdentity>" +
               "<DeviceManager/><DeviceFunction/></ProfileBody></ISO15745Profile>" +
               "<ISO15745Profile><ProfileBody xsi:type=\"ProfileBody_CommunicationNetwork_CANopen\"" + file + ">" +
               "<ApplicationLayers><CANopenObjectList mandatoryObjects=\"3\" optionalObjects=\"0\" manufacturerObjects=\"0\">" +
               "<CANopenObject index=\"1000\" name=\"Device Type\" objectType=\"7\" dataType=\"0007\" accessType=\"ro\" PDOmapping=\"no\"/>" +
               "<CANopenObject index=\"1001\" name=\"Error register\" objectType=\"7\" dataType=\"0005\" accessType=\"ro\" PDOmapping=\"no\"/>" +
               "<CANopenObject index=\"1018\" name=\"Identity\" objectType=\"9\" subNumber=\"1\">" +
               "<CANopenSubObject subIndex=\"00\" name=\"Highest sub-index\" objectType=\"7\" dataType=\"0005\" accessType=\"ro\" PDOmapping=\"no\"/>" +
               "</CANopenObject>" +
               "</CANopenObjectList></ApplicationLayers>" +
               "<TransportLayers><PhysicalLayer><baudRate defaultValue=\"250 Kbps\"><supportedBaudRate value=\"250 Kbps\"/></baudRate></PhysicalLayer></TransportLayers>" +
               "<NetworkManagement><CANopenGeneralFeatures granularity=\"8\" nrOfRxPDO=\"0\" nrOfTxPDO=\"0\"" + general + "/>" +
               "<CANopenMasterFeatures" + master + "/></NetworkManagement>" +
               "</ProfileBody></ISO15745Profile></ISO15745ProfileContainer>" + epilog;
    }
}
