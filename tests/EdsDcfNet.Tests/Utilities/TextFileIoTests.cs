namespace EdsDcfNet.Tests.Utilities;

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using EdsDcfNet;

public class TextFileIoTests
{
    [Fact]
    public async Task ReadAllTextAsync_WithUtf8BomAndDetectionEnabled_ReturnsContentWithoutBomCharacter()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        const string expected = "ÄÖÜ utf8 content";
        await File.WriteAllTextAsync(tempFile, expected, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        try
        {
            // Act
            var actual = await InvokeReadAllTextAsync(
                tempFile,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                detectEncodingFromByteOrderMarks: true,
                cancellationToken: CancellationToken.None);

            // Assert
            actual.Should().Be(expected);
            actual.Should().NotStartWith("\uFEFF");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadAllTextAsync_WithUtf8BomAndDetectionDisabled_PreservesLeadingBomCharacter()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        const string content = "BOM-sensitive text";
        await File.WriteAllTextAsync(tempFile, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        try
        {
            // Act
            var actual = await InvokeReadAllTextAsync(
                tempFile,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                detectEncodingFromByteOrderMarks: false,
                cancellationToken: CancellationToken.None);

            // Assert
            actual.Should().StartWith("\uFEFF");
            actual[1..].Should().Be(content);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteAllTextAsync_AndReadAllTextAsync_RoundTripsLargeUtf8Content()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        var largeContent = string.Concat(Enumerable.Repeat("Zeile mit Umlaut äöü 12345\n", 600));
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        try
        {
            // Act
            await InvokeWriteAllTextAsync(tempFile, largeContent, utf8NoBom, CancellationToken.None);
            var actual = await InvokeReadAllTextAsync(tempFile, utf8NoBom, detectEncodingFromByteOrderMarks: true, CancellationToken.None);

            // Assert
            actual.Should().Be(largeContent);
            var bytes = await File.ReadAllBytesAsync(tempFile);
            var utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF };
            bytes.Take(3).Should().NotEqual(utf8Bom);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadAllTextAsync_WithCanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(tempFile, "content", Encoding.UTF8);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            // Act
            var act = () => InvokeReadAllTextAsync(
                tempFile,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                detectEncodingFromByteOrderMarks: true,
                cancellationToken: cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteAllTextAsync_WithCanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            // Act
            var act = () => InvokeWriteAllTextAsync(
                tempFile,
                "content",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void WriteAllText_WithNonWritableStream_ThrowsArgumentException()
    {
        // Arrange
        using var stream = new ReadOnlyStream();

        // Act
        var act = () => InvokeWriteAllText(
            stream,
            "content",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            leaveOpen: true);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("stream");
    }

    [Fact]
    public async Task WriteAllTextAsync_WithNonWritableStream_ThrowsArgumentException()
    {
        // Arrange
        using var stream = new ReadOnlyStream();

        // Act
        var act = () => InvokeWriteAllTextAsync(
            stream,
            "content",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            leaveOpen: true,
            cancellationToken: CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .Where(ex => ex.ParamName == "stream");
    }

    [Fact]
    public void WriteAllText_WithNullStream_ThrowsArgumentNullException()
    {
        // Act
        var act = () => InvokeWriteAllText(
            null!,
            "content",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            leaveOpen: true);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("stream");
    }

    [Fact]
    public async Task WriteAllTextAsync_WithNullStream_ThrowsArgumentNullException()
    {
        // Act
        var act = () => InvokeWriteAllTextAsync(
            null!,
            "content",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            leaveOpen: true,
            cancellationToken: CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .Where(ex => ex.ParamName == "stream");
    }

    private static Task<string> InvokeReadAllTextAsync(
        string filePath,
        Encoding encoding,
        bool detectEncodingFromByteOrderMarks,
        CancellationToken cancellationToken)
    {
        var method = GetTextFileIoMethod(
            "ReadAllTextAsync",
            typeof(string),
            typeof(Encoding),
            typeof(bool),
            typeof(CancellationToken));

        try
        {
            return (Task<string>)method.Invoke(null, [filePath, encoding, detectEncodingFromByteOrderMarks, cancellationToken])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static Task InvokeWriteAllTextAsync(
        string filePath,
        string content,
        Encoding encoding,
        CancellationToken cancellationToken)
    {
        var method = GetTextFileIoMethod(
            "WriteAllTextAsync",
            typeof(string),
            typeof(string),
            typeof(Encoding),
            typeof(CancellationToken));

        try
        {
            return (Task)method.Invoke(null, [filePath, content, encoding, cancellationToken])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static void InvokeWriteAllText(
        Stream stream,
        string content,
        Encoding encoding,
        bool leaveOpen)
    {
        var method = GetTextFileIoMethod(
            "WriteAllText",
            typeof(Stream),
            typeof(string),
            typeof(Encoding),
            typeof(bool));

        try
        {
            method.Invoke(null, [stream, content, encoding, leaveOpen]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static Task InvokeWriteAllTextAsync(
        Stream stream,
        string content,
        Encoding encoding,
        bool leaveOpen,
        CancellationToken cancellationToken)
    {
        var method = GetTextFileIoMethod(
            "WriteAllTextAsync",
            typeof(Stream),
            typeof(string),
            typeof(Encoding),
            typeof(bool),
            typeof(CancellationToken));

        try
        {
            return (Task)method.Invoke(null, [stream, content, encoding, leaveOpen, cancellationToken])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static MethodInfo GetTextFileIoMethod(string name, params Type[] parameterTypes)
    {
        var textFileIoType = typeof(CanOpenFile).Assembly.GetType("EdsDcfNet.Utilities.TextFileIo");
        textFileIoType.Should().NotBeNull("TextFileIo must exist in the main assembly.");

        var method = textFileIoType!.GetMethod(
            name,
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: parameterTypes,
            modifiers: null);
        method.Should().NotBeNull($"TextFileIo.{name} should be available for reflection-based utility tests.");

        return method!;
    }

    private sealed class ReadOnlyStream : MemoryStream
    {
        public override bool CanWrite => false;

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
    }
}
