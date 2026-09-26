namespace EdsDcfNet.Checker;

using EdsDcfNet.Diagnostics;
using EdsDcfNet.Validation;

/// <summary>
/// Runs the EdsDcfNet reader and <see cref="CanOpenModelValidator"/> on a file and converts
/// their output into findings. This shows whether the library itself can load the file.
/// </summary>
public static class LibraryChecker
{
    public static void Run(string file, bool isDcf, List<Finding> findings)
    {
        IReadOnlyList<ParseDiagnostic> diagnostics;
        IReadOnlyList<ValidationIssue> issues;

        try
        {
            if (isDcf)
            {
                var result = CanOpenFile.Dcf.ReadFileWithDiagnostics(file);
                diagnostics = result.Diagnostics;
                issues = CanOpenModelValidator.Validate(result.Model);
            }
            else
            {
                var result = CanOpenFile.Eds.ReadFileWithDiagnostics(file);
                diagnostics = result.Diagnostics;
                issues = CanOpenModelValidator.Validate(result.Model);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            findings.Add(new Finding(Severity.Error, "LIB002", file, null, null, null, null,
                "EdsDcfNet reader aborted: " + ex.GetType().Name + ": " + ex.Message));
            return;
        }

        foreach (var diagnostic in diagnostics)
        {
            findings.Add(new Finding(
                diagnostic.Severity == ParseSeverity.Error ? Severity.Error : Severity.Warning,
                "LIB001",
                file,
                diagnostic.Line,
                null,
                null,
                diagnostic.RawValue,
                diagnostic.Code + " at " + diagnostic.Path + ": " + diagnostic.Message));
        }

        foreach (var issue in issues)
        {
            // Object dictionary structure is already covered in more detail (with line numbers)
            // by RawObjectChecker; only report the remaining model-level issues here.
            if (issue.Path.StartsWith("ObjectDictionary.", StringComparison.Ordinal))
            {
                continue;
            }

            findings.Add(new Finding(Severity.Error, "LIB003", file, null, null, null, null,
                issue.Path + ": " + issue.Message));
        }
    }
}
