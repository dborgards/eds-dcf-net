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

    private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

    private static readonly byte[] Utf16LeBom = { 0xFF, 0xFE };

    private static readonly byte[] Utf16BeBom = { 0xFE, 0xFF };

    private static readonly byte[] Utf32LeBom = { 0xFF, 0xFE, 0x00, 0x00 };

    private static readonly byte[] Utf32BeBom = { 0x00, 0x00, 0xFE, 0xFF };

    internal static string Decode(byte[] bytes)
    {
        var explicitEncoding = FileEncodingScope.CurrentRead;
        if (explicitEncoding != null)
            return DecodeExplicit(bytes, explicitEncoding);

        if (TryDetectWideBom(bytes, out var wideEncoding, out var wideLength))
            return wideEncoding.GetString(bytes, wideLength, bytes.Length - wideLength);

        var utf8Offset = StartsWith(bytes, Utf8Bom) ? Utf8Bom.Length : 0;
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
            // Same offset as the strict decode. Including the UTF-8 BOM would
            // turn it into "ï»¿" and hide a leading section header.
            return Iso88591.GetString(bytes, utf8Offset, bytes.Length - utf8Offset);
        }
    }

    private static string DecodeExplicit(byte[] bytes, Encoding encoding)
    {
        var bomLength = MatchingBomLength(bytes, encoding);
        if (bomLength > 0)
            return encoding.GetString(bytes, bomLength, bytes.Length - bomLength);

        return encoding.GetString(bytes);
    }

    /// <summary>
    /// Length of a byte-order mark that belongs to <paramref name="encoding"/>.
    /// Code page, not <see cref="Encoding.GetPreamble"/>: a <see cref="UnicodeEncoding"/>
    /// or <see cref="UTF32Encoding"/> constructed with <c>byteOrderMark: false</c> reports
    /// an empty preamble and would otherwise decode the mark as U+FEFF.
    /// </summary>
    private static int MatchingBomLength(byte[] bytes, Encoding encoding)
    {
        if (encoding.CodePage == Encoding.UTF32.CodePage && StartsWith(bytes, Utf32LeBom))
            return Utf32LeBom.Length;

        if (encoding.CodePage == Utf32BigEndian.CodePage && StartsWith(bytes, Utf32BeBom))
            return Utf32BeBom.Length;

        if (encoding.CodePage == Encoding.Unicode.CodePage && StartsWith(bytes, Utf16LeBom))
            return Utf16LeBom.Length;

        if (encoding.CodePage == Encoding.BigEndianUnicode.CodePage && StartsWith(bytes, Utf16BeBom))
            return Utf16BeBom.Length;

        if (encoding.CodePage == StrictUtf8.CodePage && StartsWith(bytes, Utf8Bom))
            return Utf8Bom.Length;

        var preamble = encoding.GetPreamble();
        if (preamble.Length > 0 && StartsWith(bytes, preamble))
            return preamble.Length;

        return 0;
    }

    /// <summary>UTF-16 and UTF-32 marks. UTF-32 is tested first because its mark starts with the UTF-16 mark.</summary>
    private static bool TryDetectWideBom(byte[] bytes, out Encoding encoding, out int length)
    {
        if (StartsWith(bytes, Utf32LeBom))
        {
            encoding = Encoding.UTF32;
            length = Utf32LeBom.Length;
            return true;
        }

        if (StartsWith(bytes, Utf32BeBom))
        {
            encoding = Utf32BigEndian;
            length = Utf32BeBom.Length;
            return true;
        }

        if (StartsWith(bytes, Utf16LeBom))
        {
            encoding = Encoding.Unicode;
            length = Utf16LeBom.Length;
            return true;
        }

        if (StartsWith(bytes, Utf16BeBom))
        {
            encoding = Encoding.BigEndianUnicode;
            length = Utf16BeBom.Length;
            return true;
        }

        encoding = StrictUtf8;
        length = 0;
        return false;
    }

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
