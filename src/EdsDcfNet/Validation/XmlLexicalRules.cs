namespace EdsDcfNet.Validation;

using System.Globalization;
using System.Xml;
using System.Xml.Schema;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;

/// <summary>Lexical XSD checks for model values emitted by the XDD/XDC writers.</summary>
internal static class XmlLexicalRules
{
    private static readonly HashSet<string> AccessValues = new(StringComparer.Ordinal)
    {
        "const", "read", "write", "readWrite", "readWriteInput", "readWriteOutput", "noAccess",
    };

    private static readonly HashSet<string> CountAccessValues = new(StringComparer.Ordinal)
    {
        "const", "read", "write", "readWrite", "noAccess",
    };

    private static readonly HashSet<string> SupportValues = new(StringComparer.Ordinal)
    {
        "mandatory", "optional", "conditional",
    };

    private static readonly HashSet<string> SimpleTypeNames = new(StringComparer.Ordinal)
    {
        "BOOL", "BITSTRING", "BYTE", "CHAR", "WORD", "DWORD", "LWORD", "SINT", "INT",
        "DINT", "LINT", "USINT", "UINT", "UDINT", "ULINT", "REAL", "LREAL", "STRING", "WSTRING",
    };

    internal static void Apply(
        ApplicationProcess? applicationProcess,
        ObjectDictionary dictionary,
        List<ValidationIssue> issues)
    {
        var resolver = XddUniqueIdResolver.Create(applicationProcess);
        foreach (var entry in dictionary.Objects)
        {
            var path = string.Format(CultureInfo.InvariantCulture, "ObjectDictionary.Objects[0x{0:X4}]", entry.Key);
            if (resolver.TryProject(entry.Value.UniqueIdRef) != null)
                Check(entry.Value.UniqueIdRef, XmlTypeCode.Idref, path + ".UniqueIdRef", issues);
            if (entry.Value.ObjFlagsLexical != null && entry.Value.ObjFlags == entry.Value.ObjFlagsLexicalBaseline)
                Check(entry.Value.ObjFlagsLexical, XmlTypeCode.HexBinary, path + ".ObjFlagsLexical", issues);
            foreach (var subEntry in entry.Value.SubObjects)
            {
                if (resolver.TryProject(subEntry.Value.UniqueIdRef) != null)
                    Check(
                        subEntry.Value.UniqueIdRef,
                        XmlTypeCode.Idref,
                        string.Format(CultureInfo.InvariantCulture, "{0}.SubObjects[0x{1:X2}].UniqueIdRef", path, subEntry.Key),
                        issues);
            }
        }

        if (applicationProcess != null)
            ApplyApplicationProcess(applicationProcess, issues);
    }

    internal static void CheckPreservedNetworkNumber(DeviceCommissioning? commissioning, List<ValidationIssue> issues)
    {
        if (commissioning?.NetworkNumberLexical != null && commissioning.NetNumber == commissioning.NetworkNumberLexicalBaseline)
        {
            Check(
                commissioning.NetworkNumberLexical,
                XmlTypeCode.UnsignedLong,
                "DeviceCommissioning.NetworkNumberLexical",
                issues);
        }
    }

