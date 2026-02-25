namespace EdsDcfNet.Parsers;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;

#pragma warning disable CA1845, CA1865, CA1866 // span-based and char overloads not available in netstandard2.0
#pragma warning disable CA2249 // string.Contains(string, StringComparison) not available in netstandard2.0; IndexOf is the correct alternative
#pragma warning disable CA1846 // AsSpan not available in netstandard2.0

/// <summary>
/// Reader for CiA 311 XDD (XML Device Description) files.
/// </summary>
public class XddReader
{
    /// <summary>
    /// Reads an XDD file from the specified path.
    /// </summary>
    /// <param name="filePath">Path to the XDD file</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
    /// <exception cref="EdsParseException">Thrown when the XDD content is invalid</exception>
    public ElectronicDataSheet ReadFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"XDD file not found: {filePath}", filePath);

        var content = File.ReadAllText(filePath, Encoding.UTF8);
        return ReadString(content);
    }

    /// <summary>
    /// Reads an XDD from a string.
    /// </summary>
    /// <param name="content">XDD file content as string</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    /// <exception cref="EdsParseException">Thrown when the XDD content is invalid</exception>
    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Public API — instance method for consistency with EdsReader pattern.")]
    public ElectronicDataSheet ReadString(string content)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(content);
        }
        catch (XmlException ex)
        {
            throw new EdsParseException("Failed to parse XDD XML content.", ex);
        }

        return ParseDocument(doc, includeActualValues: false);
    }

    /// <summary>
    /// Parses an XDocument into an ElectronicDataSheet.
    /// </summary>
    /// <param name="doc">The XDocument to parse</param>
    /// <param name="includeActualValues">If true, actualValue attributes are mapped to ParameterValue</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    internal static ElectronicDataSheet ParseDocument(XDocument doc, bool includeActualValues)
    {
        var root = doc.Root;
        if (root == null)
            throw new EdsParseException("XDD document has no root element.");

        var profiles = root.Elements()
            .Where(e => e.Name.LocalName == "ISO15745Profile")
            .ToList();

        XElement? deviceProfileBody = null;
        XElement? commNetProfileBody = null;

        foreach (var profile in profiles)
        {
            var profileBody = profile.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "ProfileBody");
            if (profileBody == null)
                continue;

            var xsiType = GetXsiType(profileBody);
            if (xsiType.IndexOf("ProfileBody_Device_CANopen", StringComparison.OrdinalIgnoreCase) >= 0)
                deviceProfileBody = profileBody;
            else if (xsiType.IndexOf("ProfileBody_CommunicationNetwork_CANopen", StringComparison.OrdinalIgnoreCase) >= 0)
                commNetProfileBody = profileBody;
        }

        if (commNetProfileBody == null)
            throw new EdsParseException("XDD document does not contain a CommunicationNetwork ProfileBody.");

        var eds = new ElectronicDataSheet();

        // Parse FileInfo from device profile body (preferred) or comm-net body
        var fileInfoSource = deviceProfileBody ?? commNetProfileBody;
        eds.FileInfo = ParseFileInfo(fileInfoSource);

        // Parse DeviceIdentity from device profile body
        if (deviceProfileBody != null)
        {
            eds.DeviceInfo = ParseDeviceIdentity(deviceProfileBody);
            eds.ApplicationProcess = ParseApplicationProcess(deviceProfileBody);
        }

        // Parse communication features and object dictionary from comm-net profile body
        ParseCommNetProfile(commNetProfileBody, eds, includeActualValues);

        return eds;
    }

    private static EdsFileInfo ParseFileInfo(XElement profileBody)
    {
        var fileInfo = new EdsFileInfo();

        fileInfo.FileName = profileBody.Attribute("fileName")?.Value ?? string.Empty;
        fileInfo.CreatedBy = profileBody.Attribute("fileCreator")?.Value ?? string.Empty;
        fileInfo.ModifiedBy = profileBody.Attribute("fileModifiedBy")?.Value ?? string.Empty;

        // fileVersion is a string like "1.0" or "1" — map to byte via FileVersion
        var fileVersionStr = profileBody.Attribute("fileVersion")?.Value ?? string.Empty;
        if (!string.IsNullOrEmpty(fileVersionStr))
        {
            // If it looks like "1.0", take the part before the dot
            var dotIdx = fileVersionStr.IndexOf('.');
            var majorPart = dotIdx >= 0 ? fileVersionStr.Substring(0, dotIdx) : fileVersionStr;
            if (byte.TryParse(majorPart, NumberStyles.None, CultureInfo.InvariantCulture, out var ver))
                fileInfo.FileVersion = ver;
        }

        // fileCreationDate is xsd:date "YYYY-MM-DD" → convert to EDS "MM-DD-YYYY"
        var creationDate = profileBody.Attribute("fileCreationDate")?.Value ?? string.Empty;
        fileInfo.CreationDate = ConvertXsdDateToEds(creationDate);

        var creationTime = profileBody.Attribute("fileCreationTime")?.Value ?? string.Empty;
        fileInfo.CreationTime = creationTime;

        var modDate = profileBody.Attribute("fileModificationDate")?.Value ?? string.Empty;
        fileInfo.ModificationDate = ConvertXsdDateToEds(modDate);

        var modTime = profileBody.Attribute("fileModificationTime")?.Value ?? string.Empty;
        fileInfo.ModificationTime = modTime;

        return fileInfo;
    }

    private static DeviceInfo ParseDeviceIdentity(XElement profileBody)
    {
        var deviceInfo = new DeviceInfo();

        var identity = profileBody.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "DeviceIdentity");
        if (identity == null)
            return deviceInfo;

        deviceInfo.VendorName = GetChildText(identity, "vendorName");
        deviceInfo.ProductName = GetChildText(identity, "productName");

        var vendorIdStr = GetChildText(identity, "vendorID");
        if (!string.IsNullOrEmpty(vendorIdStr))
            deviceInfo.VendorNumber = ParseHexId(vendorIdStr);

        var productIdStr = GetChildText(identity, "productID");
        if (!string.IsNullOrEmpty(productIdStr))
            deviceInfo.ProductNumber = ParseHexId(productIdStr);

        return deviceInfo;
    }

    private static ApplicationProcess? ParseApplicationProcess(XElement profileBody)
    {
        var appProcessElem = profileBody.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "ApplicationProcess");
        if (appProcessElem == null)
            return null;

        var ap = new ApplicationProcess();

        foreach (var child in appProcessElem.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "dataTypeList":
                    ap.DataTypeList = ParseDataTypeList(child);
                    break;
                case "functionTypeList":
                    foreach (var ft in child.Elements()
                        .Where(e => e.Name.LocalName == "functionType"))
                        ap.FunctionTypeList.Add(ParseFunctionType(ft));
                    break;
                case "functionInstanceList":
                    ap.FunctionInstanceList = ParseFunctionInstanceList(child);
                    break;
                case "templateList":
                    ap.TemplateList = ParseTemplateList(child);
                    break;
                case "parameterList":
                    foreach (var p in child.Elements()
                        .Where(e => e.Name.LocalName == "parameter"))
                        ap.ParameterList.Add(ParseParameter(p));
                    break;
                case "parameterGroupList":
                    foreach (var pg in child.Elements()
                        .Where(e => e.Name.LocalName == "parameterGroup"))
                        ap.ParameterGroupList.Add(ParseParameterGroup(pg));
                    break;
            }
        }

        return ap;
    }

    // ── dataTypeList ───────────────────────────────────────────────────────────

    private static ApDataTypeList ParseDataTypeList(XElement elem)
    {
        var list = new ApDataTypeList();

        foreach (var child in elem.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "array":
                    list.Arrays.Add(ParseArrayType(child));
                    break;
                case "struct":
                    list.Structs.Add(ParseStructType(child));
                    break;
                case "enum":
                    list.Enums.Add(ParseEnumType(child));
                    break;
                case "derived":
                    list.Derived.Add(ParseDerivedType(child));
                    break;
            }
        }

        return list;
    }

    private static ApArrayType ParseArrayType(XElement elem)
    {
        var array = new ApArrayType
        {
            Name = elem.Attribute("name")?.Value ?? string.Empty,
            UniqueId = elem.Attribute("uniqueID")?.Value ?? string.Empty,
        };

        ApReadLabelGroup(elem, array.LabelGroup);

        foreach (var child in elem.Elements())
        {
            if (child.Name.LocalName == "subrange")
            {
                var sr = new ApSubrange();
                if (long.TryParse(child.Attribute("lowerLimit")?.Value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var lo))
                    sr.LowerLimit = lo;
                if (long.TryParse(child.Attribute("upperLimit")?.Value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var hi))
                    sr.UpperLimit = hi;
                array.Subranges.Add(sr);
            }
        }

        array.ElementType = ParseTypeRef(elem);
        return array;
    }

    private static ApStructType ParseStructType(XElement elem)
    {
        var st = new ApStructType
        {
            Name = elem.Attribute("name")?.Value ?? string.Empty,
            UniqueId = elem.Attribute("uniqueID")?.Value ?? string.Empty,
        };

        ApReadLabelGroup(elem, st.LabelGroup);

        foreach (var child in elem.Elements()
            .Where(e => e.Name.LocalName == "varDeclaration"))
            st.VarDeclarations.Add(ParseVarDeclaration(child));

        return st;
    }

    private static ApEnumType ParseEnumType(XElement elem)
    {
        var en = new ApEnumType
        {
            Name = elem.Attribute("name")?.Value ?? string.Empty,
            UniqueId = elem.Attribute("uniqueID")?.Value ?? string.Empty,
            Size = elem.Attribute("size")?.Value,
        };

        ApReadLabelGroup(elem, en.LabelGroup);

        foreach (var child in elem.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "enumValue":
                    var ev = new ApEnumValue { Value = child.Attribute("value")?.Value };
                    ApReadLabelGroup(child, ev.LabelGroup);
                    en.EnumValues.Add(ev);
                    break;
                default:
                    if (IsSimpleTypeName(child.Name.LocalName))
                        en.SimpleTypeName = child.Name.LocalName;
                    break;
            }
        }

        return en;
    }

    private static ApDerivedType ParseDerivedType(XElement elem)
    {
        var dt = new ApDerivedType
        {
            Name = elem.Attribute("name")?.Value ?? string.Empty,
            UniqueId = elem.Attribute("uniqueID")?.Value ?? string.Empty,
        };

        ApReadLabelGroup(elem, dt.LabelGroup);

        var countElem = elem.Elements().FirstOrDefault(e => e.Name.LocalName == "count");
        if (countElem != null)
            dt.Count = ParseDerivedCount(countElem);

        dt.BaseType = ParseTypeRef(elem);
        return dt;
    }

    private static ApDerivedCount ParseDerivedCount(XElement elem)
    {
        var c = new ApDerivedCount
        {
            UniqueId = elem.Attribute("uniqueID")?.Value ?? string.Empty,
            Access = elem.Attribute("access")?.Value ?? "read",
        };

        ApReadLabelGroup(elem, c.LabelGroup);

        var defVal = elem.Elements().FirstOrDefault(e => e.Name.LocalName == "defaultValue");
        if (defVal != null)
            c.DefaultValue = ParseParameterValue(defVal);

        var av = elem.Elements().FirstOrDefault(e => e.Name.LocalName == "allowedValues");
        if (av != null)
            c.AllowedValues = ParseAllowedValues(av);

        return c;
    }

    private static ApVarDeclaration ParseVarDeclaration(XElement elem)
    {
        var vd = new ApVarDeclaration
        {
            Name = elem.Attribute("name")?.Value ?? string.Empty,
            UniqueId = elem.Attribute("uniqueID")?.Value ?? string.Empty,
            Start = elem.Attribute("start")?.Value,
            Size = elem.Attribute("size")?.Value,
            Offset = elem.Attribute("offset")?.Value,
            Multiplier = elem.Attribute("multiplier")?.Value,
            InitialValue = elem.Attribute("initialValue")?.Value,
        };

        var signedStr = elem.Attribute("signed")?.Value;
        if (signedStr != null)
            vd.IsSigned = signedStr.Equals("true", StringComparison.OrdinalIgnoreCase) || signedStr == "1";

        ApReadLabelGroup(elem, vd.LabelGroup);
        vd.Type = ParseTypeRef(elem);

        foreach (var child in elem.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "conditionalSupport":
                    var paramRef = child.Attribute("paramIDRef")?.Value;
                    if (paramRef != null)
                        vd.ConditionalSupports.Add(paramRef);
                    break;
                case "defaultValue":
                    vd.DefaultValue = ParseParameterValue(child);
                    break;
                case "allowedValues":
                    vd.AllowedValues = ParseAllowedValues(child);
                    break;
                case "unit":
                    vd.Unit = ParseUnit(child);
                    break;
            }
        }

        return vd;
    }

    // ── functionTypeList ───────────────────────────────────────────────────────

    private static ApFunctionType ParseFunctionType(XElement elem)
    {
        var ft = new ApFunctionType
        {
            Name = elem.Attribute("name")?.Value ?? string.Empty,
            UniqueId = elem.Attribute("uniqueID")?.Value ?? string.Empty,
            Package = elem.Attribute("package")?.Value,
        };

        ApReadLabelGroup(elem, ft.LabelGroup);

        foreach (var child in elem.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "versionInfo":
                    ft.VersionInfos.Add(ParseVersionInfo(child));
                    break;
                case "interfaceList":
                    ft.InterfaceList = ParseInterfaceList(child);
                    break;
                case "functionInstanceList":
                    ft.FunctionInstanceList = ParseFunctionInstanceList(child);
                    break;
            }
        }

        return ft;
    }

    private static ApVersionInfo ParseVersionInfo(XElement elem)
    {
        var vi = new ApVersionInfo
        {
            Organization = elem.Attribute("organization")?.Value ?? string.Empty,
            Version = elem.Attribute("version")?.Value ?? string.Empty,
            Author = elem.Attribute("author")?.Value ?? string.Empty,
            Date = elem.Attribute("date")?.Value ?? string.Empty,
        };

        ApReadLabelGroup(elem, vi.LabelGroup);
        return vi;
    }

    private static ApInterfaceList ParseInterfaceList(XElement elem)
    {
        var il = new ApInterfaceList();

        foreach (var child in elem.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "inputVars":
                    foreach (var vd in child.Elements()
                        .Where(e => e.Name.LocalName == "varDeclaration"))
                        il.InputVars.Add(ParseVarDeclaration(vd));
                    break;
                case "outputVars":
                    foreach (var vd in child.Elements()
                        .Where(e => e.Name.LocalName == "varDeclaration"))
                        il.OutputVars.Add(ParseVarDeclaration(vd));
                    break;
                case "configVars":
                    foreach (var vd in child.Elements()
                        .Where(e => e.Name.LocalName == "varDeclaration"))
                        il.ConfigVars.Add(ParseVarDeclaration(vd));
                    break;
            }
        }

        return il;
    }

    // ── functionInstanceList ───────────────────────────────────────────────────

    private static ApFunctionInstanceList ParseFunctionInstanceList(XElement elem)
    {
        var fil = new ApFunctionInstanceList();

        foreach (var child in elem.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "functionInstance":
                    var fi = new ApFunctionInstance
                    {
                        Name = child.Attribute("name")?.Value ?? string.Empty,
                        UniqueId = child.Attribute("uniqueID")?.Value ?? string.Empty,
                        TypeIdRef = child.Attribute("typeIDRef")?.Value ?? string.Empty,
                    };
                    ApReadLabelGroup(child, fi.LabelGroup);
                    fil.FunctionInstances.Add(fi);
                    break;
                case "connection":
                    fil.Connections.Add(new ApConnection
                    {
                        Source = child.Attribute("source")?.Value ?? string.Empty,
                        Destination = child.Attribute("destination")?.Value ?? string.Empty,
                        Description = child.Attribute("description")?.Value,
                    });
                    break;
            }
        }

        return fil;
    }

    // ── templateList ──────────────────────────────────────────────────────────

    private static ApTemplateList ParseTemplateList(XElement elem)
    {
        var tl = new ApTemplateList();

        foreach (var child in elem.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "parameterTemplate":
                    tl.ParameterTemplates.Add(ParseParameterTemplate(child));
                    break;
                case "allowedValuesTemplate":
                    tl.AllowedValuesTemplates.Add(ParseAllowedValuesTemplate(child));
                    break;
            }
        }

        return tl;
    }

    private static ApParameterTemplate ParseParameterTemplate(XElement elem)
    {
        var pt = new ApParameterTemplate
        {
            UniqueId = elem.Attribute("uniqueID")?.Value ?? string.Empty,
            Access = elem.Attribute("access")?.Value ?? "read",
            AccessList = elem.Attribute("accessList")?.Value,
            Support = elem.Attribute("support")?.Value,
            Offset = elem.Attribute("offset")?.Value,
            Multiplier = elem.Attribute("multiplier")?.Value,
        };

        var persistentStr = elem.Attribute("persistent")?.Value;
        if (persistentStr != null)
            pt.Persistent = ParseXmlBool(persistentStr);

        ApReadLabelGroup(elem, pt.LabelGroup);
        pt.TypeRef = ParseTypeRef(elem);

        foreach (var child in elem.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "conditionalSupport":
                    var pref = child.Attribute("paramIDRef")?.Value;
                    if (pref != null)
                        pt.ConditionalSupports.Add(pref);
                    break;
                case "actualValue":
                    pt.ActualValue = ParseParameterValue(child);
                    break;
                case "defaultValue":
                    pt.DefaultValue = ParseParameterValue(child);
                    break;
                case "substituteValue":
                    pt.SubstituteValue = ParseParameterValue(child);
                    break;
                case "allowedValues":
                    pt.AllowedValues = ParseAllowedValues(child);
                    break;
                case "unit":
                    pt.Unit = ParseUnit(child);
                    break;
                case "property":
                    pt.Properties.Add(new ApProperty
                    {
                        Name = child.Attribute("name")?.Value ?? string.Empty,
                        Value = child.Attribute("value")?.Value ?? string.Empty,
                    });
                    break;
            }
        }

        return pt;
    }

    private static ApAllowedValuesTemplate ParseAllowedValuesTemplate(XElement elem)
    {
        var avt = new ApAllowedValuesTemplate
        {
            UniqueId = elem.Attribute("uniqueID")?.Value ?? string.Empty,
        };

        ParseAllowedValuesContent(elem, avt.Values, avt.Ranges);
        return avt;
    }

    // ── parameterList ─────────────────────────────────────────────────────────

    private static ApParameter ParseParameter(XElement elem)
    {
        var p = new ApParameter
        {
            UniqueId = elem.Attribute("uniqueID")?.Value ?? string.Empty,
            Access = elem.Attribute("access")?.Value ?? "read",
            AccessList = elem.Attribute("accessList")?.Value,
            Support = elem.Attribute("support")?.Value,
            Offset = elem.Attribute("offset")?.Value,
            Multiplier = elem.Attribute("multiplier")?.Value,
            TemplateIdRef = elem.Attribute("templateIDRef")?.Value,
        };

        var persistentStr = elem.Attribute("persistent")?.Value;
        if (persistentStr != null)
            p.Persistent = ParseXmlBool(persistentStr);

        ApReadLabelGroup(elem, p.LabelGroup);
        p.TypeRef = ParseTypeRef(elem);

        foreach (var child in elem.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "variableRef":
                    p.VariableRefs.Add(ParseVariableRef(child));
                    break;
                case "conditionalSupport":
                    var pref = child.Attribute("paramIDRef")?.Value;
                    if (pref != null)
                        p.ConditionalSupports.Add(pref);
                    break;
                case "denotation":
                    var den = new ApLabelGroup();
                    ApReadLabelGroup(child, den);
                    p.Denotation = den;
                    break;
                case "actualValue":
                    p.ActualValue = ParseParameterValue(child);
                    break;
                case "defaultValue":
                    p.DefaultValue = ParseParameterValue(child);
                    break;
                case "substituteValue":
                    p.SubstituteValue = ParseParameterValue(child);
                    break;
                case "allowedValues":
                    p.AllowedValues = ParseAllowedValues(child);
                    break;
                case "unit":
                    p.Unit = ParseUnit(child);
                    break;
                case "property":
                    p.Properties.Add(new ApProperty
                    {
                        Name = child.Attribute("name")?.Value ?? string.Empty,
                        Value = child.Attribute("value")?.Value ?? string.Empty,
                    });
                    break;
            }
        }

        return p;
    }

    private static ApVariableRef ParseVariableRef(XElement elem)
    {
        var vr = new ApVariableRef();

        var posStr = elem.Attribute("position")?.Value;
        if (posStr != null &&
            byte.TryParse(posStr, NumberStyles.None,
                CultureInfo.InvariantCulture, out var pos))
            vr.Position = pos;

        foreach (var child in elem.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "instanceIDRef":
                    var iref = child.Attribute("uniqueIDRef")?.Value;
                    if (iref != null)
                        vr.InstanceIdRefs.Add(iref);
                    break;
                case "variableIDRef":
                    vr.VariableIdRef = child.Attribute("uniqueIDRef")?.Value ?? string.Empty;
                    break;
                case "memberRef":
                    vr.MemberRef = new ApMemberRef
                    {
                        UniqueIdRef = child.Attribute("uniqueIDRef")?.Value,
                    };
                    var idxStr = child.Attribute("index")?.Value;
                    if (idxStr != null &&
                        long.TryParse(idxStr, NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out var idx))
                        vr.MemberRef.Index = idx;
                    break;
            }
        }

        return vr;
    }

    // ── parameterGroupList ────────────────────────────────────────────────────

    private static ApParameterGroup ParseParameterGroup(XElement elem)
    {
        var pg = new ApParameterGroup
        {
            UniqueId = elem.Attribute("uniqueID")?.Value ?? string.Empty,
            KindOfAccess = elem.Attribute("kindOfAccess")?.Value,
        };

        ApReadLabelGroup(elem, pg.LabelGroup);

        foreach (var child in elem.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "parameterGroup":
                    pg.SubGroups.Add(ParseParameterGroup(child));
                    break;
                case "parameterRef":
                    var uref = child.Attribute("uniqueIDRef")?.Value;
                    if (uref != null)
                        pg.ParameterRefs.Add(uref);
                    break;
            }
        }

        return pg;
    }

    // ── Shared sub-element parsers ─────────────────────────────────────────────

    private static ApParameterValue ParseParameterValue(XElement elem)
    {
        var pv = new ApParameterValue
        {
            Value = elem.Attribute("value")?.Value ?? string.Empty,
            Offset = elem.Attribute("offset")?.Value,
            Multiplier = elem.Attribute("multiplier")?.Value,
        };

        ApReadLabelGroup(elem, pv.LabelGroup);
        return pv;
    }

    private static ApAllowedValues ParseAllowedValues(XElement elem)
    {
        var av = new ApAllowedValues
        {
            TemplateIdRef = elem.Attribute("templateIDRef")?.Value,
        };

        ParseAllowedValuesContent(elem, av.Values, av.Ranges);
        return av;
    }

    private static void ParseAllowedValuesContent(
        XElement elem, List<ApParameterValue> values, List<ApAllowedRange> ranges)
    {
        foreach (var child in elem.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "value":
                    values.Add(ParseParameterValue(child));
                    break;
                case "range":
                    var range = new ApAllowedRange();
                    var minElem = child.Elements().FirstOrDefault(e => e.Name.LocalName == "minValue");
                    var maxElem = child.Elements().FirstOrDefault(e => e.Name.LocalName == "maxValue");
                    var stepElem = child.Elements().FirstOrDefault(e => e.Name.LocalName == "step");
                    if (minElem != null) range.MinValue = ParseParameterValue(minElem);
                    if (maxElem != null) range.MaxValue = ParseParameterValue(maxElem);
                    if (stepElem != null) range.Step = ParseParameterValue(stepElem);
                    ranges.Add(range);
                    break;
            }
        }
    }

    private static ApUnit ParseUnit(XElement elem)
    {
        var u = new ApUnit
        {
            Multiplier = elem.Attribute("multiplier")?.Value ?? string.Empty,
            UnitUri = elem.Attribute("unitURI")?.Value,
        };

        ApReadLabelGroup(elem, u.LabelGroup);
        return u;
    }

    /// <summary>
    /// Parses a type reference from an element's children:
    /// looks for a g_simple element (simple type name) or a dataTypeIDRef element.
    /// Returns <see langword="null"/> if neither is present.
    /// </summary>
    private static ApTypeRef? ParseTypeRef(XElement elem)
    {
        foreach (var child in elem.Elements())
        {
            if (child.Name.LocalName == "dataTypeIDRef")
            {
                var idRef = child.Attribute("uniqueIDRef")?.Value;
                if (idRef != null)
                    return new ApTypeRef { DataTypeIdRef = idRef };
            }
            else if (IsSimpleTypeName(child.Name.LocalName))
            {
                return new ApTypeRef { SimpleTypeName = child.Name.LocalName };
            }
        }

        return null;
    }

    /// <summary>
    /// Reads label/description/labelRef/descriptionRef elements from <paramref name="elem"/>
    /// into <paramref name="group"/>.
    /// </summary>
    private static void ApReadLabelGroup(XElement elem, ApLabelGroup group)
    {
        foreach (var child in elem.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "label":
                    group.Labels.Add(new ApLabel
                    {
                        Lang = child.Attribute("lang")?.Value ?? string.Empty,
                        Text = child.Value.Trim(),
                    });
                    break;
                case "description":
                    group.Descriptions.Add(new ApDescription
                    {
                        Lang = child.Attribute("lang")?.Value ?? string.Empty,
                        Text = child.Value.Trim(),
                        Uri = child.Attribute("URI")?.Value,
                    });
                    break;
                case "labelRef":
                    group.TextRefs.Add(new ApTextRef
                    {
                        DictId = child.Attribute("dictID")?.Value ?? string.Empty,
                        TextId = child.Attribute("textID")?.Value ?? string.Empty,
                        Uri = child.Value.Trim().Length > 0 ? child.Value.Trim() : null,
                        IsDescriptionRef = false,
                    });
                    break;
                case "descriptionRef":
                    group.TextRefs.Add(new ApTextRef
                    {
                        DictId = child.Attribute("dictID")?.Value ?? string.Empty,
                        TextId = child.Attribute("textID")?.Value ?? string.Empty,
                        Uri = child.Value.Trim().Length > 0 ? child.Value.Trim() : null,
                        IsDescriptionRef = true,
                    });
                    break;
            }
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="name"/> is a known
    /// IEC 61131-3 / CANopen g_simple element name.
    /// </summary>
    private static bool IsSimpleTypeName(string name)
    {
        switch (name)
        {
            case "BOOL":
            case "SINT": case "INT": case "DINT": case "LINT":
            case "USINT": case "UINT": case "UDINT": case "ULINT":
            case "REAL": case "LREAL":
            case "BYTE": case "WORD": case "DWORD": case "LWORD":
            case "STRING": case "WSTRING": case "CHAR": case "WCHAR":
            case "TIME": case "DATE": case "DATE_AND_TIME": case "TIME_OF_DAY":
            case "BITSTRING":
                return true;
            default:
                return false;
        }
    }

    private static void ParseCommNetProfile(XElement profileBody, ElectronicDataSheet eds, bool includeActualValues)
    {
        var appLayers = profileBody.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "ApplicationLayers");

        if (appLayers != null)
        {
            // Object dictionary
            var objList = appLayers.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "CANopenObjectList");
            if (objList != null)
                ParseObjectDictionary(objList, eds.ObjectDictionary, includeActualValues);

            // Dummy usage
            var dummyUsage = appLayers.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "dummyUsage");
            if (dummyUsage != null)
                ParseDummyUsage(dummyUsage, eds.ObjectDictionary);

            // Dynamic channels
            var dynChannels = appLayers.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "dynamicChannels");
            if (dynChannels != null)
            {
                var dc = ParseDynamicChannels(dynChannels);
                if (dc != null && dc.Segments.Count > 0)
                    eds.DynamicChannels = dc;
            }
        }

        var transportLayers = profileBody.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "TransportLayers");
        if (transportLayers != null)
            ParseBaudRates(transportLayers, eds.DeviceInfo);

        var networkMgmt = profileBody.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "NetworkManagement");
        if (networkMgmt != null)
            ParseNetworkManagement(networkMgmt, eds.DeviceInfo);
    }

    private static void ParseObjectDictionary(XElement objList, ObjectDictionary dict, bool includeActualValues)
    {
        // HashSets provide O(1) duplicate detection without the O(n) List.Contains cost.
        var seenMandatory = new HashSet<ushort>();
        var seenOptional = new HashSet<ushort>();
        var seenManufacturer = new HashSet<ushort>();

        foreach (var objElem in objList.Elements().Where(e => e.Name.LocalName == "CANopenObject"))
        {
            var obj = ParseCanOpenObject(objElem, includeActualValues);
            dict.Objects[obj.Index] = obj;

            // Classify object into the right list based on index range
            ClassifyObject(dict, obj.Index, seenMandatory, seenOptional, seenManufacturer);
        }
    }

    private static CanOpenObject ParseCanOpenObject(XElement elem, bool includeActualValues)
    {
        var obj = new CanOpenObject();

        var indexStr = elem.Attribute("index")?.Value ?? "0000";
        obj.Index = ParseHexIndex(indexStr);
        obj.ParameterName = elem.Attribute("name")?.Value ?? string.Empty;

        var objTypeStr = elem.Attribute("objectType")?.Value;
        if (!string.IsNullOrEmpty(objTypeStr) &&
            byte.TryParse(objTypeStr, NumberStyles.None, CultureInfo.InvariantCulture, out var objType))
            obj.ObjectType = objType;
        else
            obj.ObjectType = 0x7;

        if (elem.Attribute("dataType")?.Value is string dataTypeStr)
            obj.DataType = ParseHexDataType(dataTypeStr);

        if (elem.Attribute("accessType")?.Value is string accessStr)
            obj.AccessType = ParseXddAccessType(accessStr);

        obj.DefaultValue = elem.Attribute("defaultValue")?.Value;
        obj.LowLimit = elem.Attribute("lowLimit")?.Value;
        obj.HighLimit = elem.Attribute("highLimit")?.Value;

        var pdoMappingStr = elem.Attribute("PDOmapping")?.Value;
        obj.PdoMapping = ParseXddPdoMapping(pdoMappingStr);

        var objFlagsStr = elem.Attribute("objFlags")?.Value;
        if (!string.IsNullOrEmpty(objFlagsStr) &&
            uint.TryParse(objFlagsStr, NumberStyles.None, CultureInfo.InvariantCulture, out var flags))
            obj.ObjFlags = flags;

        var subNumberStr = elem.Attribute("subNumber")?.Value;
        if (!string.IsNullOrEmpty(subNumberStr) &&
            byte.TryParse(subNumberStr, NumberStyles.None, CultureInfo.InvariantCulture, out var subNum))
            obj.SubNumber = subNum;

        if (includeActualValues)
        {
            if (elem.Attribute("actualValue")?.Value is string actualValue)
                obj.ParameterValue = actualValue;

            if (elem.Attribute("denotation")?.Value is string denotation)
                obj.Denotation = denotation;
        }

        // Parse sub-objects
        foreach (var subElem in elem.Elements().Where(e => e.Name.LocalName == "CANopenSubObject"))
        {
            var subObj = ParseCanOpenSubObject(subElem, includeActualValues);
            obj.SubObjects[subObj.SubIndex] = subObj;
        }

        return obj;
    }

    private static CanOpenSubObject ParseCanOpenSubObject(XElement elem, bool includeActualValues)
    {
        var subObj = new CanOpenSubObject();

        var subIndexStr = elem.Attribute("subIndex")?.Value ?? "00";
        subObj.SubIndex = ParseHexSubIndex(subIndexStr);
        subObj.ParameterName = elem.Attribute("name")?.Value ?? string.Empty;

        var objTypeStr = elem.Attribute("objectType")?.Value;
        if (!string.IsNullOrEmpty(objTypeStr) &&
            byte.TryParse(objTypeStr, NumberStyles.None, CultureInfo.InvariantCulture, out var objType))
            subObj.ObjectType = objType;
        else
            subObj.ObjectType = 0x7;

        if (elem.Attribute("dataType")?.Value is string dataTypeStr)
            subObj.DataType = ParseHexDataType(dataTypeStr);

        if (elem.Attribute("accessType")?.Value is string accessStr)
            subObj.AccessType = ParseXddAccessType(accessStr);

        subObj.DefaultValue = elem.Attribute("defaultValue")?.Value;
        subObj.LowLimit = elem.Attribute("lowLimit")?.Value;
        subObj.HighLimit = elem.Attribute("highLimit")?.Value;

        var pdoMappingStr = elem.Attribute("PDOmapping")?.Value;
        subObj.PdoMapping = ParseXddPdoMapping(pdoMappingStr);

        if (includeActualValues)
        {
            if (elem.Attribute("actualValue")?.Value is string actualValue)
                subObj.ParameterValue = actualValue;

            if (elem.Attribute("denotation")?.Value is string denotation)
                subObj.Denotation = denotation;
        }

        return subObj;
    }

    private static void ClassifyObject(
        ObjectDictionary dict, ushort index,
        HashSet<ushort> seenMandatory, HashSet<ushort> seenOptional, HashSet<ushort> seenManufacturer)
    {
        // Mandatory objects: 1000h and 1001h
        if (index == 0x1000 || index == 0x1001)
        {
            if (seenMandatory.Add(index))
                dict.MandatoryObjects.Add(index);
        }
        // Manufacturer-specific objects: 2000h-5FFFh
        else if (index >= 0x2000 && index <= 0x5FFF)
        {
            if (seenManufacturer.Add(index))
                dict.ManufacturerObjects.Add(index);
        }
        // Everything else goes to optional
        else
        {
            if (seenOptional.Add(index))
                dict.OptionalObjects.Add(index);
        }
    }

    private static void ParseDummyUsage(XElement dummyUsage, ObjectDictionary dict)
    {
        foreach (var dummy in dummyUsage.Elements().Where(e => e.Name.LocalName == "dummy"))
        {
            var entry = dummy.Attribute("entry")?.Value ?? string.Empty;
            // Format: "DummyXXXX=0" or "DummyXXXX=1"
            var eqIdx = entry.IndexOf('=');
            if (eqIdx < 0)
                continue;

            var keyPart = entry.Substring(0, eqIdx).Trim();
            var valPart = entry.Substring(eqIdx + 1).Trim();

            // keyPart must start with "Dummy" followed by 4 hex digits
            if (!keyPart.StartsWith("Dummy", StringComparison.OrdinalIgnoreCase) || keyPart.Length < 9)
                continue;

            var hexPart = keyPart.Substring(5);
            if (!ushort.TryParse(hexPart, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var dummyIndex))
                continue;

            dict.DummyUsage[dummyIndex] = valPart == "1";
        }
    }

    private static DynamicChannels? ParseDynamicChannels(XElement dynChannels)
    {
        var result = new DynamicChannels();

        foreach (var chanElem in dynChannels.Elements().Where(e => e.Name.LocalName == "dynamicChannel"))
        {
            var seg = new DynamicChannelSegment();

            if (chanElem.Attribute("dataType")?.Value is string typeStr)
                seg.Type = ParseHexDataType(typeStr);

            if (chanElem.Attribute("accessType")?.Value is string dirStr)
                seg.Dir = ParseXddAccessType(dirStr);

            seg.Range = chanElem.Attribute("startIndex")?.Value ?? string.Empty;
            var endIdx = chanElem.Attribute("endIndex")?.Value;
            if (!string.IsNullOrEmpty(endIdx) && !string.IsNullOrEmpty(seg.Range))
                seg.Range = seg.Range + "-" + endIdx;

            var ppOffsetStr = chanElem.Attribute("pDOmappingIndex")?.Value;
            if (!string.IsNullOrEmpty(ppOffsetStr) &&
                uint.TryParse(ppOffsetStr, NumberStyles.None, CultureInfo.InvariantCulture, out var ppOffset))
                seg.PPOffset = ppOffset;

            result.Segments.Add(seg);
        }

        return result.Segments.Count > 0 ? result : null;
    }

    private static void ParseBaudRates(XElement transportLayers, DeviceInfo deviceInfo)
    {
        var physLayer = transportLayers.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "PhysicalLayer");
        if (physLayer == null)
            return;

        var baudRate = physLayer.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "baudRate");
        if (baudRate == null)
            return;

        foreach (var supported in baudRate.Elements()
            .Where(e => e.Name.LocalName == "supportedBaudRate"))
        {
            var val = supported.Attribute("value")?.Value ?? string.Empty;
            var kbps = ParseBaudRateString(val);
            SetBaudRate(deviceInfo.SupportedBaudRates, kbps);
        }
    }

    private static void ParseNetworkManagement(XElement networkMgmt, DeviceInfo deviceInfo)
    {
        var generalFeatures = networkMgmt.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "CANopenGeneralFeatures");
        if (generalFeatures != null)
        {
            var granStr = generalFeatures.Attribute("granularity")?.Value;
            if (!string.IsNullOrEmpty(granStr) &&
                byte.TryParse(granStr, NumberStyles.None, CultureInfo.InvariantCulture, out var gran))
                deviceInfo.Granularity = gran;

            var rxPdoStr = generalFeatures.Attribute("nrOfRxPDO")?.Value;
            if (!string.IsNullOrEmpty(rxPdoStr) &&
                ushort.TryParse(rxPdoStr, NumberStyles.None, CultureInfo.InvariantCulture, out var rxPdo))
                deviceInfo.NrOfRxPdo = rxPdo;

            var txPdoStr = generalFeatures.Attribute("nrOfTxPDO")?.Value;
            if (!string.IsNullOrEmpty(txPdoStr) &&
                ushort.TryParse(txPdoStr, NumberStyles.None, CultureInfo.InvariantCulture, out var txPdo))
                deviceInfo.NrOfTxPdo = txPdo;

            if (generalFeatures.Attribute("bootUpSlave")?.Value is string bootUpSlaveStr)
                deviceInfo.SimpleBootUpSlave = ParseXmlBool(bootUpSlaveStr);

            if (generalFeatures.Attribute("groupMessaging")?.Value is string groupMsgStr)
                deviceInfo.GroupMessaging = ParseXmlBool(groupMsgStr);

            if (generalFeatures.Attribute("layerSettingServiceSlave")?.Value is string lssStr)
                deviceInfo.LssSupported = ParseXmlBool(lssStr);

            var dynChanStr = generalFeatures.Attribute("dynamicChannels")?.Value;
            if (!string.IsNullOrEmpty(dynChanStr) &&
                byte.TryParse(dynChanStr, NumberStyles.None, CultureInfo.InvariantCulture, out var dynChan))
                deviceInfo.DynamicChannelsSupported = dynChan;
        }

        var masterFeatures = networkMgmt.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "CANopenMasterFeatures");
        if (masterFeatures != null)
        {
            if (masterFeatures.Attribute("bootUpMaster")?.Value is string bootUpMasterStr)
                deviceInfo.SimpleBootUpMaster = ParseXmlBool(bootUpMasterStr);
        }
    }

    internal static DeviceCommissioning? ParseDeviceCommissioning(XElement networkMgmt)
    {
        var dcElem = networkMgmt.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "deviceCommissioning");
        if (dcElem == null)
            return null;

        var dc = new DeviceCommissioning();

        var nodeIdStr = dcElem.Attribute("nodeID")?.Value ?? string.Empty;
        if (!string.IsNullOrEmpty(nodeIdStr))
        {
            byte nodeIdValue;
            bool parsed;
            if (nodeIdStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                parsed = byte.TryParse(nodeIdStr.Substring(2), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out nodeIdValue);
            else
                parsed = byte.TryParse(nodeIdStr, NumberStyles.None,
                    CultureInfo.InvariantCulture, out nodeIdValue);

            if (parsed)
            {
                if (nodeIdValue < 1 || nodeIdValue > 127)
                    throw new EdsParseException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Invalid nodeID '{0}' (parsed value {1}). CANopen Node-ID must be in range 1..127.",
                            nodeIdStr,
                            nodeIdValue));
                dc.NodeId = nodeIdValue;
            }
            else
            {
                throw new EdsParseException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Invalid nodeID '{0}'. Value cannot be parsed as a CANopen Node-ID (byte).",
                        nodeIdStr));
            }
        }

        dc.NodeName = dcElem.Attribute("nodeName")?.Value ?? string.Empty;

        var baudrateStr = dcElem.Attribute("actualBaudRate")?.Value ?? string.Empty;
        dc.Baudrate = ParseBaudRateString(baudrateStr);

        var netNumberStr = dcElem.Attribute("networkNumber")?.Value ?? string.Empty;
        if (!string.IsNullOrEmpty(netNumberStr) &&
            uint.TryParse(netNumberStr, NumberStyles.None, CultureInfo.InvariantCulture, out var netNum))
            dc.NetNumber = netNum;

        dc.NetworkName = dcElem.Attribute("networkName")?.Value ?? string.Empty;

        if (dcElem.Attribute("CANopenManager")?.Value is string managerStr)
            dc.CANopenManager = ParseXmlBool(managerStr);

        return dc;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    internal static string GetXsiType(XElement element)
    {
        foreach (var attr in element.Attributes())
        {
            if (attr.Name.LocalName == "type")
                return attr.Value;
        }

        return string.Empty;
    }

    private static string GetChildText(XElement parent, string localName)
    {
        var child = parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);
        return child?.Value?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Parses a hex index string (e.g. "1000" or "0x1000") to ushort.
    /// </summary>
    internal static ushort ParseHexIndex(string value)
    {
        var trimmed = value.Trim();
        var hex = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? trimmed.Substring(2) : trimmed;

        if (ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
            return result;

        throw new EdsParseException(
            string.Format(CultureInfo.InvariantCulture,
                "Malformed CANopen object index '{0}'. Expected a 4-digit hex value (e.g. '1000' or '0x1000').",
                value));
    }

    private static byte ParseHexSubIndex(string value)
    {
        var trimmed = value.Trim();
        var hex = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? trimmed.Substring(2) : trimmed;

        if (byte.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
            return result;

        throw new EdsParseException(
            string.Format(CultureInfo.InvariantCulture,
                "Malformed CANopen sub-object subIndex '{0}'. Expected a 2-digit hex value (e.g. '00' or '0x00').",
                value));
    }

    private static ushort ParseHexDataType(string value)
    {
        value = value.Trim();

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            value = value.Substring(2);

        if (ushort.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
            return result;

        return 0;
    }

    private static uint ParseHexId(string value)
    {
        value = value.Trim();

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            value = value.Substring(2);

        if (uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
            return result;

        return 0;
    }

    private static AccessType ParseXddAccessType(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "const" => AccessType.Constant,
            "ro" => AccessType.ReadOnly,
            "wo" => AccessType.WriteOnly,
            "rw" => AccessType.ReadWrite,
            _ => AccessType.ReadOnly
        };
    }

    private static bool ParseXddPdoMapping(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        return !value!.Equals("no", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ParseXmlBool(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.Ordinal);
    }

    internal static ushort ParseBaudRateString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        value = value.Trim();

        if (value.Equals("10 Kbps", StringComparison.OrdinalIgnoreCase)) return 10;
        if (value.Equals("20 Kbps", StringComparison.OrdinalIgnoreCase)) return 20;
        if (value.Equals("50 Kbps", StringComparison.OrdinalIgnoreCase)) return 50;
        if (value.Equals("125 Kbps", StringComparison.OrdinalIgnoreCase)) return 125;
        if (value.Equals("250 Kbps", StringComparison.OrdinalIgnoreCase)) return 250;
        if (value.Equals("500 Kbps", StringComparison.OrdinalIgnoreCase)) return 500;
        if (value.Equals("800 Kbps", StringComparison.OrdinalIgnoreCase)) return 800;
        if (value.Equals("1000 Kbps", StringComparison.OrdinalIgnoreCase)) return 1000;

        return 0;
    }

    private static void SetBaudRate(BaudRates baudRates, ushort kbps)
    {
        switch (kbps)
        {
            case 10: baudRates.BaudRate10 = true; break;
            case 20: baudRates.BaudRate20 = true; break;
            case 50: baudRates.BaudRate50 = true; break;
            case 125: baudRates.BaudRate125 = true; break;
            case 250: baudRates.BaudRate250 = true; break;
            case 500: baudRates.BaudRate500 = true; break;
            case 800: baudRates.BaudRate800 = true; break;
            case 1000: baudRates.BaudRate1000 = true; break;
        }
    }

    private static string ConvertXsdDateToEds(string xsdDate)
    {
        if (string.IsNullOrEmpty(xsdDate))
            return string.Empty;

        // XSD date: "YYYY-MM-DD" → EDS: "MM-DD-YYYY"
        if (xsdDate.Length >= 10 &&
            xsdDate[4] == '-' && xsdDate[7] == '-')
        {
            var year = xsdDate.Substring(0, 4);
            var month = xsdDate.Substring(5, 2);
            var day = xsdDate.Substring(8, 2);
            return string.Format(CultureInfo.InvariantCulture, "{0}-{1}-{2}", month, day, year);
        }

        return xsdDate;
    }
}

#pragma warning restore CA1845, CA1865, CA1866
#pragma warning restore CA2249
#pragma warning restore CA1846
