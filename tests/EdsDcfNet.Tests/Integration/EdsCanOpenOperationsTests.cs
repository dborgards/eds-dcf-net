namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Parsers;

public class EdsCanOpenOperationsTests
{
    [Fact]
    public void CanOpenFileOptions_Default_UsesReaderDefaultMaxInputSize()
    {
        CanOpenFileOptions.Default.MaxInputSize.Should().Be(ReaderDefaults.DefaultMaxInputSize);
    }

    [Fact]
    public void Eds_ReadFile_WithOptions_MatchesLegacyReadEdsOverload()
    {
        var legacy = CanOpenFile.ReadEds("Fixtures/sample_device.eds");
        var viaOptions = CanOpenFile.Eds.ReadFile(
            "Fixtures/sample_device.eds",
            CanOpenFileOptions.Default);

        viaOptions.DeviceInfo.ProductName.Should().Be(legacy.DeviceInfo.ProductName);
    }

    [Fact]
    public void Eds_ReadFile_WithOptions_MatchesMaxInputSizeOverload()
    {
        var optionsResult = CanOpenFile.Eds.ReadFile(
            "Fixtures/sample_device.eds",
            new CanOpenFileOptions { MaxInputSize = IniParser.DefaultMaxInputSize });
        var legacyResult = CanOpenFile.ReadEds("Fixtures/sample_device.eds", IniParser.DefaultMaxInputSize);

        legacyResult.DeviceInfo.ProductName.Should().Be(optionsResult.DeviceInfo.ProductName);
    }

    [Fact]
    public void Eds_ReadFile_WithCustomMaxInputSize_EnforcesLimit()
    {
        var act = () => CanOpenFile.Eds.ReadFile(
            "Fixtures/sample_device.eds",
            new CanOpenFileOptions { MaxInputSize = 10 });

        act.Should().Throw<EdsParseException>();
    }

    [Fact]
    public void Eds_WriteToString_MatchesCanOpenFileWriteEdsToString()
    {
        var eds = CanOpenFile.ReadEds("Fixtures/sample_device.eds");

        var viaEntryPoint = CanOpenFile.Eds.WriteToString(eds);
        var viaFacade = CanOpenFile.WriteEdsToString(eds);

        viaEntryPoint.Should().Be(viaFacade);
    }

    [Fact]
    public void Eds_ReadString_WithOptions_MatchesMaxInputSizeOverload()
    {
        var content = File.ReadAllText("Fixtures/sample_device.eds");
        var options = new CanOpenFileOptions { MaxInputSize = IniParser.DefaultMaxInputSize };

        var optionsResult = CanOpenFile.Eds.ReadString(content, options);
        var legacyResult = CanOpenFile.ReadEdsFromString(content, IniParser.DefaultMaxInputSize);

        legacyResult.DeviceInfo.ProductName.Should().Be(optionsResult.DeviceInfo.ProductName);
    }

    [Fact]
    public void Eds_ReadStream_WithOptions_MatchesMaxInputSizeOverload()
    {
        var content = File.ReadAllText("Fixtures/sample_device.eds");
        using var optionsStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        using var legacyStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var options = new CanOpenFileOptions { MaxInputSize = IniParser.DefaultMaxInputSize };

        var optionsResult = CanOpenFile.Eds.ReadStream(optionsStream, options);
        var legacyResult = CanOpenFile.ReadEds(legacyStream, IniParser.DefaultMaxInputSize);

        legacyResult.DeviceInfo.ProductName.Should().Be(optionsResult.DeviceInfo.ProductName);
    }

    [Fact]
    public async Task Eds_ReadFileAsync_WithOptions_MatchesMaxInputSizeOverload()
    {
        var options = new CanOpenFileOptions { MaxInputSize = IniParser.DefaultMaxInputSize };

        var optionsResult = await CanOpenFile.Eds.ReadFileAsync("Fixtures/sample_device.eds", options);
        var legacyResult = await CanOpenFile.ReadEdsAsync("Fixtures/sample_device.eds", IniParser.DefaultMaxInputSize);

        legacyResult.DeviceInfo.ProductName.Should().Be(optionsResult.DeviceInfo.ProductName);
    }

    [Fact]
    public async Task Eds_ReadStreamAsync_WithOptions_MatchesMaxInputSizeOverload()
    {
        var content = File.ReadAllText("Fixtures/sample_device.eds");
        using var optionsStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        using var legacyStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var options = new CanOpenFileOptions { MaxInputSize = IniParser.DefaultMaxInputSize };

        var optionsResult = await CanOpenFile.Eds.ReadStreamAsync(optionsStream, options);
        var legacyResult = await CanOpenFile.ReadEdsAsync(legacyStream, IniParser.DefaultMaxInputSize);

        legacyResult.DeviceInfo.ProductName.Should().Be(optionsResult.DeviceInfo.ProductName);
    }
}
