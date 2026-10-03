namespace EdsDcfNet.Models;

using System.Globalization;

/// <summary>
/// Case-insensitive INI keys that the EDS/DCF readers map onto model properties, per section.
/// Every key a section parser does not recognise here is kept as a remaining entry of that
/// section and written back after the generated keys.
/// </summary>
internal static class SectionEntryKeys
{
    private static readonly HashSet<string> FileInfoKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "FileName",
        "FileVersion",
        "FileRevision",
        "EDSVersion",
        "Description",
        "CreationTime",
        "CreationDate",
        "CreatedBy",
        "ModificationTime",
        "ModificationDate",
        "ModifiedBy"
    };

    private static readonly HashSet<string> DeviceInfoKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "VendorName",
        "VendorNumber",
        "ProductName",
        "ProductNumber",
        "RevisionNumber",
        "OrderCode",
        "BaudRate_10",
        "BaudRate_20",
        "BaudRate_50",
        "BaudRate_125",
        "BaudRate_250",
        "BaudRate_500",
        "BaudRate_800",
        "BaudRate_1000",
        "SimpleBootUpMaster",
        "SimpleBootUpSlave",
        "Granularity",
        "DynamicChannelsSupported",
        "GroupMessaging",
        "NrOfRXPDO",
        "NrOfTXPDO",
        "LSS_Supported",
        "CompactPDO",
        "CANopenSafetySupported"
    };

    private static readonly HashSet<string> DeviceCommissioningKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "NodeID",
        "NodeName",
        "NodeRefd",
        "Baudrate",
        "NetNumber",
        "NetworkName",
        "NetRefd",
        "CANopenManager",
        "LSS_SerialNumber"
    };

    private static readonly HashSet<string> ModuleInfoKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ProductName",
        "ProductVersion",
        "ProductRevision",
        "OrderCode"
    };

    private static readonly HashSet<string> ModuleSubExtensionKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "SubNumber",
        "ParameterName",
        "ObjectType",
        "DataType",
        "AccessType",
        "DefaultValue",
        "LowLimit",
        "HighLimit",
        "PDOMapping",
        "ObjFlags",
        "CompactSubObj",
        "Count",
        "ObjExtend"
    };

    private static readonly HashSet<string> ToolKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Name",
        "Command"
    };

    private static readonly string[] DynamicChannelSegmentPrefixes = { "Type", "Dir", "Range", "PPOffset" };

    /// <summary>Entry-count key of compact lists, module lists and connected modules.</summary>
    internal const string NrOfEntriesKey = "NrOfEntries";

    /// <summary>Entry-count key of the three object list sections.</summary>
    internal const string SupportedObjectsKey = "SupportedObjects";

    /// <summary>Entry-count key of <c>[xxxxObjectLinks]</c>.</summary>
    internal const string ObjectLinksCountKey = "ObjectLinks";

    /// <summary>Highest sub-index a compact Name/Value/Denotation list can address.</summary>
    private const int MaxCompactListableSubIndex = 254;
    private static readonly HashSet<string> ObjectKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ParameterName",
        "ObjectType",
        "DataType",
        "AccessType",
        "DefaultValue",
        "LowLimit",
        "HighLimit",
        "PDOMapping",
        "SRDOMapping",
        "InvertedSRAD",
        "ObjFlags",
        "SubNumber",
        "CompactSubObj"
    };

    private static readonly HashSet<string> SubObjectKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ParameterName",
        "ObjectType",
        "DataType",
        "AccessType",
        "DefaultValue",
        "LowLimit",
        "HighLimit",
        "PDOMapping",
        "SRDOMapping",
        "InvertedSRAD"
    };

    private static readonly HashSet<string> DcfObjectOnlyKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ParameterValue",
        "Denotation",
        "ParamRefd",
        "UploadFile",
        "DownloadFile"
    };

    private static readonly HashSet<string> DcfSubObjectOnlyKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ParameterValue",
        "Denotation",
        "ParamRefd"
    };

    internal static bool IsEdsObjectKey(string key) => ObjectKeys.Contains(key);

    internal static bool IsEdsSubObjectKey(string key) => SubObjectKeys.Contains(key);

    internal static bool IsDcfObjectKey(string key)
        => IsEdsObjectKey(key) || DcfObjectOnlyKeys.Contains(key);

    internal static bool IsDcfSubObjectKey(string key)
        => IsEdsSubObjectKey(key) || DcfSubObjectOnlyKeys.Contains(key);

    /// <summary><c>[FileInfo]</c> keys (CiA 306-1 § 6.4, Table 1).</summary>
    internal static bool IsEdsFileInfoKey(string key) => FileInfoKeys.Contains(key);

    /// <summary><c>[FileInfo]</c> keys of a DCF: the EDS keys plus <c>LastEDS</c> (§ 7.2).</summary>
    internal static bool IsDcfFileInfoKey(string key)
        => IsEdsFileInfoKey(key) || string.Equals(key, "LastEDS", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <c>[DeviceInfo]</c> keys mapped onto <see cref="DeviceInfo"/> (§ 6.5, Table 2). The
    /// entries § 6.5 reserves for compatibility are not mapped and are not in this set.
    /// </summary>
    internal static bool IsDeviceInfoKey(string key) => DeviceInfoKeys.Contains(key);

    /// <summary><c>[DeviceComissioning]</c> keys (§ 7.3.5).</summary>
    internal static bool IsDeviceCommissioningKey(string key) => DeviceCommissioningKeys.Contains(key);

    /// <summary><c>[MxModuleInfo]</c> keys (§ 8.3, Table 14).</summary>
    internal static bool IsModuleInfoKey(string key) => ModuleInfoKeys.Contains(key);

    /// <summary><c>[MxSubExtxxxx]</c> keys mapped onto <see cref="ModuleSubExtension"/>.</summary>
    internal static bool IsModuleSubExtensionKey(string key) => ModuleSubExtensionKeys.Contains(key);

    /// <summary><c>[ToolX]</c> keys.</summary>
    internal static bool IsToolKey(string key) => ToolKeys.Contains(key);

    /// <summary><c>[Tools]</c> key.</summary>
    internal static bool IsToolsKey(string key) => string.Equals(key, "Items", StringComparison.OrdinalIgnoreCase);

    /// <summary><c>[SupportedModules]</c> key (§ 8.2.1, Table 13).</summary>
    internal static bool IsSupportedModulesKey(string key)
        => string.Equals(key, NrOfEntriesKey, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <c>[DummyUsage]</c> key <c>Dummy&lt;hex data type index&gt;</c> (§ 6.6.2, Figure 9), as the
    /// reader parses it.
    /// </summary>
    internal static bool TryParseDummyUsageKey(string key, out ushort index)
    {
        index = 0;
        if (!key.StartsWith("Dummy", StringComparison.OrdinalIgnoreCase))
            return false;

        var indexText = key.Length > 5 ? key[5..] : string.Empty;
        return ushort.TryParse(indexText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out index);
    }

    /// <summary><see langword="true"/> for a <c>[DummyUsage]</c> key the reader maps onto the model.</summary>
    internal static bool IsDummyUsageKey(string key) => TryParseDummyUsageKey(key, out _);

    /// <summary>
    /// A counted list section: the count key, or a decimal entry number <c>1..count</c> written
    /// without sign or leading zeros (§ 6.6.3.1: "numbered in decimal and shall begin with
    /// number one"). Used for object lists, <c>[xxxxObjectLinks]</c>, <c>[MxFixedObjects]</c>,
    /// <c>[MxSubExtends]</c> and <c>[ConnectedModules]</c>, on read (count from the file) and
    /// on write (count of generated entries).
    /// </summary>
    internal static bool IsCountedListKey(string key, string countKey, int count)
        => string.Equals(key, countKey, StringComparison.OrdinalIgnoreCase)
           || IsEntryNumber(key, count);

    /// <summary>
    /// <c>[Comments]</c> / <c>[MxComments]</c>: <c>Lines</c> or <c>Line&lt;n&gt;</c> with
    /// <c>1 &lt;= n &lt;= lines</c> (§ 6.6.5, Table 9; § 8.3, Table 15).
    /// </summary>
    internal static bool IsCommentsKey(string key, int lines)
    {
        if (string.Equals(key, "Lines", StringComparison.OrdinalIgnoreCase))
            return true;

        return key.StartsWith("Line", StringComparison.OrdinalIgnoreCase)
               && IsEntryNumber(key[4..], lines);
    }

    /// <summary>
    /// Writer side of <see cref="IsCommentsKey"/>: <c>Lines</c> or a <c>Line&lt;n&gt;</c> key the
    /// writer generates for one of <paramref name="lineNumbers"/>.
    /// </summary>
    internal static bool IsGeneratedCommentsKey(string key, ICollection<int> lineNumbers)
    {
        if (string.Equals(key, "Lines", StringComparison.OrdinalIgnoreCase))
            return true;

        return key.StartsWith("Line", StringComparison.OrdinalIgnoreCase)
               && TryParseEntryNumber(key[4..], out var number)
               && lineNumbers.Contains(number);
    }

    /// <summary>
    /// <c>[DynamicChannels]</c>: <c>NrOfSeg</c>, or <c>Type&lt;n&gt;</c>, <c>Dir&lt;n&gt;</c>,
    /// <c>Range&lt;n&gt;</c>, <c>PPOffset&lt;n&gt;</c> with <c>1 &lt;= n &lt;= segmentCount</c>
    /// (CiA 306-3).
    /// </summary>
    internal static bool IsDynamicChannelsKey(string key, int segmentCount)
    {
        if (string.Equals(key, "NrOfSeg", StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var prefix in DynamicChannelSegmentPrefixes)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && IsEntryNumber(key[prefix.Length..], segmentCount))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Compact sub-object list (<c>[xxxxName]</c>, DCF <c>[xxxxValue]</c> /
    /// <c>[xxxxDenotation]</c>): <c>NrOfEntries</c> or a sub-index key the reader applies.
    /// </summary>
    internal static bool IsCompactListKey(string key)
        => string.Equals(key, NrOfEntriesKey, StringComparison.OrdinalIgnoreCase)
           || TryParseCompactListSubIndex(key, out _);

    /// <summary>
    /// Parses a compact list key as a decimal sub-index in the CiA 306 range 1..254.
    /// Reserved sub-index <c>0xFF</c> and non-numeric keys are rejected.
    /// </summary>
    internal static bool TryParseCompactListSubIndex(string key, out byte subIndex)
        => byte.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out subIndex)
           && subIndex >= 1
           && subIndex <= MaxCompactListableSubIndex;

    /// <summary>
    /// <see langword="true"/> when <paramref name="text"/> is the decimal number
    /// <c>1..max</c> exactly as the invariant culture formats it.
    /// </summary>
    private static bool IsEntryNumber(string text, int max)
        => TryParseEntryNumber(text, out var number) && number <= max;

    private static bool TryParseEntryNumber(string text, out int number)
    {
        number = 0;
        if (text.Length == 0 || text.Length > 9 || text[0] == '0')
            return false;

        foreach (var c in text)
        {
            if (c < '0' || c > '9')
                return false;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }
}
