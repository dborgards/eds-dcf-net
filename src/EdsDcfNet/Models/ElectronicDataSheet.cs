namespace EdsDcfNet.Models;

/// <summary>
/// Represents a complete Electronic Data Sheet (EDS) for a CANopen device.
/// An EDS serves as a template describing the device's capabilities and object dictionary.
/// </summary>
public class ElectronicDataSheet
{
    /// <summary>
    /// File information section.
    /// </summary>
    public EdsFileInfo FileInfo { get; set; } = new();

    /// <summary>
    /// Device information section.
    /// </summary>
    public DeviceInfo DeviceInfo { get; set; } = new();

    /// <summary>
    /// Object dictionary containing all CANopen objects.
    /// </summary>
    public ObjectDictionary ObjectDictionary { get; set; } = new();

    /// <summary>
    /// Optional comments section.
    /// </summary>
    public Comments? Comments { get; set; }

    /// <summary>
    /// Supported extension modules (for modular devices).
    /// </summary>
    public List<ModuleInfo> SupportedModules { get; set; } = new();

    /// <summary>
    /// Dynamic channels configuration for CiA 302-4 programmable devices.
    /// </summary>
    public DynamicChannels? DynamicChannels { get; set; }

    /// <summary>
    /// Tool definitions from [Tools]/[ToolX] sections.
    /// </summary>
    public List<ToolInfo> Tools { get; set; } = new();

    /// <summary>
    /// Additional sections not covered by standard specification.
    /// Key is section name, value is dictionary of key-value pairs.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> AdditionalSections { get; set; } = new();
}
