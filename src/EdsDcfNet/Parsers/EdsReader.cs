namespace EdsDcfNet.Parsers;

using EdsDcfNet.Models;

/// <summary>
/// Reader for Electronic Data Sheet (EDS) files.
/// </summary>
public class EdsReader : CanOpenReaderBase
{
    private static readonly string[] EdsKnownSectionNames =
    {
        "FileInfo", "DeviceInfo", "DummyUsage", "MandatoryObjects",
        "OptionalObjects", "ManufacturerObjects", "Comments",
        "SupportedModules", "Tools", "DynamicChannels"
    };

    /// <inheritdoc/>
    protected override string[] KnownSectionNames => EdsKnownSectionNames;

    /// <summary>
    /// Reads an EDS file from the specified path.
    /// </summary>
    /// <param name="filePath">Path to the EDS file</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public ElectronicDataSheet ReadFile(string filePath)
    {
        var sections = ParseSectionsFromFile(filePath);
        return ParseEds(sections);
    }

    /// <summary>
    /// Reads an EDS from a string.
    /// </summary>
    /// <param name="content">EDS file content as string</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public ElectronicDataSheet ReadString(string content)
    {
        var sections = ParseSectionsFromString(content);
        return ParseEds(sections);
    }

    private ElectronicDataSheet ParseEds(Dictionary<string, Dictionary<string, string>> sections)
    {
        var eds = new ElectronicDataSheet
        {
            FileInfo = ParseFileInfo(sections),
            DeviceInfo = ParseDeviceInfo(sections),
            ObjectDictionary = ParseObjectDictionary(sections),
            Comments = ParseComments(sections)
        };

        // Parse supported modules if present
        if (IniParser.HasSection(sections, "SupportedModules"))
        {
            eds.SupportedModules = ParseSupportedModules(sections);
        }

        // Parse dynamic channels if present
        if (IniParser.HasSection(sections, "DynamicChannels"))
        {
            eds.DynamicChannels = ParseDynamicChannels(sections);
        }

        // Parse tools if present
        if (IniParser.HasSection(sections, "Tools"))
        {
            eds.Tools = ParseTools(sections);
        }

        // Parse any additional unknown sections
        foreach (var sectionName in sections.Keys)
        {
            if (!IsKnownSection(sectionName) && !IsToolSectionForParsedTools(sectionName, eds.Tools.Count))
            {
                eds.AdditionalSections[sectionName] = new Dictionary<string, string>(sections[sectionName]);
            }
        }

        return eds;
    }
}
