namespace EdsDcfNet.Tests.Parsers;

using System.Text;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;

/// <summary>
/// Numeric keys of <c>[DeviceComissioning]</c>, <c>[DeviceInfo]</c> and <c>[DynamicChannels]</c>
/// do not abort a lenient read (#581, extends #557): the malformed key is reported as a
/// <see cref="ParseDiagnostic"/>, the model keeps its other content, and strict mode throws
/// <see cref="EdsParseException"/> with the same code.
/// CiA 306-1 v1.4.0 § 7.3.5 Table 12 ([DeviceComissioning] entries and the one-"m" spelling) and
/// § 6.5 Table 2 ([DeviceInfo] entries).
/// Fixtures are assembled line by line so they do not depend on the checkout line ending.
/// </summary>
public class DcfLenientNumericFieldsTests
{
    private static readonly CanOpenFileOptions Strict = new() { StrictParsing = true };

    private static string Build(string[] deviceInfoLines, params string[] sectionLines)
    {
        var lines = new List<string>
        {
            "[FileInfo]",
            "FileName=lenient.dcf",
            "FileVersion=1",
            "FileRevision=1",
            "[DeviceInfo]",
            "VendorName=Test",
        };
        lines.AddRange(deviceInfoLines);
        lines.AddRange(sectionLines);

        var sb = new StringBuilder();
        foreach (var line in lines)
            sb.Append(line).Append('\n');
        return sb.ToString();
    }

    private static string Commissioning(string name, params string[] keyLines)
    {
        var lines = new List<string> { "[" + name + "]" };
        lines.AddRange(keyLines);
        return Build(Array.Empty<string>(), lines.ToArray());
    }

    private static int SourceLine(string content, string line)
        => Array.IndexOf(content.Split('\n'), line) + 1;

    private static void AssertSingleDiagnostic<T>(
        CanOpenReadResult<T> result,
        string content,
        string code,
        string section,
        string key,
        string rawValue,
        string? coercedTo)
    {
        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(code);
        diagnostic.Severity.Should().Be(ParseSeverity.Warning);
        diagnostic.Path.Should().Be(section + "." + key);
        diagnostic.Line.Should().Be(SourceLine(content, key + "=" + rawValue));
        diagnostic.RawValue.Should().Be(rawValue);
        diagnostic.CoercedTo.Should().Be(coercedTo);
    }

    private static void AssertStrict(string content, string code, string section, string key)
    {
        var act = () => CanOpenFile.Dcf.ReadString(content, Strict);

        var ex = act.Should().Throw<EdsParseException>().Which;
        ex.Code.Should().Be(code);
        ex.SectionName.Should().Be(section);
        ex.LineNumber.Should().NotBeNull();
        content.Split('\n')[ex.LineNumber!.Value - 1].Should().StartWith(key + "=");
    }

    #region [DeviceComissioning] numbers

    [Theory]
    [InlineData("abc")]
    [InlineData("300")]
    [InlineData("0x1G")]
    public void ReadStringWithDiagnostics_UnreadableNodeId_UsesDefaultAndKeepsSection(string raw)
    {
        var content = Commissioning(
            "DeviceComissioning", "NodeID=" + raw, "NodeName=Keep", "Baudrate=500", "NetNumber=3", "LSS_SerialNumber=77");

        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);