    private static void ApplyApplicationProcess(ApplicationProcess applicationProcess, List<ValidationIssue> issues)
    {
        if (applicationProcess.DataTypeList != null)
            ApplyDataTypes(applicationProcess.DataTypeList, issues);

        for (var i = 0; i < applicationProcess.FunctionTypeList.Count; i++)
        {
            var function = applicationProcess.FunctionTypeList[i];
            var path = Path("ApplicationProcess.FunctionTypeList", i);
            Check(function.UniqueId, XmlTypeCode.Id, path + ".UniqueId", issues);
            ApplyLabels(function.LabelGroup, path + ".LabelGroup", issues);
            for (var j = 0; j < function.VersionInfos.Count; j++)
            {
                var version = function.VersionInfos[j];
                var versionPath = Path(path + ".VersionInfos", j);
                Check(version.Date, XmlTypeCode.Date, versionPath + ".Date", issues);
                ApplyLabels(version.LabelGroup, versionPath + ".LabelGroup", issues);
            }

            if (function.InterfaceList != null)
                ApplyInterface(function.InterfaceList, path + ".InterfaceList", issues);
            if (function.FunctionInstanceList != null)
                ApplyInstances(function.FunctionInstanceList, path + ".FunctionInstanceList", issues);
        }

        if (applicationProcess.FunctionInstanceList != null)
            ApplyInstances(applicationProcess.FunctionInstanceList, "ApplicationProcess.FunctionInstanceList", issues);

        if (applicationProcess.TemplateList != null)
            ApplyTemplates(applicationProcess.TemplateList, issues);

        for (var i = 0; i < applicationProcess.ParameterList.Count; i++)
        {
            var parameter = applicationProcess.ParameterList[i];
            var path = Path("ApplicationProcess.ParameterList", i);
            Check(parameter.UniqueId, XmlTypeCode.Id, path + ".UniqueId", issues);
            CheckEnum(parameter.Access, AccessValues, path + ".Access", issues);
            CheckAccessList(parameter.AccessList, path + ".AccessList", issues);
            CheckOptionalEnum(parameter.Support, SupportValues, path + ".Support", issues);
            Check(parameter.TemplateIdRef, XmlTypeCode.Idref, path + ".TemplateIdRef", issues);
            ApplyTypeRef(parameter.TypeRef, path + ".TypeRef", issues);
            ApplyLabels(parameter.LabelGroup, path + ".LabelGroup", issues);
            if (parameter.Denotation != null)
                ApplyLabels(parameter.Denotation, path + ".Denotation", issues);
            for (var j = 0; j < parameter.VariableRefs.Count; j++)
            {
                var reference = parameter.VariableRefs[j];
                var referencePath = Path(path + ".VariableRefs", j);
                for (var k = 0; k < reference.InstanceIdRefs.Count; k++)
                    Check(reference.InstanceIdRefs[k], XmlTypeCode.Idref, Path(referencePath + ".InstanceIdRefs", k), issues, required: true);
                Check(reference.VariableIdRef, XmlTypeCode.Idref, referencePath + ".VariableIdRef", issues);
                if (reference.MemberRefs.Count > 0)
                {
                    for (var k = 0; k < reference.MemberRefs.Count; k++)
                        Check(reference.MemberRefs[k].UniqueIdRef, XmlTypeCode.Idref,
                            Path(referencePath + ".MemberRefs", k) + ".UniqueIdRef", issues);
                }
                else
                {
                    Check(reference.MemberRef?.UniqueIdRef, XmlTypeCode.Idref,
                        referencePath + ".MemberRef.UniqueIdRef", issues);
                }
            }

            CheckIdRefs(parameter.ConditionalSupports, path + ".ConditionalSupports", issues);
            ApplyValue(parameter.ActualValue, path + ".ActualValue", issues);
            ApplyValue(parameter.DefaultValue, path + ".DefaultValue", issues);
            ApplyValue(parameter.SubstituteValue, path + ".SubstituteValue", issues);
            ApplyAllowedValues(parameter.AllowedValues, path + ".AllowedValues", issues);
            ApplyUnit(parameter.Unit, path + ".Unit", issues);
        }

        for (var i = 0; i < applicationProcess.ParameterGroupList.Count; i++)
            ApplyParameterGroup(applicationProcess.ParameterGroupList[i], Path("ApplicationProcess.ParameterGroupList", i), issues);
    }

