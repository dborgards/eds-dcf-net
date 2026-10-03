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
    /// EDS/DCF section whose name is a hexadecimal object index, but that index is not
    /// listed in <c>MandatoryObjects</c>, <c>OptionalObjects</c>, or <c>ManufacturerObjects</c>.
    /// The section is not loaded as an object. Lenient mode keeps the original section in
    /// <c>AdditionalSections</c>; strict mode throws.
    /// </summary>
    public const string IniUnlistedObjectSection = "INI_UNLISTED_OBJECT_SECTION";

    /// <summary>
    /// EDS/DCF module section (<c>[MxModuleInfo]</c>, <c>[MxFixedObjects]</c>, <c>[MxFixedxxxx]</c>,
    /// <c>[MxSubExtends]</c>, <c>[MxSubExtxxxx]</c>, <c>[MxComments]</c>) of a module that is not
    /// parsed: its number is outside <c>[SupportedModules]</c> <c>NrOfEntries</c>, or the module
    /// has no <c>[MxModuleInfo]</c>. Reported once per module number. Lenient mode keeps the
    /// original sections in <c>AdditionalSections</c>; strict mode throws.
    /// </summary>
    public const string IniUnlistedModuleSection = "INI_UNLISTED_MODULE_SECTION";

    /// <summary>
    /// EDS/DCF companion section (<c>[xxxxsubN]</c>, <c>[xxxxName]</c>, <c>[xxxxObjectLinks]</c>,
    /// DCF <c>[xxxxValue]</c> / <c>[xxxxDenotation]</c>, or a differently spelled body) of an
    /// object index that an object list cites, but that has no <c>[xxxx]</c> section, so no
    /// object is loaded. Reported once per object index. Lenient mode keeps the original
    /// sections in <c>AdditionalSections</c>; strict mode throws.
    /// </summary>
    public const string IniOrphanCompanionSection = "INI_ORPHAN_COMPANION_SECTION";

    /// <summary>
    /// EDS/DCF <c>[xxxxsubN]</c> section of a loaded object that is not loaded as a sub-object:
    /// the object has no <c>SubNumber</c> or <c>CompactSubObj</c> and is not a RECORD/ARRAY,
    /// or the section name is not the spelling the reader looks up. Reported once per object
    /// index. Lenient mode keeps the original sections in <c>AdditionalSections</c>; strict
    /// mode throws.
    /// </summary>
    public const string IniOrphanSubObjectSection = "INI_ORPHAN_SUB_OBJECT_SECTION";

    /// <summary>
    /// EDS/DCF <c>FileVersion</c>/<c>FileRevision</c> in major/minor tooling form
    /// (for example <c>1.0</c>); lenient mode uses the major component.
    /// </summary>
    public const string IniVersionMajorMinor = "INI_VERSION_MAJOR_MINOR";

    /// <summary>
    /// EDS/DCF object or sub-object section contains a key that CiA 306-1 Table 7 marks as not
    /// supported ("n") for its <c>ObjectType</c>, for example <c>AccessType</c> on a RECORD
    /// without <c>CompactSubObj</c>, <c>SubNumber</c> on a VAR, or <c>PDOMapping</c> on a DOMAIN.
    /// Lenient mode reads the value and reports a warning; the INI writers omit the key.
    /// Strict mode throws.
    /// </summary>
    public const string IniObjectKeyNotSupported = "INI_OBJECT_KEY_NOT_SUPPORTED";

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
    /// EDS/DCF <c>ObjFlags</c> sets reserved bits 2..31 (CiA 306-1 Table 8 defines bit 0 and bit 1). The
    /// value is kept. Reported in lenient and strict mode. XDD/XDC use <see cref="XddObjFlagsReservedBits"/>
    /// with a different limit, because CiA 311 also defines bit 2.
    /// </summary>
    public const string IniObjFlagsReservedBits = "INI_OBJ_FLAGS_RESERVED_BITS";

    /// <summary>
    /// <c>SupportedObjects</c> of an EDS/DCF object list disagrees with the numbered entries: an entry
    /// above the count, or an empty or missing entry inside it (CiA 306-1 Table 5). The entries above the
    /// count are not loaded as objects and stay in <c>SectionRemainingEntries</c>. Reported in lenient and
    /// strict mode.
    /// </summary>
    public const string IniObjectListCountMismatch = "INI_OBJECT_LIST_COUNT_MISMATCH";

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

    /// <summary>
    /// Malformed <c>[DynamicChannels]</c> <c>PPOffset&lt;n&gt;</c>: not <c>offset</c> or
    /// <c>offset, addressDifference</c> with unsigned 32-bit values (CiA 306-3 § 5.2.2).
    /// Lenient mode treats the offset as <c>0</c> without an address difference.
    /// </summary>
    public const string InvalidDynamicChannelPpOffset = "INVALID_DYNAMIC_CHANNEL_PPOFFSET";

    /// <summary>
    /// Both <c>[DeviceComissioning]</c> (CiA 306-1 § 7.3.5, normative) and <c>[DeviceCommissioning]</c>
    /// are present. The normative section is read; the other one is kept unchanged in
    /// <c>AdditionalSections</c>. Strict mode throws.
    /// </summary>
    public const string IniDuplicateDeviceCommissioning = "INI_DUPLICATE_DEVICE_COMMISSIONING";

    /// <summary>
    /// Malformed or out-of-range <c>NodeID</c> in the DCF <c>[DeviceComissioning]</c> section.
    /// Lenient mode treats an unreadable value as <c>1</c> (the absent-key default) and keeps a
    /// readable value outside <c>1..127</c> for validation to report.
    /// </summary>
    public const string InvalidNodeId = "INVALID_NODE_ID";

    /// <summary>Malformed <c>Baudrate</c> in <c>[DeviceComissioning]</c>; lenient mode treats it as <c>250</c>.</summary>
    public const string InvalidBaudrate = "INVALID_BAUDRATE";

    /// <summary>Malformed <c>NetNumber</c> in <c>[DeviceComissioning]</c>; lenient mode treats it as <c>0</c>.</summary>
    public const string InvalidNetNumber = "INVALID_NET_NUMBER";

    /// <summary>Malformed <c>LSS_SerialNumber</c> in <c>[DeviceComissioning]</c>; lenient mode leaves it unset.</summary>
    public const string InvalidLssSerialNumber = "INVALID_LSS_SERIAL_NUMBER";

    /// <summary>
    /// Malformed numeric key in <c>[DeviceInfo]</c> (CiA 306-1 § 6.5), such as <c>VendorNumber</c>
    /// or <c>NrOfRXPDO</c>; lenient mode uses the value the key has when absent.
    /// </summary>
    public const string InvalidDeviceInfoNumber = "INVALID_DEVICE_INFO_NUMBER";

    /// <summary>Malformed <c>[DynamicChannels]</c> <c>NrOfSeg</c>; lenient mode treats it as <c>0</c>.</summary>
    public const string InvalidDynamicChannelCount = "INVALID_DYNAMIC_CHANNEL_COUNT";

    /// <summary>
    /// Malformed EDS/DCF <c>[Comments]</c> or <c>[MxComments]</c> <c>Lines</c> (UNSIGNED16, CiA 306-1
    /// Tables 9 and 15); lenient mode treats it as <c>0</c> and keeps the <c>Line&lt;n&gt;</c> entries
    /// as remaining entries.
    /// </summary>
    public const string InvalidCommentLineCount = "INVALID_COMMENT_LINE_COUNT";

    /// <summary>
    /// Malformed EDS/DCF <c>[Tools]</c> <c>Items</c>; lenient mode treats it as <c>0</c>, so the
    /// <c>[Tool&lt;n&gt;]</c> sections are kept in <c>AdditionalSections</c>.
    /// </summary>
    public const string InvalidToolCount = "INVALID_TOOL_COUNT";

    /// <summary>
    /// Malformed EDS/DCF <c>[SupportedModules]</c> or DCF <c>[ConnectedModules]</c> <c>NrOfEntries</c>
    /// (UNSIGNED16, CiA 306-1 Tables 13 and 18); lenient mode treats it as <c>0</c> and keeps the
    /// numbered entries as remaining entries.
    /// </summary>
    public const string InvalidModuleCount = "INVALID_MODULE_COUNT";

    /// <summary>
    /// Malformed EDS/DCF <c>[MxModuleInfo]</c> <c>ProductVersion</c> or <c>ProductRevision</c>
    /// (UNSIGNED8, CiA 306-1 Table 14); lenient mode uses the absent-key default (<c>1</c> or <c>0</c>).
    /// </summary>
    public const string InvalidModuleVersion = "INVALID_MODULE_VERSION";

    /// <summary>Malformed <c>[DynamicChannels]</c> <c>Type&lt;n&gt;</c>; lenient mode treats it as <c>0</c>.</summary>
    public const string InvalidDynamicChannelType = "INVALID_DYNAMIC_CHANNEL_TYPE";

    /// <summary>XDD/XDC document contains more than one device profile body; lenient mode uses the last one.</summary>
    public const string XddDuplicateDeviceProfile = "XDD_DUPLICATE_DEVICE_PROFILE";

    /// <summary>XDD/XDC document contains more than one communication-network profile body; lenient mode uses the last one.</summary>
    public const string XddDuplicateCommNetProfile = "XDD_DUPLICATE_COMMNET_PROFILE";

    /// <summary>
    /// XDD/XDC <c>fileVersion</c> in major/minor tooling form; the major component is used
    /// (lenient and strict mode).
    /// </summary>
    public const string XddFileVersionMajorMinor = "XDD_FILE_VERSION_MAJOR_MINOR";

    /// <summary>
    /// XDD/XDC <c>fileVersion</c> is a free <c>xsd:string</c> that holds no number in the range of
    /// <see cref="Models.EdsFileInfo.FileVersion"/>. The text is kept in
    /// <see cref="Models.EdsFileInfo.FileVersionText"/> and <c>FileVersion</c> keeps its default, in
    /// both lenient and strict mode.
    /// </summary>
    public const string XddFileVersionNotNumeric = "XDD_FILE_VERSION_NOT_NUMERIC";

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

    /// <summary>
    /// Unknown XDD/XDC baud-rate string; lenient mode treats it as <c>0</c> / ignores it. For
    /// <c>deviceCommissioning/@actualBaudRate</c> (a free string) it is reported in both modes
    /// without throwing and the original text is preserved for writing.
    /// </summary>
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

    /// <summary>
    /// XDC <c>deviceCommissioning/@networkNumber</c> is a valid <c>xsd:unsignedLong</c> that does not
    /// fit in <see cref="EdsDcfNet.Models.DeviceCommissioning.NetNumber"/>. The property stays <c>0</c>
    /// and the original text is preserved for writing. Reported in lenient and strict mode.
    /// </summary>
    public const string XddNetworkNumberExceedsUInt32 = "XDD_NETWORK_NUMBER_EXCEEDS_UINT32";

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

    /// <summary>
    /// A <c>DeviceIdentity/version</c> has a missing or unknown <c>versionType</c> (CiA 311 allows
    /// <c>SW</c>, <c>FW</c> and <c>HW</c>). Lenient mode ignores the element; strict mode throws.
    /// </summary>
    public const string XddUnknownVersionType = "XDD_UNKNOWN_VERSION_TYPE";

    /// <summary>
    /// CPJ <c>NodeXPresent</c> is neither <c>0x00</c> nor <c>0x01</c> (or a recognised alias such as
    /// <c>0</c>, <c>1</c>, <c>true</c>, <c>no</c>) and so is a reserved value (CiA 306-3 Table 3), or is
    /// empty. Lenient mode treats a reserved value as not present and does not load a node whose
    /// entry is empty; strict mode throws.
    /// </summary>
    public const string CpjReservedNodePresent = "CPJ_RESERVED_NODE_PRESENT";

    /// <summary>
    /// CPJ <c>Nodes</c> is not a number in the range 0..127 (CiA 306-3 Table 3). Lenient mode ignores
    /// the declared count; strict mode throws.
    /// </summary>
    public const string CpjInvalidNodes = "CPJ_INVALID_NODES";

    /// <summary>
    /// CPJ <c>Nodes</c> is a valid number without the <c>0x</c> prefix; CiA 306-3 Table 3 codes it
    /// hexadecimal. Lenient mode reads it with the library number convention (decimal, or octal
    /// with a leading <c>0</c>) and reports this code; strict mode throws.
    /// </summary>
    public const string CpjNodesNotHex = "CPJ_NODES_NOT_HEX";

    /// <summary>
    /// A CPJ <c>[Topology]</c> section lacks the mandatory <c>Nodes</c> entry (CiA 306-3 Table 3).
    /// Reported in lenient and strict mode; the writer always emits <c>Nodes</c>.
    /// </summary>
    public const string CpjMissingNodes = "CPJ_MISSING_NODES";

    /// <summary>
    /// The CPJ <c>Nodes</c> count differs from the number of <c>NodeXPresent</c> entries. The
    /// writer emits the number of nodes in the model. Reported in lenient and strict mode.
    /// </summary>
    public const string CpjNodeCountMismatch = "CPJ_NODE_COUNT_MISMATCH";

    /// <summary>
    /// A CPJ topology section has <c>NodeXName</c>, <c>NodeXRefd</c> or <c>NodeXDCFName</c> but no
    /// <c>NodeXPresent</c> (CiA 306-3 Table 3: a missing <c>NodeXPresent</c> means the node is not
    /// present, and the entry is mandatory for each existing node). The node is not loaded; the entry is
    /// kept verbatim in <c>NetworkTopology.RemainingEntries</c> and written back. Reported as a warning in
    /// lenient and strict mode: the information is valid, only incomplete, so strict mode does not throw.
    /// </summary>
    public const string CpjNodeEntryWithoutPresent = "CPJ_NODE_ENTRY_WITHOUT_PRESENT";
}
