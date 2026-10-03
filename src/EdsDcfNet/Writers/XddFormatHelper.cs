namespace EdsDcfNet.Writers;

using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using EdsDcfNet.Models;

/// <summary>
/// Static formatting helpers shared by XDD/XDC builder components.
/// </summary>
internal static class XddFormatHelper
{
    /// <summary>Formats a 16-bit index as 4 uppercase hex digits (e.g. "1000").</summary>
    internal static string FormatIndex(ushort index) =>
        index.ToString("X4", CultureInfo.InvariantCulture);

    /// <summary>Formats a data type as 4 uppercase hex digits (e.g. "0007").</summary>
    /// <remarks>
    /// Used for <c>CANopenObject</c> and <c>CANopenSubObject</c>.
    /// <c>dynamicChannel/@dataType</c> uses <see cref="FormatDynamicChannelDataType"/>.
    /// </remarks>
    internal static string FormatDataType(ushort dataType) =>
        dataType.ToString("X4", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats an unsigned value as <c>xsd:hexBinary</c>: uppercase hexadecimal,
    /// an even number of digits, and at least four digits.
    /// </summary>
    /// <remarks>
    /// An odd count is not schema-valid, so a value whose natural width is five
    /// or seven digits is padded to six or eight. Dynamic-channel <c>dataType</c>
    /// is not formatted here; see <see cref="FormatDynamicChannelDataType"/>.
    /// </remarks>
    internal static string FormatHexBinary(uint value)
    {
        var text = value.ToString("X", CultureInfo.InvariantCulture);
        if (text.Length < 4)
            text = text.PadLeft(4, '0');
        if ((text.Length & 1) != 0)
            text = "0" + text;
        return text;
    }

    /// <summary>
    /// Formats <c>dynamicChannel/@dataType</c>.
    /// </summary>
    /// <remarks>
    /// The schema annotation and CiA 311 Table 51 specify two hex digits, unlike
    /// <see cref="FormatDataType"/>, which emits four digits for a CANopen object.
    /// Values above <c>0xFF</c> use four digits so a <see cref="ushort"/> is not
    /// truncated. The compiled <c>xsd:hexBinary</c> type accepts both widths.
    /// </remarks>
    internal static string FormatDynamicChannelDataType(ushort dataType) =>
        dataType <= byte.MaxValue
            ? dataType.ToString("X2", CultureInfo.InvariantCulture)
            : dataType.ToString("X4", CultureInfo.InvariantCulture);

    /// <summary>
    /// Converts an <see cref="AccessType"/> to a CiA 311 <c>dynamicChannel</c>
    /// <c>accessType</c> token.
    /// </summary>
    /// <remarks>
    /// The schema allows <c>readOnly</c>, <c>writeOnly</c>, and <c>readWriteOutput</c>.
    /// <see cref="AccessType.Constant"/> is written as <c>readOnly</c>.
    /// <see cref="AccessType.ReadWrite"/> and <see cref="AccessType.ReadWriteInput"/>
    /// are written as <c>readWriteOutput</c>, the only read/write token.
    /// </remarks>
    internal static string DynamicChannelAccessTypeToString(AccessType accessType) =>
        accessType switch
        {
            AccessType.WriteOnly => "writeOnly",
            AccessType.ReadWrite => "readWriteOutput",
            AccessType.ReadWriteInput => "readWriteOutput",
            AccessType.ReadWriteOutput => "readWriteOutput",
            _ => "readOnly"
        };

    /// <summary>
    /// Converts an <see cref="AccessType"/> to an XDD access type string.
    /// <see cref="AccessType.ReadWriteInput"/> and <see cref="AccessType.ReadWriteOutput"/>
    /// have no XDD equivalent and are mapped to "rw".
    /// </summary>
    internal static string AccessTypeToString(AccessType accessType) =>
        accessType switch
        {
            AccessType.Constant => "const",
            AccessType.ReadOnly => "ro",
            AccessType.WriteOnly => "wo",
            AccessType.ReadWrite => "rw",
            AccessType.ReadWriteInput => "rw",
            AccessType.ReadWriteOutput => "rw",
            _ => "rw"
        };

    /// <summary>Formats a baud rate in kbps as the XDD string form (e.g. "250 Kbps").</summary>
    internal static string FormatBaudRate(ushort kbps) =>
        string.Format(CultureInfo.InvariantCulture, "{0} Kbps", kbps);

    /// <summary>Yields the supported baud rates in the CiA 311 spelling, ascending, <c>auto-baudRate</c> last.</summary>
    internal static IEnumerable<string> GetSupportedBaudRates(BaudRates baudRates)
    {
        if (baudRates.BaudRate10) yield return FormatBaudRate(10);
        if (baudRates.BaudRate20) yield return FormatBaudRate(20);
        if (baudRates.BaudRate50) yield return FormatBaudRate(50);
        if (baudRates.BaudRate100) yield return FormatBaudRate(100);
        if (baudRates.BaudRate125) yield return FormatBaudRate(125);
        if (baudRates.BaudRate250) yield return FormatBaudRate(250);
        if (baudRates.BaudRate500) yield return FormatBaudRate(500);
        if (baudRates.BaudRate800) yield return FormatBaudRate(800);
        if (baudRates.BaudRate1000) yield return FormatBaudRate(1000);
        if (baudRates.AutoBaudRate) yield return AutoBaudRate;
    }

    /// <summary>
    /// Returns the <c>baudRate/@defaultValue</c>: the value read from an XDD/XDC while the supported
    /// flags are unchanged since the read, otherwise the preferred default from the supported set,
    /// falling back to "250 Kbps" when no rates are flagged.
    /// </summary>
    internal static string GetDefaultBaudRateString(BaudRates baudRates)
    {
        if (baudRates.DefaultValueLexical != null && baudRates.DefaultValueFlagsBaseline == baudRates.FlagMask())
            return baudRates.DefaultValueLexical;

        if (baudRates.BaudRate250) return "250 Kbps";
        if (baudRates.BaudRate500) return "500 Kbps";
        if (baudRates.BaudRate125) return "125 Kbps";
        if (baudRates.BaudRate1000) return "1000 Kbps";
        if (baudRates.BaudRate800) return "800 Kbps";
        if (baudRates.BaudRate100) return "100 Kbps";
        if (baudRates.BaudRate50) return "50 Kbps";
        if (baudRates.BaudRate20) return "20 Kbps";
        if (baudRates.BaudRate10) return "10 Kbps";
        if (baudRates.AutoBaudRate) return AutoBaudRate;
        return "250 Kbps";
    }

    private const string AutoBaudRate = "auto-baudRate";

    private const string TimeZonePattern = "(?:Z|[+-](?:0[0-9]|1[0-3]):[0-5][0-9]|[+-]14:00)";

    // ASCII digits only ([0-9], never \d), whole input only (\z, never $).
    private static readonly Regex EdsDateRegex = new(
        "^([0-9]{2})-([0-9]{2})-([0-9]{4})\\z", RegexOptions.CultureInvariant);

    private static readonly Regex XsdDatePrefixRegex = new(
        "^([0-9]{4})-([0-9]{2})-([0-9]{2})", RegexOptions.CultureInvariant);

    private static readonly Regex XsdDateRegex = new(
        "^-?((?:[1-9][0-9]{4,}|[0-9]{4}))-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])" + TimeZonePattern + "?\\z",
        RegexOptions.CultureInvariant);

    private static readonly Regex XsdTimeRegex = new(
        "^(?:(?:[01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](?:\\.[0-9]+)?|24:00:00(?:\\.0+)?)" + TimeZonePattern + "?\\z",
        RegexOptions.CultureInvariant);

    private static readonly Regex EdsTimeRegex = new(
        "^(0?[1-9]|1[0-2]):([0-5][0-9])(AM|PM)\\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Converts an EDS date ("MM-DD-YYYY") to an <c>xsd:date</c> ("YYYY-MM-DD").
    /// </summary>
    /// <remarks>
    /// The date must exist in the calendar (month 1..12, day within the month, leap years).
    /// A value that is already a valid <c>xsd:date</c> (including a time zone or a year with more
    /// than four digits) is returned unchanged. Anything else yields <see langword="false"/> and
    /// no attribute is written: an omitted optional attribute is schema-valid, a mistyped one is not.
    /// </remarks>
    internal static bool TryConvertEdsDateToXsd(string? edsDate, out string xsdDate)
    {
        xsdDate = string.Empty;
        if (edsDate == null)
            return false;

        var text = edsDate.Trim();
        var eds = EdsDateRegex.Match(text);
        if (eds.Success)
        {
            var month = int.Parse(eds.Groups[1].Value, CultureInfo.InvariantCulture);
            var day = int.Parse(eds.Groups[2].Value, CultureInfo.InvariantCulture);
            var year = int.Parse(eds.Groups[3].Value, CultureInfo.InvariantCulture);
            if (!IsCalendarDate(year, month, day))
                return false;

            xsdDate = string.Format(CultureInfo.InvariantCulture, "{0:D4}-{1:D2}-{2:D2}", year, month, day);
            return true;
        }

        if (!IsXsdDate(text))
            return false;

        xsdDate = text;
        return true;
    }

    /// <summary>
    /// Converts an <c>xsd:date</c> read from a file to the EDS date text ("MM-DD-YYYY") of the model.
    /// </summary>
    /// <remarks>
    /// Only text that starts with four-digit year, month and day separated by hyphens converts,
    /// whether or not the date exists. Any other text is returned trimmed and otherwise unchanged.
    /// A time zone is dropped here; the reader keeps the original spelling separately.
    /// </remarks>
    internal static string ConvertXsdDateToEds(string? xsdDate)
    {
        var text = xsdDate?.Trim() ?? string.Empty;
        var match = XsdDatePrefixRegex.Match(text);
        return match.Success
            ? string.Concat(match.Groups[2].Value, "-", match.Groups[3].Value, "-", match.Groups[1].Value)
            : text;
    }

    /// <summary>
    /// Chooses the <c>xsd:date</c> text for a file date: the preserved spelling while its calendar
    /// date still equals <paramref name="modelDate"/>, otherwise the converted property.
    /// </summary>
    internal static bool TryFormatFileDate(string? modelDate, string? preservedSpelling, out string xsdDate)
    {
        if (preservedSpelling != null &&
            string.Equals(ConvertXsdDateToEds(preservedSpelling), modelDate?.Trim(), StringComparison.Ordinal) &&
            IsXsdDate(preservedSpelling))
        {
            xsdDate = preservedSpelling;
            return true;
        }

        return TryConvertEdsDateToXsd(modelDate, out xsdDate);
    }

    /// <summary>
    /// Converts a file time to <c>xsd:time</c>: a valid <c>xsd:time</c> stays as it is, the EDS form
    /// <c>hh:mmAM/PM</c> becomes <c>HH:mm:ss</c>, and anything else yields <see langword="false"/>.
    /// </summary>
    internal static bool TryConvertFileTimeToXsd(string? time, out string xsdTime)
    {
        xsdTime = string.Empty;
        if (time == null)
            return false;

        var text = time.Trim();
        if (IsXsdTime(text))
        {
            xsdTime = text;
            return true;
        }

        // EDS: h:mmAM / hh:mmPM, hour 1..12, 12 AM is midnight and 12 PM is noon.
        var eds = EdsTimeRegex.Match(text);
        if (!eds.Success)
            return false;

        var hour12 = int.Parse(eds.Groups[1].Value, CultureInfo.InvariantCulture);
        var pm = string.Equals(eds.Groups[3].Value, "PM", StringComparison.OrdinalIgnoreCase);
        xsdTime = string.Format(
            CultureInfo.InvariantCulture,
            "{0:D2}:{1}:00",
            (hour12 % 12) + (pm ? 12 : 0),
            eds.Groups[2].Value);
        return true;
    }

    /// <summary>Tests whether the text is a valid <c>xsd:date</c> (optional time zone).</summary>
    internal static bool IsXsdDate(string text)
    {
        var match = XsdDateRegex.Match(text);
        if (!match.Success)
            return false;

        var year = match.Groups[1].Value;
        var month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var day = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);

        // Year 0000 does not exist. Divisibility by 4, 100 and 400 depends only on the last four
        // digits, so a year with more digits needs no integer conversion.
        var lastFour = int.Parse(year[^4..], CultureInfo.InvariantCulture);
        if (lastFour == 0 && year.Length == 4)
            return false;

        return day <= DaysInMonth(lastFour, month);
    }

    /// <summary>Tests whether the text is a valid <c>xsd:time</c> (optional fraction and time zone).</summary>
    internal static bool IsXsdTime(string text) => XsdTimeRegex.IsMatch(text);

    private static bool IsCalendarDate(int year, int month, int day) =>
        year >= 1 && month >= 1 && month <= 12 && day >= 1 && day <= DaysInMonth(year, month);

    private static int DaysInMonth(int year, int month)
    {
        if (month == 2)
            return year % 400 == 0 || (year % 4 == 0 && year % 100 != 0) ? 29 : 28;

        return month == 4 || month == 6 || month == 9 || month == 11 ? 30 : 31;
    }
}
