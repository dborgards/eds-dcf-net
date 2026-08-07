namespace EdsDcfNet.Parsers;

using System.Globalization;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Utilities;

/// <summary>
/// Parses numeric INI field values and rethrows <see cref="EdsParseException"/>
/// with section and key attribution for callers.
/// </summary>
internal static class SectionNumericParser
{
    /// <summary>
    /// Parses an <c>Unsigned8</c> field from an INI section.
    /// </summary>
    /// <param name="sectionName">INI section name (e.g. <c>FileInfo</c>).</param>
    /// <param name="keyName">INI key name (e.g. <c>FileVersion</c>).</param>
    /// <param name="value">Raw field value.</param>
    /// <param name="allowMajorMinorVersionForm">
    /// When <see langword="true"/> and <see cref="StrictParsingScope"/> is not enabled,
    /// accepts major/minor forms such as <c>1.0</c> / <c>1,0</c> via
    /// <see cref="ValueConverter.ParseByteAllowingMajorMinor"/>. Under strict parsing,
    /// only integer forms accepted by <see cref="ValueConverter.ParseByte"/> are allowed.
    /// </param>
    internal static byte ParseUnsigned8(
        string sectionName,
        string keyName,
        string value,
        bool allowMajorMinorVersionForm = false)
    {
        try
        {
            if (allowMajorMinorVersionForm && !StrictParsingScope.IsEnabled)
                return ValueConverter.ParseByteAllowingMajorMinor(value);

            return ValueConverter.ParseByte(value);
        }
        catch (EdsParseException ex)
        {
            throw Wrap(sectionName, keyName, ex);
        }
    }

    private static EdsParseException Wrap(string sectionName, string keyName, EdsParseException ex)
    {
        return new EdsParseException(
            string.Format(
                CultureInfo.InvariantCulture,
                "[{0}] {1}: {2}",
                sectionName,
                keyName,
                ex.Message),
            ex)
        {
            SectionName = sectionName
        };
    }
}
