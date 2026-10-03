namespace EdsDcfNet.Tests.Parsers;

using System.Globalization;
using System.Text;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Tests.Utilities;

/// <summary>
/// CiA 306-3 v1.2.0 § 5.2.2 Table 2: <c>PPOffsetX</c> is <c>[offset]</c> or, for BOOLEAN
/// segments, <c>[offset], [address difference]</c> (separated by a comma). Fig. 2 and Fig. 3
/// show <c>PPOffset1=0, 1</c>; Fig. 4 (DCF) shows <c>PPOffset1=0,1</c>.
/// Lines are assembled with <see cref="StringBuilder"/> so the fixtures do not depend on the
/// checkout line ending.
/// </summary>
public class DynamicChannelPpOffsetTupleTests
{
    private static readonly CanOpenFileOptions Strict = new() { StrictParsing = true };

    private static readonly string[] HeaderLines =
    {
        "[FileInfo]",
        "FileName=tuple.eds",
        "EDSVersion=4.0",
        "[DeviceInfo]",
        "VendorName=Test",
    };

    /// <summary>
    /// Excerpt of CiA 306-3 v1.2.0 Fig. 3 (segments 1, 2, 18 and 19, verbatim keys and values).
    /// Segments 1 and 19 are the BOOLEAN segments with <c>PPOffsetX=0, 1</c>; they are
    /// renumbered 1 to 4 here.
    /// </summary>
    private static readonly string[] Figure3Excerpt =
    {
        "[DynamicChannels]",
        "NrOfSeg=4",
        "Type1=1",
        "Dir1=ro",
        "Range1=0xA080-0xA0BF",
        "PPOffset1=0, 1",
        "Type2=2",
        "Dir2=ro",
        "Range2=0xA000-0xA03F",
        "PPOffset2=0",
        "Type3=8",
        "Dir3=ro",
        "Range3=0xA240-0xA27F",
        "PPOffset3=0",
        "Type4=1",
        "Dir4=rww",
        "Range4=0xA500-0xA53F",
        "PPOffset4=0, 1",
    };

    private static string Eds(string[] sectionLines, string[]? headerLines = null)
    {
        var sb = new StringBuilder();
        foreach (var line in headerLines ?? HeaderLines)
            sb.Append(line).Append('\n');
        foreach (var line in sectionLines)
            sb.Append(line).Append('\n');
        return sb.ToString();
    }

    private static string Dcf(string[] sectionLines)
    {
        var header = new List<string> { HeaderLines[0], "FileRevision=1" };
        header.AddRange(HeaderLines.Skip(1));
        return Eds(sectionLines, header.ToArray());
    }

    private static string[] SegmentLines(string ppOffsetValue) => new[]
    {
        "[DynamicChannels]",
        "NrOfSeg=1",
        "Type1=1",
        "Dir1=ro",
        "Range1=0xA080-0xA0BF",
        "PPOffset1=" + ppOffsetValue,
    };

    private static string SingleSegment(string ppOffsetValue) => Eds(SegmentLines(ppOffsetValue));

    [Fact]
    public void ReadString_SpecFigure3TupleWithSpace_ReadsOffsetAndAddressDifference()
    {
        // CiA 306-3 v1.2.0 § 5.2.2 Fig. 3: "PPOffset1=0, 1"
        var eds = CanOpenFile.Eds.ReadString(Eds(Figure3Excerpt));

        var segments = eds.DynamicChannels!.Segments;
        segments[0].PPOffset.Should().Be(0u);
        segments[0].PPOffsetAddressDifference.Should().Be(1u);
        segments[1].PPOffset.Should().Be(0u);
        segments[1].PPOffsetAddressDifference.Should().BeNull();
        segments[3].PPOffsetAddressDifference.Should().Be(1u);
    }

    [Fact]
    public void ReadString_SpecFigure4TupleWithoutSpace_ReadsOffsetAndAddressDifference()
    {
        // CiA 306-3 v1.2.0 § 5.2.3.2 Fig. 4 (DCF): "PPOffset1=0,1"
        var eds = CanOpenFile.Eds.ReadString(SingleSegment("0,1"));

        var segment = eds.DynamicChannels!.Segments.Should().ContainSingle().Subject;
        segment.PPOffset.Should().Be(0u);
        segment.PPOffsetAddressDifference.Should().Be(1u);
    }

