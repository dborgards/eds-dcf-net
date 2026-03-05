namespace EdsDcfNet.Parsers;

using EdsDcfNet.Models;

/// <summary>
/// Reader for Electronic Data Sheet (EDS) files.
/// </summary>
public class EdsReader : CanOpenReaderBase
{
    private static readonly string[] EdsKnownSectionNames =
    {
        "FileInfo", "DeviceInfo", "DummyUsage", "MandatoryObjects",
        "OptionalObjects", "ManufacturerObjects", "Comments",
        "SupportedModules", "Tools", "DynamicChannels"
    };

    /// <inheritdoc/>
    protected override string[] KnownSectionNames => EdsKnownSectionNames;

    /// <summary>
    /// Reads an EDS file from the specified path.
    /// </summary>
    /// <param name="filePath">Path to the EDS file</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public ElectronicDataSheet ReadFile(
        string filePath,
        long maxInputSize = IniParser.DefaultMaxInputSize)
    {
        var sections = ParseSectionsFromFile(filePath, maxInputSize);
        return ParseEds(sections);
    }

    /// <summary>
    /// Reads an EDS file from a stream.
    /// </summary>
    /// <param name="stream">Readable stream containing EDS content.</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public ElectronicDataSheet ReadStream(
        Stream stream,
        long maxInputSize = IniParser.DefaultMaxInputSize)
    {
        var sections = ParseSectionsFromStream(stream, maxInputSize);
        return ParseEds(sections);
    }

    /// <summary>
    /// Reads an EDS file from the specified path asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the EDS file</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public Task<ElectronicDataSheet> ReadFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
        => ReadFileAsync(filePath, IniParser.DefaultMaxInputSize, cancellationToken);

    /// <summary>
    /// Reads an EDS file from the specified path asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the EDS file</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public async Task<ElectronicDataSheet> ReadFileAsync(
        string filePath,
        long maxInputSize,
        CancellationToken cancellationToken = default)
    {
        var sections = await ParseSectionsFromFileAsync(filePath, maxInputSize, cancellationToken).ConfigureAwait(false);
        return ParseEds(sections);
    }

    /// <summary>
    /// Reads an EDS file from a stream asynchronously.
    /// </summary>
    /// <param name="stream">Readable stream containing EDS content.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public Task<ElectronicDataSheet> ReadStreamAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
        => ReadStreamAsync(stream, IniParser.DefaultMaxInputSize, cancellationToken);

    /// <summary>
    /// Reads an EDS file from a stream asynchronously.
    /// </summary>
    /// <param name="stream">Readable stream containing EDS content.</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public async Task<ElectronicDataSheet> ReadStreamAsync(
        Stream stream,
        long maxInputSize,
        CancellationToken cancellationToken = default)
    {
        var sections = await ParseSectionsFromStreamAsync(stream, maxInputSize, cancellationToken).ConfigureAwait(false);
        return ParseEds(sections);
    }

    /// <summary>
    /// Reads an EDS from a string.
    /// </summary>
    /// <param name="content">EDS file content as string</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public ElectronicDataSheet ReadString(
        string content,
        long maxInputSize = IniParser.DefaultMaxInputSize)
    {
        var sections = ParseSectionsFromString(content, maxInputSize);
        return ParseEds(sections);
    }

    private ElectronicDataSheet ParseEds(Dictionary<string, Dictionary<string, string>> sections)
    {
        var eds = new ElectronicDataSheet
        {
            FileInfo = ParseFileInfo(sections),
            DeviceInfo = ParseDeviceInfo(sections),
            ObjectDictionary = ParseObjectDictionary(sections),
            Comments = ParseComments(sections)
        };

        // Parse supported modules if present
        if (IniParser.HasSection(sections, "SupportedModules"))
        {
            eds.SupportedModules.AddRange(ParseSupportedModules(sections));
        }

        // Parse dynamic channels if present
        if (IniParser.HasSection(sections, "DynamicChannels"))
        {
            eds.DynamicChannels = ParseDynamicChannels(sections);
        }

        // Parse tools if present
        if (IniParser.HasSection(sections, "Tools"))
        {
            eds.Tools.AddRange(ParseTools(sections));
        }

        // Parse any additional unknown sections
        foreach (var sectionName in sections.Keys)
        {
            if (!IsKnownSection(sectionName) && !IsToolSectionForParsedTools(sectionName, eds.Tools.Count))
            {
                eds.AdditionalSections[sectionName] =
                    new Dictionary<string, string>(sections[sectionName], StringComparer.OrdinalIgnoreCase);
            }
        }

        return eds;
    }
}
