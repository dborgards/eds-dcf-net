namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;

/// <summary>
/// <c>[Comments]</c> (CiA 306-1 v1.4.0 § 6.6.5) with an empty <c>Line&lt;n&gt;</c>: the reader
/// does not store an empty line in <c>CommentLines</c>, so the entry is kept verbatim.
/// </summary>
public class EmptyCommentLineTests
{
    // Built from single lines so the fixture does not depend on the checkout's line endings.
    private static readonly string Eds = string.Join(
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
        "",
        "[Comments]",
        "Lines=1",
        "Line1=",
        "") + "\n";

    [Fact]
    public void WriteString_EmptyCommentLine_IsKeptOnRoundTrip()
    {
        // Act
        var eds = CanOpenFile.Eds.ReadString(Eds);
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        eds.Comments!.CommentLines.Should().BeEmpty();
        eds.Comments.RemainingEntries.Should().Equal(new Dictionary<string, string> { ["Line1"] = "" });
        SectionLines(written, "Comments").Should().Equal("Lines=1", "Line1=");
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
