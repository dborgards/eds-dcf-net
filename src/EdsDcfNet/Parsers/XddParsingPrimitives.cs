namespace EdsDcfNet.Parsers;

using System.Globalization;
using System.Xml.Linq;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;

internal static class XddParsingPrimitives
{
    /// <summary>
    /// Styles for XSD unsigned integer-derived attributes after whitespace collapse.
    /// Optional leading sign is schema-valid (<c>+n</c>, <c>-0</c>); out-of-range
    /// negatives still fail <c>TryParse</c> for the target unsigned type.
    /// </summary>
    internal const NumberStyles UnsignedXsdIntegerStyles = NumberStyles.AllowLeadingSign;

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
        var trimmed = value.Trim();
        var hex = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? trimmed[2..] : trimmed;

        if (ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
            return result;

        throw new EdsParseException(
            string.Format(CultureInfo.InvariantCulture,
                "Malformed CANopen dataType '{0}'. Expected a hex value (e.g. '0007' or '0x0007').",
                value));
    }

    internal static uint ParseHexId(string value)
    {
        var trimmed = value.Trim();
        var hex = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? trimmed[2..] : trimmed;

        if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
            return result;

        throw new EdsParseException(
            string.Format(CultureInfo.InvariantCulture,
                "Malformed CANopen identifier '{0}'. Expected a hex value (e.g. '0x00000100').",
                value));
    }

    /// <summary>
    /// Parses a CiA 311 <c>accessType</c> / channel <c>accessType</c> token.
    /// </summary>
    /// <remarks>
    /// Recognized tokens (case-insensitive): <c>const</c>, <c>ro</c>, <c>wo</c>,
    /// <c>rw</c>, <c>rwr</c>, <c>rww</c>. Empty/whitespace maps to
    /// <see cref="AccessType.ReadOnly"/> (absent field). Unknown non-empty tokens
    /// map to <see cref="AccessType.ReadOnly"/> when lenient, or throw
    /// <see cref="EdsParseException"/> when <see cref="StrictParsingScope"/> is enabled.
    /// <c>rwr</c>/<c>rww</c> match EDS <see cref="Utilities.ValueConverter.ParseAccessType"/>;
    /// the XDD writer still emits <c>rw</c> for those model values (no CiA 311 equivalent).
    /// </remarks>
    internal static AccessType ParseXddAccessType(string value)
    {
        var token = value.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(token))
            return AccessType.ReadOnly;

