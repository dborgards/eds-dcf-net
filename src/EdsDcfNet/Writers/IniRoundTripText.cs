namespace EdsDcfNet.Writers;

using System.Globalization;
using System.Text;
using EdsDcfNet.Validation;

/// <summary>
/// Rejects INI text that the INI parser cannot read back unchanged, then writes it.
/// EDS, DCF, and CPJ writers share this helper so a value cannot smuggle a new section or key.
/// </summary>
internal static class IniRoundTripText
{
    internal static void WriteSectionHeader(StringBuilder sb, string sectionName)
    {
        Ensure(sectionName, IniWriteRules.IniTextSlot.SectionName);
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[{0}]", sectionName));
    }

    internal static void WriteKeyValue(StringBuilder sb, string key, string? value)
    {
        Ensure(key, IniWriteRules.IniTextSlot.Key);
        Ensure(value, IniWriteRules.IniTextSlot.Value);
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}={1}", key, value));
    }

    private static void Ensure(string? text, IniWriteRules.IniTextSlot slot)
    {
        if (IniWriteRules.TryReject(text, slot, out var message))
            throw new IniTextRejectedException(message);
    }
}

/// <summary>
/// Signals that an INI writer refused text which would not round-trip.
/// Public writers translate this into the format's write exception before it leaves the API.
/// </summary>
internal sealed class IniTextRejectedException : Exception
{
    internal IniTextRejectedException(string message)
        : base(message)
    {
    }
}
