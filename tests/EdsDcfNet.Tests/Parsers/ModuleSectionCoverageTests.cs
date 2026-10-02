namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Tests.Utilities;
using EdsDcfNet.Validation;

/// <summary>
/// Branch coverage for CiA 306-1 §8.3 module sections: optional writer fields,
/// rejected section names, and validator edges that a happy-path file never hits.
/// </summary>
public class ModuleSectionCoverageTests
{
    [Fact]
    public void WriteToString_OptionalModuleFieldsAndUnlistedDefinitions_EmitsEveryBranch()
    {
        // Arrange — listed object carries every optional field; the later sub-index
        // is smaller so the SubNumber scan takes both comparisons. The unlisted
        // object and sub-extension are written after the listed ones.
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        var module = new ModuleInfo { ModuleNumber = 1, ProductName = "Module", OrderCode = "OC" };

        var listed = new CanOpenObject
        {
            Index = 0x6000,
            ParameterName = "Listed",
            ObjectType = 0x7,
            DataType = 0x0005,
            DefaultValue = "1",
            LowLimit = "0",
            HighLimit = "9",
            SrdoMapping = true,
            InvertedSrad = "1",
            ObjFlags = 1,
            CompactSubObj = 2,
            ParameterValue = "pv",
            Denotation = "den",
            ParamRefd = "ref",
            UploadFile = "up.dat",
            DownloadFile = "down.dat"
        };
        listed.SubObjects[2] = new CanOpenSubObject
        {
            SubIndex = 2,
            ParameterName = "High sub",
            ObjectType = 0x7,
            DataType = 0x0005,
            DefaultValue = "2",
            LowLimit = "0",
            HighLimit = "3",
            SrdoMapping = true,
            InvertedSrad = "0",
            ParameterValue = "spv",
            Denotation = "sden",
            ParamRefd = "sref"
        };
        listed.SubObjects[1] = new CanOpenSubObject
        {
            SubIndex = 1,
            ParameterName = "Low sub",
            ObjectType = 0x7,
            DataType = 0x0005
        };
        module.FixedObjects.Add(0x6000);
        module.FixedObjectDefinitions[0x6000] = listed;

        module.FixedObjectDefinitions[0x6100] = new CanOpenObject
        {
            Index = 0x6100,
            ParameterName = "Unlisted",
            ObjectType = 0x7
        };

        module.SubExtends.Add(0x2000);
        module.SubExtensionDefinitions[0x2000] = new ModuleSubExtension
        {
            Index = 0x2000,
            ParameterName = "Listed extension",
            Count = "4"
        };
        module.SubExtensionDefinitions[0x2100] = new ModuleSubExtension
        {
            Index = 0x2100,
            ParameterName = "Unlisted extension",
            Count = "1"
        };
        eds.SupportedModules.Add(module);

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds).Replace("\r\n", "\n");

