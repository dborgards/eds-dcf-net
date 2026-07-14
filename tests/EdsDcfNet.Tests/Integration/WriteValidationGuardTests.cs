namespace EdsDcfNet.Tests.Integration;

using System.Globalization;
using System.Reflection;
using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;

using EdsDcfNet.Tests.Utilities;

public class WriteValidationGuardTests
{
    [Fact]
    public void EnsureValid_ValidDcf_DoesNotThrow()
    {
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();

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
        var eds = ValidCanOpenModelBuilder.CreateValidEds();

        var act = () => CanOpenFile.EnsureValid(eds);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureValid_ValidCpj_DoesNotThrow()
    {
        var cpj = ValidCanOpenModelBuilder.CreateValidCpj();

        var act = () => CanOpenFile.EnsureValid(cpj);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_CpjFacade_ReturnsNoIssuesForValidModel()
    {
        var cpj = ValidCanOpenModelBuilder.CreateValidCpj();

        CanOpenFile.Validate(cpj).Should().BeEmpty();
    }

    [Fact]
    public void WriteEds_WithValidatedOptions_FilePath_Succeeds()
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
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
        var eds = ValidCanOpenModelBuilder.CreateValidEds();

        var content = CanOpenFile.WriteEdsToString(eds, CanOpenWriteOptions.Validated);

        content.Should().Contain("[FileInfo]");
    }

    [Fact]
    public void WriteDcf_WithValidatedOptions_FilePath_Succeeds()
    {
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
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
        var content = CanOpenFile.WriteDcfToString(ValidCanOpenModelBuilder.CreateValidDcf(), CanOpenWriteOptions.Validated);

        content.Should().Contain("[DeviceCommissioning]");
    }

    [Fact]
    public void WriteCpj_WithValidatedOptions_Stream_Succeeds()
    {
        var cpj = ValidCanOpenModelBuilder.CreateValidCpj();
        using var stream = new MemoryStream();

        var act = () => CanOpenFile.WriteCpj(cpj, stream, CanOpenWriteOptions.Validated);

        act.Should().NotThrow();
        stream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void WriteCpjToString_WithValidatedOptions_ValidModel_ReturnsContent()
    {
        var content = CanOpenFile.WriteCpjToString(ValidCanOpenModelBuilder.CreateValidCpj(), CanOpenWriteOptions.Validated);

        content.Should().Contain("[Topology]");
    }

    [Fact]
    public void WriteXdd_WithValidatedOptions_FilePath_Succeeds()
    {
        var xdd = ValidCanOpenModelBuilder.CreateValidEds();
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
        var content = CanOpenFile.WriteXddToString(ValidCanOpenModelBuilder.CreateValidEds(), CanOpenWriteOptions.Validated);

        content.Should().Contain("ISO15745ProfileContainer");
    }

    [Fact]
    public void WriteXdc_WithValidatedOptions_FilePath_Succeeds()
    {
        var xdc = ValidCanOpenModelBuilder.CreateValidDcf();
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
        var content = CanOpenFile.WriteXdcToString(ValidCanOpenModelBuilder.CreateValidDcf(), CanOpenWriteOptions.Validated);

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
        var xdd = ValidCanOpenModelBuilder.CreateValidEds();
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
        var xdc = ValidCanOpenModelBuilder.CreateValidDcf();
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
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
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
            "Eds" => CanOpenFile.Eds.WriteToString(ValidCanOpenModelBuilder.CreateValidEds()),
            "Dcf" => CanOpenFile.Dcf.WriteToString(ValidCanOpenModelBuilder.CreateValidDcf()),
            "Cpj" => CanOpenFile.Cpj.WriteToString(ValidCanOpenModelBuilder.CreateValidCpj()),
            "Xdd" => CanOpenFile.Xdd.WriteToString(ValidCanOpenModelBuilder.CreateValidEds()),
            "Xdc" => CanOpenFile.Xdc.WriteToString(ValidCanOpenModelBuilder.CreateValidDcf()),
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
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
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
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
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
            "Eds" => CanOpenFile.Eds.WriteToString(ValidCanOpenModelBuilder.CreateValidEds(), CanOpenWriteOptions.Validated),
            "Dcf" => CanOpenFile.Dcf.WriteToString(ValidCanOpenModelBuilder.CreateValidDcf(), CanOpenWriteOptions.Validated),
            "Cpj" => CanOpenFile.Cpj.WriteToString(ValidCanOpenModelBuilder.CreateValidCpj(), CanOpenWriteOptions.Validated),
            "Xdd" => CanOpenFile.Xdd.WriteToString(ValidCanOpenModelBuilder.CreateValidEds(), CanOpenWriteOptions.Validated),
            "Xdc" => CanOpenFile.Xdc.WriteToString(ValidCanOpenModelBuilder.CreateValidDcf(), CanOpenWriteOptions.Validated),
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

    [Fact]
    public void EnsureValidForWrite_WithNullModelAndValidatedOptions_ThrowsArgumentNullException()
    {
        var ensureValidForWrite = GetEnsureValidForWriteMethod(typeof(ElectronicDataSheet));

        var act = () => ensureValidForWrite.Invoke(
            null,
            new object?[] { null, CanOpenWriteOptions.Validated });

        var exception = act.Should().Throw<TargetInvocationException>().Which.InnerException;
        exception.Should().BeOfType<ArgumentNullException>()
            .Which.ParamName.Should().Be("model");
    }

    [Fact]
    public void EnsureValidForWrite_WithUnsupportedModelType_ThrowsArgumentException()
    {
        var ensureValidForWrite = GetEnsureValidForWriteMethod(typeof(string));

        var act = () => ensureValidForWrite.Invoke(null, new object?[] { "unsupported", CanOpenWriteOptions.Validated });

        var exception = act.Should().Throw<TargetInvocationException>().Which.InnerException;
        exception.Should().BeOfType<ArgumentException>()
            .Which.Message.Should().Contain("Unsupported model type: String");
        ((ArgumentException)exception!).ParamName.Should().Be("model");
    }

    [Fact]
    public async Task EnsureValidForWriteAsync_WithValidationDisabled_ReturnsCompletedTask()
    {
        var ensureValidForWriteAsync = GetEnsureValidForWriteAsyncMethod(typeof(ElectronicDataSheet));

        var nullOptionsTask = (Task)ensureValidForWriteAsync.Invoke(
            null,
            new object?[] { ValidCanOpenModelBuilder.CreateValidEds(), null, CancellationToken.None })!;
        nullOptionsTask.IsCompletedSuccessfully.Should().BeTrue();
        await nullOptionsTask;

        var defaultOptionsTask = (Task)ensureValidForWriteAsync.Invoke(
            null,
            new object?[] { ValidCanOpenModelBuilder.CreateValidEds(), CanOpenWriteOptions.Default, CancellationToken.None })!;
        defaultOptionsTask.IsCompletedSuccessfully.Should().BeTrue();
        await defaultOptionsTask;
    }

    [Fact]
    public async Task EnsureValidForWriteAsync_WithNullModelAndValidatedOptions_ThrowsArgumentNullException()
    {
        var ensureValidForWriteAsync = GetEnsureValidForWriteAsyncMethod(typeof(ElectronicDataSheet));

        var act = async () => await (Task)ensureValidForWriteAsync.Invoke(
            null,
            new object?[] { null, CanOpenWriteOptions.Validated, CancellationToken.None })!;

        var exception = (await act.Should().ThrowAsync<TargetInvocationException>()).Which.InnerException;
        exception.Should().BeOfType<ArgumentNullException>()
            .Which.ParamName.Should().Be("model");
    }

    [Fact]
    public async Task EnsureValidForWriteAsync_WithUnsupportedModelType_ThrowsArgumentException()
    {
        var ensureValidForWriteAsync = GetEnsureValidForWriteAsyncMethod(typeof(string));

        var act = async () => await (Task)ensureValidForWriteAsync.Invoke(
            null,
            new object?[] { "unsupported", CanOpenWriteOptions.Validated, CancellationToken.None })!;

        var exception = (await act.Should().ThrowAsync<TargetInvocationException>()).Which.InnerException;
        exception.Should().BeOfType<ArgumentException>()
            .Which.Message.Should().Contain("Unsupported model type: String");
        ((ArgumentException)exception!).ParamName.Should().Be("model");
    }

    [Fact]
    public async Task EnsureValidForWriteAsync_ValidEdsDcfCpj_DoesNotThrow()
    {
        await InvokeEnsureValidForWriteAsync(ValidCanOpenModelBuilder.CreateValidEds());
        await InvokeEnsureValidForWriteAsync(ValidCanOpenModelBuilder.CreateValidDcf());
        await InvokeEnsureValidForWriteAsync(ValidCanOpenModelBuilder.CreateValidCpj());
    }

    private static async Task InvokeEnsureValidForWriteAsync<T>(T model)
    {
        var ensureValidForWriteAsync = GetEnsureValidForWriteAsyncMethod(typeof(T));
        await (Task)ensureValidForWriteAsync.Invoke(
            null,
            new object?[] { model, CanOpenWriteOptions.Validated, CancellationToken.None })!;
    }

    private static MethodInfo GetEnsureValidForWriteAsyncMethod(Type modelType)
    {
        var guardType = typeof(CanOpenFile).Assembly.GetType("EdsDcfNet.CanOpenWriteGuard")
            ?? throw new InvalidOperationException("CanOpenWriteGuard type not found.");

        var method = guardType.GetMethod(
            "EnsureValidForWriteAsync",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("EnsureValidForWriteAsync method not found.");

        return method.MakeGenericMethod(modelType);
    }

    private static MethodInfo GetEnsureValidForWriteMethod(Type modelType)
    {
        var guardType = typeof(CanOpenFile).Assembly.GetType("EdsDcfNet.CanOpenWriteGuard")
            ?? throw new InvalidOperationException("CanOpenWriteGuard type not found.");

        var method = guardType.GetMethod(
            "EnsureValidForWrite",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("EnsureValidForWrite method not found.");

        return method.MakeGenericMethod(modelType);
    }
}
