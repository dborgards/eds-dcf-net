namespace EdsDcfNet.Tests.Integration;

using System.Reflection;
using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;

// Parity with the obsolete CanOpenFile.Read*/Write* overloads is checked in CanOpenFileLegacyFacadeTests.
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

    private const string MinimalCpjContent = "[Topology]\nNetName=Options Network\nNodes=0";

    [Fact]
    public void Dcf_ReadFile_WithCustomMaxInputSize_EnforcesLimit()
    {
        var act = () => CanOpenFile.Dcf.ReadFile(
            "Fixtures/minimal.dcf",
            new CanOpenFileOptions { MaxInputSize = 10 });

        act.Should().Throw<EdsParseException>();
    }

    [Fact]
    public async Task Dcf_WritePaths_ProduceReadableOutput()
    {
        var dcf = CanOpenFile.Dcf.ReadFile("Fixtures/minimal.dcf");
        var tempFile = Path.GetTempFileName();
        var asyncTempFile = Path.GetTempFileName();

        try
        {
            CanOpenFile.Dcf.WriteFile(dcf, tempFile);
            CanOpenFile.Dcf.ReadFile(tempFile).DeviceCommissioning.NodeId.Should().Be(dcf.DeviceCommissioning.NodeId);

            using (var syncStream = new MemoryStream())
            {
                CanOpenFile.Dcf.WriteStream(dcf, syncStream);
                syncStream.Position = 0;
                CanOpenFile.Dcf.ReadStream(syncStream).DeviceCommissioning.NodeId.Should().Be(dcf.DeviceCommissioning.NodeId);
            }

            await CanOpenFile.Dcf.WriteFileAsync(dcf, asyncTempFile);
            CanOpenFile.Dcf.ReadFile(asyncTempFile).DeviceCommissioning.NodeId.Should().Be(dcf.DeviceCommissioning.NodeId);

            using var asyncStream = new MemoryStream();
            await CanOpenFile.Dcf.WriteStreamAsync(dcf, asyncStream);
            asyncStream.Position = 0;
            CanOpenFile.Dcf.ReadStream(asyncStream).DeviceCommissioning.NodeId.Should().Be(dcf.DeviceCommissioning.NodeId);
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
    public void Cpj_ReadString_WithCustomMaxInputSize_EnforcesLimit()
    {
        const string content = "[Topology]\nNetName=Too Large\nNodes=0";

        var act = () => CanOpenFile.Cpj.ReadString(
            content,
            new CanOpenFileOptions { MaxInputSize = 10 });

        act.Should().Throw<EdsParseException>();
    }

    [Fact]
    public async Task Cpj_WritePaths_ProduceReadableOutput()
    {
        var cpj = CanOpenFile.Cpj.ReadString(MinimalCpjContent);
        var tempFile = Path.GetTempFileName();
        var asyncTempFile = Path.GetTempFileName();

        try
        {
            CanOpenFile.Cpj.WriteFile(cpj, tempFile);
            CanOpenFile.Cpj.ReadFile(tempFile).Networks[0].NetName.Should().Be(cpj.Networks[0].NetName);

            using (var syncStream = new MemoryStream())
            {
                CanOpenFile.Cpj.WriteStream(cpj, syncStream);
                syncStream.Position = 0;
                CanOpenFile.Cpj.ReadStream(syncStream).Networks[0].NetName.Should().Be(cpj.Networks[0].NetName);
            }

            await CanOpenFile.Cpj.WriteFileAsync(cpj, asyncTempFile);
            CanOpenFile.Cpj.ReadFile(asyncTempFile).Networks[0].NetName.Should().Be(cpj.Networks[0].NetName);

            using var asyncStream = new MemoryStream();
            await CanOpenFile.Cpj.WriteStreamAsync(cpj, asyncStream);
            asyncStream.Position = 0;
            CanOpenFile.Cpj.ReadStream(asyncStream).Networks[0].NetName.Should().Be(cpj.Networks[0].NetName);
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
    public void Xdd_ReadFile_WithCustomMaxInputSize_EnforcesLimit()
    {
        var act = () => CanOpenFile.Xdd.ReadFile(
            "Fixtures/sample_device.xdd",
            new CanOpenFileOptions { MaxInputSize = 256 });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*too large*");
    }

    [Fact]
    public async Task Xdd_WritePaths_ProduceReadableOutput()
    {
        var xdd = CanOpenFile.Xdd.ReadFile("Fixtures/sample_device.xdd");
        var tempFile = Path.GetTempFileName();
        var asyncTempFile = Path.GetTempFileName();

        try
        {
            CanOpenFile.Xdd.WriteFile(xdd, tempFile);
            CanOpenFile.Xdd.ReadFile(tempFile).DeviceInfo.ProductName.Should().Be(xdd.DeviceInfo.ProductName);

            using (var syncStream = new MemoryStream())
            {
                CanOpenFile.Xdd.WriteStream(xdd, syncStream);
                syncStream.Position = 0;
                CanOpenFile.Xdd.ReadStream(syncStream).DeviceInfo.ProductName.Should().Be(xdd.DeviceInfo.ProductName);
            }

            await CanOpenFile.Xdd.WriteFileAsync(xdd, asyncTempFile);
            CanOpenFile.Xdd.ReadFile(asyncTempFile).DeviceInfo.ProductName.Should().Be(xdd.DeviceInfo.ProductName);

            using var asyncStream = new MemoryStream();
            await CanOpenFile.Xdd.WriteStreamAsync(xdd, asyncStream);
            asyncStream.Position = 0;
            CanOpenFile.Xdd.ReadStream(asyncStream).DeviceInfo.ProductName.Should().Be(xdd.DeviceInfo.ProductName);
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
    public void Xdc_ReadFile_WithCustomMaxInputSize_EnforcesLimit()
    {
        var act = () => CanOpenFile.Xdc.ReadFile(
            "Fixtures/minimal.xdc",
            new CanOpenFileOptions { MaxInputSize = 256 });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*too large*");
    }

    [Fact]
    public async Task Xdc_WritePaths_ProduceReadableOutput()
    {
        var xdc = CanOpenFile.Xdc.ReadFile("Fixtures/minimal.xdc");
        var tempFile = Path.GetTempFileName();
        var asyncTempFile = Path.GetTempFileName();

        try
        {
            CanOpenFile.Xdc.WriteFile(xdc, tempFile);
            CanOpenFile.Xdc.ReadFile(tempFile).DeviceCommissioning.NodeId.Should().Be(xdc.DeviceCommissioning.NodeId);

            using (var syncStream = new MemoryStream())
            {
                CanOpenFile.Xdc.WriteStream(xdc, syncStream);
                syncStream.Position = 0;
                CanOpenFile.Xdc.ReadStream(syncStream).DeviceCommissioning.NodeId.Should().Be(xdc.DeviceCommissioning.NodeId);
            }

            await CanOpenFile.Xdc.WriteFileAsync(xdc, asyncTempFile);
            CanOpenFile.Xdc.ReadFile(asyncTempFile).DeviceCommissioning.NodeId.Should().Be(xdc.DeviceCommissioning.NodeId);

            using var asyncStream = new MemoryStream();
            await CanOpenFile.Xdc.WriteStreamAsync(xdc, asyncStream);
            asyncStream.Position = 0;
            CanOpenFile.Xdc.ReadStream(asyncStream).DeviceCommissioning.NodeId.Should().Be(xdc.DeviceCommissioning.NodeId);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
            if (File.Exists(asyncTempFile))
                File.Delete(asyncTempFile);
        }
    }
}
