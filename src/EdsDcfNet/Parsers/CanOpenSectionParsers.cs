namespace EdsDcfNet.Parsers;

using System.Globalization;

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

        // Parse fixed objects
        var fixedObjSection = string.Format(CultureInfo.InvariantCulture, "M{0}FixedObjects", moduleNumber);
        if (IniParser.HasSection(sections, fixedObjSection))
        {
            var count = ValueConverter.ParseUInt16(IniParser.GetValue(sections, fixedObjSection, "NrOfEntries", "0"));
            for (int i = 1; i <= count; i++)
            {
                var indexStr = IniParser.GetValue(sections, fixedObjSection, i.ToString(CultureInfo.InvariantCulture));
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