        return token switch
        {
            "const" => AccessType.Constant,
            "ro" => AccessType.ReadOnly,
            "wo" => AccessType.WriteOnly,
            "rw" => AccessType.ReadWrite,
            "rwr" => AccessType.ReadWriteInput,
            "rww" => AccessType.ReadWriteOutput,
            _ when StrictParsingScope.IsEnabled => throw new EdsParseException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Unknown access type token '{0}'. Expected one of: ro, wo, rw, rwr, rww, const.",
                    value)),
            _ => AccessType.ReadOnly
        };
    }

    internal static PdoMappingMode ParseXddPdoMapping(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return PdoMappingMode.No;

        var token = value!.Trim();
        if (token.Equals("no", StringComparison.OrdinalIgnoreCase))
            return PdoMappingMode.No;
        if (token.Equals("default", StringComparison.OrdinalIgnoreCase))
            return PdoMappingMode.Default;
        if (token.Equals("optional", StringComparison.OrdinalIgnoreCase))
            return PdoMappingMode.Optional;
        if (token.Equals("TPDO", StringComparison.OrdinalIgnoreCase))
            return PdoMappingMode.Tpdo;
        if (token.Equals("RPDO", StringComparison.OrdinalIgnoreCase))
            return PdoMappingMode.Rpdo;

        throw new EdsParseException(
            string.Format(CultureInfo.InvariantCulture,
                "Invalid PDOmapping '{0}'. Expected one of: no, default, optional, TPDO, RPDO.",
                value));
    }

    /// <summary>
    /// Parses an XML boolean attribute (<c>true</c>/<c>false</c>/<c>1</c>/<c>0</c>).
    /// </summary>
    /// <remarks>
    /// Empty/whitespace maps to <see langword="false"/>. Unknown non-empty tokens
    /// map to <see langword="false"/> when lenient, or throw
    /// <see cref="EdsParseException"/> when <see cref="StrictParsingScope"/> is enabled.
    /// </remarks>
    internal static bool ParseXmlBool(string value)
    {
        value = value.Trim();

        if (string.IsNullOrEmpty(value))
            return false;

        if (value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("1", StringComparison.Ordinal))
            return true;

        if (value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("0", StringComparison.Ordinal))
            return false;

        if (StrictParsingScope.IsEnabled)
        {
            throw new EdsParseException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Unknown XML boolean token '{0}'. Expected one of: true, false, 1, 0.",
                    value));
        }

        return false;
    }

    /// <summary>
    /// Parses a CiA 311 baud-rate vocabulary string such as <c>250 Kbps</c>.
    /// </summary>
    /// <remarks>
    /// Supported values (case-insensitive):
    /// <c>10 Kbps</c>, <c>20 Kbps</c>, <c>50 Kbps</c>, <c>125 Kbps</c>,
    /// <c>250 Kbps</c>, <c>500 Kbps</c>, <c>800 Kbps</c>, <c>1000 Kbps</c>.
    /// Empty input returns <c>0</c>. Unknown non-empty values return <c>0</c> when
    /// lenient, or throw <see cref="EdsParseException"/> when
    /// <see cref="StrictParsingScope"/> is enabled.
    /// </remarks>
    internal static ushort ParseBaudRateString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        value = value.Trim();
        if (value.Length == 0)
            return 0;

        if (value.Equals("10 Kbps", StringComparison.OrdinalIgnoreCase)) return 10;
        if (value.Equals("20 Kbps", StringComparison.OrdinalIgnoreCase)) return 20;
        if (value.Equals("50 Kbps", StringComparison.OrdinalIgnoreCase)) return 50;
        if (value.Equals("125 Kbps", StringComparison.OrdinalIgnoreCase)) return 125;
        if (value.Equals("250 Kbps", StringComparison.OrdinalIgnoreCase)) return 250;
        if (value.Equals("500 Kbps", StringComparison.OrdinalIgnoreCase)) return 500;
        if (value.Equals("800 Kbps", StringComparison.OrdinalIgnoreCase)) return 800;
        if (value.Equals("1000 Kbps", StringComparison.OrdinalIgnoreCase)) return 1000;

        if (StrictParsingScope.IsEnabled)
        {
            throw new EdsParseException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Unknown baud-rate string '{0}'. Expected one of: 10 Kbps, 20 Kbps, 50 Kbps, 125 Kbps, 250 Kbps, 500 Kbps, 800 Kbps, 1000 Kbps.",
                    value));
        }

        return 0;
    }

    /// <summary>
    /// When <paramref name="raw"/> is non-empty and <paramref name="parsed"/> is
    /// <see langword="false"/>, either ignore (lenient) or throw
    /// <see cref="EdsParseException"/> (strict) with attribute context.
    /// </summary>
    internal static void RejectFailedNumericAttribute(string? raw, bool parsed, string attributeName)
    {
        RejectFailedIntegerAttribute(raw, parsed, attributeName, signed: false);
    }

    /// <summary>
    /// Like <see cref="RejectFailedNumericAttribute"/>, but the diagnostic describes a
    /// signed integer (for attributes parsed with <see cref="long.TryParse"/>).
    /// </summary>
    internal static void RejectFailedSignedNumericAttribute(string? raw, bool parsed, string attributeName)
    {
        RejectFailedIntegerAttribute(raw, parsed, attributeName, signed: true);
    }

    private static void RejectFailedIntegerAttribute(
        string? raw,
        bool parsed,
        string attributeName,
        bool signed)
    {
        if (string.IsNullOrEmpty(raw) || parsed)
            return;

        if (!StrictParsingScope.IsEnabled)
            return;

        throw new EdsParseException(
            string.Format(
                CultureInfo.InvariantCulture,
                signed
                    ? "Invalid {0} '{1}'. Value cannot be parsed as a signed integer."
                    : "Invalid {0} '{1}'. Value cannot be parsed as an unsigned integer.",
                attributeName,
                raw));
    }

    /// <summary>
    /// Returns the trimmed attribute value, or <see langword="null"/> when absent.
    /// Collapses XML Schema surrounding whitespace for integer-derived types.
    /// </summary>
    internal static string? GetTrimmedAttributeValue(XElement element, string localName)
    {
        var attr = element.Attribute(localName);
        if (attr == null)
            return null;

        return attr.Value.Trim();
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
