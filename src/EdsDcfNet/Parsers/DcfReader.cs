namespace EdsDcfNet.Parsers;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

/// <summary>
/// Reader for Device Configuration File (DCF) files.
/// DCF files extend EDS files with configured values and device-specific settings.
/// </summary>
public class DcfReader
{
    private readonly IniParser _iniParser = new();
    private readonly EdsReader _edsReader = new();

    /// <summary>
    /// Reads a DCF file from the specified path.
    /// </summary>
    /// <param name="filePath">Path to the DCF file</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public DeviceConfigurationFile ReadFile(string filePath)
    {
        var sections = _iniParser.ParseFile(filePath);
        return ParseDcf(sections);
    }

    /// <summary>
    /// Reads a DCF from a string.
    /// </summary>
    /// <param name="content">DCF file content as string</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public DeviceConfigurationFile ReadString(string content)
    {
        var sections = _iniParser.ParseString(content);
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

        // Parse any additional unknown sections
        foreach (var sectionName in sections.Keys)
        {
            if (!IsKnownSection(sectionName))
            {
                dcf.AdditionalSections[sectionName] = new Dictionary<string, string>(sections[sectionName]);
            }
        }

        return dcf;
    }

    private Models.FileInfo ParseFileInfo(Dictionary<string, Dictionary<string, string>> sections)
    {
        var fileInfo = new Models.FileInfo();

        if (!IniParser.HasSection(sections, "FileInfo"))
            return fileInfo;

        fileInfo.FileName = IniParser.GetValue(sections, "FileInfo", "FileName");
        fileInfo.FileVersion = ValueConverter.ParseByte(IniParser.GetValue(sections, "FileInfo", "FileVersion", "1"));
        fileInfo.FileRevision = ValueConverter.ParseByte(IniParser.GetValue(sections, "FileInfo", "FileRevision", "0"));
        fileInfo.EdsVersion = IniParser.GetValue(sections, "FileInfo", "EDSVersion", "4.0");
        fileInfo.Description = IniParser.GetValue(sections, "FileInfo", "Description");
        fileInfo.CreationTime = IniParser.GetValue(sections, "FileInfo", "CreationTime");
        fileInfo.CreationDate = IniParser.GetValue(sections, "FileInfo", "CreationDate");
        fileInfo.CreatedBy = IniParser.GetValue(sections, "FileInfo", "CreatedBy");
        fileInfo.ModificationTime = IniParser.GetValue(sections, "FileInfo", "ModificationTime");
        fileInfo.ModificationDate = IniParser.GetValue(sections, "FileInfo", "ModificationDate");
        fileInfo.ModifiedBy = IniParser.GetValue(sections, "FileInfo", "ModifiedBy");
        fileInfo.LastEds = IniParser.GetValue(sections, "FileInfo", "LastEDS");

        return fileInfo;
    }

