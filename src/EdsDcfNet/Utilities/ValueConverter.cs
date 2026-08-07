namespace EdsDcfNet.Utilities;

using System.Globalization;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;

/// <summary>
/// Utility class for converting string values from EDS/DCF files to typed values.
/// </summary>
public static class ValueConverter
{
    private enum NumericBase
    {
        Decimal = 10,
        Octal = 8,
        Hexadecimal = 16
    }

    /// <summary>
    /// Parses an integer value from string (supports decimal, hexadecimal <c>0x</c>, and octal).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Base detection (CiA DS 306 / C-style, see architecture docs §8.3 and issue #411):
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>0x</c> / <c>0X</c> prefix → hexadecimal</description></item>
    /// <item><description>leading <c>0</c> followed by a digit → octal (<c>010</c> is 8, not 10)</description></item>
    /// <item><description>otherwise → decimal</description></item>
    /// </list>
    /// <para>
    /// Zero-padded decimal literals such as <c>08</c>/<c>09</c> are invalid octal and throw
    /// <see cref="EdsParseException"/>. Prefer unpadded decimal or an explicit <c>0x</c> prefix
    /// when authoring EDS/DCF values.
    /// </para>
    /// </remarks>
    /// <param name="value">String value to parse</param>
    /// <param name="nodeId">Optional node ID for evaluating $NODEID formulas</param>
    public static uint ParseInteger(string value, byte? nodeId = null)
    {
        value = value.Trim();

        if (string.IsNullOrEmpty(value))
            return 0;

        // Handle $NODEID formula
        if (value.StartsWith("$NODEID", StringComparison.OrdinalIgnoreCase))
        {
            if (!nodeId.HasValue)
            {
                throw new NotSupportedException(
                    $"Cannot evaluate $NODEID formula '{value}' without a node ID context. " +
                    "This typically occurs when parsing EDS files where the node ID is not yet known. " +
                    "For DCF files with configured node IDs, ensure the node ID is provided during parsing.");
            }

            return EvaluateNodeIdFormula(value, nodeId.Value);
        }

        try
        {
            return ParseUnsignedNumber(
                value,
                decimalParser: static v => uint.Parse(v, CultureInfo.InvariantCulture),
                parser: static (v, numberBase) => Convert.ToUInt32(v, (int)numberBase));
        }
        catch (Exception ex) when (ex is FormatException || ex is OverflowException)
        {
            throw new EdsParseException(BuildInvalidNumericLiteralMessage("uint", value, ex), ex);
        }
    }

