namespace EdsDcfNet.Parsers;

using System.Globalization;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Utilities;

/// <summary>
/// Parses numeric INI keys that must not abort a lenient EDS/DCF read (#557).
/// Strict mode rethrows <see cref="EdsParseException"/> with the original converter
/// message plus section, key line, and diagnostic <see cref="EdsParseException.Code"/>.
/// </summary>
internal static class LenientIniNumber
{
    internal const string TreatAsVar = "Treated as VAR (0x7).";
    internal const string TreatAsZero = "Treated as 0.";
    internal const string LeaveUnset = "The value is left unset.";
    internal const string SkipEntry = "The entry is skipped.";

    internal static byte ParseByte(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string keyName,
        string rawValue,
        byte fallback,
        string code,
        string? coercedTo,
        string fallbackDescription)
        => Parse(
            sections,
            sectionName,
            keyName,
            rawValue,
            ValueConverter.ParseByte,
            fallback,
            code,
            coercedTo,
            fallbackDescription);

    internal static ushort ParseUInt16(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string keyName,
        string rawValue,
        ushort fallback,
        string code,
        string? coercedTo,
        string fallbackDescription)
        => Parse(
            sections,
            sectionName,
            keyName,
            rawValue,
            ValueConverter.ParseUInt16,
            fallback,
            code,
            coercedTo,
            fallbackDescription);

    internal static uint ParseUInt32(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string keyName,
        string rawValue,
        uint fallback,
        string code,
        string? coercedTo,
        string fallbackDescription)
        => Parse(
            sections,
            sectionName,
            keyName,
            rawValue,
            static value => ValueConverter.ParseInteger(value),
            fallback,
            code,
            coercedTo,
            fallbackDescription);

    /// <summary>
    /// Parses a present numeric key. Lenient failure returns <see langword="null"/>
    /// (the same result as an absent key). Callers must skip empty values themselves:
    /// <see cref="ValueConverter"/> maps empty input to <c>0</c>, which is a real value.
    /// </summary>
    internal static byte? ParseOptionalByte(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string keyName,
        string rawValue,
        string code,
        string fallbackDescription)
        => ParseOptional(
            sections,
            sectionName,
            keyName,
            rawValue,
            ValueConverter.ParseByte,
            code,
            fallbackDescription);

    /// <inheritdoc cref="ParseOptionalByte"/>
    internal static ushort? ParseOptionalUInt16(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string keyName,
        string rawValue,
        string code,
        string fallbackDescription)
        => ParseOptional(
            sections,
            sectionName,
            keyName,
            rawValue,
            ValueConverter.ParseUInt16,
            code,
            fallbackDescription);

    /// <summary>
    /// Parses an object-list index. Lenient failure reports <paramref name="code"/>
    /// and returns <see langword="false"/> so the caller can skip that entry and continue.
    /// </summary>
    internal static bool TryParseUInt16(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string keyName,
        string rawValue,
        string code,
        out ushort value)
    {
        try
        {
            value = ValueConverter.ParseUInt16(rawValue);
            return true;
        }
        catch (EdsParseException) when (!StrictParsingScope.IsEnabled)
        {
            Report(sections, sectionName, keyName, rawValue, code, coercedTo: null, SkipEntry);
            value = 0;
            return false;
        }
        catch (EdsParseException ex)
        {
            throw Fail(sections, sectionName, keyName, code, ex);
        }
    }

    /// <summary>
    /// Reads a counted object-index list (<c>SupportedObjects</c>, <c>ObjectLinks</c>,
    /// module <c>NrOfEntries</c>). A malformed count is <c>0</c> (the absent-key default).
    /// A malformed index is skipped; later indexes in the same list are still read.
    /// </summary>
    internal static void AppendIndexes(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string countKey,
        ICollection<ushort> target)
    {
        var rawCount = IniParser.GetValue(sections, sectionName, countKey, "0");
        var count = ParseUInt16(
            sections,
            sectionName,
            countKey,
            rawCount,
            fallback: 0,
            code: Diagnostics.ParseDiagnosticCodes.InvalidObjectListCount,
            coercedTo: "0",
            fallbackDescription: TreatAsZero);

        for (var i = 1; i <= count; i++)
        {
            var key = i.ToString(CultureInfo.InvariantCulture);
            var raw = IniParser.GetValue(sections, sectionName, key);
            if (string.IsNullOrEmpty(raw))
                continue;

            if (TryParseUInt16(
                    sections,
                    sectionName,
                    key,
                    raw,
                    Diagnostics.ParseDiagnosticCodes.InvalidObjectIndex,
                    out var index))
            {
                target.Add(index);
            }
        }
    }

    private static T Parse<T>(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string keyName,
        string rawValue,
        Func<string, T> parse,
        T fallback,
        string code,
        string? coercedTo,
        string fallbackDescription)
    {
        try
        {
            return parse(rawValue);
        }
        catch (EdsParseException) when (!StrictParsingScope.IsEnabled)
        {
            Report(sections, sectionName, keyName, rawValue, code, coercedTo, fallbackDescription);
            return fallback;
        }
        catch (EdsParseException ex)
        {
            throw Fail(sections, sectionName, keyName, code, ex);
        }
    }

    private static T? ParseOptional<T>(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string keyName,
        string rawValue,
        Func<string, T> parse,
        string code,
        string fallbackDescription)
        where T : struct
    {
        try
        {
            return parse(rawValue);
        }
        catch (EdsParseException) when (!StrictParsingScope.IsEnabled)
        {
            Report(sections, sectionName, keyName, rawValue, code, coercedTo: null, fallbackDescription);
            return null;
        }
        catch (EdsParseException ex)
        {
            throw Fail(sections, sectionName, keyName, code, ex);
        }
    }

    private static void Report(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string keyName,
        string rawValue,
        string code,
        string? coercedTo,
        string fallbackDescription)
    {
        Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
            Diagnostics.ParseSeverity.Warning,
            code,
            path: sectionName + "." + keyName,
            line: IniKeyLines.TryGetLine(sections, sectionName, keyName),
            rawValue: rawValue,
            coercedTo: coercedTo,
            message: string.Format(
                CultureInfo.InvariantCulture,
                "Invalid value '{0}' for key '{1}' in section '{2}'. {3}",
                rawValue,
                keyName,
                sectionName,
                fallbackDescription)));
    }

    private static EdsParseException Fail(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string keyName,
        string code,
        EdsParseException ex)
    {
        // Keep the ValueConverter message (strict mode still throws as before) and
        // attach the same code plus section/line that lenient mode reports.
        return new EdsParseException(ex.Message, ex)
        {
            Code = code,
            SectionName = sectionName,
            LineNumber = IniKeyLines.TryGetLine(sections, sectionName, keyName)
        };
    }
}
