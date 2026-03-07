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
