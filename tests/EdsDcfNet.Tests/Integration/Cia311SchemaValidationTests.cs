namespace EdsDcfNet.Tests.Integration;

using System.Xml;
using System.Xml.Linq;
using EdsDcfNet.Parsers;
using EdsDcfNet.Tests.Infrastructure;
using EdsDcfNet.Writers;

/// <summary>
/// Validation against the normative CiA 311 v1.1.0 schema (review finding T3).
/// <list type="bullet">
/// <item>The schema fixtures compile and accept a real-world XDD.</item>
/// <item>Negative probes prove the validation actually inspects profile content.</item>
/// <item><see cref="KnownGaps"/> pins today's non-conformant documents together
/// with the first reported problem. A fix that removes that problem fails the
/// test on purpose: update the entry to the next remaining problem, or move the
/// document to <see cref="ConformantDocuments"/> once it validates.</item>
/// <item><see cref="NamespacelessInputFixtures"/> are input fixtures in the form older
/// versions of this library wrote (no namespace). They stay that way on purpose, so
/// the reader keeps being tested against them; they are not a writer gap.</item>
/// </list>
/// </summary>
public class Cia311SchemaValidationTests
{
    private const string CorpusXdd = "Fixtures/Corpus/canopen-node/basicDevice.xdd";

    private static readonly XNamespace Xsd = "http://www.w3.org/2001/XMLSchema";

    /// <summary>Documents that must validate without any schema problem.</summary>
    public static IEnumerable<object[]> ConformantDocuments()
    {
        yield return new object[] { "corpus:canopen-node/basicDevice.xdd" };
        yield return new object[] { "fixture:preserved_content.xdd" };
        yield return new object[] { "XddWriter:sample_device.xdd" };
        yield return new object[] { "XddWriter:sample_device.eds" };
        yield return new object[] { "XddWriter:corpus/basicDevice.xdd" };
        yield return new object[] { "XddWriter:preserved_content.xdd" };
        yield return new object[] { "XdcWriter:minimal.xdc" };
        yield return new object[] { "XdcWriter:full_features.dcf" };
        yield return new object[] { "XdcWriter:ConvertToDcf(sample_device.eds)" };
        yield return new object[] { "XdcWriter:ConvertToDcf(corpus/basicDevice.xdd)" };
    }

    /// <summary>
    /// Documents that do not validate yet, with a stable token (an element or
    /// attribute name, not message text) expected in the first reported problem.
    /// </summary>
    public static IEnumerable<object[]> KnownGaps()
    {
        // Empty: every writer output listed in ConformantDocuments validates. A new gap is
        // added here as { document, token }.
        yield break;
    }

    /// <summary>Input fixtures without a namespace, with the stable token of their first problem.</summary>
    public static IEnumerable<object[]> NamespacelessInputFixtures()
    {
        yield return new object[] { "fixture:sample_device.xdd", "ISO15745ProfileContainer" };
        yield return new object[] { "fixture:minimal.xdc", "ISO15745ProfileContainer" };
    }

    [Fact]
    public void SchemaSet_Compile_ExposesGlobalElementsInTargetNamespace()
    {
        // Act
        var names = Cia311Schema.SchemaSet.GlobalElements.Names
            .Cast<XmlQualifiedName>()
            .ToList();

        // Assert
        names.Should().OnlyContain(name => name.Namespace == Cia311Schema.TargetNamespace);
        names.Select(name => name.Name).Should().Contain(new[]
        {
            "ISO15745ProfileContainer",
            "ISO15745Profile",
            "DeviceIdentity",
            "ApplicationProcess",
            "CANopenObjectList",
        });
        names.Should().HaveCount(85);
    }

    [Fact]
    public void SchemaSet_Compile_KeepsProfileStructureElementsLocal()
    {
        // Act
        var globalNames = Cia311Schema.SchemaSet.GlobalElements.Names
            .Cast<XmlQualifiedName>()
            .Select(name => name.Name)
            .ToList();

        // Assert — these are declared locally, so instance documents must leave
        // them unqualified (elementFormDefault="unqualified").
        globalNames.Should().NotContain(new[]
        {
            "ProfileHeader",
            "ProfileBody",
            "ApplicationLayers",
            "CANopenObject",
            "CANopenSubObject",
            "NetworkManagement",
            "deviceCommissioning",
        });
    }

