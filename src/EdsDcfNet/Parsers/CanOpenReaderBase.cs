namespace EdsDcfNet.Parsers;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

/// <summary>
/// Abstract base class for EDS and DCF readers.
/// Contains all shared CANopen INI parsing logic; format-specific behaviour
/// is handled via virtual methods that derived readers can override.
/// </summary>
public abstract class CanOpenReaderBase
{
    private readonly IniParser _iniParser = new();

    /// <summary>
    /// Section names that are considered "known" for this file format.
    /// Unknown sections are preserved in AdditionalSections for round-trip fidelity.
    /// </summary>
    protected abstract string[] KnownSectionNames { get; }

    /// <summary>
    /// Parses INI sections from a file path.
    /// </summary>
    protected Dictionary<string, Dictionary<string, string>> ParseSectionsFromFile(string filePath)
        => _iniParser.ParseFile(filePath);

    /// <summary>
    /// Parses INI sections from a string.
    /// </summary>
    protected Dictionary<string, Dictionary<string, string>> ParseSectionsFromString(string content)
        => _iniParser.ParseString(content);

    /// <summary>
    /// Parses the <c>[FileInfo]</c> section into an <see cref="EdsFileInfo"/> object.
    /// Derived classes may override this to read additional format-specific fields.
    /// </summary>
    protected virtual EdsFileInfo ParseFileInfo(Dictionary<string, Dictionary<string, string>> sections)
    {
        var fileInfo = new EdsFileInfo();

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

    /// <summary>
    /// Parses the <c>[DeviceInfo]</c> section into a <see cref="DeviceInfo"/> object.
    /// Throws <see cref="Exceptions.EdsParseException"/> if the section is absent.
    /// </summary>
    protected DeviceInfo ParseDeviceInfo(Dictionary<string, Dictionary<string, string>> sections)
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

    /// <summary>
    /// Parses the mandatory, optional, and manufacturer object sections into an
    /// <see cref="ObjectDictionary"/>, including all sub-objects and dummy usage entries.
    /// </summary>
    protected ObjectDictionary ParseObjectDictionary(Dictionary<string, Dictionary<string, string>> sections)
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

    /// <summary>
    /// Parses a single CANopen object at the given <paramref name="index"/> from the INI sections.
    /// Returns <see langword="null"/> if no section exists for that index.
    /// Derived classes may override this to read additional format-specific fields.
    /// </summary>
    protected virtual CanOpenObject? ParseObject(Dictionary<string, Dictionary<string, string>> sections, ushort index)
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

        // Parse sub-objects for composite types (DEFSTRUCT, ARRAY, RECORD)
        if (obj.SubNumber > 0 || obj.ObjectType == 0x6 || obj.ObjectType == 0x8 || obj.ObjectType == 0x9)
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

    /// <summary>
    /// Parses all sub-objects for the given <paramref name="obj"/> and populates
    /// <see cref="CanOpenObject.SubObjects"/>.
    /// Derived classes may override this to handle additional compact storage formats.
    /// </summary>
    protected virtual void ParseSubObjects(Dictionary<string, Dictionary<string, string>> sections, ushort index, CanOpenObject obj)
    {
        // Determine the number of sub-objects to parse
        var maxSubIndex = obj.SubNumber ?? 0;
        if (obj.CompactSubObj.HasValue && obj.CompactSubObj.Value > 0)
        {
            maxSubIndex = Math.Max(maxSubIndex, obj.CompactSubObj.Value);
        }

        for (byte subIndex = 0; subIndex <= maxSubIndex; subIndex++)
        {
            var subObj = ParseSubObject(sections, index, subIndex);
            if (subObj != null)
            {
                obj.SubObjects[subIndex] = subObj;
            }
        }
    }

    /// <summary>
    /// Parses a single sub-object at the given <paramref name="index"/> and <paramref name="subIndex"/>.
    /// Returns <see langword="null"/> if no section exists for that sub-object.
    /// Derived classes may override this to read additional format-specific fields.
    /// </summary>
    protected virtual CanOpenSubObject? ParseSubObject(Dictionary<string, Dictionary<string, string>> sections, ushort index, byte subIndex)
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

    /// <summary>
    /// Parses the <c>[Comments]</c> section into a <see cref="Comments"/> object,
    /// or returns <see langword="null"/> if the section is absent.
    /// </summary>
    protected Comments? ParseComments(Dictionary<string, Dictionary<string, string>> sections)
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

    /// <summary>
    /// Parses the <c>[SupportedModules]</c> section and each module's <c>ModuleInfo</c>
    /// section into a list of <see cref="ModuleInfo"/> objects.
    /// </summary>
    protected List<ModuleInfo> ParseSupportedModules(Dictionary<string, Dictionary<string, string>> sections)
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

    /// <summary>
    /// Parses the <c>[M{moduleNumber}ModuleInfo]</c> section for the given module number.
    /// Returns <see langword="null"/> if the section does not exist.
    /// </summary>
    protected ModuleInfo? ParseModuleInfo(Dictionary<string, Dictionary<string, string>> sections, int moduleNumber)
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

    /// <summary>
    /// Parses the <c>[DynamicChannels]</c> section into a <see cref="DynamicChannels"/> object,
    /// or returns <see langword="null"/> if the section has no segments.
    /// </summary>
    protected DynamicChannels? ParseDynamicChannels(Dictionary<string, Dictionary<string, string>> sections)
    {
        var nrOfSeg = ValueConverter.ParseByte(IniParser.GetValue(sections, "DynamicChannels", "NrOfSeg", "0"));
        if (nrOfSeg == 0)
            return null;

        var dynamicChannels = new DynamicChannels();

        for (int i = 1; i <= nrOfSeg; i++)
        {
            var idx = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var segment = new DynamicChannelSegment
            {
                Type = ValueConverter.ParseUInt16(IniParser.GetValue(sections, "DynamicChannels", $"Type{idx}", "0")),
                Dir = ValueConverter.ParseAccessType(IniParser.GetValue(sections, "DynamicChannels", $"Dir{idx}")),
                Range = IniParser.GetValue(sections, "DynamicChannels", $"Range{idx}"),
                PPOffset = ValueConverter.ParseInteger(IniParser.GetValue(sections, "DynamicChannels", $"PPOffset{idx}", "0"))
            };
            dynamicChannels.Segments.Add(segment);
        }

        return dynamicChannels;
    }

    /// <summary>
    /// Parses the <c>[Tools]</c> section and each individual <c>[Tool{n}]</c> section
    /// into a list of <see cref="ToolInfo"/> objects.
    /// </summary>
    protected List<ToolInfo> ParseTools(Dictionary<string, Dictionary<string, string>> sections)
    {
        var tools = new List<ToolInfo>();

        var items = ValueConverter.ParseByte(IniParser.GetValue(sections, "Tools", "Items", "0"));

        for (int i = 1; i <= items; i++)
        {
            var toolSection = "Tool" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!IniParser.HasSection(sections, toolSection))
                continue;

            var tool = new ToolInfo
            {
                Name = IniParser.GetValue(sections, toolSection, "Name"),
                Command = IniParser.GetValue(sections, toolSection, "Command")
            };
            tools.Add(tool);
        }

        return tools;
    }

