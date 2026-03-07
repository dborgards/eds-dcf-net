namespace EdsDcfNet.Parsers;

using System.Globalization;
using System.Xml.Linq;
using EdsDcfNet.Models;
using static EdsDcfNet.Parsers.XddParsingPrimitives;

internal static class XddApplicationProcessParser
{
internal static ApplicationProcess? ParseApplicationProcess(XElement profileBody)
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

}
