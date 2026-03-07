namespace EdsDcfNet.Writers;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

/// <summary>
/// Writer for CiA 311 XDD (XML Device Description) files.
/// Orchestrates document construction by delegating section-specific concerns to
/// <see cref="XddProfileBuilder"/>, <see cref="XddTransportLayersBuilder"/>,
/// <see cref="XddApplicationProcessBuilder"/>, and <see cref="XddFormatHelper"/>.
/// </summary>
public class XddWriter
{
    /// <summary>
    /// Writes an ElectronicDataSheet as an XDD file to the specified path.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="filePath">Path where the XDD file should be written</param>
    public void WriteFile(ElectronicDataSheet eds, string filePath)
    {
        try
        {
            var content = GenerateString(eds);
            File.WriteAllText(filePath, content, TextFileIo.Utf8NoBom);
        }
        catch (XddWriteException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new XddWriteException($"Failed to write XDD file to {filePath}", ex);
        }
    }

    /// <summary>
    /// Writes an ElectronicDataSheet as XDD content to the specified stream.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="stream">Writable destination stream</param>
    public void WriteStream(ElectronicDataSheet eds, Stream stream)
    {
        ThrowIfNull(stream, nameof(stream));
        if (!stream.CanWrite)
            throw new ArgumentException("Stream must be writable.", nameof(stream));

        try
        {
            var content = GenerateString(eds);
            TextFileIo.WriteAllText(stream, content, TextFileIo.Utf8NoBom, leaveOpen: true);
        }
        catch (XddWriteException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new XddWriteException("Failed to write XDD content to stream.", ex);
        }
    }

    /// <summary>
    /// Writes an ElectronicDataSheet as an XDD file to the specified path asynchronously.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="filePath">Path where the XDD file should be written</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    public async Task WriteFileAsync(
        ElectronicDataSheet eds,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = GenerateString(eds);
            await TextFileIo.WriteAllTextAsync(filePath, content, TextFileIo.Utf8NoBom, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (XddWriteException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new XddWriteException($"Failed to write XDD file to {filePath}", ex);
        }
    }

    /// <summary>
    /// Writes an ElectronicDataSheet as XDD content to the specified stream asynchronously.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    public async Task WriteStreamAsync(
        ElectronicDataSheet eds,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNull(stream, nameof(stream));
        if (!stream.CanWrite)
            throw new ArgumentException("Stream must be writable.", nameof(stream));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = GenerateString(eds);
            await TextFileIo.WriteAllTextAsync(stream, content, TextFileIo.Utf8NoBom, leaveOpen: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (XddWriteException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new XddWriteException("Failed to write XDD content to stream.", ex);
        }
    }

    /// <summary>
    /// Generates XDD content as a string.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to convert</param>
    /// <returns>XDD content as string</returns>
    public string GenerateString(ElectronicDataSheet eds)
        => GenerateString(eds, commissioning: null);

    /// <summary>
    /// Generates XDD/XDC content as a string, optionally including device commissioning data.
    /// Called by <see cref="XdcWriter"/> to pass commissioning through the virtual call chain
    /// without resorting to mutable instance state.
    /// </summary>
    internal string GenerateString(ElectronicDataSheet eds, DeviceCommissioning? commissioning)
    {
        return WriteContext(
            "Document",
            () =>
            {
                var doc = BuildDocument(eds, commissioning);
                return SerializeDocument(doc);
            });
    }

    /// <summary>
    /// Builds the XDocument for the given EDS without commissioning data.
    /// Override this in subclasses for commissioning-unaware customisation.
    /// Called by <see cref="BuildDocument(ElectronicDataSheet, DeviceCommissioning?)"/>
    /// when no commissioning data is present, keeping this override in the call chain
    /// for backward compatibility.
    /// </summary>
    protected virtual XDocument BuildDocument(ElectronicDataSheet eds)
        => BuildDocumentCore(eds, commissioning: null);

    /// <summary>
    /// Builds the XDocument for the given EDS, optionally including commissioning data.
    /// Override this in subclasses to customise commissioning-aware output.
    /// When <paramref name="commissioning"/> is <see langword="null"/>, delegates to
    /// <see cref="BuildDocument(ElectronicDataSheet)"/> so that single-argument overrides
    /// remain in the call chain.
    /// </summary>
    protected virtual XDocument BuildDocument(ElectronicDataSheet eds, DeviceCommissioning? commissioning)
        => commissioning == null
            ? BuildDocument(eds)
            : BuildDocumentCore(eds, commissioning);

