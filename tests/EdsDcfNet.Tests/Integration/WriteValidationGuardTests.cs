namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;

public class WriteValidationGuardTests
{
    [Fact]
    public void EnsureValid_ValidDcf_DoesNotThrow()
    {
        var dcf = CreateValidDcf();

        var act = () => CanOpenFile.EnsureValid(dcf);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureValid_InvalidEds_ThrowsModelValidationException()
    {
        var eds = new ElectronicDataSheet();
        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);

        var act = () => CanOpenFile.EnsureValid(eds);

        act.Should().Throw<ModelValidationException>();
    }

    [Fact]
    public void EnsureValid_InvalidCpj_ThrowsModelValidationException()
    {
        var cpj = new NodelistProject();
        cpj.Networks.Add(new NetworkTopology());
        cpj.Networks[0].Nodes[0] = new NetworkNode { NodeId = 0, Present = true };

        var act = () => CanOpenFile.EnsureValid(cpj);

        act.Should().Throw<ModelValidationException>();
    }

    [Fact]
    public void WriteEdsToString_WithValidatedOptions_ThrowsForInvalidModel()
    {
        var eds = new ElectronicDataSheet();
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Unclassified",
            ObjectType = 0x7
        };

        var act = () => CanOpenFile.WriteEdsToString(eds, CanOpenWriteOptions.Validated);

        act.Should().Throw<ModelValidationException>();
    }

    [Fact]
    public void WriteXddToString_WithValidatedOptions_ThrowsForInvalidModel()
    {
        var xdd = new ElectronicDataSheet();
        xdd.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Unclassified",
            ObjectType = 0x7
        };

        var act = () => CanOpenFile.WriteXddToString(xdd, CanOpenWriteOptions.Validated);

        act.Should().Throw<ModelValidationException>();
    }

    [Fact]
    public void WriteXdcToString_WithValidatedOptions_ThrowsForInvalidModel()
    {
        var xdc = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning { NodeId = 200, Baudrate = 250 }
        };

        var act = () => CanOpenFile.WriteXdcToString(xdc, CanOpenWriteOptions.Validated);

        act.Should().Throw<ModelValidationException>();
    }

    [Fact]
    public void WriteDcf_WithValidatedStreamOverload_ThrowsForInvalidModel()
    {
        var dcf = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning { NodeId = 200, Baudrate = 250 }
        };

        using var stream = new MemoryStream();

        var act = () => CanOpenFile.WriteDcf(dcf, stream, CanOpenWriteOptions.Validated);

        act.Should().Throw<ModelValidationException>();
    }

    [Fact]
    public void WriteCpj_WithValidatedFileOverload_ThrowsForInvalidModel()
    {
        var cpj = new NodelistProject();
        cpj.Networks.Add(new NetworkTopology());
        cpj.Networks[0].Nodes[0] = new NetworkNode { NodeId = 0, Present = true };

        var tempFile = Path.GetTempFileName();
        try
        {
            var act = () => CanOpenFile.WriteCpj(cpj, tempFile, CanOpenWriteOptions.Validated);

            act.Should().Throw<ModelValidationException>();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void WriteEds_WithValidatedOptions_AllowsValidModel()
    {
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

        using var stream = new MemoryStream();

        var act = () => CanOpenFile.WriteEds(eds, stream, CanOpenWriteOptions.Validated);

        act.Should().NotThrow();
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
