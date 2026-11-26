namespace EdsDcfNet.Checker.Tests;

using AwesomeAssertions;
using EdsDcfNet.Checker;
using Xunit;

/// <summary>Arguments, output formats and exit codes of <c>edsdcf-check</c>.</summary>
[Collection("CheckerConsole")]
public sealed class ProgramTests : IDisposable
{
    // A clean EDS plus one duplicate key (INI003, warning); a DCF without [DeviceInfo]
    // (reader abort, LIB002) and with a NodeID out of range (DCF002).
    private const string WarningOnly = CleanEds + "\n[Comments]\nLines=0\nLines=0\n";
    private const string WithError = "[DeviceComissioning]\nNodeID=200\n";

    private const string CleanEds = Samples.CleanEds;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "edsdcf-cli-" + Guid.NewGuid().ToString("N"));

    public ProgramTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    [InlineData("/?")]
    public void Main_Help_PrintsUsageAndReturnsZero(string arg)
    {
        var (exitCode, stdout, _) = Run(arg);

        exitCode.Should().Be(0);
        stdout.Should().Contain("Usage:").And.Contain("edsdcf-check [options]");
    }

    [Fact]
    public void Main_NoInputs_ReturnsTwo()
    {
        var (exitCode, _, stderr) = Run();

        exitCode.Should().Be(2);
        stderr.Should().Contain("Usage:");
    }

    [Fact]
    public void Main_UnknownOption_ReturnsTwo()
    {
        var (exitCode, _, stderr) = Run("--bogus", "x.eds");

        exitCode.Should().Be(2);
        stderr.Should().Contain("Unknown option '--bogus'.");
    }

    [Fact]
    public void Main_MissingPath_ReturnsTwo()
    {
        var missing = Path.Combine(_dir, "missing.eds");

        var (exitCode, _, stderr) = Run(missing);

        exitCode.Should().Be(2);
        stderr.Should().Contain("File or directory not found: " + missing);
    }

    [Fact]
    public void Main_OnlyUnsupportedFiles_ReturnsTwo()
    {
        var xdd = Write("device.xdd", "<xml/>");

        var (exitCode, _, stderr) = Run(xdd);

        exitCode.Should().Be(2);
        stderr.Should().Contain("Skipping '" + xdd + "'").And.Contain("No .eds or .dcf files were checked.");
    }

    [Fact]
    public void Main_WarningsOnly_ReturnsZeroAndPrintsThem()
    {
        var eds = Write("a.eds", WarningOnly);

        var (exitCode, stdout, _) = Run("--no-library", eds);

        exitCode.Should().Be(0);
        stdout.Should().Contain("warning INI003").And.Contain(eds + ": OK (0 error(s), ");
    }

    [Fact]
    public void Main_CleanFile_ReturnsZero()
    {
        var eds = Write("clean.eds", CleanEds);

        var (exitCode, stdout, _) = Run(eds);

        exitCode.Should().Be(0);
        stdout.Should().Contain(eds + ": OK (0 error(s), 0 warning(s))");
    }

    [Fact]
    public void Main_WarningsAsErrors_ReturnsOne()
    {
        var eds = Write("a.eds", WarningOnly);

        Run("--no-library", "--warnings-as-errors", eds).ExitCode.Should().Be(1);
    }

    [Fact]
    public void Main_Quiet_HidesWarnings()
    {
        var eds = Write("a.eds", WarningOnly);

        var (_, stdout, _) = Run("--no-library", "-q", eds);

        stdout.Should().NotContain("INI003");
    }

    [Fact]
    public void Main_Errors_ReturnsOne()
    {
        var dcf = Write("a.dcf", WithError);

        var (exitCode, stdout, _) = Run(dcf);

        exitCode.Should().Be(1);
        stdout.Should().Contain(dcf + ": INVALID").And.Contain("error DCF002").And.Contain("error LIB002");
    }

    [Fact]
    public void Main_NoLibrary_SkipsReaderCodes()
    {
        var dcf = Write("a.dcf", WithError);

        var (_, stdout, _) = Run("--no-library", dcf);

        stdout.Should().Contain("DCF002").And.NotContain("INVALID_NODE_ID").And.NotContain("LIB00");
    }

    [Fact]
    public void Main_Json_PrintsMachineReadableReport()
    {
        var dcf = Write("a.dcf", WithError);

        var (exitCode, stdout, _) = Run("--json", "--quiet", "--no-library", dcf);

        exitCode.Should().Be(1);
        using var json = System.Text.Json.JsonDocument.Parse(stdout);
        var file = json.RootElement[0];
        file.GetProperty("valid").GetBoolean().Should().BeFalse();
        file.GetProperty("errors").GetInt32().Should().BeGreaterThan(0);
        file.GetProperty("findings").EnumerateArray()
            .Should().OnlyContain(f => f.GetProperty("severity").GetString() == "error");
    }

    [Fact]
    public void Main_DirectoryWithoutCheckableFiles_ReturnsZero()
    {
        Write("readme.txt", "x");

        var (exitCode, stdout, _) = Run(_dir);

        exitCode.Should().Be(0);
        stdout.Should().Contain("Checked 0 file(s)");
    }

    [Fact]
    public void CheckFile_NoLibrary_ReturnsRawFindingsOnly()
    {
        var dcf = Write("a.dcf", WithError);

        var findings = Program.CheckFile(dcf, runLibrary: false);

        findings.Should().Contain(f => f.Code == "DCF002");
        findings.Should().NotContain(f => f.Code.Contains('_'));
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static (int ExitCode, string Stdout, string Stderr) Run(params string[] args) => Cli.Run(args);
}
