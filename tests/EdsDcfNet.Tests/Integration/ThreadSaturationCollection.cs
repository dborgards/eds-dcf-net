namespace EdsDcfNet.Tests.Integration;

using Xunit;

/// <summary>
/// Serializes tests that deliberately saturate the thread pool (barriers,
/// gated streams, dozens of concurrent tasks). Run in parallel — xUnit's
/// default across test classes — they starve timing-sensitive neighbours on
/// the small CI runners.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ThreadSaturationCollection
{
    public const string Name = "ThreadSaturation";
}
