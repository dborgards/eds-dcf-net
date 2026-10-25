namespace EdsDcfNet.Tests.Models;

using EdsDcfNet.Validation;

public class ValidationIssueTests
{
    [Fact]
    public void Constructors_WithoutSeverity_DefaultToError()
    {
        new ValidationIssue("Path", "Message").Severity.Should().Be(ValidationSeverity.Error);
        new ValidationIssue("Path", "Message", "CODE").Severity.Should().Be(ValidationSeverity.Error);
    }

    [Fact]
    public void Constructor_WithWarning_PreservesSeverityAndExistingProperties()
    {
        var issue = new ValidationIssue("Path", "Message", "CODE", ValidationSeverity.Warning);

        issue.Path.Should().Be("Path");
        issue.Message.Should().Be("Message");
        issue.Code.Should().Be("CODE");
        issue.Severity.Should().Be(ValidationSeverity.Warning);
        issue.ToString().Should().Be("Path: Message");
    }
}
