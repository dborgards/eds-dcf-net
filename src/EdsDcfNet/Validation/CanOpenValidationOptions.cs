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
    };

    /// <summary>
    /// Gets a value indicating whether <c>SubNumber</c> must equal the number of described
    /// sub-indexes including sub-index 00h and excluding sub-index FFh (CiA 306-1 clause 6.6.3.2,
    /// <see cref="Models.CanOpenObject.SubNumber"/>). Objects with a non-zero
    /// <c>CompactSubObj</c> and the tolerated <c>SubNumber=0</c> with only sub-index 00h are
    /// not reported.
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
    /// 1000h, 1001h and 1018h (CiA 306-1 Table 4), a non-empty <c>ParameterName</c> for every
    /// object and sub-object and a <c>DataType</c> for VAR entries (CiA 306-1 Table 7),
    /// <c>FileName</c>, <c>VendorName</c> and <c>ProductName</c> (CiA 306-1 Tables 1 and 2),
    /// and for DCF files a configured <c>[DeviceComissioning]</c> section with node-ID and
    /// baud rate (CiA 306-1 Table 12).
    /// </summary>
    public bool RequireMandatoryEntries { get; init; }
}
