namespace EdsDcfNet.Validation;

using System.Globalization;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Utilities;

/// <summary>
/// Checks object dictionary values (<c>DefaultValue</c>, <c>LowLimit</c>, <c>HighLimit</c>,
/// <c>ParameterValue</c>) against the entry's CANopen data type and against each other (#562).
/// </summary>
/// <remarks>
/// Only integer, BOOLEAN and REAL data types are checked; other types have no comparable value.
/// <c>$NODEID</c> formulas are evaluated for every node-ID in the supplied set, which is the
/// configured node-ID for a DCF and the range bounds 1 and 127 for an EDS.
/// </remarks>
internal static class ObjectValueValidator
{
    private static readonly byte[] NodeIdRangeBounds = { CanOpenNodeId.MinValue, CanOpenNodeId.MaxValue };

    private static readonly string[] ValueKeys = { "DefaultValue", "LowLimit", "HighLimit", "ParameterValue" };

    internal static byte[] ResolveNodeIds(byte? configuredNodeId) =>
        configuredNodeId.HasValue && CanOpenNodeId.IsInRange(configuredNodeId.Value)
            ? new[] { configuredNodeId.Value }
            : NodeIdRangeBounds;

    internal static void Validate(
        string path,
        ushort? dataType,
        string? defaultValue,
        string? lowLimit,
        string? highLimit,
        string? parameterValue,
        byte[] nodeIds,
        List<ValidationIssue> issues)
    {
        if (!dataType.HasValue || !IsComparableType(dataType.Value))
        {
            return;
        }

        var raw = new[] { defaultValue, lowLimit, highLimit, parameterValue };
        var values = new ComparableValue?[]?[ValueKeys.Length];
        for (var i = 0; i < ValueKeys.Length; i++)
        {
            values[i] = Evaluate(path, ValueKeys[i], raw[i], dataType.Value, nodeIds, issues);
        }

        var reported = new HashSet<string>(StringComparer.Ordinal);
        for (var n = 0; n < nodeIds.Length; n++)
        {
            var low = values[1]?[n];
            var high = values[2]?[n];

            if (low.HasValue && high.HasValue && low.Value.CompareTo(high.Value) > 0 && reported.Add("LowLimit"))
            {
                issues.Add(new ValidationIssue(
                    path + ".LowLimit",
                    "LowLimit " + low.Value + " is greater than HighLimit " + high.Value +
                    NodeSuffix(raw[1], raw[2], nodeIds[n]) + "."));
            }

            CheckWithinLimits(path, "DefaultValue", raw[0], values[0]?[n], raw, low, high, nodeIds[n], reported, issues);
            CheckWithinLimits(path, "ParameterValue", raw[3], values[3]?[n], raw, low, high, nodeIds[n], reported, issues);
        }
    }

    private static void CheckWithinLimits(
        string path,
        string key,
        string? rawValue,
        ComparableValue? value,
        string?[] raw,
        ComparableValue? low,
        ComparableValue? high,
        byte nodeId,
        HashSet<string> reported,
        List<ValidationIssue> issues)
    {
        if (!value.HasValue)
        {
            return;
        }

        var belowLow = low.HasValue && value.Value.CompareTo(low.Value) < 0;
        var aboveHigh = high.HasValue && value.Value.CompareTo(high.Value) > 0;
        if ((belowLow || aboveHigh) && reported.Add(key))
        {
            var limit = belowLow ? "below LowLimit " + low!.Value : "above HighLimit " + high!.Value;
            var limitRaw = belowLow ? raw[1] : raw[2];
            issues.Add(new ValidationIssue(
                path + "." + key,
                key + " " + value.Value + " is " + limit + NodeSuffix(rawValue, limitRaw, nodeId) + "."));
        }
    }

