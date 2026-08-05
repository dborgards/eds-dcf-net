namespace EdsDcfNet.Writers;

using System.Globalization;
using System.Text;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

/// <summary>
/// Shared INI section emitters for EDS and DCF writers.
/// Contains all common serialization logic; format-specific behaviour
/// is handled via virtual methods that derived writers override.
/// </summary>
public abstract class IniWriterBase
{
    /// <summary>Writes a single INI key=value pair.</summary>
    protected static void WriteKeyValue(StringBuilder sb, string key, string? value)
    {
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}={1}", key, value));
    }

    /// <summary>Writes the [FileInfo] section (shared EDS/DCF fields, without LastEDS).</summary>
    protected static void WriteFileInfo(StringBuilder sb, EdsFileInfo fileInfo)
    {
        sb.AppendLine("[FileInfo]");
        WriteKeyValue(sb, "FileName", fileInfo.FileName);
        WriteKeyValue(sb, "FileVersion", fileInfo.FileVersion.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "FileRevision", fileInfo.FileRevision.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "EDSVersion", fileInfo.EdsVersion);
        WriteKeyValue(sb, "Description", fileInfo.Description);
        WriteKeyValue(sb, "CreationTime", fileInfo.CreationTime);
        WriteKeyValue(sb, "CreationDate", fileInfo.CreationDate);
        WriteKeyValue(sb, "CreatedBy", fileInfo.CreatedBy);
        WriteKeyValue(sb, "ModificationTime", fileInfo.ModificationTime);
        WriteKeyValue(sb, "ModificationDate", fileInfo.ModificationDate);
        WriteKeyValue(sb, "ModifiedBy", fileInfo.ModifiedBy);
    }

    /// <summary>Writes the [DeviceInfo] section.</summary>
    protected static void WriteDeviceInfo(StringBuilder sb, DeviceInfo deviceInfo)
    {
        sb.AppendLine("[DeviceInfo]");
        WriteKeyValue(sb, "VendorName", deviceInfo.VendorName);
        WriteKeyValue(sb, "VendorNumber", ValueConverter.FormatInteger(deviceInfo.VendorNumber));
        WriteKeyValue(sb, "ProductName", deviceInfo.ProductName);
        WriteKeyValue(sb, "ProductNumber", ValueConverter.FormatInteger(deviceInfo.ProductNumber));
        WriteKeyValue(sb, "RevisionNumber", ValueConverter.FormatInteger(deviceInfo.RevisionNumber));
        WriteKeyValue(sb, "OrderCode", deviceInfo.OrderCode);

        WriteKeyValue(sb, "BaudRate_10", ValueConverter.FormatBoolean(deviceInfo.SupportedBaudRates.BaudRate10));
        WriteKeyValue(sb, "BaudRate_20", ValueConverter.FormatBoolean(deviceInfo.SupportedBaudRates.BaudRate20));
        WriteKeyValue(sb, "BaudRate_50", ValueConverter.FormatBoolean(deviceInfo.SupportedBaudRates.BaudRate50));
        WriteKeyValue(sb, "BaudRate_125", ValueConverter.FormatBoolean(deviceInfo.SupportedBaudRates.BaudRate125));
        WriteKeyValue(sb, "BaudRate_250", ValueConverter.FormatBoolean(deviceInfo.SupportedBaudRates.BaudRate250));
        WriteKeyValue(sb, "BaudRate_500", ValueConverter.FormatBoolean(deviceInfo.SupportedBaudRates.BaudRate500));
        WriteKeyValue(sb, "BaudRate_800", ValueConverter.FormatBoolean(deviceInfo.SupportedBaudRates.BaudRate800));
        WriteKeyValue(sb, "BaudRate_1000", ValueConverter.FormatBoolean(deviceInfo.SupportedBaudRates.BaudRate1000));

        WriteKeyValue(sb, "SimpleBootUpMaster", ValueConverter.FormatBoolean(deviceInfo.SimpleBootUpMaster));
        WriteKeyValue(sb, "SimpleBootUpSlave", ValueConverter.FormatBoolean(deviceInfo.SimpleBootUpSlave));
        WriteKeyValue(sb, "Granularity", deviceInfo.Granularity.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "DynamicChannelsSupported", deviceInfo.DynamicChannelsSupported.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "GroupMessaging", ValueConverter.FormatBoolean(deviceInfo.GroupMessaging));
        WriteKeyValue(sb, "NrOfRXPDO", deviceInfo.NrOfRxPdo.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "NrOfTXPDO", deviceInfo.NrOfTxPdo.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "LSS_Supported", ValueConverter.FormatBoolean(deviceInfo.LssSupported));

        if (deviceInfo.CompactPdo > 0)
        {
            WriteKeyValue(sb, "CompactPDO", ValueConverter.FormatInteger(deviceInfo.CompactPdo));
        }

        if (deviceInfo.CANopenSafetySupported)
        {
            WriteKeyValue(sb, "CANopenSafetySupported", ValueConverter.FormatBoolean(deviceInfo.CANopenSafetySupported));
        }

        sb.AppendLine();
    }

    /// <summary>Writes the [DummyUsage] section.</summary>
    protected static void WriteDummyUsage(StringBuilder sb, ObjectDictionary objDict)
    {
        sb.AppendLine("[DummyUsage]");

        foreach (var dummy in objDict.DummyUsage.OrderBy(d => d.Key))
        {
            WriteKeyValue(sb, string.Format(CultureInfo.InvariantCulture, "Dummy{0:X4}", dummy.Key), ValueConverter.FormatBoolean(dummy.Value));
        }

        sb.AppendLine();
    }

    /// <summary>Writes MandatoryObjects, OptionalObjects, and ManufacturerObjects list sections.</summary>
    protected static void WriteObjectLists(StringBuilder sb, ObjectDictionary objDict)
    {
        ObjectListSectionWriter.WriteObjectLists(sb, objDict, WriteKeyValue);
    }

    /// <summary>
    /// Writes the shared (EDS) fields of a <see cref="CanOpenObject"/>.
    /// DCF overrides <see cref="WriteObjectExtension"/> to append DCF-specific fields.
    /// When <see cref="CanOpenObject.CompactSubObj"/> is non-zero, redundant
    /// <c>[xxxsubN]</c> sections are omitted and compact <c>[xxxxName]</c> /
    /// <c>[xxxxValue]</c> / <c>[xxxxDenotation]</c> lists are emitted (CiA 306).
    /// </summary>
    protected void WriteObject(StringBuilder sb, CanOpenObject obj, Action<string, Action> writeSection)
    {
        var compactMax = GetCompactMaxSubIndex(obj);
        var useCompact = compactMax > 0;

        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[{0:X}]", obj.Index));

        // CiA 306: SubNumber is not supported when CompactSubObj is used.
        if (!useCompact && obj.SubNumber.HasValue && obj.SubNumber.Value > 0)
        {
            WriteKeyValue(sb, "SubNumber", obj.SubNumber.Value.ToString(CultureInfo.InvariantCulture));
        }

        WriteKeyValue(sb, "ParameterName", obj.ParameterName);
        WriteKeyValue(sb, "ObjectType", ValueConverter.FormatInteger(obj.ObjectType));

        if (obj.DataType.HasValue)
        {
            WriteKeyValue(sb, "DataType", ValueConverter.FormatInteger(obj.DataType.Value));
        }

        WriteKeyValue(sb, "AccessType", ValueConverter.AccessTypeToString(obj.AccessType));

        if (!string.IsNullOrEmpty(obj.DefaultValue))
        {
            WriteKeyValue(sb, "DefaultValue", obj.DefaultValue);
        }

        if (!string.IsNullOrEmpty(obj.LowLimit))
        {
            WriteKeyValue(sb, "LowLimit", obj.LowLimit);
        }

        if (!string.IsNullOrEmpty(obj.HighLimit))
        {
            WriteKeyValue(sb, "HighLimit", obj.HighLimit);
        }

        WriteKeyValue(sb, "PDOMapping", ValueConverter.FormatBoolean(obj.PdoMapping));

        if (obj.SrdoMapping)
        {
            WriteKeyValue(sb, "SRDOMapping", ValueConverter.FormatBoolean(obj.SrdoMapping));
        }

        if (!string.IsNullOrEmpty(obj.InvertedSrad))
        {
            WriteKeyValue(sb, "InvertedSRAD", obj.InvertedSrad);
        }

        if (obj.ObjFlags > 0)
        {
            WriteKeyValue(sb, "ObjFlags", ValueConverter.FormatInteger(obj.ObjFlags));
        }

        if (useCompact)
        {
            WriteKeyValue(sb, "CompactSubObj", obj.CompactSubObj!.Value.ToString(CultureInfo.InvariantCulture));
        }

        WriteObjectExtension(sb, obj);

        sb.AppendLine();

        var expandedSubIndexes = new HashSet<byte>();
        if (obj.SubObjects.Count > 0)
        {
            foreach (var subObjEntry in obj.SubObjects.OrderBy(s => s.Key))
            {
                var subObj = subObjEntry.Value;
                if (useCompact && !MustExpandCompactSubObject(obj, subObj, compactMax))
                    continue;

                expandedSubIndexes.Add(subObjEntry.Key);
                var sectionName = string.Format(CultureInfo.InvariantCulture, "{0:X}sub{1:X}", obj.Index, subObjEntry.Key);
                writeSection(sectionName, () => WriteSubObject(sb, obj.Index, subObj));
            }
        }

        if (useCompact)
        {
            WriteCompactNameSection(sb, obj, compactMax, expandedSubIndexes, writeSection);
            WriteCompactValueAndDenotationSections(sb, obj, compactMax, expandedSubIndexes, writeSection);
        }

        if (obj.ObjectLinks.Count > 0)
        {
            var linkSectionName = string.Format(CultureInfo.InvariantCulture, "{0:X}ObjectLinks", obj.Index);
            writeSection(
                linkSectionName,
                () =>
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[{0:X}ObjectLinks]", obj.Index));
                    WriteKeyValue(sb, "ObjectLinks", obj.ObjectLinks.Count.ToString(CultureInfo.InvariantCulture));

                    for (int i = 0; i < obj.ObjectLinks.Count; i++)
                    {
                        WriteKeyValue(sb, (i + 1).ToString(CultureInfo.InvariantCulture), ValueConverter.FormatInteger(obj.ObjectLinks[i]));
                    }

                    sb.AppendLine();
                });
        }
    }

    /// <summary>
    /// Highest compact-listable sub-index for <paramref name="obj"/>, or 0 when
    /// CompactSubObj is absent/zero. Caps at 254 per CiA 306.
    /// </summary>
    private static int GetCompactMaxSubIndex(CanOpenObject obj)
    {
        if (!obj.CompactSubObj.HasValue || obj.CompactSubObj.Value == 0)
            return 0;
        return Math.Min((int)obj.CompactSubObj.Value, 254);
    }

    /// <summary>
    /// Returns whether a sub-object must be written as an expanded <c>[xxxsubN]</c>
    /// section under CompactSubObj storage (non-template fields that compact lists
    /// cannot represent).
    /// </summary>
    private static bool MustExpandCompactSubObject(CanOpenObject parent, CanOpenSubObject subObj, int compactMax)
    {
        if (subObj.SubIndex > compactMax)
            return true;

        if (subObj.SubIndex == 0)
            return !MatchesCompactSub0Template(parent, subObj);

        // 1..compactMax: expand only when fields cannot go in Name/Value/Denotation.
        return !MatchesCompactElementTemplate(parent, subObj)
               || HasNonCompactExclusiveFields(subObj);
    }

    private static bool MatchesCompactSub0Template(CanOpenObject parent, CanOpenSubObject subObj)
    {
        return subObj.ObjectType == CanOpenObjectType.Var
               && subObj.DataType == 0x0005
               && subObj.AccessType == AccessType.ReadOnly
               && string.Equals(
                   subObj.DefaultValue,
                   parent.CompactSubObj!.Value.ToString(CultureInfo.InvariantCulture),
                   StringComparison.Ordinal)
               && !subObj.PdoMapping
               && string.IsNullOrEmpty(subObj.LowLimit)
               && string.IsNullOrEmpty(subObj.HighLimit)
               && !HasNonCompactExclusiveFields(subObj)
               && (string.IsNullOrEmpty(subObj.ParameterName)
                   || subObj.ParameterName.Equals("NrOfObjects", StringComparison.Ordinal));
    }

    private static bool MatchesCompactElementTemplate(CanOpenObject parent, CanOpenSubObject subObj)
    {
        var expectedDataType = parent.DataType ?? 0;
        return subObj.ObjectType == CanOpenObjectType.Var
               && subObj.DataType == expectedDataType
               && subObj.AccessType == parent.AccessType
               && string.Equals(subObj.DefaultValue, parent.DefaultValue, StringComparison.Ordinal)
               && subObj.PdoMapping == parent.PdoMapping
               && string.IsNullOrEmpty(subObj.LowLimit)
               && string.IsNullOrEmpty(subObj.HighLimit);
    }

    private static bool HasNonCompactExclusiveFields(CanOpenSubObject subObj)
        => subObj.SrdoMapping
           || !string.IsNullOrEmpty(subObj.InvertedSrad)
           || !string.IsNullOrEmpty(subObj.ParamRefd);

    private static void WriteCompactNameSection(
        StringBuilder sb,
        CanOpenObject obj,
        int compactMax,
        HashSet<byte> expandedSubIndexes,
        Action<string, Action> writeSection)
    {
        var names = new SortedDictionary<byte, string>();
        for (var i = 1; i <= compactMax; i++)
        {
            var subIndex = (byte)i;
            if (expandedSubIndexes.Contains(subIndex))
                continue;
            if (!obj.SubObjects.TryGetValue(subIndex, out var subObj))
                continue;

            var defaultName = string.Concat(
                obj.ParameterName,
                subIndex.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(subObj.ParameterName)
                && !subObj.ParameterName.Equals(defaultName, StringComparison.Ordinal))
            {
                names[subIndex] = subObj.ParameterName;
            }
        }

        if (names.Count == 0)
            return;

        var sectionName = string.Format(CultureInfo.InvariantCulture, "{0:X}Name", obj.Index);
        writeSection(
            sectionName,
            () =>
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[{0:X}Name]", obj.Index));
                WriteKeyValue(sb, "NrOfEntries", names.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var entry in names)
                {
                    WriteKeyValue(sb, entry.Key.ToString(CultureInfo.InvariantCulture), entry.Value);
                }

                sb.AppendLine();
            });
    }

    /// <summary>
    /// Writes compact <c>[xxxxValue]</c> / <c>[xxxxDenotation]</c> lists.
    /// EDS: no-op. DCF: emits ParameterValue and Denotation for non-expanded subs.
    /// </summary>
    protected virtual void WriteCompactValueAndDenotationSections(
        StringBuilder sb,
        CanOpenObject obj,
        int compactMax,
        HashSet<byte> expandedSubIndexes,
        Action<string, Action> writeSection)
    {
    }

    /// <summary>
    /// Extension point for format-specific object fields.
    /// EDS: no-op. DCF: writes ParameterValue, Denotation, ParamRefd, UploadFile, DownloadFile.
    /// </summary>
    protected virtual void WriteObjectExtension(StringBuilder sb, CanOpenObject obj)
    {
    }

    /// <summary>
    /// Writes the shared (EDS) fields of a <see cref="CanOpenSubObject"/>.
    /// DCF overrides <see cref="WriteSubObjectExtension"/> to append DCF-specific fields.
    /// </summary>
    protected void WriteSubObject(StringBuilder sb, ushort index, CanOpenSubObject subObj)
    {
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[{0:X}sub{1:X}]", index, subObj.SubIndex));

        WriteKeyValue(sb, "ParameterName", subObj.ParameterName);
        WriteKeyValue(sb, "ObjectType", ValueConverter.FormatInteger(subObj.ObjectType));
        WriteKeyValue(sb, "DataType", ValueConverter.FormatInteger(subObj.DataType));
        WriteKeyValue(sb, "AccessType", ValueConverter.AccessTypeToString(subObj.AccessType));

        if (!string.IsNullOrEmpty(subObj.DefaultValue))
        {
            WriteKeyValue(sb, "DefaultValue", subObj.DefaultValue);
        }

        if (!string.IsNullOrEmpty(subObj.LowLimit))
        {
            WriteKeyValue(sb, "LowLimit", subObj.LowLimit);
        }

        if (!string.IsNullOrEmpty(subObj.HighLimit))
        {
            WriteKeyValue(sb, "HighLimit", subObj.HighLimit);
        }

        WriteKeyValue(sb, "PDOMapping", ValueConverter.FormatBoolean(subObj.PdoMapping));

        if (subObj.SrdoMapping)
        {
            WriteKeyValue(sb, "SRDOMapping", ValueConverter.FormatBoolean(subObj.SrdoMapping));
        }

        if (!string.IsNullOrEmpty(subObj.InvertedSrad))
        {
            WriteKeyValue(sb, "InvertedSRAD", subObj.InvertedSrad);
        }

        WriteSubObjectExtension(sb, subObj);

        sb.AppendLine();
    }

    /// <summary>
    /// Extension point for format-specific sub-object fields.
    /// EDS: no-op. DCF: writes ParameterValue, Denotation, ParamRefd.
    /// </summary>
    protected virtual void WriteSubObjectExtension(StringBuilder sb, CanOpenSubObject subObj)
    {
    }

    /// <summary>Writes the [SupportedModules] list and each [M{n}ModuleInfo] section.</summary>
    protected static void WriteSupportedModules(StringBuilder sb, List<ModuleInfo> modules)
    {
        sb.AppendLine("[SupportedModules]");
        WriteKeyValue(sb, "NrOfEntries", modules.Count.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine();

        foreach (var module in modules)
        {
            WriteModuleInfo(sb, module);
        }
    }

    /// <summary>Writes a single [M{n}ModuleInfo] section and its [M{n}FixedObjects].</summary>
    protected static void WriteModuleInfo(StringBuilder sb, ModuleInfo module)
    {
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[M{0}ModuleInfo]", module.ModuleNumber));
        WriteKeyValue(sb, "ProductName", module.ProductName);
        WriteKeyValue(sb, "ProductVersion", module.ProductVersion.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "ProductRevision", module.ProductRevision.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "OrderCode", module.OrderCode);
        sb.AppendLine();

        if (module.FixedObjects.Count > 0)
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[M{0}FixedObjects]", module.ModuleNumber));
            WriteKeyValue(sb, "NrOfEntries", module.FixedObjects.Count.ToString(CultureInfo.InvariantCulture));

            for (int i = 0; i < module.FixedObjects.Count; i++)
            {
                WriteKeyValue(sb, (i + 1).ToString(CultureInfo.InvariantCulture), ValueConverter.FormatInteger(module.FixedObjects[i]));
            }

            sb.AppendLine();
        }
    }

    /// <summary>Writes the [Comments] section.</summary>
    protected static void WriteComments(StringBuilder sb, Comments comments)
    {
        sb.AppendLine("[Comments]");
        WriteKeyValue(sb, "Lines", comments.Lines.ToString(CultureInfo.InvariantCulture));

        foreach (var line in comments.CommentLines.OrderBy(l => l.Key))
        {
            WriteKeyValue(sb, string.Format(CultureInfo.InvariantCulture, "Line{0}", line.Key), line.Value);
        }

        sb.AppendLine();
    }

    /// <summary>Writes the [DynamicChannels] section.</summary>
    protected static void WriteDynamicChannels(StringBuilder sb, DynamicChannels dynamicChannels)
    {
        sb.AppendLine("[DynamicChannels]");
        WriteKeyValue(sb, "NrOfSeg", dynamicChannels.Segments.Count.ToString(CultureInfo.InvariantCulture));

        for (int i = 0; i < dynamicChannels.Segments.Count; i++)
        {
            var idx = (i + 1).ToString(CultureInfo.InvariantCulture);
            var seg = dynamicChannels.Segments[i];
            WriteKeyValue(sb, $"Type{idx}", ValueConverter.FormatInteger(seg.Type));
            WriteKeyValue(sb, $"Dir{idx}", ValueConverter.AccessTypeToString(seg.Dir));
            WriteKeyValue(sb, $"Range{idx}", seg.Range);
            WriteKeyValue(sb, $"PPOffset{idx}", seg.PPOffset.ToString(CultureInfo.InvariantCulture));
        }

        sb.AppendLine();
    }

    /// <summary>Writes the [Tools] list and each [Tool{n}] section.</summary>
    protected static void WriteTools(StringBuilder sb, List<ToolInfo> tools)
    {
        sb.AppendLine("[Tools]");
        WriteKeyValue(sb, "Items", tools.Count.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine();

        for (int i = 0; i < tools.Count; i++)
        {
            var idx = (i + 1).ToString(CultureInfo.InvariantCulture);
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[Tool{0}]", idx));
            WriteKeyValue(sb, "Name", tools[i].Name);
            WriteKeyValue(sb, "Command", tools[i].Command);
            sb.AppendLine();
        }
    }

    /// <summary>Writes a non-standard additional section.</summary>
    protected static void WriteAdditionalSection(StringBuilder sb, string sectionName, Dictionary<string, string> entries)
    {
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[{0}]", sectionName));

        foreach (var entry in entries.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
        {
            WriteKeyValue(sb, entry.Key, entry.Value);
        }

        sb.AppendLine();
    }
}
