namespace EdsDcfNet.Writers;

using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using EdsDcfNet.Models;

/// <summary>
/// Builds the XDD <c>ApplicationProcess</c> XML subtree from an <see cref="ApplicationProcess"/> model.
/// All members are purely static; no writer-level state or virtual dispatch is required.
/// </summary>
internal static class XddApplicationProcessBuilder
{
    private static readonly XName ApplicationProcessName =
        XddNames.ChildOfType(XddNames.DeviceProfileBodyType, "ApplicationProcess");

    /// <summary>Builds the top-level <c>ApplicationProcess</c> element.</summary>
    internal static XElement Build(ApplicationProcess ap)
    {
        var elem = new XElement(ApplicationProcessName);

        if (ap.DataTypeList != null && !ap.DataTypeList.IsEmpty)
            elem.Add(BuildDataTypeList(elem.Name, ap.DataTypeList));

        if (ap.FunctionTypeList.Count > 0)
        {
            var ftlElem = XddNames.Element(elem.Name, "functionTypeList");
            foreach (var ft in ap.FunctionTypeList)
                ftlElem.Add(BuildFunctionType(ftlElem.Name, ft));
            elem.Add(ftlElem);
        }

        if (ap.FunctionInstanceList != null)
            elem.Add(BuildFunctionInstanceList(elem.Name, ap.FunctionInstanceList));

        if (ap.TemplateList != null)
            elem.Add(BuildTemplateList(elem.Name, ap.TemplateList));

        // parameterList is mandatory per DS311 §6.4.5 when ApplicationProcess is present
        var plElem = XddNames.Element(elem.Name, "parameterList");
        foreach (var p in ap.ParameterList)
            plElem.Add(BuildParameter(plElem.Name, p));
        elem.Add(plElem);

        if (ap.ParameterGroupList.Count > 0)
        {
            var pglElem = XddNames.Element(elem.Name, "parameterGroupList");
            foreach (var pg in ap.ParameterGroupList)
                pglElem.Add(BuildParameterGroup(pglElem.Name, pg));
            elem.Add(pglElem);
        }

        return elem;
    }

    // ── dataTypeList ──────────────────────────────────────────────────────────

    private static XElement BuildDataTypeList(XName parent, ApDataTypeList list)
    {
        var elem = XddNames.Element(parent, "dataTypeList");

        foreach (var a in list.Arrays)
            elem.Add(BuildArrayType(elem.Name, a));
        foreach (var s in list.Structs)
            elem.Add(BuildStructType(elem.Name, s));
        foreach (var e in list.Enums)
            elem.Add(BuildEnumType(elem.Name, e));
        foreach (var d in list.Derived)
            elem.Add(BuildDerivedType(elem.Name, d));

        return elem;
    }

