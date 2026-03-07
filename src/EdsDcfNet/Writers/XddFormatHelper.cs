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
    internal static string FormatDataType(ushort dataType) =>
        dataType.ToString("X4", CultureInfo.InvariantCulture);

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