    [Fact]
    public void NetworkProfileSchema_SortStepDefault_IsValidHexBinary()
    {
        // Arrange
        var schema = XDocument.Load(
            Path.Combine(Cia311Schema.SchemaDirectory, Cia311Schema.NetworkProfileSchemaFile));

        // Act
        var sortStep = schema.Descendants(Xsd + "attribute")
            .Single(attribute => (string?)attribute.Attribute("name") == "sortStep");

        // Assert — Annex A.1.4 prints default="1", which is not valid hexBinary
        // and makes the schema fail to compile; see NOTICE.md.
        ((string?)sortStep.Attribute("default")).Should().Be("01");
    }

    [Theory]
    [MemberData(nameof(ConformantDocuments))]
    public void Validate_ConformantDocument_ReportsNoProblems(string document)
    {
        // Act
        var problems = Cia311Schema.Validate(Resolve(document));

        // Assert
        problems.Should().BeEmpty();
    }

    // A fact rather than a theory: xUnit fails a theory whose data source is empty.
    [Fact]
    public void Validate_KnownGaps_StillReportExpectedFirstProblem()
    {
        foreach (var gap in KnownGaps())
        {
            var document = (string)gap[0];
            var expectedToken = (string)gap[1];

            // Act
            var problems = Cia311Schema.Validate(Resolve(document));

            // Assert
            problems.Should().NotBeEmpty(
                "{0} is listed as a known schema gap; move it to ConformantDocuments now that it validates",
                document);
            problems[0].Should().Contain(
                expectedToken,
                "the first schema problem of {0} changed; update the KnownGaps entry to the next remaining problem",
                document);
        }
    }

