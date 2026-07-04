namespace EdsDcfNet.Tests.Parsers;

using System.Text;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using FluentAssertions;
using Xunit;

/// <summary>
/// Exercises the obsolete protected forwarding shims on <see cref="CanOpenReaderBase"/>
/// so external subclasses keep working and patch coverage stays above threshold.
/// </summary>
public class CanOpenReaderBaseCompatTests
{
    private const string MinimalDeviceInfo = """
        [DeviceInfo]
        VendorName=Test Vendor
        VendorNumber=1
        ProductName=Test Product
        ProductNumber=1
        RevisionNumber=1
        OrderCode=TEST-001
        BaudRate_10=0
        BaudRate_20=0
        BaudRate_50=0
        BaudRate_125=1
        BaudRate_250=1
        BaudRate_500=1
        BaudRate_800=0
        BaudRate_1000=0
        SimpleBootUpMaster=0
        SimpleBootUpSlave=1
        Granularity=8
        DynamicChannelsSupported=0
        GroupMessaging=0
        NrOfRXPDO=0
        NrOfTXPDO=0
        LSS_Supported=0
        """;

    private readonly CompatTestReader _reader = new();

    [Fact]
    public void ObsoleteParseSectionsFromFile_DelegatesToIniParser()
    {
        var sections = _reader.InvokeParseSectionsFromFile("Fixtures/sample_device.eds");

        sections.Should().ContainKey("DeviceInfo");
        sections["DeviceInfo"]["ProductName"].Should().Be("IO-Module 16x16");
    }

    [Fact]
    public async Task ObsoleteParseSectionsFromFileAsync_DelegatesToIniParser()
    {
        var sections = await _reader.InvokeParseSectionsFromFileAsync("Fixtures/sample_device.eds");

        sections.Should().ContainKey("DeviceInfo");
        sections["DeviceInfo"]["ProductName"].Should().Be("IO-Module 16x16");
    }

    [Fact]
    public void ObsoleteParseSectionsFromString_DelegatesToIniParser()
    {
        var sections = _reader.InvokeParseSectionsFromString(
            """
            [DeviceInfo]
            VendorName=Inline
            """);

        sections.Should().ContainKey("DeviceInfo");
        sections["DeviceInfo"]["VendorName"].Should().Be("Inline");
    }

