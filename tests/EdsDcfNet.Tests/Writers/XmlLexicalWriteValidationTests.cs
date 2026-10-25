namespace EdsDcfNet.Tests.Writers;

using EdsDcfNet.Models;
using EdsDcfNet.Validation;

public class XmlLexicalWriteValidationTests
{
    [Fact]
    public void ValidatedWrite_CompleteApplicationProcess_AcceptsLexicalValues()
    {
        var eds = ValidEds();
        eds.ApplicationProcess = CompleteApplicationProcess();

        var issues = Validate(eds);

        issues.Where(issue => issue.Code == ValidationIssueCodes.XddLexicalValueInvalid).Should().BeEmpty();
    }

    [Theory]
    [InlineData("id")]
    [InlineData("idref")]
    [InlineData("language")]
    [InlineData("anyUri")]
    [InlineData("access")]
    [InlineData("accessList")]
    [InlineData("support")]
    [InlineData("simpleType")]
    [InlineData("date")]
    public void ValidatedWrite_InvalidApplicationProcessLexemes_AreReported(string field)
    {
        var eds = ValidEds();
        var parameter = new ApParameter { UniqueId = "P" };
        parameter.LabelGroup.Labels.Add(new ApLabel { Lang = "en", Text = "parameter" });
        var function = new ApFunctionType { UniqueId = "F" };
        function.VersionInfos.Add(new ApVersionInfo { Date = "2026-01-01" });
        eds.ApplicationProcess = new ApplicationProcess();
        eds.ApplicationProcess.ParameterList.Add(parameter);
        eds.ApplicationProcess.FunctionTypeList.Add(function);
        var expectedPath = field switch
        {
            "id" => "ApplicationProcess.ParameterList[0].UniqueId",
            "idref" => "ApplicationProcess.ParameterList[0].TemplateIdRef",
            "language" => "ApplicationProcess.ParameterList[0].LabelGroup.Labels[0].Lang",
            "anyUri" => "ApplicationProcess.ParameterList[0].Unit.UnitUri",
            "access" => "ApplicationProcess.ParameterList[0].Access",
            "accessList" => "ApplicationProcess.ParameterList[0].AccessList",
            "support" => "ApplicationProcess.ParameterList[0].Support",
            "simpleType" => "ApplicationProcess.ParameterList[0].TypeRef.SimpleTypeName",
            _ => "ApplicationProcess.FunctionTypeList[0].VersionInfos[0].Date",
        };
        switch (field)
        {
            case "id": parameter.UniqueId = "1 invalid"; break;
            case "idref": parameter.TemplateIdRef = "not valid"; break;
            case "language": parameter.LabelGroup.Labels[0].Lang = "not a language"; break;
            case "anyUri": parameter.Unit = new ApUnit { UnitUri = "http://[::1" }; break;
            case "access": parameter.Access = "rw"; break;
            case "accessList": parameter.AccessList = "read invalid"; break;
            case "support": parameter.Support = "required"; break;
            case "simpleType": parameter.TypeRef = new ApTypeRef { SimpleTypeName = "INTEGER" }; break;
            default: function.VersionInfos[0].Date = "2026-02-30"; break;
        }

        var issues = Validate(eds);

        issues.Should().Contain(issue => issue.Code == ValidationIssueCodes.XddLexicalValueInvalid
            && issue.Path == expectedPath);
    }

