namespace EdsDcfNet.Parsers;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using EdsDcfNet.Exceptions;

/// <summary>
/// Shared secure XML parsing helpers for XDD/XDC readers.
/// </summary>
internal static class SecureXmlParser
{
    internal const long DefaultMaxInputSize = ReaderDefaults.DefaultMaxInputSize;

    /// <summary>
    /// Default maximum allowed <see cref="XmlReader.Depth"/> for XDD/XDC parsing.
    /// Root elements have depth 0. CiA 311 profiles are typically around depth 8;
    /// 64 blocks deep-nesting DoS payloads while remaining generous for real files.
    /// </summary>
    internal const int DefaultMaxDepth = 64;

    internal static void EnsureFileWithinSizeLimit(
        string filePath,
        string formatName,
        long maxInputSize = DefaultMaxInputSize)
    {
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > maxInputSize)
        {
            throw new EdsParseException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} file '{1}' is too large ({2:N0} bytes). Maximum supported size is {3:N0} bytes.",
                    formatName,
                    filePath,
                    fileInfo.Length,
                    maxInputSize));
        }
    }

    /// <summary>
    /// Opens a file for sequential reading with <paramref name="maxInputSize"/>
    /// enforced in bytes while reading, so the limit still holds if the file grew
    /// after <see cref="EnsureFileWithinSizeLimit"/> inspected its length.
    /// </summary>
    internal static Stream OpenFileWithSizeLimit(
        string filePath,
        string formatName,
        long maxInputSize,
        bool useAsync)
    {
        var message = string.Format(
            CultureInfo.InvariantCulture,
            "{0} file '{1}' is too large. Maximum supported size is {2:N0} bytes.",
            formatName,
            filePath,
            maxInputSize);

        return ByteLimitingStream.OpenFile(filePath, maxInputSize, message, useAsync);
    }

    internal static XDocument ParseDocument(
        string content,
        string formatName,
        string parseErrorMessage,
        long maxInputSize = DefaultMaxInputSize,
        int maxDepth = DefaultMaxDepth)
    {
        EnsureContentWithinSizeLimit(content, formatName, maxInputSize);
        if (maxDepth < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxDepth),
                maxDepth,
                "Maximum XML nesting depth must be non-negative.");
        }

        try
        {
            var settings = CreateSecureReaderSettings(maxInputSize);
            using var stringReader = new StringReader(content);
            using var depthLimitedReader = new DepthLimitingXmlReader(
                XmlReader.Create(stringReader, settings),
                maxDepth,
                formatName);
            return XDocument.Load(depthLimitedReader, LoadOptions.None);
        }
        catch (XmlException ex)
        {
            throw new EdsParseException(parseErrorMessage, ex);
        }
    }

    /// <summary>
    /// Buffers <paramref name="stream"/> once and decodes it from the BOM or the XML declaration.
    /// File callers pass a <see cref="ByteLimitingStream"/> so the caller's limit stays a byte cap.
    /// Stream callers are capped at the decoded character count, with a raw-byte ceiling from
    /// <see cref="InputBufferLimit"/> so multibyte text of length N is not rejected as N bytes.
    /// The decoded text is parsed by <see cref="ParseDocument"/>, which keeps
    /// <see cref="DtdProcessing.Prohibit"/>, a null <see cref="XmlResolver"/>,
    /// <see cref="XmlReaderSettings.MaxCharactersInDocument"/>, and the depth limit.
    /// That reader counter is not the stream's character limit: without a BOM it counts
    /// UTF-8 bytes, which would reject N non-ASCII characters at limit N.
    /// </summary>
    internal static string ReadContentFromStreamWithLimit(
        Stream stream,
        string formatName,
        long maxInputSize = DefaultMaxInputSize)
    {
        EnsureStreamReadable(stream);
        var bytes = ReadBounded(stream, StreamByteCap(maxInputSize), StreamByteCapMessage(formatName, maxInputSize));
        return DecodeWithinCharacterLimit(bytes, formatName, maxInputSize);
    }

    internal static async Task<string> ReadContentFromStreamWithLimitAsync(
        Stream stream,
        string formatName,
        long maxInputSize = DefaultMaxInputSize,
        CancellationToken cancellationToken = default)
    {
        EnsureStreamReadable(stream);
        var bytes = await ReadBoundedAsync(
            stream,
            StreamByteCap(maxInputSize),
            StreamByteCapMessage(formatName, maxInputSize),
            cancellationToken).ConfigureAwait(false);
        return DecodeWithinCharacterLimit(bytes, formatName, maxInputSize);
    }

    private static void EnsureContentWithinSizeLimit(
        string content,
        string formatName,
        long maxInputSize)
    {
        if (content.Length > maxInputSize)
        {
            throw new EdsParseException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} content is too large ({1:N0} characters). Maximum supported size is {2:N0} characters.",
                    formatName,
                    content.Length,
                    maxInputSize));
        }
    }

    private static void EnsureStreamReadable(Stream stream)
    {
        ThrowIfNull(stream, nameof(stream));
        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(stream));
    }

    private static string DecodeWithinCharacterLimit(byte[] bytes, string formatName, long maxInputSize)
    {
        var text = XmlTextDecoder.Decode(bytes, formatName);
        if ((long)text.Length > maxInputSize)
        {
            throw new EdsParseException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} content is too large ({1:N0} characters). Maximum supported size is {2:N0} characters.",
                    formatName,
                    text.Length,
                    maxInputSize));
        }

        return text;
    }

    private static long StreamByteCap(long maxInputSize)
        => InputBufferLimit.GetMaxBufferedByteCount(maxInputSize, FileEncodingScope.CurrentRead);

    private static string StreamByteCapMessage(string formatName, long maxInputSize)
        => string.Format(
            CultureInfo.InvariantCulture,
            "{0} content is too large. Maximum supported size is {1:N0} characters.",
            formatName,
            maxInputSize);

    private const int ReadChunkSize = 8192;

    /// <summary>
    /// Reads at most <paramref name="maxBytes"/>, plus one probe byte when the stream
    /// continues, then throws. The remainder of an overlong stream is not consumed.
    /// </summary>
    private static byte[] ReadBounded(Stream stream, long maxBytes, string exceededMessage)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[ReadChunkSize];
        long total = 0;
        while (true)
        {
            var want = NextReadSize(maxBytes, total);
            var read = stream.Read(chunk, 0, want);
            if (read == 0)
                break;

            total += read;
            if (total > maxBytes)
                throw new EdsParseException(exceededMessage);

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream stream,
        long maxBytes,
        string exceededMessage,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[ReadChunkSize];
        long total = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var want = NextReadSize(maxBytes, total);
#if NET10_0_OR_GREATER
            var read = await stream.ReadAsync(chunk.AsMemory(0, want), cancellationToken).ConfigureAwait(false);
#else
            var read = await stream.ReadAsync(chunk, 0, want, cancellationToken).ConfigureAwait(false);
#endif
            if (read == 0)
                break;

            total += read;
            if (total > maxBytes)
                throw new EdsParseException(exceededMessage);

            buffer.Write(chunk, 0, read);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return buffer.ToArray();
    }

    private static int NextReadSize(long maxBytes, long total)
    {
        if (maxBytes == long.MaxValue)
            return ReadChunkSize;

        var remaining = maxBytes - total;
        if (remaining < 0)
            return 0;

        var probe = remaining >= int.MaxValue ? int.MaxValue : (int)remaining + 1;
        return probe < ReadChunkSize ? probe : ReadChunkSize;
    }

    private static XmlReaderSettings CreateSecureReaderSettings(long maxInputSize)
    {
        return new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = maxInputSize,
            MaxCharactersFromEntities = maxInputSize
        };
    }

    [ExcludeFromCodeCoverage]
    private static void ThrowIfNull(object? value, string parameterName)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value, parameterName);
