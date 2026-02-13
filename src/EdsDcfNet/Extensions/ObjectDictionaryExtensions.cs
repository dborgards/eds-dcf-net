namespace EdsDcfNet.Extensions;

using EdsDcfNet.Models;

/// <summary>
/// Extension methods for ObjectDictionary to make it easier to work with CANopen objects.
/// </summary>
public static class ObjectDictionaryExtensions
{
    /// <summary>
    /// Gets an object by index, or null if not found.
    /// </summary>
    public static CanOpenObject? GetObject(this ObjectDictionary objDict, ushort index)
    {
        return objDict.Objects.TryGetValue(index, out var obj) ? obj : null;
    }

    /// <summary>
    /// Gets a sub-object by index and sub-index, or null if not found.
    /// </summary>
    public static CanOpenSubObject? GetSubObject(this ObjectDictionary objDict, ushort index, byte subIndex)
    {
        var obj = objDict.GetObject(index);
        return obj?.SubObjects.TryGetValue(subIndex, out var subObj) == true ? subObj : null;
    }

    /// <summary>
    /// Sets the parameter value for an object.
    /// </summary>
    public static void SetParameterValue(this ObjectDictionary objDict, ushort index, string value)
    {
        if (objDict.Objects.TryGetValue(index, out var obj))
        {
            obj.ParameterValue = value;
        }
    }

    /// <summary>
    /// Sets the parameter value for a sub-object.
    /// </summary>
    public static void SetParameterValue(this ObjectDictionary objDict, ushort index, byte subIndex, string value)
    {
        var subObj = objDict.GetSubObject(index, subIndex);
        if (subObj != null)
        {
            subObj.ParameterValue = value;
        }
    }

    /// <summary>
    /// Gets the parameter value for an object (returns configured value if available, otherwise default value).
    /// </summary>
    public static string? GetParameterValue(this ObjectDictionary objDict, ushort index)
    {
        var obj = objDict.GetObject(index);
        return obj?.ParameterValue ?? obj?.DefaultValue;
    }

    /// <summary>
    /// Gets the parameter value for a sub-object (returns configured value if available, otherwise default value).
    /// </summary>
    public static string? GetParameterValue(this ObjectDictionary objDict, ushort index, byte subIndex)
    {
        var subObj = objDict.GetSubObject(index, subIndex);
        return subObj?.ParameterValue ?? subObj?.DefaultValue;
    }

    /// <summary>
    /// Gets all objects of a specific type (mandatory, optional, or manufacturer).
    /// </summary>
    public static IEnumerable<CanOpenObject> GetObjectsByType(this ObjectDictionary objDict, ObjectCategory category)
    {
        var indices = category switch
        {
            ObjectCategory.Mandatory => objDict.MandatoryObjects,
            ObjectCategory.Optional => objDict.OptionalObjects,
            ObjectCategory.Manufacturer => objDict.ManufacturerObjects,
            _ => Enumerable.Empty<ushort>()
        };

        return indices.Select(idx => objDict.GetObject(idx)).Where(obj => obj != null)!;
    }

    /// <summary>
    /// Gets all PDO communication parameter objects (0x1400-0x15FF for RPDO, 0x1800-0x19FF for TPDO).
    /// </summary>
    public static IEnumerable<CanOpenObject> GetPdoCommunicationParameters(this ObjectDictionary objDict, bool transmit = true)
    {
        var startIndex = (ushort)(transmit ? 0x1800 : 0x1400);
        var endIndex = (ushort)(transmit ? 0x19FF : 0x15FF);

        return objDict.Objects.Values
            .Where(obj => obj.Index >= startIndex && obj.Index <= endIndex)
            .OrderBy(obj => obj.Index);
    }

    /// <summary>
    /// Gets all PDO mapping parameter objects (0x1600-0x17FF for RPDO, 0x1A00-0x1BFF for TPDO).
    /// </summary>
    public static IEnumerable<CanOpenObject> GetPdoMappingParameters(this ObjectDictionary objDict, bool transmit = true)
    {
        var startIndex = (ushort)(transmit ? 0x1A00 : 0x1600);
        var endIndex = (ushort)(transmit ? 0x1BFF : 0x17FF);

        return objDict.Objects.Values
            .Where(obj => obj.Index >= startIndex && obj.Index <= endIndex)
            .OrderBy(obj => obj.Index);
    }
}

/// <summary>
/// Category of CANopen objects.
/// </summary>
public enum ObjectCategory
{
    /// <summary>
    /// Mandatory objects that must be implemented by all CANopen devices.
    /// </summary>
    Mandatory,

    /// <summary>
    /// Optional objects that may be implemented by CANopen devices.
    /// </summary>
    Optional,

    /// <summary>
    /// Manufacturer-specific objects defined by the device manufacturer.
    /// </summary>
    Manufacturer
}
