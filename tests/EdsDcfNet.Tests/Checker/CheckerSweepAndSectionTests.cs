namespace EdsDcfNet.Tests.Checker;

using AwesomeAssertions;
using EdsDcfNet.Checker;
using Xunit;

[CollectionDefinition("CheckerConsole", DisableParallelization = true)]
public class CheckerConsoleCollection
{
}

/// <summary>
/// Directory sweep (an unreadable file must not stop the run), <c>DeviceCom(m)issioning</c>
/// precedence (same as <c>DcfReader</c>), and the raw-INI scan rules that must match
/// <c>IniParser</c> (<c>#</c> lines).
/// </summary>
[Collection("CheckerConsole")]
public class CheckerSweepAndSectionTests
{
    private const string MinimalEds = "[FileInfo]\r\nFileName=ok.eds\r\n";

    [Fact]
    public void Main_DirectoryWithUnreadableFile_ChecksRemainingFilesAndReturnsTwoAtEnd()
    {
        // Arrange — a.eds is held open with FileShare.None, so reading it throws IOException on every OS.
        var dir = Path.Combine(Path.GetTempPath(), "edsdcf-sweep-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var locked = Path.Combine(dir, "a.eds");
        var good = Path.Combine(dir, "b.eds");
        File.WriteAllText(locked, MinimalEds);
        File.WriteAllText(good, MinimalEds);
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            using var hold = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);
            Console.SetOut(stdout);
            Console.SetError(stderr);

            // Act
            var exitCode = Program.Main(new[] { "--no-library", dir });

            // Assert
            exitCode.Should().Be(2);
            stderr.ToString().Should().Contain("Cannot read '" + locked + "'");
            stdout.ToString().Should().Contain(good + ":");
            stdout.ToString().Should().Contain("Checked 1 file(s)");
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CollectSweepFiles_EnumerationThrowsMidway_KeepsFilesYieldedBeforeTheError()
    {
        // Arrange
        var files = new List<string>();

        // Act
        var complete = Program.CollectSweepFiles(Throwing(), files, out var error);

        // Assert
        complete.Should().BeFalse();
        error.Should().Be("gone");
        files.Should().Equal("a.eds", "b.dcf");

        static IEnumerable<string> Throwing()
        {
            yield return "b.dcf";
            yield return "readme.txt";
            yield return "a.eds";
            throw new IOException("gone");
        }
    }

    [Fact]
    public void WalkDirectory_InaccessibleSubdirectory_IsReportedAndSiblingsAreStillVisited()
    {
        // Arrange
        var tree = new Dictionary<string, string[]>
        {
            ["root"] = new[] { "root/denied", "root/ok" },
            ["root/ok"] = Array.Empty<string>(),
            ["root/denied"] = Array.Empty<string>(),
        };
        var contents = new Dictionary<string, string[]>
        {
            ["root"] = new[] { "root/top.eds" },
            ["root/ok"] = new[] { "root/ok/b.dcf" },
        };
        var files = new List<string>();
        var errors = new List<string>();

        // Act
        Program.WalkDirectory(
            "root",
            d => d == "root/denied" ? throw new UnauthorizedAccessException("denied") : contents.GetValueOrDefault(d, Array.Empty<string>()),
            d => d == "root/denied" ? throw new UnauthorizedAccessException("denied") : tree[d],
            files,
            errors);

        // Assert
        files.Should().Equal("root/ok/b.dcf", "root/top.eds");
        errors.Should().HaveCount(2).And.OnlyContain(e => e.Contains("root/denied"));
    }

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

    private static (RawIniDocument Document, List<Finding> Findings) Parse(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "edsdcf-check-" + Guid.NewGuid().ToString("N") + ".eds");
        File.WriteAllText(path, content);
        try
        {
            var findings = new List<Finding>();
            return (RawIniDocument.Parse(path, findings), findings);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static List<Finding> Check(string content, bool isDcf = false)
    {
        var path = Path.Combine(Path.GetTempPath(), "edsdcf-check-" + Guid.NewGuid().ToString("N") + (isDcf ? ".dcf" : ".eds"));
        File.WriteAllText(path, content);
        try
        {
            return Program.CheckFile(path, runLibrary: false);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
