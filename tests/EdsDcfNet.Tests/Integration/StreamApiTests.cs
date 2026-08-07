namespace EdsDcfNet.Tests.Integration;

using System.Text;
using EdsDcfNet;
using FluentAssertions;

public class StreamApiTests
{
    private const string FixturePath = "Fixtures/sample_device.eds";
    private const string DcfFixturePath = "Fixtures/minimal.dcf";

    [Fact]
    public void ReadEds_Stream_ParsesSuccessfully()
    {
        var edsContent = File.ReadAllText(FixturePath);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(edsContent));

        var eds = CanOpenFile.Eds.ReadStream(stream);

        eds.Should().NotBeNull();
        eds.DeviceInfo.Should().NotBeNull();
        eds.ObjectDictionary.Objects.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ReadEdsAsync_Stream_ParsesSuccessfully()
    {
        var edsContent = File.ReadAllText(FixturePath);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(edsContent));

        var eds = await CanOpenFile.Eds.ReadStreamAsync(stream);

        eds.Should().NotBeNull();
        eds.DeviceInfo.Should().NotBeNull();
    }

    [Fact]
    public void WriteEds_Stream_ProducesValidContent()
    {
        var eds = CanOpenFile.Eds.ReadFile(FixturePath);
        using var stream = new MemoryStream();

        CanOpenFile.Eds.WriteStream(eds, stream);

        stream.Length.Should().BeGreaterThan(0);
        stream.Position = 0;
        var roundTrip = CanOpenFile.Eds.ReadStream(stream);
        roundTrip.DeviceInfo.ProductName.Should().Be(eds.DeviceInfo.ProductName);
    }

    [Fact]
    public async Task WriteEdsAsync_Stream_ProducesValidContent()
    {
        var eds = CanOpenFile.Eds.ReadFile(FixturePath);
        using var stream = new MemoryStream();

        await CanOpenFile.Eds.WriteStreamAsync(eds, stream);

        stream.Length.Should().BeGreaterThan(0);
        stream.Position = 0;
        var roundTrip = CanOpenFile.Eds.ReadStream(stream);
        roundTrip.DeviceInfo.ProductName.Should().Be(eds.DeviceInfo.ProductName);
    }

    [Fact]
    public void ReadDcf_Stream_ParsesSuccessfully()
    {
        var dcfContent = File.ReadAllText(DcfFixturePath);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(dcfContent));

        var dcf = CanOpenFile.Dcf.ReadStream(stream);

        dcf.Should().NotBeNull();
        dcf.DeviceCommissioning.Should().NotBeNull();
    }

    [Fact]
    public async Task ReadDcfAsync_Stream_ParsesSuccessfully()
    {
        var dcfContent = File.ReadAllText(DcfFixturePath);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(dcfContent));

        var dcf = await CanOpenFile.Dcf.ReadStreamAsync(stream);

        dcf.Should().NotBeNull();
        dcf.DeviceCommissioning.Should().NotBeNull();
    }

    [Fact]
    public void WriteDcf_Stream_ProducesValidContent()
    {
        var dcf = CanOpenFile.Dcf.ReadFile(DcfFixturePath);
        using var stream = new MemoryStream();

        CanOpenFile.Dcf.WriteStream(dcf, stream);

        stream.Length.Should().BeGreaterThan(0);
        stream.Position = 0;
        var roundTrip = CanOpenFile.Dcf.ReadStream(stream);
        roundTrip.DeviceCommissioning.NodeId.Should().Be(dcf.DeviceCommissioning.NodeId);
    }

    [Fact]
    public async Task WriteDcfAsync_Stream_ProducesValidContent()
    {
        var dcf = CanOpenFile.Dcf.ReadFile(DcfFixturePath);
        using var stream = new MemoryStream();

        await CanOpenFile.Dcf.WriteStreamAsync(dcf, stream);

        stream.Length.Should().BeGreaterThan(0);
        stream.Position = 0;
        var roundTrip = CanOpenFile.Dcf.ReadStream(stream);
        roundTrip.DeviceCommissioning.NodeId.Should().Be(dcf.DeviceCommissioning.NodeId);
    }

    [Fact]
    public void ReadEds_Stream_LeavesStreamOpen()
    {
        var edsContent = File.ReadAllText(FixturePath);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(edsContent));

        CanOpenFile.Eds.ReadStream(stream);

        stream.CanRead.Should().BeTrue("the stream should remain open after reading");
    }

    [Fact]
    public void WriteEds_Stream_LeavesStreamOpen()
    {
        var eds = CanOpenFile.Eds.ReadFile(FixturePath);
        using var stream = new MemoryStream();

        CanOpenFile.Eds.WriteStream(eds, stream);

        stream.CanWrite.Should().BeTrue("the stream should remain open after writing");
    }

    [Fact]
    public void EdsRoundTrip_ViaStream_PreservesObjectDictionary()
    {
        var original = CanOpenFile.Eds.ReadFile(FixturePath);
        using var stream = new MemoryStream();

        CanOpenFile.Eds.WriteStream(original, stream);
        stream.Position = 0;
        var restored = CanOpenFile.Eds.ReadStream(stream);

        restored.ObjectDictionary.Objects.Count.Should().Be(original.ObjectDictionary.Objects.Count);
    }

    [Fact]
    public void DcfRoundTrip_ViaStream_PreservesCommissioning()
    {
        var original = CanOpenFile.Dcf.ReadFile(DcfFixturePath);
        using var stream = new MemoryStream();

        CanOpenFile.Dcf.WriteStream(original, stream);
        stream.Position = 0;
        var restored = CanOpenFile.Dcf.ReadStream(stream);

        restored.DeviceCommissioning.Baudrate.Should().Be(original.DeviceCommissioning.Baudrate);
        restored.DeviceCommissioning.NodeName.Should().Be(original.DeviceCommissioning.NodeName);
    }

    [Fact]
    public void ReadCpj_Stream_ParsesSuccessfully()
    {
        var cpjContent = "[Topology]\nNetName=TestNet\nNodes=0\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(cpjContent));

        var cpj = CanOpenFile.Cpj.ReadStream(stream);

        cpj.Should().NotBeNull();
    }

    [Fact]
    public void WriteCpj_Stream_RoundTripsAndLeavesStreamOpen()
    {
        var cpjContent = CanOpenFile.Cpj.WriteToString(CanOpenFile.Cpj.ReadString("[Topology]\nNetName=TestNet\nNodes=0\n"));
        var cpj = CanOpenFile.Cpj.ReadString(cpjContent);
        using var stream = new MemoryStream();

        CanOpenFile.Cpj.WriteStream(cpj, stream);

        stream.Length.Should().BeGreaterThan(0);
        stream.Position = 0;
        var parsed = CanOpenFile.Cpj.ReadStream(stream);
        parsed.Networks.Should().ContainSingle();
        parsed.Networks[0].NetName.Should().Be("TestNet");
        stream.CanRead.Should().BeTrue();
    }
}
