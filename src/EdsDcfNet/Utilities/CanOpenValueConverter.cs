namespace EdsDcfNet.Utilities;

using System.Globalization;

/// <summary>
/// Converts CANopen Object Dictionary values between their EDS/DCF string representation
/// and the corresponding .NET type defined by the CANopen data type index.
/// </summary>
public static class CanOpenValueConverter
{
    /// <summary>
    /// Parses an Object Dictionary value according to its CANopen data type index.
    /// </summary>
    /// <param name="value">The textual EDS/DCF value.</param>
    /// <param name="dataType">The CANopen data type index.</param>
    /// <param name="nodeId">
    /// Optional node ID used to evaluate <c>$NODEID</c> formulas such as <c>$NODEID+0x200</c>.
    /// Required when <paramref name="value"/> contains a <c>$NODEID</c> expression.
    /// </param>
    /// <returns>The value represented by the corresponding .NET type.</returns>
    /// <remarks>
    /// Integer values may use decimal, hexadecimal (<c>0x</c>), octal notation, or
    /// <c>$NODEID</c> formulas. Empty or whitespace-only integer literals are treated as zero.
    /// OCTET_STRING and DOMAIN values are returned as <see cref="byte"/> arrays when written
    /// as hexadecimal literals. TIME_OF_DAY and TIME_DIFFERENCE currently remain unsupported
    /// because their file representation does not provide a universally interoperable mapping.
    /// </remarks>
    public static object Parse(string value, ushort dataType, byte? nodeId = null)
    {
        EnsureNotNull(value, nameof(value));

