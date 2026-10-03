namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Tests.Utilities;
using EdsDcfNet.Validation;

/// <summary>
/// CiA 306-1 §8.3: [MxComments], [MxSubExtends], [MxSubExtxxxx] and [MxFixedxxxx]
/// must be parsed into <see cref="ModuleInfo"/> and written back. They must not
/// remain in <see cref="ElectronicDataSheet.AdditionalSections"/>.
/// </summary>
public class ModuleSectionRoundTripTests
{
    private const string ModuleSections = """
        [FileInfo]
        FileName=module_sections.eds
        FileVersion=1
        FileRevision=0
        EDSVersion=4.0
        Description=Module sections

        [DeviceInfo]
        VendorName=Modular Vendor
        VendorNumber=0x300
        ProductName=Bus Coupler
        ProductNumber=0x3001
        RevisionNumber=0x10000
        OrderCode=MOD-BC
        BaudRate_10=0
        BaudRate_20=0
        BaudRate_50=0
        BaudRate_125=1
        BaudRate_250=1
        BaudRate_500=1
        BaudRate_800=0
        BaudRate_1000=0
        SimpleBootUpMaster=0
        SimpleBootUpSlave=1
        Granularity=8
        DynamicChannelsSupported=0
        GroupMessaging=0
        NrOfRXPDO=4
        NrOfTXPDO=4
        LSS_Supported=0

        [MandatoryObjects]
        SupportedObjects=1
        1=0x1000

        [1000]
        ParameterName=Device Type
        ObjectType=0x7
        DataType=0x7
        AccessType=ro
        DefaultValue=0x191
        PDOMapping=0

        [SupportedModules]
        NrOfEntries=1

        [M1ModuleInfo]
        ProductName=Digital Input Module
        ProductVersion=1
        ProductRevision=0
        OrderCode=MOD-DI-8

        [M1Comments]
        Lines=2
        Line1=Module with 16 input lines
        Line2=and 8 output lines.

        [M1FixedObjects]
        NrOfEntries=1
        1=0x6423

        [M1Fixed6423]
        SubNumber=1
        ParameterName=Module parameter
        ObjectType=0x7
        DataType=0x6
        AccessType=rw
        DefaultValue=0
        PDOMapping=0

        [M1Fixed6423sub1]
        ParameterName=First channel
        ObjectType=0x7
        DataType=0x5
        AccessType=ro
        DefaultValue=1
        PDOMapping=1

        [M1SubExtends]
        NrOfEntries=2
        1=0x6000
        2=0xA0

        [M1SubExt6000]
        ParameterName=Input lines
        DataType=0x5
        AccessType=ro
        DefaultValue=0
        PDOMapping=1
        Count=4
        ObjExtend=128

        [M1SubExtA0]
        ParameterName=Packed bits
        DataType=0x5
        AccessType=rw
        PDOMapping=0
        Count=0;2
        ObjExtend=0
        """;

    [Fact]
    public void ReadString_AllFourModuleSectionTypes_PopulatesModuleInfo()
    {
        // Arrange
        var content = ModuleSections;

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);

        // Assert
        var module = eds.SupportedModules.Should().ContainSingle().Subject;
        module.Comments.Should().NotBeNull();
        module.Comments!.Lines.Should().Be(2);
        module.Comments.CommentLines.Should().Equal(new Dictionary<int, string>
        {
            [1] = "Module with 16 input lines",
            [2] = "and 8 output lines."
        });

        module.FixedObjects.Should().Equal((ushort)0x6423);
        module.FixedObjectDefinitions.Should().ContainKey(0x6423);
        var fixedObject = module.FixedObjectDefinitions[0x6423];
        fixedObject.ParameterName.Should().Be("Module parameter");
        fixedObject.DataType.Should().Be((ushort)0x0006);
        fixedObject.SubObjects.Should().ContainKey(1);
        fixedObject.SubObjects[1].ParameterName.Should().Be("First channel");

        module.SubExtends.Should().Equal((ushort)0x6000, (ushort)0x00A0);
        module.SubExtensionDefinitions.Should().ContainKeys((ushort)0x6000, (ushort)0x00A0);
        var extended = module.SubExtensionDefinitions[0x6000];
        extended.ParameterName.Should().Be("Input lines");
        extended.Count.Should().Be("4");
        extended.ObjExtend.Should().Be(128);
        extended.PdoMapping.Should().BeTrue();
        module.SubExtensionDefinitions[0x00A0].Count.Should().Be("0;2");
        module.SubExtensionDefinitions[0x00A0].ObjExtend.Should().Be(0);

