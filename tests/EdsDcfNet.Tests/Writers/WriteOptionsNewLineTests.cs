namespace EdsDcfNet.Tests.Writers;

using System.Text;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Tests.Integration;

/// <summary>
/// <see cref="CanOpenWriteOptions.NewLine"/> through the canonical API for all five formats.
/// Output is checked byte by byte (no CRLF to LF normalization of the actual output), so a
/// platform line ending that leaks through shows up on every runner.
/// </summary>
[Collection(ThreadSaturationCollection.Name)]
public class WriteOptionsNewLineTests
{
    private const string Lf = "\n";
    private const string CrLf = "\r\n";

    public enum Format { Eds, Dcf, Cpj, Xdd, Xdc }

    public static IEnumerable<object[]> FormatsAndNewLines()
    {
        foreach (var format in Enum.GetValues(typeof(Format)).Cast<Format>())
        {
            yield return new object[] { format, Lf };
            yield return new object[] { format, CrLf };
        }
    }

    private static string Fixture(string name)
        => File.ReadAllText(Path.Combine("Fixtures", name), Encoding.UTF8);

    // Multi-line text content (vendorName element) and attribute value (fileCreator) for XML.
    private const string MultiLineText = "Line one\nLine two\r\nLine three";

    private sealed class Case
    {
        internal required Func<CanOpenWriteOptions?, string> ToStringFn { get; init; }
        internal required Action<string, CanOpenWriteOptions?> WriteFile { get; init; }
        internal required Action<Stream, CanOpenWriteOptions?> WriteStream { get; init; }
        internal required Func<string, CanOpenWriteOptions?, Task> WriteFileAsync { get; init; }
        internal required Func<Stream, CanOpenWriteOptions?, Task> WriteStreamAsync { get; init; }
    }

    private static Case Create(Format format, bool multiLine = false)
    {
        switch (format)
        {
            case Format.Eds:
                var eds = CanOpenFile.Eds.ReadString(Fixture("sample_device.eds"));
                return new Case
                {
                    ToStringFn = o => CanOpenFile.Eds.WriteToString(eds, o),
                    WriteFile = (p, o) => CanOpenFile.Eds.WriteFile(eds, p, o),
                    WriteStream = (s, o) => CanOpenFile.Eds.WriteStream(eds, s, o),
                    WriteFileAsync = (p, o) => CanOpenFile.Eds.WriteFileAsync(eds, p, o),
                    WriteStreamAsync = (s, o) => CanOpenFile.Eds.WriteStreamAsync(eds, s, o)
                };
            case Format.Dcf:
                var dcf = CanOpenFile.Dcf.ReadString(Fixture("full_features.dcf"));
                return new Case
                {
                    ToStringFn = o => CanOpenFile.Dcf.WriteToString(dcf, o),
                    WriteFile = (p, o) => CanOpenFile.Dcf.WriteFile(dcf, p, o),
                    WriteStream = (s, o) => CanOpenFile.Dcf.WriteStream(dcf, s, o),
                    WriteFileAsync = (p, o) => CanOpenFile.Dcf.WriteFileAsync(dcf, p, o),
                    WriteStreamAsync = (s, o) => CanOpenFile.Dcf.WriteStreamAsync(dcf, s, o)
                };
            case Format.Cpj:
                var cpj = CanOpenFile.Cpj.ReadString(
                    "[Topology]\nNetName=Net\nNetRefd=N1\nNodes=0x01\nNode1Present=0x01\nNode1Name=PLC\nNode1DCFName=a.dcf\n");
                return new Case
                {
                    ToStringFn = o => CanOpenFile.Cpj.WriteToString(cpj, o),
                    WriteFile = (p, o) => CanOpenFile.Cpj.WriteFile(cpj, p, o),
                    WriteStream = (s, o) => CanOpenFile.Cpj.WriteStream(cpj, s, o),
                    WriteFileAsync = (p, o) => CanOpenFile.Cpj.WriteFileAsync(cpj, p, o),
                    WriteStreamAsync = (s, o) => CanOpenFile.Cpj.WriteStreamAsync(cpj, s, o)
                };
            case Format.Xdd:
                var xdd = CanOpenFile.Xdd.ReadString(Fixture("sample_device.xdd"));
                if (multiLine)
                    xdd.DeviceInfo.VendorName = MultiLineText;
                return new Case
                {
                    ToStringFn = o => CanOpenFile.Xdd.WriteToString(xdd, o),
                    WriteFile = (p, o) => CanOpenFile.Xdd.WriteFile(xdd, p, o),
                    WriteStream = (s, o) => CanOpenFile.Xdd.WriteStream(xdd, s, o),
                    WriteFileAsync = (p, o) => CanOpenFile.Xdd.WriteFileAsync(xdd, p, o),
                    WriteStreamAsync = (s, o) => CanOpenFile.Xdd.WriteStreamAsync(xdd, s, o)
                };
            default:
                var xdc = CanOpenFile.Xdc.ReadString(Fixture("minimal.xdc"));
                if (multiLine)
                    xdc.DeviceInfo.VendorName = MultiLineText;
                return new Case
                {
                    ToStringFn = o => CanOpenFile.Xdc.WriteToString(xdc, o),
                    WriteFile = (p, o) => CanOpenFile.Xdc.WriteFile(xdc, p, o),
                    WriteStream = (s, o) => CanOpenFile.Xdc.WriteStream(xdc, s, o),
                    WriteFileAsync = (p, o) => CanOpenFile.Xdc.WriteFileAsync(xdc, p, o),
                    WriteStreamAsync = (s, o) => CanOpenFile.Xdc.WriteStreamAsync(xdc, s, o)
                };
        }
    }

