namespace EdsDcfNet.Models;

using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using EdsDcfNet.Writers;

/// <summary>Base class for typed, lossless views over CiA 311 profile content.</summary>
public abstract class XddXmlModel
{
    private protected XddXmlModel(XElement element) => Element = element;
    internal XElement Element { get; }

    private protected string? Get(string name) => Element.Attribute(name)?.Value;
    private protected void Set(string name, string? value)
    {
        if (value == null) Element.Attribute(name)?.Remove();
        else Element.SetAttributeValue(name, value);
    }

    private protected XElement? Child(string name) => Element.Elements().FirstOrDefault(e => e.Name.LocalName == name);
    private protected XElement? Descendant(string parent, string child) => Child(parent)?.Elements().FirstOrDefault(e => e.Name.LocalName == child);
    private protected static T? Wrap<T>(XElement? element) where T : XddXmlModel => element == null ? null :
        (T)Activator.CreateInstance(typeof(T), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { element }, null)!;

    private protected XElement EnsureChild(string name, params string[] order) => EnsureChild(Element, name, order);
    private protected static XElement EnsureChild(XElement parent, string name, params string[] order)
    {
        var child = parent.Elements().FirstOrDefault(e => e.Name.LocalName == name);
        if (child != null) return child;
        child = XddXml.Create(name);
        XddXml.Add(parent, child, order);
        return child;
    }

    private protected void SetChild(string name, XddXmlModel? value, params string[] order)
    {
        Child(name)?.Remove();
        if (value == null) return;
        value.Element.Name = XNamespace.None + name;
        XddXml.Add(Element, value.Element, order);
    }

    private protected ICollection<T> List<T>(string? path, string? name, Func<XElement, T> wrap,
        Func<T> create, bool pruneEmptyParent = false, params string[] order) where T : XddXmlModel
    {
        XElement? Parent() => path == null ? Element : FindPath(path);
        IEnumerable<XElement> Items() => Parent()?.Elements().Where(e => name == null
            ? XddXml.IsText(e.Name.LocalName) : e.Name.LocalName == name) ?? Enumerable.Empty<XElement>();
        return new XddModelCollection<T>(() => Items(), wrap, item =>
        {
            var parent = Parent() ?? EnsurePath(path!);
            XddXml.Add(parent, item.Element, order);
        }, item =>
        {
            var parent = Parent();
            var element = Items().FirstOrDefault(candidate => ReferenceEquals(candidate, item.Element));
            if (element == null) return false;
            element.Remove();
            if (pruneEmptyParent && parent?.Elements().Any() == false)
                parent.Remove();
            return true;
        }, create);
    }

    private protected ICollection<T> ChoiceList<T>(string path, string name, Func<XElement, T> wrap,
        Func<T> create, string alternative) where T : XddXmlModel
    {
        var result = List(path, name, wrap, create);
        return new XddModelCollection<T>(() => result.Select(item => item.Element), wrap, item =>
        {
            Child(alternative)?.Remove();
            result.Add(item);
        }, item => result.Remove(item), create);
    }

    private protected ICollection<string> StringList(string childName, string attribute)
        => StringList(null, childName, attribute);

    private protected ICollection<string> StringList(string? path, string childName, string attribute,
        bool pruneEmptyParent = false)
    {
        XElement? Parent() => path == null ? Element : FindPath(path);
        return new XddStringCollection(
            () => Parent()?.Elements().Where(e => e.Name.LocalName == childName) ?? Enumerable.Empty<XElement>(),
            value =>
            {
                var parent = Parent() ?? EnsurePath(path!);
                var child = XddXml.Create(childName);
                child.SetAttributeValue(attribute, value);
                XddXml.Add(parent, child);
            },
            attribute,
            pruneEmptyParent ? Parent : null);
    }

