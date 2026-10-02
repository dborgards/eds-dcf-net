namespace EdsDcfNet.Tests.Writers;

using System.Text;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Writers;
using EdsDcfNet.Tests.Integration;

/// <summary>
/// Pins the exact bytes every writer emits (UTF-8 without BOM, text identical to
/// <c>GenerateString</c>) for file and stream output, synchronous and asynchronous, over all
/// fixtures. The byte output of all five writers is routed through one central encoding
/// point; these tests guard that routing (and the XDD/XDC XmlWriter path, which buffers
/// the document before copying it to the caller stream) against changing the output.
/// </summary>
public class WriterByteOutputTests
{
    private static readonly UTF8Encoding ExpectedEncoding = new(encoderShouldEmitUTF8Identifier: false);

    private const string NonAscii = "Ärger Ünïcode € 日本 \U0001F600";

    private static IEnumerable<string> Files(string extension)
    {
        var found = new List<string>();
        foreach (var file in Directory.EnumerateFiles("Fixtures", "*." + extension))
            found.Add(file);
        foreach (var row in CorpusFiles.Enumerate(extension))
        {
            var path = (string)row[0];
            if (!CorpusFiles.IsSentinel(path))
                found.Add(path);
        }

        return found.OrderBy(f => f, StringComparer.Ordinal);
    }

    private static IEnumerable<object[]> Rows(string extension) => Files(extension).Select(f => new object[] { f });

    public static IEnumerable<object[]> EdsFixtures() => Rows("eds");
    public static IEnumerable<object[]> DcfFixtures() => Rows("dcf");
    public static IEnumerable<object[]> XddFixtures() => Rows("xdd");
    public static IEnumerable<object[]> XdcFixtures() => Rows("xdc");

    private static byte[] ExpectedBytes(string text) => ExpectedEncoding.GetBytes(text);

    private static string TempPath(string extension)
        => Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.{extension}");

    private static async Task AssertAllPathsAsync(
        string expectedText,
        Action<string> writeFile,
        Action<Stream> writeStream,
        Func<string, Task> writeFileAsync,
        Func<Stream, Task> writeStreamAsync,
        string extension)
    {
        var expected = ExpectedBytes(expectedText);
        expected.Should().NotBeEmpty();
        expected.Take(3).Should().NotEqual(new byte[] { 0xEF, 0xBB, 0xBF }, "output must stay UTF-8 without BOM");

        var syncPath = TempPath(extension);
        var asyncPath = TempPath(extension);
        try
        {
            writeFile(syncPath);
            File.ReadAllBytes(syncPath).Should().Equal(expected, "WriteFile");

            await writeFileAsync(asyncPath);
            File.ReadAllBytes(asyncPath).Should().Equal(expected, "WriteFileAsync");

            using (var ms = new MemoryStream())
            {
                writeStream(ms);
                ms.ToArray().Should().Equal(expected, "WriteStream");
            }

            using (var ms = new MemoryStream())
            {
                await writeStreamAsync(ms);
                ms.ToArray().Should().Equal(expected, "WriteStreamAsync");
            }
        }
        finally
        {
            File.Delete(syncPath);
            File.Delete(asyncPath);
        }
    }

    [Theory]
    [MemberData(nameof(EdsFixtures))]
    public async Task EdsWriter_Fixture_AllOutputPathsAreByteIdentical(string path)
    {
        // Arrange
        var eds = new EdsReader().ReadFile(path);
        eds.DeviceInfo.ProductName = NonAscii;
        var writer = new EdsWriter();

        // Act + Assert
        await AssertAllPathsAsync(
            writer.GenerateString(eds),
            p => writer.WriteFile(eds, p),
            s => writer.WriteStream(eds, s),
            p => writer.WriteFileAsync(eds, p),
            s => writer.WriteStreamAsync(eds, s),
            "eds");
    }

    [Theory]
    [MemberData(nameof(DcfFixtures))]
    public async Task DcfWriter_Fixture_AllOutputPathsAreByteIdentical(string path)
    {
        // Arrange
        var dcf = new DcfReader().ReadFile(path);
        dcf.DeviceInfo.ProductName = NonAscii;
        var writer = new DcfWriter();

        // Act + Assert
        await AssertAllPathsAsync(
            writer.GenerateString(dcf),
            p => writer.WriteFile(dcf, p),
            s => writer.WriteStream(dcf, s),
            p => writer.WriteFileAsync(dcf, p),
            s => writer.WriteStreamAsync(dcf, s),
            "dcf");
    }

    [Fact]
    public async Task CpjWriter_NonAsciiProject_AllOutputPathsAreByteIdentical()
    {
        // Arrange
        var project = new CpjReader().ReadString(
            "[Topology]\nNetName=" + NonAscii + "\nNetRefd=N1\nNodes=0x01\nNode1Present=0x01\nNode1Name=PLC\nNode1DCFName=a.dcf\n");
        var writer = new CpjWriter();

        // Act + Assert
        await AssertAllPathsAsync(
            writer.GenerateString(project),
            p => writer.WriteFile(project, p),
            s => writer.WriteStream(project, s),
            p => writer.WriteFileAsync(project, p),
            s => writer.WriteStreamAsync(project, s),
            "cpj");
    }

    [Theory]
    [MemberData(nameof(XddFixtures))]
    public async Task XddWriter_Fixture_AllOutputPathsAreByteIdentical(string path)
    {
        // Arrange
        var eds = new XddReader().ReadFile(path);
        eds.DeviceInfo.ProductName = NonAscii;
        var writer = new XddWriter();

        // Act + Assert
        await AssertAllPathsAsync(
            writer.GenerateString(eds),
            p => writer.WriteFile(eds, p),
            s => writer.WriteStream(eds, s),
            p => writer.WriteFileAsync(eds, p),
            s => writer.WriteStreamAsync(eds, s),
            "xdd");
    }

    [Theory]
    [MemberData(nameof(XdcFixtures))]
    public async Task XdcWriter_Fixture_AllOutputPathsAreByteIdentical(string path)
    {
        // Arrange
        var dcf = new XdcReader().ReadFile(path);
        dcf.DeviceInfo.ProductName = NonAscii;
        var writer = new XdcWriter();

        // Act + Assert
        await AssertAllPathsAsync(
            writer.GenerateString(dcf),
            p => writer.WriteFile(dcf, p),
            s => writer.WriteStream(dcf, s),
            p => writer.WriteFileAsync(dcf, p),
            s => writer.WriteStreamAsync(dcf, s),
            "xdc");
    }

    [Fact]
    public void XddWriter_Output_DeclaresUtf8AndCarriesNonAsciiAsLiteralUtf8()
    {
        // Arrange
        var eds = new XddReader().ReadFile(Path.Combine("Fixtures", "sample_device.xdd"));
        eds.DeviceInfo.ProductName = NonAscii;
        using var ms = new MemoryStream();

        // Act
        new XddWriter().WriteStream(eds, ms);
        var text = ExpectedEncoding.GetString(ms.ToArray());

        // Assert
        text.Should().StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        text.Should().Contain(NonAscii);
    }
}
