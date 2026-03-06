namespace EdsDcfNet.Models;
using EdsDcfNet.Validation;

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
    /// Parsed <c>ApplicationProcess</c> element from the XDD device-profile body (CiA 311 §6.4.5).
    /// Populated when reading XDD/XDC files that contain an <c>ApplicationProcess</c> element.
    /// <see langword="null"/> when the source was an EDS file or when no
    /// <c>ApplicationProcess</c> element was present in the XDD/XDC file.
    /// </summary>
    public ApplicationProcess? ApplicationProcess { get; set; }

    /// <summary>
    /// Validates this model instance against common CANopen constraints.
    /// </summary>
    /// <returns>List of validation issues. Empty when model is valid.</returns>
    public IReadOnlyList<ValidationIssue> Validate()
    {
        return CanOpenModelValidator.Validate(this);
    }
}
