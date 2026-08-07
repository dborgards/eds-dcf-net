namespace EdsDcfNet.Tests.Integration;

using System.Diagnostics;
using System.Globalization;
using Xunit.Sdk;

internal static class EdsReadProbeRunner
{
    internal sealed record ProbeResult(byte SubNumber, bool HasSub0, bool HasSubFF);

    public static async Task<ProbeResult> RunAsync(string mode, string fixtureFileName, TimeSpan timeout)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName);
        if (!File.Exists(fixturePath))
        {
            throw new XunitException($"Fixture file not found: {fixturePath}");
        }

        var repoRoot = FindRepoRoot();
        var probeAssemblyPath = GetProbeAssemblyPath(repoRoot);
#if NETFRAMEWORK
        var probeLaunchPath = Path.ChangeExtension(probeAssemblyPath, ".exe");
#else
        var probeLaunchPath = probeAssemblyPath;
#endif
        if (!File.Exists(probeLaunchPath))
        {
            throw new XunitException($"Probe host was not built: {probeLaunchPath}");
        }

        using var process = new Process
        {
            StartInfo = CreateStartInfo(mode, fixturePath, probeAssemblyPath)
        };

        if (!process.Start())
        {
            throw new XunitException("Failed to start EDS read probe process.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

#if NETFRAMEWORK
        using var timeoutCts = new CancellationTokenSource(
            timeout.TotalMilliseconds > int.MaxValue ? int.MaxValue : (int)timeout.TotalMilliseconds);
#else
        using var timeoutCts = new CancellationTokenSource(timeout);
#endif

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            await process.WaitForExitAsync().ConfigureAwait(false);
            var timedOutStdout = await stdoutTask.ConfigureAwait(false);
            var timedOutStderr = await stderrTask.ConfigureAwait(false);
            throw new XunitException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "EDS read probe timed out after {0} seconds for mode '{1}'. stdout:{2}{3}{2}stderr:{2}{4}",
                    timeout.TotalSeconds,
                    mode,
                    Environment.NewLine,
                    timedOutStdout,
                    timedOutStderr));
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new XunitException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "EDS read probe failed with exit code {0} for mode '{1}'. stdout:{2}{3}{2}stderr:{2}{4}",
                    process.ExitCode,
                    mode,
                    Environment.NewLine,
                    stdout,
                    stderr));
        }

        return ParseProbeOutput(stdout);
    }

    private static ProcessStartInfo CreateStartInfo(string mode, string fixturePath, string probeAssemblyPath)
    {
#if NETFRAMEWORK
        // net48 produces a Framework exe that must be launched directly (not via the dotnet host).
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.ChangeExtension(probeAssemblyPath, ".exe"),
            Arguments = string.Format(CultureInfo.InvariantCulture, "\"{0}\" \"{1}\"", mode, fixturePath),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        return startInfo;
#else
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add(probeAssemblyPath);
        startInfo.ArgumentList.Add(mode);
        startInfo.ArgumentList.Add(fixturePath);
        return startInfo;
#endif
    }

    private static ProbeResult ParseProbeOutput(string stdout)
    {
        string? subNumber = null;
        string? hasSub0 = null;
        string? hasSubFF = null;

        foreach (var rawLine in stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line.Substring(0, separatorIndex);
            var value = line.Substring(separatorIndex + 1);

            switch (key)
            {
                case "SubNumber":
                    subNumber = value;
                    break;
                case "HasSub0":
                    hasSub0 = value;
                    break;
                case "HasSubFF":
                    hasSubFF = value;
                    break;
            }
        }

        if (!byte.TryParse(subNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSubNumber) ||
            !bool.TryParse(hasSub0, out var parsedHasSub0) ||
            !bool.TryParse(hasSubFF, out var parsedHasSubFF))
        {
            throw new XunitException($"Unexpected probe output:{Environment.NewLine}{stdout}");
        }

        return new ProbeResult(parsedSubNumber, parsedHasSub0, parsedHasSubFF);
    }

    private static string GetProbeAssemblyPath(string repoRoot)
    {
        var baseDirectoryInfo = new DirectoryInfo(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
        var targetFramework = baseDirectoryInfo.Name;
        var configuration = baseDirectoryInfo.Parent?.Name;

        if (string.IsNullOrEmpty(configuration))
        {
            throw new XunitException($"Unable to determine test configuration from base directory '{AppContext.BaseDirectory}'.");
        }

        var candidates = new[]
        {
            BuildProbeDllPath(repoRoot, configuration!, targetFramework),
            BuildProbeDllPath(repoRoot, "Release", targetFramework),
            BuildProbeDllPath(repoRoot, "Debug", targetFramework)
        };

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
#if NETFRAMEWORK
            if (File.Exists(Path.ChangeExtension(candidate, ".exe")))
#else
            if (File.Exists(candidate))
#endif
            {
                return candidate;
            }
        }

        return candidates[0];
    }

    private static string BuildProbeDllPath(string repoRoot, string configuration, string targetFramework)
    {
        return Path.Combine(
            repoRoot,
            "tests",
            "EdsDcfNet.TestHost",
            "bin",
            configuration,
            targetFramework,
            "EdsDcfNet.TestHost.dll");
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "EdsDcfNet.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new XunitException($"Unable to locate repository root from '{AppContext.BaseDirectory}'.");
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
#if NETFRAMEWORK
                process.Kill();
#else
                process.Kill(entireProcessTree: true);
#endif
            }
        }
        catch (InvalidOperationException)
        {
            // Process already exited between timeout and kill attempt.
        }
    }
}