    /// <summary>
    /// Parses a boolean value from string.
    /// </summary>
    /// <remarks>
    /// This parser is intentionally lenient to tolerate real-world EDS/DCF files:
    /// only <c>"1"</c>, <c>"true"</c>, and <c>"yes"</c> (case-insensitive) are treated as
    /// <see langword="true"/>. Any other value — including typos, <c>"0"</c>, <c>"false"</c>,
    /// <c>"no"</c>, and empty strings — silently maps to <see langword="false"/> without
    /// raising an error. Unlike the numeric parsers in this class, malformed input is not
    /// reported via <see cref="EdsParseException"/>.
    /// </remarks>
    /// <param name="value">String value to parse.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> is a recognized true token;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public static bool ParseBoolean(string value)
    {
        value = value.Trim();

        if (string.IsNullOrEmpty(value))
            return false;

        return value == "1" ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses a present/absent flag as used by CPJ <c>NodeNPresent</c> fields and similar
    /// hex-flag or boolean tokens.
    /// </summary>
    /// <remarks>
    /// <para>Recognized true tokens (case-insensitive where alphabetic):</para>
    /// <list type="bullet">
    /// <item><description><c>0x01</c>, <c>0x1</c> (writer emits <c>0x01</c>)</description></item>
    /// <item><description><c>1</c>, <c>true</c>, <c>yes</c> (same as <see cref="ParseBoolean"/>)</description></item>
    /// </list>
    /// <para>Recognized false tokens include <c>0x00</c>, <c>0x0</c>, <c>0</c>, <c>false</c>,
    /// <c>no</c>, and any other unrecognized value (lenient absent).</para>
    /// </remarks>
    /// <param name="value">String value to parse.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> is a recognized present token;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public static bool ParsePresentFlag(string value)
    {
        value = value.Trim();

        if (string.IsNullOrEmpty(value))
            return false;

        if (value.Equals("0x01", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("0x1", StringComparison.OrdinalIgnoreCase))
            return true;

        if (value.Equals("0x00", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("0x0", StringComparison.OrdinalIgnoreCase))
            return false;

        return ParseBoolean(value);
    }

    /// <summary>
    /// Parses a byte value from string.
    /// </summary>
    public static byte ParseByte(string value)
    {
        value = value.Trim();

        if (string.IsNullOrEmpty(value))
            return 0;

        try
        {
            return ParseUnsignedNumber(
                value,
                decimalParser: static v => byte.Parse(v, CultureInfo.InvariantCulture),
                parser: static (v, numberBase) => Convert.ToByte(v, (int)numberBase));
        }
        catch (Exception ex) when (ex is FormatException || ex is OverflowException)
        {
            throw new EdsParseException(BuildInvalidNumericLiteralMessage("byte", value, ex), ex);
        }
    }

    /// <summary>
    /// Parses an <c>Unsigned8</c> that CiA 306 defines as an integer, while tolerating
    /// major/minor forms some tooling emits (dot or comma separator).
    /// </summary>
    /// <remarks>
    /// When <paramref name="value"/> matches <c>major.minor</c> or <c>major,minor</c>
    /// with both sides non-empty unsigned decimal digit runs and a single separator,
    /// only the major component is parsed as decimal (same policy as XDD <c>fileVersion</c>),
    /// including leading-zero majors such as <c>07.3</c> → 7 or <c>012.5</c> → 12.
    /// Hex (<c>0x…</c>) literals and pure octal literals (no separator) are never
    /// treated as major/minor forms. All other inputs are delegated to <see cref="ParseByte"/>.
    /// </remarks>
    /// <param name="value">String value to parse.</param>
    /// <returns>The parsed byte (major component when a major/minor form is recognized).</returns>
    /// <exception cref="EdsParseException">Thrown when the value cannot be parsed as a byte.</exception>
    public static byte ParseByteAllowingMajorMinor(string value)
    {
        value = value.Trim();

        if (string.IsNullOrEmpty(value))
            return 0;

        if (TrySplitMajorMinorDecimal(value, out var major))
        {
            // Major/minor forms are tooling-style decimal versions (e.g. "012.5" → 12),
            // not CiA octal literals. ParseByte would treat a leading-zero major as octal.
            try
            {
                return byte.Parse(major, NumberStyles.None, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is FormatException || ex is OverflowException)
            {
                throw new EdsParseException(BuildInvalidNumericLiteralMessage("byte", value, ex), ex);
            }
        }

        return ParseByte(value);
    }

    /// <summary>
    /// Tries to split a major/minor decimal version literal (<c>1.0</c> / <c>1,0</c>).
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when <paramref name="value"/> is exactly one separator
    /// (<c>.</c> or <c>,</c>) between two non-empty unsigned decimal digit runs.
    /// </returns>
    internal static bool TrySplitMajorMinorDecimal(string value, out string major)
    {
        major = string.Empty;

        // Hex shapes are numeric literals, not dotted version strings.
        // Pure octals (e.g. "010") have no separator and fall through below;
        // do not reject leading-zero majors like "07.3" before looking for '.' / ','.
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return false;

        var separatorIndex = -1;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c != '.' && c != ',')
                continue;

            if (separatorIndex >= 0)
                return false;

            separatorIndex = i;
        }

        if (separatorIndex <= 0 || separatorIndex >= value.Length - 1)
            return false;

        var majorPart = value[..separatorIndex].Trim();
        var minorPart = value[(separatorIndex + 1)..].Trim();
        if (majorPart.Length == 0 || minorPart.Length == 0)
            return false;

        if (!IsAllAsciiDigits(majorPart) || !IsAllAsciiDigits(minorPart))
            return false;

        major = majorPart;
        return true;
    }

    private static bool IsAllAsciiDigits(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c < '0' || c > '9')
                return false;
        }

        return true;
    }

