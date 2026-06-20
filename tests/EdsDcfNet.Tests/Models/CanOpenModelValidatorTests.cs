namespace EdsDcfNet.Tests.Models;

using EdsDcfNet.Models;
using EdsDcfNet.Validation;

public class CanOpenModelValidatorTests
{
    [Fact]
    public void Validate_ValidDcf_ReturnsNoIssues()
    {
        // Arrange
        var dcf = CreateValidDcf();

        // Act
        var issues = CanOpenModelValidator.Validate(dcf);

        // Assert
        issues.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidCommissioning_ReturnsActionableIssues()
    {
        // Arrange
        var dcf = CreateValidDcf();
        dcf.DeviceCommissioning.NodeId = 255;
        dcf.DeviceCommissioning.Baudrate = 42;
        dcf.DeviceCommissioning.NodeName = new string('N', 247);

        // Act
        var issues = CanOpenModelValidator.Validate(dcf);

        // Assert
        issues.Should().Contain(i => i.Path == "DeviceCommissioning.NodeId");
        issues.Should().Contain(i => i.Path == "DeviceCommissioning.Baudrate");
        issues.Should().Contain(i => i.Path == "DeviceCommissioning.NodeName");
    }

    [Fact]
    public void Validate_NodeAndNetworkReferences_EnforceMaxLength()
    {
        // Arrange
        var dcf = CreateValidDcf();
        dcf.DeviceCommissioning.NodeRefd = new string('R', 250);
        dcf.DeviceCommissioning.NetRefd = new string('R', 250);

        // Act
        var issues = CanOpenModelValidator.Validate(dcf);

        // Assert
        issues.Should().Contain(i => i.Path == "DeviceCommissioning.NodeRefd");
        issues.Should().Contain(i => i.Path == "DeviceCommissioning.NetRefd");
    }

    [Fact]
    public void Validate_BaudrateZero_IsAcceptedAsUnconfigured()
    {
        // Arrange
        var dcf = CreateValidDcf();
        dcf.DeviceCommissioning.Baudrate = 0;

        // Act
        var issues = CanOpenModelValidator.Validate(dcf);

        // Assert
        issues.Should().NotContain(i => i.Path == "DeviceCommissioning.Baudrate");
    }

    [Fact]
    public void Validate_ObjectListContainsMissingIndex_ReturnsIssue()
    {
        // Arrange
        var eds = new ElectronicDataSheet();
        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);

        // Act
        var issues = CanOpenModelValidator.Validate(eds);

        // Assert
        issues.Should().ContainSingle(i =>
            i.Path == "ObjectDictionary.MandatoryObjects" &&
            i.Message.Contains("missing object 0x1000", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ModelConvenienceMethod_DelegatesToValidator()
    {
        // Arrange
        var dcf = CreateValidDcf();
        dcf.DeviceCommissioning.NodeId = 255;

        // Act
        var issues = dcf.Validate();

        // Assert
        issues.Should().Contain(i => i.Path == "DeviceCommissioning.NodeId");
    }

    [Fact]
    public void Validate_NodeIdZero_ReturnsIssue()
    {
        // Arrange
        var dcf = CreateValidDcf();
        dcf.DeviceCommissioning.NodeId = 0;

        // Act
        var issues = CanOpenModelValidator.Validate(dcf);

        // Assert
        issues.Should().Contain(i => i.Path == "DeviceCommissioning.NodeId");
    }

    [Fact]
    public void Validate_OmittedCommissioning_NodeIdZeroIsAccepted()
    {
        // Arrange
        var dcf = CreateValidDcf();
        dcf.DeviceCommissioning = new DeviceCommissioning();

        // Act
        var issues = CanOpenModelValidator.Validate(dcf);

        // Assert
        issues.Should().NotContain(i => i.Path == "DeviceCommissioning.NodeId");
    }

    [Fact]
    public void Validate_CanOpenFileFacade_DelegatesToValidator()
    {
        // Arrange
        var dcf = CreateValidDcf();
        dcf.DeviceCommissioning.Baudrate = 777;

        // Act
        var issues = CanOpenFile.Validate(dcf);

        // Assert
        issues.Should().Contain(i => i.Path == "DeviceCommissioning.Baudrate");
    }

    [Fact]
    public void Validate_InvalidObjectTypeAndNames_ReturnsDetailedPaths()
    {
        // Arrange
        var dcf = CreateValidDcf();
        dcf.DeviceInfo.VendorName = new string('V', 245);
        dcf.ObjectDictionary.Objects[0x1000].ObjectType = 0xAA;
        dcf.ObjectDictionary.Objects[0x1000].ParameterName = new string('X', 242);

        // Act
        var issues = CanOpenModelValidator.Validate(dcf);

        // Assert
        issues.Should().Contain(i => i.Path == "DeviceInfo.VendorName");
        issues.Should().Contain(i => i.Path == "ObjectDictionary.Objects[0x1000].ObjectType");
        issues.Should().Contain(i => i.Path == "ObjectDictionary.Objects[0x1000].ParameterName");
    }

    [Fact]
    public void Validate_SubNumberWithoutSubObjects_ReturnsIssue()
    {
        // Arrange
        var dcf = CreateValidDcf();
        dcf.ObjectDictionary.Objects[0x1000].SubNumber = 3;
        dcf.ObjectDictionary.Objects[0x1000].SubObjects.Clear();

        // Act
        var issues = CanOpenModelValidator.Validate(dcf);

        // Assert
        issues.Should().Contain(i => i.Path == "ObjectDictionary.Objects[0x1000].SubNumber");
    }

    [Fact]
    public void Validate_SubNumberWithCompactSubObj_DoesNotReturnIssue()
    {
        // Arrange
        var dcf = CreateValidDcf();
        dcf.ObjectDictionary.Objects[0x1000].SubNumber = 3;
        dcf.ObjectDictionary.Objects[0x1000].SubObjects.Clear();
        dcf.ObjectDictionary.Objects[0x1000].CompactSubObj = 3;

        // Act
        var issues = CanOpenModelValidator.Validate(dcf);

        // Assert
        issues.Should().NotContain(i => i.Path == "ObjectDictionary.Objects[0x1000].SubNumber");
    }

    [Fact]
    public void Validate_ValidEds_ReturnsNoIssues()
    {
        // Arrange
        var eds = new ElectronicDataSheet();
        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly
        };

        // Act
        var issues = CanOpenModelValidator.Validate(eds);

        // Assert
        issues.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ElectronicDataSheetConvenienceMethod_DelegatesToValidator()
    {
        // Arrange
        var eds = new ElectronicDataSheet();
        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0xAA
        };

        // Act
        var issues = eds.Validate();

        // Assert
        issues.Should().Contain(i => i.Path == "ObjectDictionary.Objects[0x1000].ObjectType");
    }

    [Fact]
    public void Validate_NullModels_ThrowArgumentNullException()
    {
        // Act
        var validateEds = () => CanOpenModelValidator.Validate((ElectronicDataSheet)null!);
        var validateDcf = () => CanOpenModelValidator.Validate((DeviceConfigurationFile)null!);

        // Assert
        validateEds.Should().Throw<ArgumentNullException>();
        validateDcf.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_DeviceInfoConstraints_ReturnIssues()
    {
        // Arrange
        var dcf = CreateValidDcf();
        dcf.DeviceInfo.OrderCode = new string('O', 246);
        dcf.DeviceInfo.ProductName = new string('P', 244);
        dcf.DeviceInfo.Granularity = 65;

        // Act
        var issues = CanOpenModelValidator.Validate(dcf);

        // Assert
        issues.Should().Contain(i => i.Path == "DeviceInfo.OrderCode");
        issues.Should().Contain(i => i.Path == "DeviceInfo.ProductName");
        issues.Should().Contain(i => i.Path == "DeviceInfo.Granularity");
    }

    [Fact]
    public void Validate_ObjectClassificationAndSubObjectConstraints_ReturnIssues()
    {
        // Arrange
        var dcf = CreateValidDcf();
        dcf.ObjectDictionary.OptionalObjects.Add(0x1000); // duplicate across lists
        dcf.ObjectDictionary.Objects[0x2000] = new CanOpenObject // not in any list
        {
            Index = 0x2000,
            ParameterName = "Unclassified",
            ObjectType = 0x7
        };
        dcf.ObjectDictionary.Objects[0x1000].SubObjects[0x01] = new CanOpenSubObject
        {
            SubIndex = 0x01,
            ParameterName = new string('S', 242)
        };

        // Act
        var issues = CanOpenModelValidator.Validate(dcf);

        // Assert
        issues.Should().Contain(i => i.Path == "ObjectDictionary.OptionalObjects");
        issues.Should().Contain(i => i.Path == "ObjectDictionary.Objects[0x2000]");
        issues.Should().Contain(i =>
            i.Path == "ObjectDictionary.Objects[0x1000].SubObjects[0x01].ParameterName");
    }

    [Fact]
    public void Validate_DuplicateWithinSingleObjectList_ReturnsAccurateMessage()
    {
        // Arrange
        var dcf = CreateValidDcf();
        dcf.ObjectDictionary.MandatoryObjects.Add(0x1000);

        // Act
        var issues = CanOpenModelValidator.Validate(dcf);

        // Assert
        issues.Should().Contain(i =>
            i.Path == "ObjectDictionary.MandatoryObjects" &&
            i.Message.Contains("multiple times in this object list", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_FacadeAndValidationIssue_ToStringAreCovered()
    {
        // Arrange
        var dcf = CreateValidDcf();
        dcf.DeviceCommissioning.Baudrate = 777;

        // Act
        var issues = CanOpenFile.Validate(dcf);

        // Assert
        issues.Should().NotBeEmpty();
        issues[0].ToString().Should().Contain(":");
    }

    [Fact]
    public void WriteXdcToString_WithValidDcf_ReturnsXml()
    {
        // Arrange
        var dcf = CreateValidDcf();

        // Act
        var xdc = CanOpenFile.WriteXdcToString(dcf);

        // Assert
        xdc.Should().Contain("ISO15745ProfileContainer");
    }

    [Fact]
    public void ElectronicDataSheet_ApplicationProcessProperty_CanBeAssigned()
    {
        // Arrange
        var eds = new ElectronicDataSheet();
        var process = new ApplicationProcess();

        // Act
        eds.ApplicationProcess = process;

        // Assert
        eds.ApplicationProcess.Should().BeSameAs(process);
    }

    [Fact]
    public void Validate_ValidCpj_ReturnsNoIssues()
    {
        // Arrange
        var cpj = CreateValidCpj();

        // Act
        var issues = CanOpenModelValidator.Validate(cpj);

        // Assert
        issues.Should().BeEmpty();
    }

    [Fact]
    public void Validate_Cpj_InvalidNodeIdAndKeyMismatch_ReturnsIssues()
    {
        // Arrange
        var cpj = CreateValidCpj();
        cpj.Networks[0].Nodes[2] = new NetworkNode { NodeId = 3, Present = true, Name = "Mismatch" };

        // Act
        var issues = CanOpenModelValidator.Validate(cpj);

        // Assert
        issues.Should().Contain(i => i.Path == "Networks[0].Nodes[2].NodeId");
    }

    [Fact]
    public void Validate_Cpj_NodeIdOutOfRange_ReturnsIssue()
    {
        // Arrange
        var cpj = new NodelistProject();
        var network = new NetworkTopology();
        network.Nodes[200] = new NetworkNode { NodeId = 200, Present = true };
        cpj.Networks.Add(network);

        // Act
        var issues = CanOpenModelValidator.Validate(cpj);

        // Assert
        issues.Should().Contain(i => i.Path == "Networks[0].Nodes[200]");
        issues.Should().Contain(i => i.Path == "Networks[0].Nodes[200].NodeId");
    }

    [Fact]
    public void Validate_ApplicationProcess_MissingReferencesAndDuplicateIds_ReturnIssues()
    {
        // Arrange
        var eds = new ElectronicDataSheet();
        eds.ApplicationProcess = new ApplicationProcess();
        eds.ApplicationProcess.FunctionTypeList.Add(new ApFunctionType { UniqueId = "FT_1" });
        eds.ApplicationProcess.FunctionTypeList.Add(new ApFunctionType { UniqueId = "FT_1" });
        eds.ApplicationProcess.ParameterList.Add(new ApParameter { UniqueId = "P_1" });
        eds.ApplicationProcess.ParameterGroupList.Add(new ApParameterGroup
        {
            UniqueId = "PG_1",
            ParameterRefs = { "MissingParam" }
        });
        eds.ApplicationProcess.FunctionInstanceList = new ApFunctionInstanceList();
        eds.ApplicationProcess.FunctionInstanceList.FunctionInstances.Add(new ApFunctionInstance
        {
            UniqueId = "FI_1",
            TypeIdRef = "MissingType"
        });

        // Act
        var issues = CanOpenModelValidator.Validate(eds);

        // Assert
        issues.Should().Contain(i => i.Path == "ApplicationProcess.FunctionTypeList[1].UniqueId");
        issues.Should().Contain(i => i.Path == "ApplicationProcess.FunctionTypeList[0].VersionInfos");
        issues.Should().Contain(i => i.Path == "ApplicationProcess.ParameterGroupList[0].ParameterRefs");
        issues.Should().Contain(i => i.Path == "ApplicationProcess.FunctionInstanceList.FunctionInstances[0].TypeIdRef");
    }

    [Fact]
    public void Validate_ApplicationProcess_ValidReferences_ReturnNoApplicationProcessIssues()
    {
        // Arrange
        var eds = new ElectronicDataSheet();
        eds.ApplicationProcess = new ApplicationProcess();
        var functionType = new ApFunctionType { UniqueId = "FT_1" };
        functionType.VersionInfos.Add(new ApVersionInfo());
        eds.ApplicationProcess.FunctionTypeList.Add(functionType);
        eds.ApplicationProcess.ParameterList.Add(new ApParameter { UniqueId = "P_1" });
        eds.ApplicationProcess.ParameterGroupList.Add(new ApParameterGroup
        {
            UniqueId = "PG_1",
            ParameterRefs = { "P_1" }
        });
        eds.ApplicationProcess.FunctionInstanceList = new ApFunctionInstanceList();
        eds.ApplicationProcess.FunctionInstanceList.FunctionInstances.Add(new ApFunctionInstance
        {
            UniqueId = "FI_1",
            TypeIdRef = "FT_1"
        });

        // Act
        var issues = CanOpenModelValidator.Validate(eds);

        // Assert
        issues.Should().NotContain(i => i.Path.StartsWith("ApplicationProcess", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ApplicationProcess_NestedInstanceTypeRefToLaterFunctionType_ReturnsNoTypeRefIssue()
    {
        // Arrange — nested instance under FT_1 references FT_2 defined later in the list
        var eds = new ElectronicDataSheet();
        eds.ApplicationProcess = new ApplicationProcess();

        var firstType = new ApFunctionType { UniqueId = "FT_1" };
        firstType.VersionInfos.Add(new ApVersionInfo());
        firstType.FunctionInstanceList = new ApFunctionInstanceList();
        firstType.FunctionInstanceList.FunctionInstances.Add(new ApFunctionInstance
        {
            UniqueId = "FI_NESTED",
            TypeIdRef = "FT_2"
        });

        var secondType = new ApFunctionType { UniqueId = "FT_2" };
        secondType.VersionInfos.Add(new ApVersionInfo());

        eds.ApplicationProcess.FunctionTypeList.Add(firstType);
        eds.ApplicationProcess.FunctionTypeList.Add(secondType);

        // Act
        var issues = CanOpenModelValidator.Validate(eds);

        // Assert
        issues.Should().NotContain(i => i.Path.Contains("TypeIdRef", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ApplicationProcess_DuplicateInstanceIdsAcrossLists_ReturnIssue()
    {
        // Arrange — same UniqueId in nested and top-level instance lists
        var eds = new ElectronicDataSheet();
        eds.ApplicationProcess = new ApplicationProcess();

        var functionType = new ApFunctionType { UniqueId = "FT_1" };
        functionType.VersionInfos.Add(new ApVersionInfo());
        functionType.FunctionInstanceList = new ApFunctionInstanceList();
        functionType.FunctionInstanceList.FunctionInstances.Add(new ApFunctionInstance
        {
            UniqueId = "FI_DUP",
            TypeIdRef = "FT_1"
        });
        eds.ApplicationProcess.FunctionTypeList.Add(functionType);

        eds.ApplicationProcess.FunctionInstanceList = new ApFunctionInstanceList();
        eds.ApplicationProcess.FunctionInstanceList.FunctionInstances.Add(new ApFunctionInstance
        {
            UniqueId = "FI_DUP",
            TypeIdRef = "FT_1"
        });

        // Act
        var issues = CanOpenModelValidator.Validate(eds);

        // Assert
        issues.Should().Contain(i => i.Path == "ApplicationProcess.FunctionInstanceList.FunctionInstances[0].UniqueId");
    }

    [Theory]
    [InlineData("FunctionTypeAndParameter")]
    [InlineData("FunctionTypeAndParameterGroup")]
    [InlineData("ParameterAndFunctionInstance")]
    [InlineData("ParameterGroupAndFunctionInstance")]
    public void Validate_ApplicationProcess_DuplicateIdsAcrossKinds_ReturnIssue(string scenario)
    {
        // Arrange — xsd:ID values must be unique across all applicationProcess ID-bearing elements
        var eds = new ElectronicDataSheet();
        eds.ApplicationProcess = new ApplicationProcess();
        const string sharedId = "SHARED_ID";

        switch (scenario)
        {
            case "FunctionTypeAndParameter":
            {
                var functionType = new ApFunctionType { UniqueId = sharedId };
                functionType.VersionInfos.Add(new ApVersionInfo());
                eds.ApplicationProcess.FunctionTypeList.Add(functionType);
                eds.ApplicationProcess.ParameterList.Add(new ApParameter { UniqueId = sharedId });
                break;
            }
            case "FunctionTypeAndParameterGroup":
            {
                var functionType = new ApFunctionType { UniqueId = sharedId };
                functionType.VersionInfos.Add(new ApVersionInfo());
                eds.ApplicationProcess.FunctionTypeList.Add(functionType);
                eds.ApplicationProcess.ParameterGroupList.Add(new ApParameterGroup { UniqueId = sharedId });
                break;
            }
            case "ParameterAndFunctionInstance":
            {
                var functionType = new ApFunctionType { UniqueId = "FT_1" };
                functionType.VersionInfos.Add(new ApVersionInfo());
                eds.ApplicationProcess.FunctionTypeList.Add(functionType);
                eds.ApplicationProcess.ParameterList.Add(new ApParameter { UniqueId = sharedId });
                eds.ApplicationProcess.FunctionInstanceList = new ApFunctionInstanceList();
                eds.ApplicationProcess.FunctionInstanceList.FunctionInstances.Add(new ApFunctionInstance
                {
                    UniqueId = sharedId,
                    TypeIdRef = "FT_1"
                });
                break;
            }
            case "ParameterGroupAndFunctionInstance":
            {
                var functionType = new ApFunctionType { UniqueId = "FT_1" };
                functionType.VersionInfos.Add(new ApVersionInfo());
                eds.ApplicationProcess.FunctionTypeList.Add(functionType);
                eds.ApplicationProcess.ParameterGroupList.Add(new ApParameterGroup { UniqueId = sharedId });
                eds.ApplicationProcess.FunctionInstanceList = new ApFunctionInstanceList();
                eds.ApplicationProcess.FunctionInstanceList.FunctionInstances.Add(new ApFunctionInstance
                {
                    UniqueId = sharedId,
                    TypeIdRef = "FT_1"
                });
                break;
            }
        }

        // Act
        var issues = CanOpenModelValidator.Validate(eds);

        // Assert
        issues.Should().Contain(i =>
            i.Path.EndsWith(".UniqueId", StringComparison.Ordinal) &&
            i.Message.Contains("Duplicate unique ID '" + sharedId + "'", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_DcfWithApplicationProcess_ValidatesApplicationProcessGraph()
    {
        var dcf = CreateValidDcf();
        dcf.ApplicationProcess = new ApplicationProcess();
        dcf.ApplicationProcess.FunctionTypeList.Add(new ApFunctionType { UniqueId = "FT_1" });

        var issues = CanOpenModelValidator.Validate(dcf);

        issues.Should().Contain(i => i.Path == "ApplicationProcess.FunctionTypeList[0].VersionInfos");
    }

    [Fact]
    public void Validate_ApplicationProcess_EmptyUniqueIdAndTypeIdRef_ReturnIssues()
    {
        var eds = new ElectronicDataSheet { ApplicationProcess = new ApplicationProcess() };
        eds.ApplicationProcess.FunctionTypeList.Add(new ApFunctionType());
        eds.ApplicationProcess.FunctionInstanceList = new ApFunctionInstanceList();
        eds.ApplicationProcess.FunctionInstanceList.FunctionInstances.Add(new ApFunctionInstance());

        var issues = CanOpenModelValidator.Validate(eds);

        issues.Should().Contain(i => i.Path == "ApplicationProcess.FunctionTypeList[0].UniqueId");
        issues.Should().Contain(i => i.Path == "ApplicationProcess.FunctionTypeList[0].VersionInfos");
        issues.Should().Contain(i => i.Path == "ApplicationProcess.FunctionInstanceList.FunctionInstances[0].UniqueId");
        issues.Should().Contain(i => i.Path == "ApplicationProcess.FunctionInstanceList.FunctionInstances[0].TypeIdRef");
    }

    [Fact]
    public void Validate_ApplicationProcess_EmptyParameterRef_ReturnIssue()
    {
        var eds = new ElectronicDataSheet { ApplicationProcess = new ApplicationProcess() };
        eds.ApplicationProcess.ParameterGroupList.Add(new ApParameterGroup
        {
            UniqueId = "PG_1",
            ParameterRefs = { string.Empty }
        });

        var issues = CanOpenModelValidator.Validate(eds);

        issues.Should().Contain(i => i.Path == "ApplicationProcess.ParameterGroupList[0].ParameterRefs");
    }

    [Fact]
    public void Validate_ApplicationProcess_ParameterGroupSubGroupMissingRef_ReturnIssue()
    {
        var eds = new ElectronicDataSheet { ApplicationProcess = new ApplicationProcess() };
        eds.ApplicationProcess.ParameterList.Add(new ApParameter { UniqueId = "P_1" });
        var group = new ApParameterGroup { UniqueId = "PG_1", ParameterRefs = { "P_1" } };
        group.SubGroups.Add(new ApParameterGroup
        {
            UniqueId = "PG_SUB",
            ParameterRefs = { "MissingParam" }
        });
        eds.ApplicationProcess.ParameterGroupList.Add(group);

        var issues = CanOpenModelValidator.Validate(eds);

        issues.Should().Contain(i => i.Path == "ApplicationProcess.ParameterGroupList[0].SubGroups[0].ParameterRefs");
    }

    [Fact]
    public void Validate_Cpj_NetworkAndNodeFieldLengths_ReturnIssues()
    {
        var cpj = CreateValidCpj();
        cpj.Networks[0].NetName = new string('N', 244);
        cpj.Networks[0].NetRefd = new string('R', 250);
        cpj.Networks[0].Nodes[2].Name = new string('X', 247);
        cpj.Networks[0].Nodes[2].Refd = new string('Y', 250);

        var issues = CanOpenModelValidator.Validate(cpj);

        issues.Should().Contain(i => i.Path == "Networks[0].NetName");
        issues.Should().Contain(i => i.Path == "Networks[0].NetRefd");
        issues.Should().Contain(i => i.Path == "Networks[0].Nodes[2].Name");
        issues.Should().Contain(i => i.Path == "Networks[0].Nodes[2].Refd");
    }

    [Fact]
    public void Validate_ManufacturerObjectList_IsValidated()
    {
        var dcf = CreateValidDcf();
        dcf.ObjectDictionary.ManufacturerObjects.Add(0x2000);

        var issues = CanOpenModelValidator.Validate(dcf);

        issues.Should().Contain(i =>
            i.Path == "ObjectDictionary.ManufacturerObjects" &&
            i.Message.Contains("missing object 0x2000", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ApplicationProcess_FunctionTypeWithoutNestedInstances_SkipsNestedValidation()
    {
        var eds = new ElectronicDataSheet { ApplicationProcess = new ApplicationProcess() };
        var functionType = new ApFunctionType { UniqueId = "FT_1" };
        functionType.VersionInfos.Add(new ApVersionInfo());
        eds.ApplicationProcess.FunctionTypeList.Add(functionType);

        var issues = CanOpenModelValidator.Validate(eds);

        issues.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ApplicationProcess_EmptyParameterUniqueId_SkipsParameterIdIndex()
    {
        var eds = new ElectronicDataSheet { ApplicationProcess = new ApplicationProcess() };
        eds.ApplicationProcess.ParameterList.Add(new ApParameter());
        eds.ApplicationProcess.ParameterGroupList.Add(new ApParameterGroup
        {
            UniqueId = "PG_1",
            ParameterRefs = { "MissingParam" }
        });

        var issues = CanOpenModelValidator.Validate(eds);

        issues.Should().Contain(i => i.Path == "ApplicationProcess.ParameterList[0].UniqueId");
        issues.Should().Contain(i => i.Path == "ApplicationProcess.ParameterGroupList[0].ParameterRefs");
    }

    [Fact]
    public void Validate_ApplicationProcess_DuplicateIdBetweenDataTypeAndParameter_ReturnIssue()
    {
        var eds = new ElectronicDataSheet { ApplicationProcess = new ApplicationProcess() };
        eds.ApplicationProcess.DataTypeList = new ApDataTypeList();
        eds.ApplicationProcess.DataTypeList.Structs.Add(new ApStructType { UniqueId = "SHARED_ID", Name = "MyStruct" });
        eds.ApplicationProcess.ParameterList.Add(new ApParameter { UniqueId = "SHARED_ID" });

        var issues = CanOpenModelValidator.Validate(eds);

        issues.Should().Contain(i =>
            i.Path == "ApplicationProcess.ParameterList[0].UniqueId" &&
            i.Message.Contains("Duplicate unique ID 'SHARED_ID'", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ApplicationProcess_ParameterWithMissingDataTypeRef_ReturnIssue()
    {
        var eds = new ElectronicDataSheet { ApplicationProcess = new ApplicationProcess() };
        eds.ApplicationProcess.ParameterList.Add(new ApParameter
        {
            UniqueId = "P_1",
            TypeRef = new ApTypeRef { DataTypeIdRef = "MissingStruct" }
        });

        var issues = CanOpenModelValidator.Validate(eds);

        issues.Should().Contain(i => i.Path == "ApplicationProcess.ParameterList[0].TypeRef");
    }

    [Fact]
    public void Validate_ApplicationProcess_ValidDataTypeRef_ReturnsNoTypeRefIssue()
    {
        var eds = new ElectronicDataSheet { ApplicationProcess = new ApplicationProcess() };
        eds.ApplicationProcess.DataTypeList = new ApDataTypeList();
        eds.ApplicationProcess.DataTypeList.Structs.Add(new ApStructType { UniqueId = "DT_1", Name = "MyStruct" });
        eds.ApplicationProcess.ParameterList.Add(new ApParameter
        {
            UniqueId = "P_1",
            TypeRef = new ApTypeRef { DataTypeIdRef = "DT_1" }
        });

        var issues = CanOpenModelValidator.Validate(eds);

        issues.Should().NotContain(i => i.Path.Contains("TypeRef", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ApplicationProcess_TemplateAndVarDeclarationIds_EnforceGlobalUniqueness()
    {
        var eds = new ElectronicDataSheet { ApplicationProcess = new ApplicationProcess() };
        eds.ApplicationProcess.DataTypeList = new ApDataTypeList();
        var structType = new ApStructType { UniqueId = "DT_1", Name = "MyStruct" };
        structType.VarDeclarations.Add(new ApVarDeclaration { UniqueId = "VAR_1", Name = "Field1" });
        eds.ApplicationProcess.DataTypeList.Structs.Add(structType);
        eds.ApplicationProcess.TemplateList = new ApTemplateList();
        eds.ApplicationProcess.TemplateList.ParameterTemplates.Add(new ApParameterTemplate { UniqueId = "VAR_1" });

        var issues = CanOpenModelValidator.Validate(eds);

        issues.Should().Contain(i =>
            i.Path == "ApplicationProcess.TemplateList.ParameterTemplates[0].UniqueId" &&
            i.Message.Contains("Duplicate unique ID 'VAR_1'", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_Cpj_FacadeAndModelValidate_ReturnSameIssues()
    {
        // Arrange
        var cpj = new NodelistProject();
        cpj.Networks.Add(new NetworkTopology());
        cpj.Networks[0].Nodes[0] = new NetworkNode { NodeId = 0, Present = true };

        // Act
        var facadeIssues = CanOpenFile.Validate(cpj);
        var modelIssues = cpj.Validate();

        // Assert
        facadeIssues.Should().BeEquivalentTo(modelIssues);
        facadeIssues.Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_NodelistProject_Null_ThrowsArgumentNullException()
    {
        // Act
        var act = () => CanOpenModelValidator.Validate((NodelistProject)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static NodelistProject CreateValidCpj()
    {
        var cpj = new NodelistProject();
        var network = new NetworkTopology { NetName = "Main Network" };
        network.Nodes[2] = new NetworkNode { NodeId = 2, Present = true, Name = "Node-2" };
        cpj.Networks.Add(network);
        return cpj;
    }

    private static DeviceConfigurationFile CreateValidDcf()
    {
        var dcf = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning
            {
                NodeId = 5,
                Baudrate = 500,
                NodeName = "Node-5",
                NetworkName = "Main Network"
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
