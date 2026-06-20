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
    public void ReadEds_WithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var optionsResult = CanOpenFile.ReadEds(
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
    public void ReadEdsFromString_WithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var content = File.ReadAllText("Fixtures/sample_device.eds");
        var options = new CanOpenFileOptions { MaxInputSize = IniParser.DefaultMaxInputSize };

        var optionsResult = CanOpenFile.ReadEdsFromString(content, options);
        var legacyResult = CanOpenFile.ReadEdsFromString(content, IniParser.DefaultMaxInputSize);

        legacyResult.DeviceInfo.ProductName.Should().Be(optionsResult.DeviceInfo.ProductName);
    }

    [Fact]
    public void ReadEds_Stream_WithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var content = File.ReadAllText("Fixtures/sample_device.eds");
        using var optionsStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        using var legacyStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var options = new CanOpenFileOptions { MaxInputSize = IniParser.DefaultMaxInputSize };

        var optionsResult = CanOpenFile.ReadEds(optionsStream, options);
        var legacyResult = CanOpenFile.ReadEds(legacyStream, IniParser.DefaultMaxInputSize);

        legacyResult.DeviceInfo.ProductName.Should().Be(optionsResult.DeviceInfo.ProductName);
    }

    [Fact]
    public async Task ReadEdsAsync_File_WithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var options = new CanOpenFileOptions { MaxInputSize = IniParser.DefaultMaxInputSize };

        var optionsResult = await CanOpenFile.ReadEdsAsync("Fixtures/sample_device.eds", options);
        var legacyResult = await CanOpenFile.ReadEdsAsync("Fixtures/sample_device.eds", IniParser.DefaultMaxInputSize);

        legacyResult.DeviceInfo.ProductName.Should().Be(optionsResult.DeviceInfo.ProductName);
    }

    [Fact]
    public async Task ReadEdsAsync_Stream_WithCanOpenFileOptionsOverload_MatchesMaxInputSizeOverload()
    {
        var content = File.ReadAllText("Fixtures/sample_device.eds");
        using var optionsStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        using var legacyStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var options = new CanOpenFileOptions { MaxInputSize = IniParser.DefaultMaxInputSize };

        var optionsResult = await CanOpenFile.ReadEdsAsync(optionsStream, options);
        var legacyResult = await CanOpenFile.ReadEdsAsync(legacyStream, IniParser.DefaultMaxInputSize);

        legacyResult.DeviceInfo.ProductName.Should().Be(optionsResult.DeviceInfo.ProductName);
    }
}
