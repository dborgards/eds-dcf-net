namespace EdsDcfNet.Tests.Writers;

using EdsDcfNet.Exceptions;

public partial class XddWriterTests
{
    #region WriteFile Tests

    [Fact]
    public void WriteFile_CreatesValidXmlFile()
    {
        // Arrange
        var eds = CreateSampleEds();
        var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xdd");

        try
        {
            // Act
            _writer.WriteFile(eds, filePath);

            // Assert
            File.Exists(filePath).Should().BeTrue();
            var content = File.ReadAllText(filePath);
            content.Should().Contain("ISO15745ProfileContainer");
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public void WriteFile_InvalidPath_ThrowsXddWriteException()
    {
        // Arrange
        var eds = CreateSampleEds();
        var invalidPath = "/invalid/path/that/does/not/exist/test.xdd";

        // Act
        var act = () => _writer.WriteFile(eds, invalidPath);

        // Assert
        act.Should().Throw<XddWriteException>()
            .WithMessage("*Failed to write XDD file*");
    }

    [Fact]
    public void WriteFile_GenerationThrowsXddWriteException_Rethrows()
    {
        var eds = CreateSampleEds();
        eds.DeviceInfo = null!;
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xdd");

        try
        {
            var act = () => _writer.WriteFile(eds, tempFile);

            var ex = act.Should().Throw<XddWriteException>().Which;
            ex.SectionName.Should().Be("DeviceProfile");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteFileAsync_InvalidPath_ThrowsXddWriteException()
    {
        var eds = CreateSampleEds();
        var invalidPath = "/invalid/path/that/does/not/exist/async.xdd";

        var act = () => _writer.WriteFileAsync(eds, invalidPath);

        (await act.Should().ThrowAsync<XddWriteException>())
            .WithMessage("*Failed to write XDD file*");
    }

    [Fact]
    public async Task WriteFileAsync_Cancelled_ThrowsOperationCanceledException()
    {
        var eds = CreateSampleEds();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xdd");

        try
        {
            var act = () => _writer.WriteFileAsync(eds, tempFile, cts.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void WriteStream_RoundTripsAndLeavesStreamOpen()
    {
        var eds = CreateSampleEds();
        using var stream = new MemoryStream();

        _writer.WriteStream(eds, stream);
        stream.CanWrite.Should().BeTrue();
        stream.Position = 0;
        var parsed = new EdsDcfNet.Parsers.XddReader().ReadStream(stream);

        parsed.DeviceInfo.ProductName.Should().Be("Test Product");
    }

    [Fact]
    public async Task WriteStreamAsync_RoundTripsAndLeavesStreamOpen()
    {
        var eds = CreateSampleEds();
        using var stream = new MemoryStream();

        await _writer.WriteStreamAsync(eds, stream);
        stream.CanWrite.Should().BeTrue();
        stream.Position = 0;
        var parsed = await new EdsDcfNet.Parsers.XddReader().ReadStreamAsync(stream);

        parsed.DeviceInfo.ProductName.Should().Be("Test Product");
    }

    [Fact]
    public void WriteStream_UnwritableStream_ThrowsArgumentException()
    {
        var eds = CreateSampleEds();
        using var stream = new MemoryStream(new byte[16], writable: false);

        var act = () => _writer.WriteStream(eds, stream);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("stream");
    }

    [Fact]
    public async Task WriteStreamAsync_UnwritableStream_ThrowsArgumentException()
    {
        var eds = CreateSampleEds();
        using var stream = new MemoryStream(new byte[16], writable: false);

        var act = () => _writer.WriteStreamAsync(eds, stream);

        await act.Should().ThrowAsync<ArgumentException>()
            .Where(ex => ex.ParamName == "stream");
    }

    [Fact]
    public void WriteStream_NullStream_ThrowsArgumentNullException()
    {
        var eds = CreateSampleEds();

        var act = () => _writer.WriteStream(eds, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("stream");
    }

    [Fact]
    public void WriteStream_GenerationThrowsXddWriteException_Rethrows()
    {
        var eds = CreateSampleEds();
        eds.DeviceInfo = null!;
        using var stream = new MemoryStream();

        var act = () => _writer.WriteStream(eds, stream);

        var ex = act.Should().Throw<XddWriteException>().Which;
        ex.SectionName.Should().Be("DeviceProfile");
    }

    [Fact]
    public void WriteStream_StreamWriteThrows_WrapsInXddWriteException()
    {
        var eds = CreateSampleEds();
        using var stream = new ThrowingWritableStream();

        var act = () => _writer.WriteStream(eds, stream);

        var ex = act.Should().Throw<XddWriteException>().Which;
        ex.Message.Should().Contain("Failed to write XDD content to stream.");
        ex.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task WriteStreamAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        var eds = CreateSampleEds();
        using var stream = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _writer.WriteStreamAsync(eds, stream, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WriteStreamAsync_GenerationThrowsXddWriteException_Rethrows()
    {
        var eds = CreateSampleEds();
        eds.DeviceInfo = null!;
        using var stream = new MemoryStream();

        var act = () => _writer.WriteStreamAsync(eds, stream);

        var ex = (await act.Should().ThrowAsync<XddWriteException>()).Which;
        ex.SectionName.Should().Be("DeviceProfile");
    }

    [Fact]
    public async Task WriteStreamAsync_StreamWriteThrows_WrapsInXddWriteException()
    {
        var eds = CreateSampleEds();
        using var stream = new ThrowingWritableStream();

        var act = () => _writer.WriteStreamAsync(eds, stream);

        var ex = (await act.Should().ThrowAsync<XddWriteException>()).Which;
        ex.Message.Should().Contain("Failed to write XDD content to stream.");
        ex.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    #endregion
}