        // Assert
        written.Should().Contain("[M1Fixed6000]");
        written.Should().Contain("SubNumber=2");
        written.Should().Contain("LowLimit=0");
        written.Should().Contain("HighLimit=9");
        written.Should().Contain("SRDOMapping=1");
        written.Should().Contain("InvertedSRAD=1");
        written.Should().Contain("ObjFlags=0x1");
        written.Should().Contain("CompactSubObj=2");
        written.Should().Contain("ParameterValue=pv");
        written.Should().Contain("Denotation=den");
        written.Should().Contain("ParamRefd=ref");
        written.Should().Contain("UploadFile=up.dat");
        written.Should().Contain("DownloadFile=down.dat");
        written.Should().Contain("[M1Fixed6000sub2]");
        written.Should().Contain("ParameterValue=spv");
        written.Should().Contain("[M1Fixed6000sub1]");
        written.Should().Contain("[M1Fixed6100]");
        written.Should().Contain("ParameterName=Unlisted");
        var unlistedAt = written.IndexOf("[M1Fixed6100]", StringComparison.Ordinal);
        var listedAt = written.IndexOf("[M1Fixed6000]", StringComparison.Ordinal);
        unlistedAt.Should().BeGreaterThan(listedAt);
        written.Should().Contain("[M1SubExt2000]");
        written.Should().Contain("[M1SubExt2100]");
        written.IndexOf("[M1SubExt2100]", StringComparison.Ordinal)
            .Should().BeGreaterThan(written.IndexOf("[M1SubExt2000]", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadString_ModuleSectionEdges_ParsesPresentLinesAndKeepsRejectedNames()
    {
        // Arrange
        var content = """
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

            [M1Fixed6000]
            ParameterName=With compact
            ObjectType=0x7
            DataType=0x5
            AccessType=ro
            PDOMapping=0
            CompactSubObj=4

            [M1Fixed6100]
            ParameterName=No data type
            ObjectType=0x7
            AccessType=rw
            PDOMapping=0

            [M1Fixed6200sub1]
            ParameterName=Orphan channel
            ObjectType=0x7
            DataType=0x5
            AccessType=ro
            PDOMapping=0

            [M]
            X=1

            [M1]
            X=1

            [MFixed6000]
            X=1

            [M2Fixed1]
            X=1

            [M9999999999Fixed1]
            X=1

            [M1Fixed]
            X=1

            [M1FixedZZ]
            X=1

            [M1Fixed10000]
            X=1

            [M1Fixedsub1]
            X=1

            [M1FixedZZsub1]
            X=1

            [M1Fixed1subZZ]
            X=1

            [M1Fixed10000sub1]
            X=1

            [M1Fixed1sub100]
            X=1

            [M1Fixed1sub]
            X=1
            """;

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);
        var module = eds.SupportedModules[0];

        // Assert
        module.FixedObjectDefinitions[0x6000].CompactSubObj.Should().Be(4);
        module.FixedObjectDefinitions[0x6000].DataType.Should().Be((ushort)0x0005);
        module.FixedObjectDefinitions[0x6100].DataType.Should().BeNull();
        module.FixedObjectDefinitions[0x6200].ParameterName.Should().BeEmpty();
        module.FixedObjectDefinitions[0x6200].SubObjects[1].ParameterName.Should().Be("Orphan channel");

        eds.AdditionalSections.Keys.Should().Contain(new[]
        {
            "M",
            "M1",
            "MFixed6000",
            "M2Fixed1",
            "M9999999999Fixed1",
            "M1Fixed",
            "M1FixedZZ",
            "M1Fixed10000",
            "M1Fixedsub1",
            "M1FixedZZsub1",
            "M1Fixed1subZZ",
            "M1Fixed10000sub1",
            "M1Fixed1sub100",
            "M1Fixed1sub"
        });
    }

    [Fact]
    public void IsConsumedModuleFixedSection_MissingDefinitionOrSub_ReturnsFalse()
    {
        // Arrange — the reader only calls this after a successful parse, so the
        // "section name matches but nothing was stored" branch needs a direct call.
        var module = new ModuleInfo { ModuleNumber = 1 };
        var modules = new List<ModuleInfo> { module };

        // Act / Assert
        CanOpenSectionParsers.IsConsumedModuleFixedSection("M1Fixed6000", modules).Should().BeFalse();

        module.FixedObjectDefinitions[0x6000] = new CanOpenObject { Index = 0x6000 };
        CanOpenSectionParsers.IsConsumedModuleFixedSection("M1Fixed6000sub1", modules).Should().BeFalse();
        CanOpenSectionParsers.IsConsumedModuleFixedSection("M1Fixed6000", modules).Should().BeTrue();
    }

    [Fact]
    public void Validate_UnlistedModuleDefinitionsAndMalformedCounts_ReturnsIssues()
    {
        // Arrange
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        var module = new ModuleInfo { ModuleNumber = 1, ProductName = "Module", OrderCode = "OC" };
        module.FixedObjects.Add(0x6000);
        module.FixedObjectDefinitions[0x6000] = new CanOpenObject { Index = 0x6000, ParameterName = "Listed" };
        module.FixedObjectDefinitions[0x6001] = new CanOpenObject { Index = 0x6001, ParameterName = "Extra" };

        module.SubExtends.Add(0x6100);
        module.SubExtends.Add(0x6101);
        module.SubExtends.Add(0x6102);
        module.SubExtends.Add(0x6103);
        module.SubExtensionDefinitions[0x6100] = Extension(0x6100, "");
        module.SubExtensionDefinitions[0x6101] = Extension(0x6101, ";2");
        module.SubExtensionDefinitions[0x6102] = Extension(0x6102, "0;1;2");
        module.SubExtensionDefinitions[0x6103] = Extension(0x6103, "zz");
        module.SubExtensionDefinitions[0x6104] = Extension(0x6104, "4");
        eds.SupportedModules.Add(module);

        // Act
        var issues = CanOpenModelValidator.Validate(eds);

        // Assert
        issues.Should().Contain(issue =>
            issue.Path == "SupportedModules[0].FixedObjects" &&
            issue.Message.Contains("0x6001", StringComparison.Ordinal) &&
            issue.Message.Contains("not listed", StringComparison.Ordinal));
        issues.Should().Contain(issue =>
            issue.Path == "SupportedModules[0].SubExtends" &&
            issue.Message.Contains("0x6104", StringComparison.Ordinal) &&
            issue.Message.Contains("not listed", StringComparison.Ordinal));
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].SubExtensionDefinitions[0x6100].Count");
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].SubExtensionDefinitions[0x6101].Count");
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].SubExtensionDefinitions[0x6102].Count");
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].SubExtensionDefinitions[0x6103].Count");
    }

    private static ModuleSubExtension Extension(ushort index, string count) => new()
    {
        Index = index,
        ParameterName = "Extension",
        Count = count
    };
}
