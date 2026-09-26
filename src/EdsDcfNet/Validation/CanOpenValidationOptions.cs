namespace EdsDcfNet.Validation;

/// <summary>
/// Options that control which optional rule sets <see cref="CanOpenModelValidator"/> applies.
/// </summary>
/// <remarks>
/// The default options keep the historic behavior: a freshly constructed model validates
/// clean, so <see cref="CanOpenWriteOptions.Validated"/> writes of minimal or programmatically
/// built models keep working. Stricter CiA 306 conformance checks are opt-in.
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
    public static CanOpenValidationOptions Strict { get; } = new() { RequireMandatoryEntries = true };

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
