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
    /// Parses an integer value from string (supports decimal, hexadecimal, and octal).
    /// </summary>
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
        catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is ArgumentOutOfRangeException)
        {
            throw new EdsParseException(BuildInvalidNumericLiteralMessage("integer", value, ex), ex);
        }
    }

    /// <summary>
    /// Parses a boolean value from string.
    /// </summary>
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
        catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is ArgumentOutOfRangeException)
        {
            throw new EdsParseException(BuildInvalidNumericLiteralMessage("byte", value, ex), ex);
        }
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
        catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is ArgumentOutOfRangeException)
        {
            throw new EdsParseException(BuildInvalidNumericLiteralMessage("UInt16", value, ex), ex);
        }
    }

    /// <summary>
    /// Parses an AccessType from string.
    /// </summary>
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
            return (value[2..], NumericBase.Hexadecimal);

        if (value.Length > 1 && value[0] == '0' && char.IsDigit(value[1]))
            return (value, NumericBase.Octal);

        return (value, NumericBase.Decimal);
    }

    /// <summary>
    /// Evaluates a $NODEID formula with the given node ID.
    /// Supports formulas like "$NODEID", "$NODEID+0x200", "$NODEID+512", etc.
    /// </summary>
    private static string BuildInvalidNumericLiteralMessage(string typeName, string value, Exception exception)
    {
        var literalKind = DescribeNumericLiteral(value);
        if (exception is OverflowException)
        {
            return $"Invalid {typeName} value: '{value}' ({literalKind}). The value is outside the valid {typeName} range.";
        }

        return $"Invalid {typeName} value: '{value}' ({literalKind}).";
    }

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
                    return "octal literal contains digits outside 0-7";
            }

            return "octal literal";
        }

        foreach (var c in value)
        {
            if (!char.IsDigit(c))
                return "decimal literal contains non-digit characters";
        }

        return "decimal literal";
    }

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
