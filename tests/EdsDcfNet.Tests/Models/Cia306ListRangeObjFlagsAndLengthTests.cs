namespace EdsDcfNet.Tests.Models;

using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Tests.Utilities;
using EdsDcfNet.Validation;

/// <summary>
/// CiA 306-1 v1.4.0 rules of WP-15 (#581): S9a character set and line length (§ 6.2), S10 object
/// list ranges and counts (§ 6.6.3.1, Table 4 and 5), S13 reserved <c>ObjFlags</c> bits
/// (§ 6.6.3.3, Table 8) and S14 length limits (Table 9 <c>Line&lt;n&gt;</c> 249, Table 10
/// <c>UploadFile</c> 244 / <c>DownloadFile</c> 242, Table 11 <c>ParamRefd</c> 249, Table 15
/// <c>[MxComments]</c> <c>Line&lt;n&gt;</c> 248).
/// </summary>
public class Cia306ListRangeObjFlagsAndLengthTests
{
    private const string EdsHeader =
        "[FileInfo]\n" +
        "FileName=list.eds\n" +
        "EDSVersion=4.0\n" +
        "[DeviceInfo]\n" +
        "VendorName=Test\n";

    private static CanOpenValidationOptions Only(
        bool iso646 = false,
        bool lineLength = false,
        bool listRanges = false) => new()
        {
            RequireIso646 = iso646,
            CheckLineLength = lineLength,
            CheckObjectListRanges = listRanges,
        };

    private static CanOpenObject Var(ushort index) => new()
    {
        Index = index,
        ParameterName = "Value",
        ObjectType = CanOpenObjectType.Var,
        DataType = CanOpenDataType.Unsigned8,
        AccessType = AccessType.ReadWrite,
    };

