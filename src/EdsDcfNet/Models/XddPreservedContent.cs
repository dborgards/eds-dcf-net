namespace EdsDcfNet.Models;

using System.Xml.Linq;

/// <summary>
/// XDD/XDC content that the model does not represent, kept as read so an XDD/XDC write can put
/// it back at its schema position (CiA 311 Annex A).
/// </summary>
/// <remarks>
/// <para>
/// Elements are kept whole (with everything inside them, comments included) and keep the
/// <see cref="XName"/> they had in the source. Only a source without any namespace (older
/// outputs of this library) is qualified while reading, from the parent-based name table.
/// Namespace declarations are not kept; the writer declares its own.
/// </para>
/// <para>
/// The content is internal: it is copied by <c>ModelCloner</c> and read by the XDD/XDC writers
/// only. Comments and processing instructions between modelled elements are not kept.
/// </para>
/// </remarks>
internal sealed class XddPreservedContent
{
    /// <summary>Children of the device <c>ISO15745Profile</c> (its <c>ProfileHeader</c>).</summary>
    internal const string DeviceProfile = "DeviceProfile";

    /// <summary>Children of the communication network <c>ISO15745Profile</c>.</summary>
    internal const string NetworkProfile = "NetworkProfile";

    /// <summary>Device <c>ProfileBody</c> (children and attributes).</summary>
    internal const string DeviceProfileBody = "DeviceProfileBody";

    /// <summary>Communication network <c>ProfileBody</c> (children and attributes).</summary>
    internal const string NetworkProfileBody = "NetworkProfileBody";

    /// <summary><c>DeviceIdentity</c> children; attributes of a child use <c>DeviceIdentity/name</c>.</summary>
    internal const string DeviceIdentity = "DeviceIdentity";

    /// <summary><c>ApplicationLayers</c> (children and attributes).</summary>
    internal const string ApplicationLayers = "ApplicationLayers";

    /// <summary><c>NetworkManagement</c> children.</summary>
    internal const string NetworkManagement = "NetworkManagement";

    /// <summary>
    /// The <c>ag_formatAndFile</c> attributes that both <c>ProfileBody</c> elements carry and
    /// <see cref="EdsFileInfo"/> represents once.
    /// </summary>
    internal static readonly string[] FileAttributeNames =
    {
        "fileName",
        "fileCreator",
        "fileCreationDate",
        "fileCreationTime",
        "fileVersion",
        "fileModificationDate",
        "fileModificationTime",
        "fileModifiedBy",
    };

    /// <summary>Text of the comments before the root element that do not carry <see cref="Comments"/>.</summary>
    internal List<string> RootComments { get; } = new();

    /// <summary>Kept elements per position key, in source order.</summary>
    internal Dictionary<string, List<XElement>> Elements { get; } = new(StringComparer.Ordinal);

