namespace EdsDcfNet.Tests.Parsers;

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Xml.Linq;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Parsers;

public class SecureXmlParserTests
{
    private static readonly Type SecureXmlParserType =
        typeof(XddReader).Assembly.GetType("EdsDcfNet.Parsers.SecureXmlParser")!;

    [Fact]
    public void ReadContentFromStreamWithLimit_ValidStream_ReturnsContent()
    {
        const string xml = "<root><value>ok</value></root>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        var result = Invoke<string>(
            "ReadContentFromStreamWithLimit",
            stream,
            "XDD",
            1024L);

        result.Should().Contain("<root>");
    }

    [Fact]
    public void ReadContentFromStreamWithLimit_SeekableTooLarge_ThrowsEdsParseException()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("<root/>"));

        var act = () => Invoke<string>(
            "ReadContentFromStreamWithLimit",
            stream,
            "XDD",
            1L);

        act.Should().Throw<EdsParseException>()
            .WithMessage("*too large*");
    }

    [Fact]
    public void ReadContentFromStreamWithLimit_NonSeekableTooLargeByChars_ThrowsEdsParseException()
    {
        const string xml = "<root>" + "abcdefghijk" + "</root>";
        using var stream = new NonSeekableReadStream(Encoding.UTF8.GetBytes(xml));

        var act = () => Invoke<string>(
            "ReadContentFromStreamWithLimit",
            stream,
            "XDD",
            8L);

        act.Should().Throw<EdsParseException>()
            .WithMessage("*too large*");
    }

    [Fact]
    public void ReadContentFromStreamWithLimit_NullStream_ThrowsArgumentNullException()
    {
        var act = () => Invoke<string>(
            "ReadContentFromStreamWithLimit",
            null!,
            "XDD",
            128L);

        act.Should().Throw<ArgumentNullException>()
            .Where(ex => ex.ParamName == "stream");
    }

    [Fact]
    public void ReadContentFromStreamWithLimit_UnreadableStream_ThrowsArgumentException()
    {
        using var stream = new WriteOnlyStream();

        var act = () => Invoke<string>(
            "ReadContentFromStreamWithLimit",
            stream,
            "XDD",
            128L);

        act.Should().Throw<ArgumentException>()
            .Where(ex => ex.ParamName == "stream");
    }

    [Fact]
    public async Task ReadContentFromStreamWithLimitAsync_ValidStream_ReturnsContent()
    {
        const string xml = "<root><value>ok</value></root>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        var result = await InvokeAsync<string>(
            "ReadContentFromStreamWithLimitAsync",
            stream,
            "XDD",
            1024L,
            CancellationToken.None);

        result.Should().Contain("<root>");
    }

    [Fact]
    public async Task ReadContentFromStreamWithLimitAsync_Canceled_ThrowsOperationCanceledException()
    {
        const string xml = "<root><value>ok</value></root>";
        using var stream = new NonSeekableReadStream(Encoding.UTF8.GetBytes(xml));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => InvokeAsync<string>(
            "ReadContentFromStreamWithLimitAsync",
            stream,
            "XDD",
            1024L,
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void ParseDocument_InvalidXml_ThrowsEdsParseException()
    {
        var act = () => Invoke<XDocument>(
            "ParseDocument",
            "<root><broken></root>",
            "XDD",
            "Failed to parse XDD XML content.",
            1024L);

        act.Should().Throw<EdsParseException>()
            .WithMessage("*Failed to parse XDD XML content.*");
    }

    [Fact]
    public void EnsureFileWithinSizeLimit_TooLarge_ThrowsEdsParseException()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            File.WriteAllText(tempFile, "1234567890");
            var act = () => Invoke<object?>(
                "EnsureFileWithinSizeLimit",
                tempFile,
                "XDD",
                3L);

            act.Should().Throw<EdsParseException>()
                .WithMessage("*too large*");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    private static T Invoke<T>(string methodName, params object?[] args)
    {
        var method = SecureXmlParserType.GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static)!;

        try
        {
            var result = method.Invoke(null, args);
            return (T)result!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static async Task<T> InvokeAsync<T>(string methodName, params object?[] args)
    {
        var method = SecureXmlParserType.GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Task<T> task;
        try
        {
            task = (Task<T>)method.Invoke(null, args)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }

        return await task.ConfigureAwait(false);
    }

    private sealed class WriteOnlyStream : MemoryStream
    {
        public override bool CanRead => false;

        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
    }

    private sealed class NonSeekableReadStream : Stream
    {
        private readonly MemoryStream _inner;

        public NonSeekableReadStream(byte[] data)
        {
            _inner = new MemoryStream(data);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
