namespace EdsDcfNet.Validation;

using System.Globalization;
using EdsDcfNet.Models;

/// <summary>
/// Validates CANopen model instances against CiA DS 306 constraints.
/// </summary>
public static class CanOpenValidator
{
    private static readonly HashSet<ushort> ValidBaudRates =
        new() { 10, 20, 50, 125, 250, 500, 800, 1000 };

    private const int MaxParameterNameLength = 241;
    private const int MaxNodeNameLength = 246;
    private const int MaxNetworkNameLength = 243;

    /// <summary>
    /// Validates a <see cref="DeviceConfigurationFile"/> against CANopen constraints.
    /// </summary>
    /// <param name="dcf">The DCF to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> containing any errors found.</returns>
    public static ValidationResult Validate(DeviceConfigurationFile dcf)
    {
        var result = new ValidationResult();

        ValidateDeviceCommissioning(dcf.DeviceCommissioning, result);
        ValidateDeviceInfo(dcf.DeviceInfo, result);
        ValidateObjectDictionary(dcf.ObjectDictionary, result);

        return result;
    }

    /// <summary>
    /// Validates an <see cref="ElectronicDataSheet"/> against CANopen constraints.
    /// </summary>
    /// <param name="eds">The EDS to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> containing any errors found.</returns>
    public static ValidationResult Validate(ElectronicDataSheet eds)
    {
        var result = new ValidationResult();

        ValidateDeviceInfo(eds.DeviceInfo, result);
        ValidateObjectDictionary(eds.ObjectDictionary, result);

        return result;
    }

    private static void ValidateDeviceCommissioning(DeviceCommissioning dc, ValidationResult result)
    {
        const string ctx = "[DeviceCommissioning]";

        if (dc.NodeId < 1 || dc.NodeId > 127)
        {
            result.AddError(
                "NODE_ID_RANGE",
                string.Format(CultureInfo.InvariantCulture,
                    "NodeID {0} is out of range. Valid range is 1..127.", dc.NodeId),
                ctx);
        }

        if (!ValidBaudRates.Contains(dc.Baudrate) && dc.Baudrate != 0)
        {
            result.AddError(
                "BAUDRATE_INVALID",
                string.Format(CultureInfo.InvariantCulture,
                    "Baudrate {0} kbit/s is not a standard CANopen baud rate. Expected one of: 10, 20, 50, 125, 250, 500, 800, 1000.",
                    dc.Baudrate),
                ctx);
        }

        if (dc.NodeName.Length > MaxNodeNameLength)
        {
            result.AddError(
                "NODE_NAME_TOO_LONG",
                string.Format(CultureInfo.InvariantCulture,
                    "NodeName length {0} exceeds maximum of {1} characters.",
                    dc.NodeName.Length, MaxNodeNameLength),
                ctx);
        }

        if (dc.NetworkName.Length > MaxNetworkNameLength)
        {
            result.AddError(
                "NETWORK_NAME_TOO_LONG",
                string.Format(CultureInfo.InvariantCulture,
                    "NetworkName length {0} exceeds maximum of {1} characters.",
                    dc.NetworkName.Length, MaxNetworkNameLength),
                ctx);
        }
    }

    private static void ValidateDeviceInfo(DeviceInfo di, ValidationResult result)
    {
        const string ctx = "[DeviceInfo]";

        if (di.VendorName.Length > 244)
        {
            result.AddError(
                "VENDOR_NAME_TOO_LONG",
                string.Format(CultureInfo.InvariantCulture,
                    "VendorName length {0} exceeds maximum of 244 characters.", di.VendorName.Length),
                ctx);
        }

        if (di.ProductName.Length > 243)
        {
            result.AddError(
                "PRODUCT_NAME_TOO_LONG",
                string.Format(CultureInfo.InvariantCulture,
                    "ProductName length {0} exceeds maximum of 243 characters.", di.ProductName.Length),
                ctx);
        }

        if (di.OrderCode.Length > 245)
        {
            result.AddError(
                "ORDER_CODE_TOO_LONG",
                string.Format(CultureInfo.InvariantCulture,
                    "OrderCode length {0} exceeds maximum of 245 characters.", di.OrderCode.Length),
                ctx);
        }

        if (di.Granularity > 64)
        {
            result.AddError(
                "GRANULARITY_RANGE",
                string.Format(CultureInfo.InvariantCulture,
                    "Granularity {0} exceeds maximum of 64.", di.Granularity),
                ctx);
        }
    }

    private static void ValidateObjectDictionary(ObjectDictionary objDict, ValidationResult result)
    {
        foreach (var kvp in objDict.Objects)
        {
            ValidateObject(kvp.Value, result);
        }
    }

    private static void ValidateObject(CanOpenObject obj, ValidationResult result)
    {
        var ctx = string.Format(CultureInfo.InvariantCulture, "[{0:X4}]", obj.Index);

        if (obj.ParameterName.Length > MaxParameterNameLength)
        {
            result.AddError(
                "PARAMETER_NAME_TOO_LONG",
                string.Format(CultureInfo.InvariantCulture,
                    "ParameterName length {0} exceeds maximum of {1} characters.",
                    obj.ParameterName.Length, MaxParameterNameLength),
                ctx);
        }

        if (!IsValidObjectType(obj.ObjectType))
        {
            result.AddError(
                "OBJECT_TYPE_INVALID",
                string.Format(CultureInfo.InvariantCulture,
                    "ObjectType 0x{0:X2} is not a valid CiA 306 object code.",
                    obj.ObjectType),
                ctx);
        }

        if (obj.SubNumber.HasValue && obj.SubNumber.Value > 0 && obj.SubObjects.Count == 0)
        {
            result.AddError(
                "SUB_NUMBER_MISMATCH",
                string.Format(CultureInfo.InvariantCulture,
                    "SubNumber is {0} but no sub-objects are defined.",
                    obj.SubNumber.Value),
                ctx);
        }

        foreach (var subKvp in obj.SubObjects)
        {
            var subCtx = string.Format(CultureInfo.InvariantCulture,
                "[{0:X4}sub{1:X2}]", obj.Index, subKvp.Key);

            if (subKvp.Value.ParameterName.Length > MaxParameterNameLength)
            {
                result.AddError(
                    "PARAMETER_NAME_TOO_LONG",
                    string.Format(CultureInfo.InvariantCulture,
                        "ParameterName length {0} exceeds maximum of {1} characters.",
                        subKvp.Value.ParameterName.Length, MaxParameterNameLength),
                    subCtx);
            }
        }
    }

    private static bool IsValidObjectType(byte objectType)
    {
        return objectType switch
        {
            0x0 => true,  // NULL
            0x2 => true,  // DOMAIN
            0x5 => true,  // DEFTYPE
            0x6 => true,  // DEFSTRUCT
            0x7 => true,  // VAR
            0x8 => true,  // ARRAY
            0x9 => true,  // RECORD
            _ => false
        };
    }
}
