namespace EdsDcfNet.Models;

/// <summary>
/// A single label entry (element <c>label</c> in the g_labels schema group).
/// Provides a localized display name for the parent element.
/// </summary>
public class ApLabel
{
    /// <summary>ISO 639-2 language code, e.g. "en", "de", "fr".</summary>
    public string Lang { get; set; } = string.Empty;

    /// <summary>The label text in the specified language.</summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// A single description entry (element <c>description</c> in the g_labels schema group).
/// Provides a localized descriptive text with an optional URI for further information.
/// </summary>
public class ApDescription
{
    /// <summary>ISO 639-2 language code.</summary>
    public string Lang { get; set; } = string.Empty;

    /// <summary>The description text in the specified language.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Optional URI linking to further descriptive information.</summary>
    public string? Uri { get; set; }
}

/// <summary>
/// A reference to an entry in an external text resource file.
/// Used by both <c>labelRef</c> and <c>descriptionRef</c> elements of the g_labels schema group.
/// </summary>
public class ApTextRef
{
    /// <summary>Identifies the dictionary element in the dictionaryList.</summary>
    public string DictId { get; set; } = string.Empty;

    /// <summary>References a character sequence inside the external text resource file.</summary>
    public string TextId { get; set; } = string.Empty;

    /// <summary>Optional URI content of the element linking to further information.</summary>
    public string? Uri { get; set; }

    /// <summary>True when this represents a <c>descriptionRef</c> element; false for <c>labelRef</c>.</summary>
    public bool IsDescriptionRef { get; set; }
}

/// <summary>
/// Represents the g_labels schema group: a collection of multilingual labels,
/// descriptions, and external text references for the parent element.
/// </summary>
public class ApLabelGroup
{
    /// <summary>Localized label entries (<c>label</c> elements).</summary>
    public List<ApLabel> Labels { get; } = new();

    /// <summary>Localized description entries (<c>description</c> elements).</summary>
    public List<ApDescription> Descriptions { get; } = new();

    /// <summary>
    /// External text resource references (<c>labelRef</c> and <c>descriptionRef</c> elements).
    /// Use <see cref="ApTextRef.IsDescriptionRef"/> to distinguish them.
    /// </summary>
    public List<ApTextRef> TextRefs { get; } = new();

    /// <summary>
    /// Returns <see langword="true"/> when no labels, descriptions, or refs are present.
    /// </summary>
    public bool IsEmpty =>
        Labels.Count == 0 && Descriptions.Count == 0 && TextRefs.Count == 0;

    /// <summary>
    /// Gets the first label text, preferring English; falls back to the first available label.
    /// Returns <see langword="null"/> when <see cref="IsEmpty"/> is <see langword="true"/>.
    /// </summary>
    public string? GetDisplayName()
    {
        if (Labels.Count == 0)
            return null;

        foreach (var lbl in Labels)
        {
            if (lbl.Lang.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                return lbl.Text;
        }

        return Labels[0].Text;
    }
}
