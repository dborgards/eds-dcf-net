namespace EdsDcfNet.Tests;

using EdsDcfNet;

public class CanOpenNodeIdTests
{
    [Fact]
    public void MinValue_Is1()
    {
        CanOpenNodeId.MinValue.Should().Be(1);
    }

    [Fact]
    public void MaxValue_Is127()
    {
        CanOpenNodeId.MaxValue.Should().Be(127);
    }

    [Fact]
    public void RangeDescription_MatchesMinAndMax()
    {
        CanOpenNodeId.RangeDescription.Should().Be("1..127");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(64)]
    [InlineData(126)]
    [InlineData(127)]
    public void IsInRange_ValidNodeIds_ReturnsTrue(int nodeId)
    {
        CanOpenNodeId.IsInRange(nodeId).Should().BeTrue();
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(128)]
    [InlineData(255)]
    [InlineData(int.MaxValue)]
    public void IsInRange_InvalidNodeIds_ReturnsFalse(int nodeId)
    {
        CanOpenNodeId.IsInRange(nodeId).Should().BeFalse();
    }
}
