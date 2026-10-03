namespace EdsDcfNet.Tests.Utilities;

using System.Runtime.InteropServices;
using EdsDcfNet.Utilities;

/// <summary>
/// The pure path step of the writer's symbolic-link walk. Rooted targets are taken verbatim
/// (a UNC target must not become "&lt;cwd&gt;\UNC\server\share\..."), relative targets are combined
/// with the link's directory without normalization.
/// </summary>
public class SymbolicLinkPathTests
{
    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    [Theory]
    [InlineData(@"\\server\share\x.eds")]
    [InlineData(@"\\?\UNC\server\share\x.eds")]
    [InlineData(@"C:\data\x.eds")]
    [InlineData(@"\\?\C:\data\x.eds")]
    public void Next_WindowsRootedTarget_IsReturnedVerbatim(string rawTarget)
    {
        Assert.SkipUnless(IsWindows, "Backslash and drive roots are Windows path syntax.");

        // Act
        var next = SymbolicLinkPath.Next(@"D:\links\link.eds", rawTarget);

        // Assert
        next.Should().Be(rawTarget);
    }

    [Fact]
    public void Next_UnixAbsoluteTarget_IsReturnedVerbatim()
    {
        Assert.SkipWhen(IsWindows, "A leading slash is not fully qualified on Windows.");

        SymbolicLinkPath.Next("/links/link.eds", "/data/x.eds").Should().Be("/data/x.eds");
    }

    [Fact]
    public void Next_RelativeTarget_IsCombinedWithLinkDirectoryWithoutNormalizing()
    {
        // Arrange
        var linkDirectory = Path.Combine(Path.GetTempPath(), "links");
        var link = Path.Combine(linkDirectory, "link.eds");
        var raw = Path.Combine("..", "data", "x.eds");

        // Act
        var next = SymbolicLinkPath.Next(link, raw);

        // Assert
        next.Should().Be(Path.Combine(linkDirectory, raw));
    }

    [Fact]
    public void Next_ChainedRelativeTargets_ResolveAgainstEachLinksDirectory()
    {
        // Arrange: link -> sub/middle.eds -> ../target.eds
        var root = Path.Combine(Path.GetTempPath(), "chain");
        var link = Path.Combine(root, "link.eds");

        // Act
        var middle = SymbolicLinkPath.Next(link, Path.Combine("sub", "middle.eds"));
        var target = SymbolicLinkPath.Next(middle, Path.Combine("..", "target.eds"));

        // Assert
        middle.Should().Be(Path.Combine(root, "sub", "middle.eds"));
        target.Should().Be(Path.Combine(root, "sub", "..", "target.eds"));
    }

    [Fact]
    public void Next_LinkWithoutDirectory_ReturnsRelativeTarget()
        => SymbolicLinkPath.Next("link.eds", "target.eds").Should().Be("target.eds");
}
