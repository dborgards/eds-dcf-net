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
    /// Copies the entries of <paramref name="sectionName"/> that <paramref name="isKnownKey"/>
    /// rejects into <paramref name="destination"/>, in file order. Does nothing when the section
    /// is absent. A key already present in <paramref name="destination"/> is left unchanged.
    /// </summary>
    /// <remarks>
    /// CiA 306-1 § 6.2 allows additional entries inside the standard sections; a section the
    /// reader processes must keep them so the writer can emit them again.
    /// </remarks>
    internal static void CaptureUnmappedEntries(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        Func<string, bool> isKnownKey,
        OrderedStringDictionary destination)
    {
        if (!sections.TryGetValue(sectionName, out var section))
            return;

        foreach (var entry in EntriesInFileOrder(section))
        {
            if (isKnownKey(entry.Key) || destination.ContainsKey(entry.Key))
                continue;

            destination.Add(entry.Key, entry.Value);
        }
    }

    /// <summary>
    /// Like <see cref="CaptureUnmappedEntries(Dictionary{string, Dictionary{string, string}}, string, Func{string, bool}, OrderedStringDictionary)"/>,
    /// for sections without a model object of their own: the entries go to
    /// <paramref name="store"/> under <paramref name="canonicalName"/>, the section name the
    /// writer emits. No store entry is created when every key is known.
    /// </summary>
    internal static void CaptureUnmappedEntries(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string canonicalName,
        Func<string, bool> isKnownKey,
        Dictionary<string, OrderedStringDictionary> store)
        => CaptureUnmappedEntries(sections, sectionName, canonicalName, (key, _) => isKnownKey(key), store);

    /// <summary>
    /// Like the overload with a key predicate, for list sections where a slot counts as
    /// processed only with a usable value: <paramref name="isProcessedEntry"/> gets key and value.
    /// </summary>
    internal static void CaptureUnmappedEntries(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string canonicalName,
        Func<string, string, bool> isProcessedEntry,
        Dictionary<string, OrderedStringDictionary> store)
    {
        if (!sections.TryGetValue(sectionName, out var section))
            return;

        foreach (var entry in EntriesInFileOrder(section))
        {
            if (isProcessedEntry(entry.Key, entry.Value))
                continue;

            if (!store.TryGetValue(canonicalName, out var destination))
            {
                destination = new OrderedStringDictionary();
                store[canonicalName] = destination;
            }

            if (!destination.ContainsKey(entry.Key))
                destination.Add(entry.Key, entry.Value);
        }
    }

    /// <summary>
    /// The entry count of a counted list as the list parsers use it: a malformed or absent
    /// count is <c>0</c> (lenient default; strict mode has already thrown while parsing the list).
    /// </summary>
    internal static int ListCountOrZero(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string countKey)
    {
        try
        {
            return ValueConverter.ParseUInt16(IniParser.GetValue(sections, sectionName, countKey, "0"));
        }
        catch (EdsParseException)
        {
            return 0;
        }
    }

    private static IEnumerable<KeyValuePair<string, string>> EntriesInFileOrder(Dictionary<string, string> section)
        => section is IniSectionDictionary ordered ? ordered.EntriesInOrder() : section;

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
        deviceInfo.VendorNumber = DeviceInfoUInt32(sections, "VendorNumber");
        deviceInfo.ProductName = IniParser.GetValue(sections, "DeviceInfo", "ProductName");
        deviceInfo.ProductNumber = DeviceInfoUInt32(sections, "ProductNumber");
        deviceInfo.RevisionNumber = DeviceInfoUInt32(sections, "RevisionNumber");
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
        deviceInfo.Granularity = DeviceInfoByte(sections, "Granularity", 8);
        deviceInfo.DynamicChannelsSupported = DeviceInfoByte(sections, "DynamicChannelsSupported", 0);
        deviceInfo.GroupMessaging = ValueConverter.ParseBoolean(IniParser.GetValue(sections, "DeviceInfo", "GroupMessaging"));
        deviceInfo.NrOfRxPdo = DeviceInfoUInt16(sections, "NrOfRXPDO");
        deviceInfo.NrOfTxPdo = DeviceInfoUInt16(sections, "NrOfTXPDO");
        deviceInfo.LssSupported = ValueConverter.ParseBoolean(IniParser.GetValue(sections, "DeviceInfo", "LSS_Supported"));
        deviceInfo.CompactPdo = DeviceInfoByte(sections, "CompactPDO", 0);
        deviceInfo.CANopenSafetySupported = ValueConverter.ParseBoolean(IniParser.GetValue(sections, "DeviceInfo", "CANopenSafetySupported"));

        // Includes the entries § 6.5 reserves for compatibility (ProductVersion, LMT_*, ExtendedBootUp*).
        CaptureUnmappedEntries(sections, "DeviceInfo", SectionEntryKeys.IsDeviceInfoKey, deviceInfo.RemainingEntries);

        return deviceInfo;
    }

    private static uint DeviceInfoUInt32(Dictionary<string, Dictionary<string, string>> sections, string key)
        => LenientIniNumber.ParseUInt32(
            sections,
            "DeviceInfo",
            key,
            IniParser.GetValue(sections, "DeviceInfo", key, "0"),
            fallback: 0,
            code: ParseDiagnosticCodes.InvalidDeviceInfoNumber,
            coercedTo: "0",
            fallbackDescription: LenientIniNumber.TreatAsZero);

    private static ushort DeviceInfoUInt16(Dictionary<string, Dictionary<string, string>> sections, string key)
        => LenientIniNumber.ParseUInt16(
            sections,
            "DeviceInfo",
            key,
            IniParser.GetValue(sections, "DeviceInfo", key, "0"),
            fallback: 0,
            code: ParseDiagnosticCodes.InvalidDeviceInfoNumber,
            coercedTo: "0",
            fallbackDescription: LenientIniNumber.TreatAsZero);

    private static byte DeviceInfoByte(
        Dictionary<string, Dictionary<string, string>> sections,
        string key,
        byte defaultValue)
    {
        var defaultText = defaultValue.ToString(CultureInfo.InvariantCulture);
        return LenientIniNumber.ParseByte(
            sections,
            "DeviceInfo",
            key,
            IniParser.GetValue(sections, "DeviceInfo", key, defaultText),
            defaultValue,
            ParseDiagnosticCodes.InvalidDeviceInfoNumber,
            coercedTo: defaultText,
            fallbackDescription: "Treated as " + defaultText + ".");
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

        // Only stored lines count as processed: an empty Line<n> is not added to CommentLines
        // and is kept verbatim instead.
        CaptureUnmappedEntries(
            sections,
            "Comments",
            key => SectionEntryKeys.IsGeneratedCommentsKey(key, comments.CommentLines.Keys),
            comments.RemainingEntries);

        return comments;
    }

    /// <summary>
    /// Parses the <c>[SupportedModules]</c> section and each module's <c>ModuleInfo</c>
    /// section into a list of <see cref="ModuleInfo"/> objects.
    /// </summary>
    internal static List<ModuleInfo> ParseSupportedModules(Dictionary<string, Dictionary<string, string>> sections)
        => ParseSupportedModules(sections, new Dictionary<string, OrderedStringDictionary>(StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Parses <c>[SupportedModules]</c> and the module sections, keeping unmapped entries of
    /// <c>[SupportedModules]</c>, <c>[MxModuleInfo]</c>, <c>[MxFixedObjects]</c>,
    /// <c>[MxSubExtends]</c> and <c>[MxSubExtxxxx]</c> in <paramref name="store"/>.
    /// </summary>
    internal static List<ModuleInfo> ParseSupportedModules(
        Dictionary<string, Dictionary<string, string>> sections,
        Dictionary<string, OrderedStringDictionary> store)
    {
        var modules = new List<ModuleInfo>();
        var count = ValueConverter.ParseUInt16(IniParser.GetValue(sections, "SupportedModules", "NrOfEntries", "0"));

        for (int i = 1; i <= count; i++)
        {
            var moduleInfo = ParseModuleInfo(sections, i, store);
            if (moduleInfo != null)
            {
                modules.Add(moduleInfo);
            }
        }

        CaptureUnmappedEntries(sections, "SupportedModules", "SupportedModules", SectionEntryKeys.IsSupportedModulesKey, store);

        return modules;
    }

    /// <summary>
    /// Parses the <c>[M{moduleNumber}ModuleInfo]</c> section for the given module number.
    /// Returns <see langword="null"/> if the section does not exist.
    /// </summary>
    internal static ModuleInfo? ParseModuleInfo(Dictionary<string, Dictionary<string, string>> sections, int moduleNumber)
        => ParseModuleInfo(sections, moduleNumber, new Dictionary<string, OrderedStringDictionary>(StringComparer.OrdinalIgnoreCase));

    private static ModuleInfo? ParseModuleInfo(
        Dictionary<string, Dictionary<string, string>> sections,
        int moduleNumber,
        Dictionary<string, OrderedStringDictionary> store)
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
        ParseModuleSubExtensionDefinitions(sections, moduleNumber, moduleInfo, store);

        // One store entry per module section: the same vendor key may appear in several of
        // them with different values (CiA 306-1 § 8.3).
        CaptureUnmappedEntries(sections, sectionName, sectionName, SectionEntryKeys.IsModuleInfoKey, store);
        CaptureCountedListEntries(sections, fixedObjSection, "NrOfEntries", store);
        CaptureCountedListEntries(
            sections,
            string.Format(CultureInfo.InvariantCulture, "M{0}SubExtends", moduleNumber),
            "NrOfEntries",
            store);

        return moduleInfo;
    }

    /// <summary>
    /// Keeps the entries of a counted list section that the list parser does not load: every
    /// key except the count key and the numbered slots <c>1..count</c> whose value was loaded.
    /// This includes numbered entries above the count and slots inside the count that lenient
    /// parsing skips because the value is empty or invalid.
    /// </summary>
    /// <param name="sections">Parsed INI sections.</param>
    /// <param name="sectionName">The list section.</param>
    /// <param name="countKey">The count key of the list.</param>
    /// <param name="store">Destination, keyed by <paramref name="sectionName"/>.</param>
    /// <param name="isLoadedValue">
    /// <see langword="true"/> when the list parser loads a slot with this value. The default
    /// mirrors <see cref="LenientIniNumber.AppendIndexes"/>: a non-empty object index.
    /// </param>
    internal static void CaptureCountedListEntries(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string countKey,
        Dictionary<string, OrderedStringDictionary> store,
        Func<string, bool>? isLoadedValue = null)
    {
        if (!sections.ContainsKey(sectionName))
            return;

        var isLoaded = isLoadedValue ?? IsLoadableIndexValue;
        var count = ListCountOrZero(sections, sectionName, countKey);
        CaptureUnmappedEntries(
            sections,
            sectionName,
            sectionName,
            (key, value) => string.Equals(key, countKey, StringComparison.OrdinalIgnoreCase)
                            || (SectionEntryKeys.IsCountedListKey(key, countKey, count) && isLoaded(value)),
            store);
    }

    /// <summary>
    /// Keeps the entries of a compact sub-object list (<c>[xxxxName]</c>, DCF
    /// <c>[xxxxValue]</c> / <c>[xxxxDenotation]</c>) that the reader does not apply: every key
    /// except <c>NrOfEntries</c> and sub-index keys with a non-empty value for an existing
    /// sub-object of <paramref name="obj"/>. This mirrors <c>ApplyCompactListSection</c>, which
    /// skips empty values and sub-indexes without a sub-object.
    /// </summary>
    internal static void CaptureCompactListEntries(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        CanOpenObject obj,
        Dictionary<string, OrderedStringDictionary> store)
    {
        CaptureUnmappedEntries(
            sections,
            sectionName,
            sectionName,
            (key, value) => string.Equals(key, SectionEntryKeys.NrOfEntriesKey, StringComparison.OrdinalIgnoreCase)
                            || (!string.IsNullOrEmpty(value)
                                && SectionEntryKeys.IsAppliedCompactListKey(key, obj.SubObjects.Keys)),
            store);
    }

    /// <summary>
    /// <see langword="true"/> when <see cref="LenientIniNumber.AppendIndexes"/> loads a slot with
    /// <paramref name="value"/>: not empty and a valid UNSIGNED16 index.
    /// </summary>
    private static bool IsLoadableIndexValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        try
        {
            _ = ValueConverter.ParseUInt16(value);
            return true;
        }
        catch (EdsParseException)
        {
            return false;
        }
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

        CaptureUnmappedEntries(
            sections,
            sectionName,
            key => SectionEntryKeys.IsCommentsKey(key, comments.Lines),
            comments.RemainingEntries);

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
        ModuleInfo moduleInfo,
        Dictionary<string, OrderedStringDictionary> store)
    {
        foreach (var sectionName in sections.Keys)
        {
            if (!TryParseSubExtSection(sectionName, moduleNumber, out var index))
                continue;

            moduleInfo.SubExtensionDefinitions[index] = ReadSubExtension(sections, sectionName, index);

            // Keyed by the name the writer emits ([MxSubExt] + index without leading zeros).
            CaptureUnmappedEntries(
                sections,
                sectionName,
                string.Format(CultureInfo.InvariantCulture, "M{0}SubExt{1:X}", moduleNumber, index),
                SectionEntryKeys.IsModuleSubExtensionKey,
                store);
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
            LowLimit = EmptyToNull(IniParser.GetValue(sections, sectionName, "LowLimit")),
            HighLimit = EmptyToNull(IniParser.GetValue(sections, sectionName, "HighLimit")),
            PdoMapping = ValueConverter.ParseBoolean(IniParser.GetValue(sections, sectionName, "PDOMapping")),
            Count = IniParser.GetValue(sections, sectionName, "Count")
        };

        // CiA 306: a missing ObjectType is VAR. Store the entry only when the section has it,
        // so a later write does not invent ObjectType=0x7.
        var objectType = IniParser.GetValue(sections, sectionName, "ObjectType");
        if (!string.IsNullOrEmpty(objectType))
        {
            extension.ObjectType = LenientIniNumber.ParseByte(
                sections,
                sectionName,
                "ObjectType",
                objectType,
                fallback: CanOpenObjectType.Var,
                code: ParseDiagnosticCodes.InvalidObjectType,
                coercedTo: CanOpenObjectType.VarLiteral,
                fallbackDescription: LenientIniNumber.TreatAsVar);
        }

        var subNumber = IniParser.GetValue(sections, sectionName, "SubNumber");
        if (!string.IsNullOrEmpty(subNumber))
        {
            extension.SubNumber = LenientIniNumber.ParseOptionalByte(
                sections,
                sectionName,
                "SubNumber",
                subNumber,
                ParseDiagnosticCodes.InvalidSubNumber,
                LenientIniNumber.LeaveUnset);
        }

        var objFlags = IniParser.GetValue(sections, sectionName, "ObjFlags");
        if (!string.IsNullOrEmpty(objFlags))
        {
            extension.ObjFlags = LenientIniNumber.ParseUInt32(
                sections,
                sectionName,
                "ObjFlags",
                objFlags,
                fallback: 0,
                code: ParseDiagnosticCodes.InvalidObjFlags,
                coercedTo: "0",
                fallbackDescription: LenientIniNumber.TreatAsZero);
        }

        var compactSubObj = IniParser.GetValue(sections, sectionName, "CompactSubObj");
        if (!string.IsNullOrEmpty(compactSubObj))
        {
            extension.CompactSubObj = LenientIniNumber.ParseOptionalByte(
                sections,
                sectionName,
                "CompactSubObj",
                compactSubObj,
                ParseDiagnosticCodes.InvalidCompactSubObj,
                LenientIniNumber.LeaveUnset);
        }

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

    private static string? EmptyToNull(string value) => string.IsNullOrEmpty(value) ? null : value;

    /// <summary>
    /// CiA 306 treats a missing <c>ObjectType</c> as VAR (<c>0x7</c>). A present empty
    /// value is that same omission. <see cref="ValueConverter.ParseByte"/> would
    /// otherwise map <c>ObjectType=</c> to <c>0</c> (NULL).
    /// </summary>
    private static string FixedObjectTypeOrVar(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName)
    {
        var raw = IniParser.GetValue(sections, sectionName, "ObjectType", CanOpenObjectType.VarLiteral);
        return string.IsNullOrWhiteSpace(raw) ? CanOpenObjectType.VarLiteral : raw;
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
                FixedObjectTypeOrVar(sections, sectionName),
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
                FixedObjectTypeOrVar(sections, sectionName),
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
        var nrOfSeg = LenientIniNumber.ParseByte(
            sections,
            "DynamicChannels",
            "NrOfSeg",
            IniParser.GetValue(sections, "DynamicChannels", "NrOfSeg", "0"),
            fallback: 0,
            code: ParseDiagnosticCodes.InvalidDynamicChannelCount,
            coercedTo: "0",
            fallbackDescription: LenientIniNumber.TreatAsZero);
        var dynamicChannels = new DynamicChannels();
        CaptureUnmappedEntries(
            sections,
            "DynamicChannels",
            key => SectionEntryKeys.IsDynamicChannelsKey(key, nrOfSeg),
            dynamicChannels.RemainingEntries);

        // Without segments the section is only kept when it carries unmapped entries.
        if (nrOfSeg == 0)
            return dynamicChannels.RemainingEntries.Count > 0 ? dynamicChannels : null;

        for (int i = 1; i <= nrOfSeg; i++)
        {
            var typeKey = string.Format(CultureInfo.InvariantCulture, "Type{0}", i);
            var type = LenientIniNumber.ParseUInt16(
                sections,
                "DynamicChannels",
                typeKey,
                IniParser.GetValue(sections, "DynamicChannels", typeKey, "0"),
                fallback: 0,
                code: ParseDiagnosticCodes.InvalidDynamicChannelType,
                coercedTo: "0",
                fallbackDescription: LenientIniNumber.TreatAsZero);
            var ppOffsetKey = string.Format(CultureInfo.InvariantCulture, "PPOffset{0}", i);
            var ppOffset = LenientIniNumber.ParsePpOffset(
                sections,
                "DynamicChannels",
                ppOffsetKey,
                IniParser.GetValue(sections, "DynamicChannels", ppOffsetKey, "0"));
            var segment = new DynamicChannelSegment
            {
                Type = type,
                Dir = ValueConverter.ParseAccessType(IniParser.GetValue(sections, "DynamicChannels", string.Format(CultureInfo.InvariantCulture, "Dir{0}", i))),
                Range = IniParser.GetValue(sections, "DynamicChannels", string.Format(CultureInfo.InvariantCulture, "Range{0}", i)),
                PPOffset = ppOffset.Offset,
                PPOffsetAddressDifference = ppOffset.AddressDifference
            };
            dynamicChannels.Segments.Add(segment);
        }

        return dynamicChannels;
    }

    /// <summary>
    /// Parses the <c>[Tools]</c> section and each individual <c>[Tool{n}]</c> section
    /// into a list of <see cref="ToolInfo"/> objects.
    /// </summary>
    /// <summary>
    /// <see langword="true"/> when <see cref="ParseTools(Dictionary{string, Dictionary{string, string}}, Dictionary{string, OrderedStringDictionary})"/>
    /// reads <paramref name="sectionName"/> if the section is present: the canonical name
    /// <c>Tool&lt;n&gt;</c> with <c>1 &lt;= n &lt;= Items</c>. The reader uses this to keep a
    /// parsed tool section out of <c>AdditionalSections</c>.
    /// </summary>
    internal static bool IsParsedToolSection(Dictionary<string, Dictionary<string, string>> sections, string sectionName)
    {
        if (!sectionName.StartsWith("Tool", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(sectionName[4..], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || number < 1)
        {
            return false;
        }

        // ParseTools has already parsed Items successfully, so this cannot throw here.
        var items = ValueConverter.ParseByte(IniParser.GetValue(sections, "Tools", "Items", "0"));
        return number <= items
               && string.Equals(sectionName, "Tool" + number.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
    }

    internal static List<ToolInfo> ParseTools(Dictionary<string, Dictionary<string, string>> sections)
        =>ParseTools(sections, new Dictionary<string, OrderedStringDictionary>(StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Parses <c>[Tools]</c> and <c>[Tool{n}]</c>. Unmapped <c>[Tools]</c> entries go to
    /// <paramref name="store"/>; unmapped <c>[Tool{n}]</c> entries go to the tool.
    /// </summary>
    internal static List<ToolInfo> ParseTools(
        Dictionary<string, Dictionary<string, string>> sections,
        Dictionary<string, OrderedStringDictionary> store)
    {
        CaptureUnmappedEntries(sections, "Tools", "Tools", SectionEntryKeys.IsToolsKey, store);

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
            CaptureUnmappedEntries(sections, toolSection, SectionEntryKeys.IsToolKey, tool.RemainingEntries);
            tools.Add(tool);
        }

        return tools;
    }
}