    [Fact]
    public void KnownGaps_AfterPhase3_IsEmpty()
    {
        // Act
        var gaps = KnownGaps().ToList();

        // Assert
        gaps.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(NamespacelessInputFixtures))]
    public void Validate_NamespacelessInputFixture_IsRejectedForTheMissingNamespaceOnly(string document, string expectedToken)
    {
        // Act
        var problems = Cia311Schema.Validate(Resolve(document));

        // Assert — without the namespace no element is declared, so the validator only warns
        // that it has no schema information; the content is not checked at all.
        problems.Should().NotBeEmpty();
        problems[0].Should().Contain(expectedToken);
        problems.Should().OnlyContain(problem => problem.StartsWith("Warning", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WriterOutputWithHundredKbpsAndAutoBaudRate_ReportsNoBaudRateProblem()
    {
        // Arrange
        var eds = new XddReader().ReadFile(CorpusXdd);
        eds.DeviceInfo.SupportedBaudRates.BaudRate100 = true;
        eds.DeviceInfo.SupportedBaudRates.AutoBaudRate = true;
        var xml = new XddWriter().GenerateString(eds);

        // Act
        var problems = Cia311Schema.Validate(xml);
        var probe = Cia311Schema.Validate(Mutate(xml, ("value=\"100 Kbps\"", "value=\"77 Kbps\"")));

        // Assert
        xml.Should().Contain("value=\"100 Kbps\"").And.Contain("value=\"auto-baudRate\"");
        problems.Should().BeEmpty();
        probe.Should().Contain(problem => problem.Contains("77 Kbps", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFile_CorpusBasicDeviceXdd_ReportsNoProblems()
    {
        // Act
        var problems = Cia311Schema.ValidateFile(CorpusXdd);

        // Assert
        problems.Should().BeEmpty();
    }

    [Fact]
    public void Validate_UnknownAttributeOnCanOpenObject_ReportsProblem()
    {
        // Arrange
        var xml = Mutate(
            File.ReadAllText(CorpusXdd),
            ("<CANopenObject index=\"1000\"", "<CANopenObject bogusAttribute=\"1\" index=\"1000\""));

        // Act
        var problems = Cia311Schema.Validate(xml);

        // Assert
        problems.Should().Contain(problem => problem.Contains("bogusAttribute"));
    }

    [Fact]
    public void Validate_InvalidObjectTypeValue_ReportsProblem()
    {
        // Arrange
        var xml = Mutate(
            File.ReadAllText(CorpusXdd),
            ("name=\"Device type\" objectType=\"7\"", "name=\"Device type\" objectType=\"banana\""));

        // Act
        var problems = Cia311Schema.Validate(xml);

        // Assert
        problems.Should().Contain(problem => problem.Contains("objectType"));
    }

    [Fact]
    public void Validate_LocalElementQualified_ReportsProblem()
    {
        // Arrange — CANopenObject is declared locally and must stay unqualified.
        var xml = Mutate(
            File.ReadAllText(CorpusXdd),
            ("<CANopenObject index=\"1000\"", "<q2:CANopenObject index=\"1000\""));

        // Act
        var problems = Cia311Schema.Validate(xml);

        // Assert
        problems.Should().Contain(problem => problem.Contains("CANopenObject"));
    }

    [Fact]
    public void Validate_GlobalElementUnqualified_ReportsProblem()
    {
        // Arrange — DeviceIdentity is declared globally and must be qualified.
        var xml = Mutate(
            File.ReadAllText(CorpusXdd),
            ("<q1:DeviceIdentity>", "<DeviceIdentity>"),
            ("</q1:DeviceIdentity>", "</DeviceIdentity>"));

        // Act
        var problems = Cia311Schema.Validate(xml);

        // Assert
        problems.Should().Contain(problem => problem.Contains("DeviceIdentity"));
    }

    private static string Resolve(string document) => document switch
    {
        "corpus:canopen-node/basicDevice.xdd" => File.ReadAllText(CorpusXdd),
        "fixture:sample_device.xdd" => File.ReadAllText("Fixtures/sample_device.xdd"),
        "fixture:minimal.xdc" => File.ReadAllText("Fixtures/minimal.xdc"),
        "fixture:preserved_content.xdd" => File.ReadAllText("Fixtures/preserved_content.xdd"),
        "XddWriter:preserved_content.xdd" =>
            new XddWriter().GenerateString(new XddReader().ReadFile("Fixtures/preserved_content.xdd")),
        "XdcWriter:full_features.dcf" =>
            new XdcWriter().GenerateString(new DcfReader().ReadFile("Fixtures/full_features.dcf")),
        "XdcWriter:ConvertToDcf(sample_device.eds)" =>
            new XdcWriter().GenerateString(CanOpenFile.Eds.ConvertToDcf(
                new EdsReader().ReadFile("Fixtures/sample_device.eds"), 5, new DateTime(2026, 6, 1, 8, 0, 0))),
        "XdcWriter:ConvertToDcf(corpus/basicDevice.xdd)" =>
            new XdcWriter().GenerateString(CanOpenFile.Eds.ConvertToDcf(
                new XddReader().ReadFile(CorpusXdd), 5, new DateTime(2026, 6, 1, 8, 0, 0))),
        "XddWriter:sample_device.xdd" =>
            new XddWriter().GenerateString(new XddReader().ReadFile("Fixtures/sample_device.xdd")),
        "XddWriter:sample_device.eds" =>
            new XddWriter().GenerateString(new EdsReader().ReadFile("Fixtures/sample_device.eds")),
        "XddWriter:corpus/basicDevice.xdd" =>
            new XddWriter().GenerateString(new XddReader().ReadFile(CorpusXdd)),
        "XdcWriter:minimal.xdc" =>
            new XdcWriter().GenerateString(new XdcReader().ReadFile("Fixtures/minimal.xdc")),
        _ => throw new ArgumentOutOfRangeException(nameof(document), document, "Unknown document key."),
    };

    /// <summary>
    /// Replaces the first occurrence of each search text and fails when a search
    /// text is absent, so a probe can never silently validate the unmodified file.
    /// </summary>
    private static string Mutate(string text, params (string Find, string Replace)[] edits)
    {
        foreach (var (find, replace) in edits)
        {
            var position = text.IndexOf(find, StringComparison.Ordinal);
            position.Should().BeGreaterThanOrEqualTo(0, "the corpus file must contain '{0}'", find);
            text = text.Substring(0, position) + replace + text.Substring(position + find.Length);
        }

        return text;
    }
}
