namespace EdsDcfNet.Parsers;

using System.Globalization;
using System.Xml.Linq;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;

/// <summary>
/// Resolves CiA 311 <c>uniqueIDRef</c> on CANopen objects against the application process.
/// Direct types, function-instance variable references, parameter/allowed-values templates,
/// defaults and unambiguous ranges are projected onto the CiA 306 object model.
/// </summary>
internal sealed class XddUniqueIdResolver
{

    private static readonly AsyncLocal<XddUniqueIdResolver?> WriteResolver = new AsyncLocal<XddUniqueIdResolver?>();

    private static readonly Dictionary<string, ushort> SimpleTypeMap =
        new Dictionary<string, ushort>(StringComparer.Ordinal)
        {
            // IEC 61131-3 g_simple (CiA 311 CommonElements.xsd) → CiA 301 data type index.
            // BYTE/WORD/DWORD/LWORD are fixed-width bit strings and map to the unsigned
            // integer of the same width. CHAR is STRING[1] (schema annotation) and maps
            // to VISIBLE_STRING. BITSTRING maps to OCTET_STRING: g_simple has no
            // OCTET_STRING or DOMAIN element, so those CANopen types cannot be told apart.
            { "BOOL", CanOpenDataType.Boolean },
            { "SINT", CanOpenDataType.Integer8 },
            { "INT", CanOpenDataType.Integer16 },
            { "DINT", CanOpenDataType.Integer32 },
            { "LINT", CanOpenDataType.Integer64 },
            { "USINT", CanOpenDataType.Unsigned8 },
            { "UINT", CanOpenDataType.Unsigned16 },
            { "UDINT", CanOpenDataType.Unsigned32 },
            { "ULINT", CanOpenDataType.Unsigned64 },
            { "REAL", CanOpenDataType.Real32 },
            { "LREAL", CanOpenDataType.Real64 },
            { "STRING", CanOpenDataType.VisibleString },
            { "WSTRING", CanOpenDataType.UnicodeString },
            { "CHAR", CanOpenDataType.VisibleString },
            { "WCHAR", CanOpenDataType.UnicodeString },
            { "BYTE", CanOpenDataType.Unsigned8 },
            { "WORD", CanOpenDataType.Unsigned16 },
            { "DWORD", CanOpenDataType.Unsigned32 },
            { "LWORD", CanOpenDataType.Unsigned64 },
            { "BITSTRING", CanOpenDataType.OctetString },
            { "TIME", CanOpenDataType.TimeDifference },
            { "TIME_OF_DAY", CanOpenDataType.TimeOfDay },
        };

    private readonly ApplicationProcess? _applicationProcess;
    private readonly Dictionary<string, ApParameter> _parameters;
    private readonly Dictionary<string, ApArrayType> _arrays;
    private readonly Dictionary<string, ApStructType> _structs;
    private readonly Dictionary<string, ApEnumType> _enums;
    private readonly Dictionary<string, ApDerivedType> _derived;
    private readonly Dictionary<string, List<ApFunctionType>> _functionTypes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<ApParameterTemplate>> _parameterTemplates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<ApAllowedValuesTemplate>> _allowedValuesTemplates = new(StringComparer.Ordinal);

