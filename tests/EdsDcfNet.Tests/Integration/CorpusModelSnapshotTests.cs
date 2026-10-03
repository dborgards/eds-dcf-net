namespace EdsDcfNet.Tests.Integration;

using System.Globalization;
using System.Text;
using System.Xml.Linq;
using EdsDcfNet;
using EdsDcfNet.Models;
using AwesomeAssertions;
using Xunit;

/// <summary>
/// Real-world corpus guard (WP-01 of the 2026-09-30 action plan): next to the
/// diagnostics snapshot, the parsed object model of every corpus file is frozen to
/// <c>&lt;file&gt;.model.json</c>. Per object and sub-object it records Index, SubIndex,
/// ObjectType, DataType, AccessType, DefaultValue and PDOMapping, sorted by
/// (Index, SubIndex) and formatted culture-neutrally. For XDD/XDC files the header
/// additionally counts <c>uniqueIDRef</c> references and how many of them already
/// carry a resolved DataType in the model.
///
/// A parser fix that changes what ends up in the model shows up as a snapshot diff.
/// Regenerate after an intentional change with
/// <c>UPDATE_CORPUS_SNAPSHOTS=1 dotnet test -f net10.0 --filter CorpusModelSnapshotTests</c>
/// and review the diff like any other source change (see CONTRIBUTING.md).
/// </summary>
public class CorpusModelSnapshotTests
{
    private const string SnapshotExtension = ".model.json";

    public static IEnumerable<object[]> EdsCorpusFiles() => CorpusFiles.Enumerate("eds");

    public static IEnumerable<object[]> DcfCorpusFiles() => CorpusFiles.Enumerate("dcf");

    public static IEnumerable<object[]> XddCorpusFiles() => CorpusFiles.Enumerate("xdd");

    public static IEnumerable<object[]> XdcCorpusFiles() => CorpusFiles.Enumerate("xdc");

    [Theory]
    [MemberData(nameof(EdsCorpusFiles))]
    public void EdsCorpusFile_ModelMatchesSnapshot(string filePath)
    {
        if (CorpusFiles.IsSentinel(filePath))
            return;

        var model = CanOpenFile.Eds.ReadFileWithDiagnostics(filePath).Model;
        AssertModelMatchesSnapshot(filePath, Serialize(model.ObjectDictionary, uniqueIdRefs: null, model.SupportedModules));
    }

    [Theory]
    [MemberData(nameof(DcfCorpusFiles))]
    public void DcfCorpusFile_ModelMatchesSnapshot(string filePath)
    {
        if (CorpusFiles.IsSentinel(filePath))
            return;

        var model = CanOpenFile.Dcf.ReadFileWithDiagnostics(filePath).Model;
        AssertModelMatchesSnapshot(filePath, Serialize(model.ObjectDictionary, uniqueIdRefs: null, model.SupportedModules));
    }

    [Theory]
    [MemberData(nameof(XddCorpusFiles))]
    public void XddCorpusFile_ModelMatchesSnapshot(string filePath)
    {
        if (CorpusFiles.IsSentinel(filePath))
            return;

        var model = CanOpenFile.Xdd.ReadFileWithDiagnostics(filePath).Model;
        AssertModelMatchesSnapshot(
            filePath,
            Serialize(
                model.ObjectDictionary,
                CountUniqueIdRefs(filePath, model.ObjectDictionary),
                model.SupportedModules));
    }

    [Theory]
    [MemberData(nameof(XdcCorpusFiles))]
    public void XdcCorpusFile_ModelMatchesSnapshot(string filePath)
    {
        if (CorpusFiles.IsSentinel(filePath))
            return;

        var model = CanOpenFile.Xdc.ReadFileWithDiagnostics(filePath).Model;
        AssertModelMatchesSnapshot(
            filePath,
            Serialize(
                model.ObjectDictionary,
                CountUniqueIdRefs(filePath, model.ObjectDictionary),
                model.SupportedModules));
    }

