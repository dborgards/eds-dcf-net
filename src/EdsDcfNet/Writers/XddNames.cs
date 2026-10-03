namespace EdsDcfNet.Writers;

using System.Globalization;
using System.Xml.Linq;

/// <summary>
/// Instance names for CiA 311 elements. Each name is taken from the XSD
/// declaration of that parent/child particle: a globally declared element
/// (<c>ref</c>) is in <see cref="Namespace"/>, a locally declared element is
/// in no namespace. The key is the parent context plus the local name, because
/// <c>moduleManagement</c>, <c>interfaceList</c>, <c>interface</c> and
/// <c>range</c> are global in the device profile and local in the network profile.
/// </summary>
/// <remarks>
/// <para>
/// Writers declare <see cref="Prefix"/> on the document element and do not set
/// a default namespace. Qualified elements are therefore written with that
/// prefix, and unqualified elements need no <c>xmlns=""</c> reset. A default
/// namespace plus <c>xmlns=""</c> would also validate; the prefix form keeps
/// <c>xsi:type</c> a prefixed QName with a single namespace declaration.
/// </para>
/// <para>
/// An element whose <see cref="XName"/> was observed at read time (an
/// unmodelled fragment kept for a later round-trip) must be written with that
/// name. It must not be passed through <see cref="Child"/> or
/// <see cref="ChildOfType"/>, which would assign the namespace of a different
/// declaration of the same local name.
/// </para>
/// </remarks>
internal static class XddNames
{
    internal const string NamespaceUri = "http://www.canopen.org/xml/1.1";

    internal const string Prefix = "co";

    internal const string DeviceProfileBodyType = "ProfileBody_Device_CANopen";

    internal const string NetworkProfileBodyType = "ProfileBody_CommunicationNetwork_CANopen";

    internal static readonly XNamespace Namespace = NamespaceUri;

    internal static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    /// <summary>The document element. It is global and has no parent particle.</summary>
    internal static readonly XName ProfileContainer = Namespace + "ISO15745ProfileContainer";

    internal static int DeclarationCount => Declarations.Count;

    /// <summary>
    /// Name of <paramref name="localName"/> as a child of an element whose
    /// instance name is <paramref name="parent"/>.
    /// </summary>
    internal static XName Child(XName parent, string localName)
        => Resolve(ElementKey(parent), parent.ToString(), localName);

    /// <summary>
    /// Name of <paramref name="localName"/> as a child of a complex type used
    /// as <c>xsi:type</c> (the two <c>ProfileBody</c> derivations).
    /// </summary>
    internal static XName ChildOfType(string typeLocalName, string localName)
    {
        if (string.IsNullOrEmpty(typeLocalName))
            throw new ArgumentException("Profile body type name is required.", nameof(typeLocalName));

        return Resolve("t:" + typeLocalName, typeLocalName, localName);
    }

    /// <summary>
    /// <c>xsi:type</c> attribute whose value is a prefixed QName in <see cref="Namespace"/>.
    /// </summary>
    internal static XAttribute TypeAttribute(string typeLocalName)
    {
        if (string.IsNullOrEmpty(typeLocalName))
            throw new ArgumentException("Profile body type name is required.", nameof(typeLocalName));

        return new XAttribute(Xsi + "type", Prefix + ":" + typeLocalName);
    }

