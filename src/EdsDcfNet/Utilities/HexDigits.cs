namespace EdsDcfNet.Utilities;

/// <summary>
/// Character checks for hexadecimal text, shared by the INI readers and the validator.
/// </summary>
internal static class HexDigits
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="value"/> is non-empty and
    /// consists solely of hexadecimal digits (no whitespace, no <c>0x</c> prefix).
    /// </summary>
    internal static bool IsHexDigitsOnly(string value)
    {
        if (value.Length == 0)
            return false;

        foreach (var c in value)
        {
            var isHexDigit = (c >= '0' && c <= '9') ||
                             (c >= 'a' && c <= 'f') ||
                             (c >= 'A' && c <= 'F');
            if (!isHexDigit)
                return false;
        }

        return true;
    }
}
