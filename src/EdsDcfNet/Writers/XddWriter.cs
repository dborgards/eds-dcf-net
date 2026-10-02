namespace EdsDcfNet.Writers;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
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
    /// <remarks>
    /// The content is written to a temporary file in the target directory and then moved or
    /// replaced over the target. On failure the target is left untouched and the temporary file
    /// is removed. Whether the final replace is atomic depends on the file system (for example,
    /// network shares may not guarantee it).
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="eds"/> is <see langword="null"/>.</exception>
    public void WriteFile(ElectronicDataSheet eds, string filePath)
    {
        ThrowIfNull(eds, nameof(eds));

        try
        {
            var doc = BuildOutputDocument(eds, commissioning: null);
            TextFileIo.WriteFileAtomic(filePath, stream => SerializeDocument(doc, stream));
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
    /// <exception cref="ArgumentNullException"><paramref name="eds"/> or <paramref name="stream"/> is <see langword="null"/>.</exception>
    public void WriteStream(ElectronicDataSheet eds, Stream stream)
    {
        ThrowIfNull(eds, nameof(eds));
        ThrowIfNull(stream, nameof(stream));
        if (!stream.CanWrite)
            throw new ArgumentException("Stream must be writable.", nameof(stream));

        try
        {
            var doc = BuildOutputDocument(eds, commissioning: null);
            SerializeDocument(doc, stream);
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
    /// <remarks>
    /// The content is written to a temporary file in the target directory and then moved or
    /// replaced over the target. On failure or cancellation the target is left untouched and the
    /// temporary file is removed. Whether the final replace is atomic depends on the file system
    /// (for example, network shares may not guarantee it).
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="eds"/> is <see langword="null"/>.</exception>
    public async Task WriteFileAsync(
        ElectronicDataSheet eds,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNull(eds, nameof(eds));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var doc = BuildOutputDocument(eds, commissioning: null);
            await TextFileIo.WriteFileAtomicAsync(
                filePath,
                stream => SerializeDocumentAsync(doc, stream, cancellationToken),
                cancellationToken).ConfigureAwait(false);
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
    /// <exception cref="ArgumentNullException"><paramref name="eds"/> or <paramref name="stream"/> is <see langword="null"/>.</exception>
    public async Task WriteStreamAsync(
        ElectronicDataSheet eds,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNull(eds, nameof(eds));
        ThrowIfNull(stream, nameof(stream));
        if (!stream.CanWrite)
            throw new ArgumentException("Stream must be writable.", nameof(stream));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var doc = BuildOutputDocument(eds, commissioning: null);
            await SerializeDocumentAsync(doc, stream, cancellationToken).ConfigureAwait(false);
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
    /// <exception cref="ArgumentNullException"><paramref name="eds"/> is <see langword="null"/>.</exception>
    public string GenerateString(ElectronicDataSheet eds)
    {
        ThrowIfNull(eds, nameof(eds));
        return GenerateString(eds, commissioning: null);
    }

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
    /// Builds the document for file and stream output. Failures are reported as
    /// <see cref="XddWriteException"/> for section <c>Document</c>, like <see cref="GenerateString(ElectronicDataSheet, DeviceCommissioning?)"/>.
    /// </summary>
    internal XDocument BuildOutputDocument(ElectronicDataSheet eds, DeviceCommissioning? commissioning)
        => WriteContext("Document", () => BuildDocument(eds, commissioning));

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
        // The resolver is ambient so protected virtual object builders keep their
        // signatures and can still see whether uniqueIDRef's parameter exists.
        using (XddUniqueIdResolver.EnterWriteScope(eds.ApplicationProcess))
        {
            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

            var container = new XElement("ISO15745ProfileContainer",
                new XAttribute(XNamespace.Xmlns + "xsi", xsi));

            container.Add(WriteContext("DeviceProfile", () => BuildDeviceProfile(eds, xsi)));
            container.Add(WriteContext("CommunicationNetworkProfile", () => BuildCommNetProfile(eds, xsi, commissioning)));

            return new XDocument(
                new XDeclaration("1.0", null, null),
                container);
        }
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
        var projection = XddUniqueIdResolver.CurrentWriteProjection(obj.UniqueIdRef);

        elem.Add(new XAttribute("index", FormatIndex(obj.Index)));
        elem.Add(new XAttribute("name", obj.ParameterName));
        elem.Add(new XAttribute("objectType",
            obj.ObjectType.ToString(CultureInfo.InvariantCulture)));

        if (obj.DataType.HasValue && !ReferenceSuppliesDataType(projection, obj.DataType))
            elem.Add(new XAttribute("dataType", FormatDataType(obj.DataType.Value)));

        // Omit accessType when the emitted uniqueIDRef still supplies it, so
        // readWriteInput/readWriteOutput are not collapsed to "rw". A noAccess
        // reference does not supply a CiA 306 access; the untouched ReadOnly fallback
        // is left off, while a caller-assigned or source accessType is written.
        // With the parameter gone, a resolved access is written even when the object
        // has no scalar data type (struct-backed RECORD).
        if (ShouldWriteAccessAttribute(
                projection,
                obj.AccessType,
                obj.AccessTypeSpecified,
                writeWhenUnspecified: obj.DataType.HasValue))
            elem.Add(new XAttribute("accessType", XddAccessTypeToString(obj.AccessType)));

        AddStringUnlessSupplied(elem, "defaultValue", obj.DefaultValue, projection, projection?.HasDefault == true, projection?.DefaultValue);
        AddStringUnlessSupplied(elem, "lowLimit", obj.LowLimit, projection, projection?.HasUnambiguousRange == true, projection?.LowLimit);
        AddStringUnlessSupplied(elem, "highLimit", obj.HighLimit, projection, projection?.HasUnambiguousRange == true, projection?.HighLimit);

        if (obj.DataType.HasValue)
            elem.Add(new XAttribute("PDOmapping", ToXddPdoMappingAttribute(obj.PdoMappingMode)));

        if (obj.ObjFlags > 0)
            elem.Add(new XAttribute("objFlags",
                obj.ObjFlags.ToString(CultureInfo.InvariantCulture)));

        AddUniqueIdRefAttribute(elem, obj.UniqueIdRef, projection);

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
        var projection = XddUniqueIdResolver.CurrentWriteProjection(subObject.UniqueIdRef);
        var subDataType = subObject.DataType == 0 ? (ushort?)null : subObject.DataType;

        elem.Add(new XAttribute("subIndex",
            subObject.SubIndex.ToString("X2", CultureInfo.InvariantCulture)));
        elem.Add(new XAttribute("name", subObject.ParameterName));
        elem.Add(new XAttribute("objectType",
            subObject.ObjectType.ToString(CultureInfo.InvariantCulture)));
        if (projection == null || !SameResolvedDataType(subDataType, projection.DataType))
            elem.Add(new XAttribute("dataType", FormatDataType(subObject.DataType)));
        if (ShouldWriteAccessAttribute(
                projection,
                subObject.AccessType,
                subObject.AccessTypeSpecified,
                writeWhenUnspecified: true))
            elem.Add(new XAttribute("accessType", XddAccessTypeToString(subObject.AccessType)));

        AddStringUnlessSupplied(elem, "defaultValue", subObject.DefaultValue, projection, projection?.HasDefault == true, projection?.DefaultValue);
        AddStringUnlessSupplied(elem, "lowLimit", subObject.LowLimit, projection, projection?.HasUnambiguousRange == true, projection?.LowLimit);
        AddStringUnlessSupplied(elem, "highLimit", subObject.HighLimit, projection, projection?.HasUnambiguousRange == true, projection?.HighLimit);

        elem.Add(new XAttribute("PDOmapping", ToXddPdoMappingAttribute(subObject.PdoMappingMode)));
        AddUniqueIdRefAttribute(elem, subObject.UniqueIdRef, projection);

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

    /// <summary>
    /// Writes <paramref name="modelValue"/> unless the emitted <c>uniqueIDRef</c> still
    /// supplies the same text. <paramref name="projection"/> is null when the reference
    /// is not emitted, and then the value is written as before.
    /// </summary>
    private static void AddStringUnlessSupplied(
        XElement elem,
        string attributeName,
        string? modelValue,
        ParameterProjection? projection,
        bool supplied,
        string? projectedValue)
    {
        if (string.IsNullOrEmpty(modelValue))
            return;

        if (projection != null
            && supplied
            && string.Equals(projectedValue, modelValue, StringComparison.Ordinal))
            return;

        elem.Add(new XAttribute(attributeName, modelValue));
    }

    private static bool ReferenceSuppliesDataType(ParameterProjection? projection, ushort? model) =>
        projection != null && SameResolvedDataType(model, projection.DataType);

    private static bool ReferenceSuppliesAccess(ParameterProjection? projection, AccessType access) =>
        projection != null
        && projection.AccessKind == AccessProjectionKind.Mapped
        && projection.MappedAccess == access;

    /// <summary>
    /// Decides whether <c>accessType</c> is written.
    /// <paramref name="writeWhenUnspecified"/> keeps the historical rule: VAR objects
    /// (those with a data type) and every sub-object emit access even without a reference,
    /// while a complex object does not.
    /// </summary>
    private static bool ShouldWriteAccessAttribute(
        ParameterProjection? projection,
        AccessType access,
        bool accessSpecified,
        bool writeWhenUnspecified)
    {
        if (ReferenceSuppliesAccess(projection, access))
            return false;

        // noAccess and any other unmapped access leave the fallback in place.
        // Writing that fallback would become an explicit attribute and win next time.
        if (projection != null && projection.AccessKind != AccessProjectionKind.Mapped)
            return accessSpecified;

        if (projection == null)
            return writeWhenUnspecified || accessSpecified;

        return true;
    }

    /// <summary>
    /// Treats <c>0</c> and <see langword="null"/> as "no CANopen data type" so a sub-object
    /// whose reference has no scalar type is not rewritten as <c>0000</c> while the
    /// reference is still emitted.
    /// </summary>
    private static bool SameResolvedDataType(ushort? model, ushort? projected)
    {
        var normalizedModel = model.GetValueOrDefault() == 0 ? null : model;
        return normalizedModel == projected;
    }

    private static void AddUniqueIdRefAttribute(XElement elem, string? uniqueIdRef, ParameterProjection? projection)
    {
        if (projection == null || string.IsNullOrEmpty(uniqueIdRef))
            return;

        elem.Add(new XAttribute("uniqueIDRef", uniqueIdRef));
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

    private static XmlWriterSettings CreateWriterSettings(bool async) => new()
    {
        Indent = true,
        IndentChars = "  ",
        Encoding = TextFileIo.GetOutputEncoding(),
        OmitXmlDeclaration = false,
        CloseOutput = false,
        Async = async
    };

    /// <summary>String route (<see cref="GenerateString(ElectronicDataSheet)"/>): the declaration follows <see cref="TextFileIo.GetOutputEncoding"/>.</summary>
    private static string SerializeDocument(XDocument doc)
    {
        using var sb = new StringBuilderWriter();
        using (var writer = XmlWriter.Create(sb, CreateWriterSettings(async: false)))
        {
            doc.Save(writer);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Stream route shared by XDD and XDC. The <see cref="XmlWriter"/> serializes into a buffer
    /// with the encoding from <see cref="TextFileIo.GetOutputEncoding"/> (it emits the matching
    /// declaration itself and keeps its XML context, so characters the encoding cannot represent
    /// are still escaped). The buffer is copied to <paramref name="stream"/> only after that
    /// succeeds, so a content error such as an invalid XML character leaves the stream unchanged.
    /// The bytes match <see cref="SerializeDocument(XDocument)"/>. The stream stays open.
    /// </summary>
    internal static void SerializeDocument(XDocument doc, Stream stream)
    {
        var bytes = SerializeToBuffer(doc);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    /// <summary>Asynchronous variant of <see cref="SerializeDocument(XDocument, Stream)"/>.</summary>
    internal static async Task SerializeDocumentAsync(XDocument doc, Stream stream, CancellationToken cancellationToken)
    {
        var bytes = SerializeToBuffer(doc);
        cancellationToken.ThrowIfCancellationRequested();
#if NET10_0_OR_GREATER
        await stream.WriteAsync(bytes.AsMemory(), cancellationToken).ConfigureAwait(false);
#else
        await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
#endif
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Fully serializes <paramref name="doc"/> in memory. <see cref="ArgumentException"/> from
    /// <see cref="XmlWriter"/> (invalid characters and similar content errors) is reported as a
    /// document-section failure and never reaches the caller stream.
    /// </summary>
    private static byte[] SerializeToBuffer(XDocument doc)
    {
        try
        {
            using var buffer = new MemoryStream();
            using (var writer = XmlWriter.Create(buffer, CreateWriterSettings(async: false)))
            {
                doc.Save(writer);
            }

            return buffer.ToArray();
        }
        catch (ArgumentException ex)
        {
            throw CreateDocumentException(ex);
        }
    }

    private static XddWriteException CreateDocumentException(Exception inner)
        => new("Failed to write section [Document]", inner)
        {
            SectionName = "Document"
        };

    /// <summary>Helper to write XML to a StringBuilder.</summary>
    private sealed class StringBuilderWriter : System.IO.TextWriter
    {
        private readonly StringBuilder _sb = new();

        public override Encoding Encoding => TextFileIo.GetOutputEncoding();

        public override void Write(char value) => _sb.Append(value);
        public override void Write(string? value) => _sb.Append(value);
        public override void Write(char[] buffer, int index, int count) =>
            _sb.Append(buffer, index, count);

        public override string ToString() => _sb.ToString();
    }

    private static string ToXddPdoMappingAttribute(PdoMappingMode mode) => mode switch
    {
        PdoMappingMode.No => "no",
        PdoMappingMode.Default => "default",
        PdoMappingMode.Optional => "optional",
        PdoMappingMode.Tpdo => "TPDO",
        PdoMappingMode.Rpdo => "RPDO",
        _ => "no"
    };

    private static void ThrowIfNull(object? value, string parameterName)
    {
        if (value == null)
            throw new ArgumentNullException(parameterName);
    }
}