    private static void ApplyDataTypes(ApDataTypeList dataTypes, List<ValidationIssue> issues)
    {
        for (var i = 0; i < dataTypes.Arrays.Count; i++)
        {
            var array = dataTypes.Arrays[i];
            var path = Path("ApplicationProcess.DataTypeList.Arrays", i);
            Check(array.UniqueId, XmlTypeCode.Id, path + ".UniqueId", issues);
            ApplyLabels(array.LabelGroup, path + ".LabelGroup", issues);
            ApplyTypeRef(array.ElementType, path + ".ElementType", issues);
        }

        for (var i = 0; i < dataTypes.Structs.Count; i++)
        {
            var structure = dataTypes.Structs[i];
            var path = Path("ApplicationProcess.DataTypeList.Structs", i);
            Check(structure.UniqueId, XmlTypeCode.Id, path + ".UniqueId", issues);
            ApplyLabels(structure.LabelGroup, path + ".LabelGroup", issues);
            ApplyVariables(structure.VarDeclarations, path + ".VarDeclarations", issues);
        }

        for (var i = 0; i < dataTypes.Enums.Count; i++)
        {
            var enumeration = dataTypes.Enums[i];
            var path = Path("ApplicationProcess.DataTypeList.Enums", i);
            Check(enumeration.UniqueId, XmlTypeCode.Id, path + ".UniqueId", issues);
            ApplyLabels(enumeration.LabelGroup, path + ".LabelGroup", issues);
            CheckOptionalEnum(enumeration.SimpleTypeName, SimpleTypeNames, path + ".SimpleTypeName", issues);
            for (var j = 0; j < enumeration.EnumValues.Count; j++)
                ApplyLabels(enumeration.EnumValues[j].LabelGroup, Path(path + ".EnumValues", j) + ".LabelGroup", issues);
        }

        for (var i = 0; i < dataTypes.Derived.Count; i++)
        {
            var derived = dataTypes.Derived[i];
            var path = Path("ApplicationProcess.DataTypeList.Derived", i);
            Check(derived.UniqueId, XmlTypeCode.Id, path + ".UniqueId", issues);
            ApplyLabels(derived.LabelGroup, path + ".LabelGroup", issues);
            ApplyTypeRef(derived.BaseType, path + ".BaseType", issues);
            if (derived.Count == null)
                continue;
            Check(derived.Count.UniqueId, XmlTypeCode.Id, path + ".Count.UniqueId", issues);
            CheckEnum(derived.Count.Access, CountAccessValues, path + ".Count.Access", issues);
            ApplyLabels(derived.Count.LabelGroup, path + ".Count.LabelGroup", issues);
            ApplyValue(derived.Count.DefaultValue, path + ".Count.DefaultValue", issues);
            ApplyAllowedValues(derived.Count.AllowedValues, path + ".Count.AllowedValues", issues);
        }
    }

    private static void ApplyInterface(ApInterfaceList interfaces, string path, List<ValidationIssue> issues)
    {
        ApplyVariables(interfaces.InputVars, path + ".InputVars", issues);
        ApplyVariables(interfaces.OutputVars, path + ".OutputVars", issues);
        ApplyVariables(interfaces.ConfigVars, path + ".ConfigVars", issues);
    }

    private static void ApplyVariables(List<ApVarDeclaration> variables, string path, List<ValidationIssue> issues)
    {
        for (var i = 0; i < variables.Count; i++)
        {
            var variable = variables[i];
            var variablePath = Path(path, i);
            Check(variable.UniqueId, XmlTypeCode.Id, variablePath + ".UniqueId", issues);
            ApplyLabels(variable.LabelGroup, variablePath + ".LabelGroup", issues);
            ApplyTypeRef(variable.Type, variablePath + ".Type", issues);
            CheckIdRefs(variable.ConditionalSupports, variablePath + ".ConditionalSupports", issues);
            ApplyValue(variable.DefaultValue, variablePath + ".DefaultValue", issues);
            ApplyAllowedValues(variable.AllowedValues, variablePath + ".AllowedValues", issues);
            ApplyUnit(variable.Unit, variablePath + ".Unit", issues);
        }
    }

