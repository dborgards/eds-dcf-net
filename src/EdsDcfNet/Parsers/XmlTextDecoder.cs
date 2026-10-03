namespace EdsDcfNet.Parsers;

using System.Globalization;
using System.Text;
using EdsDcfNet.Exceptions;

/// <summary>
/// Decodes buffered XDD/XDC bytes once, so a non-seekable stream is never read again.
/// A byte-order mark wins. Otherwise the XML declaration names the encoding.
/// <see cref="FileEncodingScope.CurrentRead"/> overrides the declaration.
/// </summary>
internal static class XmlTextDecoder
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly Encoding Utf32BigEndian = new UTF32Encoding(bigEndian: true, byteOrderMark: false, throwOnInvalidCharacters: true);

    private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

    private static readonly byte[] Utf16LeBom = { 0xFF, 0xFE };

    private static readonly byte[] Utf16BeBom = { 0xFE, 0xFF };

    private static readonly byte[] Utf32LeBom = { 0xFF, 0xFE, 0x00, 0x00 };

    private static readonly byte[] Utf32BeBom = { 0x00, 0x00, 0xFE, 0xFF };

    private static readonly byte[] Utf32BeSignature = { 0x00, 0x00, 0x00, 0x3C };

    private static readonly byte[] Utf32LeSignature = { 0x3C, 0x00, 0x00, 0x00 };

    /// <summary>XML appendix F signature for <c>&lt;?xm</c> in EBCDIC.</summary>
    private static readonly byte[] EbcdicXmlSignature = { 0x4C, 0x6F, 0xA7, 0x94 };

    /// <summary>
    /// EBCDIC byte to ASCII for the XML declaration repertoire (IBM037, and the other
    /// EBCDIC pages that share those bytes). 0xFC is the quotation mark on IBM1026.
    /// 0x15 is the line feed on IBM1047.
    /// </summary>
    private static readonly byte[] EbcdicDeclarationToAscii = CreateEbcdicDeclarationMap();

    private const int MaxEbcdicDeclarationBytes = 2048;

    private const string EncodingAttribute = "encoding";

    internal static string Decode(byte[] bytes, string formatName)
    {
        var explicitEncoding = FileEncodingScope.CurrentRead;
        if (explicitEncoding != null)
            return IniTextDecoder.DecodeExplicit(bytes, explicitEncoding);

        if (TryDetectWide(bytes, out var wideEncoding, out var wideOffset))
            return DecodeStrict(wideEncoding, bytes, wideOffset, formatName, wideEncoding.WebName);

        var declared = TryReadDeclaredEncoding(bytes);
        if (declared == null && StartsWith(bytes, EbcdicXmlSignature))
        {
            declared = TryReadDeclaredEncoding(TranslateEbcdicDeclaration(bytes));
            if (declared == null)
            {
                throw new EdsParseException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} content uses an EBCDIC encoding but does not declare one.",
                        formatName));
            }
        }

        if (declared == null)
            return DecodeStrict(StrictUtf8, bytes, 0, formatName, "utf-8");

        if (RequiresByteOrderMark(declared))
        {
            throw new EdsParseException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} content declares encoding '{1}', which requires a byte order mark.",
                    formatName,
                    declared));
        }

        Encoding encoding;
        try
        {
            encoding = string.Equals(declared, "utf-8", StringComparison.OrdinalIgnoreCase)
                ? StrictUtf8
                : Encoding.GetEncoding(declared);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            throw new EdsParseException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} content declares encoding '{1}', which this runtime does not support.",
                    formatName,
                    declared),
                ex);
        }

        return DecodeStrict(encoding, bytes, 0, formatName, declared);
    }

    private static string DecodeStrict(
        Encoding encoding,
        byte[] bytes,
        int index,
        string formatName,
        string displayName)
    {
        var decoding = (Encoding)encoding.Clone();
        decoding.DecoderFallback = DecoderFallback.ExceptionFallback;
        try
        {
            return decoding.GetString(bytes, index, bytes.Length - index);
        }
        catch (Exception ex) when (ex is DecoderFallbackException or ArgumentException)
        {
            throw new EdsParseException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} content could not be decoded with encoding '{1}'.",
                    formatName,
                    displayName),
                ex);
        }
    }

    /// <summary>
    /// BOM, then the no-BOM wide signatures from the XML specification.
    /// UTF-32 is tested before UTF-16 because its mark starts with the UTF-16 mark.
    /// </summary>
    private static bool TryDetectWide(byte[] bytes, out Encoding encoding, out int offset)
    {
        if (StartsWith(bytes, Utf32LeBom))
        {
            encoding = Encoding.UTF32;
            offset = Utf32LeBom.Length;
            return true;
        }

        if (StartsWith(bytes, Utf32BeBom))
        {
            encoding = Utf32BigEndian;
            offset = Utf32BeBom.Length;
            return true;
        }

        if (StartsWith(bytes, Utf16LeBom))
        {
            encoding = Encoding.Unicode;
            offset = Utf16LeBom.Length;
            return true;
        }

        if (StartsWith(bytes, Utf16BeBom))
        {
            encoding = Encoding.BigEndianUnicode;
            offset = Utf16BeBom.Length;
            return true;
        }

        if (StartsWith(bytes, Utf8Bom))
        {
            encoding = StrictUtf8;
            offset = Utf8Bom.Length;
            return true;
        }

        if (StartsWith(bytes, Utf32BeSignature))
        {
            encoding = Utf32BigEndian;
            offset = 0;
            return true;
        }

        if (StartsWith(bytes, Utf32LeSignature))
        {
            encoding = Encoding.UTF32;
            offset = 0;
            return true;
        }

        if (bytes.Length >= 2 && bytes[0] == 0x00 && bytes[1] == 0x3C)
        {
            encoding = Encoding.BigEndianUnicode;
            offset = 0;
            return true;
        }

        if (bytes.Length >= 2 && bytes[0] == 0x3C && bytes[1] == 0x00)
        {
            encoding = Encoding.Unicode;
            offset = 0;
            return true;
        }

        encoding = StrictUtf8;
        offset = 0;
        return false;
    }

    private static bool RequiresByteOrderMark(string name)
        => name.Equals("utf-16", StringComparison.OrdinalIgnoreCase)
           || name.Equals("utf-16be", StringComparison.OrdinalIgnoreCase)
           || name.Equals("utf-16le", StringComparison.OrdinalIgnoreCase)
           || name.Equals("utf-32", StringComparison.OrdinalIgnoreCase)
           || name.Equals("utf-32be", StringComparison.OrdinalIgnoreCase)
           || name.Equals("utf-32le", StringComparison.OrdinalIgnoreCase)
           || name.Equals("ucs-2", StringComparison.OrdinalIgnoreCase)
           || name.Equals("iso-10646-ucs-2", StringComparison.OrdinalIgnoreCase);

    private static byte[] TranslateEbcdicDeclaration(byte[] bytes)
    {
        var end = IndexOfEbcdicDeclarationEnd(bytes);
        if (end < 0)
            end = Math.Min(bytes.Length, MaxEbcdicDeclarationBytes);
        else
            end = Math.Min(bytes.Length, end + 2);

        var ascii = new byte[end];
        for (var i = 0; i < end; i++)
            ascii[i] = EbcdicDeclarationToAscii[bytes[i]];

        return ascii;
    }

    private static int IndexOfEbcdicDeclarationEnd(byte[] bytes)
    {
        var last = Math.Min(bytes.Length, MaxEbcdicDeclarationBytes) - 2;
        for (var i = 0; i <= last; i++)
        {
            if (bytes[i] == 0x6F && bytes[i + 1] == 0x6E)
                return i;
        }

        return -1;
    }

    private static byte[] CreateEbcdicDeclarationMap()
    {
        var map = new byte[256];
        map[0x05] = (byte)'\t';
        map[0x0D] = (byte)'\r';
        map[0x15] = (byte)'\n';
        map[0x25] = (byte)'\n';
        map[0x40] = (byte)' ';
        map[0x4B] = (byte)'.';
        map[0x4C] = (byte)'<';
        map[0x60] = (byte)'-';
        map[0x6D] = (byte)'_';
        map[0x6E] = (byte)'>';
        map[0x6F] = (byte)'?';
        map[0x7D] = (byte)'\'';
        map[0x7E] = (byte)'=';
        map[0x7F] = (byte)'"';
        map[0xFC] = (byte)'"';

        for (var digit = 0; digit <= 9; digit++)
            map[0xF0 + digit] = (byte)('0' + digit);

        MapLetters(map, 0x81, "abcdefghi");
        MapLetters(map, 0x91, "jklmnopqr");
        MapLetters(map, 0xA2, "stuvwxyz");
        MapLetters(map, 0xC1, "ABCDEFGHI");
        MapLetters(map, 0xD1, "JKLMNOPQR");
        MapLetters(map, 0xE2, "STUVWXYZ");
        return map;
    }

    private static void MapLetters(byte[] map, int start, string letters)
    {
        for (var i = 0; i < letters.Length; i++)
            map[start + i] = (byte)letters[i];
    }

    private static string? TryReadDeclaredEncoding(byte[] bytes)
    {
        if (!StartsWithAscii(bytes, "<?xml"))
            return null;

        var declEnd = IndexOfAscii(bytes, 5, bytes.Length, "?>");
        if (declEnd < 0)
            return null;

        var key = IndexOfAscii(bytes, 5, declEnd, EncodingAttribute);
        if (key < 0)
            return null;

        var index = SkipWhitespace(bytes, key + EncodingAttribute.Length, declEnd);
        if (index >= declEnd || bytes[index] != (byte)'=')
            return null;

        index = SkipWhitespace(bytes, index + 1, declEnd);
        if (index >= declEnd)
            return null;

        var quote = bytes[index];
        if (quote != (byte)'"' && quote != (byte)'\'')
            return null;

        var start = index + 1;
        var end = start;
        while (end < declEnd && bytes[end] != quote)
            end++;

        if (end >= declEnd)
            return null;

        var name = Encoding.ASCII.GetString(bytes, start, end - start).Trim();
        return name.Length == 0 ? null : name;
    }

    private static int SkipWhitespace(byte[] bytes, int index, int end)
    {
        while (index < end)
        {
            var value = bytes[index];
            if (value != (byte)' ' && value != (byte)'\t' && value != (byte)'\r' && value != (byte)'\n')
                break;

            index++;
        }

        return index;
    }

    private static bool StartsWithAscii(byte[] bytes, string ascii)
    {
        if (bytes.Length < ascii.Length)
            return false;

        for (var i = 0; i < ascii.Length; i++)
        {
            if (bytes[i] != (byte)ascii[i])
                return false;
        }

        return true;
    }

    private static int IndexOfAscii(byte[] bytes, int start, int end, string ascii)
    {
        var last = end - ascii.Length;
        for (var i = start; i <= last; i++)
        {
            var match = true;
            for (var j = 0; j < ascii.Length; j++)
            {
                if (bytes[i + j] != (byte)ascii[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return i;
        }

        return -1;
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
