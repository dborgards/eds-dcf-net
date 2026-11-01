namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Tests.Utilities;
using EdsDcfNet.Validation;

/// <summary><see cref="CanOpenWriteOptions.AllowWarnings"/> on the validated write paths.</summary>
public class WriteAllowWarningsTests
{
    private static readonly CanOpenValidationOptions TextRules = new() { RequireIso646 = true, CheckLineLength = true };

    private static readonly CanOpenValidationOptions TextRulesAndListRanges = new()
    {
        RequireIso646 = true,
        CheckLineLength = true,
        CheckObjectListRanges = true,
    };

    [Fact]
    public void AllowWarnings_DefaultsToFalse()
    {
        new CanOpenWriteOptions().AllowWarnings.Should().BeFalse();
        CanOpenWriteOptions.Default.AllowWarnings.Should().BeFalse();
        CanOpenWriteOptions.Validated.AllowWarnings.Should().BeFalse();
    }

    [Theory]
    [InlineData("Eds")]
    [InlineData("Dcf")]
    public void WriteToString_WarningsOnly_AllowWarnings_WritesSameTextAsUnvalidated(string format)
    {
        var model = WithWarnings(format);

        var validated = WriteToString(format, model, Options(TextRules, allowWarnings: true));

        validated.Should().Be(WriteToString(format, model, CanOpenWriteOptions.Default));
    }

    [Theory]
    [InlineData("Eds")]
    [InlineData("Dcf")]
    public void WriteToString_WarningsOnly_WithoutAllowWarnings_Throws(string format)
    {
        var model = WithWarnings(format);

        var act = () => WriteToString(format, model, Options(TextRules, allowWarnings: false));

        act.Should().Throw<ModelValidationException>().Which.Issues
            .Should().OnlyContain(issue => issue.Severity == ValidationSeverity.Warning);
    }

    [Theory]
    [InlineData("Eds")]
    [InlineData("Dcf")]
    public void WriteToString_WarningAndError_AllowWarnings_ThrowsWithErrorsOnly(string format)
    {
        var model = WithWarnings(format);
        model.ObjectDictionary.OptionalObjects.Add(0x2000);

        var act = () => WriteToString(format, model, Options(TextRulesAndListRanges, allowWarnings: true));

        var issues = act.Should().Throw<ModelValidationException>().Which.Issues;
        issues.Should().NotBeEmpty().And.OnlyContain(issue => issue.Severity == ValidationSeverity.Error);
        issues.Should().Contain(issue => issue.Code == ValidationIssueCodes.ObjectListIndexOutOfRange);
    }

