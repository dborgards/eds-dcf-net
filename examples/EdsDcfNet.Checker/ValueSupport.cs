namespace EdsDcfNet.Checker;

using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using EdsDcfNet.Utilities;

/// <summary>A parsed numeric value that can be compared against limits.</summary>
public readonly record struct NumericValue(BigInteger? Integer, double? Real)
{
    public int CompareTo(NumericValue other)
    {
        if (Integer.HasValue && other.Integer.HasValue)
        {
            return Integer.Value.CompareTo(other.Integer.Value);
        }

        return ToDouble().CompareTo(other.ToDouble());
    }

    public double ToDouble() => Integer.HasValue ? (double)Integer.Value : Real ?? 0d;

    public override string ToString() =>
        Integer.HasValue
            ? Integer.Value.ToString(CultureInfo.InvariantCulture)
            : (Real ?? 0d).ToString("R", CultureInfo.InvariantCulture);
}

/// <summary>Result of evaluating a raw EDS/DCF value against a CANopen data type.</summary>
public sealed class ValueEvaluation
{
    /// <summary>Value per node-ID (key 0 when the value does not depend on the node-ID).</summary>
    public Dictionary<byte, NumericValue> Values { get; } = new();

    public bool IsFormula { get; init; }

    public bool Checked => Values.Count > 0;

    public bool TryGet(byte nodeId, out NumericValue value) =>
        Values.TryGetValue(IsFormula ? nodeId : (byte)0, out value);
}

/// <summary>Helpers for data-type aware value checks.</summary>
public static class ValueSupport
{
    private static readonly Regex NodeIdSuffixFormula = new(
        @"^\$NODEID\s*(?:(?<op>[+-])\s*(?<operand>\S+))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex NodeIdPrefixFormula = new(
        @"^(?<operand>[^\s+$]+)\s*\+\s*\$NODEID$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool IsInteger(ushort dataType) =>
        CanOpenDataType.IsSigned(dataType) || CanOpenDataType.IsUnsigned(dataType);

    public static bool IsReal(ushort dataType) =>
        dataType is CanOpenDataType.Real32 or CanOpenDataType.Real64;

    public static bool IsNumeric(ushort dataType) =>
        IsInteger(dataType) || IsReal(dataType) || dataType == CanOpenDataType.Boolean;

    public static string TypeName(ushort dataType) =>
        CanOpenDataType.GetName(dataType)
        ?? string.Format(CultureInfo.InvariantCulture, "0x{0:X4}", dataType);

    /// <summary>Describes the valid value range of a numeric data type, e.g. <c>UNSIGNED8 (0..255)</c>.</summary>
    public static string RangeDescription(ushort dataType)
    {
        var bits = CanOpenDataType.TryGetBitLength(dataType);
        if (dataType == CanOpenDataType.Boolean)
        {
            return "BOOLEAN (0..1)";
        }

        if (bits is null || !IsInteger(dataType))
        {
            return TypeName(dataType);
        }

        BigInteger min, max;
        if (CanOpenDataType.IsSigned(dataType))
        {
            min = -(BigInteger.One << (bits.Value - 1));
            max = (BigInteger.One << (bits.Value - 1)) - 1;
        }
        else
        {
            min = BigInteger.Zero;
            max = (BigInteger.One << bits.Value) - 1;
        }

        return string.Format(CultureInfo.InvariantCulture, "{0} ({1}..{2})", TypeName(dataType), min, max);
    }

    /// <summary>
    /// Returns <see langword="true"/> for literals with a leading zero followed by digits,
    /// which CiA 306 / EdsDcfNet interpret as octal (<c>010</c> is 8, not 10).
    /// </summary>
    public static bool IsOctalLiteral(string value) =>
        value.Length > 1 && value[0] == '0' && char.IsDigit(value[1]);

    /// <summary>
    /// Tries to split a <c>$NODEID</c> formula into operator and operand.
    /// </summary>
    /// <param name="value">Raw value.</param>
    /// <param name="sign">+1 or -1 for the operand.</param>
    /// <param name="operand">Operand literal, or <see langword="null"/> for a plain <c>$NODEID</c>.</param>
    /// <param name="prefixForm">
    /// <see langword="true"/> for the <c>0x180+$NODEID</c> form, which EdsDcfNet cannot evaluate.
    /// </param>
    public static bool TrySplitFormula(string value, out int sign, out string? operand, out bool prefixForm)
    {
        sign = 1;
        operand = null;
        prefixForm = false;

        var suffix = NodeIdSuffixFormula.Match(value);
        if (suffix.Success)
        {
            if (suffix.Groups["op"].Success)
            {
                sign = suffix.Groups["op"].Value == "-" ? -1 : 1;
                operand = suffix.Groups["operand"].Value;
            }

            return true;
        }

        var prefix = NodeIdPrefixFormula.Match(value);
        if (prefix.Success)
        {
            operand = prefix.Groups["operand"].Value;
            prefixForm = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Parses a literal (no formula) with the library converter and returns it as a comparable value.
    /// Throws the converter's exceptions for values that do not fit the data type.
    /// </summary>
    public static NumericValue? ParseLiteral(string value, ushort dataType)
    {
        if (dataType == CanOpenDataType.Boolean &&
            (value.Equals("0x0", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("0x1", StringComparison.OrdinalIgnoreCase)))
        {
            // Accepted by the EdsDcfNet reader (#543) although not canonical.
            return new NumericValue(value.EndsWith('1') ? BigInteger.One : BigInteger.Zero, null);
        }

        var parsed = CanOpenValueConverter.Parse(value, dataType);
        return parsed switch
        {
            bool b => new NumericValue(b ? BigInteger.One : BigInteger.Zero, null),
            float f => new NumericValue(null, f),
            double d => new NumericValue(null, d),
            sbyte or short or int or long => new NumericValue(new BigInteger(Convert.ToInt64(parsed, CultureInfo.InvariantCulture)), null),
            byte or ushort or uint or ulong => new NumericValue(new BigInteger(Convert.ToUInt64(parsed, CultureInfo.InvariantCulture)), null),
            _ => null,
        };
    }

    /// <summary>Parses the operand of a <c>$NODEID</c> formula (decimal, hex or octal).</summary>
    public static long ParseOperand(string operand) => ValueConverter.ParseInteger(operand);
}
