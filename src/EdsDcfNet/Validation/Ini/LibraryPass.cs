namespace EdsDcfNet.Validation;

using EdsDcfNet.Diagnostics;
using EdsDcfNet.Models;
using Finding = EdsDcfNet.Validation.IniValidationIssue;
using Severity = EdsDcfNet.Validation.IniValidationSeverity;

/// <summary>
/// Loads the source with the EdsDcfNet reader and reports its diagnostics and the model
/// validation issues as <see cref="IniValidationIssue"/>s, keeping their own codes.
/// </summary>
internal static class LibraryPass
{
    internal const string ReaderFallbackCode = "LIB001";
    internal const string ReaderAbortCode = "LIB002";
    internal const string ModelFallbackCode = "LIB003";

    private const string ObjectDictionaryPathPrefix = "ObjectDictionary.";

    internal static void Run(
        Func<(IReadOnlyList<ParseDiagnostic> Diagnostics, Func<IReadOnlyList<ValidationIssue>> Validate)> read,
        string sourceName,
        CanOpenIniValidationOptions options,
        List<Finding> findings)
    {
        IReadOnlyList<ParseDiagnostic> diagnostics;
        IReadOnlyList<ValidationIssue> issues;
        try
        {
            var result = read();
            diagnostics = result.Diagnostics;
            issues = options.IncludeModelValidation ? result.Validate() : Array.Empty<ValidationIssue>();
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            findings.Add(new Finding(Severity.Error, ReaderAbortCode, sourceName, null, null, null, null,
                "EdsDcfNet reader aborted: " + ex.GetType().Name + ": " + ex.Message));
            return;
        }

        if (options.IncludeReaderDiagnostics)
        {
            foreach (var diagnostic in diagnostics)
            {
                findings.Add(FromDiagnostic(diagnostic, sourceName));
            }
        }

        foreach (var issue in issues)
        {
            if (issue.Path.StartsWith(ObjectDictionaryPathPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            findings.Add(new Finding(
                issue.Severity == ValidationSeverity.Error ? Severity.Error : Severity.Warning,
                string.IsNullOrEmpty(issue.Code) ? ModelFallbackCode : issue.Code!,
                sourceName,
                null,
                null,
                null,
                null,
                issue.Path + ": " + issue.Message));
        }
    }

    internal static (IReadOnlyList<ParseDiagnostic>, Func<IReadOnlyList<ValidationIssue>>) Eds(
        CanOpenReadResult<ElectronicDataSheet> result) =>
        (result.Diagnostics, () => CanOpenModelValidator.Validate(result.Model));

    internal static (IReadOnlyList<ParseDiagnostic>, Func<IReadOnlyList<ValidationIssue>>) Dcf(
        CanOpenReadResult<DeviceConfigurationFile> result) =>
        (result.Diagnostics, () => CanOpenModelValidator.Validate(result.Model));

    private static Finding FromDiagnostic(ParseDiagnostic diagnostic, string sourceName)
    {
        string? section = null;
        string? key = null;
        if (diagnostic.Path.Length > 0)
        {
            var dot = diagnostic.Path.IndexOf('.');
            section = dot < 0 ? diagnostic.Path : diagnostic.Path[..dot];
            key = dot < 0 ? null : diagnostic.Path[(dot + 1)..];
        }

        return new Finding(
            diagnostic.Severity == ParseSeverity.Error ? Severity.Error : Severity.Warning,
            string.IsNullOrEmpty(diagnostic.Code) ? ReaderFallbackCode : diagnostic.Code,
            sourceName,
            diagnostic.Line,
            section,
            key,
            key is null ? null : diagnostic.RawValue,
            diagnostic.Message);
    }
}
