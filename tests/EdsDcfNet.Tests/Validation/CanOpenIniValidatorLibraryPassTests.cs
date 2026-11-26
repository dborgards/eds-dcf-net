namespace EdsDcfNet.Tests.IniValidation;

using EdsDcfNet.Diagnostics;
using EdsDcfNet.Validation;

public class CanOpenIniValidatorLibraryPassTests
{
    private static readonly CanOpenIniValidationOptions Both = new()
    {
        IncludeReaderDiagnostics = true,
        IncludeModelValidation = true,
    };

    private static readonly string NodeId200 = CanOpenIniValidatorRuleSampleTests.Dcf(
        commissioning: "NodeID=200\nNodeName=Node\nBaudrate=250\nNetNumber=1\nNetworkName=Net\n");

    [Fact]
    public void ValidateString_ReaderDiagnostics_KeepTheirCodeAndLocation()
    {
        var issues = CanOpenIniValidator.ValidateString(NodeId200, CanOpenIniFormat.Dcf, "a.dcf", Both);

        var reader = issues.Should().ContainSingle(i => i.Code == ParseDiagnosticCodes.InvalidNodeId).Subject;
        reader.Severity.Should().Be(IniValidationSeverity.Warning);
        reader.File.Should().Be("a.dcf");
        reader.Line.Should().NotBeNull();
        reader.Section.Should().Be("DeviceComissioning");
        reader.Key.Should().Be("NodeID");
        reader.Value.Should().Be("200");
        issues.Should().NotContain(i => i.Code == "LIB001");
    }

    [Fact]
    public void ValidateString_ModelIssueWithoutCode_IsLib003()
    {
        var issues = CanOpenIniValidator.ValidateString(NodeId200, CanOpenIniFormat.Dcf, "a.dcf", Both);

        issues.Should().ContainSingle(i => i.Code == "LIB003")
            .Which.Message.Should().StartWith("DeviceCommissioning.NodeId: ");
    }

    [Fact]
    public void ValidateString_ObjectDictionaryModelIssues_AreLeftToTheRawRules()
    {
        // ParameterName above 241 characters is a model issue on ObjectDictionary.* and OBJ010 in the raw pass.
        var text = CanOpenIniValidatorRuleSampleTests.Eds().Replace("ParameterName=Device type", "ParameterName=" + new string('x', 250));
        var model = CanOpenModelValidator.Validate(CanOpenFile.Eds.ReadString(text));

        var issues = CanOpenIniValidator.ValidateString(text, CanOpenIniFormat.Eds, null, Both);

        model.Should().Contain(i => i.Path.StartsWith("ObjectDictionary.", StringComparison.Ordinal));
        issues.Should().Contain(i => i.Code == "OBJ010");
        issues.Should().NotContain(i => i.Code == "LIB003");
    }

    [Fact]
    public void ValidateString_OnlyModelValidation_SkipsReaderDiagnostics()
    {
        var options = new CanOpenIniValidationOptions { IncludeModelValidation = true };

        var issues = CanOpenIniValidator.ValidateString(NodeId200, CanOpenIniFormat.Dcf, null, options);

        issues.Should().Contain(i => i.Code == "LIB003");
        issues.Should().NotContain(i => i.Code == ParseDiagnosticCodes.InvalidNodeId);
    }

    [Fact]
    public void ValidateString_OnlyReaderDiagnostics_SkipsModelValidation()
    {
        var options = new CanOpenIniValidationOptions { IncludeReaderDiagnostics = true };

        var issues = CanOpenIniValidator.ValidateString(NodeId200, CanOpenIniFormat.Dcf, null, options);

        issues.Should().Contain(i => i.Code == ParseDiagnosticCodes.InvalidNodeId);
        issues.Should().NotContain(i => i.Code == "LIB003");
    }

    [Fact]
    public void ValidateString_Eds_RunsReaderAndModelPass()
    {
        var text = CanOpenIniValidatorRuleSampleTests.Eds().Replace("FileVersion=1", "FileVersion=1.0");

        var issues = CanOpenIniValidator.ValidateString(text, CanOpenIniFormat.Eds, null, Both);

        issues.Should().Contain(i => i.Code == ParseDiagnosticCodes.IniVersionMajorMinor && i.Key == "FileVersion");
        issues.Should().Contain(i => i.Code == "MND003" && i.Key == "FileVersion");
    }

