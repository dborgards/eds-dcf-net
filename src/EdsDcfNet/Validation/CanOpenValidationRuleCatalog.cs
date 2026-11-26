namespace EdsDcfNet.Validation;

using System.Diagnostics.CodeAnalysis;
using EdsDcfNet.Diagnostics;

/// <summary>
/// Lists every rule <see cref="CanOpenIniValidator"/> can report, with its default severity,
/// a short title and the CiA reference it follows.
/// </summary>
/// <remarks>
/// Besides the raw-source rules, the catalog lists the EDS/DCF reader codes
/// (<see cref="ParseDiagnosticCodes"/>) and the <c>LIB</c> fallback codes that
/// <see cref="CanOpenIniValidationOptions.IncludeReaderDiagnostics"/> and
/// <see cref="CanOpenIniValidationOptions.IncludeModelValidation"/> report.
/// </remarks>
public static class CanOpenValidationRuleCatalog
{
    private const IniValidationSeverity Error = IniValidationSeverity.Error;
    private const IniValidationSeverity Warning = IniValidationSeverity.Warning;

    private const string FileFormat = "CiA 306-1 clause 6.2";
    private const string Formula = "CiA 306-1 clause 6.3";
    private const string ObjectKeys = "CiA 306-1 Table 7";
    private const string ObjectLists = "CiA 306-1 Table 4";
    private const string ObjectListEntries = "CiA 306-1 Table 5";
    private const string Commissioning = "CiA 306-1 Table 12";
    private const string Cia301 = "CiA 301";