    private static XElement BuildArrayType(XName parent, ApArrayType array)
    {
        var elem = XddNames.Element(parent, "array",
            new XAttribute("name", array.Name),
            new XAttribute("uniqueID", array.UniqueId));

        ApAddLabelGroup(elem, array.LabelGroup);

        foreach (var sr in array.Subranges)
            elem.Add(XddNames.Element(elem.Name, "subrange",
                new XAttribute("lowerLimit", sr.LowerLimit.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("upperLimit", sr.UpperLimit.ToString(CultureInfo.InvariantCulture))));

        ApAddTypeRef(elem, array.ElementType);
        return elem;
    }

    private static XElement BuildStructType(XName parent, ApStructType st)
    {
        var elem = XddNames.Element(parent, "struct",
            new XAttribute("name", st.Name),
            new XAttribute("uniqueID", st.UniqueId));

        ApAddLabelGroup(elem, st.LabelGroup);

        foreach (var vd in st.VarDeclarations)
            elem.Add(BuildVarDeclaration(elem.Name, vd));

        return elem;
    }

    private static XElement BuildEnumType(XName parent, ApEnumType en)
    {
        var elem = XddNames.Element(parent, "enum",
            new XAttribute("name", en.Name),
            new XAttribute("uniqueID", en.UniqueId));

        if (!string.IsNullOrEmpty(en.Size))
            elem.Add(new XAttribute("size", en.Size));

        ApAddLabelGroup(elem, en.LabelGroup);

        if (!string.IsNullOrEmpty(en.SimpleTypeName))
            elem.Add(new XElement(XddNames.SimpleType(elem.Name, en.SimpleTypeName!)));

        foreach (var ev in en.EnumValues)
        {
            var evElem = XddNames.Element(elem.Name, "enumValue");
            if (ev.Value != null)
                evElem.Add(new XAttribute("value", ev.Value));
            ApAddLabelGroup(evElem, ev.LabelGroup);
            elem.Add(evElem);
        }

        return elem;
    }

    private static XElement BuildDerivedType(XName parent, ApDerivedType dt)
    {
        var elem = XddNames.Element(parent, "derived",
            new XAttribute("name", dt.Name),
            new XAttribute("uniqueID", dt.UniqueId));

        ApAddLabelGroup(elem, dt.LabelGroup);

        if (dt.Count != null)
            elem.Add(BuildDerivedCount(elem.Name, dt.Count));

        ApAddTypeRef(elem, dt.BaseType);
        return elem;
    }

    private static XElement BuildDerivedCount(XName parent, ApDerivedCount c)
    {
        var elem = XddNames.Element(parent, "count",
            new XAttribute("uniqueID", c.UniqueId));

        if (c.Access != "read")
            elem.Add(new XAttribute("access", c.Access));

        ApAddLabelGroup(elem, c.LabelGroup);

        if (c.DefaultValue != null)
            elem.Add(BuildParameterValueElem(elem.Name, "defaultValue", c.DefaultValue));

        if (c.AllowedValues != null)
            elem.Add(BuildAllowedValues(elem.Name, c.AllowedValues));

        return elem;
    }

    private static XElement BuildVarDeclaration(XName parent, ApVarDeclaration vd)
    {
        var elem = XddNames.Element(parent, "varDeclaration",
            new XAttribute("name", vd.Name),
            new XAttribute("uniqueID", vd.UniqueId));

        if (!string.IsNullOrEmpty(vd.Start))
            elem.Add(new XAttribute("start", vd.Start));
        if (!string.IsNullOrEmpty(vd.Size))
            elem.Add(new XAttribute("size", vd.Size));
        if (vd.IsSigned.HasValue)
            elem.Add(new XAttribute("signed", vd.IsSigned.Value ? "true" : "false"));
        if (!string.IsNullOrEmpty(vd.Offset))
            elem.Add(new XAttribute("offset", vd.Offset));
        if (!string.IsNullOrEmpty(vd.Multiplier))
            elem.Add(new XAttribute("multiplier", vd.Multiplier));
        if (!string.IsNullOrEmpty(vd.InitialValue))
            elem.Add(new XAttribute("initialValue", vd.InitialValue));

        ApAddLabelGroup(elem, vd.LabelGroup);
        ApAddTypeRef(elem, vd.Type);

        foreach (var cs in vd.ConditionalSupports)
            elem.Add(XddNames.Element(elem.Name, "conditionalSupport",
                new XAttribute("paramIDRef", cs)));

        if (vd.DefaultValue != null)
            elem.Add(BuildParameterValueElem(elem.Name, "defaultValue", vd.DefaultValue));

        if (vd.AllowedValues != null)
            elem.Add(BuildAllowedValues(elem.Name, vd.AllowedValues));

        if (vd.Unit != null)
            elem.Add(BuildUnit(elem.Name, vd.Unit));

        return elem;
    }

    // ── functionTypeList ──────────────────────────────────────────────────────

    private static XElement BuildFunctionType(XName parent, ApFunctionType ft)
    {
        var elem = XddNames.Element(parent, "functionType",
            new XAttribute("name", ft.Name),
            new XAttribute("uniqueID", ft.UniqueId));

        if (!string.IsNullOrEmpty(ft.Package))
            elem.Add(new XAttribute("package", ft.Package));

        ApAddLabelGroup(elem, ft.LabelGroup);

        foreach (var vi in ft.VersionInfos)
            elem.Add(BuildVersionInfo(elem.Name, vi));

        if (ft.InterfaceList != null)
            elem.Add(BuildInterfaceList(elem.Name, ft.InterfaceList));

        if (ft.FunctionInstanceList != null)
            elem.Add(BuildFunctionInstanceList(elem.Name, ft.FunctionInstanceList));

        return elem;
    }

    private static XElement BuildVersionInfo(XName parent, ApVersionInfo vi)
    {
        var elem = XddNames.Element(parent, "versionInfo",
            new XAttribute("organization", vi.Organization),
            new XAttribute("version", vi.Version),
            new XAttribute("author", vi.Author),
            new XAttribute("date", vi.Date));

        ApAddLabelGroup(elem, vi.LabelGroup);
        return elem;
    }

    private static XElement BuildInterfaceList(XName parent, ApInterfaceList il)
    {
        var elem = XddNames.Element(parent, "interfaceList");

        if (il.InputVars.Count > 0)
        {
            var iv = XddNames.Element(elem.Name, "inputVars");
            foreach (var vd in il.InputVars)
                iv.Add(BuildVarDeclaration(iv.Name, vd));
            elem.Add(iv);
        }

        if (il.OutputVars.Count > 0)
        {
            var ov = XddNames.Element(elem.Name, "outputVars");
            foreach (var vd in il.OutputVars)
                ov.Add(BuildVarDeclaration(ov.Name, vd));
            elem.Add(ov);
        }

        if (il.ConfigVars.Count > 0)
        {
            var cv = XddNames.Element(elem.Name, "configVars");
            foreach (var vd in il.ConfigVars)
                cv.Add(BuildVarDeclaration(cv.Name, vd));
            elem.Add(cv);
        }

        return elem;
    }

    // ── functionInstanceList ──────────────────────────────────────────────────

    private static XElement BuildFunctionInstanceList(XName parent, ApFunctionInstanceList fil)
    {
        var elem = XddNames.Element(parent, "functionInstanceList");

        foreach (var fi in fil.FunctionInstances)
        {
            var fiElem = XddNames.Element(elem.Name, "functionInstance",
                new XAttribute("name", fi.Name),
                new XAttribute("uniqueID", fi.UniqueId),
                new XAttribute("typeIDRef", fi.TypeIdRef));
            ApAddLabelGroup(fiElem, fi.LabelGroup);
            elem.Add(fiElem);
        }

        foreach (var conn in fil.Connections)
        {
            var connElem = XddNames.Element(elem.Name, "connection",
                new XAttribute("source", conn.Source),
                new XAttribute("destination", conn.Destination));
            if (!string.IsNullOrEmpty(conn.Description))
                connElem.Add(new XAttribute("description", conn.Description));
            elem.Add(connElem);
        }

        return elem;
    }

    // ── templateList ──────────────────────────────────────────────────────────

    private static XElement BuildTemplateList(XName parent, ApTemplateList tl)
    {
        var elem = XddNames.Element(parent, "templateList");

        foreach (var pt in tl.ParameterTemplates)
            elem.Add(BuildParameterTemplate(elem.Name, pt));

        foreach (var avt in tl.AllowedValuesTemplates)
        {
            var avtElem = XddNames.Element(elem.Name, "allowedValuesTemplate",
                new XAttribute("uniqueID", avt.UniqueId));
            BuildAllowedValuesContent(avtElem, avt.Values, avt.Ranges);
            elem.Add(avtElem);
        }

        return elem;
    }

    private static XElement BuildParameterTemplate(XName parent, ApParameterTemplate pt)
    {
        var elem = XddNames.Element(parent, "parameterTemplate",
            new XAttribute("uniqueID", pt.UniqueId));

        if (pt.Access != "read")
            elem.Add(new XAttribute("access", pt.Access));
        if (!string.IsNullOrEmpty(pt.AccessList))
            elem.Add(new XAttribute("accessList", pt.AccessList));
        if (!string.IsNullOrEmpty(pt.Support))
            elem.Add(new XAttribute("support", pt.Support));
        if (pt.Persistent)
            elem.Add(new XAttribute("persistent", "true"));
        if (!string.IsNullOrEmpty(pt.Offset))
            elem.Add(new XAttribute("offset", pt.Offset));
        if (!string.IsNullOrEmpty(pt.Multiplier))
            elem.Add(new XAttribute("multiplier", pt.Multiplier));

        ApAddLabelGroup(elem, pt.LabelGroup);
        ApAddTypeRef(elem, pt.TypeRef);

        foreach (var cs in pt.ConditionalSupports)
            elem.Add(XddNames.Element(elem.Name, "conditionalSupport",
                new XAttribute("paramIDRef", cs)));

        if (pt.ActualValue != null)
            elem.Add(BuildParameterValueElem(elem.Name, "actualValue", pt.ActualValue));
        if (pt.DefaultValue != null)
            elem.Add(BuildParameterValueElem(elem.Name, "defaultValue", pt.DefaultValue));
        if (pt.SubstituteValue != null)
            elem.Add(BuildParameterValueElem(elem.Name, "substituteValue", pt.SubstituteValue));
        if (pt.AllowedValues != null)
            elem.Add(BuildAllowedValues(elem.Name, pt.AllowedValues));
        if (pt.Unit != null)
            elem.Add(BuildUnit(elem.Name, pt.Unit));

        foreach (var prop in pt.Properties)
            elem.Add(XddNames.Element(elem.Name, "property",
                new XAttribute("name", prop.Name),
                new XAttribute("value", prop.Value)));

        return elem;
    }

    // ── parameterList ─────────────────────────────────────────────────────────

    private static XElement BuildParameter(XName parent, ApParameter p)
    {
        var elem = XddNames.Element(parent, "parameter",
            new XAttribute("uniqueID", p.UniqueId));

        if (p.Access != "read")
            elem.Add(new XAttribute("access", p.Access));
        if (!string.IsNullOrEmpty(p.AccessList))
            elem.Add(new XAttribute("accessList", p.AccessList));
        if (!string.IsNullOrEmpty(p.Support))
            elem.Add(new XAttribute("support", p.Support));
        if (p.Persistent)
            elem.Add(new XAttribute("persistent", "true"));
        if (!string.IsNullOrEmpty(p.Offset))
            elem.Add(new XAttribute("offset", p.Offset));
        if (!string.IsNullOrEmpty(p.Multiplier))
            elem.Add(new XAttribute("multiplier", p.Multiplier));
        if (!string.IsNullOrEmpty(p.TemplateIdRef))
            elem.Add(new XAttribute("templateIDRef", p.TemplateIdRef));

        ApAddLabelGroup(elem, p.LabelGroup);
        ApAddTypeRef(elem, p.TypeRef);

        foreach (var vr in p.VariableRefs)
            elem.Add(BuildVariableRef(elem.Name, vr));

        foreach (var cs in p.ConditionalSupports)
            elem.Add(XddNames.Element(elem.Name, "conditionalSupport",
                new XAttribute("paramIDRef", cs)));

        if (p.Denotation != null && !p.Denotation.IsEmpty)
        {
            var den = XddNames.Element(elem.Name, "denotation");
            ApAddLabelGroup(den, p.Denotation);
            elem.Add(den);
        }

        if (p.ActualValue != null)
            elem.Add(BuildParameterValueElem(elem.Name, "actualValue", p.ActualValue));
        if (p.DefaultValue != null)
            elem.Add(BuildParameterValueElem(elem.Name, "defaultValue", p.DefaultValue));
        if (p.SubstituteValue != null)
            elem.Add(BuildParameterValueElem(elem.Name, "substituteValue", p.SubstituteValue));
        if (p.AllowedValues != null)
            elem.Add(BuildAllowedValues(elem.Name, p.AllowedValues));
        if (p.Unit != null)
            elem.Add(BuildUnit(elem.Name, p.Unit));

        foreach (var prop in p.Properties)
            elem.Add(XddNames.Element(elem.Name, "property",
                new XAttribute("name", prop.Name),
                new XAttribute("value", prop.Value)));

        return elem;
    }

    private static XElement BuildVariableRef(XName parent, ApVariableRef vr)
    {
        var elem = XddNames.Element(parent, "variableRef");

        if (vr.Position != 1)
            elem.Add(new XAttribute("position",
                vr.Position.ToString(CultureInfo.InvariantCulture)));

        foreach (var iref in vr.InstanceIdRefs)
            elem.Add(XddNames.Element(elem.Name, "instanceIDRef",
                new XAttribute("uniqueIDRef", iref)));

        if (!string.IsNullOrEmpty(vr.VariableIdRef))
            elem.Add(XddNames.Element(elem.Name, "variableIDRef",
                new XAttribute("uniqueIDRef", vr.VariableIdRef)));

        if (vr.MemberRef != null)
        {
            var mrElem = XddNames.Element(elem.Name, "memberRef");
            if (!string.IsNullOrEmpty(vr.MemberRef.UniqueIdRef))
                mrElem.Add(new XAttribute("uniqueIDRef", vr.MemberRef.UniqueIdRef));
            if (vr.MemberRef.Index.HasValue)
                mrElem.Add(new XAttribute("index",
                    vr.MemberRef.Index.Value.ToString(CultureInfo.InvariantCulture)));
            elem.Add(mrElem);
        }

        return elem;
    }

    // ── parameterGroupList ────────────────────────────────────────────────────

    private static XElement BuildParameterGroup(XName parent, ApParameterGroup pg)
    {
        var elem = XddNames.Element(parent, "parameterGroup",
            new XAttribute("uniqueID", pg.UniqueId));

        if (!string.IsNullOrEmpty(pg.KindOfAccess))
            elem.Add(new XAttribute("kindOfAccess", pg.KindOfAccess));

        ApAddLabelGroup(elem, pg.LabelGroup);

        foreach (var pref in pg.ParameterRefs)
            elem.Add(XddNames.Element(elem.Name, "parameterRef",
                new XAttribute("uniqueIDRef", pref)));

        foreach (var sub in pg.SubGroups)
            elem.Add(BuildParameterGroup(elem.Name, sub));

        return elem;
    }

    // ── Shared value element builders ─────────────────────────────────────────

    private static XElement BuildParameterValueElem(XName parent, string name, ApParameterValue pv)
    {
        var elem = XddNames.Element(parent, name,
            new XAttribute("value", pv.Value));

        if (!string.IsNullOrEmpty(pv.Offset))
            elem.Add(new XAttribute("offset", pv.Offset));
        if (!string.IsNullOrEmpty(pv.Multiplier))
            elem.Add(new XAttribute("multiplier", pv.Multiplier));

        ApAddLabelGroup(elem, pv.LabelGroup);
        return elem;
    }

    private static XElement BuildAllowedValues(XName parent, ApAllowedValues av)
    {
        var elem = XddNames.Element(parent, "allowedValues");

        if (!string.IsNullOrEmpty(av.TemplateIdRef))
            elem.Add(new XAttribute("templateIDRef", av.TemplateIdRef));

        BuildAllowedValuesContent(elem, av.Values, av.Ranges);
        return elem;
    }

    private static void BuildAllowedValuesContent(
        XElement elem, List<ApParameterValue> values, List<ApAllowedRange> ranges)
    {
        foreach (var v in values)
            elem.Add(BuildParameterValueElem(elem.Name, "value", v));

        foreach (var r in ranges)
        {
            var rangeElem = XddNames.Element(elem.Name, "range");
            if (r.MinValue != null)
                rangeElem.Add(BuildParameterValueElem(rangeElem.Name, "minValue", r.MinValue));
            if (r.MaxValue != null)
                rangeElem.Add(BuildParameterValueElem(rangeElem.Name, "maxValue", r.MaxValue));
            if (r.Step != null)
                rangeElem.Add(BuildParameterValueElem(rangeElem.Name, "step", r.Step));
            elem.Add(rangeElem);
        }
    }

    private static XElement BuildUnit(XName parent, ApUnit u)
    {
        var elem = XddNames.Element(parent, "unit",
            new XAttribute("multiplier", u.Multiplier));

        if (!string.IsNullOrEmpty(u.UnitUri))
            elem.Add(new XAttribute("unitURI", u.UnitUri));

        ApAddLabelGroup(elem, u.LabelGroup);
        return elem;
    }

    // ── g_labels helpers ──────────────────────────────────────────────────────

    private static void ApAddLabelGroup(XElement elem, ApLabelGroup group)
    {
        if (group.IsEmpty)
            return;

        foreach (var lbl in group.Labels)
            elem.Add(new XElement(XddNames.Label(elem.Name, "label"),
                new XAttribute("lang", lbl.Lang),
                lbl.Text));

        foreach (var desc in group.Descriptions)
        {
            var descElem = new XElement(XddNames.Label(elem.Name, "description"),
                new XAttribute("lang", desc.Lang),
                desc.Text);
            if (!string.IsNullOrEmpty(desc.Uri))
                descElem.Add(new XAttribute("URI", desc.Uri));
            elem.Add(descElem);
        }

        foreach (var tref in group.TextRefs)
        {
            var refName = tref.IsDescriptionRef ? "descriptionRef" : "labelRef";
            var refElem = new XElement(XddNames.Label(elem.Name, refName),
                new XAttribute("dictID", tref.DictId),
                new XAttribute("textID", tref.TextId));
            if (!string.IsNullOrEmpty(tref.Uri))
                refElem.Add(tref.Uri);
            elem.Add(refElem);
        }
    }

    private static void ApAddTypeRef(XElement elem, ApTypeRef? typeRef)
    {
        if (typeRef == null)
            return;

        if (!string.IsNullOrEmpty(typeRef.SimpleTypeName))
            elem.Add(new XElement(XddNames.SimpleType(elem.Name, typeRef.SimpleTypeName!)));
        else if (!string.IsNullOrEmpty(typeRef.DataTypeIdRef))
            elem.Add(XddNames.Element(elem.Name, "dataTypeIDRef",
                new XAttribute("uniqueIDRef", typeRef.DataTypeIdRef)));
    }
}