    /// <summary>
    /// Name of a simple-type element (<c>UDINT</c>, <c>BOOL</c>, …) under
    /// <paramref name="parent"/>. Declared names come from the table. A name
    /// the schema does not list is still written with no namespace, which is
    /// the form of every <c>g_simple</c> element, so a model value such as
    /// <c>DATE</c> keeps the element the writer emitted before this table existed.
    /// </summary>
    internal static XName SimpleType(XName parent, string localName)
    {
        if (string.IsNullOrEmpty(localName))
            throw new ArgumentException("Element local name is required.", nameof(localName));

        var key = ElementKey(parent) + "\n" + localName;
        if (Declarations.TryGetValue(key, out var qualified))
            return qualified ? Namespace + localName : localName;

        if (!Declarations.ContainsKey(ElementKey(parent) + "\nBOOL"))
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                "No CiA 311 simple-type choice for '{0}' under '{1}'.",
                localName,
                parent));
        }

        return localName;
    }

    /// <summary>
    /// Name of a <c>g_labels</c> element (<c>label</c>, <c>description</c>,
    /// <c>labelRef</c>, <c>descriptionRef</c>). Every schema declaration of
    /// those names is local. A parent that does not include the group still
    /// gets the unqualified element, which is the form the writer emitted
    /// before this table existed.
    /// </summary>
    internal static XName Label(XName parent, string localName)
    {
        if (!IsLabelName(localName))
            throw new ArgumentException("Element local name is not a g_labels element.", nameof(localName));

        var key = ElementKey(parent) + "\n" + localName;
        if (Declarations.TryGetValue(key, out var qualified))
            return qualified ? Namespace + localName : localName;

        return localName;
    }

    /// <summary>
    /// Like <see cref="Child"/>, but a name the table does not list (an unknown element, a
    /// <c>g_simple</c> or <c>g_labels</c> element) is unqualified instead of an error. Used only to
    /// qualify kept fragments of a source that has no namespace at all (older outputs of this library).
    /// </summary>
    internal static XName ChildOrUnqualified(XName parent, string localName)
        => Lookup(ElementKey(parent), localName);

    /// <summary>Like <see cref="ChildOfType"/>, with the fallback of <see cref="ChildOrUnqualified"/>.</summary>
    internal static XName ChildOfTypeOrUnqualified(string typeLocalName, string localName)
        => Lookup("t:" + typeLocalName, localName);

    private static XName Lookup(string parentKey, string localName)
        => Declarations.TryGetValue(parentKey + "\n" + localName, out var qualified) && qualified
            ? Namespace + localName
            : localName;

    internal static XElement Element(XName parent, string localName, params object[] content)
        => new(Child(parent, localName), content);

    internal static XElement ElementOfType(string typeLocalName, string localName, params object[] content)
        => new(ChildOfType(typeLocalName, localName), content);

    private static XName Resolve(string parentKey, string parentDisplay, string localName)
    {
        if (string.IsNullOrEmpty(localName))
            throw new ArgumentException("Element local name is required.", nameof(localName));

        if (!Declarations.TryGetValue(parentKey + "\n" + localName, out var qualified))
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                "No CiA 311 element declaration for '{0}' under '{1}'.",
                localName,
                parentDisplay));
        }

        return qualified ? Namespace + localName : localName;
    }

    private static string ElementKey(XName parent) => "e:" + parent.ToString();

    private static bool IsLabelName(string localName)
        => localName == "label"
            || localName == "description"
            || localName == "labelRef"
            || localName == "descriptionRef";

    /// <summary>
    /// Parent context, newline, child local name → <see langword="true"/> when
    /// the child particle is qualified. Generated from the compiled CiA 311
    /// schema set and checked against that set by <c>XddSchemaNamespaceTests</c>.
    /// </summary>
    private static readonly Dictionary<string, bool> Declarations = new()
    {
        ["e:ApplicationLayers\nCANopenObjectList"] = true,
        ["e:ApplicationLayers\ndummyUsage"] = false,
        ["e:ApplicationLayers\ndynamicChannels"] = false,
        ["e:ApplicationLayers\nidentity"] = false,
        ["e:ApplicationLayers\nmoduleManagement"] = false,
        ["e:CANopenObject\nCANopenSubObject"] = false,
        ["e:ExternalProfileHandle\nProfileIdentification"] = false,
        ["e:ExternalProfileHandle\nProfileLocation"] = false,
        ["e:ExternalProfileHandle\nProfileRevision"] = false,
        ["e:ISO15745Reference\nISO15745Edition"] = false,
        ["e:ISO15745Reference\nISO15745Part"] = false,
        ["e:ISO15745Reference\nProfileTechnology"] = false,
        ["e:NetworkManagement\nCANopenGeneralFeatures"] = false,
        ["e:NetworkManagement\nCANopenMasterFeatures"] = false,
        ["e:NetworkManagement\ndeviceCommissioning"] = false,
        ["e:PhysicalLayer\nbaudRate"] = false,
        ["e:ProfileHeader\nAdditionalInformation"] = false,
        ["e:ProfileHeader\nIASInterfaceType"] = false,
        ["e:ProfileHeader\nISO15745Reference"] = false,
        ["e:ProfileHeader\nProfileClassID"] = false,
        ["e:ProfileHeader\nProfileDate"] = false,
        ["e:ProfileHeader\nProfileIdentification"] = false,
        ["e:ProfileHeader\nProfileName"] = false,
        ["e:ProfileHeader\nProfileRevision"] = false,
        ["e:ProfileHeader\nProfileSource"] = false,
        ["e:TransportLayers\nPhysicalLayer"] = false,
        ["e:baudRate\nsupportedBaudRate"] = false,
        ["e:category\ndescription"] = false,
        ["e:category\ndescriptionRef"] = false,
        ["e:category\nlabel"] = false,
        ["e:category\nlabelRef"] = false,
        ["e:connectedModuleList\nconnectedModule"] = false,
        ["e:dummyUsage\ndummy"] = false,
        ["e:dynamicChannels\ndynamicChannel"] = false,
        ["e:identity\nbuildDate"] = true,
        ["e:identity\ndeviceFamily"] = true,
        ["e:identity\nproductID"] = true,
        ["e:identity\nspecificationRevision"] = true,
        ["e:identity\nvendorID"] = true,
        ["e:identity\nversion"] = true,
        ["e:interface\nrangeList"] = false,
        ["e:interfaceList\ninterface"] = false,
        ["e:maxValue\ndescription"] = false,
        ["e:maxValue\ndescriptionRef"] = false,
        ["e:maxValue\nlabel"] = false,
        ["e:maxValue\nlabelRef"] = false,
        ["e:minValue\ndescription"] = false,
        ["e:minValue\ndescriptionRef"] = false,
        ["e:minValue\nlabel"] = false,
        ["e:minValue\nlabelRef"] = false,
        ["e:moduleManagement\ninterfaceList"] = false,
        ["e:moduleManagement\nmoduleInterface"] = false,
        ["e:rangeList\nrange"] = false,
        ["e:step\ndescription"] = false,
        ["e:step\ndescriptionRef"] = false,
        ["e:step\nlabel"] = false,
        ["e:step\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}ApplicationProcess\ndataTypeList"] = true,
        ["e:{http://www.canopen.org/xml/1.1}ApplicationProcess\nfunctionInstanceList"] = true,
        ["e:{http://www.canopen.org/xml/1.1}ApplicationProcess\nfunctionTypeList"] = true,
        ["e:{http://www.canopen.org/xml/1.1}ApplicationProcess\nparameterGroupList"] = true,
        ["e:{http://www.canopen.org/xml/1.1}ApplicationProcess\nparameterList"] = true,
        ["e:{http://www.canopen.org/xml/1.1}ApplicationProcess\ntemplateList"] = true,
        ["e:{http://www.canopen.org/xml/1.1}CANopenObjectList\nCANopenObject"] = false,
        ["e:{http://www.canopen.org/xml/1.1}DeviceFunction\ncapabilities"] = true,
        ["e:{http://www.canopen.org/xml/1.1}DeviceFunction\ndictionaryList"] = true,
        ["e:{http://www.canopen.org/xml/1.1}DeviceFunction\npicturesList"] = true,
        ["e:{http://www.canopen.org/xml/1.1}DeviceIdentity\nbuildDate"] = true,
        ["e:{http://www.canopen.org/xml/1.1}DeviceIdentity\ndeviceFamily"] = true,
        ["e:{http://www.canopen.org/xml/1.1}DeviceIdentity\ninstanceName"] = true,
        ["e:{http://www.canopen.org/xml/1.1}DeviceIdentity\norderNumber"] = true,
        ["e:{http://www.canopen.org/xml/1.1}DeviceIdentity\nproductFamily"] = true,
        ["e:{http://www.canopen.org/xml/1.1}DeviceIdentity\nproductID"] = true,
        ["e:{http://www.canopen.org/xml/1.1}DeviceIdentity\nproductName"] = true,
        ["e:{http://www.canopen.org/xml/1.1}DeviceIdentity\nproductText"] = true,
        ["e:{http://www.canopen.org/xml/1.1}DeviceIdentity\nspecificationRevision"] = true,
        ["e:{http://www.canopen.org/xml/1.1}DeviceIdentity\nvendorID"] = true,
        ["e:{http://www.canopen.org/xml/1.1}DeviceIdentity\nvendorName"] = true,
        ["e:{http://www.canopen.org/xml/1.1}DeviceIdentity\nvendorText"] = true,
        ["e:{http://www.canopen.org/xml/1.1}DeviceIdentity\nversion"] = true,
        ["e:{http://www.canopen.org/xml/1.1}DeviceManager\nindicatorList"] = true,
        ["e:{http://www.canopen.org/xml/1.1}DeviceManager\nmoduleManagement"] = true,
        ["e:{http://www.canopen.org/xml/1.1}ISO15745Profile\nProfileBody"] = false,
        ["e:{http://www.canopen.org/xml/1.1}ISO15745Profile\nProfileHeader"] = false,
        ["e:{http://www.canopen.org/xml/1.1}ISO15745ProfileContainer\nISO15745Profile"] = true,
        ["e:{http://www.canopen.org/xml/1.1}LED\nLEDstate"] = true,
        ["e:{http://www.canopen.org/xml/1.1}LED\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}LED\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}LED\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}LED\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}LEDList\nLED"] = true,
        ["e:{http://www.canopen.org/xml/1.1}LEDList\ncombinedState"] = true,
        ["e:{http://www.canopen.org/xml/1.1}LEDstate\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}LEDstate\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}LEDstate\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}LEDstate\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}actualValue\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}actualValue\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}actualValue\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}actualValue\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}allowedValues\nrange"] = true,
        ["e:{http://www.canopen.org/xml/1.1}allowedValues\nvalue"] = true,
        ["e:{http://www.canopen.org/xml/1.1}allowedValuesTemplate\nrange"] = true,
        ["e:{http://www.canopen.org/xml/1.1}allowedValuesTemplate\nvalue"] = true,
        ["e:{http://www.canopen.org/xml/1.1}array\nBITSTRING"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\nBOOL"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\nBYTE"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\nCHAR"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\nDINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\nDWORD"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\nINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\nLINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\nLREAL"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\nLWORD"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\nREAL"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\nSINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\nSTRING"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\nUDINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\nUINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\nULINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\nUSINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\nWORD"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\nWSTRING"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\ndataTypeIDRef"] = true,
        ["e:{http://www.canopen.org/xml/1.1}array\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}array\nsubrange"] = true,
        ["e:{http://www.canopen.org/xml/1.1}capabilities\ncharacteristicsList"] = true,
        ["e:{http://www.canopen.org/xml/1.1}capabilities\nstandardComplianceList"] = true,
        ["e:{http://www.canopen.org/xml/1.1}characteristic\ncharacteristicContent"] = true,
        ["e:{http://www.canopen.org/xml/1.1}characteristic\ncharacteristicName"] = true,
        ["e:{http://www.canopen.org/xml/1.1}characteristicContent\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}characteristicContent\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}characteristicContent\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}characteristicContent\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}characteristicName\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}characteristicName\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}characteristicName\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}characteristicName\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}characteristicsList\ncategory"] = false,
        ["e:{http://www.canopen.org/xml/1.1}characteristicsList\ncharacteristic"] = true,
        ["e:{http://www.canopen.org/xml/1.1}combinedState\nLEDstateRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}combinedState\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}combinedState\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}combinedState\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}combinedState\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}compliantWith\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}compliantWith\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}compliantWith\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}compliantWith\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}configVars\nvarDeclaration"] = true,
        ["e:{http://www.canopen.org/xml/1.1}count\nallowedValues"] = true,
        ["e:{http://www.canopen.org/xml/1.1}count\ndefaultValue"] = true,
        ["e:{http://www.canopen.org/xml/1.1}count\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}count\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}count\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}count\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}dataTypeList\narray"] = true,
        ["e:{http://www.canopen.org/xml/1.1}dataTypeList\nderived"] = true,
        ["e:{http://www.canopen.org/xml/1.1}dataTypeList\nenum"] = true,
        ["e:{http://www.canopen.org/xml/1.1}dataTypeList\nstruct"] = true,
        ["e:{http://www.canopen.org/xml/1.1}defaultValue\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}defaultValue\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}defaultValue\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}defaultValue\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}denotation\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}denotation\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}denotation\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}denotation\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\nBITSTRING"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\nBOOL"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\nBYTE"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\nCHAR"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\nDINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\nDWORD"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\nINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\nLINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\nLREAL"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\nLWORD"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\nREAL"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\nSINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\nSTRING"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\nUDINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\nUINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\nULINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\nUSINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\nWORD"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\nWSTRING"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\ncount"] = true,
        ["e:{http://www.canopen.org/xml/1.1}derived\ndataTypeIDRef"] = true,
        ["e:{http://www.canopen.org/xml/1.1}derived\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}derived\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}deviceFamily\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}deviceFamily\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}deviceFamily\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}deviceFamily\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}dictionary\nfile"] = true,
        ["e:{http://www.canopen.org/xml/1.1}dictionaryList\ndictionary"] = true,
        ["e:{http://www.canopen.org/xml/1.1}enum\nBITSTRING"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enum\nBOOL"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enum\nBYTE"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enum\nCHAR"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enum\nDINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enum\nDWORD"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enum\nINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enum\nLINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enum\nLREAL"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enum\nLWORD"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enum\nREAL"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enum\nSINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enum\nSTRING"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enum\nUDINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enum\nUINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enum\nULINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enum\nUSINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enum\nWORD"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enum\nWSTRING"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enum\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enum\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enum\nenumValue"] = true,
        ["e:{http://www.canopen.org/xml/1.1}enum\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enum\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enumValue\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enumValue\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enumValue\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}enumValue\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}fileList\nfile"] = true,
        ["e:{http://www.canopen.org/xml/1.1}functionInstance\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}functionInstance\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}functionInstance\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}functionInstance\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}functionInstanceList\nconnection"] = true,
        ["e:{http://www.canopen.org/xml/1.1}functionInstanceList\nfunctionInstance"] = true,
        ["e:{http://www.canopen.org/xml/1.1}functionType\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}functionType\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}functionType\nfunctionInstanceList"] = true,
        ["e:{http://www.canopen.org/xml/1.1}functionType\ninterfaceList"] = true,
        ["e:{http://www.canopen.org/xml/1.1}functionType\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}functionType\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}functionType\nversionInfo"] = true,
        ["e:{http://www.canopen.org/xml/1.1}functionTypeList\nfunctionType"] = true,
        ["e:{http://www.canopen.org/xml/1.1}indicatorList\nLEDList"] = true,
        ["e:{http://www.canopen.org/xml/1.1}inputVars\nvarDeclaration"] = true,
        ["e:{http://www.canopen.org/xml/1.1}interface\nconnectedModuleList"] = false,
        ["e:{http://www.canopen.org/xml/1.1}interface\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}interface\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}interface\nfileList"] = true,
        ["e:{http://www.canopen.org/xml/1.1}interface\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}interface\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}interface\nmoduleTypeList"] = true,
        ["e:{http://www.canopen.org/xml/1.1}interfaceList\nconfigVars"] = true,
        ["e:{http://www.canopen.org/xml/1.1}interfaceList\ninputVars"] = true,
        ["e:{http://www.canopen.org/xml/1.1}interfaceList\noutputVars"] = true,
        ["e:{http://www.canopen.org/xml/1.1}moduleInterfaceList\ninterface"] = true,
        ["e:{http://www.canopen.org/xml/1.1}moduleManagement\nmoduleInterface"] = false,
        ["e:{http://www.canopen.org/xml/1.1}moduleManagement\nmoduleInterfaceList"] = true,
        ["e:{http://www.canopen.org/xml/1.1}moduleTypeList\nmoduleType"] = true,
        ["e:{http://www.canopen.org/xml/1.1}outputVars\nvarDeclaration"] = true,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nBITSTRING"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nBOOL"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nBYTE"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nCHAR"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nDINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nDWORD"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nLINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nLREAL"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nLWORD"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nREAL"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nSINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nSTRING"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nUDINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nUINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nULINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nUSINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nWORD"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nWSTRING"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nactualValue"] = true,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nallowedValues"] = true,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nconditionalSupport"] = true,
        ["e:{http://www.canopen.org/xml/1.1}parameter\ndataTypeIDRef"] = true,
        ["e:{http://www.canopen.org/xml/1.1}parameter\ndefaultValue"] = true,
        ["e:{http://www.canopen.org/xml/1.1}parameter\ndenotation"] = true,
        ["e:{http://www.canopen.org/xml/1.1}parameter\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nproperty"] = true,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nsubstituteValue"] = true,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nunit"] = true,
        ["e:{http://www.canopen.org/xml/1.1}parameter\nvariableRef"] = true,
        ["e:{http://www.canopen.org/xml/1.1}parameterGroup\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterGroup\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterGroup\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterGroup\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterGroup\nparameterGroup"] = true,
        ["e:{http://www.canopen.org/xml/1.1}parameterGroup\nparameterRef"] = true,
        ["e:{http://www.canopen.org/xml/1.1}parameterGroupList\nparameterGroup"] = true,
        ["e:{http://www.canopen.org/xml/1.1}parameterList\nparameter"] = true,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nBITSTRING"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nBOOL"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nBYTE"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nCHAR"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nDINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nDWORD"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nLINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nLREAL"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nLWORD"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nREAL"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nSINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nSTRING"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nUDINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nUINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nULINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nUSINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nWORD"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nWSTRING"] = false,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nactualValue"] = true,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nallowedValues"] = true,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nconditionalSupport"] = true,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\ndataTypeIDRef"] = true,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\ndefaultValue"] = true,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nproperty"] = true,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nsubstituteValue"] = true,
        ["e:{http://www.canopen.org/xml/1.1}parameterTemplate\nunit"] = true,
        ["e:{http://www.canopen.org/xml/1.1}picture\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}picture\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}picture\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}picture\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}picturesList\npicture"] = true,
        ["e:{http://www.canopen.org/xml/1.1}productText\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}productText\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}productText\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}productText\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}range\nmaxValue"] = false,
        ["e:{http://www.canopen.org/xml/1.1}range\nminValue"] = false,
        ["e:{http://www.canopen.org/xml/1.1}range\nstep"] = false,
        ["e:{http://www.canopen.org/xml/1.1}standardComplianceList\ncompliantWith"] = true,
        ["e:{http://www.canopen.org/xml/1.1}struct\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}struct\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}struct\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}struct\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}struct\nvarDeclaration"] = true,
        ["e:{http://www.canopen.org/xml/1.1}substituteValue\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}substituteValue\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}substituteValue\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}substituteValue\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}templateList\nallowedValuesTemplate"] = true,
        ["e:{http://www.canopen.org/xml/1.1}templateList\nparameterTemplate"] = true,
        ["e:{http://www.canopen.org/xml/1.1}unit\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}unit\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}unit\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}unit\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}value\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}value\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}value\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}value\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nBITSTRING"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nBOOL"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nBYTE"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nCHAR"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nDINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nDWORD"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nLINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nLREAL"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nLWORD"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nREAL"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nSINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nSTRING"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nUDINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nUINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nULINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nUSINT"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nWORD"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nWSTRING"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nallowedValues"] = true,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nconditionalSupport"] = true,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\ndataTypeIDRef"] = true,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\ndefaultValue"] = true,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}varDeclaration\nunit"] = true,
        ["e:{http://www.canopen.org/xml/1.1}variableRef\ninstanceIDRef"] = true,
        ["e:{http://www.canopen.org/xml/1.1}variableRef\nmemberRef"] = true,
        ["e:{http://www.canopen.org/xml/1.1}variableRef\nvariableIDRef"] = true,
        ["e:{http://www.canopen.org/xml/1.1}vendorText\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}vendorText\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}vendorText\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}vendorText\nlabelRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}versionInfo\ndescription"] = false,
        ["e:{http://www.canopen.org/xml/1.1}versionInfo\ndescriptionRef"] = false,
        ["e:{http://www.canopen.org/xml/1.1}versionInfo\nlabel"] = false,
        ["e:{http://www.canopen.org/xml/1.1}versionInfo\nlabelRef"] = false,
        ["t:ProfileBody_CommunicationNetwork_CANopen\nApplicationLayers"] = false,
        ["t:ProfileBody_CommunicationNetwork_CANopen\nExternalProfileHandle"] = false,
        ["t:ProfileBody_CommunicationNetwork_CANopen\nNetworkManagement"] = false,
        ["t:ProfileBody_CommunicationNetwork_CANopen\nTransportLayers"] = false,
        ["t:ProfileBody_Device_CANopen\nApplicationProcess"] = true,
        ["t:ProfileBody_Device_CANopen\nDeviceFunction"] = true,
        ["t:ProfileBody_Device_CANopen\nDeviceIdentity"] = true,
        ["t:ProfileBody_Device_CANopen\nDeviceManager"] = true,
        ["t:ProfileBody_Device_CANopen\nExternalProfileHandle"] = false,
    };
}
