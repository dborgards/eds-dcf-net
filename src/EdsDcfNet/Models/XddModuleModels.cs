namespace EdsDcfNet.Models;

/// <summary>Module interfaces or a single module declaration below <c>DeviceManager</c>.</summary>
public sealed class XddDeviceModuleManagement : XddXmlModel
{
    /// <summary>Creates empty device-side module management.</summary>
    public XddDeviceModuleManagement() : base(XddXml.Create("moduleManagement")) { }
    internal XddDeviceModuleManagement(System.Xml.Linq.XElement element) : base(element) { }

    /// <summary>Available module interfaces. Adding the first one replaces a single-module declaration.</summary>
    public ICollection<XddDeviceModuleInterface> Interfaces => ChoiceList<XddDeviceModuleInterface>(
        "moduleInterfaceList", "interface", e => new XddDeviceModuleInterface(e),
        () => new XddDeviceModuleInterface(), "moduleInterface");

    /// <summary>Single module declaration, or <see langword="null"/> in interface-list form.</summary>
    public XddDeviceModuleReference? Module
    {
        get => Wrap<XddDeviceModuleReference>(Child("moduleInterface"));
        set
        {
            if (value != null) Child("moduleInterfaceList")?.Remove();
            SetChild("moduleInterface", value);
        }
    }
}

/// <summary>A module interface below <c>DeviceManager</c>.</summary>
public sealed class XddDeviceModuleInterface : XddXmlModel
{
    /// <summary>Creates an interface with empty file and module-type lists.</summary>
    public XddDeviceModuleInterface() : base(XddXml.Create("interface",
        XddXml.Create("fileList", XddXml.Create("file")), XddXml.Create("moduleTypeList"))) { }
    internal XddDeviceModuleInterface(System.Xml.Linq.XElement element) : base(element) { }
    /// <summary>Unique interface identifier.</summary>
    public string Id { get => Get("uniqueID") ?? string.Empty; set => Set("uniqueID", value); }
    /// <summary>Maximum number of connected modules.</summary>
    public ulong MaxChildren { get => ULong("maxChilds", 1); set => SetULong("maxChilds", value, null); }
    /// <summary>Whether empty slots are permitted.</summary>
    public bool UnusedSlots { get => Bool("unusedSlots"); set => SetBool("unusedSlots", value); }
    /// <summary>Whether multiple modules of one type are permitted.</summary>
    public bool MultipleChildren { get => Bool("multipleChilds"); set => SetBool("multipleChilds", value); }
    /// <summary>Localized interface labels and descriptions.</summary>
    public XddTextGroup Texts => new(Element);
    /// <summary>Search path for module-description files.</summary>
    public string SearchPath
    {
        get => Descendant("fileList", "file")?.Attribute("URI")?.Value ?? string.Empty;
        set
        {
            var list = EnsureChild("fileList", "fileList", "moduleTypeList", "connectedModuleList");
            EnsureChild(list, "file").SetAttributeValue("URI", value);
        }
    }
    /// <summary>Module types accepted by this interface.</summary>
    public ICollection<string> ModuleTypes => StringList("moduleTypeList", "moduleType", "type");
    /// <summary>Identifiers of connected modules.</summary>
    public ICollection<string> ConnectedModuleIds => StringList("connectedModuleList", "connectedModule", "childIDRef",
        pruneEmptyParent: true);
}

/// <summary>A single module supported by a device.</summary>
public sealed class XddDeviceModuleReference : XddXmlModel
{
    /// <summary>Creates an empty module declaration.</summary>
    public XddDeviceModuleReference() : base(XddXml.Create("moduleInterface")) { }
    internal XddDeviceModuleReference(System.Xml.Linq.XElement element) : base(element) { }
    /// <summary>Unique module identifier.</summary>
    public string ChildId { get => Get("childID") ?? string.Empty; set => Set("childID", value); }
    /// <summary>Manufacturer-specific module type.</summary>
    public string Type { get => Get("type") ?? string.Empty; set => Set("type", value); }
    /// <summary>Maximum number of instances; zero means unlimited.</summary>
    public ulong MaxCount { get => ULong("maxCount", 0); set => SetULong("maxCount", value, 0); }
}

/// <summary>Object-dictionary allocation for modules below <c>ApplicationLayers</c>.</summary>
public sealed class XddNetworkModuleManagement : XddXmlModel
{
    /// <summary>Creates empty network-side module management.</summary>
    public XddNetworkModuleManagement() : base(XddXml.Create("moduleManagement")) { }
    internal XddNetworkModuleManagement(System.Xml.Linq.XElement element) : base(element) { }

    /// <summary>Interface-to-index mappings. Adding the first one replaces a single-module declaration.</summary>
    public ICollection<XddNetworkModuleInterface> Interfaces => ChoiceList<XddNetworkModuleInterface>(
        "interfaceList", "interface", e => new XddNetworkModuleInterface(e),
        () => new XddNetworkModuleInterface(), "moduleInterface");

    /// <summary>Single connected module, or <see langword="null"/> in interface-list form.</summary>
    public XddNetworkModuleReference? Module
    {
        get => Wrap<XddNetworkModuleReference>(Child("moduleInterface"));
        set
        {
            if (value != null) Child("interfaceList")?.Remove();
            SetChild("moduleInterface", value);
        }
    }
}

