namespace EdsDcfNet.Models;

/// <summary>
/// Case-insensitive INI keys that are stored on object and sub-object properties.
/// </summary>
internal static class SectionEntryKeys
{
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
}
