namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;

/// <summary>
/// Sections whose names look like module, object companion or sub-object sections, but that
/// the reader does not load: module sections of a module that is not parsed from
/// <c>[SupportedModules]</c>, companion sections of a listed object without an object body,
/// and <c>[xxxxsubN]</c> sections of an object whose sub-objects are not loaded. They are
/// kept unchanged in <c>AdditionalSections</c>, reported once per module or object, and
/// written back.
/// </summary>
public class OrphanKnownSectionTests
{
    private static readonly CanOpenFileOptions Strict = new() { StrictParsing = true };

    private const string UnparsedModuleSections = """
        [SupportedModules]
        NrOfEntries=2
        1=0x0001
        2=0x0002

        [M1ModuleInfo]
        ProductName=First
        ProductVersion=1
        ProductRevision=0
        OrderCode=A-1

        [M2ModuleInfo]
        ProductName=Second
        ProductVersion=1
        ProductRevision=0
        OrderCode=A-2

        [M5ModuleInfo]
        ProductName=Fifth
        OrderCode=A-5

        [M5FixedObjects]
        NrOfEntries=1
        1=0x2000

        [M5Fixed2000]
        ParameterName=Fixed Fifth
        DataType=0x0005
        AccessType=ro

        [M5SubExtends]
        NrOfEntries=1
        1=0x6000

        [M5SubExt6000]
        ParameterName=Ext Fifth
        DataType=0x0005
        AccessType=rw

        [M5Comments]
        Lines=1
        Line1=Kept comment

        """;

    private static readonly string[] UnparsedModuleSectionNames =
    {
        "M5ModuleInfo", "M5FixedObjects", "M5Fixed2000", "M5SubExtends", "M5SubExt6000", "M5Comments"
    };

    private const string ListedObjectWithoutBodySections = """
        [2000sub1]
        ParameterName=Orphan sub
        ObjectType=0x7
        DataType=0x0005
        AccessType=rw
        DefaultValue=3

        [2000Name]
        NrOfEntries=1
        1=Orphan name

        [2000ObjectLinks]
        ObjectLinks=1
        1=0x1000

        """;

    private const string ListedObjectWithoutBodyDcfSections = """
        [2000Value]
        NrOfEntries=1
        1=7

        [2000Denotation]
        NrOfEntries=1
        1=Orphan denotation

        """;