    [Fact]
    public async Task WriteStreamAsync_WarningsOnly_AllowWarnings_Writes()
    {
        var eds = (ElectronicDataSheet)WithWarnings("Eds");
        using var stream = new MemoryStream();

        await CanOpenFile.Eds.WriteStreamAsync(eds, stream, Options(TextRules, allowWarnings: true));

        stream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task WriteStreamAsync_WarningAndError_AllowWarnings_ThrowsWithErrorsOnly()
    {
        var dcf = (DeviceConfigurationFile)WithWarnings("Dcf");
        dcf.ObjectDictionary.OptionalObjects.Add(0x2000);
        using var stream = new MemoryStream();

        var act = () => CanOpenFile.Dcf.WriteStreamAsync(dcf, stream, Options(TextRulesAndListRanges, allowWarnings: true));

        (await act.Should().ThrowAsync<ModelValidationException>()).Which.Issues
            .Should().OnlyContain(issue => issue.Severity == ValidationSeverity.Error);
        stream.Length.Should().Be(0);
    }

    [Fact]
    public async Task WriteFileAsync_AllowWarnings_WritesWarningsAndKeepsTargetOnError()
    {
        var path = Path.GetTempFileName();
        try
        {
            var eds = (ElectronicDataSheet)WithWarnings("Eds");
            await CanOpenFile.Eds.WriteFileAsync(eds, path, Options(TextRules, allowWarnings: true));
            var written = File.ReadAllText(path);
            written.Should().Be(CanOpenFile.Eds.WriteToString(eds));

            eds.ObjectDictionary.OptionalObjects.Add(0x2000);
            var act = () => CanOpenFile.Eds.WriteFileAsync(eds, path, Options(TextRulesAndListRanges, allowWarnings: true));

            await act.Should().ThrowAsync<ModelValidationException>();
            (File.ReadAllText(path)).Should().Be(written);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteFile_WarningAndError_AllowWarnings_KeepsTarget()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "old");
            var dcf = (DeviceConfigurationFile)WithWarnings("Dcf");
            dcf.ObjectDictionary.OptionalObjects.Add(0x2000);

            var act = () => CanOpenFile.Dcf.WriteFile(dcf, path, Options(TextRulesAndListRanges, allowWarnings: true));

            act.Should().Throw<ModelValidationException>();
            File.ReadAllText(path).Should().Be("old");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteXdd_Error_AllowWarnings_Throws()
    {
        var xdd = ValidCanOpenModelBuilder.CreateValidEds();
        xdd.ObjectDictionary.OptionalObjects.Add(0x2000);

        var act = () => CanOpenFile.Xdd.WriteToString(xdd, Options(TextRulesAndListRanges, allowWarnings: true));

        act.Should().Throw<ModelValidationException>().Which.Issues
            .Should().Contain(issue => issue.Code == ValidationIssueCodes.ObjectListIndexOutOfRange);
    }

    [Fact]
    public async Task WriteXdcAsync_Error_AllowWarnings_Throws()
    {
        var xdc = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning { NodeId = 200, Baudrate = 250 },
        };
        using var stream = new MemoryStream();

        var act = () => CanOpenFile.Xdc.WriteStreamAsync(xdc, stream, new CanOpenWriteOptions { ValidateBeforeWrite = true, AllowWarnings = true });

        await act.Should().ThrowAsync<ModelValidationException>();
    }

    [Fact]
    public void WriteCpj_Error_AllowWarnings_Throws()
    {
        var cpj = new NodelistProject();
        cpj.Networks.Add(new NetworkTopology());
        cpj.Networks[0].Nodes[0] = new NetworkNode { NodeId = 0, Present = true };

        var act = () => CanOpenFile.Cpj.WriteToString(cpj, new CanOpenWriteOptions { ValidateBeforeWrite = true, AllowWarnings = true });

        act.Should().Throw<ModelValidationException>();
    }

    [Fact]
    public void AllowWarnings_WithoutValidateBeforeWrite_DoesNotValidate()
    {
        var eds = (ElectronicDataSheet)WithWarnings("Eds");
        eds.ObjectDictionary.OptionalObjects.Add(0x2000);
        var options = new CanOpenWriteOptions { ValidationOptions = TextRulesAndListRanges, AllowWarnings = true };

        var act = () => CanOpenFile.Eds.WriteToString(eds, options);

        act.Should().NotThrow();
    }

    private static CanOpenWriteOptions Options(CanOpenValidationOptions validation, bool allowWarnings) => new()
    {
        ValidateBeforeWrite = true,
        ValidationOptions = validation,
        AllowWarnings = allowWarnings,
    };

    private static ICanOpenFileModel WithWarnings(string format)
    {
        ICanOpenFileModel model = format == "Eds"
            ? ValidCanOpenModelBuilder.CreateValidEds()
            : ValidCanOpenModelBuilder.CreateValidDcf();
        model.ObjectDictionary.Objects[0x1000].ParameterName = "Gerät";
        model.FileInfo.Description = new string('a', 300);
        return model;
    }

    private static string WriteToString(string format, ICanOpenFileModel model, CanOpenWriteOptions options) =>
        format == "Eds"
            ? CanOpenFile.Eds.WriteToString((ElectronicDataSheet)model, options)
            : CanOpenFile.Dcf.WriteToString((DeviceConfigurationFile)model, options);
}
