namespace EdsDcfNet.Parsers;

using EdsDcfNet.Exceptions;

/// <summary>
/// Forwards reads to an inner stream and aborts once more than a configured
/// number of bytes has been read.
/// </summary>
/// <remarks>
/// File-based readers document their limit in bytes, while the decoding readers
/// count characters. Multibyte encodings decode to fewer characters than bytes,
/// so the byte limit has to be enforced on the raw stream to stay effective when
/// a file grows after its length was checked.
/// </remarks>
internal sealed class ByteLimitingStream : Stream
{
    private readonly Stream _inner;
    private readonly long _maxBytes;
    private readonly string _exceededMessage;
    private long _totalBytesRead;

    internal ByteLimitingStream(Stream inner, long maxBytes, string exceededMessage)
    {
        _inner = inner;
        _maxBytes = maxBytes;
        _exceededMessage = exceededMessage;
    }

    /// <summary>
    /// Opens a file for sequential reading with the given byte limit applied.
    /// </summary>
    internal static ByteLimitingStream OpenFile(
        string filePath,
        long maxBytes,
        string exceededMessage,
        bool useAsync)
    {
        var options = useAsync
            ? FileOptions.Asynchronous | FileOptions.SequentialScan
            : FileOptions.SequentialScan;

        var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            options: options);

        return new ByteLimitingStream(fileStream, maxBytes, exceededMessage);
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();

    public override int Read(byte[] buffer, int offset, int count)
        => CountBytes(_inner.Read(buffer, offset, AllowedCount(count)));

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        var allowedCount = AllowedCount(count);
#if NET10_0_OR_GREATER
        var bytesRead = await _inner.ReadAsync(buffer.AsMemory(offset, allowedCount), cancellationToken).ConfigureAwait(false);
#else
        var bytesRead = await _inner.ReadAsync(buffer, offset, allowedCount, cancellationToken).ConfigureAwait(false);
#endif
        return CountBytes(bytesRead);
    }

#if NET10_0_OR_GREATER
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        var allowed = buffer[..AllowedCount(buffer.Length)];
        var bytesRead = await _inner.ReadAsync(allowed, cancellationToken).ConfigureAwait(false);
        return CountBytes(bytesRead);
    }
#endif

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();

        base.Dispose(disposing);
    }

    /// <summary>
    /// Caps a requested read at one byte past the remaining budget, so an
    /// oversized input is still detected without buffering a whole block beyond
    /// the limit.
    /// </summary>
    private int AllowedCount(int count)
    {
        var allowed = _maxBytes - _totalBytesRead + 1;
        return allowed < count ? (int)allowed : count;
    }

    private int CountBytes(int bytesRead)
    {
        _totalBytesRead += bytesRead;
        if (_totalBytesRead > _maxBytes)
            throw new EdsParseException(_exceededMessage);

        return bytesRead;
    }
}