    private static readonly ValidationRule[] Rules =
    {
        Rule("INI001", Error, "Malformed section header or line without Key=Value", FileFormat),
        Rule("INI002", Error, "Duplicate section, including two spellings of the same object or sub-index", FileFormat),
        Rule("INI003", Warning, "Duplicate key within a section", FileFormat),
        Rule("INI004", Error, "Entry outside of any section", FileFormat),

        Rule("OBJ001", Error, "ObjectType is not a number or not a CiA 306 object code", ObjectKeys),
        Rule("OBJ002", Error, "DataType is not a number", ObjectKeys),
        Rule("OBJ003", Error, "DataType is not a basic type or not defined by a DEFTYPE/DEFSTRUCT", ObjectKeys),
        Rule("OBJ004", Error, "DataType is missing", ObjectKeys),
        Rule("OBJ005", Error, "AccessType is missing or not ro/wo/rw/rwr/rww/const", ObjectKeys),
        Rule("OBJ006", Error, "SubNumber or CompactSubObj does not match the sub-index sections", "CiA 306-1 clause 6.6.3.2"),
        Rule("OBJ007", Error, "Sub-index 0 value does not match the highest defined sub-index", "CiA 306-1 Table 6"),
        Rule("OBJ008", Error, "Sub-index section without parent object section", null),
        Rule("OBJ009", Error, "SubNumber/CompactSubObj is not UNSIGNED8 or PDOMapping is not 0/1", ObjectKeys),
        Rule("OBJ010", Error, "ParameterName is missing or longer than 241 characters", ObjectKeys),
        Rule("OBJ011", Error, "Zero-padded section name that the reader does not probe", null),

        Rule("VAL001", Error, "Value is invalid or out of range for the data type", ObjectKeys),
        Rule("VAL002", Error, "LowLimit is greater than HighLimit", ObjectKeys),
        Rule("VAL003", Error, "DefaultValue is outside LowLimit..HighLimit", ObjectKeys),
        Rule("VAL004", Error, "ParameterValue is outside LowLimit..HighLimit", ObjectKeys),
        Rule("VAL005", Warning, "Leading zero makes the value an octal literal", ObjectKeys),
        Rule("VAL006", Warning, "Limits on a non-numeric data type", ObjectKeys),
        Rule("VAL007", Warning, "Hex literal for REAL32/REAL64", ObjectKeys),
        Rule("VAL008", Error, "DCF compact value entry that the reader discards", null),

        Rule("FRM001", Error, "Invalid $NODEID formula or operand", Formula),
        Rule("FRM002", Error, "$NODEID is not at the beginning of the formula", Formula),
        Rule("FRM003", Error, "$NODEID formula on a non-integer data type", Formula),
        Rule("FRM004", Error, "Formula result out of range", Formula),
        Rule("FRM005", Warning, "Subtraction in a $NODEID formula is not CiA 306 syntax", Formula),

        Rule("STD001", Error, "Well-known CiA 301 entry has the wrong DataType", Cia301),

        Rule("LST001", Error, "SupportedObjects is missing or does not match the number of entries", ObjectListEntries),
        Rule("LST002", Error, "Object list references a missing section", ObjectLists),
        Rule("LST003", Error, "Object section is not listed in any object list", ObjectLists),
        Rule("LST004", Error, "Object is listed more than once", ObjectLists),
        Rule("LST005", Error, "Mandatory object 1000h/1001h/1018h is missing or in the wrong list", ObjectLists),
        Rule("LST006", Error, "Invalid object list entry", ObjectListEntries),
        Rule("LST007", Warning, "Object is listed in the wrong object list", ObjectLists),

        Rule("PDO001", Error, "PDO mapping references a missing object or an invalid dummy type", Cia301),
        Rule("PDO002", Error, "Mapped object is not PDO-mappable or has the wrong access direction", Cia301),
        Rule("PDO003", Error, "Mapping length does not match the mapped data type", Cia301),
        Rule("PDO004", Error, "PDO maps more than 64 bits", Cia301),

        Rule("MND001", Error, "Mandatory section is missing", "CiA 306-1 Tables 1, 2, 4 and 12"),
        Rule("MND002", Warning, "Mandatory FileInfo/DeviceInfo/DeviceComissioning entry is missing or empty", "CiA 306-1 Tables 1, 2 and 12"),
        Rule("MND003", Error, "Entry value has the wrong type or format", "CiA 306-1 Tables 1, 2 and 12"),
        Rule("MND004", Warning, "No supported baud rate is set", "CiA 306-1 Table 2"),
        Rule("MND005", Error, "DeviceInfo disagrees with the object dictionary", "CiA 306-1 Table 2"),

        Rule("DCF001", Warning, "DCF without configured NodeID", Commissioning),
        Rule("DCF002", Error, "DCF NodeID is outside 1..127", Commissioning),
        Rule("DCF003", Warning, "DCF has both [DeviceComissioning] and [DeviceCommissioning]", Commissioning),

        Rule("LIB001", Warning, "Reader diagnostic without a code", null),
        Rule("LIB002", Error, "The EdsDcfNet reader aborted", null),
        Rule("LIB003", Error, "Model validation issue without a code", null),

        Rule(ParseDiagnosticCodes.IniDuplicateKey, Warning, "Reader: duplicate key, the last value is kept", null),
        Rule(ParseDiagnosticCodes.IniMalformedSectionHeader, Warning, "Reader: malformed section header is ignored with its keys", null),
        Rule(ParseDiagnosticCodes.IniMissingEquals, Warning, "Reader: line without Key=Value is ignored", null),
        Rule(ParseDiagnosticCodes.IniDuplicateSection, Warning, "Reader: repeated section is merged into the first one", null),
        Rule(ParseDiagnosticCodes.IniDecodedAsIso88591, Warning, "Reader: file is not valid UTF-8 and was decoded as ISO-8859-1", null),
        Rule(ParseDiagnosticCodes.InvalidEncodedBytes, Warning, "Reader: bytes are not valid in the requested encoding", null),
        Rule(ParseDiagnosticCodes.IniInvalidDummyUsageKey, Warning, "Reader: invalid DummyUsage key is ignored", null),
        Rule(ParseDiagnosticCodes.IniUnlistedObjectSection, Warning, "Reader: object section is not listed in an object list and is not loaded", ObjectLists),
        Rule(ParseDiagnosticCodes.IniUnlistedModuleSection, Warning, "Reader: module section of a module that is not parsed", null),
        Rule(ParseDiagnosticCodes.IniOrphanCompanionSection, Warning, "Reader: companion section of a listed object without object section", null),
        Rule(ParseDiagnosticCodes.IniOrphanSubObjectSection, Warning, "Reader: sub-index section is not loaded as a sub-object", null),
        Rule(ParseDiagnosticCodes.IniVersionMajorMinor, Warning, "Reader: FileVersion/FileRevision in major.minor form, the major part is used", null),
        Rule(ParseDiagnosticCodes.IniObjectKeyNotSupported, Warning, "Reader: key not supported for the ObjectType", ObjectKeys),
        Rule(ParseDiagnosticCodes.UnknownBooleanToken, Warning, "Reader: unknown boolean value", null),
        Rule(ParseDiagnosticCodes.UnknownAccessTypeToken, Warning, "Reader: unknown AccessType, read as ro", null),
        Rule(ParseDiagnosticCodes.InvalidObjectType, Warning, "Reader: malformed ObjectType, read as VAR", ObjectKeys),
        Rule(ParseDiagnosticCodes.InvalidDataType, Warning, "Reader: malformed DataType", ObjectKeys),
        Rule(ParseDiagnosticCodes.InvalidSubNumber, Warning, "Reader: malformed SubNumber is ignored", ObjectKeys),
        Rule(ParseDiagnosticCodes.InvalidCompactSubObj, Warning, "Reader: malformed CompactSubObj is ignored", ObjectKeys),
        Rule(ParseDiagnosticCodes.InvalidObjFlags, Warning, "Reader: malformed ObjFlags, read as 0", "CiA 306-1 Table 8"),
        Rule(ParseDiagnosticCodes.IniObjFlagsReservedBits, Warning, "Reader: ObjFlags sets reserved bits 2..31", "CiA 306-1 Table 8"),
        Rule(ParseDiagnosticCodes.IniObjectListCountMismatch, Warning, "Reader: SupportedObjects disagrees with the numbered entries", ObjectListEntries),
        Rule(ParseDiagnosticCodes.InvalidModuleObjExtend, Warning, "Reader: malformed module ObjExtend is ignored", null),
        Rule(ParseDiagnosticCodes.InvalidObjectListCount, Warning, "Reader: malformed object list count, read as 0", ObjectListEntries),
        Rule(ParseDiagnosticCodes.InvalidObjectIndex, Warning, "Reader: malformed object list index is skipped", ObjectListEntries),
        Rule(ParseDiagnosticCodes.InvalidDynamicChannelPpOffset, Warning, "Reader: malformed DynamicChannels PPOffset", null),
        Rule(ParseDiagnosticCodes.IniDuplicateDeviceCommissioning, Warning, "Reader: both [DeviceComissioning] and [DeviceCommissioning] are present", Commissioning),
        Rule(ParseDiagnosticCodes.InvalidNodeId, Warning, "Reader: malformed or out-of-range NodeID", Commissioning),
        Rule(ParseDiagnosticCodes.InvalidBaudrate, Warning, "Reader: malformed Baudrate, read as 250", Commissioning),
        Rule(ParseDiagnosticCodes.InvalidNetNumber, Warning, "Reader: malformed NetNumber, read as 0", Commissioning),
        Rule(ParseDiagnosticCodes.InvalidLssSerialNumber, Warning, "Reader: malformed LSS_SerialNumber is ignored", Commissioning),
        Rule(ParseDiagnosticCodes.InvalidDeviceInfoNumber, Warning, "Reader: malformed numeric DeviceInfo entry", "CiA 306-1 Table 2"),
        Rule(ParseDiagnosticCodes.InvalidDynamicChannelCount, Warning, "Reader: malformed DynamicChannels NrOfSeg, read as 0", null),
        Rule(ParseDiagnosticCodes.InvalidCommentLineCount, Warning, "Reader: malformed Comments Lines, read as 0", "CiA 306-1 Table 9"),
        Rule(ParseDiagnosticCodes.InvalidCompactListCount, Warning, "Reader: malformed NrOfEntries of a compact list", null),
        Rule(ParseDiagnosticCodes.InvalidToolCount, Warning, "Reader: malformed Tools Items, read as 0", null),
        Rule(ParseDiagnosticCodes.InvalidModuleCount, Warning, "Reader: malformed module NrOfEntries, read as 0", null),
        Rule(ParseDiagnosticCodes.InvalidModuleVersion, Warning, "Reader: malformed module ProductVersion/ProductRevision", "CiA 306-1 Table 14"),
        Rule(ParseDiagnosticCodes.InvalidDynamicChannelType, Warning, "Reader: malformed DynamicChannels Type, read as 0", null),
    };

    private static readonly Dictionary<string, ValidationRule> ById =
        Rules.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets every rule, grouped by rule family.</summary>
    public static IReadOnlyList<ValidationRule> All { get; } = Array.AsReadOnly(Rules);

    /// <summary>Looks up a rule by its id (case-insensitive).</summary>
    /// <param name="id">Rule id, for example <c>VAL001</c>.</param>
    /// <param name="rule">The rule, or <see langword="null"/> when the id is unknown.</param>
    /// <returns><see langword="true"/> when the id is in the catalog.</returns>
    public static bool TryGet(string id, [NotNullWhen(true)] out ValidationRule? rule)
    {
        if (id is null)
        {
            rule = null;
            return false;
        }

        return ById.TryGetValue(id, out rule);
    }

    private static ValidationRule Rule(string id, IniValidationSeverity severity, string title, string? specReference) =>
        new(id, severity, title, specReference, null);
}
