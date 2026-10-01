namespace EdsDcfNet.Tests.Infrastructure;

using System.Globalization;
using System.Xml;
using System.Xml.Schema;

/// <summary>
/// Validates XDD/XDC documents against the normative CiA 311 v1.1.0 schema kept
/// under <c>Fixtures/Schemas/cia-311</c> (see the NOTICE.md there for origin
/// and the two documented changes against the delivered files).
/// </summary>
/// <remarks>
/// The schemas declare <c>elementFormDefault="unqualified"</c>: globally
/// declared elements (for example <c>ISO15745ProfileContainer</c>,
/// <c>DeviceIdentity</c>, <c>CANopenObjectList</c>) belong to
/// <see cref="TargetNamespace"/>, locally declared ones (for example
/// <c>ProfileHeader</c>, <c>ProfileBody</c>, <c>CANopenObject</c>) to no
/// namespace. A document that qualifies every element is as invalid as one
/// that qualifies none.
/// </remarks>
internal static class Cia311Schema
{
    internal const string TargetNamespace = "http://www.canopen.org/xml/1.1";

    internal const string NetworkProfileSchemaFile = "ProfileBody_Network_CANopen.xsd";

    internal static readonly string SchemaDirectory = Path.Combine("Fixtures", "Schemas", "cia-311");

    // Both profile-body schemas include ISO15745ProfileContainer.xsd and
    // CommonElements.xsd; XmlSchemaSet de-duplicates those by source URI.
    private static readonly string[] EntryFiles =
    {
        "ProfileBody_Device_CANopen.xsd",
        NetworkProfileSchemaFile,
    };

    private static readonly Lazy<XmlSchemaSet> Compiled = new(Compile);

    /// <summary>The compiled schema set; compiles once per test run.</summary>
    internal static XmlSchemaSet SchemaSet => Compiled.Value;

    /// <summary>
    /// Validates an XML document given as text and returns every schema error
    /// and warning. An empty list means the document is schema-valid.
    /// </summary>
    internal static IReadOnlyList<string> Validate(string xml)
    {
        using var text = new StringReader(xml);
        return Validate(settings => XmlReader.Create(text, settings));
    }

    /// <summary>
    /// Validates an XML file and returns every schema error and warning. The
    /// file is decoded by the XML reader, so a BOM or declared encoding applies.
    /// </summary>
    internal static IReadOnlyList<string> ValidateFile(string path)
        => Validate(settings => XmlReader.Create(path, settings));

    private static List<string> Validate(Func<XmlReaderSettings, XmlReader> createReader)
    {
        var problems = new List<string>();
        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = SchemaSet,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };

        // Without this flag an element with no matching declaration (for
        // example a root element outside the target namespace) is only a
        // warning and the document would pass silently.
        settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
        settings.ValidationEventHandler += (_, e) => problems.Add(Describe(e));

        using var reader = createReader(settings);
        while (reader.Read())
        {
        }

        return problems;
    }

    private static XmlSchemaSet Compile()
    {
        var directory = Path.GetFullPath(SchemaDirectory);
        var problems = new List<string>();
        var set = new XmlSchemaSet { XmlResolver = new DirectoryBoundResolver(directory) };
        set.ValidationEventHandler += (_, e) => problems.Add(Describe(e));

        var readerSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };

        foreach (var file in EntryFiles)
        {
            using var reader = XmlReader.Create(Path.Combine(directory, file), readerSettings);
            set.Add(null, reader);
        }

        set.Compile();

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "CiA 311 schema set did not compile cleanly: " + string.Join(" | ", problems));
        }

        return set;
    }

    private static string Describe(ValidationEventArgs e)
        => string.Format(
            CultureInfo.InvariantCulture,
            "{0} ({1},{2}): {3}",
            e.Severity,
            e.Exception?.LineNumber ?? 0,
            e.Exception?.LinePosition ?? 0,
            e.Message);

    /// <summary>
    /// Resolves <c>xsd:include</c> targets only inside the schema fixture
    /// directory, so schema loading can never reach the network or other files.
    /// </summary>
    private sealed class DirectoryBoundResolver : XmlUrlResolver
    {
        private readonly string _directoryPrefix;

        internal DirectoryBoundResolver(string directory)
        {
            _directoryPrefix = directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }

        public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
        {
            if (!absoluteUri.IsFile ||
                !Path.GetFullPath(absoluteUri.LocalPath).StartsWith(_directoryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new XmlException("Schema reference outside the CiA 311 fixture directory: " + absoluteUri);
            }

            return base.GetEntity(absoluteUri, role, ofObjectToReturn);
        }
    }
}
