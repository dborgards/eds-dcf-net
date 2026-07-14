namespace EdsDcfNet.Tests.Integration;

using System.Reflection;
using System.Text;
using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;

public class FormatCanOpenOperationsTests
{
    [Fact]
    public void ProtectedConstructor_PreservesElevenArgumentOverloadForBinaryCompatibility()
    {
        var formatType = typeof(FormatCanOpenOperations<ElectronicDataSheet>);
        var constructors = formatType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        var elevenArgConstructor = constructors.SingleOrDefault(ctor => ctor.GetParameters().Length == 11);
        elevenArgConstructor.Should().NotBeNull("subclasses compiled against the previous package must still bind to the 11-argument protected constructor");

        var twelveArgConstructor = constructors.SingleOrDefault(ctor => ctor.GetParameters().Length == 12);
        twelveArgConstructor.Should().NotBeNull("built-in format classes can opt into async validation via the 12-argument overload");
    }

    private static CanOpenFileOptions DefaultReadOptions =>
        new() { MaxInputSize = IniParser.DefaultMaxInputSize };

    private const string MinimalDcfContent = """
                                             [FileInfo]
                                             FileName=minimal.dcf
                                             FileVersion=1
                                             [DeviceInfo]
                                             VendorName=Vendor
                                             ProductName=Product
                                             [DeviceCommissioning]
                                             NodeID=5
                                             Baudrate=500
                                             [MandatoryObjects]
                                             SupportedObjects=1
                                             1=0x1000
                                             [1000]
                                             ParameterName=Device Type
                                             ObjectType=0x7
                                             DataType=0x0007
                                             AccessType=ro
                                             PDOMapping=0
                                             """;

    private const string MinimalCpjContent = "[Topology]\nNetName=Options Network\nNodes=0";
    [Fact]
    public void Dcf_ReadFile_WithOptions_MatchesLegacyReadDcfOverload()
    {
        var legacy = CanOpenFile.ReadDcf("Fixtures/minimal.dcf");
        var viaOptions = CanOpenFile.Dcf.ReadFile(
            "Fixtures/minimal.dcf",
            CanOpenFileOptions.Default);

        viaOptions.DeviceCommissioning.NodeId.Should().Be(legacy.DeviceCommissioning.NodeId);
    }

    [Fact]
    public void ReadDcf_WithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var optionsResult = CanOpenFile.ReadDcf(
            "Fixtures/minimal.dcf",
            new CanOpenFileOptions { MaxInputSize = IniParser.DefaultMaxInputSize });
        var legacyResult = CanOpenFile.ReadDcf("Fixtures/minimal.dcf", IniParser.DefaultMaxInputSize);

