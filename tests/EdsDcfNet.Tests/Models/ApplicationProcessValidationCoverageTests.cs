namespace EdsDcfNet.Tests.Models;

using EdsDcfNet.Models;
using EdsDcfNet.Validation;

public class ApplicationProcessValidationCoverageTests
{
    [Fact]
    public void Validate_FullyPopulatedApplicationProcess_ReturnsNoApplicationProcessIssues()
    {
        var eds = new ElectronicDataSheet { ApplicationProcess = BuildValidApplicationProcess() };

        var issues = CanOpenModelValidator.Validate(eds);

        issues.Should().NotContain(i => i.Path.StartsWith("ApplicationProcess", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_DcfWithFullyPopulatedApplicationProcess_ReturnsNoApplicationProcessIssues()
    {
        var dcf = CreateValidDcf();
        dcf.ApplicationProcess = BuildValidApplicationProcess();

        var issues = CanOpenModelValidator.Validate(dcf);

        issues.Should().NotContain(i => i.Path.StartsWith("ApplicationProcess", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ApplicationProcess_ArrayElementTypeMissingDataTypeRef_ReturnIssue()
    {
        var ap = new ApplicationProcess { DataTypeList = new ApDataTypeList() };
        ap.DataTypeList.Arrays.Add(new ApArrayType
        {
            UniqueId = "DT_ARRAY",
            Name = "Buf",
            ElementType = new ApTypeRef { DataTypeIdRef = "MissingType" }
        });

        var issues = ValidateEdsApplicationProcess(ap);

        issues.Should().Contain(i => i.Path == "ApplicationProcess.DataTypeList.Arrays[0].ElementType");
    }

    [Fact]
    public void Validate_ApplicationProcess_DerivedBaseTypeMissingDataTypeRef_ReturnIssue()
    {
        var ap = new ApplicationProcess { DataTypeList = new ApDataTypeList() };
        ap.DataTypeList.Derived.Add(new ApDerivedType
        {
            UniqueId = "DT_DERIVED",
            Name = "Alias",
            BaseType = new ApTypeRef { DataTypeIdRef = "MissingBase" }
        });

        var issues = ValidateEdsApplicationProcess(ap);

        issues.Should().Contain(i => i.Path == "ApplicationProcess.DataTypeList.Derived[0].BaseType");
    }

    [Fact]
    public void Validate_ApplicationProcess_StructVarDeclarationMissingDataTypeRef_ReturnIssue()
    {
        var ap = new ApplicationProcess { DataTypeList = new ApDataTypeList() };
        var structType = new ApStructType { UniqueId = "DT_STRUCT", Name = "Container" };
        structType.VarDeclarations.Add(new ApVarDeclaration
        {
            UniqueId = "VAR_1",
            Name = "Field",
            Type = new ApTypeRef { DataTypeIdRef = "MissingStruct" }
        });
        ap.DataTypeList.Structs.Add(structType);

        var issues = ValidateEdsApplicationProcess(ap);

        issues.Should().Contain(i =>
            i.Path == "ApplicationProcess.DataTypeList.Structs[0].VarDeclarations[0].Type");
    }

    [Fact]
    public void Validate_ApplicationProcess_ParameterWithNullTypeRef_ReturnsNoTypeRefIssue()
    {
        var ap = new ApplicationProcess();
        ap.ParameterList.Add(new ApParameter { UniqueId = "P_1", TypeRef = null });

        var issues = ValidateEdsApplicationProcess(ap);

        issues.Should().NotContain(i => i.Path.Contains("TypeRef", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ApplicationProcess_ParameterWithEmptyDataTypeIdRef_ReturnsNoTypeRefIssue()
    {
        var ap = new ApplicationProcess();
        ap.ParameterList.Add(new ApParameter
        {
            UniqueId = "P_1",
            TypeRef = new ApTypeRef { DataTypeIdRef = string.Empty }
        });

        var issues = ValidateEdsApplicationProcess(ap);

        issues.Should().NotContain(i => i.Path.Contains("TypeRef", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ApplicationProcess_OutputVarWithValidDataTypeIdRef_ReturnsNoTypeRefIssue()
    {
        var ap = new ApplicationProcess { DataTypeList = new ApDataTypeList() };
        ap.DataTypeList.Structs.Add(new ApStructType { UniqueId = "DT_STRUCT", Name = "Status" });

        var functionType = new ApFunctionType { UniqueId = "FT_1", Name = "Ctrl" };
        functionType.VersionInfos.Add(new ApVersionInfo());
        functionType.InterfaceList = new ApInterfaceList();
        functionType.InterfaceList.OutputVars.Add(new ApVarDeclaration
        {
            UniqueId = "V_OUT",
            Name = "Out",
            Type = new ApTypeRef { DataTypeIdRef = "DT_STRUCT" }
        });
        ap.FunctionTypeList.Add(functionType);

        var issues = ValidateEdsApplicationProcess(ap);

        issues.Should().NotContain(i =>
            i.Path == "ApplicationProcess.FunctionTypeList[0].InterfaceList.OutputVars[0].Type");
    }

    [Fact]
    public void Validate_ApplicationProcess_TemplateParameterMissingDataTypeRef_ReturnIssue()
    {
        var ap = new ApplicationProcess { TemplateList = new ApTemplateList() };
        ap.TemplateList.ParameterTemplates.Add(new ApParameterTemplate
        {
            UniqueId = "TPL_1",
            TypeRef = new ApTypeRef { DataTypeIdRef = "MissingType" }
        });

        var issues = ValidateEdsApplicationProcess(ap);

        issues.Should().Contain(i => i.Path == "ApplicationProcess.TemplateList.ParameterTemplates[0].TypeRef");
    }

    [Fact]
    public void Validate_ApplicationProcess_ParameterMissingParameterTemplateRef_ReturnIssue()
    {
        var ap = new ApplicationProcess { TemplateList = new ApTemplateList() };
        ap.TemplateList.ParameterTemplates.Add(new ApParameterTemplate { UniqueId = "TPL_PARAM" });
        ap.ParameterList.Add(new ApParameter { UniqueId = "P_1", TemplateIdRef = "MissingTemplate" });

        var issues = ValidateEdsApplicationProcess(ap);

        issues.Should().Contain(i => i.Path == "ApplicationProcess.ParameterList[0].TemplateIdRef");
    }

    [Fact]
    public void Validate_ApplicationProcess_ParameterWithValidParameterTemplateRef_ReturnsNoTemplateRefIssue()
    {
        var ap = new ApplicationProcess { TemplateList = new ApTemplateList() };
        ap.TemplateList.ParameterTemplates.Add(new ApParameterTemplate { UniqueId = "TPL_PARAM" });
        ap.ParameterList.Add(new ApParameter { UniqueId = "P_1", TemplateIdRef = "TPL_PARAM" });

        var issues = ValidateEdsApplicationProcess(ap);

        issues.Should().NotContain(i => i.Path == "ApplicationProcess.ParameterList[0].TemplateIdRef");
    }

    [Fact]
    public void Validate_ApplicationProcess_ParameterMissingAllowedValuesTemplateRef_ReturnIssue()
    {
        var ap = new ApplicationProcess { TemplateList = new ApTemplateList() };
        ap.TemplateList.AllowedValuesTemplates.Add(new ApAllowedValuesTemplate { UniqueId = "TPL_AV" });
        ap.ParameterList.Add(new ApParameter
        {
            UniqueId = "P_1",
            AllowedValues = new ApAllowedValues { TemplateIdRef = "MissingAvTemplate" }
        });

        var issues = ValidateEdsApplicationProcess(ap);

        issues.Should().Contain(i => i.Path == "ApplicationProcess.ParameterList[0].AllowedValues.TemplateIdRef");
    }

    [Fact]
    public void Validate_ApplicationProcess_ParameterWithNullTemplateIdRef_ReturnsNoTemplateRefIssue()
    {
        var ap = new ApplicationProcess();
        ap.ParameterList.Add(new ApParameter { UniqueId = "P_1", TemplateIdRef = null });

        var issues = ValidateEdsApplicationProcess(ap);

        issues.Should().NotContain(i => i.Path.Contains("TemplateIdRef", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ApplicationProcess_ParameterWithNullAllowedValuesTemplateIdRef_ReturnsNoTemplateRefIssue()
    {
        var ap = new ApplicationProcess();
        ap.ParameterList.Add(new ApParameter
        {
            UniqueId = "P_1",
            AllowedValues = new ApAllowedValues { TemplateIdRef = null }
        });

        var issues = ValidateEdsApplicationProcess(ap);

        issues.Should().NotContain(i =>
            i.Path == "ApplicationProcess.ParameterList[0].AllowedValues.TemplateIdRef");
    }

    [Fact]
    public void Validate_ApplicationProcess_ParameterWithEmptyAllowedValuesTemplateIdRef_ReturnsNoTemplateRefIssue()
    {
        var ap = new ApplicationProcess();
        ap.ParameterList.Add(new ApParameter
        {
            UniqueId = "P_1",
            AllowedValues = new ApAllowedValues { TemplateIdRef = string.Empty }
        });

        var issues = ValidateEdsApplicationProcess(ap);

        issues.Should().NotContain(i =>
            i.Path == "ApplicationProcess.ParameterList[0].AllowedValues.TemplateIdRef");
    }

    [Fact]
    public void Validate_ApplicationProcess_ParameterWithValidAllowedValuesTemplateRef_ReturnsNoTemplateRefIssue()
    {
        var ap = new ApplicationProcess { TemplateList = new ApTemplateList() };
        ap.TemplateList.AllowedValuesTemplates.Add(new ApAllowedValuesTemplate { UniqueId = "TPL_AV" });
        ap.ParameterList.Add(new ApParameter
        {
            UniqueId = "P_1",
            AllowedValues = new ApAllowedValues { TemplateIdRef = "TPL_AV" }
        });

        var issues = ValidateEdsApplicationProcess(ap);

        issues.Should().NotContain(i =>
            i.Path == "ApplicationProcess.ParameterList[0].AllowedValues.TemplateIdRef");
    }

    [Fact]
    public void Validate_ApplicationProcess_InterfaceInputVarMissingDataTypeRef_ReturnIssue()
    {
        var ap = new ApplicationProcess();
        var functionType = new ApFunctionType { UniqueId = "FT_1", Name = "Ctrl" };
        functionType.VersionInfos.Add(new ApVersionInfo());
        functionType.InterfaceList = new ApInterfaceList();
        functionType.InterfaceList.InputVars.Add(new ApVarDeclaration
        {
            UniqueId = "V_IN",
            Name = "In",
            Type = new ApTypeRef { DataTypeIdRef = "MissingType" }
        });
        ap.FunctionTypeList.Add(functionType);

        var issues = ValidateEdsApplicationProcess(ap);

        issues.Should().Contain(i =>
            i.Path == "ApplicationProcess.FunctionTypeList[0].InterfaceList.InputVars[0].Type");
    }

    [Fact]
    public void Validate_ApplicationProcess_DuplicateDerivedCountId_ReturnIssue()
    {
        var ap = new ApplicationProcess { DataTypeList = new ApDataTypeList() };
        ap.DataTypeList.Derived.Add(new ApDerivedType
        {
            UniqueId = "DT_DERIVED",
            Name = "Alias",
            Count = new ApDerivedCount { UniqueId = "DUP_ID" },
            BaseType = new ApTypeRef { SimpleTypeName = "UINT" }
        });
        ap.ParameterList.Add(new ApParameter { UniqueId = "DUP_ID" });

        var issues = ValidateEdsApplicationProcess(ap);

        issues.Should().Contain(i =>
            i.Path == "ApplicationProcess.ParameterList[0].UniqueId" &&
            i.Message.Contains("Duplicate unique ID 'DUP_ID'", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Arrays", "DT_ARRAY")]
    [InlineData("Enums", "DT_ENUM")]
    [InlineData("Derived", "DT_DERIVED")]
    public void Validate_ApplicationProcess_DuplicateDataTypeIdAcrossKinds_ReturnIssue(string kind, string sharedId)
    {
        var ap = new ApplicationProcess { DataTypeList = new ApDataTypeList() };
        switch (kind)
        {
            case "Arrays":
                ap.DataTypeList.Arrays.Add(new ApArrayType { UniqueId = sharedId, Name = "Buf" });
                break;
            case "Enums":
                ap.DataTypeList.Enums.Add(new ApEnumType { UniqueId = sharedId, Name = "Mode" });
                break;
            case "Derived":
                ap.DataTypeList.Derived.Add(new ApDerivedType
                {
                    UniqueId = sharedId,
                    Name = "Alias",
                    BaseType = new ApTypeRef { SimpleTypeName = "UINT" }
                });
                break;
        }

        ap.ParameterList.Add(new ApParameter { UniqueId = sharedId });

        var issues = ValidateEdsApplicationProcess(ap);

        issues.Should().Contain(i =>
            i.Path == "ApplicationProcess.ParameterList[0].UniqueId" &&
            i.Message.Contains("Duplicate unique ID '" + sharedId + "'", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ApplicationProcess_EmptyDataTypeUniqueIds_ReturnIssues()
    {
        var ap = new ApplicationProcess { DataTypeList = new ApDataTypeList() };
        ap.DataTypeList.Arrays.Add(new ApArrayType { Name = "Buf" });
        ap.DataTypeList.Enums.Add(new ApEnumType { Name = "Mode" });
        ap.DataTypeList.Structs.Add(new ApStructType { Name = "Container" });
        ap.DataTypeList.Derived.Add(new ApDerivedType
        {
            Name = "Alias",
            Count = new ApDerivedCount(),
            BaseType = new ApTypeRef { SimpleTypeName = "UINT" }
        });

        var issues = ValidateEdsApplicationProcess(ap);

        issues.Should().Contain(i => i.Path == "ApplicationProcess.DataTypeList.Arrays[0].UniqueId");
        issues.Should().Contain(i => i.Path == "ApplicationProcess.DataTypeList.Enums[0].UniqueId");
        issues.Should().Contain(i => i.Path == "ApplicationProcess.DataTypeList.Structs[0].UniqueId");
        issues.Should().Contain(i => i.Path == "ApplicationProcess.DataTypeList.Derived[0].UniqueId");
        issues.Should().Contain(i => i.Path == "ApplicationProcess.DataTypeList.Derived[0].Count.UniqueId");
    }

    [Fact]
    public void Validate_ApplicationProcess_ParameterWithNullDataTypeIdRef_ReturnsNoTypeRefIssue()
    {
        var ap = new ApplicationProcess();
        ap.ParameterList.Add(new ApParameter
        {
            UniqueId = "P_1",
            TypeRef = new ApTypeRef { DataTypeIdRef = null }
        });

        var issues = ValidateEdsApplicationProcess(ap);

        issues.Should().NotContain(i => i.Path.Contains("TypeRef", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ApplicationProcess_ConfigVarWithValidDataTypeIdRef_ReturnsNoTypeRefIssue()
    {
        var ap = new ApplicationProcess { DataTypeList = new ApDataTypeList() };
        ap.DataTypeList.Enums.Add(new ApEnumType { UniqueId = "DT_ENUM", Name = "Mode" });

        var functionType = new ApFunctionType { UniqueId = "FT_1", Name = "Ctrl" };
        functionType.VersionInfos.Add(new ApVersionInfo());
        functionType.InterfaceList = new ApInterfaceList();
        functionType.InterfaceList.ConfigVars.Add(new ApVarDeclaration
        {
            UniqueId = "V_CFG",
            Name = "Cfg",
            Type = new ApTypeRef { DataTypeIdRef = "DT_ENUM" }
        });
        ap.FunctionTypeList.Add(functionType);

        var issues = ValidateEdsApplicationProcess(ap);

        issues.Should().NotContain(i =>
            i.Path == "ApplicationProcess.FunctionTypeList[0].InterfaceList.ConfigVars[0].Type");
    }

    [Fact]
    public void Validate_ApplicationProcess_DerivedWithoutCount_ReturnsNoCountUniqueIdIssue()
    {
        var ap = new ApplicationProcess { DataTypeList = new ApDataTypeList() };
        ap.DataTypeList.Derived.Add(new ApDerivedType
        {
            UniqueId = "DT_DERIVED",
            Name = "Alias",
            BaseType = new ApTypeRef { SimpleTypeName = "UINT" }
        });

        var issues = ValidateEdsApplicationProcess(ap);

        issues.Should().NotContain(i => i.Path.Contains(".Count.", StringComparison.Ordinal));
    }

    private static IReadOnlyList<ValidationIssue> ValidateEdsApplicationProcess(ApplicationProcess applicationProcess)
    {
        var eds = new ElectronicDataSheet { ApplicationProcess = applicationProcess };
        return CanOpenModelValidator.Validate(eds);
    }

    private static ApplicationProcess BuildValidApplicationProcess()
    {
        var ap = new ApplicationProcess
        {
            DataTypeList = new ApDataTypeList(),
            TemplateList = new ApTemplateList(),
            FunctionInstanceList = new ApFunctionInstanceList()
        };

        ap.DataTypeList.Structs.Add(new ApStructType { UniqueId = "DT_STRUCT", Name = "Status" });

        ap.DataTypeList.Arrays.Add(new ApArrayType
        {
            UniqueId = "DT_ARRAY",
            Name = "Buf",
            ElementType = new ApTypeRef { DataTypeIdRef = "DT_STRUCT" }
        });

        ap.DataTypeList.Enums.Add(new ApEnumType { UniqueId = "DT_ENUM", Name = "Mode" });

        ap.DataTypeList.Derived.Add(new ApDerivedType
        {
            UniqueId = "DT_DERIVED",
            Name = "DerivedStatus",
            BaseType = new ApTypeRef { DataTypeIdRef = "DT_STRUCT" },
            Count = new ApDerivedCount { UniqueId = "DT_COUNT" }
        });

        var structWithMember = new ApStructType { UniqueId = "DT_MEMBER_STRUCT", Name = "Container" };
        structWithMember.VarDeclarations.Add(new ApVarDeclaration
        {
            UniqueId = "VAR_MEMBER",
            Name = "Field",
            Type = new ApTypeRef { DataTypeIdRef = "DT_STRUCT" }
        });
        ap.DataTypeList.Structs.Add(structWithMember);

        ap.TemplateList.ParameterTemplates.Add(new ApParameterTemplate
        {
            UniqueId = "TPL_PARAM",
            TypeRef = new ApTypeRef { SimpleTypeName = "UINT" }
        });
        ap.TemplateList.AllowedValuesTemplates.Add(new ApAllowedValuesTemplate { UniqueId = "TPL_AV" });

        var functionType = new ApFunctionType { UniqueId = "FT_1", Name = "Ctrl" };
        functionType.VersionInfos.Add(new ApVersionInfo());
        functionType.InterfaceList = new ApInterfaceList();
        functionType.InterfaceList.InputVars.Add(new ApVarDeclaration
        {
            UniqueId = "V_IN",
            Name = "In",
            Type = new ApTypeRef { DataTypeIdRef = "DT_STRUCT" }
        });
        functionType.InterfaceList.OutputVars.Add(new ApVarDeclaration
        {
            UniqueId = "V_OUT",
            Name = "Out",
            Type = new ApTypeRef { SimpleTypeName = "UINT" }
        });
        functionType.InterfaceList.ConfigVars.Add(new ApVarDeclaration
        {
            UniqueId = "V_CFG",
            Name = "Cfg",
            Type = new ApTypeRef { SimpleTypeName = "BOOL" }
        });
        ap.FunctionTypeList.Add(functionType);

        ap.ParameterList.Add(new ApParameter
        {
            UniqueId = "P_1",
            TypeRef = new ApTypeRef { DataTypeIdRef = "DT_STRUCT" }
        });

        ap.FunctionInstanceList.FunctionInstances.Add(new ApFunctionInstance
        {
            UniqueId = "FI_1",
            TypeIdRef = "FT_1"
        });

        return ap;
    }

    private static DeviceConfigurationFile CreateValidDcf()
    {
        var dcf = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning
            {
                NodeId = 5,
                Baudrate = 500
            }
        };

        dcf.ObjectDictionary.MandatoryObjects.Add(0x1000);
        dcf.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly
        };

        return dcf;
    }
}
