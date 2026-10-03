namespace EdsDcfNet.Writers;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

/// <summary>
/// Writer for CiA 311 XDC (XML Device Configuration) files.
/// Extends XddWriter with actualValue, denotation, and deviceCommissioning support.
/// </summary>
/// <remarks>
/// <para>
/// CiA 311 <c>deviceCommissioning</c> only defines <c>nodeID</c>, <c>nodeName</c>,
/// <c>actualBaudRate</c>, <c>networkNumber</c>, <c>networkName</c>, and
/// <c>CANopenManager</c>. The DCF-only fields
/// <see cref="DeviceCommissioning.LssSerialNumber"/>,
/// <see cref="DeviceCommissioning.NodeRefd"/>, and
/// <see cref="DeviceCommissioning.NetRefd"/> have no schema-equivalent attributes
/// and are intentionally not written when a valid NodeId (<c>1..127</c>) is present.
/// Prefer DCF when those values must be preserved (CPJ can retain network/node
/// reference designators, but has no serial-number field).
/// </para>
/// <para>
/// A DCF→XDC conversion with NodeId in <c>1..127</c> therefore drops those three
/// fields from the emitted element. If NodeId is <c>0</c> (or otherwise outside
/// <c>1..127</c>) while any commissioning field is set — including only those
/// DCF-only fields — writing throws <see cref="XdcWriteException"/> instead of
/// omitting <c>deviceCommissioning</c>.
/// </para>
/// </remarks>
public class XdcWriter : XddWriter
{
    /// <summary>
    /// Writes a DeviceConfigurationFile as an XDC file to the specified path.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="filePath">Path where the XDC file should be written</param>
    /// <remarks>
    /// The content is written to a temporary file in the target directory and then moved or
    /// replaced over the target. On failure the target is left untouched and the temporary file
    /// is removed. Whether the final replace is atomic depends on the file system (for example,
    /// network shares may not guarantee it).
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="dcf"/> is <see langword="null"/>.</exception>
    public void WriteFile(DeviceConfigurationFile dcf, string filePath)
    {
        ThrowIfNull(dcf, nameof(dcf));

        WriteEntryPoints.ToFile(
            filePath,
            "XDC",
            () =>
            {
                var doc = BuildOutputDocument(dcf);
                TextFileIo.WriteFileAtomic(filePath, stream => SerializeOutput(doc, stream));
            },
            (message, inner) => new XdcWriteException(message, inner));
    }

    /// <summary>
    /// Writes a DeviceConfigurationFile as XDC content to the specified stream.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <exception cref="ArgumentNullException"><paramref name="dcf"/> or <paramref name="stream"/> is <see langword="null"/>.</exception>
    public void WriteStream(DeviceConfigurationFile dcf, Stream stream)
    {
        ThrowIfNull(dcf, nameof(dcf));
        ThrowIfNull(stream, nameof(stream));
        if (!stream.CanWrite)
            throw new ArgumentException("Stream must be writable.", nameof(stream));

        WriteEntryPoints.ToStream(
            "XDC",
            () =>
            {
                var doc = BuildOutputDocument(dcf);
                SerializeOutput(doc, stream);
            },
            (message, inner) => new XdcWriteException(message, inner));
    }

