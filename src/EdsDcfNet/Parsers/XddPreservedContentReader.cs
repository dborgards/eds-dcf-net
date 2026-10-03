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
        var kept = KeptAttributes(
            element,
            element.Attributes().Where(a => !a.IsNamespaceDeclaration && !IsModelled(a.Name, modelled, includeActualValues)));
        return kept.Count == 0 ? null : kept;
    }

    /// <summary>
    /// Copies of <paramref name="attributes"/> of <paramref name="source"/>. For a value that starts
    /// with a prefix (<c>custom="vendor:choice"</c>), the binding in scope at
    /// <paramref name="source"/> is kept as a namespace declaration next to it, so the element that
    /// receives the attributes on write can resolve the prefix.
    /// </summary>
    private static List<XAttribute> KeptAttributes(XElement source, IEnumerable<XAttribute> attributes)
    {
        var kept = new List<XAttribute>();
        foreach (var attribute in attributes)
        {
            kept.Add(new XAttribute(attribute));
            var declaration = ReferencedBinding(source, attribute.Value);
            if (declaration != null && !kept.Any(a => a.Name == declaration.Name))
                kept.Add(declaration);
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
                foreach (var attribute in KeptAttributes(child, child.Attributes().Where(a => !a.IsNamespaceDeclaration)))
                    content.AddAttribute(XddPreservedContent.DeviceIdentity + "/" + local, attribute);
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
            foreach (var attribute in KeptAttributes(applicationLayers, applicationLayers.Attributes().Where(a => !a.IsNamespaceDeclaration)))
                content.AddAttribute(XddPreservedContent.ApplicationLayers, attribute);

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
        var unmodelled = body.Attributes().Where(attribute =>
            !attribute.IsNamespaceDeclaration
            && attribute.Name != XddNames.Xsi + "type"
            && !(attribute.Name.Namespace == XNamespace.None && ModelledFileAttributes.Contains(attribute.Name.LocalName)));
        foreach (var attribute in KeptAttributes(body, unmodelled))
            content.AddAttribute(key, attribute);
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
    /// each descendant from its (already renamed) parent. A binding that a QName value of the
    /// fragment refers to (<c>xsi:type="vendor:Extension"</c>) is declared again on that element,
    /// unless the writer declares the same binding on the document element.
    /// </summary>
    private static XElement Copy(XElement source, Func<string, XName> rootName, bool qualify)
    {
        var copy = new XElement(source);
        var originals = source.DescendantsAndSelf().ToList();
        var copies = copy.DescendantsAndSelf().ToList();
        for (var i = 0; i < copies.Count; i++)
        {
            var element = copies[i];
            element.Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
            KeepReferencedBindings(originals[i], element);
            if (!qualify)
                continue;

            element.Name = element.Parent == null
                ? rootName(element.Name.LocalName)
                : XddNames.ChildOrUnqualified(element.Parent.Name, element.Name.LocalName);
        }

        return copy;
    }

    private static readonly System.Text.RegularExpressions.Regex QNamePrefix = new(
        @"^\s*([A-Za-z_][\w.-]*):",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>Bindings the XDD/XDC writers declare on the document element.</summary>
    private static readonly Dictionary<string, XNamespace> WriterBindings = new(StringComparer.Ordinal)
    {
        [XddNames.Prefix] = XddNames.Namespace,
        ["xsi"] = XddNames.Xsi,
        ["xml"] = XNamespace.Xml,
    };

    private static void KeepReferencedBindings(XElement original, XElement copy)
    {
        var values = copy.Attributes().Select(a => a.Value)
            .Concat(copy.Nodes().OfType<XText>().Select(t => t.Value))
            .ToList();
        foreach (var value in values)
        {
            var declaration = ReferencedBinding(original, value);
            if (declaration == null || copy.GetNamespaceOfPrefix(declaration.Name.LocalName)?.NamespaceName == declaration.Value)
                continue;

            copy.Add(declaration);
        }
    }

    /// <summary>
    /// Declaration of the binding that <paramref name="value"/> refers to with a leading
    /// <c>prefix:</c>, as in scope at <paramref name="source"/>; <see langword="null"/> when the value
    /// has no prefix, the prefix is not bound, or the writer declares the same binding on the
    /// document element.
    /// </summary>
    private static XAttribute? ReferencedBinding(XElement source, string value)
    {
        var match = QNamePrefix.Match(value);
        if (!match.Success)
            return null;

        var prefix = match.Groups[1].Value;
        var bound = source.GetNamespaceOfPrefix(prefix);
        if (bound == null || (WriterBindings.TryGetValue(prefix, out var declared) && declared == bound))
            return null;

        return new XAttribute(XNamespace.Xmlns + prefix, bound.NamespaceName);
    }
}
