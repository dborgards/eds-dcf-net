namespace EdsDcfNet.Tests.IniValidation;

using EdsDcfNet.Tests.Utilities;

/// <summary>
/// <c>DeviceCom(m)issioning</c> precedence (same as <c>DcfReader</c>) and raw-INI scan rules
/// that must match <c>IniParser</c> (<c>#</c> lines).
/// </summary>
public class CanOpenIniValidatorSectionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Check_BothCommissioningSpellings_NormativeSectionWinsRegardlessOfOrder(bool normativeFirst)
    {
        // Arrange — only the two-'m' section carries an invalid NodeID.
        var normative = new[] { "[DeviceComissioning]", "NodeID=5" };
        var common = new[] { "[DeviceCommissioning]", "NodeID=200" };
        var lines = normativeFirst ? normative.Concat(common) : common.Concat(normative);

        // Act
        var findings = Check(string.Join("\r\n", lines) + "\r\n", isDcf: true);

        // Assert
        findings.Should().NotContain(f => f.Code == "DCF002");
        findings.Should().ContainSingle(f => f.Code == "DCF003" && f.Section == "DeviceCommissioning");
    }

    [Fact]
    public void Check_NormativeCommissioningWithInvalidNodeId_ReportsDcf002ForNormativeSection()
    {
        // Arrange
        var content = "[DeviceCommissioning]\r\nNodeID=5\r\n[DeviceComissioning]\r\nNodeID=200\r\n";

        // Act
        var findings = Check(content, isDcf: true);

        // Assert
        findings.Should().Contain(f => f.Code == "DCF002" && f.Section == "DeviceComissioning");
    }

    [Theory]
    [InlineData("100", false)]
    [InlineData("99", true)]
    public void Check_DcfBaudrate_HundredKbitIsAllowedAndUnlistedValueIsMnd003(string baudrate, bool expectFinding)
    {
        // Arrange
        var content = "[DeviceComissioning]\r\nNodeID=5\r\nBaudrate=" + baudrate + "\r\n";

        // Act
        var findings = Check(content, isDcf: true);

        // Assert
        (findings.Any(f => f.Code == "MND003")).Should().Be(expectFinding);
    }

    [Fact]
    public void Parse_HashLineWithEquals_IsAKeyLikeInIniParser()
    {
        // Arrange — IniParser ignores '#' only when the line has no '='.
        var content = "[Sec]\r\n#Key=Value\r\n# just a comment\r\n";

        // Act
        var (document, findings) = Parse(content);

        // Assert
        document.Get("Sec")!.Get("#Key")!.Value.Should().Be("Value");
        findings.Should().BeEmpty();
    }

    [Fact]
    public void Check_InvalidObjectTypeWithSubSections_ChecksSubSections()
    {
        // Arrange
        var content = string.Join("\r\n", new[]
        {
            "[OptionalObjects]", "SupportedObjects=1", "1=0x2000", "",
            "[2000]", "ParameterName=Bad", "ObjectType=0x3", "SubNumber=2", "",
            "[2000sub0]", "ParameterName=Count", "DataType=0x0005", "AccessType=ro", "DefaultValue=1", "PDOMapping=0", "",
            "[2000sub1]", "ParameterName=Val", "DataType=0x0005", "AccessType=ro", "DefaultValue=999", "PDOMapping=0", "",
        });

        // Act
        var findings = Check(content + "\r\n");

        // Assert
        findings.Should().Contain(f => f.Code == "OBJ001" && f.Section == "2000");
        findings.Should().Contain(f => f.Code == "VAL001" && f.Section == "2000sub1");
    }

    [Fact]
    public void Check_DomainWithoutAccessType_ReportsObj005()
    {
        // Arrange
        var content = string.Join("\r\n", new[]
        {
            "[OptionalObjects]", "SupportedObjects=1", "1=0x2000", "",
            "[2000]", "ParameterName=Blob", "ObjectType=0x2", "DataType=0x000F", "PDOMapping=0", "",
        });

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().Contain(f => f.Code == "OBJ005" && f.Section == "2000");
    }

    [Fact]
    public void ValidateFile_ValidatedDcfWithHundredKbit_IsAcceptedByValidatorWriterAndLibraryPass()
    {
        // Arrange — reference integration pattern for the validation-rule checklist:
        // a value accepted by model validation must survive all downstream consumers.
        var path = Path.Combine(Path.GetTempPath(), "edsdcf-check-" + Guid.NewGuid().ToString("N") + ".dcf");
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        dcf.DeviceCommissioning.Baudrate = 100;

        try
        {
            // Act
            var issues = CanOpenFile.Validate(dcf);
            CanOpenFile.Dcf.WriteFile(dcf, path, CanOpenWriteOptions.Validated);
            var findings = CanOpenIniValidator.ValidateFile(path, CanOpenIniFormat.Dcf, new CanOpenIniValidationOptions { IncludeReaderDiagnostics = true, IncludeModelValidation = true });

            // Assert
            issues.Should().BeEmpty();
            findings.Should().NotContain(finding => finding.Code == "MND003");
            findings.Should().NotContain(finding =>
                finding.Severity == Severity.Error && finding.Section == "DeviceComissioning");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static (RawIniDocument Document, List<Finding> Findings) Parse(string content)
    {
        var findings = new List<Finding>();
        return (RawIniDocument.Parse(new StringReader(content), "<test>", findings, CancellationToken.None), findings);
    }

    private static IReadOnlyList<Finding> Check(string content, bool isDcf = false) =>
        CanOpenIniValidator.ValidateString(content, isDcf ? CanOpenIniFormat.Dcf : CanOpenIniFormat.Eds);
}
