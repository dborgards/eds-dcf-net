namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;
using EdsDcfNet.Models;
using FluentAssertions;
using Xunit;

/// <summary>
/// Real-world corpus guard (#525, phase 1): redistributable vendor/community device
/// files under Fixtures/Corpus must read without exception in lenient mode, validate
/// with no unexpected issues, and round-trip structurally stable (write -> re-read
/// preserves object count, index set, and DeviceInfo).
///
/// Lenient-mode regressions on real-world files are invisible with hand-written
/// fixtures alone — this theory is the safety net before parser changes land.
/// </summary>
public class RealWorldCorpusTests
{
    private static readonly string CorpusRoot = Path.Combine("Fixtures", "Corpus");

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

    public static IEnumerable<object[]> EdsCorpusFiles() => EnumerateCorpus("eds");

    public static IEnumerable<object[]> DcfCorpusFiles() => EnumerateCorpus("dcf");

    public static IEnumerable<object[]> XddCorpusFiles() => EnumerateCorpus("xdd");

    public static IEnumerable<object[]> XdcCorpusFiles() => EnumerateCorpus("xdc");

    [Theory]
    [MemberData(nameof(EdsCorpusFiles))]
    public void EdsCorpusFile_ReadsValidatesAndRoundTrips(string filePath)
    {
        if (NoCorpusFileYet(filePath))
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
        if (NoCorpusFileYet(filePath))
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
        if (NoCorpusFileYet(filePath))
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
        if (NoCorpusFileYet(filePath))
            return;

        var model = CanOpenFile.Xdc.ReadFile(filePath);
        AssertValidAsAllowListed(filePath, CanOpenFile.Validate(model));
        AssertRoundTripStable(
            model,
            m => CanOpenFile.Xdc.WriteToString(m),
            s => CanOpenFile.Xdc.ReadString(s));
    }

    private static IEnumerable<object[]> EnumerateCorpus(string extension)
    {
        var found = new List<string>();
        if (Directory.Exists(CorpusRoot))
        {
            foreach (var sourceDir in Directory.EnumerateDirectories(CorpusRoot))
                found.AddRange(Directory.EnumerateFiles(sourceDir, "*." + extension));
        }

        // xUnit errors on empty MemberData; emit a sentinel row the theories turn
        // into a documented early return so adding the first corpus file for a
        // format requires no test-code change.
        if (found.Count == 0)
            yield return new object[] { string.Empty };

        foreach (var file in found)
            yield return new object[] { file };
    }

    private static bool NoCorpusFileYet(string filePath)
    {
        // No corpus files for this format yet — the corpus grows incrementally
        // (redistributable real-world files only, see Fixtures/Corpus/*/NOTICE.md).
        return string.IsNullOrEmpty(filePath);
    }

    private static string CorpusRelativeKey(string filePath)
    {
        // Path.GetRelativePath is unavailable on net48; Uri.MakeRelativeUri works
        // on every target and already yields forward slashes.
        var rootUri = new Uri(Path.GetFullPath(CorpusRoot) + Path.DirectorySeparatorChar);
        var relative = rootUri.MakeRelativeUri(new Uri(Path.GetFullPath(filePath)));
        return Uri.UnescapeDataString(relative.ToString());
    }

    private static void AssertValidAsAllowListed(
        string filePath,
        IReadOnlyList<Validation.ValidationIssue> issues)
    {
        var key = CorpusRelativeKey(filePath);
        var allowed = ValidationAllowList.TryGetValue(key, out var entries)
            ? entries
            : Array.Empty<string>();

        var unexpected = issues
            .Select(i => i.ToString())
            .Except(allowed, StringComparer.Ordinal)
            .ToList();

        unexpected.Should().BeEmpty(
            "corpus file {0} must validate without unexpected issues (got: {1})",
            key,
            string.Join("; ", unexpected));
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

        reserialized.DeviceInfo.VendorName.Should().Be(model.DeviceInfo.VendorName);
        reserialized.DeviceInfo.ProductName.Should().Be(model.DeviceInfo.ProductName);
        reserialized.DeviceInfo.VendorNumber.Should().Be(model.DeviceInfo.VendorNumber);
        reserialized.DeviceInfo.ProductNumber.Should().Be(model.DeviceInfo.ProductNumber);
    }
}
