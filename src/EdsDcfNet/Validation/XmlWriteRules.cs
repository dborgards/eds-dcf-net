namespace EdsDcfNet.Validation;

using System.Globalization;
using EdsDcfNet.Models;

/// <summary>
/// Format-family checks applied after shared model validation when an XDD or XDC file is
/// written with <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/>.
/// </summary>
/// <remarks>
/// CiA 311 Annex A.1.4 defines <c>objFlags</c> bits 0, 1 and 2 and reserves bits 3..31.
/// Bit 2 (the change takes effect after reset, <c>objFlags="0004"</c>) is valid here.
/// The stricter EDS/DCF limit, which also reserves bit 2, stays in <see cref="IniWriteRules"/>.
/// </remarks>
internal static class XmlWriteRules
{
    /// <summary>Bits 0..2 are defined. Every higher bit is reserved.</summary>
    private const uint DefinedObjFlagsMask = 0x7;

    internal static void Apply(object model, List<ValidationIssue> issues)
    {
        switch (model)
        {
            case ElectronicDataSheet eds:
                ApplyDictionary(eds.ObjectDictionary, issues);
                break;
            case DeviceConfigurationFile dcf:
                ApplyDictionary(dcf.ObjectDictionary, issues);
                break;
        }
    }

    private static void ApplyDictionary(ObjectDictionary dictionary, List<ValidationIssue> issues)
    {
        foreach (var entry in dictionary.Objects)
            ApplyObject(entry.Key, entry.Value, issues);
    }

    private static void ApplyObject(ushort index, CanOpenObject obj, List<ValidationIssue> issues)
    {
        var path = string.Format(
            CultureInfo.InvariantCulture,
            "ObjectDictionary.Objects[0x{0:X4}].ObjFlags",
            index);

        if (obj.ObjFlagsLexical != null && obj.ObjFlags == obj.ObjFlagsLexicalBaseline)
        {
            issues.Add(new ValidationIssue(
                path,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ObjFlags preserves hexBinary '{0}', which does not fit in 32 bits. CiA 311 reserves bits 3..31.",
                    obj.ObjFlagsLexical),
                ValidationIssueCodes.XddObjFlagsExceedsUInt32));
            return;
        }

        if ((obj.ObjFlags & ~DefinedObjFlagsMask) == 0)
            return;

        issues.Add(new ValidationIssue(
            path,
            string.Format(
                CultureInfo.InvariantCulture,
                "ObjFlags 0x{0:X} sets reserved bits 3..31. CiA 311 defines bits 0, 1, and 2; bits 3..31 are reserved.",
                obj.ObjFlags),
            ValidationIssueCodes.XddObjFlagsReservedBits));
    }
}