        AssertSingleDiagnostic(result, content, ParseDiagnosticCodes.InvalidNodeId, "DeviceComissioning", "NodeID", raw, "1");
        var dc = result.Model.DeviceCommissioning;
        dc.NodeId.Should().Be(1);
        dc.NodeName.Should().Be("Keep");
        dc.Baudrate.Should().Be(500);
        dc.NetNumber.Should().Be(3u);
        dc.LssSerialNumber.Should().Be(77u);
        AssertStrict(content, ParseDiagnosticCodes.InvalidNodeId, "DeviceComissioning", "NodeID");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("128")]
    [InlineData("255")]
    public void ReadStringWithDiagnostics_NodeIdOutsideRange_KeepsValueAndValidationReportsIt(string raw)
    {
        var content = Commissioning("DeviceComissioning", "NodeID=" + raw, "NodeName=Keep", "Baudrate=500");

        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);

        AssertSingleDiagnostic(result, content, ParseDiagnosticCodes.InvalidNodeId, "DeviceComissioning", "NodeID", raw, null);
        result.Model.DeviceCommissioning.NodeId.Should().Be(byte.Parse(raw));
        result.Model.DeviceCommissioning.NodeName.Should().Be("Keep");
        CanOpenFile.Validate(result.Model).Should().Contain(i => i.Path == "DeviceCommissioning.NodeId");

        var act = () => CanOpenFile.Dcf.ReadString(content, Strict);
        var ex = act.Should().Throw<EdsParseException>().Which;
        ex.Code.Should().Be(ParseDiagnosticCodes.InvalidNodeId);
        ex.SectionName.Should().Be("DeviceComissioning");
        ex.LineNumber.Should().Be(SourceLine(content, "NodeID=" + raw));
        ex.Message.Should().Contain("Invalid NodeID").And.Contain("1..127");
    }

    [Fact]
    public void ReadStringWithDiagnostics_NodeIdZeroInTwoMSpelling_ReportsSectionAsWritten()
    {
        var content = Commissioning("DeviceCommissioning", "NodeID=0", "Baudrate=500");

        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == ParseDiagnosticCodes.InvalidNodeId && d.Path == "DeviceCommissioning.NodeID");
        result.Model.DeviceCommissioning.NodeId.Should().Be(0);
    }

    [Fact]
    public void ReadStringWithDiagnostics_MalformedBaudrate_UsesDefault250()
    {
        var content = Commissioning("DeviceComissioning", "NodeID=5", "NodeName=Keep", "Baudrate=fast", "NetNumber=3");

        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);

        AssertSingleDiagnostic(result, content, ParseDiagnosticCodes.InvalidBaudrate, "DeviceComissioning", "Baudrate", "fast", "250");
        result.Model.DeviceCommissioning.Baudrate.Should().Be(250);
        result.Model.DeviceCommissioning.NodeId.Should().Be(5);
        result.Model.DeviceCommissioning.NetNumber.Should().Be(3u);
        AssertStrict(content, ParseDiagnosticCodes.InvalidBaudrate, "DeviceComissioning", "Baudrate");
    }

    [Fact]
    public void ReadStringWithDiagnostics_BaudrateAtUInt16Overflow_UsesDefault250()
    {
        // Baudrate is Unsigned16 (CiA 306-1 Table 12): 65536 does not fit.
        var content = Commissioning("DeviceComissioning", "NodeID=5", "Baudrate=65536");

        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.InvalidBaudrate);
        result.Model.DeviceCommissioning.Baudrate.Should().Be(250);
    }

    [Theory]
    [InlineData("0x1G")]
    [InlineData("4294967296")]
    [InlineData("-1")]
    public void ReadStringWithDiagnostics_MalformedNetNumber_UsesZero(string raw)
    {
        var content = Commissioning("DeviceComissioning", "NodeID=5", "NetNumber=" + raw, "NetworkName=Keep");

        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);

        AssertSingleDiagnostic(result, content, ParseDiagnosticCodes.InvalidNetNumber, "DeviceComissioning", "NetNumber", raw, "0");
        result.Model.DeviceCommissioning.NetNumber.Should().Be(0u);
        result.Model.DeviceCommissioning.NetworkName.Should().Be("Keep");
        AssertStrict(content, ParseDiagnosticCodes.InvalidNetNumber, "DeviceComissioning", "NetNumber");
    }

    [Fact]
    public void ReadStringWithDiagnostics_NetNumberAtMaxValue_IsRead()
    {
        // NetNumber is Unsigned32 (CiA 306-1 Table 12).
        var content = Commissioning("DeviceComissioning", "NodeID=5", "NetNumber=4294967295");

        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().BeEmpty();
        result.Model.DeviceCommissioning.NetNumber.Should().Be(uint.MaxValue);
    }

    [Fact]
    public void ReadStringWithDiagnostics_MalformedLssSerialNumber_LeavesItUnset()
    {
        var content = Commissioning("DeviceComissioning", "NodeID=5", "NodeName=Keep", "LSS_SerialNumber=xyz");

        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);

        AssertSingleDiagnostic(
            result, content, ParseDiagnosticCodes.InvalidLssSerialNumber, "DeviceComissioning", "LSS_SerialNumber", "xyz", null);
        result.Model.DeviceCommissioning.LssSerialNumber.Should().BeNull();
        result.Model.DeviceCommissioning.NodeName.Should().Be("Keep");
        AssertStrict(content, ParseDiagnosticCodes.InvalidLssSerialNumber, "DeviceComissioning", "LSS_SerialNumber");
    }

    [Fact]
    public void ReadString_WellFormedCommissioningNumbers_AreReadWithoutDiagnostics()
    {
        var content = Commissioning(
            "DeviceComissioning", "NodeID=0x7F", "Baudrate=1000", "NetNumber=0x10", "LSS_SerialNumber=0xFFFFFFFF");

        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content, Strict);

        result.Diagnostics.Should().BeEmpty();
        result.Model.DeviceCommissioning.NodeId.Should().Be(127);
        result.Model.DeviceCommissioning.Baudrate.Should().Be(1000);
        result.Model.DeviceCommissioning.NetNumber.Should().Be(16u);
        result.Model.DeviceCommissioning.LssSerialNumber.Should().Be(uint.MaxValue);
    }

    #endregion

    #region [DeviceInfo] numbers

    private static readonly Dictionary<string, Func<DeviceInfo, object>> DeviceInfoValue = new()
    {
        ["VendorNumber"] = d => d.VendorNumber,
        ["ProductNumber"] = d => d.ProductNumber,
        ["RevisionNumber"] = d => d.RevisionNumber,
        ["Granularity"] = d => d.Granularity,
        ["DynamicChannelsSupported"] = d => d.DynamicChannelsSupported,
        ["NrOfRXPDO"] = d => d.NrOfRxPdo,
        ["NrOfTXPDO"] = d => d.NrOfTxPdo,
        ["CompactPDO"] = d => d.CompactPdo,
    };

    [Theory]
    [InlineData("VendorNumber", "0x1G", "0")]
    [InlineData("VendorNumber", "$NODEID+1", "0")]
    [InlineData("ProductNumber", "4294967296", "0")]
    [InlineData("RevisionNumber", "rev", "0")]
    [InlineData("Granularity", "256", "8")]
    [InlineData("DynamicChannelsSupported", "many", "0")]
    [InlineData("NrOfRXPDO", "65536", "0")]
    [InlineData("NrOfTXPDO", "lots", "0")]
    [InlineData("CompactPDO", "yes please", "0")]
    public void ReadStringWithDiagnostics_MalformedDeviceInfoNumber_UsesAbsentKeyDefault(string key, string raw, string expected)
    {
        var content = Build(new[] { "ProductName=Keep", key + "=" + raw });

        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);

        AssertSingleDiagnostic(result, content, ParseDiagnosticCodes.InvalidDeviceInfoNumber, "DeviceInfo", key, raw, expected);
        DeviceInfoValue[key](result.Model.DeviceInfo).ToString().Should().Be(expected);
        result.Model.DeviceInfo.VendorName.Should().Be("Test");
        result.Model.DeviceInfo.ProductName.Should().Be("Keep");
        AssertStrict(content, ParseDiagnosticCodes.InvalidDeviceInfoNumber, "DeviceInfo", key);
    }

    [Fact]
    public void ReadStringWithDiagnostics_SeveralMalformedDeviceInfoNumbers_ReportsEachAndContinues()
    {
        var content = Build(new[] { "VendorNumber=0x1G", "ProductNumber=0x20", "NrOfRXPDO=bad", "NrOfTXPDO=4" });

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.Diagnostics.Select(d => d.Path).Should().Equal("DeviceInfo.VendorNumber", "DeviceInfo.NrOfRXPDO");
        result.Model.DeviceInfo.ProductNumber.Should().Be(0x20u);
        result.Model.DeviceInfo.NrOfTxPdo.Should().Be(4);
    }

    [Fact]
    public void ReadString_DeviceInfoNumbersAtMaxValue_AreReadWithoutDiagnostics()
    {
        var content = Build(new[]
        {
            "VendorNumber=4294967295", "Granularity=255", "NrOfRXPDO=65535", "CompactPDO=255",
        });

        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content, Strict);

        result.Diagnostics.Should().BeEmpty();
        result.Model.DeviceInfo.VendorNumber.Should().Be(uint.MaxValue);
        result.Model.DeviceInfo.Granularity.Should().Be(255);
        result.Model.DeviceInfo.NrOfRxPdo.Should().Be(ushort.MaxValue);
        result.Model.DeviceInfo.CompactPdo.Should().Be(255);
    }

    #endregion

    #region [DynamicChannels] numbers

    [Fact]
    public void ReadStringWithDiagnostics_MalformedNrOfSeg_TreatsSegmentCountAsZero()
    {
        var content = Build(
            Array.Empty<string>(), "[DynamicChannels]", "NrOfSeg=many", "Type1=1", "Dir1=ro", "Range1=0xA080-0xA0BF");

        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);

        AssertSingleDiagnostic(
            result, content, ParseDiagnosticCodes.InvalidDynamicChannelCount, "DynamicChannels", "NrOfSeg", "many", "0");
        // No segments, but the unmapped keys stay on the section so they are written again.
        result.Model.DynamicChannels!.Segments.Should().BeEmpty();
        result.Model.DynamicChannels.RemainingEntries.Keys.Should().Contain("Type1");
        AssertStrict(content, ParseDiagnosticCodes.InvalidDynamicChannelCount, "DynamicChannels", "NrOfSeg");
    }

    [Fact]
    public void ReadStringWithDiagnostics_MalformedSegmentType_UsesZeroAndKeepsSegment()
    {
        var content = Build(
            Array.Empty<string>(),
            "[DynamicChannels]", "NrOfSeg=2", "Type1=bad", "Dir1=rw", "Range1=0xA080-0xA0BF", "PPOffset1=4",
            "Type2=8", "Dir2=ro", "Range2=0xA240-0xA27F");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        AssertSingleDiagnostic(
            result, content, ParseDiagnosticCodes.InvalidDynamicChannelType, "DynamicChannels", "Type1", "bad", "0");
        var segments = result.Model.DynamicChannels!.Segments;
        segments.Should().HaveCount(2);
        segments[0].Type.Should().Be(0);
        segments[0].Dir.Should().Be(AccessType.ReadWrite);
        segments[0].Range.Should().Be("0xA080-0xA0BF");
        segments[0].PPOffset.Should().Be(4u);
        segments[1].Type.Should().Be(8);

        var act = () => CanOpenFile.Eds.ReadString(content, Strict);
        var ex = act.Should().Throw<EdsParseException>().Which;
        ex.Code.Should().Be(ParseDiagnosticCodes.InvalidDynamicChannelType);
        ex.SectionName.Should().Be("DynamicChannels");
    }

    [Fact]
    public void ReadStringWithDiagnostics_SegmentTypeAtMaxValue_IsRead()
    {
        var content = Build(Array.Empty<string>(), "[DynamicChannels]", "NrOfSeg=1", "Type1=65535", "Dir1=ro", "Range1=0xA080-0xA0BF");

        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content, Strict);

        result.Diagnostics.Should().BeEmpty();
        result.Model.DynamicChannels!.Segments.Single().Type.Should().Be(ushort.MaxValue);
    }

    #endregion

    #region Both commissioning spellings

    private static string[] NormativeSection() => new[] { "[DeviceComissioning]", "NodeID=5", "NodeName=Normative", "Baudrate=500" };

    private static string[] CommonSection() => new[] { "[DeviceCommissioning]", "NodeID=9", "NodeName=Common", "CustomKey=Value" };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadStringWithDiagnostics_BothSpellings_NormativeSpellingWinsRegardlessOfOrder(bool commonFirst)
    {
        // CiA 306-1 v1.4.0 § 7.3.5 Table 12: the section is spelled [DeviceComissioning].
        var content = commonFirst
            ? Build(Array.Empty<string>(), CommonSection().Concat(NormativeSection()).ToArray())
            : Build(Array.Empty<string>(), NormativeSection().Concat(CommonSection()).ToArray());

        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);

        var dc = result.Model.DeviceCommissioning;
        dc.NodeId.Should().Be(5);
        dc.NodeName.Should().Be("Normative");
        dc.RemainingEntries.Should().BeEmpty();
        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.IniDuplicateDeviceCommissioning);
        diagnostic.Severity.Should().Be(ParseSeverity.Warning);
        diagnostic.Path.Should().Be("DeviceCommissioning");
        result.Model.AdditionalSections.Should().ContainKey("DeviceCommissioning");
        var kept = result.Model.AdditionalSections["DeviceCommissioning"];
        kept["NodeID"].Should().Be("9");
        kept["NodeName"].Should().Be("Common");
        kept["CustomKey"].Should().Be("Value");
        result.Model.AdditionalSections.Should().NotContainKey("DeviceComissioning");
    }

    [Fact]
    public void ReadString_BothSpellingsStrict_ThrowsWithCode()
    {
        var content = Build(Array.Empty<string>(), NormativeSection().Concat(CommonSection()).ToArray());

        var act = () => CanOpenFile.Dcf.ReadString(content, Strict);

        var ex = act.Should().Throw<EdsParseException>().Which;
        ex.Code.Should().Be(ParseDiagnosticCodes.IniDuplicateDeviceCommissioning);
        ex.SectionName.Should().Be("DeviceCommissioning");
    }

    [Fact]
    public void ReadStringWithDiagnostics_OnlyTwoMSpelling_IsReadWithoutDiagnostic()
    {
        var content = Build(Array.Empty<string>(), CommonSection());

        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);

        result.Model.DeviceCommissioning.NodeId.Should().Be(9);
        result.Diagnostics.Should().BeEmpty();
        result.Model.AdditionalSections.Should().BeEmpty();
    }

    [Fact]
    public void WriteToString_BothSpellingsRead_ValidatedRoundTripKeepsSecondSectionVerbatim()
    {
        var content = Build(Array.Empty<string>(), NormativeSection().Concat(CommonSection()).ToArray());
        var dcf = CanOpenFile.Dcf.ReadString(content);

        var written = CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);
        var reread = CanOpenFile.Dcf.ReadStringWithDiagnostics(written);

        CountLines(written, "[DeviceComissioning]").Should().Be(1);
        CountLines(written, "[DeviceCommissioning]").Should().Be(1);
        reread.Model.DeviceCommissioning.NodeId.Should().Be(5);
        reread.Model.DeviceCommissioning.NodeName.Should().Be("Normative");
        reread.Model.AdditionalSections["DeviceCommissioning"]["NodeID"].Should().Be("9");
        reread.Model.AdditionalSections["DeviceCommissioning"]["CustomKey"].Should().Be("Value");
        reread.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniDuplicateDeviceCommissioning);
    }

    [Fact]
    public void WriteToString_PropertyChangedAfterReadingBothSpellings_OutputFollowsProperty()
    {
        var content = Build(Array.Empty<string>(), NormativeSection().Concat(CommonSection()).ToArray());
        var dcf = CanOpenFile.Dcf.ReadString(content);
        dcf.DeviceCommissioning.NodeId = 7;

        var reread = CanOpenFile.Dcf.ReadString(CanOpenFile.Dcf.WriteToString(dcf));

        reread.DeviceCommissioning.NodeId.Should().Be(7);
        reread.AdditionalSections["DeviceCommissioning"]["NodeID"].Should().Be("9");
    }

    [Fact]
    public void WriteToString_AdditionalSectionWithGeneratedName_GeneratedSectionWins()
    {
        var dcf = CanOpenFile.Dcf.ReadString(Build(Array.Empty<string>(), NormativeSection()));
        dcf.AdditionalSections["DeviceComissioning"] = new Dictionary<string, string> { ["NodeID"] = "99" };

        var written = CanOpenFile.Dcf.WriteToString(dcf);

        CountLines(written, "[DeviceComissioning]").Should().Be(1);
        CanOpenFile.Dcf.ReadString(written).DeviceCommissioning.NodeId.Should().Be(5);
    }

    [Fact]
    public void WriteToString_AdditionalSectionWithGeneratedNameAndNoCommissioning_IsWrittenAsIs()
    {
        var dcf = new DeviceConfigurationFile();
        dcf.AdditionalSections["DeviceComissioning"] = new Dictionary<string, string> { ["Custom"] = "1" };

        var written = CanOpenFile.Dcf.WriteToString(dcf);

        CountLines(written, "[DeviceComissioning]").Should().Be(1);
        written.Should().Contain("Custom=1");
    }

    private static int CountLines(string content, string line)
        => content.Split('\n').Count(l => l.TrimEnd('\r') == line);

    #endregion
}
