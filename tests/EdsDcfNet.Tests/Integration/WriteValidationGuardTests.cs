namespace EdsDcfNet.Tests.Integration;

using System.Globalization;
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
    public void EnsureValid_SingleIssueDcf_FormatsModelValidationExceptionMessage()
    {
        var dcf = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning { NodeId = 200, Baudrate = 250 }
        };

        var act = () => CanOpenFile.EnsureValid(dcf);

        var exception = act.Should().Throw<ModelValidationException>().Which;
        exception.Issues.Should().ContainSingle();
        exception.Message.Should().Be(string.Format(
            CultureInfo.InvariantCulture,
            "Model validation failed: {0}",
            exception.Issues[0]));
    }

    [Fact]
    public void WriteDcfToString_WithValidatedOptions_SingleIssue_FormatsModelValidationExceptionMessage()
    {
        var dcf = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning { NodeId = 200, Baudrate = 250 }
        };

        var act = () => CanOpenFile.WriteDcfToString(dcf, CanOpenWriteOptions.Validated);

        var exception = act.Should().Throw<ModelValidationException>().Which;
        exception.Issues.Should().ContainSingle();
        exception.Message.Should().Be(string.Format(
            CultureInfo.InvariantCulture,
            "Model validation failed: {0}",
            exception.Issues[0]));
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
    public void EnsureValid_ValidEds_DoesNotThrow()
    {
        var eds = CreateValidEds();

        var act = () => CanOpenFile.EnsureValid(eds);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureValid_ValidCpj_DoesNotThrow()
    {
        var cpj = CreateValidCpj();

        var act = () => CanOpenFile.EnsureValid(cpj);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_CpjFacade_ReturnsNoIssuesForValidModel()
    {
        var cpj = CreateValidCpj();

        CanOpenFile.Validate(cpj).Should().BeEmpty();
    }

    [Fact]
    public void WriteEds_WithValidatedOptions_FilePath_Succeeds()
    {
        var eds = CreateValidEds();
        var tempFile = Path.GetTempFileName();
        try
        {
            var act = () => CanOpenFile.WriteEds(eds, tempFile, CanOpenWriteOptions.Validated);

            act.Should().NotThrow();
            File.Exists(tempFile).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void WriteEdsToString_WithValidatedOptions_ValidModel_ReturnsContent()
    {
        var eds = CreateValidEds();

        var content = CanOpenFile.WriteEdsToString(eds, CanOpenWriteOptions.Validated);

        content.Should().Contain("[FileInfo]");
    }

    [Fact]
    public void WriteDcf_WithValidatedOptions_FilePath_Succeeds()
    {
        var dcf = CreateValidDcf();
        var tempFile = Path.GetTempFileName();
        try
        {
            var act = () => CanOpenFile.WriteDcf(dcf, tempFile, CanOpenWriteOptions.Validated);

            act.Should().NotThrow();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void WriteDcfToString_WithValidatedOptions_ValidModel_ReturnsContent()
    {
        var content = CanOpenFile.WriteDcfToString(CreateValidDcf(), CanOpenWriteOptions.Validated);

        content.Should().Contain("[DeviceCommissioning]");
    }

    [Fact]
    public void WriteCpj_WithValidatedOptions_Stream_Succeeds()
    {
        var cpj = CreateValidCpj();
        using var stream = new MemoryStream();

        var act = () => CanOpenFile.WriteCpj(cpj, stream, CanOpenWriteOptions.Validated);

        act.Should().NotThrow();
        stream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void WriteCpjToString_WithValidatedOptions_ValidModel_ReturnsContent()
    {
        var content = CanOpenFile.WriteCpjToString(CreateValidCpj(), CanOpenWriteOptions.Validated);

        content.Should().Contain("[Topology]");
    }

    [Fact]
    public void WriteXdd_WithValidatedOptions_FilePath_Succeeds()
    {
        var xdd = CreateValidEds();
        var tempFile = Path.GetTempFileName();
        try
        {
            var act = () => CanOpenFile.WriteXdd(xdd, tempFile, CanOpenWriteOptions.Validated);

            act.Should().NotThrow();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void WriteXddToString_WithValidatedOptions_ValidModel_ReturnsContent()
    {
        var content = CanOpenFile.WriteXddToString(CreateValidEds(), CanOpenWriteOptions.Validated);

        content.Should().Contain("ISO15745ProfileContainer");
    }

    [Fact]
    public void WriteXdc_WithValidatedOptions_FilePath_Succeeds()
    {
        var xdc = CreateValidDcf();
        var tempFile = Path.GetTempFileName();
        try
        {
            var act = () => CanOpenFile.WriteXdc(xdc, tempFile, CanOpenWriteOptions.Validated);

            act.Should().NotThrow();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void WriteXdcToString_WithValidatedOptions_ValidModel_ReturnsContent()
    {
        var content = CanOpenFile.WriteXdcToString(CreateValidDcf(), CanOpenWriteOptions.Validated);

        content.Should().Contain("ISO15745ProfileContainer");
    }

    [Fact]
    public void WriteWithDefaultOptions_SkipsValidationForInvalidModel()
    {
        var dcf = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning { NodeId = 200, Baudrate = 250 }
        };

        var act = () => CanOpenFile.WriteDcfToString(dcf, CanOpenWriteOptions.Default);

        act.Should().NotThrow();
    }

    [Fact]
    public void WriteEdsToString_WithDefaultOptions_SkipsValidationForInvalidModel()
    {
        var eds = new ElectronicDataSheet();
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Unclassified",
            ObjectType = 0x7
        };

        var act = () => CanOpenFile.WriteEdsToString(eds, CanOpenWriteOptions.Default);

        act.Should().NotThrow();
    }

    [Fact]
    public void WriteCpjToString_WithDefaultOptions_SkipsValidationForInvalidModel()
    {
        var cpj = new NodelistProject();
        cpj.Networks.Add(new NetworkTopology());
        cpj.Networks[0].Nodes[0] = new NetworkNode { NodeId = 0, Present = true };

        var act = () => CanOpenFile.WriteCpjToString(cpj, CanOpenWriteOptions.Default);

        act.Should().NotThrow();
    }

    [Fact]
    public void WriteXdd_WithValidatedOptions_Stream_Succeeds()
    {
        var xdd = CreateValidEds();
        using var stream = new MemoryStream();

        var act = () => CanOpenFile.WriteXdd(xdd, stream, CanOpenWriteOptions.Validated);

        act.Should().NotThrow();
        stream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void WriteXdd_WithValidatedOptions_Stream_ThrowsForInvalidModel()
    {
        var xdd = new ElectronicDataSheet();
        xdd.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Unclassified",
            ObjectType = 0x7
        };
        using var stream = new MemoryStream();

        var act = () => CanOpenFile.WriteXdd(xdd, stream, CanOpenWriteOptions.Validated);

        act.Should().Throw<ModelValidationException>();
    }

    [Fact]
    public void WriteXdc_WithValidatedOptions_Stream_Succeeds()
    {
        var xdc = CreateValidDcf();
        using var stream = new MemoryStream();

        var act = () => CanOpenFile.WriteXdc(xdc, stream, CanOpenWriteOptions.Validated);

        act.Should().NotThrow();
        stream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void WriteXdc_WithValidatedOptions_Stream_ThrowsForInvalidModel()
    {
        var xdc = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning { NodeId = 200, Baudrate = 250 }
        };
        using var stream = new MemoryStream();

        var act = () => CanOpenFile.WriteXdc(xdc, stream, CanOpenWriteOptions.Validated);

        act.Should().Throw<ModelValidationException>();
    }

    [Fact]
    public void WriteDcf_WithValidatedOptions_Stream_Succeeds()
    {
        var dcf = CreateValidDcf();
        using var stream = new MemoryStream();

        var act = () => CanOpenFile.WriteDcf(dcf, stream, CanOpenWriteOptions.Validated);

        act.Should().NotThrow();
        stream.Length.Should().BeGreaterThan(0);
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

    private static ElectronicDataSheet CreateValidEds()
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

        return eds;
    }

    private static NodelistProject CreateValidCpj()
    {
        var cpj = new NodelistProject();
        var network = new NetworkTopology { NetName = "Main Network" };
        network.Nodes[2] = new NetworkNode { NodeId = 2, Present = true, Name = "Node-2" };
        cpj.Networks.Add(network);
        return cpj;
    }
}