    // ---------------------------------------------------------------------------------------
    // Module sections of a module that is not parsed
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ReadString_ModuleBeyondSupportedModulesCount_PreservesSectionsAndReportsOnce()
    {
        // Arrange
        var content = Eds(UnparsedModuleSections);

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.SupportedModules.Select(m => m.ModuleNumber).Should().Equal(1, 2);
        result.Model.SupportedModules[0].ProductName.Should().Be("First");
        result.Model.SupportedModules[1].ProductName.Should().Be("Second");
        result.Model.AdditionalSections.Keys.Should().Contain(UnparsedModuleSectionNames);
        result.Model.AdditionalSections["M5ModuleInfo"].Should().Equal(new Dictionary<string, string>
        {
            ["ProductName"] = "Fifth",
            ["OrderCode"] = "A-5"
        });
        result.Model.AdditionalSections["M5SubExt6000"]["ParameterName"].Should().Be("Ext Fifth");
        result.Model.AdditionalSections["M5Comments"]["Line1"].Should().Be("Kept comment");
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniUnlistedModuleSection)
            .Which.Should().Match<ParseDiagnostic>(d =>
                d.Severity == ParseSeverity.Warning &&
                d.Path == "M5ModuleInfo" &&
                d.Message == "module 5 sections are not loaded: module 5 is not a parsed entry of [SupportedModules]");
    }

    [Fact]
    public void ReadString_DcfModuleBeyondSupportedModulesCount_PreservesSectionsAndReportsOnce()
    {
        // Arrange
        var content = Dcf(UnparsedModuleSections);

        // Act
        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.SupportedModules.Select(m => m.ModuleNumber).Should().Equal(1, 2);
        result.Model.AdditionalSections.Keys.Should().Contain(UnparsedModuleSectionNames);
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniUnlistedModuleSection);
    }

    [Fact]
    public void ReadString_ModuleSubExtendsWithoutModuleInfo_PreservesSectionAndReportsOnce()
    {
        // Arrange — module 1 is within NrOfEntries, but has no [M1ModuleInfo].
        var content = Eds("""
            [SupportedModules]
            NrOfEntries=1
            1=0x0001

            [M1SubExtends]
            NrOfEntries=1
            1=0x6000

            [M1SubExt6000]
            ParameterName=Ext
            DataType=0x0005

            """);

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.SupportedModules.Should().BeEmpty();
        result.Model.AdditionalSections["M1SubExtends"]["1"].Should().Be("0x6000");
        result.Model.AdditionalSections["M1SubExt6000"]["ParameterName"].Should().Be("Ext");
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniUnlistedModuleSection)
            .Which.Path.Should().Be("M1SubExtends");
    }

    [Fact]
    public void ReadString_SectionsOfTwoUnparsedModules_ReportsOncePerModule()
    {
        // Arrange — no [SupportedModules] at all; module numbers 3 and 12.
        var content = Eds("""
            [M3ModuleInfo]
            ProductName=Three

            [M12Comments]
            Lines=0

            [M3Comments]
            Lines=0

            """);

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.AdditionalSections.Keys.Should().Contain(new[] { "M3ModuleInfo", "M12Comments", "M3Comments" });
        result.Diagnostics.Where(d => d.Code == ParseDiagnosticCodes.IniUnlistedModuleSection)
            .Select(d => d.Path).Should().Equal("M3ModuleInfo", "M12Comments");
    }

    [Fact]
    public void ReadString_ModuleNumberBeyondInt32_PreservesSectionAndReportsDigits()
    {
        // Arrange
        var content = Eds("""
            [M99999999999ModuleInfo]
            ProductName=Huge

            """);

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.AdditionalSections["M99999999999ModuleInfo"]["ProductName"].Should().Be("Huge");
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniUnlistedModuleSection)
            .Which.Message.Should().Be(
                "module 99999999999 sections are not loaded: module 99999999999 is not a parsed entry of [SupportedModules]");
    }

    [Fact]
    public void ReadString_ModuleFixedNameWithoutHexIndex_PreservedWithoutModuleDiagnostic()
    {
        // Arrange — the module parser never loads [M5FixedVendor]; it is an ordinary section.
        var content = Eds("""
            [M5FixedVendor]
            VendorKey=kept

            """);

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);
        var strict = () => CanOpenFile.Eds.ReadString(content, Strict);

        // Assert
        result.Model.AdditionalSections["M5FixedVendor"]["VendorKey"].Should().Be("kept");
        result.Diagnostics.Should().NotContain(d => d.Code == ParseDiagnosticCodes.IniUnlistedModuleSection);
        strict.Should().NotThrow();
    }

    [Theory]
    [InlineData("M5Fixed2000")]
    [InlineData("M5Fixed2000sub1")]
    public void ReadString_ModuleFixedObjectOfUnparsedModule_ReportsModuleDiagnostic(string sectionName)
    {
        // Arrange
        var content = Eds("[" + sectionName + "]" + Environment.NewLine + "ParameterName=Fixed" + Environment.NewLine);

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);
        var strict = () => CanOpenFile.Eds.ReadString(content, Strict);

        // Assert
        result.Model.AdditionalSections[sectionName]["ParameterName"].Should().Be("Fixed");
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniUnlistedModuleSection)
            .Which.Path.Should().Be(sectionName);
        strict.Should().Throw<EdsParseException>()
            .Which.Code.Should().Be(ParseDiagnosticCodes.IniUnlistedModuleSection);
    }

    [Fact]
    public void ReadString_ParsedModuleSections_AreNotCopiedOrReported()
    {
        // Arrange
        var content = Eds("""
            [SupportedModules]
            NrOfEntries=1
            1=0x0001

            [M1ModuleInfo]
            ProductName=First

            [M1SubExtends]
            NrOfEntries=1
            1=0x6000

            [M1SubExt6000]
            ParameterName=Ext

            [M1Comments]
            Lines=1
            Line1=c

            """);

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.SupportedModules.Should().ContainSingle();
        result.Model.SupportedModules[0].SubExtensionDefinitions.Should().ContainKey(0x6000);
        result.Model.AdditionalSections.Keys.Should().NotContain(k => k.StartsWith("M1", StringComparison.Ordinal));
        result.Diagnostics.Should().NotContain(d => d.Code == ParseDiagnosticCodes.IniUnlistedModuleSection);
    }

    [Fact]
    public void ReadString_UnparsedModuleSection_StrictParsing_ThrowsEdsParseException()
    {
        // Arrange
        var content = Eds(UnparsedModuleSections);

        // Act
        var act = () => CanOpenFile.Eds.ReadString(content, Strict);

        // Assert
        act.Should().Throw<EdsParseException>().Which.Should().Match<EdsParseException>(e =>
            e.Code == ParseDiagnosticCodes.IniUnlistedModuleSection &&
            e.SectionName == "M5ModuleInfo");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteToString_UnparsedModuleSections_ValidatedRoundTrip(bool validated)
    {
        // Arrange
        var read = CanOpenFile.Eds.ReadStringWithDiagnostics(Eds(UnparsedModuleSections));
        var options = validated ? CanOpenWriteOptions.Validated : null;

        // Act
        var written = CanOpenFile.Eds.WriteToString(read.Model, options);
        var again = CanOpenFile.Eds.ReadStringWithDiagnostics(written);

        // Assert
        AssertSectionsRoundTrip(read.Model.AdditionalSections, again.Model.AdditionalSections, UnparsedModuleSectionNames);
        SectionOrder(written, UnparsedModuleSectionNames).Should().Equal(UnparsedModuleSectionNames);
        again.Model.SupportedModules.Select(m => m.ProductName).Should().Equal("First", "Second");
        again.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniUnlistedModuleSection);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteToString_DcfUnparsedModuleSections_ValidatedRoundTrip(bool validated)
    {
        // Arrange
        var read = CanOpenFile.Dcf.ReadStringWithDiagnostics(Dcf(UnparsedModuleSections));
        var options = validated ? CanOpenWriteOptions.Validated : null;

        // Act
        var written = CanOpenFile.Dcf.WriteToString(read.Model, options);
        var again = CanOpenFile.Dcf.ReadStringWithDiagnostics(written);

        // Assert
        AssertSectionsRoundTrip(read.Model.AdditionalSections, again.Model.AdditionalSections, UnparsedModuleSectionNames);
        SectionOrder(written, UnparsedModuleSectionNames).Should().Equal(UnparsedModuleSectionNames);
        again.Model.SupportedModules.Select(m => m.ProductName).Should().Equal("First", "Second");
    }

    [Fact]
    public void WriteToString_UnparsedModuleSectionWithControlCharacter_ValidatedWriteRejects()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Eds(UnparsedModuleSections));
        eds.AdditionalSections["M5ModuleInfo"]["ProductName"] = "bad\nvalue";

        // Act
        var act = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        act.Should().Throw<ModelValidationException>().Which.Issues.Should().Contain(issue =>
            issue.Path == "AdditionalSections[M5ModuleInfo].ProductName");
    }

    [Fact]
    public void WriteToString_ModuleAddedForPreservedModuleSection_GeneratedSectionWins()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Eds(UnparsedModuleSections));
        eds.SupportedModules.Add(new ModuleInfo { ModuleNumber = 5, ProductName = "Generated" });

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        CountOccurrences(written, "[M5ModuleInfo]").Should().Be(1);
        written.Should().Contain("ProductName=Generated");
        written.Should().NotContain("ProductName=Fifth");
        CountOccurrences(written, "[M5Comments]").Should().Be(1);
    }

    // ---------------------------------------------------------------------------------------
    // Companion sections of a listed object without an object body
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ReadString_ListedObjectWithoutBody_PreservesCompanionsAndReportsOnce()
    {
        // Arrange
        var content = Eds(ListedObjectWithoutBodySections, optionalObjects: "0x2000");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.ObjectDictionary.OptionalObjects.Should().Equal((ushort)0x2000);
        result.Model.ObjectDictionary.Objects.Should().NotContainKey(0x2000);
        result.Model.AdditionalSections["2000sub1"]["DefaultValue"].Should().Be("3");
        result.Model.AdditionalSections["2000Name"]["1"].Should().Be("Orphan name");
        result.Model.AdditionalSections["2000ObjectLinks"]["1"].Should().Be("0x1000");
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniOrphanCompanionSection)
            .Which.Should().Match<ParseDiagnostic>(d =>
                d.Severity == ParseSeverity.Warning &&
                d.Path == "2000sub1" &&
                d.Message == "object 0x2000 is listed, but has no [2000] section; its companion sections are not loaded");
        result.Diagnostics.Should().NotContain(d => d.Code == ParseDiagnosticCodes.IniUnlistedObjectSection);
    }

    [Fact]
    public void ReadString_DcfListedObjectWithoutBody_PreservesValueAndDenotationAndReportsOnce()
    {
        // Arrange
        var content = Dcf(ListedObjectWithoutBodyDcfSections + ListedObjectWithoutBodySections, optionalObjects: "0x2000");

        // Act
        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.ObjectDictionary.Objects.Should().NotContainKey(0x2000);
        result.Model.AdditionalSections["2000Value"].Should().Equal(new Dictionary<string, string>
        {
            ["NrOfEntries"] = "1",
            ["1"] = "7"
        });
        result.Model.AdditionalSections["2000Denotation"]["1"].Should().Be("Orphan denotation");
        result.Model.AdditionalSections["2000sub1"]["DefaultValue"].Should().Be("3");
        result.Model.AdditionalSections["2000Name"]["1"].Should().Be("Orphan name");
        result.Model.AdditionalSections["2000ObjectLinks"]["1"].Should().Be("0x1000");
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniOrphanCompanionSection)
            .Which.Path.Should().Be("2000Value");
    }

    [Fact]
    public void ReadString_ListedObjectWithOnlyPaddedBody_PreservesPaddedBodyAndReports()
    {
        // Arrange — [02000] is not the spelling the reader looks up for 0x2000.
        var content = Eds("""
            [02000]
            ParameterName=Padded
            ObjectType=0x7

            """, optionalObjects: "0x2000");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.ObjectDictionary.Objects.Should().NotContainKey(0x2000);
        result.Model.AdditionalSections["02000"]["ParameterName"].Should().Be("Padded");
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniOrphanCompanionSection)
            .Which.Path.Should().Be("02000");
    }

    [Fact]
    public void ReadString_ListedObjectWithoutBody_StrictParsing_ThrowsEdsParseException()
    {
        // Arrange
        var content = Dcf(ListedObjectWithoutBodyDcfSections, optionalObjects: "0x2000");

        // Act
        var act = () => CanOpenFile.Dcf.ReadString(content, Strict);

        // Assert
        act.Should().Throw<EdsParseException>().Which.Should().Match<EdsParseException>(e =>
            e.Code == ParseDiagnosticCodes.IniOrphanCompanionSection &&
            e.SectionName == "2000Value");
    }

    [Fact]
    public void WriteToString_ListedObjectWithoutBody_RoundTripsPreservedSections()
    {
        // Arrange — a validated write rejects this model on its own (the list cites a missing
        // object); see WriteToString_ListedObjectWithoutBody_ValidatedWriteRejectsMissingObjectOnly.
        var names = new[] { "2000sub1", "2000Name", "2000ObjectLinks" };
        var read = CanOpenFile.Eds.ReadStringWithDiagnostics(Eds(ListedObjectWithoutBodySections, optionalObjects: "0x2000"));

        // Act
        var written = CanOpenFile.Eds.WriteToString(read.Model);
        var again = CanOpenFile.Eds.ReadStringWithDiagnostics(written);

        // Assert
        AssertSectionsRoundTrip(read.Model.AdditionalSections, again.Model.AdditionalSections, names);
        SectionOrder(written, names).Should().Equal(names);
        again.Model.ObjectDictionary.OptionalObjects.Should().Equal((ushort)0x2000);
        again.Model.ObjectDictionary.Objects.Should().NotContainKey(0x2000);
        again.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniOrphanCompanionSection);
    }

    [Fact]
    public void WriteToString_DcfListedObjectWithoutBody_RoundTripsPreservedSections()
    {
        // Arrange — a validated write rejects this model on its own (the list cites a missing
        // object); see WriteToString_DcfListedObjectWithoutBody_ValidatedWriteRejectsMissingObjectOnly.
        var names = new[] { "2000Value", "2000Denotation", "2000sub1", "2000Name", "2000ObjectLinks" };
        var read = CanOpenFile.Dcf.ReadStringWithDiagnostics(
            Dcf(ListedObjectWithoutBodyDcfSections + ListedObjectWithoutBodySections, optionalObjects: "0x2000"));

        // Act
        var written = CanOpenFile.Dcf.WriteToString(read.Model);
        var again = CanOpenFile.Dcf.ReadStringWithDiagnostics(written);

        // Assert
        AssertSectionsRoundTrip(read.Model.AdditionalSections, again.Model.AdditionalSections, names);
        SectionOrder(written, names).Should().Equal(names);
        again.Model.ObjectDictionary.Objects.Should().NotContainKey(0x2000);
        again.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniOrphanCompanionSection);
    }

    [Fact]
    public void WriteToString_ListedObjectWithoutBody_ValidatedWriteRejectsMissingObjectOnly()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Eds(ListedObjectWithoutBodySections, optionalObjects: "0x2000"));

        // Act
        var act = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert — the preserved sections themselves are valid INI text.
        act.Should().Throw<ModelValidationException>().Which.Issues.Should().ContainSingle()
            .Which.Path.Should().Be("ObjectDictionary.OptionalObjects");
    }

    [Fact]
    public void WriteToString_DcfListedObjectWithoutBody_ValidatedWriteRejectsMissingObjectOnly()
    {
        // Arrange
        var dcf = CanOpenFile.Dcf.ReadString(
            Dcf(ListedObjectWithoutBodyDcfSections + ListedObjectWithoutBodySections, optionalObjects: "0x2000"));

        // Act
        var act = () => CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);

        // Assert
        act.Should().Throw<ModelValidationException>().Which.Issues.Should().ContainSingle()
            .Which.Path.Should().Be("ObjectDictionary.OptionalObjects");
    }

    [Fact]
    public void WriteToString_DcfBodyAddedForPreservedCompanion_GeneratedSectionWins()
    {
        // Arrange
        var dcf = CanOpenFile.Dcf.ReadString(
            Dcf(ListedObjectWithoutBodyDcfSections, optionalObjects: "0x2000"));
        var obj = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "Added",
            ObjectType = CanOpenObjectType.Array,
            DataType = 0x0005,
            CompactSubObj = 1
        };
        obj.SubObjects[0] = new CanOpenSubObject { SubIndex = 0, ParameterName = "Count", DataType = 0x0005, DefaultValue = "1" };
        obj.SubObjects[1] = new CanOpenSubObject { SubIndex = 1, ParameterName = "Added1", DataType = 0x0005, ParameterValue = "9" };
        dcf.ObjectDictionary.Objects[0x2000] = obj;

        // Act
        var written = CanOpenFile.Dcf.WriteToString(dcf);

        // Assert
        CountOccurrences(written, "[2000Value]").Should().Be(1);
        CanOpenFile.Dcf.ReadString(written).ObjectDictionary.Objects[0x2000].SubObjects[1].ParameterValue
            .Should().Be("9");
    }

    // ---------------------------------------------------------------------------------------
    // [xxxxsubN] of an object whose sub-objects are not loaded
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("0x7")]
    [InlineData("0x5")]
    [InlineData("0x2")]
    public void ReadString_SubObjectSectionOfObjectWithoutSubNumber_PreservesAndReportsOnce(string objectType)
    {
        // Arrange — VAR, DEFTYPE and DOMAIN without SubNumber/CompactSubObj load no sub-objects.
        var content = Eds(SubObjectsWithoutSubNumber(objectType), optionalObjects: "0x2000");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        var obj = result.Model.ObjectDictionary.Objects[0x2000];
        obj.ParameterName.Should().Be("Plain");
        obj.SubObjects.Should().BeEmpty();
        result.Model.AdditionalSections["2000sub0"]["DefaultValue"].Should().Be("1");
        result.Model.AdditionalSections["2000sub1"]["ParameterName"].Should().Be("Stray");
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniOrphanSubObjectSection)
            .Which.Should().Match<ParseDiagnostic>(d =>
                d.Severity == ParseSeverity.Warning &&
                d.Path == "2000sub0" &&
                d.Message == "sub-object sections of object 0x2000 are not loaded: the object has no SubNumber or CompactSubObj");
    }

    [Fact]
    public void ReadString_DcfSubObjectSectionOfVarWithoutSubNumber_PreservesAndReportsOnce()
    {
        // Arrange
        var content = Dcf(SubObjectsWithoutSubNumber("0x7"), optionalObjects: "0x2000");

        // Act
        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.ObjectDictionary.Objects[0x2000].SubObjects.Should().BeEmpty();
        result.Model.AdditionalSections.Keys.Should().Contain(new[] { "2000sub0", "2000sub1" });
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniOrphanSubObjectSection);
    }

    [Fact]
    public void ReadString_NonCanonicalSubObjectSpellingOfLoadedRecord_PreservesAndReports()
    {
        // Arrange — [2000sub01] is not the spelling the reader looks up for sub-index 1.
        var content = Eds("""
            [2000]
            ParameterName=Record
            ObjectType=0x9
            SubNumber=1

            [2000sub0]
            ParameterName=Count
            DataType=0x0005
            AccessType=ro
            DefaultValue=1

            [2000sub01]
            ParameterName=Padded
            DataType=0x0005

            """, optionalObjects: "0x2000");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.ObjectDictionary.Objects[0x2000].SubObjects.Keys.Should().Equal((byte)0);
        result.Model.AdditionalSections["2000sub01"]["ParameterName"].Should().Be("Padded");
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniOrphanSubObjectSection)
            .Which.Should().Match<ParseDiagnostic>(d =>
                d.Path == "2000sub01" &&
                d.Message == "sub-object section [2000sub01] of object 0x2000 is not loaded: the reader expects [2000sub1]");
        result.Model.AdditionalSections.Should().NotContainKey("2000sub0");
    }

    [Fact]
    public void ReadString_ListedRecordWithSubObjects_LoadsSubObjectsAndReportsNothing()
    {
        // Arrange
        var content = Eds("""
            [2000]
            ParameterName=Record
            ObjectType=0x9
            SubNumber=2

            [2000sub0]
            ParameterName=Count
            DataType=0x0005
            AccessType=ro
            DefaultValue=1

            [2000sub1]
            ParameterName=Value
            DataType=0x0005
            AccessType=rw

            """, optionalObjects: "0x2000");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.ObjectDictionary.Objects[0x2000].SubObjects.Keys.Should().Equal((byte)0, (byte)1);
        result.Model.AdditionalSections.Should().BeEmpty();
        result.Diagnostics.Should().NotContain(d =>
            d.Code == ParseDiagnosticCodes.IniOrphanSubObjectSection ||
            d.Code == ParseDiagnosticCodes.IniOrphanCompanionSection);
    }

    [Fact]
    public void ReadString_SubObjectSectionOfVarWithoutSubNumber_StrictParsing_ThrowsEdsParseException()
    {
        // Arrange
        var content = Eds(SubObjectsWithoutSubNumber("0x7"), optionalObjects: "0x2000");

        // Act
        var act = () => CanOpenFile.Eds.ReadString(content, Strict);

        // Assert
        act.Should().Throw<EdsParseException>().Which.Should().Match<EdsParseException>(e =>
            e.Code == ParseDiagnosticCodes.IniOrphanSubObjectSection &&
            e.SectionName == "2000sub0");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteToString_SubObjectSectionOfVarWithoutSubNumber_ValidatedRoundTrip(bool validated)
    {
        // Arrange
        var names = new[] { "2000sub0", "2000sub1" };
        var read = CanOpenFile.Eds.ReadStringWithDiagnostics(Eds(SubObjectsWithoutSubNumber("0x7"), optionalObjects: "0x2000"));
        var options = validated ? CanOpenWriteOptions.Validated : null;

        // Act
        var written = CanOpenFile.Eds.WriteToString(read.Model, options);
        var again = CanOpenFile.Eds.ReadStringWithDiagnostics(written);

        // Assert
        AssertSectionsRoundTrip(read.Model.AdditionalSections, again.Model.AdditionalSections, names);
        SectionOrder(written, names).Should().Equal(names);
        again.Model.ObjectDictionary.Objects[0x2000].SubObjects.Should().BeEmpty();
        again.Model.ObjectDictionary.Objects[0x2000].ParameterName.Should().Be("Plain");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteToString_DcfSubObjectSectionOfVarWithoutSubNumber_ValidatedRoundTrip(bool validated)
    {
        // Arrange
        var names = new[] { "2000sub0", "2000sub1" };
        var read = CanOpenFile.Dcf.ReadStringWithDiagnostics(Dcf(SubObjectsWithoutSubNumber("0x7"), optionalObjects: "0x2000"));
        var options = validated ? CanOpenWriteOptions.Validated : null;

        // Act
        var written = CanOpenFile.Dcf.WriteToString(read.Model, options);
        var again = CanOpenFile.Dcf.ReadStringWithDiagnostics(written);

        // Assert
        AssertSectionsRoundTrip(read.Model.AdditionalSections, again.Model.AdditionalSections, names);
        again.Model.ObjectDictionary.Objects[0x2000].SubObjects.Should().BeEmpty();
    }

    [Fact]
    public void ConvertToDcf_PreservedSubObjectSection_IsWrittenToDcf()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Eds(SubObjectsWithoutSubNumber("0x7"), optionalObjects: "0x2000"));
        eds.FileInfo.FileName = "device.eds";

        // Act
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 5, timestamp: new DateTime(2026, 1, 2), baudrate: 500);
        var written = CanOpenFile.Dcf.WriteToString(dcf);

        // Assert
        CanOpenFile.Dcf.ReadString(written).AdditionalSections["2000sub1"]["ParameterName"].Should().Be("Stray");
    }

    [Fact]
    public void WriteToString_SubObjectsAddedForPreservedSubSection_GeneratedSectionWins()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Eds(SubObjectsWithoutSubNumber("0x7"), optionalObjects: "0x2000"));
        var obj = eds.ObjectDictionary.Objects[0x2000];
        obj.ObjectType = CanOpenObjectType.Record;
        obj.SubNumber = 2;
        obj.SubObjects[0] = new CanOpenSubObject { SubIndex = 0, ParameterName = "Count", DataType = 0x0005, DefaultValue = "1" };
        obj.SubObjects[1] = new CanOpenSubObject { SubIndex = 1, ParameterName = "Generated", DataType = 0x0005 };

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        CountOccurrences(written, "[2000sub1]").Should().Be(1);
        written.Should().NotContain("ParameterName=Stray");
        CanOpenFile.Eds.ReadString(written).ObjectDictionary.Objects[0x2000].SubObjects[1].ParameterName
            .Should().Be("Generated");
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------

    private static string SubObjectsWithoutSubNumber(string objectType) => """
        [2000]
        ParameterName=Plain
        ObjectType=
        """ + objectType + """

        DataType=0x0005
        AccessType=rw
        DefaultValue=0

        [2000sub0]
        ParameterName=Count
        DataType=0x0005
        AccessType=ro
        DefaultValue=1

        [2000sub1]
        ParameterName=Stray
        DataType=0x0005
        AccessType=rw

        """;

    private static void AssertSectionsRoundTrip(
        Dictionary<string, Dictionary<string, string>> expected,
        Dictionary<string, Dictionary<string, string>> actual,
        IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            actual.Should().ContainKey(name);
            actual[name].Should().Equal(expected[name]);
        }
    }

    /// <summary>The section headers of <paramref name="names"/> in the order they appear in <paramref name="text"/>.</summary>
    private static List<string> SectionOrder(string text, IEnumerable<string> names)
        => names
            .Select(name => (Name: name, Position: text.IndexOf("[" + name + "]", StringComparison.Ordinal)))
            .Where(entry => entry.Position >= 0)
            .OrderBy(entry => entry.Position)
            .Select(entry => entry.Name)
            .ToList();

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal))
            count++;

        return count;
    }

    private static string Eds(string extraSections, string? optionalObjects = null)
        => """
            [DeviceInfo]
            VendorName=Test
            ProductName=Test

            [MandatoryObjects]
            SupportedObjects=1
            1=0x1000

            [1000]
            ParameterName=Device Type
            ObjectType=0x7
            DataType=0x0007
            AccessType=ro
            DefaultValue=0
            PDOMapping=0

            """ + OptionalObjects(optionalObjects) + extraSections;

    private static string Dcf(string extraSections, string? optionalObjects = null)
        => """
            [DeviceInfo]
            VendorName=Test
            ProductName=Test

            [DeviceComissioning]
            NodeID=5
            NodeName=TestNode
            Baudrate=500
            NetNumber=1
            NetworkName=TestNetwork
            CANopenManager=0

            [MandatoryObjects]
            SupportedObjects=1
            1=0x1000

            [1000]
            ParameterName=Device Type
            ObjectType=0x7
            DataType=0x0007
            AccessType=ro
            DefaultValue=0
            PDOMapping=0

            """ + OptionalObjects(optionalObjects) + extraSections;

    private static string OptionalObjects(string? index)
        => index == null
            ? string.Empty
            : """
                [OptionalObjects]
                SupportedObjects=1
                1=
                """ + index + """


                """;
}
