namespace EdsDcfNet.Tests.Integration;

using System.Text;
using EdsDcfNet;
using EdsDcfNet.Models;

public class CanOpenFileStreamTests
{
    [Fact]
    public void ReadEds_Stream_ParsesAndKeepsStreamOpen()
    {
        using var stream = File.OpenRead("Fixtures/sample_device.eds");

        var eds = CanOpenFile.ReadEds(stream);

        eds.DeviceInfo.ProductName.Should().Be("IO-Module 16x16");
        stream.CanRead.Should().BeTrue();
    }

    [Fact]
    public void WriteEds_Stream_WritesContentAndKeepsStreamOpen()
    {
        var eds = CreateMinimalEds();
        using var stream = new MemoryStream();

        CanOpenFile.WriteEds(eds, stream);

        stream.CanWrite.Should().BeTrue();
        stream.Position = 0;
        using var reader = new StreamReader(
            stream,
            System.Text.Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024,
            leaveOpen: true);
        var content = reader.ReadToEnd();
        content.Should().Contain("[FileInfo]");
        content.Should().Contain("[1000]");
    }

    [Fact]
    public void ReadWriteDcf_Stream_RoundTripAndKeepsStreamsOpen()
    {
        var dcf = CreateMinimalDcf();
        using var writeStream = new MemoryStream();

        CanOpenFile.WriteDcf(dcf, writeStream);
        writeStream.CanWrite.Should().BeTrue();
        writeStream.Position = 0;

        var parsed = CanOpenFile.ReadDcf(writeStream);

        parsed.DeviceCommissioning.NodeId.Should().Be(5);
        parsed.DeviceCommissioning.Baudrate.Should().Be(500);
        writeStream.CanRead.Should().BeTrue();
    }

    [Fact]
    public void ReadWriteCpj_Stream_RoundTripAndKeepsStreamsOpen()
    {
        var cpj = new NodelistProject();
        cpj.Networks.Add(new NetworkTopology
        {
            NetName = "Plant",
            Nodes =
            {
                [2] = new NetworkNode
                {
                    NodeId = 2,
                    Present = true,
                    Name = "Drive",
                    DcfFileName = "drive.dcf"
                }
            }
        });

        using var stream = new MemoryStream();
        CanOpenFile.WriteCpj(cpj, stream);
        stream.Position = 0;

        var parsed = CanOpenFile.ReadCpj(stream);

        parsed.Networks.Should().ContainSingle();
        parsed.Networks[0].Nodes.Should().ContainKey(2);
        stream.CanRead.Should().BeTrue();
    }

    [Fact]
    public void ReadXdd_Stream_ParsesAndKeepsStreamOpen()
    {
        using var stream = File.OpenRead("Fixtures/sample_device.xdd");

        var eds = CanOpenFile.ReadXdd(stream);

        eds.DeviceInfo.VendorName.Should().Be("Example Automation Inc.");
        stream.CanRead.Should().BeTrue();
    }

    [Fact]
    public void ReadXdc_Stream_ParsesAndKeepsStreamOpen()
    {
        using var stream = File.OpenRead("Fixtures/minimal.xdc");

        var dcf = CanOpenFile.ReadXdc(stream);

        dcf.DeviceCommissioning.NodeId.Should().Be(5);
        stream.CanRead.Should().BeTrue();
    }

    [Fact]
    public async Task ReadWriteDcfAsync_Stream_RoundTripAndKeepsStreamsOpen()
    {
        var dcf = CreateMinimalDcf();
        using var writeStream = new MemoryStream();

        await CanOpenFile.WriteDcfAsync(dcf, writeStream);
        writeStream.Position = 0;
        var parsed = await CanOpenFile.ReadDcfAsync(writeStream);

        parsed.DeviceCommissioning.NodeId.Should().Be(5);
        writeStream.CanRead.Should().BeTrue();
    }

    [Fact]
    public async Task ReadWriteXddAsync_Stream_RoundTripAndKeepsStreamsOpen()
    {
        var eds = CreateMinimalEds();
        using var stream = new MemoryStream();

        await CanOpenFile.WriteXddAsync(eds, stream);
        stream.Position = 0;
        var parsed = await CanOpenFile.ReadXddAsync(stream);

        parsed.DeviceInfo.ProductName.Should().Be("Stream Device");
        stream.CanRead.Should().BeTrue();
    }

