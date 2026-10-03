namespace EdsDcfNet.Models;

/// <summary>
/// One typed <c>version</c> element of the CiA 311 <c>DeviceIdentity</c> (XDD/XDC).
/// </summary>
/// <remarks>
/// A device can carry several versions (<c>basicDevice.xdd</c> lists a software, a firmware and a
/// hardware version). The entries of <see cref="DeviceInfo.Versions"/> keep their order.
/// </remarks>
public class DeviceVersion
{
    /// <summary>
    /// Kind of version (<c>versionType</c> attribute: <c>SW</c>, <c>FW</c> or <c>HW</c>).
    /// </summary>
    public DeviceVersionType Type { get; set; }

    /// <summary>
    /// Version text (<c>xsd:string</c>), kept exactly as read.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Whether the value is read-only for a user (<c>readOnly</c> attribute, default <see langword="true"/>).
    /// </summary>
    public bool ReadOnly { get; set; } = true;
}

/// <summary>
/// Kind of a <see cref="DeviceVersion"/> (CiA 311 <c>versionType</c>).
/// </summary>
public enum DeviceVersionType
{
    /// <summary>Software version (<c>SW</c>).</summary>
    Software,

    /// <summary>Firmware version (<c>FW</c>).</summary>
    Firmware,

    /// <summary>Hardware version (<c>HW</c>).</summary>
    Hardware
}