    private DeviceInfo ParseDeviceInfo(Dictionary<string, Dictionary<string, string>> sections)
    {
        // Use the same parser as EDS reader
        return _edsReader.GetType()
            .GetMethod("ParseDeviceInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(_edsReader, new object[] { sections }) as DeviceInfo ?? new DeviceInfo();
    }

    private DeviceCommissioning ParseDeviceCommissioning(Dictionary<string, Dictionary<string, string>> sections)
    {
        var dc = new DeviceCommissioning();

        if (!IniParser.HasSection(sections, "DeviceCommissioning"))
            return dc;

        dc.NodeId = ValueConverter.ParseByte(IniParser.GetValue(sections, "DeviceCommissioning", "NodeID", "1"));
        dc.NodeName = IniParser.GetValue(sections, "DeviceCommissioning", "NodeName");
        dc.Baudrate = ValueConverter.ParseUInt16(IniParser.GetValue(sections, "DeviceCommissioning", "Baudrate", "250"));
        dc.NetNumber = ValueConverter.ParseInteger(IniParser.GetValue(sections, "DeviceCommissioning", "NetNumber", "0"));
        dc.NetworkName = IniParser.GetValue(sections, "DeviceCommissioning", "NetworkName");
        dc.CANopenManager = ValueConverter.ParseBoolean(IniParser.GetValue(sections, "DeviceCommissioning", "CANopenManager"));

        var lssSerialStr = IniParser.GetValue(sections, "DeviceCommissioning", "LSS_SerialNumber");
        if (!string.IsNullOrEmpty(lssSerialStr))
        {
            dc.LssSerialNumber = ValueConverter.ParseInteger(lssSerialStr);
        }

        return dc;
    }

    private ObjectDictionary ParseObjectDictionary(Dictionary<string, Dictionary<string, string>> sections)
    {
        var objDict = new ObjectDictionary();

        // Parse mandatory objects
        if (IniParser.HasSection(sections, "MandatoryObjects"))
        {
            var count = ValueConverter.ParseUInt16(IniParser.GetValue(sections, "MandatoryObjects", "SupportedObjects", "0"));
            for (int i = 1; i <= count; i++)
            {
                var indexStr = IniParser.GetValue(sections, "MandatoryObjects", i.ToString());
                if (!string.IsNullOrEmpty(indexStr))
                {
                    objDict.MandatoryObjects.Add(ValueConverter.ParseUInt16(indexStr));
                }
            }
        }

        // Parse optional objects
        if (IniParser.HasSection(sections, "OptionalObjects"))
        {
            var count = ValueConverter.ParseUInt16(IniParser.GetValue(sections, "OptionalObjects", "SupportedObjects", "0"));
            for (int i = 1; i <= count; i++)
            {
                var indexStr = IniParser.GetValue(sections, "OptionalObjects", i.ToString());
                if (!string.IsNullOrEmpty(indexStr))
                {
                    objDict.OptionalObjects.Add(ValueConverter.ParseUInt16(indexStr));
                }
            }
        }

        // Parse manufacturer objects
        if (IniParser.HasSection(sections, "ManufacturerObjects"))
        {
            var count = ValueConverter.ParseUInt16(IniParser.GetValue(sections, "ManufacturerObjects", "SupportedObjects", "0"));
            for (int i = 1; i <= count; i++)
            {
                var indexStr = IniParser.GetValue(sections, "ManufacturerObjects", i.ToString());
                if (!string.IsNullOrEmpty(indexStr))
                {
                    objDict.ManufacturerObjects.Add(ValueConverter.ParseUInt16(indexStr));
                }
            }
        }

        // Parse all object definitions
        var allObjects = objDict.MandatoryObjects
            .Concat(objDict.OptionalObjects)
            .Concat(objDict.ManufacturerObjects)
            .Distinct();

        foreach (var index in allObjects)
        {
            var obj = ParseObject(sections, index);
            if (obj != null)
            {
                objDict.Objects[index] = obj;
            }
        }

        // Parse dummy usage
        if (IniParser.HasSection(sections, "DummyUsage"))
        {
            foreach (var key in IniParser.GetKeys(sections, "DummyUsage"))
            {
                if (key.StartsWith("Dummy", StringComparison.OrdinalIgnoreCase) && key.Length > 5)
                {
                    var indexStr = key.Substring(5);
                    if (ushort.TryParse(indexStr, System.Globalization.NumberStyles.HexNumber, null, out var index))
                    {
                        objDict.DummyUsage[index] = ValueConverter.ParseBoolean(
                            IniParser.GetValue(sections, "DummyUsage", key));
                    }
                }
            }
        }

        return objDict;
    }

    private CanOpenObject? ParseObject(Dictionary<string, Dictionary<string, string>> sections, ushort index)
    {
        var sectionName = $"{index:X}";
        if (!IniParser.HasSection(sections, sectionName))
            return null;

        var obj = new CanOpenObject
        {
            Index = index,
            ParameterName = IniParser.GetValue(sections, sectionName, "ParameterName"),
            ObjectType = ValueConverter.ParseByte(IniParser.GetValue(sections, sectionName, "ObjectType", "0x7"))
        };

        var dataTypeStr = IniParser.GetValue(sections, sectionName, "DataType");
        if (!string.IsNullOrEmpty(dataTypeStr))
        {
            obj.DataType = ValueConverter.ParseUInt16(dataTypeStr);
        }

        var accessTypeStr = IniParser.GetValue(sections, sectionName, "AccessType");
        if (!string.IsNullOrEmpty(accessTypeStr))
        {
            obj.AccessType = ValueConverter.ParseAccessType(accessTypeStr);
        }

        obj.DefaultValue = IniParser.GetValue(sections, sectionName, "DefaultValue");
        obj.LowLimit = IniParser.GetValue(sections, sectionName, "LowLimit");
        obj.HighLimit = IniParser.GetValue(sections, sectionName, "HighLimit");
        obj.PdoMapping = ValueConverter.ParseBoolean(IniParser.GetValue(sections, sectionName, "PDOMapping"));
        obj.ObjFlags = ValueConverter.ParseInteger(IniParser.GetValue(sections, sectionName, "ObjFlags", "0"));

        var subNumberStr = IniParser.GetValue(sections, sectionName, "SubNumber");
        if (!string.IsNullOrEmpty(subNumberStr))
        {
            obj.SubNumber = ValueConverter.ParseByte(subNumberStr);
        }

        var compactSubObjStr = IniParser.GetValue(sections, sectionName, "CompactSubObj");
        if (!string.IsNullOrEmpty(compactSubObjStr))
        {
            obj.CompactSubObj = ValueConverter.ParseByte(compactSubObjStr);
        }

        // DCF-specific fields
        obj.ParameterValue = IniParser.GetValue(sections, sectionName, "ParameterValue");
        obj.Denotation = IniParser.GetValue(sections, sectionName, "Denotation");
        obj.UploadFile = IniParser.GetValue(sections, sectionName, "UploadFile");
        obj.DownloadFile = IniParser.GetValue(sections, sectionName, "DownloadFile");

        // Parse sub-objects
        if (obj.SubNumber > 0 || obj.ObjectType == 0x8 || obj.ObjectType == 0x9)
        {
            ParseSubObjects(sections, index, obj);
        }

        // Parse object links
        var linksSectionName = $"{index:X}ObjectLinks";
        if (IniParser.HasSection(sections, linksSectionName))
        {
            var count = ValueConverter.ParseUInt16(IniParser.GetValue(sections, linksSectionName, "ObjectLinks", "0"));
            for (int i = 1; i <= count; i++)
            {
                var linkStr = IniParser.GetValue(sections, linksSectionName, i.ToString());
                if (!string.IsNullOrEmpty(linkStr))
                {
                    obj.ObjectLinks.Add(ValueConverter.ParseUInt16(linkStr));
                }
            }
        }

        return obj;
    }

    private void ParseSubObjects(Dictionary<string, Dictionary<string, string>> sections, ushort index, CanOpenObject obj)
    {
        // Determine the number of sub-objects to parse
        var maxSubIndex = obj.SubNumber ?? 0;
        if (obj.CompactSubObj.HasValue && obj.CompactSubObj.Value > 0)
        {
            maxSubIndex = Math.Max(maxSubIndex, obj.CompactSubObj.Value);
        }

        for (byte subIndex = 0; subIndex <= maxSubIndex; subIndex++)
        {
            var sectionName = $"{index:X}sub{subIndex:X}";
            if (IniParser.HasSection(sections, sectionName))
            {
                var subObj = ParseSubObject(sections, index, subIndex);
                if (subObj != null)
                {
                    obj.SubObjects[subIndex] = subObj;
                }
            }
        }

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

    private CanOpenSubObject? ParseSubObject(Dictionary<string, Dictionary<string, string>> sections, ushort index, byte subIndex)
    {
        var sectionName = $"{index:X}sub{subIndex:X}";
        if (!IniParser.HasSection(sections, sectionName))
            return null;

        var subObj = new CanOpenSubObject
        {
            SubIndex = subIndex,
            ParameterName = IniParser.GetValue(sections, sectionName, "ParameterName"),
            ObjectType = ValueConverter.ParseByte(IniParser.GetValue(sections, sectionName, "ObjectType", "0x7")),
            DataType = ValueConverter.ParseUInt16(IniParser.GetValue(sections, sectionName, "DataType", "0")),
            AccessType = ValueConverter.ParseAccessType(IniParser.GetValue(sections, sectionName, "AccessType")),
            DefaultValue = IniParser.GetValue(sections, sectionName, "DefaultValue"),
            LowLimit = IniParser.GetValue(sections, sectionName, "LowLimit"),
            HighLimit = IniParser.GetValue(sections, sectionName, "HighLimit"),
            PdoMapping = ValueConverter.ParseBoolean(IniParser.GetValue(sections, sectionName, "PDOMapping"))
        };

        // DCF-specific fields
        subObj.ParameterValue = IniParser.GetValue(sections, sectionName, "ParameterValue");
        subObj.Denotation = IniParser.GetValue(sections, sectionName, "Denotation");

        return subObj;
    }

    private Comments? ParseComments(Dictionary<string, Dictionary<string, string>> sections)
    {
        if (!IniParser.HasSection(sections, "Comments"))
            return null;

        var comments = new Comments
        {
            Lines = ValueConverter.ParseUInt16(IniParser.GetValue(sections, "Comments", "Lines", "0"))
        };

        for (int i = 1; i <= comments.Lines; i++)
        {
            var line = IniParser.GetValue(sections, "Comments", $"Line{i}");
            if (!string.IsNullOrEmpty(line))
            {
                comments.CommentLines[i] = line;
            }
        }

        return comments;
    }

    private List<int> ParseConnectedModules(Dictionary<string, Dictionary<string, string>> sections)
    {
        var modules = new List<int>();
        var count = ValueConverter.ParseUInt16(IniParser.GetValue(sections, "ConnectedModules", "NrOfEntries", "0"));

        for (int i = 1; i <= count; i++)
        {
            var moduleStr = IniParser.GetValue(sections, "ConnectedModules", i.ToString());
            if (!string.IsNullOrEmpty(moduleStr) && int.TryParse(moduleStr, out var moduleNumber))
            {
                modules.Add(moduleNumber);
            }
        }

        return modules;
    }

    private List<ModuleInfo> ParseSupportedModules(Dictionary<string, Dictionary<string, string>> sections)
    {
        var modules = new List<ModuleInfo>();
        var count = ValueConverter.ParseUInt16(IniParser.GetValue(sections, "SupportedModules", "NrOfEntries", "0"));

        for (int i = 1; i <= count; i++)
        {
            var moduleInfo = ParseModuleInfo(sections, i);
            if (moduleInfo != null)
            {
                modules.Add(moduleInfo);
            }
        }

        return modules;
    }

    private ModuleInfo? ParseModuleInfo(Dictionary<string, Dictionary<string, string>> sections, int moduleNumber)
    {
        var sectionName = $"M{moduleNumber}ModuleInfo";
        if (!IniParser.HasSection(sections, sectionName))
            return null;

        var moduleInfo = new ModuleInfo
        {
            ModuleNumber = moduleNumber,
            ProductName = IniParser.GetValue(sections, sectionName, "ProductName"),
            ProductVersion = ValueConverter.ParseByte(IniParser.GetValue(sections, sectionName, "ProductVersion", "1")),
            ProductRevision = ValueConverter.ParseByte(IniParser.GetValue(sections, sectionName, "ProductRevision", "0")),
            OrderCode = IniParser.GetValue(sections, sectionName, "OrderCode")
        };

        // Parse fixed objects
        var fixedObjSection = $"M{moduleNumber}FixedObjects";
        if (IniParser.HasSection(sections, fixedObjSection))
        {
            var count = ValueConverter.ParseUInt16(IniParser.GetValue(sections, fixedObjSection, "NrOfEntries", "0"));
            for (int i = 1; i <= count; i++)
            {
                var indexStr = IniParser.GetValue(sections, fixedObjSection, i.ToString());
                if (!string.IsNullOrEmpty(indexStr))
                {
                    moduleInfo.FixedObjects.Add(ValueConverter.ParseUInt16(indexStr));
                }
            }
        }

        return moduleInfo;
    }

    private bool IsKnownSection(string sectionName)
    {
        var knownSections = new[]
        {
            "FileInfo", "DeviceInfo", "DeviceCommissioning", "DummyUsage",
            "MandatoryObjects", "OptionalObjects", "ManufacturerObjects",
            "Comments", "SupportedModules", "ConnectedModules", "Tools",
            "DynamicChannels"
        };

        if (knownSections.Contains(sectionName, StringComparer.OrdinalIgnoreCase))
            return true;

        // Check for object sections (hex index)
        if (ushort.TryParse(sectionName, System.Globalization.NumberStyles.HexNumber, null, out _))
            return true;

        // Check for sub-object sections
        if (sectionName.Contains("sub", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check for value and denotation sections
        if (sectionName.EndsWith("Value", StringComparison.OrdinalIgnoreCase) ||
            sectionName.EndsWith("Denotation", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check for module sections
        if (sectionName.StartsWith("M", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
