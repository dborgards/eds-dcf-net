namespace EdsDcfNet.Parsers;

using System.Text;

/// <summary>
/// Raw-byte ceiling for a limit expressed in decoded characters.
/// </summary>
/// <remarks>
/// File APIs compare <see cref="CanOpenFileOptions.MaxInputSize"/> with the file length in
/// bytes. Stream APIs compare it with the decoded character count, so the raw buffer has
/// to be large enough for the widest encoding that can produce that many characters and
/// for a leading byte-order mark. The same helper is used by the XML reader.
/// The result saturates at <see cref="long.MaxValue"/>.
/// </remarks>
internal static class InputBufferLimit
{
    /// <summary>UTF-32 is the widest BOM-detected encoding: 4 bytes per character plus a 4-byte mark.</summary>
    private const long AutoBytesPerChar = 4;

    private const long AutoPreambleBytes = 4;

    /// <summary>
    /// Returns how many raw bytes may be buffered for <paramref name="charLimit"/> decoded characters.
    /// </summary>
    /// <param name="charLimit">Caller limit in decoded characters. Zero or negative yields no bytes.</param>
    /// <param name="encoding">
    /// Explicit encoding, or <see langword="null"/> for automatic detection
    /// (UTF-8, BOM-selected UTF-16/UTF-32, and the ISO-8859-1 fallback).
    /// </param>
    internal static long GetMaxBufferedByteCount(long charLimit, Encoding? encoding)
    {
        if (charLimit <= 0)
            return 0;

        if (encoding == null)
            return SaturatingAdd(SaturatingMultiply(charLimit, AutoBytesPerChar), AutoPreambleBytes);

        return SaturatingAdd(GetMaxByteCountSaturating(encoding, charLimit), encoding.GetPreamble().Length);
    }

    private static long GetMaxByteCountSaturating(Encoding encoding, long charLimit)
    {
        if (charLimit > int.MaxValue)
            return long.MaxValue;

        try
        {
            return encoding.GetMaxByteCount((int)charLimit);
        }
        catch (ArgumentOutOfRangeException)
        {
            return long.MaxValue;
        }
    }

    private static long SaturatingMultiply(long left, long right)
    {
        try
        {
            return checked(left * right);
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
    }

    private static long SaturatingAdd(long left, long right)
    {
        try
        {
            return checked(left + right);
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
    }
}
