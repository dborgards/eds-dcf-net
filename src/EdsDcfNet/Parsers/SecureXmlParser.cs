namespace EdsDcfNet.Parsers;

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
    internal const long DefaultMaxInputSize = IniParser.DefaultMaxInputSize;

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
        long maxInputSize = DefaultMaxInputSize)
    {
        EnsureContentWithinSizeLimit(content, formatName, maxInputSize);

        try
        {
            var settings = CreateSecureReaderSettings(maxInputSize);
            using var stringReader = new StringReader(content);
            using var xmlReader = XmlReader.Create(stringReader, settings);
            return XDocument.Load(xmlReader, LoadOptions.None);
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
        EnsureStreamWithinSizeLimit(stream, formatName, maxInputSize);

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
        EnsureStreamWithinSizeLimit(stream, formatName, maxInputSize);

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

    private static void EnsureStreamWithinSizeLimit(
        Stream stream,
        string formatName,
        long maxInputSize)
    {
        ThrowIfNull(stream, nameof(stream));
        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(stream));

        if (!stream.CanSeek)
            return;

        var remainingLength = stream.Length - stream.Position;
        if (remainingLength > maxInputSize)
        {
            throw new EdsParseException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} stream content is too large ({1:N0} bytes). Maximum supported size is {2:N0} bytes.",
                    formatName,
                    remainingLength,
                    maxInputSize));
        }
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
            var charsRead = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
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

    private static void ThrowIfNull(object? value, string parameterName)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value, parameterName);
#else
        if (value == null)
            throw new ArgumentNullException(parameterName);
#endif
    }
}