    private static void ApplyInstances(ApFunctionInstanceList instances, string path, List<ValidationIssue> issues)
    {
        for (var i = 0; i < instances.FunctionInstances.Count; i++)
        {
            var instance = instances.FunctionInstances[i];
            var instancePath = Path(path + ".FunctionInstances", i);
            Check(instance.UniqueId, XmlTypeCode.Id, instancePath + ".UniqueId", issues);
            Check(instance.TypeIdRef, XmlTypeCode.Idref, instancePath + ".TypeIdRef", issues, required: true);
            ApplyLabels(instance.LabelGroup, instancePath + ".LabelGroup", issues);
        }
    }

    private static void ApplyTemplates(ApTemplateList templates, List<ValidationIssue> issues)
    {
        for (var i = 0; i < templates.AllowedValuesTemplates.Count; i++)
        {
            var template = templates.AllowedValuesTemplates[i];
            var path = Path("ApplicationProcess.TemplateList.AllowedValuesTemplates", i);
            Check(template.UniqueId, XmlTypeCode.Id, path + ".UniqueId", issues);
            ApplyAllowedValuesContent(template.Values, template.Ranges, path, issues);
        }

        for (var i = 0; i < templates.ParameterTemplates.Count; i++)
        {
            var template = templates.ParameterTemplates[i];
            var path = Path("ApplicationProcess.TemplateList.ParameterTemplates", i);
            Check(template.UniqueId, XmlTypeCode.Id, path + ".UniqueId", issues);
            CheckEnum(template.Access, AccessValues, path + ".Access", issues);
            CheckAccessList(template.AccessList, path + ".AccessList", issues);
            CheckOptionalEnum(template.Support, SupportValues, path + ".Support", issues);
            ApplyTypeRef(template.TypeRef, path + ".TypeRef", issues);
            ApplyLabels(template.LabelGroup, path + ".LabelGroup", issues);
            CheckIdRefs(template.ConditionalSupports, path + ".ConditionalSupports", issues);
            ApplyValue(template.ActualValue, path + ".ActualValue", issues);
            ApplyValue(template.DefaultValue, path + ".DefaultValue", issues);
            ApplyValue(template.SubstituteValue, path + ".SubstituteValue", issues);
            ApplyAllowedValues(template.AllowedValues, path + ".AllowedValues", issues);
            ApplyUnit(template.Unit, path + ".Unit", issues);
        }
    }

    private static void ApplyParameterGroup(ApParameterGroup group, string path, List<ValidationIssue> issues)
    {
        Check(group.UniqueId, XmlTypeCode.Id, path + ".UniqueId", issues);
        ApplyLabels(group.LabelGroup, path + ".LabelGroup", issues);
        CheckIdRefs(group.ParameterRefs, path + ".ParameterRefs", issues);
        for (var i = 0; i < group.SubGroups.Count; i++)
            ApplyParameterGroup(group.SubGroups[i], Path(path + ".SubGroups", i), issues);
    }

    private static void ApplyTypeRef(ApTypeRef? type, string path, List<ValidationIssue> issues)
    {
        if (type == null)
            return;
        CheckOptionalEnum(type.SimpleTypeName, SimpleTypeNames, path + ".SimpleTypeName", issues);
        Check(type.DataTypeIdRef, XmlTypeCode.Idref, path + ".DataTypeIdRef", issues);
    }

    private static void ApplyAllowedValues(ApAllowedValues? allowed, string path, List<ValidationIssue> issues)
    {
        if (allowed == null)
            return;
        Check(allowed.TemplateIdRef, XmlTypeCode.Idref, path + ".TemplateIdRef", issues);
        ApplyAllowedValuesContent(allowed.Values, allowed.Ranges, path, issues);
    }

    private static void ApplyAllowedValuesContent(
        List<ApParameterValue> values,
        List<ApAllowedRange> ranges,
        string path,
        List<ValidationIssue> issues)
    {
        for (var i = 0; i < values.Count; i++)
            ApplyValue(values[i], Path(path + ".Values", i), issues);
        for (var i = 0; i < ranges.Count; i++)
        {
            var rangePath = Path(path + ".Ranges", i);
            ApplyValue(ranges[i].MinValue, rangePath + ".MinValue", issues);
            ApplyValue(ranges[i].MaxValue, rangePath + ".MaxValue", issues);
            ApplyValue(ranges[i].Step, rangePath + ".Step", issues);
        }
    }

