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

        var eds = CanOpenFile.Eds.ReadStream(stream);

        eds.DeviceInfo.ProductName.Should().Be("IO-Module 16x16");
        stream.CanRead.Should().BeTrue();
    }

    [Fact]
    public void WriteEds_Stream_WritesContentAndKeepsStreamOpen()
    {
        var eds = CreateMinimalEds();
        using var stream = new MemoryStream();

        CanOpenFile.Eds.WriteStream(eds, stream);

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

        CanOpenFile.Dcf.WriteStream(dcf, writeStream);
        writeStream.CanWrite.Should().BeTrue();
        writeStream.Position = 0;

        var parsed = CanOpenFile.Dcf.ReadStream(writeStream);

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
        CanOpenFile.Cpj.WriteStream(cpj, stream);
        stream.Position = 0;

        var parsed = CanOpenFile.Cpj.ReadStream(stream);

        parsed.Networks.Should().ContainSingle();
        parsed.Networks[0].Nodes.Should().ContainKey(2);
        stream.CanRead.Should().BeTrue();
    }

    [Fact]
    public void ReadXdd_Stream_ParsesAndKeepsStreamOpen()
    {
        using var stream = File.OpenRead("Fixtures/sample_device.xdd");

        var eds = CanOpenFile.Xdd.ReadStream(stream);

        eds.DeviceInfo.VendorName.Should().Be("Example Automation Inc.");
        stream.CanRead.Should().BeTrue();
    }

    [Fact]
    public void ReadXdc_Stream_ParsesAndKeepsStreamOpen()
    {
        using var stream = File.OpenRead("Fixtures/minimal.xdc");

        var dcf = CanOpenFile.Xdc.ReadStream(stream);

        dcf.DeviceCommissioning.NodeId.Should().Be(5);
        stream.CanRead.Should().BeTrue();
    }

    [Fact]
    public async Task ReadWriteDcfAsync_Stream_RoundTripAndKeepsStreamsOpen()
    {
        var dcf = CreateMinimalDcf();
        using var writeStream = new MemoryStream();

        await CanOpenFile.Dcf.WriteStreamAsync(dcf, writeStream);
        writeStream.Position = 0;
        var parsed = await CanOpenFile.Dcf.ReadStreamAsync(writeStream);

        parsed.DeviceCommissioning.NodeId.Should().Be(5);
        writeStream.CanRead.Should().BeTrue();
    }

    [Fact]
    public async Task ReadWriteXddAsync_Stream_RoundTripAndKeepsStreamsOpen()
    {
        var eds = CreateMinimalEds();
        using var stream = new MemoryStream();

        await CanOpenFile.Xdd.WriteStreamAsync(eds, stream);
        stream.Position = 0;
        var parsed = await CanOpenFile.Xdd.ReadStreamAsync(stream);

        parsed.DeviceInfo.ProductName.Should().Be("Stream Device");
        stream.CanRead.Should().BeTrue();
    }

    [Fact]
    public void ReadEds_Stream_WithExplicitMaxInputSize_Parses()
    {
        using var stream = File.OpenRead("Fixtures/sample_device.eds");
        var eds = CanOpenFile.Eds.ReadStream(stream, new CanOpenFileOptions { MaxInputSize = stream.Length + 128 });
        eds.DeviceInfo.ProductName.Should().Be("IO-Module 16x16");
    }

    [Fact]
    public async Task ReadEdsAsync_Stream_WithExplicitMaxInputSize_Parses()
    {
        using var stream = File.OpenRead("Fixtures/sample_device.eds");
        var eds = await CanOpenFile.Eds.ReadStreamAsync(stream, new CanOpenFileOptions { MaxInputSize = stream.Length + 128 });
        eds.DeviceInfo.ProductName.Should().Be("IO-Module 16x16");
    }

    [Fact]
    public void ReadDcf_Stream_WithExplicitMaxInputSize_Parses()
    {
        using var stream = File.OpenRead("Fixtures/minimal.dcf");
        var dcf = CanOpenFile.Dcf.ReadStream(stream, new CanOpenFileOptions { MaxInputSize = stream.Length + 128 });
        dcf.DeviceCommissioning.NodeId.Should().Be(5);
    }

    [Fact]
    public async Task ReadDcfAsync_Stream_WithExplicitMaxInputSize_Parses()
    {
        using var stream = File.OpenRead("Fixtures/minimal.dcf");
        var dcf = await CanOpenFile.Dcf.ReadStreamAsync(stream, new CanOpenFileOptions { MaxInputSize = stream.Length + 128 });
        dcf.DeviceCommissioning.NodeId.Should().Be(5);
    }

    [Fact]
    public void ReadCpj_Stream_WithExplicitMaxInputSize_Parses()
    {
        var cpjText = "[Topology]\nNetName=Plant\nNodes=0\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(cpjText));
        var cpj = CanOpenFile.Cpj.ReadStream(stream, new CanOpenFileOptions { MaxInputSize = cpjText.Length + 16 });
        cpj.Networks.Should().ContainSingle();
        cpj.Networks[0].NetName.Should().Be("Plant");
    }

    [Fact]
    public async Task ReadCpjAsync_Stream_WithExplicitMaxInputSize_Parses()
    {
        var cpjText = "[Topology]\nNetName=Plant\nNodes=0\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(cpjText));
        var cpj = await CanOpenFile.Cpj.ReadStreamAsync(stream, new CanOpenFileOptions { MaxInputSize = cpjText.Length + 16 });
        cpj.Networks.Should().ContainSingle();
        cpj.Networks[0].NetName.Should().Be("Plant");
    }

    [Fact]
    public void ReadXdd_Stream_WithExplicitMaxInputSize_Parses()
    {
        using var stream = File.OpenRead("Fixtures/sample_device.xdd");
        var eds = CanOpenFile.Xdd.ReadStream(stream, new CanOpenFileOptions { MaxInputSize = stream.Length + 128 });
        eds.DeviceInfo.VendorName.Should().Be("Example Automation Inc.");
    }

    [Fact]
    public async Task ReadXddAsync_Stream_WithExplicitMaxInputSize_Parses()
    {
        using var stream = File.OpenRead("Fixtures/sample_device.xdd");
        var eds = await CanOpenFile.Xdd.ReadStreamAsync(stream, new CanOpenFileOptions { MaxInputSize = stream.Length + 128 });
        eds.DeviceInfo.VendorName.Should().Be("Example Automation Inc.");
    }

    [Fact]
    public void ReadXdc_Stream_WithExplicitMaxInputSize_Parses()
    {
        using var stream = File.OpenRead("Fixtures/minimal.xdc");
        var dcf = CanOpenFile.Xdc.ReadStream(stream, new CanOpenFileOptions { MaxInputSize = stream.Length + 128 });
        dcf.DeviceCommissioning.NodeId.Should().Be(5);
    }

    [Fact]
    public async Task ReadXdcAsync_Stream_WithExplicitMaxInputSize_Parses()
    {
        using var stream = File.OpenRead("Fixtures/minimal.xdc");
        var dcf = await CanOpenFile.Xdc.ReadStreamAsync(stream, new CanOpenFileOptions { MaxInputSize = stream.Length + 128 });
        dcf.DeviceCommissioning.NodeId.Should().Be(5);
    }

    [Fact]
    public async Task WriteReadCpjAsync_Stream_RoundTrip()
    {
        var cpj = CreateMinimalCpj();
        using var stream = new MemoryStream();

        await CanOpenFile.Cpj.WriteStreamAsync(cpj, stream);
        stream.Position = 0;
        var parsed = await CanOpenFile.Cpj.ReadStreamAsync(stream);

        parsed.Networks.Should().ContainSingle();
        parsed.Networks[0].Nodes.Should().ContainKey(2);
    }

    [Fact]
    public async Task WriteReadXdd_Stream_AndAsync_RoundTrip()
    {
        var eds = CreateMinimalEds();

        using var syncStream = new MemoryStream();
        CanOpenFile.Xdd.WriteStream(eds, syncStream);
        syncStream.Position = 0;
        CanOpenFile.Xdd.ReadStream(syncStream).DeviceInfo.ProductName.Should().Be("Stream Device");

        using var asyncStream = new MemoryStream();
        await CanOpenFile.Xdd.WriteStreamAsync(eds, asyncStream);
        asyncStream.Position = 0;
        var reparsed = await CanOpenFile.Xdd.ReadStreamAsync(asyncStream);
        reparsed.DeviceInfo.ProductName.Should().Be("Stream Device");
    }

    [Fact]
    public async Task WriteReadXdc_Stream_AndAsync_RoundTrip()
    {
        var dcf = CreateMinimalDcf();

        using var syncStream = new MemoryStream();
        CanOpenFile.Xdc.WriteStream(dcf, syncStream);
        syncStream.Position = 0;
        CanOpenFile.Xdc.ReadStream(syncStream).DeviceCommissioning.NodeId.Should().Be(5);

        using var asyncStream = new MemoryStream();
        await CanOpenFile.Xdc.WriteStreamAsync(dcf, asyncStream);
        asyncStream.Position = 0;
        var reparsed = await CanOpenFile.Xdc.ReadStreamAsync(asyncStream);
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
