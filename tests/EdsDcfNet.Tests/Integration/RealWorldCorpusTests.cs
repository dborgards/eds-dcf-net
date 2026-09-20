namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;
using EdsDcfNet.Models;
using AwesomeAssertions;
using Xunit;

/// <summary>
/// Real-world corpus guard (#525, phase 1): redistributable vendor/community device
/// files under Fixtures/Corpus must read without exception in lenient mode, validate
/// with no unexpected issues, and round-trip structurally stable (write -> re-read
/// preserves object count, index set, and the complete DeviceInfo graph).
///
/// Lenient-mode regressions on real-world files are invisible with hand-written
/// fixtures alone — this theory is the safety net before parser changes land.
/// </summary>
public class RealWorldCorpusTests
{
    /// <summary>
    /// Validation issues that are known and accepted for a specific corpus file,
    /// keyed by path relative to the corpus root. Real-world files can legitimately
    /// deviate from CiA 306; every entry must be a conscious decision.
    /// </summary>
    private static readonly Dictionary<string, string[]> ValidationAllowList =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // e35.eds lists 0x6505 in [OptionalObjects] but ships no [6505] section —
            // a genuine inconsistency of the vendor file, kept as-is on purpose.
            ["python-canopen/e35.eds"] = new[]
            {
                "ObjectDictionary.OptionalObjects: Object list references missing object 0x6505.",
            },
        };

    public static IEnumerable<object[]> EdsCorpusFiles() => CorpusFiles.Enumerate("eds");

    public static IEnumerable<object[]> DcfCorpusFiles() => CorpusFiles.Enumerate("dcf");

    public static IEnumerable<object[]> XddCorpusFiles() => CorpusFiles.Enumerate("xdd");

    public static IEnumerable<object[]> XdcCorpusFiles() => CorpusFiles.Enumerate("xdc");

    [Theory]
    [MemberData(nameof(EdsCorpusFiles))]
    public void EdsCorpusFile_ReadsValidatesAndRoundTrips(string filePath)
    {
        if (CorpusFiles.IsSentinel(filePath))
            return;

        var model = CanOpenFile.Eds.ReadFile(filePath);
        AssertValidAsAllowListed(filePath, CanOpenFile.Validate(model));
        AssertRoundTripStable(
            model,
            m => CanOpenFile.Eds.WriteToString(m),
            s => CanOpenFile.Eds.ReadString(s));
    }

    [Theory]
    [MemberData(nameof(DcfCorpusFiles))]
    public void DcfCorpusFile_ReadsValidatesAndRoundTrips(string filePath)
    {
        if (CorpusFiles.IsSentinel(filePath))
            return;

        var model = CanOpenFile.Dcf.ReadFile(filePath);
        AssertValidAsAllowListed(filePath, CanOpenFile.Validate(model));
        AssertRoundTripStable(
            model,
            m => CanOpenFile.Dcf.WriteToString(m),
            s => CanOpenFile.Dcf.ReadString(s));
    }

    [Theory]
    [MemberData(nameof(XddCorpusFiles))]
    public void XddCorpusFile_ReadsAndRoundTrips(string filePath)
    {
        if (CorpusFiles.IsSentinel(filePath))
            return;

        var model = CanOpenFile.Xdd.ReadFile(filePath);
        AssertValidAsAllowListed(filePath, CanOpenFile.Validate(model));
        AssertRoundTripStable(
            model,
            m => CanOpenFile.Xdd.WriteToString(m),
            s => CanOpenFile.Xdd.ReadString(s));
    }

    [Theory]
    [MemberData(nameof(XdcCorpusFiles))]
    public void XdcCorpusFile_ReadsAndRoundTrips(string filePath)
    {
        if (CorpusFiles.IsSentinel(filePath))
            return;

        var model = CanOpenFile.Xdc.ReadFile(filePath);
        AssertValidAsAllowListed(filePath, CanOpenFile.Validate(model));
        AssertRoundTripStable(
            model,
            m => CanOpenFile.Xdc.WriteToString(m),
            s => CanOpenFile.Xdc.ReadString(s));
    }

    private static void AssertValidAsAllowListed(
        string filePath,
        IReadOnlyList<Validation.ValidationIssue> issues)
    {
        var key = CorpusFiles.RelativeKey(filePath);
        var allowed = ValidationAllowList.TryGetValue(key, out var entries)
            ? entries
            : Array.Empty<string>();

        var actual = issues.Select(i => i.ToString()).ToList();

        var unexpected = actual
            .Except(allowed, StringComparer.Ordinal)
            .ToList();

        unexpected.Should().BeEmpty(
            "corpus file {0} must validate without unexpected issues (got: {1})",
            key,
            string.Join("; ", unexpected));

        // Symmetric check: an allow-listed deviation that no longer occurs is stale
        // and must be removed — otherwise the list silently stops matching reality.
        var stale = allowed
            .Except(actual, StringComparer.Ordinal)
            .ToList();

        stale.Should().BeEmpty(
            "allow-list entries for {0} must still occur (stale: {1})",
            key,
            string.Join("; ", stale));
    }

    private static void AssertRoundTripStable<TModel>(
        TModel model,
        Func<TModel, string> write,
        Func<string, TModel> read)
        where TModel : class, ICanOpenFileModel
    {
        var reserialized = read(write(model));

        reserialized.ObjectDictionary.Objects.Count.Should().Be(
            model.ObjectDictionary.Objects.Count,
            "round-trip must preserve the object count");
        reserialized.ObjectDictionary.Objects.Keys.Should().BeEquivalentTo(
            model.ObjectDictionary.Objects.Keys,
            "round-trip must preserve the index set");

        // Full DeviceInfo graph (revision, order code, baud rates, boot-up flags,
        // PDO counts, …) — a dropped writer/reader field must fail here.
        reserialized.DeviceInfo.Should().BeEquivalentTo(
            model.DeviceInfo,
            "round-trip must preserve the complete DeviceInfo graph");
    }
}
