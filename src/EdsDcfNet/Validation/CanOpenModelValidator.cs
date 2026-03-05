namespace EdsDcfNet.Validation;

using System.Collections.ObjectModel;
using System.Globalization;
using EdsDcfNet.Models;

/// <summary>
/// Validates CANopen models against common CiA 306 constraints.
/// </summary>
public static class CanOpenModelValidator
{
    private static readonly ushort[] AllowedBaudrates =
    {
        10, 20, 50, 125, 250, 500, 800, 1000
    };

    /// <summary>
    /// Validates an <see cref="ElectronicDataSheet"/> instance.
    /// </summary>
    /// <param name="eds">Model instance to validate.</param>
    /// <returns>List of validation issues. Empty when model is valid.</returns>
    public static IReadOnlyList<ValidationIssue> Validate(ElectronicDataSheet eds)
    {
        if (eds == null) throw new ArgumentNullException(nameof(eds));

        var issues = new List<ValidationIssue>();
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
        if (dcf == null) throw new ArgumentNullException(nameof(dcf));

        var issues = new List<ValidationIssue>();
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

        if (!AllowedBaudrates.Contains(commissioning.Baudrate))
        {
            issues.Add(new ValidationIssue(
                "DeviceCommissioning.Baudrate",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Baudrate {0} is not supported. Allowed values: {1}.",
                    commissioning.Baudrate,
                    string.Join(", ", AllowedBaudrates))));
        }

        ValidateMaxLength(commissioning.NodeName, 246, "DeviceCommissioning.NodeName", issues);
        ValidateMaxLength(commissioning.NetworkName, 243, "DeviceCommissioning.NetworkName", issues);
        ValidateMaxLength(commissioning.NodeRefd, 249, "DeviceCommissioning.NodeRefd", issues);
        ValidateMaxLength(commissioning.NetRefd, 249, "DeviceCommissioning.NetRefd", issues);
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
        IDictionary<ushort, CanOpenObject> objects,
        ISet<ushort> classifiedIndices,
        List<ValidationIssue> issues)
    {
        foreach (var index in indexes)
        {
            var hexIndex = string.Format(CultureInfo.InvariantCulture, "0x{0:X4}", index);

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

    private static void ValidateMaxLength(
        string? value,
        int maxLength,
        string path,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrEmpty(value))
            return;

        if (value.Length > maxLength)
        {
            issues.Add(new ValidationIssue(
                path,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Length {0} exceeds max allowed length {1}.",
                    value.Length,
                    maxLength)));
        }
    }
}
