namespace EdsDcfNet.Tests.Writers;

using System.Text;
using EdsDcfNet;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Validation;
using EdsDcfNet.Writers;

/// <summary>
/// Edge cases of the remaining-entries mechanism (CiA 306-1 v1.4.0 § 6.2): the legacy
/// protected writer helpers, empty or <see langword="null"/> store values, kept keys that
/// collide with generated ones, the key templates and the INI write rules.
/// </summary>
public class RemainingEntriesBranchCoverageTests
{
    // Built from single lines so the fixtures do not depend on the checkout's line endings.
    private static readonly string Eds = Ini(
        "[DeviceInfo]",
        "VendorName=Vendor",
        "",
        "[MandatoryObjects]",
        "SupportedObjects=1",
        "1=0x1000",
        "",
        "[ManufacturerObjects]",
        "SupportedObjects=1",
        "1=0x2000",
        "",
        "[1000]",
        "ParameterName=Device type",
        "ObjectType=0x7",
        "DataType=0x0007",
        "AccessType=ro",
        "DefaultValue=0",
        "PDOMapping=0",
        "",
        "[1000ObjectLinks]",
        "ObjectLinks=1",
        "1=0x2000",
        "",
        "[2000]",
        "ParameterName=Compact",
        "ObjectType=0x8",
        "DataType=0x0007",
        "AccessType=rw",
        "DefaultValue=0",
        "PDOMapping=0",
        "CompactSubObj=1",
        "",
        "[2000Name]",
        "NrOfEntries=1",
        "1=First",
        "",
        "[Comments]",
        "Lines=1",
        "Line1=first",
        "");

    private static readonly string Commissioning = Ini(
        "[DeviceComissioning]",
        "NodeID=2",
        "NodeName=Node",
        "Baudrate=250",
        "NetNumber=1",
        "NetworkName=Net",
        "CANopenManager=0",
        "",
        "[2000Value]",
        "NrOfEntries=1",
        "1=5",
        "");

    [Fact]
    public void LegacyProtectedHelpers_WithoutSectionStore_WriteSectionsAsBefore()
    {
        // Arrange
        var od = new ObjectDictionary();
        od.MandatoryObjects.Add(0x1000);
        od.DummyUsage[0x0002] = true;
        var module = new ModuleInfo { ModuleNumber = 1, ProductName = "Module" };
        var tools = new List<ToolInfo> { new() { Name = "Tool", Command = "cmd" } };

        // Act
        var text = ProbeWriter.WriteAll(od, module, tools);

        // Assert
        text.Should().Contain("[DummyUsage]").And.Contain("Dummy0002=1");
        text.Should().Contain("[MandatoryObjects]").And.Contain("SupportedObjects=1");
        text.Should().Contain("[SupportedModules]").And.Contain("[M1ModuleInfo]");
        text.Should().Contain("[Tools]").And.Contain("[Tool1]");
    }