    [Fact]
    public void ValidatedWrite_TemplateAllowedValuesAndEveryMemberRef_AreReported()
    {
        var eds = ValidEds();
        eds.ApplicationProcess = CompleteApplicationProcess();
        eds.ApplicationProcess.TemplateList!.ParameterTemplates[0].AllowedValues!.TemplateIdRef = "template invalid";
        var parameter = eds.ApplicationProcess.ParameterList[0];
        parameter.VariableRefs[0].MemberRef!.UniqueIdRef = "first invalid";
        var second = new ApVariableRef
        {
            VariableIdRef = "V2",
            MemberRef = new ApMemberRef { UniqueIdRef = "second invalid" },
        };
        second.InstanceIdRefs.Add("I");
        second.MemberRefs.Add(new ApMemberRef { UniqueIdRef = "last valid" });
        parameter.VariableRefs.Add(second);

        var issues = Validate(eds);

        issues.Where(issue => issue.Code == ValidationIssueCodes.XddLexicalValueInvalid)
            .Select(issue => issue.Path)
            .Should().Contain(new[]
            {
                "ApplicationProcess.TemplateList.ParameterTemplates[0].AllowedValues.TemplateIdRef",
                "ApplicationProcess.ParameterList[0].VariableRefs[0].MemberRef.UniqueIdRef",
                "ApplicationProcess.ParameterList[0].VariableRefs[1].MemberRefs[0].UniqueIdRef",
            });
    }

    [Fact]
    public void ValidatedWrite_ObjectAndPreservedCommissioningLexemes_AreReported()
    {
        var dcf = CanOpenFile.Eds.ConvertToDcf(ValidEds(), nodeId: 5, timestamp: new DateTime(2026, 1, 1));
        dcf.ObjectDictionary.Objects[0x2000].UniqueIdRef = "not valid";
        dcf.ObjectDictionary.Objects[0x2000].ObjFlagsLexical = "not-hex";
        dcf.ObjectDictionary.Objects[0x2000].ObjFlagsLexicalBaseline = 0;
        dcf.ObjectDictionary.Objects[0x2000].SubObjects[0] = new CanOpenSubObject
        {
            SubIndex = 0,
            ParameterName = "sub",
            ObjectType = 7,
            DataType = 7,
            UniqueIdRef = "also invalid",
        };
        dcf.DeviceCommissioning.NetworkNumberLexical = "not-a-number";
        dcf.DeviceCommissioning.NetNumber = 0;
        dcf.DeviceCommissioning.NetworkNumberLexicalBaseline = 0;

        var issues = new List<ValidationIssue>();
        XmlWriteRules.Apply(dcf, issues);

        var lexicalPaths = issues.Where(issue => issue.Code == ValidationIssueCodes.XddLexicalValueInvalid)
            .Select(issue => issue.Path).ToList();
        lexicalPaths.Should().Contain(new[]
        {
            "ObjectDictionary.Objects[0x2000].ObjFlagsLexical",
            "DeviceCommissioning.NetworkNumberLexical",
        });
        lexicalPaths.Should().NotContain("ObjectDictionary.Objects[0x2000].UniqueIdRef");
        lexicalPaths.Should().NotContain("ObjectDictionary.Objects[0x2000].SubObjects[0x00].UniqueIdRef");
    }

    [Fact]
    public void ValidatedWrite_EmittedObjectReferences_AreValidated()
    {
        var eds = ValidEds();
        eds.ApplicationProcess = new ApplicationProcess();
        eds.ApplicationProcess.ParameterList.Add(new ApParameter { UniqueId = "not valid" });
        eds.ApplicationProcess.ParameterList.Add(new ApParameter { UniqueId = "also invalid" });
        var obj = eds.ObjectDictionary.Objects[0x2000];
        obj.UniqueIdRef = "not valid";
        obj.SubObjects[0] = new CanOpenSubObject
        {
            SubIndex = 0,
            ParameterName = "sub",
            ObjectType = 7,
            DataType = 7,
            UniqueIdRef = "also invalid",
        };

        var paths = Validate(eds).Where(issue => issue.Code == ValidationIssueCodes.XddLexicalValueInvalid)
            .Select(issue => issue.Path);

        paths.Should().Contain("ObjectDictionary.Objects[0x2000].UniqueIdRef");
        paths.Should().Contain("ObjectDictionary.Objects[0x2000].SubObjects[0x00].UniqueIdRef");
    }

