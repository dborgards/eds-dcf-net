namespace EdsDcfNet.Parsers;

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml.Linq;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

/// <summary>
/// Reader for CiA 311 XDC (XML Device Configuration) files.
/// XDC extends XDD with actualValue, denotation, and deviceCommissioning.
/// </summary>
public class XdcReader
{
    /// <summary>
    /// Reads an XDC file from the specified path.
    /// </summary>
    /// <param name="filePath">Path to the XDC file</param>
    /// <param name="maxInputSize">Maximum file size in bytes.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
    /// <exception cref="EdsParseException">Thrown when the XDC content is invalid</exception>
    public DeviceConfigurationFile ReadFile(
        string filePath,
        long maxInputSize = IniParser.DefaultMaxInputSize)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"XDC file not found: {filePath}", filePath);

        SecureXmlParser.EnsureFileWithinSizeLimit(filePath, "XDC", maxInputSize);
        var content = File.ReadAllText(filePath);
        return ReadString(content, maxInputSize);
    }

    /// <summary>
    /// Reads an XDC file from a stream.
    /// </summary>
    /// <param name="stream">Readable stream containing XDC content.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    /// <exception cref="EdsParseException">Thrown when the XDC content is invalid</exception>
    public DeviceConfigurationFile ReadStream(
        Stream stream,
        long maxInputSize = IniParser.DefaultMaxInputSize)
    {
        var content = SecureXmlParser.ReadContentFromStreamWithLimit(stream, "XDC", maxInputSize);
        return ReadString(content, maxInputSize);
    }

    /// <summary>
    /// Reads an XDC file from the specified path asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the XDC file</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
    /// <exception cref="EdsParseException">Thrown when the XDC content is invalid</exception>
    public Task<DeviceConfigurationFile> ReadFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
        => ReadFileAsync(filePath, IniParser.DefaultMaxInputSize, cancellationToken);

    /// <summary>
    /// Reads an XDC file from the specified path asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the XDC file</param>
    /// <param name="maxInputSize">Maximum file size in bytes.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
    /// <exception cref="EdsParseException">Thrown when the XDC content is invalid</exception>
    public async Task<DeviceConfigurationFile> ReadFileAsync(
        string filePath,
        long maxInputSize,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"XDC file not found: {filePath}", filePath);

        SecureXmlParser.EnsureFileWithinSizeLimit(filePath, "XDC", maxInputSize);
        var content = await TextFileIo.ReadAllTextAsync(
            filePath,
            Encoding.UTF8,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return ReadString(content, maxInputSize);
    }

    /// <summary>
    /// Reads an XDC file from a stream asynchronously.
    /// </summary>
    /// <param name="stream">Readable stream containing XDC content.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    /// <exception cref="EdsParseException">Thrown when the XDC content is invalid</exception>
    public Task<DeviceConfigurationFile> ReadStreamAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
        => ReadStreamAsync(stream, IniParser.DefaultMaxInputSize, cancellationToken);

    /// <summary>
    /// Reads an XDC file from a stream asynchronously.
    /// </summary>
    /// <param name="stream">Readable stream containing XDC content.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    /// <exception cref="EdsParseException">Thrown when the XDC content is invalid</exception>
    public async Task<DeviceConfigurationFile> ReadStreamAsync(
        Stream stream,
        long maxInputSize,
        CancellationToken cancellationToken = default)
    {
        var content = await SecureXmlParser.ReadContentFromStreamWithLimitAsync(
            stream,
            "XDC",
            maxInputSize,
            cancellationToken).ConfigureAwait(false);
        return ReadString(content, maxInputSize);
    }

    /// <summary>
    /// Reads an XDC from a string.
    /// </summary>
    /// <param name="content">XDC file content as string</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    /// <exception cref="EdsParseException">Thrown when the XDC content is invalid</exception>
    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Public API — instance method for consistency with EdsReader pattern.")]
    public DeviceConfigurationFile ReadString(
        string content,
        long maxInputSize = IniParser.DefaultMaxInputSize)
    {
        var doc = SecureXmlParser.ParseDocument(content, "XDC", "Failed to parse XDC XML content.", maxInputSize);
        return ParseXdcDocument(doc);
    }

    private static DeviceConfigurationFile ParseXdcDocument(XDocument doc)
    {
        // Parse using the XDD reader with actual values enabled
        var eds = XddReader.ParseDocument(doc, includeActualValues: true);

        var dcf = new DeviceConfigurationFile
        {
            FileInfo = eds.FileInfo,
            DeviceInfo = eds.DeviceInfo,
            ObjectDictionary = eds.ObjectDictionary,
            Comments = eds.Comments,
            DynamicChannels = eds.DynamicChannels,
            ApplicationProcess = eds.ApplicationProcess
        };

        dcf.SupportedModules.AddRange(eds.SupportedModules);
        foreach (var kvp in eds.AdditionalSections)
        {
            dcf.AdditionalSections[kvp.Key] =
                AdditionalSectionsCloner.CloneSectionEntriesCaseInsensitive(kvp.Value);
        }

        // Parse deviceCommissioning from XDC NetworkManagement
        dcf.DeviceCommissioning = ParseDeviceCommissioning(doc) ?? new DeviceCommissioning();

        return dcf;
    }

    private static DeviceCommissioning? ParseDeviceCommissioning(XDocument doc)
    {
        var root = doc.Root;
        if (root == null)
            return null;

        foreach (var profile in root.Elements().Where(e => e.Name.LocalName == "ISO15745Profile"))
        {
            var profileBody = profile.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "ProfileBody");
            if (profileBody == null)
                continue;

            // Only look in the CommunicationNetwork profile body
            var xsiType = XddReader.GetXsiType(profileBody);
            if (!xsiType.Contains("ProfileBody_CommunicationNetwork_CANopen", StringComparison.OrdinalIgnoreCase))
                continue;

            var networkMgmt = profileBody.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "NetworkManagement");
            if (networkMgmt == null)
                continue;

            return XddReader.ParseDeviceCommissioning(networkMgmt);
        }

        return null;
    }

}