    /// <summary>
    /// Parses a ushort value from string.
    /// </summary>
    public static ushort ParseUInt16(string value)
    {
        value = value.Trim();

        if (string.IsNullOrEmpty(value))
            return 0;

        try
        {
            return ParseUnsignedNumber(
                value,
                decimalParser: static v => ushort.Parse(v, CultureInfo.InvariantCulture),
                parser: static (v, numberBase) => Convert.ToUInt16(v, (int)numberBase));
        }
        catch (Exception ex) when (ex is FormatException || ex is OverflowException)
        {
            throw new EdsParseException(BuildInvalidNumericLiteralMessage("UInt16", value, ex), ex);
        }
    }

    /// <summary>
    /// Parses an AccessType from string.
    /// </summary>
    /// <remarks>
    /// This parser is intentionally lenient to tolerate real-world EDS/DCF files:
    /// recognized tokens are <c>"ro"</c>, <c>"wo"</c>, <c>"rw"</c>, <c>"rwr"</c>,
    /// <c>"rww"</c>, and <c>"const"</c> (case-insensitive, surrounding whitespace ignored).
    /// Any unrecognized value — including typos and <see langword="null"/> — silently maps to
    /// <see cref="AccessType.ReadOnly"/> without raising an error. Unlike the numeric parsers
    /// in this class, malformed input is not reported via <see cref="EdsParseException"/>.
    /// Note that this can change round-trip output: an unknown access type in the source file
    /// is written back as <c>"ro"</c>.
    /// </remarks>
    /// <param name="value">String value to parse.</param>
    /// <returns>
    /// The parsed <see cref="AccessType"/>, or <see cref="AccessType.ReadOnly"/> if
    /// <paramref name="value"/> is not a recognized access type token.
    /// </returns>
    public static AccessType ParseAccessType(string value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "ro" => AccessType.ReadOnly,
            "wo" => AccessType.WriteOnly,
            "rw" => AccessType.ReadWrite,
            "rwr" => AccessType.ReadWriteInput,
            "rww" => AccessType.ReadWriteOutput,
            "const" => AccessType.Constant,
            _ => AccessType.ReadOnly
        };
    }

    /// <summary>
    /// Converts an AccessType to string representation.
    /// </summary>
    public static string AccessTypeToString(AccessType accessType)
    {
        return accessType switch
        {
            AccessType.ReadOnly => "ro",
            AccessType.WriteOnly => "wo",
            AccessType.ReadWrite => "rw",
            AccessType.ReadWriteInput => "rwr",
            AccessType.ReadWriteOutput => "rww",
            AccessType.Constant => "const",
            _ => "ro"
        };
    }

    /// <summary>
    /// Formats an integer value for EDS/DCF output (uses hexadecimal with 0x prefix).
    /// </summary>
    public static string FormatInteger(uint value, bool useHex = true)
    {
        if (useHex)
            return $"0x{value:X}";
        return value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a boolean value for EDS/DCF output.
    /// </summary>
    public static string FormatBoolean(bool value)
    {
        return value ? "1" : "0";
    }

    /// <summary>
    /// Shared helper that detects the numeric base of <paramref name="value"/> and
    /// delegates to the appropriate parser (decimal or non-decimal).
    /// </summary>
    private static T ParseUnsignedNumber<T>(
        string value,
        Func<string, T> decimalParser,
        Func<string, NumericBase, T> parser)
    {
        var (normalizedValue, numberBase) = GetNumericFormat(value);
        if (numberBase == NumericBase.Decimal)
            return decimalParser(normalizedValue);

        return parser(normalizedValue, numberBase);
    }

    private static (string Value, NumericBase NumberBase) GetNumericFormat(string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            var hexDigits = value[2..];
            if (hexDigits.Length == 0)
                throw new FormatException($"The value '{value}' has no digits after the hex prefix.");

            return (hexDigits, NumericBase.Hexadecimal);
        }

        // Leading 0 + digit → octal (CiA DS 306 / C-style). Not decimal padding.
        // "010" → 8; "08"/"09" fail as invalid octal. See docs §8.3 / issue #411.
        if (value.Length > 1 && value[0] == '0' && char.IsDigit(value[1]))
            return (value, NumericBase.Octal);

        return (value, NumericBase.Decimal);
    }

    /// <summary>
    /// Builds a detailed error message for an invalid numeric literal, including its interpreted kind
    /// (decimal/hex/octal) and whether the value is outside the valid range for the requested type.
    /// </summary>
    /// <param name="typeName">The logical type name being parsed (e.g. "uint", "byte").</param>
    /// <param name="value">The original string literal value that failed to parse.</param>
    /// <param name="exception">The exception that was thrown while parsing the value.</param>
    /// <returns>A human-readable error message describing why the numeric literal is invalid.</returns>
    private static string BuildInvalidNumericLiteralMessage(string typeName, string value, Exception exception)
    {
        var literalKind = DescribeNumericLiteral(value);
        if (exception is OverflowException)
        {
            return $"Invalid {typeName} value: '{value}' ({literalKind}). The value is outside the representable range for this numeric type.";
        }

        return $"Invalid {typeName} value: '{value}' ({literalKind}).";
    }

    /// <summary>
    /// Describes the shape of a numeric literal (hex/octal/decimal) for error diagnostics.
    /// </summary>
    private static string DescribeNumericLiteral(string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            var digits = value[2..];
            if (digits.Length == 0)
                return "hexadecimal literal has no digits after the 0x prefix";

            foreach (var c in digits)
            {
                var isHexDigit = (c >= '0' && c <= '9') ||
                                 (c >= 'a' && c <= 'f') ||
                                 (c >= 'A' && c <= 'F');
                if (!isHexDigit)
                    return "hexadecimal literal contains non-hex characters";
            }

            return "hexadecimal literal";
        }

        if (value.Length > 1 && value[0] == '0' && char.IsDigit(value[1]))
        {
            foreach (var c in value)
            {
                if (c < '0' || c > '7')
                    return "octal literal contains characters outside 0-7";
            }

            return "octal literal";
        }

        var startIndex = 0;
        if (value[0] == '+' || value[0] == '-')
        {
            if (value.Length == 1)
                return "decimal literal contains non-digit characters";

            startIndex = 1;
        }

        for (var i = startIndex; i < value.Length; i++)
        {
            if (!char.IsDigit(value[i]))
                return "decimal literal contains non-digit characters";
        }

        return "decimal literal";
    }

    /// <summary>
    /// Evaluates a $NODEID formula with the given node ID.
    /// Supports formulas like "$NODEID", "$NODEID+0x200", "$NODEID+512", etc.
    /// </summary>
    private static uint EvaluateNodeIdFormula(string formula, byte nodeId)
    {
        formula = formula.Trim();

        const string token = "$NODEID";
        var suffix = formula[token.Length..].Trim();

        if (suffix.Length == 0)
            return nodeId;

        if (suffix[0] == '+' || suffix[0] == '-')
        {
            var rightSide = suffix[1..].Trim();
            if (string.IsNullOrEmpty(rightSide) || rightSide.Contains('+') || rightSide.Contains('-'))
            {
                throw new EdsParseException(
                    $"Unsupported $NODEID formula '{formula}'. Expected '$NODEID', '$NODEID+<number>' or '$NODEID-<number>'.");
            }

            var right = ParseInteger(rightSide);

            if (suffix[0] == '+')
            {
                try
                {
                    return checked(nodeId + right);
                }
                catch (OverflowException ex)
                {
                    throw new EdsParseException($"$NODEID formula '{formula}' overflows uint range.", ex);
                }
            }

            try
            {
                return checked((uint)nodeId - right);
            }
            catch (OverflowException ex)
            {
                throw new EdsParseException($"$NODEID formula '{formula}' underflows uint range.", ex);
            }
        }

        throw new EdsParseException(
            $"Unsupported $NODEID formula '{formula}'. Expected '$NODEID', '$NODEID+<number>' or '$NODEID-<number>'.");
    }
}
