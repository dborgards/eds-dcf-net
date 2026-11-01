namespace EdsDcfNet.Checker.Tests;

using System.Text.Json;
using AwesomeAssertions;
using EdsDcfNet.Checker;
using Xunit;

/// <summary>Output, severity, config and rule-list options and the exit-code matrix.</summary>
[Collection("CheckerConsole")]
public sealed class CliOptionTests : IDisposable
{
    // INI003 warning only.
    private const string WarningOnly = Samples.CleanEds + "\n[Comments]\nLines=0\nLines=0\n";

    // INI003 warning plus VAL001 and LST003 errors.
    private const string WithErrors = Samples.Findings;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "edsdcf-opt-" + Guid.NewGuid().ToString("N"));

    public CliOptionTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    public static TheoryData<string, string[], int> ExitCodes => new()
    {
        { "clean", Array.Empty<string>(), 0 },
        { "warning", Array.Empty<string>(), 0 },
        { "warning", new[] { "--warnings-as-errors" }, 1 },
        { "warning", new[] { "--severity", "INI003=error" }, 1 },
        { "warning", new[] { "--severity", "INI003=none", "--warnings-as-errors" }, 0 },
        { "errors", Array.Empty<string>(), 1 },
        { "errors", new[] { "--severity", "VAL001=warning", "--severity", "LST003=warning" }, 0 },
        { "errors", new[] { "--severity", "val001=none", "--severity", "lst003=none" }, 0 },
        { "errors", new[] { "--severity", "VAL001=none", "--severity", "LST003=none", "--warnings-as-errors" }, 1 },
        { "errors", new[] { "--severity", "VAL001=bogus" }, 2 },
        { "errors", new[] { "--severity", "NOPE01=error" }, 2 },
        { "errors", new[] { "--severity", "=error" }, 2 },
        { "errors", new[] { "--format", "xml" }, 2 },
    };

    [Theory]
    [MemberData(nameof(ExitCodes))]
    public void Main_ExitCodeMatrix(string sample, string[] options, int expected)
    {
        var file = Write(sample + ".eds", sample switch
        {
            "clean" => Samples.CleanEds,
            "warning" => WarningOnly,
            _ => WithErrors,
        });

        Cli.Run(options.Append("--no-library").Append(file).ToArray()).ExitCode.Should().Be(expected);
    }

    [Theory]
    [InlineData("--output")]
    [InlineData("-o")]
    [InlineData("--format")]
    [InlineData("--severity")]
    [InlineData("--config")]
    public void Main_OptionWithoutValue_ReturnsTwo(string option)
    {
        var (exitCode, _, stderr) = Cli.Run("x.eds", option);

        exitCode.Should().Be(2);
        stderr.Should().Contain("Option '" + option + "' needs a value.");
    }

    [Fact]
    public void Main_MissingPathWinsOverErrors()
    {
        var file = Write("errors.eds", WithErrors);

        Cli.Run("--no-library", file, Path.Combine(_dir, "missing.eds")).ExitCode.Should().Be(2);
    }

    [Fact]
    public void Main_SeverityOverride_ChangesReportedLevel()
    {
        var file = Write("warning.eds", WarningOnly);

        var (_, stdout, _) = Cli.Run("--no-library", "--severity", "INI003=error", file);

        stdout.Should().Contain("error INI003").And.Contain("INVALID (1 error(s), 0 warning(s))");
    }

    [Fact]
    public void Main_Output_WritesReportToFile()
    {
        var file = Write("errors.eds", WithErrors);
        var report = Path.Combine(_dir, "report.json");

        var (exitCode, stdout, _) = Cli.Run("--no-library", "--json", "-o", report, file);

        exitCode.Should().Be(1);
        stdout.Should().BeEmpty();
        using var json = JsonDocument.Parse(File.ReadAllText(report));
        json.RootElement[0].GetProperty("errors").GetInt32().Should().Be(3);
        File.ReadAllBytes(report)[0].Should().Be((byte)'[', "the report is written without a BOM");
    }

    [Fact]
    public void Main_TextOutputToFile_HasNoColourAndSameContent()
    {
        var file = Write("errors.eds", WithErrors);
        var report = Path.Combine(_dir, "report.txt");

        Cli.Run("--no-library", "--output", report, file);
        var (_, stdout, _) = Cli.Run("--no-library", file);

        File.ReadAllText(report).Should().Be(stdout);
    }

    [Fact]
    public void Main_OutputToMissingDirectory_ReturnsTwo()
    {
        var file = Write("clean.eds", Samples.CleanEds);
        var report = Path.Combine(_dir, "no", "such", "dir", "report.txt");

        var (exitCode, _, stderr) = Cli.Run("--output", report, file);

        exitCode.Should().Be(2);
        stderr.Should().Contain("Cannot write '" + report + "'");
    }

    [Fact]
    public void Main_Config_AppliesSeverityOverridesAndExclude()
    {
        Write("errors.eds", WithErrors);
        Directory.CreateDirectory(Path.Combine(_dir, "vendor"));
        Write(Path.Combine("vendor", "other.eds"), WithErrors);
        Directory.CreateDirectory(Path.Combine(_dir, "legacy"));
        Write(Path.Combine("legacy", "old.eds"), WithErrors);
        var config = Write("edsdcf-check.json", """
            {
              // Comments and trailing commas are allowed.
              "severity": { "LST003": "none" },
              "exclude": [ "vendor/**" ],
              "overrides": [
                { "files": [ "legacy/*.eds" ], "severity": { "VAL001": "warning" } },
              ],
            }
            """);

        var (exitCode, stdout, _) = Cli.Run("--no-library", "--config", config, _dir);

        exitCode.Should().Be(1);
        stdout.Should().NotContain("other.eds").And.NotContain("LST003");
        stdout.Should().Contain("old.eds: OK (0 error(s), 3 warning(s))");
        stdout.Should().Contain("errors.eds: INVALID (1 error(s), 2 warning(s))");
    }

    [Fact]
    public void Main_CommandLineSeverityWinsOverConfig()
    {
        var file = Write("errors.eds", WithErrors);
        var config = Write("c.json", """{ "severity": { "VAL001": "error", "LST003": "none" } }""");

        Cli.Run("--no-library", "--config", config, "--severity", "VAL001=none", file).ExitCode.Should().Be(0);
    }

    [Theory]
    [InlineData("[]", "must be a JSON object")]
    [InlineData("{", "Invalid JSON")]
    [InlineData("""{ "rules": {} }""", "Unknown property 'rules'")]
    [InlineData("""{ "severity": [] }""", "'severity' must map rule ids")]
    [InlineData("""{ "severity": { "VAL001": 1 } }""", "Severity of 'VAL001' must be")]
    [InlineData("""{ "severity": { "VAL001": "fatal" } }""", "got 'fatal'")]
    [InlineData("""{ "severity": { "XYZ": "none" } }""", "Unknown rule 'XYZ'")]
    [InlineData("""{ "exclude": "*.eds" }""", "'exclude' must be an array")]
    [InlineData("""{ "exclude": [ "" ] }""", "must contain non-empty strings")]
    [InlineData("""{ "exclude": [ 1 ] }""", "must contain non-empty strings")]
    [InlineData("""{ "overrides": {} }""", "'overrides' must be an array")]
    [InlineData("""{ "overrides": [ 1 ] }""", "needs 'files' and 'severity'")]
    [InlineData("""{ "overrides": [ { "files": [] } ] }""", "needs 'files' and 'severity'")]
    [InlineData("""{ "overrides": [ { "severity": {} } ] }""", "needs 'files' and 'severity'")]
    [InlineData("""{ "overrides": [ { "files": [], "severity": {}, "x": 1 } ] }""", "Unknown override property 'x'")]
    public void Main_InvalidConfig_ReturnsTwo(string json, string message)
    {
        var file = Write("clean.eds", Samples.CleanEds);
        var config = Write("c.json", json);

        var (exitCode, _, stderr) = Cli.Run("--config", config, file);

        exitCode.Should().Be(2);
        stderr.Should().Contain("Invalid config '" + config + "': ").And.Contain(message);
    }

    [Fact]
    public void Main_MissingConfig_ReturnsTwo()
    {
        var file = Write("clean.eds", Samples.CleanEds);

        var (exitCode, _, stderr) = Cli.Run("--config", Path.Combine(_dir, "none.json"), file);

        exitCode.Should().Be(2);
        stderr.Should().Contain("Invalid config");
    }

    [Fact]
    public void Main_InvalidSeverity_NamesTheOption()
    {
        var file = Write("clean.eds", Samples.CleanEds);

        var (_, _, stderr) = Cli.Run("--severity", "VAL001", file);

        stderr.Should().Contain("Invalid --severity: Expected ID=error|warning|none, got 'VAL001'.");
    }

    [Fact]
    public void Main_ListRules_PrintsCatalog()
    {
        var (exitCode, stdout, _) = Cli.Run("--list-rules");

        exitCode.Should().Be(0);
        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(EdsDcfNet.Validation.CanOpenValidationRuleCatalog.All.Count);
        stdout.Should().Contain("VAL001").And.Contain("(CiA 306-1 Table 7)");
    }

    [Fact]
    public void Main_ListRulesJson_PrintsCatalog()
    {
        var (exitCode, stdout, _) = Cli.Run("--list-rules", "--format", "json");

        exitCode.Should().Be(0);
        using var json = JsonDocument.Parse(stdout);
        json.RootElement.GetArrayLength().Should().Be(EdsDcfNet.Validation.CanOpenValidationRuleCatalog.All.Count);
        json.RootElement[0].GetProperty("id").GetString().Should().Be("INI001");
        json.RootElement[0].GetProperty("defaultSeverity").GetString().Should().Be("error");
    }

    [Fact]
    public void Main_ListRulesToFile_WritesFile()
    {
        var report = Path.Combine(_dir, "rules.txt");

        Cli.Run("--list-rules", "--output", report).ExitCode.Should().Be(0);

        File.ReadAllText(report).Should().Contain("INI001");
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }
}
