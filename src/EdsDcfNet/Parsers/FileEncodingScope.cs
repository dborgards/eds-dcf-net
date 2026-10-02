namespace EdsDcfNet.Parsers;

using System.Text;

/// <summary>
/// Ambient read and write encodings for one facade call.
/// Mirrors <see cref="StrictParsingScope"/>: <see cref="AsyncLocal{T}"/> keeps each value
/// on the current async flow so <see cref="CanOpenFileOptions.Encoding"/> and
/// <see cref="CanOpenWriteOptions.Encoding"/> reach the parsers and
/// <see cref="Utilities.TextFileIo"/> without changing delegate signatures.
/// A null value means automatic detection on read and UTF-8 without a BOM on write.
/// </summary>
internal static class FileEncodingScope
{
    private static readonly AsyncLocal<Encoding?> ReadEncoding = new();
    private static readonly AsyncLocal<Encoding?> WriteEncoding = new();

    internal static Encoding? CurrentRead => ReadEncoding.Value;

    internal static Encoding? CurrentWrite => WriteEncoding.Value;

    internal static IDisposable EnterRead(Encoding? encoding)
    {
        var previous = ReadEncoding.Value;
        ReadEncoding.Value = encoding;
        return new Restorer(ReadEncoding, previous);
    }

    internal static IDisposable EnterWrite(Encoding? encoding)
    {
        var previous = WriteEncoding.Value;
        WriteEncoding.Value = encoding;
        return new Restorer(WriteEncoding, previous);
    }

    private sealed class Restorer : IDisposable
    {
        private readonly AsyncLocal<Encoding?> _slot;
        private readonly Encoding? _previous;
        private bool _disposed;

        internal Restorer(AsyncLocal<Encoding?> slot, Encoding? previous)
        {
            _slot = slot;
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _slot.Value = _previous;
            _disposed = true;
        }
    }
}
