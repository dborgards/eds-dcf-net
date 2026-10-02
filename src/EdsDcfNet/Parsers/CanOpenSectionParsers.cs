namespace EdsDcfNet.Parsers;

using System.Globalization;

using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

/// <summary>
/// Stateless parsers for shared CANopen INI sections. These helpers are pure
/// functions of the parsed <c>sections</c> dictionary and are shared by
/// <see cref="EdsReader"/> and <see cref="DcfReader"/>; format-specific
/// polymorphic parsing stays on <see cref="CanOpenReaderBase"/>.
/// </summary>
internal static class CanOpenSectionParsers
{
    /// <summary>
    /// Parses the <c>[DeviceInfo]</c> section into a <see cref="DeviceInfo"/> object.
    /// </summary>
    /// <remarks>
    /// <c>[DeviceInfo]</c> is <b>mandatory</b> per CiA 306-1 §5.2: every valid EDS and DCF
    /// file must contain this section. Without it the library cannot determine basic device
    /// identity (vendor, product, supported baud rates), so an <see cref="EdsParseException"/>
    /// is thrown rather than silently returning an empty or misleading object.
    /// </remarks>
    /// <exception cref="EdsParseException">Thrown when the <c>[DeviceInfo]</c> section is absent.</exception>
    internal static DeviceInfo ParseDeviceInfo(Dictionary<string, Dictionary<string, string>> sections)
    {
        var deviceInfo = new DeviceInfo();

        // [DeviceInfo] is mandatory (CiA 306-1 §5.2) — reject the file when absent.
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
    /// Parses the <c>[Comments]</c> section into a <see cref="Comments"/> object,
    /// or returns <see langword="null"/> if the section is absent.
    /// </summary>
    internal static Comments? ParseComments(Dictionary<string, Dictionary<string, string>> sections)
    {
        if (!IniParser.HasSection(sections, "Comments"))
            return null;

        var comments = new Comments
        {
            Lines = ValueConverter.ParseUInt16(IniParser.GetValue(sections, "Comments", "Lines", "0"))
        };

        for (int i = 1; i <= comments.Lines; i++)
        {
            var line = IniParser.GetValue(sections, "Comments", string.Format(CultureInfo.InvariantCulture, "Line{0}", i));
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
    internal static List<ModuleInfo> ParseSupportedModules(Dictionary<string, Dictionary<string, string>> sections)
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
    internal static ModuleInfo? ParseModuleInfo(Dictionary<string, Dictionary<string, string>> sections, int moduleNumber)
    {
        var sectionName = string.Format(CultureInfo.InvariantCulture, "M{0}ModuleInfo", moduleNumber);
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

        // Parse fixed objects (index list, then [MxFixedxxxx] / [MxFixedxxxxsubx] bodies).
        var fixedObjSection = string.Format(CultureInfo.InvariantCulture, "M{0}FixedObjects", moduleNumber);
        if (IniParser.HasSection(sections, fixedObjSection))
        {
            LenientIniNumber.AppendIndexes(sections, fixedObjSection, "NrOfEntries", moduleInfo.FixedObjects);
        }

        ParseModuleComments(sections, moduleNumber, moduleInfo);
        ParseModuleFixedObjectDefinitions(sections, moduleNumber, moduleInfo);
        ParseModuleSubExtends(sections, moduleNumber, moduleInfo);
        ParseModuleSubExtensionDefinitions(sections, moduleNumber, moduleInfo);

        return moduleInfo;
    }

    /// <summary>
    /// Parses optional <c>[MxComments]</c> (CiA 306-1 §8.3): <c>Lines</c> and <c>Line&lt;n&gt;</c>.
    /// A present key with an empty value is a blank line and is stored. A missing key is not.
    /// </summary>
    private static void ParseModuleComments(
        Dictionary<string, Dictionary<string, string>> sections,
        int moduleNumber,
        ModuleInfo moduleInfo)
    {
        var sectionName = string.Format(CultureInfo.InvariantCulture, "M{0}Comments", moduleNumber);
        if (!sections.TryGetValue(sectionName, out var section))
            return;

        var comments = new Comments
        {
            Lines = ValueConverter.ParseUInt16(IniParser.GetValue(sections, sectionName, "Lines", "0"))
        };

        for (var i = 1; i <= comments.Lines; i++)
        {
            var key = string.Format(CultureInfo.InvariantCulture, "Line{0}", i);
            if (section.TryGetValue(key, out var line))
                comments.CommentLines[i] = line;
        }

        moduleInfo.Comments = comments;
    }

    /// <summary>
    /// Parses <c>[MxSubExtends]</c> (CiA 306-1 §8.3): <c>NrOfEntries</c> and the numbered index list.
    /// </summary>
    private static void ParseModuleSubExtends(
        Dictionary<string, Dictionary<string, string>> sections,
        int moduleNumber,
        ModuleInfo moduleInfo)
    {
        var sectionName = string.Format(CultureInfo.InvariantCulture, "M{0}SubExtends", moduleNumber);
        if (!IniParser.HasSection(sections, sectionName))
            return;

        LenientIniNumber.AppendIndexes(sections, sectionName, "NrOfEntries", moduleInfo.SubExtends);
    }

    /// <summary>
    /// Parses every <c>[MxSubExtxxxx]</c> section for this module into
    /// <see cref="ModuleInfo.SubExtensionDefinitions"/>, including sections whose
    /// index is not listed in <c>[MxSubExtends]</c>.
    /// </summary>
    private static void ParseModuleSubExtensionDefinitions(
        Dictionary<string, Dictionary<string, string>> sections,
        int moduleNumber,
        ModuleInfo moduleInfo)
    {
        foreach (var sectionName in sections.Keys)
        {
            if (!TryParseSubExtSection(sectionName, moduleNumber, out var index))
                continue;

            moduleInfo.SubExtensionDefinitions[index] = ReadSubExtension(sections, sectionName, index);
        }
    }

    /// <summary>
    /// Parses <c>[MxFixedxxxx]</c> bodies and <c>[MxFixedxxxxsubx]</c> sub-objects
    /// (CiA 306-1 §8.3) into <see cref="ModuleInfo.FixedObjectDefinitions"/>.
    /// </summary>
    private static void ParseModuleFixedObjectDefinitions(
        Dictionary<string, Dictionary<string, string>> sections,
        int moduleNumber,
        ModuleInfo moduleInfo)
    {
        var objects = new Dictionary<ushort, CanOpenObject>();
        var subSections = new List<(ushort Index, byte SubIndex, string SectionName)>();

        foreach (var sectionName in sections.Keys)
        {
            if (!TryParseFixedObjectSection(sectionName, moduleNumber, out var index, out var subIndex, out var isSubObject))
                continue;

            if (isSubObject)
            {
                subSections.Add((index, subIndex, sectionName));
                continue;
            }

            if (!objects.ContainsKey(index))
            {
                objects[index] = ReadFixedObject(sections, sectionName, index);
            }
        }

        foreach (var subSection in subSections)
        {
            if (!objects.TryGetValue(subSection.Index, out var parent))
            {
                parent = new CanOpenObject { Index = subSection.Index };
                objects[subSection.Index] = parent;
            }

            parent.SubObjects[subSection.SubIndex] = ReadFixedSubObject(
                sections,
                subSection.SectionName,
                subSection.SubIndex);
        }

        foreach (var entry in objects)
        {
            moduleInfo.FixedObjectDefinitions[entry.Key] = entry.Value;
        }
    }

    /// <summary>
    /// True when <paramref name="sectionName"/> was stored on a parsed module as
    /// <c>[MxFixedxxxx]</c> or <c>[MxFixedxxxxsubx]</c> (CiA 306-1 §8.3).
    /// A body whose module is not in <paramref name="modules"/> is left for
    /// <c>AdditionalSections</c>.
    /// </summary>
    internal static bool IsConsumedModuleFixedSection(string sectionName, IReadOnlyList<ModuleInfo> modules)
    {
        foreach (var module in modules)
        {
            if (!TryParseFixedObjectSection(
                    sectionName,
                    module.ModuleNumber,
                    out var index,
                    out var subIndex,
                    out var isSubObject))
            {
                continue;
            }

            if (!module.FixedObjectDefinitions.TryGetValue(index, out var obj))
                return false;

            return !isSubObject || obj.SubObjects.ContainsKey(subIndex);
        }

        return false;
    }

    private static ModuleSubExtension ReadSubExtension(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        ushort index)
    {
        var extension = new ModuleSubExtension
        {
            Index = index,
            ParameterName = IniParser.GetValue(sections, sectionName, "ParameterName"),
            DataType = LenientIniNumber.ParseUInt16(
                sections,
                sectionName,
                "DataType",
                IniParser.GetValue(sections, sectionName, "DataType", "0"),
                fallback: 0,
                code: ParseDiagnosticCodes.InvalidDataType,
                coercedTo: "0",
                fallbackDescription: LenientIniNumber.TreatAsZero),
            AccessType = ValueConverter.ParseAccessType(IniParser.GetValue(sections, sectionName, "AccessType")),
            DefaultValue = IniParser.GetValue(sections, sectionName, "DefaultValue"),
            PdoMapping = ValueConverter.ParseBoolean(IniParser.GetValue(sections, sectionName, "PDOMapping")),
            Count = IniParser.GetValue(sections, sectionName, "Count")
        };

        var objExtend = IniParser.GetValue(sections, sectionName, "ObjExtend");
        if (!string.IsNullOrEmpty(objExtend))
        {
            extension.ObjExtend = LenientIniNumber.ParseOptionalByte(
                sections,
                sectionName,
                "ObjExtend",
                objExtend,
                ParseDiagnosticCodes.InvalidModuleObjExtend,
                LenientIniNumber.LeaveUnset);
        }

        return extension;
    }

    private static CanOpenObject ReadFixedObject(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        ushort index)
    {
        var obj = new CanOpenObject
        {
            Index = index,
            ParameterName = IniParser.GetValue(sections, sectionName, "ParameterName"),
            ObjectType = LenientIniNumber.ParseByte(
                sections,
                sectionName,
                "ObjectType",
                IniParser.GetValue(sections, sectionName, "ObjectType", CanOpenObjectType.VarLiteral),
                fallback: CanOpenObjectType.Var,
                code: ParseDiagnosticCodes.InvalidObjectType,
                coercedTo: CanOpenObjectType.VarLiteral,
                fallbackDescription: LenientIniNumber.TreatAsVar),
            AccessType = ValueConverter.ParseAccessType(IniParser.GetValue(sections, sectionName, "AccessType")),
            DefaultValue = IniParser.GetValue(sections, sectionName, "DefaultValue"),
            LowLimit = IniParser.GetValue(sections, sectionName, "LowLimit"),
            HighLimit = IniParser.GetValue(sections, sectionName, "HighLimit"),
            PdoMapping = ValueConverter.ParseBoolean(IniParser.GetValue(sections, sectionName, "PDOMapping")),
            SrdoMapping = ValueConverter.ParseBoolean(IniParser.GetValue(sections, sectionName, "SRDOMapping")),
            InvertedSrad = IniParser.GetValue(sections, sectionName, "InvertedSRAD"),
            ObjFlags = LenientIniNumber.ParseUInt32(
                sections,
                sectionName,
                "ObjFlags",
                IniParser.GetValue(sections, sectionName, "ObjFlags", "0"),
                fallback: 0,
                code: ParseDiagnosticCodes.InvalidObjFlags,
                coercedTo: "0",
                fallbackDescription: LenientIniNumber.TreatAsZero),
            ParameterValue = IniParser.GetValue(sections, sectionName, "ParameterValue"),
            Denotation = IniParser.GetValue(sections, sectionName, "Denotation"),
            ParamRefd = IniParser.GetValue(sections, sectionName, "ParamRefd"),
            UploadFile = IniParser.GetValue(sections, sectionName, "UploadFile"),
            DownloadFile = IniParser.GetValue(sections, sectionName, "DownloadFile")
        };

        var dataType = IniParser.GetValue(sections, sectionName, "DataType");
        if (!string.IsNullOrEmpty(dataType))
        {
            obj.DataType = LenientIniNumber.ParseOptionalUInt16(
                sections,
                sectionName,
                "DataType",
                dataType,
                ParseDiagnosticCodes.InvalidDataType,
                LenientIniNumber.LeaveUnset);
        }

        var subNumber = IniParser.GetValue(sections, sectionName, "SubNumber");
        if (!string.IsNullOrEmpty(subNumber))
        {
            obj.SubNumber = LenientIniNumber.ParseOptionalByte(
                sections,
                sectionName,
                "SubNumber",
                subNumber,
                ParseDiagnosticCodes.InvalidSubNumber,
                LenientIniNumber.LeaveUnset);
        }

        var compactSubObj = IniParser.GetValue(sections, sectionName, "CompactSubObj");
        if (!string.IsNullOrEmpty(compactSubObj))
        {
            obj.CompactSubObj = LenientIniNumber.ParseOptionalByte(
                sections,
                sectionName,
                "CompactSubObj",
                compactSubObj,
                ParseDiagnosticCodes.InvalidCompactSubObj,
                LenientIniNumber.LeaveUnset);
        }

        CanOpenReaderBase.CaptureRemainingEntries(
            sections,
            sectionName,
            SectionEntryKeys.IsDcfObjectKey,
            obj.RemainingEntries);

        return obj;
    }

    private static CanOpenSubObject ReadFixedSubObject(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        byte subIndex)
    {
        var subObj = new CanOpenSubObject
        {
            SubIndex = subIndex,
            ParameterName = IniParser.GetValue(sections, sectionName, "ParameterName"),
            ObjectType = LenientIniNumber.ParseByte(
                sections,
                sectionName,
                "ObjectType",
                IniParser.GetValue(sections, sectionName, "ObjectType", CanOpenObjectType.VarLiteral),
                fallback: CanOpenObjectType.Var,
                code: ParseDiagnosticCodes.InvalidObjectType,
                coercedTo: CanOpenObjectType.VarLiteral,
                fallbackDescription: LenientIniNumber.TreatAsVar),
            DataType = LenientIniNumber.ParseUInt16(
                sections,
                sectionName,
                "DataType",
                IniParser.GetValue(sections, sectionName, "DataType", "0"),
                fallback: 0,
                code: ParseDiagnosticCodes.InvalidDataType,
                coercedTo: "0",
                fallbackDescription: LenientIniNumber.TreatAsZero),
            AccessType = ValueConverter.ParseAccessType(IniParser.GetValue(sections, sectionName, "AccessType")),
            DefaultValue = IniParser.GetValue(sections, sectionName, "DefaultValue"),
            LowLimit = IniParser.GetValue(sections, sectionName, "LowLimit"),
            HighLimit = IniParser.GetValue(sections, sectionName, "HighLimit"),
            PdoMapping = ValueConverter.ParseBoolean(IniParser.GetValue(sections, sectionName, "PDOMapping")),
            SrdoMapping = ValueConverter.ParseBoolean(IniParser.GetValue(sections, sectionName, "SRDOMapping")),
            InvertedSrad = IniParser.GetValue(sections, sectionName, "InvertedSRAD"),
            ParameterValue = IniParser.GetValue(sections, sectionName, "ParameterValue"),
            Denotation = IniParser.GetValue(sections, sectionName, "Denotation"),
            ParamRefd = IniParser.GetValue(sections, sectionName, "ParamRefd")
        };

        CanOpenReaderBase.CaptureRemainingEntries(
            sections,
            sectionName,
            SectionEntryKeys.IsDcfSubObjectKey,
            subObj.RemainingEntries);

        return subObj;
    }

    private static bool TryParseSubExtSection(string sectionName, int moduleNumber, out ushort index)
    {
        index = 0;
        if (!TryParseModuleSuffix(sectionName, moduleNumber, out var suffix) ||
            !suffix.StartsWith("SubExt", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = suffix[6..];
        return rest.Length > 0 &&
               IsHexDigitsOnly(rest) &&
               ushort.TryParse(rest, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out index);
    }

    private static bool TryParseFixedObjectSection(
        string sectionName,
        int moduleNumber,
        out ushort index,
        out byte subIndex,
        out bool isSubObject)
    {
        index = 0;
        subIndex = 0;
        isSubObject = false;

        if (!TryParseModuleSuffix(sectionName, moduleNumber, out var suffix) ||
            !suffix.StartsWith("Fixed", StringComparison.OrdinalIgnoreCase) ||
            suffix.Equals("FixedObjects", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = suffix[5..];
        var subPos = rest.IndexOf("sub", StringComparison.OrdinalIgnoreCase);
        if (subPos < 0)
        {
            return rest.Length > 0 &&
                   IsHexDigitsOnly(rest) &&
                   ushort.TryParse(rest, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out index);
        }

        if (subPos == 0)
            return false;

        var indexPart = rest[..subPos];
        var subPart = rest[(subPos + 3)..];
        if (!IsHexDigitsOnly(indexPart) ||
            !IsHexDigitsOnly(subPart) ||
            !ushort.TryParse(indexPart, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out index) ||
            !byte.TryParse(subPart, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out subIndex))
        {
            return false;
        }

        isSubObject = true;
        return true;
    }

    private static bool TryParseModuleSuffix(string sectionName, int moduleNumber, out string suffix)
    {
        suffix = string.Empty;
        if (sectionName.Length < 2 ||
            !sectionName.StartsWith("M", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var i = 1;
        while (i < sectionName.Length && char.IsDigit(sectionName[i]))
            i++;

        if (i == 1 ||
            !int.TryParse(sectionName[1..i], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
            parsed != moduleNumber)
        {
            return false;
        }

        suffix = sectionName[i..];
        return true;
    }

    private static bool IsHexDigitsOnly(string value)
    {
        if (value.Length == 0)
            return false;

        foreach (var c in value)
        {
            var isHexDigit = (c >= '0' && c <= '9') ||
                             (c >= 'a' && c <= 'f') ||
                             (c >= 'A' && c <= 'F');
            if (!isHexDigit)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Parses the <c>[DynamicChannels]</c> section into a <see cref="DynamicChannels"/> object,
    /// or returns <see langword="null"/> if the section has no segments.
    /// </summary>
    internal static DynamicChannels? ParseDynamicChannels(Dictionary<string, Dictionary<string, string>> sections)
    {
        var nrOfSeg = ValueConverter.ParseByte(IniParser.GetValue(sections, "DynamicChannels", "NrOfSeg", "0"));
        if (nrOfSeg == 0)
            return null;

        var dynamicChannels = new DynamicChannels();

        for (int i = 1; i <= nrOfSeg; i++)
        {
            var segment = new DynamicChannelSegment
            {
                Type = ValueConverter.ParseUInt16(IniParser.GetValue(sections, "DynamicChannels", string.Format(CultureInfo.InvariantCulture, "Type{0}", i), "0")),
                Dir = ValueConverter.ParseAccessType(IniParser.GetValue(sections, "DynamicChannels", string.Format(CultureInfo.InvariantCulture, "Dir{0}", i))),
                Range = IniParser.GetValue(sections, "DynamicChannels", string.Format(CultureInfo.InvariantCulture, "Range{0}", i)),
                PPOffset = ValueConverter.ParseInteger(IniParser.GetValue(sections, "DynamicChannels", string.Format(CultureInfo.InvariantCulture, "PPOffset{0}", i), "0"))
            };
            dynamicChannels.Segments.Add(segment);
        }

        return dynamicChannels;
    }

    /// <summary>
    /// Parses the <c>[Tools]</c> section and each individual <c>[Tool{n}]</c> section
    /// into a list of <see cref="ToolInfo"/> objects.
    /// </summary>
    internal static List<ToolInfo> ParseTools(Dictionary<string, Dictionary<string, string>> sections)
    {
        var tools = new List<ToolInfo>();

        var items = ValueConverter.ParseByte(IniParser.GetValue(sections, "Tools", "Items", "0"));

        for (int i = 1; i <= items; i++)
        {
            var toolSection = "Tool" + i.ToString(CultureInfo.InvariantCulture);
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
}
