namespace EdsDcfNet.Tests.Integration;

using System.Text;
using EdsDcfNet;
using EdsDcfNet.Exceptions;
using FluentAssertions;
using Xunit;

/// <summary>
/// Guards the documented thread-safety contract of the format entry points:
/// concurrent read/write/validate calls through <see cref="CanOpenFile"/> are safe
/// because the singleton operation objects hold only immutable delegates, each call
/// constructs its own reader/writer, and strict-mode state lives in an
/// <see cref="AsyncLocal{T}"/> scoped per call.
/// </summary>
[Collection(ThreadSaturationCollection.Name)]
public class ThreadSafetyTests
{
    private const int Concurrency = 32;
    private const int AsyncConcurrency = 8;
    private const int IterationsPerTask = 5;

    [Fact]
    public async Task ParallelEdsReadWriteValidate_MixedStrictParsing_ProducesIdenticalResults()
    {
        var reference = CanOpenFile.Eds.ReadFile("Fixtures/sample_device.eds");
        var expectedObjectCount = reference.ObjectDictionary?.Objects.Count ?? 0;
        var expectedProductName = reference.DeviceInfo.ProductName;

        var tasks = Enumerable.Range(0, Concurrency).Select(i => Task.Run(() =>
        {
            var options = new CanOpenFileOptions { StrictParsing = i % 2 == 0 };

            for (var iteration = 0; iteration < IterationsPerTask; iteration++)
            {
                var model = CanOpenFile.Eds.ReadFile("Fixtures/sample_device.eds", options);
                model.DeviceInfo.ProductName.Should().Be(expectedProductName);
                (model.ObjectDictionary?.Objects.Count ?? 0).Should().Be(expectedObjectCount);

                var serialized = CanOpenFile.Eds.WriteToString(model);
                var roundTripped = CanOpenFile.Eds.ReadString(serialized, options);
                (roundTripped.ObjectDictionary?.Objects.Count ?? 0).Should().Be(expectedObjectCount);

                CanOpenFile.Validate(model);
            }
        })).ToArray();

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task ParallelXddReadWrite_MixedStrictParsing_ProducesIdenticalResults()
    {
        var reference = CanOpenFile.Xdd.ReadFile("Fixtures/sample_device.xdd");
        var expectedProductName = reference.DeviceInfo.ProductName;
        var expectedObjectCount = reference.ObjectDictionary?.Objects.Count ?? 0;

        var tasks = Enumerable.Range(0, Concurrency).Select(i => Task.Run(() =>
        {
            var options = new CanOpenFileOptions { StrictParsing = i % 2 == 1 };

            for (var iteration = 0; iteration < IterationsPerTask; iteration++)
            {
                var model = CanOpenFile.Xdd.ReadFile("Fixtures/sample_device.xdd", options);
                model.DeviceInfo.ProductName.Should().Be(expectedProductName);
                (model.ObjectDictionary?.Objects.Count ?? 0).Should().Be(expectedObjectCount);

                var serialized = CanOpenFile.Xdd.WriteToString(model);
                var roundTripped = CanOpenFile.Xdd.ReadString(serialized, options);
                (roundTripped.ObjectDictionary?.Objects.Count ?? 0).Should().Be(expectedObjectCount);
            }
        })).ToArray();

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Uses strict-sensitive input: a duplicate key inside an INI section is coerced
    /// (last write wins) in lenient mode but throws in strict mode. If
    /// <c>StrictParsingScope</c> leaked or were a shared global flag, strict tasks
    /// would randomly succeed or lenient tasks would randomly throw. A
    /// <see cref="Barrier"/> maximizes the time overlap of the active scopes.
    /// </summary>
    [Fact]
    public async Task StrictParsingScope_DoesNotLeakAcrossConcurrentCalls()
    {
        var malformed = LoadEdsWithDuplicateKey();
        using var start = new Barrier(Concurrency);

        var tasks = Enumerable.Range(0, Concurrency).Select(i => Task.Run(() =>
        {
            var strict = i % 2 == 0;
            var options = new CanOpenFileOptions { StrictParsing = strict };
            start.SignalAndWait();

            for (var iteration = 0; iteration < IterationsPerTask; iteration++)
            {
                if (strict)
                {
                    FluentActions.Invoking(() => CanOpenFile.Eds.ReadString(malformed, options))
                        .Should().Throw<EdsParseException>(
                            "strict mode must reject the duplicate key — a leaked lenient scope would swallow it");
                }
                else
                {
                    var model = CanOpenFile.Eds.ReadString(malformed, options);
                    model.FileInfo.FileName.Should().Be("duplicate-b.eds",
                        "lenient mode applies last-write-wins — a leaked strict scope would throw");
                }
            }
        })).ToArray();

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Async variant of the isolation guard: scopes must survive <c>await</c>
    /// continuations without leaking into interleaved concurrent reads. Every read
    /// parks on a shared gate (<see cref="GatedReadStream"/>) until the test
    /// releases it from its own thread; because the gate completes continuations
    /// inline on the releasing thread, each parser continuation deterministically
    /// resumes on a thread other than the one that entered the strict scope — a
    /// thread-local regression would lose the scope right there. Neither
    /// <c>MemoryStream</c> (completes synchronously) nor <c>Task.Yield</c> (may
    /// resume on the same pool thread) can guard that contract.
    /// </summary>
    [Fact]
    public async Task StrictParsingScope_AsyncReads_DoNotLeakAcrossAwait()
    {
        var malformed = LoadEdsWithDuplicateKey();
        using var start = new Barrier(AsyncConcurrency);
        var gate = new TaskCompletionSource<object?>();
        var allParked = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var parked = 0;

        void OnFirstRead()
        {
            if (Interlocked.Increment(ref parked) == AsyncConcurrency)
                allParked.TrySetResult(null);
        }

        var reads = Enumerable.Range(0, AsyncConcurrency).Select(i => Task.Run(async () =>
        {
            var strict = i % 2 == 0;
            var options = new CanOpenFileOptions { StrictParsing = strict };
            start.SignalAndWait();

            using var stream = new GatedReadStream(
                Encoding.UTF8.GetBytes(malformed), gate.Task, OnFirstRead);
            if (strict)
            {
                await FluentActions.Awaiting(() => CanOpenFile.Eds.ReadStreamAsync(stream, options))
                    .Should().ThrowAsync<EdsParseException>(
                        "strict mode must survive the cross-thread continuation — " +
                        "thread-local state is lost when the continuation resumes on another thread");
            }
            else
            {
                var model = await CanOpenFile.Eds.ReadStreamAsync(stream, options);
                model.FileInfo.FileName.Should().Be("duplicate-b.eds");
            }
        })).ToArray();

        // Every task is parked at its stream's first read; releasing the gate from
        // this (test) thread resumes all parser continuations here — deterministically
        // not on the threads that entered the strict scopes.
        await allParked.Task;
        gate.TrySetResult(null);
        await Task.WhenAll(reads);
    }

    /// <summary>
    /// Loads the well-formed EDS fixture and injects a duplicate <c>FileName</c> key
    /// into <c>[FileInfo]</c>: lenient mode keeps the last value, strict mode throws.
    /// </summary>
    private static string LoadEdsWithDuplicateKey()
    {
        var content = File.ReadAllText("Fixtures/sample_device.eds");
        var match = System.Text.RegularExpressions.Regex.Match(
            content, "(?m)^FileName=.+$");
        match.Success.Should().BeTrue("the fixture must contain a FileName key");
        // Inject AFTER the original line: lenient duplicate handling is
        // last-write-wins, so the injected value must come last to be observable.
        return content.Insert(
            match.Index + match.Length,
            "\nFileName=duplicate-b.eds");
    }

    /// <summary>
    /// Read-only stream whose first read parks on a shared gate until the test
    /// releases it from a different thread. The gate is a plain
    /// <see cref="TaskCompletionSource{TResult}"/>, so the parked continuation
    /// resumes inline on the releasing thread — deterministically a different
    /// thread than the one that entered the strict scope, which is what a
    /// thread-local regression cannot survive. Only the array-based
    /// <c>ReadAsync</c> overload is overridden; the base class routes the
    /// <c>Memory&lt;byte&gt;</c> overload through it.
    /// </summary>
    private sealed class GatedReadStream : Stream
    {
        private readonly byte[] _payload;
        private readonly Task _gate;
        private readonly Action _onFirstRead;
        private int _position;
        private bool _firstRead = true;

        public GatedReadStream(byte[] payload, Task gate, Action onFirstRead)
        {
            _payload = payload;
            _gate = gate;
            _onFirstRead = onFirstRead;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            if (_firstRead)
            {
                _firstRead = false;
                _onFirstRead();
                // Resumes inline on the gate-releasing thread. ExecutionContext
                // (and therefore AsyncLocal) flows across this await; thread-local
                // state would be lost.
                await _gate.ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Read(buffer, offset, count);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = Math.Min(count, _payload.Length - _position);
            Array.Copy(_payload, _position, buffer, offset, read);
            _position += read;
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
