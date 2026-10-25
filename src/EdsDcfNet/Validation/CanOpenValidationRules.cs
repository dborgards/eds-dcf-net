namespace EdsDcfNet.Validation;

using System.Globalization;

/// <summary>Single source of truth for constraints shared by model and raw-source validation.</summary>
internal static class CanOpenValidationRules
{
    internal const int MaxParameterNameLength = 241;
    internal const int MaxNodeNameLength = 246;
    internal const int MaxNetworkNameLength = 243;
    internal const int MaxVendorNameLength = 244;
    internal const int MaxProductNameLength = 243;
    internal const int MaxOrderCodeLength = 245;
    internal const int MaxReferenceNameLength = 249;
    internal const byte MaxGranularity = 64;

    internal static readonly ushort[] AllowedBaudrateValues = { 10, 20, 50, 100, 125, 250, 500, 800, 1000 };

    internal static readonly HashSet<ushort> AllowedBaudrates = new(AllowedBaudrateValues);

    internal static readonly string AllowedBaudratesDescription = string.Join(
        ", ",
        AllowedBaudrateValues.Select(v => v.ToString(CultureInfo.InvariantCulture)));

    internal static readonly ushort[] MandatoryObjectIndexes = { 0x1000, 0x1001, 0x1018 };
}