    private XddUniqueIdResolver(ApplicationProcess? applicationProcess)
    {
        _applicationProcess = applicationProcess;
        _parameters = new Dictionary<string, ApParameter>(StringComparer.Ordinal);
        _arrays = new Dictionary<string, ApArrayType>(StringComparer.Ordinal);
        _structs = new Dictionary<string, ApStructType>(StringComparer.Ordinal);
        _enums = new Dictionary<string, ApEnumType>(StringComparer.Ordinal);
        _derived = new Dictionary<string, ApDerivedType>(StringComparer.Ordinal);

        if (applicationProcess == null)
            return;

        foreach (var parameter in applicationProcess.ParameterList)
        {
            if (parameter.UniqueId.Length > 0)
                _parameters[parameter.UniqueId] = parameter;
        }

        AddById(_functionTypes, applicationProcess.FunctionTypeList);
        AddById(_parameterTemplates, applicationProcess.TemplateList?.ParameterTemplates);
        AddById(_allowedValuesTemplates, applicationProcess.TemplateList?.AllowedValuesTemplates);

        var types = applicationProcess.DataTypeList;
        if (types == null)
            return;

        foreach (var array in types.Arrays)
        {
            if (array.UniqueId.Length > 0)
                _arrays[array.UniqueId] = array;
        }

        foreach (var structure in types.Structs)
        {
            if (structure.UniqueId.Length > 0)
                _structs[structure.UniqueId] = structure;
        }

        foreach (var enumeration in types.Enums)
        {
            if (enumeration.UniqueId.Length > 0)
                _enums[enumeration.UniqueId] = enumeration;
        }

        foreach (var derived in types.Derived)
        {
            if (derived.UniqueId.Length > 0)
                _derived[derived.UniqueId] = derived;
        }
    }

    internal static XddUniqueIdResolver Create(ApplicationProcess? applicationProcess) =>
        new XddUniqueIdResolver(applicationProcess);

    /// <summary>
    /// Makes <see cref="CurrentWriteProjection"/> see <paramref name="applicationProcess"/>
    /// until the returned scope is disposed. Nested writes restore the previous scope.
    /// </summary>
    internal static IDisposable EnterWriteScope(ApplicationProcess? applicationProcess)
    {
        var previous = WriteResolver.Value;
        WriteResolver.Value = Create(applicationProcess);
        return new WriteScope(previous);
    }

    /// <summary>
    /// Projects the parameter identified by <paramref name="uniqueId"/> during a write
    /// started with <see cref="EnterWriteScope"/>. Returns <see langword="null"/> when
    /// no write scope is active or the parameter is gone.
    /// </summary>
    internal static ParameterProjection? CurrentWriteProjection(string? uniqueId)
    {
        var resolver = WriteResolver.Value;
        if (resolver == null)
            return null;
        return resolver.TryProject(uniqueId);
    }

    internal void ApplyObject(CanOpenObject obj, ExplicitAttributes explicitAttributes)
    {
        if (string.IsNullOrEmpty(obj.UniqueIdRef))
            return;

        Apply(
            obj.UniqueIdRef!,
            FormatObjectPath(obj.Index),
            explicitAttributes,
            string.IsNullOrWhiteSpace(obj.ParameterName),
            value => obj.DataType = value,
            value => obj.AccessType = value,
            value => obj.DefaultValue = value,
            value => obj.LowLimit = value,
            value => obj.HighLimit = value,
            value => obj.ParameterName = value);
    }

    internal void ApplySubObject(ushort index, CanOpenSubObject sub, ExplicitAttributes explicitAttributes)
    {
        if (string.IsNullOrEmpty(sub.UniqueIdRef))
            return;

        Apply(
            sub.UniqueIdRef!,
            FormatSubObjectPath(index, sub.SubIndex),
            explicitAttributes,
            string.IsNullOrWhiteSpace(sub.ParameterName),
            value => sub.DataType = value,
            value => sub.AccessType = value,
            value => sub.DefaultValue = value,
            value => sub.LowLimit = value,
            value => sub.HighLimit = value,
            value => sub.ParameterName = value);
    }

    internal ParameterProjection? TryProject(string? uniqueId)
    {
        if (string.IsNullOrEmpty(uniqueId) || !_parameters.TryGetValue(uniqueId!, out var parameter))
            return null;
        return Project(parameter);
    }

