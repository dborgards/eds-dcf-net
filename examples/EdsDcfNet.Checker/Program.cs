namespace EdsDcfNet.Checker;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// edsdcf-check — validates CiA 306 EDS/DCF files and prints every problem found.
/// </summary>
/// <remarks>
/// Exit codes: 0 = no errors, 1 = errors found (or warnings with --warnings-as-errors),
/// 2 = usage or I/O problem.
/// </remarks>
internal static class Program
{
    private const string Usage =
        """
        edsdcf-check - validate CiA 306 EDS/DCF files

        Usage:
          edsdcf-check [options] <file|directory>...

        Directories are searched recursively for *.eds and *.dcf files.

        Options:
          --json                 Print findings as JSON instead of text.
          -q, --quiet            Only print errors (hide warnings and infos).
          --warnings-as-errors   Exit with code 1 when warnings are found.
          --no-library           Skip the EdsDcfNet reader/model validation pass.
          -h, --help             Show this help.

        Exit codes: 0 = valid, 1 = errors found, 2 = usage or I/O problem.
        """;

    private static int Main(string[] args)
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
        foreach (var input in inputs)
        {
            if (Directory.Exists(input))
            {
                try
                {
                    // EnumerateFiles is lazy; AddRange is what walks the tree, so I/O failures
                    // surface here rather than inside the per-file read handler below.
                    files.AddRange(Directory.EnumerateFiles(input, "*.*", SearchOption.AllDirectories)
                        .Where(f => IsEds(f) || IsDcf(f))
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Console.Error.WriteLine("Cannot read '" + input + "': " + ex.Message);
                    return 2;
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
        foreach (var file in files)
        {
            if (!IsEds(file) && !IsDcf(file))
            {
                Console.Error.WriteLine("Skipping '" + file + "': only .eds and .dcf files are supported (XML formats are out of scope).");
                continue;
            }

            try
            {
                results.Add((file, CheckFile(file, runLibrary)));
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine("Cannot read '" + file + "': " + ex.Message);
                return 2;
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.Error.WriteLine("Cannot read '" + file + "': " + ex.Message);
                return 2;
            }
        }

        var minimum = quiet ? Severity.Error : Severity.Info;
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
        return failed ? 1 : 0;
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
