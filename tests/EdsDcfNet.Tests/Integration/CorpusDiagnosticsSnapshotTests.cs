namespace EdsDcfNet.Tests.Integration;

using System.Text;
using EdsDcfNet;
using EdsDcfNet.Diagnostics;
using FluentAssertions;
using Xunit;

/// <summary>
/// Real-world corpus guard (#525, phase 2): the <see cref="ParseDiagnostic"/> list
/// emitted for each corpus file is snapshotted to <c>&lt;file&gt;.diagnostics.json</c>
/// next to the fixture and asserted for equality. Any parser change that alters
/// lenient-mode behaviour shows up as a snapshot diff in the PR.
///
/// Regenerate after an intentional parser change with
/// <c>UPDATE_CORPUS_SNAPSHOTS=1 dotnet test --filter CorpusDiagnosticsSnapshotTests</c>
/// and review the diff like any other source change (see CONTRIBUTING.md).
/// </summary>
public class CorpusDiagnosticsSnapshotTests
{
    private const string SnapshotExtension = ".diagnostics.json";

    public static IEnumerable<object[]> EdsCorpusFiles() => CorpusFiles.Enumerate("eds");

    public static IEnumerable<object[]> DcfCorpusFiles() => CorpusFiles.Enumerate("dcf");

    public static IEnumerable<object[]> XddCorpusFiles() => CorpusFiles.Enumerate("xdd");

    public static IEnumerable<object[]> XdcCorpusFiles() => CorpusFiles.Enumerate("xdc");

    [Theory]
    [MemberData(nameof(EdsCorpusFiles))]
    public void EdsCorpusFile_DiagnosticsMatchSnapshot(string filePath)
    {
        if (CorpusFiles.IsSentinel(filePath))
            return;

        AssertDiagnosticsMatchSnapshot(
            filePath,
            CanOpenFile.Eds.ReadFileWithDiagnostics(filePath).Diagnostics);
    }

    [Theory]
    [MemberData(nameof(DcfCorpusFiles))]
    public void DcfCorpusFile_DiagnosticsMatchSnapshot(string filePath)
    {
        if (CorpusFiles.IsSentinel(filePath))
            return;

        AssertDiagnosticsMatchSnapshot(
            filePath,
            CanOpenFile.Dcf.ReadFileWithDiagnostics(filePath).Diagnostics);
    }

    [Theory]
    [MemberData(nameof(XddCorpusFiles))]
    public void XddCorpusFile_DiagnosticsMatchSnapshot(string filePath)
    {
        if (CorpusFiles.IsSentinel(filePath))
            return;

        AssertDiagnosticsMatchSnapshot(
            filePath,
            CanOpenFile.Xdd.ReadFileWithDiagnostics(filePath).Diagnostics);
    }

    [Theory]
    [MemberData(nameof(XdcCorpusFiles))]
    public void XdcCorpusFile_DiagnosticsMatchSnapshot(string filePath)
    {
        if (CorpusFiles.IsSentinel(filePath))
            return;

        AssertDiagnosticsMatchSnapshot(
            filePath,
            CanOpenFile.Xdc.ReadFileWithDiagnostics(filePath).Diagnostics);
    }

    [Fact]
    public void SnapshotFiles_HaveMatchingCorpusFile()
    {
        // Symmetric guard: a snapshot whose corpus file was removed is stale and
        // must be deleted — otherwise it silently stops matching reality.
        if (!Directory.Exists(CorpusFiles.CorpusRoot))
            return;

        var orphans = Directory.EnumerateDirectories(CorpusFiles.CorpusRoot)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*" + SnapshotExtension))
            .Where(snapshot => !File.Exists(
                snapshot.Substring(0, snapshot.Length - SnapshotExtension.Length)))
            .Select(CorpusFiles.RelativeKey)
            .ToList();

        orphans.Should().BeEmpty(
            "every diagnostics snapshot must belong to an existing corpus file (orphans: {0})",
            string.Join("; ", orphans));
    }

    private static void AssertDiagnosticsMatchSnapshot(
        string filePath,
        IReadOnlyList<ParseDiagnostic> diagnostics)
    {
        var snapshotPath = filePath + SnapshotExtension;
        var actual = Serialize(diagnostics);

        if (string.Equals(
                Environment.GetEnvironmentVariable("UPDATE_CORPUS_SNAPSHOTS"),
                "1",
                StringComparison.Ordinal))
        {
            var sourceSnapshotPath = Path.Combine(
                CorpusFiles.FindSourceCorpusRoot(),
                CorpusFiles.RelativeKey(filePath) + SnapshotExtension);
            File.WriteAllText(sourceSnapshotPath, actual);
            return;
        }

        File.Exists(snapshotPath).Should().BeTrue(
            $"missing diagnostics snapshot at {snapshotPath}; regenerate with " +
            "UPDATE_CORPUS_SNAPSHOTS=1 dotnet test --filter CorpusDiagnosticsSnapshotTests " +
            "and commit the new snapshot");

        var expected = NormalizeNewlines(File.ReadAllText(snapshotPath));
        actual.Should().Be(expected,
            "lenient-mode diagnostics for corpus file {0} changed. If the parser change is " +
            "intentional, regenerate with UPDATE_CORPUS_SNAPSHOTS=1 and review the snapshot " +
            "diff in the PR (see CONTRIBUTING.md § Contributing a corpus file).",
            CorpusFiles.RelativeKey(filePath));
    }

    private static string Serialize(IReadOnlyList<ParseDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
            return "[]\n";

        var sb = new StringBuilder();
        sb.Append("[\n");
        for (var i = 0; i < diagnostics.Count; i++)
        {
            var d = diagnostics[i];
            sb.Append("  {\n");
            AppendProperty(sb, "severity", d.Severity.ToString());
            AppendProperty(sb, "code", d.Code);
            AppendProperty(sb, "path", d.Path);
            if (d.Line is { } line)
                sb.Append("    \"line\": ").Append(line).Append(",\n");
            if (d.RawValue is { } rawValue)
                AppendProperty(sb, "rawValue", rawValue);
            if (d.CoercedTo is { } coercedTo)
                AppendProperty(sb, "coercedTo", coercedTo);
            AppendProperty(sb, "message", d.Message, trailingComma: false);
            sb.Append("  }").Append(i < diagnostics.Count - 1 ? "," : string.Empty).Append('\n');
        }

        sb.Append("]\n");
        return sb.ToString();
    }

    private static void AppendProperty(
        StringBuilder sb,
        string name,
        string value,
        bool trailingComma = true)
    {
        sb.Append("    \"").Append(name).Append("\": ");
        AppendJsonString(sb, value);
        sb.Append(trailingComma ? ",\n" : "\n");
    }

    private static void AppendJsonString(StringBuilder sb, string value)
    {
        sb.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ')
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else
                        sb.Append(c);
                    break;
            }
        }

        sb.Append('"');
    }

    private static string NormalizeNewlines(string text)
        => text.Replace("\r\n", "\n");
}