    [Fact]
    public void ReadEds_Stream_WithExplicitMaxInputSize_Parses()
    {
        using var stream = File.OpenRead("Fixtures/sample_device.eds");
        var eds = CanOpenFile.ReadEds(stream, maxInputSize: stream.Length + 128);
        eds.DeviceInfo.ProductName.Should().Be("IO-Module 16x16");
    }

    [Fact]
    public async Task ReadEdsAsync_Stream_WithExplicitMaxInputSize_Parses()
    {
        using var stream = File.OpenRead("Fixtures/sample_device.eds");
        var eds = await CanOpenFile.ReadEdsAsync(stream, maxInputSize: stream.Length + 128);
        eds.DeviceInfo.ProductName.Should().Be("IO-Module 16x16");
    }

    [Fact]
    public void ReadDcf_Stream_WithExplicitMaxInputSize_Parses()
    {
        using var stream = File.OpenRead("Fixtures/minimal.dcf");
        var dcf = CanOpenFile.ReadDcf(stream, maxInputSize: stream.Length + 128);
        dcf.DeviceCommissioning.NodeId.Should().Be(5);
    }

    [Fact]
    public async Task ReadDcfAsync_Stream_WithExplicitMaxInputSize_Parses()
    {
        using var stream = File.OpenRead("Fixtures/minimal.dcf");
        var dcf = await CanOpenFile.ReadDcfAsync(stream, maxInputSize: stream.Length + 128);
        dcf.DeviceCommissioning.NodeId.Should().Be(5);
    }

    [Fact]
    public void ReadCpj_Stream_WithExplicitMaxInputSize_Parses()
    {
        var cpjText = "[Topology]\nNetName=Plant\nNodes=0\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(cpjText));
        var cpj = CanOpenFile.ReadCpj(stream, maxInputSize: cpjText.Length + 16);
        cpj.Networks.Should().ContainSingle();
        cpj.Networks[0].NetName.Should().Be("Plant");
    }

    [Fact]
    public async Task ReadCpjAsync_Stream_WithExplicitMaxInputSize_Parses()
    {
        var cpjText = "[Topology]\nNetName=Plant\nNodes=0\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(cpjText));
        var cpj = await CanOpenFile.ReadCpjAsync(stream, maxInputSize: cpjText.Length + 16);
        cpj.Networks.Should().ContainSingle();
        cpj.Networks[0].NetName.Should().Be("Plant");
    }

    [Fact]
    public void ReadXdd_Stream_WithExplicitMaxInputSize_Parses()
    {
        using var stream = File.OpenRead("Fixtures/sample_device.xdd");
        var eds = CanOpenFile.ReadXdd(stream, maxInputSize: stream.Length + 128);
        eds.DeviceInfo.VendorName.Should().Be("Example Automation Inc.");
    }

    [Fact]
    public async Task ReadXddAsync_Stream_WithExplicitMaxInputSize_Parses()
    {
        using var stream = File.OpenRead("Fixtures/sample_device.xdd");
        var eds = await CanOpenFile.ReadXddAsync(stream, maxInputSize: stream.Length + 128);
        eds.DeviceInfo.VendorName.Should().Be("Example Automation Inc.");
    }

    [Fact]
    public void ReadXdc_Stream_WithExplicitMaxInputSize_Parses()
    {
        using var stream = File.OpenRead("Fixtures/minimal.xdc");
        var dcf = CanOpenFile.ReadXdc(stream, maxInputSize: stream.Length + 128);
        dcf.DeviceCommissioning.NodeId.Should().Be(5);
    }

    [Fact]
    public async Task ReadXdcAsync_Stream_WithExplicitMaxInputSize_Parses()
    {
        using var stream = File.OpenRead("Fixtures/minimal.xdc");
        var dcf = await CanOpenFile.ReadXdcAsync(stream, maxInputSize: stream.Length + 128);
        dcf.DeviceCommissioning.NodeId.Should().Be(5);
    }

