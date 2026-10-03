namespace EdsDcfNet.Diagnostics;

/// <summary>
/// Stable machine-readable identifiers for <see cref="ParseDiagnostic.Code"/>. The same
/// code is carried by the <see cref="Exceptions.EdsParseException"/> thrown when the
/// condition is hit under <see cref="CanOpenFileOptions.StrictParsing"/>.
/// </summary>
public static class ParseDiagnosticCodes
{
    /// <summary>Duplicate key within an INI section; lenient mode keeps the last value.</summary>
    public const string IniDuplicateKey = "INI_DUPLICATE_KEY";

    /// <summary>
    /// Malformed INI section header (for example <c>[2000</c> or text after the closing bracket).
    /// Lenient mode ignores the header line and the keys that follow it up to the next valid header.
    /// </summary>
    public const string IniMalformedSectionHeader = "INI_MALFORMED_SECTION_HEADER";

    /// <summary>
    /// INI line that is neither blank, comment, section header nor <c>key=value</c> (no <c>=</c>, or an
    /// empty key); lenient mode ignores the line.
    /// </summary>
    public const string IniMissingEquals = "INI_MISSING_EQUALS";

    /// <summary>Section header that repeats an earlier section; lenient mode merges the keys into it.</summary>
    public const string IniDuplicateSection = "INI_DUPLICATE_SECTION";

    /// <summary>
    /// EDS/DCF/CPJ bytes are not valid UTF-8 and were decoded as ISO-8859-1.
    /// Reported in both lenient and strict mode; strict mode does not throw, because the
    /// file is legible legacy text rather than a malformed INI construct.
    /// </summary>
    public const string IniDecodedAsIso88591 = "INI_DECODED_AS_ISO_8859_1";

    /// <summary>Invalid <c>DummyUsage</c> key in an INI file; the entry is ignored.</summary>
    public const string IniInvalidDummyUsageKey = "INI_INVALID_DUMMY_USAGE_KEY";

    /// <summary>
    /// EDS/DCF <c>FileVersion</c>/<c>FileRevision</c> in major/minor tooling form
    /// (for example <c>1.0</c>); lenient mode uses the major component.
    /// </summary>
    public const string IniVersionMajorMinor = "INI_VERSION_MAJOR_MINOR";

    /// <summary>Unknown boolean token; lenient mode treats it as <see langword="false"/>.</summary>
    public const string UnknownBooleanToken = "UNKNOWN_BOOLEAN_TOKEN";

    /// <summary>Unknown access-type token; lenient mode maps it to <c>ro</c>.</summary>
    public const string UnknownAccessTypeToken = "UNKNOWN_ACCESS_TYPE_TOKEN";

    /// <summary>
    /// Malformed EDS/DCF <c>ObjectType</c> on an object or sub-object; lenient mode treats it as VAR (<c>0x7</c>).
    /// </summary>
    public const string InvalidObjectType = "INVALID_OBJECT_TYPE";

    /// <summary>
    /// Malformed EDS/DCF <c>DataType</c>. Lenient mode leaves an object value unset and treats a sub-object value as <c>0</c>.
    /// </summary>
    public const string InvalidDataType = "INVALID_DATA_TYPE";

    /// <summary>
    /// Malformed EDS/DCF <c>SubNumber</c>; lenient mode leaves it unset.
    /// </summary>
    public const string InvalidSubNumber = "INVALID_SUB_NUMBER";

    /// <summary>
    /// Malformed EDS/DCF <c>CompactSubObj</c>; lenient mode leaves it unset.
    /// </summary>
    public const string InvalidCompactSubObj = "INVALID_COMPACT_SUB_OBJ";

    /// <summary>
    /// Malformed EDS/DCF <c>ObjFlags</c>; lenient mode treats it as <c>0</c>.
    /// </summary>
    public const string InvalidObjFlags = "INVALID_OBJ_FLAGS";

    /// <summary>
    /// Malformed <c>ObjExtend</c> in a module <c>[MxSubExtxxxx]</c> section (CiA 306-1 §8.3);
    /// lenient mode leaves it unset.
    /// </summary>
    public const string InvalidModuleObjExtend = "INVALID_MODULE_OBJ_EXTEND";

    /// <summary>
    /// Malformed object-list count (<c>SupportedObjects</c>, <c>ObjectLinks</c>, or module <c>NrOfEntries</c>);
    /// lenient mode treats it as <c>0</c>.
    /// </summary>
    public const string InvalidObjectListCount = "INVALID_OBJECT_LIST_COUNT";

    /// <summary>
    /// Malformed object-list index; lenient mode skips that entry and continues with the rest of the list.
    /// </summary>
    public const string InvalidObjectIndex = "INVALID_OBJECT_INDEX";

    /// <summary>XDD/XDC document contains more than one device profile body; lenient mode uses the last one.</summary>
    public const string XddDuplicateDeviceProfile = "XDD_DUPLICATE_DEVICE_PROFILE";

    /// <summary>XDD/XDC document contains more than one communication-network profile body; lenient mode uses the last one.</summary>
    public const string XddDuplicateCommNetProfile = "XDD_DUPLICATE_COMMNET_PROFILE";

    /// <summary>
    /// XDD/XDC <c>fileVersion</c> in major/minor tooling form; lenient mode uses the major
    /// component.
    /// </summary>
    public const string XddFileVersionMajorMinor = "XDD_FILE_VERSION_MAJOR_MINOR";