    /// <summary>
    /// Writes a DeviceConfigurationFile as an XDC file to the specified path asynchronously.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="filePath">Path where the XDC file should be written</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <remarks>
    /// The content is written to a temporary file in the target directory and then moved or
    /// replaced over the target. On failure or cancellation the target is left untouched and the
    /// temporary file is removed. Whether the final replace is atomic depends on the file system
    /// (for example, network shares may not guarantee it).
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="dcf"/> is <see langword="null"/>.</exception>
    public async Task WriteFileAsync(
        DeviceConfigurationFile dcf,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNull(dcf, nameof(dcf));

        await WriteEntryPoints.ToFileAsync(
            filePath,
            "XDC",
            async () =>
            {
                var doc = BuildOutputDocument(dcf);
                await TextFileIo.WriteFileAtomicAsync(
                    filePath,
                    stream => SerializeOutputAsync(doc, stream, cancellationToken),
                    cancellationToken).ConfigureAwait(false);
            },
            (message, inner) => new XdcWriteException(message, inner),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a DeviceConfigurationFile as an XDC stream asynchronously.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <exception cref="ArgumentNullException"><paramref name="dcf"/> or <paramref name="stream"/> is <see langword="null"/>.</exception>
    public async Task WriteStreamAsync(
        DeviceConfigurationFile dcf,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNull(dcf, nameof(dcf));
        ThrowIfNull(stream, nameof(stream));
        if (!stream.CanWrite)
            throw new ArgumentException("Stream must be writable.", nameof(stream));

        await WriteEntryPoints.ToStreamAsync(
            "XDC",
            async () =>
            {
                var doc = BuildOutputDocument(dcf);
                await SerializeOutputAsync(doc, stream, cancellationToken).ConfigureAwait(false);
            },
            (message, inner) => new XdcWriteException(message, inner),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Generates XDC content as a string.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to convert</param>
    /// <returns>XDC content as string</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dcf"/> is <see langword="null"/>.</exception>
    public string GenerateString(DeviceConfigurationFile dcf)
    {
        ThrowIfNull(dcf, nameof(dcf));

        return ConvertXddFailures(() => base.GenerateString(CreateEdsView(dcf), dcf.DeviceCommissioning));
    }

    private XDocument BuildOutputDocument(DeviceConfigurationFile dcf)
        => ConvertXddFailures(() => BuildOutputDocument(CreateEdsView(dcf), dcf.DeviceCommissioning));

    private static void SerializeOutput(XDocument doc, Stream stream)
    {
        try
        {
            SerializeDocument(doc, stream);
        }
        catch (XddWriteException ex)
        {
            throw ToXdcException(ex);
        }
    }

    private static async Task SerializeOutputAsync(XDocument doc, Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            await SerializeDocumentAsync(doc, stream, cancellationToken).ConfigureAwait(false);
        }
        catch (XddWriteException ex)
        {
            throw ToXdcException(ex);
        }
    }

    private static XdcWriteException ToXdcException(XddWriteException ex)
        => new(ex.Message, ex.InnerException ?? ex)
        {
            SectionName = ex.SectionName
        };

    private static T ConvertXddFailures<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (XddWriteException ex)
        {
            throw ToXdcException(ex);
        }
        catch (XdcWriteException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new XdcWriteException(
                "Failed to write section [Document]",
                ex)
            {
                SectionName = "Document"
            };
        }
    }

    /// <inheritdoc/>
    protected override void AddCanOpenObjectXdcAttributes(XElement elem, CanOpenObject obj)
    {
        if (!string.IsNullOrEmpty(obj.ParameterValue))
            elem.Add(new XAttribute("actualValue", obj.ParameterValue));

        if (!string.IsNullOrEmpty(obj.Denotation))
            elem.Add(new XAttribute("denotation", obj.Denotation));
    }

    /// <inheritdoc/>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
        Justification = "Parameter name is a CANopen domain term; VB conflict not applicable here.")]
    protected override void AddCanOpenSubObjectXdcAttributes(XElement elem, CanOpenSubObject subObject)
    {
        if (!string.IsNullOrEmpty(subObject.ParameterValue))
            elem.Add(new XAttribute("actualValue", subObject.ParameterValue));

        if (!string.IsNullOrEmpty(subObject.Denotation))
            elem.Add(new XAttribute("denotation", subObject.Denotation));
    }

    /// <summary>
    /// XDC models <c>actualValue</c> and <c>denotation</c>; values kept from an XDD read are not
    /// written, the model's <see cref="CanOpenObject.ParameterValue"/> and
    /// <see cref="CanOpenObject.Denotation"/> are (rule 13).
    /// </summary>
    internal override bool KeepsPreservedObjectAttribute(XName name)
        => name != "actualValue" && name != "denotation";