    private static string WithoutLineBreaks(string text)
        => text.Replace("\r\n", string.Empty).Replace("\n", string.Empty);

    /// <summary>Asserts that every line break in <paramref name="text"/> is exactly <paramref name="newLine"/>.</summary>
    private static void AssertOnlyNewLine(string text, string newLine, string because)
    {
        text.Should().Contain(newLine, because);
        text.Replace(newLine, string.Empty).Should().NotContain("\n", because)
            .And.NotContain("\r", because);
    }

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.out");

    private static async Task<string[]> AllOutputsAsync(Case c, CanOpenWriteOptions? options)
    {
        var path = TempPath();
        var asyncPath = TempPath();
        try
        {
            c.WriteFile(path, options);
            await c.WriteFileAsync(asyncPath, options);
            using var stream = new MemoryStream();
            c.WriteStream(stream, options);
            using var asyncStream = new MemoryStream();
            await c.WriteStreamAsync(asyncStream, options);
            return new[]
            {
                c.ToStringFn(options),
                Encoding.UTF8.GetString(File.ReadAllBytes(path)),
                Encoding.UTF8.GetString(File.ReadAllBytes(asyncPath)),
                Encoding.UTF8.GetString(stream.ToArray()),
                Encoding.UTF8.GetString(asyncStream.ToArray())
            };
        }
        finally
        {
            File.Delete(path);
            File.Delete(asyncPath);
        }
    }

    [Theory]
    [MemberData(nameof(FormatsAndNewLines))]
    public async Task Write_ChosenNewLine_AllOutputPathsContainOnlyThatLineEnding(Format format, string newLine)
    {
        // Arrange
        var c = Create(format);
        var options = new CanOpenWriteOptions { NewLine = newLine };

        // Act
        var outputs = await AllOutputsAsync(c, options);

        // Assert
        var names = new[] { "WriteToString", "WriteFile", "WriteFileAsync", "WriteStream", "WriteStreamAsync" };
        for (var i = 0; i < outputs.Length; i++)
            AssertOnlyNewLine(outputs[i], newLine, names[i]);

        outputs.Distinct().Should().ContainSingle("all paths produce identical text");
    }

