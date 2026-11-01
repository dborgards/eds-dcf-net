namespace EdsDcfNet.Checker.Tests;

using System.Text.Json;
using AwesomeAssertions;
using EdsDcfNet.Checker;
using EdsDcfNet.Validation;
using Xunit;

/// <summary>Glob matching, per-file options and report details not covered by the snapshots.</summary>
[Collection("CheckerConsole")]
public sealed class ReportsAndConfigTests
{
    [Theory]
    [InlineData("*.eds", "a.eds", true)]
    [InlineData("*.eds", "dir/a.eds", false)]
    [InlineData("**/*.eds", "a.eds", true)]
    [InlineData("**/*.eds", "x/y/a.EDS", true)]
    [InlineData("vendor/**", "vendor/x/a.eds", true)]
    [InlineData("vendor/**", "other/a.eds", false)]
    [InlineData("a?.dcf", "ab.dcf", true)]
    [InlineData("a?.dcf", "a/.dcf", false)]
    [InlineData("a+b(1).eds", "a+b(1).eds", true)]
    [InlineData("dir\\*.eds", "dir/a.eds", true)]
    public void GlobToRegex_Matches(string glob, string path, bool expected) =>
        CheckerConfig.GlobToRegex(glob).IsMatch(path).Should().Be(expected);

    [Fact]
    public void OptionsFor_FileOutsideConfigDirectory_MatchesFullPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "edsdcf-cfg-" + Guid.NewGuid().ToString("N"));
        var outside = Path.Combine(Path.GetTempPath(), "elsewhere", "dev.eds");
        var config = CheckerConfig.Load(
            Path.Combine(root, "c.json"),
            """{ "overrides": [ { "files": [ "**/elsewhere/*.eds" ], "severity": { "VAL005": "none" } } ] }""");

        config.OptionsFor(outside, runLibrary: true).SeverityOverrides.Should().ContainKey("VAL005").WhoseValue.Should().BeNull();
        config.OptionsFor(Path.Combine(root, "dev.eds"), runLibrary: false).SeverityOverrides.Should().BeEmpty();
        config.IsExcluded(outside).Should().BeFalse();
    }

    [Fact]
    public void OptionsFor_PassesLibraryFlag()
    {
        var options = new CheckerConfig().OptionsFor("a.eds", runLibrary: true);

        options.IncludeReaderDiagnostics.Should().BeTrue();
        options.IncludeModelValidation.Should().BeTrue();
    }

    [Fact]
    public void WriteSarif_FileLevelAndUnknownCode_OmitsRegionLogicalLocationAndRuleIndex()
    {
        var findings = new[]
        {
            new IniValidationIssue(IniValidationSeverity.Error, "X999", "a.eds", null, null, null, null, "Custom."),
        };
        using var writer = new StringWriter();

        Reports.WriteSarif(writer, new[] { new FileResult("a.eds", findings) }, IniValidationSeverity.Warning, Directory.GetCurrentDirectory());

        using var log = JsonDocument.Parse(writer.ToString());
        var result = log.RootElement.GetProperty("runs")[0].GetProperty("results")[0];
        result.TryGetProperty("ruleIndex", out _).Should().BeFalse();
        var location = result.GetProperty("locations")[0];
        location.TryGetProperty("logicalLocations", out _).Should().BeFalse();
        location.GetProperty("physicalLocation").TryGetProperty("region", out _).Should().BeFalse();
        location.GetProperty("physicalLocation").GetProperty("artifactLocation").GetProperty("uri").GetString().Should().Be("a.eds");
    }

    [Fact]
    public void WriteSarif_Quiet_DropsWarnings()
    {
        var findings = new[]
        {
            new IniValidationIssue(IniValidationSeverity.Warning, "INI003", "a.eds", 3, "S", "K", "V", "Dup."),
        };
        using var writer = new StringWriter();

        Reports.WriteSarif(writer, new[] { new FileResult("a.eds", findings) }, IniValidationSeverity.Error, Directory.GetCurrentDirectory());

        using var log = JsonDocument.Parse(writer.ToString());
        log.RootElement.GetProperty("runs")[0].GetProperty("results").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void ToArtifactUri_OutsideBase_IsAbsoluteFileUri()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), "base");
        var file = Path.Combine(Path.GetTempPath(), "other dir", "a b.eds");

        var uri = Reports.ToArtifactUri(file, baseDirectory);

        uri.Should().StartWith("file:///").And.EndWith("/other%20dir/a%20b.eds");
    }

    [Fact]
    public void ToArtifactUri_BelowBase_IsEscapedRelativeUri()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), "base");

        Reports.ToArtifactUri(Path.Combine(baseDirectory, "sub dir", "a#1.eds"), baseDirectory).Should().Be("sub%20dir/a%231.eds");
    }

    [Fact]
    public void WriteText_WithColour_RestoresConsoleColour()
    {
        var findings = new[]
        {
            new IniValidationIssue(IniValidationSeverity.Error, "VAL001", "a.eds", 1, "S", "K", "V", "Bad."),
            new IniValidationIssue(IniValidationSeverity.Warning, "VAL005", "a.eds", 2, "S", "K", "V", "Octal."),
        };
        var before = Console.ForegroundColor;
        using var writer = new StringWriter();

        Reports.WriteText(writer, new[] { new FileResult("a.eds", findings) }, IniValidationSeverity.Warning, color: true);

        Console.ForegroundColor.Should().Be(before);
        writer.ToString().Should().Contain("error VAL001").And.Contain("warning VAL005");
    }

    [Fact]
    public void ToolVersion_HasNoBuildMetadata() =>
        Reports.ToolVersion.Should().NotBeNullOrEmpty().And.NotContain("+");
}
