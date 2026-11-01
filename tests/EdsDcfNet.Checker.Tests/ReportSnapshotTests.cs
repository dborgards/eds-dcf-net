namespace EdsDcfNet.Checker.Tests;

using System.Text.Json;
using AwesomeAssertions;
using EdsDcfNet.Checker;
using Json.Schema;
using Xunit;

/// <summary>
/// Compares the text, JSON and SARIF reports with the files in <c>Snapshots/</c>. Set
/// <c>UPDATE_SNAPSHOTS=1</c> to rewrite them after an intended change.
/// </summary>
[Collection("CheckerConsole")]
public sealed class ReportSnapshotTests : IDisposable
{
    private static readonly Lazy<JsonSchema> SarifSchema =
        new(() => JsonSchema.FromFile(Path.Combine(AppContext.BaseDirectory, "Schemas", "sarif-schema-2.1.0.json")));

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "edsdcf-report-" + Guid.NewGuid().ToString("N"));
    private readonly string _previousDirectory = Directory.GetCurrentDirectory();

    public ReportSnapshotTests()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "devices"));
        File.WriteAllText(Path.Combine(_dir, "devices", "clean.eds"), Samples.CleanEds);
        File.WriteAllText(Path.Combine(_dir, "devices", "findings.eds"), Samples.Findings);
        Directory.SetCurrentDirectory(_dir);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_previousDirectory);
        Directory.Delete(_dir, recursive: true);
    }

    [Theory]
    [InlineData("text", "report.txt")]
    [InlineData("json", "report.json")]
    [InlineData("sarif", "report.sarif")]
    public void Report_MatchesSnapshot(string format, string snapshot)
    {
        var (exitCode, stdout, _) = Cli.Run("--no-library", "--format", format, "devices");

        exitCode.Should().Be(1);
        Normalize(stdout).Should().Be(ReadSnapshot(snapshot, Normalize(stdout)));
    }

    [Fact]
    public void Sarif_IsValidAgainstSchema()
    {
        var report = Path.Combine(_dir, "out.sarif");
        Cli.Run("--format", "sarif", "--output", report, "devices").ExitCode.Should().Be(1);

        var schema = SarifSchema.Value;
        using var log = JsonDocument.Parse(File.ReadAllText(report));
        var result = schema.Evaluate(log.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });

        result.IsValid.Should().BeTrue(JsonSerializer.Serialize(result));
        log.RootElement.GetProperty("runs")[0].GetProperty("results").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public void SarifSchema_RejectsAnInvalidLog()
    {
        var schema = SarifSchema.Value;
        using var log = JsonDocument.Parse("""{ "version": "2.1.0", "runs": [ { "results": [] } ] }""");

        schema.Evaluate(log.RootElement).IsValid.Should().BeFalse();
    }

    private static string Normalize(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\\\\", "/", StringComparison.Ordinal)
            .Replace('\\', '/')
            .Replace("\"version\": \"" + Reports.ToolVersion + "\"", "\"version\": \"<version>\"", StringComparison.Ordinal);

    private static string ReadSnapshot(string name, string actual)
    {
        if (Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1")
        {
            var source = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Snapshots", name);
            File.WriteAllText(source, actual);
            return actual;
        }

        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Snapshots", name)).Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