    [Theory]
    [MemberData(nameof(FormatsAndNewLines))]
    public void Write_ChosenNewLine_OnlyLineBreaksDifferFromDefaultOutput(Format format, string newLine)
    {
        // Arrange
        var c = Create(format);
        var baseline = c.ToStringFn(null);

        // Act
        var chosen = c.ToStringFn(new CanOpenWriteOptions { NewLine = newLine });

        // Assert
        WithoutLineBreaks(chosen).Should().Be(WithoutLineBreaks(baseline));
    }

    [Theory]
    [InlineData(Format.Eds)]
    [InlineData(Format.Dcf)]
    [InlineData(Format.Cpj)]
    [InlineData(Format.Xdd)]
    [InlineData(Format.Xdc)]
    public async Task Write_NoNewLineOption_OutputUnchangedFromEnvironmentNewLine(Format format)
    {
        // Arrange
        var c = Create(format);
        var explicitDefault = new CanOpenWriteOptions { NewLine = Environment.NewLine };

        // Act
        var implicitOutputs = await AllOutputsAsync(c, null);
        var defaultOptionsOutputs = await AllOutputsAsync(c, CanOpenWriteOptions.Default);
        var explicitOutputs = await AllOutputsAsync(c, explicitDefault);

        // Assert
        defaultOptionsOutputs.Should().Equal(implicitOutputs);
        explicitOutputs.Should().Equal(implicitOutputs);
        AssertOnlyNewLine(implicitOutputs[0], Environment.NewLine, "default is the platform line ending");
    }

