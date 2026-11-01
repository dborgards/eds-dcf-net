namespace EdsDcfNet.Checker.Tests;

using System.Diagnostics;
using System.Reflection;
using AwesomeAssertions;
using Xunit;

/// <summary>
/// Packs the checker as a dotnet tool, installs it from that local package only and runs it,
/// so a broken tool package fails the build.
/// </summary>
public sealed class ToolPackageTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "edsdcf-tool-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Pack_InstallFromLocalPackage_RunsHelp()
    {
        // Arrange — the test runs from tests/EdsDcfNet.Checker.Tests/bin/<Configuration>/net10.0.
        var output = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var configuration = output.Parent!.Name;
        var project = Path.Combine(FindRepoRoot(output), "src", "EdsDcfNet.Checker", "EdsDcfNet.Checker.csproj");
        var packages = Path.Combine(_root, "packages");
        var toolPath = Path.Combine(_root, "bin");
        var config = Path.Combine(_root, "nuget.config");
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host ? host : "dotnet";

        // Act
        Run(dotnet, "pack", project, "--configuration", configuration, "--no-build", "--disable-build-servers", "--output", packages)
            .ExitCode.Should().Be(0);
        var package = Directory.GetFiles(packages, "EdsDcfNet.Checker.*.nupkg").Should().ContainSingle().Subject;
        var version = Path.GetFileNameWithoutExtension(package)["EdsDcfNet.Checker.".Length..];
        version.Should().Be(LibraryVersion(), "the tool is released in lockstep with EdsDcfNet");

        // Only the local source, so a published package with the same version is never used.
        File.WriteAllText(config,
            "<configuration><packageSources><clear /><add key=\"local\" value=\"" + packages + "\" /></packageSources></configuration>");
        Run(dotnet, "tool", "install", "EdsDcfNet.Checker", "--version", version, "--tool-path", toolPath, "--configfile", config)
            .ExitCode.Should().Be(0);

        var help = Run(Path.Combine(toolPath, OperatingSystem.IsWindows() ? "edsdcf-check.exe" : "edsdcf-check"), "--help");

        // Assert
        help.ExitCode.Should().Be(0);
        help.Output.Should().Contain("edsdcf-check - validate CiA 306 EDS/DCF files");
    }

    [Fact]
    public void ToolProject_HasTheLibraryVersion()
    {
        var root = FindRepoRoot(new DirectoryInfo(AppContext.BaseDirectory));

        ProjectVersion(Path.Combine(root, "src", "EdsDcfNet.Checker", "EdsDcfNet.Checker.csproj"))
            .Should().Be(ProjectVersion(Path.Combine(root, "src", "EdsDcfNet", "EdsDcfNet.csproj")))
            .And.Be(LibraryVersion());
    }

    private static string LibraryVersion() =>
        typeof(EdsDcfNet.Validation.CanOpenIniValidator).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];

    private static string ProjectVersion(string project) =>
        System.Xml.Linq.XDocument.Load(project).Descendants("Version").Single().Value.Trim();

    private static (int ExitCode, string Output) Run(string fileName, params string[] arguments)
    {
        var start = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";

        // MSBuild nodes and the build server outlive the command and keep the output pipes open.
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(Timeout) || !Task.WaitAll(new Task[] { stdout, stderr }, Timeout))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(fileName + " did not finish within " + Timeout + ".");
        }

        var output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
        return (process.ExitCode, process.ExitCode == 0 ? output : throw new InvalidOperationException(
            fileName + " " + string.Join(" ", arguments) + " failed with " + process.ExitCode + ":\n" + output));
    }

    private static string FindRepoRoot(DirectoryInfo start)
    {
        for (var current = start; current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "EdsDcfNet.sln")))
            {
                return current.FullName;
            }
        }

        throw new InvalidOperationException("Repository root not found from " + start.FullName + ".");
    }
}
