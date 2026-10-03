namespace EdsDcfNet.Validation;

using System.Globalization;
using EdsDcfNet.Models;
using EdsDcfNet.Writers;

/// <summary>
/// Format-family checks applied after shared model validation when an XDD or XDC file is
/// written with <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/>.
/// </summary>
/// <remarks>
/// CiA 311 Annex A.1.4 defines <c>objFlags</c> bits 0, 1 and 2 and reserves bits 3..31.
/// Bit 2 (the change takes effect after reset, <c>objFlags="0004"</c>) is valid here.
/// The stricter EDS/DCF limit, which also reserves bit 2, stays in <see cref="IniWriteRules"/>.
/// <para>
/// The XDD/XDC writers stay tolerant. They omit a date or time that is not valid and write an empty
/// string for a required text; only a validated write reports what the schema would reject
/// (<c>fileCreationDate</c> is required, <c>CANopenObjectList</c> needs a <c>CANopenObject</c>) or
/// what carries no information (empty <c>fileName</c>, <c>fileCreator</c>, <c>actualBaudRate</c>).
/// </para>
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
                ApplyFileInfo(eds.FileInfo, issues);
                ApplyDictionary(eds.ObjectDictionary, issues);
                break;
            case DeviceConfigurationFile dcf:
                ApplyFileInfo(dcf.FileInfo, issues);
                ApplyDictionary(dcf.ObjectDictionary, issues);
                ApplyCommissioning(dcf.DeviceCommissioning, issues);
                break;
        }
    }

    private static void ApplyFileInfo(EdsFileInfo fileInfo, List<ValidationIssue> issues)
    {
        if (string.IsNullOrEmpty(fileInfo.FileName))
            issues.Add(EmptyFileText("FileInfo.FileName", "fileName"));

        if (string.IsNullOrEmpty(fileInfo.CreatedBy))
            issues.Add(EmptyFileText("FileInfo.CreatedBy", "fileCreator"));

        if (string.IsNullOrWhiteSpace(fileInfo.CreationDate))
        {
            issues.Add(new ValidationIssue(
                "FileInfo.CreationDate",
                "fileCreationDate is required by CiA 311 and CreationDate is empty. The attribute is omitted and the document is not schema-valid.",
                ValidationIssueCodes.XddFileCreationDateMissing));
        }
        else
        {
            CheckDate(fileInfo.CreationDate, fileInfo.CreationDateLexical, "FileInfo.CreationDate", "fileCreationDate", issues);
        }

        if (!string.IsNullOrWhiteSpace(fileInfo.ModificationDate))
            CheckDate(fileInfo.ModificationDate, fileInfo.ModificationDateLexical, "FileInfo.ModificationDate", "fileModificationDate", issues);

        CheckTime(fileInfo.CreationTime, "FileInfo.CreationTime", "fileCreationTime", issues);
        CheckTime(fileInfo.ModificationTime, "FileInfo.ModificationTime", "fileModificationTime", issues);
    }

    private static ValidationIssue EmptyFileText(string path, string attribute) =>
        new(
            path,
            string.Format(
                CultureInfo.InvariantCulture,
                "{0} is required by CiA 311 and is written as an empty string, which says nothing about the file.",
                attribute),
            ValidationIssueCodes.XddFileTextEmpty);

    private static void CheckDate(string value, string? preserved, string path, string attribute, List<ValidationIssue> issues)
    {
        if (XddFormatHelper.TryFormatFileDate(value, preserved, out _))
            return;

        issues.Add(new ValidationIssue(
            path,
            string.Format(
                CultureInfo.InvariantCulture,
                "{0} '{1}' is not a valid date (MM-DD-YYYY or xsd:date). The attribute is omitted.",
                attribute,
                value),
            ValidationIssueCodes.XddFileDateInvalid));
    }

    private static void CheckTime(string value, string path, string attribute, List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value) || XddFormatHelper.TryConvertFileTimeToXsd(value, out _))
            return;

        issues.Add(new ValidationIssue(
            path,
            string.Format(
                CultureInfo.InvariantCulture,
                "{0} '{1}' is neither xsd:time nor hh:mmAM/PM. The attribute is omitted.",
                attribute,
                value),
            ValidationIssueCodes.XddFileTimeInvalid));
    }

    private static void ApplyCommissioning(DeviceCommissioning? commissioning, List<ValidationIssue> issues)
    {
        // The writer omits an all-default commissioning, so there is no attribute to check.
        if (commissioning == null || DeviceCommissioningSemantics.IsOmitted(commissioning))
            return;

        var preserved = commissioning.ActualBaudRateLexical != null &&
                        commissioning.Baudrate == commissioning.ActualBaudRateLexicalBaseline;
        if (commissioning.Baudrate != 0 || preserved)
            return;

        issues.Add(new ValidationIssue(
            "DeviceCommissioning.Baudrate",
            "Baudrate is not set. actualBaudRate is required by CiA 311 and would be written as an empty string.",
            ValidationIssueCodes.XddActualBaudRateNotSet));
    }

    private static void ApplyDictionary(ObjectDictionary dictionary, List<ValidationIssue> issues)
    {
        if (dictionary.Objects.Count == 0)
        {
            issues.Add(new ValidationIssue(
                "ObjectDictionary.Objects",
                "CANopenObjectList requires at least one CANopenObject (CiA 311).",
                ValidationIssueCodes.XddObjectDictionaryEmpty));
        }

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
