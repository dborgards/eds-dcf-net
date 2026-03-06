namespace EdsDcfNet.Validation;

using System.Collections.ObjectModel;
using System.Globalization;
using EdsDcfNet.Models;

/// <summary>
/// Validates CANopen models against common CiA 306 constraints.
/// </summary>
public static class CanOpenModelValidator
{
    private static readonly HashSet<ushort> AllowedBaudrates = new()
    {
        10, 20, 50, 125, 250, 500, 800, 1000
    };

    private const int MaxParameterNameLength = 241;
    private const int MaxNodeNameLength = 246;
    private const int MaxNetworkNameLength = 243;
    private const int MaxVendorNameLength = 244;
    private const int MaxProductNameLength = 243;
    private const int MaxOrderCodeLength = 245;
    private const int MaxReferenceNameLength = 249;
    private const byte MaxGranularity = 64;

    /// <summary>
    /// Validates an <see cref="ElectronicDataSheet"/> instance.
    /// </summary>
    /// <param name="eds">Model instance to validate.</param>
    /// <returns>List of validation issues. Empty when model is valid.</returns>
    public static IReadOnlyList<ValidationIssue> Validate(ElectronicDataSheet eds)
    {
        ThrowIfNull(eds, nameof(eds));

        var issues = new List<ValidationIssue>();
        ValidateDeviceInfo(eds.DeviceInfo, issues);
        ValidateObjectDictionary(eds.ObjectDictionary, issues);

        return new ReadOnlyCollection<ValidationIssue>(issues);
    }

    /// <summary>
    /// Validates a <see cref="DeviceConfigurationFile"/> instance.
    /// </summary>
    /// <param name="dcf">Model instance to validate.</param>
    /// <returns>List of validation issues. Empty when model is valid.</returns>
    public static IReadOnlyList<ValidationIssue> Validate(DeviceConfigurationFile dcf)
    {
        ThrowIfNull(dcf, nameof(dcf));

        var issues = new List<ValidationIssue>();
        ValidateDeviceInfo(dcf.DeviceInfo, issues);
        ValidateObjectDictionary(dcf.ObjectDictionary, issues);
        ValidateDeviceCommissioning(dcf.DeviceCommissioning, issues);

        return new ReadOnlyCollection<ValidationIssue>(issues);
    }

    private static void ValidateDeviceCommissioning(
        DeviceCommissioning commissioning,
        List<ValidationIssue> issues)
    {
        if (commissioning.NodeId < 1 || commissioning.NodeId > 127)
        {
            issues.Add(new ValidationIssue(
                "DeviceCommissioning.NodeId",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Node-ID {0} is outside the CANopen range 1..127.",
                    commissioning.NodeId)));
        }

