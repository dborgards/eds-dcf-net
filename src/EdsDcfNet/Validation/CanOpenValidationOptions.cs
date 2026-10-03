namespace EdsDcfNet.Validation;

/// <summary>
/// Options that control which optional rule sets <see cref="CanOpenModelValidator"/> applies.
/// </summary>
/// <remarks>
/// The default options keep the historic behavior, so existing
/// <see cref="CanOpenWriteOptions.Validated"/> writes and <c>Validate</c> calls report exactly
/// what they reported before. The stricter CiA 306 conformance rule sets are opt-in, either
/// individually or all at once via <see cref="Strict"/>.
/// </remarks>
public sealed class CanOpenValidationOptions
{
    /// <summary>
    /// Gets the default options (no opt-in rule sets enabled).
    /// </summary>
    public static CanOpenValidationOptions Default { get; } = new();

    /// <summary>
    /// Gets options with every opt-in CiA 306 conformance rule set enabled.
    /// </summary>
    public static CanOpenValidationOptions Strict { get; } = new()
    {
        CheckSubNumberCount = true,
        CheckValueRanges = true,
        RequireMandatoryEntries = true,
        RequireIso646 = true,
        CheckLineLength = true,
        CheckObjectListRanges = true,
        CheckObjectListEntries = true,
    };

    /// <summary>
    /// Gets a value indicating whether <c>SubNumber</c> must equal the number of described
    /// sub-indexes including sub-index 00h and excluding sub-index FFh (CiA 306-1 clause 6.6.3.2,
    /// <see cref="Models.CanOpenObject.SubNumber"/>). Objects with a non-zero
    /// <c>CompactSubObj</c> are not reported. A <c>SubNumber</c> of 0 with only sub-index 00h
    /// is reported, because that one sub-index is counted.
    /// </summary>
    public bool CheckSubNumberCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>DefaultValue</c>, <c>LowLimit</c>, <c>HighLimit</c>
    /// and <c>ParameterValue</c> are checked against the entry's integer, BOOLEAN or REAL
    /// <c>DataType</c> (e.g. <c>1000</c> for UNSIGNED8), and whether <c>LowLimit</c> &lt;=
    /// <c>HighLimit</c> holds and default/parameter values lie within the limits.
    /// <c>$NODEID</c> formulas are evaluated with the DCF node-ID, or for node-IDs 1 and 127
    /// in an EDS.
    /// </summary>
    public bool CheckValueRanges { get; init; }

    /// <summary>
    /// Gets a value indicating whether mandatory CiA 306 content is required: the objects
    /// 1000h, 1001h and 1018h present and listed in <c>MandatoryObjects</c> (CiA 306-1 Table 4),
    /// a non-empty <c>ParameterName</c> for every
    /// object and sub-object and a <c>DataType</c> for VAR entries (CiA 306-1 Table 7),
    /// <c>FileName</c>, <c>VendorName</c> and <c>ProductName</c> (CiA 306-1 Tables 1 and 2),
    /// and for DCF files a configured <c>[DeviceComissioning]</c> section with node-ID and
    /// baud rate (CiA 306-1 Table 12).
    /// </summary>
    public bool RequireMandatoryEntries { get; init; }

    /// <summary>
    /// Gets a value indicating whether the text of an EDS or DCF must consist of ISO/IEC 646
    /// (7-bit) characters only (CiA 306-1 clause 6.2). The check runs on the file text the INI
    /// writer would produce and reports each line with a character above U+007F, which includes
    /// every non-ASCII character in names, values, and comments. This library writes UTF-8 by
    /// default, a documented deviation from the specification, so the check is off by default.
    /// It has no effect on the other formats.
    /// </summary>
    public bool RequireIso646 { get; init; }

    /// <summary>
    /// Gets a value indicating whether every line of an EDS or DCF must be at most 255
    /// characters long, including the key, the <c>=</c>, and the value (CiA 306-1 clause 6.2).
    /// The check runs on the file text the INI writer would produce. It has no effect on the
    /// other formats.
    /// </summary>
    public bool CheckLineLength { get; init; }

    /// <summary>
    /// Gets a value indicating whether every index in <c>OptionalObjects</c> lies in
    /// 1000h-1FFFh or 6000h-9FFFh and every index in <c>ManufacturerObjects</c> lies in
    /// 2000h-5FFFh (CiA 306-1 Table 4). The ranges apply to the object lists of every format.
    /// </summary>
    public bool CheckObjectListRanges { get; init; }

    /// <summary>
    /// Gets a value indicating whether an EDS or DCF may keep numbered entries of
    /// <c>[MandatoryObjects]</c>, <c>[OptionalObjects]</c> or <c>[ManufacturerObjects]</c> in
    /// <see cref="Models.ICanOpenFileModel.SectionRemainingEntries"/>. The reader keeps an entry it
    /// does not load there (above <c>SupportedObjects</c>, or empty or invalid inside it), and the
    /// writer emits it again after the generated list. Such an entry is reported when it lies above
    /// the written <c>SupportedObjects</c> (CiA 306-1 Table 5) or when a generated entry with the
    /// same number replaces it. It has no effect on the other formats.
    /// </summary>
    public bool CheckObjectListEntries { get; init; }
}
