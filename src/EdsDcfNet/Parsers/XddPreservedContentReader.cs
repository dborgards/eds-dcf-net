namespace EdsDcfNet.Parsers;

using System.Xml.Linq;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;
using EdsDcfNet.Writers;

/// <summary>
/// Collects the XDD/XDC elements and attributes that the reader does not map onto the model
/// (<see cref="XddPreservedContent"/>), so the XDD/XDC writers can put them back.
/// </summary>
internal static class XddPreservedContentReader
{
    private static readonly HashSet<string> ModelledFileAttributes = new(XddPreservedContent.FileAttributeNames, StringComparer.Ordinal);

    private static readonly HashSet<string> ModelledIdentityChildren = new(StringComparer.Ordinal)
    {
        "vendorName",
        "vendorID",
        "productName",
        "productID",
    };

    private static readonly HashSet<string> ModelledApplicationLayersChildren = new(StringComparer.Ordinal)
    {
        "CANopenObjectList",
        "dummyUsage",
        "dynamicChannels",
    };

    private static readonly HashSet<string> ModelledNetworkManagementChildren = new(StringComparer.Ordinal)
    {
        "CANopenGeneralFeatures",
        "CANopenMasterFeatures",
    };

    private static readonly HashSet<string> ModelledObjectAttributes = new(StringComparer.Ordinal)
    {
        "index", "name", "objectType", "dataType", "accessType", "defaultValue", "lowLimit",
        "highLimit", "PDOmapping", "objFlags", "uniqueIDRef", "subNumber",
    };

    private static readonly HashSet<string> ModelledSubObjectAttributes = new(StringComparer.Ordinal)
    {
        "subIndex", "name", "objectType", "dataType", "accessType", "defaultValue", "lowLimit",
        "highLimit", "PDOmapping", "uniqueIDRef",
    };

    /// <summary>
    /// Reads the content of both profiles that the model does not represent. Must run after
    /// <see cref="ElectronicDataSheet.FileInfo"/> and <see cref="ElectronicDataSheet.DeviceInfo"/>
    /// are parsed. <paramref name="includeActualValues"/> marks an XDC read, in which
    /// <c>deviceCommissioning</c>, <c>actualValue</c> and <c>denotation</c> are modelled.
    /// </summary>
    internal static XddPreservedContent Read(
        XDocument doc,
        XElement? deviceProfileBody,
        XElement networkProfileBody,
        ElectronicDataSheet eds,
        bool includeActualValues)
    {
        var qualify = HasNoNamespace(doc.Root!);
        var content = new XddPreservedContent();

        foreach (var comment in doc.Nodes().TakeWhile(node => node is not XElement).OfType<XComment>())
        {
            if (!XddRootComments.IsMarked(comment.Value))
                content.RootComments.Add(comment.Value);
        }

        var profileName = XddNames.Child(XddNames.ProfileContainer, "ISO15745Profile");
        if (deviceProfileBody != null)
        {
            KeepProfileSiblings(content, XddPreservedContent.DeviceProfile, deviceProfileBody, profileName, qualify);
            ReadDeviceProfileBody(content, deviceProfileBody, eds.DeviceInfo, qualify);
            ReadNetworkFileAttributes(content, networkProfileBody, eds.FileInfo);
        }

        KeepProfileSiblings(content, XddPreservedContent.NetworkProfile, networkProfileBody, profileName, qualify);
        ReadNetworkProfileBody(content, networkProfileBody, includeActualValues, qualify);
        return content;
    }

    /// <summary>
    /// Unmodelled attributes of a <c>CANopenObject</c>, or <see langword="null"/> when there are none.
    /// </summary>
    internal static List<XAttribute>? ObjectAttributes(XElement element, bool includeActualValues)
        => UnmodelledAttributes(element, ModelledObjectAttributes, includeActualValues);

    /// <summary>
    /// Unmodelled attributes of a <c>CANopenSubObject</c>, or <see langword="null"/> when there are none.
    /// </summary>
    internal static List<XAttribute>? SubObjectAttributes(XElement element, bool includeActualValues)
        => UnmodelledAttributes(element, ModelledSubObjectAttributes, includeActualValues);

