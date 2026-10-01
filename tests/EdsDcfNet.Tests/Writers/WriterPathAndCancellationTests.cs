namespace EdsDcfNet.Tests.Writers;

using EdsDcfNet.Parsers;
using EdsDcfNet.Utilities;
using EdsDcfNet.Writers;

/// <summary>Disables parallelization for tests that change the process-wide current directory.</summary>
[CollectionDefinition("CurrentDirectory", DisableParallelization = true)]
public sealed class CurrentDirectoryCollection;

[Collection("CurrentDirectory")]
public sealed class WriterPathAndCancellationTests : IDisposable
{
    private readonly string _originalDirectory = Environment.CurrentDirectory;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"pathcancel-{Guid.NewGuid():N}");

    public WriterPathAndCancellationTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        Environment.CurrentDirectory = _originalDirectory;
        Directory.Delete(_root, recursive: true);
    }

    private string MakeDir(string name)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        return Path.GetFullPath(dir);
    }

    [Fact]
    public void WriteFileAtomic_RelativePathAndCurrentDirectoryChangesWhileWriting_CommitsToOriginalDirectory()
    {
        // Arrange
        var first = MakeDir("first");
        var second = MakeDir("second");
        Environment.CurrentDirectory = first;

        // Act
        TextFileIo.WriteFileAtomic("out.txt", s =>
        {
            s.Write(new byte[] { 1 }, 0, 1);
            Environment.CurrentDirectory = second;
        });

        // Assert
        File.Exists(Path.Combine(first, "out.txt")).Should().BeTrue();
        Directory.GetFileSystemEntries(second).Should().BeEmpty();
        Directory.GetFileSystemEntries(first).Should().ContainSingle();
    }

    [Fact]
    public async Task WriteFileAtomicAsync_RelativePathAndCurrentDirectoryChangesWhileWriting_CommitsToOriginalDirectory()
    {
        // Arrange
        var first = MakeDir("first");
        var second = MakeDir("second");
        Environment.CurrentDirectory = first;
        File.WriteAllText(Path.Combine(first, "out.txt"), "old");

        // Act
        await TextFileIo.WriteFileAtomicAsync("out.txt", async s =>
        {
            await s.WriteAsync(new byte[] { 1 }, 0, 1);
            Environment.CurrentDirectory = second;
        });

        // Assert
        File.ReadAllBytes(Path.Combine(first, "out.txt")).Should().Equal(1);
        Directory.GetFileSystemEntries(second).Should().BeEmpty();
        Directory.GetFileSystemEntries(first).Should().ContainSingle();
    }

    [Fact]
    public void WriteFileAtomic_PathWithoutFileName_ThrowsArgumentException()
    {
        // Arrange
        var root = Path.GetPathRoot(_root)!;

        // Act
        var act = () => TextFileIo.WriteFileAtomic(root, _ => { });

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task XddWriteStreamAsync_CancelledDuringFinalFlush_ThrowsOperationCanceled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var stream = new CancelOnFlushStream(cts);
        var eds = new EdsReader().ReadFile(Path.Combine("Fixtures", "sample_device.eds"));

        // Act
        var act = () => new XddWriter().WriteStreamAsync(eds, stream, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        stream.FlushRequested.Should().BeTrue();
    }

    [Fact]
    public async Task XdcWriteStreamAsync_CancelledDuringFinalFlush_ThrowsOperationCanceled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var stream = new CancelOnFlushStream(cts);
        var dcf = new DcfReader().ReadFile(Path.Combine("Fixtures", "minimal.dcf"));

        // Act
        var act = () => new XdcWriter().WriteStreamAsync(dcf, stream, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        stream.FlushRequested.Should().BeTrue();
    }

    [Fact]
    public async Task XddWriteStreamAsync_InvalidXmlCharacter_ThrowsDocumentSectionException()
    {
        // Arrange
        var eds = new EdsReader().ReadFile(Path.Combine("Fixtures", "sample_device.eds"));
        eds.DeviceInfo.ProductName = "bad \u0001 char";
        using var stream = new MemoryStream();

        // Act
        var act = () => new XddWriter().WriteStreamAsync(eds, stream);

        // Assert
        var ex = (await act.Should().ThrowAsync<EdsDcfNet.Exceptions.XddWriteException>()).Which;
        ex.SectionName.Should().Be("Document");
    }

    [Fact]
    public async Task XdcWriteStream_InvalidXmlCharacter_ThrowsDocumentSectionException()
    {
        // Arrange
        var dcf = new DcfReader().ReadFile(Path.Combine("Fixtures", "minimal.dcf"));
        dcf.DeviceInfo.ProductName = "bad \u0001 char";
        using var syncStream = new MemoryStream();
        using var asyncStream = new MemoryStream();

        // Act
        var sync = () => new XdcWriter().WriteStream(dcf, syncStream);
        var async = () => new XdcWriter().WriteStreamAsync(dcf, asyncStream);

        // Assert
        sync.Should().Throw<EdsDcfNet.Exceptions.XdcWriteException>().Which.SectionName.Should().Be("Document");
        (await async.Should().ThrowAsync<EdsDcfNet.Exceptions.XdcWriteException>()).Which.SectionName.Should().Be("Document");
    }

    private sealed class CancelOnFlushStream : MemoryStream
    {
        private readonly CancellationTokenSource _cts;

        public CancelOnFlushStream(CancellationTokenSource cts) => _cts = cts;

        public bool FlushRequested { get; private set; }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            FlushRequested = true;
            _cts.Cancel();
            return Task.CompletedTask;
        }
    }
}