    private XDocument BuildDocumentCore(ElectronicDataSheet eds, DeviceCommissioning? commissioning)
    {
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

        var container = new XElement("ISO15745ProfileContainer",
            new XAttribute(XNamespace.Xmlns + "xsi", xsi));

        container.Add(WriteContext("DeviceProfile", () => BuildDeviceProfile(eds, xsi)));
        container.Add(WriteContext("CommunicationNetworkProfile", () => BuildCommNetProfile(eds, xsi, commissioning)));

        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            container);
    }

    private static XElement BuildDeviceProfile(ElectronicDataSheet eds, XNamespace xsi)
    {
        var profileBody = new XElement("ProfileBody",
            new XAttribute(xsi + "type", "ProfileBody_Device_CANopen"));

        XddProfileBuilder.AddFileInfoAttributes(profileBody, eds.FileInfo);

        profileBody.Add(XddProfileBuilder.BuildDeviceIdentity(eds.DeviceInfo));
        profileBody.Add(new XElement("DeviceManager"));
        profileBody.Add(new XElement("DeviceFunction"));

        if (eds.ApplicationProcess != null)
            profileBody.Add(XddApplicationProcessBuilder.Build(eds.ApplicationProcess));

        return XddProfileBuilder.BuildProfile("Device", profileBody);
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Calls virtual members via instance dispatch.")]
    private XElement BuildCommNetProfile(ElectronicDataSheet eds, XNamespace xsi, DeviceCommissioning? commissioning)
    {
        var profileBody = new XElement("ProfileBody",
            new XAttribute(xsi + "type", "ProfileBody_CommunicationNetwork_CANopen"));

        XddProfileBuilder.AddFileInfoAttributes(profileBody, eds.FileInfo);
        profileBody.Add(BuildApplicationLayers(eds));
        profileBody.Add(XddTransportLayersBuilder.Build(eds.DeviceInfo));
        profileBody.Add(BuildNetworkManagement(eds, commissioning));

        return XddProfileBuilder.BuildProfile("CommunicationNetwork", profileBody);
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Calls virtual members via instance dispatch.")]
    private XElement BuildApplicationLayers(ElectronicDataSheet eds)
    {
        var appLayers = new XElement("ApplicationLayers");

        appLayers.Add(BuildObjectList(eds.ObjectDictionary));

        if (eds.ObjectDictionary.DummyUsage.Count > 0)
            appLayers.Add(XddProfileBuilder.BuildDummyUsage(eds.ObjectDictionary));

        if (eds.DynamicChannels != null && eds.DynamicChannels.Segments.Count > 0)
            appLayers.Add(XddProfileBuilder.BuildDynamicChannels(eds.DynamicChannels));

        return appLayers;
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Calls virtual members via instance dispatch.")]
    private XElement BuildObjectList(ObjectDictionary dict)
    {
        var objList = new XElement("CANopenObjectList",
            new XAttribute("mandatoryObjects",
                dict.MandatoryObjects.Count.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("optionalObjects",
                dict.OptionalObjects.Count.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("manufacturerObjects",
                dict.ManufacturerObjects.Count.ToString(CultureInfo.InvariantCulture)));

        foreach (var obj in dict.Objects.OrderBy(o => o.Key).Select(o => o.Value))
        {
            objList.Add(BuildCanOpenObject(obj));
        }

        return objList;
    }

    /// <summary>
    /// Builds a CANopenObject XElement. Override in subclasses to add extra attributes (e.g. actualValue).
    /// </summary>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
        Justification = "Parameter name is a CANopen domain term, not a VB keyword conflict in context.")]
    protected virtual XElement BuildCanOpenObject(CanOpenObject obj)
    {
        var elem = new XElement("CANopenObject");

        elem.Add(new XAttribute("index", FormatIndex(obj.Index)));
        elem.Add(new XAttribute("name", obj.ParameterName));
        elem.Add(new XAttribute("objectType",
            obj.ObjectType.ToString(CultureInfo.InvariantCulture)));

        if (obj.DataType.HasValue)
            elem.Add(new XAttribute("dataType", FormatDataType(obj.DataType.Value)));

        // Only write accessType for objects with a data type (VAR-like)
        if (obj.DataType.HasValue)
            elem.Add(new XAttribute("accessType", XddAccessTypeToString(obj.AccessType)));

        if (!string.IsNullOrEmpty(obj.DefaultValue))
            elem.Add(new XAttribute("defaultValue", obj.DefaultValue));

        if (!string.IsNullOrEmpty(obj.LowLimit))
            elem.Add(new XAttribute("lowLimit", obj.LowLimit));

        if (!string.IsNullOrEmpty(obj.HighLimit))
            elem.Add(new XAttribute("highLimit", obj.HighLimit));

        if (obj.DataType.HasValue)
            elem.Add(new XAttribute("PDOmapping", obj.PdoMapping ? "optional" : "no"));

        if (obj.ObjFlags > 0)
            elem.Add(new XAttribute("objFlags",
                obj.ObjFlags.ToString(CultureInfo.InvariantCulture)));

        if (obj.SubNumber.HasValue)
            elem.Add(new XAttribute("subNumber",
                obj.SubNumber.Value.ToString(CultureInfo.InvariantCulture)));

        AddCanOpenObjectXdcAttributes(elem, obj);

        // Sub-objects
        foreach (var subObj in obj.SubObjects.OrderBy(s => s.Key).Select(s => s.Value))
        {
            elem.Add(BuildCanOpenSubObject(subObj));
        }

        return elem;
    }

    /// <summary>
    /// Hook for subclasses to add extra attributes (e.g. actualValue/denotation) to CANopenObject elements.
    /// </summary>
    protected virtual void AddCanOpenObjectXdcAttributes(XElement elem, CanOpenObject obj)
    {
        // Base implementation does nothing
    }

    /// <summary>
    /// Builds a CANopenSubObject XElement. Override in subclasses to add extra attributes.
    /// </summary>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
        Justification = "Parameter name is a CANopen domain term; VB conflict not applicable here.")]
    protected virtual XElement BuildCanOpenSubObject(CanOpenSubObject subObject)
    {
        var elem = new XElement("CANopenSubObject");

        elem.Add(new XAttribute("subIndex",
            subObject.SubIndex.ToString("X2", CultureInfo.InvariantCulture)));
        elem.Add(new XAttribute("name", subObject.ParameterName));
        elem.Add(new XAttribute("objectType",
            subObject.ObjectType.ToString(CultureInfo.InvariantCulture)));
        elem.Add(new XAttribute("dataType", FormatDataType(subObject.DataType)));
        elem.Add(new XAttribute("accessType", XddAccessTypeToString(subObject.AccessType)));

        if (!string.IsNullOrEmpty(subObject.DefaultValue))
            elem.Add(new XAttribute("defaultValue", subObject.DefaultValue));

        if (!string.IsNullOrEmpty(subObject.LowLimit))
            elem.Add(new XAttribute("lowLimit", subObject.LowLimit));

        if (!string.IsNullOrEmpty(subObject.HighLimit))
            elem.Add(new XAttribute("highLimit", subObject.HighLimit));

        elem.Add(new XAttribute("PDOmapping", subObject.PdoMapping ? "optional" : "no"));

        AddCanOpenSubObjectXdcAttributes(elem, subObject);

        return elem;
    }

    /// <summary>
    /// Hook for subclasses to add extra attributes to CANopenSubObject elements.
    /// </summary>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
        Justification = "Parameter name is a CANopen domain term; VB conflict not applicable here.")]
    protected virtual void AddCanOpenSubObjectXdcAttributes(XElement elem, CanOpenSubObject subObject)
    {
        // Base implementation does nothing
    }

    /// <summary>
    /// Builds the NetworkManagement element.
    /// Subclasses can override to inspect <paramref name="commissioning"/> and append
    /// a deviceCommissioning child when it is non-null.
    /// </summary>
    protected virtual XElement BuildNetworkManagement(ElectronicDataSheet eds, DeviceCommissioning? commissioning)
    {
        var networkMgmt = new XElement("NetworkManagement");
        networkMgmt.Add(XddProfileBuilder.BuildGeneralFeatures(eds.DeviceInfo));
        networkMgmt.Add(XddProfileBuilder.BuildMasterFeatures(eds.DeviceInfo));
        return networkMgmt;
    }

    // ── Protected format helpers (part of the extensibility API for subclasses) ──

    /// <summary>Formats a 16-bit index as 4 uppercase hex digits (e.g. "1000").</summary>
    protected static string FormatIndex(ushort index) =>
        XddFormatHelper.FormatIndex(index);

    /// <summary>Formats a data type as 4 uppercase hex digits (e.g. "0007").</summary>
    protected static string FormatDataType(ushort dataType) =>
        XddFormatHelper.FormatDataType(dataType);

    /// <summary>
    /// Converts an AccessType to XDD access type string.
    /// ReadWriteInput/ReadWriteOutput have no XDD equivalent → mapped to "rw".
    /// </summary>
    protected static string XddAccessTypeToString(AccessType accessType) =>
        XddFormatHelper.AccessTypeToString(accessType);

    /// <summary>Formats a baud rate in kbps as the XDD string form (e.g. "250 Kbps").</summary>
    protected static string FormatBaudRate(ushort kbps) =>
        XddFormatHelper.FormatBaudRate(kbps);

    // ── Serialization helpers ──────────────────────────────────────────────────

    private static T WriteContext<T>(string sectionName, Func<T> writeAction)
    {
        try
        {
            return writeAction();
        }
        catch (XddWriteException)
        {
            throw;
        }
        catch (XdcWriteException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new XddWriteException(
                $"Failed to write section [{sectionName}]",
                ex)
            {
                SectionName = sectionName
            };
        }
    }

    private static string SerializeDocument(XDocument doc)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            OmitXmlDeclaration = false
        };

        using var sb = new StringBuilderWriter();
        using (var writer = XmlWriter.Create(sb, settings))
        {
            doc.Save(writer);
        }

        return sb.ToString();
    }

    /// <summary>Helper to write XML to a StringBuilder.</summary>
    private sealed class StringBuilderWriter : System.IO.TextWriter
    {
        private readonly StringBuilder _sb = new();

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => _sb.Append(value);
        public override void Write(string? value) => _sb.Append(value);
        public override void Write(char[] buffer, int index, int count) =>
            _sb.Append(buffer, index, count);

        public override string ToString() => _sb.ToString();
    }

    private static void ThrowIfNull(object? value, string parameterName)
    {
        if (value == null)
            throw new ArgumentNullException(parameterName);
    }
}
