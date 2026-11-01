namespace EdsDcfNet.Checker.Tests;

using AwesomeAssertions;
using EdsDcfNet.Checker;
using Xunit;

[CollectionDefinition("CheckerConsole", DisableParallelization = true)]
public class CheckerConsoleCollection
{
}

/// <summary>
/// Directory sweep: an unreadable file or directory must not stop the run.
/// </summary>
[Collection("CheckerConsole")]
public class ProgramSweepTests
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

    [Fact]
    public void WalkDirectory_DirectoryListingThrowsMidway_StillVisitsDirectoriesYieldedBefore()
    {
        // Arrange
        var files = new List<string>();
        var errors = new List<string>();

        // Act
        Program.WalkDirectory(
            "root",
            d => d == "root/ok" ? new[] { "root/ok/c.eds" } : Array.Empty<string>(),
            d => d == "root" ? Throwing() : Array.Empty<string>(),
            files,
            errors);

        // Assert
        files.Should().Equal("root/ok/c.eds");
        errors.Should().ContainSingle().Which.Should().Contain("gone");

        static IEnumerable<string> Throwing()
        {
            yield return "root/ok";
            throw new IOException("gone");
        }
    }
}