    [Fact]
    public void ReadString_TupleWithPaddingAndHex_TrimsAndParsesBothValues()
    {
        var eds = CanOpenFile.Eds.ReadString(SingleSegment("  0x10 ,\t 0x08  "));

        var segment = eds.DynamicChannels!.Segments.Single();
        segment.PPOffset.Should().Be(16u);
        segment.PPOffsetAddressDifference.Should().Be(8u);
    }

    [Fact]
    public void ReadString_SingleValue_LeavesAddressDifferenceNull()
    {
        var eds = CanOpenFile.Eds.ReadString(SingleSegment("0x20"));

        var segment = eds.DynamicChannels!.Segments.Single();
        segment.PPOffset.Should().Be(32u);
        segment.PPOffsetAddressDifference.Should().BeNull();
    }

    [Fact]
    public void ReadString_EmptyValue_StaysZeroWithoutAddressDifference()
    {
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(SingleSegment(string.Empty));

        result.HasDiagnostics.Should().BeFalse();
        var segment = result.Model.DynamicChannels!.Segments.Single();
        segment.PPOffset.Should().Be(0u);
        segment.PPOffsetAddressDifference.Should().BeNull();
    }

    [Fact]
    public void ReadString_DcfWithTuple_ReadsOffsetAndAddressDifference()
    {
        var dcf = CanOpenFile.Dcf.ReadString(Dcf(Figure3Excerpt));

        dcf.DynamicChannels!.Segments[0].PPOffsetAddressDifference.Should().Be(1u);
    }

    [Fact]
    public void WriteToString_Figure3Excerpt_WritesTupleOnlyForBooleanSegmentsAndRoundTrips()
    {
        var eds = CanOpenFile.Eds.ReadString(Eds(Figure3Excerpt));

        var written = CanOpenFile.Eds.WriteToString(eds);

        var lines = written.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        lines.Should().Contain("PPOffset1=0, 1");
        lines.Should().Contain("PPOffset2=0");
        lines.Should().Contain("PPOffset3=0");
        lines.Should().Contain("PPOffset4=0, 1");
        var reread = CanOpenFile.Eds.ReadString(written);
        reread.DynamicChannels!.Segments.Should().BeEquivalentTo(eds.DynamicChannels!.Segments);
        CanOpenFile.Eds.WriteToString(reread).Should().Be(written);
    }

    [Fact]
    public void WriteToString_AddressDifferenceNotSet_WritesOffsetOnly()
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.DynamicChannels = new DynamicChannels();
        eds.DynamicChannels.Segments.Add(new DynamicChannelSegment { Type = 1, Range = "0xA080-0xA0BF", PPOffset = 4 });

        var written = CanOpenFile.Eds.WriteToString(eds);

