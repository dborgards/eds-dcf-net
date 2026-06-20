namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Parsers;

public class FormatCanOpenOperationsTests
{
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
        const string content = "[Topology]\nNetName=Options Network\nNodes=0";
        var optionsResult = CanOpenFile.ReadCpjFromString(
            content,
            new CanOpenFileOptions { MaxInputSize = IniParser.DefaultMaxInputSize });
        var legacyResult = CanOpenFile.ReadCpjFromString(content, IniParser.DefaultMaxInputSize);

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
}
