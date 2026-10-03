namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;

/// <summary>
/// An empty compact-list entry (<c>1=</c>) is skipped by the reader even when sub-object 1
/// exists, so it maps onto nothing and is kept verbatim (CiA 306-1 v1.4.0 § 6.2).
/// </summary>
public class CompactListEmptyEntryTests
{
    // Built from single lines so the fixtures do not depend on the checkout's line endings.
    private static readonly string Eds = Ini(
        "[DeviceInfo]",
        "VendorName=Vendor",
        "",
        "[ManufacturerObjects]",
        "SupportedObjects=1",
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
        "1=",
        "");

    private static readonly string Dcf = Eds + Ini(
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
        "1=",
        "");

    [Fact]
    public void WriteString_EdsEmptyCompactNameEntry_IsKeptLiterally()
    {
        // Act
        var eds = CanOpenFile.Eds.ReadString(Eds);
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        eds.SectionRemainingEntries["2000Name"].Should().Equal(new Dictionary<string, string> { ["1"] = "" });
        SectionLines(written, "2000Name").Should().Equal("NrOfEntries=0", "1=");
    }

    [Fact]
    public void WriteString_DcfEmptyCompactValueEntry_IsKeptLiterally()
    {
        // Act
        var dcf = CanOpenFile.Dcf.ReadString(Dcf);
        var written = CanOpenFile.Dcf.WriteToString(dcf);

        // Assert
        dcf.SectionRemainingEntries["2000Value"].Should().Equal(new Dictionary<string, string> { ["1"] = "" });
        SectionLines(written, "2000Value").Should().Equal("NrOfEntries=0", "1=");
        SectionLines(written, "2000Name").Should().Equal("NrOfEntries=0", "1=");
    }

    [Fact]
    public void WriteString_EmptyEntryForGeneratedSubIndex_GeneratedEntryWins()
    {
        // Arrange — the caller gives sub-object 1 a name and a value, so the writer emits entry 1.
        var dcf = CanOpenFile.Dcf.ReadString(Dcf);
        dcf.ObjectDictionary.Objects[0x2000].SubObjects[1].ParameterName = "Custom";
        dcf.ObjectDictionary.Objects[0x2000].SubObjects[1].ParameterValue = "7";

        // Act
        var written = CanOpenFile.Dcf.WriteToString(dcf);

        // Assert
        SectionLines(written, "2000Name").Should().Equal("NrOfEntries=1", "1=Custom");
        SectionLines(written, "2000Value").Should().Equal("NrOfEntries=1", "1=7");
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
