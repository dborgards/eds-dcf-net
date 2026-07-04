namespace EdsDcfNet.Validation;

using System.Collections.ObjectModel;
using System.Globalization;
using EdsDcfNet.Models;

/// <summary>
/// Validates CANopen models against common CiA 306 and CiA 311 constraints.
/// </summary>
public static class CanOpenModelValidator
{
    private static readonly ushort[] AllowedBaudrateValues = { 10, 20, 50, 125, 250, 500, 800, 1000 };

    private static readonly HashSet<ushort> AllowedBaudrates = new(AllowedBaudrateValues);

    private static readonly string AllowedBaudratesDescription = string.Join(
        ", ",
        AllowedBaudrateValues.Select(v => v.ToString(CultureInfo.InvariantCulture)));

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
        if (eds.ApplicationProcess != null)
            ValidateApplicationProcess(eds.ApplicationProcess, "ApplicationProcess", issues);

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
        if (dcf.ApplicationProcess != null)
            ValidateApplicationProcess(dcf.ApplicationProcess, "ApplicationProcess", issues);

        return new ReadOnlyCollection<ValidationIssue>(issues);
    }

    /// <summary>
    /// Validates a <see cref="NodelistProject"/> instance.
    /// </summary>
    /// <param name="cpj">Model instance to validate.</param>
    /// <returns>List of validation issues. Empty when model is valid.</returns>
    public static IReadOnlyList<ValidationIssue> Validate(NodelistProject cpj)
    {
        ThrowIfNull(cpj, nameof(cpj));

        var issues = new List<ValidationIssue>();
        for (var networkIndex = 0; networkIndex < cpj.Networks.Count; networkIndex++)
            ValidateNetworkTopology(cpj.Networks[networkIndex], "Networks[" + networkIndex.ToString(CultureInfo.InvariantCulture) + "]", issues);

        return new ReadOnlyCollection<ValidationIssue>(issues);
    }

    private static void ValidateDeviceCommissioning(
        DeviceCommissioning commissioning,
        List<ValidationIssue> issues)
    {
        var commissioningOmitted = DeviceCommissioningSemantics.IsOmitted(commissioning);
        if (commissioningOmitted
                ? commissioning.NodeId > CanOpenNodeId.MaxValue
                : !CanOpenNodeId.IsInRange(commissioning.NodeId))
        {
            issues.Add(new ValidationIssue(
                "DeviceCommissioning.NodeId",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Node-ID {0} is outside the CANopen range " + CanOpenNodeId.RangeDescription + " (or 0 when commissioning is omitted).",
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
                    AllowedBaudratesDescription)));
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

        foreach (var kvp in objectDictionary.Objects)
        {
            ValidateObject(kvp.Key, kvp.Value, issues);
        }

        foreach (var index in objectDictionary.Objects.Keys)
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

        foreach (var subObject in obj.SubObjects)
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
        return CanOpenObjectType.IsValid(objectType);
    }

    private static void ValidateNetworkTopology(
        NetworkTopology network,
        string path,
        List<ValidationIssue> issues)
    {
        ValidateMaxLength(network.NetName, MaxNetworkNameLength, path + ".NetName", issues);
        ValidateMaxLength(network.NetRefd, MaxReferenceNameLength, path + ".NetRefd", issues);

        foreach (var kvp in network.Nodes)
        {
            var nodePath = string.Format(
                CultureInfo.InvariantCulture,
                "{0}.Nodes[{1}]",
                path,
                kvp.Key);

            if (!CanOpenNodeId.IsInRange(kvp.Key))
            {
                issues.Add(new ValidationIssue(
                    nodePath,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Node dictionary key {0} is outside the CANopen range " + CanOpenNodeId.RangeDescription + ".",
                        kvp.Key)));
            }

            if (kvp.Key != kvp.Value.NodeId)
            {
                issues.Add(new ValidationIssue(
                    nodePath + ".NodeId",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "NodeId {0} does not match the dictionary key {1}.",
                        kvp.Value.NodeId,
                        kvp.Key)));
            }

            if (!CanOpenNodeId.IsInRange(kvp.Value.NodeId))
            {
                issues.Add(new ValidationIssue(
                    nodePath + ".NodeId",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Node-ID {0} is outside the CANopen range " + CanOpenNodeId.RangeDescription + ".",
                        kvp.Value.NodeId)));
            }

            ValidateMaxLength(kvp.Value.Name, MaxNodeNameLength, nodePath + ".Name", issues);
            ValidateMaxLength(kvp.Value.Refd, MaxReferenceNameLength, nodePath + ".Refd", issues);
        }
    }

    private static void ValidateApplicationProcess(
        ApplicationProcess applicationProcess,
        string path,
        List<ValidationIssue> issues)
    {
        var allUniqueIds = new HashSet<string>(StringComparer.Ordinal);
        var dataTypeIds = new HashSet<string>(StringComparer.Ordinal);
        var parameterTemplateIds = new HashSet<string>(StringComparer.Ordinal);
        var allowedValuesTemplateIds = new HashSet<string>(StringComparer.Ordinal);

        if (applicationProcess.DataTypeList != null)
        {
            ValidateDataTypeList(
                applicationProcess.DataTypeList,
                path + ".DataTypeList",
                allUniqueIds,
                dataTypeIds,
                issues);
        }

        if (applicationProcess.TemplateList != null)
        {
            ValidateTemplateList(
                applicationProcess.TemplateList,
                path + ".TemplateList",
                allUniqueIds,
                dataTypeIds,
                issues);

            foreach (var template in applicationProcess.TemplateList.ParameterTemplates)
            {
                if (!string.IsNullOrEmpty(template.UniqueId))
                    parameterTemplateIds.Add(template.UniqueId);
            }

            foreach (var template in applicationProcess.TemplateList.AllowedValuesTemplates)
            {
                if (!string.IsNullOrEmpty(template.UniqueId))
                    allowedValuesTemplateIds.Add(template.UniqueId);
            }
        }

        // Two passes over FunctionTypeList: function-instance lists may forward-reference
        // function-type IDs, so all IDs must be registered before instances are validated.
        var functionTypeIds = RegisterFunctionTypeIds(applicationProcess, path, allUniqueIds, dataTypeIds, issues);
        ValidateFunctionTypeInstances(applicationProcess, path, functionTypeIds, allUniqueIds, issues);

        var parameterIds = ValidateParameters(
            applicationProcess,
            path,
            allUniqueIds,
            dataTypeIds,
            parameterTemplateIds,
            allowedValuesTemplateIds,
            issues);

        ValidateParameterGroups(applicationProcess, path, allUniqueIds, parameterIds, issues);

        if (applicationProcess.FunctionInstanceList != null)
        {
            ValidateFunctionInstanceList(
                applicationProcess.FunctionInstanceList,
                path + ".FunctionInstanceList",
                functionTypeIds,
                allUniqueIds,
                issues);
        }
    }

    /// <summary>
    /// First pass over <see cref="ApplicationProcess.FunctionTypeList"/>: registers all
    /// function-type unique IDs (returned for the second pass and top-level instance
    /// validation) and validates version infos and interface lists.
    /// </summary>
    private static HashSet<string> RegisterFunctionTypeIds(
        ApplicationProcess applicationProcess,
        string path,
        HashSet<string> allUniqueIds,
        HashSet<string> dataTypeIds,
        List<ValidationIssue> issues)
    {
        var functionTypeIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < applicationProcess.FunctionTypeList.Count; index++)
        {
            var functionType = applicationProcess.FunctionTypeList[index];
            var functionTypePath = IndexedPath(path + ".FunctionTypeList", index);
            RegisterUniqueId(functionType.UniqueId, functionTypePath + ".UniqueId", allUniqueIds, issues);
            if (!string.IsNullOrEmpty(functionType.UniqueId))
                functionTypeIds.Add(functionType.UniqueId);

            if (functionType.VersionInfos.Count == 0)
            {
                issues.Add(new ValidationIssue(
                    functionTypePath + ".VersionInfos",
                    "At least one versionInfo entry is required."));
            }

            if (functionType.InterfaceList != null)
            {
                ValidateInterfaceList(
                    functionType.InterfaceList,
                    functionTypePath + ".InterfaceList",
                    allUniqueIds,
                    dataTypeIds,
                    issues);
            }
        }

        return functionTypeIds;
    }

    /// <summary>
    /// Second pass over <see cref="ApplicationProcess.FunctionTypeList"/>: validates each
    /// function type's instance list once <paramref name="functionTypeIds"/> contains all
    /// registered IDs (instances may forward-reference later function types).
    /// </summary>
    private static void ValidateFunctionTypeInstances(
        ApplicationProcess applicationProcess,
        string path,
        HashSet<string> functionTypeIds,
        HashSet<string> allUniqueIds,
        List<ValidationIssue> issues)
    {
        for (var index = 0; index < applicationProcess.FunctionTypeList.Count; index++)
        {
            var functionType = applicationProcess.FunctionTypeList[index];
            if (functionType.FunctionInstanceList != null)
            {
                var functionTypePath = IndexedPath(path + ".FunctionTypeList", index);
                ValidateFunctionInstanceList(
                    functionType.FunctionInstanceList,
                    functionTypePath + ".FunctionInstanceList",
                    functionTypeIds,
                    allUniqueIds,
                    issues);
            }
        }
    }

    /// <summary>
    /// Validates <see cref="ApplicationProcess.ParameterList"/> (unique IDs, typeRef,
    /// templateIdRef, allowedValues refs) and returns the registered parameter IDs
    /// for parameter-group validation.
    /// </summary>
    private static HashSet<string> ValidateParameters(
        ApplicationProcess applicationProcess,
        string path,
        HashSet<string> allUniqueIds,
        HashSet<string> dataTypeIds,
        HashSet<string> parameterTemplateIds,
        HashSet<string> allowedValuesTemplateIds,
        List<ValidationIssue> issues)
    {
        var parameterIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < applicationProcess.ParameterList.Count; index++)
        {
            var parameter = applicationProcess.ParameterList[index];
            var parameterPath = IndexedPath(path + ".ParameterList", index);
            RegisterUniqueId(
                parameter.UniqueId,
                parameterPath + ".UniqueId",
                allUniqueIds,
                issues);
            if (!string.IsNullOrEmpty(parameter.UniqueId))
                parameterIds.Add(parameter.UniqueId);

            ValidateDataTypeRef(parameter.TypeRef, parameterPath + ".TypeRef", dataTypeIds, issues);
            ValidateParameterTemplateIdRef(
                parameter.TemplateIdRef,
                parameterPath + ".TemplateIdRef",
                parameterTemplateIds,
                issues);
            if (parameter.AllowedValues != null)
            {
                ValidateAllowedValuesTemplateIdRef(
                    parameter.AllowedValues.TemplateIdRef,
                    parameterPath + ".AllowedValues.TemplateIdRef",
                    allowedValuesTemplateIds,
                    issues);
            }
        }

        return parameterIds;
    }

    /// <summary>
    /// Validates <see cref="ApplicationProcess.ParameterGroupList"/> against the
    /// registered <paramref name="parameterIds"/>.
    /// </summary>
    private static void ValidateParameterGroups(
        ApplicationProcess applicationProcess,
        string path,
        HashSet<string> allUniqueIds,
        HashSet<string> parameterIds,
        List<ValidationIssue> issues)
    {
        for (var index = 0; index < applicationProcess.ParameterGroupList.Count; index++)
        {
            ValidateParameterGroup(
                applicationProcess.ParameterGroupList[index],
                IndexedPath(path + ".ParameterGroupList", index),
                allUniqueIds,
                parameterIds,
                issues);
        }
    }

    private static void ValidateDataTypeList(
        ApDataTypeList dataTypeList,
        string path,
        HashSet<string> allUniqueIds,
        HashSet<string> dataTypeIds,
        List<ValidationIssue> issues)
    {
        for (var index = 0; index < dataTypeList.Arrays.Count; index++)
        {
            var arrayType = dataTypeList.Arrays[index];
            var arrayPath = IndexedPath(path + ".Arrays", index);
            RegisterUniqueId(arrayType.UniqueId, arrayPath + ".UniqueId", allUniqueIds, issues);
            if (!string.IsNullOrEmpty(arrayType.UniqueId))
                dataTypeIds.Add(arrayType.UniqueId);
        }

        for (var index = 0; index < dataTypeList.Structs.Count; index++)
        {
            var structType = dataTypeList.Structs[index];
            var structPath = IndexedPath(path + ".Structs", index);
            RegisterUniqueId(structType.UniqueId, structPath + ".UniqueId", allUniqueIds, issues);
            if (!string.IsNullOrEmpty(structType.UniqueId))
                dataTypeIds.Add(structType.UniqueId);
        }

        for (var index = 0; index < dataTypeList.Enums.Count; index++)
        {
            var enumType = dataTypeList.Enums[index];
            var enumPath = IndexedPath(path + ".Enums", index);
            RegisterUniqueId(enumType.UniqueId, enumPath + ".UniqueId", allUniqueIds, issues);
            if (!string.IsNullOrEmpty(enumType.UniqueId))
                dataTypeIds.Add(enumType.UniqueId);
        }

        for (var index = 0; index < dataTypeList.Derived.Count; index++)
        {
            var derivedType = dataTypeList.Derived[index];
            var derivedPath = IndexedPath(path + ".Derived", index);
            RegisterUniqueId(derivedType.UniqueId, derivedPath + ".UniqueId", allUniqueIds, issues);
            if (!string.IsNullOrEmpty(derivedType.UniqueId))
                dataTypeIds.Add(derivedType.UniqueId);

            if (derivedType.Count != null)
            {
                RegisterUniqueId(
                    derivedType.Count.UniqueId,
                    derivedPath + ".Count.UniqueId",
                    allUniqueIds,
                    issues);
            }
        }

        for (var index = 0; index < dataTypeList.Arrays.Count; index++)
        {
            var arrayType = dataTypeList.Arrays[index];
            var arrayPath = IndexedPath(path + ".Arrays", index);
            ValidateDataTypeRef(arrayType.ElementType, arrayPath + ".ElementType", dataTypeIds, issues);
        }

        for (var index = 0; index < dataTypeList.Structs.Count; index++)
        {
            ValidateVarDeclarations(
                dataTypeList.Structs[index].VarDeclarations,
                IndexedPath(path + ".Structs", index) + ".VarDeclarations",
                allUniqueIds,
                dataTypeIds,
                issues);
        }

        for (var index = 0; index < dataTypeList.Derived.Count; index++)
        {
            var derivedPath = IndexedPath(path + ".Derived", index);
            ValidateDataTypeRef(
                dataTypeList.Derived[index].BaseType,
                derivedPath + ".BaseType",
                dataTypeIds,
                issues);
        }
    }

    private static void ValidateTemplateList(
        ApTemplateList templateList,
        string path,
        HashSet<string> allUniqueIds,
        HashSet<string> dataTypeIds,
        List<ValidationIssue> issues)
    {
        for (var index = 0; index < templateList.ParameterTemplates.Count; index++)
        {
            var template = templateList.ParameterTemplates[index];
            var templatePath = IndexedPath(path + ".ParameterTemplates", index);
            RegisterUniqueId(template.UniqueId, templatePath + ".UniqueId", allUniqueIds, issues);
            ValidateDataTypeRef(template.TypeRef, templatePath + ".TypeRef", dataTypeIds, issues);
        }

        for (var index = 0; index < templateList.AllowedValuesTemplates.Count; index++)
        {
            var template = templateList.AllowedValuesTemplates[index];
            var templatePath = IndexedPath(path + ".AllowedValuesTemplates", index);
            RegisterUniqueId(template.UniqueId, templatePath + ".UniqueId", allUniqueIds, issues);
        }
    }

    private static void ValidateInterfaceList(
        ApInterfaceList interfaceList,
        string path,
        HashSet<string> allUniqueIds,
        HashSet<string> dataTypeIds,
        List<ValidationIssue> issues)
    {
        ValidateVarDeclarations(interfaceList.InputVars, path + ".InputVars", allUniqueIds, dataTypeIds, issues);
        ValidateVarDeclarations(interfaceList.OutputVars, path + ".OutputVars", allUniqueIds, dataTypeIds, issues);
        ValidateVarDeclarations(interfaceList.ConfigVars, path + ".ConfigVars", allUniqueIds, dataTypeIds, issues);
    }

    private static void ValidateVarDeclarations(
        List<ApVarDeclaration> varDeclarations,
        string path,
        HashSet<string> allUniqueIds,
        HashSet<string> dataTypeIds,
        List<ValidationIssue> issues)
    {
        for (var index = 0; index < varDeclarations.Count; index++)
        {
            var varDeclaration = varDeclarations[index];
            var varPath = IndexedPath(path, index);
            RegisterUniqueId(varDeclaration.UniqueId, varPath + ".UniqueId", allUniqueIds, issues);
            ValidateDataTypeRef(varDeclaration.Type, varPath + ".Type", dataTypeIds, issues);
        }
    }

    private static void ValidateDataTypeRef(
        ApTypeRef? typeRef,
        string path,
        HashSet<string> dataTypeIds,
        List<ValidationIssue> issues)
    {
        if (typeRef?.DataTypeIdRef is not { Length: > 0 } dataTypeIdRef)
            return;

        if (!dataTypeIds.Contains(dataTypeIdRef))
        {
            issues.Add(new ValidationIssue(
                path,
                "Data type reference '" + dataTypeIdRef + "' does not match any dataTypeList uniqueID."));
        }
    }

    private static void ValidateParameterTemplateIdRef(
        string? templateIdRef,
        string path,
        HashSet<string> parameterTemplateIds,
        List<ValidationIssue> issues)
    {
        if (templateIdRef is not { Length: > 0 })
            return;

        if (!parameterTemplateIds.Contains(templateIdRef))
        {
            issues.Add(new ValidationIssue(
                path,
                "Parameter template reference '" + templateIdRef + "' does not match any parameterTemplate uniqueID."));
        }
    }

    private static void ValidateAllowedValuesTemplateIdRef(
        string? templateIdRef,
        string path,
        HashSet<string> allowedValuesTemplateIds,
        List<ValidationIssue> issues)
    {
        if (templateIdRef is not { Length: > 0 })
            return;

        if (!allowedValuesTemplateIds.Contains(templateIdRef))
        {
            issues.Add(new ValidationIssue(
                path,
                "Allowed values template reference '" + templateIdRef + "' does not match any allowedValuesTemplate uniqueID."));
        }
    }

    private static string IndexedPath(string path, int index)
        => path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";

    private static void ValidateParameterGroup(
        ApParameterGroup group,
        string path,
        HashSet<string> allUniqueIds,
        HashSet<string> parameterIds,
        List<ValidationIssue> issues)
    {
        RegisterUniqueId(group.UniqueId, path + ".UniqueId", allUniqueIds, issues);

        foreach (var parameterRef in group.ParameterRefs)
        {
            if (string.IsNullOrEmpty(parameterRef))
            {
                issues.Add(new ValidationIssue(path + ".ParameterRefs", "Parameter reference must not be empty."));
                continue;
            }

            if (!parameterIds.Contains(parameterRef))
            {
                issues.Add(new ValidationIssue(
                    path + ".ParameterRefs",
                    "Parameter reference '" + parameterRef + "' does not match any parameter uniqueID."));
            }
        }

        for (var index = 0; index < group.SubGroups.Count; index++)
        {
            ValidateParameterGroup(
                group.SubGroups[index],
                IndexedPath(path + ".SubGroups", index),
                allUniqueIds,
                parameterIds,
                issues);
        }
    }

    private static void ValidateFunctionInstanceList(
        ApFunctionInstanceList instanceList,
        string path,
        HashSet<string> functionTypeIds,
        HashSet<string> allUniqueIds,
        List<ValidationIssue> issues)
    {
        for (var index = 0; index < instanceList.FunctionInstances.Count; index++)
        {
            var instance = instanceList.FunctionInstances[index];
            var instancePath = IndexedPath(path + ".FunctionInstances", index);
            RegisterUniqueId(instance.UniqueId, instancePath + ".UniqueId", allUniqueIds, issues);

            if (string.IsNullOrEmpty(instance.TypeIdRef))
            {
                issues.Add(new ValidationIssue(instancePath + ".TypeIdRef", "Function instance type reference must not be empty."));
            }
            else if (!functionTypeIds.Contains(instance.TypeIdRef))
            {
                issues.Add(new ValidationIssue(
                    instancePath + ".TypeIdRef",
                    "Function instance references unknown function type '" + instance.TypeIdRef + "'."));
            }
        }
    }

    private static void RegisterUniqueId(
        string uniqueId,
        string path,
        HashSet<string> seenIds,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrEmpty(uniqueId))
        {
            issues.Add(new ValidationIssue(path, "Unique ID must not be empty."));
            return;
        }

        if (!seenIds.Add(uniqueId))
            issues.Add(new ValidationIssue(path, "Duplicate unique ID '" + uniqueId + "'."));
    }

    private static void ThrowIfNull(object? value, string paramName)
    {
        if (value is null)
            throw new ArgumentNullException(paramName);
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
