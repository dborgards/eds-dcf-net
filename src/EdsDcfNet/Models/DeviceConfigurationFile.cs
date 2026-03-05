namespace EdsDcfNet.Models;

/// <summary>
/// Represents a complete Device Configuration File (DCF) for a CANopen device.
/// A DCF describes a concrete incarnation of a configured device with specific values.
/// </summary>
public class DeviceConfigurationFile
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
    /// Device commissioning section (DCF-specific).
    /// Contains node ID, baudrate, and network configuration.
    /// </summary>
    public DeviceCommissioning DeviceCommissioning { get; set; } = new();

    /// <summary>
    /// Object dictionary with configured values.
    /// </summary>
    public ObjectDictionary ObjectDictionary { get; set; } = new();

    /// <summary>
    /// Optional comments section.
    /// </summary>
    public Comments? Comments { get; set; }

    /// <summary>
    /// Connected modules (for modular devices).
    /// List of module indices referring to SupportedModules.
    /// </summary>
    public List<int> ConnectedModules { get; } = new();

    /// <summary>
    /// Supported extension modules (copied from EDS).
    /// </summary>
    public List<ModuleInfo> SupportedModules { get; } = new();

    /// <summary>
    /// Dynamic channels configuration for CiA 302-4 programmable devices.
    /// </summary>
    public DynamicChannels? DynamicChannels { get; set; }

    /// <summary>
    /// Tool definitions from [Tools]/[ToolX] sections.
    /// </summary>
    public List<ToolInfo> Tools { get; } = new();

    /// <summary>
    /// Additional sections not covered by standard specification.
    /// Section names are compared case-insensitively using <see cref="StringComparer.OrdinalIgnoreCase"/>,
    /// so assigning names that differ only by case overwrites the previous section.
    /// Each section contains key-value pairs that are treated case-insensitively by readers/writers.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> AdditionalSections { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Parsed <c>ApplicationProcess</c> element from the XDC device-profile body (CiA 311 §6.4.5).
    /// Populated when reading XDD/XDC files that contain an <c>ApplicationProcess</c> element.
    /// <see langword="null"/> when the source was a DCF file or when no
    /// <c>ApplicationProcess</c> element was present in the XDD/XDC file.
    /// </summary>
    public ApplicationProcess? ApplicationProcess { get; set; }
}
