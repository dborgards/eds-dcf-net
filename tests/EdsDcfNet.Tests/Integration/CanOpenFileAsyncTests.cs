namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using FluentAssertions;
using Xunit;

public class CanOpenFileAsyncTests
{
    [Fact]
    public async Task ReadEdsAsync_ValidFile_ReturnsElectronicDataSheet()
    {
        var result = await CanOpenFile.Eds.ReadFileAsync("Fixtures/sample_device.eds");

        result.Should().NotBeNull();
        result.DeviceInfo.ProductName.Should().Be("IO-Module 16x16");
    }

    [Fact]
    public async Task ReadEdsAsync_WithExplicitMaxInputSize_InvokesOverload()
    {
        var result = await CanOpenFile.Eds.ReadFileAsync("Fixtures/sample_device.eds", new CanOpenFileOptions { MaxInputSize = IniParser.DefaultMaxInputSize });

        result.Should().NotBeNull();
        result.FileInfo.FileName.Should().Be("sample_device.eds");
    }

    [Fact]
    public async Task ReadEdsAsync_MaxSubNumber_DoesNotHangAndParsesHighestSubObject()
    {
        var result = await EdsReadProbeRunner.RunAsync("async", "max_subnumber.eds", TimeSpan.FromSeconds(5));

        result.SubNumber.Should().Be(0xFF);
        result.HasSub0.Should().BeTrue();
        result.HasSubFF.Should().BeTrue();
    }

