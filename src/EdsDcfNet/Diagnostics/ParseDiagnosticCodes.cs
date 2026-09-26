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

    /// <summary>Malformed XDD/XDC <c>dummyUsage</c> entry; lenient mode ignores or degrades it.</summary>
    public const string XddInvalidDummyUsage = "XDD_INVALID_DUMMY_USAGE";
}