        eds.AdditionalSections.Should().NotContainKey("M1Comments");
        eds.AdditionalSections.Should().NotContainKey("M1SubExtends");
        eds.AdditionalSections.Should().NotContainKey("M1SubExt6000");
        eds.AdditionalSections.Should().NotContainKey("M1SubExtA0");
        eds.AdditionalSections.Should().NotContainKey("M1Fixed6423");
        eds.AdditionalSections.Should().NotContainKey("M1Fixed6423sub1");
    }

    [Fact]
    public void ReadString_FixedObjectForUndeclaredModule_StaysInAdditionalSections()
    {
        // Arrange — [M9Fixed6000] has no [M9ModuleInfo], so it must not be dropped.
        var content = ModuleSections + """

            [M9Fixed6000]
            ParameterName=Unlisted module
            ObjectType=0x7
            DataType=0x5
            AccessType=ro
            DefaultValue=0
            PDOMapping=0
            """;

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);

        // Assert
        eds.SupportedModules.Should().ContainSingle();
        eds.SupportedModules[0].FixedObjectDefinitions.Should().ContainKey((ushort)0x6423);
        eds.SupportedModules[0].FixedObjectDefinitions.Should().NotContainKey((ushort)0x6000);
        eds.AdditionalSections.Should().ContainKey("M9Fixed6000");
        eds.AdditionalSections["M9Fixed6000"]["ParameterName"].Should().Be("Unlisted module");
        eds.AdditionalSections.Should().NotContainKey("M1Fixed6423");
        eds.AdditionalSections.Should().NotContainKey("M1Fixed6423sub1");
    }

    [Fact]
    public void WriteToString_AllFourModuleSectionTypes_RoundTripsAndIsIdempotent()
    {
        // Arrange
        var original = CanOpenFile.Eds.ReadString(ModuleSections);

        // Act
        var first = CanOpenFile.Eds.WriteToString(original);
        var reread = CanOpenFile.Eds.ReadString(first);
        var second = CanOpenFile.Eds.WriteToString(reread);

        // Assert
        second.Should().Be(first);
        AssertModuleSectionsEquivalent(original.SupportedModules, reread.SupportedModules);
        first.IndexOf("[M1Comments]", StringComparison.Ordinal).Should().BeLessThan(first.IndexOf("[M1FixedObjects]", StringComparison.Ordinal));
        first.IndexOf("[M1FixedObjects]", StringComparison.Ordinal).Should().BeLessThan(first.IndexOf("[M1Fixed6423]", StringComparison.Ordinal));
        first.IndexOf("[M1Fixed6423]", StringComparison.Ordinal).Should().BeLessThan(first.IndexOf("[M1Fixed6423sub1]", StringComparison.Ordinal));
        first.IndexOf("[M1Fixed6423sub1]", StringComparison.Ordinal).Should().BeLessThan(first.IndexOf("[M1SubExtends]", StringComparison.Ordinal));
        first.IndexOf("[M1SubExtends]", StringComparison.Ordinal).Should().BeLessThan(first.IndexOf("[M1SubExt6000]", StringComparison.Ordinal));
        first.IndexOf("[M1SubExt6000]", StringComparison.Ordinal).Should().BeLessThan(first.IndexOf("[M1SubExtA0]", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadFile_CanonicalModuleSections_MatchesWriterBytes()
    {
        // Arrange — fixture is already in writer-canonical form (normative section
        // names and writer key order), so byte equality is meaningful here.
        var fixturePath = Path.Combine("Fixtures", "module_sections_canonical.eds");
        var original = File.ReadAllText(fixturePath).Replace("\r\n", "\n");

        // Act
        var written = CanOpenFile.Eds.WriteToString(CanOpenFile.Eds.ReadString(original)).Replace("\r\n", "\n");

        // Assert
        written.Should().Be(original);
    }

    [Fact]
    public void ReadFile_ModularDeviceDcf_SemanticRoundTripAndWriterIdempotent()
    {
        // Arrange — the fixture spells [DeviceCommissioning]; the writer normalizes
        // that name, so this test compares models and writer output, not fixture bytes.
        var original = CanOpenFile.Dcf.ReadFile("Fixtures/modular_device.dcf");

        // Act
        var first = CanOpenFile.Dcf.WriteToString(original);
        var reread = CanOpenFile.Dcf.ReadString(first);
        var second = CanOpenFile.Dcf.WriteToString(reread);

        // Assert
        second.Should().Be(first);
        reread.SupportedModules.Should().HaveCount(original.SupportedModules.Count);
        AssertModuleSectionsEquivalent(original.SupportedModules, reread.SupportedModules);
        reread.ConnectedModules.Should().Equal(original.ConnectedModules);
        reread.AdditionalSections.Keys.Should().BeEquivalentTo(original.AdditionalSections.Keys);
    }

    [Fact]
    public void ReadString_SubExtensionObjectDescriptionEntries_RoundTrips()
    {
        // Arrange — CiA 306: [MxSubExtxxxx] has the same entries as an object
        // description, plus Count and ObjExtend. These keys are not vendor text.
        var content = ModuleHeader() + """
            [M1SubExtends]
            NrOfEntries=1
            1=0x6000

            [M1SubExt6000]
            ParameterName=Input lines
            ObjectType=0x8
            DataType=0x5
            AccessType=ro
            DefaultValue=1
            LowLimit=0
            HighLimit=10
            PDOMapping=1
            ObjFlags=0x1
            SubNumber=1
            CompactSubObj=4
            Count=4
            ObjExtend=128
            """;

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);
        var written = CanOpenFile.Eds.WriteToString(eds).Replace("\r\n", "\n");
        var reread = CanOpenFile.Eds.ReadString(written);
        var second = CanOpenFile.Eds.WriteToString(reread).Replace("\r\n", "\n");

        // Assert
        eds.AdditionalSections.Should().NotContainKey("M1SubExt6000");
        var extension = reread.SupportedModules[0].SubExtensionDefinitions[0x6000];
        extension.ObjectType.Should().Be(0x8);
        extension.LowLimit.Should().Be("0");
        extension.HighLimit.Should().Be("10");
        extension.ObjFlags.Should().Be(1u);
        extension.SubNumber.Should().Be(1);
        extension.CompactSubObj.Should().Be(4);
        // The literal keeps the checkout's newlines. Compare it as LF, the same
        // form as written, and still require every object-description key in order.
        var expectedSection = """
            [M1SubExt6000]
            SubNumber=1
            ParameterName=Input lines
            ObjectType=0x8
            DataType=0x5
            AccessType=ro
            DefaultValue=1
            LowLimit=0
            HighLimit=10
            PDOMapping=1
            ObjFlags=0x1
            CompactSubObj=4
            Count=4
            ObjExtend=128
            """.Replace("\r\n", "\n");
        written.Should().Contain(expectedSection);
        second.Should().Be(written);
    }

    [Fact]
    public void Validate_SubExtensionValueOutsideDataType_ReportsIssue()
    {
        // Arrange — matching list counts, so only the value check can fail.
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        var module = new ModuleInfo { ModuleNumber = 1, ProductName = "Module", OrderCode = "OC" };
        module.SubExtends.Add(0x6000);
        module.SubExtensionDefinitions[0x6000] = new ModuleSubExtension
        {
            Index = 0x6000,
            ParameterName = "Input",
            DataType = CanOpenDataType.Unsigned8,
            DefaultValue = "256",
            LowLimit = "0",
            HighLimit = "1000",
            Count = "1"
        };
        eds.SupportedModules.Add(module);
        var ranges = new CanOpenValidationOptions { CheckValueRanges = true };

        // Act
        var ranged = CanOpenModelValidator.Validate(eds, ranges);
        var strict = CanOpenModelValidator.Validate(eds, CanOpenValidationOptions.Strict);
        var defaults = CanOpenModelValidator.Validate(eds);

        // Assert
        ranged.Should().Contain(issue =>
            issue.Path == "SupportedModules[0].SubExtensionDefinitions[0x6000].DefaultValue" &&
            issue.Message.Contains("UNSIGNED8", StringComparison.Ordinal));
        ranged.Should().Contain(issue =>
            issue.Path == "SupportedModules[0].SubExtensionDefinitions[0x6000].HighLimit" &&
            issue.Message.Contains("UNSIGNED8", StringComparison.Ordinal));
        strict.Should().Contain(issue =>
            issue.Path == "SupportedModules[0].SubExtensionDefinitions[0x6000].DefaultValue");
        defaults.Should().NotContain(issue =>
            issue.Path == "SupportedModules[0].SubExtensionDefinitions[0x6000].DefaultValue");
    }

    [Fact]
    public void Validate_SubExtensionInvalidObjectType_ReportsIssue()
    {
        // Arrange — the same unconditional object-code check as a dictionary object.
        var eds = SubExtensionEds(new ModuleSubExtension
        {
            Index = 0x6000,
            ParameterName = "Input",
            ObjectType = 0xFF,
            DataType = CanOpenDataType.Unsigned8,
            Count = "1"
        });
        var ranges = new CanOpenValidationOptions { CheckValueRanges = true };

        // Act
        var defaults = CanOpenModelValidator.Validate(eds);
        var ranged = CanOpenModelValidator.Validate(eds, ranges);
        var strict = CanOpenModelValidator.Validate(eds, CanOpenValidationOptions.Strict);
        var act = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        defaults.Should().Contain(issue =>
            issue.Path == "SupportedModules[0].SubExtensionDefinitions[0x6000].ObjectType" &&
            issue.Message.Contains("0xFF", StringComparison.Ordinal));
        ranged.Should().Contain(issue =>
            issue.Path == "SupportedModules[0].SubExtensionDefinitions[0x6000].ObjectType");
        strict.Should().Contain(issue =>
            issue.Path == "SupportedModules[0].SubExtensionDefinitions[0x6000].ObjectType");
        act.Should().Throw<ModelValidationException>().Which.Issues.Should().Contain(issue =>
            issue.Path == "SupportedModules[0].SubExtensionDefinitions[0x6000].ObjectType");
    }

    [Fact]
    public void Validate_SubExtensionOverlongParameterName_ReportsIssue()
    {
        // Arrange — CiA 306 parameter names are at most 241 characters.
        var eds = SubExtensionEds(new ModuleSubExtension
        {
            Index = 0x6000,
            ParameterName = new string('A', 242),
            ObjectType = CanOpenObjectType.Var,
            Count = "1"
        });
        var ranges = new CanOpenValidationOptions { CheckValueRanges = true };

        // Act
        var defaults = CanOpenModelValidator.Validate(eds);
        var ranged = CanOpenModelValidator.Validate(eds, ranges);
        var strict = CanOpenModelValidator.Validate(eds, CanOpenValidationOptions.Strict);
        var act = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        defaults.Should().Contain(issue =>
            issue.Path == "SupportedModules[0].SubExtensionDefinitions[0x6000].ParameterName" &&
            issue.Message.Contains("242", StringComparison.Ordinal));
        ranged.Should().Contain(issue =>
            issue.Path == "SupportedModules[0].SubExtensionDefinitions[0x6000].ParameterName");
        strict.Should().Contain(issue =>
            issue.Path == "SupportedModules[0].SubExtensionDefinitions[0x6000].ParameterName");
        act.Should().Throw<ModelValidationException>().Which.Issues.Should().Contain(issue =>
            issue.Path == "SupportedModules[0].SubExtensionDefinitions[0x6000].ParameterName");
    }

    [Fact]
    public void ReadString_EmptyFixedObjectType_BecomesVarAndRoundTrips()
    {
        // Arrange — ObjectType= is the omitted form, not NULL. An explicit 0x0 stays NULL.
        var content = ModuleHeader() + """
            [M1FixedObjects]
            NrOfEntries=3
            1=0x6423
            2=0x6424
            3=0x6425

            [M1Fixed6423]
            ParameterName=Inputs
            ObjectType=
            DataType=0x5
            AccessType=ro
            PDOMapping=0

            [M1Fixed6423sub1]
            ParameterName=Line
            ObjectType=
            DataType=0x5
            AccessType=ro
            PDOMapping=0

            [M1Fixed6424]
            ParameterName=Explicit null
            ObjectType=0x0
            DataType=0x5
            AccessType=ro
            PDOMapping=0

            [M1Fixed6425]
            ParameterName=Omitted
            DataType=0x5
            AccessType=ro
            PDOMapping=0
            """;

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);
        var written = CanOpenFile.Eds.WriteToString(result.Model).Replace("\r\n", "\n");

        // Assert
        result.Diagnostics.Should().NotContain(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.InvalidObjectType);
        var empty = result.Model.SupportedModules[0].FixedObjectDefinitions[0x6423];
        empty.ObjectType.Should().Be(CanOpenObjectType.Var);
        empty.SubObjects[1].ObjectType.Should().Be(CanOpenObjectType.Var);
        result.Model.SupportedModules[0].FixedObjectDefinitions[0x6424].ObjectType.Should().Be(CanOpenObjectType.Null);
        result.Model.SupportedModules[0].FixedObjectDefinitions[0x6425].ObjectType.Should().Be(CanOpenObjectType.Var);
        SectionBody(written, "[M1Fixed6423]").Should().Contain("ObjectType=0x7\n").And.NotContain("ObjectType=0x0");
        SectionBody(written, "[M1Fixed6423sub1]").Should().Contain("ObjectType=0x7\n");
        SectionBody(written, "[M1Fixed6424]").Should().Contain("ObjectType=0x0\n");
        SectionBody(written, "[M1Fixed6425]").Should().Contain("ObjectType=0x7\n");
    }

    [Fact]
    public void Validate_SubExtensionInsideDataType_ReturnsNoValueIssue()
    {
        // Arrange
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        var module = new ModuleInfo { ModuleNumber = 1, ProductName = "Module", OrderCode = "OC" };
        module.SubExtends.Add(0x6000);
        module.SubExtensionDefinitions[0x6000] = new ModuleSubExtension
        {
            Index = 0x6000,
            ParameterName = "Input",
            DataType = CanOpenDataType.Unsigned8,
            DefaultValue = "255",
            LowLimit = "0",
            HighLimit = "255",
            Count = "1"
        };
        eds.SupportedModules.Add(module);

        // Act
        var issues = CanOpenModelValidator.Validate(eds, new CanOpenValidationOptions
        {
            CheckValueRanges = true
        });

        // Assert
        issues.Should().NotContain(issue =>
            issue.Path.StartsWith("SupportedModules[0].SubExtensionDefinitions", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(CanOpenObjectType.DefType)]
    [InlineData(CanOpenObjectType.DefStruct)]
    public void Validate_SubExtensionTypeDescription_SkipsValueCheck(byte objectType)
    {
        // Arrange — DEFTYPE and DEFSTRUCT describe types, as they do on a dictionary object.
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        var module = new ModuleInfo { ModuleNumber = 1, ProductName = "Module", OrderCode = "OC" };
        module.SubExtends.Add(0x6000);
        module.SubExtensionDefinitions[0x6000] = new ModuleSubExtension
        {
            Index = 0x6000,
            ParameterName = "Type",
            ObjectType = objectType,
            DataType = CanOpenDataType.Unsigned8,
            DefaultValue = "256",
            Count = "1"
        };
        eds.SupportedModules.Add(module);

        // Act
        var issues = CanOpenModelValidator.Validate(eds, new CanOpenValidationOptions
        {
            CheckValueRanges = true
        });

        // Assert
        issues.Should().NotContain(issue =>
            issue.Path == "SupportedModules[0].SubExtensionDefinitions[0x6000].DefaultValue");
    }

    [Fact]
    public void ReadString_ObjExtendAtMaxValue_Preserves255()
    {
        // Arrange
        var content = ModuleHeader() + """
            [M1SubExtends]
            NrOfEntries=1
            1=0x6000

            [M1SubExt6000]
            ParameterName=At max
            DataType=0x5
            AccessType=ro
            PDOMapping=0
            Count=255
            ObjExtend=255
            """;

        // Act
        var module = CanOpenFile.Eds.ReadString(content).SupportedModules[0];

        // Assert
        var extension = module.SubExtensionDefinitions[0x6000];
        extension.ObjExtend.Should().Be(byte.MaxValue);
        extension.Count.Should().Be("255");
        var written = CanOpenFile.Eds.WriteToString(CanOpenFile.Eds.ReadString(content));
        written.Should().Contain("ObjExtend=255");
        written.Should().Contain("Count=255");
    }

    [Fact]
    public void ReadString_CountBitPackAtMaxValue_PreservesZeroSemicolon255()
    {
        // Arrange
        var content = ModuleHeader() + """
            [M1SubExtends]
            NrOfEntries=1
            1=0x1

            [M1SubExt1]
            ParameterName=Bits
            DataType=0x5
            AccessType=rw
            PDOMapping=0
            Count=0;255
            """;

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);
        var written = CanOpenFile.Eds.WriteToString(eds);
        var reread = CanOpenFile.Eds.ReadString(written);

        // Assert
        reread.SupportedModules[0].SubExtensionDefinitions[0x0001].Count.Should().Be("0;255");
        written.Should().Contain("[M1SubExt1]");
        written.Should().Contain("Count=0;255");
        eds.AdditionalSections.Should().NotContainKey("M1SubExt1");
    }

    [Fact]
    public void ReadStringWithDiagnostics_MalformedObjExtend_LeavesUnsetAndContinues()
    {
        // Arrange
        var content = ModuleHeader() + """
            [M1SubExtends]
            NrOfEntries=1
            1=0x6000

            [M1SubExt6000]
            ParameterName=Broken
            DataType=0x5
            AccessType=ro
            PDOMapping=0
            Count=4
            ObjExtend=nope
            """;

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.InvalidModuleObjExtend);
        diagnostic.Path.Should().Be("M1SubExt6000.ObjExtend");
        result.Model.SupportedModules[0].SubExtensionDefinitions[0x6000].ObjExtend.Should().BeNull();
        result.Model.SupportedModules[0].SubExtensionDefinitions[0x6000].ParameterName.Should().Be("Broken");

        var act = () => CanOpenFile.Eds.ReadString(content, new CanOpenFileOptions { StrictParsing = true });
        act.Should().Throw<EdsParseException>()
            .Which.Code.Should().Be(ParseDiagnosticCodes.InvalidModuleObjExtend);
    }

    [Fact]
    public void ReadString_DcfFixedObjectParameterValue_RoundTrips()
    {
        // Arrange
        var content = """
            [FileInfo]
            FileName=mod.dcf
            FileVersion=1
            FileRevision=0
            EDSVersion=4.0

            [DeviceInfo]
            VendorName=Vendor
            ProductName=Product

            [DeviceComissioning]
            NodeID=4
            NodeName=Node
            Baudrate=250
            NetNumber=0
            NetworkName=Net
            CANopenManager=0

            [MandatoryObjects]
            SupportedObjects=1
            1=0x1000

            [1000]
            ParameterName=Device Type
            ObjectType=0x7
            DataType=0x7
            AccessType=ro
            PDOMapping=0

            [SupportedModules]
            NrOfEntries=1

            [M1ModuleInfo]
            ProductName=Out
            ProductVersion=1
            ProductRevision=0
            OrderCode=DO

            [M1FixedObjects]
            NrOfEntries=1
            1=0x6200

            [M1Fixed6200]
            ParameterName=Digital Output
            ObjectType=0x7
            DataType=0x5
            AccessType=rw
            DefaultValue=0
            PDOMapping=1
            ParameterValue=1
            """;

        // Act
        var dcf = CanOpenFile.Dcf.ReadString(content);
        var written = CanOpenFile.Dcf.WriteToString(dcf);
        var reread = CanOpenFile.Dcf.ReadString(written);

        // Assert
        reread.SupportedModules[0].FixedObjectDefinitions[0x6200].ParameterValue.Should().Be("1");
        written.Should().Contain("[M1Fixed6200]");
        written.Should().Contain("ParameterValue=1");
        reread.AdditionalSections.Should().NotContainKey("M1Fixed6200");
    }

    [Fact]
    public void Validate_ModuleCommentsLinesMismatch_IsAssessedAsWritten()
    {
        // Arrange
        var eds = ValidModuleEds();
        eds.SupportedModules[0].Comments = new Comments { Lines = 2 };
        eds.SupportedModules[0].Comments!.CommentLines[1] = "only one";

        // Act
        var issues = CanOpenModelValidator.Validate(eds);

        // Assert — the writer emits Lines=1 for the one stored line, so the stale 2 is not a finding.
        issues.Should().NotContain(issue => issue.Path == "SupportedModules[0].Comments.Lines");
    }

    [Fact]
    public void Validate_ModuleCommentsKeyOffset_ReturnsIssue()
    {
        // Arrange — same count as Lines, but the only key is 2, so a write/read would drop it.
        var eds = ValidModuleEds();
        var comments = new Comments { Lines = 1 };
        comments.CommentLines[2] = "offset";
        eds.SupportedModules[0].Comments = comments;

        // Act
        var issues = CanOpenModelValidator.Validate(eds);

        // Assert
        issues.Should().Contain(issue =>
            issue.Path == "SupportedModules[0].Comments.Lines" &&
            issue.Message.Contains("1..2", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ModuleCommentsKeyGap_ReturnsIssue()
    {
        // Arrange — count matches Lines, but key 2 is missing.
        var eds = ValidModuleEds();
        var comments = new Comments { Lines = 2 };
        comments.CommentLines[1] = "first";
        comments.CommentLines[3] = "skipped";
        eds.SupportedModules[0].Comments = comments;

        // Act
        var issues = CanOpenModelValidator.Validate(eds);

        // Assert
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].Comments.Lines");
    }

    [Fact]
    public void ReadString_BlankModuleCommentLine_ValidatesAndRoundTrips()
    {
        // Arrange — Line1= is present and empty. That is a real line, not a gap.
        var content = ModuleHeader() + """
            [M1Comments]
            Lines=2
            Line1=
            Line2=text
            """;

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);
        var issues = CanOpenModelValidator.Validate(eds);
        var written = CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);
        var reread = CanOpenFile.Eds.ReadString(written);
        var second = CanOpenFile.Eds.WriteToString(reread, CanOpenWriteOptions.Validated);

        // Assert
        var comments = eds.SupportedModules[0].Comments;
        comments.Should().NotBeNull();
        comments!.Lines.Should().Be(2);
        comments.CommentLines.Should().Equal(new Dictionary<int, string>
        {
            [1] = string.Empty,
            [2] = "text"
        });
        issues.Should().NotContain(issue => issue.Path == "SupportedModules[0].Comments.Lines");
        written.Replace("\r\n", "\n").Should().Contain("Line1=\n").And.Contain("Line2=text");
        reread.SupportedModules[0].Comments!.CommentLines.Should().Equal(comments.CommentLines);
        second.Should().Be(written);
    }

    [Fact]
    public void ReadString_MissingModuleCommentLine_StaysInvalid()
    {
        // Arrange — Line1 is absent, so the contiguous 1..Lines range is still broken.
        var content = ModuleHeader() + """
            [M1Comments]
            Lines=2
            Line2=text
            """;

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);
        var issues = CanOpenModelValidator.Validate(eds);
        var act = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        var comments = eds.SupportedModules[0].Comments!;
        comments.Lines.Should().Be(2);
        comments.CommentLines.Should().NotContainKey(1);
        comments.CommentLines.Should().ContainKey(2);
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].Comments.Lines");
        act.Should().Throw<ModelValidationException>();
    }

    [Fact]
    public void WriteToString_ValidatedCommentKeyOffset_ThrowsModelValidationException()
    {
        // Arrange
        var eds = ValidModuleEds();
        var comments = new Comments { Lines = 1 };
        comments.CommentLines[2] = "offset";
        eds.SupportedModules[0].Comments = comments;

        // Act
        var act = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        act.Should().Throw<ModelValidationException>().Which.Issues.Should().Contain(issue =>
            issue.Path == "SupportedModules[0].Comments.Lines");
    }

    [Fact]
    public void Validate_ModuleSubExtendsCountMismatch_ReturnsIssue()
    {
        // Arrange
        var eds = ValidModuleEds();
        eds.SupportedModules[0].SubExtends.Add(0x6000);
        eds.SupportedModules[0].SubExtends.Add(0x6001);
        eds.SupportedModules[0].SubExtensionDefinitions[0x6000] = new ModuleSubExtension
        {
            Index = 0x6000,
            ParameterName = "Only one",
            Count = "4"
        };

        // Act
        var issues = CanOpenModelValidator.Validate(eds);

        // Assert
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].SubExtends");
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].SubExtensionDefinitions[0x6001]");
    }

    [Fact]
    public void Validate_ModuleFixedObjectsCountMismatch_ReturnsIssue()
    {
        // Arrange
        var eds = ValidModuleEds();
        eds.SupportedModules[0].FixedObjects.Add(0x6423);
        eds.SupportedModules[0].FixedObjectDefinitions[0x6423] = new CanOpenObject
        {
            Index = 0x6423,
            ParameterName = "Present"
        };
        eds.SupportedModules[0].FixedObjects.Add(0x6424);

        // Act
        var issues = CanOpenModelValidator.Validate(eds);

        // Assert
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].FixedObjectDefinitions[0x6424]");
    }

    [Fact]
    public void Validate_ModuleSubExtensionCountMalformed_ReturnsIssue()
    {
        // Arrange
        var eds = ValidModuleEds();
        eds.SupportedModules[0].SubExtends.Add(0x6000);
        eds.SupportedModules[0].SubExtensionDefinitions[0x6000] = new ModuleSubExtension
        {
            Index = 0x6000,
            ParameterName = "Bad count",
            Count = "4;2"
        };

        // Act
        var issues = CanOpenModelValidator.Validate(eds);

        // Assert
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].SubExtensionDefinitions[0x6000].Count");
    }

    [Fact]
    public void WriteToString_ValidatedRoundTrip_ConsistentModuleSections_Succeeds()
    {
        // Arrange — [M1Fixed6423] is a VAR, which CiA 306-1 Table 7 gives no sub-indexes
        // (decision E10 rejects it on a validated write); drop them to keep the model consistent.
        var eds = CanOpenFile.Eds.ReadString(ModuleSections);
        var fixedVar = eds.SupportedModules[0].FixedObjectDefinitions[0x6423];
        fixedVar.SubObjects.Clear();
        fixedVar.SubNumber = null;

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);
        var reread = CanOpenFile.Eds.ReadString(written);

        // Assert
        AssertModuleSectionsEquivalent(eds.SupportedModules, reread.SupportedModules);
    }

    [Fact]
    public void WriteToString_ValidatedStaleLines_WritesTheStoredLineCount()
    {
        // Arrange
        var eds = ValidModuleEds();
        var comments = new Comments { Lines = 3 };
        comments.CommentLines[1] = "one";
        eds.SupportedModules[0].Comments = comments;

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert — Lines is not written as stored, so the stale 3 is not rejected.
        written.Should().Contain("Lines=1");
    }

    [Fact]
    public void ConvertToDcf_ModuleSections_PreservesCollectionsInOutput()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(ModuleSections);

        // Act
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 5, baudrate: 250, nodeName: "Coupler");
        var written = CanOpenFile.Dcf.WriteToString(dcf);
        var reread = CanOpenFile.Dcf.ReadString(written);

        // Assert
        AssertModuleSectionsEquivalent(eds.SupportedModules, dcf.SupportedModules);
        dcf.SupportedModules[0].Comments.Should().NotBeSameAs(eds.SupportedModules[0].Comments);
        dcf.SupportedModules[0].FixedObjectDefinitions.Should().NotBeSameAs(
            eds.SupportedModules[0].FixedObjectDefinitions);
        written.Should().Contain("[M1Comments]");
        written.Should().Contain("[M1SubExtends]");
        written.Should().Contain("[M1SubExt6000]");
        written.Should().Contain("[M1Fixed6423]");
        written.Should().Contain("Line1=Module with 16 input lines");
        AssertModuleSectionsEquivalent(eds.SupportedModules, reread.SupportedModules);
    }

    private static void AssertModuleSectionsEquivalent(
        IReadOnlyList<ModuleInfo> expected,
        IReadOnlyList<ModuleInfo> actual)
    {
        actual.Should().HaveCount(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            var left = expected[i];
            var right = actual[i];
            right.ModuleNumber.Should().Be(left.ModuleNumber);
            right.ProductName.Should().Be(left.ProductName);
            right.ProductVersion.Should().Be(left.ProductVersion);
            right.ProductRevision.Should().Be(left.ProductRevision);
            right.OrderCode.Should().Be(left.OrderCode);
            right.FixedObjects.Should().Equal(left.FixedObjects);
            right.SubExtends.Should().Equal(left.SubExtends);

            if (left.Comments == null)
            {
                right.Comments.Should().BeNull();
            }
            else
            {
                right.Comments.Should().NotBeNull();
                right.Comments!.Lines.Should().Be(left.Comments.Lines);
                right.Comments.CommentLines.Should().Equal(left.Comments.CommentLines);
            }

            right.FixedObjectDefinitions.Keys.Should().BeEquivalentTo(left.FixedObjectDefinitions.Keys);
            foreach (var index in left.FixedObjectDefinitions.Keys)
            {
                var expectedObject = left.FixedObjectDefinitions[index];
                var actualObject = right.FixedObjectDefinitions[index];
                actualObject.ParameterName.Should().Be(expectedObject.ParameterName);
                actualObject.ObjectType.Should().Be(expectedObject.ObjectType);
                actualObject.DataType.Should().Be(expectedObject.DataType);
                actualObject.AccessType.Should().Be(expectedObject.AccessType);
                actualObject.DefaultValue.Should().Be(expectedObject.DefaultValue);
                actualObject.PdoMapping.Should().Be(expectedObject.PdoMapping);
                actualObject.ParameterValue.Should().Be(expectedObject.ParameterValue);
                actualObject.SubNumber.Should().Be(expectedObject.SubNumber);
                actualObject.SubObjects.Keys.Should().BeEquivalentTo(expectedObject.SubObjects.Keys);
                foreach (var subIndex in expectedObject.SubObjects.Keys)
                {
                    actualObject.SubObjects[subIndex].ParameterName.Should().Be(
                        expectedObject.SubObjects[subIndex].ParameterName);
                    actualObject.SubObjects[subIndex].DataType.Should().Be(
                        expectedObject.SubObjects[subIndex].DataType);
                }
            }

            right.SubExtensionDefinitions.Keys.Should().BeEquivalentTo(left.SubExtensionDefinitions.Keys);
            foreach (var index in left.SubExtensionDefinitions.Keys)
            {
                var expectedExtension = left.SubExtensionDefinitions[index];
                var actualExtension = right.SubExtensionDefinitions[index];
                actualExtension.Index.Should().Be(expectedExtension.Index);
                actualExtension.ParameterName.Should().Be(expectedExtension.ParameterName);
                actualExtension.DataType.Should().Be(expectedExtension.DataType);
                actualExtension.AccessType.Should().Be(expectedExtension.AccessType);
                actualExtension.DefaultValue.Should().Be(expectedExtension.DefaultValue);
                actualExtension.LowLimit.Should().Be(expectedExtension.LowLimit);
                actualExtension.HighLimit.Should().Be(expectedExtension.HighLimit);
                actualExtension.ObjectType.Should().Be(expectedExtension.ObjectType);
                actualExtension.SubNumber.Should().Be(expectedExtension.SubNumber);
                actualExtension.ObjFlags.Should().Be(expectedExtension.ObjFlags);
                actualExtension.CompactSubObj.Should().Be(expectedExtension.CompactSubObj);
                actualExtension.PdoMapping.Should().Be(expectedExtension.PdoMapping);
                actualExtension.Count.Should().Be(expectedExtension.Count);
                actualExtension.ObjExtend.Should().Be(expectedExtension.ObjExtend);
            }
        }
    }

    private static string ModuleHeader() => """
        [DeviceInfo]
        VendorName=Test
        ProductName=Coupler

        [MandatoryObjects]
        SupportedObjects=0

        [SupportedModules]
        NrOfEntries=1

        [M1ModuleInfo]
        ProductName=Module
        ProductVersion=1
        ProductRevision=0
        OrderCode=MOD

        """;

    private static string SectionBody(string written, string header)
    {
        var start = written.IndexOf(header + "\n", StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);
        var next = written.IndexOf("\n[", start + header.Length, StringComparison.Ordinal);
        return next < 0 ? written[start..] : written[start..next];
    }

    private static ElectronicDataSheet SubExtensionEds(ModuleSubExtension extension)
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        var module = new ModuleInfo { ModuleNumber = 1, ProductName = "Module", OrderCode = "OC" };
        module.SubExtends.Add(extension.Index);
        module.SubExtensionDefinitions[extension.Index] = extension;
        eds.SupportedModules.Add(module);
        return eds;
    }

    private static ElectronicDataSheet ValidModuleEds()
    {
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "m.eds", EdsVersion = "4.0" },
            DeviceInfo = new DeviceInfo { VendorName = "V", ProductName = "P" },
            ObjectDictionary = new ObjectDictionary()
        };
        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly
        };
        eds.SupportedModules.Add(new ModuleInfo
        {
            ModuleNumber = 1,
            ProductName = "Module",
            OrderCode = "OC"
        });
        return eds;
    }
}
