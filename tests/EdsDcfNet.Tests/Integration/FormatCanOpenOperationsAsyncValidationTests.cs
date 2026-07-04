namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;

public class FormatCanOpenOperationsAsyncValidationTests
{
    [Fact]
    public async Task SyncOnlySubclass_WriteFileAsync_WithValidatedOptions_UsesSyncValidationFallback()
    {
        var tracker = new SyncValidationTracker();
        var operations = new SyncOnlyFormatOperations(tracker);
        var eds = CreateValidEds();
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            await operations.WriteFileAsync(eds, tempFile, CanOpenWriteOptions.Validated);

            tracker.Called.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task SyncOnlySubclass_WriteStreamAsync_WithValidatedOptions_UsesSyncValidationFallback()
    {
        var tracker = new SyncValidationTracker();
        var operations = new SyncOnlyFormatOperations(tracker);
        var eds = CreateValidEds();
        using var stream = new MemoryStream();

        await operations.WriteStreamAsync(eds, stream, CanOpenWriteOptions.Validated);

        tracker.Called.Should().BeTrue();
        stream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SyncOnlySubclass_WriteFileAsync_WithDefaultOptions_SkipsValidation()
    {
        var tracker = new SyncValidationTracker();
        var operations = new SyncOnlyFormatOperations(tracker);
        var eds = CreateInvalidEds();
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            await operations.WriteFileAsync(eds, tempFile, CanOpenWriteOptions.Default);

            tracker.Called.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task SyncOnlySubclass_WriteStreamAsync_WithDefaultOptions_SkipsValidation()
    {
        var tracker = new SyncValidationTracker();
        var operations = new SyncOnlyFormatOperations(tracker);
        var eds = CreateInvalidEds();
        using var stream = new MemoryStream();

        await operations.WriteStreamAsync(eds, stream, CanOpenWriteOptions.Default);

        tracker.Called.Should().BeFalse();
        stream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SyncOnlySubclass_WriteFileAsync_WithValidatedOptions_ThrowsWhenSyncValidationFails()
    {
        var tracker = new SyncValidationTracker();
        var operations = new SyncOnlyFormatOperations(tracker);
        var eds = CreateInvalidEds();
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            var act = () => operations.WriteFileAsync(eds, tempFile, CanOpenWriteOptions.Validated);

            await act.Should().ThrowAsync<ModelValidationException>();
            tracker.Called.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task AsyncDelegateSubclass_WriteFileAsync_WithValidatedOptions_UsesInjectedAsyncValidation()
    {
        var tracker = new AsyncValidationTracker();
        var operations = new AsyncDelegateFormatOperations(tracker);
        var eds = CreateValidEds();
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            await operations.WriteFileAsync(eds, tempFile, CanOpenWriteOptions.Validated);

            tracker.AsyncCalled.Should().BeTrue();
            tracker.SyncCalled.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task AsyncDelegateSubclass_WriteStreamAsync_WithValidatedOptions_UsesInjectedAsyncValidation()
    {
        var tracker = new AsyncValidationTracker();
        var operations = new AsyncDelegateFormatOperations(tracker);
        var eds = CreateValidEds();
        using var stream = new MemoryStream();

        await operations.WriteStreamAsync(eds, stream, CanOpenWriteOptions.Validated);

        tracker.AsyncCalled.Should().BeTrue();
        tracker.SyncCalled.Should().BeFalse();
        stream.Length.Should().BeGreaterThan(0);
    }

    private static ElectronicDataSheet CreateValidEds()
    {
        var eds = new ElectronicDataSheet();
        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly
        };

        return eds;
    }

    private static ElectronicDataSheet CreateInvalidEds()
    {
        var eds = new ElectronicDataSheet();
        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);
        return eds;
    }

    private sealed class SyncValidationTracker
    {
        public bool Called { get; set; }
    }

    private sealed class AsyncValidationTracker
    {
        public bool SyncCalled { get; set; }
        public bool AsyncCalled { get; set; }
    }

    private static class EdsFormatDelegates
    {
        internal static ElectronicDataSheet ReadFile(string filePath, long maxInputSize) =>
            CanOpenFile.ReadEds(filePath, maxInputSize);

        internal static Task<ElectronicDataSheet> ReadFileAsync(
            string filePath,
            long maxInputSize,
            CancellationToken cancellationToken) =>
            CanOpenFile.ReadEdsAsync(filePath, maxInputSize, cancellationToken);

        internal static ElectronicDataSheet ReadString(string content, long maxInputSize) =>
            CanOpenFile.ReadEdsFromString(content, maxInputSize);

        internal static ElectronicDataSheet ReadStream(Stream stream, long maxInputSize) =>
            CanOpenFile.ReadEds(stream, maxInputSize);

        internal static Task<ElectronicDataSheet> ReadStreamAsync(
            Stream stream,
            long maxInputSize,
            CancellationToken cancellationToken) =>
            CanOpenFile.ReadEdsAsync(stream, maxInputSize, cancellationToken);

        internal static void WriteFile(ElectronicDataSheet model, string filePath) =>
            CanOpenFile.WriteEds(model, filePath);

        internal static void WriteStream(ElectronicDataSheet model, Stream stream) =>
            CanOpenFile.WriteEds(model, stream);

        internal static Task WriteFileAsync(
            ElectronicDataSheet model,
            string filePath,
            CancellationToken cancellationToken) =>
            CanOpenFile.WriteEdsAsync(model, filePath, cancellationToken);

        internal static Task WriteStreamAsync(
            ElectronicDataSheet model,
            Stream stream,
            CancellationToken cancellationToken) =>
            CanOpenFile.WriteEdsAsync(model, stream, cancellationToken);

        internal static string WriteToString(ElectronicDataSheet model) =>
            CanOpenFile.WriteEdsToString(model);
    }

    /// <summary>
    /// Uses the 11-argument protected constructor (no async validation delegate).
    /// </summary>
    private sealed class SyncOnlyFormatOperations : FormatCanOpenOperations<ElectronicDataSheet>
    {
        internal SyncOnlyFormatOperations(SyncValidationTracker tracker)
            : base(
                (model, options) => RunSyncValidation(model, options, tracker),
                EdsFormatDelegates.ReadFile,
                EdsFormatDelegates.ReadFileAsync,
                EdsFormatDelegates.ReadString,
                EdsFormatDelegates.ReadStream,
                EdsFormatDelegates.ReadStreamAsync,
                EdsFormatDelegates.WriteFile,
                EdsFormatDelegates.WriteStream,
                EdsFormatDelegates.WriteFileAsync,
                EdsFormatDelegates.WriteStreamAsync,
                EdsFormatDelegates.WriteToString)
        {
        }

        private static void RunSyncValidation(
            ElectronicDataSheet model,
            CanOpenWriteOptions? options,
            SyncValidationTracker tracker)
        {
            if (options?.ValidateBeforeWrite != true)
                return;

            tracker.Called = true;
            CanOpenFile.EnsureValid(model);
        }
    }

    /// <summary>
    /// Uses the 12-argument protected constructor with a custom async validation delegate.
    /// </summary>
    private sealed class AsyncDelegateFormatOperations : FormatCanOpenOperations<ElectronicDataSheet>
    {
        internal AsyncDelegateFormatOperations(AsyncValidationTracker tracker)
            : base(
                (_, options) =>
                {
                    if (options?.ValidateBeforeWrite == true)
                        tracker.SyncCalled = true;
                },
                EdsFormatDelegates.ReadFile,
                EdsFormatDelegates.ReadFileAsync,
                EdsFormatDelegates.ReadString,
                EdsFormatDelegates.ReadStream,
                EdsFormatDelegates.ReadStreamAsync,
                EdsFormatDelegates.WriteFile,
                EdsFormatDelegates.WriteStream,
                EdsFormatDelegates.WriteFileAsync,
                EdsFormatDelegates.WriteStreamAsync,
                EdsFormatDelegates.WriteToString,
                (model, options, cancellationToken) =>
                    RunAsyncValidation(model, options, cancellationToken, tracker))
        {
        }

        private static Task RunAsyncValidation(
            ElectronicDataSheet model,
            CanOpenWriteOptions? options,
            CancellationToken cancellationToken,
            AsyncValidationTracker tracker)
        {
            if (options?.ValidateBeforeWrite == true)
                tracker.AsyncCalled = true;

            return CanOpenFile.EnsureValidAsync(model, cancellationToken);
        }
    }
}
