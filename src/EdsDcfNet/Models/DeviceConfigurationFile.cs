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
    public List<int> ConnectedModules { get; set; } = new();

    /// <summary>
    /// Supported extension modules (copied from EDS).
    /// </summary>
    public List<ModuleInfo> SupportedModules { get; set; } = new();

    /// <summary>
    /// Additional sections not covered by standard specification.
    /// Key is section name, value is dictionary of key-value pairs.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> AdditionalSections { get; set; } = new();
}
