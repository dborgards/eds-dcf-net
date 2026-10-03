namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;

/// <summary>
/// The remaining EDS/DCF counter and version keys are read leniently: a malformed
/// <c>[Comments]</c> / <c>[MxComments]</c> <c>Lines</c>, <c>[Tools]</c> <c>Items</c>,
/// <c>[SupportedModules]</c> / <c>[ConnectedModules]</c> <c>NrOfEntries</c>, or
/// <c>[MxModuleInfo]</c> <c>ProductVersion</c> / <c>ProductRevision</c> is reported and replaced by
/// the absent-key default; the numbered entries stay as kept entries. Strict mode throws with the
/// same code, section and line. Unknown boolean and access-type tokens carry their section, key and
/// line.
/// </summary>
public class LenientCounterKeyTests
{
    private static readonly CanOpenFileOptions Strict = new() { StrictParsing = true };

    // ---------------------------------------------------------------------------------------
    // [Comments] Lines (CiA 306-1 Table 9, UNSIGNED16)
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("abc")]
    [InlineData("65536")]
    [InlineData("-1")]
    public void ReadString_MalformedCommentsLines_ReportsAndKeepsLines(string lines)
    {
        // Arrange
        var content = Eds("[Comments]\nLines=" + lines + "\nLine1=First\nLine2=Second\n");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        var comments = result.Model.Comments!;
        comments.Lines.Should().Be(0);
        comments.CommentLines.Should().BeEmpty();
        comments.RemainingEntries["Line1"].Should().Be("First");
        comments.RemainingEntries["Line2"].Should().Be("Second");
        result.Model.ObjectDictionary.Objects.Should().ContainKey(0x1000);
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.InvalidCommentLineCount)
            .Which.Should().Match<ParseDiagnostic>(d =>
                d.Path == "Comments.Lines" &&
                d.RawValue == lines &&
                d.CoercedTo == "0" &&
                d.Line != null);
    }

    [Fact]
    public void ReadString_CommentsLinesAtMaxValue_ReadsLines()
    {
        // Arrange
        var content = Eds("[Comments]\nLines=65535\nLine1=First\n");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.Comments!.Lines.Should().Be(65535);
        result.Model.Comments.CommentLines[1].Should().Be("First");
        result.Diagnostics.Should().NotContain(d => d.Code == ParseDiagnosticCodes.InvalidCommentLineCount);
    }

    [Fact]
    public void ReadString_MalformedCommentsLines_StrictParsing_Throws()
    {
        // Arrange
        var content = Eds("[Comments]\nLines=abc\nLine1=First\n");

        // Act
        var act = () => CanOpenFile.Eds.ReadString(content, Strict);

        // Assert
        act.Should().Throw<EdsParseException>().Which.Should().Match<EdsParseException>(e =>
            e.Code == ParseDiagnosticCodes.InvalidCommentLineCount &&
            e.SectionName == "Comments" &&
            e.LineNumber != null);
    }

    [Fact]
    public void WriteToString_MalformedCommentsLines_RoundTripsCommentLines()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Eds("[Comments]\nLines=abc\nLine1=First\n"));

        // Act
        var again = CanOpenFile.Eds.ReadString(CanOpenFile.Eds.WriteToString(eds));

        // Assert
        again.Comments!.RemainingEntries["Line1"].Should().Be("First");
    }

    // ---------------------------------------------------------------------------------------
    // [MxComments] Lines (Table 15) and [MxModuleInfo] ProductVersion / ProductRevision (Table 14)
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ReadString_MalformedModuleCommentsLines_ReportsAndKeepsModule()
    {
        // Arrange
        var content = Eds(Module("ProductVersion=1\nProductRevision=0\n") + "[M1Comments]\nLines=abc\nLine1=Module comment\n");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        var module = result.Model.SupportedModules.Should().ContainSingle().Subject;
        module.ProductName.Should().Be("Module");
        module.Comments!.Lines.Should().Be(0);
        module.Comments.RemainingEntries["Line1"].Should().Be("Module comment");
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.InvalidCommentLineCount)
            .Which.Path.Should().Be("M1Comments.Lines");
    }

    [Fact]
    public void ReadString_MalformedModuleCommentsLines_StrictParsing_Throws()
    {
        // Arrange
        var content = Eds(Module(string.Empty) + "[M1Comments]\nLines=abc\n");

        // Act
        var act = () => CanOpenFile.Eds.ReadString(content, Strict);

        // Assert
        act.Should().Throw<EdsParseException>().Which.Should().Match<EdsParseException>(e =>
            e.Code == ParseDiagnosticCodes.InvalidCommentLineCount &&
            e.SectionName == "M1Comments");
    }

    [Theory]
    [InlineData("ProductVersion", "256", (byte)1, (byte)0)]
    [InlineData("ProductVersion", "abc", (byte)1, (byte)0)]
    [InlineData("ProductRevision", "0x1G", (byte)1, (byte)0)]
    [InlineData("ProductRevision", "-1", (byte)1, (byte)0)]
    public void ReadString_MalformedModuleVersion_ReportsAndUsesDefault(
        string key, string value, byte expectedVersion, byte expectedRevision)
    {
        // Arrange
        var content = Eds(Module(key + "=" + value + "\n"));

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        var module = result.Model.SupportedModules.Should().ContainSingle().Subject;
        module.ProductVersion.Should().Be(expectedVersion);
        module.ProductRevision.Should().Be(expectedRevision);
        module.ProductName.Should().Be("Module");
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.InvalidModuleVersion)
            .Which.Should().Match<ParseDiagnostic>(d =>
                d.Path == "M1ModuleInfo." + key &&
                d.RawValue == value &&
                d.Line != null);
    }

    [Fact]
    public void ReadString_ModuleVersionAtMaxValue_ReadsValue()
    {
        // Arrange
        var content = Eds(Module("ProductVersion=255\nProductRevision=0xFF\n"));

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.SupportedModules[0].ProductVersion.Should().Be(255);
        result.Model.SupportedModules[0].ProductRevision.Should().Be(255);
        result.Diagnostics.Should().NotContain(d => d.Code == ParseDiagnosticCodes.InvalidModuleVersion);
    }

    [Fact]
    public void ReadString_MalformedModuleVersion_StrictParsing_Throws()
    {
        // Arrange
        var content = Eds(Module("ProductRevision=abc\n"));

        // Act
        var act = () => CanOpenFile.Eds.ReadString(content, Strict);

        // Assert
        act.Should().Throw<EdsParseException>().Which.Should().Match<EdsParseException>(e =>
            e.Code == ParseDiagnosticCodes.InvalidModuleVersion &&
            e.SectionName == "M1ModuleInfo" &&
            e.LineNumber != null);
    }

    // ---------------------------------------------------------------------------------------
    // [SupportedModules] NrOfEntries (Table 13) and [ConnectedModules] NrOfEntries (Table 18)
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("abc")]
    [InlineData("65536")]
    public void ReadString_MalformedSupportedModulesCount_ReportsAndKeepsEntries(string count)
    {
        // Arrange
        var content = Eds(Module(string.Empty).Replace("NrOfEntries=1", "NrOfEntries=" + count));

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.SupportedModules.Should().BeEmpty();
        result.Model.SectionRemainingEntries["SupportedModules"]["1"].Should().Be("0x0001");
        result.Model.AdditionalSections["M1ModuleInfo"]["ProductName"].Should().Be("Module");
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.InvalidModuleCount)
            .Which.Should().Match<ParseDiagnostic>(d =>
                d.Path == "SupportedModules.NrOfEntries" &&
                d.RawValue == count &&
                d.CoercedTo == "0");
    }

    [Fact]
    public void ReadString_MalformedSupportedModulesCount_StrictParsing_Throws()
    {
        // Arrange
        var content = Eds(Module(string.Empty).Replace("NrOfEntries=1", "NrOfEntries=abc"));

        // Act
        var act = () => CanOpenFile.Eds.ReadString(content, Strict);

        // Assert
        act.Should().Throw<EdsParseException>().Which.Should().Match<EdsParseException>(e =>
            e.Code == ParseDiagnosticCodes.InvalidModuleCount &&
            e.SectionName == "SupportedModules" &&
            e.LineNumber != null);
    }

    [Fact]
    public void WriteToString_MalformedSupportedModulesCount_RoundTripsModuleSections()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(
            Eds(Module(string.Empty).Replace("NrOfEntries=1", "NrOfEntries=abc")));

        // Act
        var again = CanOpenFile.Eds.ReadString(CanOpenFile.Eds.WriteToString(eds));

        // Assert
        again.SectionRemainingEntries["SupportedModules"]["1"].Should().Be("0x0001");
        again.AdditionalSections["M1ModuleInfo"]["ProductName"].Should().Be("Module");
    }

    [Fact]
    public void ReadString_MalformedConnectedModulesCount_ReportsAndKeepsEntries()
    {
        // Arrange
        var content = Dcf("[ConnectedModules]\nNrOfEntries=abc\n1=3\n2=3\n");

        // Act
        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.ConnectedModules.Should().BeEmpty();
        result.Model.SectionRemainingEntries["ConnectedModules"]["1"].Should().Be("3");
        result.Model.SectionRemainingEntries["ConnectedModules"]["2"].Should().Be("3");
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.InvalidModuleCount)
            .Which.Path.Should().Be("ConnectedModules.NrOfEntries");
    }

    [Fact]
    public void ReadString_ConnectedModulesCountAtMaxValue_ReadsList()
    {
        // Arrange
        var content = Dcf("[ConnectedModules]\nNrOfEntries=65535\n1=3\n");

        // Act
        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.ConnectedModules.Should().Equal(3);
        result.Diagnostics.Should().NotContain(d => d.Code == ParseDiagnosticCodes.InvalidModuleCount);
    }

    [Fact]
    public void ReadString_MalformedConnectedModulesCount_StrictParsing_Throws()
    {
        // Arrange
        var content = Dcf("[ConnectedModules]\nNrOfEntries=abc\n1=3\n");

        // Act
        var act = () => CanOpenFile.Dcf.ReadString(content, Strict);

        // Assert
        act.Should().Throw<EdsParseException>().Which.Should().Match<EdsParseException>(e =>
            e.Code == ParseDiagnosticCodes.InvalidModuleCount &&
            e.SectionName == "ConnectedModules");
    }

    [Fact]
    public void WriteToString_MalformedConnectedModulesCount_RoundTripsEntries()
    {
        // Arrange
        var dcf = CanOpenFile.Dcf.ReadString(Dcf("[ConnectedModules]\nNrOfEntries=abc\n1=3\n"));

        // Act
        var again = CanOpenFile.Dcf.ReadString(CanOpenFile.Dcf.WriteToString(dcf));

        // Assert
        again.SectionRemainingEntries["ConnectedModules"]["1"].Should().Be("3");
    }

    // ---------------------------------------------------------------------------------------
    // [Tools] Items (CiA 306-3)
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("abc")]
    [InlineData("256")]
    public void ReadString_MalformedToolsItems_ReportsAndKeepsToolSections(string items)
    {
        // Arrange
        var content = Eds("[Tools]\nItems=" + items + "\n\n[Tool1]\nName=Configurator\nCommand=cfg.exe\n");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.Tools.Should().BeEmpty();
        result.Model.AdditionalSections["Tool1"]["Name"].Should().Be("Configurator");
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.InvalidToolCount)
            .Which.Should().Match<ParseDiagnostic>(d =>
                d.Path == "Tools.Items" &&
                d.RawValue == items &&
                d.CoercedTo == "0");
    }

    [Fact]
    public void ReadString_ToolsItemsAtMaxValue_ReadsTools()
    {
        // Arrange
        var content = Eds("[Tools]\nItems=255\n\n[Tool1]\nName=Configurator\nCommand=cfg.exe\n");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.Tools.Should().ContainSingle().Which.Name.Should().Be("Configurator");
        result.Model.AdditionalSections.Should().NotContainKey("Tool1");
        result.Diagnostics.Should().NotContain(d => d.Code == ParseDiagnosticCodes.InvalidToolCount);
    }

    [Fact]
    public void ReadString_MalformedToolsItems_StrictParsing_Throws()
    {
        // Arrange
        var content = Eds("[Tools]\nItems=abc\n\n[Tool1]\nName=Configurator\n");

        // Act
        var act = () => CanOpenFile.Eds.ReadString(content, Strict);

        // Assert
        act.Should().Throw<EdsParseException>().Which.Should().Match<EdsParseException>(e =>
            e.Code == ParseDiagnosticCodes.InvalidToolCount &&
            e.SectionName == "Tools" &&
            e.LineNumber != null);
    }

    [Fact]
    public void WriteToString_MalformedToolsItems_RoundTripsToolSection()
    {
        // Arrange
        var dcf = CanOpenFile.Dcf.ReadString(Dcf("[Tools]\nItems=abc\n\n[Tool1]\nName=Configurator\n"));

        // Act
        var again = CanOpenFile.Dcf.ReadString(CanOpenFile.Dcf.WriteToString(dcf));

        // Assert
        again.AdditionalSections["Tool1"]["Name"].Should().Be("Configurator");
    }

    // ---------------------------------------------------------------------------------------
    // Unknown boolean / access-type tokens carry section, key and line
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ReadString_UnknownDynamicChannelDirToken_ReportsSectionKeyAndLine()
    {
        // Arrange
        var content = Eds("[DynamicChannels]\nNrOfSeg=1\nType1=0x0007\nDir1=sideways\nRange1=0xA080-0xA0BF\nPPOffset1=0\n");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.DynamicChannels!.Segments[0].Dir.Should().Be(AccessType.ReadOnly);
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.UnknownAccessTypeToken)
            .Which.Should().Match<ParseDiagnostic>(d =>
                d.Path == "DynamicChannels.Dir1" &&
                d.RawValue == "sideways" &&
                d.Line != null);
    }

    [Fact]
    public void ReadString_UnknownDynamicChannelDirToken_StrictParsing_ThrowsWithSection()
    {
        // Arrange
        var content = Eds("[DynamicChannels]\nNrOfSeg=1\nType1=0x0007\nDir1=sideways\n");

        // Act
        var act = () => CanOpenFile.Eds.ReadString(content, Strict);

        // Assert
        act.Should().Throw<EdsParseException>().Which.Should().Match<EdsParseException>(e =>
            e.Code == ParseDiagnosticCodes.UnknownAccessTypeToken &&
            e.SectionName == "DynamicChannels" &&
            e.LineNumber != null);
    }

    [Fact]
    public void ReadString_UnknownObjectAccessTypeToken_ReportsSectionAndKey()
    {
        // Arrange
        var content = Eds(string.Empty).Replace("AccessType=ro", "AccessType=sideways");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.UnknownAccessTypeToken)
            .Which.Should().Match<ParseDiagnostic>(d => d.Path == "1000.AccessType" && d.Line != null);
    }

    [Fact]
    public void ReadString_UnknownBooleanTokens_ReportSectionKeyAndLine()
    {
        // Arrange
        var content = Eds(string.Empty)
            .Replace("ProductName=Test", "ProductName=Test\nBaudRate_10=maybe")
            .Replace("PDOMapping=0", "PDOMapping=perhaps");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Diagnostics.Where(d => d.Code == ParseDiagnosticCodes.UnknownBooleanToken)
            .Select(d => d.Path).Should().BeEquivalentTo("DeviceInfo.BaudRate_10", "1000.PDOMapping");
        result.Diagnostics.Where(d => d.Code == ParseDiagnosticCodes.UnknownBooleanToken)
            .Should().OnlyContain(d => d.Line != null);
    }

    [Fact]
    public void ReadString_UnknownBooleanToken_StrictParsing_ThrowsWithSection()
    {
        // Arrange
        var content = Dcf(string.Empty).Replace("CANopenManager=0", "CANopenManager=maybe");

        // Act
        var act = () => CanOpenFile.Dcf.ReadString(content, Strict);

        // Assert
        act.Should().Throw<EdsParseException>().Which.Should().Match<EdsParseException>(e =>
            e.Code == ParseDiagnosticCodes.UnknownBooleanToken &&
            e.SectionName == "DeviceComissioning" &&
            e.LineNumber != null);
    }

    [Fact]
    public void ParseBoolean_PublicApiUnknownToken_ReportsWithoutPath()
    {
        // Arrange
        using var scope = ParseDiagnosticScope.Enter();

        // Act
        var value = global::EdsDcfNet.Utilities.ValueConverter.ParseBoolean("maybe");

        // Assert
        value.Should().BeFalse();
        scope.Diagnostics.Should().ContainSingle().Which.Path.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------

    private static string Module(string moduleInfoEntries)
        => "[SupportedModules]\nNrOfEntries=1\n1=0x0001\n\n[M1ModuleInfo]\nProductName=Module\n"
           + moduleInfoEntries + "OrderCode=M-1\n\n";

    private static string Eds(string extraSections)
        => "[DeviceInfo]\nVendorName=Test\nProductName=Test\n\n"
           + "[MandatoryObjects]\nSupportedObjects=1\n1=0x1000\n\n"
           + "[1000]\nParameterName=Device Type\nObjectType=0x7\nDataType=0x0007\nAccessType=ro\nDefaultValue=0\nPDOMapping=0\n\n"
           + extraSections;

    private static string Dcf(string extraSections)
        => "[DeviceInfo]\nVendorName=Test\nProductName=Test\n\n"
           + "[DeviceComissioning]\nNodeID=5\nNodeName=Node\nBaudrate=500\nNetNumber=1\nNetworkName=Net\nCANopenManager=0\n\n"
           + "[MandatoryObjects]\nSupportedObjects=1\n1=0x1000\n\n"
           + "[1000]\nParameterName=Device Type\nObjectType=0x7\nDataType=0x0007\nAccessType=ro\nDefaultValue=0\nPDOMapping=0\n\n"
           + extraSections;
}
