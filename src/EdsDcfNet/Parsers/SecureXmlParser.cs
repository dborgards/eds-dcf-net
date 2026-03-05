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

    private static void EnsureContentWithinSizeLimit(
        string content,
        string formatName,
        long maxInputSize)
    {
        var contentSizeInBytes = Encoding.UTF8.GetByteCount(content);
        if (contentSizeInBytes > maxInputSize)
        {
            throw new EdsParseException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} content is too large ({1:N0} bytes). Maximum supported size is {2:N0} bytes.",
                    formatName,
                    contentSizeInBytes,
                    maxInputSize));
        }
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
}
