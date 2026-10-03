namespace EdsDcfNet.Models;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Represents the object dictionary of a CANopen device.
/// Contains mandatory, optional, and manufacturer-specific objects.
/// </summary>
[SuppressMessage("Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Justification = "ObjectDictionary is a CiA CANopen domain term, not a Dictionary<K,V>.")]
public class ObjectDictionary
{
    /// <summary>
    /// Mandatory objects (1000h, 1001h and 1018h; the XDD/XDC reader lists these three).
    /// </summary>
    public List<ushort> MandatoryObjects { get; } = new();

    /// <summary>
    /// Optional objects (area 1000h-1FFFh and 6000h-FFFFh).
    /// </summary>
    public List<ushort> OptionalObjects { get; } = new();

    /// <summary>
    /// Manufacturer specific objects (area 2000h-5FFFh).
    /// </summary>
    public List<ushort> ManufacturerObjects { get; } = new();

    /// <summary>
    /// All objects indexed by their index.
    /// </summary>
    public Dictionary<ushort, CanOpenObject> Objects { get; } = new();

    /// <summary>
    /// Dummy usage for mapping (data type index -> supported).
    /// </summary>
    public Dictionary<ushort, bool> DummyUsage { get; } = new();
}

/// <summary>
/// Represents a CANopen object in the object dictionary.
/// </summary>
public class CanOpenObject
{
    /// <summary>
    /// Object index in hexadecimal.
    /// </summary>
    public ushort Index { get; set; }

    /// <summary>
    /// Parameter name (up to 241 characters).
    /// </summary>
    public string ParameterName { get; set; } = string.Empty;

    /// <summary>
    /// Object code per CiA DS 306:
    /// 0x0 = NULL, 0x2 = DOMAIN, 0x5 = DEFTYPE, 0x6 = DEFSTRUCT,
    /// 0x7 = VAR, 0x8 = ARRAY, 0x9 = RECORD.
    /// </summary>
    public byte ObjectType { get; set; } = 0x7; // Default: VAR

    /// <summary>
    /// Index of the data type in the object dictionary.
    /// </summary>
    public ushort? DataType { get; set; }

    /// <summary>
    /// Access type (ro, wo, rw, rwr, rww, const).
    /// </summary>
    /// <remarks>
    /// Assigning this property marks the value as explicit for the XDD/XDC writer.
    /// The untouched fallback (<see cref="AccessType.ReadOnly"/>, left in place when a
    /// referenced parameter has <c>noAccess</c> and the source had no <c>accessType</c>)
    /// is not explicit, so the writer does not invent <c>accessType="ro"</c>.
    /// </remarks>
    public AccessType AccessType
    {
        get => _accessType;
        set
        {
            _accessType = value;
            _accessTypeSpecified = true;
        }
    }

    private AccessType _accessType;

    private bool _accessTypeSpecified;

    /// <summary>
    /// <see langword="true"/> when <see cref="AccessType"/> was set from an XDD attribute,
    /// from a resolved parameter access, or by assigning <see cref="AccessType"/>.
    /// </summary>
    internal bool AccessTypeSpecified => _accessTypeSpecified;

    /// <summary>
    /// Copies <see cref="AccessType"/> without treating a profile-file keyword as an
    /// XDD explicit attribute.
    /// </summary>
    internal void SetAccessTypeFromProfile(AccessType value) => _accessType = value;

    /// <summary>
    /// Restores both the access value and whether it is explicit. Used by <c>ModelCloner</c>.
    /// </summary>
    internal void CopyAccessTypeStateFrom(CanOpenObject source)
    {
        _accessType = source._accessType;
        _accessTypeSpecified = source._accessTypeSpecified;
    }

    /// <summary>
    /// Default value for this object.
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Lowest limit of the object value (only if applicable).
    /// </summary>
    public string? LowLimit { get; set; }

    /// <summary>
    /// Upper limit of the object value (only if applicable).
    /// </summary>
    public string? HighLimit { get; set; }

    /// <summary>
    /// CiA 311 <c>uniqueIDRef</c> of the application-process <c>parameter</c> this object
    /// refers to, or <see langword="null"/> when the object does not carry the attribute.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CiA 311 v1.1.0 § 6.5.2.3.2.1 Table 48 (schema
    /// <c>CANopenObject</c> attribute order; Table 49 is the same for
    /// <c>CANopenSubObject</c>, with <c>subIndex</c> in place of <c>index</c>) lists
    /// <c>dataType</c>, <c>lowLimit</c>, <c>highLimit</c>, <c>accessType</c>, and
    /// <c>defaultValue</c> before <c>uniqueIDRef</c>. The schema annotation says that when
    /// <c>uniqueIDRef</c> is present those five attributes shall be defined by the referenced
    /// application-process element.
    /// </para>
    /// <para>
    /// An explicit attribute on the object wins: the reader keeps it and does not overwrite
    /// it from the reference. Attributes that are absent are filled from the referenced
    /// <c>parameter</c> (a direct simple type or <c>dataTypeIDRef</c>, <c>access</c>,
    /// <c>defaultValue</c>, one unambiguous <c>allowedValues</c> range, and <c>label</c> for
    /// <see cref="ParameterName"/> only when <c>name</c> is missing). <c>noAccess</c> is not
    /// copied onto <see cref="AccessType"/>. <c>variableRef</c> and <c>templateIDRef</c> are
    /// not followed.
    /// </para>
    /// <para>
    /// The XDD/XDC writer emits <c>uniqueIDRef</c> again only while a parameter with this id
    /// still exists on <see cref="ElectronicDataSheet.ApplicationProcess"/>. While the
    /// reference is emitted, <c>dataType</c>, <c>lowLimit</c>, <c>highLimit</c>,
    /// <c>accessType</c>, and <c>defaultValue</c> are omitted when they still match that
    /// parameter, so the next read derives them again. A value changed after reading is
    /// written as an explicit attribute and wins on the next read.
    /// <c>noAccess</c> does not match a CiA 306 access type: the writer leaves
    /// <c>accessType</c> off while <see cref="AccessType"/> is still the untouched fallback.
    /// A mapped reference does the same when access was never supplied on the object, so
    /// the next read takes the parameter access instead of an invented <c>accessType="ro"</c>.
    /// The writer emits <c>accessType</c> when the source attribute, a resolved parameter
    /// access, or a later assignment supplied one. An empty <c>defaultValue</c>,
    /// <c>lowLimit</c>, or <c>highLimit</c> is written when it overrides a different value
    /// from the emitted reference. When the parameter is removed, a resolved
    /// <see cref="AccessType"/> is still written even if the object has no scalar
    /// <see cref="DataType"/> (a struct-backed RECORD).
    /// </para>
    /// </remarks>
    public string? UniqueIdRef { get; set; }

    /// <summary>
    /// CiA 311 PDO mapping mode. Prefer this for XDD/XDC round-trips.
    /// </summary>
    public PdoMappingMode PdoMappingMode { get; set; }

    /// <summary>
    /// Whether the object can be mapped into a PDO (EDS/DCF boolean view).
    /// <see langword="true"/> maps to <see cref="PdoMappingMode.Optional"/> when the mode was
    /// <see cref="PdoMappingMode.No"/>; setting <see langword="false"/> clears the mode to
    /// <see cref="PdoMappingMode.No"/>. Fine-grained XDD modes (<c>default</c>/<c>TPDO</c>/<c>RPDO</c>)
    /// are preserved when only this boolean is left <see langword="true"/>.
    /// </summary>
    public bool PdoMapping
    {
        get => PdoMappingMode != PdoMappingMode.No;
        set
        {
            if (value)
            {
                if (PdoMappingMode == PdoMappingMode.No)
                    PdoMappingMode = PdoMappingMode.Optional;
            }
            else
            {
                PdoMappingMode = PdoMappingMode.No;
            }
        }
    }

    /// <summary>
    /// Special behavior flags (Unsigned32).
    /// Bit 0: Refuse write on download
    /// Bit 1: Refuse read on scan
    /// </summary>
    public uint ObjFlags { get; set; }

    /// <summary>
    /// Original XDD/XDC <c>objFlags</c> text when a schema-valid <c>xsd:hexBinary</c>
    /// value does not fit in <see cref="ObjFlags"/>. The writer emits this text only
    /// while <see cref="ObjFlags"/> still equals <see cref="ObjFlagsLexicalBaseline"/>.
    /// </summary>
    internal string? ObjFlagsLexical { get; set; }

    /// <summary>
    /// <see cref="ObjFlags"/> captured when <see cref="ObjFlagsLexical"/> was stored.
    /// </summary>
    internal uint ObjFlagsLexicalBaseline { get; set; }

    /// <summary>
    /// Number of sub-indexes available at this index (Unsigned8).
    /// Not counting sub-index FFh.
    /// </summary>
    public byte? SubNumber { get; set; }

    /// <summary>
    /// Sub-objects if this is a DEFSTRUCT, ARRAY, or RECORD.
    /// </summary>
    public Dictionary<byte, CanOpenSubObject> SubObjects { get; } = new();

    /// <summary>
    /// For compact sub-object storage (CiA 306 §4.5.2.4.2): highest sub-index described
    /// by the parent object template. When non-zero, missing <c>[xxxsubN]</c> sections are
    /// synthesized from this object; ParameterValues/Denotations use <c>[xxxxValue]</c> /
    /// <c>[xxxxDenotation]</c> (DCF) and optional names use <c>[xxxxName]</c>.
    /// </summary>
    public byte? CompactSubObj { get; set; }

    /// <summary>
    /// Object links (related objects grouped together).
    /// </summary>
    public List<ushort> ObjectLinks { get; } = new();

    /// <summary>
    /// For DCF files: configured parameter value.
    /// </summary>
    public string? ParameterValue { get; set; }

    /// <summary>
    /// For DCF files: application specific name (Denotation).
    /// </summary>
    public string? Denotation { get; set; }

    /// <summary>
    /// For domain objects: file for upload operations.
    /// </summary>
    public string? UploadFile { get; set; }

    /// <summary>
    /// For domain objects: file for download operations.
    /// </summary>
    public string? DownloadFile { get; set; }

    /// <summary>
    /// Whether the object can be mapped into an SRDO (Boolean, 0 = not mappable, 1 = mappable).
    /// </summary>
    public bool SrdoMapping { get; set; }

    /// <summary>
    /// Index and sub-index of the inverted SRAD (hex string, e.g. "0x610101").
    /// </summary>
    public string? InvertedSrad { get; set; }

    /// <summary>
    /// For DCF files: parameter reference designator (max 249 characters).
    /// </summary>
    public string? ParamRefd { get; set; }

    /// <summary>
    /// Unknown keys from this object's INI section, in file order.
    /// </summary>
    /// <remarks>
    /// CiA 306 allows additional entries inside an object section. Keys are compared
    /// case-insensitively, and a key that differs from a known keyword only by case
    /// is not stored here. Known keywords stay on their dedicated properties.
    /// When the source is a DCF, <see cref="ParameterValue"/>, <see cref="Denotation"/>,
    /// <see cref="ParamRefd"/>, <see cref="UploadFile"/>, and <see cref="DownloadFile"/>
    /// are known keywords. In an EDS those names are not keywords, so they are kept here.
    /// </remarks>
    public OrderedStringDictionary RemainingEntries { get; } = new();
}

/// <summary>
/// Represents a sub-object of a CANopen object.
/// </summary>
public class CanOpenSubObject
{
    /// <summary>
    /// Sub-index in hexadecimal.
    /// </summary>
    public byte SubIndex { get; set; }

    /// <summary>
    /// Parameter name.
    /// </summary>
    public string ParameterName { get; set; } = string.Empty;

    /// <summary>
    /// Object type per CiA DS 306 (usually 0x7 = VAR).
    /// See <see cref="CanOpenObject.ObjectType"/> for all defined values.
    /// </summary>
    public byte ObjectType { get; set; } = 0x7;

    /// <summary>
    /// Data type index.
    /// </summary>
    public ushort DataType { get; set; }

    /// <summary>
    /// Access type.
    /// </summary>
    /// <remarks>
    /// Same explicit-assignment rule as <see cref="CanOpenObject.AccessType"/>.
    /// </remarks>
    public AccessType AccessType
    {
        get => _accessType;
        set
        {
            _accessType = value;
            _accessTypeSpecified = true;
        }
    }

    private AccessType _accessType;

    private bool _accessTypeSpecified;

    /// <summary>
    /// <see langword="true"/> when <see cref="AccessType"/> was set from an XDD attribute,
    /// from a resolved parameter access, or by assigning <see cref="AccessType"/>.
    /// </summary>
    internal bool AccessTypeSpecified => _accessTypeSpecified;

    /// <summary>
    /// Copies <see cref="AccessType"/> without treating a profile-file keyword as an
    /// XDD explicit attribute.
    /// </summary>
    internal void SetAccessTypeFromProfile(AccessType value) => _accessType = value;

    /// <summary>
    /// Restores both the access value and whether it is explicit. Used by <c>ModelCloner</c>.
    /// </summary>
    internal void CopyAccessTypeStateFrom(CanOpenSubObject source)
    {
        _accessType = source._accessType;
        _accessTypeSpecified = source._accessTypeSpecified;
    }

    /// <summary>
    /// Default value.
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Low limit.
    /// </summary>
    public string? LowLimit { get; set; }

    /// <summary>
    /// High limit.
    /// </summary>
    public string? HighLimit { get; set; }

    /// <summary>
    /// CiA 311 <c>uniqueIDRef</c> of the application-process <c>parameter</c> this
    /// sub-object refers to, or <see langword="null"/> when the attribute is absent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Resolution and write-back follow <see cref="CanOpenObject.UniqueIdRef"/>
    /// (CiA 311 v1.1.0 § 6.5.2.3.2.1 Table 49). An explicit attribute on the sub-object
    /// wins over the referenced parameter.
    /// </para>
    /// </remarks>
    public string? UniqueIdRef { get; set; }

    /// <summary>
    /// CiA 311 PDO mapping mode. Prefer this for XDD/XDC round-trips.
    /// </summary>
    public PdoMappingMode PdoMappingMode { get; set; }

    /// <summary>
    /// PDO mapping capability (EDS/DCF boolean view of <see cref="PdoMappingMode"/>).
    /// </summary>
    public bool PdoMapping
    {
        get => PdoMappingMode != PdoMappingMode.No;
        set
        {
            if (value)
            {
                if (PdoMappingMode == PdoMappingMode.No)
                    PdoMappingMode = PdoMappingMode.Optional;
            }
            else
            {
                PdoMappingMode = PdoMappingMode.No;
            }
        }
    }

    /// <summary>
    /// For DCF files: configured parameter value.
    /// </summary>
    public string? ParameterValue { get; set; }

    /// <summary>
    /// For DCF files: application specific name.
    /// </summary>
    public string? Denotation { get; set; }

    /// <summary>
    /// Whether the sub-object can be mapped into an SRDO (Boolean, 0 = not mappable, 1 = mappable).
    /// </summary>
    public bool SrdoMapping { get; set; }

    /// <summary>
    /// Index and sub-index of the inverted SRAD (hex string, e.g. "0x610101").
    /// </summary>
    public string? InvertedSrad { get; set; }

    /// <summary>
    /// For DCF files: parameter reference designator (max 249 characters).
    /// </summary>
    public string? ParamRefd { get; set; }

    /// <summary>
    /// Unknown keys from this sub-object's INI section, in file order.
    /// </summary>
    /// <remarks>
    /// Keys are compared case-insensitively. Known sub-object keywords stay on their
    /// dedicated properties and are not duplicated here, including when the file spells
    /// them with different casing. DCF-only keywords <see cref="ParameterValue"/>,
    /// <see cref="Denotation"/>, and <see cref="ParamRefd"/> are excluded for DCF sections.
    /// The same names in an EDS sub-object section are kept here.
    /// </remarks>
    public OrderedStringDictionary RemainingEntries { get; } = new();
}

/// <summary>
/// Access type for CANopen objects.
/// </summary>
public enum AccessType
{
    /// <summary>
    /// Read only.
    /// </summary>
    ReadOnly,

    /// <summary>
    /// Write only.
    /// </summary>
    WriteOnly,

    /// <summary>
    /// Read/Write.
    /// </summary>
    ReadWrite,

    /// <summary>
    /// Read/Write on process input.
    /// </summary>
    ReadWriteInput,

    /// <summary>
    /// Read/Write on process output.
    /// </summary>
    ReadWriteOutput,

    /// <summary>
    /// Constant value.
    /// </summary>
    Constant
}