    private void Apply(
        string uniqueIdRef,
        string path,
        ExplicitAttributes explicitAttributes,
        bool nameMissing,
        Action<ushort> setDataType,
        Action<AccessType> setAccess,
        Action<string?> setDefault,
        Action<string?> setLow,
        Action<string?> setHigh,
        Action<string> setName)
    {
        if (!_parameters.TryGetValue(uniqueIdRef, out var parameter))
        {
            var message = string.Format(
                CultureInfo.InvariantCulture,
                "uniqueIDRef '{0}' does not identify a parameter in the application process.",
                uniqueIdRef);
            Report(ParseDiagnosticCodes.XddUnresolvedUniqueIdRef, path, message, uniqueIdRef);
            ThrowIfStrict(ParseDiagnosticCodes.XddUnresolvedUniqueIdRef, message);
            return;
        }

        var projection = Project(parameter);

        if (!explicitAttributes.AccessType && projection.AccessKind == AccessProjectionKind.Mapped)
            setAccess(projection.MappedAccess);

        if (projection.AccessKind == AccessProjectionKind.NoAccess)
        {
            Report(
                ParseDiagnosticCodes.XddUniqueIdRefNoAccess,
                path,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Referenced parameter '{0}' has access 'noAccess', which has no CiA 306 access type. AccessType was left unchanged.",
                    parameter.UniqueId),
                "noAccess");
        }

        if (!explicitAttributes.DataType && projection.DataTypeKind == DataTypeProjectionKind.Resolved)
            setDataType(projection.DataType.GetValueOrDefault());

        if (!explicitAttributes.DefaultValue && projection.HasDefault)
            setDefault(projection.DefaultValue);

        if (projection.HasUnambiguousRange)
        {
            if (!explicitAttributes.LowLimit && projection.LowLimit != null)
                setLow(projection.LowLimit);
            if (!explicitAttributes.HighLimit && projection.HighLimit != null)
                setHigh(projection.HighLimit);
        }

        if (nameMissing && !string.IsNullOrEmpty(projection.Label))
            setName(projection.Label!);

        foreach (var error in projection.IndirectErrors)
        {
            var message = string.Format(
                CultureInfo.InvariantCulture,
                "Cannot resolve {0} on parameter '{1}': {2}",
                error.Kind,
                parameter.UniqueId,
                error.Detail);
            Report(ParseDiagnosticCodes.XddUnresolvedIndirectUniqueIdRef, path, message, error.Kind);
            ThrowIfStrict(ParseDiagnosticCodes.XddUnresolvedIndirectUniqueIdRef, message);
        }

