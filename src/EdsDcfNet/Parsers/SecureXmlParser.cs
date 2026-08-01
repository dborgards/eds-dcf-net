namespace EdsDcfNet.Parsers;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
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

    internal static string ReadContentFromStreamWithLimit(
        Stream stream,
        string formatName,
        long maxInputSize = DefaultMaxInputSize)
    {
        EnsureStreamReadable(stream);

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true);

        return ReadAllWithLimit(reader, formatName, maxInputSize);
    }

    internal static async Task<string> ReadContentFromStreamWithLimitAsync(
        Stream stream,
        string formatName,
        long maxInputSize = DefaultMaxInputSize,
        CancellationToken cancellationToken = default)
    {
        EnsureStreamReadable(stream);

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true);

        return await ReadAllWithLimitAsync(reader, formatName, maxInputSize, cancellationToken)
            .ConfigureAwait(false);
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

    private static string ReadAllWithLimit(
        StreamReader reader,
        string formatName,
        long maxInputSize)
    {
        var builder = new StringBuilder();
        var buffer = new char[4096];
        long totalChars = 0;

        while (true)
        {
            var charsRead = reader.Read(buffer, 0, buffer.Length);
            if (charsRead == 0)
                break;

            totalChars += charsRead;
            if (totalChars > maxInputSize)
            {
                throw new EdsParseException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} content is too large ({1:N0} characters). Maximum supported size is {2:N0} characters.",
                        formatName,
                        totalChars,
                        maxInputSize));
            }

            builder.Append(buffer, 0, charsRead);
        }

        return builder.ToString();
    }

    private static async Task<string> ReadAllWithLimitAsync(
        StreamReader reader,
        string formatName,
        long maxInputSize,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var buffer = new char[4096];
        long totalChars = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
#if NET10_0_OR_GREATER
            var charsRead = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
#else
            var charsRead = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
#endif
            if (charsRead == 0)
                break;

            totalChars += charsRead;
            if (totalChars > maxInputSize)
            {
                throw new EdsParseException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} content is too large ({1:N0} characters). Maximum supported size is {2:N0} characters.",
                        formatName,
                        totalChars,
                        maxInputSize));
            }

            builder.Append(buffer, 0, charsRead);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return builder.ToString();
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
