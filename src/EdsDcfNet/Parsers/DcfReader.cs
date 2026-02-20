namespace EdsDcfNet.Parsers;

using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

/// <summary>
/// Reader for Device Configuration File (DCF) files.
/// DCF files extend EDS files with configured values and device-specific settings.
/// </summary>
public class DcfReader : CanOpenReaderBase
{
    private static readonly string[] DcfKnownSectionNames =
    {
        "FileInfo", "DeviceInfo", "DeviceCommissioning", "DeviceComissioning", "DummyUsage",
        "MandatoryObjects", "OptionalObjects", "ManufacturerObjects",
        "Comments", "SupportedModules", "ConnectedModules", "Tools",
        "DynamicChannels"
    };

    /// <inheritdoc/>
    protected override string[] KnownSectionNames => DcfKnownSectionNames;

    /// <summary>
    /// Reads a DCF file from the specified path.
    /// </summary>
    /// <param name="filePath">Path to the DCF file</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public DeviceConfigurationFile ReadFile(string filePath)
    {
        var sections = ParseSectionsFromFile(filePath);
        return ParseDcf(sections);
    }

    /// <summary>
    /// Reads a DCF from a string.
    /// </summary>
    /// <param name="content">DCF file content as string</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public DeviceConfigurationFile ReadString(string content)
    {
        var sections = ParseSectionsFromString(content);
        return ParseDcf(sections);
    }

    private DeviceConfigurationFile ParseDcf(Dictionary<string, Dictionary<string, string>> sections)
    {
        var dcf = new DeviceConfigurationFile
        {
            FileInfo = ParseFileInfo(sections),
            DeviceInfo = ParseDeviceInfo(sections),
            DeviceCommissioning = ParseDeviceCommissioning(sections),
            ObjectDictionary = ParseObjectDictionary(sections),
            Comments = ParseComments(sections)
        };

        // Parse connected modules if present
        if (IniParser.HasSection(sections, "ConnectedModules"))
        {
            dcf.ConnectedModules = ParseConnectedModules(sections);
        }

        // Parse supported modules if present
        if (IniParser.HasSection(sections, "SupportedModules"))
        {
            dcf.SupportedModules = ParseSupportedModules(sections);
        }

        // Parse dynamic channels if present
        if (IniParser.HasSection(sections, "DynamicChannels"))
        {
            dcf.DynamicChannels = ParseDynamicChannels(sections);
        }

        // Parse tools if present
        if (IniParser.HasSection(sections, "Tools"))
        {
            dcf.Tools = ParseTools(sections);
        }

        // Parse any additional unknown sections
        foreach (var sectionName in sections.Keys)
        {
            if (!IsKnownSection(sectionName) && !IsToolSectionForParsedTools(sectionName, dcf.Tools.Count) && !IsObjectLinksSectionForExistingObject(sectionName, dcf.ObjectDictionary))
            {
                dcf.AdditionalSections[sectionName] = new Dictionary<string, string>(sections[sectionName]);
            }
        }

        return dcf;
    }

    #region DCF-specific overrides

    /// <inheritdoc/>
    protected override EdsFileInfo ParseFileInfo(Dictionary<string, Dictionary<string, string>> sections)
    {
        var fileInfo = base.ParseFileInfo(sections);

        if (IniParser.HasSection(sections, "FileInfo"))
        {
            fileInfo.LastEds = IniParser.GetValue(sections, "FileInfo", "LastEDS");
        }

        return fileInfo;
    }

    /// <inheritdoc/>
    protected override CanOpenObject? ParseObject(Dictionary<string, Dictionary<string, string>> sections, ushort index)
    {
        var obj = base.ParseObject(sections, index);
        if (obj == null)
            return null;

        // DCF-specific fields
        var sectionName = $"{index:X}";
        obj.ParameterValue = IniParser.GetValue(sections, sectionName, "ParameterValue");
        obj.Denotation = IniParser.GetValue(sections, sectionName, "Denotation");
        obj.ParamRefd = IniParser.GetValue(sections, sectionName, "ParamRefd");
        obj.UploadFile = IniParser.GetValue(sections, sectionName, "UploadFile");
        obj.DownloadFile = IniParser.GetValue(sections, sectionName, "DownloadFile");

        return obj;
    }