    private static List<XAttribute>? UnmodelledAttributes(XElement element, HashSet<string> modelled, bool includeActualValues)
    {
        List<XAttribute>? kept = null;
        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration || IsModelled(attribute.Name, modelled, includeActualValues))
                continue;

            (kept ??= new List<XAttribute>()).Add(new XAttribute(attribute));
        }

        return kept;
    }

    private static bool IsModelled(XName name, HashSet<string> modelled, bool includeActualValues)
    {
        if (name.Namespace != XNamespace.None)
            return false;

        return modelled.Contains(name.LocalName)
            || (includeActualValues && (name.LocalName == "actualValue" || name.LocalName == "denotation"));
    }

    private static void ReadDeviceProfileBody(XddPreservedContent content, XElement body, DeviceInfo deviceInfo, bool qualify)
    {
        KeepBodyAttributes(content, XddPreservedContent.DeviceProfileBody, body);

        var identity = First(body, "DeviceIdentity");
        var applicationProcess = First(body, "ApplicationProcess");
        foreach (var child in body.Elements())
        {
            if (child == identity || child == applicationProcess)
                continue;

            var copy = Copy(child, local => XddNames.ChildOfTypeOrUnqualified(XddNames.DeviceProfileBodyType, local), qualify);
            if (child.Name.LocalName == "DeviceFunction" && IsDerivedDeviceFunction(copy, deviceInfo))
                continue;

            content.AddElement(XddPreservedContent.DeviceProfileBody, copy);
        }

        if (identity != null)
            ReadDeviceIdentity(content, identity, qualify);
    }

    /// <summary>
    /// A <c>DeviceFunction</c> without child elements (older outputs of this library, not
    /// schema-valid) or with exactly the content the writer derives from the model (indentation
    /// aside) carries nothing to keep; the writer derives it again.
    /// </summary>
    private static bool IsDerivedDeviceFunction(XElement function, DeviceInfo deviceInfo)
    {
        if (!function.HasElements)
            return true;

        var normalized = new XElement(function);
        normalized.DescendantNodes().OfType<XText>().Where(text => text.Value.Trim().Length == 0).Remove();
        return XNode.DeepEquals(normalized, XddProfileBuilder.BuildDefaultDeviceFunction(deviceInfo));
    }

    private static void ReadDeviceIdentity(XddPreservedContent content, XElement identity, bool qualify)
    {
        var identityName = XddNames.ChildOfType(XddNames.DeviceProfileBodyType, "DeviceIdentity");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var child in identity.Elements())
        {
            var local = child.Name.LocalName;
            if (ModelledIdentityChildren.Contains(local) && seen.Add(local))
            {
                foreach (var attribute in child.Attributes().Where(a => !a.IsNamespaceDeclaration))
                    content.AddAttribute(XddPreservedContent.DeviceIdentity + "/" + local, new XAttribute(attribute));
                continue;
            }

            if (local == "orderNumber" || local == "version")
                continue;

            content.AddElement(
                XddPreservedContent.DeviceIdentity,
                Copy(child, name => XddNames.ChildOrUnqualified(identityName, name), qualify));
        }
    }

    private static void ReadNetworkProfileBody(XddPreservedContent content, XElement body, bool includeActualValues, bool qualify)
    {
        KeepBodyAttributes(content, XddPreservedContent.NetworkProfileBody, body);

        var applicationLayers = First(body, "ApplicationLayers");
        var transportLayers = First(body, "TransportLayers");
        var networkManagement = First(body, "NetworkManagement");
        foreach (var child in body.Elements())
        {
            if (child == applicationLayers || child == transportLayers || child == networkManagement)
                continue;

            content.AddElement(
                XddPreservedContent.NetworkProfileBody,
                Copy(child, local => XddNames.ChildOfTypeOrUnqualified(XddNames.NetworkProfileBodyType, local), qualify));
        }

        if (applicationLayers != null)
        {
            foreach (var attribute in applicationLayers.Attributes().Where(a => !a.IsNamespaceDeclaration))
                content.AddAttribute(XddPreservedContent.ApplicationLayers, new XAttribute(attribute));

            KeepChildren(
                content,
                XddPreservedContent.ApplicationLayers,
                applicationLayers,
                XddNames.ChildOfType(XddNames.NetworkProfileBodyType, "ApplicationLayers"),
                ModelledApplicationLayersChildren,
                qualify);
        }

        if (networkManagement != null)
        {
            var modelled = includeActualValues
                ? new HashSet<string>(ModelledNetworkManagementChildren, StringComparer.Ordinal) { "deviceCommissioning" }
                : ModelledNetworkManagementChildren;
            KeepChildren(
                content,
                XddPreservedContent.NetworkManagement,
                networkManagement,
                XddNames.ChildOfType(XddNames.NetworkProfileBodyType, "NetworkManagement"),
                modelled,
                qualify);
        }
    }

    // The first element of each modelled name is read into the model; any further one is kept.
    private static void KeepChildren(
        XddPreservedContent content,
        string key,
        XElement parent,
        XName parentName,
        HashSet<string> modelled,
        bool qualify)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var child in parent.Elements())
        {
            if (modelled.Contains(child.Name.LocalName) && seen.Add(child.Name.LocalName))
                continue;

            content.AddElement(key, Copy(child, local => XddNames.ChildOrUnqualified(parentName, local), qualify));
        }
    }

    private static void KeepProfileSiblings(XddPreservedContent content, string key, XElement body, XName profileName, bool qualify)
    {
        foreach (var sibling in body.Parent!.Elements())
        {
            if (sibling == body)
                continue;

            content.AddElement(key, Copy(sibling, local => XddNames.ChildOrUnqualified(profileName, local), qualify));
        }
    }

    // xsi:type selects the body and the file attributes are handled separately; everything else
    // (formatName, formatVersion, supportedLanguages, deviceClass, …) is kept.
    private static void KeepBodyAttributes(XddPreservedContent content, string key, XElement body)
    {
        foreach (var attribute in body.Attributes())
        {
            if (attribute.IsNamespaceDeclaration
                || attribute.Name == XddNames.Xsi + "type"
                || (attribute.Name.Namespace == XNamespace.None && ModelledFileAttributes.Contains(attribute.Name.LocalName)))
                continue;

            content.AddAttribute(key, new XAttribute(attribute));
        }
    }

    /// <summary>
    /// The device profile supplies <see cref="EdsFileInfo"/>. The network profile's own file
    /// attributes, read the same way, and the value of each field right after the read are kept
    /// (rule 13). Diagnostics of that second read are not reported again.
    /// </summary>
    private static void ReadNetworkFileAttributes(XddPreservedContent content, XElement networkProfileBody, EdsFileInfo fileInfo)
    {
        using (Diagnostics.ParseDiagnosticScope.Enter())
            content.NetworkFileInfo = XddDeviceProfileParser.ParseFileInfo(networkProfileBody);

        foreach (var name in XddPreservedContent.FileAttributeNames)
            content.FileInfoBaseline[name] = XddPreservedContent.FileInfoField(fileInfo, name);
    }

    private static XElement? First(XElement parent, string localName)
        => parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);

    private static bool HasNoNamespace(XElement root)
        => root.DescendantsAndSelf().All(element => element.Name.Namespace == XNamespace.None);

    /// <summary>
    /// Deep copy without namespace declarations. For a source without any namespace, every element
    /// is named from the parent-based table: the copy's own name from <paramref name="rootName"/>,
    /// each descendant from its (already renamed) parent.
    /// </summary>
    private static XElement Copy(XElement source, Func<string, XName> rootName, bool qualify)
    {
        var copy = new XElement(source);
        foreach (var element in copy.DescendantsAndSelf().ToList())
        {
            element.Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
            if (!qualify)
                continue;

            element.Name = element.Parent == null
                ? rootName(element.Name.LocalName)
                : XddNames.ChildOrUnqualified(element.Parent.Name, element.Name.LocalName);
        }

        return copy;
    }
}
