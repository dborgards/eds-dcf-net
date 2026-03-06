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

        var eds = CanOpenFile.ReadEds(stream);

        eds.Should().NotBeNull();
        eds.DeviceInfo.Should().NotBeNull();
        eds.ObjectDictionary.Objects.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ReadEdsAsync_Stream_ParsesSuccessfully()
    {
        var edsContent = File.ReadAllText(FixturePath);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(edsContent));

        var eds = await CanOpenFile.ReadEdsAsync(stream);

        eds.Should().NotBeNull();
        eds.DeviceInfo.Should().NotBeNull();
    }

    [Fact]
    public void WriteEds_Stream_ProducesValidContent()
    {
        var eds = CanOpenFile.ReadEds(FixturePath);
        using var stream = new MemoryStream();

        CanOpenFile.WriteEds(eds, stream);

        stream.Length.Should().BeGreaterThan(0);
        stream.Position = 0;
        var roundTrip = CanOpenFile.ReadEds(stream);
        roundTrip.DeviceInfo.ProductName.Should().Be(eds.DeviceInfo.ProductName);
    }

    [Fact]
    public async Task WriteEdsAsync_Stream_ProducesValidContent()
    {
        var eds = CanOpenFile.ReadEds(FixturePath);
        using var stream = new MemoryStream();

        await CanOpenFile.WriteEdsAsync(eds, stream);

        stream.Length.Should().BeGreaterThan(0);
        stream.Position = 0;
        var roundTrip = CanOpenFile.ReadEds(stream);
        roundTrip.DeviceInfo.ProductName.Should().Be(eds.DeviceInfo.ProductName);
    }

    [Fact]
    public void ReadDcf_Stream_ParsesSuccessfully()
    {
        var dcfContent = File.ReadAllText(DcfFixturePath);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(dcfContent));

        var dcf = CanOpenFile.ReadDcf(stream);

        dcf.Should().NotBeNull();
        dcf.DeviceCommissioning.Should().NotBeNull();
    }

    [Fact]
    public async Task ReadDcfAsync_Stream_ParsesSuccessfully()
    {
        var dcfContent = File.ReadAllText(DcfFixturePath);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(dcfContent));

        var dcf = await CanOpenFile.ReadDcfAsync(stream);

        dcf.Should().NotBeNull();
        dcf.DeviceCommissioning.Should().NotBeNull();
    }

    [Fact]
    public void WriteDcf_Stream_ProducesValidContent()
    {
        var dcf = CanOpenFile.ReadDcf(DcfFixturePath);
        using var stream = new MemoryStream();

        CanOpenFile.WriteDcf(dcf, stream);

        stream.Length.Should().BeGreaterThan(0);
        stream.Position = 0;
        var roundTrip = CanOpenFile.ReadDcf(stream);
        roundTrip.DeviceCommissioning.NodeId.Should().Be(dcf.DeviceCommissioning.NodeId);
    }

    [Fact]
    public async Task WriteDcfAsync_Stream_ProducesValidContent()
    {
        var dcf = CanOpenFile.ReadDcf(DcfFixturePath);
        using var stream = new MemoryStream();

        await CanOpenFile.WriteDcfAsync(dcf, stream);

        stream.Length.Should().BeGreaterThan(0);
        stream.Position = 0;
        var roundTrip = CanOpenFile.ReadDcf(stream);
        roundTrip.DeviceCommissioning.NodeId.Should().Be(dcf.DeviceCommissioning.NodeId);
    }

    [Fact]
    public void ReadEds_Stream_LeavesStreamOpen()
    {
        var edsContent = File.ReadAllText(FixturePath);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(edsContent));

        CanOpenFile.ReadEds(stream);

        stream.CanRead.Should().BeTrue("the stream should remain open after reading");
    }

    [Fact]
    public void WriteEds_Stream_LeavesStreamOpen()
    {
        var eds = CanOpenFile.ReadEds(FixturePath);
        using var stream = new MemoryStream();

        CanOpenFile.WriteEds(eds, stream);

        stream.CanWrite.Should().BeTrue("the stream should remain open after writing");
    }

    [Fact]
    public void EdsRoundTrip_ViaStream_PreservesObjectDictionary()
    {
        var original = CanOpenFile.ReadEds(FixturePath);
        using var stream = new MemoryStream();

        CanOpenFile.WriteEds(original, stream);
        stream.Position = 0;
        var restored = CanOpenFile.ReadEds(stream);

        restored.ObjectDictionary.Objects.Count.Should().Be(original.ObjectDictionary.Objects.Count);
    }

    [Fact]
    public void DcfRoundTrip_ViaStream_PreservesCommissioning()
    {
        var original = CanOpenFile.ReadDcf(DcfFixturePath);
        using var stream = new MemoryStream();

        CanOpenFile.WriteDcf(original, stream);
        stream.Position = 0;
        var restored = CanOpenFile.ReadDcf(stream);

        restored.DeviceCommissioning.Baudrate.Should().Be(original.DeviceCommissioning.Baudrate);
        restored.DeviceCommissioning.NodeName.Should().Be(original.DeviceCommissioning.NodeName);
    }

    [Fact]
    public void ReadCpj_Stream_ParsesSuccessfully()
    {
        var cpjContent = "[Topology]\nNetName=TestNet\nNodes=0\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(cpjContent));

        var cpj = CanOpenFile.ReadCpj(stream);

        cpj.Should().NotBeNull();
    }

    [Fact]
    public void WriteCpj_Stream_RoundTripsAndLeavesStreamOpen()
    {
        var cpjContent = CanOpenFile.WriteCpjToString(CanOpenFile.ReadCpjFromString("[Topology]\nNetName=TestNet\nNodes=0\n"));
        var cpj = CanOpenFile.ReadCpjFromString(cpjContent);
        using var stream = new MemoryStream();

        CanOpenFile.WriteCpj(cpj, stream);

        stream.Length.Should().BeGreaterThan(0);
        stream.Position = 0;
        var parsed = CanOpenFile.ReadCpj(stream);
        parsed.Networks.Should().ContainSingle();
        parsed.Networks[0].NetName.Should().Be("TestNet");
        stream.CanRead.Should().BeTrue();
    }
}
