namespace EdsDcfNet.Tests.Parsers;

using System.Globalization;
using System.Text;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;

/// <summary>
/// An integer header that cannot store a <c>$NODEID</c> formula fails like any other
/// unreadable number (#577). Strict mode throws <see cref="EdsParseException"/>. Lenient
/// mode reports <see cref="ParseDiagnosticCodes.InvalidDeviceInfoNumber"/> and uses the
/// absent-key fallback. <c>DefaultValue</c> stays text.
/// </summary>
public class NodeIdFormulaIntegerHeaderTests
{
    private static readonly CanOpenFileOptions Strict = new() { StrictParsing = true };

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

    private static string Eds(params string[] deviceInfoLines)
    {
        var lines = new List<string>
        {
            "[FileInfo]",
            "FileName=nodeid.eds",
            "FileVersion=1",
            "FileRevision=0",
            "[DeviceInfo]",
            "VendorName=Test",
            "ProductName=Keep",
        };
        lines.AddRange(deviceInfoLines);

        var sb = new StringBuilder();
        foreach (var line in lines)
            sb.Append(line).Append('\n');
        return sb.ToString();
    }

    private static int SourceLine(string content, string line)
        => Array.IndexOf(content.Split('\n'), line) + 1;

    private static void AssertLenientDeviceInfo(string content, string key, string raw, string expected)
    {
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.InvalidDeviceInfoNumber);
        diagnostic.Severity.Should().Be(ParseSeverity.Warning);
        diagnostic.Path.Should().Be("DeviceInfo." + key);
        diagnostic.Line.Should().Be(SourceLine(content, key + "=" + raw));
        diagnostic.RawValue.Should().Be(raw);
        diagnostic.CoercedTo.Should().Be(expected);
        Convert.ToString(DeviceInfoValue[key](result.Model.DeviceInfo), CultureInfo.InvariantCulture)
            .Should().Be(expected);
        result.Model.DeviceInfo.VendorName.Should().Be("Test");
        result.Model.DeviceInfo.ProductName.Should().Be("Keep");