    [Fact]
    public void ValidatedWrite_WhitespaceOnlyAccessLists_AreReported()
    {
        var eds = ValidEds();
        eds.ApplicationProcess = CompleteApplicationProcess();
        eds.ApplicationProcess.ParameterList[0].AccessList = "   ";
        eds.ApplicationProcess.TemplateList!.ParameterTemplates[0].AccessList = "\t ";

        var paths = Validate(eds).Where(issue => issue.Code == ValidationIssueCodes.XddLexicalValueInvalid)
            .Select(issue => issue.Path);

        paths.Should().Contain("ApplicationProcess.ParameterList[0].AccessList");
        paths.Should().Contain("ApplicationProcess.TemplateList.ParameterTemplates[0].AccessList");
    }

    [Fact]
    public void ValidatedWrite_OptionalEmptyValuesOmittedByWriter_AreAccepted()
    {
        var eds = ValidEds();
        eds.ApplicationProcess = CompleteApplicationProcess();
        eds.ObjectDictionary.Objects[0x2000].UniqueIdRef = string.Empty;
        var parameter = eds.ApplicationProcess.ParameterList[0];
        parameter.Support = string.Empty;
        parameter.AccessList = string.Empty;
        parameter.TemplateIdRef = string.Empty;
        parameter.TypeRef = new ApTypeRef { DataTypeIdRef = string.Empty };
        parameter.VariableRefs[0].VariableIdRef = string.Empty;
        parameter.VariableRefs[0].MemberRef!.UniqueIdRef = string.Empty;
        parameter.AllowedValues!.TemplateIdRef = string.Empty;
        parameter.Unit!.UnitUri = string.Empty;
        parameter.LabelGroup.Descriptions[0].Uri = string.Empty;
        eds.ApplicationProcess.TemplateList!.ParameterTemplates[0].Support = string.Empty;
        eds.ApplicationProcess.TemplateList.ParameterTemplates[0].AccessList = string.Empty;
        eds.ApplicationProcess.DataTypeList!.Enums[0].SimpleTypeName = string.Empty;

        var issues = Validate(eds);

        issues.Where(issue => issue.Code == ValidationIssueCodes.XddLexicalValueInvalid).Should().BeEmpty();
    }

    [Fact]
    public void ValidatedWrite_NmTokenEnums_CollapseWhitespaceButSimpleTypeNamesRemainExact()
    {
        var eds = ValidEds();
        eds.ApplicationProcess = CompleteApplicationProcess();
        var parameter = eds.ApplicationProcess.ParameterList[0];
        parameter.Access = " read ";
        parameter.Support = " optional ";
        eds.ApplicationProcess.TemplateList!.ParameterTemplates[0].Access = " readWrite ";
        eds.ApplicationProcess.TemplateList.ParameterTemplates[0].Support = " mandatory ";
        eds.ApplicationProcess.DataTypeList!.Derived[0].Count!.Access = " readWrite ";
        eds.ApplicationProcess.DataTypeList.Enums[0].SimpleTypeName = " USINT ";

        var issues = Validate(eds);

        issues.Where(issue => issue.Code == ValidationIssueCodes.XddLexicalValueInvalid)
            .Should().ContainSingle(issue => issue.Path == "ApplicationProcess.DataTypeList.Enums[0].SimpleTypeName");
    }

    [Fact]
    public void ValidatedWrite_OmittedOptionalMembers_AreAccepted()
    {
        var eds = ValidEds();
        eds.ApplicationProcess = CompleteApplicationProcess();
        eds.ApplicationProcess.ParameterList[0].VariableRefs[0].MemberRef = null;
        eds.ApplicationProcess.DataTypeList!.Derived[0].Count = null;

        var issues = Validate(eds);

        issues.Where(issue => issue.Code == ValidationIssueCodes.XddLexicalValueInvalid).Should().BeEmpty();
    }

