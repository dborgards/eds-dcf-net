namespace EdsDcfNet.Models;

/// <summary>
/// Represents the [DeviceCommissioning] section of a DCF file.
/// Contains configuration information for a specific device instance.
/// </summary>
public class DeviceCommissioning
{
    /// <summary>
    /// Device's address/Node-ID (Unsigned8).
    /// </summary>
    public byte NodeId { get; set; }

    /// <summary>
    /// Node name (max 246 characters).
    /// </summary>
    public string NodeName { get; set; } = string.Empty;

    /// <summary>
    /// Device's baudrate in kbit/s (Unsigned16).
    /// Common values: 10, 20, 50, 125, 250, 500, 800, 1000
    /// </summary>
    public ushort Baudrate { get; set; }

    /// <summary>
    /// Network number (Unsigned32).
    /// </summary>
    public uint NetNumber { get; set; }

    /// <summary>
    /// Name of the network (max 243 characters).
    /// </summary>
    public string NetworkName { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if device is the CANopen manager (boolean, 1 = manager, 0 = not manager).
    /// </summary>
    public bool CANopenManager { get; set; }

    /// <summary>
    /// Serial number according to identity object sub 4 (Unsigned32).
    /// Used for LSS (Layer Setting Services).
    /// </summary>
    /// <remarks>
    /// CiA 306 DCF key <c>LSS_SerialNumber</c>. Not part of the CiA 311
    /// <c>deviceCommissioning</c> schema; <see cref="Writers.XdcWriter"/> omits this
    /// property when writing XDC.
    /// </remarks>
    public uint? LssSerialNumber { get; set; }

    /// <summary>
    /// Node reference designator (max 249 characters).
    /// </summary>
    /// <remarks>
    /// CiA 306 DCF key <c>NodeRefd</c>. Not part of the CiA 311
    /// <c>deviceCommissioning</c> schema; <see cref="Writers.XdcWriter"/> omits this
    /// property when writing XDC.
    /// </remarks>
    public string? NodeRefd { get; set; }

    /// <summary>
    /// Network reference designator (max 249 characters).
    /// </summary>
    /// <remarks>
    /// CiA 306 DCF key <c>NetRefd</c>. Not part of the CiA 311
    /// <c>deviceCommissioning</c> schema; <see cref="Writers.XdcWriter"/> omits this
    /// property when writing XDC.
    /// </remarks>
    public string? NetRefd { get; set; }
}
