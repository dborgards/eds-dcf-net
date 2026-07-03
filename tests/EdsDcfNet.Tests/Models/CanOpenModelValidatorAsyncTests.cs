namespace EdsDcfNet.Tests.Models;

using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Validation;

public class CanOpenModelValidatorAsyncTests
{
    [Fact]
    public async Task ValidateAsync_Dcf_ReturnsSameIssuesAsSync()
    {
        // Arrange
        var dcf = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning { NodeId = 200, Baudrate = 42 }
        };
        dcf.ObjectDictionary.MandatoryObjects.Add(0x1000);

        // Act
        var syncIssues = CanOpenModelValidator.Validate(dcf);
        var asyncIssues = await CanOpenModelValidator.ValidateAsync(dcf);

        // Assert
        asyncIssues.Select(i => i.ToString())
            .Should().Equal(syncIssues.Select(i => i.ToString()));
    }

    [Fact]
    public async Task ValidateAsync_Eds_ReturnsSameIssuesAsSync()
    {
        // Arrange
        var eds = new ElectronicDataSheet();
        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);

        // Act
        var syncIssues = CanOpenModelValidator.Validate(eds);
        var asyncIssues = await CanOpenModelValidator.ValidateAsync(eds);

        // Assert
        asyncIssues.Select(i => i.ToString())
            .Should().Equal(syncIssues.Select(i => i.ToString()));
    }

    [Fact]
    public async Task ValidateAsync_Cpj_ReturnsSameIssuesAsSync()
    {
        // Arrange
        var cpj = new NodelistProject();
        cpj.Networks.Add(new NetworkTopology());
        cpj.Networks[0].Nodes[0] = new NetworkNode { NodeId = 0, Present = true };

        // Act
        var syncIssues = CanOpenModelValidator.Validate(cpj);
        var asyncIssues = await CanOpenModelValidator.ValidateAsync(cpj);

        // Assert
        asyncIssues.Select(i => i.ToString())
            .Should().Equal(syncIssues.Select(i => i.ToString()));
    }

    [Fact]
    public async Task ValidateAsync_ValidModel_ReturnsNoIssues()
    {
        // Arrange
        var eds = CreateValidEds();

        // Act
        var issues = await CanOpenFile.ValidateAsync(eds);

        // Assert
        issues.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_PreCanceledToken_ThrowsOperationCanceled()
    {
        // Arrange
        var eds = CreateValidEds();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => CanOpenModelValidator.ValidateAsync(eds, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ValidateAsync_LargeModelCanceledMidRun_ThrowsOperationCanceled()
    {
        // Arrange — model large enough to hit many per-object cancellation checkpoints
        var eds = CreateLargeEds(objectCount: 50_000);
        using var cts = new CancellationTokenSource();

        // Act — cancel immediately after starting; validation of 50k objects cannot
        // complete before Cancel() executes, so the checkpoint (or task scheduling)
        // must observe the cancellation.
        var task = CanOpenModelValidator.ValidateAsync(eds, cts.Token);
        cts.Cancel();
        var act = () => task;

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task EnsureValidAsync_InvalidDcf_ThrowsModelValidationException()
    {
        // Arrange
        var dcf = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning { NodeId = 200, Baudrate = 250 }
        };

        // Act
        var act = () => CanOpenFile.EnsureValidAsync(dcf);

        // Assert
        await act.Should().ThrowAsync<ModelValidationException>();
    }

    [Fact]
    public async Task EnsureValidAsync_ValidEdsDcfCpj_DoesNotThrow()
    {
        await CanOpenFile.EnsureValidAsync(CreateValidEds());
        await CanOpenFile.EnsureValidAsync(CreateValidDcf());
        await CanOpenFile.EnsureValidAsync(CreateValidCpj());
    }

    [Fact]
    public async Task WriteFileAsync_ValidatedOptions_PreCanceledToken_ThrowsOperationCanceled()
    {
        // Arrange — invalid model, but cancellation must win over validation
        var dcf = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning { NodeId = 200, Baudrate = 250 }
        };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => CanOpenFile.Dcf.WriteFileAsync(
            dcf, Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
            CanOpenWriteOptions.Validated, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WriteStreamAsync_ValidatedOptions_InvalidModel_ThrowsModelValidationException()
    {
        // Arrange
        var eds = new ElectronicDataSheet();
        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);
        using var stream = new MemoryStream();

        // Act
        var act = () => CanOpenFile.Eds.WriteStreamAsync(eds, stream, CanOpenWriteOptions.Validated);

        // Assert
        await act.Should().ThrowAsync<ModelValidationException>();
    }

    [Fact]
    public async Task WriteStreamAsync_ValidatedOptions_ValidModel_WritesContent()
    {
        // Arrange
        var eds = CreateValidEds();
        using var stream = new MemoryStream();

        // Act
        await CanOpenFile.Eds.WriteStreamAsync(eds, stream, CanOpenWriteOptions.Validated);

        // Assert
        stream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task WriteStreamAsync_DefaultOptions_SkipsValidationForInvalidModel()
    {
        // Arrange
        var eds = new ElectronicDataSheet();
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Unclassified",
            ObjectType = 0x7
        };
        using var stream = new MemoryStream();

        // Act
        var act = () => CanOpenFile.Eds.WriteStreamAsync(eds, stream);

        // Assert
        await act.Should().NotThrowAsync();
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

    private static DeviceConfigurationFile CreateValidDcf()
    {
        var dcf = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning { NodeId = 5, Baudrate = 500 }
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

    private static NodelistProject CreateValidCpj()
    {
        var cpj = new NodelistProject();
        var network = new NetworkTopology { NetName = "Main Network" };
        network.Nodes[2] = new NetworkNode { NodeId = 2, Present = true, Name = "Node-2" };
        cpj.Networks.Add(network);
        return cpj;
    }

    private static ElectronicDataSheet CreateLargeEds(int objectCount)
    {
        var eds = new ElectronicDataSheet();
        for (var i = 0; i < objectCount; i++)
        {
            var index = (ushort)(0x2000 + i % 0x9000);
            if (eds.ObjectDictionary.Objects.ContainsKey(index))
                continue;

            eds.ObjectDictionary.ManufacturerObjects.Add(index);
            eds.ObjectDictionary.Objects[index] = new CanOpenObject
            {
                Index = index,
                ParameterName = "Manufacturer Object " + i,
                ObjectType = 0x7,
                DataType = 0x0007,
                AccessType = AccessType.ReadWrite
            };
        }

        return eds;
    }
}
