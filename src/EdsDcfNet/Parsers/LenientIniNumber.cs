namespace EdsDcfNet.Parsers;

using System.Globalization;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
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
            ParseObjFlagsInteger,
            fallback,
            code,
            coercedTo,
            fallbackDescription);

    /// <summary>Bits 0 and 1 are the only <c>ObjFlags</c> bits CiA 306-1 Table 8 defines.</summary>
    private const uint DefinedIniObjFlagsMask = 0x3;

    /// <summary>
    /// Parses an EDS/DCF <c>ObjFlags</c> entry: malformed values fall back to <c>0</c> as in
    /// <see cref="ParseUInt32"/>, and a readable value with reserved bits 2..31 set (CiA 306-1
    /// Table 8) is kept and reported as <see cref="Diagnostics.ParseDiagnosticCodes.IniObjFlagsReservedBits"/>
    /// in lenient and strict mode. CiA 311 also defines bit 2, which is why the XDD reader has its
    /// own limit.
    /// </summary>
    internal static uint ParseObjFlags(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string rawValue)
    {
        var flags = ParseUInt32(
            sections,
            sectionName,
            "ObjFlags",
            rawValue,
            fallback: 0,
            code: Diagnostics.ParseDiagnosticCodes.InvalidObjFlags,
            coercedTo: "0",
            fallbackDescription: TreatAsZero);

        if ((flags & ~DefinedIniObjFlagsMask) != 0)
        {
            Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
                Diagnostics.ParseSeverity.Warning,
                Diagnostics.ParseDiagnosticCodes.IniObjFlagsReservedBits,
                path: sectionName + ".ObjFlags",
                line: IniKeyLines.TryGetLine(sections, sectionName, "ObjFlags"),
                rawValue: rawValue,
                message: string.Format(
                    CultureInfo.InvariantCulture,
                    "ObjFlags '{0}' sets reserved bits 2..31. CiA 306-1 defines only bit 0 (refuse write on download) and bit 1 (refuse read on scan). The value is kept.",
                    rawValue)));
        }

        return flags;
    }

    /// <summary>
    /// Reports a counted object list whose count disagrees with its numbered entries: an entry above
    /// the count (kept in the remaining entries of the section, not loaded) or an empty or missing
    /// entry inside it (CiA 306-1 Table 5). A malformed count was already reported by
    /// <see cref="AppendIndexes"/>. One diagnostic per list, in lenient and strict mode.
    /// </summary>
    internal static void ReportCountMismatch(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string countKey)
    {
        var rawCount = IniParser.GetValue(sections, sectionName, countKey, "0");
        int count;
        try
        {
            count = ValueConverter.ParseUInt16(rawCount);
        }
        catch (EdsParseException)
        {
            return;
        }

        var above = new List<string>();
        foreach (var key in IniParser.GetKeys(sections, sectionName))
        {
            if (SectionEntryKeys.IsEntryNumberAbove(key, count))
                above.Add(key);
        }

        var missing = new List<string>();
        for (var i = 1; i <= count; i++)
        {
            var key = i.ToString(CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(IniParser.GetValue(sections, sectionName, key)))
                missing.Add(key);
        }

        if (above.Count == 0 && missing.Count == 0)
            return;

        var message = string.Format(
            CultureInfo.InvariantCulture,
            "{0} is {1} but the numbered entries do not match.",
            countKey,
            count);
        if (above.Count > 0)
        {
            message += " Entries above the count (" + Summarize(above)
                + ") are not loaded and are kept unchanged.";
        }

        if (missing.Count > 0)
            message += " Entries empty or missing inside the count (" + Summarize(missing) + ").";

        Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
            Diagnostics.ParseSeverity.Warning,
            Diagnostics.ParseDiagnosticCodes.IniObjectListCountMismatch,
            path: sectionName + "." + countKey,
            line: IniKeyLines.TryGetLine(sections, sectionName, countKey),
            rawValue: rawCount,
            message: message));
    }

    private static string Summarize(List<string> keys)
    {
        const int shown = 5;
        return keys.Count <= shown
            ? string.Join(", ", keys)
            : string.Join(", ", keys.Take(shown)) + ", and "
              + (keys.Count - shown).ToString(CultureInfo.InvariantCulture) + " more";
    }

    /// <summary>
    /// Parses <c>ObjFlags</c>. A <c>$NODEID</c> formula has no node-id context here, so
    /// <see cref="ValueConverter.ParseInteger(string, byte?)"/> throws
    /// <see cref="NotSupportedException"/>. That failure is an invalid numeric key:
    /// lenient mode falls back, strict mode throws <see cref="EdsParseException"/>.
    /// </summary>
    private static uint ParseObjFlagsInteger(string value)
    {
        try
        {
            return ValueConverter.ParseInteger(value);
        }
        catch (NotSupportedException ex)
        {
            throw new EdsParseException(ex.Message, ex);
        }
    }

    /// <summary>
    /// Reports a readable value that is outside its allowed range. Lenient mode adds a
    /// diagnostic and the caller keeps the value (validation reports it); strict mode throws.
    /// </summary>
    internal static void ReportOutOfRange(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string keyName,
        string rawValue,
        string code,
        string message)
    {
        if (StrictParsingScope.IsEnabled)
        {
            throw new EdsParseException(message)
            {
                Code = code,
                SectionName = sectionName,
                LineNumber = IniKeyLines.TryGetLine(sections, sectionName, keyName)
            };
        }

        Report(sections, sectionName, keyName, rawValue, code, coercedTo: null, message + " The value is kept.");
    }

    /// <summary>
    /// Parses a <c>[DynamicChannels]</c> <c>PPOffset&lt;n&gt;</c> value: <c>offset</c> or
    /// <c>offset, addressDifference</c> (CiA 306-3 § 5.2.2). An empty value is offset <c>0</c>.
    /// A malformed value is offset <c>0</c> without an address difference in lenient mode.
    /// </summary>
    internal static (uint Offset, uint? AddressDifference) ParsePpOffset(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string keyName,
        string rawValue)
        => Parse<(uint Offset, uint? AddressDifference)>(
            sections,
            sectionName,
            keyName,
            rawValue,
            ParsePpOffsetTuple,
            (0u, null),
            Diagnostics.ParseDiagnosticCodes.InvalidDynamicChannelPpOffset,
            coercedTo: "0",
            TreatAsZero);

    private static (uint Offset, uint? AddressDifference) ParsePpOffsetTuple(string value)
    {
        if (value.Trim().Length == 0)
            return (0u, null);

        var parts = value.Split(',');
        if (parts.Length > 2)
        {
            throw new EdsParseException(
                "Invalid PPOffset value: '" + value + "'. Expected 'offset' or 'offset, addressDifference'.");
        }

        var offset = ParseUInt32Part(parts[0]);
        return parts.Length == 2 ? (offset, ParseUInt32Part(parts[1])) : (offset, null);
    }

    private static uint ParseUInt32Part(string part)
    {
        // An empty part ("0," or ",1") is malformed here, unlike an empty whole value.
        if (part.Trim().Length == 0)
            throw new EdsParseException("Invalid PPOffset value: an empty offset or address difference.");

        // ParseObjFlagsInteger turns the $NODEID NotSupportedException into EdsParseException.
        return ParseObjFlagsInteger(part);
    }

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

    /// <inheritdoc cref="ParseOptionalByte"/>
    internal static uint? ParseOptionalUInt32(
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
            ParseObjFlagsInteger,
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
