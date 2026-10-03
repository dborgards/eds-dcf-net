namespace EdsDcfNet.Models;

/// <summary>
/// Represents the [DeviceInfo] section of an EDS/DCF file.
/// Contains general device information.
/// </summary>
public class DeviceInfo
{
    /// <summary>
    /// Vendor name (max 244 characters).
    /// </summary>
    public string VendorName { get; set; } = string.Empty;

    /// <summary>
    /// Unique vendor ID according to identity object sub-index 01h (Unsigned32).
    /// </summary>
    public uint VendorNumber { get; set; }

    /// <summary>
    /// Product name (max 243 characters).
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Product code according to identity object sub-index 02h (Unsigned32).
    /// </summary>
    public uint ProductNumber { get; set; }

    /// <summary>
    /// Product revision number according to identity object sub-index 03h (Unsigned32).
    /// </summary>
    public uint RevisionNumber { get; set; }

    /// <summary>
    /// Order code for this product (max 245 characters).
    /// </summary>
    public string OrderCode { get; set; } = string.Empty;

    /// <summary>
    /// Supported baud rates (Boolean, 0 = not supported, 1 = supported).
    /// </summary>
    public BaudRates SupportedBaudRates { get; set; } = new();

    /// <summary>
    /// Simple boot-up master functionality (Boolean, 0 = not supported, 1 = supported).
    /// </summary>
    public bool SimpleBootUpMaster { get; set; }

    /// <summary>
    /// Simple boot-up slave functionality (Boolean, 0 = not supported, 1 = supported).
    /// </summary>
    public bool SimpleBootUpSlave { get; set; }

    /// <summary>
    /// Granularity allowed for the mapping on this device (Unsigned8; 0 - mapping not modifiable, 1-64 granularity).
    /// Most existing devices support a granularity of 8.
    /// </summary>
    public byte Granularity { get; set; } = 8;

    /// <summary>
    /// Facility of dynamic variable generation (Unsigned8).
    /// If the value is unequal to 0, the additional section DynamicChannels exists.
    /// </summary>
    public byte DynamicChannelsSupported { get; set; }

    /// <summary>
    /// Facility of multiplexed PDOs (Boolean, 0 = not supported, 1 = supported).
    /// </summary>
    public bool GroupMessaging { get; set; }

    /// <summary>
    /// Number of supported receive PDOs (Unsigned16).
    /// </summary>
    public ushort NrOfRxPdo { get; set; }

    /// <summary>
    /// Number of supported transmit PDOs (Unsigned16).
    /// </summary>
    public ushort NrOfTxPdo { get; set; }

    /// <summary>
    /// LSS functionality supported (Boolean, 0 = not supported, 1 = supported).
    /// </summary>
    public bool LssSupported { get; set; }

    /// <summary>
    /// Implemented sub-indexes of the PDO communication parameter objects as a bitmask (Unsigned8).
    /// Used for compact PDO storage.
    /// </summary>
    public byte CompactPdo { get; set; }

    /// <summary>
    /// CANopen Safety supported according to EN 50325-5 (Boolean, 0 = not supported, 1 = supported).
    /// </summary>
    public bool CANopenSafetySupported { get; set; }

    /// <summary>
    /// Entries of the <c>[DeviceInfo]</c> section that the reader does not map onto a
    /// property, in file order. Keys compare case-insensitively.
    /// </summary>
    /// <remarks>
    /// CiA 306-1 allows additional entries inside the standard sections "in order to support
    /// future extensions" (§ 6.2). The EDS/DCF writers emit these entries after the keys they
    /// generate for the section. A key the writer already generates for this section is not
    /// written a second time. This includes the entries CiA 306-1 § 6.5 reserves for compatibility, such as <c>ProductVersion</c>, <c>ProductRevision</c>, <c>LMT_ManufacturerName</c>, <c>LMT_ProductName</c>, <c>ExtendedBootUpMaster</c> and <c>ExtendedBootUpSlave</c>.
    /// </remarks>
    public OrderedStringDictionary RemainingEntries { get; } = new();
}

/// <summary>
/// Supported baud rates for CANopen devices.
/// </summary>
public class BaudRates
{
    /// <summary>
    /// Supports 10 kbit/s baud rate.
    /// </summary>
    public bool BaudRate10 { get; set; }

    /// <summary>
    /// Supports 20 kbit/s baud rate.
    /// </summary>
    public bool BaudRate20 { get; set; }

    /// <summary>
    /// Supports 50 kbit/s baud rate.
    /// </summary>
    public bool BaudRate50 { get; set; }

    /// <summary>
    /// Supports 125 kbit/s baud rate.
    /// </summary>
    public bool BaudRate125 { get; set; }

    /// <summary>
    /// Supports 250 kbit/s baud rate.
    /// </summary>
    public bool BaudRate250 { get; set; }

    /// <summary>
    /// Supports 500 kbit/s baud rate.
    /// </summary>
    public bool BaudRate500 { get; set; }

    /// <summary>
    /// Supports 800 kbit/s baud rate.
    /// </summary>
    public bool BaudRate800 { get; set; }

    /// <summary>
    /// Supports 1000 kbit/s baud rate.
    /// </summary>
    public bool BaudRate1000 { get; set; }
}
