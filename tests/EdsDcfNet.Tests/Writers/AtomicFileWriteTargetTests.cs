namespace EdsDcfNet.Tests.Writers;

using System.Runtime.Versioning;
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
    public void WriteFileAtomic_SymbolicLinkLoop_ThrowsIOExceptionAndLeavesNoTemp()
    {
        // Arrange
        var first = PathOf("first.eds");
        var second = PathOf("second.eds");
        CreateSymbolicLinkOrSkip(first, second);
        CreateSymbolicLinkOrSkip(second, first);

        // Act
        var act = () => TextFileIo.WriteFileAtomic(first, s => s.Write(NewContent, 0, NewContent.Length));

        // Assert
        act.Should().Throw<IOException>().WithMessage("*symbolic links*");
        Directory.EnumerateFiles(_dir, ".edsdcf.*").Should().BeEmpty();
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

    // ---- Unix permissions of the replaced file ----

    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private const UnixFileMode Group0640 = OwnerOnly | UnixFileMode.GroupRead;

    private static void SkipOnWindows()
        => Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes do not exist on Windows.");

    [UnsupportedOSPlatform("windows")]
    [Theory]
    [InlineData(OwnerOnly)]
    [InlineData(Group0640)]
    public void WriteFileAtomic_ExistingFileWithRestrictiveMode_KeepsMode(UnixFileMode mode)
    {
        SkipOnWindows();

        // Arrange
        var target = PathOf("secret.eds");
        File.WriteAllText(target, "old");
        File.SetUnixFileMode(target, mode);

        // Act
        TextFileIo.WriteFileAtomic(target, s => s.Write(NewContent, 0, NewContent.Length));

        // Assert
        File.GetUnixFileMode(target).Should().Be(mode);
        File.ReadAllBytes(target).Should().Equal(NewContent);
    }

    private const UnixFileMode SetUser4700 = UnixFileMode.SetUser | UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private const UnixFileMode SetGroup2700 = UnixFileMode.SetGroup | UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    [UnsupportedOSPlatform("windows")]
    private string CreateTargetWithModeOrSkip(UnixFileMode mode)
    {
        var target = PathOf("special.eds");
        File.WriteAllText(target, "old");
        File.SetUnixFileMode(target, mode);
        Assert.SkipWhen(
            File.GetUnixFileMode(target) != mode,
            $"This process cannot set {mode} on a file in the temp directory.");
        return target;
    }

    [UnsupportedOSPlatform("windows")]
    [Theory]
    [InlineData(SetUser4700)]
    [InlineData(SetGroup2700)]
    public void WriteFileAtomic_ExistingFileWithSetIdBit_KeepsSpecialBits(UnixFileMode mode)
    {
        SkipOnWindows();

        // Arrange
        var target = CreateTargetWithModeOrSkip(mode);

        // Act: writing clears set-user-ID/set-group-ID without CAP_FSETID, so the mode must be reapplied.
        TextFileIo.WriteFileAtomic(target, s => s.Write(NewContent, 0, NewContent.Length));

        // Assert
        File.GetUnixFileMode(target).Should().Be(mode);
        File.ReadAllBytes(target).Should().Equal(NewContent);
    }

    [UnsupportedOSPlatform("windows")]
    [Theory]
    [InlineData(SetUser4700)]
    [InlineData(SetGroup2700)]
    public async Task WriteFileAtomicAsync_ExistingFileWithSetIdBit_KeepsSpecialBits(UnixFileMode mode)
    {
        SkipOnWindows();

        // Arrange
        var target = CreateTargetWithModeOrSkip(mode);

        // Act
        await TextFileIo.WriteFileAtomicAsync(target, s => s.WriteAsync(NewContent, 0, NewContent.Length));

        // Assert
        File.GetUnixFileMode(target).Should().Be(mode);
        File.ReadAllBytes(target).Should().Equal(NewContent);
    }

    [UnsupportedOSPlatform("windows")]
    [Fact]
    public async Task WriteFileAtomicAsync_ExistingFileWithMode0600_KeepsMode()
    {
        SkipOnWindows();

        // Arrange
        var target = PathOf("secret.eds");
        File.WriteAllText(target, "old");
        File.SetUnixFileMode(target, OwnerOnly);

        // Act
        await TextFileIo.WriteFileAtomicAsync(target, s => s.WriteAsync(NewContent, 0, NewContent.Length));

        // Assert
        File.GetUnixFileMode(target).Should().Be(OwnerOnly);
        File.ReadAllBytes(target).Should().Equal(NewContent);
    }

    [UnsupportedOSPlatform("windows")]
    [Fact]
    public void WriteFileAtomic_ExistingFileWithMode0600_TemporaryFileIsNeverWider()
    {
        SkipOnWindows();

        // Arrange
        var target = PathOf("secret.eds");
        File.WriteAllText(target, "old");
        File.SetUnixFileMode(target, OwnerOnly);
        UnixFileMode? tempModeWhileWriting = null;

        // Act
        TextFileIo.WriteFileAtomic(target, s =>
        {
            var temp = Directory.EnumerateFiles(_dir, ".edsdcf.*").Single();
            tempModeWhileWriting = File.GetUnixFileMode(temp);
            s.Write(NewContent, 0, NewContent.Length);
        });

        // Assert
        tempModeWhileWriting.Should().Be(OwnerOnly, "the content must not be readable by others while it is written");
    }

    [UnsupportedOSPlatform("windows")]
    [Fact]
    public void WriteFileAtomic_SymbolicLinkToFileWithMode0600_KeepsTargetMode()
    {
        SkipOnWindows();

        // Arrange
        var target = PathOf("secret.eds");
        var link = PathOf("link.eds");
        File.WriteAllText(target, "old");
        File.SetUnixFileMode(target, OwnerOnly);
        CreateSymbolicLinkOrSkip(link, target);

        // Act
        TextFileIo.WriteFileAtomic(link, s => s.Write(NewContent, 0, NewContent.Length));

        // Assert
        File.GetUnixFileMode(target).Should().Be(OwnerOnly);
        new FileInfo(link).LinkTarget.Should().Be(target);
    }

    [UnsupportedOSPlatform("windows")]
    [Fact]
    public void DcfWriteFile_ExistingFileWithMode0600_KeepsMode()
    {
        SkipOnWindows();

        // Arrange
        var target = PathOf("secret.dcf");
        File.WriteAllText(target, "old");
        File.SetUnixFileMode(target, OwnerOnly);
        var dcf = CanOpenFile.Dcf.ReadFile(Path.Combine("Fixtures", "minimal.dcf"));

        // Act
        CanOpenFile.Dcf.WriteFile(dcf, target);

        // Assert
        File.GetUnixFileMode(target).Should().Be(OwnerOnly);
        CanOpenFile.Dcf.ReadFile(target).DeviceInfo.ProductName.Should().Be(dcf.DeviceInfo.ProductName);
    }
#endif
}