    private static ComparableValue?[]? Evaluate(
        string path,
        string key,
        string? rawValue,
        ushort dataType,
        byte[] nodeIds,
        List<ValidationIssue> issues)
    {
        if (rawValue is null || string.IsNullOrWhiteSpace(rawValue))
        {
            // The converter maps empty integer literals to 0; an absent limit must not act as 0.
            return null;
        }

        var value = rawValue.Trim();
        if (dataType == CanOpenDataType.Boolean &&
            (value.Equals("0x0", StringComparison.OrdinalIgnoreCase) || value.Equals("0x1", StringComparison.OrdinalIgnoreCase)))
        {
            // Hex booleans are accepted by the reader (#543).
            value = value.Substring(2);
        }

        if (dataType is CanOpenDataType.Real32 or CanOpenDataType.Real64 &&
            value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            // Some tools store REAL defaults as hex bit patterns; that is not a decimal literal the
            // converter can compare, so it is left unchecked rather than reported as invalid.
            return null;
        }

        var isFormula = value.StartsWith("$NODEID", StringComparison.OrdinalIgnoreCase);
        var result = new ComparableValue?[nodeIds.Length];
        for (var n = 0; n < nodeIds.Length; n++)
        {
            if (n > 0 && !isFormula)
            {
                result[n] = result[0];
                continue;
            }

            try
            {
                result[n] = ComparableValue.From(CanOpenValueConverter.Parse(value, dataType, nodeIds[n]));
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (Exception ex) when (ex is FormatException or OverflowException or EdsParseException)
            {
                issues.Add(new ValidationIssue(
                    path + "." + key,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} '{1}' is not a valid {2} value{3}: {4}",
                        key,
                        rawValue,
                        DescribeType(dataType),
                        isFormula ? string.Format(CultureInfo.InvariantCulture, " for node-ID {0}", nodeIds[n]) : string.Empty,
                        ex.Message)));
                return null;
            }
        }

        return result;
    }

    private static bool IsComparableType(ushort dataType) =>
        CanOpenDataType.IsSigned(dataType) ||
        CanOpenDataType.IsUnsigned(dataType) ||
        dataType is CanOpenDataType.Boolean or CanOpenDataType.Real32 or CanOpenDataType.Real64;

    private static string DescribeType(ushort dataType)
    {
        var name = CanOpenDataType.GetName(dataType) ?? dataType.ToString("X4", CultureInfo.InvariantCulture);
        var bits = CanOpenDataType.TryGetBitLength(dataType);
        if (!bits.HasValue || dataType == CanOpenDataType.Boolean || !(CanOpenDataType.IsSigned(dataType) || CanOpenDataType.IsUnsigned(dataType)))
        {
            return name;
        }

        string min, max;
        if (CanOpenDataType.IsSigned(dataType))
        {
            min = (bits.Value == 64 ? long.MinValue : -(1L << (bits.Value - 1))).ToString(CultureInfo.InvariantCulture);
            max = (bits.Value == 64 ? long.MaxValue : (1L << (bits.Value - 1)) - 1).ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            min = "0";
            max = (bits.Value == 64 ? ulong.MaxValue : (1UL << bits.Value) - 1).ToString(CultureInfo.InvariantCulture);
        }

        return name + " (" + min + ".." + max + ")";
    }

    private static string NodeSuffix(string? a, string? b, byte nodeId) =>
        IsFormula(a) || IsFormula(b)
            ? string.Format(CultureInfo.InvariantCulture, " (node-ID {0})", nodeId)
            : string.Empty;

    private static bool IsFormula(string? value) =>
        value != null && value.TrimStart().StartsWith("$NODEID", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A parsed value: 64-bit integers are compared exactly as <see cref="decimal"/>, REAL
    /// values as <see cref="double"/>.
    /// </summary>
    private readonly struct ComparableValue
    {
        private readonly decimal? _integer;
        private readonly double _real;

        private ComparableValue(decimal? integer, double real)
        {
            _integer = integer;
            _real = real;
        }

        public static ComparableValue? From(object parsed) => parsed switch
        {
            bool b => new ComparableValue(b ? 1m : 0m, 0d),
            float f => new ComparableValue(null, f),
            double d => new ComparableValue(null, d),
            sbyte or short or int or long => new ComparableValue(Convert.ToInt64(parsed, CultureInfo.InvariantCulture), 0d),
            byte or ushort or uint or ulong => new ComparableValue(Convert.ToUInt64(parsed, CultureInfo.InvariantCulture), 0d),
            _ => null,
        };

        public int CompareTo(ComparableValue other) =>
            _integer.HasValue && other._integer.HasValue
                ? _integer.Value.CompareTo(other._integer.Value)
                : ToDouble().CompareTo(other.ToDouble());

        public override string ToString() =>
            _integer.HasValue
                ? _integer.Value.ToString(CultureInfo.InvariantCulture)
                : _real.ToString("R", CultureInfo.InvariantCulture);

        private double ToDouble() => _integer.HasValue ? (double)_integer.Value : _real;
    }
}
