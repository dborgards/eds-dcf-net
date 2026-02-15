namespace EdsDcfNet.Models;

/// <summary>
/// Represents the object dictionary of a CANopen device.
/// Contains mandatory, optional, and manufacturer-specific objects.
/// </summary>
public class ObjectDictionary
{
    /// <summary>
    /// Mandatory objects (at least 1000h and 1001h).
    /// </summary>
    public List<ushort> MandatoryObjects { get; set; } = new();

    /// <summary>
    /// Optional objects (area 1000h-1FFFh and 6000h-FFFFh).
    /// </summary>
    public List<ushort> OptionalObjects { get; set; } = new();

    /// <summary>
    /// Manufacturer specific objects (area 2000h-5FFFh).
    /// </summary>
    public List<ushort> ManufacturerObjects { get; set; } = new();

    /// <summary>
    /// All objects indexed by their index.
    /// </summary>
    public Dictionary<ushort, CanOpenObject> Objects { get; set; } = new();

    /// <summary>
    /// Dummy usage for mapping (data type index -> supported).
    /// </summary>
    public Dictionary<ushort, bool> DummyUsage { get; set; } = new();
}

/// <summary>
/// Represents a CANopen object in the object dictionary.
/// </summary>
public class CanOpenObject
{
    /// <summary>
    /// Object index in hexadecimal.
    /// </summary>
    public ushort Index { get; set; }

    /// <summary>
    /// Parameter name (up to 241 characters).
    /// </summary>
    public string ParameterName { get; set; } = string.Empty;

    /// <summary>
    /// Object code per CiA DS 306:
    /// 0x0 = NULL, 0x2 = DOMAIN, 0x5 = DEFTYPE, 0x6 = DEFSTRUCT,
    /// 0x7 = VAR, 0x8 = ARRAY, 0x9 = RECORD.
    /// </summary>
    public byte ObjectType { get; set; } = 0x7; // Default: VAR

    /// <summary>
    /// Index of the data type in the object dictionary.
    /// </summary>
    public ushort? DataType { get; set; }

    /// <summary>
    /// Access type (ro, wo, rw, rwr, rww, const).
    /// </summary>
    public AccessType AccessType { get; set; }

    /// <summary>
    /// Default value for this object.
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Lowest limit of the object value (only if applicable).
    /// </summary>
    public string? LowLimit { get; set; }

    /// <summary>
    /// Upper limit of the object value (only if applicable).
    /// </summary>
    public string? HighLimit { get; set; }

    /// <summary>
    /// Whether the object can be mapped into a PDO (Boolean, 0 = not mappable, 1 = mappable).
    /// </summary>
    public bool PdoMapping { get; set; }

    /// <summary>
    /// Special behavior flags (Unsigned32).
    /// Bit 0: Refuse write on download
    /// Bit 1: Refuse read on scan
    /// </summary>
    public uint ObjFlags { get; set; }

    /// <summary>
    /// Number of sub-indexes available at this index (Unsigned8).
    /// Not counting sub-index FFh.
    /// </summary>
    public byte? SubNumber { get; set; }

    /// <summary>
    /// Sub-objects if this is a DEFSTRUCT, ARRAY, or RECORD.
    /// </summary>
    public Dictionary<byte, CanOpenSubObject> SubObjects { get; set; } = new();

    /// <summary>
    /// For compact sub-object storage: number of sub-indexes with equal description.
    /// </summary>
    public byte? CompactSubObj { get; set; }

    /// <summary>
    /// Object links (related objects grouped together).
    /// </summary>
    public List<ushort> ObjectLinks { get; set; } = new();

    /// <summary>
    /// For DCF files: configured parameter value.
    /// </summary>
    public string? ParameterValue { get; set; }

    /// <summary>
    /// For DCF files: application specific name (Denotation).
    /// </summary>
    public string? Denotation { get; set; }

    /// <summary>
    /// For domain objects: file for upload operations.
    /// </summary>
    public string? UploadFile { get; set; }

    /// <summary>
    /// For domain objects: file for download operations.
    /// </summary>
    public string? DownloadFile { get; set; }

    /// <summary>
    /// Whether the object can be mapped into an SRDO (Boolean, 0 = not mappable, 1 = mappable).
    /// </summary>
    public bool SrdoMapping { get; set; }

    /// <summary>
    /// Index and sub-index of the inverted SRAD (hex string, e.g. "0x610101").
    /// </summary>
    public string? InvertedSrad { get; set; }

    /// <summary>
    /// For DCF files: parameter reference designator (max 249 characters).
    /// </summary>
    public string? ParamRefd { get; set; }
}

/// <summary>
/// Represents a sub-object of a CANopen object.
/// </summary>
public class CanOpenSubObject
{
    /// <summary>
    /// Sub-index in hexadecimal.
    /// </summary>
    public byte SubIndex { get; set; }

    /// <summary>
    /// Parameter name.
    /// </summary>
    public string ParameterName { get; set; } = string.Empty;

    /// <summary>
    /// Object type per CiA DS 306 (usually 0x7 = VAR).
    /// See <see cref="CanOpenObject.ObjectType"/> for all defined values.
    /// </summary>
    public byte ObjectType { get; set; } = 0x7;

    /// <summary>
    /// Data type index.
    /// </summary>
    public ushort DataType { get; set; }

    /// <summary>
    /// Access type.
    /// </summary>
    public AccessType AccessType { get; set; }

    /// <summary>
    /// Default value.
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Low limit.
    /// </summary>
    public string? LowLimit { get; set; }

    /// <summary>
    /// High limit.
    /// </summary>
    public string? HighLimit { get; set; }

    /// <summary>
    /// PDO mapping capability.
    /// </summary>
    public bool PdoMapping { get; set; }

    /// <summary>
    /// For DCF files: configured parameter value.
    /// </summary>
    public string? ParameterValue { get; set; }

    /// <summary>
    /// For DCF files: application specific name.
    /// </summary>
    public string? Denotation { get; set; }

    /// <summary>
    /// Whether the sub-object can be mapped into an SRDO (Boolean, 0 = not mappable, 1 = mappable).
    /// </summary>
    public bool SrdoMapping { get; set; }

    /// <summary>
    /// Index and sub-index of the inverted SRAD (hex string, e.g. "0x610101").
    /// </summary>
    public string? InvertedSrad { get; set; }

    /// <summary>
    /// For DCF files: parameter reference designator (max 249 characters).
    /// </summary>
    public string? ParamRefd { get; set; }
}

/// <summary>
/// Access type for CANopen objects.
/// </summary>
public enum AccessType
{
    /// <summary>
    /// Read only.
    /// </summary>
    ReadOnly,

    /// <summary>
    /// Write only.
    /// </summary>
    WriteOnly,

    /// <summary>
    /// Read/Write.
    /// </summary>
    ReadWrite,

    /// <summary>
    /// Read/Write on process input.
    /// </summary>
    ReadWriteInput,

    /// <summary>
    /// Read/Write on process output.
    /// </summary>
    ReadWriteOutput,

    /// <summary>
    /// Constant value.
    /// </summary>
    Constant
}