        var read = CanOpenFile.Eds.ReadString(content);
        Convert.ToString(DeviceInfoValue[key](read.DeviceInfo), CultureInfo.InvariantCulture)
            .Should().Be(expected);
    }

    private static void AssertStrictDeviceInfo(string content, string key, string raw)
    {
        var withDiagnostics = () => CanOpenFile.Eds.ReadStringWithDiagnostics(content, Strict);
        var ex = withDiagnostics.Should().Throw<EdsParseException>().Which;
        ex.GetType().Should().Be(typeof(EdsParseException));
        ex.Code.Should().Be(ParseDiagnosticCodes.InvalidDeviceInfoNumber);
        ex.SectionName.Should().Be("DeviceInfo");
        ex.LineNumber.Should().Be(SourceLine(content, key + "=" + raw));
        ex.Message.Should().Contain(raw);
        content.Split('\n')[ex.LineNumber!.Value - 1].Should().StartWith(key + "=");

        var read = () => CanOpenFile.Eds.ReadString(content, Strict);
        read.Should().Throw<EdsParseException>().Which.GetType().Should().Be(typeof(EdsParseException));
    }

    [Theory]
    [InlineData("VendorNumber", "0xZZ", "0")]
    [InlineData("VendorNumber", "99999999999999999999", "0")]
    [InlineData("VendorNumber", "$NODEID", "0")]
    [InlineData("VendorNumber", "$NODEID+0x10", "0")]
    [InlineData("VendorNumber", "$NODEID+", "0")]
    [InlineData("ProductNumber", "0xZZ", "0")]
    [InlineData("ProductNumber", "99999999999999999999", "0")]
    [InlineData("ProductNumber", "$NODEID", "0")]
    [InlineData("ProductNumber", "$NODEID+0x10", "0")]
    [InlineData("ProductNumber", "$NODEID+", "0")]
    [InlineData("RevisionNumber", "$NODEID", "0")]
    [InlineData("RevisionNumber", "$NODEID+0x10", "0")]
    [InlineData("RevisionNumber", "$NODEID+", "0")]
    public void ReadStringWithDiagnostics_UnreadableDeviceInfoUInt32_StrictThrowsEdsParseExceptionAndLenientUsesZero(
        string key, string raw, string expected)
    {
        var content = Eds(key + "=" + raw);

        AssertLenientDeviceInfo(content, key, raw, expected);
        AssertStrictDeviceInfo(content, key, raw);
    }

    [Theory]
    [InlineData("NrOfRXPDO", "$NODEID", "0")]
    [InlineData("NrOfRXPDO", "$NODEID+0x10", "0")]
    [InlineData("NrOfRXPDO", "$NODEID+", "0")]
    [InlineData("NrOfTXPDO", "$NODEID+0x10", "0")]
    [InlineData("Granularity", "$NODEID", "8")]
    [InlineData("Granularity", "$NODEID+", "8")]
    [InlineData("DynamicChannelsSupported", "$NODEID+0x10", "0")]
    [InlineData("CompactPDO", "$NODEID", "0")]
    [InlineData("CompactPDO", "$NODEID+", "0")]
    public void ReadStringWithDiagnostics_UnreadableNarrowDeviceInfoInteger_StrictThrowsEdsParseExceptionAndLenientUsesAbsentDefault(
        string key, string raw, string expected)
    {
        var content = Eds(key + "=" + raw);

        AssertLenientDeviceInfo(content, key, raw, expected);
        AssertStrictDeviceInfo(content, key, raw);
    }

    [Fact]
    public void ReadStringWithDiagnostics_DcfVendorNumberFormula_DoesNotEvaluateCommissioningNodeId()
    {
        var lines = new[]
        {
            "[FileInfo]",
            "FileName=nodeid.dcf",
            "FileVersion=1",
            "FileRevision=0",
            "[DeviceInfo]",
            "VendorName=Test",
            "ProductName=Keep",
            "VendorNumber=$NODEID+0x10",
            "[DeviceComissioning]",
            "NodeID=5",
            "Baudrate=250",
        };
        var sb = new StringBuilder();
        foreach (var line in lines)
            sb.Append(line).Append('\n');
        var content = sb.ToString();

        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == ParseDiagnosticCodes.InvalidDeviceInfoNumber &&
            d.Path == "DeviceInfo.VendorNumber" &&
            d.RawValue == "$NODEID+0x10" &&
            d.CoercedTo == "0");
        result.Model.DeviceInfo.VendorNumber.Should().Be(0u);
        result.Model.DeviceCommissioning.NodeId.Should().Be(5);

        var act = () => CanOpenFile.Dcf.ReadStringWithDiagnostics(content, Strict);
        var ex = act.Should().Throw<EdsParseException>().Which;
        ex.GetType().Should().Be(typeof(EdsParseException));
        ex.Code.Should().Be(ParseDiagnosticCodes.InvalidDeviceInfoNumber);
        ex.SectionName.Should().Be("DeviceInfo");
        ex.Message.Should().Contain("$NODEID+0x10");
    }

    [Theory]
    [InlineData("FileVersion", "$NODEID")]
    [InlineData("FileVersion", "$NODEID+0x10")]
    [InlineData("FileVersion", "$NODEID+")]
    [InlineData("FileVersion", "0xZZ")]
    [InlineData("FileVersion", "99999999999999999999")]
    [InlineData("FileRevision", "$NODEID")]
    [InlineData("FileRevision", "$NODEID+0x10")]
    [InlineData("FileRevision", "$NODEID+")]
    public void ReadStringWithDiagnostics_UnreadableFileInfoInteger_ThrowsEdsParseExceptionInBothModes(
        string key, string raw)
    {
        var version = key == "FileVersion" ? raw : "1";
        var revision = key == "FileRevision" ? raw : "0";
        var content = Eds()
            .Replace("FileVersion=1", "FileVersion=" + version)
            .Replace("FileRevision=0", "FileRevision=" + revision);

        AssertFileInfoThrowsEdsParseException(() => CanOpenFile.Eds.ReadStringWithDiagnostics(content), key, raw);
        AssertFileInfoThrowsEdsParseException(() => CanOpenFile.Eds.ReadString(content), key, raw);
        AssertFileInfoThrowsEdsParseException(() => CanOpenFile.Eds.ReadStringWithDiagnostics(content, Strict), key, raw);
        AssertFileInfoThrowsEdsParseException(() => CanOpenFile.Eds.ReadString(content, Strict), key, raw);
    }

    private static void AssertFileInfoThrowsEdsParseException(Action act, string key, string raw)
    {
        var ex = act.Should().Throw<EdsParseException>().Which;
        ex.GetType().Should().Be(typeof(EdsParseException));
        ex.SectionName.Should().Be("FileInfo");
        ex.Message.Should().Contain("[FileInfo] " + key + ":");
        ex.Message.Should().Contain(raw);
    }

    [Theory]
    [InlineData("$NODEID+0x80")]
    [InlineData("$NODEID+")]
    [InlineData("$NODEID")]
    public void ReadString_DefaultValueNodeIdFormula_StaysText(string formula)
    {
        var content = Eds("VendorNumber=0x100") +
            "[MandatoryObjects]\n" +
            "SupportedObjects=1\n" +
            "1=0x2000\n" +
            "[2000]\n" +
            "ParameterName=CobId\n" +
            "ObjectType=0x7\n" +
            "DataType=0x0007\n" +
            "AccessType=rw\n" +
            "DefaultValue=" + formula + "\n";

        var lenient = CanOpenFile.Eds.ReadStringWithDiagnostics(content);
        lenient.Diagnostics.Should().BeEmpty();
        lenient.Model.ObjectDictionary.Objects[0x2000].DefaultValue.Should().Be(formula);
        lenient.Model.DeviceInfo.VendorNumber.Should().Be(0x100u);

        var strict = CanOpenFile.Eds.ReadStringWithDiagnostics(content, Strict);
        strict.Diagnostics.Should().BeEmpty();
        strict.Model.ObjectDictionary.Objects[0x2000].DefaultValue.Should().Be(formula);
    }
}
