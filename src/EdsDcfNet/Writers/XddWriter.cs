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

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Calls virtual members via instance dispatch.")]
    private XElement BuildDeviceProfile(ElectronicDataSheet eds, XNamespace xsi)
    {
        var profileBody = new XElement("ProfileBody",
            new XAttribute(xsi + "type", "ProfileBody_Device_CANopen"));

        AddFileInfoAttributes(profileBody, eds.FileInfo);

        // DeviceIdentity
        profileBody.Add(BuildDeviceIdentity(eds.DeviceInfo));

        // DeviceManager (empty)
        profileBody.Add(new XElement("DeviceManager"));

        // DeviceFunction (empty)
        profileBody.Add(new XElement("DeviceFunction"));

        // ApplicationProcess
        if (eds.ApplicationProcess != null)
            profileBody.Add(BuildApplicationProcess(eds.ApplicationProcess));

        return BuildProfile("Device", profileBody);
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Calls virtual members via instance dispatch.")]
    private XElement BuildCommNetProfile(ElectronicDataSheet eds, XNamespace xsi, DeviceCommissioning? commissioning)
    {
        var profileBody = new XElement("ProfileBody",
            new XAttribute(xsi + "type", "ProfileBody_CommunicationNetwork_CANopen"));

        AddFileInfoAttributes(profileBody, eds.FileInfo);

        // ApplicationLayers
        profileBody.Add(BuildApplicationLayers(eds));

        // TransportLayers
        profileBody.Add(BuildTransportLayers(eds.DeviceInfo));

        // NetworkManagement
        profileBody.Add(BuildNetworkManagement(eds, commissioning));

        return BuildProfile("CommunicationNetwork", profileBody);
    }

    private static XElement BuildProfile(string classId, XElement profileBody)
    {
        return new XElement("ISO15745Profile",
            new XElement("ProfileHeader",
                new XElement("ProfileIdentification", string.Empty),
                new XElement("ProfileRevision", "1"),
                new XElement("ProfileName", string.Empty),
                new XElement("ProfileSource", string.Empty),
                new XElement("ProfileClassID", classId),
                new XElement("ISO15745Reference",
                    new XElement("ISO15745Part", "1"),
                    new XElement("ISO15745Edition", "1"),
                    new XElement("ProfileTechnology", "CANopen"))),
            profileBody);
    }

    private static void AddFileInfoAttributes(XElement profileBody, EdsFileInfo fileInfo)
    {
        if (!string.IsNullOrEmpty(fileInfo.FileName))
            profileBody.Add(new XAttribute("fileName", fileInfo.FileName));

        if (!string.IsNullOrEmpty(fileInfo.CreatedBy))
            profileBody.Add(new XAttribute("fileCreator", fileInfo.CreatedBy));

        // Convert EDS date "MM-DD-YYYY" to XSD date "YYYY-MM-DD"
        var xsdCreationDate = ConvertEdsDateToXsd(fileInfo.CreationDate);
        if (!string.IsNullOrEmpty(xsdCreationDate))
            profileBody.Add(new XAttribute("fileCreationDate", xsdCreationDate));

        if (!string.IsNullOrEmpty(fileInfo.CreationTime))
            profileBody.Add(new XAttribute("fileCreationTime", fileInfo.CreationTime));

        profileBody.Add(new XAttribute("fileVersion",
            fileInfo.FileVersion.ToString(CultureInfo.InvariantCulture)));

        var xsdModDate = ConvertEdsDateToXsd(fileInfo.ModificationDate);
        if (!string.IsNullOrEmpty(xsdModDate))
            profileBody.Add(new XAttribute("fileModificationDate", xsdModDate));

        if (!string.IsNullOrEmpty(fileInfo.ModificationTime))
            profileBody.Add(new XAttribute("fileModificationTime", fileInfo.ModificationTime));

        if (!string.IsNullOrEmpty(fileInfo.ModifiedBy))
            profileBody.Add(new XAttribute("fileModifiedBy", fileInfo.ModifiedBy));
    }

    private static XElement BuildDeviceIdentity(DeviceInfo deviceInfo)
    {
        return new XElement("DeviceIdentity",
            new XElement("vendorName", deviceInfo.VendorName),
            new XElement("vendorID",
                string.Format(CultureInfo.InvariantCulture, "0x{0:X8}", deviceInfo.VendorNumber)),
            new XElement("productName", deviceInfo.ProductName),
            new XElement("productID",
                string.Format(CultureInfo.InvariantCulture, "0x{0:X8}", deviceInfo.ProductNumber)));
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Calls virtual members via instance dispatch.")]
    private XElement BuildApplicationLayers(ElectronicDataSheet eds)
    {
        var appLayers = new XElement("ApplicationLayers");

        // CANopenObjectList
        appLayers.Add(BuildObjectList(eds.ObjectDictionary));

        // dummyUsage
        if (eds.ObjectDictionary.DummyUsage.Count > 0)
            appLayers.Add(BuildDummyUsage(eds.ObjectDictionary));

        // dynamicChannels
        if (eds.DynamicChannels != null && eds.DynamicChannels.Segments.Count > 0)
            appLayers.Add(BuildDynamicChannels(eds.DynamicChannels));

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

    private static XElement BuildDummyUsage(ObjectDictionary dict)
    {
        var dummyElem = new XElement("dummyUsage");

        foreach (var kvp in dict.DummyUsage.OrderBy(d => d.Key))
        {
            dummyElem.Add(new XElement("dummy",
                new XAttribute("entry",
                    string.Format(CultureInfo.InvariantCulture,
                        "Dummy{0:X4}={1}", kvp.Key, kvp.Value ? "1" : "0"))));
        }

        return dummyElem;
    }

    private static XElement BuildDynamicChannels(DynamicChannels channels)
    {
        var dynElem = new XElement("dynamicChannels");

        foreach (var seg in channels.Segments)
        {
            var chanElem = new XElement("dynamicChannel",
                new XAttribute("dataType", FormatDataType(seg.Type)),
                new XAttribute("accessType", XddAccessTypeToString(seg.Dir)));

            // Parse Range back to startIndex/endIndex
            var rangeParts = seg.Range.Split('-');
            if (rangeParts.Length >= 1)
                chanElem.Add(new XAttribute("startIndex", rangeParts[0].Trim()));
            if (rangeParts.Length >= 2)
                chanElem.Add(new XAttribute("endIndex", rangeParts[1].Trim()));

            if (seg.PPOffset > 0)
                chanElem.Add(new XAttribute("pDOmappingIndex",
                    seg.PPOffset.ToString(CultureInfo.InvariantCulture)));

            dynElem.Add(chanElem);
        }

        return dynElem;
    }

    private static XElement BuildTransportLayers(DeviceInfo deviceInfo)
    {
        var defaultBaudRate = GetDefaultBaudRateString(deviceInfo.SupportedBaudRates);

        var baudRateElem = new XElement("baudRate",
            new XAttribute("defaultValue", defaultBaudRate));

        var hasSupported = false;
        foreach (var kbps in GetSupportedBaudRates(deviceInfo.SupportedBaudRates))
        {
            hasSupported = true;
            baudRateElem.Add(new XElement("supportedBaudRate",
                new XAttribute("value", FormatBaudRate(kbps))));
        }

        if (!hasSupported)
        {
            // No baud-rate flags set: emit the fallback default as a supported entry
            // so the XML is self-consistent (defaultValue must appear in supportedBaudRate).
            baudRateElem.Add(new XElement("supportedBaudRate",
                new XAttribute("value", defaultBaudRate)));
        }

        return new XElement("TransportLayers",
            new XElement("PhysicalLayer",
                baudRateElem));
    }

    /// <summary>
    /// Builds the NetworkManagement element.
    /// Subclasses can override to inspect <paramref name="commissioning"/> and append
    /// a deviceCommissioning child when it is non-null.
    /// </summary>
    protected virtual XElement BuildNetworkManagement(ElectronicDataSheet eds, DeviceCommissioning? commissioning)
    {
        var networkMgmt = new XElement("NetworkManagement");
        networkMgmt.Add(BuildGeneralFeatures(eds.DeviceInfo));
        networkMgmt.Add(BuildMasterFeatures(eds.DeviceInfo));
        return networkMgmt;
    }

    private static XElement BuildGeneralFeatures(DeviceInfo deviceInfo)
    {
        return new XElement("CANopenGeneralFeatures",
            new XAttribute("granularity",
                deviceInfo.Granularity.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("nrOfRxPDO",
                deviceInfo.NrOfRxPdo.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("nrOfTxPDO",
                deviceInfo.NrOfTxPdo.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("bootUpSlave",
                deviceInfo.SimpleBootUpSlave ? "true" : "false"),
            new XAttribute("layerSettingServiceSlave",
                deviceInfo.LssSupported ? "true" : "false"),
            new XAttribute("groupMessaging",
                deviceInfo.GroupMessaging ? "true" : "false"),
            new XAttribute("dynamicChannels",
                deviceInfo.DynamicChannelsSupported.ToString(CultureInfo.InvariantCulture)));
    }

    private static XElement BuildMasterFeatures(DeviceInfo deviceInfo)
    {
        return new XElement("CANopenMasterFeatures",
            new XAttribute("bootUpMaster",
                deviceInfo.SimpleBootUpMaster ? "true" : "false"));
    }

    // ── ApplicationProcess ────────────────────────────────────────────────────

    private static XElement BuildApplicationProcess(ApplicationProcess ap)
    {
        var elem = new XElement("ApplicationProcess");

        if (ap.DataTypeList != null && !ap.DataTypeList.IsEmpty)
            elem.Add(BuildDataTypeList(ap.DataTypeList));

        if (ap.FunctionTypeList.Count > 0)
        {
            var ftlElem = new XElement("functionTypeList");
            foreach (var ft in ap.FunctionTypeList)
                ftlElem.Add(BuildFunctionType(ft));
            elem.Add(ftlElem);
        }

        if (ap.FunctionInstanceList != null)
            elem.Add(BuildFunctionInstanceList(ap.FunctionInstanceList));

        if (ap.TemplateList != null)
            elem.Add(BuildTemplateList(ap.TemplateList));

        // parameterList is mandatory per DS311 §6.4.5 when ApplicationProcess is present
        var plElem = new XElement("parameterList");
        foreach (var p in ap.ParameterList)
            plElem.Add(BuildParameter(p));
        elem.Add(plElem);

        if (ap.ParameterGroupList.Count > 0)
        {
            var pglElem = new XElement("parameterGroupList");
            foreach (var pg in ap.ParameterGroupList)
                pglElem.Add(BuildParameterGroup(pg));
            elem.Add(pglElem);
        }

        return elem;
    }

    // ── dataTypeList ──────────────────────────────────────────────────────────

    private static XElement BuildDataTypeList(ApDataTypeList list)
    {
        var elem = new XElement("dataTypeList");

        foreach (var a in list.Arrays)
            elem.Add(BuildArrayType(a));
        foreach (var s in list.Structs)
            elem.Add(BuildStructType(s));
        foreach (var e in list.Enums)
            elem.Add(BuildEnumType(e));
        foreach (var d in list.Derived)
            elem.Add(BuildDerivedType(d));

        return elem;
    }

    private static XElement BuildArrayType(ApArrayType array)
    {
        var elem = new XElement("array",
            new XAttribute("name", array.Name),
            new XAttribute("uniqueID", array.UniqueId));

        ApAddLabelGroup(elem, array.LabelGroup);

        foreach (var sr in array.Subranges)
            elem.Add(new XElement("subrange",
                new XAttribute("lowerLimit", sr.LowerLimit.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("upperLimit", sr.UpperLimit.ToString(CultureInfo.InvariantCulture))));

        ApAddTypeRef(elem, array.ElementType);
        return elem;
    }

    private static XElement BuildStructType(ApStructType st)
    {
        var elem = new XElement("struct",
            new XAttribute("name", st.Name),
            new XAttribute("uniqueID", st.UniqueId));

        ApAddLabelGroup(elem, st.LabelGroup);

        foreach (var vd in st.VarDeclarations)
            elem.Add(BuildVarDeclaration(vd));

        return elem;
    }

    private static XElement BuildEnumType(ApEnumType en)
    {
        var elem = new XElement("enum",
            new XAttribute("name", en.Name),
            new XAttribute("uniqueID", en.UniqueId));

        if (!string.IsNullOrEmpty(en.Size))
            elem.Add(new XAttribute("size", en.Size));

        ApAddLabelGroup(elem, en.LabelGroup);

        if (!string.IsNullOrEmpty(en.SimpleTypeName))
            elem.Add(new XElement(en.SimpleTypeName));

        foreach (var ev in en.EnumValues)
        {
            var evElem = new XElement("enumValue");
            if (ev.Value != null)
                evElem.Add(new XAttribute("value", ev.Value));
            ApAddLabelGroup(evElem, ev.LabelGroup);
            elem.Add(evElem);
        }

        return elem;
    }

    private static XElement BuildDerivedType(ApDerivedType dt)
    {
        var elem = new XElement("derived",
            new XAttribute("name", dt.Name),
            new XAttribute("uniqueID", dt.UniqueId));

        ApAddLabelGroup(elem, dt.LabelGroup);

        if (dt.Count != null)
            elem.Add(BuildDerivedCount(dt.Count));

        ApAddTypeRef(elem, dt.BaseType);
        return elem;
    }

    private static XElement BuildDerivedCount(ApDerivedCount c)
    {
        var elem = new XElement("count",
            new XAttribute("uniqueID", c.UniqueId));

        if (c.Access != "read")
            elem.Add(new XAttribute("access", c.Access));

        ApAddLabelGroup(elem, c.LabelGroup);

        if (c.DefaultValue != null)
            elem.Add(BuildParameterValueElem("defaultValue", c.DefaultValue));

        if (c.AllowedValues != null)
            elem.Add(BuildAllowedValues(c.AllowedValues));

        return elem;
    }

    private static XElement BuildVarDeclaration(ApVarDeclaration vd)
    {
        var elem = new XElement("varDeclaration",
            new XAttribute("name", vd.Name),
            new XAttribute("uniqueID", vd.UniqueId));

        if (!string.IsNullOrEmpty(vd.Start))
            elem.Add(new XAttribute("start", vd.Start));
        if (!string.IsNullOrEmpty(vd.Size))
            elem.Add(new XAttribute("size", vd.Size));
        if (vd.IsSigned.HasValue)
            elem.Add(new XAttribute("signed", vd.IsSigned.Value ? "true" : "false"));
        if (!string.IsNullOrEmpty(vd.Offset))
            elem.Add(new XAttribute("offset", vd.Offset));
        if (!string.IsNullOrEmpty(vd.Multiplier))
            elem.Add(new XAttribute("multiplier", vd.Multiplier));
        if (!string.IsNullOrEmpty(vd.InitialValue))
            elem.Add(new XAttribute("initialValue", vd.InitialValue));

        ApAddLabelGroup(elem, vd.LabelGroup);
        ApAddTypeRef(elem, vd.Type);

        foreach (var cs in vd.ConditionalSupports)
            elem.Add(new XElement("conditionalSupport",
                new XAttribute("paramIDRef", cs)));

        if (vd.DefaultValue != null)
            elem.Add(BuildParameterValueElem("defaultValue", vd.DefaultValue));

        if (vd.AllowedValues != null)
            elem.Add(BuildAllowedValues(vd.AllowedValues));

        if (vd.Unit != null)
            elem.Add(BuildUnit(vd.Unit));

        return elem;
    }

    // ── functionTypeList ──────────────────────────────────────────────────────

    private static XElement BuildFunctionType(ApFunctionType ft)
    {
        var elem = new XElement("functionType",
            new XAttribute("name", ft.Name),
            new XAttribute("uniqueID", ft.UniqueId));

        if (!string.IsNullOrEmpty(ft.Package))
            elem.Add(new XAttribute("package", ft.Package));

        ApAddLabelGroup(elem, ft.LabelGroup);

        foreach (var vi in ft.VersionInfos)
            elem.Add(BuildVersionInfo(vi));

        if (ft.InterfaceList != null)
            elem.Add(BuildInterfaceList(ft.InterfaceList));

        if (ft.FunctionInstanceList != null)
            elem.Add(BuildFunctionInstanceList(ft.FunctionInstanceList));

        return elem;
    }

    private static XElement BuildVersionInfo(ApVersionInfo vi)
    {
        var elem = new XElement("versionInfo",
            new XAttribute("organization", vi.Organization),
            new XAttribute("version", vi.Version),
            new XAttribute("author", vi.Author),
            new XAttribute("date", vi.Date));

        ApAddLabelGroup(elem, vi.LabelGroup);
        return elem;
    }

    private static XElement BuildInterfaceList(ApInterfaceList il)
    {
        var elem = new XElement("interfaceList");

        if (il.InputVars.Count > 0)
        {
            var iv = new XElement("inputVars");
            foreach (var vd in il.InputVars)
                iv.Add(BuildVarDeclaration(vd));
            elem.Add(iv);
        }

        if (il.OutputVars.Count > 0)
        {
            var ov = new XElement("outputVars");
            foreach (var vd in il.OutputVars)
                ov.Add(BuildVarDeclaration(vd));
            elem.Add(ov);
        }

        if (il.ConfigVars.Count > 0)
        {
            var cv = new XElement("configVars");
            foreach (var vd in il.ConfigVars)
                cv.Add(BuildVarDeclaration(vd));
            elem.Add(cv);
        }

        return elem;
    }

    // ── functionInstanceList ──────────────────────────────────────────────────

    private static XElement BuildFunctionInstanceList(ApFunctionInstanceList fil)
    {
        var elem = new XElement("functionInstanceList");

        foreach (var fi in fil.FunctionInstances)
        {
            var fiElem = new XElement("functionInstance",
                new XAttribute("name", fi.Name),
                new XAttribute("uniqueID", fi.UniqueId),
                new XAttribute("typeIDRef", fi.TypeIdRef));
            ApAddLabelGroup(fiElem, fi.LabelGroup);
            elem.Add(fiElem);
        }

        foreach (var conn in fil.Connections)
        {
            var connElem = new XElement("connection",
                new XAttribute("source", conn.Source),
                new XAttribute("destination", conn.Destination));
            if (!string.IsNullOrEmpty(conn.Description))
                connElem.Add(new XAttribute("description", conn.Description));
            elem.Add(connElem);
        }

        return elem;
    }

    // ── templateList ──────────────────────────────────────────────────────────

    private static XElement BuildTemplateList(ApTemplateList tl)
    {
        var elem = new XElement("templateList");

        foreach (var pt in tl.ParameterTemplates)
            elem.Add(BuildParameterTemplate(pt));

        foreach (var avt in tl.AllowedValuesTemplates)
        {
            var avtElem = new XElement("allowedValuesTemplate",
                new XAttribute("uniqueID", avt.UniqueId));
            BuildAllowedValuesContent(avtElem, avt.Values, avt.Ranges);
            elem.Add(avtElem);
        }

        return elem;
    }

    private static XElement BuildParameterTemplate(ApParameterTemplate pt)
    {
        var elem = new XElement("parameterTemplate",
            new XAttribute("uniqueID", pt.UniqueId));

        if (pt.Access != "read")
            elem.Add(new XAttribute("access", pt.Access));
        if (!string.IsNullOrEmpty(pt.AccessList))
            elem.Add(new XAttribute("accessList", pt.AccessList));
        if (!string.IsNullOrEmpty(pt.Support))
            elem.Add(new XAttribute("support", pt.Support));
        if (pt.Persistent)
            elem.Add(new XAttribute("persistent", "true"));
        if (!string.IsNullOrEmpty(pt.Offset))
            elem.Add(new XAttribute("offset", pt.Offset));
        if (!string.IsNullOrEmpty(pt.Multiplier))
            elem.Add(new XAttribute("multiplier", pt.Multiplier));

        ApAddLabelGroup(elem, pt.LabelGroup);
        ApAddTypeRef(elem, pt.TypeRef);

        foreach (var cs in pt.ConditionalSupports)
            elem.Add(new XElement("conditionalSupport",
                new XAttribute("paramIDRef", cs)));

        if (pt.ActualValue != null)
            elem.Add(BuildParameterValueElem("actualValue", pt.ActualValue));
        if (pt.DefaultValue != null)
            elem.Add(BuildParameterValueElem("defaultValue", pt.DefaultValue));
        if (pt.SubstituteValue != null)
            elem.Add(BuildParameterValueElem("substituteValue", pt.SubstituteValue));
        if (pt.AllowedValues != null)
            elem.Add(BuildAllowedValues(pt.AllowedValues));
        if (pt.Unit != null)
            elem.Add(BuildUnit(pt.Unit));

        foreach (var prop in pt.Properties)
            elem.Add(new XElement("property",
                new XAttribute("name", prop.Name),
                new XAttribute("value", prop.Value)));

        return elem;
    }

    // ── parameterList ─────────────────────────────────────────────────────────

    private static XElement BuildParameter(ApParameter p)
    {
        var elem = new XElement("parameter",
            new XAttribute("uniqueID", p.UniqueId));

        if (p.Access != "read")
            elem.Add(new XAttribute("access", p.Access));
        if (!string.IsNullOrEmpty(p.AccessList))
            elem.Add(new XAttribute("accessList", p.AccessList));
        if (!string.IsNullOrEmpty(p.Support))
            elem.Add(new XAttribute("support", p.Support));
        if (p.Persistent)
            elem.Add(new XAttribute("persistent", "true"));
        if (!string.IsNullOrEmpty(p.Offset))
            elem.Add(new XAttribute("offset", p.Offset));
        if (!string.IsNullOrEmpty(p.Multiplier))
            elem.Add(new XAttribute("multiplier", p.Multiplier));
        if (!string.IsNullOrEmpty(p.TemplateIdRef))
            elem.Add(new XAttribute("templateIDRef", p.TemplateIdRef));

        ApAddLabelGroup(elem, p.LabelGroup);
        ApAddTypeRef(elem, p.TypeRef);

        foreach (var vr in p.VariableRefs)
            elem.Add(BuildVariableRef(vr));

        foreach (var cs in p.ConditionalSupports)
            elem.Add(new XElement("conditionalSupport",
                new XAttribute("paramIDRef", cs)));

        if (p.Denotation != null && !p.Denotation.IsEmpty)
        {
            var den = new XElement("denotation");
            ApAddLabelGroup(den, p.Denotation);
            elem.Add(den);
        }

        if (p.ActualValue != null)
            elem.Add(BuildParameterValueElem("actualValue", p.ActualValue));
        if (p.DefaultValue != null)
            elem.Add(BuildParameterValueElem("defaultValue", p.DefaultValue));
        if (p.SubstituteValue != null)
            elem.Add(BuildParameterValueElem("substituteValue", p.SubstituteValue));
        if (p.AllowedValues != null)
            elem.Add(BuildAllowedValues(p.AllowedValues));
        if (p.Unit != null)
            elem.Add(BuildUnit(p.Unit));

        foreach (var prop in p.Properties)
            elem.Add(new XElement("property",
                new XAttribute("name", prop.Name),
                new XAttribute("value", prop.Value)));

        return elem;
    }

    private static XElement BuildVariableRef(ApVariableRef vr)
    {
        var elem = new XElement("variableRef");

        if (vr.Position != 1)
            elem.Add(new XAttribute("position",
                vr.Position.ToString(CultureInfo.InvariantCulture)));

        foreach (var iref in vr.InstanceIdRefs)
            elem.Add(new XElement("instanceIDRef",
                new XAttribute("uniqueIDRef", iref)));

        if (!string.IsNullOrEmpty(vr.VariableIdRef))
            elem.Add(new XElement("variableIDRef",
                new XAttribute("uniqueIDRef", vr.VariableIdRef)));

        if (vr.MemberRef != null)
        {
            var mrElem = new XElement("memberRef");
            if (!string.IsNullOrEmpty(vr.MemberRef.UniqueIdRef))
                mrElem.Add(new XAttribute("uniqueIDRef", vr.MemberRef.UniqueIdRef));
            if (vr.MemberRef.Index.HasValue)
                mrElem.Add(new XAttribute("index",
                    vr.MemberRef.Index.Value.ToString(CultureInfo.InvariantCulture)));
            elem.Add(mrElem);
        }

        return elem;
    }

    // ── parameterGroupList ────────────────────────────────────────────────────

    private static XElement BuildParameterGroup(ApParameterGroup pg)
    {
        var elem = new XElement("parameterGroup",
            new XAttribute("uniqueID", pg.UniqueId));

        if (!string.IsNullOrEmpty(pg.KindOfAccess))
            elem.Add(new XAttribute("kindOfAccess", pg.KindOfAccess));

        ApAddLabelGroup(elem, pg.LabelGroup);

        foreach (var pref in pg.ParameterRefs)
            elem.Add(new XElement("parameterRef",
                new XAttribute("uniqueIDRef", pref)));

        foreach (var sub in pg.SubGroups)
            elem.Add(BuildParameterGroup(sub));

        return elem;
    }

    // ── Shared value element builders ─────────────────────────────────────────

    private static XElement BuildParameterValueElem(string name, ApParameterValue pv)
    {
        var elem = new XElement(name,
            new XAttribute("value", pv.Value));

        if (!string.IsNullOrEmpty(pv.Offset))
            elem.Add(new XAttribute("offset", pv.Offset));
        if (!string.IsNullOrEmpty(pv.Multiplier))
            elem.Add(new XAttribute("multiplier", pv.Multiplier));

        ApAddLabelGroup(elem, pv.LabelGroup);
        return elem;
    }

    private static XElement BuildAllowedValues(ApAllowedValues av)
    {
        var elem = new XElement("allowedValues");

        if (!string.IsNullOrEmpty(av.TemplateIdRef))
            elem.Add(new XAttribute("templateIDRef", av.TemplateIdRef));

        BuildAllowedValuesContent(elem, av.Values, av.Ranges);
        return elem;
    }

    private static void BuildAllowedValuesContent(
        XElement elem, List<ApParameterValue> values, List<ApAllowedRange> ranges)
    {
        foreach (var v in values)
            elem.Add(BuildParameterValueElem("value", v));

        foreach (var r in ranges)
        {
            var rangeElem = new XElement("range");
            if (r.MinValue != null)
                rangeElem.Add(BuildParameterValueElem("minValue", r.MinValue));
            if (r.MaxValue != null)
                rangeElem.Add(BuildParameterValueElem("maxValue", r.MaxValue));
            if (r.Step != null)
                rangeElem.Add(BuildParameterValueElem("step", r.Step));
            elem.Add(rangeElem);
        }
    }

    private static XElement BuildUnit(ApUnit u)
    {
        var elem = new XElement("unit",
            new XAttribute("multiplier", u.Multiplier));

        if (!string.IsNullOrEmpty(u.UnitUri))
            elem.Add(new XAttribute("unitURI", u.UnitUri));

        ApAddLabelGroup(elem, u.LabelGroup);
        return elem;
    }

    // ── g_labels helpers ──────────────────────────────────────────────────────

    private static void ApAddLabelGroup(XElement elem, ApLabelGroup group)
    {
        if (group.IsEmpty)
            return;

        foreach (var lbl in group.Labels)
            elem.Add(new XElement("label",
                new XAttribute("lang", lbl.Lang),
                lbl.Text));

        foreach (var desc in group.Descriptions)
        {
            var descElem = new XElement("description",
                new XAttribute("lang", desc.Lang),
                desc.Text);
            if (!string.IsNullOrEmpty(desc.Uri))
                descElem.Add(new XAttribute("URI", desc.Uri));
            elem.Add(descElem);
        }

        foreach (var tref in group.TextRefs)
        {
            var refName = tref.IsDescriptionRef ? "descriptionRef" : "labelRef";
            var refElem = new XElement(refName,
                new XAttribute("dictID", tref.DictId),
                new XAttribute("textID", tref.TextId));
            if (!string.IsNullOrEmpty(tref.Uri))
                refElem.Add(tref.Uri);
            elem.Add(refElem);
        }
    }

    private static void ApAddTypeRef(XElement elem, ApTypeRef? typeRef)
    {
        if (typeRef == null)
            return;

        if (!string.IsNullOrEmpty(typeRef.SimpleTypeName))
            elem.Add(new XElement(typeRef.SimpleTypeName));
        else if (!string.IsNullOrEmpty(typeRef.DataTypeIdRef))
            elem.Add(new XElement("dataTypeIDRef",
                new XAttribute("uniqueIDRef", typeRef.DataTypeIdRef)));
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Formats a 16-bit index as 4 uppercase hex digits (e.g. "1000").</summary>
    protected static string FormatIndex(ushort index) =>
        index.ToString("X4", CultureInfo.InvariantCulture);

    /// <summary>Formats a data type as 4 uppercase hex digits (e.g. "0007").</summary>
    protected static string FormatDataType(ushort dataType) =>
        dataType.ToString("X4", CultureInfo.InvariantCulture);

    /// <summary>
    /// Converts an AccessType to XDD access type string.
    /// ReadWriteInput/ReadWriteOutput have no XDD equivalent → mapped to "rw".
    /// </summary>
    protected static string XddAccessTypeToString(AccessType accessType) =>
        accessType switch
        {
            AccessType.Constant => "const",
            AccessType.ReadOnly => "ro",
            AccessType.WriteOnly => "wo",
            AccessType.ReadWrite => "rw",
            AccessType.ReadWriteInput => "rw",
            AccessType.ReadWriteOutput => "rw",
            _ => "rw"
        };

    private static string ConvertEdsDateToXsd(string edsDate)
    {
        if (string.IsNullOrEmpty(edsDate))
            return string.Empty;

        // EDS date: "MM-DD-YYYY" → XSD date: "YYYY-MM-DD"
        if (edsDate.Length >= 10 &&
            edsDate[2] == '-' && edsDate[5] == '-')
        {
            var month = edsDate.Substring(0, 2);
            var day = edsDate.Substring(3, 2);
            var year = edsDate.Substring(6, 4);
            return string.Format(CultureInfo.InvariantCulture, "{0}-{1}-{2}", year, month, day);
        }

        return edsDate;
    }

    private static IEnumerable<ushort> GetSupportedBaudRates(BaudRates baudRates)
    {
        if (baudRates.BaudRate10) yield return 10;
        if (baudRates.BaudRate20) yield return 20;
        if (baudRates.BaudRate50) yield return 50;
        if (baudRates.BaudRate125) yield return 125;
        if (baudRates.BaudRate250) yield return 250;
        if (baudRates.BaudRate500) yield return 500;
        if (baudRates.BaudRate800) yield return 800;
        if (baudRates.BaudRate1000) yield return 1000;
    }

    private static string GetDefaultBaudRateString(BaudRates baudRates)
    {
        if (baudRates.BaudRate250) return "250 Kbps";
        if (baudRates.BaudRate500) return "500 Kbps";
        if (baudRates.BaudRate125) return "125 Kbps";
        if (baudRates.BaudRate1000) return "1000 Kbps";
        if (baudRates.BaudRate800) return "800 Kbps";
        if (baudRates.BaudRate50) return "50 Kbps";
        if (baudRates.BaudRate20) return "20 Kbps";
        if (baudRates.BaudRate10) return "10 Kbps";
        return "250 Kbps";
    }

    internal static string FormatBaudRate(ushort kbps) =>
        string.Format(CultureInfo.InvariantCulture, "{0} Kbps", kbps);

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
