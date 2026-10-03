namespace EdsDcfNet.Tests.Integration;

using System.Reflection;
using System.Text;
using EdsDcfNet;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;

/// <summary>
/// Completeness guard for unknown keys in every section the EDS/DCF reader processes.
/// </summary>
/// <remarks>
/// CiA 306-1 v1.4.0 § 6.2: "The additional sections and entries inside of the sections are
/// included in order to support future extensions." A section the reader consumes (and
/// therefore does not copy into <c>AdditionalSections</c>) must still keep the entries it does
/// not map, or they are lost on write. The case list is derived from the reader:
/// every name in <c>KnownSectionNames</c> must have a template here, and the cases are the
/// fixture sections that <c>IsKnownSection</c>, <c>IsSectionHandledByFormat</c> (including the
/// DCF override), <c>CanOpenSectionParsers.IsParsedToolSection</c> or
/// <c>CanOpenSectionParsers.IsConsumedModuleFixedSection</c> classify as processed. <c>[xxxxObjectLinks]</c> of an existing object is
/// added for EDS as well, because the EDS writer emits it from the object and skips the
/// <c>AdditionalSections</c> copy.
/// </remarks>
public class SectionRemainingEntriesCompletenessTests
{
    private const string VendorKey = "VendorKey";

    /// <summary>Section templates. The vendor key is appended by the fixture builder.</summary>
    private static readonly (string Section, string Body, bool DcfOnly)[] Templates =
    {
        ("FileInfo", "FileName=wp06\nFileVersion=1\nFileRevision=0\nEDSVersion=4.0", false),
        ("DeviceInfo", "VendorName=Vendor\nVendorNumber=0x1\nProductName=Product\nProductNumber=0x2\nRevisionNumber=0x3\nOrderCode=OC", false),
        ("DeviceComissioning", "NodeID=2\nNodeName=Node\nBaudrate=250\nNetNumber=1\nNetworkName=Net\nCANopenManager=0", true),
        ("DeviceCommissioning", "NodeID=2\nNodeName=Node\nBaudrate=250\nNetNumber=1\nNetworkName=Net\nCANopenManager=0", true),
        ("DummyUsage", "Dummy0001=0\nDummy0002=1", false),
        ("MandatoryObjects", "SupportedObjects=1\n1=0x1000", false),
        ("OptionalObjects", "SupportedObjects=1\n1=0x1018", false),
        ("ManufacturerObjects", "SupportedObjects=1\n1=0x2000", false),
        ("1000", "ParameterName=Device type\nObjectType=0x7\nDataType=0x0007\nAccessType=ro\nDefaultValue=0\nPDOMapping=0", false),
        ("1018", "ParameterName=Identity\nObjectType=0x9\nSubNumber=1", false),
        ("1018sub0", "ParameterName=Highest\nObjectType=0x7\nDataType=0x0005\nAccessType=ro\nDefaultValue=0\nPDOMapping=0", false),
        ("2000", "ParameterName=Compact\nObjectType=0x8\nDataType=0x0007\nAccessType=rw\nDefaultValue=0\nPDOMapping=0\nCompactSubObj=2", false),
        ("2000Name", "NrOfEntries=1\n1=First", false),
        ("2000ObjectLinks", "ObjectLinks=1\n1=0x1000", false),
        ("2000Value", "NrOfEntries=1\n1=5", true),
        ("2000Denotation", "NrOfEntries=1\n1=Den", true),
        ("Comments", "Lines=1\nLine1=Hello", false),
        ("SupportedModules", "NrOfEntries=1", false),
        ("M1ModuleInfo", "ProductName=Module\nProductVersion=1\nProductRevision=0\nOrderCode=M-1", false),
        ("M1Comments", "Lines=1\nLine1=Module line", false),
        ("M1FixedObjects", "NrOfEntries=1\n1=0x6423", false),
        ("M1Fixed6423", "ParameterName=Fixed\nObjectType=0x7\nDataType=0x0001\nAccessType=rw\nPDOMapping=0", false),
        ("M1SubExtends", "NrOfEntries=1\n1=0x6000", false),
        ("M1SubExt6000", "ParameterName=Extension\nObjectType=0x8\nDataType=0x0005\nAccessType=ro\nPDOMapping=1\nCount=1", false),
        ("ConnectedModules", "NrOfEntries=1\n1=1", true),
        ("Tools", "Items=1", false),
        ("Tool1", "Name=Tool\nCommand=tool $EDS", false),
        ("DynamicChannels", "NrOfSeg=1\nType1=0x0007\nDir1=ro\nRange1=0xA080-0xA0BF\nPPOffset1=0", false),
    };

    /// <summary>Alternative spellings of one section; a fixture contains only one of them.</summary>
    private static readonly string[] CommissioningSpellings = { "DeviceComissioning", "DeviceCommissioning" };

    public static IEnumerable<object[]> ProcessedSections()
    {
        foreach (var isDcf in new[] { false, true })
        {
            foreach (var section in ProcessedSectionNames(isDcf))
                yield return new object[] { isDcf ? "DCF" : "EDS", section };
        }
    }