    [Fact]
    public void NewLine_Default_IsEnvironmentNewLine()
    {
        // Act + Assert
        new CanOpenWriteOptions().NewLine.Should().Be(Environment.NewLine);
        CanOpenWriteOptions.Default.NewLine.Should().Be(Environment.NewLine);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void NewLine_SupportedValue_IsReturnedUnchanged(string value)
    {
        // Act
        var options = new CanOpenWriteOptions { NewLine = value };

        // Assert
        options.NewLine.Should().Be(value);
    }

    [Theory]
    [InlineData(Format.Xdd)]
    [InlineData(Format.Xdc)]
    public async Task Write_XmlMultiLineTextContent_ReadsBackUnchangedWithBothLineEndings(Format format)
    {
        // Arrange
        foreach (var newLine in new[] { Lf, CrLf })
        {
            var c = Create(format, multiLine: true);

            // Act
            var outputs = await AllOutputsAsync(c, new CanOpenWriteOptions { NewLine = newLine });

            // Assert
            foreach (var output in outputs)
            {
                AssertOnlyNewLine(output, newLine, "multi-line content is written with the chosen line ending");
                var readBack = format == Format.Xdd
                    ? CanOpenFile.Xdd.ReadString(output).DeviceInfo.VendorName
                    : CanOpenFile.Xdc.ReadString(output).DeviceInfo.VendorName;
                readBack.Should().Be("Line one\nLine two\nLine three",
                    "XML parsers read a line break in text as a line feed");
            }
        }
    }

    [Theory]
    [InlineData(Format.Xdd)]
    [InlineData(Format.Xdc)]
    public void Write_XmlMultiLineAttributeValue_StaysCharacterReferenceAndReadsBack(Format format)
    {
        // Arrange
        const string creator = "Tool\nName\r\nLine";
        foreach (var newLine in new[] { Lf, CrLf })
        {
            string output;
            string readBack;
            if (format == Format.Xdd)
            {
                var model = CanOpenFile.Xdd.ReadString(Fixture("sample_device.xdd"));
                model.FileInfo.CreatedBy = creator;
                output = CanOpenFile.Xdd.WriteToString(model, new CanOpenWriteOptions { NewLine = newLine });
                readBack = CanOpenFile.Xdd.ReadString(output).FileInfo.CreatedBy;
            }
            else
            {
                var model = CanOpenFile.Xdc.ReadString(Fixture("minimal.xdc"));
                model.FileInfo.CreatedBy = creator;
                output = CanOpenFile.Xdc.WriteToString(model, new CanOpenWriteOptions { NewLine = newLine });
                readBack = CanOpenFile.Xdc.ReadString(output).FileInfo.CreatedBy;
            }

            // Act + Assert
            AssertOnlyNewLine(output, newLine, "attribute line breaks are character references");
            readBack.Should().Be(creator, "attribute values are not normalized");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\r")]
    [InlineData("\n\n")]
    [InlineData("\r\n\r\n")]
    [InlineData(" ")]
    [InlineData("\n[FileInfo]\nFileName=pwned\n")]
    [InlineData("<!-- x -->")]
    public void NewLine_UnsupportedValue_ThrowsArgumentExceptionWhenSet(string? value)
    {
        // Act
        var act = () => new CanOpenWriteOptions { NewLine = value! };

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void WriteToString_RejectedIniNewLine_NeverReachesTheWriter()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Fixture("sample_device.eds"));

        // Act: the options object cannot be constructed, so no write happens at all.
        var act = () => CanOpenFile.Eds.WriteToString(
            eds, new CanOpenWriteOptions { NewLine = "\n[FileInfo]\nFileName=pwned\n" });

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WriteToString_RejectedXmlNewLine_NeverReachesTheWriter()
    {
        // Arrange
        var xdd = CanOpenFile.Xdd.ReadString(Fixture("sample_device.xdd"));

        // Act
        var act = () => CanOpenFile.Xdd.WriteToString(xdd, new CanOpenWriteOptions { NewLine = "<x/>" });

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WriteFile_FailingWriteWithNewLine_DoesNotLeakScopeIntoLaterDefaultWrite()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Fixture("sample_device.eds"));
        var failing = new ThrowingWritableStream();

        // Act
        var act = () => CanOpenFile.Eds.WriteStream(eds, failing, new CanOpenWriteOptions { NewLine = Environment.NewLine == Lf ? CrLf : Lf });
        act.Should().Throw<EdsWriteException>();
        var afterwards = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        AssertOnlyNewLine(afterwards, Environment.NewLine, "the scope must be restored after a failed write");
    }

    [Fact]
    public async Task NewLineScope_DoesNotLeakAcrossConcurrentWrites()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Fixture("sample_device.eds"));
        var xdd = CanOpenFile.Xdd.ReadString(Fixture("sample_device.xdd"));
        const int concurrency = 32;
        using var start = new Barrier(concurrency);

        var tasks = Enumerable.Range(0, concurrency).Select(i => Task.Run(async () =>
        {
            var newLine = i % 2 == 0 ? Lf : CrLf;
            var options = new CanOpenWriteOptions { NewLine = newLine };
            start.SignalAndWait();

            for (var iteration = 0; iteration < 5; iteration++)
            {
                AssertOnlyNewLine(CanOpenFile.Eds.WriteToString(eds, options), newLine, "EDS string");
                AssertOnlyNewLine(CanOpenFile.Xdd.WriteToString(xdd, options), newLine, "XDD string");

                using var stream = new MemoryStream();
                await CanOpenFile.Eds.WriteStreamAsync(eds, stream, options);
                AssertOnlyNewLine(Encoding.UTF8.GetString(stream.ToArray()), newLine, "EDS async stream");

                using var xmlStream = new MemoryStream();
                await CanOpenFile.Xdd.WriteStreamAsync(xdd, xmlStream, options);
                AssertOnlyNewLine(Encoding.UTF8.GetString(xmlStream.ToArray()), newLine, "XDD async stream");
            }
        })).ToArray();

        // Act + Assert
        await Task.WhenAll(tasks);
    }
}