    [Fact]
    public async Task WriteReadCpjAsync_Stream_RoundTrip()
    {
        var cpj = CreateMinimalCpj();
        using var stream = new MemoryStream();

        await CanOpenFile.WriteCpjAsync(cpj, stream);
        stream.Position = 0;
        var parsed = await CanOpenFile.ReadCpjAsync(stream);

        parsed.Networks.Should().ContainSingle();
        parsed.Networks[0].Nodes.Should().ContainKey(2);
    }

    [Fact]
    public async Task WriteReadXdd_Stream_AndAsync_RoundTrip()
    {
        var eds = CreateMinimalEds();

        using var syncStream = new MemoryStream();
        CanOpenFile.WriteXdd(eds, syncStream);
        syncStream.Position = 0;
        CanOpenFile.ReadXdd(syncStream).DeviceInfo.ProductName.Should().Be("Stream Device");

        using var asyncStream = new MemoryStream();
        await CanOpenFile.WriteXddAsync(eds, asyncStream);
        asyncStream.Position = 0;
        var reparsed = await CanOpenFile.ReadXddAsync(asyncStream);
        reparsed.DeviceInfo.ProductName.Should().Be("Stream Device");
    }

    [Fact]
    public async Task WriteReadXdc_Stream_AndAsync_RoundTrip()
    {
        var dcf = CreateMinimalDcf();

        using var syncStream = new MemoryStream();
        CanOpenFile.WriteXdc(dcf, syncStream);
        syncStream.Position = 0;
        CanOpenFile.ReadXdc(syncStream).DeviceCommissioning.NodeId.Should().Be(5);

        using var asyncStream = new MemoryStream();
        await CanOpenFile.WriteXdcAsync(dcf, asyncStream);
        asyncStream.Position = 0;
        var reparsed = await CanOpenFile.ReadXdcAsync(asyncStream);
        reparsed.DeviceCommissioning.NodeId.Should().Be(5);
    }

    private static ElectronicDataSheet CreateMinimalEds()
    {
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo
            {
                FileName = "stream.eds",
                FileVersion = 1,
                FileRevision = 0,
                EdsVersion = "4.0"
            },
            DeviceInfo = new DeviceInfo
            {
                VendorName = "Stream Vendor",
                ProductName = "Stream Device",
                SupportedBaudRates = new BaudRates { BaudRate250 = true }
            },
            ObjectDictionary = new ObjectDictionary()
        };

        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly
        };

        return eds;
    }

    private static DeviceConfigurationFile CreateMinimalDcf()
    {
        return new DeviceConfigurationFile
        {
            FileInfo = new EdsFileInfo
            {
                FileName = "stream.dcf",
                FileVersion = 1,
                FileRevision = 0,
                EdsVersion = "4.0"
            },
            DeviceInfo = new DeviceInfo
            {
                VendorName = "Stream Vendor",
                ProductName = "Stream Device",
                SupportedBaudRates = new BaudRates { BaudRate250 = true, BaudRate500 = true }
            },
            DeviceCommissioning = new DeviceCommissioning
            {
                NodeId = 5,
                NodeName = "StreamNode",
                Baudrate = 500,
                NetNumber = 1,
                NetworkName = "Stream Network"
            },
            ObjectDictionary = new ObjectDictionary
            {
                MandatoryObjects = { 0x1000 },
                Objects =
                {
                    [0x1000] = new CanOpenObject
                    {
                        Index = 0x1000,
                        ParameterName = "Device Type",
                        ObjectType = 0x7,
                        DataType = 0x0007,
                        AccessType = AccessType.ReadOnly
                    }
                }
            }
        };
    }

    private static NodelistProject CreateMinimalCpj()
    {
        var cpj = new NodelistProject();
        cpj.Networks.Add(new NetworkTopology
        {
            NetName = "Plant",
            Nodes =
            {
                [2] = new NetworkNode
                {
                    NodeId = 2,
                    Present = true,
                    Name = "Drive",
                    DcfFileName = "drive.dcf"
                }
            }
        });
        return cpj;
    }
}