    [Theory]
    [MemberData(nameof(ProcessedSections))]
    public void WriteString_ProcessedSectionWithVendorKey_PreservesVendorKey(string format, string section)
    {
        // Arrange
        var isDcf = format == "DCF";
        var content = BuildFixture(isDcf, section);

        // Act
        var written = isDcf
            ? CanOpenFile.Dcf.WriteToString(CanOpenFile.Dcf.ReadString(content))
            : CanOpenFile.Eds.WriteToString(CanOpenFile.Eds.ReadString(content));

        // Assert
        // The writer always uses the CiA 306-1 spelling [DeviceComissioning] (§ 7.3.5).
        var writtenName = CommissioningSpellings.Contains(section) ? CommissioningSpellings[0] : section;
        var sections = IniParser.ParseString(written);
        sections.Should().ContainKey(writtenName, $"[{section}] must be written again as [{writtenName}] ({format})");
        sections[writtenName].Should().Contain(VendorKey, VendorValue(section), $"the vendor key of [{section}] must survive the {format} round-trip");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KnownSectionNames_EveryName_HasFixtureTemplate(bool isDcf)
    {
        // Arrange — a new standard section name must get a template, which adds it to the theory above.
        var known = KnownSectionNames(isDcf);

        // Assert
        known.Should().OnlyContain(
            name => Templates.Any(t => t.Section.Equals(name, StringComparison.OrdinalIgnoreCase)),
            "every section name the reader knows needs a template in this test");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProcessedSectionNames_CoverEveryFixtureSectionNotInAdditionalSections(bool isDcf)
    {
        // Arrange
        var content = BuildFixture(isDcf, sectionUnderTest: null);

        // Act
        var additional = isDcf
            ? CanOpenFile.Dcf.ReadString(content).AdditionalSections
            : CanOpenFile.Eds.ReadString(content).AdditionalSections;
        var processed = ProcessedSectionNames(isDcf).ToList();

        // Assert — the fixture is complete: every template is either processed or deliberately
        // preserved as an additional section by this format, never silently consumed.
        foreach (var template in FixtureTemplates(isDcf, sectionUnderTest: null))
        {
            var inAdditional = additional.ContainsKey(template.Section);
            var isProcessed = processed.Contains(template.Section, StringComparer.OrdinalIgnoreCase);
            (inAdditional || isProcessed).Should().BeTrue($"[{template.Section}] must be covered ({(isDcf ? "DCF" : "EDS")})");
        }
    }

    private static IEnumerable<string> ProcessedSectionNames(bool isDcf)
    {
        var reader = isDcf ? (CanOpenReaderBase)new DcfReader() : new EdsReader();
        var isKnown = typeof(CanOpenReaderBase).GetMethod("IsKnownSection", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var isHandled = typeof(CanOpenReaderBase).GetMethod("IsSectionHandledByFormat", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var sectionParsers = typeof(CanOpenReaderBase).Assembly
            .GetType("EdsDcfNet.Parsers.CanOpenSectionParsers", throwOnError: true)!;
        var isTool = sectionParsers.GetMethod("IsParsedToolSection", BindingFlags.Static | BindingFlags.NonPublic)!;
        var isModuleFixed = sectionParsers.GetMethod("IsConsumedModuleFixedSection", BindingFlags.Static | BindingFlags.NonPublic)!;

        var sections = new List<string>();
        foreach (var spelling in CommissioningSpellings)
        {
            if (!isDcf && spelling != CommissioningSpellings[0])
                continue;

            var content = BuildFixture(isDcf, spelling);
            object model = isDcf ? CanOpenFile.Dcf.ReadString(content) : CanOpenFile.Eds.ReadString(content);
            var modules = isDcf ? ((DeviceConfigurationFile)model).SupportedModules : ((ElectronicDataSheet)model).SupportedModules;

            foreach (var template in FixtureTemplates(isDcf, spelling))
            {
                var known = (bool)isKnown.Invoke(reader, new object[] { template.Section })!;
                // The same checks ParseCommonSections uses to keep a section out of AdditionalSections.
                var handled = (bool)isHandled.Invoke(reader, new[] { template.Section, model })!
                              || (bool)isTool.Invoke(null, new object[] { IniParser.ParseString(content), template.Section })!
                              || (bool)isModuleFixed.Invoke(null, new object[] { template.Section, modules })!;

                // EDS keeps [xxxxObjectLinks] of an existing object in AdditionalSections, but the
                // writer emits the section from the object and skips that copy.
                var edsObjectLinks = !isDcf && template.Section.EndsWith("ObjectLinks", StringComparison.Ordinal);

                if ((known || handled || edsObjectLinks) && !sections.Contains(template.Section))
                    sections.Add(template.Section);
            }
        }

        return sections;
    }

    private static string[] KnownSectionNames(bool isDcf)
    {
        var reader = isDcf ? (CanOpenReaderBase)new DcfReader() : new EdsReader();
        var property = typeof(CanOpenReaderBase).GetProperty("KnownSectionNames", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string[])property.GetValue(reader)!;
    }

    private static IEnumerable<(string Section, string Body, bool DcfOnly)> FixtureTemplates(bool isDcf, string? sectionUnderTest)
    {
        var spelling = sectionUnderTest != null && CommissioningSpellings.Contains(sectionUnderTest)
            ? sectionUnderTest
            : CommissioningSpellings[0];

        foreach (var template in Templates)
        {
            if (template.DcfOnly && !isDcf)
                continue;

            if (CommissioningSpellings.Contains(template.Section) && template.Section != spelling)
                continue;

            yield return template;
        }
    }

    private static string BuildFixture(bool isDcf, string? sectionUnderTest)
    {
        var sb = new StringBuilder();
        foreach (var template in FixtureTemplates(isDcf, sectionUnderTest))
        {
            sb.Append('[').Append(template.Section).Append("]\n");
            sb.Append(template.Body).Append('\n');
            sb.Append(VendorKey).Append('=').Append(VendorValue(template.Section)).Append("\n\n");
        }

        return sb.ToString();
    }

    private static string VendorValue(string section) => section + "-vendor";
}
