namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;
using EdsDcfNet.Models;
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
            // Alternate strict/lenient per task: with AsyncLocal scoping the strict
            // flag must not leak between concurrent calls.
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

    [Fact]
    public async Task ParallelAsyncReads_MixedStrictParsing_DoNotLeakScopeAcrossAwait()
    {
        var options = Enumerable.Range(0, Concurrency)
            .Select(i => new CanOpenFileOptions { StrictParsing = i % 2 == 0 })
            .ToArray();

        var reads = options.Select(o => Task.Run(async () =>
        {
            var model = await CanOpenFile.Eds.ReadFileAsync("Fixtures/sample_device.eds", o);
            // Yield so continuations interleave across AsyncLocal scopes.
            await Task.Yield();
            return CanOpenFile.Eds.WriteToString(model);
        })).ToArray();

        var results = await Task.WhenAll(reads);
        results.Distinct().Should().ContainSingle(
            "every concurrent read of the same fixture must serialize identically");
    }
}
