namespace EdsDcfNet.Models;

/// <summary>
/// Represents module information for modular CANopen devices.
/// Describes extension modules that can be attached to a bus coupler.
/// </summary>
public class ModuleInfo
{
    /// <summary>
    /// Module number (1-based index in SupportedModules list).
    /// </summary>
    public int ModuleNumber { get; set; }

    /// <summary>
    /// Product name (max 243 characters).
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Product version (Unsigned8).
    /// </summary>
    public byte ProductVersion { get; set; }

    /// <summary>
    /// Product revision (Unsigned8).
    /// </summary>
    public byte ProductRevision { get; set; }

    /// <summary>
    /// Manufacturer specific order code (max 245 characters).
    /// </summary>
    public string OrderCode { get; set; } = string.Empty;

    /// <summary>
    /// Fixed objects that are instantiated when at least one module of this type is connected.
    /// </summary>
    public List<ushort> FixedObjects { get; } = new();

    /// <summary>
    /// Object bodies for <see cref="FixedObjects"/>, keyed by index.
    /// Populated from <c>[MxFixedxxxx]</c> and <c>[MxFixedxxxxsubx]</c> (CiA 306-1 §8.3).
    /// </summary>
    public Dictionary<ushort, CanOpenObject> FixedObjectDefinitions { get; } = new();

    /// <summary>
    /// Objects that instantiate new sub-indexes per module.
    /// Populated from <c>[MxSubExtends]</c> (CiA 306-1 §8.3).
    /// </summary>
    public List<ushort> SubExtends { get; } = new();

    /// <summary>
    /// Sub-extension object definitions, keyed by object index.
    /// Populated from <c>[MxSubExtxxxx]</c> (CiA 306-1 §8.3). The section carries the
    /// same entries as a standard EDS object description, plus <c>Count</c> and <c>ObjExtend</c>.
    /// </summary>
    public Dictionary<ushort, ModuleSubExtension> SubExtensionDefinitions { get; } = new();

    /// <summary>
    /// Optional module comments from <c>[MxComments]</c> (CiA 306-1 §8.3).
    /// </summary>
    public Comments? Comments { get; set; }
}

/// <summary>
/// Represents a sub-extension definition for module objects.
/// </summary>
public class ModuleSubExtension
{
    /// <summary>
    /// Object index.
    /// </summary>
    public ushort Index { get; set; }

    /// <summary>
    /// Parameter name.
    /// </summary>
    public string ParameterName { get; set; } = string.Empty;

    /// <summary>
    /// Number of sub-indexes at this index, not counting FFh.
    /// <see langword="null"/> when the section omits <c>SubNumber</c>.
    /// </summary>
    public byte? SubNumber { get; set; }

    /// <summary>
    /// Object code. <see langword="null"/> when the section omits <c>ObjectType</c>,
    /// which CiA 306 treats as VAR (<c>0x7</c>).
    /// </summary>
    public byte? ObjectType { get; set; }

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
    /// Lowest limit of the object value, when the section contains <c>LowLimit</c>.
    /// </summary>
    public string? LowLimit { get; set; }

    /// <summary>
    /// Upper limit of the object value, when the section contains <c>HighLimit</c>.
    /// </summary>
    public string? HighLimit { get; set; }

    /// <summary>
    /// PDO mapping capability.
    /// </summary>
    public bool PdoMapping { get; set; }

    /// <summary>
    /// Special behavior flags (Unsigned32). Zero matches a missing <c>ObjFlags</c> entry.
    /// </summary>
    public uint ObjFlags { get; set; }

    /// <summary>
    /// Compact sub-object template length. <see langword="null"/> or zero when the
    /// section does not use <c>CompactSubObj</c>.
    /// </summary>
    public byte? CompactSubObj { get; set; }

    /// <summary>
    /// Number of extended sub-indexes created per module.
    /// Format: count or "0;bits" for bit-wise assembly.
    /// </summary>
    public string Count { get; set; } = string.Empty;

    /// <summary>
    /// Maximum sub-index after which the next object shall be used.
    /// 0 or missing means next object shall not be used.
    /// </summary>
    public byte? ObjExtend { get; set; }
}
