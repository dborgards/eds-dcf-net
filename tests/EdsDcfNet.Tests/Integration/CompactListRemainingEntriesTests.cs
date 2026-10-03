namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;
using EdsDcfNet.Models;

/// <summary>
/// A numbered entry of a compact list (<c>[xxxxName]</c>, DCF <c>[xxxxValue]</c> /
/// <c>[xxxxDenotation]</c>) that addresses no sub-object is not applied by the reader and
/// must be kept verbatim (CiA 306-1 v1.4.0 § 6.2: additional entries support future
/// extensions; § 6.6.3.4 / § 7.3.4 compact storage).
/// </summary>
public class CompactListRemainingEntriesTests
{
    private const string Content = """
        [DeviceInfo]
        VendorName=Vendor

        [ManufacturerObjects]
        SupportedObjects=1
        1=0x2000

        [2000]
        ParameterName=Compact
        ObjectType=0x8
        DataType=0x0007
        AccessType=rw
        DefaultValue=0
        PDOMapping=0
        CompactSubObj=1

        [2000Name]
        NrOfEntries=1
        1=First
        2=FutureName

        """;

    private const string DcfLists = """
        [2000Value]
        NrOfEntries=1
        1=5
        2=FutureValue

        [2000Denotation]
        NrOfEntries=1
        1=Den
        2=FutureDenotation

        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteString_CompactNameEntryWithoutSubObject_IsKeptLiterally(bool isDcf)
    {
        // Act
        var written = isDcf
            ? CanOpenFile.Dcf.WriteToString(CanOpenFile.Dcf.ReadString(Content + DcfLists))
            : CanOpenFile.Eds.WriteToString(CanOpenFile.Eds.ReadString(Content));

        // Assert
        SectionLines(written, "2000Name").Should().Equal("NrOfEntries=1", "1=First", "2=FutureName");
        if (isDcf)
        {
            SectionLines(written, "2000Value").Should().Equal("NrOfEntries=1", "1=5", "2=FutureValue");
            SectionLines(written, "2000Denotation").Should().Equal("NrOfEntries=1", "1=Den", "2=FutureDenotation");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteString_SubObjectAddedForKeptCompactEntry_GeneratedEntryWins(bool isDcf)
    {
        // Arrange — the caller adds sub-object 2, so the writer now generates entry 2 itself.
        var model = isDcf
            ? (object)CanOpenFile.Dcf.ReadString(Content + DcfLists)
            : CanOpenFile.Eds.ReadString(Content);
        var obj = isDcf
            ? ((DeviceConfigurationFile)model).ObjectDictionary.Objects[0x2000]
            : ((ElectronicDataSheet)model).ObjectDictionary.Objects[0x2000];
        var sub2 = new CanOpenSubObject
        {
            SubIndex = 2,
            ParameterName = "Generated",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadWrite,
            DefaultValue = "0",
            ParameterValue = isDcf ? "7" : null,
            Denotation = isDcf ? "GenDen" : null
        };
        obj.SubObjects[2] = sub2;
        obj.CompactSubObj = 2;

        // Act
        var written = isDcf
            ? CanOpenFile.Dcf.WriteToString((DeviceConfigurationFile)model)
            : CanOpenFile.Eds.WriteToString((ElectronicDataSheet)model);

        // Assert
        var names = SectionLines(written, "2000Name");
        names.Count(line => line.StartsWith("2=", StringComparison.Ordinal)).Should().Be(1);
        names.Should().Contain("2=Generated");
        if (isDcf)
        {
            SectionLines(written, "2000Value").Should().ContainSingle(line => line.StartsWith("2=", StringComparison.Ordinal))
                .Which.Should().Be("2=7");
            SectionLines(written, "2000Denotation").Should().ContainSingle(line => line.StartsWith("2=", StringComparison.Ordinal))
                .Which.Should().Be("2=GenDen");
        }
    }

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
