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
    public List<ushort> FixedObjects { get; set; } = new();

    /// <summary>
    /// Objects indexed by their index that are created once per device.
    /// </summary>
    public Dictionary<ushort, CanOpenObject> FixedObjectDefinitions { get; set; } = new();

    /// <summary>
    /// Objects that instantiate new sub-indexes per module.
    /// </summary>
    public List<ushort> SubExtends { get; set; } = new();

    /// <summary>
    /// Sub-extension object definitions.
    /// </summary>
    public Dictionary<ushort, ModuleSubExtension> SubExtensionDefinitions { get; set; } = new();

    /// <summary>
    /// Optional module comments.
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
    /// PDO mapping capability.
    /// </summary>
    public bool PdoMapping { get; set; }

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
