namespace EdsDcfNet.Tests.Parsers;

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Parsers;

public class ByteLimitingStreamTests
{
    private const string ExceededMessage = "File 'test.eds' is too large. Maximum supported size is 8 bytes.";

    private static readonly Type ByteLimitingStreamType =
        typeof(XddReader).Assembly.GetType("EdsDcfNet.Parsers.ByteLimitingStream")
        ?? throw new InvalidOperationException("Internal type EdsDcfNet.Parsers.ByteLimitingStream not found.");

    [Fact]
    public void Read_ContentAtExactByteLimit_ReadsFully()
    {
        var data = Encoding.UTF8.GetBytes("12345678");
        using var stream = Create(new MemoryStream(data), maxBytes: data.Length);

        using var reader = new StreamReader(stream, Encoding.UTF8);

        reader.ReadToEnd().Should().Be("12345678");
    }

    [Fact]
    public void Read_MaxInputSizeAtLongMaxValue_ReadsFully()
    {
        // Callers may set MaxInputSize = long.MaxValue to disable the limit.
        // AllowedCount must not overflow when computing remaining+1.
        var data = Encoding.UTF8.GetBytes("12345678");
        using var stream = Create(new MemoryStream(data), maxBytes: long.MaxValue);

        using var reader = new StreamReader(stream, Encoding.UTF8);

        reader.ReadToEnd().Should().Be("12345678");
    }

    [Fact]
    public async Task ReadAsync_MaxInputSizeAtLongMaxValue_ReadsFully()
    {
        var data = Encoding.UTF8.GetBytes("12345678");
        using var stream = Create(new MemoryStream(data), maxBytes: long.MaxValue);
        var buffer = new byte[data.Length];

        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

        bytesRead.Should().Be(data.Length);
        Encoding.UTF8.GetString(buffer).Should().Be("12345678");
    }

    [Fact]
    public void Read_ContentOneOverByteLimit_ThrowsEdsParseException()
    {
        var data = Encoding.UTF8.GetBytes("123456789");
        using var stream = Create(new MemoryStream(data), maxBytes: data.Length - 1);

        var act = () =>
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        };

