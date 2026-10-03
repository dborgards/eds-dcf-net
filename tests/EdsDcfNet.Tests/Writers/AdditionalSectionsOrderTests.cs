namespace EdsDcfNet.Tests.Writers;

using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using System.Text;
using EdsDcfNet.Writers;

/// <summary>
/// The INI writers keep the reader order of <c>AdditionalSections</c> and of their keys instead
/// of sorting them alphabetically (EDS, DCF and CPJ alike).
/// </summary>
public class AdditionalSectionsOrderTests
{
    public static TheoryData<string> Formats => new() { "eds", "dcf", "cpj" };

    private sealed class Opened
    {
        public Opened(Dictionary<string, Dictionary<string, string>> sections, Func<string> write)
        {
            Sections = sections;
            Write = write;
        }

        public Dictionary<string, Dictionary<string, string>> Sections { get; }

        public Func<string> Write { get; }
    }

    private static string Join(params string[] lines) => string.Join("\r\n", lines) + "\r\n";

    private static string[] Header(string format) => format switch
    {
        "eds" => new[]
        {
            "[FileInfo]", "FileName=t.eds", "[DeviceInfo]", "VendorName=V", ""
        },
        "dcf" => new[]
        {
            "[FileInfo]", "FileName=t.dcf", "[DeviceInfo]", "VendorName=V",
            "[DeviceComissioning]", "NodeID=1", ""
        },
        _ => new[] { "[Topology]", "NetName=N", "" }
    };

    private static Opened Open(string format, params string[] additional)
    {
        var text = Join(Header(format).Concat(additional).ToArray());
        switch (format)
        {
            case "eds":
                var eds = new EdsReader().ReadString(text);
                return new Opened(eds.AdditionalSections, () => new EdsWriter().GenerateString(eds));
            case "dcf":
                var dcf = new DcfReader().ReadString(text);
                return new Opened(dcf.AdditionalSections, () => new DcfWriter().GenerateString(dcf));
            default:
                var cpj = new CpjReader().ReadString(text);
                return new Opened(cpj.AdditionalSections, () => new CpjWriter().GenerateString(cpj));
        }
    }