    [Fact]
    public void WriteToString_NullOrEmptyStoreValues_AreIgnored()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Eds);
        eds.SectionRemainingEntries["Tools"] = null!;
        eds.SectionRemainingEntries["DummyUsage"] = new OrderedStringDictionary();
        eds.SectionRemainingEntries["MandatoryObjects"] = null!;
        eds.SectionRemainingEntries["ManufacturerObjects"] = new OrderedStringDictionary();

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        SectionLines(written, "Tools").Should().BeEmpty();
        SectionLines(written, "DummyUsage").Should().BeEmpty();
        SectionLines(written, "MandatoryObjects").Should().Equal("SupportedObjects=1", "1=0x1000");
        SectionLines(written, "ManufacturerObjects").Should().Equal("SupportedObjects=1", "1=0x2000");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteToString_KeptKeysThatTheWriterGenerates_AreWrittenOnce(bool isDcf)
    {
        // Arrange — kept entries that collide with generated keys (count keys and slots).
        var store = new Dictionary<string, OrderedStringDictionary>(StringComparer.OrdinalIgnoreCase)
        {
            ["MandatoryObjects"] = new() { ["SupportedObjects"] = "9", ["1"] = "0x9999" },
            ["1000ObjectLinks"] = new() { ["ObjectLinks"] = "9", ["1"] = "0x9999" },
            ["2000Name"] = new() { ["NrOfEntries"] = "9", ["1"] = "Stale" },
            ["2000Value"] = new() { ["NrOfEntries"] = "9", ["1"] = "99" },
        };
        string written;
        if (isDcf)
        {
            var dcf = CanOpenFile.Dcf.ReadString(Eds + Commissioning);
            Fill(dcf.SectionRemainingEntries, store);
            dcf.Comments!.RemainingEntries["Lines"] = "9";
            written = CanOpenFile.Dcf.WriteToString(dcf);
        }
        else
        {
            var eds = CanOpenFile.Eds.ReadString(Eds);
            Fill(eds.SectionRemainingEntries, store);
            eds.Comments!.RemainingEntries["Lines"] = "9";
            written = CanOpenFile.Eds.WriteToString(eds);
        }

        // Assert
        SectionLines(written, "MandatoryObjects").Should().Equal("SupportedObjects=1", "1=0x1000");
        SectionLines(written, "1000ObjectLinks").Should().Equal("ObjectLinks=1", "1=0x2000");
        SectionLines(written, "2000Name").Should().Equal("NrOfEntries=1", "1=First");
        SectionLines(written, "Comments").Should().Equal("Lines=1", "Line1=first");
        if (isDcf)
            SectionLines(written, "2000Value").Should().Equal("NrOfEntries=1", "1=5");
    }

    [Theory]
    [InlineData("01")]
    [InlineData("1234567890")]
    [InlineData("1a")]
    [InlineData("1-")]
    [InlineData("")]
    public void IsCountedListKey_NotACanonicalEntryNumber_IsNotKnown(string key)
    {
        // CiA 306-1 § 6.2: "10=xxx is not the same as 0xA=xxx" — keys are strings, so only the
        // plain decimal spelling is a list slot.
        SectionEntryKeys.IsCountedListKey(key, "SupportedObjects", 2).Should().BeFalse();
        SectionEntryKeys.IsCommentsKey("Line" + key, 2).Should().BeFalse();
    }

    [Fact]
    public void IsGeneratedCommentsKey_LinesAndGeneratedLine_AreGenerated()
    {
        // Act / Assert
        SectionEntryKeys.IsGeneratedCommentsKey("lines", new[] { 1 }).Should().BeTrue();
        SectionEntryKeys.IsGeneratedCommentsKey("Line1", new[] { 1 }).Should().BeTrue();
        SectionEntryKeys.IsGeneratedCommentsKey("Line2", new[] { 1 }).Should().BeFalse();
    }

    [Fact]
    public void CaptureUnmappedEntries_PlainDictionarySection_CopiesUnknownKeys()
    {
        // Arrange — sections built without IniParser have no recorded key order.
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tools"] = new(StringComparer.OrdinalIgnoreCase) { ["Items"] = "0", ["Vendor"] = "kept" }
        };
        var destination = new OrderedStringDictionary();

        // Act
        CanOpenSectionParsers.CaptureUnmappedEntries(sections, "Tools", SectionEntryKeys.IsToolsKey, destination);

        // Assert
        destination.Should().Equal(new Dictionary<string, string> { ["Vendor"] = "kept" });
    }

    [Fact]
    public void IniWriteRules_CommentsAndDynamicChannelsRemainingEntries_AreChecked()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Eds);
        eds.Comments!.RemainingEntries["Lines"] = "dedicated, not written";
        eds.Comments.RemainingEntries["Bad=Key"] = "x";
        eds.DynamicChannels = new DynamicChannels();
        eds.DynamicChannels.RemainingEntries["NrOfSeg"] = "dedicated, not written";
        eds.DynamicChannels.RemainingEntries["Bad]Key"] = "x";
        var issues = new List<ValidationIssue>();

        // Act
        IniWriteRules.Apply(eds, issues);

        // Assert — dedicated keys are skipped like the writer skips them.
        issues.Select(issue => issue.Path).Should().BeEquivalentTo(
            "Comments.RemainingEntries[Bad=Key]",
            "DynamicChannels.RemainingEntries[Bad]Key]");
    }

    private static void Fill(
        Dictionary<string, OrderedStringDictionary> target,
        Dictionary<string, OrderedStringDictionary> source)
    {
        foreach (var entry in source)
            target[entry.Key] = entry.Value;
    }

    private static string Ini(params string[] lines) => string.Join("\n", lines) + "\n";

    private static List<string> SectionLines(string ini, string section)
    {
        var result = new List<string>();
        var inSection = false;
        foreach (var line in ini.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.StartsWith("[", StringComparison.Ordinal))
            {
                inSection = line.Equals("[" + section + "]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (inSection && line.Length > 0)
                result.Add(line);
        }

        return result;
    }

    /// <summary>Exposes the protected static section helpers kept for external subclasses.</summary>
    private sealed class ProbeWriter : EdsWriter
    {
        public static string WriteAll(ObjectDictionary od, ModuleInfo module, List<ToolInfo> tools)
        {
            var sb = new StringBuilder();
            WriteDummyUsage(sb, od);
            WriteObjectLists(sb, od);
            WriteSupportedModules(sb, new List<ModuleInfo> { module });
            WriteModuleInfo(sb, module);
            WriteTools(sb, tools);
            return sb.ToString();
        }
    }
}
