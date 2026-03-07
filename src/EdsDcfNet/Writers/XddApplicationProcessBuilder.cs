namespace EdsDcfNet.Writers;

using System.Globalization;
using System.Xml.Linq;
using EdsDcfNet.Models;

/// <summary>
/// Builds the XDD <c>ApplicationProcess</c> XML subtree from an <see cref="ApplicationProcess"/> model.
/// All members are purely static; no writer-level state or virtual dispatch is required.
/// </summary>
internal static class XddApplicationProcessBuilder
{
    /// <summary>Builds the top-level <c>ApplicationProcess</c> element.</summary>
    internal static XElement Build(ApplicationProcess ap)
    {
        var elem = new XElement("ApplicationProcess");

        if (ap.DataTypeList != null && !ap.DataTypeList.IsEmpty)
            elem.Add(BuildDataTypeList(ap.DataTypeList));

        if (ap.FunctionTypeList.Count > 0)
        {
            var ftlElem = new XElement("functionTypeList");
            foreach (var ft in ap.FunctionTypeList)
                ftlElem.Add(BuildFunctionType(ft));
            elem.Add(ftlElem);
        }

        if (ap.FunctionInstanceList != null)
            elem.Add(BuildFunctionInstanceList(ap.FunctionInstanceList));

        if (ap.TemplateList != null)
            elem.Add(BuildTemplateList(ap.TemplateList));

        // parameterList is mandatory per DS311 §6.4.5 when ApplicationProcess is present
        var plElem = new XElement("parameterList");
        foreach (var p in ap.ParameterList)
            plElem.Add(BuildParameter(p));
        elem.Add(plElem);

        if (ap.ParameterGroupList.Count > 0)
        {
            var pglElem = new XElement("parameterGroupList");
            foreach (var pg in ap.ParameterGroupList)
                pglElem.Add(BuildParameterGroup(pg));
            elem.Add(pglElem);
        }

        return elem;
    }

    // ── dataTypeList ──────────────────────────────────────────────────────────

    private static XElement BuildDataTypeList(ApDataTypeList list)
    {
        var elem = new XElement("dataTypeList");

        foreach (var a in list.Arrays)
            elem.Add(BuildArrayType(a));
        foreach (var s in list.Structs)
            elem.Add(BuildStructType(s));
        foreach (var e in list.Enums)
            elem.Add(BuildEnumType(e));
        foreach (var d in list.Derived)
            elem.Add(BuildDerivedType(d));

        return elem;
    }

