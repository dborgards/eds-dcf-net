namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;
using EdsDcfNet.Parsers;

/// <summary>
/// A <c>[ToolN]</c> section counts as handled exactly when the reader parsed it, that is when
/// <c>N</c> is within <c>[Tools] Items</c> and the section has the canonical name
/// <c>Tool&lt;N&gt;</c>. Comparing with the number of parsed tools duplicated sections with gaps.
/// </summary>
public class ToolSectionClassificationTests
{
    private static readonly string Base = string.Join(
        "\n",
        "[DeviceInfo]",
        "VendorName=Vendor",
        "",
        "[MandatoryObjects]",
        "SupportedObjects=1",
        "1=0x1000",
        "",
        "[1000]",
        "ParameterName=Device type",
        "ObjectType=0x7",
        "DataType=0x0007",
        "AccessType=ro",
        "DefaultValue=0",
        "PDOMapping=0",
        "") + "\n";

    [Fact]
    public void ReadString_ToolGapWithVendorKey_WritesVendorKeyOnce()
    {
        // Arrange — Items=2, but only [Tool2] exists.
        var content = Base + string.Join("\n", "[Tools]", "Items=2", "", "[Tool2]", "Name=Second", "Command=cmd", "Vendor=X", "") + "\n";

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        eds.Tools.Should().ContainSingle().Which.Name.Should().Be("Second");
        eds.AdditionalSections.Should().NotContainKey("Tool2");
        written.Replace("\r\n", "\n").Split('\n').Count(line => line == "Vendor=X").Should().Be(1);
        written.Should().Contain("[Tool1]").And.NotContain("[Tool2]");
    }

    [Fact]
    public void ReadString_NonCanonicalToolSectionName_IsKeptAsAdditionalSection()
    {
        // Arrange — the reader looks up [Tool1], so [Tool01] is never parsed.
        var content = Base + string.Join("\n", "[Tools]", "Items=1", "", "[Tool01]", "Name=Padded", "") + "\n";

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);

        // Assert
        eds.Tools.Should().BeEmpty();
        eds.AdditionalSections.Should().ContainKey("Tool01");
    }

    [Theory]
    [InlineData("Tool1", true)]
    [InlineData("tool2", true)]
    [InlineData("Tool3", false)]
    [InlineData("Tool0", false)]
    [InlineData("Tool01", false)]
    [InlineData("Toolx", false)]
    [InlineData("Tool", false)]
    [InlineData("Tools", false)]
    [InlineData("Other", false)]
    public void IsParsedToolSection_SectionName_MatchesParserLookup(string sectionName, bool expected)
    {
        // Arrange
        var sections = IniParser.ParseString(string.Join("\n", "[Tools]", "Items=2", "") + "\n");

        // Act / Assert
        CanOpenSectionParsers.IsParsedToolSection(sections, sectionName).Should().Be(expected);
    }
}