    [Fact]
    public void ModuleSectionsCanonicalFile_ModelSnapshot_ShowsFilledCollections()
    {
        // Arrange
        var model = CanOpenFile.Eds.ReadFile(Path.Combine("Fixtures", "module_sections_canonical.eds"));
        var snapshotPath = Path.Combine("Fixtures", "module_sections_canonical.eds.model.json");

        // Act
        var snapshot = Serialize(model.ObjectDictionary, uniqueIdRefs: null, model.SupportedModules);

        // Assert — the frozen snapshot is the visible record that module collections are filled.
        var expected = File.ReadAllText(snapshotPath).Replace("\r\n", "\n");
        snapshot.Should().Be(expected);
        snapshot.Should().Contain("\"comments\"");
        snapshot.Should().Contain("\"fixedObjectDefinitions\"");
        snapshot.Should().Contain("\"subExtends\"");
        snapshot.Should().Contain("\"subExtensionDefinitions\"");
        snapshot.Should().Contain("\"count\": \"4\"");
        snapshot.Should().Contain("\"count\": \"0;2\"");
    }

    [Fact]
    public void ModelSnapshotFiles_HaveMatchingCorpusFile()
    {
        if (!Directory.Exists(CorpusFiles.CorpusRoot))
            return;

        var orphans = Directory.EnumerateDirectories(CorpusFiles.CorpusRoot)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*" + SnapshotExtension))
            .Where(snapshot => !File.Exists(
                snapshot.Substring(0, snapshot.Length - SnapshotExtension.Length)))
            .Select(CorpusFiles.RelativeKey)
            .ToList();

        orphans.Should().BeEmpty(
            "every model snapshot must belong to an existing corpus file (orphans: {0})",
            string.Join("; ", orphans));
    }

    private static void AssertModelMatchesSnapshot(string filePath, string actual)
    {
        var snapshotPath = filePath + SnapshotExtension;

        if (CorpusFiles.UpdateSnapshotsRequested)
        {
            var sourceSnapshotPath = Path.Combine(
                CorpusFiles.FindSourceCorpusRoot(),
                CorpusFiles.RelativeKey(filePath) + SnapshotExtension);
            File.WriteAllText(sourceSnapshotPath, actual);
            return;
        }

        File.Exists(snapshotPath).Should().BeTrue(
            $"missing model snapshot at {snapshotPath}; regenerate with " +
            "UPDATE_CORPUS_SNAPSHOTS=1 dotnet test -f net10.0 --filter CorpusModelSnapshotTests " +
            "and commit the new snapshot");

        var expected = File.ReadAllText(snapshotPath).Replace("\r\n", "\n");
        actual.Should().Be(expected,
            "the parsed object model for corpus file {0} changed. If the parser change is " +
            "intentional, regenerate with UPDATE_CORPUS_SNAPSHOTS=1 and review the snapshot " +
            "diff in the PR (see CONTRIBUTING.md § Contributing a corpus file).",
            CorpusFiles.RelativeKey(filePath));
    }

    /// <summary>
    /// Counts CANopenObject/CANopenSubObject elements carrying a <c>uniqueIDRef</c> in the
    /// source XML and how many of them have a DataType in the parsed model.
    /// </summary>
    private static (int Total, int Resolved) CountUniqueIdRefs(string filePath, ObjectDictionary od)
    {
        var doc = XDocument.Load(filePath);
        var total = 0;
        var resolved = 0;

        foreach (var obj in doc.Descendants().Where(e => e.Name.LocalName == "CANopenObject"))
        {
            var index = ParseHex(obj.Attribute("index")?.Value);
            od.Objects.TryGetValue((ushort)index, out var model);

            if (obj.Attribute("uniqueIDRef") is not null)
            {
                total++;
                if (model?.DataType is { } dt && dt != 0)
                    resolved++;
            }

            foreach (var sub in obj.Elements().Where(e => e.Name.LocalName == "CANopenSubObject"))
            {
                if (sub.Attribute("uniqueIDRef") is null)
                    continue;

                total++;
                var subIndex = (byte)ParseHex(sub.Attribute("subIndex")?.Value);
                if (model is not null
                    && model.SubObjects.TryGetValue(subIndex, out var subModel)
                    && subModel.DataType != 0)
                    resolved++;
            }
        }

        return (total, resolved);
    }