        return dataType switch
        {
            0x0001 => ParseBoolean(value),
            0x0002 => (sbyte)ParseSignedInteger(value, 8, nodeId),
            0x0003 => (short)ParseSignedInteger(value, 16, nodeId),
            0x0004 => (int)ParseSignedInteger(value, 32, nodeId),
            0x0005 => (byte)ParseUnsignedInteger(value, 8, nodeId),
            0x0006 => (ushort)ParseUnsignedInteger(value, 16, nodeId),
            0x0007 => (uint)ParseUnsignedInteger(value, 32, nodeId),
            0x0008 => float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture),
            0x0009 => value,
            0x000A => ParseByteString(value),
            0x000B => value,
            0x000F => ParseByteString(value),
            0x0010 => (int)ParseSignedInteger(value, 24, nodeId),
            0x0011 => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture),
            0x0012 => ParseSignedInteger(value, 40, nodeId),
            0x0013 => ParseSignedInteger(value, 48, nodeId),
            0x0014 => ParseSignedInteger(value, 56, nodeId),
            0x0015 => ParseSignedInteger(value, 64, nodeId),
            0x0016 => (uint)ParseUnsignedInteger(value, 24, nodeId),
            0x0018 => ParseUnsignedInteger(value, 40, nodeId),
            0x0019 => ParseUnsignedInteger(value, 48, nodeId),
            0x001A => ParseUnsignedInteger(value, 56, nodeId),
            0x001B => ParseUnsignedInteger(value, 64, nodeId),
            0x000C or 0x000D => throw new NotSupportedException(
                $"CANopen data type 0x{dataType:X4} does not have a universally interoperable EDS/DCF to .NET mapping."),
            _ => throw new NotSupportedException($"CANopen data type 0x{dataType:X4} is not supported for typed value conversion.")
        };
    }

    /// <summary>
    /// Formats a .NET value according to its CANopen data type index for storage in an EDS/DCF model.
    /// </summary>
    public static string Format(object value, ushort dataType)
    {
        EnsureNotNull(value, nameof(value));

        try
        {
            return dataType switch
            {
                0x0001 => FormatBoolean(value),
                0x0002 => FormatSigned(value, 8),
                0x0003 => FormatSigned(value, 16),
                0x0004 => FormatSigned(value, 32),
                0x0005 => FormatUnsigned(value, 8),
                0x0006 => FormatUnsigned(value, 16),
                0x0007 => FormatUnsigned(value, 32),
                0x0008 => Convert.ToSingle(value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture),
                0x0009 or 0x000B => value as string
                    ?? throw new InvalidCastException("VISIBLE_STRING and UNICODE_STRING values must be supplied as strings."),
                0x000A or 0x000F => FormatByteString(value),
                0x0010 => FormatSigned(value, 24),
                0x0011 => Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture),
                0x0012 => FormatSigned(value, 40),
                0x0013 => FormatSigned(value, 48),
                0x0014 => FormatSigned(value, 56),
                0x0015 => FormatSigned(value, 64),
                0x0016 => FormatUnsigned(value, 24),
                0x0018 => FormatUnsigned(value, 40),
                0x0019 => FormatUnsigned(value, 48),
                0x001A => FormatUnsigned(value, 56),
                0x001B => FormatUnsigned(value, 64),
                0x000C or 0x000D => throw new NotSupportedException(
                    $"CANopen data type 0x{dataType:X4} does not have a universally interoperable EDS/DCF to .NET mapping."),
                _ => throw new NotSupportedException($"CANopen data type 0x{dataType:X4} is not supported for typed value conversion.")
            };
        }
        catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
        {
            throw new ArgumentException(
                $"Value '{value}' cannot be represented by CANopen data type 0x{dataType:X4}.",
                nameof(value),
                ex);
        }
    }

    private static bool ParseBoolean(string value)
    {
        var normalized = value.Trim();
        if (normalized == "1"
            || normalized.Equals("true", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalized == "0"
            || normalized.Equals("false", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("no", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new FormatException($"'{value}' is not a valid CANopen BOOLEAN value.");
    }

    private static string FormatBoolean(object value)
    {
        if (value is bool boolean)
        {
            return boolean ? "1" : "0";
        }

        // Accept numeric 0/1 only; any other number is outside the CANopen BOOLEAN value range.
        // Fractional inputs must be rejected before conversion so 0.25 does not round to 0.
        EnsureIntegral(value);
        var numeric = Convert.ToInt64(value, CultureInfo.InvariantCulture);
        return numeric switch
        {
            0 => "0",
            1 => "1",
            _ => throw new OverflowException($"Value {numeric} is outside the CANopen BOOLEAN value range (0 or 1).")
        };
    }

    private static long ParseSignedInteger(string value, int bits, byte? nodeId)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return 0;
        }

        long result;
        if (trimmed.StartsWith("$NODEID", StringComparison.OrdinalIgnoreCase))
        {
            result = EvaluateSignedNodeIdFormula(trimmed, nodeId);
            ValidateSignedRange(result, bits);
            return result;
        }

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            var raw = ulong.Parse(trimmed[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
            ValidateUnsignedRange(raw, bits);
            if (bits < 64 && (raw & (1UL << (bits - 1))) != 0)
            {
                result = (long)(raw - (1UL << bits));
            }
            else
            {
                result = unchecked((long)raw);
            }
        }
        else if (IsOctal(trimmed))
        {
            var raw = Convert.ToUInt64(trimmed, 8);
            ValidateUnsignedRange(raw, bits);
            result = bits < 64 && (raw & (1UL << (bits - 1))) != 0
                ? (long)(raw - (1UL << bits))
                : unchecked((long)raw);
        }
        else
        {
            result = long.Parse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture);
            ValidateSignedRange(result, bits);
        }

        return result;
    }

    private static ulong ParseUnsignedInteger(string value, int bits, byte? nodeId)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return 0;
        }

        ulong result;
        if (trimmed.StartsWith("$NODEID", StringComparison.OrdinalIgnoreCase))
        {
            result = EvaluateUnsignedNodeIdFormula(trimmed, nodeId);
        }
        else if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            result = ParseUnsignedLiteral(trimmed);
        }
        else if (IsOctal(trimmed))
        {
            result = ParseUnsignedLiteral(trimmed);
        }
        else
        {
            result = ParseUnsignedLiteral(trimmed);
        }

        ValidateUnsignedRange(result, bits);
        return result;
    }

    private static string FormatSigned(object value, int bits)
    {
        EnsureIntegral(value);
        var converted = Convert.ToInt64(value, CultureInfo.InvariantCulture);
        ValidateSignedRange(converted, bits);
        return converted.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatUnsigned(object value, int bits)
    {
        EnsureIntegral(value);
        var converted = Convert.ToUInt64(value, CultureInfo.InvariantCulture);
        ValidateUnsignedRange(converted, bits);
        return converted.ToString(CultureInfo.InvariantCulture);
    }

    private static void EnsureIntegral(object value)
    {
        if (value is float or double or decimal)
        {
            throw new InvalidCastException(
                $"Value '{value}' is a floating-point number and cannot be stored in an integer CANopen data type without loss.");
        }
    }

    private static void ValidateSignedRange(long value, int bits)
    {
        if (bits == 64)
        {
            return;
        }

        var minimum = -(1L << (bits - 1));
        var maximum = (1L << (bits - 1)) - 1;
        if (value < minimum || value > maximum)
        {
            throw new OverflowException($"Value {value} is outside the signed {bits}-bit range.");
        }
    }

    private static void ValidateUnsignedRange(ulong value, int bits)
    {
        if (bits < 64 && value >= (1UL << bits))
        {
            throw new OverflowException($"Value {value} is outside the unsigned {bits}-bit range.");
        }
    }

    private static bool IsOctal(string value) =>
        value.Length > 1 && value[0] == '0' && char.IsDigit(value[1]);

    /// <summary>
    /// Parses an unsigned integer literal (decimal, hexadecimal, or octal) into a
    /// <see cref="ulong"/> so 40/48/56/64-bit values are preserved.
    /// </summary>
    private static ulong ParseUnsignedLiteral(string trimmed)
    {
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ulong.Parse(trimmed[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        }

        if (IsOctal(trimmed))
        {
            return Convert.ToUInt64(trimmed, 8);
        }

        return ulong.Parse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Splits a <c>$NODEID</c> formula into its operator and unsigned operand. The operand is
    /// parsed as <see cref="ulong"/> so 40/48/56/64-bit offsets are preserved, and malformed
    /// input consistently surfaces as <see cref="FormatException"/> like other literals in
    /// this converter.
    /// </summary>
    private static (char? Operator, ulong Operand) SplitNodeIdFormula(string formula, byte? nodeId)
    {
        if (!nodeId.HasValue)
        {
            throw new NotSupportedException(
                $"Cannot evaluate $NODEID formula '{formula}' without a node ID context.");
        }

        const string token = "$NODEID";
        var suffix = formula[token.Length..].Trim();
        if (suffix.Length == 0)
        {
            return (null, 0);
        }

        if (suffix[0] is '+' or '-')
        {
            var rightSide = suffix[1..].Trim();
            if (rightSide.Length == 0 || rightSide.Contains('+') || rightSide.Contains('-'))
            {
                throw new FormatException(
                    $"Unsupported $NODEID formula '{formula}'. Expected '$NODEID', '$NODEID+<number>' or '$NODEID-<number>'.");
            }

            return (suffix[0], ParseUnsignedLiteral(rightSide));
        }

        throw new FormatException(
            $"Unsupported $NODEID formula '{formula}'. Expected '$NODEID', '$NODEID+<number>' or '$NODEID-<number>'.");
    }

    /// <summary>
    /// Evaluates a <c>$NODEID</c> formula in signed 64-bit arithmetic so subtraction can yield
    /// negative values for signed target types (e.g. <c>$NODEID-2</c> with node ID 1 is -1).
    /// </summary>
    private static long EvaluateSignedNodeIdFormula(string formula, byte? nodeId)
    {
        var (op, operand) = SplitNodeIdFormula(formula, nodeId);
        if (op is null)
        {
            return nodeId!.Value;
        }

        if (operand > long.MaxValue)
        {
            throw new OverflowException($"$NODEID formula '{formula}' has an operand outside the signed 64-bit range.");
        }

        // Fold the sign into the operand so a single checked addition covers both cases.
        // Only addition can overflow: the node ID is at most 255 and the negated operand
        // is at least -long.MaxValue, so subtraction stays within the signed 64-bit range.
        var signedOperand = op.Value == '+' ? (long)operand : -(long)operand;
        return checked(nodeId!.Value + signedOperand);
    }

    /// <summary>
    /// Evaluates a <c>$NODEID</c> formula in unsigned 64-bit arithmetic so 40/48/56/64-bit
    /// offsets are preserved. Subtraction below zero raises <see cref="OverflowException"/>.
    /// </summary>
    private static ulong EvaluateUnsignedNodeIdFormula(string formula, byte? nodeId)
    {
        var (op, operand) = SplitNodeIdFormula(formula, nodeId);
        return op switch
        {
            null => nodeId!.Value,
            '+' => checked(nodeId!.Value + operand),
            _ => nodeId!.Value >= operand
                ? nodeId!.Value - operand
                : throw new OverflowException($"$NODEID formula '{formula}' underflows the unsigned value range.")
        };
    }

    private static byte[] ParseByteString(string value)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..];
        }

        normalized = normalized.Replace(" ", string.Empty).Replace("-", string.Empty);
        if (normalized.Length % 2 != 0)
        {
            throw new FormatException("A CANopen byte string must contain an even number of hexadecimal digits.");
        }

        var result = new byte[normalized.Length / 2];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = (byte)((ParseHexDigit(normalized[i * 2]) << 4) | ParseHexDigit(normalized[(i * 2) + 1]));
        }

        return result;
    }

    private static string FormatByteString(object value)
    {
        if (value is not byte[] bytes)
        {
            throw new InvalidCastException("OCTET_STRING and DOMAIN values must be supplied as byte arrays.");
        }

        const string hexDigits = "0123456789ABCDEF";
        var chars = new char[(bytes.Length * 2) + 2];
        chars[0] = '0';
        chars[1] = 'x';
        for (var i = 0; i < bytes.Length; i++)
        {
            chars[(i * 2) + 2] = hexDigits[bytes[i] >> 4];
            chars[(i * 2) + 3] = hexDigits[bytes[i] & 0x0F];
        }

        return new string(chars);
    }

    private static int ParseHexDigit(char value)
    {
        if (value >= '0' && value <= '9')
        {
            return value - '0';
        }

        if (value >= 'A' && value <= 'F')
        {
            return value - 'A' + 10;
        }

        if (value >= 'a' && value <= 'f')
        {
            return value - 'a' + 10;
        }

        throw new FormatException($"'{value}' is not a hexadecimal digit.");
    }

    private static void EnsureNotNull(object? value, string parameterName)
    {
        if (value == null)
        {
            throw new ArgumentNullException(parameterName);
        }
    }
}
