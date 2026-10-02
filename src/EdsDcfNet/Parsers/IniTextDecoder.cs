namespace EdsDcfNet.Parsers;

using System.Text;
using EdsDcfNet.Diagnostics;

/// <summary>
/// Decodes buffered INI bytes once, twice when automatic UTF-8 fails.
/// The source stream is never read again, so non-seekable streams stay consistent.
/// </summary>
internal static class IniTextDecoder
{
    internal const string Iso88591FallbackMessage = "file is not valid UTF-8, decoded as ISO-8859-1";

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly Encoding Iso88591 = Encoding.GetEncoding(28591);

    private static readonly Encoding Utf32BigEndian = new UTF32Encoding(bigEndian: true, byteOrderMark: true);

    internal static string Decode(byte[] bytes)
    {
        var explicitEncoding = FileEncodingScope.CurrentRead;
        if (explicitEncoding != null)
            return DecodeExplicit(bytes, explicitEncoding);

        if (TryDetectWideBom(bytes, out var wideEncoding, out var wideLength))
            return wideEncoding.GetString(bytes, wideLength, bytes.Length - wideLength);

        var utf8Offset = StartsWithUtf8Bom(bytes) ? 3 : 0;
        try
        {
            return StrictUtf8.GetString(bytes, utf8Offset, bytes.Length - utf8Offset);
        }
        catch (DecoderFallbackException)
        {
            ParseDiagnosticScope.Report(new ParseDiagnostic(
                ParseSeverity.Info,
                ParseDiagnosticCodes.IniDecodedAsIso88591,
                path: string.Empty,
                message: Iso88591FallbackMessage));
            return Iso88591.GetString(bytes);
        }
    }

    private static string DecodeExplicit(byte[] bytes, Encoding encoding)
    {
        var preamble = encoding.GetPreamble();
        if (preamble.Length > 0 && StartsWith(bytes, preamble))
            return encoding.GetString(bytes, preamble.Length, bytes.Length - preamble.Length);

        // UTF-8 writers often omit the BOM even though readers still accept one.
        if (StartsWithUtf8Bom(bytes) && encoding.WebName.Equals("utf-8", StringComparison.OrdinalIgnoreCase))
            return encoding.GetString(bytes, 3, bytes.Length - 3);

        return encoding.GetString(bytes);
    }

    /// <summary>UTF-16 and UTF-32 marks. UTF-32 is tested first because its mark starts with the UTF-16 mark.</summary>
    private static bool TryDetectWideBom(byte[] bytes, out Encoding encoding, out int length)
    {
        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
        {
            encoding = Encoding.UTF32;
            length = 4;
            return true;
        }

        if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
        {
            encoding = Utf32BigEndian;
            length = 4;
            return true;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            encoding = Encoding.Unicode;
            length = 2;
            return true;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            encoding = Encoding.BigEndianUnicode;
            length = 2;
            return true;
        }

        encoding = StrictUtf8;
        length = 0;
        return false;
    }

    private static bool StartsWithUtf8Bom(byte[] bytes)
        => bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

    private static bool StartsWith(byte[] bytes, byte[] prefix)
    {
        if (bytes.Length < prefix.Length)
            return false;

        for (var i = 0; i < prefix.Length; i++)
        {
            if (bytes[i] != prefix[i])
                return false;
        }

        return true;
    }
}