#else
        if (value == null)
            throw new ArgumentNullException(parameterName);
#endif
    }

    /// <summary>
    /// Forwards to an inner <see cref="XmlReader"/> and rejects nodes whose
    /// <see cref="XmlReader.Depth"/> exceeds a configured maximum.
    /// </summary>
    private sealed class DepthLimitingXmlReader : XmlReader
    {
        private readonly XmlReader _inner;
        private readonly int _maxDepth;
        private readonly string _formatName;

        public DepthLimitingXmlReader(XmlReader inner, int maxDepth, string formatName)
        {
            _inner = inner;
            _maxDepth = maxDepth;
            _formatName = formatName;
        }

        public override XmlNodeType NodeType => _inner.NodeType;
        public override string LocalName => _inner.LocalName;
        public override string NamespaceURI => _inner.NamespaceURI;
        public override string Prefix => _inner.Prefix;
        public override string Value => _inner.Value;
        public override int Depth => _inner.Depth;
        public override string BaseURI => _inner.BaseURI;
        public override bool IsEmptyElement => _inner.IsEmptyElement;
        public override int AttributeCount => _inner.AttributeCount;
        public override bool EOF => _inner.EOF;
        public override ReadState ReadState => _inner.ReadState;
        public override XmlNameTable NameTable => _inner.NameTable;

        public override string? GetAttribute(string name) => _inner.GetAttribute(name);
        public override string? GetAttribute(string name, string? namespaceURI) => _inner.GetAttribute(name, namespaceURI);
        public override string GetAttribute(int i) => _inner.GetAttribute(i);

        public override bool MoveToAttribute(string name) => _inner.MoveToAttribute(name);
        public override bool MoveToAttribute(string name, string? ns) => _inner.MoveToAttribute(name, ns);
        public override void MoveToAttribute(int i) => _inner.MoveToAttribute(i);
        public override bool MoveToFirstAttribute() => _inner.MoveToFirstAttribute();
        public override bool MoveToNextAttribute() => _inner.MoveToNextAttribute();
        public override bool MoveToElement() => _inner.MoveToElement();
        public override bool ReadAttributeValue() => _inner.ReadAttributeValue();

        public override string? LookupNamespace(string prefix) => _inner.LookupNamespace(prefix);
        public override void ResolveEntity() => _inner.ResolveEntity();

        public override bool Read()
        {
            if (!_inner.Read())
                return false;

            EnsureDepthWithinLimit();
            return true;
        }

        public override void Close() => _inner.Close();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();
            base.Dispose(disposing);
        }

        private void EnsureDepthWithinLimit()
        {
            if (_inner.Depth <= _maxDepth)
                return;

            throw new EdsParseException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} content exceeds the maximum XML nesting depth of {1}.",
                    _formatName,
                    _maxDepth));
        }
    }
}
