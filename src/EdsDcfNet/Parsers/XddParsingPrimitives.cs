namespace EdsDcfNet.Parsers;

using System.Globalization;
using System.Xml.Linq;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;

internal static class XddParsingPrimitives
{
    internal static string GetXsiType(XElement element)
    {
        foreach (var attr in element.Attributes())
        {
            if (attr.Name.LocalName == "type")
                return attr.Value;
        }

        return string.Empty;
    }

    internal static string GetChildText(XElement parent, string localName)
    {
        var child = parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);
        if (child == null)
            return string.Empty;

        return child.Value.Trim();
    }

    /// <summary>
    /// Parses a hex index string (e.g. "1000" or "0x1000") to ushort.
    /// </summary>
    internal static ushort ParseHexIndex(string value)
    {
        var trimmed = value.Trim();
        var hex = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? trimmed[2..] : trimmed;

        if (ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
            return result;

        throw new EdsParseException(
            string.Format(CultureInfo.InvariantCulture,
                "Malformed CANopen object index '{0}'. Expected a 4-digit hex value (e.g. '1000' or '0x1000').",
                value));
    }

    internal static byte ParseHexSubIndex(string value)
    {
        var trimmed = value.Trim();
        var hex = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? trimmed[2..] : trimmed;

        if (byte.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
            return result;

        throw new EdsParseException(
            string.Format(CultureInfo.InvariantCulture,
                "Malformed CANopen sub-object subIndex '{0}'. Expected a 2-digit hex value (e.g. '00' or '0x00').",
                value));
    }

    internal static ushort ParseHexDataType(string value)
    {
        value = value.Trim();

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            value = value[2..];

        if (ushort.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
            return result;

        return 0;
    }

    internal static uint ParseHexId(string value)
    {
        value = value.Trim();

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            value = value[2..];

        if (uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
            return result;

        return 0;
    }

    internal static AccessType ParseXddAccessType(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "const" => AccessType.Constant,
            "ro" => AccessType.ReadOnly,
            "wo" => AccessType.WriteOnly,
            "rw" => AccessType.ReadWrite,
            _ => AccessType.ReadOnly
        };
    }

    internal static bool ParseXddPdoMapping(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        return !value!.Equals("no", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool ParseXmlBool(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.Ordinal);
    }

    internal static ushort ParseBaudRateString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        value = value.Trim();

        if (value.Equals("10 Kbps", StringComparison.OrdinalIgnoreCase)) return 10;
        if (value.Equals("20 Kbps", StringComparison.OrdinalIgnoreCase)) return 20;
        if (value.Equals("50 Kbps", StringComparison.OrdinalIgnoreCase)) return 50;
        if (value.Equals("125 Kbps", StringComparison.OrdinalIgnoreCase)) return 125;
        if (value.Equals("250 Kbps", StringComparison.OrdinalIgnoreCase)) return 250;
        if (value.Equals("500 Kbps", StringComparison.OrdinalIgnoreCase)) return 500;
        if (value.Equals("800 Kbps", StringComparison.OrdinalIgnoreCase)) return 800;
        if (value.Equals("1000 Kbps", StringComparison.OrdinalIgnoreCase)) return 1000;

        return 0;
    }

    internal static string ConvertXsdDateToEds(string xsdDate)
    {
        if (string.IsNullOrEmpty(xsdDate))
            return string.Empty;

        // XSD date: "YYYY-MM-DD" → EDS: "MM-DD-YYYY"
        if (xsdDate.Length >= 10 &&
            xsdDate[4] == '-' && xsdDate[7] == '-')
        {
            var year = xsdDate[..4];
            var month = xsdDate[5..7];
            var day = xsdDate[8..10];
            return string.Format(CultureInfo.InvariantCulture, "{0}-{1}-{2}", month, day, year);
        }

        return xsdDate;
    }
}
