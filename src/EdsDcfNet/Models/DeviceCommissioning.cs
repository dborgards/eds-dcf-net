namespace EdsDcfNet.Models;

/// <summary>
/// Represents the [DeviceComissioning] section of a DCF file (CiA 306 spelling; the reader also
/// accepts [DeviceCommissioning]).
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
    /// <remarks>
    /// The XDC <c>actualBaudRate</c> attribute is a free string. A read value that is not one of
    /// the listed rates (for example <c>auto-baudRate</c>) leaves this property at <c>0</c>, is
    /// reported as a diagnostic, and is written back unchanged while this property is unchanged.
    /// An XDC written with <c>0</c> and no such value gets an empty <c>actualBaudRate</c>, which is
    /// schema-valid; a validated XDC write rejects it.
    /// </remarks>
    public ushort Baudrate { get; set; }

    /// <summary>Original <c>actualBaudRate</c> text of an XDC read.</summary>
    internal string? ActualBaudRateLexical { get; set; }

    /// <summary><see cref="Baudrate"/> captured when <see cref="ActualBaudRateLexical"/> was stored.</summary>
    internal ushort ActualBaudRateLexicalBaseline { get; set; }

    /// <summary>
    /// Network number (Unsigned32).
    /// </summary>
    /// <remarks>
    /// The XDC <c>networkNumber</c> is an <c>xsd:unsignedLong</c>. A read value above
    /// <see cref="uint.MaxValue"/> leaves this property at <c>0</c>, is reported as a diagnostic, and is
    /// written back unchanged while this property is unchanged.
    /// </remarks>
    public uint NetNumber { get; set; }

    /// <summary>Original <c>networkNumber</c> text of an XDC read.</summary>
    internal string? NetworkNumberLexical { get; set; }

    /// <summary><see cref="NetNumber"/> captured when <see cref="NetworkNumberLexical"/> was stored.</summary>
    internal uint NetworkNumberLexicalBaseline { get; set; }

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
    /// property from the emitted element when NodeId is <c>1..127</c>. Prefer DCF
    /// (not CPJ) when this value must be preserved — CPJ has no serial-number field.
    /// A write with NodeId <c>0</c> and this property set throws
    /// <see cref="Exceptions.XdcWriteException"/> rather than silently dropping it.
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

    /// <summary>
    /// Entries of the <c>[DeviceComissioning]</c> section that the reader does not map onto a
    /// property, in file order. Keys compare case-insensitively.
    /// </summary>
    /// <remarks>
    /// CiA 306-1 allows additional entries inside the standard sections "in order to support
    /// future extensions" (§ 6.2). The EDS/DCF writers emit these entries after the keys they
    /// generate for the section. A key the writer already generates for this section is not
    /// written a second time. The section is written only when the commissioning data is not empty.
    /// </remarks>
    public OrderedStringDictionary RemainingEntries { get; } = new();
}