        act.Should().Throw<EdsParseException>().WithMessage(ExceededMessage);
    }

    [Fact]
    public void Read_MultibyteContentUnderCharacterLimitButOverByteLimit_ThrowsEdsParseException()
    {
        // Four characters that encode to eight UTF-8 bytes: a character-based
        // limit would accept this content, the byte limit must not.
        var data = Encoding.UTF8.GetBytes("äöüß");
        using var stream = Create(new MemoryStream(data), maxBytes: 4);

        var act = () =>
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        };

        act.Should().Throw<EdsParseException>().WithMessage(ExceededMessage);
    }

    [Fact]
    public async Task ReadAsync_ContentOverByteLimit_ThrowsEdsParseException()
    {
        var data = Encoding.UTF8.GetBytes("123456789");
        using var stream = Create(new MemoryStream(data), maxBytes: data.Length - 1);
        var buffer = new byte[data.Length];

        var act = () => stream.ReadAsync(buffer, 0, buffer.Length);

        await act.Should().ThrowAsync<EdsParseException>().WithMessage(ExceededMessage);
    }

    [Fact]
    public async Task ReadAsync_ContentWithinByteLimit_ReadsContent()
    {
        var data = Encoding.UTF8.GetBytes("12345678");
        using var stream = Create(new MemoryStream(data), maxBytes: data.Length);
        var buffer = new byte[data.Length];

        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

        bytesRead.Should().Be(data.Length);
        Encoding.UTF8.GetString(buffer).Should().Be("12345678");
    }

    [Fact]
    public void OpenFile_FileGrownBeyondLimitAfterOpen_ThrowsEdsParseException()
    {
        // Simulates the TOCTOU window: the reader opens the file after its length
        // was checked, so the byte limit has to be enforced while reading.
        var tempFile = Path.GetTempFileName();

        try
        {
            File.WriteAllText(tempFile, new string('x', 64));

            using var stream = OpenFile(tempFile, maxBytes: 8);

            var act = () =>
            {
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return reader.ReadToEnd();
            };

            act.Should().Throw<EdsParseException>().WithMessage(ExceededMessage);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void OpenFile_DisposingWrapper_ReleasesUnderlyingFile()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            File.WriteAllText(tempFile, "[Section1]");

            using (var stream = OpenFile(tempFile, maxBytes: 1024))
            {
                stream.CanRead.Should().BeTrue();
            }

            // The wrapper owns the FileStream; deleting proves the handle is gone.
            File.Delete(tempFile);
            File.Exists(tempFile).Should().BeFalse();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void Read_OverLimit_ReadsAtMostOneByteBeyondTheLimit()
    {
        var inner = new CountingStream(new byte[1024]);
        using var stream = Create(inner, maxBytes: 8);
        var buffer = new byte[1024];

        var act = () => stream.Read(buffer, 0, buffer.Length);

        act.Should().Throw<EdsParseException>();
        inner.TotalBytesServed.Should().Be(9);
    }

    [Fact]
    public async Task ReadAsync_OverLimit_ReadsAtMostOneByteBeyondTheLimit()
    {
        var inner = new CountingStream(new byte[1024]);
        using var stream = Create(inner, maxBytes: 8);
        var buffer = new byte[1024];

        var act = () => stream.ReadAsync(buffer, 0, buffer.Length);

        await act.Should().ThrowAsync<EdsParseException>();
        inner.TotalBytesServed.Should().Be(9);
    }

#if NET10_0_OR_GREATER
    // ByteLimitingStream overrides ReadAsync(Memory<byte>) only on net10.0+.
    [Fact]
    public async Task ReadAsync_MemoryOverload_OverLimit_ReadsAtMostOneByteBeyondTheLimit()
    {
        var inner = new CountingStream(new byte[1024]);
        using var stream = Create(inner, maxBytes: 8);
        var buffer = new byte[1024];

        var act = async () => await stream.ReadAsync(buffer.AsMemory());

        await act.Should().ThrowAsync<EdsParseException>();
        inner.TotalBytesServed.Should().Be(9);
    }
#endif

    [Fact]
    public void Dispose_WithoutDisposingManagedState_LeavesInnerStreamOpen()
    {
        var inner = new CountingStream(new byte[8]);
        var stream = Create(inner, maxBytes: 8);

        InvokeDispose(stream, disposing: false);
        inner.CanRead.Should().BeTrue();

        stream.Dispose();
        inner.CanRead.Should().BeFalse();
    }

    [Fact]
    public void UnsupportedMembers_ThrowNotSupportedException()
    {
        using var stream = Create(new MemoryStream([1, 2, 3]), maxBytes: 8);

        stream.CanSeek.Should().BeFalse();
        stream.CanWrite.Should().BeFalse();

        var length = () => stream.Length;
        var position = () => stream.Position;
        var setPosition = () => stream.Position = 0;
        var seek = () => stream.Seek(0, SeekOrigin.Begin);
        var setLength = () => stream.SetLength(1);
        var write = () => stream.Write([1], 0, 1);

        length.Should().Throw<NotSupportedException>();
        position.Should().Throw<NotSupportedException>();
        setPosition.Should().Throw<NotSupportedException>();
        seek.Should().Throw<NotSupportedException>();
        setLength.Should().Throw<NotSupportedException>();
        write.Should().Throw<NotSupportedException>();

        stream.Flush();
    }

    private static Stream Create(Stream inner, long maxBytes)
        => (Stream)Activator.CreateInstance(
            ByteLimitingStreamType,
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [inner, maxBytes, ExceededMessage],
            culture: null)!;

    private static void InvokeDispose(Stream stream, bool disposing)
    {
        var method = ByteLimitingStreamType
            .GetMethod(
                "Dispose",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(bool)],
                modifiers: null)
            ?? throw new InvalidOperationException("ByteLimitingStream.Dispose(bool) not found.");

        method.Invoke(stream, [disposing]);
    }

    private sealed class CountingStream : MemoryStream
    {
        public CountingStream(byte[] buffer)
            : base(buffer)
        {
        }

        public long TotalBytesServed { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = base.Read(buffer, offset, count);
            TotalBytesServed += bytesRead;
            return bytesRead;
        }

#if NETCOREAPP
        public override int Read(Span<byte> buffer)
        {
            var bytesRead = base.Read(buffer);
            TotalBytesServed += bytesRead;
            return bytesRead;
        }
#endif
    }

    private static Stream OpenFile(string filePath, long maxBytes)
    {
        var method = ByteLimitingStreamType
            .GetMethod("OpenFile", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Internal method ByteLimitingStream.OpenFile not found.");

        try
        {
            return (Stream)method.Invoke(null, [filePath, maxBytes, ExceededMessage, false])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}
