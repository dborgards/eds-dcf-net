namespace EdsDcfNet.Validation;

/// <summary>
/// Stable machine-readable identifiers for <see cref="ValidationIssue.Code"/>.
/// </summary>
public static class ValidationIssueCodes
{
    /// <summary>
    /// INI section name, key, or value that would not survive reading the written file back.
    /// Covers C0 controls, a tab outside a value, leading or trailing whitespace, and separators
    /// the INI parser consumes (<c>=</c>, <c>[</c>, <c>]</c>, and a key that starts with <c>;</c>).
    /// </summary>
    public const string IniTextNotRoundTrippable = "INI_TEXT_NOT_ROUND_TRIPPABLE";

    /// <summary>
    /// EDS/DCF object of type VAR, DEFTYPE, or DOMAIN has sub-objects. CiA 306-1 Table 7 does
    /// not support <c>SubNumber</c> for these object types. An unvalidated write still emits it
    /// so the sub-objects are read back. Checked only on a validated EDS or DCF write.
    /// </summary>
    public const string IniSubObjectsNotSupported = "INI_SUB_OBJECTS_NOT_SUPPORTED";

    /// <summary>
    /// XDD/XDC <c>ObjFlags</c> sets CiA 311 reserved bits 3..31. Checked only on a
    /// validated XDD or XDC write. Bit 2 remains valid (CiA 311: change of value takes
    /// effect after reset). The EDS/DCF limit, which also reserves bit 2, is separate.
    /// </summary>
    public const string XddObjFlagsReservedBits = "XDD_OBJ_FLAGS_RESERVED_BITS";

    /// <summary>
    /// XDD/XDC <c>ObjFlags</c> still carries a preserved <c>xsd:hexBinary</c> value that
    /// does not fit in 32 bits. Checked only on a validated XDD or XDC write.
    /// </summary>
    public const string XddObjFlagsExceedsUInt32 = "XDD_OBJ_FLAGS_EXCEEDS_UINT32";

    /// <summary>
    /// XDD/XDC <c>fileName</c> or <c>fileCreator</c> is empty. The attribute is required, and an
    /// empty string is schema-valid, so it is written; a validated write reports it because the
    /// value says nothing. Checked only on a validated XDD or XDC write.
    /// </summary>
    public const string XddFileTextEmpty = "XDD_FILE_TEXT_EMPTY";

    /// <summary>
    /// XDD/XDC <c>fileCreationDate</c> is required but <see cref="Models.EdsFileInfo.CreationDate"/> is empty.
    /// A date is never invented. Checked only on a validated XDD or XDC write.
    /// </summary>
    public const string XddFileCreationDateMissing = "XDD_FILE_CREATION_DATE_MISSING";

    /// <summary>
    /// A file date is not a valid calendar date (<c>MM-DD-YYYY</c> or <c>xsd:date</c>), so it cannot
    /// be written as <c>xsd:date</c>. Applies to <c>fileCreationDate</c> and
    /// <c>fileModificationDate</c>. Checked only on a validated XDD or XDC write.
    /// </summary>
    public const string XddFileDateInvalid = "XDD_FILE_DATE_INVALID";

    /// <summary>
    /// A file time is neither <c>xsd:time</c> nor the EDS <c>hh:mmAM/PM</c> form, so it cannot be
    /// written as <c>xsd:time</c>. Applies to <c>fileCreationTime</c> and
    /// <c>fileModificationTime</c>. Checked only on a validated XDD or XDC write.
    /// </summary>
    public const string XddFileTimeInvalid = "XDD_FILE_TIME_INVALID";

    /// <summary>
    /// The object dictionary has no object, but <c>CANopenObjectList</c> requires at least one
    /// <c>CANopenObject</c>. Checked only on a validated XDD or XDC write.
    /// </summary>
    public const string XddObjectDictionaryEmpty = "XDD_OBJECT_DICTIONARY_EMPTY";

    /// <summary>
    /// XDC <c>deviceCommissioning/@actualBaudRate</c> is required, but the baud rate is not set and
    /// no read value is preserved, so an empty string would be written. Checked only on a validated
    /// XDC write.
    /// </summary>
    public const string XddActualBaudRateNotSet = "XDD_ACTUAL_BAUD_RATE_NOT_SET";

    /// <summary>
    /// A <see cref="Models.Comments.CommentLines"/> entry cannot be carried as an XML comment in XDD/XDC:
    /// its text holds a character that is not valid in XML, or its line number is outside 1..65535.
    /// An unvalidated write leaves the line out. Checked only on a validated XDD or XDC write.
    /// </summary>
    public const string XddCommentLineNotRepresentable = "XDD_COMMENT_LINE_NOT_REPRESENTABLE";

    /// <summary>
    /// EDS/DCF <c>ObjFlags</c> sets reserved bits 2..31. CiA 306-1 Table 8 defines only bit 0 (refuse
    /// write on download) and bit 1 (refuse read on scan). Checked only on a validated EDS or DCF write;
    /// a validated XDD or XDC write allows bit 2 (<see cref="XddObjFlagsReservedBits"/> reserves 3..31).
    /// </summary>
    public const string IniObjFlagsReservedBits = "INI_OBJ_FLAGS_RESERVED_BITS";

    /// <summary>
    /// EDS/DCF text that exceeds a CiA 306-1 length limit: a comment <c>Line&lt;n&gt;</c> (249 characters,
    /// 248 in <c>[MxComments]</c>), <c>ParamRefd</c> (249), <c>UploadFile</c> (244) or <c>DownloadFile</c> (242).
    /// Checked only on a validated EDS or DCF write, and only for what the format writes.
    /// </summary>
    public const string IniValueTooLong = "INI_VALUE_TOO_LONG";

    /// <summary>
    /// An index in <c>OptionalObjects</c> or <c>ManufacturerObjects</c> lies outside the range of that list
    /// (CiA 306-1 Table 4). Reported only with <see cref="CanOpenValidationOptions.CheckObjectListRanges"/>.
    /// </summary>
    public const string ObjectListIndexOutOfRange = "OBJECT_LIST_INDEX_OUT_OF_RANGE";

    /// <summary>
    /// An EDS/DCF line holds a character outside ISO/IEC 646 (CiA 306-1 clause 6.2). Reported only with
    /// <see cref="CanOpenValidationOptions.RequireIso646"/>.
    /// </summary>
    public const string Iso646CharacterNotAllowed = "ISO646_CHARACTER_NOT_ALLOWED";

    /// <summary>
    /// An EDS/DCF line is longer than 255 characters (CiA 306-1 clause 6.2). Reported only with
    /// <see cref="CanOpenValidationOptions.CheckLineLength"/>.
    /// </summary>
    public const string LineTooLong = "LINE_TOO_LONG";
}
