namespace EdsDcfNet.Tests.Checker;

using EdsDcfNet.Validation;

public class CanOpenIniValidationOptionsTests
{
    // INI003 (warning) on line 3, DCF002 (error) on line 5.
    private const string Text = "[FileInfo]\nFileName=a.dcf\nFileName=b.dcf\n[DeviceComissioning]\nNodeID=200\n";

    [Fact]
    public void ValidateString_DefaultOptions_MatchesOverloadWithoutOptions()
    {
        var plain = CanOpenIniValidator.ValidateString(Text, CanOpenIniFormat.Dcf, "a.dcf");
        var withOptions = CanOpenIniValidator.ValidateString(
            Text, CanOpenIniFormat.Dcf, "a.dcf", CanOpenIniValidationOptions.Default);

        withOptions.Should().Equal(plain);
        CanOpenIniValidationOptions.Default.SeverityOverrides.Should().BeEmpty();
    }

    [Fact]
    public void ValidateString_OverrideErrorToWarning_ChangesSeverity()
    {
        var issues = Validate(new Dictionary<string, IniValidationSeverity?> { ["DCF002"] = IniValidationSeverity.Warning });

        issues.Should().ContainSingle(i => i.Code == "DCF002")
            .Which.Severity.Should().Be(IniValidationSeverity.Warning);
    }

    [Fact]
    public void ValidateString_OverrideWarningToError_ChangesSeverityAndOrder()
    {
        var issues = Validate(new Dictionary<string, IniValidationSeverity?> { ["ini003"] = IniValidationSeverity.Error });

        issues.Should().ContainSingle(i => i.Code == "INI003")
            .Which.Severity.Should().Be(IniValidationSeverity.Error);
        issues.Select(i => i.Line ?? 0).Should().BeInAscendingOrder();
    }

    [Fact]
    public void ValidateString_NullOverride_TurnsRuleOff()
    {
        var issues = Validate(new Dictionary<string, IniValidationSeverity?> { ["DCF002"] = null, ["NOPE01"] = null });

        issues.Should().NotContain(i => i.Code == "DCF002");
        issues.Should().Contain(i => i.Code == "INI003");
    }

    [Fact]
    public void ValidateString_NullOverrideDictionary_ReportsDefaults()
    {
        var issues = CanOpenIniValidator.ValidateString(
            Text, CanOpenIniFormat.Dcf, null, new CanOpenIniValidationOptions { SeverityOverrides = null! });

        issues.Should().Equal(CanOpenIniValidator.ValidateString(Text, CanOpenIniFormat.Dcf));
    }

    [Fact]
    public void ValidateFile_WithOverrides_MatchesStringValidation()
    {
        var options = new CanOpenIniValidationOptions
        {
            SeverityOverrides = new Dictionary<string, IniValidationSeverity?> { ["DCF002"] = null },
        };
        var path = Path.Combine(Path.GetTempPath(), "edsdcf-ini-" + Guid.NewGuid().ToString("N") + ".dcf");
        File.WriteAllText(path, Text);
        try
        {
            var fromFile = CanOpenIniValidator.ValidateFile(path, CanOpenIniFormat.Dcf, options);

            fromFile.Should().Equal(CanOpenIniValidator.ValidateString(Text, CanOpenIniFormat.Dcf, path, options));
            fromFile.Should().NotContain(i => i.Code == "DCF002");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ValidateString_NullOptions_Throws()
    {
        var act = () => CanOpenIniValidator.ValidateString(Text, CanOpenIniFormat.Dcf, null, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public void ValidateString_NullTextWithOptions_Throws()
    {
        var act = () => CanOpenIniValidator.ValidateString(null!, CanOpenIniFormat.Dcf, null, CanOpenIniValidationOptions.Default);

        act.Should().Throw<ArgumentNullException>().WithParameterName("text");
    }

    [Fact]
    public void ValidateFile_NullArguments_Throw()
    {
        var noPath = () => CanOpenIniValidator.ValidateFile(null!, CanOpenIniFormat.Dcf, CanOpenIniValidationOptions.Default);
        var noOptions = () => CanOpenIniValidator.ValidateFile("x.dcf", CanOpenIniFormat.Dcf, (CanOpenIniValidationOptions)null!);

        noPath.Should().Throw<ArgumentNullException>().WithParameterName("filePath");
        noOptions.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    private static IReadOnlyList<IniValidationIssue> Validate(IReadOnlyDictionary<string, IniValidationSeverity?> overrides) =>
        CanOpenIniValidator.ValidateString(
            Text,
            CanOpenIniFormat.Dcf,
            "a.dcf",
            new CanOpenIniValidationOptions { SeverityOverrides = overrides });
}