    /// <summary>Kept attributes per element key, in source order.</summary>
    internal Dictionary<string, List<XAttribute>> Attributes { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// File attributes of the communication network <c>ProfileBody</c>, read like those of the device
    /// profile, kept when the device profile supplied <see cref="EdsFileInfo"/>.
    /// <see langword="null"/> when the network profile was the source.
    /// </summary>
    internal EdsFileInfo? NetworkFileInfo { get; set; }

    /// <summary>
    /// Value of each <see cref="EdsFileInfo"/> field right after the read, per file attribute
    /// (see <see cref="FileInfoField"/>). A field that still has this value is written with the
    /// value each profile had; a changed field is written to both profiles (rule 13).
    /// </summary>
    internal Dictionary<string, string> FileInfoBaseline { get; } = new(StringComparer.Ordinal);

    /// <summary>Kept elements at <paramref name="key"/>, or an empty list.</summary>
    internal IReadOnlyList<XElement> ElementsAt(string key)
        => Elements.TryGetValue(key, out var list) ? list : Array.Empty<XElement>();

    /// <summary>Kept attributes at <paramref name="key"/>, or an empty list.</summary>
    internal IReadOnlyList<XAttribute> AttributesAt(string key)
        => Attributes.TryGetValue(key, out var list) ? list : Array.Empty<XAttribute>();

    internal void AddElement(string key, XElement element)
    {
        if (!Elements.TryGetValue(key, out var list))
        {
            list = new List<XElement>();
            Elements[key] = list;
        }

        list.Add(element);
    }

    internal void AddAttribute(string key, XAttribute attribute)
    {
        if (!Attributes.TryGetValue(key, out var list))
        {
            list = new List<XAttribute>();
            Attributes[key] = list;
        }

        list.Add(attribute);
    }

    /// <summary>
    /// The <see cref="EdsFileInfo"/> field (or fields) behind file attribute <paramref name="attributeName"/>,
    /// as one comparable text.
    /// </summary>
    internal static string FileInfoField(EdsFileInfo fileInfo, string attributeName) => attributeName switch
    {
        "fileName" => fileInfo.FileName,
        "fileCreator" => fileInfo.CreatedBy,
        "fileCreationDate" => fileInfo.CreationDate,
        "fileCreationTime" => fileInfo.CreationTime,
        "fileVersion" => fileInfo.FileVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "\n" + fileInfo.FileVersionText,
        "fileModificationDate" => fileInfo.ModificationDate,
        "fileModificationTime" => fileInfo.ModificationTime,
        _ => fileInfo.ModifiedBy,
    };

    /// <summary>
    /// Copies the <see cref="EdsFileInfo"/> field (or fields, with their read spelling) behind file
    /// attribute <paramref name="attributeName"/>.
    /// </summary>
    internal static void CopyFileField(EdsFileInfo source, EdsFileInfo target, string attributeName)
    {
        switch (attributeName)
        {
            case "fileName":
                target.FileName = source.FileName;
                break;
            case "fileCreator":
                target.CreatedBy = source.CreatedBy;
                break;
            case "fileCreationDate":
                target.CreationDate = source.CreationDate;
                target.CreationDateLexical = source.CreationDateLexical;
                break;
            case "fileCreationTime":
                target.CreationTime = source.CreationTime;
                break;
            case "fileVersion":
                target.FileVersion = source.FileVersion;
                target.FileVersionText = source.FileVersionText;
                target.FileVersionTextBaseline = source.FileVersionTextBaseline;
                break;
            case "fileModificationDate":
                target.ModificationDate = source.ModificationDate;
                target.ModificationDateLexical = source.ModificationDateLexical;
                break;
            case "fileModificationTime":
                target.ModificationTime = source.ModificationTime;
                break;
            default:
                target.ModifiedBy = source.ModifiedBy;
                break;
        }
    }

    /// <summary>Deep copy; kept nodes are copied so the clone shares no XML objects.</summary>
    internal XddPreservedContent Clone()
    {
        var clone = new XddPreservedContent();
        clone.RootComments.AddRange(RootComments);
        foreach (var entry in Elements)
            clone.Elements[entry.Key] = entry.Value.Select(element => new XElement(element)).ToList();
        foreach (var entry in Attributes)
            clone.Attributes[entry.Key] = CloneAttributes(entry.Value)!;
        if (NetworkFileInfo != null)
        {
            clone.NetworkFileInfo = new EdsFileInfo();
            foreach (var name in FileAttributeNames)
                CopyFileField(NetworkFileInfo, clone.NetworkFileInfo, name);
        }

        foreach (var entry in FileInfoBaseline)
            clone.FileInfoBaseline[entry.Key] = entry.Value;
        return clone;
    }

    /// <summary>Copies a list of kept attributes (<see langword="null"/> stays <see langword="null"/>).</summary>
    internal static List<XAttribute>? CloneAttributes(List<XAttribute>? source)
        => source?.Select(attribute => new XAttribute(attribute)).ToList();
}
