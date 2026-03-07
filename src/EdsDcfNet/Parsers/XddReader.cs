namespace EdsDcfNet.Parsers;

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml.Linq;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

/// <summary>
/// Reader for CiA 311 XDD (XML Device Description) files.
/// </summary>
public class XddReader : IFileReader<ElectronicDataSheet>
{
    /// <summary>
    /// Reads an XDD file from the specified path.
    /// </summary>
    /// <param name="filePath">Path to the XDD file</param>
    /// <param name="maxInputSize">Maximum file size in bytes.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
    /// <exception cref="EdsParseException">Thrown when the XDD content is invalid</exception>
    public ElectronicDataSheet ReadFile(
        string filePath,
        long maxInputSize = IniParser.DefaultMaxInputSize)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"XDD file not found: {filePath}", filePath);

        SecureXmlParser.EnsureFileWithinSizeLimit(filePath, "XDD", maxInputSize);
        var content = File.ReadAllText(filePath);
        return ReadString(content, maxInputSize);
    }

    /// <summary>
    /// Reads an XDD file from a stream.
    /// </summary>
    /// <param name="stream">Readable stream containing XDD content.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    /// <exception cref="EdsParseException">Thrown when the XDD content is invalid</exception>
    public ElectronicDataSheet ReadStream(
        Stream stream,
        long maxInputSize = IniParser.DefaultMaxInputSize)
    {
        var content = SecureXmlParser.ReadContentFromStreamWithLimit(stream, "XDD", maxInputSize);
        return ReadString(content, maxInputSize);
    }

    /// <summary>
    /// Reads an XDD file from the specified path asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the XDD file</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
    /// <exception cref="EdsParseException">Thrown when the XDD content is invalid</exception>
    public Task<ElectronicDataSheet> ReadFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
        => ReadFileAsync(filePath, IniParser.DefaultMaxInputSize, cancellationToken);

    /// <summary>
    /// Reads an XDD file from the specified path asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the XDD file</param>
    /// <param name="maxInputSize">Maximum file size in bytes.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
    /// <exception cref="EdsParseException">Thrown when the XDD content is invalid</exception>
    public async Task<ElectronicDataSheet> ReadFileAsync(
        string filePath,
        long maxInputSize,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"XDD file not found: {filePath}", filePath);

        SecureXmlParser.EnsureFileWithinSizeLimit(filePath, "XDD", maxInputSize);
        var content = await TextFileIo.ReadAllTextAsync(
            filePath,
            Encoding.UTF8,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return ReadString(content, maxInputSize);
    }

    /// <summary>
    /// Reads an XDD file from a stream asynchronously.
    /// </summary>
    /// <param name="stream">Readable stream containing XDD content.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    /// <exception cref="EdsParseException">Thrown when the XDD content is invalid</exception>
    public Task<ElectronicDataSheet> ReadStreamAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
        => ReadStreamAsync(stream, IniParser.DefaultMaxInputSize, cancellationToken);

    /// <summary>
    /// Reads an XDD file from a stream asynchronously.
    /// </summary>
    /// <param name="stream">Readable stream containing XDD content.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    /// <exception cref="EdsParseException">Thrown when the XDD content is invalid</exception>
    public async Task<ElectronicDataSheet> ReadStreamAsync(
        Stream stream,
        long maxInputSize,
        CancellationToken cancellationToken = default)
    {
        var content = await SecureXmlParser.ReadContentFromStreamWithLimitAsync(
            stream,
            "XDD",
            maxInputSize,
            cancellationToken).ConfigureAwait(false);
        return ReadString(content, maxInputSize);
    }

    /// <summary>
    /// Reads an XDD from a string.
    /// </summary>
    /// <param name="content">XDD file content as string</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    /// <exception cref="EdsParseException">Thrown when the XDD content is invalid</exception>
    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Public API — instance method for consistency with EdsReader pattern.")]
    public ElectronicDataSheet ReadString(
        string content,
        long maxInputSize = IniParser.DefaultMaxInputSize)
    {
        var doc = SecureXmlParser.ParseDocument(content, "XDD", "Failed to parse XDD XML content.", maxInputSize);
        return ParseDocument(doc, includeActualValues: false);
    }

    /// <summary>
    /// Parses an XDocument into an ElectronicDataSheet.
    /// </summary>
    /// <param name="doc">The XDocument to parse</param>
    /// <param name="includeActualValues">If true, actualValue attributes are mapped to ParameterValue</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    internal static ElectronicDataSheet ParseDocument(XDocument doc, bool includeActualValues)
    {
        var root = doc.Root;
        if (root == null)
            throw new EdsParseException("XDD document has no root element.");

        var profiles = root.Elements()
            .Where(e => e.Name.LocalName == "ISO15745Profile")
            .ToList();

        XElement? deviceProfileBody = null;
        XElement? commNetProfileBody = null;

        foreach (var profile in profiles)
        {
            var profileBody = profile.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "ProfileBody");
            if (profileBody == null)
                continue;

            var xsiType = GetXsiType(profileBody);
            if (xsiType.Contains("ProfileBody_Device_CANopen", StringComparison.OrdinalIgnoreCase))
                deviceProfileBody = profileBody;
            else if (xsiType.Contains("ProfileBody_CommunicationNetwork_CANopen", StringComparison.OrdinalIgnoreCase))
                commNetProfileBody = profileBody;
        }

        if (commNetProfileBody == null)
            throw new EdsParseException("XDD document does not contain a CommunicationNetwork ProfileBody.");

        var eds = new ElectronicDataSheet();

        // Parse FileInfo from device profile body (preferred) or comm-net body
        var fileInfoSource = deviceProfileBody ?? commNetProfileBody;
        eds.FileInfo = XddDeviceProfileParser.ParseFileInfo(fileInfoSource);

        // Parse DeviceIdentity from device profile body
        if (deviceProfileBody != null)
        {
            eds.DeviceInfo = XddDeviceProfileParser.ParseDeviceIdentity(deviceProfileBody);
            eds.ApplicationProcess = XddApplicationProcessParser.ParseApplicationProcess(deviceProfileBody);
        }

        // Parse communication features and object dictionary from comm-net profile body
        XddCommNetProfileParser.ParseCommNetProfile(commNetProfileBody, eds, includeActualValues);

        return eds;
    }

    // Kept for XdcReader compatibility.
    internal static DeviceCommissioning? ParseDeviceCommissioning(XElement networkMgmt)
        => XddCommNetProfileParser.ParseDeviceCommissioning(networkMgmt);

    // Kept for XdcReader compatibility.
    internal static string GetXsiType(XElement element)
        => XddParsingPrimitives.GetXsiType(element);

}
