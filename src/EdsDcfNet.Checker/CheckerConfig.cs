namespace EdsDcfNet.Checker;

using System.Text.Json;
using System.Text.RegularExpressions;
using EdsDcfNet.Validation;

/// <summary>
/// Severity overrides and path globs for <c>edsdcf-check</c>, read from a <c>--config</c> JSON file
/// and the <c>--severity</c> options.
/// </summary>
/// <remarks>
/// <code>
/// {
///   "severity": { "VAL005": "none", "INI003": "error" },
///   "exclude": [ "vendor/**" ],
///   "overrides": [
///     { "files": [ "legacy/*.eds" ], "severity": { "OBJ010": "warning" } }
///   ]
/// }
/// </code>
/// Globs are matched case-insensitively against the path relative to the config file directory,
/// with <c>/</c> as separator: <c>*</c> matches within one path segment, <c>**</c> across segments,
/// <c>?</c> one character. Later entries win: <c>severity</c>, then each matching override in order,
/// then <c>--severity</c> on the command line.
/// </remarks>
public sealed class CheckerConfig
{
    private readonly List<(Regex[] Files, Dictionary<string, IniValidationSeverity?> Severity)> _overrides = new();
    private readonly List<Regex> _exclude = new();
    private readonly Dictionary<string, IniValidationSeverity?> _severity = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IniValidationSeverity?> _commandLine = new(StringComparer.OrdinalIgnoreCase);
    private string _baseDirectory = Directory.GetCurrentDirectory();

    /// <summary>Loads a config file. Throws <see cref="FormatException"/> for invalid content.</summary>
    public static CheckerConfig Load(string path, string json)
    {
        var config = new CheckerConfig
        {
            _baseDirectory = Path.GetDirectoryName(Path.GetFullPath(path))!,
        };

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException ex)
        {
            throw new FormatException("Invalid JSON: " + ex.Message, ex);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("The config must be a JSON object.");
            }

            foreach (var property in root.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "severity":
                        ReadSeverity(property.Value, "severity", config._severity);
                        break;
                    case "exclude":
                        config._exclude.AddRange(ReadGlobs(property.Value, "exclude"));
                        break;
                    case "overrides":
                        ReadOverrides(property.Value, config._overrides);
                        break;
                    default:
                        throw new FormatException("Unknown property '" + property.Name + "'.");
                }
            }
        }

        return config;
    }

    /// <summary>Adds a <c>--severity ID=level</c> override. Throws <see cref="FormatException"/> when invalid.</summary>
    public void AddCommandLineOverride(string value)
    {
        var separator = value.IndexOf('=', StringComparison.Ordinal);
        if (separator <= 0)
        {
            throw new FormatException("Expected ID=error|warning|none, got '" + value + "'.");
        }

        var id = value[..separator].Trim();
        _commandLine[CheckRule(id)] = ParseLevel(value[(separator + 1)..].Trim(), id);
    }

    /// <summary>Whether <paramref name="file"/> matches an <c>exclude</c> glob.</summary>
    public bool IsExcluded(string file) => Matches(_exclude, file);

    /// <summary>The validation options for one file, with every override that applies to it.</summary>
    public CanOpenIniValidationOptions OptionsFor(string file, bool runLibrary)
    {
        var merged = new Dictionary<string, IniValidationSeverity?>(_severity, StringComparer.OrdinalIgnoreCase);
        foreach (var (files, severity) in _overrides)
        {
            if (Matches(files, file))
            {
                foreach (var pair in severity)
                {
                    merged[pair.Key] = pair.Value;
                }
            }
        }

        foreach (var pair in _commandLine)
        {
            merged[pair.Key] = pair.Value;
        }

        return new CanOpenIniValidationOptions
        {
            SeverityOverrides = merged,
            IncludeReaderDiagnostics = runLibrary,
            IncludeModelValidation = runLibrary,
        };
    }

    /// <summary>Turns a glob into a regular expression over <c>/</c>-separated paths.</summary>
    public static Regex GlobToRegex(string glob)
    {
        var pattern = new System.Text.StringBuilder("^");
        var text = glob.Replace('\\', '/');
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '*' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i++;
                if (i + 1 < text.Length && text[i + 1] == '/')
                {
                    // "**/" also matches no directory at all.
                    i++;
                    pattern.Append("(?:.*/)?");
                }
                else
                {
                    pattern.Append(".*");
                }
            }
            else if (c == '*')
            {
                pattern.Append("[^/]*");
            }
            else if (c == '?')
            {
                pattern.Append("[^/]");
            }
            else
            {
                pattern.Append(Regex.Escape(c.ToString()));
            }
        }

        return new Regex(pattern.Append('$').ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private bool Matches(IReadOnlyCollection<Regex> globs, string file)
    {
        if (globs.Count == 0)
        {
            return false;
        }

        var candidate = (Paths.RelativeTo(_baseDirectory, file) ?? Path.GetFullPath(file)).Replace('\\', '/');
        return globs.Any(g => g.IsMatch(candidate));
    }

    private static void ReadOverrides(
        JsonElement element,
        List<(Regex[] Files, Dictionary<string, IniValidationSeverity?> Severity)> overrides)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("'overrides' must be an array.");
        }

        foreach (var entry in element.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object ||
                !entry.TryGetProperty("files", out var files) ||
                !entry.TryGetProperty("severity", out var severity))
            {
                throw new FormatException("Each override needs 'files' and 'severity'.");
            }

            foreach (var property in entry.EnumerateObject())
            {
                if (property.Name is not ("files" or "severity"))
                {
                    throw new FormatException("Unknown override property '" + property.Name + "'.");
                }
            }

            var map = new Dictionary<string, IniValidationSeverity?>(StringComparer.OrdinalIgnoreCase);
            ReadSeverity(severity, "overrides[].severity", map);
            overrides.Add((ReadGlobs(files, "overrides[].files").ToArray(), map));
        }
    }

    private static List<Regex> ReadGlobs(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("'" + name + "' must be an array of globs.");
        }

        var globs = new List<Regex>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                throw new FormatException("'" + name + "' must contain non-empty strings.");
            }

            globs.Add(GlobToRegex(item.GetString()!));
        }

        return globs;
    }

    private static void ReadSeverity(JsonElement element, string name, Dictionary<string, IniValidationSeverity?> target)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("'" + name + "' must map rule ids to error, warning or none.");
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw new FormatException("Severity of '" + property.Name + "' must be error, warning or none.");
            }

            target[CheckRule(property.Name)] = ParseLevel(property.Value.GetString()!, property.Name);
        }
    }

    private static string CheckRule(string id) =>
        CanOpenValidationRuleCatalog.TryGet(id, out var rule)
            ? rule.Id
            : throw new FormatException("Unknown rule '" + id + "'. Use --list-rules to see all rules.");

    private static IniValidationSeverity? ParseLevel(string level, string id) =>
        level.ToLowerInvariant() switch
        {
            "error" => IniValidationSeverity.Error,
            "warning" => IniValidationSeverity.Warning,
            "none" => null,
            _ => throw new FormatException("Severity of '" + id + "' must be error, warning or none, got '" + level + "'."),
        };
}
