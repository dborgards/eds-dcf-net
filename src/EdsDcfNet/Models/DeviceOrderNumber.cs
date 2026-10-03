namespace EdsDcfNet.Models;

/// <summary>
/// One <c>orderNumber</c> element of the CiA 311 <c>DeviceIdentity</c> (XDD/XDC).
/// </summary>
/// <remarks>
/// CiA 311 allows any number of order numbers, for example the products of a device family. The
/// entries of <see cref="DeviceInfo.OrderNumbers"/> keep their order.
/// </remarks>
public class DeviceOrderNumber
{
    /// <summary>
    /// Order number text (<c>xsd:string</c>), kept exactly as read.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Whether the value is read-only for a user (<c>readOnly</c> attribute, default <see langword="true"/>).
    /// </summary>
    public bool ReadOnly { get; set; } = true;
}
