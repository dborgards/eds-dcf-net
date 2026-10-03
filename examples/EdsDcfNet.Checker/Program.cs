namespace EdsDcfNet.Checker;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// edsdcf-check — validates CiA 306 EDS/DCF files and prints every problem found.
/// </summary>
/// <remarks>
/// Exit codes: 0 = no errors, 1 = errors found (or warnings with --warnings-as-errors),
/// 2 = usage or I/O problem, including when every given file is skipped. An unreadable file
/// does not stop the run: the remaining files are still checked and exit code 2 is returned
/// at the end (it takes precedence over 1, because the result is incomplete).
/// </remarks>
public static class Program
{
    private const string Usage =
        """
        edsdcf-check - validate CiA 306 EDS/DCF files

        Usage:
          edsdcf-check [options] <file|directory>...

        Directories are searched recursively for *.eds and *.dcf files.

        Options:
          --json                 Print findings as JSON instead of text.
          -q, --quiet            Only print errors (hide warnings).
          --warnings-as-errors   Exit with code 1 when warnings are found.
          --no-library           Skip the EdsDcfNet reader/model validation pass.
          -h, --help             Show this help.

        Exit codes: 0 = valid, 1 = errors found, 2 = usage or I/O problem.
        An unreadable file or directory is reported and skipped; the rest is still checked.
        Exit code 2 is also used when every given file is skipped (not .eds/.dcf).
        """;