    private static void ApplyValue(ApParameterValue? value, string path, List<ValidationIssue> issues)
    {
        if (value != null)
            ApplyLabels(value.LabelGroup, path + ".LabelGroup", issues);
    }

    private static void ApplyUnit(ApUnit? unit, string path, List<ValidationIssue> issues)
    {
        if (unit == null)
            return;
        Check(unit.UnitUri, XmlTypeCode.AnyUri, path + ".UnitUri", issues);
        ApplyLabels(unit.LabelGroup, path + ".LabelGroup", issues);
    }

    private static void ApplyLabels(ApLabelGroup labels, string path, List<ValidationIssue> issues)
    {
        for (var i = 0; i < labels.Labels.Count; i++)
            Check(labels.Labels[i].Lang, XmlTypeCode.Language, Path(path + ".Labels", i) + ".Lang", issues);
        for (var i = 0; i < labels.Descriptions.Count; i++)
        {
            var description = labels.Descriptions[i];
            var descriptionPath = Path(path + ".Descriptions", i);
            Check(description.Lang, XmlTypeCode.Language, descriptionPath + ".Lang", issues);
            Check(description.Uri, XmlTypeCode.AnyUri, descriptionPath + ".Uri", issues);
        }

        for (var i = 0; i < labels.TextRefs.Count; i++)
            Check(labels.TextRefs[i].Uri, XmlTypeCode.AnyUri, Path(path + ".TextRefs", i) + ".Uri", issues);
    }

    private static void CheckIdRefs(List<string> values, string path, List<ValidationIssue> issues)
    {
        for (var i = 0; i < values.Count; i++)
            Check(values[i], XmlTypeCode.Idref, Path(path, i), issues, required: true);
    }

    private static void CheckAccessList(string? value, string path, List<ValidationIssue> issues)
    {
        if (value == null)
            return;
        foreach (var token in value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            Check(token, XmlTypeCode.NmToken, path, issues);
            CheckEnum(token, AccessValues, path, issues);
        }
    }

    private static void CheckOptionalEnum(string? value, HashSet<string> allowed, string path, List<ValidationIssue> issues)
    {
        if (!string.IsNullOrEmpty(value))
            CheckEnum(value, allowed, path, issues);
    }

    private static void CheckEnum(string? value, HashSet<string> allowed, string path, List<ValidationIssue> issues)
    {
        if (value == null)
            return;
        var comparison = ReferenceEquals(allowed, SimpleTypeNames)
            ? value
            : string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (!allowed.Contains(comparison))
            AddIssue(path, value, "one of " + string.Join(", ", allowed), issues);
    }

    private static void Check(
        string? value,
        XmlTypeCode type,
        string path,
        List<ValidationIssue> issues,
        bool required = false)
    {
        if (value == null || (!required && value.Length == 0 && (type == XmlTypeCode.Idref || type == XmlTypeCode.AnyUri)))
            return;
        try
        {
            XmlSchemaType.GetBuiltInSimpleType(type).Datatype!.ParseValue(value, new NameTable(), null);
        }
        catch (Exception exception) when (exception is XmlSchemaException || exception is FormatException || exception is ArgumentException)
        {
            AddIssue(path, value, "xsd:" + type, issues);
        }
    }

    private static void AddIssue(string path, string value, string expected, List<ValidationIssue> issues)
        => issues.Add(new ValidationIssue(
            path,
            string.Format(CultureInfo.InvariantCulture, "Value '{0}' is not lexically valid for {1}.", value, expected),
            ValidationIssueCodes.XddLexicalValueInvalid));

    private static string Path(string path, int index)
        => string.Format(CultureInfo.InvariantCulture, "{0}[{1}]", path, index);
}
