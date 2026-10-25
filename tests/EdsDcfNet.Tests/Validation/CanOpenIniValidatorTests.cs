namespace EdsDcfNet.Tests.Checker;

using AwesomeAssertions;
using EdsDcfNet.Validation;
using Xunit;

public class CanOpenIniValidatorTests
{
    [Fact]
    public void ValidateString_RawEds_PreservesSourceLocationAndRuleDetails()
    {
        const string text = "[FileInfo]\nFileName=a.eds\nFileName=b.eds\n";

        var issues = CanOpenIniValidator.ValidateString(text, CanOpenIniFormat.Eds, "memory.eds");

        var duplicate = issues.Should().ContainSingle(i => i.Code == "INI003").Subject;
        duplicate.Severity.Should().Be(IniValidationSeverity.Warning);
        duplicate.File.Should().Be("memory.eds");
        duplicate.Line.Should().Be(3);
        duplicate.Section.Should().Be("FileInfo");
        duplicate.Key.Should().Be("FileName");
        duplicate.Value.Should().Be("b.eds");
        duplicate.ToDisplayString().Should().Be(
            "memory.eds:3: warning INI003 [FileInfo] FileName=b.eds: Duplicate key; previous value 'a.eds' on line 2 is overridden.");
    }

    [Fact]
    public void ValidateString_WithoutSourceName_UsesMemoryLabel()
    {
        var issues = CanOpenIniValidator.ValidateString("broken", CanOpenIniFormat.Eds);

        issues.Should().ContainSingle(i => i.Code == "INI001")
            .Which.File.Should().Be("<memory>");
    }

    [Fact]
    public void ValidateFile_SameDcfText_MatchesStringValidationAndUsesPath()
    {
        const string text = "[DeviceComissioning]\nNodeID=200\n";
        var path = Path.Combine(Path.GetTempPath(), "edsdcf-ini-" + Guid.NewGuid().ToString("N") + ".dcf");
        File.WriteAllText(path, text);
        try
        {
            var fromFile = CanOpenIniValidator.ValidateFile(path, CanOpenIniFormat.Dcf);
            var fromText = CanOpenIniValidator.ValidateString(text, CanOpenIniFormat.Dcf, path);

            fromFile.Should().Equal(fromText);
            fromFile.Should().Contain(i => i.Code == "DCF002" && i.File == path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ValidateString_InvalidFormat_Throws()
    {
        var act = () => CanOpenIniValidator.ValidateString(string.Empty, (CanOpenIniFormat)99);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("format");
    }

    [Fact]
    public void ValidateString_NullText_Throws()
    {
        var act = () => CanOpenIniValidator.ValidateString(null!, CanOpenIniFormat.Eds);

        act.Should().Throw<ArgumentNullException>().WithParameterName("text");
    }

    [Fact]
    public void ValidateFile_NullPath_Throws()
    {
        var act = () => CanOpenIniValidator.ValidateFile(null!, CanOpenIniFormat.Eds);

        act.Should().Throw<ArgumentNullException>().WithParameterName("filePath");
    }

    [Fact]
    public void ValidateString_PreCanceledToken_ThrowsBeforeReturningIssues()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => CanOpenIniValidator.ValidateString(
            "[FileInfo]\nFileName=a.eds\n",
            CanOpenIniFormat.Eds,
            cancellationToken: cts.Token);

        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void ValidateString_StructuralErrors_ReportsOutsideEntryMalformedAndDuplicateSections()
    {
        const string text = "Key=outside\n[broken\n[Sec]\n[Sec]\n";

        var issues = CanOpenIniValidator.ValidateString(text, CanOpenIniFormat.Eds);

        issues.Should().Contain(i => i.Code == "INI004" && i.Line == 1);
        issues.Should().Contain(i => i.Code == "INI001" && i.Line == 2 && i.Section == null);
        issues.Should().Contain(i => i.Code == "INI002" && i.Line == 4 && i.Section == "Sec");
    }

    [Fact]
    public void ValidateString_CommentsBlankLinesAndMalformedSectionEntry_AreClassified()
    {
        const string text = "\n; comment\n[Sec]\nnot an entry\n# comment\n";

        var issues = CanOpenIniValidator.ValidateString(text, CanOpenIniFormat.Eds);

        issues.Should().ContainSingle(i => i.Code == "INI001")
            .Which.Should().Match<IniValidationIssue>(i => i.Line == 4 && i.Section == "Sec");
    }

    [Fact]
    public void ValidateString_InvalidMandatoryMetadata_ReportsSpecificRules()
    {
        var text = "[FileInfo]\nEDSVersion=40\nDescription=" + new string('x', 244) +
            "\nCreationTime=25:00\nCreationDate=2026-01-01\n" +
            "[DeviceInfo]\nGranularity=65\nCANopenManager=1\n" +
            "[DeviceCommissioning]\nCANopenManager=yes\n";

        var issues = CanOpenIniValidator.ValidateString(text, CanOpenIniFormat.Dcf);

        issues.Should().Contain(i => i.Code == "MND003" && i.Key == "EDSVersion" && i.Severity == IniValidationSeverity.Error);
        issues.Should().Contain(i => i.Code == "MND003" && i.Key == "Description");
        issues.Should().Contain(i => i.Code == "MND003" && i.Key == "CreationTime");
        issues.Should().Contain(i => i.Code == "MND003" && i.Key == "CreationDate");
        issues.Should().Contain(i => i.Code == "MND003" && i.Key == "Granularity");
        issues.Should().Contain(i => i.Code == "MND003" && i.Key == "CANopenManager");
        issues.Should().Contain(i => i.Code == "MND003" && i.Section == "DeviceCommissioning" && i.Key == null);
    }

    [Theory]
    [InlineData("1.0", IniValidationSeverity.Warning)]
    [InlineData("256.0", IniValidationSeverity.Error)]
    [InlineData("0x1", IniValidationSeverity.Error)]
    public void ValidateString_FileVersionForms_MatchReaderSemantics(string value, IniValidationSeverity severity)
    {
        var issues = CanOpenIniValidator.ValidateString(
            "[FileInfo]\nFileVersion=" + value + "\n",
            CanOpenIniFormat.Eds);

        issues.Should().Contain(i => i.Code == "MND003" && i.Key == "FileVersion" && i.Severity == severity);
    }

    [Fact]
    public void IniValidationIssue_WithoutOptionalLocation_FormatsSourceOnly()
    {
        var issue = new IniValidationIssue(
            IniValidationSeverity.Error,
            "TEST001",
            "device.eds",
            null,
            null,
            null,
            null,
            "Invalid.");

        issue.ToDisplayString().Should().Be("device.eds: error TEST001: Invalid.");
    }
}