    [Fact]
    public void ValidateString_ReaderCodeOverride_AppliesToLibraryIssues()
    {
        var options = new CanOpenIniValidationOptions
        {
            IncludeReaderDiagnostics = true,
            IncludeModelValidation = true,
            SeverityOverrides = new Dictionary<string, IniValidationSeverity?>
            {
                [ParseDiagnosticCodes.InvalidNodeId] = IniValidationSeverity.Error,
                ["LIB003"] = null,
            },
        };

        var issues = CanOpenIniValidator.ValidateString(NodeId200, CanOpenIniFormat.Dcf, null, options);

        issues.Should().ContainSingle(i => i.Code == ParseDiagnosticCodes.InvalidNodeId)
            .Which.Severity.Should().Be(IniValidationSeverity.Error);
        issues.Should().NotContain(i => i.Code == "LIB003");
    }

    [Theory]
    [InlineData(CanOpenIniFormat.Dcf, ".dcf")]
    [InlineData(CanOpenIniFormat.Eds, ".eds")]
    public void ValidateFile_WithLibraryPass_MatchesStringValidation(CanOpenIniFormat format, string extension)
    {
        var text = format == CanOpenIniFormat.Dcf ? NodeId200 : CanOpenIniValidatorRuleSampleTests.Eds().Replace("FileVersion=1", "FileVersion=1.0");
        var path = Path.Combine(Path.GetTempPath(), "edsdcf-lib-" + Guid.NewGuid().ToString("N") + extension);
        File.WriteAllText(path, text);
        try
        {
            var fromFile = CanOpenIniValidator.ValidateFile(path, format, Both);

            fromFile.Should().Equal(CanOpenIniValidator.ValidateString(text, format, path, Both));
            fromFile.Should().Contain(i => i.Code.Contains('_'));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ValidateString_DefaultOptions_DoNotRunTheReader()
    {
        var issues = CanOpenIniValidator.ValidateString(NodeId200, CanOpenIniFormat.Dcf);

        issues.Should().NotContain(i => i.Code == ParseDiagnosticCodes.InvalidNodeId || i.Code.StartsWith("LIB", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_ReaderThrows_ReportsLib002()
    {
        var findings = new List<IniValidationIssue>();

        LibraryPass.Run(() => throw new InvalidOperationException("boom"), "x.eds", Both, findings);

        var abort = findings.Should().ContainSingle().Subject;
        abort.Code.Should().Be("LIB002");
        abort.Severity.Should().Be(IniValidationSeverity.Error);
        abort.File.Should().Be("x.eds");
        abort.Message.Should().Be("EdsDcfNet reader aborted: InvalidOperationException: boom");
    }

    [Fact]
    public void Run_Canceled_Rethrows()
    {
        var act = () => LibraryPass.Run(() => throw new OperationCanceledException(), "x.eds", Both, new List<IniValidationIssue>());

        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void Run_DiagnosticsAndIssues_MapSeverityCodeAndLocation()
    {
        var diagnostics = new[]
        {
            new ParseDiagnostic(ParseSeverity.Error, ParseDiagnosticCodes.InvalidDataType, "2000.DataType", "bad", 7, "abc"),
            new ParseDiagnostic(ParseSeverity.Info, ParseDiagnosticCodes.IniDecodedAsIso88591, string.Empty, "latin-1"),
            new ParseDiagnostic(ParseSeverity.Warning, string.Empty, "Tools", "no code", 3, "raw"),
        };
        var issues = new[]
        {
            new ValidationIssue("DeviceInfo.VendorName", "too long", "CUSTOM_CODE", ValidationSeverity.Warning),
            new ValidationIssue("FileInfo.FileName", "missing"),
            new ValidationIssue("ObjectDictionary.Objects[0x2000]", "dropped"),
        };
        var findings = new List<IniValidationIssue>();

        LibraryPass.Run(() => (diagnostics, () => issues), "x.eds", Both, findings);

        findings.Should().Equal(
            new IniValidationIssue(IniValidationSeverity.Error, ParseDiagnosticCodes.InvalidDataType, "x.eds", 7, "2000", "DataType", "abc", "bad"),
            new IniValidationIssue(IniValidationSeverity.Warning, ParseDiagnosticCodes.IniDecodedAsIso88591, "x.eds", null, null, null, null, "latin-1"),
            new IniValidationIssue(IniValidationSeverity.Warning, "LIB001", "x.eds", 3, "Tools", null, null, "no code"),
            new IniValidationIssue(IniValidationSeverity.Warning, "CUSTOM_CODE", "x.eds", null, null, null, null, "DeviceInfo.VendorName: too long"),
            new IniValidationIssue(IniValidationSeverity.Error, "LIB003", "x.eds", null, null, null, null, "FileInfo.FileName: missing"));
    }
}
