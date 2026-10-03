namespace EdsDcfNet.Validation;

using System.Globalization;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Writers;

/// <summary>
/// Opt-in checks of the plain-text rules of CiA 306-1 clause 6.2 for an EDS or DCF: ISO/IEC 646
/// characters (<see cref="CanOpenValidationOptions.RequireIso646"/>) and lines of at most 255
/// characters (<see cref="CanOpenValidationOptions.CheckLineLength"/>).
/// </summary>
/// <remarks>
/// Both rules describe the file, not the model: a line is <c>key=value</c> including the separator.
/// The check therefore runs on the text the INI writer produces for the model, which also covers
/// section names, keys, and kept entries without a second walk over every text field. A model the
/// writer rejects has no file text; the check then adds nothing, because the writer's own exception
/// is the finding.
/// </remarks>
internal static class IniFileTextRules
{
    private const int MaxLineLength = 255;
    private const char MaxIso646Character = '\u007F';

    internal static void Apply(
        ElectronicDataSheet eds,
        CanOpenValidationOptions options,
        List<ValidationIssue> issues,
        CancellationToken cancellationToken)
        => Apply(() => new EdsWriter().GenerateString(eds), options, issues, cancellationToken);

    internal static void Apply(
        DeviceConfigurationFile dcf,
        CanOpenValidationOptions options,
        List<ValidationIssue> issues,
        CancellationToken cancellationToken)
        => Apply(() => new DcfWriter().GenerateString(dcf), options, issues, cancellationToken);

    private static void Apply(
        Func<string> generate,
        CanOpenValidationOptions options,
        List<ValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        if (!options.RequireIso646 && !options.CheckLineLength)
            return;

        string text;
        try
        {
            text = generate();
        }
        catch (WriteException)
        {
            return;
        }

        var section = string.Empty;
        var lineNumber = 0;
        foreach (var rawLine in text.Split('\n'))
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNumber++;
            var line = rawLine.TrimEnd('\r');
            if (line.Length > 0 && line[0] == '[')
                section = line.Trim('[', ']');

            if (options.RequireIso646)
                CheckCharacters(line, lineNumber, section, issues);

            if (options.CheckLineLength && line.Length > MaxLineLength)
            {
                issues.Add(new ValidationIssue(
                    LinePath(lineNumber),
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Line {0} in section [{1}] has {2} characters; at most {3} are allowed (CiA 306-1 clause 6.2).",
                        lineNumber,
                        section,
                        line.Length,
                        MaxLineLength),
                    ValidationIssueCodes.LineTooLong));
            }
        }
    }

    private static void CheckCharacters(string line, int lineNumber, string section, List<ValidationIssue> issues)
    {
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] <= MaxIso646Character)
                continue;

            issues.Add(new ValidationIssue(
                LinePath(lineNumber),
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Line {0} in section [{1}] contains the character U+{2:X4}, which is outside ISO/IEC 646 (CiA 306-1 clause 6.2).",
                    lineNumber,
                    section,
                    (int)line[i]),
                ValidationIssueCodes.Iso646CharacterNotAllowed));
            return;
        }
    }

    private static string LinePath(int lineNumber)
        => string.Format(CultureInfo.InvariantCulture, "Line[{0}]", lineNumber);
}
