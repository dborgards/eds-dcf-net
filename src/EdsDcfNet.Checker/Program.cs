namespace EdsDcfNet.Checker;

using System.Globalization;
using System.Text;
using EdsDcfNet.Validation;
using Finding = EdsDcfNet.Validation.IniValidationIssue;
using Severity = EdsDcfNet.Validation.IniValidationSeverity;

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
          edsdcf-check --list-rules [--format text|json]

        Directories are searched recursively for *.eds and *.dcf files.

        Options:
          --format <text|json|sarif>   Report format (default text).
          --json                       Same as --format json.
          -o, --output <file>          Write the report to a file instead of stdout.
          --severity <ID>=<level>      Set a rule to error, warning or none (repeatable).
          --config <file>              JSON file with severity overrides and path globs.
          --list-rules                 Print every rule with its default severity.
          -q, --quiet                  Only report errors (hide warnings).
          --warnings-as-errors         Exit with code 1 when warnings are found.
          --no-library                 Skip the EdsDcfNet reader/model validation pass.
          -h, --help                   Show this help.

        Exit codes: 0 = valid, 1 = errors found, 2 = usage or I/O problem.
        An unreadable file or directory is reported and skipped; the rest is still checked.
        Exit code 2 is also used when every given file is skipped (not .eds/.dcf).
        """;

    public static int Main(string[] args)
    {
        var format = "text";
        string? output = null;
        string? configPath = null;
        var severities = new List<string>();
        var listRules = false;
        var quiet = false;
        var warningsAsErrors = false;
        var runLibrary = true;
        var inputs = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--json": format = "json"; break;
                case "--format" or "-o" or "--output" or "--severity" or "--config":
                    if (i + 1 >= args.Length)
                    {
                        return UsageError("Option '" + arg + "' needs a value.");
                    }

                    var value = args[++i];
                    switch (arg)
                    {
                        case "--format":
                            if (value is not ("text" or "json" or "sarif"))
                            {
                                return UsageError("Unknown format '" + value + "'. Use text, json or sarif.");
                            }

                            format = value;
                            break;
                        case "--severity": severities.Add(value); break;
                        case "--config": configPath = value; break;
                        default: output = value; break;
                    }

                    break;
                case "--list-rules": listRules = true; break;
                case "-q" or "--quiet": quiet = true; break;
                case "--warnings-as-errors": warningsAsErrors = true; break;
                case "--no-library": runLibrary = false; break;
                case "-h" or "--help" or "/?":
                    Console.WriteLine(Usage);
                    return 0;
                default:
                    if (arg.StartsWith('-'))
                    {
                        return UsageError("Unknown option '" + arg + "'.");
                    }

                    inputs.Add(arg);
                    break;
            }
        }

        if (listRules)
        {
            return Emit(output, writer => Reports.WriteRules(writer, format == "json"));
        }

        if (inputs.Count == 0)
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        CheckerConfig config;
        try
        {
            config = configPath is null ? new CheckerConfig() : CheckerConfig.Load(configPath, File.ReadAllText(configPath));
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Invalid config '" + configPath + "': " + ex.Message);
            return 2;
        }

        try
        {
            foreach (var severity in severities)
            {
                config.AddCommandLineOverride(severity);
            }
        }
        catch (FormatException ex)
        {
            Console.Error.WriteLine("Invalid --severity: " + ex.Message);
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

        var results = new List<FileResult>();
        var skipped = 0;
        foreach (var file in files)
        {
            if (!IsEds(file) && !IsDcf(file))
            {
                skipped++;
                Console.Error.WriteLine("Skipping '" + file + "': only .eds and .dcf files are supported (XML formats are out of scope).");
                continue;
            }

            if (config.IsExcluded(file))
            {
                continue;
            }

            try
            {
                results.Add(new FileResult(file, CheckFile(file, config.OptionsFor(file, runLibrary))));
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
        var written = Emit(output, writer =>
        {
            switch (format)
            {
                case "json": Reports.WriteJson(writer, results, minimum); break;
                case "sarif": Reports.WriteSarif(writer, results, minimum, Directory.GetCurrentDirectory()); break;
                default: Reports.WriteText(writer, results, minimum, color: output is null && !Console.IsOutputRedirected); break;
            }
        });

        var all = results.SelectMany(r => r.Findings).ToList();
        var failed = all.Any(f => f.Severity == Severity.Error) ||
                     (warningsAsErrors && all.Any(f => f.Severity == Severity.Warning));
        if (unreadable > 0)
        {
            Console.Error.WriteLine(unreadable.ToString(CultureInfo.InvariantCulture) + " file or directory input(s) could not be read.");
            return 2;
        }

        return written != 0 ? written : failed ? 1 : 0;
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

    public static List<Finding> CheckFile(string file, bool runLibrary) =>
        CheckFile(file, new CanOpenIniValidationOptions
        {
            IncludeReaderDiagnostics = runLibrary,
            IncludeModelValidation = runLibrary,
        });

    public static List<Finding> CheckFile(string file, CanOpenIniValidationOptions options) =>
        CanOpenIniValidator.ValidateFile(
            file,
            IsDcf(file) ? CanOpenIniFormat.Dcf : CanOpenIniFormat.Eds,
            options).ToList();

    private static bool IsEds(string file) => file.EndsWith(".eds", StringComparison.OrdinalIgnoreCase);

    private static bool IsDcf(string file) => file.EndsWith(".dcf", StringComparison.OrdinalIgnoreCase);

    private static int UsageError(string message)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine(Usage);
        return 2;
    }

    /// <summary>Writes a report to stdout or, with <paramref name="output"/>, to a UTF-8 file. Returns 2 on an I/O error.</summary>
    private static int Emit(string? output, Action<TextWriter> write)
    {
        if (output is null)
        {
            write(Console.Out);
            return 0;
        }

        try
        {
            using var writer = new StreamWriter(output, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            write(writer);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Cannot write '" + output + "': " + ex.Message);
            return 2;
        }
    }
}
