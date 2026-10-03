namespace EdsDcfNet.Parsers;

using System.Globalization;
using System.Xml.Linq;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;

/// <summary>
/// Resolves CiA 311 <c>uniqueIDRef</c> on CANopen objects against
/// <c>ApplicationProcess/parameterList/parameter</c>. Direct forms only:
/// a simple type or <c>dataTypeIDRef</c>, <c>access</c>, <c>defaultValue</c>,
/// and one unambiguous <c>allowedValues</c> range. <c>variableRef</c> and
/// <c>templateIDRef</c> are reported and not followed.
/// </summary>
internal sealed class XddUniqueIdResolver
{
    private const string IndirectMessage = "Resolution via variableRef/templateIDRef is not supported.";

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

    private readonly Dictionary<string, ApParameter> _parameters;
    private readonly Dictionary<string, ApArrayType> _arrays;
    private readonly Dictionary<string, ApStructType> _structs;
    private readonly Dictionary<string, ApEnumType> _enums;
    private readonly Dictionary<string, ApDerivedType> _derived;

    private XddUniqueIdResolver(ApplicationProcess? applicationProcess)
    {
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

        if (projection.DataTypeKind == DataTypeProjectionKind.Indirect)
        {
            Report(ParseDiagnosticCodes.XddUniqueIdRefIndirect, path, IndirectMessage, "variableRef");
        }

        if (projection.DefaultFromTemplateOnly || projection.RangeFromTemplateOnly)
        {
            Report(ParseDiagnosticCodes.XddUniqueIdRefIndirect, path, IndirectMessage, "templateIDRef");
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
        DescribeAccess(parameter, projection);
        DescribeType(parameter, projection);
        DescribeDefault(parameter, projection);
        DescribeRange(parameter, projection);
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

    /// <summary>
    /// Walks a direct type chain (array element or derived base) on the heap.
    /// A shallow document can still name an arbitrarily long acyclic chain, so this
    /// must not recurse. A repeated id is a cycle and is reported as dangling.
    /// </summary>
    private void DescribeType(ApParameter parameter, ParameterProjection projection)
    {
        if (parameter.TypeRef == null)
        {
            projection.DataTypeKind = parameter.VariableRefs.Count > 0
                ? DataTypeProjectionKind.Indirect
                : DataTypeProjectionKind.None;
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = parameter.TypeRef;
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

    private static void DescribeDefault(ApParameter parameter, ParameterProjection projection)
    {
        if (parameter.DefaultValue != null)
        {
            projection.HasDefault = true;
            projection.DefaultValue = parameter.DefaultValue.Value;
            return;
        }

        if (!string.IsNullOrEmpty(parameter.TemplateIdRef))
            projection.DefaultFromTemplateOnly = true;
    }

    private static void DescribeRange(ApParameter parameter, ParameterProjection projection)
    {
        if (TryUnambiguousRange(parameter.AllowedValues, out var low, out var high))
        {
            projection.HasUnambiguousRange = true;
            projection.LowLimit = low;
            projection.HighLimit = high;
            return;
        }

        if (RangeOnlyViaTemplate(parameter))
            projection.RangeFromTemplateOnly = true;
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

    private static bool RangeOnlyViaTemplate(ApParameter parameter)
    {
        var allowed = parameter.AllowedValues;
        var hasLocalContent = allowed != null && (allowed.Values.Count > 0 || allowed.Ranges.Count > 0);
        if (hasLocalContent)
            return false;

        if (!string.IsNullOrEmpty(parameter.TemplateIdRef))
            return true;

        return allowed != null && !string.IsNullOrEmpty(allowed.TemplateIdRef);
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
    Dangling,
    Indirect
}

internal enum AccessProjectionKind
{
    Mapped,
    NoAccess,
    Unknown
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

    public bool DefaultFromTemplateOnly { get; set; }

    public string? LowLimit { get; set; }

    public string? HighLimit { get; set; }

    public bool HasUnambiguousRange { get; set; }

    public bool RangeFromTemplateOnly { get; set; }

    public string? Label { get; set; }
}
