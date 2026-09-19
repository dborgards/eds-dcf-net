namespace EdsDcfNet.Tests.Integration;

using System.Text;
using EdsDcfNet;
using EdsDcfNet.Exceptions;
using FluentAssertions;
using Xunit;

/// <summary>
/// Guards the documented thread-safety contract of the format entry points:
/// concurrent read/write/validate calls through <see cref="CanOpenFile"/> are safe
/// because the singleton readers/writers are stateless and strict-mode state lives
/// in an <see cref="AsyncLocal{T}"/> scoped per call.
/// </summary>
public class ThreadSafetyTests
{
    private const int Concurrency = 32;
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
    /// continuations without leaking into interleaved concurrent reads.
    /// </summary>
    [Fact]
    public async Task StrictParsingScope_AsyncReads_DoNotLeakAcrossAwait()
    {
        var malformed = LoadEdsWithDuplicateKey();
        using var start = new Barrier(Concurrency);

        var reads = Enumerable.Range(0, Concurrency).Select(i => Task.Run(async () =>
        {
            var strict = i % 2 == 0;
            var options = new CanOpenFileOptions { StrictParsing = strict };
            start.SignalAndWait();

            for (var iteration = 0; iteration < IterationsPerTask; iteration++)
            {
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(malformed));
                if (strict)
                {
                    await FluentActions.Awaiting(() => CanOpenFile.Eds.ReadStreamAsync(stream, options))
                        .Should().ThrowAsync<EdsParseException>();
                }
                else
                {
                    var model = await CanOpenFile.Eds.ReadStreamAsync(stream, options);
                    model.FileInfo.FileName.Should().Be("duplicate-b.eds");
                }
            }
        })).ToArray();

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
}
