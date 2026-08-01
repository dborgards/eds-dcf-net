namespace EdsDcfNet.Extensions;

using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

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
    /// <returns><c>true</c> if the object was found and the value was set; <c>false</c> if the object does not exist.</returns>
    public static bool SetParameterValue(this ObjectDictionary objDict, ushort index, string value)
    {
        if (objDict.Objects.TryGetValue(index, out var obj))
        {
            obj.ParameterValue = value;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Sets the parameter value for a sub-object.
    /// </summary>
    /// <returns><c>true</c> if the sub-object was found and the value was set; <c>false</c> if the object or sub-object does not exist.</returns>
    public static bool SetParameterValue(this ObjectDictionary objDict, ushort index, byte subIndex, string value)
    {
        var subObj = objDict.GetSubObject(index, subIndex);
        if (subObj != null)
        {
            subObj.ParameterValue = value;
            return true;
        }

        return false;
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
    /// Gets an object's configured or default value converted to the .NET type indicated
    /// by its CANopen data type.
    /// </summary>
    /// <param name="objDict">The object dictionary.</param>
    /// <param name="index">Object index.</param>
    /// <param name="nodeId">
    /// Optional node ID used to evaluate <c>$NODEID</c> formulas in the configured or default value.
    /// </param>
    /// <returns>The typed value, or <see langword="null"/> if the object or value does not exist.</returns>
    public static object? GetParameterValueAsObject(this ObjectDictionary objDict, ushort index, byte? nodeId = null)
    {
        var obj = objDict.GetObject(index);
        var value = ResolveParameterOrDefaultValue(obj?.ParameterValue, obj?.DefaultValue, obj?.DataType);
        if (value is null)
        {
            return null;
        }

        // Explicit DataType=0 from EDS parsing is HasValue but not a real CANopen type
        // (same sentinel sub-objects use when the field is omitted).
        if (obj!.DataType is null or 0)
        {
            throw new InvalidOperationException($"Object 0x{index:X4} does not define a CANopen data type.");
        }

        return CanOpenValueConverter.Parse(value, obj.DataType.Value, nodeId);
    }

    /// <summary>
    /// Gets a sub-object's configured or default value converted to the .NET type indicated
    /// by its CANopen data type.
    /// </summary>
    /// <param name="objDict">The object dictionary.</param>
    /// <param name="index">Object index.</param>
    /// <param name="subIndex">Sub-object sub-index.</param>
    /// <param name="nodeId">
    /// Optional node ID used to evaluate <c>$NODEID</c> formulas in the configured or default value.
    /// </param>
    /// <returns>The typed value, or <see langword="null"/> if the sub-object or value does not exist.</returns>
    public static object? GetParameterValueAsObject(this ObjectDictionary objDict, ushort index, byte subIndex, byte? nodeId = null)
    {
        var subObj = objDict.GetSubObject(index, subIndex);
        var value = ResolveParameterOrDefaultValue(subObj?.ParameterValue, subObj?.DefaultValue, subObj?.DataType);
        if (value is null)
        {
            return null;
        }

        // Sub-objects store a missing DataType as 0 (see ParseSubObject default), unlike
        // top-level objects which leave DataType null when the field is omitted.
        if (subObj!.DataType == 0)
        {
            throw new InvalidOperationException(
                $"Sub-object 0x{index:X4}:{subIndex:X2} does not define a CANopen data type.");
        }

        return CanOpenValueConverter.Parse(value, subObj.DataType, nodeId);
    }

    /// <summary>
    /// Gets an object's configured or default value as <typeparamref name="T"/>.
    /// </summary>
    /// <param name="objDict">The object dictionary.</param>
    /// <param name="index">Object index.</param>
    /// <param name="nodeId">
    /// Optional node ID used to evaluate <c>$NODEID</c> formulas in the configured or default value.
    /// </param>
    /// <exception cref="KeyNotFoundException">The object or its configured/default value does not exist.</exception>
    public static T GetParameterValue<T>(this ObjectDictionary objDict, ushort index, byte? nodeId = null)
    {
        return CastParameterValue<T>(objDict.GetParameterValueAsObject(index, nodeId), index, null);
    }

    /// <summary>
    /// Gets a sub-object's configured or default value as <typeparamref name="T"/>.
    /// </summary>
    /// <param name="objDict">The object dictionary.</param>
    /// <param name="index">Object index.</param>
    /// <param name="subIndex">Sub-object sub-index.</param>
    /// <param name="nodeId">
    /// Optional node ID used to evaluate <c>$NODEID</c> formulas in the configured or default value.
    /// </param>
    /// <exception cref="KeyNotFoundException">The sub-object or its configured/default value does not exist.</exception>
    public static T GetParameterValue<T>(this ObjectDictionary objDict, ushort index, byte subIndex, byte? nodeId = null)
    {
        return CastParameterValue<T>(objDict.GetParameterValueAsObject(index, subIndex, nodeId), index, subIndex);
    }

    /// <summary>
    /// Converts and sets an object's parameter value according to its CANopen data type.
    /// </summary>
    /// <returns><c>true</c> if the object was found and the value was set.</returns>
    public static bool SetParameterValue(this ObjectDictionary objDict, ushort index, object value)
    {
        EnsureNotNull(value, nameof(value));

        var obj = objDict.GetObject(index);
        if (obj == null)
        {
            return false;
        }

        // Explicit DataType=0 from EDS parsing is HasValue but not a real CANopen type
        // (same sentinel sub-objects use when the field is omitted).
        if (obj.DataType is null or 0)
        {
            throw new InvalidOperationException($"Object 0x{index:X4} does not define a CANopen data type.");
        }

        obj.ParameterValue = CanOpenValueConverter.Format(value, obj.DataType.Value);
        return true;
    }

    /// <summary>
    /// Converts and sets a sub-object's parameter value according to its CANopen data type.
    /// </summary>
    /// <returns><c>true</c> if the sub-object was found and the value was set.</returns>
    public static bool SetParameterValue(this ObjectDictionary objDict, ushort index, byte subIndex, object value)
    {
        EnsureNotNull(value, nameof(value));

        var subObj = objDict.GetSubObject(index, subIndex);
        if (subObj == null)
        {
            return false;
        }

        // Sub-objects store a missing DataType as 0 (see ParseSubObject default), unlike
        // top-level objects which leave DataType null when the field is omitted.
        if (subObj.DataType == 0)
        {
            throw new InvalidOperationException(
                $"Sub-object 0x{index:X4}:{subIndex:X2} does not define a CANopen data type.");
        }

        subObj.ParameterValue = CanOpenValueConverter.Format(value, subObj.DataType);
        return true;
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

    private static T CastParameterValue<T>(object? value, ushort index, byte? subIndex)
    {
        var address = subIndex.HasValue ? $"0x{index:X4}:{subIndex.Value:X2}" : $"0x{index:X4}";
        if (value == null)
        {
            throw new KeyNotFoundException($"Object Dictionary value {address} does not exist.");
        }

        if (value is T typedValue)
        {
            return typedValue;
        }

        throw new InvalidCastException(
            $"Object Dictionary value {address} has .NET type {value.GetType().Name}, not {typeof(T).Name}.");
    }

    private static void EnsureNotNull(object? value, string parameterName)
    {
        if (value == null)
        {
            throw new ArgumentNullException(parameterName);
        }
    }

    /// <summary>
    /// Prefers a non-missing configured value, otherwise a non-missing default value.
    /// Empty/whitespace handling is applied per field so a blank ParameterValue does not
    /// block fallback to a usable DefaultValue.
    /// </summary>
    private static string? ResolveParameterOrDefaultValue(string? parameterValue, string? defaultValue, ushort? dataType)
    {
        if (!IsMissingValue(parameterValue, dataType))
        {
            return parameterValue;
        }

        if (!IsMissingValue(defaultValue, dataType))
        {
            return defaultValue;
        }

        return null;
    }

    /// <summary>
    /// Decides whether a resolved textual value should be treated as "no value".
    /// Parsed models store a missing DefaultValue as an empty string, so empty is always
    /// missing. Whitespace-only is missing too, except for string data types where
    /// whitespace is representable content that the writers persist.
    /// </summary>
    private static bool IsMissingValue(string? value, ushort? dataType)
    {
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        var isStringType = dataType is 0x0009 or 0x000B; // VISIBLE_STRING, UNICODE_STRING
        return !isStringType && string.IsNullOrWhiteSpace(value);
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
