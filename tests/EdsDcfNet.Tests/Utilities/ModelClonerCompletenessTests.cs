namespace EdsDcfNet.Tests.Utilities;

using System.Reflection;
using EdsDcfNet;
using EdsDcfNet.Models;
using FluentAssertions;
using Xunit;

/// <summary>
/// Reflection-based completeness guard for <c>ModelCloner</c>: every public settable
/// property on cloned model types must be deep-copied.
/// </summary>
public class ModelClonerCompletenessTests
{
    private static readonly Type ModelClonerType =
        typeof(CanOpenFile).Assembly.GetType("EdsDcfNet.Utilities.ModelCloner", throwOnError: true)!;

    [Theory]
    [MemberData(nameof(ModelClonerTestCatalog.CloneMethods), MemberType = typeof(ModelClonerTestCatalog))]
    public void CloneMethod_PopulatedSource_ProducesDeepEqualDistinctClone(string cloneMethodName)
    {
        // Arrange
        var method = ModelClonerType.GetMethod(
            cloneMethodName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        method.Should().NotBeNull($"expected ModelCloner.{cloneMethodName} to exist");

        var seed = cloneMethodName.GetHashCode(StringComparison.Ordinal);
        var sourceArgument = ModelCloneSampleBuilder.CreateSourceArgument(method!.GetParameters()[0], ref seed);

        // Act
        var clone = method.Invoke(null, new[] { sourceArgument });

        // Assert
        ModelCloneDeepAssert.AssertDeepClone(sourceArgument, clone, cloneMethodName);
    }

    [Fact]
    public void CloneMethod_PopulatedCanOpenObject_FailsWhenPropertyNotCopied()
    {
        // Guard-rail: the completeness harness must detect a deliberate omission.
        var seed = 0;
        var source = (CanOpenObject)ModelCloneSampleBuilder.CreateSample(typeof(CanOpenObject), ref seed);
        var incompleteClone = (CanOpenObject)ModelCloneSampleBuilder.CreateSample(typeof(CanOpenObject), ref seed);
        incompleteClone.ParameterName = source.ParameterName;
        incompleteClone.Index = source.Index;

        var act = () => ModelCloneDeepAssert.AssertDeepClone(source, incompleteClone, "CanOpenObject");

        act.Should().Throw<Exception>();
    }
}
