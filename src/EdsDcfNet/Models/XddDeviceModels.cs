namespace EdsDcfNet.Models;

/// <summary>Device monitoring and module-management information from an XDD/XDC device profile.</summary>
public sealed class XddDeviceManager : XddXmlModel
{
    /// <summary>Creates an empty device manager.</summary>
    public XddDeviceManager() : base(XddXml.Create("DeviceManager")) { }
    internal XddDeviceManager(System.Xml.Linq.XElement element) : base(element) { }

    /// <summary>LED indicators declared by the device.</summary>
    public ICollection<XddLed> Indicators => List<XddLed>("indicatorList/LEDList", "LED",
        e => new XddLed(e), () => new XddLed(), true, "LED", "combinedState");

    /// <summary>Combined states that reference two or more LED states.</summary>
    public ICollection<XddCombinedState> CombinedStates => List<XddCombinedState>("indicatorList/LEDList", "combinedState",
        e => new XddCombinedState(e), () => new XddCombinedState(), true, "LED", "combinedState");

    /// <summary>Device-side module management, or <see langword="null"/> when absent.</summary>
    public XddDeviceModuleManagement? ModuleManagement
    {
        get => Wrap<XddDeviceModuleManagement>(Child("moduleManagement"));
        set => SetChild("moduleManagement", value, "indicatorList", "moduleManagement");
    }
}

/// <summary>A network-independent device function.</summary>
public sealed class XddDeviceFunction : XddXmlModel
{
    /// <summary>Creates the smallest schema-shaped function with one empty characteristic.</summary>
    public XddDeviceFunction() : base(XddXml.CreateFunction()) { }
    internal XddDeviceFunction(System.Xml.Linq.XElement element) : base(element) { }

    /// <summary>Groups of textual device characteristics.</summary>
    public ICollection<XddCharacteristicsList> CharacteristicsLists => List<XddCharacteristicsList>(
        "capabilities", "characteristicsList", e => new XddCharacteristicsList(e),
        () => new XddCharacteristicsList(), false, "characteristicsList", "standardComplianceList");

    /// <summary>Standards with which the device complies.</summary>
    public ICollection<XddStandardCompliance> Standards => List<XddStandardCompliance>(
        "capabilities/standardComplianceList", "compliantWith", e => new XddStandardCompliance(e),
        () => new XddStandardCompliance(), pruneEmptyParent: true);

    /// <summary>External pictures linked to this function.</summary>
    public ICollection<XddPicture> Pictures => List<XddPicture>("picturesList", "picture",
        e => new XddPicture(e), () => new XddPicture(), pruneEmptyParent: true);

    /// <summary>External dictionaries used by label and description references.</summary>
    public ICollection<XddTextResource> Dictionaries => List<XddTextResource>("dictionaryList", "dictionary",
        e => new XddTextResource(e), () => new XddTextResource(), pruneEmptyParent: true);
}

/// <summary>A categorized group of device characteristics.</summary>
public sealed class XddCharacteristicsList : XddXmlModel
{
    /// <summary>Creates an empty characteristics group.</summary>
    public XddCharacteristicsList() : base(XddXml.Create("characteristicsList")) { }
    internal XddCharacteristicsList(System.Xml.Linq.XElement element) : base(element) { }

    /// <summary>Localized labels and descriptions of the optional category, or <see langword="null"/>.</summary>
    public XddTextGroup? Category
    {
        get => Wrap<XddTextGroup>(Child("category"));
        set => SetChild("category", value, "category", "characteristic");
    }

    /// <summary>Characteristics in this group.</summary>
    public ICollection<XddCharacteristic> Characteristics => List<XddCharacteristic>(null, "characteristic",
        e => new XddCharacteristic(e), () => new XddCharacteristic());
}

/// <summary>A named technical characteristic with one or more values.</summary>
public sealed class XddCharacteristic : XddXmlModel
{
    /// <summary>Creates a characteristic with an empty name and value.</summary>
    public XddCharacteristic() : base(XddXml.Create("characteristic", XddXml.Create("characteristicName"),
        XddXml.Create("characteristicContent"))) { }
    internal XddCharacteristic(System.Xml.Linq.XElement element) : base(element) { }

    /// <summary>Localized names of the characteristic.</summary>
    public XddTextGroup Name
    {
        get => new(EnsureChild("characteristicName", "characteristicName", "characteristicContent"));
        set => SetChild("characteristicName", value, "characteristicName", "characteristicContent");
    }

    /// <summary>Localized values of the characteristic.</summary>
    public ICollection<XddTextGroup> Contents => List<XddTextGroup>(null, "characteristicContent",
        e => new XddTextGroup(e), () => new XddTextGroup("characteristicContent"));
}