    private static ElectronicDataSheet EdsWithList(string list, ushort index)
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        var dictionary = eds.ObjectDictionary;
        dictionary.MandatoryObjects.Clear(); // keeps 0x1000 free for the range boundary
        dictionary.Objects.Clear();
        (list == "Optional" ? dictionary.OptionalObjects : dictionary.ManufacturerObjects).Add(index);
        dictionary.Objects[index] = Var(index);
        return eds;
    }

    // ---------------------------------------------------------------- S9a: ISO 646

    [Theory]
    [InlineData('\u007F', false)] // last ISO 646 code point
    [InlineData('\u0080', true)] // first code point outside 7 bits
    [InlineData('ä', true)]
    public void Validate_Iso646ParameterNameAtMaxValue_ReportsOnlyNonAscii(char character, bool reported)
    {
        // CiA 306-1 § 6.2: "The EDS file shall contain characters encoded according to /ISO646/."
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.ObjectDictionary.Objects[0x1000].ParameterName = "Type" + character;

        var issues = CanOpenModelValidator.Validate(eds, Only(iso646: true));

        if (reported)
        {
            issues.Should().ContainSingle(i => i.Code == ValidationIssueCodes.Iso646CharacterNotAllowed)
                .Which.Message.Should().Contain("1000");
        }
        else
        {
            issues.Should().BeEmpty();
        }
    }

    [Fact]
    public void Validate_NonAsciiText_DefaultOptionsKeepUtf8Deviation()
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.ObjectDictionary.Objects[0x1000].ParameterName = "Gerät";

        CanOpenModelValidator.Validate(eds).Should().BeEmpty();
        CanOpenModelValidator.Validate(eds, Only(lineLength: true, listRanges: true)).Should().BeEmpty();
    }

    [Fact]
    public void Validate_Iso646DcfNonAscii_ReportsIssueAndNamesLine()
    {
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        dcf.DeviceCommissioning.NodeName = "Knotenü";

        var issues = CanOpenModelValidator.Validate(dcf, Only(iso646: true));

        var issue = issues.Should().ContainSingle(i => i.Code == ValidationIssueCodes.Iso646CharacterNotAllowed).Subject;
        issue.Path.Should().StartWith("Line[");
        issue.Message.Should().Contain("U+00FC").And.Contain("DeviceComissioning");
    }

    [Fact]
    public void Validate_Iso646AllAscii_ReturnsNoIssues()
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();

        CanOpenModelValidator.Validate(eds, Only(iso646: true, lineLength: true)).Should().BeEmpty();
    }

    // ---------------------------------------------------------------- S9a: line length

    [Theory]
    [InlineData(243, false)] // "Description=" is 12 characters: 255 in total
    [InlineData(244, true)] // 256 in total
    public void Validate_LineLengthDescriptionAtMaxValue_ReportsOverLongLine(int length, bool reported)
    {
        // CiA 306-1 § 6.2: "The total line length shall not exceed 255 d characters."
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.FileInfo.Description = new string('a', length);

        var issues = CanOpenModelValidator.Validate(eds, Only(lineLength: true));

        if (reported)
        {
            issues.Should().ContainSingle(i => i.Code == ValidationIssueCodes.LineTooLong)
                .Which.Message.Should().Contain("256").And.Contain("FileInfo");
        }
        else
        {
            issues.Should().BeEmpty();
        }
    }

    [Fact]
    public void Validate_LineLengthOverLongLine_DefaultOptionsDoNotReport()
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.FileInfo.Description = new string('a', 400);

        CanOpenModelValidator.Validate(eds).Should().BeEmpty();
    }

    [Fact]
    public void Validate_PlainTextRulesOnModelTheWriterRejects_ReportOtherIssuesWithoutThrowing()
    {
        // The writer throws for Node-ID 200, so there is no file text to check.
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        dcf.DeviceCommissioning.NodeId = 200;
        dcf.FileInfo.Description = "G\u00E4rt " + new string('a', 300);

        var issues = CanOpenModelValidator.Validate(dcf, Only(iso646: true, lineLength: true));

        issues.Should().ContainSingle(i => i.Path == "DeviceCommissioning.NodeId");
    }

    [Fact]
    public void Validate_StrictOptions_EnableTheNewRuleSets()
    {
        CanOpenValidationOptions.Strict.RequireIso646.Should().BeTrue();
        CanOpenValidationOptions.Strict.CheckLineLength.Should().BeTrue();
        CanOpenValidationOptions.Strict.CheckObjectListRanges.Should().BeTrue();
        CanOpenValidationOptions.Default.RequireIso646.Should().BeFalse();
        CanOpenValidationOptions.Default.CheckLineLength.Should().BeFalse();
        CanOpenValidationOptions.Default.CheckObjectListRanges.Should().BeFalse();
    }

    // ---------------------------------------------------------------- S10: list ranges

    [Theory]
    [InlineData("Optional", 0x0FFF, true)]
    [InlineData("Optional", 0x1000, false)]
    [InlineData("Optional", 0x1FFF, false)]
    [InlineData("Optional", 0x2000, true)]
    [InlineData("Optional", 0x5FFF, true)]
    [InlineData("Optional", 0x6000, false)]
    [InlineData("Optional", 0x9FFF, false)]
    [InlineData("Optional", 0xA000, true)]
    [InlineData("Manufacturer", 0x1FFF, true)]
    [InlineData("Manufacturer", 0x2000, false)]
    [InlineData("Manufacturer", 0x5FFF, false)]
    [InlineData("Manufacturer", 0x6000, true)]
    public void Validate_ObjectListRangeAtMaxValue_ReportsIndexInWrongList(string list, int index, bool reported)
    {
        // CiA 306-1 Table 4: [OptionalObjects] 1000h-1FFFh and 6000h-9FFFh,
        // [ManufacturerObjects] 2000h-5FFFh.
        var eds = EdsWithList(list, (ushort)index);

        var issues = CanOpenModelValidator.Validate(eds, Only(listRanges: true));

        if (reported)
        {
            issues.Should().ContainSingle(i => i.Code == ValidationIssueCodes.ObjectListIndexOutOfRange)
                .Which.Path.Should().Be("ObjectDictionary." + list + "Objects");
        }
        else
        {
            issues.Should().BeEmpty();
        }
    }

    [Fact]
    public void Validate_ObjectInWrongList_DefaultOptionsDoNotReport()
    {
        var eds = EdsWithList("Optional", 0x2000);

        CanOpenModelValidator.Validate(eds).Should().BeEmpty();
    }

    // ---------------------------------------------------------------- SubNumber alignment

    [Fact]
    public void Validate_SubNumberZeroWithOnlySubIndexZero_CountRuleReportsIssue()
    {
        // WP-11 counts sub-index 00h in SubNumber, so a lone 00h means SubNumber=1.
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        var obj = eds.ObjectDictionary.Objects[0x1000];
        obj.ObjectType = CanOpenObjectType.Array;
        obj.SubNumber = 0;
        obj.SubObjects[0] = new CanOpenSubObject
        {
            SubIndex = 0,
            ParameterName = "Count",
            DataType = CanOpenDataType.Unsigned8,
        };

        var strict = CanOpenModelValidator.Validate(eds, new CanOpenValidationOptions { CheckSubNumberCount = true });
        var lenient = CanOpenModelValidator.Validate(eds);

        strict.Should().ContainSingle(i => i.Path == "ObjectDictionary.Objects[0x1000].SubNumber")
            .Which.Message.Should().Contain("SubNumber is 0 but 1 sub-objects");
        lenient.Should().BeEmpty();
    }

    // ---------------------------------------------------------------- S13: ObjFlags (write)

    [Theory]
    [InlineData(0x0u, false)]
    [InlineData(0x3u, false)] // bits 0 and 1 are defined
    [InlineData(0x4u, true)] // bit 2: defined by CiA 311 only
    [InlineData(0x80000000u, true)] // bit 31
    [InlineData(0xFFFFFFFFu, true)]
    public void WriteToString_EdsObjFlagsReservedBitsAtMaxValue_ValidatedRejectsUnvalidatedWrites(uint flags, bool rejected)
    {
        // CiA 306-1 Table 8: bit 0 and 1 defined, "Reserved; always 0" for bit 2..31.
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.ObjectDictionary.Objects[0x1000].ObjFlags = flags;

        var validated = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);
        var unvalidated = CanOpenFile.Eds.WriteToString(eds);

        if (rejected)
        {
            validated.Should().Throw<ModelValidationException>().Which.Issues.Should().ContainSingle(i =>
                i.Path == "ObjectDictionary.Objects[0x1000].ObjFlags" &&
                i.Code == ValidationIssueCodes.IniObjFlagsReservedBits);
        }
        else
        {
            validated.Should().NotThrow();
        }

        unvalidated.Should().Contain(flags == 0 ? "[1000]" : "ObjFlags=");
    }

    [Fact]
    public void WriteToString_DcfObjFlagsBit2_ValidatedRejects()
    {
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        dcf.ObjectDictionary.Objects[0x1000].ObjFlags = 0x4;

        var act = () => CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);

        act.Should().Throw<ModelValidationException>().Which.Issues.Should().Contain(i =>
            i.Code == ValidationIssueCodes.IniObjFlagsReservedBits);
    }

    [Fact]
    public void WriteToString_XddObjFlagsBit2_ValidatedXddAllowsAndValidatedEdsRejectsAfterRead()
    {
        // CiA 311 defines bit 2; CiA 306-1 reserves it. The rule belongs to the INI formats only.
        var source = ValidCanOpenModelBuilder.CreateValidEds();
        source.ObjectDictionary.Objects[0x1000].ObjFlags = 0x4;
        var xdd = CanOpenFile.Xdd.WriteToString(source, CanOpenWriteOptions.Validated);

        var fromXdd = CanOpenFile.Xdd.ReadString(xdd);
        var act = () => CanOpenFile.Eds.WriteToString(fromXdd, CanOpenWriteOptions.Validated);

        fromXdd.ObjectDictionary.Objects[0x1000].ObjFlags.Should().Be(0x4u);
        act.Should().Throw<ModelValidationException>().Which.Issues.Should().Contain(i =>
            i.Code == ValidationIssueCodes.IniObjFlagsReservedBits);
        var again = () => CanOpenFile.Xdd.WriteToString(fromXdd, CanOpenWriteOptions.Validated);
        again.Should().NotThrow();
    }

    [Fact]
    public void WriteToString_ModuleFixedObjectAndSubExtensionObjFlagsBit2_ValidatedRejects()
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        var module = new ModuleInfo { ModuleNumber = 1, ProductName = "M", OrderCode = "O" };
        module.FixedObjects.Add(0x6000);
        var fixedObject = Var(0x6000);
        fixedObject.ObjFlags = 0x4;
        module.FixedObjectDefinitions[0x6000] = fixedObject;
        module.SubExtends.Add(0x6100);
        module.SubExtensionDefinitions[0x6100] = new ModuleSubExtension
        {
            ParameterName = "Ext",
            ObjFlags = 0x8,
            Count = "1",
        };
        eds.SupportedModules.Add(module);

        var act = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        var issues = act.Should().Throw<ModelValidationException>().Which.Issues;
        issues.Should().Contain(i =>
            i.Path == "SupportedModules[0].FixedObjectDefinitions[0x6000].ObjFlags" &&
            i.Code == ValidationIssueCodes.IniObjFlagsReservedBits);
        issues.Should().Contain(i =>
            i.Path == "SupportedModules[0].SubExtensionDefinitions[0x6100].ObjFlags" &&
            i.Code == ValidationIssueCodes.IniObjFlagsReservedBits);
    }

    // ---------------------------------------------------------------- S13: ObjFlags (read)

    private static string ObjectEds(string objectBody) =>
        EdsHeader +
        "[MandatoryObjects]\nSupportedObjects=1\n1=0x1000\n" +
        "[1000]\nParameterName=Device Type\nObjectType=0x7\nDataType=0x7\nAccessType=ro\n" + objectBody;

    [Theory]
    [InlineData("0", 0u, false)]
    [InlineData("3", 3u, false)]
    [InlineData("4", 4u, true)]
    [InlineData("0x80000000", 0x80000000u, true)]
    public void ReadStringWithDiagnostics_EdsObjFlagsReservedBitsAtMaxValue_ReportsDiagnosticAndKeepsValue(
        string raw,
        uint expected,
        bool reported)
    {
        var content = ObjectEds("ObjFlags=" + raw + "\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.Model.ObjectDictionary.Objects[0x1000].ObjFlags.Should().Be(expected);
        var diagnostics = result.Diagnostics.Where(d => d.Code == ParseDiagnosticCodes.IniObjFlagsReservedBits).ToList();
        if (reported)
        {
            var diagnostic = diagnostics.Should().ContainSingle().Subject;
            diagnostic.Severity.Should().Be(ParseSeverity.Warning);
            diagnostic.Path.Should().Be("1000.ObjFlags");
            diagnostic.RawValue.Should().Be(raw);
            diagnostic.Line.Should().Be(Array.IndexOf(content.Split('\n'), "ObjFlags=" + raw) + 1);
        }
        else
        {
            diagnostics.Should().BeEmpty();
        }
    }

    [Fact]
    public void ReadStringWithDiagnostics_EdsObjFlagsReservedBits_StrictParsingStillReadsAndReports()
    {
        var content = ObjectEds("ObjFlags=4\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content, new CanOpenFileOptions { StrictParsing = true });

        result.Model.ObjectDictionary.Objects[0x1000].ObjFlags.Should().Be(4u);
        result.Diagnostics.Should().Contain(d => d.Code == ParseDiagnosticCodes.IniObjFlagsReservedBits);
    }

    [Fact]
    public void ReadStringWithDiagnostics_DcfObjFlagsReservedBits_ReportsDiagnostic()
    {
        var content = ObjectEds("ObjFlags=4\n") +
            "[DeviceComissioning]\nNodeID=5\nBaudrate=500\n";

        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniObjFlagsReservedBits);
    }

    [Fact]
    public void ReadStringWithDiagnostics_ModuleObjFlagsReservedBits_ReportsFixedObjectAndSubExtension()
    {
        var content = ObjectEds("") +
            "[SupportedModules]\nNrOfEntries=1\n" +
            "[M1ModuleInfo]\nProductName=M\nProductVersion=1\nProductRevision=0\nOrderCode=O\n" +
            "[M1FixedObjects]\nNrOfEntries=1\n1=0x6000\n" +
            "[M1Fixed6000]\nParameterName=Fixed\nObjectType=0x7\nDataType=0x5\nAccessType=ro\nObjFlags=4\n" +
            "[M1SubExtends]\nNrOfEntries=1\n1=0x6100\n" +
            "[M1SubExt6100]\nParameterName=Ext\nDataType=0x5\nAccessType=ro\nObjFlags=8\nCount=1\n";

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.Diagnostics.Where(d => d.Code == ParseDiagnosticCodes.IniObjFlagsReservedBits)
            .Select(d => d.Path)
            .Should().BeEquivalentTo("M1Fixed6000.ObjFlags", "M1SubExt6100.ObjFlags");
    }

    // ---------------------------------------------------------------- S10: counts (read)

    [Fact]
    public void ReadStringWithDiagnostics_EntryAboveSupportedObjects_ReportsDiagnosticAndKeepsEntry()
    {
        var content = EdsHeader +
            "[MandatoryObjects]\nSupportedObjects=1\n1=0x1000\n2=0x1001\n" +
            "[1000]\nParameterName=Device Type\nObjectType=0x7\nDataType=0x7\nAccessType=ro\n" +
            "[1001]\nParameterName=Error Register\nObjectType=0x7\nDataType=0x5\nAccessType=ro\n";

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should()
            .ContainSingle(d => d.Code == ParseDiagnosticCodes.IniObjectListCountMismatch).Subject;
        diagnostic.Severity.Should().Be(ParseSeverity.Warning);
        diagnostic.Path.Should().Be("MandatoryObjects.SupportedObjects");
        diagnostic.RawValue.Should().Be("1");
        diagnostic.Message.Should().Contain("above").And.Contain("2");
        result.Model.ObjectDictionary.MandatoryObjects.Should().Equal((ushort)0x1000);
        result.Model.SectionRemainingEntries["MandatoryObjects"].Should()
            .Equal(new Dictionary<string, string> { ["2"] = "0x1001" });
    }

    [Fact]
    public void ReadStringWithDiagnostics_MissingEntryBelowSupportedObjects_ReportsDiagnostic()
    {
        var content = EdsHeader +
            "[ManufacturerObjects]\nSupportedObjects=3\n1=0x2000\n3=0x2002\n" +
            "[2000]\nParameterName=A\nObjectType=0x7\nDataType=0x5\nAccessType=ro\n" +
            "[2002]\nParameterName=C\nObjectType=0x7\nDataType=0x5\nAccessType=ro\n";

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should()
            .ContainSingle(d => d.Code == ParseDiagnosticCodes.IniObjectListCountMismatch).Subject;
        diagnostic.Path.Should().Be("ManufacturerObjects.SupportedObjects");
        diagnostic.Message.Should().Contain("2");
        result.Model.ObjectDictionary.ManufacturerObjects.Should().Equal((ushort)0x2000, (ushort)0x2002);
    }

    [Fact]
    public void ReadStringWithDiagnostics_SupportedObjectsMatchesEntries_ReportsNothing()
    {
        var content = ObjectEds("");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().NotContain(d => d.Code == ParseDiagnosticCodes.IniObjectListCountMismatch);
    }

    [Fact]
    public void ReadStringWithDiagnostics_SupportedObjectsMismatch_StrictParsingDoesNotThrow()
    {
        var content = EdsHeader +
            "[MandatoryObjects]\nSupportedObjects=1\n1=0x1000\n2=0x1001\n" +
            "[1000]\nParameterName=Device Type\nObjectType=0x7\nDataType=0x7\nAccessType=ro\n";

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content, new CanOpenFileOptions { StrictParsing = true });

        result.Diagnostics.Should().Contain(d => d.Code == ParseDiagnosticCodes.IniObjectListCountMismatch);
    }

    // ---------------------------------------------------------------- S14: lengths (INI write)

    private static ElectronicDataSheet EdsWithComment(int length)
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.Comments = new Comments { Lines = 1 };
        eds.Comments.CommentLines[1] = new string('c', length);
        return eds;
    }

    [Theory]
    [InlineData(249, false)]
    [InlineData(250, true)]
    public void WriteToString_CommentLineAtMaxValue_ValidatedRejectsOverLongLine(int length, bool rejected)
    {
        // CiA 306-1 Table 9: Line<n> "maximum 249 d characters".
        var eds = EdsWithComment(length);
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        dcf.Comments = eds.Comments;

        var edsAct = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);
        var dcfAct = () => CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);

        AssertLengthRule(edsAct, rejected, "Comments.CommentLines[1]");
        AssertLengthRule(dcfAct, rejected, "Comments.CommentLines[1]");
        CanOpenFile.Eds.WriteToString(eds).Should().Contain("Line1=");
    }

    [Theory]
    [InlineData(248, false)]
    [InlineData(249, true)]
    public void WriteToString_ModuleCommentLineAtMaxValue_ValidatedRejectsOverLongLine(int length, bool rejected)
    {
        // CiA 306-1 Table 15: [MxComments] Line<n> "max. 248 d characters".
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        var module = new ModuleInfo { ModuleNumber = 1, ProductName = "M", OrderCode = "O", Comments = new Comments { Lines = 1 } };
        module.Comments.CommentLines[1] = new string('c', length);
        eds.SupportedModules.Add(module);

        var act = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        AssertLengthRule(act, rejected, "SupportedModules[0].Comments.CommentLines[1]");
    }

    [Theory]
    [InlineData(249, false)]
    [InlineData(250, true)]
    public void WriteToString_DcfParamRefdAtMaxValue_ValidatedRejectsOverLongValue(int length, bool rejected)
    {
        // CiA 306-1 Table 11: ParamRefd "maximum 249 d characters".
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        var obj = dcf.ObjectDictionary.Objects[0x1000];
        obj.ParamRefd = new string('p', length);
        var withSub = ValidCanOpenModelBuilder.CreateValidDcf();
        var recordObject = withSub.ObjectDictionary.Objects[0x1000];
        recordObject.ObjectType = CanOpenObjectType.Record;
        recordObject.SubNumber = 1;
        recordObject.SubObjects[1] = new CanOpenSubObject
        {
            SubIndex = 1,
            ParameterName = "Sub",
            DataType = CanOpenDataType.Unsigned8,
            ParamRefd = new string('p', length),
        };

        var objectAct = () => CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);
        var subAct = () => CanOpenFile.Dcf.WriteToString(withSub, CanOpenWriteOptions.Validated);

        AssertLengthRule(objectAct, rejected, "ObjectDictionary.Objects[0x1000].ParamRefd");
        AssertLengthRule(subAct, rejected, "ObjectDictionary.Objects[0x1000].SubObjects[0x01].ParamRefd");
    }

    [Theory]
    [InlineData(244, false)]
    [InlineData(245, true)]
    public void WriteToString_DcfUploadFileAtMaxValue_ValidatedRejectsOverLongValue(int length, bool rejected)
    {
        // CiA 306-1 Table 10: UploadFile "maximum 244 d characters".
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        var obj = dcf.ObjectDictionary.Objects[0x1000];
        obj.ObjectType = CanOpenObjectType.Domain;
        obj.UploadFile = new string('u', length);

        var act = () => CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);

        AssertLengthRule(act, rejected, "ObjectDictionary.Objects[0x1000].UploadFile");
    }

    [Theory]
    [InlineData(242, false)]
    [InlineData(243, true)]
    public void WriteToString_DcfDownloadFileAtMaxValue_ValidatedRejectsOverLongValue(int length, bool rejected)
    {
        // CiA 306-1 Table 10: DownloadFile "maximum 242 d characters".
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        var obj = dcf.ObjectDictionary.Objects[0x1000];
        obj.ObjectType = CanOpenObjectType.Domain;
        obj.DownloadFile = new string('d', length);

        var act = () => CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);

        AssertLengthRule(act, rejected, "ObjectDictionary.Objects[0x1000].DownloadFile");
    }

    [Fact]
    public void WriteToString_OverLongDcfOnlyFieldsAndComments_EdsAndXmlFormatsAreNotAffected()
    {
        // Rule 14: the limits apply to what the INI writers emit. EDS does not write the DCF
        // fields; XDD/XDC carry comments as XML comments without the INI line limit.
        var eds = EdsWithComment(300);
        var obj = eds.ObjectDictionary.Objects[0x1000];
        obj.ParamRefd = new string('p', 300);
        obj.UploadFile = new string('u', 300);
        obj.DownloadFile = new string('d', 300);
        ValidCanOpenModelBuilder.FillXmlFileInfo(eds.FileInfo);

        var edsAct = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);
        var xddAct = () => CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);

        edsAct.Should().Throw<ModelValidationException>().Which.Issues.Should()
            .OnlyContain(i => i.Path == "Comments.CommentLines[1]" && i.Code == ValidationIssueCodes.IniValueTooLong);
        xddAct.Should().NotThrow();
    }

    private static void AssertLengthRule(Func<string> act, bool rejected, string path)
    {
        if (rejected)
        {
            act.Should().Throw<ModelValidationException>().Which.Issues.Should().ContainSingle(i =>
                i.Path == path && i.Code == ValidationIssueCodes.IniValueTooLong);
        }
        else
        {
            act.Should().NotThrow();
        }
    }

    // ---------------------------------------------------------------- S14: Comments.Lines (writer)

    [Theory]
    [InlineData(0, 2, "Lines=2")] // Lines not maintained after adding comment lines
    [InlineData(5, 2, "Lines=2")] // Lines left behind after removing comment lines
    [InlineData(2, 2, "Lines=2")]
    public void WriteToString_CommentsLinesDisagreeWithCommentLines_WritesCommentLineCount(
        ushort lines,
        int entries,
        string expected)
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.Comments = new Comments { Lines = lines };
        for (var n = 1; n <= entries; n++)
            eds.Comments.CommentLines[n] = "line " + n;

        var written = CanOpenFile.Eds.WriteToString(eds);
        var reread = CanOpenFile.Eds.ReadString(written);

        written.Should().Contain(expected);
        reread.Comments!.CommentLines.Should().Equal(eds.Comments.CommentLines);
    }

    [Fact]
    public void WriteToString_SparseCommentLineNumbers_KeepsEveryLineReadable()
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.Comments = new Comments { Lines = 0 };
        eds.Comments.CommentLines[1] = "first";
        eds.Comments.CommentLines[3] = "third";

        var reread = CanOpenFile.Eds.ReadString(CanOpenFile.Eds.WriteToString(eds));

        reread.Comments!.CommentLines.Should().Equal(eds.Comments.CommentLines);
    }

    [Fact]
    public void WriteToString_ModuleCommentsLinesDisagree_WritesCommentLineCount()
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        var module = new ModuleInfo { ModuleNumber = 1, ProductName = "M", OrderCode = "O", Comments = new Comments { Lines = 0 } };
        module.Comments.CommentLines[1] = "one";
        module.Comments.CommentLines[2] = "two";
        eds.SupportedModules.Add(module);

        var written = CanOpenFile.Eds.WriteToString(eds);
        var reread = CanOpenFile.Eds.ReadString(written);

        written.Should().Contain("[M1Comments]").And.Contain("Lines=2");
        reread.SupportedModules[0].Comments!.CommentLines.Should().Equal(module.Comments.CommentLines);
    }
}