    /// <inheritdoc/>
    protected override void ParseSubObjects(Dictionary<string, Dictionary<string, string>> sections, ushort index, CanOpenObject obj)
    {
        base.ParseSubObjects(sections, index, obj);

        // Parse compact value storage
        var valueSectionName = $"{index:X}Value";
        if (IniParser.HasSection(sections, valueSectionName))
        {
            var count = ValueConverter.ParseUInt16(IniParser.GetValue(sections, valueSectionName, "NrOfEntries", "0"));
            for (int i = 1; i <= count; i++)
            {
                var value = IniParser.GetValue(sections, valueSectionName, i.ToString());
                if (!string.IsNullOrEmpty(value) && i <= 254)
                {
                    var subIndex = (byte)i;
                    if (obj.SubObjects.ContainsKey(subIndex))
                    {
                        obj.SubObjects[subIndex].ParameterValue = value;
                    }
                }
            }
        }

        // Parse compact denotation storage
        var denotationSectionName = $"{index:X}Denotation";
        if (IniParser.HasSection(sections, denotationSectionName))
        {
            var count = ValueConverter.ParseUInt16(IniParser.GetValue(sections, denotationSectionName, "NrOfEntries", "0"));
            for (int i = 1; i <= count; i++)
            {
                var denotation = IniParser.GetValue(sections, denotationSectionName, i.ToString());
                if (!string.IsNullOrEmpty(denotation) && i <= 254)
                {
                    var subIndex = (byte)i;
                    if (obj.SubObjects.ContainsKey(subIndex))
                    {
                        obj.SubObjects[subIndex].Denotation = denotation;
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    protected override CanOpenSubObject? ParseSubObject(Dictionary<string, Dictionary<string, string>> sections, ushort index, byte subIndex)
    {
        var subObj = base.ParseSubObject(sections, index, subIndex);
        if (subObj == null)
            return null;

        // DCF-specific fields
        var sectionName = $"{index:X}sub{subIndex:X}";
        subObj.ParameterValue = IniParser.GetValue(sections, sectionName, "ParameterValue");
        subObj.Denotation = IniParser.GetValue(sections, sectionName, "Denotation");
        subObj.ParamRefd = IniParser.GetValue(sections, sectionName, "ParamRefd");

        return subObj;
    }

    /// <inheritdoc/>
    protected override bool IsKnownSection(string sectionName)
    {
        if (base.IsKnownSection(sectionName))
            return true;

        // Check for compact value/denotation sections (hex index + "Value" or "Denotation")
        // Note: ObjectLinks sections are intentionally NOT marked as known here.
        // This allows orphaned ObjectLinks (for non-existent objects) to be preserved in AdditionalSections.
        if (IsHexPrefixedSection(sectionName, "Value") ||
            IsHexPrefixedSection(sectionName, "Denotation"))
            return true;

        return false;
    }

    #endregion

    #region DCF-only parsing methods

    private DeviceCommissioning ParseDeviceCommissioning(Dictionary<string, Dictionary<string, string>> sections)
    {
        var dc = new DeviceCommissioning();

        // Accept both spec spelling "DeviceComissioning" (one 'm') and common spelling (two 'm's)
        var sectionName = IniParser.HasSection(sections, "DeviceCommissioning")
            ? "DeviceCommissioning"
            : IniParser.HasSection(sections, "DeviceComissioning")
                ? "DeviceComissioning"
                : null;

        if (sectionName == null)
            return dc;

        dc.NodeId = ValueConverter.ParseByte(IniParser.GetValue(sections, sectionName, "NodeID", "1"));
        dc.NodeName = IniParser.GetValue(sections, sectionName, "NodeName");
        dc.NodeRefd = IniParser.GetValue(sections, sectionName, "NodeRefd");
        dc.Baudrate = ValueConverter.ParseUInt16(IniParser.GetValue(sections, sectionName, "Baudrate", "250"));
        dc.NetNumber = ValueConverter.ParseInteger(IniParser.GetValue(sections, sectionName, "NetNumber", "0"));
        dc.NetworkName = IniParser.GetValue(sections, sectionName, "NetworkName");
        dc.NetRefd = IniParser.GetValue(sections, sectionName, "NetRefd");
        dc.CANopenManager = ValueConverter.ParseBoolean(IniParser.GetValue(sections, sectionName, "CANopenManager"));

        var lssSerialStr = IniParser.GetValue(sections, sectionName, "LSS_SerialNumber");
        if (!string.IsNullOrEmpty(lssSerialStr))
        {
            dc.LssSerialNumber = ValueConverter.ParseInteger(lssSerialStr);
        }

        return dc;
    }

    private List<int> ParseConnectedModules(Dictionary<string, Dictionary<string, string>> sections)
    {
        var modules = new List<int>();
        var count = ValueConverter.ParseUInt16(IniParser.GetValue(sections, "ConnectedModules", "NrOfEntries", "0"));

        for (int i = 1; i <= count; i++)
        {
            var moduleStr = IniParser.GetValue(sections, "ConnectedModules", i.ToString());
            if (!string.IsNullOrEmpty(moduleStr) && int.TryParse(moduleStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var moduleNumber))
            {
                modules.Add(moduleNumber);
            }
        }

        return modules;
    }

    private static bool IsObjectLinksSectionForExistingObject(string sectionName, ObjectDictionary objectDictionary)
    {
        const string suffix = "ObjectLinks";

        if (!sectionName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var indexPart = sectionName.Substring(0, sectionName.Length - suffix.Length);
        if (string.IsNullOrWhiteSpace(indexPart))
        {
            return false;
        }

        if (!ushort.TryParse(indexPart, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var index))
        {
            return false;
        }

        return objectDictionary.Objects.ContainsKey(index);
    }

    #endregion
}
