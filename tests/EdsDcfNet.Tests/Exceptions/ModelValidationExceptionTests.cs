namespace EdsDcfNet.Tests.Exceptions;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Validation;

public class ModelValidationExceptionTests
{
    [Fact]
    public void Constructor_WithSingleIssue_FormatsMessage()
    {
        var issues = new[] { new ValidationIssue("DeviceCommissioning.NodeId", "Invalid node id.") };

        var exception = new ModelValidationException(issues);

        exception.Message.Should().Contain("DeviceCommissioning.NodeId");
        exception.Message.Should().Be("Model validation failed: " + issues[0]);
        exception.Issues.Should().ContainSingle();
    }

    [Fact]
    public void Constructor_WithMultipleIssues_IncludesCountInMessage()
    {
        var issues = new[]
        {
            new ValidationIssue("A", "First"),
            new ValidationIssue("B", "Second")
        };

        var exception = new ModelValidationException(issues);

        exception.Message.Should().Contain("2 issue(s)");
        exception.Issues.Should().HaveCount(2);
    }

    [Fact]
    public void Constructor_WithNoIssues_UsesDefaultMessage()
    {
        var exception = new ModelValidationException(Array.Empty<ValidationIssue>());

        exception.Message.Should().Be("Model validation failed.");
        exception.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Issues_ReturnsSnapshotNotLiveList()
    {
        var issues = new List<ValidationIssue> { new("Path", "Message") };
        var exception = new ModelValidationException(issues);

        issues.Add(new ValidationIssue("Other", "Changed"));

        exception.Issues.Should().ContainSingle();
    }
}
