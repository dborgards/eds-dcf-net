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
        IniRoundTripText.WriteKeyValue(sb, key, value);
    }

    /// <summary>Writes the [FileInfo] section (shared EDS/DCF fields, without LastEDS).</summary>
    protected static void WriteFileInfo(StringBuilder sb, EdsFileInfo fileInfo)
    {
        IniRoundTripText.WriteSectionHeader(sb, "FileInfo");
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
        IniRoundTripText.WriteSectionHeader(sb, "DeviceInfo");
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

        WriteRemainingEntries(sb, deviceInfo.RemainingEntries, SectionEntryKeys.IsDeviceInfoKey);

        sb.AppendLine();
    }

    /// <summary>Writes the [DummyUsage] section.</summary>
    protected static void WriteDummyUsage(StringBuilder sb, ObjectDictionary objDict)
        => WriteDummyUsage(sb, objDict, sectionEntries: null);

    /// <summary>
    /// Writes <c>[DummyUsage]</c>, then the kept entries of that section from
    /// <paramref name="sectionEntries"/> (see <c>SectionRemainingEntries</c>).
    /// </summary>
    private protected static void WriteDummyUsage(
        StringBuilder sb,
        ObjectDictionary objDict,
        Dictionary<string, OrderedStringDictionary>? sectionEntries)
    {
        IniRoundTripText.WriteSectionHeader(sb, "DummyUsage");

        foreach (var dummy in objDict.DummyUsage.OrderBy(d => d.Key))
        {
            WriteKeyValue(sb, string.Format(CultureInfo.InvariantCulture, "Dummy{0:X4}", dummy.Key), ValueConverter.FormatBoolean(dummy.Value));
        }

        WriteRemainingEntries(sb, GetSectionEntries(sectionEntries, "DummyUsage"), SectionEntryKeys.IsDummyUsageKey);

        sb.AppendLine();
    }

    /// <summary>Writes MandatoryObjects, OptionalObjects, and ManufacturerObjects list sections.</summary>
    protected static void WriteObjectLists(StringBuilder sb, ObjectDictionary objDict)
    {
        ObjectListSectionWriter.WriteObjectLists(sb, objDict, WriteKeyValue, sectionEntries: null);
    }

    /// <summary>
    /// Writes the three object list sections with their kept entries. A list without objects is
    /// written when <paramref name="sectionEntries"/> keeps entries for it.
    /// </summary>
    private protected static void WriteObjectLists(
        StringBuilder sb,
        ObjectDictionary objDict,
        Dictionary<string, OrderedStringDictionary>? sectionEntries)
    {
        ObjectListSectionWriter.WriteObjectLists(sb, objDict, WriteKeyValue, sectionEntries);
    }

    /// <summary>
    /// Returns the kept entries of <paramref name="sectionName"/>, or <see langword="null"/>
    /// when there are none.
    /// </summary>
    private protected static OrderedStringDictionary? GetSectionEntries(
        Dictionary<string, OrderedStringDictionary>? sectionEntries,
        string sectionName)
        => sectionEntries != null
           && sectionEntries.TryGetValue(sectionName, out var entries)
           && entries != null
           && entries.Count > 0
            ? entries
            : null;

    /// <summary>
    /// <see langword="true"/> when <paramref name="sectionEntries"/> keeps at least one entry
    /// for <paramref name="sectionName"/>.
    /// </summary>
    private protected static bool HasSectionEntries(
        Dictionary<string, OrderedStringDictionary>? sectionEntries,
        string sectionName)
        => GetSectionEntries(sectionEntries, sectionName) != null;

    /// <summary>
    /// Writes the shared (EDS) fields of a <see cref="CanOpenObject"/>.
    /// DCF overrides <see cref="WriteObjectExtension"/> to append DCF-specific fields.
    /// When <see cref="CanOpenObject.CompactSubObj"/> is non-zero, redundant
    /// <c>[xxxsubN]</c> sections are omitted and compact <c>[xxxxName]</c> /
    /// <c>[xxxxValue]</c> / <c>[xxxxDenotation]</c> lists are emitted (CiA 306).
    /// </summary>
    protected void WriteObject(StringBuilder sb, CanOpenObject obj, Action<string, Action> writeSection)
        => WriteObject(sb, obj, writeSection, sectionEntries: null);

    /// <summary>
    /// Writes an object like <see cref="WriteObject(StringBuilder, CanOpenObject, Action{string, Action})"/>
    /// and appends the kept entries of its companion sections (<c>[xxxxName]</c>,
    /// <c>[xxxxObjectLinks]</c>, DCF <c>[xxxxValue]</c> / <c>[xxxxDenotation]</c>) from
    /// <paramref name="sectionEntries"/>.
    /// </summary>
    private protected void WriteObject(
        StringBuilder sb,
        CanOpenObject obj,
        Action<string, Action> writeSection,
        Dictionary<string, OrderedStringDictionary>? sectionEntries)
    {
        var compactMax = GetCompactMaxSubIndex(obj);
        var useCompact = compactMax > 0;

        IniRoundTripText.WriteSectionHeader(
            sb,
            string.Format(CultureInfo.InvariantCulture, "{0:X}", obj.Index));

        // CiA 306-1 Table 7: keys marked "n" for the object type are not written.
        bool IsWritten(string key) => IsObjectKeyWritten(obj, key);

        // CiA 306: SubNumber is normally omitted under CompactSubObj. Keep/emit it when
        // expanded sub-objects exist above the compact range so the reader can reach them
        // (S18, Table 7 "nc"). Also emit when expanded SubObjects exist even if the highest
        // sub-index is 0 (SubNumber=0), so the key is not silently dropped for that boundary case.
        // Table 7 marks SubNumber "n" for VAR/DEFTYPE/DOMAIN, but it is still written while such
        // an object has sub-objects, so an unvalidated write loses nothing on re-read (decision
        // E10: reject only on validated writes, see IniWriteRules).
        var subNumberToWrite = ResolveSubNumberForWrite(obj, compactMax, useCompact);
        var keepsSubNumberForSubObjects = !IsWritten("SubNumber") && obj.SubObjects.Count > 0;
        if (keepsSubNumberForSubObjects)
        {
            // The reader loads sub-objects of these types only for SubNumber > 0, so the E10 path
            // writes the sub-object count (at least 1), not the highest sub-index: a lone
            // sub-index 0 must not become SubNumber=0. The general S11 correction is WP-11.
            subNumberToWrite = Math.Max((byte)1, DescribedSubIndexCount(obj));
        }

        if ((subNumberToWrite > 0 || (!useCompact && obj.SubObjects.Count > 0)) &&
            (IsWritten("SubNumber") || keepsSubNumberForSubObjects))
        {
            WriteKeyValue(sb, "SubNumber", subNumberToWrite.ToString(CultureInfo.InvariantCulture));
        }

        WriteKeyValue(sb, "ParameterName", obj.ParameterName);
        WriteKeyValue(sb, "ObjectType", ValueConverter.FormatInteger(obj.ObjectType));

        if (obj.DataType.HasValue && IsWritten("DataType"))
        {
            WriteKeyValue(sb, "DataType", ValueConverter.FormatInteger(obj.DataType.Value));
        }

        if (IsWritten("AccessType"))
        {
            WriteKeyValue(sb, "AccessType", ValueConverter.AccessTypeToString(obj.AccessType));
        }

        if (!string.IsNullOrEmpty(obj.DefaultValue) && IsWritten("DefaultValue"))
        {
            WriteKeyValue(sb, "DefaultValue", obj.DefaultValue);
        }

        if (!string.IsNullOrEmpty(obj.LowLimit) && IsWritten("LowLimit"))
        {
            WriteKeyValue(sb, "LowLimit", obj.LowLimit);
        }

        if (!string.IsNullOrEmpty(obj.HighLimit) && IsWritten("HighLimit"))
        {
            WriteKeyValue(sb, "HighLimit", obj.HighLimit);
        }

        if (IsWritten("PDOMapping"))
        {
            WriteKeyValue(sb, "PDOMapping", ValueConverter.FormatBoolean(obj.PdoMapping));
        }

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
        WriteRemainingEntries(sb, obj.RemainingEntries, IsDedicatedObjectEntryKey);

        sb.AppendLine();

        var expandedSubIndexes = GetExpandedSubIndexes(obj, compactMax);
        foreach (var subObjEntry in obj.SubObjects.OrderBy(s => s.Key))
        {
            if (!expandedSubIndexes.Contains(subObjEntry.Key))
                continue;

            var subObj = subObjEntry.Value;
            var sectionName = string.Format(CultureInfo.InvariantCulture, "{0:X}sub{1:X}", obj.Index, subObjEntry.Key);
            writeSection(sectionName, () => WriteSubObject(sb, obj.Index, subObj));
        }

        if (useCompact)
        {
            WriteCompactNameSection(sb, obj, compactMax, expandedSubIndexes, writeSection, sectionEntries);
        }

        if (useCompact)
        {
            // Keep dispatching through the protected virtual hook so subclass overrides still
            // run. The kept section entries reach the built-in DCF override through an
            // AsyncLocal scope, because the hook has no parameter for them and EdsWriter /
            // DcfWriter share one writer instance across concurrent calls.
            var previous = CurrentSectionEntries.Value;
            CurrentSectionEntries.Value = sectionEntries;
            try
            {
                WriteCompactValueAndDenotationSections(sb, obj, compactMax, expandedSubIndexes, writeSection);
            }
            finally
            {
                CurrentSectionEntries.Value = previous;
            }
        }
        else
        {
            WriteCompactValueAndDenotationSections(sb, obj, 0, expandedSubIndexes, writeSection, sectionEntries);
        }

        var linkSectionName = string.Format(CultureInfo.InvariantCulture, "{0:X}ObjectLinks", obj.Index);
        var keptLinkEntries = GetSectionEntries(sectionEntries, linkSectionName);
        if (obj.ObjectLinks.Count > 0 || keptLinkEntries != null)
        {
            writeSection(
                linkSectionName,
                () =>
                {
                    IniRoundTripText.WriteSectionHeader(
                        sb,
                        string.Format(CultureInfo.InvariantCulture, "{0:X}ObjectLinks", obj.Index));
                    WriteKeyValue(sb, "ObjectLinks", obj.ObjectLinks.Count.ToString(CultureInfo.InvariantCulture));

                    for (int i = 0; i < obj.ObjectLinks.Count; i++)
                    {
                        WriteKeyValue(sb, (i + 1).ToString(CultureInfo.InvariantCulture), ValueConverter.FormatInteger(obj.ObjectLinks[i]));
                    }

                    WriteCountedListRemainingEntries(
                        sb,
                        keptLinkEntries,
                        SectionEntryKeys.ObjectLinksCountKey,
                        obj.ObjectLinks.Count);

                    sb.AppendLine();
                });
        }
    }

    /// <summary>
    /// Kept section entries of the object currently written through
    /// <see cref="WriteCompactValueAndDenotationSections(StringBuilder, CanOpenObject, int, HashSet{byte}, Action{string, Action})"/>,
    /// scoped to the current call.
    /// </summary>
    private static readonly AsyncLocal<Dictionary<string, OrderedStringDictionary>?> CurrentSectionEntries = new();

    /// <summary>
    /// The kept section entries (<c>SectionRemainingEntries</c>) of the model being written,
    /// available inside the compact-list hook; <see langword="null"/> outside a model write.
    /// </summary>
    private protected static Dictionary<string, OrderedStringDictionary>? CurrentObjectSectionEntries
        => CurrentSectionEntries.Value;

    /// <summary>
    /// Writes compact value lists from kept entries. For a compact object the writer calls
    /// <see cref="WriteCompactValueAndDenotationSections(StringBuilder, CanOpenObject, int, HashSet{byte}, Action{string, Action})"/>
    /// instead; this overload is used for an object without CompactSubObj storage
    /// (<paramref name="compactMax"/> <c>0</c>). EDS: no-op.
    /// </summary>
    private protected virtual void WriteCompactValueAndDenotationSections(
        StringBuilder sb,
        CanOpenObject obj,
        int compactMax,
        HashSet<byte> expandedSubIndexes,
        Action<string, Action> writeSection,
        Dictionary<string, OrderedStringDictionary>? sectionEntries)
    {
    }

    /// <summary>
    /// <see langword="false"/> when CiA 306-1 Table 7 marks <paramref name="key"/> as not supported
    /// for <paramref name="obj"/>, so the object writer omits it. Shared with the validated-write
    /// rules, which check only keys that are written.
    /// </summary>
    internal static bool IsObjectKeyWritten(CanOpenObject obj, string key)
        => !ObjectTypeKeyMatrix.IsNotSupported(obj.ObjectType, GetCompactMaxSubIndex(obj) > 0, key);

    /// <summary>
    /// Sub-object counterpart of <see cref="IsObjectKeyWritten"/>; a sub-object has no
    /// <c>CompactSubObj</c> of its own.
    /// </summary>
    internal static bool IsSubObjectKeyWritten(CanOpenSubObject subObj, string key)
        => !ObjectTypeKeyMatrix.IsNotSupported(subObj.ObjectType, hasCompactSubObj: false, key);

    /// <summary>
    /// Highest compact-listable sub-index for <paramref name="obj"/>, or 0 when
    /// CompactSubObj is absent/zero. Caps at 254 per CiA 306. Also 0 for VAR, DEFTYPE and
    /// DOMAIN, for which CiA 306-1 Table 7 does not support <c>CompactSubObj</c>: their
    /// sub-objects are written as expanded sections and the key is omitted.
    /// </summary>
    internal static int GetCompactMaxSubIndex(CanOpenObject obj)
    {
        if (!obj.CompactSubObj.HasValue || obj.CompactSubObj.Value == 0)
            return 0;
        if (ObjectTypeKeyMatrix.IsNotSupported(obj.ObjectType, hasCompactSubObj: true, "CompactSubObj"))
            return 0;
        return Math.Min((int)obj.CompactSubObj.Value, 254);
    }

    /// <summary>
    /// Chooses the SubNumber to emit. Under compact storage this is usually omitted,
    /// except when expanded sub-objects above the compact range must remain reachable.
    /// When not using compact storage and <see cref="CanOpenObject.SubNumber"/> is
    /// unset or zero, falls back to the highest present sub-index so CiA 306
    /// <c>SubNumber</c> is still emitted for ARRAY/RECORD objects.
    /// </summary>
    private static byte ResolveSubNumberForWrite(CanOpenObject obj, int compactMax, bool useCompact)
    {
        if (!useCompact)
        {
            var fromModel = obj.SubNumber.GetValueOrDefault();
            if (fromModel > 0)
                return fromModel;

            byte maxSubIndex = 0;
            foreach (var key in obj.SubObjects.Keys)
            {
                if (key > maxSubIndex)
                    maxSubIndex = key;
            }

            return maxSubIndex;
        }

        byte maxExpandedBeyondCompact = 0;
        var hasBeyond = false;
        foreach (var key in obj.SubObjects.Keys)
        {
            if (key <= compactMax)
                continue;
            hasBeyond = true;
            if (key > maxExpandedBeyondCompact)
                maxExpandedBeyondCompact = key;
        }

        if (!hasBeyond)
            return 0;

        var compactFromModel = obj.SubNumber.GetValueOrDefault();
        return compactFromModel > maxExpandedBeyondCompact ? compactFromModel : maxExpandedBeyondCompact;
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
        // Sub0 has no compact Value/Denotation list entry (CiA 306 keys are 1..254),
        // so non-empty ParameterValue/Denotation must force an expanded [xxxsub0].
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
               && string.IsNullOrEmpty(subObj.ParameterValue)
               && string.IsNullOrEmpty(subObj.Denotation)
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
           || !string.IsNullOrEmpty(subObj.ParamRefd)
           || subObj.RemainingEntries.Count > 0;

    private static void WriteCompactNameSection(
        StringBuilder sb,
        CanOpenObject obj,
        int compactMax,
        HashSet<byte> expandedSubIndexes,
        Action<string, Action> writeSection,
        Dictionary<string, OrderedStringDictionary>? sectionEntries)
    {
        var names = GetCompactNameEntries(obj, compactMax, expandedSubIndexes);

        var sectionName = string.Format(CultureInfo.InvariantCulture, "{0:X}Name", obj.Index);
        var keptEntries = GetSectionEntries(sectionEntries, sectionName);
        if (names.Count == 0 && keptEntries == null)
            return;

        writeSection(
            sectionName,
            () =>
            {
                IniRoundTripText.WriteSectionHeader(
                    sb,
                    string.Format(CultureInfo.InvariantCulture, "{0:X}Name", obj.Index));
                WriteKeyValue(sb, "NrOfEntries", names.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var entry in names)
                {
                    WriteKeyValue(sb, entry.Key.ToString(CultureInfo.InvariantCulture), entry.Value);
                }

                WriteCompactListRemainingEntries(sb, keptEntries, names.Keys);

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
        IniRoundTripText.WriteSectionHeader(
            sb,
            string.Format(CultureInfo.InvariantCulture, "{0:X}sub{1:X}", index, subObj.SubIndex));

        // CiA 306-1 Table 7 applies to sub-index sections as well; a sub-object has no
        // CompactSubObj of its own.
        bool IsWritten(string key) => IsSubObjectKeyWritten(subObj, key);

        WriteKeyValue(sb, "ParameterName", subObj.ParameterName);
        WriteKeyValue(sb, "ObjectType", ValueConverter.FormatInteger(subObj.ObjectType));

        if (IsWritten("DataType"))
        {
            WriteKeyValue(sb, "DataType", ValueConverter.FormatInteger(subObj.DataType));
        }

        if (IsWritten("AccessType"))
        {
            WriteKeyValue(sb, "AccessType", ValueConverter.AccessTypeToString(subObj.AccessType));
        }

        if (!string.IsNullOrEmpty(subObj.DefaultValue) && IsWritten("DefaultValue"))
        {
            WriteKeyValue(sb, "DefaultValue", subObj.DefaultValue);
        }

        if (!string.IsNullOrEmpty(subObj.LowLimit) && IsWritten("LowLimit"))
        {
            WriteKeyValue(sb, "LowLimit", subObj.LowLimit);
        }

        if (!string.IsNullOrEmpty(subObj.HighLimit) && IsWritten("HighLimit"))
        {
            WriteKeyValue(sb, "HighLimit", subObj.HighLimit);
        }

        if (IsWritten("PDOMapping"))
        {
            WriteKeyValue(sb, "PDOMapping", ValueConverter.FormatBoolean(subObj.PdoMapping));
        }

        if (subObj.SrdoMapping)
        {
            WriteKeyValue(sb, "SRDOMapping", ValueConverter.FormatBoolean(subObj.SrdoMapping));
        }

        if (!string.IsNullOrEmpty(subObj.InvertedSrad))
        {
            WriteKeyValue(sb, "InvertedSRAD", subObj.InvertedSrad);
        }

        WriteSubObjectExtension(sb, subObj);

        // A kept key that Table 7 marks "n" for the sub-object type (for example SubNumber on a
        // VAR) is dropped like the dedicated "n" keys above. Object sections need no such filter:
        // every Table 7 key is a dedicated object key.
        WriteRemainingEntries(
            sb,
            subObj.RemainingEntries,
            key => IsDedicatedSubObjectEntryKey(key) || !IsSubObjectKeyWritten(subObj, key));

        sb.AppendLine();
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="key"/> is written from a
    /// <see cref="CanOpenObject"/> property. EDS keywords only; DCF adds configured-value keywords.
    /// </summary>
    /// <param name="key">Remaining-entry key.</param>
    protected virtual bool IsDedicatedObjectEntryKey(string key) => SectionEntryKeys.IsEdsObjectKey(key);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="key"/> is written from a
    /// <see cref="CanOpenSubObject"/> property. EDS keywords only; DCF adds configured-value keywords.
    /// </summary>
    /// <param name="key">Remaining-entry key.</param>
    protected virtual bool IsDedicatedSubObjectEntryKey(string key) => SectionEntryKeys.IsEdsSubObjectKey(key);

    /// <summary>
    /// Writes unknown section keys in insertion order, after the known keywords.
    /// Keys that this format already writes from dedicated properties are skipped so they
    /// cannot be emitted twice or replace a commissioned property value. For list sections
    /// <paramref name="isDedicatedKey"/> also covers the entries the writer generated, so a
    /// generated entry wins over a kept entry with the same key.
    /// </summary>
    private protected static void WriteRemainingEntries(
        StringBuilder sb,
        OrderedStringDictionary? entries,
        Func<string, bool> isDedicatedKey)
    {
        if (entries == null)
            return;

        foreach (var entry in entries)
        {
            if (isDedicatedKey(entry.Key))
                continue;

            WriteKeyValue(sb, entry.Key, entry.Value);
        }
    }

    /// <summary>
    /// Decides for one <c>SectionRemainingEntries</c> section whether the EDS/DCF writer emits
    /// it for <paramref name="model"/> and, if so, which kept keys it suppresses because it
    /// generates them itself. The validated-write rules use this so they check exactly the kept
    /// entries the writer outputs.
    /// </summary>
    /// <param name="model">An <see cref="ElectronicDataSheet"/> or <see cref="DeviceConfigurationFile"/>.</param>
    /// <param name="sectionName">A store key; the writer looks sections up by its canonical name.</param>
    /// <param name="isSuppressedKey">Kept keys the writer does not output.</param>
    /// <returns><see langword="false"/> when the writer does not emit the section.</returns>
    internal static bool TryGetWrittenSectionFilter(
        ICanOpenFileModel model,
        string sectionName,
        out Func<string, bool> isSuppressedKey)
    {
        isSuppressedKey = static _ => true;
        var od = model.ObjectDictionary;
        switch (sectionName.ToUpperInvariant())
        {
            case "DUMMYUSAGE":
                isSuppressedKey = SectionEntryKeys.IsDummyUsageKey;
                return true;
            case "MANDATORYOBJECTS":
                return CountedList(SectionEntryKeys.SupportedObjectsKey, od.MandatoryObjects.Count, out isSuppressedKey);
            case "OPTIONALOBJECTS":
                return CountedList(SectionEntryKeys.SupportedObjectsKey, od.OptionalObjects.Count, out isSuppressedKey);
            case "MANUFACTUREROBJECTS":
                return CountedList(SectionEntryKeys.SupportedObjectsKey, od.ManufacturerObjects.Count, out isSuppressedKey);
            case "SUPPORTEDMODULES":
                isSuppressedKey = SectionEntryKeys.IsSupportedModulesKey;
                return true;
            case "TOOLS":
                isSuppressedKey = SectionEntryKeys.IsToolsKey;
                return true;
            case "CONNECTEDMODULES":
                return model is DeviceConfigurationFile dcf
                       && CountedList(SectionEntryKeys.NrOfEntriesKey, dcf.ConnectedModules.Count, out isSuppressedKey);
        }

        foreach (var module in model.SupportedModules)
        {
            if (TryGetWrittenModuleSectionFilter(module, sectionName, out isSuppressedKey))
                return true;
        }

        foreach (var obj in od.Objects.Values)
        {
            if (TryGetWrittenObjectSectionFilter(obj, model is DeviceConfigurationFile, sectionName, out isSuppressedKey))
                return true;
        }

        return false;
    }

    private static bool TryGetWrittenModuleSectionFilter(
        ModuleInfo module,
        string sectionName,
        out Func<string, bool> isSuppressedKey)
    {
        isSuppressedKey = static _ => true;
        if (IsSectionNamed(sectionName, "M{0}ModuleInfo", module.ModuleNumber))
        {
            isSuppressedKey = SectionEntryKeys.IsModuleInfoKey;
            return true;
        }

        if (IsSectionNamed(sectionName, "M{0}FixedObjects", module.ModuleNumber))
            return CountedList(SectionEntryKeys.NrOfEntriesKey, module.FixedObjects.Count, out isSuppressedKey);

        if (IsSectionNamed(sectionName, "M{0}SubExtends", module.ModuleNumber))
            return CountedList(SectionEntryKeys.NrOfEntriesKey, module.SubExtends.Count, out isSuppressedKey);

        foreach (var index in module.SubExtensionDefinitions.Keys)
        {
            if (IsSectionNamed(sectionName, "M{0}SubExt{1:X}", module.ModuleNumber, index))
            {
                isSuppressedKey = SectionEntryKeys.IsModuleSubExtensionKey;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetWrittenObjectSectionFilter(
        CanOpenObject obj,
        bool isDcf,
        string sectionName,
        out Func<string, bool> isSuppressedKey)
    {
        isSuppressedKey = static _ => true;
        if (IsSectionNamed(sectionName, "{0:X}ObjectLinks", obj.Index))
            return CountedList(SectionEntryKeys.ObjectLinksCountKey, obj.ObjectLinks.Count, out isSuppressedKey);

        var compactMax = GetCompactMaxSubIndex(obj);
        var expanded = GetExpandedSubIndexes(obj, compactMax);
        if (compactMax > 0 && IsSectionNamed(sectionName, "{0:X}Name", obj.Index))
            return CompactList(GetCompactNameEntries(obj, compactMax, expanded).Keys, out isSuppressedKey);

        // DCF writes [xxxxValue] / [xxxxDenotation] for every object (compact lists only with compact storage).
        if (isDcf && IsSectionNamed(sectionName, "{0:X}Value", obj.Index))
            return CompactList(GetCompactListEntries(obj, compactMax, expanded, (_, sub) => DcfWriter.SelectParameterValue(sub)).Keys, out isSuppressedKey);

        if (isDcf && IsSectionNamed(sectionName, "{0:X}Denotation", obj.Index))
            return CompactList(GetCompactListEntries(obj, compactMax, expanded, (_, sub) => DcfWriter.SelectDenotation(sub)).Keys, out isSuppressedKey);

        return false;
    }

    private static bool IsSectionNamed(string sectionName, string format, params object[] args)
        => string.Equals(
            sectionName,
            string.Format(CultureInfo.InvariantCulture, format, args),
            StringComparison.OrdinalIgnoreCase);

    private static bool CountedList(string countKey, int generatedCount, out Func<string, bool> isSuppressedKey)
    {
        isSuppressedKey = key => SectionEntryKeys.IsCountedListKey(key, countKey, generatedCount);
        return true;
    }

    private static bool CompactList(ICollection<byte> generatedSubIndexes, out Func<string, bool> isSuppressedKey)
    {
        isSuppressedKey = key => SectionEntryKeys.IsAppliedCompactListKey(key, generatedSubIndexes);
        return true;
    }

    /// <summary>
    /// Sub-indexes the writer emits as expanded <c>[xxxxsubN]</c> sections: all of them without
    /// compact storage (<paramref name="compactMax"/> <c>0</c>), otherwise only those that the
    /// compact lists cannot represent.
    /// </summary>
    internal static HashSet<byte> GetExpandedSubIndexes(CanOpenObject obj, int compactMax)
    {
        var expanded = new HashSet<byte>();
        foreach (var entry in obj.SubObjects)
        {
            if (compactMax == 0 || MustExpandCompactSubObject(obj, entry.Value, compactMax))
                expanded.Add(entry.Key);
        }

        return expanded;
    }

    /// <summary>
    /// The entries of a compact list the writer generates: for every sub-index
    /// <c>1..compactMax</c> that is not expanded, the non-empty value from
    /// <paramref name="selectValue"/>.
    /// </summary>
    internal static SortedDictionary<byte, string> GetCompactListEntries(
        CanOpenObject obj,
        int compactMax,
        HashSet<byte> expandedSubIndexes,
        Func<byte, CanOpenSubObject, string?> selectValue)
    {
        var entries = new SortedDictionary<byte, string>();
        for (var i = 1; i <= compactMax; i++)
        {
            var subIndex = (byte)i;
            if (expandedSubIndexes.Contains(subIndex) || !obj.SubObjects.TryGetValue(subIndex, out var subObj))
                continue;

            var value = selectValue(subIndex, subObj);
            if (!string.IsNullOrEmpty(value))
                entries[subIndex] = value!;
        }

        return entries;
    }

    /// <summary>
    /// The <c>[xxxxName]</c> entries the writer generates: parameter names that differ from the
    /// compact default <c>ParameterName + sub-index</c> (CiA 306 § 6.6.3.4).
    /// </summary>
    internal static SortedDictionary<byte, string> GetCompactNameEntries(
        CanOpenObject obj,
        int compactMax,
        HashSet<byte> expandedSubIndexes)
        => GetCompactListEntries(
            obj,
            compactMax,
            expandedSubIndexes,
            (subIndex, subObj) => string.Equals(
                subObj.ParameterName,
                string.Concat(obj.ParameterName, subIndex.ToString(CultureInfo.InvariantCulture)),
                StringComparison.Ordinal)
                ? null
                : subObj.ParameterName);

    /// <summary>
    /// Kept entries of a counted list section. The count key and the slots
    /// <c>1..<paramref name="generatedCount"/></c> the writer emitted are skipped.
    /// </summary>
    private protected static void WriteCountedListRemainingEntries(
        StringBuilder sb,
        OrderedStringDictionary? entries,
        string countKey,
        int generatedCount)
        => WriteRemainingEntries(sb, entries, key => SectionEntryKeys.IsCountedListKey(key, countKey, generatedCount));

    /// <summary>
    /// Kept entries of a compact sub-object list. <c>NrOfEntries</c> and the sub-indexes in
    /// <paramref name="generatedSubIndexes"/> the writer emitted are skipped.
    /// </summary>
    private protected static void WriteCompactListRemainingEntries(
        StringBuilder sb,
        OrderedStringDictionary? entries,
        ICollection<byte> generatedSubIndexes)
        => WriteRemainingEntries(sb, entries, key => SectionEntryKeys.IsAppliedCompactListKey(key, generatedSubIndexes));

    /// <summary>
    /// Extension point for format-specific sub-object fields.
    /// EDS: no-op. DCF: writes ParameterValue, Denotation, ParamRefd.
    /// </summary>
    protected virtual void WriteSubObjectExtension(StringBuilder sb, CanOpenSubObject subObj)
    {
    }

    /// <summary>Writes the [SupportedModules] list and each [M{n}ModuleInfo] section.</summary>
    protected static void WriteSupportedModules(StringBuilder sb, List<ModuleInfo> modules)
        => WriteSupportedModules(sb, modules, sectionEntries: null);

    /// <summary>
    /// Writes <c>[SupportedModules]</c> and every module with the kept entries of each module
    /// section from <paramref name="sectionEntries"/>.
    /// </summary>
    private protected static void WriteSupportedModules(
        StringBuilder sb,
        List<ModuleInfo> modules,
        Dictionary<string, OrderedStringDictionary>? sectionEntries)
    {
        IniRoundTripText.WriteSectionHeader(sb, "SupportedModules");
        WriteKeyValue(sb, "NrOfEntries", modules.Count.ToString(CultureInfo.InvariantCulture));
        WriteRemainingEntries(
            sb,
            GetSectionEntries(sectionEntries, "SupportedModules"),
            SectionEntryKeys.IsSupportedModulesKey);
        sb.AppendLine();

        foreach (var module in modules)
        {
            WriteModuleInfo(sb, module, sectionEntries);
        }
    }

    /// <summary>
    /// Writes one module in CiA 306-1 §8.3 order:
    /// <c>[MxModuleInfo]</c>, <c>[MxComments]</c>, <c>[MxFixedObjects]</c>,
    /// <c>[MxFixedxxxx]</c> (with <c>sub</c> sections), <c>[MxSubExtends]</c>,
    /// <c>[MxSubExtxxxx]</c>.
    /// </summary>
    protected static void WriteModuleInfo(StringBuilder sb, ModuleInfo module)
        => WriteModuleInfo(sb, module, sectionEntries: null);

    private static void WriteModuleInfo(
        StringBuilder sb,
        ModuleInfo module,
        Dictionary<string, OrderedStringDictionary>? sectionEntries)
    {
        var moduleInfoSection = string.Format(CultureInfo.InvariantCulture, "M{0}ModuleInfo", module.ModuleNumber);
        IniRoundTripText.WriteSectionHeader(sb, moduleInfoSection);
        WriteKeyValue(sb, "ProductName", module.ProductName);
        WriteKeyValue(sb, "ProductVersion", module.ProductVersion.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "ProductRevision", module.ProductRevision.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "OrderCode", module.OrderCode);
        WriteRemainingEntries(sb, GetSectionEntries(sectionEntries, moduleInfoSection), SectionEntryKeys.IsModuleInfoKey);
        sb.AppendLine();

        if (module.Comments != null)
        {
            WriteModuleComments(sb, module);
        }

        var fixedObjectsSection = string.Format(CultureInfo.InvariantCulture, "M{0}FixedObjects", module.ModuleNumber);
        var keptFixedObjects = GetSectionEntries(sectionEntries, fixedObjectsSection);
        if (module.FixedObjects.Count > 0 || keptFixedObjects != null)
        {
            IniRoundTripText.WriteSectionHeader(sb, fixedObjectsSection);
            WriteKeyValue(sb, "NrOfEntries", module.FixedObjects.Count.ToString(CultureInfo.InvariantCulture));

            for (int i = 0; i < module.FixedObjects.Count; i++)
            {
                WriteKeyValue(sb, (i + 1).ToString(CultureInfo.InvariantCulture), ValueConverter.FormatInteger(module.FixedObjects[i]));
            }

            WriteCountedListRemainingEntries(sb, keptFixedObjects, SectionEntryKeys.NrOfEntriesKey, module.FixedObjects.Count);
            sb.AppendLine();
        }

        var writtenFixed = new HashSet<ushort>();
        foreach (var index in module.FixedObjects)
        {
            if (!writtenFixed.Add(index))
                continue;

            if (module.FixedObjectDefinitions.TryGetValue(index, out var listed))
            {
                WriteModuleFixedObject(sb, module.ModuleNumber, index, listed);
            }
        }

        foreach (var index in module.FixedObjectDefinitions.Keys.OrderBy(key => key))
        {
            if (writtenFixed.Contains(index))
                continue;

            WriteModuleFixedObject(sb, module.ModuleNumber, index, module.FixedObjectDefinitions[index]);
        }

        var subExtendsSection = string.Format(CultureInfo.InvariantCulture, "M{0}SubExtends", module.ModuleNumber);
        var keptSubExtends = GetSectionEntries(sectionEntries, subExtendsSection);
        if (module.SubExtends.Count > 0 || keptSubExtends != null)
        {
            IniRoundTripText.WriteSectionHeader(sb, subExtendsSection);
            WriteKeyValue(sb, "NrOfEntries", module.SubExtends.Count.ToString(CultureInfo.InvariantCulture));

            for (var i = 0; i < module.SubExtends.Count; i++)
            {
                WriteKeyValue(
                    sb,
                    (i + 1).ToString(CultureInfo.InvariantCulture),
                    ValueConverter.FormatInteger(module.SubExtends[i]));
            }

            WriteCountedListRemainingEntries(sb, keptSubExtends, SectionEntryKeys.NrOfEntriesKey, module.SubExtends.Count);
            sb.AppendLine();
        }

        var writtenExtensions = new HashSet<ushort>();
        foreach (var index in module.SubExtends)
        {
            if (!writtenExtensions.Add(index))
                continue;

            if (module.SubExtensionDefinitions.TryGetValue(index, out var listed))
            {
                WriteModuleSubExtension(sb, module.ModuleNumber, index, listed, sectionEntries);
            }
        }

        foreach (var index in module.SubExtensionDefinitions.Keys.OrderBy(key => key))
        {
            if (writtenExtensions.Contains(index))
                continue;

            WriteModuleSubExtension(sb, module.ModuleNumber, index, module.SubExtensionDefinitions[index], sectionEntries);
        }
    }

    /// <summary>Writes <c>[MxComments]</c> (CiA 306-1 §8.3).</summary>
    private static void WriteModuleComments(StringBuilder sb, ModuleInfo module)
    {
        var comments = module.Comments!;
        IniRoundTripText.WriteSectionHeader(
            sb,
            string.Format(CultureInfo.InvariantCulture, "M{0}Comments", module.ModuleNumber));
        WriteKeyValue(sb, "Lines", comments.Lines.ToString(CultureInfo.InvariantCulture));

        foreach (var line in comments.CommentLines.OrderBy(entry => entry.Key))
        {
            WriteKeyValue(sb, string.Format(CultureInfo.InvariantCulture, "Line{0}", line.Key), line.Value);
        }

        WriteCommentsRemainingEntries(sb, comments);
        sb.AppendLine();
    }

    /// <summary>
    /// Kept entries of <c>[Comments]</c> / <c>[MxComments]</c>. A <c>Line&lt;n&gt;</c> generated
    /// from <see cref="Comments.CommentLines"/> wins over a kept entry with the same key.
    /// </summary>
    private static void WriteCommentsRemainingEntries(StringBuilder sb, Comments comments)
        => WriteRemainingEntries(
            sb,
            comments.RemainingEntries,
            key => SectionEntryKeys.IsGeneratedCommentsKey(key, comments.CommentLines.Keys));

    /// <summary>
    /// Writes <c>[MxFixedxxxx]</c> and its <c>[MxFixedxxxxsubx]</c> sections.
    /// Field order matches <see cref="WriteObject(StringBuilder, CanOpenObject, Action{string, Action})"/> so a module object body is
    /// the same canonical INI as a dictionary object, with the module prefix.
    /// An explicit <see cref="CanOpenObject.SubNumber"/> is written as stored.
    /// When it is absent and sub-objects exist, the emitted value is the described-entry
    /// count (including sub-index 00h and excluding FFh), matching
    /// <see cref="CanOpenObject.SubNumber"/>.
    /// </summary>
    private static void WriteModuleFixedObject(StringBuilder sb, int moduleNumber, ushort index, CanOpenObject obj)
    {
        IniRoundTripText.WriteSectionHeader(
            sb,
            string.Format(CultureInfo.InvariantCulture, "M{0}Fixed{1:X}", moduleNumber, index));

        var subNumberToWrite = obj.SubNumber ?? DescribedSubIndexCount(obj);

        if (subNumberToWrite > 0 || obj.SubObjects.Count > 0)
        {
            WriteKeyValue(sb, "SubNumber", subNumberToWrite.ToString(CultureInfo.InvariantCulture));
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

        if (obj.CompactSubObj.HasValue && obj.CompactSubObj.Value > 0)
        {
            WriteKeyValue(sb, "CompactSubObj", obj.CompactSubObj.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrEmpty(obj.ParameterValue))
        {
            WriteKeyValue(sb, "ParameterValue", obj.ParameterValue);
        }

        if (!string.IsNullOrEmpty(obj.Denotation))
        {
            WriteKeyValue(sb, "Denotation", obj.Denotation);
        }

        if (!string.IsNullOrEmpty(obj.ParamRefd))
        {
            WriteKeyValue(sb, "ParamRefd", obj.ParamRefd);
        }

        if (!string.IsNullOrEmpty(obj.UploadFile))
        {
            WriteKeyValue(sb, "UploadFile", obj.UploadFile);
        }

        if (!string.IsNullOrEmpty(obj.DownloadFile))
        {
            WriteKeyValue(sb, "DownloadFile", obj.DownloadFile);
        }

        WriteRemainingEntries(sb, obj.RemainingEntries, SectionEntryKeys.IsDcfObjectKey);
        sb.AppendLine();

        foreach (var subEntry in obj.SubObjects.OrderBy(entry => entry.Key))
        {
            WriteModuleFixedSubObject(sb, moduleNumber, index, subEntry.Key, subEntry.Value);
        }
    }

    /// <summary>
    /// Count of described sub-indices, including 00h and excluding FFh.
    /// Byte keys without FFh hold at most 255 entries, so the count fits in a byte.
    /// </summary>
    private static byte DescribedSubIndexCount(CanOpenObject obj)
    {
        var count = obj.SubObjects.Count;
        if (obj.SubObjects.ContainsKey(0xFF))
            count--;

        return (byte)count;
    }

    private static void WriteModuleFixedSubObject(
        StringBuilder sb,
        int moduleNumber,
        ushort index,
        byte dictionaryKey,
        CanOpenSubObject subObj)
    {
        // The section identity is the dictionary key. SubIndex defaults to 0, so
        // two entries whose property was left unset would otherwise both be sub0.
        IniRoundTripText.WriteSectionHeader(
            sb,
            string.Format(
                CultureInfo.InvariantCulture,
                "M{0}Fixed{1:X}sub{2:X}",
                moduleNumber,
                index,
                dictionaryKey));
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

        if (!string.IsNullOrEmpty(subObj.ParameterValue))
        {
            WriteKeyValue(sb, "ParameterValue", subObj.ParameterValue);
        }

        if (!string.IsNullOrEmpty(subObj.Denotation))
        {
            WriteKeyValue(sb, "Denotation", subObj.Denotation);
        }

        if (!string.IsNullOrEmpty(subObj.ParamRefd))
        {
            WriteKeyValue(sb, "ParamRefd", subObj.ParamRefd);
        }

        WriteRemainingEntries(sb, subObj.RemainingEntries, SectionEntryKeys.IsDcfSubObjectKey);
        sb.AppendLine();
    }

    /// <summary>
    /// Writes <c>[MxSubExtxxxx]</c> (CiA 306-1 §8.3). The body is a standard object
    /// description, then the module entries <c>Count</c> and <c>ObjExtend</c>.
    /// Optional object entries are omitted when the section did not contain them.
    /// </summary>
    private static void WriteModuleSubExtension(
        StringBuilder sb,
        int moduleNumber,
        ushort index,
        ModuleSubExtension extension,
        Dictionary<string, OrderedStringDictionary>? sectionEntries)
    {
        var sectionName = string.Format(CultureInfo.InvariantCulture, "M{0}SubExt{1:X}", moduleNumber, index);
        IniRoundTripText.WriteSectionHeader(sb, sectionName);

        if (extension.SubNumber.HasValue)
        {
            WriteKeyValue(sb, "SubNumber", extension.SubNumber.Value.ToString(CultureInfo.InvariantCulture));
        }

        WriteKeyValue(sb, "ParameterName", extension.ParameterName);

        if (extension.ObjectType.HasValue)
        {
            WriteKeyValue(sb, "ObjectType", ValueConverter.FormatInteger(extension.ObjectType.Value));
        }

        WriteKeyValue(sb, "DataType", ValueConverter.FormatInteger(extension.DataType));
        WriteKeyValue(sb, "AccessType", ValueConverter.AccessTypeToString(extension.AccessType));

        if (!string.IsNullOrEmpty(extension.DefaultValue))
        {
            WriteKeyValue(sb, "DefaultValue", extension.DefaultValue);
        }

        if (!string.IsNullOrEmpty(extension.LowLimit))
        {
            WriteKeyValue(sb, "LowLimit", extension.LowLimit);
        }

        if (!string.IsNullOrEmpty(extension.HighLimit))
        {
            WriteKeyValue(sb, "HighLimit", extension.HighLimit);
        }

        WriteKeyValue(sb, "PDOMapping", ValueConverter.FormatBoolean(extension.PdoMapping));

        if (extension.ObjFlags > 0)
        {
            WriteKeyValue(sb, "ObjFlags", ValueConverter.FormatInteger(extension.ObjFlags));
        }

        if (extension.CompactSubObj is > 0)
        {
            WriteKeyValue(sb, "CompactSubObj", extension.CompactSubObj.Value.ToString(CultureInfo.InvariantCulture));
        }

        WriteKeyValue(sb, "Count", extension.Count);

        if (extension.ObjExtend.HasValue)
        {
            WriteKeyValue(sb, "ObjExtend", extension.ObjExtend.Value.ToString(CultureInfo.InvariantCulture));
        }

        WriteRemainingEntries(sb, GetSectionEntries(sectionEntries, sectionName), SectionEntryKeys.IsModuleSubExtensionKey);
        sb.AppendLine();
    }

    /// <summary>Writes the [Comments] section.</summary>
    protected static void WriteComments(StringBuilder sb, Comments comments)
    {
        IniRoundTripText.WriteSectionHeader(sb, "Comments");
        WriteKeyValue(sb, "Lines", comments.Lines.ToString(CultureInfo.InvariantCulture));

        foreach (var line in comments.CommentLines.OrderBy(l => l.Key))
        {
            WriteKeyValue(sb, string.Format(CultureInfo.InvariantCulture, "Line{0}", line.Key), line.Value);
        }

        WriteCommentsRemainingEntries(sb, comments);
        sb.AppendLine();
    }

    /// <summary>Writes the [DynamicChannels] section.</summary>
    protected static void WriteDynamicChannels(StringBuilder sb, DynamicChannels dynamicChannels)
    {
        IniRoundTripText.WriteSectionHeader(sb, "DynamicChannels");
        WriteKeyValue(sb, "NrOfSeg", dynamicChannels.Segments.Count.ToString(CultureInfo.InvariantCulture));

        for (int i = 0; i < dynamicChannels.Segments.Count; i++)
        {
            var idx = (i + 1).ToString(CultureInfo.InvariantCulture);
            var seg = dynamicChannels.Segments[i];
            WriteKeyValue(sb, $"Type{idx}", ValueConverter.FormatInteger(seg.Type));
            WriteKeyValue(sb, $"Dir{idx}", ValueConverter.AccessTypeToString(seg.Dir));
            WriteKeyValue(sb, $"Range{idx}", seg.Range);
            var ppOffset = seg.PPOffset.ToString(CultureInfo.InvariantCulture);
            if (seg.PPOffsetAddressDifference.HasValue)
                ppOffset += ", " + seg.PPOffsetAddressDifference.Value.ToString(CultureInfo.InvariantCulture);
            WriteKeyValue(sb, $"PPOffset{idx}", ppOffset);
        }

        WriteRemainingEntries(
            sb,
            dynamicChannels.RemainingEntries,
            key => SectionEntryKeys.IsDynamicChannelsKey(key, dynamicChannels.Segments.Count));
        sb.AppendLine();
    }

    /// <summary>Writes the [Tools] list and each [Tool{n}] section.</summary>
    protected static void WriteTools(StringBuilder sb, List<ToolInfo> tools)
        => WriteTools(sb, tools, sectionEntries: null);

    /// <summary>
    /// Writes <c>[Tools]</c> with its kept entries from <paramref name="sectionEntries"/>, then
    /// each <c>[Tool{n}]</c> with <see cref="ToolInfo.RemainingEntries"/>.
    /// </summary>
    private protected static void WriteTools(
        StringBuilder sb,
        List<ToolInfo> tools,
        Dictionary<string, OrderedStringDictionary>? sectionEntries)
    {
        IniRoundTripText.WriteSectionHeader(sb, "Tools");
        WriteKeyValue(sb, "Items", tools.Count.ToString(CultureInfo.InvariantCulture));
        WriteRemainingEntries(sb, GetSectionEntries(sectionEntries, "Tools"), SectionEntryKeys.IsToolsKey);
        sb.AppendLine();

        for (int i = 0; i < tools.Count; i++)
        {
            var idx = (i + 1).ToString(CultureInfo.InvariantCulture);
            IniRoundTripText.WriteSectionHeader(
                sb,
                string.Format(CultureInfo.InvariantCulture, "Tool{0}", idx));
            WriteKeyValue(sb, "Name", tools[i].Name);
            WriteKeyValue(sb, "Command", tools[i].Command);
            WriteRemainingEntries(sb, tools[i].RemainingEntries, SectionEntryKeys.IsToolKey);
            sb.AppendLine();
        }
    }

    /// <summary>
    /// <see langword="true"/> when <paramref name="sectionEntries"/> or the model keeps entries
    /// that require the <c>[Tools]</c> section although there is no tool.
    /// </summary>
    private protected static bool MustWriteTools(
        List<ToolInfo> tools,
        Dictionary<string, OrderedStringDictionary> sectionEntries)
        => tools.Count > 0 || HasSectionEntries(sectionEntries, "Tools");

    /// <summary>
    /// <see langword="true"/> when <c>[Comments]</c> has lines or kept entries to write.
    /// </summary>
    private protected static bool MustWriteComments(Comments? comments)
        => comments != null && (comments.CommentLines.Count > 0 || comments.RemainingEntries.Count > 0);

    /// <summary>
    /// <see langword="true"/> when <c>[DynamicChannels]</c> has segments or kept entries to write.
    /// </summary>
    private protected static bool MustWriteDynamicChannels(DynamicChannels? dynamicChannels)
        => dynamicChannels != null
           && (dynamicChannels.Segments.Count > 0 || dynamicChannels.RemainingEntries.Count > 0);

    /// <summary>Writes a non-standard additional section.</summary>
    protected static void WriteAdditionalSection(StringBuilder sb, string sectionName, Dictionary<string, string> entries)
    {
        IniRoundTripText.WriteSectionHeader(sb, sectionName);

        foreach (var entry in entries.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
        {
            WriteKeyValue(sb, entry.Key, entry.Value);
        }

        sb.AppendLine();
    }
}
