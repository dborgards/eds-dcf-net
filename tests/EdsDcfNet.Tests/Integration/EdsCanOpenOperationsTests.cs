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
}
