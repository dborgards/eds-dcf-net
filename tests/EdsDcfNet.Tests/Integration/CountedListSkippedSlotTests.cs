namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;

/// <summary>
/// Lenient parsing skips a numbered slot inside the count of a counted list when its value is
/// empty or not a valid index. The slot loads nothing into the model, so it is kept verbatim
/// (CiA 306-1 v1.4.0 § 6.2: additional entries support future extensions; § 6.6.3.1 numbers
/// list entries from 1 to the count).
/// </summary>
public class CountedListSkippedSlotTests
{
    // Built from single lines so the fixtures do not depend on the checkout's line endings.
    private static readonly string Base = Ini(
        "[DeviceInfo]",
        "VendorName=Vendor",
        "",
        "[MandatoryObjects]",
        "SupportedObjects=2",
        "1=0x1000",
        "2=bad",
        "",
        "[1000]",
        "ParameterName=Device type",
        "ObjectType=0x7",
        "DataType=0x0007",
        "AccessType=ro",
        "DefaultValue=0",
        "PDOMapping=0",
        "");

    private static readonly string Commissioning = Ini(
        "[DeviceComissioning]",
        "NodeID=2",
        "NodeName=Node",
        "Baudrate=250",
        "NetNumber=1",
        "NetworkName=Net",
        "CANopenManager=0",
        "");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteString_ObjectListSlotWithInvalidIndex_IsKeptLiterally(bool isDcf)
    {
        // Arrange
        var content = isDcf ? Base + Commissioning : Base;

        // Act
        var diagnostics = isDcf
            ? CanOpenFile.Dcf.ReadStringWithDiagnostics(content).Diagnostics
            : CanOpenFile.Eds.ReadStringWithDiagnostics(content).Diagnostics;
        var written = isDcf
            ? CanOpenFile.Dcf.WriteToString(CanOpenFile.Dcf.ReadString(content))
            : CanOpenFile.Eds.WriteToString(CanOpenFile.Eds.ReadString(content));

        // Assert — the warning for the skipped slot stays as before.
        diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.InvalidObjectIndex && d.Path == "MandatoryObjects.2");
        SectionLines(written, "MandatoryObjects").Should().Equal("SupportedObjects=1", "1=0x1000", "2=bad");
    }

    [Fact]
    public void ReadString_ObjectListSlotWithInvalidIndex_StrictParsing_StillThrows()
    {
        // Act
        var act = () => CanOpenFile.Eds.ReadString(Base, new CanOpenFileOptions { StrictParsing = true });

        // Assert
        act.Should().Throw<EdsParseException>();
    }

    [Fact]
    public void WriteString_ObjectListSlotWithEmptyValue_IsKeptLiterally()
    {
        // Arrange
        var content = Base.Replace("2=bad", "2=");

        // Act
        var written = CanOpenFile.Eds.WriteToString(CanOpenFile.Eds.ReadString(content));

        // Assert
        SectionLines(written, "MandatoryObjects").Should().Equal("SupportedObjects=1", "1=0x1000", "2=");
    }

    [Fact]
    public void WriteString_ObjectAddedOverSkippedSlot_GeneratedEntryWins()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Base);

        // Act
        eds.ObjectDictionary.MandatoryObjects.Add(0x1018);
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        SectionLines(written, "MandatoryObjects").Should().Equal("SupportedObjects=2", "1=0x1000", "2=0x1018");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteString_ObjectLinksSlotWithInvalidIndex_IsKeptLiterally(bool isDcf)
    {
        // Arrange
        var content = Base + Ini(
            "[1000ObjectLinks]",
            "ObjectLinks=2",
            "1=0x1000",
            "2=bad",
            "");
        if (isDcf)
            content += Commissioning;

        // Act
        var written = isDcf
            ? CanOpenFile.Dcf.WriteToString(CanOpenFile.Dcf.ReadString(content))
            : CanOpenFile.Eds.WriteToString(CanOpenFile.Eds.ReadString(content));

        // Assert
        SectionLines(written, "1000ObjectLinks").Should().Equal("ObjectLinks=1", "1=0x1000", "2=bad");
    }

    [Fact]
    public void WriteString_ModuleFixedObjectsSlotWithInvalidIndex_IsKeptLiterally()
    {
        // Arrange
        var content = Base + Ini(
            "[SupportedModules]",
            "NrOfEntries=1",
            "",
            "[M1ModuleInfo]",
            "ProductName=Module",
            "ProductVersion=1",
            "ProductRevision=0",
            "OrderCode=M-1",
            "",
            "[M1FixedObjects]",
            "NrOfEntries=2",
            "1=0x6423",
            "2=bad",
            "");

        // Act
        var written = CanOpenFile.Eds.WriteToString(CanOpenFile.Eds.ReadString(content));

        // Assert
        SectionLines(written, "M1FixedObjects").Should().Equal("NrOfEntries=1", "1=0x6423", "2=bad");
    }

    [Fact]
    public void WriteString_ConnectedModulesSlotWithInvalidNumber_IsKeptLiterally()
    {
        // Arrange
        var content = Base + Commissioning + Ini(
            "[ConnectedModules]",
            "NrOfEntries=2",
            "1=1",
            "2=bad",
            "");

        // Act
        var written = CanOpenFile.Dcf.WriteToString(CanOpenFile.Dcf.ReadString(content));

        // Assert
        SectionLines(written, "ConnectedModules").Should().Equal("NrOfEntries=1", "1=1", "2=bad");
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
}