    [Fact]
    public async Task WriteEdsAsync_ValidModel_WritesAndReadsBackFile()
    {
        var eds = CreateMinimalEds();
        var tempFile = Path.GetTempFileName();

        try
        {
            await CanOpenFile.Eds.WriteFileAsync(eds, tempFile);
            var roundTrip = await CanOpenFile.Eds.ReadFileAsync(tempFile);

            roundTrip.FileInfo.FileName.Should().Be("async.eds");
            roundTrip.DeviceInfo.ProductName.Should().Be("Async Device");
            roundTrip.ObjectDictionary.Objects.Should().ContainKey(0x1000);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadDcfAsync_ValidFile_ReturnsDeviceConfigurationFile()
    {
        var result = await CanOpenFile.Dcf.ReadFileAsync("Fixtures/minimal.dcf");

        result.Should().NotBeNull();
        result.DeviceCommissioning.NodeId.Should().Be(5);
        result.DeviceCommissioning.Baudrate.Should().Be(500);
    }

    [Fact]
    public async Task ReadDcfAsync_WithExplicitMaxInputSize_InvokesOverload()
    {
        var result = await CanOpenFile.Dcf.ReadFileAsync("Fixtures/minimal.dcf", new CanOpenFileOptions { MaxInputSize = IniParser.DefaultMaxInputSize });

        result.Should().NotBeNull();
        result.DeviceCommissioning.NodeId.Should().Be(5);
    }

    [Fact]
    public async Task WriteDcfAsync_ValidModel_WritesAndReadsBackFile()
    {
        var dcf = CreateMinimalDcf();
        var tempFile = Path.GetTempFileName();

        try
        {
            await CanOpenFile.Dcf.WriteFileAsync(dcf, tempFile);
            var roundTrip = await CanOpenFile.Dcf.ReadFileAsync(tempFile);

            roundTrip.DeviceCommissioning.NodeId.Should().Be(5);
            roundTrip.DeviceCommissioning.Baudrate.Should().Be(500);
            roundTrip.ObjectDictionary.Objects.Should().ContainKey(0x1000);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadCpjAsync_ValidFile_ReturnsNodelistProject()
    {
        var tempFile = Path.GetTempFileName();
        var content = """
                      [Topology]
                      NetName=Async Network
                      Nodes=0x01
                      Node1Present=0x01
                      Node1Name=PLC
                      Node1DCFName=plc.dcf
                      """;

        try
        {
            await File.WriteAllTextAsync(tempFile, content);
            var result = await CanOpenFile.Cpj.ReadFileAsync(tempFile);

            result.Networks.Should().ContainSingle();
            result.Networks[0].NetName.Should().Be("Async Network");
            result.Networks[0].Nodes.Should().ContainKey(1);
            result.Networks[0].Nodes[1].DcfFileName.Should().Be("plc.dcf");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadCpjAsync_WithExplicitMaxInputSize_InvokesOverload()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "[Topology]\nNetName=SizeTest\nNodes=0");
            var result = await CanOpenFile.Cpj.ReadFileAsync(tempFile, new CanOpenFileOptions { MaxInputSize = IniParser.DefaultMaxInputSize });

            result.Should().NotBeNull();
            result.Networks[0].NetName.Should().Be("SizeTest");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteCpjAsync_ValidModel_WritesAndReadsBackFile()
    {
        var cpj = new NodelistProject();
        cpj.Networks.Add(new NetworkTopology
        {
            NetName = "Async Plant",
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

        var tempFile = Path.GetTempFileName();

        try
        {
            await CanOpenFile.Cpj.WriteFileAsync(cpj, tempFile);
            var roundTrip = await CanOpenFile.Cpj.ReadFileAsync(tempFile);

            roundTrip.Networks.Should().ContainSingle();
            roundTrip.Networks[0].Nodes.Should().ContainKey(2);
            roundTrip.Networks[0].Nodes[2].Name.Should().Be("Drive");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadXddAsync_ValidFile_ReturnsElectronicDataSheet()
    {
        var result = await CanOpenFile.Xdd.ReadFileAsync("Fixtures/sample_device.xdd");

        result.Should().NotBeNull();
        result.DeviceInfo.VendorName.Should().Be("Example Automation Inc.");
    }

    [Fact]
    public async Task ReadXddAsync_CustomMaxInputSizeTooSmall_ThrowsEdsParseException()
    {
        var act = () => CanOpenFile.Xdd.ReadFileAsync("Fixtures/sample_device.xdd", new CanOpenFileOptions { MaxInputSize = 256 });

        await act.Should().ThrowAsync<EdsParseException>()
            .WithMessage("*too large*");
    }

    [Fact]
    public async Task ReadXdcAsync_ValidFile_ReturnsDeviceConfigurationFile()
    {
        var result = await CanOpenFile.Xdc.ReadFileAsync("Fixtures/minimal.xdc");

        result.Should().NotBeNull();
        result.DeviceCommissioning.NodeId.Should().Be(5);
    }

    [Fact]
    public async Task ReadXdcAsync_CustomMaxInputSizeTooSmall_ThrowsEdsParseException()
    {
        var act = () => CanOpenFile.Xdc.ReadFileAsync("Fixtures/minimal.xdc", new CanOpenFileOptions { MaxInputSize = 256 });

        await act.Should().ThrowAsync<EdsParseException>()
            .WithMessage("*too large*");
    }

    [Fact]
    public async Task WriteXddAsync_ValidModel_WritesFile()
    {
        var eds = CreateMinimalEds();
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xdd");

        try
        {
            await CanOpenFile.Xdd.WriteFileAsync(eds, tempFile);
            File.Exists(tempFile).Should().BeTrue();
            File.ReadAllText(tempFile).Should().Contain("ISO15745ProfileContainer");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteXdcAsync_ValidModel_WritesFile()
    {
        var dcf = CreateMinimalDcf();
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xdc");

        try
        {
            await CanOpenFile.Xdc.WriteFileAsync(dcf, tempFile);
            File.Exists(tempFile).Should().BeTrue();
            File.ReadAllText(tempFile).Should().Contain("deviceCommissioning");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadEdsAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => CanOpenFile.Eds.ReadFileAsync("Fixtures/sample_device.eds", cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ReadDcfAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => CanOpenFile.Dcf.ReadFileAsync("Fixtures/minimal.dcf", cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ReadCpjAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "[Topology]\nNetName=CancelTest\nNodes=0");
            var act = () => CanOpenFile.Cpj.ReadFileAsync(tempFile, cancellationToken: cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadXddAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => CanOpenFile.Xdd.ReadFileAsync("Fixtures/sample_device.xdd", cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ReadXdcAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => CanOpenFile.Xdc.ReadFileAsync("Fixtures/minimal.xdc", cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WriteEdsAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        var act = () => CanOpenFile.Eds.WriteFileAsync(CreateMinimalEds(), filePath, cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task WriteDcfAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        var act = () => CanOpenFile.Dcf.WriteFileAsync(CreateMinimalDcf(), filePath, cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task WriteCpjAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        var cpj = new NodelistProject();
        cpj.Networks.Add(new NetworkTopology { NetName = "Cancel Network" });

        var act = () => CanOpenFile.Cpj.WriteFileAsync(cpj, filePath, cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task WriteXddAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xdd");

        var act = () => CanOpenFile.Xdd.WriteFileAsync(CreateMinimalEds(), filePath, cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task WriteXdcAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xdc");

        var act = () => CanOpenFile.Xdc.WriteFileAsync(CreateMinimalDcf(), filePath, cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        File.Exists(filePath).Should().BeFalse();
    }

    private static ElectronicDataSheet CreateMinimalEds()
    {
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo
            {
                FileName = "async.eds",
                FileVersion = 1,
                FileRevision = 0,
                EdsVersion = "4.0"
            },
            DeviceInfo = new DeviceInfo
            {
                VendorName = "Async Vendor",
                ProductName = "Async Device",
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
            AccessType = AccessType.ReadOnly,
            DefaultValue = "0x00000191",
            PdoMapping = false
        };

        return eds;
    }

    private static DeviceConfigurationFile CreateMinimalDcf()
    {
        return new DeviceConfigurationFile
        {
            FileInfo = new EdsFileInfo
            {
                FileName = "async.dcf",
                FileVersion = 1,
                FileRevision = 0,
                EdsVersion = "4.0"
            },
            DeviceInfo = new DeviceInfo
            {
                VendorName = "Async Vendor",
                ProductName = "Async Device",
                SupportedBaudRates = new BaudRates { BaudRate250 = true, BaudRate500 = true }
            },
            DeviceCommissioning = new DeviceCommissioning
            {
                NodeId = 5,
                NodeName = "AsyncNode",
                Baudrate = 500,
                NetNumber = 1,
                NetworkName = "Async Network"
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
                        AccessType = AccessType.ReadOnly,
                        DefaultValue = "0x00000191",
                        ParameterValue = "0x00000191",
                        PdoMapping = false
                    }
                }
            }
        };
    }
}