/// <summary>Labels, descriptions, or external text references belonging to one element.</summary>
public sealed class XddTextGroup : XddXmlModel
{
    /// <summary>Creates an empty characteristic-content group.</summary>
    public XddTextGroup() : this("characteristicContent") { }
    internal XddTextGroup(string name) : base(XddXml.Create(name)) { }
    internal XddTextGroup(System.Xml.Linq.XElement element) : base(element) { }

    /// <summary>Inline and referenced text entries in document order.</summary>
    public ICollection<XddText> Items => List<XddText>(null, null,
        e => new XddText(e), () => new XddText(XddTextKind.Label));

    /// <summary>Adds a localized label or description.</summary>
    public XddText Add(string text, string language, XddTextKind kind = XddTextKind.Label)
    {
        var item = new XddText(kind) { Value = text, Language = language };
        Items.Add(item);
        return item;
    }
}

/// <summary>One entry of a CiA 311 <c>g_labels</c> group.</summary>
public sealed class XddText : XddXmlModel
{
    /// <summary>Creates an inline label.</summary>
    public XddText() : this(XddTextKind.Label) { }
    /// <summary>Creates a text entry of the selected kind.</summary>
    public XddText(XddTextKind kind) : base(XddXml.Create(XddXml.TextName(kind))) { Kind = kind; }
    internal XddText(System.Xml.Linq.XElement element) : base(element) { }

    /// <summary>Inline text or external-reference form of this entry.</summary>
    public XddTextKind Kind
    {
        get => XddXml.TextKind(Element.Name.LocalName);
        set => Element.Name = Element.Name.Namespace + XddXml.TextName(value);
    }

    /// <summary>Language for inline text; <see langword="null"/> for external references.</summary>
    public string? Language { get => Get("lang"); set => Set("lang", value); }
    /// <summary>Optional URI of an inline description.</summary>
    public string? Uri { get => Get("URI"); set => Set("URI", value); }
    /// <summary>Dictionary identifier for an external reference.</summary>
    public string? DictionaryId { get => Get("dictID"); set => Set("dictID", value); }
    /// <summary>Text identifier for an external reference.</summary>
    public string? TextId { get => Get("textID"); set => Set("textID", value); }
    /// <summary>Inline text or the URI of an external text resource.</summary>
    public string Value { get => Element.Value; set => Element.Value = value ?? string.Empty; }
}

/// <summary>The four entries allowed in a CiA 311 <c>g_labels</c> group.</summary>
public enum XddTextKind
{
    /// <summary>Inline identifying text.</summary>
    Label,
    /// <summary>Inline descriptive text.</summary>
    Description,
    /// <summary>Identifying text in an external dictionary.</summary>
    LabelReference,
    /// <summary>Descriptive text in an external dictionary.</summary>
    DescriptionReference
}

/// <summary>A standards-compliance declaration.</summary>
public sealed class XddStandardCompliance : XddXmlModel
{
    /// <summary>Creates an empty compliance declaration.</summary>
    public XddStandardCompliance() : base(XddXml.Create("compliantWith")) { }
    internal XddStandardCompliance(System.Xml.Linq.XElement element) : base(element) { }
    /// <summary>Name or number of the standard.</summary>
    public string Name { get => Get("name") ?? string.Empty; set => Set("name", value); }
    /// <summary>International or company-internal scope.</summary>
    public XddStandardRange Range { get => Enum("range", XddStandardRange.International); set => SetEnum("range", value); }
    /// <summary>Localized compliance descriptions.</summary>
    public XddTextGroup Texts => new(Element);
}
/// <summary>Scope of a standards-compliance declaration.</summary>
public enum XddStandardRange
{
    /// <summary>An international standard.</summary>
    International,
    /// <summary>A company-internal standard.</summary>
    Internal
}

/// <summary>An external picture reference.</summary>
public sealed class XddPicture : XddXmlModel
{
    /// <summary>Creates an empty picture reference.</summary>
    public XddPicture() : base(XddXml.Create("picture")) { }
    internal XddPicture(System.Xml.Linq.XElement element) : base(element) { }
    /// <summary>Relative or absolute picture URI.</summary>
    public string Uri { get => Get("URI") ?? string.Empty; set => Set("URI", value); }
    /// <summary>Optional vendor-assigned picture number.</summary>
    public uint? Number { get => UInt("number"); set => SetUInt("number", value); }
    /// <summary>Localized picture labels and descriptions.</summary>
    public XddTextGroup Texts => new(Element);
}

