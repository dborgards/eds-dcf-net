namespace EdsDcfNet.Parsers;

using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

/// <summary>
/// Reads boolean and access-type INI keys through <see cref="ValueConverter"/> so an unknown
/// token is reported with its section, key and source line (strict mode: thrown with the
/// section and line).
/// </summary>
internal static class IniKeyTokens
{
    /// <summary>Reads <paramref name="keyName"/> as a boolean (absent: <see langword="false"/>).</summary>
    internal static bool ParseBoolean(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string keyName)
        => ValueConverter.ParseBoolean(
            IniParser.GetValue(sections, sectionName, keyName),
            sectionName,
            keyName,
            IniKeyLines.TryGetLine(sections, sectionName, keyName));

    /// <summary>Reads <paramref name="keyName"/> as an access type (absent: <c>ro</c>).</summary>
    internal static AccessType ParseAccessType(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string keyName)
        => ValueConverter.ParseAccessType(
            IniParser.GetValue(sections, sectionName, keyName),
            sectionName,
            keyName,
            IniKeyLines.TryGetLine(sections, sectionName, keyName));
}