        legacyResult.DeviceCommissioning.NodeId.Should().Be(optionsResult.DeviceCommissioning.NodeId);
    }

    [Fact]
    public void Dcf_ReadFile_WithCustomMaxInputSize_EnforcesLimit()
    {
        var act = () => CanOpenFile.Dcf.ReadFile(
            "Fixtures/minimal.dcf",
            new CanOpenFileOptions { MaxInputSize = 10 });

        act.Should().Throw<EdsParseException>();
    }

    [Fact]
    public void Dcf_WriteToString_MatchesCanOpenFileWriteDcfToString()
    {
        var dcf = CanOpenFile.ReadDcf("Fixtures/minimal.dcf");

        CanOpenFile.Dcf.WriteToString(dcf).Should().Be(CanOpenFile.WriteDcfToString(dcf));
    }

    [Fact]
    public void ReadDcfFromString_WithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var optionsResult = CanOpenFile.ReadDcfFromString(MinimalDcfContent, DefaultReadOptions);
        var legacyResult = CanOpenFile.ReadDcfFromString(MinimalDcfContent, IniParser.DefaultMaxInputSize);

        legacyResult.DeviceCommissioning.NodeId.Should().Be(optionsResult.DeviceCommissioning.NodeId);
    }

    [Fact]
    public void ReadDcf_StreamWithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var bytes = Encoding.UTF8.GetBytes(MinimalDcfContent);
        using var optionsStream = new MemoryStream(bytes);
        using var legacyStream = new MemoryStream(bytes);

        var optionsResult = CanOpenFile.ReadDcf(optionsStream, DefaultReadOptions);
        var legacyResult = CanOpenFile.ReadDcf(legacyStream, IniParser.DefaultMaxInputSize);

        legacyResult.DeviceCommissioning.NodeId.Should().Be(optionsResult.DeviceCommissioning.NodeId);
    }

    [Fact]
    public async Task ReadDcfAsync_WithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var optionsResult = await CanOpenFile.ReadDcfAsync("Fixtures/minimal.dcf", DefaultReadOptions);
        var legacyResult = await CanOpenFile.ReadDcfAsync("Fixtures/minimal.dcf", IniParser.DefaultMaxInputSize);

        legacyResult.DeviceCommissioning.NodeId.Should().Be(optionsResult.DeviceCommissioning.NodeId);
    }

    [Fact]
    public async Task ReadDcfAsync_StreamWithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var bytes = Encoding.UTF8.GetBytes(MinimalDcfContent);
        using var optionsStream = new MemoryStream(bytes);
        using var legacyStream = new MemoryStream(bytes);

        var optionsResult = await CanOpenFile.ReadDcfAsync(optionsStream, DefaultReadOptions);
        var legacyResult = await CanOpenFile.ReadDcfAsync(legacyStream, IniParser.DefaultMaxInputSize);

        legacyResult.DeviceCommissioning.NodeId.Should().Be(optionsResult.DeviceCommissioning.NodeId);
    }

    [Fact]
    public async Task Dcf_WritePaths_ProduceReadableOutput()
    {
        var dcf = CanOpenFile.ReadDcf("Fixtures/minimal.dcf");
        var tempFile = Path.GetTempFileName();
        var asyncTempFile = Path.GetTempFileName();

        try
        {
            CanOpenFile.Dcf.WriteFile(dcf, tempFile);
            CanOpenFile.ReadDcf(tempFile).DeviceCommissioning.NodeId.Should().Be(dcf.DeviceCommissioning.NodeId);

            using (var syncStream = new MemoryStream())
            {
                CanOpenFile.Dcf.WriteStream(dcf, syncStream);
                syncStream.Position = 0;
                CanOpenFile.ReadDcf(syncStream).DeviceCommissioning.NodeId.Should().Be(dcf.DeviceCommissioning.NodeId);
            }

            await CanOpenFile.Dcf.WriteFileAsync(dcf, asyncTempFile);
            CanOpenFile.ReadDcf(asyncTempFile).DeviceCommissioning.NodeId.Should().Be(dcf.DeviceCommissioning.NodeId);

            using var asyncStream = new MemoryStream();
            await CanOpenFile.Dcf.WriteStreamAsync(dcf, asyncStream);
            asyncStream.Position = 0;
            CanOpenFile.ReadDcf(asyncStream).DeviceCommissioning.NodeId.Should().Be(dcf.DeviceCommissioning.NodeId);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
            if (File.Exists(asyncTempFile))
                File.Delete(asyncTempFile);
        }
    }

    [Fact]
    public void Cpj_ReadFile_WithOptions_MatchesLegacyReadCpjOverload()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "[Topology]\nNetName=Entry Point Network\nNodes=0");

            var legacy = CanOpenFile.ReadCpj(tempFile);
            var viaOptions = CanOpenFile.Cpj.ReadFile(tempFile, CanOpenFileOptions.Default);

            viaOptions.Networks[0].NetName.Should().Be(legacy.Networks[0].NetName);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ReadCpj_WithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, MinimalCpjContent);

            var optionsResult = CanOpenFile.ReadCpj(tempFile, DefaultReadOptions);
            var legacyResult = CanOpenFile.ReadCpj(tempFile, IniParser.DefaultMaxInputSize);

            legacyResult.Networks[0].NetName.Should().Be(optionsResult.Networks[0].NetName);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ReadCpjFromString_WithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var optionsResult = CanOpenFile.ReadCpjFromString(MinimalCpjContent, DefaultReadOptions);
        var legacyResult = CanOpenFile.ReadCpjFromString(MinimalCpjContent, IniParser.DefaultMaxInputSize);

        legacyResult.Networks[0].NetName.Should().Be(optionsResult.Networks[0].NetName);
    }

    [Fact]
    public void ReadCpj_StreamWithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var bytes = Encoding.UTF8.GetBytes(MinimalCpjContent);
        using var optionsStream = new MemoryStream(bytes);
        using var legacyStream = new MemoryStream(bytes);

        var optionsResult = CanOpenFile.ReadCpj(optionsStream, DefaultReadOptions);
        var legacyResult = CanOpenFile.ReadCpj(legacyStream, IniParser.DefaultMaxInputSize);

        legacyResult.Networks[0].NetName.Should().Be(optionsResult.Networks[0].NetName);
    }

    [Fact]
    public async Task ReadCpjAsync_WithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, MinimalCpjContent);

            var optionsResult = await CanOpenFile.ReadCpjAsync(tempFile, DefaultReadOptions);
            var legacyResult = await CanOpenFile.ReadCpjAsync(tempFile, IniParser.DefaultMaxInputSize);

            legacyResult.Networks[0].NetName.Should().Be(optionsResult.Networks[0].NetName);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadCpjAsync_StreamWithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var bytes = Encoding.UTF8.GetBytes(MinimalCpjContent);
        using var optionsStream = new MemoryStream(bytes);
        using var legacyStream = new MemoryStream(bytes);

        var optionsResult = await CanOpenFile.ReadCpjAsync(optionsStream, DefaultReadOptions);
        var legacyResult = await CanOpenFile.ReadCpjAsync(legacyStream, IniParser.DefaultMaxInputSize);

        legacyResult.Networks[0].NetName.Should().Be(optionsResult.Networks[0].NetName);
    }

    [Fact]
    public void Cpj_ReadString_WithCustomMaxInputSize_EnforcesLimit()
    {
        const string content = "[Topology]\nNetName=Too Large\nNodes=0";

        var act = () => CanOpenFile.Cpj.ReadString(
            content,
            new CanOpenFileOptions { MaxInputSize = 10 });

        act.Should().Throw<EdsParseException>();
    }

    [Fact]
    public void Cpj_WriteToString_MatchesCanOpenFileWriteCpjToString()
    {
        var cpj = CanOpenFile.ReadCpjFromString("[Topology]\nNetName=Write Test\nNodes=0");

        CanOpenFile.Cpj.WriteToString(cpj).Should().Be(CanOpenFile.WriteCpjToString(cpj));
    }

    [Fact]
    public async Task Cpj_WritePaths_ProduceReadableOutput()
    {
        var cpj = CanOpenFile.ReadCpjFromString(MinimalCpjContent);
        var tempFile = Path.GetTempFileName();
        var asyncTempFile = Path.GetTempFileName();

        try
        {
            CanOpenFile.Cpj.WriteFile(cpj, tempFile);
            CanOpenFile.ReadCpj(tempFile).Networks[0].NetName.Should().Be(cpj.Networks[0].NetName);

            using (var syncStream = new MemoryStream())
            {
                CanOpenFile.Cpj.WriteStream(cpj, syncStream);
                syncStream.Position = 0;
                CanOpenFile.ReadCpj(syncStream).Networks[0].NetName.Should().Be(cpj.Networks[0].NetName);
            }

            await CanOpenFile.Cpj.WriteFileAsync(cpj, asyncTempFile);
            CanOpenFile.ReadCpj(asyncTempFile).Networks[0].NetName.Should().Be(cpj.Networks[0].NetName);

            using var asyncStream = new MemoryStream();
            await CanOpenFile.Cpj.WriteStreamAsync(cpj, asyncStream);
            asyncStream.Position = 0;
            CanOpenFile.ReadCpj(asyncStream).Networks[0].NetName.Should().Be(cpj.Networks[0].NetName);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
            if (File.Exists(asyncTempFile))
                File.Delete(asyncTempFile);
        }
    }

    [Fact]
    public void Xdd_ReadFile_WithOptions_MatchesLegacyReadXddOverload()
    {
        var legacy = CanOpenFile.ReadXdd("Fixtures/sample_device.xdd");
        var viaOptions = CanOpenFile.Xdd.ReadFile(
            "Fixtures/sample_device.xdd",
            CanOpenFileOptions.Default);

        viaOptions.DeviceInfo.ProductName.Should().Be(legacy.DeviceInfo.ProductName);
    }

    [Fact]
    public void ReadXdd_WithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var optionsResult = CanOpenFile.ReadXdd(
            "Fixtures/sample_device.xdd",
            new CanOpenFileOptions { MaxInputSize = IniParser.DefaultMaxInputSize });
        var legacyResult = CanOpenFile.ReadXdd("Fixtures/sample_device.xdd", IniParser.DefaultMaxInputSize);

        legacyResult.DeviceInfo.ProductName.Should().Be(optionsResult.DeviceInfo.ProductName);
    }

    [Fact]
    public void Xdd_ReadFile_WithCustomMaxInputSize_EnforcesLimit()
    {
        var act = () => CanOpenFile.Xdd.ReadFile(
            "Fixtures/sample_device.xdd",
            new CanOpenFileOptions { MaxInputSize = 256 });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*too large*");
    }

    [Fact]
    public void Xdd_WriteToString_MatchesCanOpenFileWriteXddToString()
    {
        var xdd = CanOpenFile.ReadXdd("Fixtures/sample_device.xdd");

        CanOpenFile.Xdd.WriteToString(xdd).Should().Be(CanOpenFile.WriteXddToString(xdd));
    }

    [Fact]
    public void ReadXddFromString_WithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var content = File.ReadAllText("Fixtures/sample_device.xdd");
        var optionsResult = CanOpenFile.ReadXddFromString(content, DefaultReadOptions);
        var legacyResult = CanOpenFile.ReadXddFromString(content, IniParser.DefaultMaxInputSize);

        legacyResult.DeviceInfo.ProductName.Should().Be(optionsResult.DeviceInfo.ProductName);
    }

    [Fact]
    public void ReadXdd_StreamWithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var content = File.ReadAllText("Fixtures/sample_device.xdd");
        var bytes = Encoding.UTF8.GetBytes(content);
        using var optionsStream = new MemoryStream(bytes);
        using var legacyStream = new MemoryStream(bytes);

        var optionsResult = CanOpenFile.ReadXdd(optionsStream, DefaultReadOptions);
        var legacyResult = CanOpenFile.ReadXdd(legacyStream, IniParser.DefaultMaxInputSize);

        legacyResult.DeviceInfo.ProductName.Should().Be(optionsResult.DeviceInfo.ProductName);
    }

    [Fact]
    public async Task ReadXddAsync_WithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var optionsResult = await CanOpenFile.ReadXddAsync("Fixtures/sample_device.xdd", DefaultReadOptions);
        var legacyResult = await CanOpenFile.ReadXddAsync("Fixtures/sample_device.xdd", IniParser.DefaultMaxInputSize);

        legacyResult.DeviceInfo.ProductName.Should().Be(optionsResult.DeviceInfo.ProductName);
    }

    [Fact]
    public async Task ReadXddAsync_StreamWithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var content = File.ReadAllText("Fixtures/sample_device.xdd");
        var bytes = Encoding.UTF8.GetBytes(content);
        using var optionsStream = new MemoryStream(bytes);
        using var legacyStream = new MemoryStream(bytes);

        var optionsResult = await CanOpenFile.ReadXddAsync(optionsStream, DefaultReadOptions);
        var legacyResult = await CanOpenFile.ReadXddAsync(legacyStream, IniParser.DefaultMaxInputSize);

        legacyResult.DeviceInfo.ProductName.Should().Be(optionsResult.DeviceInfo.ProductName);
    }

    [Fact]
    public async Task Xdd_WritePaths_ProduceReadableOutput()
    {
        var xdd = CanOpenFile.ReadXdd("Fixtures/sample_device.xdd");
        var tempFile = Path.GetTempFileName();
        var asyncTempFile = Path.GetTempFileName();

        try
        {
            CanOpenFile.Xdd.WriteFile(xdd, tempFile);
            CanOpenFile.ReadXdd(tempFile).DeviceInfo.ProductName.Should().Be(xdd.DeviceInfo.ProductName);

            using (var syncStream = new MemoryStream())
            {
                CanOpenFile.Xdd.WriteStream(xdd, syncStream);
                syncStream.Position = 0;
                CanOpenFile.ReadXdd(syncStream).DeviceInfo.ProductName.Should().Be(xdd.DeviceInfo.ProductName);
            }

            await CanOpenFile.Xdd.WriteFileAsync(xdd, asyncTempFile);
            CanOpenFile.ReadXdd(asyncTempFile).DeviceInfo.ProductName.Should().Be(xdd.DeviceInfo.ProductName);

            using var asyncStream = new MemoryStream();
            await CanOpenFile.Xdd.WriteStreamAsync(xdd, asyncStream);
            asyncStream.Position = 0;
            CanOpenFile.ReadXdd(asyncStream).DeviceInfo.ProductName.Should().Be(xdd.DeviceInfo.ProductName);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
            if (File.Exists(asyncTempFile))
                File.Delete(asyncTempFile);
        }
    }

    [Fact]
    public void Xdc_ReadFile_WithOptions_MatchesLegacyReadXdcOverload()
    {
        var legacy = CanOpenFile.ReadXdc("Fixtures/minimal.xdc");
        var viaOptions = CanOpenFile.Xdc.ReadFile(
            "Fixtures/minimal.xdc",
            CanOpenFileOptions.Default);

        viaOptions.DeviceCommissioning.NodeId.Should().Be(legacy.DeviceCommissioning.NodeId);
    }

    [Fact]
    public void ReadXdc_WithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var optionsResult = CanOpenFile.ReadXdc(
            "Fixtures/minimal.xdc",
            new CanOpenFileOptions { MaxInputSize = IniParser.DefaultMaxInputSize });
        var legacyResult = CanOpenFile.ReadXdc("Fixtures/minimal.xdc", IniParser.DefaultMaxInputSize);

        legacyResult.DeviceCommissioning.NodeId.Should().Be(optionsResult.DeviceCommissioning.NodeId);
    }

    [Fact]
    public void Xdc_ReadFile_WithCustomMaxInputSize_EnforcesLimit()
    {
        var act = () => CanOpenFile.Xdc.ReadFile(
            "Fixtures/minimal.xdc",
            new CanOpenFileOptions { MaxInputSize = 256 });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*too large*");
    }

    [Fact]
    public void Xdc_WriteToString_MatchesCanOpenFileWriteXdcToString()
    {
        var xdc = CanOpenFile.ReadXdc("Fixtures/minimal.xdc");

        CanOpenFile.Xdc.WriteToString(xdc).Should().Be(CanOpenFile.WriteXdcToString(xdc));
    }

    [Fact]
    public void ReadXdcFromString_WithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var content = File.ReadAllText("Fixtures/minimal.xdc");
        var optionsResult = CanOpenFile.ReadXdcFromString(content, DefaultReadOptions);
        var legacyResult = CanOpenFile.ReadXdcFromString(content, IniParser.DefaultMaxInputSize);

        legacyResult.DeviceCommissioning.NodeId.Should().Be(optionsResult.DeviceCommissioning.NodeId);
    }

    [Fact]
    public void ReadXdc_StreamWithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var content = File.ReadAllText("Fixtures/minimal.xdc");
        var bytes = Encoding.UTF8.GetBytes(content);
        using var optionsStream = new MemoryStream(bytes);
        using var legacyStream = new MemoryStream(bytes);

        var optionsResult = CanOpenFile.ReadXdc(optionsStream, DefaultReadOptions);
        var legacyResult = CanOpenFile.ReadXdc(legacyStream, IniParser.DefaultMaxInputSize);

        legacyResult.DeviceCommissioning.NodeId.Should().Be(optionsResult.DeviceCommissioning.NodeId);
    }

    [Fact]
    public async Task ReadXdcAsync_WithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var optionsResult = await CanOpenFile.ReadXdcAsync("Fixtures/minimal.xdc", DefaultReadOptions);
        var legacyResult = await CanOpenFile.ReadXdcAsync("Fixtures/minimal.xdc", IniParser.DefaultMaxInputSize);

        legacyResult.DeviceCommissioning.NodeId.Should().Be(optionsResult.DeviceCommissioning.NodeId);
    }

    [Fact]
    public async Task ReadXdcAsync_StreamWithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var content = File.ReadAllText("Fixtures/minimal.xdc");
        var bytes = Encoding.UTF8.GetBytes(content);
        using var optionsStream = new MemoryStream(bytes);
        using var legacyStream = new MemoryStream(bytes);

        var optionsResult = await CanOpenFile.ReadXdcAsync(optionsStream, DefaultReadOptions);
        var legacyResult = await CanOpenFile.ReadXdcAsync(legacyStream, IniParser.DefaultMaxInputSize);

        legacyResult.DeviceCommissioning.NodeId.Should().Be(optionsResult.DeviceCommissioning.NodeId);
    }

    [Fact]
    public async Task Xdc_WritePaths_ProduceReadableOutput()
    {
        var xdc = CanOpenFile.ReadXdc("Fixtures/minimal.xdc");
        var tempFile = Path.GetTempFileName();
        var asyncTempFile = Path.GetTempFileName();

        try
        {
            CanOpenFile.Xdc.WriteFile(xdc, tempFile);
            CanOpenFile.ReadXdc(tempFile).DeviceCommissioning.NodeId.Should().Be(xdc.DeviceCommissioning.NodeId);

            using (var syncStream = new MemoryStream())
            {
                CanOpenFile.Xdc.WriteStream(xdc, syncStream);
                syncStream.Position = 0;
                CanOpenFile.ReadXdc(syncStream).DeviceCommissioning.NodeId.Should().Be(xdc.DeviceCommissioning.NodeId);
            }

            await CanOpenFile.Xdc.WriteFileAsync(xdc, asyncTempFile);
            CanOpenFile.ReadXdc(asyncTempFile).DeviceCommissioning.NodeId.Should().Be(xdc.DeviceCommissioning.NodeId);

            using var asyncStream = new MemoryStream();
            await CanOpenFile.Xdc.WriteStreamAsync(xdc, asyncStream);
            asyncStream.Position = 0;
            CanOpenFile.ReadXdc(asyncStream).DeviceCommissioning.NodeId.Should().Be(xdc.DeviceCommissioning.NodeId);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
            if (File.Exists(asyncTempFile))
                File.Delete(asyncTempFile);
        }
    }

    [Fact]
    public async Task Eds_AsyncWritePathsWithOptions_ProduceReadableOutput()
    {
        var eds = CanOpenFile.ReadEds("Fixtures/sample_device.eds");
        var asyncTempFile = Path.GetTempFileName();

        try
        {
            await CanOpenFile.WriteEdsAsync(eds, asyncTempFile, CanOpenWriteOptions.Default);
            CanOpenFile.ReadEds(asyncTempFile).DeviceInfo.ProductName.Should().Be(eds.DeviceInfo.ProductName);

            using var asyncStream = new MemoryStream();
            await CanOpenFile.WriteEdsAsync(eds, asyncStream, CanOpenWriteOptions.Default);
            asyncStream.Position = 0;
            CanOpenFile.ReadEds(asyncStream).DeviceInfo.ProductName.Should().Be(eds.DeviceInfo.ProductName);
        }
        finally
        {
            if (File.Exists(asyncTempFile))
                File.Delete(asyncTempFile);
        }
    }

    [Fact]
    public async Task Dcf_AsyncWritePathsWithOptions_ProduceReadableOutput()
    {
        var dcf = CanOpenFile.ReadDcf("Fixtures/minimal.dcf");
        var asyncTempFile = Path.GetTempFileName();

        try
        {
            await CanOpenFile.WriteDcfAsync(dcf, asyncTempFile, CanOpenWriteOptions.Default);
            CanOpenFile.ReadDcf(asyncTempFile).DeviceCommissioning.NodeId.Should().Be(dcf.DeviceCommissioning.NodeId);

            using var asyncStream = new MemoryStream();
            await CanOpenFile.WriteDcfAsync(dcf, asyncStream, CanOpenWriteOptions.Default);
            asyncStream.Position = 0;
            CanOpenFile.ReadDcf(asyncStream).DeviceCommissioning.NodeId.Should().Be(dcf.DeviceCommissioning.NodeId);
        }
        finally
        {
            if (File.Exists(asyncTempFile))
                File.Delete(asyncTempFile);
        }
    }

    [Fact]
    public async Task Cpj_AsyncWritePathsWithOptions_ProduceReadableOutput()
    {
        var cpj = CanOpenFile.ReadCpjFromString(MinimalCpjContent);
        var asyncTempFile = Path.GetTempFileName();

        try
        {
            await CanOpenFile.WriteCpjAsync(cpj, asyncTempFile, CanOpenWriteOptions.Default);
            CanOpenFile.ReadCpj(asyncTempFile).Networks[0].NetName.Should().Be(cpj.Networks[0].NetName);

            using var asyncStream = new MemoryStream();
            await CanOpenFile.WriteCpjAsync(cpj, asyncStream, CanOpenWriteOptions.Default);
            asyncStream.Position = 0;
            CanOpenFile.ReadCpj(asyncStream).Networks[0].NetName.Should().Be(cpj.Networks[0].NetName);
        }
        finally
        {
            if (File.Exists(asyncTempFile))
                File.Delete(asyncTempFile);
        }
    }

    [Fact]
    public async Task Xdd_AsyncWritePathsWithOptions_ProduceReadableOutput()
    {
        var xdd = CanOpenFile.ReadXdd("Fixtures/sample_device.xdd");
        var asyncTempFile = Path.GetTempFileName();

        try
        {
            await CanOpenFile.WriteXddAsync(xdd, asyncTempFile, CanOpenWriteOptions.Default);
            CanOpenFile.ReadXdd(asyncTempFile).DeviceInfo.ProductName.Should().Be(xdd.DeviceInfo.ProductName);

            using var asyncStream = new MemoryStream();
            await CanOpenFile.WriteXddAsync(xdd, asyncStream, CanOpenWriteOptions.Default);
            asyncStream.Position = 0;
            CanOpenFile.ReadXdd(asyncStream).DeviceInfo.ProductName.Should().Be(xdd.DeviceInfo.ProductName);
        }
        finally
        {
            if (File.Exists(asyncTempFile))
                File.Delete(asyncTempFile);
        }
    }

    [Fact]
    public async Task Xdc_AsyncWritePathsWithOptions_ProduceReadableOutput()
    {
        var xdc = CanOpenFile.ReadXdc("Fixtures/minimal.xdc");
        var asyncTempFile = Path.GetTempFileName();

        try
        {
            await CanOpenFile.WriteXdcAsync(xdc, asyncTempFile, CanOpenWriteOptions.Default);
            CanOpenFile.ReadXdc(asyncTempFile).DeviceCommissioning.NodeId.Should().Be(xdc.DeviceCommissioning.NodeId);

            using var asyncStream = new MemoryStream();
            await CanOpenFile.WriteXdcAsync(xdc, asyncStream, CanOpenWriteOptions.Default);
            asyncStream.Position = 0;
            CanOpenFile.ReadXdc(asyncStream).DeviceCommissioning.NodeId.Should().Be(xdc.DeviceCommissioning.NodeId);
        }
        finally
        {
            if (File.Exists(asyncTempFile))
                File.Delete(asyncTempFile);
        }
    }
}