        written.Should().Contain("PPOffset1=4");
        written.Should().NotContain("PPOffset1=4,");
    }

    [Fact]
    public void WriteToString_AddressDifferenceSetOnProgrammaticModel_WritesTuple()
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.DynamicChannels = new DynamicChannels();
        eds.DynamicChannels.Segments.Add(new DynamicChannelSegment
        {
            Type = 1,
            Range = "0xA080-0xA0BF",
            PPOffset = 4,
            PPOffsetAddressDifference = 8,
        });

        var written = CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        written.Should().Contain("PPOffset1=4, 8");
    }

    [Fact]
    public void WriteToString_DcfWithTuple_RoundTrips()
    {
        var dcf = CanOpenFile.Dcf.ReadString(Dcf(Figure3Excerpt));

        var written = CanOpenFile.Dcf.WriteToString(dcf);

        written.Should().Contain("PPOffset1=0, 1");
        CanOpenFile.Dcf.ReadString(written).DynamicChannels!.Segments[0]
            .PPOffsetAddressDifference.Should().Be(1u);
    }

    [Fact]
    public void ConvertToDcf_SegmentWithAddressDifference_KeepsValueAndWritesIt()
    {
        var eds = CanOpenFile.Eds.ReadString(Eds(Figure3Excerpt));

        var dcf = CanOpenFile.Eds.ConvertToDcf(
            eds,
            nodeId: 5,
            timestamp: new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            baudrate: 250,
            nodeName: "Node");

        dcf.DynamicChannels!.Segments[0].PPOffsetAddressDifference.Should().Be(1u);
        dcf.DynamicChannels.Segments[1].PPOffsetAddressDifference.Should().BeNull();
        CanOpenFile.Dcf.WriteToString(dcf).Should().Contain("PPOffset1=0, 1");
    }

    [Fact]
    public void WriteToString_XddWithAddressDifference_IgnoresAddressDifference()
    {
        // The XDD/XDC dynamicChannel element has no counterpart: addressOffset is the offset only.
        var eds = CanOpenFile.Eds.ReadString(Eds(Figure3Excerpt));

        var withTuple = CanOpenFile.Xdd.WriteToString(eds);
        eds.DynamicChannels!.Segments.ForEach(s => s.PPOffsetAddressDifference = null);
        var withoutTuple = CanOpenFile.Xdd.WriteToString(eds);

        withTuple.Should().Be(withoutTuple);
    }

    [Theory]
    [InlineData("0, 1, 2")]
    [InlineData("0,")]
    [InlineData(",1")]
    [InlineData("abc")]
    [InlineData("0, abc")]
    [InlineData("-1")]
    [InlineData("0, -1")]
    [InlineData("4294967296")]
    [InlineData("0, 0x100000000")]
    [InlineData("$NODEID+1")]
    public void ReadStringWithDiagnostics_InvalidPpOffset_LenientFallsBackToZeroWithDiagnostic(string raw)
    {
        var content = SingleSegment(raw);

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.InvalidDynamicChannelPpOffset);
        diagnostic.Severity.Should().Be(ParseSeverity.Warning);
        diagnostic.Path.Should().Be("DynamicChannels.PPOffset1");
        diagnostic.RawValue.Should().Be(raw);
        diagnostic.CoercedTo.Should().Be("0");
        var segment = result.Model.DynamicChannels!.Segments.Single();
        segment.PPOffset.Should().Be(0u);
        segment.PPOffsetAddressDifference.Should().BeNull();
    }

    [Theory]
    [InlineData("0, 1, 2")]
    [InlineData("0,")]
    [InlineData(",1")]
    [InlineData("abc")]
    [InlineData("0, abc")]
    [InlineData("4294967296")]
    public void ReadStringWithDiagnostics_InvalidPpOffset_StrictThrowsWithCodeAndLocation(string raw)
    {
        var content = SingleSegment(raw);

        var act = () => CanOpenFile.Eds.ReadStringWithDiagnostics(content, Strict);

        var ex = act.Should().Throw<EdsParseException>().Which;
        ex.Code.Should().Be(ParseDiagnosticCodes.InvalidDynamicChannelPpOffset);
        ex.SectionName.Should().Be("DynamicChannels");
        ex.LineNumber.Should().NotBeNull();
    }

    [Fact]
    public void ReadString_InvalidPpOffsetFacadeLenient_DoesNotThrow()
    {
        var act = () => CanOpenFile.Eds.ReadString(SingleSegment("0, 1, 2"));

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("4294967295, 4294967295")]
    [InlineData("0xFFFFFFFF, 0xFFFFFFFF")]
    [InlineData("0xffffffff,0xFFFFFFFF")]
    public void ReadString_PpOffsetTupleAtMaxValue_ReadsBothValuesAsUInt32Max(string raw)
    {
        var eds = CanOpenFile.Eds.ReadString(SingleSegment(raw));

        var segment = eds.DynamicChannels!.Segments.Single();
        segment.PPOffset.Should().Be(uint.MaxValue);
        segment.PPOffsetAddressDifference.Should().Be(uint.MaxValue);
    }

    [Fact]
    public void WriteToString_PpOffsetTupleAtMaxValue_ValidatedRoundTrip()
    {
        var eds = CanOpenFile.Eds.ReadString(SingleSegment("0xFFFFFFFF, 0xFFFFFFFF"));

        var written = CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        var max = uint.MaxValue.ToString(CultureInfo.InvariantCulture);
        written.Should().Contain("PPOffset1=" + max + ", " + max);
        var segment = CanOpenFile.Eds.ReadString(written).DynamicChannels!.Segments.Single();
        segment.PPOffset.Should().Be(uint.MaxValue);
        segment.PPOffsetAddressDifference.Should().Be(uint.MaxValue);
    }

    [Fact]
    public void CloneDynamicChannels_SegmentWithAddressDifference_CopiesValue()
    {
        var source = new DynamicChannels();
        source.Segments.Add(new DynamicChannelSegment { PPOffset = 3, PPOffsetAddressDifference = 8 });
        source.Segments.Add(new DynamicChannelSegment { PPOffset = 4 });

        var clone = EdsDcfNet.Utilities.ModelCloner.CloneDynamicChannels(source)!;

        clone.Segments[0].PPOffsetAddressDifference.Should().Be(8u);
        clone.Segments[1].PPOffsetAddressDifference.Should().BeNull();
    }
}