    [Fact]
    public void ValidatedWrite_RequiredEmptyIdRefs_AreStillReported()
    {
        var eds = ValidEds();
        eds.ApplicationProcess = CompleteApplicationProcess();
        eds.ApplicationProcess.FunctionInstanceList!.FunctionInstances[0].TypeIdRef = string.Empty;
        var parameter = eds.ApplicationProcess.ParameterList[0];
        parameter.ConditionalSupports[0] = string.Empty;
        parameter.VariableRefs[0].InstanceIdRefs[0] = string.Empty;
        parameter.Access = null!;

        var issues = Validate(eds);

        issues.Where(issue => issue.Code == ValidationIssueCodes.XddLexicalValueInvalid)
            .Select(issue => issue.Path)
            .Should().Contain(new[]
            {
                "ApplicationProcess.FunctionInstanceList.FunctionInstances[0].TypeIdRef",
                "ApplicationProcess.ParameterList[0].ConditionalSupports[0]",
                "ApplicationProcess.ParameterList[0].VariableRefs[0].InstanceIdRefs[0]",
            });
    }

    private static List<ValidationIssue> Validate(ElectronicDataSheet eds)
    {
        var issues = new List<ValidationIssue>();
        XmlWriteRules.Apply(eds, issues);
        return issues;
    }

