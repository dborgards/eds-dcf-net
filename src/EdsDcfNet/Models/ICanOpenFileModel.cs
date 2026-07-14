namespace EdsDcfNet.Models;

/// <summary>
/// Internal shape shared by <see cref="ElectronicDataSheet"/> and
/// <see cref="DeviceConfigurationFile"/> so that
/// <see cref="Parsers.CanOpenReaderBase"/> can populate the sections common to
/// both formats in one place. Intentionally internal: the two public model
/// classes stay unrelated on the public API surface.
/// </summary>
internal interface ICanOpenFileModel
{
    /// <summary>File information section.</summary>
    EdsFileInfo FileInfo { get; set; }

    /// <summary>Device information section.</summary>
    DeviceInfo DeviceInfo { get; set; }

    /// <summary>Object dictionary.</summary>
    ObjectDictionary ObjectDictionary { get; set; }

    /// <summary>Optional comments section.</summary>
    Comments? Comments { get; set; }

    /// <summary>Supported extension modules.</summary>
    List<ModuleInfo> SupportedModules { get; }

    /// <summary>Dynamic channels configuration.</summary>
    DynamicChannels? DynamicChannels { get; set; }

    /// <summary>Tool definitions from [Tools]/[ToolX] sections.</summary>
    List<ToolInfo> Tools { get; }

    /// <summary>Additional sections not covered by the standard specification.</summary>
    Dictionary<string, Dictionary<string, string>> AdditionalSections { get; }
}
