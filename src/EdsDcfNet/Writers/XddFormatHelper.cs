namespace EdsDcfNet.Writers;

using System.Collections.Generic;
using System.Globalization;
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

    /// <summary>Yields the baud rates that are flagged as supported.</summary>
    internal static IEnumerable<ushort> GetSupportedBaudRates(BaudRates baudRates)
    {
        if (baudRates.BaudRate10) yield return 10;
        if (baudRates.BaudRate20) yield return 20;
        if (baudRates.BaudRate50) yield return 50;
        if (baudRates.BaudRate125) yield return 125;
        if (baudRates.BaudRate250) yield return 250;
        if (baudRates.BaudRate500) yield return 500;
        if (baudRates.BaudRate800) yield return 800;
        if (baudRates.BaudRate1000) yield return 1000;
    }

    /// <summary>
    /// Returns the preferred default baud rate string from the supported set,
    /// falling back to "250 Kbps" when no rates are flagged.
    /// </summary>
    internal static string GetDefaultBaudRateString(BaudRates baudRates)
    {
        if (baudRates.BaudRate250) return "250 Kbps";
        if (baudRates.BaudRate500) return "500 Kbps";
        if (baudRates.BaudRate125) return "125 Kbps";
        if (baudRates.BaudRate1000) return "1000 Kbps";
        if (baudRates.BaudRate800) return "800 Kbps";
        if (baudRates.BaudRate50) return "50 Kbps";
        if (baudRates.BaudRate20) return "20 Kbps";
        if (baudRates.BaudRate10) return "10 Kbps";
        return "250 Kbps";
    }

    /// <summary>
    /// Converts an EDS date string ("MM-DD-YYYY") to an XSD date string ("YYYY-MM-DD").
    /// Returns the input unchanged when the format is not recognised.
    /// </summary>
    internal static string ConvertEdsDateToXsd(string edsDate)
    {
        if (string.IsNullOrEmpty(edsDate))
            return string.Empty;

        // EDS date: "MM-DD-YYYY" → XSD date: "YYYY-MM-DD"
        if (edsDate.Length >= 10 &&
            edsDate[2] == '-' && edsDate[5] == '-')
        {
            var month = edsDate.Substring(0, 2);
            var day = edsDate.Substring(3, 2);
            var year = edsDate.Substring(6, 4);
            return string.Format(CultureInfo.InvariantCulture, "{0}-{1}-{2}", year, month, day);
        }

        return edsDate;
    }
}
