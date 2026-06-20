namespace EdsDcfNet.Exceptions;

using System.Collections.ObjectModel;
using System.Globalization;
using EdsDcfNet.Validation;

/// <summary>
/// Exception thrown when a model fails validation before a write operation.
/// </summary>
public sealed class ModelValidationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ModelValidationException"/> class.
    /// </summary>
    /// <param name="issues">Validation issues that blocked the write operation.</param>
    public ModelValidationException(IReadOnlyList<ValidationIssue> issues)
        : base(BuildMessage(issues))
    {
        Issues = new ReadOnlyCollection<ValidationIssue>(issues.ToList());
    }

    /// <summary>
    /// Gets the validation issues that blocked the write operation.
    /// </summary>
    public IReadOnlyList<ValidationIssue> Issues { get; }

    private static string BuildMessage(IReadOnlyList<ValidationIssue> issues)
    {
        if (issues.Count == 0)
            return "Model validation failed.";

        if (issues.Count == 1)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Model validation failed: {0}",
                issues[0]);
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "Model validation failed with {0} issue(s). First issue: {1}",
            issues.Count,
            issues[0]);
    }
}