    [Fact]
    public void ObsoleteParseSectionsFromStream_DelegatesToIniParser()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            """
            [DeviceInfo]
            VendorName=Stream
            """));

        var sections = _reader.InvokeParseSectionsFromStream(stream);

        sections.Should().ContainKey("DeviceInfo");
        sections["DeviceInfo"]["VendorName"].Should().Be("Stream");
    }

    [Fact]
    public async Task ObsoleteParseSectionsFromStreamAsync_DelegatesToIniParser()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            """
            [DeviceInfo]
            VendorName=AsyncStream
            """));

        var sections = await _reader.InvokeParseSectionsFromStreamAsync(stream);

        sections.Should().ContainKey("DeviceInfo");
        sections["DeviceInfo"]["VendorName"].Should().Be("AsyncStream");
    }

    [Fact]
    public void ObsoleteParseDeviceInfo_DelegatesToCanOpenSectionParsers()
    {
        var sections = _reader.InvokeParseSectionsFromString(MinimalDeviceInfo);

        var deviceInfo = _reader.InvokeParseDeviceInfo(sections);

        deviceInfo.VendorName.Should().Be("Test Vendor");
        deviceInfo.ProductName.Should().Be("Test Product");
    }

    [Fact]
    public void ObsoleteParseDeviceInfo_MissingSection_ThrowsEdsParseException()
    {
        var sections = _reader.InvokeParseSectionsFromString("[MandatoryObjects]\nSupportedObjects=0");

        var act = () => _reader.InvokeParseDeviceInfo(sections);

        act.Should().Throw<EdsParseException>();
    }

    [Fact]
    public void ObsoleteParseComments_DelegatesToCanOpenSectionParsers()
    {
        var sections = _reader.InvokeParseSectionsFromString(
            """
            [Comments]
            Lines=1
            Line1=Device note
            """);

        var comments = _reader.InvokeParseComments(sections);

        comments.Should().NotBeNull();
        comments!.CommentLines[1].Should().Be("Device note");
    }

    [Fact]
    public void ObsoleteParseComments_SkipsEmptyCommentLines()
    {
        var sections = _reader.InvokeParseSectionsFromString(
            """
            [Comments]
            Lines=2
            Line1=First note
            Line2=
            """);

        var comments = _reader.InvokeParseComments(sections);

        comments.Should().NotBeNull();
        comments!.Lines.Should().Be(2);
        comments.CommentLines.Should().ContainKey(1);
        comments.CommentLines.Should().NotContainKey(2);
    }

    [Fact]
    public void ObsoleteParseSupportedModules_DelegatesToCanOpenSectionParsers()
    {
        var sections = _reader.InvokeParseSectionsFromString(
            """
            [SupportedModules]
            NrOfEntries=1

            [M1ModuleInfo]
            ProductName=Input Module
            ProductVersion=1
            ProductRevision=0
            OrderCode=MOD-IN-8
            """);

        var modules = _reader.InvokeParseSupportedModules(sections);

        modules.Should().ContainSingle();
        modules[0].ProductName.Should().Be("Input Module");
    }

    [Fact]
    public void ObsoleteParseModuleInfo_DelegatesToCanOpenSectionParsers()
    {
        var sections = _reader.InvokeParseSectionsFromString(
            """
            [M1ModuleInfo]
            ProductName=Input Module
            ProductVersion=1
            ProductRevision=0
            OrderCode=MOD-IN-8
            """);

        var module = _reader.InvokeParseModuleInfo(sections, 1);

        module.Should().NotBeNull();
        module!.ProductName.Should().Be("Input Module");
    }

    [Fact]
    public void ObsoleteParseModuleInfo_SkipsEmptyFixedObjectIndex()
    {
        var sections = _reader.InvokeParseSectionsFromString(
            """
            [M1ModuleInfo]
            ProductName=Input Module
            ProductVersion=1
            ProductRevision=0
            OrderCode=MOD-IN-8

            [M1FixedObjects]
            NrOfEntries=2
            1=0x6000
            2=
            """);

        var module = _reader.InvokeParseModuleInfo(sections, 1);

        module.Should().NotBeNull();
        module!.FixedObjects.Should().ContainSingle().Which.Should().Be(0x6000);
    }

    [Fact]
    public void ObsoleteParseDynamicChannels_DelegatesToCanOpenSectionParsers()
    {
        var sections = _reader.InvokeParseSectionsFromString(
            """
            [DynamicChannels]
            NrOfSeg=1
            Type1=0x0007
            Dir1=ro
            Range1=0xA080-0xA0BF
            PPOffset1=0
            """);

        var dynamicChannels = _reader.InvokeParseDynamicChannels(sections);

        dynamicChannels.Should().NotBeNull();
        dynamicChannels!.Segments.Should().ContainSingle();
        dynamicChannels.Segments[0].Range.Should().Be("0xA080-0xA0BF");
    }

    [Fact]
    public void ObsoleteParseTools_DelegatesToCanOpenSectionParsers()
    {
        var sections = _reader.InvokeParseSectionsFromString(
            """
            [Tools]
            Items=1

            [Tool1]
            Name=EDS Checker
            Command=checker.exe $EDS
            """);

        var tools = _reader.InvokeParseTools(sections);

        tools.Should().ContainSingle();
        tools[0].Name.Should().Be("EDS Checker");
    }

    private sealed class CompatTestReader : CanOpenReaderBase
    {
        protected override string[] KnownSectionNames => Array.Empty<string>();

#pragma warning disable CS0618 // Obsolete protected shims are exercised intentionally.
        public Dictionary<string, Dictionary<string, string>> InvokeParseSectionsFromFile(string filePath)
            => ParseSectionsFromFile(filePath);

        public Task<Dictionary<string, Dictionary<string, string>>> InvokeParseSectionsFromFileAsync(string filePath)
            => ParseSectionsFromFileAsync(filePath);

        public Dictionary<string, Dictionary<string, string>> InvokeParseSectionsFromString(string content)
            => ParseSectionsFromString(content);

        public Dictionary<string, Dictionary<string, string>> InvokeParseSectionsFromStream(Stream stream)
            => ParseSectionsFromStream(stream);

        public Task<Dictionary<string, Dictionary<string, string>>> InvokeParseSectionsFromStreamAsync(Stream stream)
            => ParseSectionsFromStreamAsync(stream);

        public DeviceInfo InvokeParseDeviceInfo(Dictionary<string, Dictionary<string, string>> sections)
            => ParseDeviceInfo(sections);

        public Comments? InvokeParseComments(Dictionary<string, Dictionary<string, string>> sections)
            => ParseComments(sections);

        public List<ModuleInfo> InvokeParseSupportedModules(Dictionary<string, Dictionary<string, string>> sections)
            => ParseSupportedModules(sections);

        public ModuleInfo? InvokeParseModuleInfo(
            Dictionary<string, Dictionary<string, string>> sections,
            int moduleNumber)
            => ParseModuleInfo(sections, moduleNumber);

        public DynamicChannels? InvokeParseDynamicChannels(Dictionary<string, Dictionary<string, string>> sections)
            => ParseDynamicChannels(sections);

        public List<ToolInfo> InvokeParseTools(Dictionary<string, Dictionary<string, string>> sections)
            => ParseTools(sections);
#pragma warning restore CS0618
    }
}