/// <summary>An external text dictionary.</summary>
public sealed class XddTextResource : XddXmlModel
{
    /// <summary>Creates an empty dictionary and file reference.</summary>
    public XddTextResource() : base(XddXml.Create("dictionary", XddXml.Create("file"))) { }
    internal XddTextResource(System.Xml.Linq.XElement element) : base(element) { }
    /// <summary>Language used by the external file.</summary>
    public string Language { get => Get("lang") ?? string.Empty; set => Set("lang", value); }
    /// <summary>Identifier used by text references.</summary>
    public string Id { get => Get("dictID") ?? string.Empty; set => Set("dictID", value); }
    /// <summary>Relative or absolute dictionary-file URI.</summary>
    public string Uri
    {
        get => Child("file")?.Attribute("URI")?.Value ?? string.Empty;
        set => EnsureChild("file").SetAttributeValue("URI", value);
    }
}

/// <summary>An LED indicator and its signalled states.</summary>
public sealed class XddLed : XddXmlModel
{
    /// <summary>Creates an empty LED declaration.</summary>
    public XddLed() : base(XddXml.Create("LED")) { }
    internal XddLed(System.Xml.Linq.XElement element) : base(element) { }
    /// <summary>Monocolor or bicolor hardware.</summary>
    public XddLedColors Colors { get => Enum("LEDcolors", XddLedColors.Monocolor); set => SetEnum("LEDcolors", value); }
    /// <summary>Optional supervised functional category.</summary>
    public XddLedType? Type { get => NullableEnum<XddLedType>("LEDtype"); set => SetNullableEnum("LEDtype", value); }
    /// <summary>Localized LED labels and descriptions.</summary>
    public XddTextGroup Texts => new(Element);
    /// <summary>States signalled by this LED.</summary>
    public ICollection<XddLedState> States => List<XddLedState>(null, "LEDstate",
        e => new XddLedState(e), () => new XddLedState());
}
/// <summary>Color capabilities of an LED.</summary>
public enum XddLedColors
{
    /// <summary>One color.</summary>
    Monocolor,
    /// <summary>Two colors.</summary>
    Bicolor
}
/// <summary>Functional category supervised by an LED.</summary>
public enum XddLedType
{
    /// <summary>Input/output functionality.</summary>
    IO,
    /// <summary>Device functionality.</summary>
    Device,
    /// <summary>Communication functionality.</summary>
    Communication
}

/// <summary>A visual state signalled by an LED.</summary>
public sealed class XddLedState : XddXmlModel
{
    /// <summary>Creates an empty LED state.</summary>
    public XddLedState() : base(XddXml.Create("LEDstate")) { }
    internal XddLedState(System.Xml.Linq.XElement element) : base(element) { }
    /// <summary>Unique identifier used by combined states.</summary>
    public string Id { get => Get("uniqueID") ?? string.Empty; set => Set("uniqueID", value); }
    /// <summary>Visual behavior of the LED.</summary>
    public XddLedVisualState State { get => Enum("state", XddLedVisualState.Off); set => SetEnum("state", value); }
    /// <summary>Color used in this state.</summary>
    public XddLedColor Color { get => Enum("LEDcolor", XddLedColor.Green); set => SetEnum("LEDcolor", value); }
    /// <summary>Optional flashing period in milliseconds.</summary>
    public uint? FlashingPeriod { get => UInt("flashingPeriod"); set => SetUInt("flashingPeriod", value); }
    /// <summary>Impulse width as a percentage; the schema default is 50.</summary>
    public byte ImpulseWidth { get => Byte("impulsWidth", 50); set => SetByte("impulsWidth", value, 50); }
    /// <summary>Impulses per period; the schema default is 1.</summary>
    public byte NumberOfImpulses { get => Byte("numberOfImpulses", 1); set => SetByte("numberOfImpulses", value, 1); }
    /// <summary>Localized state labels and descriptions.</summary>
    public XddTextGroup Texts => new(Element);
}
/// <summary>Visual behavior of an LED state.</summary>
public enum XddLedVisualState
{
    /// <summary>Continuously on.</summary>
    On,
    /// <summary>Continuously off.</summary>
    Off,
    /// <summary>Flashing.</summary>
    Flashing
}
/// <summary>Color of an LED state.</summary>
public enum XddLedColor
{
    /// <summary>Green.</summary>
    Green,
    /// <summary>Amber.</summary>
    Amber,
    /// <summary>Red.</summary>
    Red
}

/// <summary>A state represented by a combination of LED states.</summary>
public sealed class XddCombinedState : XddXmlModel
{
    /// <summary>Creates an empty combined state.</summary>
    public XddCombinedState() : base(XddXml.Create("combinedState")) { }
    internal XddCombinedState(System.Xml.Linq.XElement element) : base(element) { }
    /// <summary>Localized combined-state labels and descriptions.</summary>
    public XddTextGroup Texts => new(Element);
    /// <summary>Identifiers of the participating LED states.</summary>
    public ICollection<string> StateReferences => StringList("LEDstateRef", "stateIDRef");
}