        // 0 is treated as "not configured yet" and accepted by writers/parsers.
        if (commissioning.Baudrate != 0 &&
            !AllowedBaudrates.Contains(commissioning.Baudrate))
        {
            issues.Add(new ValidationIssue(
                "DeviceCommissioning.Baudrate",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Baudrate {0} is not supported. Allowed values: {1}.",
                    commissioning.Baudrate,
                    string.Join(
                        ", ",
                        AllowedBaudrates
                            .OrderBy(v => v)
                            .Select(v => v.ToString(CultureInfo.InvariantCulture))))));
        }

        ValidateMaxLength(commissioning.NodeName, MaxNodeNameLength, "DeviceCommissioning.NodeName", issues);
        ValidateMaxLength(commissioning.NetworkName, MaxNetworkNameLength, "DeviceCommissioning.NetworkName", issues);
        ValidateMaxLength(commissioning.NodeRefd, MaxReferenceNameLength, "DeviceCommissioning.NodeRefd", issues);
        ValidateMaxLength(commissioning.NetRefd, MaxReferenceNameLength, "DeviceCommissioning.NetRefd", issues);
    }

    private static void ValidateDeviceInfo(
        DeviceInfo deviceInfo,
        List<ValidationIssue> issues)
    {
        ValidateMaxLength(deviceInfo.VendorName, MaxVendorNameLength, "DeviceInfo.VendorName", issues);
        ValidateMaxLength(deviceInfo.ProductName, MaxProductNameLength, "DeviceInfo.ProductName", issues);
        ValidateMaxLength(deviceInfo.OrderCode, MaxOrderCodeLength, "DeviceInfo.OrderCode", issues);

        if (deviceInfo.Granularity > MaxGranularity)
        {
            issues.Add(new ValidationIssue(
                "DeviceInfo.Granularity",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Granularity {0} exceeds maximum of {1}.",
                    deviceInfo.Granularity,
                    MaxGranularity)));
        }
    }

    private static void ValidateObjectDictionary(
        ObjectDictionary objectDictionary,
        List<ValidationIssue> issues)
    {
        var classifiedIndices = new HashSet<ushort>();

        ValidateObjectList(
            objectDictionary.MandatoryObjects,
            "ObjectDictionary.MandatoryObjects",
            objectDictionary.Objects,
            classifiedIndices,
            issues);
        ValidateObjectList(
            objectDictionary.OptionalObjects,
            "ObjectDictionary.OptionalObjects",
            objectDictionary.Objects,
            classifiedIndices,
            issues);
        ValidateObjectList(
            objectDictionary.ManufacturerObjects,
            "ObjectDictionary.ManufacturerObjects",
            objectDictionary.Objects,
            classifiedIndices,
            issues);

        foreach (var kvp in objectDictionary.Objects.OrderBy(k => k.Key))
        {
            ValidateObject(kvp.Key, kvp.Value, issues);
        }

        foreach (var index in objectDictionary.Objects.Keys.OrderBy(i => i))
        {
            if (!classifiedIndices.Contains(index))
            {
                issues.Add(new ValidationIssue(
                    "ObjectDictionary.Objects[" +
                    string.Format(CultureInfo.InvariantCulture, "0x{0:X4}", index) +
                    "]",
                    "Object is present in dictionary but not listed in Mandatory/Optional/Manufacturer object lists."));
            }
        }
    }

    private static void ValidateObjectList(
        IEnumerable<ushort> indexes,
        string listPath,
        Dictionary<ushort, CanOpenObject> objects,
        HashSet<ushort> classifiedIndices,
        List<ValidationIssue> issues)
    {
        var seenInCurrentList = new HashSet<ushort>();
        foreach (var index in indexes)
        {
            var hexIndex = string.Format(CultureInfo.InvariantCulture, "0x{0:X4}", index);
            if (!seenInCurrentList.Add(index))
            {
                issues.Add(new ValidationIssue(
                    listPath,
                    "Object index " + hexIndex + " appears multiple times in this object list."));
                continue;
            }

            if (!classifiedIndices.Add(index))
            {
                issues.Add(new ValidationIssue(
                    listPath,
                    "Object index " + hexIndex + " appears in multiple object lists."));
            }

            if (!objects.ContainsKey(index))
            {
                issues.Add(new ValidationIssue(
                    listPath,
                    "Object list references missing object " + hexIndex + "."));
            }
        }
    }

    private static void ValidateObject(
        ushort index,
        CanOpenObject obj,
        List<ValidationIssue> issues)
    {
        var objectPath = string.Format(CultureInfo.InvariantCulture, "ObjectDictionary.Objects[0x{0:X4}]", index);

        ValidateMaxLength(
            obj.ParameterName,
            MaxParameterNameLength,
            objectPath + ".ParameterName",
            issues);

        if (!IsValidObjectType(obj.ObjectType))
        {
            issues.Add(new ValidationIssue(
                objectPath + ".ObjectType",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ObjectType 0x{0:X2} is not a valid CiA 306 object code.",
                    obj.ObjectType)));
        }

        var hasCompactSubObjects = obj.CompactSubObj.HasValue && obj.CompactSubObj.Value > 0;
        if (obj.SubNumber.HasValue &&
            obj.SubNumber.Value > 0 &&
            obj.SubObjects.Count == 0 &&
            !hasCompactSubObjects)
        {
            issues.Add(new ValidationIssue(
                objectPath + ".SubNumber",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "SubNumber is {0} but neither sub-objects nor CompactSubObj are defined.",
                    obj.SubNumber.Value)));
        }

        foreach (var subObject in obj.SubObjects.OrderBy(s => s.Key))
        {
            ValidateMaxLength(
                subObject.Value.ParameterName,
                MaxParameterNameLength,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.SubObjects[0x{1:X2}].ParameterName",
                    objectPath,
                    subObject.Key),
                issues);
        }
    }

    private static bool IsValidObjectType(byte objectType)
    {
        return objectType == 0x0 ||
               objectType == 0x2 ||
               objectType == 0x5 ||
               objectType == 0x6 ||
               objectType == 0x7 ||
               objectType == 0x8 ||
               objectType == 0x9;
    }

    private static void ThrowIfNull(object? value, string paramName)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value, paramName);
#else
        if (value == null)
            throw new ArgumentNullException(paramName);
#endif
    }

    private static void ValidateMaxLength(
        string? value,
        int maxLength,
        string path,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrEmpty(value))
            return;

        var text = value!;
        if (text.Length > maxLength)
        {
            issues.Add(new ValidationIssue(
                path,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Length {0} exceeds max allowed length {1}.",
                    text.Length,
                    maxLength)));
        }
    }
}
