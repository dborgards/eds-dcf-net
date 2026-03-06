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
    public void Validate_NodeIdZero_IsAcceptedAsUnconfigured()
    {
        // Arrange
        var dcf = CreateValidDcf();
        dcf.DeviceCommissioning.NodeId = 0;

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