        if (projection.DataTypeKind == DataTypeProjectionKind.Dangling)
        {
            var message = string.Format(
                CultureInfo.InvariantCulture,
                "dataTypeIDRef '{0}' on parameter '{1}' does not identify a data type in the application process.",
                projection.DataTypeDetail,
                parameter.UniqueId);
            Report(ParseDiagnosticCodes.XddUnresolvedDataTypeIdRef, path, message, projection.DataTypeDetail);
            ThrowIfStrict(ParseDiagnosticCodes.XddUnresolvedDataTypeIdRef, message);
        }
    }

    private ParameterProjection Project(ApParameter parameter)
    {
        var projection = new ParameterProjection();
        var variables = ResolveVariables(parameter);
        projection.IndirectErrors.AddRange(variables.Errors);
        DescribeAccess(parameter, projection);
        DescribeType(parameter, variables, projection);
        DescribeDefault(parameter, variables, projection);
        DescribeRange(parameter, variables, projection);
        projection.Label = parameter.LabelGroup.GetDisplayName();
        return projection;
    }

    private static void DescribeAccess(ApParameter parameter, ParameterProjection projection)
    {
        switch (parameter.Access)
        {
            case "const":
                projection.AccessKind = AccessProjectionKind.Mapped;
                projection.MappedAccess = AccessType.Constant;
                break;
            case "read":
                projection.AccessKind = AccessProjectionKind.Mapped;
                projection.MappedAccess = AccessType.ReadOnly;
                break;
            case "write":
                projection.AccessKind = AccessProjectionKind.Mapped;
                projection.MappedAccess = AccessType.WriteOnly;
                break;
            case "readWrite":
                projection.AccessKind = AccessProjectionKind.Mapped;
                projection.MappedAccess = AccessType.ReadWrite;
                break;
            case "readWriteInput":
                projection.AccessKind = AccessProjectionKind.Mapped;
                projection.MappedAccess = AccessType.ReadWriteInput;
                break;
            case "readWriteOutput":
                projection.AccessKind = AccessProjectionKind.Mapped;
                projection.MappedAccess = AccessType.ReadWriteOutput;
                break;
            case "noAccess":
                projection.AccessKind = AccessProjectionKind.NoAccess;
                break;
            default:
                projection.AccessKind = AccessProjectionKind.Unknown;
                break;
        }
    }

    private void DescribeType(
        ApParameter parameter,
        VariableProjection variables,
        ParameterProjection projection)
    {
        if (parameter.TypeRef != null)
        {
            DescribeTypeRef(parameter.TypeRef, projection);
            return;
        }

        if (parameter.VariableRefs.Count > 0 && variables.Errors.Count == 0)
        {
            var candidate = new ParameterProjection();
            DescribeTypeRef(variables.Variables[0].TypeRef, candidate);
            if (candidate.DataTypeKind != DataTypeProjectionKind.None)
            {
                projection.DataType = candidate.DataType;
                projection.DataTypeDetail = candidate.DataTypeDetail;
                projection.DataTypeKind = candidate.DataTypeKind;
            }

            if (candidate.DataTypeKind != DataTypeProjectionKind.Resolved
                && candidate.DataTypeKind != DataTypeProjectionKind.None)
            {
                AddIndirectError(projection, "variableRef", "an interface variable has no resolvable scalar data type.");
            }

            return;
        }

        var template = ResolveParameterTemplate(parameter.TemplateIdRef, projection);
        if (template?.TypeRef != null)
            DescribeTypeRef(template.TypeRef, projection);
    }

    /// <summary>
    /// Walks a direct type chain (array element or derived base) on the heap.
    /// A shallow document can still name an arbitrarily long acyclic chain, so this
    /// must not recurse. A repeated id is a cycle and is reported as dangling.
    /// </summary>
    private void DescribeTypeRef(ApTypeRef? typeRef, ParameterProjection projection)
    {
        if (typeRef == null)
        {
            projection.DataTypeKind = DataTypeProjectionKind.None;
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = typeRef;
        // Every path through the body returns or continues with a non-null type.
        while (true)
        {
            var simpleTypeName = current.SimpleTypeName;
            if (!string.IsNullOrEmpty(simpleTypeName))
            {
                if (SimpleTypeMap.TryGetValue(simpleTypeName!, out var code))
                {
                    projection.DataType = code;
                    projection.DataTypeKind = DataTypeProjectionKind.Resolved;
                    return;
                }

                projection.DataTypeDetail = simpleTypeName;
                projection.DataTypeKind = DataTypeProjectionKind.Unsupported;
                return;
            }

            var dataTypeIdRef = current.DataTypeIdRef;
            if (string.IsNullOrEmpty(dataTypeIdRef))
            {
                projection.DataTypeKind = DataTypeProjectionKind.None;
                return;
            }

            var id = dataTypeIdRef!;
            if (!seen.Add(id))
            {
                projection.DataTypeDetail = id;
                projection.DataTypeKind = DataTypeProjectionKind.Dangling;
                return;
            }

            if (_arrays.TryGetValue(id, out var array))
            {
                current = array.ElementType;
                if (current == null)
                {
                    projection.DataTypeKind = DataTypeProjectionKind.None;
                    return;
                }

                continue;
            }

            if (_structs.ContainsKey(id))
            {
                projection.DataTypeKind = DataTypeProjectionKind.NoScalar;
                return;
            }

            if (_enums.TryGetValue(id, out var enumeration))
            {
                var enumTypeName = enumeration.SimpleTypeName;
                if (string.IsNullOrEmpty(enumTypeName))
                {
                    projection.DataTypeDetail = id;
                    projection.DataTypeKind = DataTypeProjectionKind.Unsupported;
                    return;
                }

                if (SimpleTypeMap.TryGetValue(enumTypeName!, out var code))
                {
                    projection.DataType = code;
                    projection.DataTypeKind = DataTypeProjectionKind.Resolved;
                    return;
                }

                projection.DataTypeDetail = enumTypeName;
                projection.DataTypeKind = DataTypeProjectionKind.Unsupported;
                return;
            }

            if (_derived.TryGetValue(id, out var derived))
            {
                current = derived.BaseType;
                if (current == null)
                {
                    projection.DataTypeKind = DataTypeProjectionKind.None;
                    return;
                }

                continue;
            }

            projection.DataTypeDetail = id;
            projection.DataTypeKind = DataTypeProjectionKind.Dangling;
            return;
        }
    }

    private void DescribeDefault(
        ApParameter parameter,
        VariableProjection variables,
        ParameterProjection projection)
    {
        var variable = variables.Errors.Count == 0 && variables.Variables.Count == 1
            ? variables.Variables[0]
            : null;
        var value = variable?.DefaultValue?.Value
            ?? parameter.DefaultValue?.Value
            ?? ResolveParameterTemplate(parameter.TemplateIdRef, projection)?.DefaultValue?.Value
            ?? variable?.InitialValue
            ?? variable?.ComponentDefaultValue?.Value
            ?? variable?.ComponentInitialValue;
        if (value != null)
        {
            projection.HasDefault = true;
            projection.DefaultValue = value;
        }
    }

    private void DescribeRange(
        ApParameter parameter,
        VariableProjection variables,
        ParameterProjection projection)
    {
        var variable = variables.Errors.Count == 0 && variables.Variables.Count == 1
            ? variables.Variables[0]
            : null;
        var allowed = variable?.AllowedValues
            ?? parameter.AllowedValues
            ?? ResolveParameterTemplate(parameter.TemplateIdRef, projection)?.AllowedValues
            ?? variable?.ComponentAllowedValues;
        allowed = ResolveAllowedValues(allowed, projection);

        if (TryUnambiguousRange(allowed, out var low, out var high))
        {
            projection.HasUnambiguousRange = true;
            projection.LowLimit = low;
            projection.HighLimit = high;
        }
    }

    private static bool TryUnambiguousRange(ApAllowedValues? allowed, out string? low, out string? high)
    {
        low = null;
        high = null;
        if (allowed == null || allowed.Ranges.Count != 1 || allowed.Values.Count != 0)
            return false;

        var range = allowed.Ranges[0];
        if (range.MinValue == null && range.MaxValue == null)
            return false;

        low = range.MinValue?.Value;
        high = range.MaxValue?.Value;
        return true;
    }

    private VariableProjection ResolveVariables(ApParameter parameter)
    {
        var result = new VariableProjection();
        if (parameter.VariableRefs.Count > 1)
        {
            result.Errors.Add(new IndirectResolutionError(
                "variableRef",
                "packed variables have no scalar aggregate projection; component metadata was not projected."));
            return result;
        }

        foreach (var reference in parameter.VariableRefs)
        {
            if (TryResolveVariable(reference, out var variable, out var error))
                result.Variables.Add(variable!);
            else
                result.Errors.Add(new IndirectResolutionError("variableRef", error!));
        }

        return result;
    }

    private bool TryResolveVariable(
        ApVariableRef reference,
        out ResolvedVariable? result,
        out string? error)
    {
        result = null;
        error = null;
        // A parameter can only have been indexed when an application process exists.
        var instances = _applicationProcess!.FunctionInstanceList;
        ApFunctionType? functionType = null;
        foreach (var instanceId in reference.InstanceIdRefs)
        {
            var matches = instances?.FunctionInstances
                .Where(instance => instance.UniqueId == instanceId)
                .ToList() ?? new List<ApFunctionInstance>();
            if (matches.Count != 1)
            {
                error = string.Format(CultureInfo.InvariantCulture,
                    "instanceIDRef '{0}' identifies {1} instances.", instanceId, matches.Count);
                return false;
            }

            if (!TryUnique(_functionTypes, matches[0].TypeIdRef, out functionType, out error))
                return false;
            instances = functionType!.FunctionInstanceList;
        }

        if (functionType?.InterfaceList == null)
        {
            error = "the instance path does not identify a function type with an interface.";
            return false;
        }

        var variables = InterfaceVariables(functionType.InterfaceList)
            .Where(variable => variable.UniqueId == reference.VariableIdRef)
            .ToList();
        if (variables.Count != 1)
        {
            error = string.Format(CultureInfo.InvariantCulture,
                "variableIDRef '{0}' identifies {1} interface variables.", reference.VariableIdRef, variables.Count);
            return false;
        }

        var declaration = variables[0];
        result = new ResolvedVariable(
            declaration.Type,
            declaration.DefaultValue,
            declaration.AllowedValues,
            initialValue: declaration.InitialValue);
        var member = reference.MemberRef;
        if (member == null)
        {
            if (IsAnonymousArray(declaration)
                || TryResolveCompositeType(declaration.Type, _arrays, out _))
            {
                error = "a whole-array variable has no scalar projection without a memberRef.";
                result = null;
                return false;
            }

            return true;
        }

        if (member.Index.HasValue)
        {
            if (!TryResolveArrayElement(declaration, member.Index.Value, out var elementType))
            {
                error = "memberRef index does not identify an element of a one-dimensional array.";
                result = null;
                return false;
            }

            if (TryResolveCompositeType(elementType, _arrays, out _))
            {
                error = "memberRef index selects an array element, which has no scalar projection.";
                result = null;
                return false;
            }

            // A composite initializer belongs to the array as a whole, not to a scalar element.
            result = new ResolvedVariable(
                elementType,
                declaration.DefaultValue,
                declaration.AllowedValues);
            return true;
        }

        if (IsAnonymousArray(declaration))
        {
            error = "memberRef does not select an element of the enclosing array.";
            result = null;
            return false;
        }

        if (!TryResolveCompositeType(declaration.Type, _structs, out var structure))
        {
            error = "memberRef does not identify a structure component.";
            result = null;
            return false;
        }

        var members = structure!.VarDeclarations.Where(item => item.UniqueId == member.UniqueIdRef).ToList();
        if (members.Count != 1)
        {
            error = string.Format(CultureInfo.InvariantCulture,
                "memberRef '{0}' identifies {1} structure components.", member.UniqueIdRef, members.Count);
            result = null;
            return false;
        }

        if (IsAnonymousArray(members[0])
            || TryResolveCompositeType(members[0].Type, _arrays, out _))
        {
            error = "a whole-array structure component has no scalar projection without an element selection.";
            result = null;
            return false;
        }

        result = new ResolvedVariable(
            members[0].Type,
            declaration.DefaultValue,
            declaration.AllowedValues,
            members[0].DefaultValue,
            members[0].AllowedValues,
            componentInitialValue: members[0].InitialValue);
        return true;
    }

    private bool TryResolveArrayElement(
        ApVarDeclaration declaration,
        long index,
        out ApTypeRef? elementType)
    {
        if (IsAnonymousArray(declaration))
        {
            elementType = declaration.Type;
            return int.TryParse(declaration.Start ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)
                && int.TryParse(declaration.Size ?? "1", NumberStyles.Integer, CultureInfo.InvariantCulture, out var size)
                && size > 0
                && index >= start
                && (long)index < (long)start + size;
        }

        if (TryResolveCompositeType(declaration.Type, _arrays, out var array)
            && array!.Subranges.Count == 1
            && index >= array.Subranges[0].LowerLimit
            && index <= array.Subranges[0].UpperLimit)
        {
            elementType = array.ElementType;
            return true;
        }

        elementType = null;
        return false;
    }

    private bool TryResolveCompositeType<T>(
        ApTypeRef? typeRef,
        Dictionary<string, T> targets,
        out T? result)
        where T : class
    {
        result = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = typeRef;
        while (current?.DataTypeIdRef is { Length: > 0 } id)
        {
            if (!seen.Add(id))
                return false;

            if (targets.TryGetValue(id, out result))
                return true;

            if (!_derived.TryGetValue(id, out var derived))
                return false;
            current = derived.BaseType;
        }

        return false;
    }

    private static bool IsAnonymousArray(ApVarDeclaration declaration) =>
        !string.IsNullOrEmpty(declaration.Size)
        && declaration.Type?.SimpleTypeName is not ("STRING" or "WSTRING" or "BITSTRING");

    private static IEnumerable<ApVarDeclaration> InterfaceVariables(ApInterfaceList interfaces) =>
        interfaces.InputVars.Concat(interfaces.OutputVars).Concat(interfaces.ConfigVars);

    private ParameterTemplateProjection? ResolveParameterTemplate(
        string? templateIdRef,
        ParameterProjection projection)
    {
        if (string.IsNullOrEmpty(templateIdRef))
            return null;

        var result = new ParameterTemplateProjection();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = templateIdRef;
        while (!string.IsNullOrEmpty(current))
        {
            if (!seen.Add(current!))
            {
                AddIndirectError(projection, "templateIDRef", "the parameter-template chain contains a cycle.");
                return result;
            }

            if (!TryUnique(_parameterTemplates, current!, out var template, out var error))
            {
                AddIndirectError(projection, "templateIDRef", error!);
                return result;
            }

            result.TypeRef ??= template!.TypeRef;
            result.DefaultValue ??= template!.DefaultValue;
            result.AllowedValues ??= template!.AllowedValues;
            current = template!.TemplateIdRef;
        }

        return result;
    }

    private ApAllowedValues? ResolveAllowedValues(
        ApAllowedValues? allowed,
        ParameterProjection projection)
    {
        if (allowed == null || allowed.Values.Count > 0 || allowed.Ranges.Count > 0
            || string.IsNullOrEmpty(allowed.TemplateIdRef))
            return allowed;

        if (!TryUnique(_allowedValuesTemplates, allowed.TemplateIdRef!, out var template, out var error))
        {
            AddIndirectError(projection, "templateIDRef", error!);
            return null;
        }

        var resolved = new ApAllowedValues();
        resolved.Values.AddRange(template!.Values);
        resolved.Ranges.AddRange(template.Ranges);
        return resolved;
    }

    private static void AddIndirectError(ParameterProjection projection, string kind, string detail)
    {
        if (!projection.IndirectErrors.Any(error => error.Kind == kind && error.Detail == detail))
            projection.IndirectErrors.Add(new IndirectResolutionError(kind, detail));
    }

    private static void AddById(Dictionary<string, List<ApFunctionType>> target, IEnumerable<ApFunctionType> source)
        => AddByIdCore(target, source, item => item.UniqueId);

    private static void AddById(Dictionary<string, List<ApParameterTemplate>> target, IEnumerable<ApParameterTemplate>? source)
        => AddByIdCore(target, source, item => item.UniqueId);

    private static void AddById(Dictionary<string, List<ApAllowedValuesTemplate>> target, IEnumerable<ApAllowedValuesTemplate>? source)
        => AddByIdCore(target, source, item => item.UniqueId);

    private static void AddByIdCore<T>(Dictionary<string, List<T>> target, IEnumerable<T>? source, Func<T, string> id)
    {
        if (source == null)
            return;
        foreach (var item in source)
        {
            var key = id(item);
            if (key.Length == 0)
                continue;
            if (!target.TryGetValue(key, out var items))
            {
                items = new List<T>();
                target.Add(key, items);
            }

            items.Add(item);
        }
    }

    private static bool TryUnique<T>(
        Dictionary<string, List<T>> source,
        string id,
        out T? value,
        out string? error)
    {
        value = default;
        error = null;
        if (!source.TryGetValue(id, out var matches) || matches.Count != 1)
        {
            error = string.Format(CultureInfo.InvariantCulture,
                "uniqueIDRef '{0}' identifies {1} targets.", id, matches?.Count ?? 0);
            return false;
        }

        value = matches[0];
        return true;
    }

    private static void Report(string code, string path, string message, string? rawValue)
    {
        ParseDiagnosticScope.Report(new ParseDiagnostic(
            ParseSeverity.Warning,
            code,
            path,
            message,
            rawValue: rawValue));
    }

    private static void ThrowIfStrict(string code, string message)
    {
        if (!StrictParsingScope.IsEnabled)
            return;

        throw new EdsParseException(message) { Code = code };
    }

    private static string FormatObjectPath(ushort index) =>
        string.Format(CultureInfo.InvariantCulture, "CANopenObject[@index='{0:X4}']", index);

    private static string FormatSubObjectPath(ushort index, byte subIndex) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "CANopenObject[@index='{0:X4}']/CANopenSubObject[@subIndex='{1:X2}']",
            index,
            subIndex);

    private sealed class VariableProjection
    {
        internal List<ResolvedVariable> Variables { get; } = new();

        internal List<IndirectResolutionError> Errors { get; } = new();
    }

    private sealed class ResolvedVariable
    {
        internal ResolvedVariable(
            ApTypeRef? typeRef,
            ApParameterValue? defaultValue,
            ApAllowedValues? allowedValues,
            ApParameterValue? componentDefaultValue = null,
            ApAllowedValues? componentAllowedValues = null,
            string? initialValue = null,
            string? componentInitialValue = null)
        {
            TypeRef = typeRef;
            DefaultValue = defaultValue;
            AllowedValues = allowedValues;
            ComponentDefaultValue = componentDefaultValue;
            ComponentAllowedValues = componentAllowedValues;
            InitialValue = initialValue;
            ComponentInitialValue = componentInitialValue;
        }

        internal ApTypeRef? TypeRef { get; }

        internal ApParameterValue? DefaultValue { get; }

        internal ApAllowedValues? AllowedValues { get; }

        internal ApParameterValue? ComponentDefaultValue { get; }

        internal ApAllowedValues? ComponentAllowedValues { get; }

        internal string? InitialValue { get; }

        internal string? ComponentInitialValue { get; }
    }

    private sealed class ParameterTemplateProjection
    {
        internal ApTypeRef? TypeRef { get; set; }

        internal ApParameterValue? DefaultValue { get; set; }

        internal ApAllowedValues? AllowedValues { get; set; }
    }

    private sealed class WriteScope : IDisposable
    {
        private readonly XddUniqueIdResolver? _previous;
        private bool _disposed;

        internal WriteScope(XddUniqueIdResolver? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed)
                return;

            WriteResolver.Value = _previous;
            _disposed = true;
        }
    }
}