    /// <summary>
    /// Determines whether <paramref name="sectionName"/> is a known section for this file format.
    /// Unknown sections are preserved in <c>AdditionalSections</c> for round-trip fidelity.
    /// Derived classes may override this to recognise additional format-specific sections.
    /// </summary>
    protected virtual bool IsKnownSection(string sectionName)
    {
        if (KnownSectionNames.Contains(sectionName, StringComparer.OrdinalIgnoreCase))
            return true;

        // Check for object sections (hex index)
        if (ushort.TryParse(sectionName, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out _))
            return true;

        // Check for sub-object sections (hex index + "sub" + hex subindex)
        if (IsSubObjectSection(sectionName))
            return true;

        // Check for module sections (M + digits + known suffix)
        if (IsModuleSection(sectionName))
            return true;

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="sectionName"/> matches a
    /// <c>[Tool{n}]</c> section for one of the already-parsed tools (1 ≤ n ≤ <paramref name="parsedToolCount"/>).
    /// Used to avoid treating tool data sections as unknown additional sections.
    /// </summary>
    protected static bool IsToolSectionForParsedTools(string sectionName, int parsedToolCount)
    {
        if (!sectionName.StartsWith("Tool", StringComparison.OrdinalIgnoreCase) || sectionName.Length <= 4)
            return false;

        if (!int.TryParse(sectionName.Substring(4), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var toolNumber))
            return false;

        return toolNumber >= 1 && toolNumber <= parsedToolCount;
    }

    /// <summary>
    /// Checks if a section name matches the sub-object pattern: {HexIndex}sub{HexSubIndex}.
    /// </summary>
    protected static bool IsSubObjectSection(string sectionName)
    {
        var subPos = sectionName.IndexOf("sub", StringComparison.OrdinalIgnoreCase);
        if (subPos < 1)
            return false;

        var prefix = sectionName.Substring(0, subPos);
        return ushort.TryParse(prefix, System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out _);
    }

    /// <summary>
    /// Checks if a section name has a valid hex object index prefix followed by the given suffix.
    /// </summary>
    protected static bool IsHexPrefixedSection(string sectionName, string suffix)
    {
        if (!sectionName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return false;

        var prefix = sectionName.Substring(0, sectionName.Length - suffix.Length);
        return prefix.Length > 0 && ushort.TryParse(prefix,
            System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out _);
    }

    /// <summary>
    /// Checks if a section name matches a module section pattern: M{Digits}{KnownSuffix}.
    /// </summary>
    protected static bool IsModuleSection(string sectionName)
    {
        if (sectionName.Length < 2 ||
            !sectionName.StartsWith("M", StringComparison.OrdinalIgnoreCase))
            return false;

        // Must have at least one digit after "M"
        var i = 1;
        while (i < sectionName.Length && char.IsDigit(sectionName[i]))
            i++;

        if (i == 1)
            return false;

        // The suffix after "M{digits}" must be a known module suffix
        var suffix = sectionName.Substring(i);
        return suffix.Equals("ModuleInfo", StringComparison.OrdinalIgnoreCase) ||
               suffix.Equals("FixedObjects", StringComparison.OrdinalIgnoreCase) ||
               suffix.StartsWith("SubExtend", StringComparison.OrdinalIgnoreCase) ||
               suffix.StartsWith("SubExt", StringComparison.OrdinalIgnoreCase) ||
               suffix.Equals("Comments", StringComparison.OrdinalIgnoreCase);
    }
}
