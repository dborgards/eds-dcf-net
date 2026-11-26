namespace EdsDcfNet.Checker;

using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using EdsDcfNet.Validation;
using Finding = EdsDcfNet.Validation.IniValidationIssue;
using Severity = EdsDcfNet.Validation.IniValidationSeverity;

/// <summary>The findings of one checked file.</summary>
/// <param name="File">Path as given on the command line or found in a directory.</param>
/// <param name="Findings">Findings after severity overrides.</param>
public sealed record FileResult(string File, IReadOnlyList<Finding> Findings);

/// <summary>Writes the text, JSON and SARIF reports and the rule list.</summary>
public static class Reports
{
    /// <summary>URI of the SARIF 2.1.0 JSON schema.</summary>
    public const string SarifSchema = "https://json.schemastore.org/sarif-2.1.0.json";

    private const string InformationUri = "https://github.com/dborgards/eds-dcf-net";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Version reported as the SARIF driver version (the informational version without build metadata).</summary>
    public static string ToolVersion { get; } = ReadVersion();

    /// <summary>Writes the compiler-style text report.</summary>
    /// <param name="writer">Target.</param>
    /// <param name="results">Checked files.</param>
    /// <param name="minimum">Lowest severity to list (counts always include every finding).</param>
    /// <param name="color">Colour finding lines through <see cref="Console.ForegroundColor"/>.</param>
    public static void WriteText(TextWriter writer, IReadOnlyList<FileResult> results, Severity minimum, bool color)
    {
        int totalErrors = 0, totalWarnings = 0;

        foreach (var (file, findings) in results)
        {
            var errors = findings.Count(f => f.Severity == Severity.Error);
            var warnings = findings.Count(f => f.Severity == Severity.Warning);
            totalErrors += errors;
            totalWarnings += warnings;

            writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0}: {1} ({2} error(s), {3} warning(s))",
                file, errors == 0 ? "OK" : "INVALID", errors, warnings));

            foreach (var finding in findings.Where(f => f.Severity >= minimum))
            {
                WriteLine(writer, "  " + finding.ToDisplayString(), finding.Severity, color);
            }

            writer.WriteLine();
        }

        writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "Checked {0} file(s): {1} error(s), {2} warning(s).", results.Count, totalErrors, totalWarnings));
    }

    /// <summary>Writes the JSON report: one object per file with counts and the listed findings.</summary>
    public static void WriteJson(TextWriter writer, IReadOnlyList<FileResult> results, Severity minimum)
    {
        var payload = results.Select(r => new
        {
            file = r.File,
            valid = r.Findings.All(f => f.Severity != Severity.Error),
            errors = r.Findings.Count(f => f.Severity == Severity.Error),
            warnings = r.Findings.Count(f => f.Severity == Severity.Warning),
            findings = r.Findings.Where(f => f.Severity >= minimum),
        });

        writer.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
    }

    /// <summary>
    /// Writes a SARIF 2.1.0 log with one run. Every catalog rule is listed as a reporting
    /// descriptor; each finding is a result with the file and line as physical location and
    /// the INI section and key as logical location.
    /// </summary>
    /// <param name="writer">Target.</param>
    /// <param name="results">Checked files.</param>
    /// <param name="minimum">Lowest severity to report.</param>
    /// <param name="baseDirectory">Paths below this directory are written as relative URIs.</param>
    public static void WriteSarif(TextWriter writer, IReadOnlyList<FileResult> results, Severity minimum, string baseDirectory)
    {
        var rules = CanOpenValidationRuleCatalog.All;
        var index = rules.Select((r, i) => (r.Id, i)).ToDictionary(p => p.Id, p => p.i, StringComparer.Ordinal);

        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, WriterOptions))
        {
            json.WriteStartObject();
            json.WriteString("$schema", SarifSchema);
            json.WriteString("version", "2.1.0");
            json.WriteStartArray("runs");
            json.WriteStartObject();

            json.WriteStartObject("tool");
            json.WriteStartObject("driver");
            json.WriteString("name", "edsdcf-check");
            json.WriteString("version", ToolVersion);
            json.WriteString("informationUri", InformationUri);
            json.WriteStartArray("rules");
            foreach (var rule in rules)
            {
                WriteRule(json, rule);
            }

            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteEndObject();

            json.WriteStartArray("results");
            foreach (var (file, findings) in results)
            {
                var uri = ToArtifactUri(file, baseDirectory);
                foreach (var finding in findings.Where(f => f.Severity >= minimum))
                {
                    WriteResult(json, finding, uri, index);
                }
            }

            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteEndArray();
            json.WriteEndObject();
        }

        writer.WriteLine(Encoding.UTF8.GetString(buffer.ToArray()));
    }

    /// <summary>Writes the rule catalog as a text table or, with <paramref name="json"/>, as a JSON array.</summary>
    public static void WriteRules(TextWriter writer, bool json)
    {
        if (json)
        {
            var payload = CanOpenValidationRuleCatalog.All.Select(r => new
            {
                id = r.Id,
                defaultSeverity = r.DefaultSeverity,
                title = r.Title,
                specReference = r.SpecReference,
                helpUri = r.HelpUri,
            });
            writer.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return;
        }

        var width = CanOpenValidationRuleCatalog.All.Max(r => r.Id.Length);
        foreach (var rule in CanOpenValidationRuleCatalog.All)
        {
            var line = rule.Id.PadRight(width) + "  " + Level(rule.DefaultSeverity).PadRight(7) + "  " + rule.Title;
            writer.WriteLine(rule.SpecReference is null ? line : line + " (" + rule.SpecReference + ")");
        }
    }

    /// <summary>
    /// Turns a checked path into a SARIF artifact URI: relative with <c>/</c> separators when the
    /// file is below <paramref name="baseDirectory"/>, otherwise an absolute <c>file:</c> URI.
    /// </summary>
    public static string ToArtifactUri(string file, string baseDirectory)
    {
        var relative = Paths.RelativeTo(baseDirectory, file);
        if (relative is null)
        {
            return new Uri(Path.GetFullPath(file)).AbsoluteUri;
        }

        return string.Join("/", relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Select(Uri.EscapeDataString));
    }

    private static void WriteRule(Utf8JsonWriter json, ValidationRule rule)
    {
        json.WriteStartObject();
        json.WriteString("id", rule.Id);
        json.WriteStartObject("shortDescription");
        json.WriteString("text", rule.Title);
        json.WriteEndObject();
        json.WriteStartObject("defaultConfiguration");
        json.WriteString("level", Level(rule.DefaultSeverity));
        json.WriteEndObject();
        if (rule.SpecReference is not null)
        {
            json.WriteStartObject("properties");
            json.WriteString("specReference", rule.SpecReference);
            json.WriteEndObject();
        }

        json.WriteEndObject();
    }

    private static void WriteResult(Utf8JsonWriter json, Finding finding, string uri, Dictionary<string, int> index)
    {
        json.WriteStartObject();
        json.WriteString("ruleId", finding.Code);
        if (index.TryGetValue(finding.Code, out var ruleIndex))
        {
            json.WriteNumber("ruleIndex", ruleIndex);
        }

        json.WriteString("level", Level(finding.Severity));
        json.WriteStartObject("message");
        json.WriteString("text", finding.Message);
        json.WriteEndObject();

        json.WriteStartArray("locations");
        json.WriteStartObject();
        json.WriteStartObject("physicalLocation");
        json.WriteStartObject("artifactLocation");
        json.WriteString("uri", uri);
        json.WriteEndObject();
        if (finding.Line is { } line)
        {
            json.WriteStartObject("region");
            json.WriteNumber("startLine", line);
            json.WriteEndObject();
        }

        json.WriteEndObject();
        if (finding.Section is not null)
        {
            json.WriteStartArray("logicalLocations");
            json.WriteStartObject();
            if (finding.Key is null)
            {
                json.WriteString("name", finding.Section);
                json.WriteString("fullyQualifiedName", "[" + finding.Section + "]");
                json.WriteString("kind", "object");
            }
            else
            {
                json.WriteString("name", finding.Key);
                json.WriteString("fullyQualifiedName", "[" + finding.Section + "]" + finding.Key);
                json.WriteString("kind", "member");
            }

            json.WriteEndObject();
            json.WriteEndArray();
        }

        json.WriteEndObject();
        json.WriteEndArray();
        json.WriteEndObject();
    }

    private static string Level(Severity severity) => severity == Severity.Error ? "error" : "warning";

    private static void WriteLine(TextWriter writer, string line, Severity severity, bool color)
    {
        if (!color)
        {
            writer.WriteLine(line);
            return;
        }

        var previous = Console.ForegroundColor;
        Console.ForegroundColor = severity == Severity.Error ? ConsoleColor.Red : ConsoleColor.Yellow;
        writer.WriteLine(line);
        Console.ForegroundColor = previous;
    }

    private static string ReadVersion()
    {
        var version = typeof(Reports).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        return version.Split('+')[0];
    }
}