    /// <summary><c>CANopenObject</c> without <c>index</c>; lenient mode treats it as <c>0x0000</c>.</summary>
    public const string XddMissingIndex = "XDD_MISSING_INDEX";

    /// <summary><c>CANopenObject</c>/<c>CANopenSubObject</c> without <c>objectType</c>; lenient mode treats it as VAR (0x7).</summary>
    public const string XddMissingObjectType = "XDD_MISSING_OBJECT_TYPE";

    /// <summary>Invalid <c>objectType</c> attribute; lenient mode treats it as VAR (0x7).</summary>
    public const string XddInvalidObjectType = "XDD_INVALID_OBJECT_TYPE";

    /// <summary>Unknown XDD/XDC access-type token; lenient mode maps it to <c>ro</c>.</summary>
    public const string XddUnknownAccessType = "XDD_UNKNOWN_ACCESS_TYPE";

    /// <summary>Unknown XDD/XDC XML boolean token; lenient mode treats it as <see langword="false"/>.</summary>
    public const string XddUnknownXmlBool = "XDD_UNKNOWN_XML_BOOL";

    /// <summary>Unknown XDD/XDC baud-rate string; lenient mode treats it as <c>0</c> / ignores it.</summary>
    public const string XddUnknownBaudRate = "XDD_UNKNOWN_BAUD_RATE";

    /// <summary>Malformed XDD/XDC unsigned numeric attribute; lenient mode ignores it / leaves the value unset.</summary>
    public const string XddInvalidNumericAttribute = "XDD_INVALID_NUMERIC_ATTRIBUTE";

    /// <summary>
    /// XDD/XDC <c>objFlags</c> has an odd number of hex digits. <c>xsd:hexBinary</c> requires
    /// an even count. Lenient mode still accepts the hexadecimal value; strict mode throws.
    /// </summary>
    public const string XddObjFlagsOddHexLength = "XDD_OBJ_FLAGS_ODD_HEX_LENGTH";

    /// <summary>
    /// XDD/XDC <c>objFlags</c> sets CiA 311 reserved bits 3..31 after hexadecimal
    /// interpretation. The value is kept. Reported in lenient and strict mode, because a
    /// multi-digit decimal spelling from a library version before the hexBinary fix can
    /// otherwise change meaning without notice.
    /// </summary>
    public const string XddObjFlagsReservedBits = "XDD_OBJ_FLAGS_RESERVED_BITS";

    /// <summary>
    /// XDD/XDC <c>objFlags</c> is schema-valid <c>xsd:hexBinary</c> that does not fit in
    /// <see cref="EdsDcfNet.Models.CanOpenObject.ObjFlags"/>. The property stays <c>0</c> and the
    /// original text is preserved for writing. Reported in lenient and strict mode.
    /// </summary>
    public const string XddObjFlagsExceedsUInt32 = "XDD_OBJ_FLAGS_EXCEEDS_UINT32";

    /// <summary>Malformed XDD/XDC <c>dummyUsage</c> entry; lenient mode ignores or degrades it.</summary>
    public const string XddInvalidDummyUsage = "XDD_INVALID_DUMMY_USAGE";

    /// <summary>
    /// <c>uniqueIDRef</c> on a CANopen object or sub-object does not identify a
    /// <c>parameter</c> in the application process. Lenient mode leaves the referenced
    /// fields unset; strict mode throws.
    /// </summary>
    public const string XddUnresolvedUniqueIdRef = "XDD_UNRESOLVED_UNIQUE_ID_REF";

    /// <summary>
    /// The referenced parameter's <c>access</c> is <c>noAccess</c>, which has no CiA 306
    /// access type. <c>AccessType</c> is left unchanged. Reported in lenient and strict mode.
    /// </summary>
    public const string XddUniqueIdRefNoAccess = "XDD_UNIQUE_ID_REF_NO_ACCESS";

    /// <summary>
    /// The parameter type, default, or allowed range is only available through
    /// <c>variableRef</c> or <c>templateIDRef</c>. Those chains are not resolved.
    /// Reported in lenient and strict mode.
    /// </summary>
    public const string XddUniqueIdRefIndirect = "XDD_UNIQUE_ID_REF_INDIRECT";

    /// <summary>
    /// A direct <c>dataTypeIDRef</c> does not identify a data type in <c>dataTypeList</c>
    /// (or the chain cycles). Lenient mode leaves <c>DataType</c> unset; strict mode throws.
    /// </summary>
    public const string XddUnresolvedDataTypeIdRef = "XDD_UNRESOLVED_DATA_TYPE_ID_REF";

    /// <summary>
    /// XDD/XDC <c>addressOffset</c> is schema-valid <c>xsd:hexBinary</c> that does not fit
    /// in <see cref="EdsDcfNet.Models.DynamicChannelSegment.PPOffset"/>. The property stays
    /// <c>0</c> and the original text is preserved for writing. Reported in lenient and
    /// strict mode; strict mode does not throw.
    /// </summary>
    public const string XddAddressOffsetExceedsUInt32 = "XDD_ADDRESS_OFFSET_EXCEEDS_UINT32";

    /// <summary>
    /// An attribute written by an older version of this library and absent from the
    /// CiA 311 schema. Lenient mode still applies a recognized value. Strict mode throws.
    /// </summary>
    public const string XddLegacyAttribute = "XDD_LEGACY_ATTRIBUTE";
}
