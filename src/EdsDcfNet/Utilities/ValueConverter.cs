namespace EdsDcfNet.Utilities;

using System.Globalization;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;

#pragma warning disable CA1845, CA1865, CA1866 // span-based and char overloads not available in netstandard2.0
#pragma warning disable CA2249 // string.Contains(char) not available in netstandard2.0; IndexOf(char) >= 0 is the correct alternative

/// <summary>
/// Utility class for converting string values from EDS/DCF files to typed values.
/// </summary>
public static class ValueConverter
{
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
            // Hexadecimal (0x prefix)
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToUInt32(value.Substring(2), 16);
            }

            // Octal (leading 0, but not 0x)
            if (value.Length > 1 && value[0] == '0' && char.IsDigit(value[1]))
            {
                return Convert.ToUInt32(value, 8);
            }

            // Decimal
            return uint.Parse(value, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException || ex is OverflowException)
        {
            throw new EdsParseException($"Invalid integer value: '{value}'", ex);
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
            // Hexadecimal
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToByte(value.Substring(2), 16);
            }

            // Octal
            if (value.Length > 1 && value[0] == '0' && char.IsDigit(value[1]))
            {
                return Convert.ToByte(value, 8);
            }

            // Decimal
            return byte.Parse(value, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException || ex is OverflowException)
        {
            throw new EdsParseException($"Invalid byte value: '{value}'", ex);
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
            // Hexadecimal
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToUInt16(value.Substring(2), 16);
            }

            // Octal
            if (value.Length > 1 && value[0] == '0' && char.IsDigit(value[1]))
            {
                return Convert.ToUInt16(value, 8);
            }

            // Decimal
            return ushort.Parse(value, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException || ex is OverflowException)
        {
            throw new EdsParseException($"Invalid UInt16 value: '{value}'", ex);
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
    /// Evaluates a $NODEID formula with the given node ID.
    /// Supports formulas like "$NODEID", "$NODEID+0x200", "$NODEID+512", etc.
    /// </summary>
    private static uint EvaluateNodeIdFormula(string formula, byte nodeId)
    {
        formula = formula.Trim();

        if (formula.Equals("$NODEID", StringComparison.OrdinalIgnoreCase))
            return nodeId;

        const string token = "$NODEID";
        var suffix = formula.Substring(token.Length).Trim();

        if (suffix[0] == '+' || suffix[0] == '-')
        {
            var rightSide = suffix.Substring(1).Trim();
            if (string.IsNullOrEmpty(rightSide) || rightSide.IndexOf('+') >= 0 || rightSide.IndexOf('-') >= 0)
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

#pragma warning restore CA1845, CA1865, CA1866, CA2249 // span-based and char overloads not available in netstandard2.0