    private static int Pos(string text, string token)
    {
        var index = text.IndexOf(token, StringComparison.Ordinal);
        index.Should().BeGreaterThanOrEqualTo(0, "'" + token + "' is expected in the output");
        return index;
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Write_SectionsZetaBeforeAlpha_KeepReaderOrder(string format)
    {
        // Arrange
        var file = Open(format, "[Zeta]", "A=1", "", "[Alpha]", "B=2", "", "[Middle]", "C=3");

        // Act
        var written = file.Write();

        // Assert
        Pos(written, "[Zeta]").Should().BeLessThan(Pos(written, "[Alpha]"));
        Pos(written, "[Alpha]").Should().BeLessThan(Pos(written, "[Middle]"));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Write_KeysZetaBeforeAlpha_KeepReaderOrder(string format)
    {
        // Arrange
        var file = Open(format, "[Vendor]", "Zeta=1", "Alpha=2", "Middle=3");

        // Act
        var written = file.Write();

        // Assert
        Pos(written, "Zeta=1").Should().BeLessThan(Pos(written, "Alpha=2"));
        Pos(written, "Alpha=2").Should().BeLessThan(Pos(written, "Middle=3"));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Write_RoundTrip_IsStableAndKeepsOrder(string format)
    {
        // Arrange
        var first = Open(format, "[Zeta]", "Z=1", "A=2", "", "[Alpha]", "Y=3", "B=4").Write();

        // Act
        var second = Open(format, "[Zeta]", "Z=1", "A=2", "", "[Alpha]", "Y=3", "B=4").Write();

        // Assert
        second.Should().Be(first);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Write_AddedSectionAndKey_FollowTheReadOnes(string format)
    {
        // Arrange
        var file = Open(format, "[Zeta]", "Z=1", "", "[Alpha]", "A=2");
        file.Sections["Aaa"] = new Dictionary<string, string> { ["K"] = "v" };
        file.Sections["Zeta"]["Aaa"] = "added";

        // Act
        var written = file.Write();

        // Assert
        Pos(written, "[Alpha]").Should().BeLessThan(Pos(written, "[Aaa]"));
        Pos(written, "Z=1").Should().BeLessThan(Pos(written, "Aaa=added"));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Write_RemovedSectionAndKey_AreSkipped(string format)
    {
        // Arrange
        var file = Open(format, "[Zeta]", "Z=1", "Y=2", "", "[Alpha]", "A=3");
        file.Sections.Remove("Zeta");
        file.Sections["Alpha"].Remove("A");
        file.Sections["Alpha"]["N"] = "new";

        // Act
        var written = file.Write();

        // Assert
        written.Should().NotContain("[Zeta]");
        written.Should().NotContain("A=3");
        written.Should().Contain("N=new");
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Write_RemovedAndReAddedUnderSameName_TakesReaderPosition(string format)
    {
        // Arrange
        var file = Open(format, "[Zeta]", "K1=1", "K2=2", "", "[Alpha]", "A=1", "", "[Omega]", "O=1");
        file.Sections.Remove("Zeta");
        file.Sections["Newer"] = new Dictionary<string, string> { ["N"] = "1" };
        file.Sections["Zeta"] = new Dictionary<string, string>
        {
            ["K2"] = "second",
            ["Extra"] = "x",
            ["K1"] = "first"
        };

        // Act
        var written = file.Write();

        // Assert
        Pos(written, "[Zeta]").Should().BeLessThan(Pos(written, "[Alpha]"));
        Pos(written, "[Omega]").Should().BeLessThan(Pos(written, "[Newer]"));
        Pos(written, "K1=first").Should().BeLessThan(Pos(written, "K2=second"));
        Pos(written, "K2=second").Should().BeLessThan(Pos(written, "Extra=x"));
    }

    [Fact]
    public void Write_Eds_UnlistedObjectSectionKeepsFilePositionAmongAdditionalSections()
    {
        // Arrange
        var text = Join(Header("eds").Concat(new[]
        {
            "[Zeta]", "A=1", "", "[2000]", "ParameterName=Orphan", "ObjectType=0x7", "", "[Alpha]", "B=2"
        }).ToArray());
        var eds = new EdsReader().ReadString(text);

        // Act
        var written = new EdsWriter().GenerateString(eds);

        // Assert
        Pos(written, "[Zeta]").Should().BeLessThan(Pos(written, "[2000]"));
        Pos(written, "[2000]").Should().BeLessThan(Pos(written, "[Alpha]"));
    }

    [Fact]
    public void Write_Dcf_SecondCommissioningSpellingKeepsFilePosition()
    {
        // Arrange
        var text = Join(
            "[FileInfo]", "FileName=t.dcf", "[DeviceInfo]", "VendorName=V",
            "[DeviceComissioning]", "NodeID=1", "",
            "[Zeta]", "A=1", "",
            "[DeviceCommissioning]", "NodeID=2", "",
            "[Alpha]", "B=2");
        var dcf = new DcfReader().ReadString(text);

        // Act
        var order = dcf.AdditionalSectionOrder.Sections(dcf.AdditionalSections).Select(s => s.Key).ToList();

        // Assert
        order.Should().Equal("Zeta", "DeviceCommissioning", "Alpha");
    }

    [Fact]
    public void ConvertToDcf_KeepsAdditionalSectionAndKeyOrder()
    {
        // Arrange
        var eds = new EdsReader().ReadString(Join(
            Header("eds").Concat(new[] { "[Zeta]", "Z=1", "A=2", "", "[Alpha]", "B=3" }).ToArray()));

        // Act
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 3, timestamp: new DateTime(2026, 1, 2), baudrate: 250);
        var written = new DcfWriter().GenerateString(dcf);

        // Assert
        Pos(written, "[Zeta]").Should().BeLessThan(Pos(written, "[Alpha]"));
        Pos(written, "Z=1").Should().BeLessThan(Pos(written, "A=2"));
    }

    [Fact]
    public void Write_ModelWithoutReaderOrder_UsesDictionaryEnumerationOrder()
    {
        // Arrange
        var cpj = new NodelistProject();
        cpj.AdditionalSections["Zeta"] = new Dictionary<string, string> { ["Z"] = "1", ["A"] = "2" };
        cpj.AdditionalSections["Alpha"] = new Dictionary<string, string> { ["B"] = "3" };

        // Act
        var written = new CpjWriter().GenerateString(cpj);

        // Assert
        Pos(written, "[Zeta]").Should().BeLessThan(Pos(written, "[Alpha]"));
        Pos(written, "Z=1").Should().BeLessThan(Pos(written, "A=2"));
    }

    [Fact]
    public void Capture_PlainDictionarySection_RecordsKeyEnumerationOrder()
    {
        // Arrange
        var additional = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Zeta"] = new Dictionary<string, string> { ["Z"] = "1", ["A"] = "2" }
        };
        var fileSections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Zeta"] = additional["Zeta"],
            ["Other"] = new Dictionary<string, string>()
        };
        var order = new AdditionalSectionOrder();

        // Act
        order.Capture(additional, fileSections);
        var keys = order.Entries("Zeta", additional["Zeta"]).Select(e => e.Key).ToList();

        // Assert
        keys.Should().Equal("Z", "A");
        order.Sections(additional).Select(s => s.Key).Should().Equal("Zeta");
    }

    [Fact]
    public void CopyFrom_Order_IsIndependentCopy()
    {
        // Arrange
        var additional = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["B"] = new Dictionary<string, string> { ["k2"] = "1", ["k1"] = "2" },
            ["A"] = new Dictionary<string, string>()
        };
        var source = new AdditionalSectionOrder();
        source.Capture(additional, additional);
        var copy = new AdditionalSectionOrder();
        copy.CopyFrom(new AdditionalSectionOrder());

        // Act
        copy.CopyFrom(source);
        source.Capture(new Dictionary<string, Dictionary<string, string>>(), additional);

        // Assert
        copy.Sections(additional).Select(s => s.Key).Should().Equal("B", "A");
        copy.Entries("B", additional["B"]).Select(e => e.Key).Should().Equal("k2", "k1");
    }

    private sealed class DerivedWriter : IniWriterBase
    {
        public static string Write(Dictionary<string, string> entries)
        {
            var sb = new StringBuilder();
            WriteAdditionalSection(sb, "Vendor", entries);
            return sb.ToString();
        }
    }

    [Fact]
    public void WriteAdditionalSection_DerivedWriterWithDictionary_WritesKeysInEnumerationOrder()
    {
        // Arrange
        var entries = new Dictionary<string, string> { ["Zeta"] = "1", ["Alpha"] = "2" };

        // Act
        var written = DerivedWriter.Write(entries);

        // Assert
        Pos(written, "Zeta=1").Should().BeLessThan(Pos(written, "Alpha=2"));
    }
}
