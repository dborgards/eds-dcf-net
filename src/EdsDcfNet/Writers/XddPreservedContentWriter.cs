namespace EdsDcfNet.Writers;

using System.Xml.Linq;
using EdsDcfNet.Models;

/// <summary>
/// Puts <see cref="XddPreservedContent"/> back into the elements the XDD/XDC writers build.
/// </summary>
/// <remarks>
/// Kept nodes are always copied, so a model can be written any number of times (also
/// concurrently) and a caller that changes the returned document does not change the model.
/// </remarks>
internal static class XddPreservedContentWriter
{
    /// <summary>Children of <c>ISO15745Profile</c> in schema order.</summary>
    internal static readonly string[] ProfileOrder = { "ProfileHeader", "ProfileBody" };

    /// <summary>Children of <c>ProfileBody_Device_CANopen</c> in schema order.</summary>
    internal static readonly string[] DeviceProfileBodyOrder =
    {
        "DeviceIdentity", "DeviceManager", "DeviceFunction", "ApplicationProcess", "ExternalProfileHandle",
    };

    /// <summary>Children of <c>DeviceIdentity</c> in schema order.</summary>
    internal static readonly string[] DeviceIdentityOrder =
    {
        "vendorName", "vendorID", "vendorText", "deviceFamily", "productFamily", "productName", "productID",
        "productText", "orderNumber", "version", "buildDate", "specificationRevision", "instanceName",
    };

    /// <summary>Children of <c>ProfileBody_CommunicationNetwork_CANopen</c> in schema order.</summary>
    internal static readonly string[] NetworkProfileBodyOrder =
    {
        "ApplicationLayers", "TransportLayers", "NetworkManagement", "ExternalProfileHandle",
    };

    /// <summary>Children of <c>ApplicationLayers</c> in schema order.</summary>
    internal static readonly string[] ApplicationLayersOrder =
    {
        "identity", "CANopenObjectList", "dummyUsage", "dynamicChannels", "moduleManagement",
    };

    /// <summary>Children of <c>NetworkManagement</c> in schema order.</summary>
    internal static readonly string[] NetworkManagementOrder =
    {
        "CANopenGeneralFeatures", "CANopenMasterFeatures", "deviceCommissioning",
    };

    /// <summary>
    /// Adds copies of <paramref name="kept"/> to <paramref name="parent"/> and orders all children
    /// by their position in <paramref name="order"/>; an element the schema does not list goes last,
    /// and elements of the same position keep their order. A kept element whose local name is in
    /// <paramref name="replaces"/> replaces the generated children of that name (the writer only
    /// generates them as a default).
    /// </summary>
    internal static void MergeElements(XElement parent, IEnumerable<XElement> kept, string[] order, params string[] replaces)
    {
        var copies = kept.Select(element => new XElement(element)).ToList();
        if (copies.Count == 0)
            return;

        var replaced = new HashSet<string>(
            copies.Select(element => element.Name.LocalName).Where(name => replaces.Contains(name)),
            StringComparer.Ordinal);
        var children = parent.Elements()
            .Where(element => !replaced.Contains(element.Name.LocalName))
            .Concat(copies)
            .OrderBy(element => Rank(order, element.Name.LocalName))
            .ToList();
        parent.ReplaceNodes(children);
    }

    /// <summary>
    /// Adds copies of <paramref name="kept"/> to <paramref name="element"/>. An attribute the writer
    /// already generated wins (rule 13).
    /// </summary>
    internal static void AddAttributes(XElement element, IEnumerable<XAttribute> kept)
    {
        foreach (var attribute in kept)
        {
            if (element.Attribute(attribute.Name) == null)
                element.Add(new XAttribute(attribute));
        }
    }

    /// <summary>
    /// File information for the network <c>ProfileBody</c>. Field by field, it is the network
    /// profile's own value from the read while the <see cref="EdsFileInfo"/> field behind it still
    /// has its value from the read; a changed field takes the value of <paramref name="fileInfo"/>,
    /// so it is the same in both profiles (rule 13). Without kept network values it is
    /// <paramref name="fileInfo"/> itself.
    /// </summary>
    internal static EdsFileInfo NetworkFileInfo(XddPreservedContent preserved, EdsFileInfo fileInfo)
    {
        if (preserved.NetworkFileInfo == null)
            return fileInfo;

        var merged = new EdsFileInfo();
        foreach (var name in XddPreservedContent.FileAttributeNames)
        {
            var unchanged = preserved.FileInfoBaseline[name] == XddPreservedContent.FileInfoField(fileInfo, name);
            XddPreservedContent.CopyFileField(unchanged ? preserved.NetworkFileInfo : fileInfo, merged, name);
        }

        return merged;
    }

    private static int Rank(string[] order, string localName)
    {
        var index = Array.IndexOf(order, localName);
        return index < 0 ? order.Length : index;
    }
}
