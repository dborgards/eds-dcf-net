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
    /// When <see langword="true"/>, parses as a version field: plain integers use
    /// decimal <see cref="ValueConverter.ParseByteDecimalPlain"/> (aligned with XDD
    /// <c>fileVersion</c>; zero-padded <c>010</c> → 10, not CiA octal 8). When
    /// <see cref="StrictParsingScope"/> is not enabled, also accepts major/minor forms
    /// such as <c>1.0</c> / <c>1,0</c> via <see cref="ValueConverter.ParseByteAllowingMajorMinor"/>.
    /// Under strict parsing, major/minor forms are rejected. When
    /// <see langword="false"/>, uses <see cref="ValueConverter.ParseByte"/> (decimal/hex/octal).
    /// </param>
    internal static byte ParseUnsigned8(
        string sectionName,
        string keyName,
        string value,
        bool allowMajorMinorVersionForm = false)
    {
        try
        {
            if (!allowMajorMinorVersionForm)
                return ValueConverter.ParseByte(value);

            // FileVersion/FileRevision: decimal plain integers like XDD fileVersion.
            // Do not route through ParseByte (CiA octal for 0+digit).
            if (!StrictParsingScope.IsEnabled &&
                ValueConverter.TrySplitMajorMinorDecimal(value.Trim(), out var major))
            {
                Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
                    Diagnostics.ParseSeverity.Warning,
                    Diagnostics.ParseDiagnosticCodes.IniVersionMajorMinor,
                    path: sectionName + "." + keyName,
                    rawValue: value,
                    coercedTo: major,
                    message: string.Format(
                        CultureInfo.InvariantCulture,
                        "Version field '{0}' uses a major/minor tooling form; the major component {1} is used.",
                        value,
                        major)));

                return ValueConverter.ParseByteAllowingMajorMinor(value);
            }

            return ValueConverter.ParseByteDecimalPlain(value);
        }
        catch (EdsParseException ex)
        {
            var isMajorMinorForm = allowMajorMinorVersionForm
                && ValueConverter.TrySplitMajorMinorDecimal(value.Trim(), out _);
            throw Wrap(
                sectionName,
                keyName,
                ex,
                isMajorMinorForm ? Diagnostics.ParseDiagnosticCodes.IniVersionMajorMinor : null);
        }
    }

    private static EdsParseException Wrap(string sectionName, string keyName, EdsParseException ex, string? code = null)
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
            SectionName = sectionName,
            Code = code
        };
    }
}