    /// <inheritdoc/>
    protected override XElement BuildNetworkManagement(ElectronicDataSheet eds, DeviceCommissioning? commissioning)
    {
        var networkMgmt = base.BuildNetworkManagement(eds, commissioning);

        // Align with DCF: omit only when every commissioning field is empty/zero.
        // Non-omitted commissioning with NodeId outside 1..127 fails in BuildDeviceCommissioning.
        if (commissioning != null && !DeviceCommissioningSemantics.IsOmitted(commissioning))
            networkMgmt.Add(BuildDeviceCommissioning(networkMgmt.Name, commissioning));

        return networkMgmt;
    }

    private static XElement BuildDeviceCommissioning(XName networkManagementName, DeviceCommissioning dc)
    {
        if (!CanOpenNodeId.IsInRange(dc.NodeId))
        {
            throw new XdcWriteException(
                string.Format(CultureInfo.InvariantCulture,
                    "Cannot write XDC: NodeId {0} is outside the valid CANopen range " + CanOpenNodeId.RangeDescription + ".",
                    dc.NodeId),
                "deviceCommissioning");
        }

        // CiA 311 deviceCommissioning has no attributes for LssSerialNumber / NodeRefd /
        // NetRefd (CiA 306 DCF keys). Those properties are intentionally omitted here.
        var elem = XddNames.Element(networkManagementName, "deviceCommissioning");

        elem.Add(new XAttribute("nodeID",
            dc.NodeId.ToString(CultureInfo.InvariantCulture)));

        // nodeName, actualBaudRate, networkNumber and networkName are required by the schema
        // (xsd:string / xsd:unsignedLong); an empty string is schema-valid.
        elem.Add(new XAttribute("nodeName", dc.NodeName ?? string.Empty));

        elem.Add(new XAttribute("actualBaudRate", FormatActualBaudRate(dc)));

        elem.Add(new XAttribute("networkNumber",
            dc.NetworkNumberLexical != null && dc.NetNumber == dc.NetworkNumberLexicalBaseline
                ? dc.NetworkNumberLexical
                : dc.NetNumber.ToString(CultureInfo.InvariantCulture)));

        elem.Add(new XAttribute("networkName", dc.NetworkName ?? string.Empty));

        elem.Add(new XAttribute("CANopenManager",
            dc.CANopenManager ? "true" : "false"));

        return elem;
    }

    private static string FormatActualBaudRate(DeviceCommissioning dc)
    {
        // The read spelling (for example "auto-baudRate") stays while the property is unchanged.
        if (dc.ActualBaudRateLexical != null && dc.Baudrate == dc.ActualBaudRateLexicalBaseline)
            return dc.ActualBaudRateLexical;

        return dc.Baudrate > 0
            ? string.Format(CultureInfo.InvariantCulture, "{0} Kbps", dc.Baudrate)
            : string.Empty;
    }

    /// <summary>Creates a temporary ElectronicDataSheet view from a DeviceConfigurationFile.</summary>
    private static ElectronicDataSheet CreateEdsView(DeviceConfigurationFile dcf)
    {
        var eds = new ElectronicDataSheet
        {
            FileInfo = dcf.FileInfo,
            DeviceInfo = dcf.DeviceInfo,
            ObjectDictionary = dcf.ObjectDictionary,
            Comments = dcf.Comments,
            DynamicChannels = dcf.DynamicChannels,
            ApplicationProcess = dcf.ApplicationProcess,
            XddPreserved = dcf.XddPreserved
        };

        eds.SupportedModules.AddRange(dcf.SupportedModules);
        eds.Tools.AddRange(dcf.Tools);
        foreach (var kvp in dcf.AdditionalSections)
            eds.AdditionalSections[kvp.Key] = kvp.Value;

        return eds;
    }

    private static void ThrowIfNull(object? value, string parameterName)
    {
        if (value == null)
            throw new ArgumentNullException(parameterName);
    }
}