    private static XElement BuildArrayType(ApArrayType array)
    {
        var elem = new XElement("array",
            new XAttribute("name", array.Name),
            new XAttribute("uniqueID", array.UniqueId));

        ApAddLabelGroup(elem, array.LabelGroup);

        foreach (var sr in array.Subranges)
            elem.Add(new XElement("subrange",
                new XAttribute("lowerLimit", sr.LowerLimit.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("upperLimit", sr.UpperLimit.ToString(CultureInfo.InvariantCulture))));

        ApAddTypeRef(elem, array.ElementType);
        return elem;
    }

    private static XElement BuildStructType(ApStructType st)
    {
        var elem = new XElement("struct",
            new XAttribute("name", st.Name),
            new XAttribute("uniqueID", st.UniqueId));

        ApAddLabelGroup(elem, st.LabelGroup);

        foreach (var vd in st.VarDeclarations)
            elem.Add(BuildVarDeclaration(vd));

        return elem;
    }

    private static XElement BuildEnumType(ApEnumType en)
    {
        var elem = new XElement("enum",
            new XAttribute("name", en.Name),
            new XAttribute("uniqueID", en.UniqueId));

        if (!string.IsNullOrEmpty(en.Size))
            elem.Add(new XAttribute("size", en.Size));

        ApAddLabelGroup(elem, en.LabelGroup);

        if (!string.IsNullOrEmpty(en.SimpleTypeName))
            elem.Add(new XElement(en.SimpleTypeName));

        foreach (var ev in en.EnumValues)
        {
            var evElem = new XElement("enumValue");
            if (ev.Value != null)
                evElem.Add(new XAttribute("value", ev.Value));
            ApAddLabelGroup(evElem, ev.LabelGroup);
            elem.Add(evElem);
        }

        return elem;
    }

    private static XElement BuildDerivedType(ApDerivedType dt)
    {
        var elem = new XElement("derived",
            new XAttribute("name", dt.Name),
            new XAttribute("uniqueID", dt.UniqueId));

        ApAddLabelGroup(elem, dt.LabelGroup);

        if (dt.Count != null)
            elem.Add(BuildDerivedCount(dt.Count));

        ApAddTypeRef(elem, dt.BaseType);
        return elem;
    }

    private static XElement BuildDerivedCount(ApDerivedCount c)
    {
        var elem = new XElement("count",
            new XAttribute("uniqueID", c.UniqueId));

        if (c.Access != "read")
            elem.Add(new XAttribute("access", c.Access));

        ApAddLabelGroup(elem, c.LabelGroup);

        if (c.DefaultValue != null)
            elem.Add(BuildParameterValueElem("defaultValue", c.DefaultValue));

        if (c.AllowedValues != null)
            elem.Add(BuildAllowedValues(c.AllowedValues));

        return elem;
    }

    private static XElement BuildVarDeclaration(ApVarDeclaration vd)
    {
        var elem = new XElement("varDeclaration",
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
            elem.Add(new XElement("conditionalSupport",
                new XAttribute("paramIDRef", cs)));

        if (vd.DefaultValue != null)
            elem.Add(BuildParameterValueElem("defaultValue", vd.DefaultValue));

        if (vd.AllowedValues != null)
            elem.Add(BuildAllowedValues(vd.AllowedValues));

        if (vd.Unit != null)
            elem.Add(BuildUnit(vd.Unit));

        return elem;
    }

    // ── functionTypeList ──────────────────────────────────────────────────────

    private static XElement BuildFunctionType(ApFunctionType ft)
    {
        var elem = new XElement("functionType",
            new XAttribute("name", ft.Name),
            new XAttribute("uniqueID", ft.UniqueId));

        if (!string.IsNullOrEmpty(ft.Package))
            elem.Add(new XAttribute("package", ft.Package));

        ApAddLabelGroup(elem, ft.LabelGroup);

        foreach (var vi in ft.VersionInfos)
            elem.Add(BuildVersionInfo(vi));

        if (ft.InterfaceList != null)
            elem.Add(BuildInterfaceList(ft.InterfaceList));

        if (ft.FunctionInstanceList != null)
            elem.Add(BuildFunctionInstanceList(ft.FunctionInstanceList));

        return elem;
    }

    private static XElement BuildVersionInfo(ApVersionInfo vi)
    {
        var elem = new XElement("versionInfo",
            new XAttribute("organization", vi.Organization),
            new XAttribute("version", vi.Version),
            new XAttribute("author", vi.Author),
            new XAttribute("date", vi.Date));

        ApAddLabelGroup(elem, vi.LabelGroup);
        return elem;
    }

    private static XElement BuildInterfaceList(ApInterfaceList il)
    {
        var elem = new XElement("interfaceList");

        if (il.InputVars.Count > 0)
        {
            var iv = new XElement("inputVars");
            foreach (var vd in il.InputVars)
                iv.Add(BuildVarDeclaration(vd));
            elem.Add(iv);
        }

        if (il.OutputVars.Count > 0)
        {
            var ov = new XElement("outputVars");
            foreach (var vd in il.OutputVars)
                ov.Add(BuildVarDeclaration(vd));
            elem.Add(ov);
        }

        if (il.ConfigVars.Count > 0)
        {
            var cv = new XElement("configVars");
            foreach (var vd in il.ConfigVars)
                cv.Add(BuildVarDeclaration(vd));
            elem.Add(cv);
        }

        return elem;
    }

    // ── functionInstanceList ──────────────────────────────────────────────────

    private static XElement BuildFunctionInstanceList(ApFunctionInstanceList fil)
    {
        var elem = new XElement("functionInstanceList");

        foreach (var fi in fil.FunctionInstances)
        {
            var fiElem = new XElement("functionInstance",
                new XAttribute("name", fi.Name),
                new XAttribute("uniqueID", fi.UniqueId),
                new XAttribute("typeIDRef", fi.TypeIdRef));
            ApAddLabelGroup(fiElem, fi.LabelGroup);
            elem.Add(fiElem);
        }

        foreach (var conn in fil.Connections)
        {
            var connElem = new XElement("connection",
                new XAttribute("source", conn.Source),
                new XAttribute("destination", conn.Destination));
            if (!string.IsNullOrEmpty(conn.Description))
                connElem.Add(new XAttribute("description", conn.Description));
            elem.Add(connElem);
        }

        return elem;
    }

    // ── templateList ──────────────────────────────────────────────────────────

    private static XElement BuildTemplateList(ApTemplateList tl)
    {
        var elem = new XElement("templateList");

        foreach (var pt in tl.ParameterTemplates)
            elem.Add(BuildParameterTemplate(pt));

        foreach (var avt in tl.AllowedValuesTemplates)
        {
            var avtElem = new XElement("allowedValuesTemplate",
                new XAttribute("uniqueID", avt.UniqueId));
            BuildAllowedValuesContent(avtElem, avt.Values, avt.Ranges);
            elem.Add(avtElem);
        }

        return elem;
    }

    private static XElement BuildParameterTemplate(ApParameterTemplate pt)
    {
        var elem = new XElement("parameterTemplate",
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
            elem.Add(new XElement("conditionalSupport",
                new XAttribute("paramIDRef", cs)));

        if (pt.ActualValue != null)
            elem.Add(BuildParameterValueElem("actualValue", pt.ActualValue));
        if (pt.DefaultValue != null)
            elem.Add(BuildParameterValueElem("defaultValue", pt.DefaultValue));
        if (pt.SubstituteValue != null)
            elem.Add(BuildParameterValueElem("substituteValue", pt.SubstituteValue));
        if (pt.AllowedValues != null)
            elem.Add(BuildAllowedValues(pt.AllowedValues));
        if (pt.Unit != null)
            elem.Add(BuildUnit(pt.Unit));

        foreach (var prop in pt.Properties)
            elem.Add(new XElement("property",
                new XAttribute("name", prop.Name),
                new XAttribute("value", prop.Value)));

        return elem;
    }

    // ── parameterList ─────────────────────────────────────────────────────────

    private static XElement BuildParameter(ApParameter p)
    {
        var elem = new XElement("parameter",
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
            elem.Add(BuildVariableRef(vr));

        foreach (var cs in p.ConditionalSupports)
            elem.Add(new XElement("conditionalSupport",
                new XAttribute("paramIDRef", cs)));

        if (p.Denotation != null && !p.Denotation.IsEmpty)
        {
            var den = new XElement("denotation");
            ApAddLabelGroup(den, p.Denotation);
            elem.Add(den);
        }

        if (p.ActualValue != null)
            elem.Add(BuildParameterValueElem("actualValue", p.ActualValue));
        if (p.DefaultValue != null)
            elem.Add(BuildParameterValueElem("defaultValue", p.DefaultValue));
        if (p.SubstituteValue != null)
            elem.Add(BuildParameterValueElem("substituteValue", p.SubstituteValue));
        if (p.AllowedValues != null)
            elem.Add(BuildAllowedValues(p.AllowedValues));
        if (p.Unit != null)
            elem.Add(BuildUnit(p.Unit));

        foreach (var prop in p.Properties)
            elem.Add(new XElement("property",
                new XAttribute("name", prop.Name),
                new XAttribute("value", prop.Value)));

        return elem;
    }

    private static XElement BuildVariableRef(ApVariableRef vr)
    {
        var elem = new XElement("variableRef");

        if (vr.Position != 1)
            elem.Add(new XAttribute("position",
                vr.Position.ToString(CultureInfo.InvariantCulture)));

        foreach (var iref in vr.InstanceIdRefs)
            elem.Add(new XElement("instanceIDRef",
                new XAttribute("uniqueIDRef", iref)));

        if (!string.IsNullOrEmpty(vr.VariableIdRef))
            elem.Add(new XElement("variableIDRef",
                new XAttribute("uniqueIDRef", vr.VariableIdRef)));

        if (vr.MemberRef != null)
        {
            var mrElem = new XElement("memberRef");
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

    private static XElement BuildParameterGroup(ApParameterGroup pg)
    {
        var elem = new XElement("parameterGroup",
            new XAttribute("uniqueID", pg.UniqueId));

        if (!string.IsNullOrEmpty(pg.KindOfAccess))
            elem.Add(new XAttribute("kindOfAccess", pg.KindOfAccess));

        ApAddLabelGroup(elem, pg.LabelGroup);

        foreach (var pref in pg.ParameterRefs)
            elem.Add(new XElement("parameterRef",
                new XAttribute("uniqueIDRef", pref)));

        foreach (var sub in pg.SubGroups)
            elem.Add(BuildParameterGroup(sub));

        return elem;
    }

    // ── Shared value element builders ─────────────────────────────────────────

    private static XElement BuildParameterValueElem(string name, ApParameterValue pv)
    {
        var elem = new XElement(name,
            new XAttribute("value", pv.Value));

        if (!string.IsNullOrEmpty(pv.Offset))
            elem.Add(new XAttribute("offset", pv.Offset));
        if (!string.IsNullOrEmpty(pv.Multiplier))
            elem.Add(new XAttribute("multiplier", pv.Multiplier));

        ApAddLabelGroup(elem, pv.LabelGroup);
        return elem;
    }

    private static XElement BuildAllowedValues(ApAllowedValues av)
    {
        var elem = new XElement("allowedValues");

        if (!string.IsNullOrEmpty(av.TemplateIdRef))
            elem.Add(new XAttribute("templateIDRef", av.TemplateIdRef));

        BuildAllowedValuesContent(elem, av.Values, av.Ranges);
        return elem;
    }

    private static void BuildAllowedValuesContent(
        XElement elem, List<ApParameterValue> values, List<ApAllowedRange> ranges)
    {
        foreach (var v in values)
            elem.Add(BuildParameterValueElem("value", v));

        foreach (var r in ranges)
        {
            var rangeElem = new XElement("range");
            if (r.MinValue != null)
                rangeElem.Add(BuildParameterValueElem("minValue", r.MinValue));
            if (r.MaxValue != null)
                rangeElem.Add(BuildParameterValueElem("maxValue", r.MaxValue));
            if (r.Step != null)
                rangeElem.Add(BuildParameterValueElem("step", r.Step));
            elem.Add(rangeElem);
        }
    }

    private static XElement BuildUnit(ApUnit u)
    {
        var elem = new XElement("unit",
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
            elem.Add(new XElement("label",
                new XAttribute("lang", lbl.Lang),
                lbl.Text));

        foreach (var desc in group.Descriptions)
        {
            var descElem = new XElement("description",
                new XAttribute("lang", desc.Lang),
                desc.Text);
            if (!string.IsNullOrEmpty(desc.Uri))
                descElem.Add(new XAttribute("URI", desc.Uri));
            elem.Add(descElem);
        }

        foreach (var tref in group.TextRefs)
        {
            var refName = tref.IsDescriptionRef ? "descriptionRef" : "labelRef";
            var refElem = new XElement(refName,
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
            elem.Add(new XElement(typeRef.SimpleTypeName));
        else if (!string.IsNullOrEmpty(typeRef.DataTypeIdRef))
            elem.Add(new XElement("dataTypeIDRef",
                new XAttribute("uniqueIDRef", typeRef.DataTypeIdRef)));
    }
}
