namespace EdsDcfNet.Tests.Integration;

using System.Globalization;
using System.Reflection;
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
    public async Task WriteEdsAsync_WithValidatedOptions_ThrowsForInvalidModel()
    {
        var eds = new ElectronicDataSheet();
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Unclassified",
            ObjectType = 0x7
        };

        using var stream = new MemoryStream();
        var act = () => CanOpenFile.WriteEdsAsync(eds, stream, CanOpenWriteOptions.Validated);

        await act.Should().ThrowAsync<ModelValidationException>();
    }

    [Fact]
    public async Task WriteDcfAsync_WithValidatedOptions_ThrowsForInvalidModel()
    {
        var dcf = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning { NodeId = 200, Baudrate = 250 }
        };

        using var stream = new MemoryStream();
        var act = () => CanOpenFile.WriteDcfAsync(dcf, stream, CanOpenWriteOptions.Validated);

        await act.Should().ThrowAsync<ModelValidationException>();
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

    [Theory]
    [InlineData("Eds")]
    [InlineData("Dcf")]
    [InlineData("Cpj")]
    [InlineData("Xdd")]
    [InlineData("Xdc")]
    public void FormatEntryPoint_WriteToString_LegacySignature_ReturnsContent(string format)
    {
        var content = format switch
        {
            "Eds" => CanOpenFile.Eds.WriteToString(CreateValidEds()),
            "Dcf" => CanOpenFile.Dcf.WriteToString(CreateValidDcf()),
            "Cpj" => CanOpenFile.Cpj.WriteToString(CreateValidCpj()),
            "Xdd" => CanOpenFile.Xdd.WriteToString(CreateValidEds()),
            "Xdc" => CanOpenFile.Xdc.WriteToString(CreateValidDcf()),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

        content.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(typeof(EdsCanOpenOperations), typeof(ElectronicDataSheet), nameof(EdsCanOpenOperations.WriteFile), typeof(string))]
    [InlineData(typeof(EdsCanOpenOperations), typeof(ElectronicDataSheet), nameof(EdsCanOpenOperations.WriteStream), typeof(Stream))]
    [InlineData(typeof(EdsCanOpenOperations), typeof(ElectronicDataSheet), nameof(EdsCanOpenOperations.WriteToString))]
    [InlineData(typeof(DcfCanOpenOperations), typeof(DeviceConfigurationFile), nameof(DcfCanOpenOperations.WriteFile), typeof(string))]
    [InlineData(typeof(DcfCanOpenOperations), typeof(DeviceConfigurationFile), nameof(DcfCanOpenOperations.WriteStream), typeof(Stream))]
    [InlineData(typeof(DcfCanOpenOperations), typeof(DeviceConfigurationFile), nameof(DcfCanOpenOperations.WriteToString))]
    [InlineData(typeof(CpjCanOpenOperations), typeof(NodelistProject), nameof(CpjCanOpenOperations.WriteFile), typeof(string))]
    [InlineData(typeof(CpjCanOpenOperations), typeof(NodelistProject), nameof(CpjCanOpenOperations.WriteStream), typeof(Stream))]
    [InlineData(typeof(CpjCanOpenOperations), typeof(NodelistProject), nameof(CpjCanOpenOperations.WriteToString))]
    [InlineData(typeof(XddCanOpenOperations), typeof(ElectronicDataSheet), nameof(XddCanOpenOperations.WriteFile), typeof(string))]
    [InlineData(typeof(XddCanOpenOperations), typeof(ElectronicDataSheet), nameof(XddCanOpenOperations.WriteStream), typeof(Stream))]
    [InlineData(typeof(XddCanOpenOperations), typeof(ElectronicDataSheet), nameof(XddCanOpenOperations.WriteToString))]
    [InlineData(typeof(XdcCanOpenOperations), typeof(DeviceConfigurationFile), nameof(XdcCanOpenOperations.WriteFile), typeof(string))]
    [InlineData(typeof(XdcCanOpenOperations), typeof(DeviceConfigurationFile), nameof(XdcCanOpenOperations.WriteStream), typeof(Stream))]
    [InlineData(typeof(XdcCanOpenOperations), typeof(DeviceConfigurationFile), nameof(XdcCanOpenOperations.WriteToString))]
    public void FormatEntryPoint_PreservesLegacyWriteMemberSignatures(
        Type operationsType,
        Type modelType,
        string methodName,
        params Type[] additionalParameterTypes)
    {
        var parameterTypes = new[] { modelType }.Concat(additionalParameterTypes).ToArray();

        var legacyMethod = operationsType.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: parameterTypes,
            modifiers: null);

        legacyMethod.Should().NotBeNull(
            because: "{0}.{1}({2}) must remain in the assembly for binary compatibility",
            operationsType.Name,
            methodName,
            string.Join(", ", parameterTypes.Select(t => t.Name)));
    }

    [Fact]
    public void EdsEntryPoint_WriteFile_LegacySignature_Succeeds()
    {
        var eds = CreateValidEds();
        var tempFile = Path.GetTempFileName();
        try
        {
            var act = () => CanOpenFile.Eds.WriteFile(eds, tempFile);

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
    public async Task EdsEntryPoint_WriteStreamAsync_LegacySignature_Succeeds()
    {
        var eds = CreateValidEds();
        using var stream = new MemoryStream();

        await CanOpenFile.Eds.WriteStreamAsync(eds, stream);

        stream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EdsEntryPoint_WriteToString_LegacySignature_SkipsValidationForInvalidModel()
    {
        var eds = new ElectronicDataSheet();
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Unclassified",
            ObjectType = 0x7
        };

        var act = () => CanOpenFile.Eds.WriteToString(eds);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("Eds")]
    [InlineData("Dcf")]
    [InlineData("Cpj")]
    [InlineData("Xdd")]
    [InlineData("Xdc")]
    public void FormatEntryPoint_WriteToString_WithValidatedOptions_ValidModel_ReturnsContent(string format)
    {
        var content = format switch
        {
            "Eds" => CanOpenFile.Eds.WriteToString(CreateValidEds(), CanOpenWriteOptions.Validated),
            "Dcf" => CanOpenFile.Dcf.WriteToString(CreateValidDcf(), CanOpenWriteOptions.Validated),
            "Cpj" => CanOpenFile.Cpj.WriteToString(CreateValidCpj(), CanOpenWriteOptions.Validated),
            "Xdd" => CanOpenFile.Xdd.WriteToString(CreateValidEds(), CanOpenWriteOptions.Validated),
            "Xdc" => CanOpenFile.Xdc.WriteToString(CreateValidDcf(), CanOpenWriteOptions.Validated),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

        content.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void EdsEntryPoint_WriteToString_WithValidatedOptions_ThrowsForInvalidModel()
    {
        var eds = new ElectronicDataSheet();
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Unclassified",
            ObjectType = 0x7
        };

        var act = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        act.Should().Throw<ModelValidationException>();
    }

    [Fact]
    public void DcfEntryPoint_WriteToString_WithValidatedOptions_ThrowsForInvalidModel()
    {
        var dcf = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning { NodeId = 200, Baudrate = 250 }
        };

        var act = () => CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);

        act.Should().Throw<ModelValidationException>();
    }

    [Fact]
    public void CpjEntryPoint_WriteToString_WithValidatedOptions_ThrowsForInvalidModel()
    {
        var cpj = new NodelistProject();
        cpj.Networks.Add(new NetworkTopology());
        cpj.Networks[0].Nodes[0] = new NetworkNode { NodeId = 0, Present = true };

        var act = () => CanOpenFile.Cpj.WriteToString(cpj, CanOpenWriteOptions.Validated);

        act.Should().Throw<ModelValidationException>();
    }

    [Fact]
    public void XddEntryPoint_WriteToString_WithValidatedOptions_ThrowsForInvalidModel()
    {
        var xdd = new ElectronicDataSheet();
        xdd.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Unclassified",
            ObjectType = 0x7
        };

        var act = () => CanOpenFile.Xdd.WriteToString(xdd, CanOpenWriteOptions.Validated);

        act.Should().Throw<ModelValidationException>();
    }

    [Fact]
    public void XdcEntryPoint_WriteToString_WithValidatedOptions_ThrowsForInvalidModel()
    {
        var xdc = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning { NodeId = 200, Baudrate = 250 }
        };

        var act = () => CanOpenFile.Xdc.WriteToString(xdc, CanOpenWriteOptions.Validated);

        act.Should().Throw<ModelValidationException>();
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
