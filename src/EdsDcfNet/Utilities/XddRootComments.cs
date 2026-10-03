namespace EdsDcfNet.Utilities;

using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using EdsDcfNet.Models;

/// <summary>
/// Carries <see cref="Comments"/> in XDD/XDC as XML comments directly before the root element.
/// </summary>
/// <remarks>
/// <para>
/// CiA 311 has no element for free-text comments, so each <c>Comments</c> line becomes one XML
/// comment before the root element. Only a comment that starts with the fixed identifier
/// <see cref="Marker"/> is read back; comments of the generating tool (generator, copyright)
/// are not device comments and stay untouched.
/// </para>
/// <para>
/// Layout: <c>&lt;!--EdsDcfNet.Comment 3: text--&gt;</c>, the line number, a colon, one space, then
/// the text. The text is escaped so it can hold what an XML comment cannot: <c>\</c> becomes
/// <c>\\</c>, a carriage return or line feed becomes <c>\r</c> or <c>\n</c> (a parser normalizes
/// raw line breaks), and each hyphen of a run of two or more, or a hyphen at the end, becomes
/// <c>\h</c> (<c>--</c> and a closing <c>-</c> are not allowed in an XML comment). The reader
/// reverses this exactly.
/// </para>
/// <para>
/// A line that cannot be carried (a character that is not valid in XML, or a line number outside
/// 1..65535) is not written; a validated write reports it (see <c>XmlWriteRules</c>).
/// </para>
/// </remarks>
internal static class XddRootComments
{
    /// <summary>Fixed identifier at the start of every comment written from <see cref="Comments"/>.</summary>
    internal const string Marker = "EdsDcfNet.Comment ";

    private const string Separator = ": ";

    /// <summary>
    /// Builds the XML comments for <paramref name="comments"/>, ordered by line number. When
    /// <see cref="Comments.Lines"/> exceeds the last carried line, an empty line with that number
    /// keeps the count.
    /// </summary>
    internal static IEnumerable<XComment> Build(Comments? comments)
    {
        if (comments == null)
            yield break;

        var last = 0;
        foreach (var line in comments.CommentLines.OrderBy(l => l.Key))
        {
            if (!IsCarried(line.Key, line.Value))
                continue;

            last = line.Key;
            yield return Create(line.Key, line.Value);
        }

        if (comments.Lines > last)
            yield return Create(comments.Lines, string.Empty);
    }

    /// <summary>
    /// Reads the marked comments that stand before the root element. Returns <see langword="null"/>
    /// when there is none.
    /// </summary>
    internal static Comments? Read(XDocument doc)
    {
        Comments? comments = null;
        foreach (var comment in doc.Nodes().TakeWhile(node => node is not XElement).OfType<XComment>())
        {
            if (!TryParse(comment.Value, out var number, out var text))
                continue;

            comments ??= new Comments();
            if (number > comments.Lines)
                comments.Lines = (ushort)number;

            if (text.Length > 0)
                comments.CommentLines[number] = text;
        }

        return comments;
    }

    /// <summary>
    /// <see langword="true"/> when a line can be written as an XML comment: its number is 1..65535
    /// and its text holds only characters that are valid in XML.
    /// </summary>
    internal static bool IsCarried(int number, string? text)
    {
        if (number < 1 || number > ushort.MaxValue)
            return false;

        if (text == null)
            return true;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (XmlConvert.IsXmlChar(c))
                continue;

            if (i + 1 < text.Length && XmlConvert.IsXmlSurrogatePair(text[i + 1], c))
            {
                i++;
                continue;
            }

            return false;
        }

        return true;
    }

    private static XComment Create(int number, string? text)
        => new(Marker + number.ToString(CultureInfo.InvariantCulture) + Separator + Escape(text ?? string.Empty));

    private static bool TryParse(string value, out int number, out string text)
    {
        number = 0;
        text = string.Empty;
        if (!value.StartsWith(Marker, StringComparison.Ordinal))
            return false;

        var separator = value.IndexOf(Separator, Marker.Length, StringComparison.Ordinal);
        if (separator < 0)
            return false;

        var digits = value.Substring(Marker.Length, separator - Marker.Length);
        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number) ||
            number < 1 || number > ushort.MaxValue)
        {
            return false;
        }

        text = Unescape(value.Substring(separator + Separator.Length));
        return true;
    }

    private static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '-' when i == text.Length - 1 || text[i + 1] == '-' || (i > 0 && text[i - 1] == '-'):
                    sb.Append("\\h");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    private static string Unescape(string text)
    {
        if (text.IndexOf('\\') < 0)
            return text;

        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c != '\\' || i + 1 == text.Length)
            {
                sb.Append(c);
                continue;
            }

            switch (text[i + 1])
            {
                case '\\':
                    sb.Append('\\');
                    break;
                case 'r':
                    sb.Append('\r');
                    break;
                case 'n':
                    sb.Append('\n');
                    break;
                case 'h':
                    sb.Append('-');
                    break;
                default:
                    // Not an escape written by this library: keep the backslash.
                    sb.Append(c);
                    continue;
            }

            i++;
        }

        return sb.ToString();
    }
}