/// <summary>An interface and its allocatable object-dictionary ranges.</summary>
public sealed class XddNetworkModuleInterface : XddXmlModel
{
    /// <summary>Creates an interface with an empty range list.</summary>
    public XddNetworkModuleInterface() : base(XddXml.Create("interface", XddXml.Create("rangeList"))) { }
    internal XddNetworkModuleInterface(System.Xml.Linq.XElement element) : base(element) { }
    /// <summary>Reference to a device-side module interface.</summary>
    public string InterfaceReference { get => Get("uniqueIDRef") ?? string.Empty; set => Set("uniqueIDRef", value); }
    /// <summary>Automatic or manual module addressing.</summary>
    public XddAddressing Addressing { get => Enum("addressing", XddAddressing.Auto); set => SetEnum("addressing", value); }
    /// <summary>Allocatable object-dictionary ranges.</summary>
    public ICollection<XddModuleRange> Ranges => List<XddModuleRange>("rangeList", "range",
        e => new XddModuleRange(e), () => new XddModuleRange());
}

/// <summary>A single connected module and its permitted node-address range.</summary>
public sealed class XddNetworkModuleReference : XddXmlModel
{
    /// <summary>Creates an empty connected-module declaration.</summary>
    public XddNetworkModuleReference() : base(XddXml.Create("moduleInterface")) { }
    internal XddNetworkModuleReference(System.Xml.Linq.XElement element) : base(element) { }
    /// <summary>Reference to the device-side module.</summary>
    public string ChildReference { get => Get("childIDRef") ?? string.Empty; set => Set("childIDRef", value); }
    /// <summary>Minimum permitted node address.</summary>
    public ulong MinimumAddress { get => ULong("addressMin", 1); set => SetULong("addressMin", value, null); }
    /// <summary>Optional maximum permitted node address.</summary>
    public ulong? MaximumAddress { get => NullableULong("addressMax"); set => SetULong("addressMax", value, null); }
    /// <summary>Addressing mode, including inheritance from the interface.</summary>
    public XddModuleAddressing Addressing { get => Enum("addressing", XddModuleAddressing.Inherit); set => SetEnum("addressing", value, XddModuleAddressing.Inherit); }
}

/// <summary>An object-dictionary range reserved for a module type.</summary>
public sealed class XddModuleRange : XddXmlModel
{
    /// <summary>Creates an empty module range.</summary>
    public XddModuleRange() : base(XddXml.Create("range")) { }
    internal XddModuleRange(System.Xml.Linq.XElement element) : base(element) { }
    /// <summary>Human-readable range name.</summary>
    public string Name { get => Get("name") ?? string.Empty; set => Set("name", value); }
    /// <summary>Module-type identifier for this range.</summary>
    public string ModuleType { get => Get("moduleType") ?? string.Empty; set => Set("moduleType", value); }
    /// <summary>Hexadecimal start index, for example <c>3000</c>.</summary>
    public string BaseIndex { get => Get("baseIndex") ?? string.Empty; set => Set("baseIndex", value); }
    /// <summary>Hexadecimal inclusive end index, for example <c>3FFF</c>.</summary>
    public string MaximumIndex { get => Get("maxIndex") ?? string.Empty; set => Set("maxIndex", value); }
    /// <summary>Optional hexadecimal last sub-index.</summary>
    public string? MaximumSubIndex { get => Get("maxSubIndex"); set => Set("maxSubIndex", value); }
    /// <summary>Whether allocation advances indexes or sub-indexes.</summary>
    public XddModuleSortMode SortMode { get => Enum("sortMode", XddModuleSortMode.Index); set => SetEnum("sortMode", value); }
    /// <summary>Whether allocation uses a free or specific address.</summary>
    public XddModuleSortNumber SortNumber { get => Enum("sortNumber", XddModuleSortNumber.Continuous); set => SetEnum("sortNumber", value); }
    /// <summary>Optional hexadecimal allocation step; the schema default is <c>01</c>.</summary>
    public string? SortStep { get => Get("sortStep"); set => Set("sortStep", value); }
}

/// <summary>Addressing mode of a module interface.</summary>
public enum XddAddressing
{
    /// <summary>Automatic assignment.</summary>
    Auto,
    /// <summary>Manual assignment.</summary>
    Manual
}
/// <summary>Addressing mode of a connected module.</summary>
public enum XddModuleAddressing
{
    /// <summary>Inherit the interface setting.</summary>
    Inherit,
    /// <summary>Automatic assignment.</summary>
    Auto,
    /// <summary>Manual assignment.</summary>
    Manual
}
/// <summary>Object-dictionary allocation dimension.</summary>
public enum XddModuleSortMode
{
    /// <summary>Allocate a new index.</summary>
    Index,
    /// <summary>Allocate a new sub-index.</summary>
    SubIndex
}
/// <summary>Object-dictionary allocation address strategy.</summary>
public enum XddModuleSortNumber
{
    /// <summary>Use the next free address.</summary>
    Continuous,
    /// <summary>Use a specific address.</summary>
    Address
}
