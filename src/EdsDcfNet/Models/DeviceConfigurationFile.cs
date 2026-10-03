namespace EdsDcfNet.Models;
using EdsDcfNet.Validation;

/// <summary>
/// Represents a complete Device Configuration File (DCF) for a CANopen device.
/// A DCF describes a concrete incarnation of a configured device with specific values.
/// </summary>
public class DeviceConfigurationFile : ICanOpenFileModel
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
    /// Unmapped entries of standard sections that have no model object of their own, keyed by
    /// the full section name (case-insensitive), for example <c>Tools</c>, <c>DummyUsage</c>,
    /// <c>MandatoryObjects</c>, <c>OptionalObjects</c>, <c>ManufacturerObjects</c>,
    /// <c>SupportedModules</c>, <c>ConnectedModules</c>, <c>M1ModuleInfo</c>, <c>M1FixedObjects</c>,
    /// <c>M1SubExtends</c>, <c>M1SubExt2000</c>, <c>2000Name</c>, <c>2000Value</c>, <c>2000Denotation</c> and <c>2000ObjectLinks</c>.
    /// Each value keeps the entries in file order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CiA 306-1 allows additional entries inside the standard sections "in order to support
    /// future extensions" (§ 6.2). Sections with a model object of their own keep them on that
    /// object instead (<c>RemainingEntries</c> of <see cref="EdsFileInfo"/>, <see cref="DeviceInfo"/>, <see cref="DeviceCommissioning"/>,
    /// <see cref="Comments"/>, <see cref="ToolInfo"/>, <see cref="DynamicChannels"/>,
    /// <see cref="CanOpenObject"/> and <see cref="CanOpenSubObject"/>). Sections in
    /// <see cref="AdditionalSections"/> are kept whole and do not appear here.
    /// </para>
    /// <para>
    /// The writer emits the entries of a section after the keys it generates for that section,
    /// using the canonical section name. A key the writer generates (for example a numbered list
    /// entry after the caller added an object) replaces a kept entry with the same key. Entries
    /// for a section the writer does not emit (for example a removed module) are not written.
    /// Numbered entries above the count of a list (<c>SupportedObjects=1</c> with entries
    /// <c>1</c> and <c>2</c>) are not loaded into the model and stay here verbatim.
    /// </para>
    /// </remarks>
    public Dictionary<string, OrderedStringDictionary> SectionRemainingEntries { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Parsed <c>ApplicationProcess</c> element from the XDC device-profile body (CiA 311 §6.4.5).
    /// Populated when reading XDD/XDC files that contain an <c>ApplicationProcess</c> element.
    /// <see langword="null"/> when the source was a DCF file or when no
    /// <c>ApplicationProcess</c> element was present in the XDD/XDC file.
    /// </summary>
    public ApplicationProcess? ApplicationProcess { get; set; }

    /// <summary>
    /// XDD/XDC content the model does not represent, kept by the XDD/XDC reader for the XDD/XDC
    /// writers. <see langword="null"/> for a model that was not read from XDD/XDC.
    /// </summary>
    internal XddPreservedContent? XddPreserved { get; set; }

    /// <summary>
    /// Validates this model instance against common CANopen constraints.
    /// </summary>
    /// <returns>List of validation issues. Empty when model is valid.</returns>
    public IReadOnlyList<ValidationIssue> Validate()
    {
        return CanOpenModelValidator.Validate(this);
    }
}
