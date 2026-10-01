namespace EdsDcfNet.Tests.Writers;

using System.Text;
using System.Xml.Linq;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Utilities;
using EdsDcfNet.Writers;

/// <summary>
/// WriteFile/WriteFileAsync commit through a temporary file in the target directory: the old
/// target survives until the new content is complete, failures and cancellation leave the
/// target untouched and no temporary file behind.
/// </summary>
public sealed class AtomicFileWriteTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"atomic-{Guid.NewGuid():N}");

    public AtomicFileWriteTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private string Target(string name) => Path.Combine(_dir, name);

    private void AssertOnlyFiles(params string[] names)
        => Directory.EnumerateFileSystemEntries(_dir).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal)
            .Should().Equal(names.OrderBy(n => n, StringComparer.Ordinal), "no temporary file may remain");

    private static ElectronicDataSheet SampleEds() => new EdsReader().ReadFile(Path.Combine("Fixtures", "sample_device.eds"));

    private static DeviceConfigurationFile SampleDcf() => new DcfReader().ReadFile(Path.Combine("Fixtures", "minimal.dcf"));

    // ---- TextFileIo.WriteFileAtomic ----

    [Fact]
    public void WriteFileAtomic_TargetMissing_CreatesTargetAndLeavesNoTemp()
    {
        // Arrange
        var target = Target("new.txt");

        // Act
        TextFileIo.WriteFileAtomic(target, s => s.Write(new byte[] { 1, 2, 3 }, 0, 3));

        // Assert
        File.ReadAllBytes(target).Should().Equal(1, 2, 3);
        AssertOnlyFiles("new.txt");
    }

    [Fact]
    public void WriteFileAtomic_TargetExists_ReplacesContentAndLeavesNoTempOrBackup()
    {
        // Arrange
        var target = Target("existing.txt");
        File.WriteAllText(target, "old");

        // Act
        TextFileIo.WriteFileAtomic(target, s => s.Write(new byte[] { 9 }, 0, 1));

        // Assert
        File.ReadAllBytes(target).Should().Equal(9);
        AssertOnlyFiles("existing.txt");
    }

    [Fact]
    public void WriteFileAtomic_TargetExists_OldContentIsVisibleUntilCommit()
    {
        // Arrange
        var target = Target("existing.txt");
        File.WriteAllText(target, "old");
        string? seenWhileWriting = null;
        int entriesWhileWriting = 0;

        // Act
        TextFileIo.WriteFileAtomic(target, s =>
        {
            s.Write(new byte[] { 1, 2, 3 }, 0, 3);
            s.Flush();
            seenWhileWriting = File.ReadAllText(target);
            entriesWhileWriting = Directory.GetFileSystemEntries(_dir).Length;
        });

        // Assert
        seenWhileWriting.Should().Be("old");
        entriesWhileWriting.Should().Be(2, "the temporary file lives next to the target");
    }

    [Fact]
    public void WriteFileAtomic_WriteCallbackThrows_KeepsTargetAndRemovesTemp()
    {
        // Arrange
        var target = Target("existing.txt");
        File.WriteAllText(target, "old");

        // Act
        var act = () => TextFileIo.WriteFileAtomic(target, s =>
        {
            s.Write(new byte[] { 1, 2, 3 }, 0, 3);
            throw new InvalidOperationException("simulated failure while writing");
        });

        // Assert
        act.Should().Throw<InvalidOperationException>();
        File.ReadAllText(target).Should().Be("old");
        AssertOnlyFiles("existing.txt");
    }

    [Fact]
    public void WriteFileAtomic_WriteCallbackThrowsAndTargetMissing_CreatesNothing()
    {
        // Arrange
        var target = Target("never.txt");

        // Act
        var act = () => TextFileIo.WriteFileAtomic(target, _ => throw new InvalidOperationException("boom"));

        // Assert
        act.Should().Throw<InvalidOperationException>();
        AssertOnlyFiles();
    }

    [Fact]
    public async Task WriteFileAtomicAsync_CancelledWhileWriting_KeepsTargetAndRemovesTemp()
    {
        // Arrange
        var target = Target("existing.txt");
        File.WriteAllText(target, "old");
        using var cts = new CancellationTokenSource();

        // Act
        var act = () => TextFileIo.WriteFileAtomicAsync(
            target,
            async s =>
            {
                await s.WriteAsync(new byte[] { 1, 2, 3 }, 0, 3);
                cts.Cancel();
            },
            cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        File.ReadAllText(target).Should().Be("old");
        AssertOnlyFiles("existing.txt");
    }

    [Fact]
    public async Task WriteFileAtomicAsync_TargetExists_ReplacesContent()
    {
        // Arrange
        var target = Target("existing.txt");
        File.WriteAllText(target, "old");

        // Act
        await TextFileIo.WriteFileAtomicAsync(target, s => s.WriteAsync(new byte[] { 7, 8 }, 0, 2));

        // Assert
        File.ReadAllBytes(target).Should().Equal(7, 8);
        AssertOnlyFiles("existing.txt");
    }

    [Fact]
    public void GetOutputEncoding_IsUtf8WithoutBom()
    {
        // Act
        var encoding = TextFileIo.GetOutputEncoding();

        // Assert
        encoding.WebName.Should().Be("utf-8");
        encoding.GetPreamble().Should().BeEmpty();
    }

    // ---- Writers: existing / missing target, no temp left ----

    public static IEnumerable<object[]> AllWriters()
    {
        yield return new object[] { "eds" };
        yield return new object[] { "dcf" };
        yield return new object[] { "cpj" };
        yield return new object[] { "xdd" };
        yield return new object[] { "xdc" };
    }

    private static void WriteSync(string format, string path)
    {
        switch (format)
        {
            case "eds": new EdsWriter().WriteFile(SampleEds(), path); break;
            case "dcf": new DcfWriter().WriteFile(SampleDcf(), path); break;
            case "cpj": new CpjWriter().WriteFile(new CpjReader().ReadString("[Topology]\nNetName=N\nNodes=0x00\n"), path); break;
            case "xdd": new XddWriter().WriteFile(SampleEds(), path); break;
            case "xdc": new XdcWriter().WriteFile(SampleDcf(), path); break;
            default: throw new ArgumentOutOfRangeException(nameof(format));
        }
    }

    private static Task WriteAsync(string format, string path, CancellationToken ct)
        => format switch
        {
            "eds" => new EdsWriter().WriteFileAsync(SampleEds(), path, ct),
            "dcf" => new DcfWriter().WriteFileAsync(SampleDcf(), path, ct),
            "cpj" => new CpjWriter().WriteFileAsync(new CpjReader().ReadString("[Topology]\nNetName=N\nNodes=0x00\n"), path, ct),
            "xdd" => new XddWriter().WriteFileAsync(SampleEds(), path, ct),
            "xdc" => new XdcWriter().WriteFileAsync(SampleDcf(), path, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

    [Theory]
    [MemberData(nameof(AllWriters))]
    public void WriteFile_TargetMissing_CreatesFileWithoutTemp(string format)
    {
        // Arrange
        var target = Target("out." + format);

        // Act
        WriteSync(format, target);

        // Assert
        new FileInfo(target).Length.Should().BeGreaterThan(0);
        AssertOnlyFiles("out." + format);
    }

    [Theory]
    [MemberData(nameof(AllWriters))]
    public void WriteFile_TargetExists_ReplacesFullyWithoutTemp(string format)
    {
        // Arrange
        var target = Target("out." + format);
        File.WriteAllText(target, "STALE CONTENT THAT IS LONGER THAN NOTHING");

        // Act
        WriteSync(format, target);

        // Assert
        File.ReadAllText(target).Should().NotContain("STALE");
        AssertOnlyFiles("out." + format);
    }

    [Theory]
    [MemberData(nameof(AllWriters))]
    public async Task WriteFileAsync_TargetExists_ReplacesFullyWithoutTemp(string format)
    {
        // Arrange
        var target = Target("out." + format);
        File.WriteAllText(target, "STALE CONTENT THAT IS LONGER THAN NOTHING");

        // Act
        await WriteAsync(format, target, CancellationToken.None);

        // Assert
        File.ReadAllText(target).Should().NotContain("STALE");
        AssertOnlyFiles("out." + format);
    }

    [Theory]
    [MemberData(nameof(AllWriters))]
    public async Task WriteFileAsync_AlreadyCancelled_KeepsTargetAndThrowsOperationCanceled(string format)
    {
        // Arrange
        var target = Target("out." + format);
        File.WriteAllText(target, "old");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => WriteAsync(format, target, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        File.ReadAllText(target).Should().Be("old");
        AssertOnlyFiles("out." + format);
    }

    [Theory]
    [MemberData(nameof(AllWriters))]
    public void WriteFile_CommitFails_ThrowsFormatExceptionKeepsTargetAndLeavesNoTemp(string format)
    {
        // Arrange: a directory occupies the target path, so the commit cannot succeed.
        var target = Target("out." + format);
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "keep.txt"), "keep");

        // Act
        var act = () => WriteSync(format, target);

        // Assert
        var ex = act.Should().Throw<Exception>().Which;
        ex.Should().BeAssignableTo<WriteException>();
        File.ReadAllText(Path.Combine(target, "keep.txt")).Should().Be("keep");
        AssertOnlyFiles("out." + format);
    }

    [Theory]
    [MemberData(nameof(AllWriters))]
    public async Task WriteFileAsync_CommitFails_ThrowsFormatExceptionAndLeavesNoTemp(string format)
    {
        // Arrange
        var target = Target("out." + format);
        Directory.CreateDirectory(target);

        // Act
        var act = () => WriteAsync(format, target, CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<Exception>()).Which.Should().BeAssignableTo<WriteException>();
        AssertOnlyFiles("out." + format);
    }

    [Fact]
    public void XddWriteFile_InvalidXmlCharacterMidDocument_KeepsTargetAndLeavesNoTemp()
    {
        // Arrange
        var target = Target("out.xdd");
        File.WriteAllText(target, "old");
        var eds = SampleEds();
        eds.DeviceInfo.ProductName = "bad \u0001 char";

        // Act
        var act = () => new XddWriter().WriteFile(eds, target);

        // Assert
        act.Should().Throw<XddWriteException>();
        File.ReadAllText(target).Should().Be("old");
        AssertOnlyFiles("out.xdd");
    }

    [Fact]
    public void XddWriteFile_ResultIsWellFormedXmlInUtf8()
    {
        // Arrange
        var target = Target("out.xdd");

        // Act
        new XddWriter().WriteFile(SampleEds(), target);

        // Assert
        var bytes = File.ReadAllBytes(target);
        bytes.Take(3).Should().NotEqual(new byte[] { 0xEF, 0xBB, 0xBF });
        XDocument.Parse(new UTF8Encoding(false).GetString(bytes)).Root.Should().NotBeNull();
    }
}
