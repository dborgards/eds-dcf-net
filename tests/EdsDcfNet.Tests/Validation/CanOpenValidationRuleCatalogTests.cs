namespace EdsDcfNet.Tests.IniValidation;

using System.Text.RegularExpressions;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Tests.Infrastructure;
using EdsDcfNet.Validation;

public class CanOpenValidationRuleCatalogTests
{
    private static readonly Regex RuleCode = new("\"([A-Z]{3}[0-9]{3})\"", RegexOptions.CultureInvariant);

    [Fact]
    public void All_HasUniqueIdsAndTitles()
    {
        var rules = CanOpenValidationRuleCatalog.All;

        rules.Should().NotBeEmpty();
        rules.Select(r => r.Id).Should().OnlyHaveUniqueItems();
        rules.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.Title) && r.HelpUri == null);
    }

    [Fact]
    public void All_MatchesEveryCodeTheIniValidatorEmits()
    {
        var source = Path.Combine(FindRepoRoot(), "src", "EdsDcfNet", "Validation", "Ini");
        var emitted = Directory.GetFiles(source, "*.cs")
            .SelectMany(file => RuleCode.Matches(File.ReadAllText(file)).Cast<Match>())
            .Select(m => m.Groups[1].Value)
            .Concat(IniReaderCodes().Values)
            .ToHashSet(StringComparer.Ordinal);

        CanOpenValidationRuleCatalog.All.Select(r => r.Id).Should().BeEquivalentTo(emitted);
    }

    [Fact]
    public void All_EveryRuleIsAssertedByAtLeastOneTest()
    {
        var tests = Path.Combine(FindRepoRoot(), "tests", "EdsDcfNet.Tests");
        var readerCodes = IniReaderCodes();
        var sources = Directory.GetFiles(tests, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.EndsWith(nameof(CanOpenValidationRuleCatalogTests) + ".cs", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToList();
        var asserted = sources
            .SelectMany(text => RuleCode.Matches(text).Cast<Match>())
            .Select(m => m.Groups[1].Value)
            .Concat(readerCodes
                .Where(code => sources.Any(text => text.Contains("ParseDiagnosticCodes." + code.Key, StringComparison.Ordinal)))
                .Select(code => code.Value))
            .ToHashSet(StringComparer.Ordinal);

        CanOpenValidationRuleCatalog.All.Select(r => r.Id).Should().BeSubsetOf(asserted);
    }

    [Theory]
    [InlineData("VAL001", IniValidationSeverity.Error)]
    [InlineData("val001", IniValidationSeverity.Error)]
    [InlineData("INI003", IniValidationSeverity.Warning)]
    public void TryGet_KnownId_ReturnsRule(string id, IniValidationSeverity severity)
    {
        CanOpenValidationRuleCatalog.TryGet(id, out var rule).Should().BeTrue();

        rule!.Id.Should().Be(id.ToUpperInvariant());
        rule.DefaultSeverity.Should().Be(severity);
        rule.SpecReference.Should().StartWith("CiA 306-1");
    }

    [Theory]
    [InlineData("XYZ999")]
    [InlineData("")]
    [InlineData(null)]
    public void TryGet_UnknownId_ReturnsFalse(string? id)
    {
        CanOpenValidationRuleCatalog.TryGet(id!, out var rule).Should().BeFalse();

        rule.Should().BeNull();
    }

    [Fact]
    public void All_ReaderCodesAreWarnings()
    {
        foreach (var code in IniReaderCodes().Values)
        {
            CanOpenValidationRuleCatalog.TryGet(code, out var rule).Should().BeTrue(code);
            rule!.DefaultSeverity.Should().Be(IniValidationSeverity.Warning, code);
        }
    }

    /// <summary>EDS/DCF reader codes: every <see cref="ParseDiagnosticCodes"/> constant except the XDD and CPJ ones.</summary>
    private static Dictionary<string, string> IniReaderCodes() =>
        typeof(ParseDiagnosticCodes)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (Name: f.Name, Value: (string)f.GetRawConstantValue()!))
            .Where(f => !f.Value.StartsWith("XDD_", StringComparison.Ordinal) && !f.Value.StartsWith("CPJ_", StringComparison.Ordinal))
            .ToDictionary(f => f.Name, f => f.Value, StringComparer.Ordinal);

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(TestOutputDirectory.Value);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "EdsDcfNet.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