    private static ElectronicDataSheet ValidEds()
    {
        var eds = new ElectronicDataSheet();
        eds.FileInfo.CreationDate = "01-01-2026";
        eds.ObjectDictionary.Objects[0x2000] = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "value",
            ObjectType = 7,
            DataType = 7,
        };
        return eds;
    }

    private static ApplicationProcess CompleteApplicationProcess()
    {
        var result = new ApplicationProcess { DataTypeList = new ApDataTypeList() };
        var array = new ApArrayType { UniqueId = "ARR", ElementType = new ApTypeRef { SimpleTypeName = "USINT" } };
        AddLabels(array.LabelGroup);
        result.DataTypeList.Arrays.Add(array);

        var structure = new ApStructType { UniqueId = "REC" };
        AddLabels(structure.LabelGroup);
        structure.VarDeclarations.Add(Variable("MEMBER"));
        result.DataTypeList.Structs.Add(structure);

        var enumeration = new ApEnumType { UniqueId = "ENUM", SimpleTypeName = "USINT" };
        AddLabels(enumeration.LabelGroup);
        var enumValue = new ApEnumValue();
        AddLabels(enumValue.LabelGroup);
        enumeration.EnumValues.Add(enumValue);
        result.DataTypeList.Enums.Add(enumeration);

        var derived = new ApDerivedType
        {
            UniqueId = "DERIVED",
            BaseType = new ApTypeRef { DataTypeIdRef = "ARR" },
            Count = new ApDerivedCount { UniqueId = "COUNT", Access = "readWrite" },
        };
        AddLabels(derived.LabelGroup);
        AddLabels(derived.Count.LabelGroup);
        derived.Count.DefaultValue = Value();
        derived.Count.AllowedValues = AllowedValues();
        result.DataTypeList.Derived.Add(derived);

        var function = new ApFunctionType
        {
            UniqueId = "FUNCTION",
            InterfaceList = new ApInterfaceList(),
            FunctionInstanceList = Instances("NESTED"),
        };
        AddLabels(function.LabelGroup);
        var version = new ApVersionInfo { Date = "2026-01-01" };
        AddLabels(version.LabelGroup);
        function.VersionInfos.Add(version);
        function.InterfaceList.InputVars.Add(Variable("INPUT"));
        function.InterfaceList.OutputVars.Add(Variable("OUTPUT"));
        function.InterfaceList.ConfigVars.Add(Variable("CONFIG"));
        result.FunctionTypeList.Add(function);
        result.FunctionInstanceList = Instances("ROOT");

        result.TemplateList = new ApTemplateList();
        var allowedTemplate = new ApAllowedValuesTemplate { UniqueId = "ALLOWED_TEMPLATE" };
        allowedTemplate.Values.Add(Value());
        allowedTemplate.Ranges.Add(new ApAllowedRange { MinValue = Value(), MaxValue = Value(), Step = Value() });
        result.TemplateList.AllowedValuesTemplates.Add(allowedTemplate);
        var parameterTemplate = new ApParameterTemplate
        {
            UniqueId = "PARAMETER_TEMPLATE",
            TypeRef = new ApTypeRef { SimpleTypeName = "UINT" },
            Access = "read",
            AccessList = "read write",
            Support = "optional",
            ActualValue = Value(),
            DefaultValue = Value(),
            SubstituteValue = Value(),
            AllowedValues = AllowedValues(),
            Unit = Unit(),
        };
        AddLabels(parameterTemplate.LabelGroup);
        parameterTemplate.ConditionalSupports.Add("P");
        result.TemplateList.ParameterTemplates.Add(parameterTemplate);

        var parameter = new ApParameter
        {
            UniqueId = "P",
            Access = "readWriteInput",
            AccessList = "read readWriteOutput",
            Support = "conditional",
            TemplateIdRef = "PARAMETER_TEMPLATE",
            TypeRef = new ApTypeRef { DataTypeIdRef = "REC" },
            ActualValue = Value(),
            DefaultValue = Value(),
            SubstituteValue = Value(),
            AllowedValues = AllowedValues(),
            Unit = Unit(),
            Denotation = new ApLabelGroup(),
        };
        AddLabels(parameter.LabelGroup);
        AddLabels(parameter.Denotation);
        parameter.ConditionalSupports.Add("P");
        parameter.VariableRefs.Add(new ApVariableRef
        {
            VariableIdRef = "INPUT",
            MemberRef = new ApMemberRef { UniqueIdRef = "MEMBER" },
        });
        parameter.VariableRefs[0].InstanceIdRefs.Add("ROOT");
        result.ParameterList.Add(parameter);

        var group = new ApParameterGroup { UniqueId = "GROUP" };
        AddLabels(group.LabelGroup);
        group.ParameterRefs.Add("P");
        var child = new ApParameterGroup { UniqueId = "CHILD" };
        AddLabels(child.LabelGroup);
        child.ParameterRefs.Add("P");
        group.SubGroups.Add(child);
        result.ParameterGroupList.Add(group);
        return result;
    }

    private static ApVarDeclaration Variable(string id)
    {
        var variable = new ApVarDeclaration
        {
            UniqueId = id,
            Type = new ApTypeRef { SimpleTypeName = "USINT" },
            DefaultValue = Value(),
            AllowedValues = AllowedValues(),
            Unit = Unit(),
        };
        AddLabels(variable.LabelGroup);
        variable.ConditionalSupports.Add("P");
        return variable;
    }

    private static ApFunctionInstanceList Instances(string id)
    {
        var result = new ApFunctionInstanceList();
        var instance = new ApFunctionInstance { UniqueId = id, TypeIdRef = "FUNCTION" };
        AddLabels(instance.LabelGroup);
        result.FunctionInstances.Add(instance);
        return result;
    }

    private static ApAllowedValues AllowedValues()
    {
        var result = new ApAllowedValues { TemplateIdRef = "ALLOWED_TEMPLATE" };
        result.Values.Add(Value());
        result.Ranges.Add(new ApAllowedRange { MinValue = Value(), MaxValue = Value(), Step = Value() });
        return result;
    }

    private static ApParameterValue Value()
    {
        var value = new ApParameterValue();
        AddLabels(value.LabelGroup);
        return value;
    }

    private static ApUnit Unit()
    {
        var unit = new ApUnit { UnitUri = "units.xml#length" };
        AddLabels(unit.LabelGroup);
        return unit;
    }

    private static void AddLabels(ApLabelGroup labels)
    {
        labels.Labels.Add(new ApLabel { Lang = "en", Text = "label" });
        labels.Descriptions.Add(new ApDescription { Lang = "de-DE", Text = "description", Uri = "docs/info.html" });
        labels.TextRefs.Add(new ApTextRef { Uri = "dictionary.txt#entry" });
    }
}
