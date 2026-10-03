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
    /// not support <c>SubNumber</c> for these object types, so the INI writers omit it and the
    /// sub-objects would not be read back. Checked only on a validated EDS or DCF write.
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
}
