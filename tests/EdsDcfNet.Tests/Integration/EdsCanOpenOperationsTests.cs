namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Parsers;

// Parity with the obsolete CanOpenFile.ReadEds*/WriteEds* overloads is checked in CanOpenFileLegacyFacadeTests.
public class EdsCanOpenOperationsTests
{
    [Fact]
    public void CanOpenFileOptions_Default_UsesReaderDefaultMaxInputSize()
    {
        CanOpenFileOptions.Default.MaxInputSize.Should().Be(ReaderDefaults.DefaultMaxInputSize);
    }

    [Fact]
    public void Eds_ReadFile_WithCustomMaxInputSize_EnforcesLimit()
    {
        var act = () => CanOpenFile.Eds.ReadFile(
            "Fixtures/sample_device.eds",
            new CanOpenFileOptions { MaxInputSize = 10 });

        act.Should().Throw<EdsParseException>();
    }
}
