namespace EdsDcfNet.Parsers;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

/// <summary>
/// Reader for Electronic Data Sheet (EDS) files.
/// </summary>
public class EdsReader
{
    private readonly IniParser _iniParser = new();

    /// <summary>
    /// Reads an EDS file from the specified path.
    /// </summary>
    /// <param name="filePath">Path to the EDS file</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public ElectronicDataSheet ReadFile(string filePath)
    {
        var sections = _iniParser.ParseFile(filePath);
        return ParseEds(sections);
    }

    /// <summary>
    /// Reads an EDS from a string.
    /// </summary>
    /// <param name="content">EDS file content as string</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public ElectronicDataSheet ReadString(string content)
    {
        var sections = _iniParser.ParseString(content);
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

        // Parse any additional unknown sections
        foreach (var sectionName in sections.Keys)
        {
            if (!IsKnownSection(sectionName))
            {
                eds.AdditionalSections[sectionName] = new Dictionary<string, string>(sections[sectionName]);
            }
        }

        return eds;
    }

    internal Models.EdsFileInfo ParseFileInfo(Dictionary<string, Dictionary<string, string>> sections)
    {
        var fileInfo = new Models.EdsFileInfo();

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

        return fileInfo;
    }

    internal DeviceInfo ParseDeviceInfo(Dictionary<string, Dictionary<string, string>> sections)
    {
        var deviceInfo = new DeviceInfo();

        if (!IniParser.HasSection(sections, "DeviceInfo"))
            throw new EdsParseException("Required section [DeviceInfo] not found");

        deviceInfo.VendorName = IniParser.GetValue(sections, "DeviceInfo", "VendorName");
        deviceInfo.VendorNumber = ValueConverter.ParseInteger(IniParser.GetValue(sections, "DeviceInfo", "VendorNumber", "0"));
        deviceInfo.ProductName = IniParser.GetValue(sections, "DeviceInfo", "ProductName");
        deviceInfo.ProductNumber = ValueConverter.ParseInteger(IniParser.GetValue(sections, "DeviceInfo", "ProductNumber", "0"));
        deviceInfo.RevisionNumber = ValueConverter.ParseInteger(IniParser.GetValue(sections, "DeviceInfo", "RevisionNumber", "0"));
        deviceInfo.OrderCode = IniParser.GetValue(sections, "DeviceInfo", "OrderCode");

        // Parse baud rates
        deviceInfo.SupportedBaudRates.BaudRate10 = ValueConverter.ParseBoolean(IniParser.GetValue(sections, "DeviceInfo", "BaudRate_10"));
        deviceInfo.SupportedBaudRates.BaudRate20 = ValueConverter.ParseBoolean(IniParser.GetValue(sections, "DeviceInfo", "BaudRate_20"));
        deviceInfo.SupportedBaudRates.BaudRate50 = ValueConverter.ParseBoolean(IniParser.GetValue(sections, "DeviceInfo", "BaudRate_50"));
        deviceInfo.SupportedBaudRates.BaudRate125 = ValueConverter.ParseBoolean(IniParser.GetValue(sections, "DeviceInfo", "BaudRate_125"));
        deviceInfo.SupportedBaudRates.BaudRate250 = ValueConverter.ParseBoolean(IniParser.GetValue(sections, "DeviceInfo", "BaudRate_250"));
        deviceInfo.SupportedBaudRates.BaudRate500 = ValueConverter.ParseBoolean(IniParser.GetValue(sections, "DeviceInfo", "BaudRate_500"));
        deviceInfo.SupportedBaudRates.BaudRate800 = ValueConverter.ParseBoolean(IniParser.GetValue(sections, "DeviceInfo", "BaudRate_800"));
        deviceInfo.SupportedBaudRates.BaudRate1000 = ValueConverter.ParseBoolean(IniParser.GetValue(sections, "DeviceInfo", "BaudRate_1000"));

        deviceInfo.SimpleBootUpMaster = ValueConverter.ParseBoolean(IniParser.GetValue(sections, "DeviceInfo", "SimpleBootUpMaster"));
        deviceInfo.SimpleBootUpSlave = ValueConverter.ParseBoolean(IniParser.GetValue(sections, "DeviceInfo", "SimpleBootUpSlave"));
        deviceInfo.Granularity = ValueConverter.ParseByte(IniParser.GetValue(sections, "DeviceInfo", "Granularity", "8"));
        deviceInfo.DynamicChannelsSupported = ValueConverter.ParseByte(IniParser.GetValue(sections, "DeviceInfo", "DynamicChannelsSupported", "0"));
        deviceInfo.GroupMessaging = ValueConverter.ParseBoolean(IniParser.GetValue(sections, "DeviceInfo", "GroupMessaging"));
        deviceInfo.NrOfRxPdo = ValueConverter.ParseUInt16(IniParser.GetValue(sections, "DeviceInfo", "NrOfRXPDO", "0"));
        deviceInfo.NrOfTxPdo = ValueConverter.ParseUInt16(IniParser.GetValue(sections, "DeviceInfo", "NrOfTXPDO", "0"));
        deviceInfo.LssSupported = ValueConverter.ParseBoolean(IniParser.GetValue(sections, "DeviceInfo", "LSS_Supported"));
        deviceInfo.CompactPdo = ValueConverter.ParseByte(IniParser.GetValue(sections, "DeviceInfo", "CompactPDO", "0"));
        deviceInfo.CANopenSafetySupported = ValueConverter.ParseBoolean(IniParser.GetValue(sections, "DeviceInfo", "CANopenSafetySupported"));

        return deviceInfo;
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
                    if (ushort.TryParse(indexStr, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var index))
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
        obj.SrdoMapping = ValueConverter.ParseBoolean(IniParser.GetValue(sections, sectionName, "SRDOMapping"));
        obj.InvertedSrad = IniParser.GetValue(sections, sectionName, "InvertedSRAD");
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
            PdoMapping = ValueConverter.ParseBoolean(IniParser.GetValue(sections, sectionName, "PDOMapping")),
            SrdoMapping = ValueConverter.ParseBoolean(IniParser.GetValue(sections, sectionName, "SRDOMapping")),
            InvertedSrad = IniParser.GetValue(sections, sectionName, "InvertedSRAD")
        };

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
            "FileInfo", "DeviceInfo", "DummyUsage", "MandatoryObjects",
            "OptionalObjects", "ManufacturerObjects", "Comments",
            "SupportedModules", "Tools", "DynamicChannels"
        };

        if (knownSections.Contains(sectionName, StringComparer.OrdinalIgnoreCase))
            return true;

        // Check for object sections (hex index)
        if (ushort.TryParse(sectionName, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out _))
            return true;

        // Check for sub-object sections (hex index + "sub" + hex subindex)
        if (sectionName.IndexOf("sub", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        // Check for module sections
        if (sectionName.StartsWith("M", StringComparison.OrdinalIgnoreCase) &&
            (sectionName.Contains("ModuleInfo") || sectionName.Contains("FixedObjects") ||
             sectionName.Contains("SubExtends") || sectionName.Contains("SubExt")))
            return true;

        return false;
    }
}
