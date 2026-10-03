namespace EdsDcfNet.Tests.Writers;

using EdsDcfNet.Utilities;

/// <summary>
/// What the atomic commit keeps from the file it replaces: a symbolic link stays a link and
/// its target receives the content. The tests need symbolic links (net10.0 creates them; on
/// Windows that requires a privilege, so they skip when creation is refused).
/// </summary>
public sealed class AtomicFileWriteTargetTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"atomic-target-{Guid.NewGuid():N}");

    public AtomicFileWriteTargetTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private string PathOf(string name) => Path.Combine(_dir, name);

    private static readonly byte[] NewContent = { 0x4E, 0x45, 0x57 };

#if NET
    private static void CreateSymbolicLinkOrSkip(string link, string target)
    {
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Skip($"Symbolic links are not available here: {ex.Message}");
        }
    }

    private void AssertLinkKeptAndTargetWritten(string link, string target, string expectedLinkTarget)
    {
        new FileInfo(link).LinkTarget.Should().Be(expectedLinkTarget, "the link itself must not be replaced");
        File.ReadAllBytes(target).Should().Equal(NewContent);
        File.ReadAllBytes(link).Should().Equal(NewContent);
        Directory.EnumerateFileSystemEntries(_dir, ".edsdcf.*", SearchOption.AllDirectories)
            .Should().BeEmpty("no temporary file may remain");
    }

    [Fact]
    public void WriteFileAtomic_SymbolicLinkToExistingFile_WritesTargetAndKeepsLink()
    {
        // Arrange
        var target = PathOf("target.eds");
        var link = PathOf("link.eds");
        File.WriteAllText(target, "old");
        CreateSymbolicLinkOrSkip(link, target);

        // Act
        TextFileIo.WriteFileAtomic(link, s => s.Write(NewContent, 0, NewContent.Length));

        // Assert
        AssertLinkKeptAndTargetWritten(link, target, target);
    }

    [Fact]
    public async Task WriteFileAtomicAsync_SymbolicLinkToExistingFile_WritesTargetAndKeepsLink()
    {
        // Arrange
        var target = PathOf("target.eds");
        var link = PathOf("link.eds");
        File.WriteAllText(target, "old");
        CreateSymbolicLinkOrSkip(link, target);

        // Act
        await TextFileIo.WriteFileAtomicAsync(link, s => s.WriteAsync(NewContent, 0, NewContent.Length));

        // Assert
        AssertLinkKeptAndTargetWritten(link, target, target);
    }

    [Fact]
    public void WriteFileAtomic_RelativeSymbolicLinkIntoOtherDirectory_WritesTargetThere()
    {
        // Arrange: the temporary file must live next to the target, not next to the link.
        var otherDir = PathOf("other");
        Directory.CreateDirectory(otherDir);
        var target = Path.Combine(otherDir, "target.eds");
        var link = PathOf("link.eds");
        File.WriteAllText(target, "old");
        var relative = Path.Combine("other", "target.eds");
        CreateSymbolicLinkOrSkip(link, relative);

        // Act
        TextFileIo.WriteFileAtomic(link, s => s.Write(NewContent, 0, NewContent.Length));

        // Assert
        AssertLinkKeptAndTargetWritten(link, target, relative);
    }

    [Fact]
    public void WriteFileAtomic_ChainedSymbolicLinks_WritesFinalTarget()
    {
        // Arrange
        var target = PathOf("target.eds");
        var middle = PathOf("middle.eds");
        var link = PathOf("link.eds");
        File.WriteAllText(target, "old");
        CreateSymbolicLinkOrSkip(middle, target);
        CreateSymbolicLinkOrSkip(link, middle);

        // Act
        TextFileIo.WriteFileAtomic(link, s => s.Write(NewContent, 0, NewContent.Length));

        // Assert
        AssertLinkKeptAndTargetWritten(link, target, middle);
        new FileInfo(middle).LinkTarget.Should().Be(target);
    }

    [Fact]
    public void WriteFileAtomic_DanglingSymbolicLink_CreatesTargetAndKeepsLink()
    {
        // Arrange
        var target = PathOf("missing.eds");
        var link = PathOf("link.eds");
        CreateSymbolicLinkOrSkip(link, target);

        // Act
        TextFileIo.WriteFileAtomic(link, s => s.Write(NewContent, 0, NewContent.Length));

        // Assert
        AssertLinkKeptAndTargetWritten(link, target, target);
    }

    [Fact]
    public void EdsWriteFile_SymbolicLink_WritesTargetAndKeepsLink()
    {
        // Arrange
        var target = PathOf("target.eds");
        var link = PathOf("link.eds");
        File.WriteAllText(target, "old");
        CreateSymbolicLinkOrSkip(link, target);
        var eds = CanOpenFile.Eds.ReadFile(Path.Combine("Fixtures", "sample_device.eds"));

        // Act
        CanOpenFile.Eds.WriteFile(eds, link);

        // Assert
        new FileInfo(link).LinkTarget.Should().Be(target);
        CanOpenFile.Eds.ReadFile(target).DeviceInfo.ProductName.Should().Be(eds.DeviceInfo.ProductName);
    }
#endif
}