    public static int Main(string[] args)
    {
        var json = false;
        var quiet = false;
        var warningsAsErrors = false;
        var runLibrary = true;
        var inputs = new List<string>();

        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--json": json = true; break;
                case "-q" or "--quiet": quiet = true; break;
                case "--warnings-as-errors": warningsAsErrors = true; break;
                case "--no-library": runLibrary = false; break;
                case "-h" or "--help" or "/?":
                    Console.WriteLine(Usage);
                    return 0;
                default:
                    if (arg.StartsWith('-'))
                    {
                        Console.Error.WriteLine("Unknown option '" + arg + "'.");
                        Console.Error.WriteLine(Usage);
                        return 2;
                    }

                    inputs.Add(arg);
                    break;
            }
        }

        if (inputs.Count == 0)
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var files = new List<string>();
        var unreadable = 0;
        foreach (var input in inputs)
        {
            if (Directory.Exists(input))
            {
                var errors = new List<string>();
                WalkDirectory(input, Directory.EnumerateFiles, Directory.EnumerateDirectories, files, errors);
                foreach (var error in errors)
                {
                    Console.Error.WriteLine("Cannot read " + error);
                    unreadable++;
                }
            }
            else if (File.Exists(input))
            {
                files.Add(input);
            }
            else
            {
                Console.Error.WriteLine("File or directory not found: " + input);
                return 2;
            }
        }

        var results = new List<(string File, List<Finding> Findings)>();
        var skipped = 0;
        foreach (var file in files)
        {
            if (!IsEds(file) && !IsDcf(file))
            {
                skipped++;
                Console.Error.WriteLine("Skipping '" + file + "': only .eds and .dcf files are supported (XML formats are out of scope).");
                continue;
            }

            try
            {
                results.Add((file, CheckFile(file, runLibrary)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Keep sweeping: one unreadable file must not hide the findings of the others.
                Console.Error.WriteLine("Cannot read '" + file + "': " + ex.Message);
                unreadable++;
            }
        }

        // A wrong-type path must not look like a clean pass to CI. Directories that
        // simply contain no EDS/DCF files are unchanged (zero files checked, exit 0).
        if (results.Count == 0 && skipped > 0 && unreadable == 0)
        {
            Console.Error.WriteLine("No .eds or .dcf files were checked.");
            return 2;
        }

        var minimum = quiet ? Severity.Error : Severity.Warning;
        if (json)
        {
            PrintJson(results, minimum);
        }
        else
        {
            PrintText(results, minimum);
        }

        var all = results.SelectMany(r => r.Findings).ToList();
        var failed = all.Any(f => f.Severity == Severity.Error) ||
                     (warningsAsErrors && all.Any(f => f.Severity == Severity.Warning));
        if (unreadable > 0)
        {
            Console.Error.WriteLine(unreadable.ToString(CultureInfo.InvariantCulture) + " file or directory input(s) could not be read.");
            return 2;
        }

        return failed ? 1 : 0;
    }

    /// <summary>
    /// Adds the EDS/DCF files of a (lazy) enumeration to <paramref name="files"/>, sorted. When the
    /// enumeration throws an I/O error midway, the files yielded before it are kept and
    /// <see langword="false"/> is returned.
    /// </summary>
    public static bool CollectSweepFiles(IEnumerable<string> enumeration, List<string> files, out string? error)
    {
        var found = new List<string>();
        error = null;
        try
        {
            foreach (var f in enumeration)
            {
                if (IsEds(f) || IsDcf(f))
                {
                    found.Add(f);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
        }

        found.Sort(StringComparer.OrdinalIgnoreCase);
        files.AddRange(found);
        return error is null;
    }

    /// <summary>
    /// Walks <paramref name="root"/> recursively and adds its EDS/DCF files to <paramref name="files"/>.
    /// A directory that cannot be listed is described in <paramref name="errors"/> and its siblings
    /// are still visited, so an inaccessible subtree never makes the sweep look complete.
    /// </summary>
    public static void WalkDirectory(
        string root,
        Func<string, IEnumerable<string>> listFiles,
        Func<string, IEnumerable<string>> listDirectories,
        List<string> files,
        List<string> errors)
    {
        var start = files.Count;
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            if (!CollectSweepFiles(Defer(() => listFiles(dir)), files, out var fileError))
            {
                errors.Add("'" + dir + "': " + fileError);
            }

            try
            {
                foreach (var sub in listDirectories(dir))
                {
                    pending.Push(sub);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add("'" + dir + "': " + ex.Message);
            }
        }

        files.Sort(start, files.Count - start, StringComparer.OrdinalIgnoreCase);

        static IEnumerable<string> Defer(Func<IEnumerable<string>> source)
        {
            // Lets CollectSweepFiles catch a failure raised by the listing call itself.
            foreach (var item in source())
            {
                yield return item;
            }
        }
    }

    public static List<Finding> CheckFile(string file, bool runLibrary)
    {
        var findings = new List<Finding>();
        var isDcf = IsDcf(file);

        var document = RawIniDocument.Parse(file, findings);
        new MandatoryFieldsChecker(file, document, isDcf, findings).Run();
        new RawObjectChecker(file, document, isDcf, findings).Run();

        if (runLibrary)
        {
            LibraryChecker.Run(file, isDcf, findings);
        }

        return findings
            .Distinct()
            .OrderBy(f => f.Line ?? 0)
            .ThenByDescending(f => f.Severity)
            .ThenBy(f => f.Code, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsEds(string file) => file.EndsWith(".eds", StringComparison.OrdinalIgnoreCase);

    private static bool IsDcf(string file) => file.EndsWith(".dcf", StringComparison.OrdinalIgnoreCase);

    private static void PrintText(List<(string File, List<Finding> Findings)> results, Severity minimum)
    {
        int totalErrors = 0, totalWarnings = 0;

        foreach (var (file, findings) in results)
        {
            var errors = findings.Count(f => f.Severity == Severity.Error);
            var warnings = findings.Count(f => f.Severity == Severity.Warning);
            totalErrors += errors;
            totalWarnings += warnings;

            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0}: {1} ({2} error(s), {3} warning(s))",
                file, errors == 0 ? "OK" : "INVALID", errors, warnings));

            foreach (var finding in findings.Where(f => f.Severity >= minimum))
            {
                WriteColored("  " + finding.ToDisplayString(), finding.Severity);
            }

            Console.WriteLine();
        }

        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "Checked {0} file(s): {1} error(s), {2} warning(s).", results.Count, totalErrors, totalWarnings));
    }

    private static void WriteColored(string line, Severity severity)
    {
        var previous = Console.ForegroundColor;
        if (!Console.IsOutputRedirected)
        {
            Console.ForegroundColor = severity switch
            {
                Severity.Error => ConsoleColor.Red,
                Severity.Warning => ConsoleColor.Yellow,
                _ => previous,
            };
        }

        Console.WriteLine(line);
        Console.ForegroundColor = previous;
    }

    private static void PrintJson(List<(string File, List<Finding> Findings)> results, Severity minimum)
    {
        var payload = results.Select(r => new
        {
            file = r.File,
            valid = r.Findings.All(f => f.Severity != Severity.Error),
            errors = r.Findings.Count(f => f.Severity == Severity.Error),
            warnings = r.Findings.Count(f => f.Severity == Severity.Warning),
            findings = r.Findings.Where(f => f.Severity >= minimum),
        });

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };

        Console.WriteLine(JsonSerializer.Serialize(payload, options));
    }
}
