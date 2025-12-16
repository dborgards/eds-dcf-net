namespace EdsDcfNet.Tests.Extensions;

using EdsDcfNet.Extensions;
using EdsDcfNet.Models;
using FluentAssertions;
using Xunit;

public class ObjectDictionaryExtensionsTests
{
    private ObjectDictionary CreateTestDictionary()
    {
        var dict = new ObjectDictionary();

        // Add mandatory object
        dict.MandatoryObjects.Add(0x1000);
        dict.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "0x00000191",
            PdoMapping = false
        };

        // Add optional object
        dict.OptionalObjects.Add(0x1008);
        dict.Objects[0x1008] = new CanOpenObject
        {
            Index = 0x1008,
            ParameterName = "Manufacturer Device Name",
            ObjectType = 0x7,
            DataType = 0x0009,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "Test Device",
            PdoMapping = false
        };

        // Add manufacturer object
        dict.ManufacturerObjects.Add(0x2000);
        dict.Objects[0x2000] = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "Custom Object",
            ObjectType = 0x7,
            DataType = 0x0005,
            AccessType = AccessType.ReadWrite,
            DefaultValue = "100",
            PdoMapping = true
        };

        // Add object with sub-objects (Identity Object 0x1018)
        dict.MandatoryObjects.Add(0x1018);
        dict.Objects[0x1018] = new CanOpenObject
        {
            Index = 0x1018,
            ParameterName = "Identity Object",
            ObjectType = 0x9,
            SubNumber = 4
        };

        dict.Objects[0x1018].SubObjects[0] = new CanOpenSubObject
        {
            SubIndex = 0,
            ParameterName = "Number of Entries",
            ObjectType = 0x7,
            DataType = 0x0005,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "4",
            PdoMapping = false
        };

        dict.Objects[0x1018].SubObjects[1] = new CanOpenSubObject
        {
            SubIndex = 1,
            ParameterName = "Vendor ID",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "0x00000100",
            PdoMapping = false
        };

        // Add RPDO communication parameter (0x1400)
        dict.Objects[0x1400] = new CanOpenObject
        {
            Index = 0x1400,
            ParameterName = "RPDO Communication Parameter",
            ObjectType = 0x9,
            SubNumber = 2
        };

        // Add RPDO mapping parameter (0x1600)
        dict.Objects[0x1600] = new CanOpenObject
        {
            Index = 0x1600,
            ParameterName = "RPDO Mapping Parameter",
            ObjectType = 0x9,
            SubNumber = 0
        };

        // Add TPDO communication parameter (0x1800)
        dict.Objects[0x1800] = new CanOpenObject
        {
            Index = 0x1800,
            ParameterName = "TPDO Communication Parameter",
            ObjectType = 0x9,
            SubNumber = 2
        };

        // Add TPDO mapping parameter (0x1A00)
        dict.Objects[0x1A00] = new CanOpenObject
        {
            Index = 0x1A00,
            ParameterName = "TPDO Mapping Parameter",
            ObjectType = 0x9,
            SubNumber = 0
        };

        return dict;
    }

    #region GetObject Tests

    [Fact]
    public void GetObject_ExistingObject_ReturnsObject()
    {
        // Arrange
        var dict = CreateTestDictionary();

        // Act
        var result = dict.GetObject(0x1000);

        // Assert
        result.Should().NotBeNull();
        result!.ParameterName.Should().Be("Device Type");
        result.Index.Should().Be(0x1000);
    }

    [Fact]
    public void GetObject_NonExistentObject_ReturnsNull()
    {
        // Arrange
        var dict = CreateTestDictionary();

        // Act
        var result = dict.GetObject(0x9999);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetSubObject Tests

    [Fact]
    public void GetSubObject_ExistingSubObject_ReturnsSubObject()
    {
        // Arrange
        var dict = CreateTestDictionary();

        // Act
        var result = dict.GetSubObject(0x1018, 0);

        // Assert
        result.Should().NotBeNull();
        result!.ParameterName.Should().Be("Number of Entries");
        result.SubIndex.Should().Be(0);
    }

    [Fact]
    public void GetSubObject_NonExistentObject_ReturnsNull()
    {
        // Arrange
        var dict = CreateTestDictionary();

        // Act
        var result = dict.GetSubObject(0x9999, 0);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetSubObject_NonExistentSubIndex_ReturnsNull()
    {
        // Arrange
        var dict = CreateTestDictionary();

        // Act
        var result = dict.GetSubObject(0x1018, 99);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetSubObject_ObjectWithoutSubObjects_ReturnsNull()
    {
        // Arrange
        var dict = CreateTestDictionary();

        // Act
        var result = dict.GetSubObject(0x1000, 0);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region SetParameterValue Tests

    [Fact]
    public void SetParameterValue_ExistingObject_SetsValue()
    {
        // Arrange
        var dict = CreateTestDictionary();

        // Act
        dict.SetParameterValue(0x1000, "0x12345678");

        // Assert
        dict.Objects[0x1000].ParameterValue.Should().Be("0x12345678");
    }

    [Fact]
    public void SetParameterValue_NonExistentObject_DoesNothing()
    {
        // Arrange
        var dict = CreateTestDictionary();

        // Act
        var act = () => dict.SetParameterValue(0x9999, "value");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void SetParameterValue_SubObject_SetsValue()
    {
        // Arrange
        var dict = CreateTestDictionary();

        // Act
        dict.SetParameterValue(0x1018, 1, "0xABCD");

        // Assert
        dict.Objects[0x1018].SubObjects[1].ParameterValue.Should().Be("0xABCD");
    }

    [Fact]
    public void SetParameterValue_NonExistentSubObject_DoesNothing()
    {
        // Arrange
        var dict = CreateTestDictionary();

        // Act
        var act = () => dict.SetParameterValue(0x1018, 99, "value");

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region GetParameterValue Tests

    [Fact]
    public void GetParameterValue_ObjectWithParameterValue_ReturnsParameterValue()
    {
        // Arrange
        var dict = CreateTestDictionary();
        dict.SetParameterValue(0x1000, "0xABCD");

        // Act
        var result = dict.GetParameterValue(0x1000);

        // Assert
        result.Should().Be("0xABCD");
    }

    [Fact]
    public void GetParameterValue_ObjectWithoutParameterValue_ReturnsDefaultValue()
    {
        // Arrange
        var dict = CreateTestDictionary();

        // Act
        var result = dict.GetParameterValue(0x1000);

        // Assert
        result.Should().Be("0x00000191"); // Default value
    }

    [Fact]
    public void GetParameterValue_NonExistentObject_ReturnsNull()
    {
        // Arrange
        var dict = CreateTestDictionary();

        // Act
        var result = dict.GetParameterValue(0x9999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetParameterValue_SubObjectWithParameterValue_ReturnsParameterValue()
    {
        // Arrange
        var dict = CreateTestDictionary();
        dict.SetParameterValue(0x1018, 1, "0xFFFF");

        // Act
        var result = dict.GetParameterValue(0x1018, 1);

        // Assert
        result.Should().Be("0xFFFF");
    }

    [Fact]
    public void GetParameterValue_SubObjectWithoutParameterValue_ReturnsDefaultValue()
    {
        // Arrange
        var dict = CreateTestDictionary();

        // Act
        var result = dict.GetParameterValue(0x1018, 1);

        // Assert
        result.Should().Be("0x00000100"); // Default value
    }

    [Fact]
    public void GetParameterValue_NonExistentSubObject_ReturnsNull()
    {
        // Arrange
        var dict = CreateTestDictionary();

        // Act
        var result = dict.GetParameterValue(0x1018, 99);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetObjectsByType Tests

    [Fact]
    public void GetObjectsByType_Mandatory_ReturnsMandatoryObjects()
    {
        // Arrange
        var dict = CreateTestDictionary();

        // Act
        var result = dict.GetObjectsByType(ObjectCategory.Mandatory).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(obj => obj.Index == 0x1000);
        result.Should().Contain(obj => obj.Index == 0x1018);
    }

    [Fact]
    public void GetObjectsByType_Optional_ReturnsOptionalObjects()
    {
        // Arrange
        var dict = CreateTestDictionary();

        // Act
        var result = dict.GetObjectsByType(ObjectCategory.Optional).ToList();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(obj => obj.Index == 0x1008);
    }

    [Fact]
    public void GetObjectsByType_Manufacturer_ReturnsManufacturerObjects()
    {
        // Arrange
        var dict = CreateTestDictionary();

        // Act
        var result = dict.GetObjectsByType(ObjectCategory.Manufacturer).ToList();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(obj => obj.Index == 0x2000);
    }

    [Fact]
    public void GetObjectsByType_EmptyCategory_ReturnsEmptyEnumerable()
    {
        // Arrange
        var dict = new ObjectDictionary();

        // Act
        var result = dict.GetObjectsByType(ObjectCategory.Mandatory).ToList();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetPdoCommunicationParameters Tests

    [Fact]
    public void GetPdoCommunicationParameters_Transmit_ReturnsTPDOCommParameters()
    {
        // Arrange
        var dict = CreateTestDictionary();

        // Act
        var result = dict.GetPdoCommunicationParameters(transmit: true).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Index.Should().Be(0x1800);
        result[0].ParameterName.Should().Contain("TPDO");
    }

    [Fact]
    public void GetPdoCommunicationParameters_Receive_ReturnsRPDOCommParameters()
    {
        // Arrange
        var dict = CreateTestDictionary();

        // Act
        var result = dict.GetPdoCommunicationParameters(transmit: false).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Index.Should().Be(0x1400);
        result[0].ParameterName.Should().Contain("RPDO");
    }

    [Fact]
    public void GetPdoCommunicationParameters_MultipleTPDOs_ReturnsAllInOrder()
    {
        // Arrange
        var dict = CreateTestDictionary();
        dict.Objects[0x1801] = new CanOpenObject { Index = 0x1801, ParameterName = "TPDO 2" };
        dict.Objects[0x1802] = new CanOpenObject { Index = 0x1802, ParameterName = "TPDO 3" };

        // Act
        var result = dict.GetPdoCommunicationParameters(transmit: true).ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].Index.Should().Be(0x1800);
        result[1].Index.Should().Be(0x1801);
        result[2].Index.Should().Be(0x1802);
    }

    [Fact]
    public void GetPdoCommunicationParameters_NoPDOs_ReturnsEmpty()
    {
        // Arrange
        var dict = new ObjectDictionary();

        // Act
        var result = dict.GetPdoCommunicationParameters(transmit: true).ToList();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetPdoMappingParameters Tests

    [Fact]
    public void GetPdoMappingParameters_Transmit_ReturnsTPDOMappingParameters()
    {
        // Arrange
        var dict = CreateTestDictionary();

        // Act
        var result = dict.GetPdoMappingParameters(transmit: true).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Index.Should().Be(0x1A00);
        result[0].ParameterName.Should().Contain("TPDO");
    }

    [Fact]
    public void GetPdoMappingParameters_Receive_ReturnsRPDOMappingParameters()
    {
        // Arrange
        var dict = CreateTestDictionary();

        // Act
        var result = dict.GetPdoMappingParameters(transmit: false).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Index.Should().Be(0x1600);
        result[0].ParameterName.Should().Contain("RPDO");
    }

    [Fact]
    public void GetPdoMappingParameters_MultipleRPDOs_ReturnsAllInOrder()
    {
        // Arrange
        var dict = CreateTestDictionary();
        dict.Objects[0x1601] = new CanOpenObject { Index = 0x1601, ParameterName = "RPDO 2 Mapping" };
        dict.Objects[0x1602] = new CanOpenObject { Index = 0x1602, ParameterName = "RPDO 3 Mapping" };

        // Act
        var result = dict.GetPdoMappingParameters(transmit: false).ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].Index.Should().Be(0x1600);
        result[1].Index.Should().Be(0x1601);
        result[2].Index.Should().Be(0x1602);
    }

    [Fact]
    public void GetPdoMappingParameters_NoPDOs_ReturnsEmpty()
    {
        // Arrange
        var dict = new ObjectDictionary();

        // Act
        var result = dict.GetPdoMappingParameters(transmit: true).ToList();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion
}
