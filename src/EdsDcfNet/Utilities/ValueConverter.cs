namespace EdsDcfNet.Utilities;

using EdsDcfNet.Models;

/// <summary>
/// Utility class for converting string values from EDS/DCF files to typed values.
/// </summary>
public static class ValueConverter
{
    /// <summary>
    /// Parses an integer value from string (supports decimal, hexadecimal, and octal).
    /// </summary>
    public static uint ParseInteger(string value)
    {
        value = value.Trim();

        if (string.IsNullOrEmpty(value))
            return 0;

        // Handle $NODEID formula
        if (value.StartsWith("$NODEID", StringComparison.OrdinalIgnoreCase))
        {
            // For now, return 0 as a placeholder
            // In real usage, this should be replaced with actual node ID
            return 0;
        }

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
        return uint.Parse(value);
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
        return byte.Parse(value);
    }

    /// <summary>
    /// Parses a ushort value from string.
    /// </summary>
    public static ushort ParseUInt16(string value)
    {
        value = value.Trim();

        if (string.IsNullOrEmpty(value))
            return 0;

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
        return ushort.Parse(value);
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
        return value.ToString();
    }

    /// <summary>
    /// Formats a boolean value for EDS/DCF output.
    /// </summary>
    public static string FormatBoolean(bool value)
    {
        return value ? "1" : "0";
    }
}
