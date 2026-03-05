namespace EdsDcfNet.Tests.Validation;

using EdsDcfNet.Models;
using EdsDcfNet.Validation;
using FluentAssertions;

public class CanOpenValidatorTests
{
    [Fact]
    public void Validate_ValidDcf_ReturnsNoErrors()
    {
        var dcf = CreateValidDcf();
        var result = CanOpenValidator.Validate(dcf);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(128)]
    [InlineData(255)]
    public void Validate_NodeIdOutOfRange_ReturnsError(byte nodeId)
    {
        var dcf = CreateValidDcf();
        dcf.DeviceCommissioning.NodeId = nodeId;

        var result = CanOpenValidator.Validate(dcf);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "NODE_ID_RANGE");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(64)]
    [InlineData(127)]
    public void Validate_NodeIdInRange_NoError(byte nodeId)
    {
        var dcf = CreateValidDcf();
        dcf.DeviceCommissioning.NodeId = nodeId;

        var result = CanOpenValidator.Validate(dcf);

        result.Errors.Should().NotContain(e => e.Code == "NODE_ID_RANGE");
    }

    [Theory]
    [InlineData((ushort)15)]
    [InlineData((ushort)99)]
    [InlineData((ushort)333)]
    public void Validate_NonStandardBaudrate_ReturnsError(ushort baudrate)
    {
        var dcf = CreateValidDcf();
        dcf.DeviceCommissioning.Baudrate = baudrate;

        var result = CanOpenValidator.Validate(dcf);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "BAUDRATE_INVALID");
    }

    [Theory]
    [InlineData((ushort)10)]
    [InlineData((ushort)125)]
    [InlineData((ushort)250)]
    [InlineData((ushort)500)]
    [InlineData((ushort)1000)]
    public void Validate_StandardBaudrate_NoError(ushort baudrate)
    {
        var dcf = CreateValidDcf();
        dcf.DeviceCommissioning.Baudrate = baudrate;

        var result = CanOpenValidator.Validate(dcf);

        result.Errors.Should().NotContain(e => e.Code == "BAUDRATE_INVALID");
    }

    [Fact]
    public void Validate_NodeNameTooLong_ReturnsError()
    {
        var dcf = CreateValidDcf();
        dcf.DeviceCommissioning.NodeName = new string('A', 247);

        var result = CanOpenValidator.Validate(dcf);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "NODE_NAME_TOO_LONG");
    }

    [Fact]
    public void Validate_NetworkNameTooLong_ReturnsError()
    {
        var dcf = CreateValidDcf();
        dcf.DeviceCommissioning.NetworkName = new string('N', 244);

        var result = CanOpenValidator.Validate(dcf);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "NETWORK_NAME_TOO_LONG");
    }

    [Fact]
    public void Validate_VendorNameTooLong_ReturnsError()
    {
        var dcf = CreateValidDcf();
        dcf.DeviceInfo.VendorName = new string('V', 245);

        var result = CanOpenValidator.Validate(dcf);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "VENDOR_NAME_TOO_LONG");
    }

    [Fact]
    public void Validate_ProductNameTooLong_ReturnsError()
    {
        var dcf = CreateValidDcf();
        dcf.DeviceInfo.ProductName = new string('P', 244);

        var result = CanOpenValidator.Validate(dcf);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "PRODUCT_NAME_TOO_LONG");
    }

    [Fact]
    public void Validate_GranularityTooHigh_ReturnsError()
    {
        var dcf = CreateValidDcf();
        dcf.DeviceInfo.Granularity = 65;

        var result = CanOpenValidator.Validate(dcf);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "GRANULARITY_RANGE");
    }

    [Fact]
    public void Validate_InvalidObjectType_ReturnsError()
    {
        var dcf = CreateValidDcf();
        dcf.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Test",
            ObjectType = 0xAA
        };

        var result = CanOpenValidator.Validate(dcf);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "OBJECT_TYPE_INVALID");
    }

    [Fact]
    public void Validate_ParameterNameTooLong_ReturnsError()
    {
        var dcf = CreateValidDcf();
        dcf.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = new string('X', 242)
        };

        var result = CanOpenValidator.Validate(dcf);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "PARAMETER_NAME_TOO_LONG");
    }

    [Fact]
    public void Validate_SubNumberMismatch_ReturnsError()
    {
        var dcf = CreateValidDcf();
        dcf.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Test",
            SubNumber = 3
        };

        var result = CanOpenValidator.Validate(dcf);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "SUB_NUMBER_MISMATCH");
    }

    [Fact]
    public void Validate_ValidEds_ReturnsNoErrors()
    {
        var eds = new ElectronicDataSheet
        {
            DeviceInfo = new DeviceInfo
            {
                VendorName = "TestVendor",
                ProductName = "TestProduct"
            }
        };

        var result = CanOpenValidator.Validate(eds);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MultipleErrors_ReportsAll()
    {
        var dcf = CreateValidDcf();
        dcf.DeviceCommissioning.NodeId = 0;
        dcf.DeviceCommissioning.Baudrate = 42;
        dcf.DeviceInfo.VendorName = new string('V', 245);

        var result = CanOpenValidator.Validate(dcf);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterOrEqualTo(3);
    }

    [Fact]
    public void ValidationError_ToString_IncludesContext()
    {
        var error = new ValidationError("TEST_CODE", "test message", "[DeviceCommissioning]");
        error.ToString().Should().Contain("[DeviceCommissioning]");
        error.ToString().Should().Contain("TEST_CODE");
        error.ToString().Should().Contain("test message");
    }

    [Fact]
    public void ValidationError_ToString_WithoutContext_StillWorks()
    {
        var error = new ValidationError("TEST_CODE", "test message");
        error.ToString().Should().Contain("TEST_CODE");
        error.ToString().Should().Contain("test message");
    }

    private static DeviceConfigurationFile CreateValidDcf()
    {
        return new DeviceConfigurationFile
        {
            DeviceInfo = new DeviceInfo
            {
                VendorName = "TestVendor",
                ProductName = "TestProduct",
                Granularity = 8
            },
            DeviceCommissioning = new DeviceCommissioning
            {
                NodeId = 1,
                NodeName = "Node1",
                Baudrate = 250,
                NetworkName = "TestNetwork"
            }
        };
    }
}
