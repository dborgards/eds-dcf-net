namespace EdsDcfNet.Tests.Writers;

using EdsDcfNet.Models;
using EdsDcfNet.Writers;

/// <summary>
/// A <see langword="null"/> model is a caller bug, not a write failure: every writer throws
/// <see cref="ArgumentNullException"/> (parameter name = model parameter) instead of wrapping
/// a <see cref="NullReferenceException"/> into its format-specific write exception.
/// </summary>
public class WriterNullModelTests
{
    private static string TempPath(string extension)
        => Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.{extension}");

    private static object[] Row(string name, string param, Func<Task> call) => new object[] { name, param, call };

    public static IEnumerable<object[]> NullModelCalls()
    {
        var eds = new EdsWriter();
        var dcf = new DcfWriter();
        var cpj = new CpjWriter();
        var xdd = new XddWriter();
        var xdc = new XdcWriter();

        yield return Row("Eds.GenerateString", "eds", () => { eds.GenerateString(null!); return Task.CompletedTask; });
        yield return Row("Eds.WriteFile", "eds", () => { eds.WriteFile(null!, TempPath("eds")); return Task.CompletedTask; });
        yield return Row("Eds.WriteStream", "eds", () => { eds.WriteStream(null!, new MemoryStream()); return Task.CompletedTask; });
        yield return Row("Eds.WriteFileAsync", "eds", () => eds.WriteFileAsync(null!, TempPath("eds")));
        yield return Row("Eds.WriteStreamAsync", "eds", () => eds.WriteStreamAsync(null!, new MemoryStream()));

        yield return Row("Dcf.GenerateString", "dcf", () => { dcf.GenerateString(null!); return Task.CompletedTask; });
        yield return Row("Dcf.WriteFile", "dcf", () => { dcf.WriteFile(null!, TempPath("dcf")); return Task.CompletedTask; });
        yield return Row("Dcf.WriteStream", "dcf", () => { dcf.WriteStream(null!, new MemoryStream()); return Task.CompletedTask; });
        yield return Row("Dcf.WriteFileAsync", "dcf", () => dcf.WriteFileAsync(null!, TempPath("dcf")));
        yield return Row("Dcf.WriteStreamAsync", "dcf", () => dcf.WriteStreamAsync(null!, new MemoryStream()));

        yield return Row("Cpj.GenerateString", "cpj", () => { cpj.GenerateString(null!); return Task.CompletedTask; });
        yield return Row("Cpj.WriteFile", "cpj", () => { cpj.WriteFile(null!, TempPath("cpj")); return Task.CompletedTask; });
        yield return Row("Cpj.WriteStream", "cpj", () => { cpj.WriteStream(null!, new MemoryStream()); return Task.CompletedTask; });
        yield return Row("Cpj.WriteFileAsync", "cpj", () => cpj.WriteFileAsync(null!, TempPath("cpj")));
        yield return Row("Cpj.WriteStreamAsync", "cpj", () => cpj.WriteStreamAsync(null!, new MemoryStream()));

        yield return Row("Xdd.GenerateString", "eds", () => { xdd.GenerateString((ElectronicDataSheet)null!); return Task.CompletedTask; });
        yield return Row("Xdd.WriteFile", "eds", () => { xdd.WriteFile(null!, TempPath("xdd")); return Task.CompletedTask; });
        yield return Row("Xdd.WriteStream", "eds", () => { xdd.WriteStream(null!, new MemoryStream()); return Task.CompletedTask; });
        yield return Row("Xdd.WriteFileAsync", "eds", () => xdd.WriteFileAsync(null!, TempPath("xdd")));
        yield return Row("Xdd.WriteStreamAsync", "eds", () => xdd.WriteStreamAsync(null!, new MemoryStream()));

        yield return Row("Xdc.GenerateString", "dcf", () => { xdc.GenerateString((DeviceConfigurationFile)null!); return Task.CompletedTask; });
        yield return Row("Xdc.WriteFile", "dcf", () => { xdc.WriteFile((DeviceConfigurationFile)null!, TempPath("xdc")); return Task.CompletedTask; });
        yield return Row("Xdc.WriteStream", "dcf", () => { xdc.WriteStream((DeviceConfigurationFile)null!, new MemoryStream()); return Task.CompletedTask; });
        yield return Row("Xdc.WriteFileAsync", "dcf", () => xdc.WriteFileAsync((DeviceConfigurationFile)null!, TempPath("xdc")));
        yield return Row("Xdc.WriteStreamAsync", "dcf", () => xdc.WriteStreamAsync((DeviceConfigurationFile)null!, new MemoryStream()));
    }

    [Theory]
    [MemberData(nameof(NullModelCalls))]
    public async Task Write_NullModel_ThrowsArgumentNullException(string call, string parameterName, Func<Task> action)
    {
        // Arrange
        _ = call;

        // Act
        var act = async () => await action();

        // Assert
        var ex = (await act.Should().ThrowAsync<ArgumentNullException>()).Which;
        ex.ParamName.Should().Be(parameterName);
    }
}
