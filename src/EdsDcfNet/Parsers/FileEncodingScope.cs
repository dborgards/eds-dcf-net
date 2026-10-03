namespace EdsDcfNet.Parsers;

using System.Text;

/// <summary>
/// Ambient read and write settings for one facade call.
/// Mirrors <see cref="StrictParsingScope"/>: <see cref="AsyncLocal{T}"/> keeps each value
/// on the current async flow so <see cref="CanOpenFileOptions.Encoding"/>,
/// <see cref="CanOpenWriteOptions.Encoding"/> and <see cref="CanOpenWriteOptions.NewLine"/>
/// reach the parsers, the writers and <see cref="Utilities.TextFileIo"/> without changing
/// delegate signatures.
/// A null encoding means automatic detection on read and UTF-8 without a BOM on write;
/// a null line ending means the platform default.
/// </summary>
internal static class FileEncodingScope
{
    private static readonly AsyncLocal<Encoding?> ReadEncoding = new();
    private static readonly AsyncLocal<Encoding?> WriteEncoding = new();
    private static readonly AsyncLocal<string?> WriteNewLine = new();

    internal static Encoding? CurrentRead => ReadEncoding.Value;

    internal static Encoding? CurrentWrite => WriteEncoding.Value;

    /// <summary>The line ending chosen with <see cref="CanOpenWriteOptions.NewLine"/>; null keeps the platform default.</summary>
    internal static string? CurrentWriteNewLine => WriteNewLine.Value;

    internal static IDisposable EnterRead(Encoding? encoding)
    {
        var previous = ReadEncoding.Value;
        ReadEncoding.Value = encoding;
        return new Restorer<Encoding?>(ReadEncoding, previous);
    }

    internal static IDisposable EnterWrite(Encoding? encoding)
    {
        var previous = WriteEncoding.Value;
        WriteEncoding.Value = encoding;
        return new Restorer<Encoding?>(WriteEncoding, previous);
    }

    internal static IDisposable EnterWriteNewLine(string? newLine)
    {
        var previous = WriteNewLine.Value;
        WriteNewLine.Value = newLine;
        return new Restorer<string?>(WriteNewLine, previous);
    }

    private sealed class Restorer<T> : IDisposable
    {
        private readonly AsyncLocal<T> _slot;
        private readonly T _previous;
        private bool _disposed;

        internal Restorer(AsyncLocal<T> slot, T previous)
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
