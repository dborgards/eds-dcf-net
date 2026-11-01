namespace EdsDcfNet.Tests.Checker;

using System.Text.RegularExpressions;
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
            .ToHashSet(StringComparer.Ordinal);

        CanOpenValidationRuleCatalog.All.Select(r => r.Id).Should().BeEquivalentTo(emitted);
    }

    [Fact]
    public void All_EveryRuleIsAssertedByAtLeastOneTest()
    {
        var tests = Path.Combine(FindRepoRoot(), "tests", "EdsDcfNet.Tests");
        var asserted = Directory.GetFiles(tests, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.EndsWith(nameof(CanOpenValidationRuleCatalogTests) + ".cs", StringComparison.Ordinal))
            .SelectMany(file => RuleCode.Matches(File.ReadAllText(file)).Cast<Match>())
            .Select(m => m.Groups[1].Value)
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