    private static int ParseHex(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;
        if (value!.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            value = value.Substring(2);
        return int.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private static string Serialize(
        ObjectDictionary od,
        (int Total, int Resolved)? uniqueIdRefs,
        IReadOnlyList<ModuleInfo>? modules = null)
    {
        var sb = new StringBuilder();
        sb.Append("{\n");

        var subCount = od.Objects.Values.Sum(o => o.SubObjects.Count);
        var emptyDataTypes = od.Objects.Values.Count(o => (o.DataType ?? 0) == 0)
            + od.Objects.Values.SelectMany(o => o.SubObjects.Values).Count(s => s.DataType == 0);

        sb.Append("  \"objectCount\": ").Append(od.Objects.Count.ToString(CultureInfo.InvariantCulture)).Append(",\n");
        sb.Append("  \"subObjectCount\": ").Append(subCount.ToString(CultureInfo.InvariantCulture)).Append(",\n");
        sb.Append("  \"emptyDataTypeCount\": ").Append(emptyDataTypes.ToString(CultureInfo.InvariantCulture)).Append(",\n");
        if (uniqueIdRefs is { } refs)
        {
            sb.Append("  \"uniqueIdRefCount\": ").Append(refs.Total.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"uniqueIdRefResolvedCount\": ").Append(refs.Resolved.ToString(CultureInfo.InvariantCulture)).Append(",\n");
        }

        sb.Append("  \"objects\": [");
        var firstObject = true;
        foreach (var obj in od.Objects.Values.OrderBy(o => o.Index))
        {
            sb.Append(firstObject ? "\n" : ",\n");
            firstObject = false;
            sb.Append("    {");
            AppendIndex(sb, "index", obj.Index, 4);
            AppendCommon(sb, obj.ObjectType, obj.DataType, obj.AccessType, obj.DefaultValue, obj.PdoMappingMode);
            if (obj.SubObjects.Count > 0)
            {
                sb.Append(", \"subObjects\": [");
                var firstSub = true;
                foreach (var sub in obj.SubObjects.Values.OrderBy(s => s.SubIndex))
                {
                    sb.Append(firstSub ? "\n      {" : ",\n      {");
                    firstSub = false;
                    AppendIndex(sb, "subIndex", sub.SubIndex, 2);
                    AppendCommon(sb, sub.ObjectType, sub.DataType, sub.AccessType, sub.DefaultValue, sub.PdoMappingMode);
                    sb.Append('}');
                }

                sb.Append("\n    ]");
            }

            sb.Append('}');
        }

        sb.Append(firstObject ? "]" : "\n  ]");
        AppendModules(sb, modules);
        sb.Append("\n}\n");
        return sb.ToString();
    }

    /// <summary>
    /// Emits module collections only when a file actually carries CiA 306-1 §8.3
    /// section data. Files without those collections keep the previous snapshot text.
    /// </summary>
    private static void AppendModules(StringBuilder sb, IReadOnlyList<ModuleInfo>? modules)
    {
        if (modules == null || !modules.Any(HasModuleSectionData))
            return;

        sb.Append(",\n  \"modules\": [");
        var firstModule = true;
        foreach (var module in modules.Where(HasModuleSectionData))
        {
            sb.Append(firstModule ? "\n    {" : ",\n    {");
            firstModule = false;
            sb.Append("\"moduleNumber\": ").Append(module.ModuleNumber.ToString(CultureInfo.InvariantCulture));

            if (module.Comments != null)
            {
                sb.Append(", \"comments\": {\"lines\": ")
                    .Append(module.Comments.Lines.ToString(CultureInfo.InvariantCulture))
                    .Append(", \"commentLines\": [");
                var firstLine = true;
                foreach (var line in module.Comments.CommentLines.OrderBy(entry => entry.Key))
                {
                    sb.Append(firstLine ? "" : ", ");
                    firstLine = false;
                    CorpusFiles.AppendJsonString(sb, line.Value);
                }

                sb.Append("]}");
            }

            if (module.FixedObjectDefinitions.Count > 0)
            {
                sb.Append(", \"fixedObjectDefinitions\": [");
                var firstObject = true;
                foreach (var obj in module.FixedObjectDefinitions.Values.OrderBy(o => o.Index))
                {
                    sb.Append(firstObject ? "\n      {" : ",\n      {");
                    firstObject = false;
                    AppendIndex(sb, "index", obj.Index, 4);
                    sb.Append(", \"parameterName\": ");
                    CorpusFiles.AppendJsonString(sb, obj.ParameterName);
                    if (obj.SubObjects.Count > 0)
                    {
                        sb.Append(", \"subObjects\": [");
                        var firstSub = true;
                        foreach (var sub in obj.SubObjects.Values.OrderBy(s => s.SubIndex))
                        {
                            sb.Append(firstSub ? "{" : ", {");
                            firstSub = false;
                            AppendIndex(sb, "subIndex", sub.SubIndex, 2);
                            sb.Append(", \"parameterName\": ");
                            CorpusFiles.AppendJsonString(sb, sub.ParameterName);
                            sb.Append('}');
                        }

                        sb.Append(']');
                    }

                    sb.Append('}');
                }

                sb.Append("\n    ]");
            }

            if (module.SubExtends.Count > 0)
            {
                sb.Append(", \"subExtends\": [");
                for (var i = 0; i < module.SubExtends.Count; i++)
                {
                    if (i > 0)
                        sb.Append(", ");
                    sb.Append("\"0x").Append(module.SubExtends[i].ToString("X4", CultureInfo.InvariantCulture)).Append('"');
                }

                sb.Append(']');
            }

            if (module.SubExtensionDefinitions.Count > 0)
            {
                sb.Append(", \"subExtensionDefinitions\": [");
                var firstExtension = true;
                foreach (var extension in module.SubExtensionDefinitions.Values.OrderBy(e => e.Index))
                {
                    sb.Append(firstExtension ? "\n      {" : ",\n      {");
                    firstExtension = false;
                    AppendIndex(sb, "index", extension.Index, 4);
                    sb.Append(", \"parameterName\": ");
                    CorpusFiles.AppendJsonString(sb, extension.ParameterName);
                    sb.Append(", \"count\": ");
                    CorpusFiles.AppendJsonString(sb, extension.Count);
                    sb.Append(", \"objExtend\": ");
                    if (extension.ObjExtend is { } objExtend)
                        sb.Append(objExtend.ToString(CultureInfo.InvariantCulture));
                    else
                        sb.Append("null");
                    sb.Append('}');
                }

                sb.Append("\n    ]");
            }

            sb.Append('}');
        }

        sb.Append("\n  ]");
    }

    private static bool HasModuleSectionData(ModuleInfo module)
        => module.Comments != null
           || module.FixedObjectDefinitions.Count > 0
           || module.SubExtends.Count > 0
           || module.SubExtensionDefinitions.Count > 0;

    private static void AppendIndex(StringBuilder sb, string name, int value, int width)
    {
        sb.Append('"').Append(name).Append("\": \"0x")
            .Append(value.ToString("X" + width, CultureInfo.InvariantCulture)).Append('"');
    }

    private static void AppendCommon(
        StringBuilder sb,
        byte objectType,
        ushort? dataType,
        AccessType accessType,
        string? defaultValue,
        PdoMappingMode pdoMapping)
    {
        sb.Append(", \"objectType\": ").Append(objectType.ToString(CultureInfo.InvariantCulture));
        sb.Append(", \"dataType\": ");
        if (dataType is { } dt)
            sb.Append("\"0x").Append(dt.ToString("X4", CultureInfo.InvariantCulture)).Append('"');
        else
            sb.Append("null");
        sb.Append(", \"accessType\": \"").Append(accessType.ToString()).Append('"');
        sb.Append(", \"defaultValue\": ");
        if (defaultValue is null)
            sb.Append("null");
        else
            CorpusFiles.AppendJsonString(sb, defaultValue);
        sb.Append(", \"pdoMapping\": \"").Append(pdoMapping.ToString()).Append('"');
    }
}
