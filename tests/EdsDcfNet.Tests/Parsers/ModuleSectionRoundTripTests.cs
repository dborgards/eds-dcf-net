namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
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
    public void Validate_ModuleCommentsLinesMismatch_ReturnsIssue()
    {
        // Arrange
        var eds = ValidModuleEds();
        eds.SupportedModules[0].Comments = new Comments { Lines = 2 };
        eds.SupportedModules[0].Comments!.CommentLines[1] = "only one";

        // Act
        var issues = CanOpenModelValidator.Validate(eds);

        // Assert
        issues.Should().Contain(issue =>
            issue.Path == "SupportedModules[0].Comments.Lines" &&
            issue.Message.Contains("2", StringComparison.Ordinal) &&
            issue.Message.Contains("1", StringComparison.Ordinal));
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
            issue.Message.Contains("1..1", StringComparison.Ordinal));
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
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(ModuleSections);

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);
        var reread = CanOpenFile.Eds.ReadString(written);

        // Assert
        AssertModuleSectionsEquivalent(eds.SupportedModules, reread.SupportedModules);
    }

    [Fact]
    public void WriteToString_ValidatedLinesMismatch_ThrowsModelValidationException()
    {
        // Arrange
        var eds = ValidModuleEds();
        var comments = new Comments { Lines = 3 };
        comments.CommentLines[1] = "one";
        eds.SupportedModules[0].Comments = comments;

        // Act
        var act = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        act.Should().Throw<ModelValidationException>();
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
