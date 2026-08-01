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
        typeof(XddReader).Assembly.GetType("EdsDcfNet.Parsers.SecureXmlParser")
        ?? throw new InvalidOperationException("Internal type EdsDcfNet.Parsers.SecureXmlParser not found.");

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
        var act = () => InvokeParseDocument(
            "<root><broken></root>",
            "XDD",
            "Failed to parse XDD XML content.",
            1024L);

        act.Should().Throw<EdsParseException>()
            .WithMessage("*Failed to parse XDD XML content.*");
    }

    [Fact]
    public void ParseDocument_ExceedsMaxDepth_ThrowsEdsParseException()
    {
        // Depth 0 = <a>, depth 1 = <b>, depth 2 = <c>. With maxDepth 1, <c> must fail.
        const string nested = "<a><b><c/></b></a>";

        var act = () => InvokeParseDocument(
            nested,
            "XDD",
            "Failed to parse XDD XML content.",
            1024L,
            maxDepth: 1);

        act.Should().Throw<EdsParseException>()
            .WithMessage("*maximum XML nesting depth of 1*");
    }

    [Fact]
    public void ParseDocument_AtMaxDepth_Succeeds()
    {
        const string nested = "<a><b><c/></b></a>";

        var doc = InvokeParseDocument(
            nested,
            "XDD",
            "Failed to parse XDD XML content.",
            1024L,
            maxDepth: 2);

        doc.Root.Should().NotBeNull();
        doc.Root!.Name.LocalName.Should().Be("a");
    }

    [Fact]
    public void ParseDocument_RealisticFlatFixture_Succeeds()
    {
        var content = File.ReadAllText("Fixtures/sample_device.xdd");

        var doc = InvokeParseDocument(
            content,
            "XDD",
            "Failed to parse XDD XML content.",
            10L * 1024 * 1024);

        doc.Root.Should().NotBeNull();
        doc.Root!.Name.LocalName.Should().Be("ISO15745ProfileContainer");
    }

    [Fact]
    public void ParseDocument_NegativeMaxDepth_ThrowsArgumentOutOfRangeException()
    {
        var act = () => InvokeParseDocument(
            "<root/>",
            "XDD",
            "Failed to parse XDD XML content.",
            1024L,
            maxDepth: -1);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .Where(ex => ex.ParamName == "maxDepth");
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

    private static XDocument InvokeParseDocument(
        string content,
        string formatName,
        string parseErrorMessage,
        long maxInputSize,
        int maxDepth = 64)
    {
        return Invoke<XDocument>(
            "ParseDocument",
            content,
            formatName,
            parseErrorMessage,
            maxInputSize,
            maxDepth);
    }

    private static T Invoke<T>(string methodName, params object?[] args)
    {
        var method = ResolveMethod(methodName, args.Length);

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
        var method = ResolveMethod(methodName, args.Length);

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

    private static MethodInfo ResolveMethod(string methodName, int argumentCount)
    {
        var method = SecureXmlParserType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(m => m.Name == methodName && m.GetParameters().Length == argumentCount);

        return method
            ?? throw new InvalidOperationException(
                $"Internal method {methodName} with {argumentCount} parameter(s) not found.");
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