/// <summary>Which CANopen-object attributes were present in the source element.</summary>
internal readonly struct ExplicitAttributes
{
    internal bool DataType { get; }

    internal bool AccessType { get; }

    internal bool DefaultValue { get; }

    internal bool LowLimit { get; }

    internal bool HighLimit { get; }

    private ExplicitAttributes(bool dataType, bool accessType, bool defaultValue, bool lowLimit, bool highLimit)
    {
        DataType = dataType;
        AccessType = accessType;
        DefaultValue = defaultValue;
        LowLimit = lowLimit;
        HighLimit = highLimit;
    }

    internal static ExplicitAttributes From(XElement element) =>
        new ExplicitAttributes(
            element.Attribute("dataType") != null,
            element.Attribute("accessType") != null,
            element.Attribute("defaultValue") != null,
            element.Attribute("lowLimit") != null,
            element.Attribute("highLimit") != null);
}

internal enum DataTypeProjectionKind
{
    None,
    Resolved,
    NoScalar,
    Unsupported,
    Dangling
}

internal enum AccessProjectionKind
{
    Mapped,
    NoAccess,
    Unknown
}

internal sealed class IndirectResolutionError
{
    internal IndirectResolutionError(string kind, string detail)
    {
        Kind = kind;
        Detail = detail;
    }

    internal string Kind { get; }

    internal string Detail { get; }
}

internal sealed class ParameterProjection
{
    public ushort? DataType { get; set; }

    public DataTypeProjectionKind DataTypeKind { get; set; }

    public string? DataTypeDetail { get; set; }

    public AccessType MappedAccess { get; set; }

    public AccessProjectionKind AccessKind { get; set; }

    public string? DefaultValue { get; set; }

    public bool HasDefault { get; set; }

    public string? LowLimit { get; set; }

    public string? HighLimit { get; set; }

    public bool HasUnambiguousRange { get; set; }

    public string? Label { get; set; }

    public List<IndirectResolutionError> IndirectErrors { get; } = new();
}