    private protected uint? UInt(string name) => uint.TryParse(Get(name), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;
    private protected void SetUInt(string name, uint? value) => Set(name, value?.ToString(CultureInfo.InvariantCulture));
    private protected byte Byte(string name, byte fallback) => byte.TryParse(Get(name), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    private protected void SetByte(string name, byte value, byte defaultValue) => Set(name, value == defaultValue ? null : value.ToString(CultureInfo.InvariantCulture));
    private protected ulong ULong(string name, ulong fallback) => ulong.TryParse(Get(name), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    private protected ulong? NullableULong(string name) => ulong.TryParse(Get(name), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;
    private protected void SetULong(string name, ulong? value, ulong? defaultValue)
        => Set(name, value == null || value.Value == defaultValue ? null : value.Value.ToString(CultureInfo.InvariantCulture));
    private protected bool Bool(string name) => string.Equals(Get(name), "true", StringComparison.Ordinal) || Get(name) == "1";
    private protected void SetBool(string name, bool value) => Set(name, value ? "true" : "false");

    private protected T Enum<T>(string name, T fallback) where T : struct
        => NullableEnum<T>(name) ?? fallback;
    private protected T? NullableEnum<T>(string name) where T : struct
    {
        var text = Get(name);
        if (text == null) return null;
        foreach (var value in System.Enum.GetValues(typeof(T)).Cast<T>())
            if (string.Equals(XddXml.EnumName(value), text, StringComparison.Ordinal)) return value;
        return null;
    }
    private protected void SetEnum<T>(string name, T value) where T : struct => Set(name, XddXml.EnumName(value));
    private protected void SetEnum<T>(string name, T value, T defaultValue) where T : struct
        => Set(name, EqualityComparer<T>.Default.Equals(value, defaultValue) ? null : XddXml.EnumName(value));
    private protected void SetNullableEnum<T>(string name, T? value) where T : struct
        => Set(name, value.HasValue ? XddXml.EnumName(value.Value) : null);

    private protected XElement EnsurePath(string path)
    {
        var parent = Element;
        foreach (var part in path.Split('/')) parent = EnsureChild(parent, part);
        return parent;
    }

    private protected XElement? FindPath(string path)
    {
        var parent = Element;
        foreach (var part in path.Split('/'))
        {
            parent = parent.Elements().FirstOrDefault(e => e.Name.LocalName == part);
            if (parent == null) return null;
        }
        return parent;
    }
}

internal static class XddXml
{
    internal static XElement Create(string name, params object[] content) => new(name, content);
    internal static XElement CreateFunction() => Create("DeviceFunction", Create("capabilities",
        Create("characteristicsList", Create("characteristic", Create("characteristicName"), Create("characteristicContent")))));

    internal static void Require(object? value, string name)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value, name);
#else
        if (value == null) throw new ArgumentNullException(name);
#endif
    }

    internal static bool IsText(string name) => name is "label" or "description" or "labelRef" or "descriptionRef";
    internal static string TextName(XddTextKind kind) => kind switch
    {
        XddTextKind.Label => "label", XddTextKind.Description => "description",
        XddTextKind.LabelReference => "labelRef", XddTextKind.DescriptionReference => "descriptionRef",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown XDD text kind.")
    };
    internal static XddTextKind TextKind(string name) => name switch
    {
        "label" => XddTextKind.Label, "description" => XddTextKind.Description,
        "labelRef" => XddTextKind.LabelReference, "descriptionRef" => XddTextKind.DescriptionReference,
        _ => throw new ArgumentException("Element is not a CiA 311 text entry.", nameof(name))
    };
    internal static string EnumName<T>(T value) where T : struct
    {
        var name = value.ToString()!;
        if (!System.Enum.IsDefined(typeof(T), value)) throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown XDD enumeration value.");
        if (name == "IO") return name;
        if (name == "SubIndex") return "subIndex";
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    internal static void Add(XElement parent, XElement child, params string[] order)
    {
        if (child.Parent != null) child.Remove();
        Adopt(parent, child);
        var rank = Rank(order.Length == 0 ? DefaultOrder(parent.Name.LocalName) : order, child.Name.LocalName);
        var following = parent.Elements().FirstOrDefault(e => Rank(order.Length == 0 ? DefaultOrder(parent.Name.LocalName) : order, e.Name.LocalName) > rank);
        if (following == null) parent.Add(child); else following.AddBeforeSelf(child);
    }

    /// <summary>Assigns schema names throughout a fragment added at a profile-body position.</summary>
    internal static void QualifyRoot(XElement element, bool global)
    {
        element.Name = global ? XddNames.Namespace + element.Name.LocalName : XNamespace.None + element.Name.LocalName;
        foreach (var child in element.Elements()) Adopt(element, child);
    }

    private static void Adopt(XElement parent, XElement child)
    {
        if (child.Name.Namespace == XNamespace.None)
            child.Name = XddNames.ChildOrUnqualified(parent.Name, child.Name.LocalName);
        foreach (var descendant in child.Elements()) Adopt(child, descendant);
    }

    private static string[] DefaultOrder(string parent) => parent switch
    {
        "DeviceManager" => new[] { "indicatorList", "moduleManagement" },
        "DeviceFunction" => new[] { "capabilities", "picturesList", "dictionaryList" },
        "capabilities" => new[] { "characteristicsList", "standardComplianceList" },
        "characteristicsList" => new[] { "category", "characteristic" },
        "characteristic" => new[] { "characteristicName", "characteristicContent" },
        "interface" => new[] { "label", "description", "labelRef", "descriptionRef", "fileList", "rangeList", "moduleTypeList", "connectedModuleList" },
        "LED" => new[] { "label", "description", "labelRef", "descriptionRef", "LEDstate" },
        "LEDstate" or "picture" or "compliantWith" => new[] { "label", "description", "labelRef", "descriptionRef" },
        "combinedState" => new[] { "label", "description", "labelRef", "descriptionRef", "LEDstateRef" },
        "moduleManagement" => new[] { "moduleInterfaceList", "interfaceList", "moduleInterface" },
        _ => Array.Empty<string>()
    };

    private static int Rank(string[] order, string name)
    {
        var index = Array.IndexOf(order, name);
        return index < 0 ? order.Length : index;
    }
}

internal sealed class XddModelCollection<T> : ICollection<T> where T : XddXmlModel
{
    private readonly Func<IEnumerable<XElement>> _elements;
    private readonly Func<XElement, T> _wrap;
    private readonly Action<T> _add;
    private readonly Func<T, bool> _remove;

    internal XddModelCollection(Func<IEnumerable<XElement>> elements, Func<XElement, T> wrap,
        Action<T> add, Func<T, bool> remove, Func<T> create)
    { _elements = elements; _wrap = wrap; _add = add; _remove = remove; }

    public int Count => _elements().Count();
    public bool IsReadOnly => false;
    public void Add(T item) { XddXml.Require(item, nameof(item)); _add(item); }
    public void Clear() { foreach (var item in this.ToList()) Remove(item); }
    public bool Contains(T item) => item != null && _elements().Any(e => ReferenceEquals(e, item.Element));
    public void CopyTo(T[] array, int arrayIndex)
    { foreach (var item in this) array[arrayIndex++] = item; }
    public bool Remove(T item) => item != null && _remove(item);
    public IEnumerator<T> GetEnumerator() => _elements().Select(_wrap).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class XddStringCollection : ICollection<string>
{
    private readonly Func<IEnumerable<XElement>> _elements;
    private readonly Action<string> _add;
    private readonly string _attribute;
    private readonly Func<XElement?>? _pruneParent;
    internal XddStringCollection(Func<IEnumerable<XElement>> elements, Action<string> add, string attribute,
        Func<XElement?>? pruneParent)
    { _elements = elements; _add = add; _attribute = attribute; _pruneParent = pruneParent; }
    public int Count => _elements().Count();
    public bool IsReadOnly => false;
    public void Add(string item) { XddXml.Require(item, nameof(item)); _add(item); }
    public void Clear()
    {
        foreach (var element in _elements().ToList()) element.Remove();
        PruneEmptyParent();
    }
    public bool Contains(string item) => _elements().Any(e => e.Attribute(_attribute)?.Value == item);
    public void CopyTo(string[] array, int arrayIndex)
    { foreach (var item in this) array[arrayIndex++] = item; }
    public bool Remove(string item)
    {
        var element = _elements().FirstOrDefault(e => e.Attribute(_attribute)?.Value == item);
        if (element == null) return false;
        element.Remove();
        PruneEmptyParent();
        return true;
    }
    public IEnumerator<string> GetEnumerator() => _elements().Select(e => e.Attribute(_attribute)?.Value ?? string.Empty).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void PruneEmptyParent()
    {
        if (_pruneParent?.Invoke() is { } parent && !parent.Elements().Any())
            parent.Remove();
    }
}
