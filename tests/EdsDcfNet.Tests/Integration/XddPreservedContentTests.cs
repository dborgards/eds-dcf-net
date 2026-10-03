namespace EdsDcfNet.Tests.Integration;

using System.Xml.Linq;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Tests.Infrastructure;
using EdsDcfNet.Utilities;
using EdsDcfNet.Validation;

/// <summary>
/// WP-43 (X12): XDD/XDC content the model does not represent survives XDD→XDD, XDC→XDC and
/// ConvertToDcf, and models without an XDD source get a schema-valid <c>DeviceFunction</c>.
/// </summary>
public class XddPreservedContentTests
{
    private const string CorpusXdd = "Fixtures/Corpus/canopen-node/basicDevice.xdd";
    private const string FixtureXdd = "Fixtures/preserved_content.xdd";

    private static readonly XNamespace Co = "http://www.canopen.org/xml/1.1";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    // ── fixture ──────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateFile_PreservedContentFixture_ReportsNoProblems()
    {
        // Act
        var problems = Cia311Schema.ValidateFile(FixtureXdd);

        // Assert
        problems.Should().BeEmpty();
    }

    [Fact]
    public void WriteToString_PreservedContentFixtureRoundTrip_ValidatesAgainstSchema()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadFile(FixtureXdd);

        // Act
        var problems = Cia311Schema.Validate(CanOpenFile.Xdd.WriteToString(eds));

        // Assert
        problems.Should().BeEmpty();
    }

    // ── corpus basicDevice.xdd ───────────────────────────────────────────────

    [Fact]
    public void WriteToString_CorpusXddRoundTrip_KeepsDeviceFunctionUnchanged()
    {
        // Arrange
        var source = XDocument.Load(CorpusXdd);

        // Act
        var written = RoundTrip(CorpusXdd);

        // Assert
        XNode.DeepEquals(Comparable(Single(written, "DeviceFunction")), Comparable(Single(source, "DeviceFunction")))
            .Should().BeTrue();
    }

    [Fact]
    public void WriteToString_CorpusXddRoundTrip_KeepsGeneratorCommentsBeforeRoot()
    {
        // Arrange
        var expected = XDocument.Load(CorpusXdd).Nodes().TakeWhile(n => n is not XElement).OfType<XComment>()
            .Select(c => c.Value).ToList();

        // Act
        var written = RoundTrip(CorpusXdd);

        // Assert
        expected.Should().HaveCount(2);
        written.Nodes().TakeWhile(n => n is not XElement).OfType<XComment>().Select(c => c.Value)
            .Should().Equal(expected);
    }

    [Fact]
    public void WriteToString_CorpusXddRoundTrip_KeepsProfileAttributesHeaderAndProductText()
    {
        // Act
        var written = RoundTrip(CorpusXdd);

        // Assert
        foreach (var body in new[] { DeviceBody(written), NetworkBody(written) })
        {
            ((string?)body.Attribute("supportedLanguages")).Should().Be("en");
            ((string?)body.Attribute("formatName")).Should().Be("CANopen");
            ((string?)body.Attribute("formatVersion")).Should().Be("1.0");
        }

        Header(DeviceBody(written)).Element("ProfileIdentification")!.Value.Should().Be("CANopen device profile");
        Header(DeviceBody(written)).Element("ProfileRevision")!.Value.Should().Be("1.1");
        Single(written, "productText").Element("description")!.Value
            .Should().Be("Basic CANopen device with example usage.");
    }

    [Fact]
    public void WriteToString_CorpusXddRoundTrip_KeepsEachProfilesFileModifiedBy()
    {
        // Act — the device profile names a modifier, the network profile does not.
        var written = RoundTrip(CorpusXdd);

        // Assert
        ((string?)DeviceBody(written).Attribute("fileModifiedBy")).Should().Be("Janez Paternoster");
        NetworkBody(written).Attribute("fileModifiedBy").Should().BeNull();
        Cia311Schema.Validate(written.ToString()).Should().BeEmpty();
    }

    // ── fixture with moduleManagement, identity, rangeSelector ───────────────

    [Fact]
    public void WriteToString_FixtureRoundTrip_KeepsModuleManagementInBothContexts()
    {
        // Arrange
        var source = XDocument.Load(FixtureXdd);

        // Act
        var written = RoundTrip(FixtureXdd);

        // Assert — global under DeviceManager, local under ApplicationLayers.
        var deviceModules = Single(written, "DeviceManager").Element(Co + "moduleManagement");
        var networkModules = Single(written, "ApplicationLayers").Element("moduleManagement");
        deviceModules.Should().NotBeNull();
        networkModules.Should().NotBeNull();
        XNode.DeepEquals(Comparable(deviceModules!), Comparable(Single(source, "DeviceManager").Element(Co + "moduleManagement")!))
            .Should().BeTrue();
        XNode.DeepEquals(Comparable(networkModules!), Comparable(Single(source, "ApplicationLayers").Element("moduleManagement")!))
            .Should().BeTrue();
        networkModules!.Descendants().Select(e => e.Name.Namespace).Should().OnlyContain(ns => ns == XNamespace.None);
    }

    [Fact]
    public void WriteToString_FixtureRoundTrip_KeepsIdentityRangeSelectorAndSubObjectFlags()
    {
        // Arrange
        var source = XDocument.Load(FixtureXdd);

        // Act
        var written = RoundTrip(FixtureXdd);

        // Assert
        XNode.DeepEquals(Comparable(Single(written, "identity")), Comparable(Single(source, "identity"))).Should().BeTrue();
        ((string?)Object(written, "1000").Attribute("rangeSelector")).Should().Be("base");
        var vendor = Object(written, "1018").Elements("CANopenSubObject").Single(e => e.Attribute("subIndex")?.Value == "01");
        ((string?)vendor.Attribute("rangeSelector")).Should().Be("vendor");
        ((string?)vendor.Attribute("objFlags")).Should().Be("0002");
    }

    [Fact]
    public void WriteToString_FixtureRoundTrip_KeepsReadOnlyOnModelledIdentityElements()
    {
        // Act
        var written = RoundTrip(FixtureXdd);

        // Assert
        var identity = Single(written, "DeviceIdentity");
        ((string?)identity.Element(Co + "vendorName")!.Attribute("readOnly")).Should().Be("false");
        ((string?)identity.Element(Co + "vendorID")!.Attribute("readOnly")).Should().Be("false");
        ((string?)identity.Element(Co + "productName")!.Attribute("readOnly")).Should().Be("false");
        identity.Element(Co + "productID")!.Attribute("readOnly").Should().BeNull();
    }

    [Fact]
    public void WriteToString_FixtureRoundTrip_KeepsUnmodelledIdentityElementsInSchemaOrder()
    {
        // Arrange
        var expected = Single(XDocument.Load(FixtureXdd), "DeviceIdentity").Elements().Select(e => e.Name).ToList();

        // Act
        var written = RoundTrip(FixtureXdd);

        // Assert
        Single(written, "DeviceIdentity").Elements().Select(e => e.Name).Should().Equal(expected);
        Single(written, "buildDate").Value.Should().Be("2026-01-15");
        Single(written, "instanceName").Value.Should().Be("io1");
    }

    [Fact]
    public void WriteToString_FixtureRoundTrip_KeepsProfileHeadersBodyAttributesAndExternalHandle()
    {
        // Act
        var written = RoundTrip(FixtureXdd);

        // Assert
        Header(DeviceBody(written)).Element("ProfileIdentification")!.Value.Should().Be("preserved-content-device");
        Header(DeviceBody(written)).Element("ProfileDate")!.Value.Should().Be("2026-01-01");
        Header(NetworkBody(written)).Element("ProfileIdentification")!.Value.Should().Be("preserved-content-network");
        ((string?)DeviceBody(written).Attribute("deviceClass")).Should().Be("modular");
        ((string?)DeviceBody(written).Attribute("supportedLanguages")).Should().Be("en de");
        ((string?)Single(written, "ApplicationLayers").Attribute("conformanceClass")).Should().Be("Class B");
        ((string?)Single(written, "ApplicationLayers").Attribute("communicationEntityType")).Should().Be("slave");
        DeviceBody(written).Elements().Select(e => e.Name.LocalName).Should().Equal(
            "DeviceIdentity", "DeviceManager", "DeviceFunction", "ExternalProfileHandle");
        DeviceBody(written).Element("ExternalProfileHandle")!.Element("ProfileLocation")!.Value.Should().Be("legacy/io.eds");
    }

    [Fact]
    public void WriteToString_FixtureRoundTrip_WritesEachRootCommentOnce()
    {
        // Act
        var written = RoundTrip(FixtureXdd);

        // Assert — tool comments first, then the comment that carries Comments.
        written.Nodes().TakeWhile(n => n is not XElement).OfType<XComment>().Select(c => c.Value).Should().Equal(
            "Generated by Example Tool 2.1",
            "Copyright Example Automation Inc.",
            "EdsDcfNet.Comment 1: device note");
    }

    [Fact]
    public void WriteToString_FixtureRoundTrip_KeepsCommentInsideKeptFragment()
    {
        // Act
        var written = RoundTrip(FixtureXdd);

        // Assert
        Single(written, "characteristicsList").Nodes().OfType<XComment>().Select(c => c.Value)
            .Should().Equal("kept with its fragment");
    }

    [Fact]
    public void WriteToString_SameModelTwice_WritesTheSameDocument()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadFile(FixtureXdd);

        // Act
        var first = CanOpenFile.Xdd.WriteToString(eds);
        var second = CanOpenFile.Xdd.WriteToString(eds);

        // Assert
        second.Should().Be(first);
    }

    // ── file attributes per profile (rule 13) ────────────────────────────────

    [Fact]
    public void WriteToString_ProfilesWithDifferentFileAttributes_KeepsEachProfilesValues()
    {
        // Act
        var written = RoundTrip(FixtureXdd);

        // Assert
        Attributes(DeviceBody(written)).Should().Equal(("3", "2026-02-01", "Device Editor"));
        Attributes(NetworkBody(written)).Should().Equal(("7", "2026-03-01", "Network Editor"));
    }

    [Fact]
    public void WriteToString_ModifiedByChangedAfterRead_WritesNewValueToBothProfiles()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadFile(FixtureXdd);
        eds.FileInfo.ModifiedBy = "Caller";

        // Act
        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(eds));

        // Assert — the unchanged fields keep each profile's value.
        Attributes(DeviceBody(written)).Should().Equal(("3", "2026-02-01", "Caller"));
        Attributes(NetworkBody(written)).Should().Equal(("7", "2026-03-01", "Caller"));
    }

    [Fact]
    public void WriteToString_FileVersionAndModificationDateChangedAfterRead_WritesNewValuesToBothProfiles()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadFile(FixtureXdd);
        eds.FileInfo.FileVersion = 9;
        eds.FileInfo.ModificationDate = "04-05-2026";

        // Act
        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(eds));

        // Assert
        Attributes(DeviceBody(written)).Should().Equal(("9", "2026-04-05", "Device Editor"));
        Attributes(NetworkBody(written)).Should().Equal(("9", "2026-04-05", "Network Editor"));
    }

    [Fact]
    public void ReadString_NetworkProfileFileAttributes_AreNotReportedTwice()
    {
        // Arrange — both profiles carry a fileVersion the model cannot hold.
        var xml = Mutate(FixtureXdd, doc =>
        {
            DeviceBody(doc).SetAttributeValue("fileVersion", "1.2");
            NetworkBody(doc).SetAttributeValue("fileVersion", "7.1");
        });

        // Act
        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml);
        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(result.Model));

        // Assert
        result.Diagnostics.Should().ContainSingle(d => d.Path == "ProfileBody.fileVersion");
        ((string?)DeviceBody(written).Attribute("fileVersion")).Should().Be("1.2");
        ((string?)NetworkBody(written).Attribute("fileVersion")).Should().Be("7.1");
    }

    // ── DeviceFunction without a source ──────────────────────────────────────

    [Fact]
    public void WriteToString_EdsToXdd_WritesDerivedProductNameCharacteristic()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadFile("Fixtures/sample_device.eds");

        // Act
        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(eds));

        // Assert
        var characteristic = Single(written, "characteristic");
        characteristic.Element(Co + "characteristicName")!.Element("label")!.Value.Should().Be("Product name");
        ((string?)characteristic.Element(Co + "characteristicName")!.Element("label")!.Attribute("lang")).Should().Be("en");
        characteristic.Element(Co + "characteristicContent")!.Element("label")!.Value.Should().Be(eds.DeviceInfo.ProductName);
        Cia311Schema.Validate(written.ToString()).Should().BeEmpty();
    }

    [Fact]
    public void ReadString_DerivedDeviceFunction_FollowsLaterProductNameChange()
    {
        // Arrange — the derived DeviceFunction is not kept, so it follows the model.
        var eds = CanOpenFile.Eds.ReadFile("Fixtures/sample_device.eds");
        var reread = CanOpenFile.Xdd.ReadString(CanOpenFile.Xdd.WriteToString(eds));
        reread.DeviceInfo.ProductName = "Renamed";

        // Act
        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(reread));

        // Assert
        written.Descendants(Co + "DeviceFunction").Should().ContainSingle();
        Single(written, "characteristicContent").Element("label")!.Value.Should().Be("Renamed");
    }

    [Fact]
    public void WriteToString_LegacyEmptyDeviceFunction_IsReplacedByDerivedOne()
    {
        // Act — older outputs of this library wrote <DeviceFunction/>.
        var written = RoundTrip("Fixtures/sample_device.xdd");

        // Assert
        written.Descendants(Co + "DeviceFunction").Should().ContainSingle();
        Single(written, "characteristicContent").Element("label")!.Value.Should().Be("IO-Module 16x16");
    }

    [Fact]
    public void WriteToString_EdsToXddWithoutCreationDate_OnlyLacksFileCreationDate()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadFile("Fixtures/sample_device.eds");
        eds.FileInfo.CreationDate = string.Empty;

        // Act
        var plain = CanOpenFile.Xdd.WriteToString(eds);
        var validated = () => CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        Cia311Schema.Validate(plain).Should().HaveCount(2).And.OnlyContain(p => p.Contains("fileCreationDate"));
        validated.Should().Throw<ModelValidationException>().Which.Issues.Should().Contain(issue =>
            issue.Code == ValidationIssueCodes.XddFileCreationDateMissing);
    }

    [Fact]
    public void WriteToString_ModelBuiltInCode_ValidatesAsXddAndAfterConversionAsXdc()
    {
        // Arrange — the two values that are not derived: a creation date and one object.
        var eds = new ElectronicDataSheet();
        eds.FileInfo.CreationDate = "01-01-2026";
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000, ParameterName = "Device type", ObjectType = 7, DataType = 7, DefaultValue = "0",
        };
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 5, timestamp: new DateTime(2026, 1, 1));

        // Act
        var xdd = Cia311Schema.Validate(CanOpenFile.Xdd.WriteToString(eds));
        var xdc = Cia311Schema.Validate(CanOpenFile.Xdc.WriteToString(dcf));

        // Assert
        xdd.Should().BeEmpty();
        xdc.Should().BeEmpty();
    }

    [Fact]
    public void WriteToString_DcfToXdc_ValidatesAgainstSchema()
    {
        // Arrange
        var dcf = CanOpenFile.Dcf.ReadFile("Fixtures/minimal.dcf");
        dcf.FileInfo.CreationDate = "01-15-2026";

        // Act
        var problems = Cia311Schema.Validate(CanOpenFile.Xdc.WriteToString(dcf));

        // Assert
        problems.Should().BeEmpty();
    }

    // ── namespace-less sources ───────────────────────────────────────────────

    [Fact]
    public void WriteToString_NamespacelessSource_QualifiesKeptFragmentsFromParentTable()
    {
        // Arrange — the fixture as an older library version would have written it.
        var xml = WithoutNamespaces(XDocument.Load(FixtureXdd));

        // Act
        var written = CanOpenFile.Xdd.WriteToString(CanOpenFile.Xdd.ReadString(xml));
        var document = XDocument.Parse(written);

        // Assert
        Cia311Schema.Validate(written).Should().BeEmpty();
        Single(document, "DeviceManager").Element(Co + "moduleManagement").Should().NotBeNull();
        Single(document, "ApplicationLayers").Element("moduleManagement").Should().NotBeNull();
        Single(document, "identity").Element(Co + "vendorID").Should().NotBeNull();
    }

    [Fact]
    public void WriteToString_NamespacelessSourceWithUnknownElement_KeepsItUnqualified()
    {
        // Arrange
        var source = XDocument.Load(FixtureXdd);
        Single(source, "NetworkManagement").Add(new XElement("vendorExtension", new XElement("note", "x")));
        var xml = WithoutNamespaces(source);

        // Act
        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(CanOpenFile.Xdd.ReadString(xml)));

        // Assert
        var extension = Single(written, "NetworkManagement").Element("vendorExtension");
        extension.Should().NotBeNull();
        extension!.Element("note")!.Value.Should().Be("x");
    }

    // ── conversions and XDC (rules 13 and 17) ────────────────────────────────

    [Fact]
    public void ConvertToDcf_XddWithKeptContent_KeepsItInXdcOutput()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadFile(FixtureXdd);

        // Act
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 5, timestamp: new DateTime(2026, 6, 1, 8, 0, 0));
        var written = XDocument.Parse(CanOpenFile.Xdc.WriteToString(dcf));

        // Assert
        Cia311Schema.Validate(written.ToString()).Should().BeEmpty();
        Single(written, "characteristicContent").Element("label")!.Value.Should().Be("8 digital");
        Single(written, "DeviceManager").Element(Co + "moduleManagement").Should().NotBeNull();
        ((string?)Object(written, "1000").Attribute("rangeSelector")).Should().Be("base");
        Header(DeviceBody(written)).Element("ProfileIdentification")!.Value.Should().Be("preserved-content-device");
    }

    [Fact]
    public void ConvertToDcf_XddWithDifferentProfileFileAttributes_KeepsUnchangedNetworkValues()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadFile(FixtureXdd);

        // Act — the conversion sets a new name and creation date and keeps the file version.
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 5, timestamp: new DateTime(2026, 6, 1, 8, 0, 0));
        var written = XDocument.Parse(CanOpenFile.Xdc.WriteToString(dcf));

        // Assert
        ((string?)DeviceBody(written).Attribute("fileVersion")).Should().Be("3");
        ((string?)NetworkBody(written).Attribute("fileVersion")).Should().Be("7");
        ((string?)DeviceBody(written).Attribute("fileName")).Should().Be("preserved_content.dcf");
        ((string?)NetworkBody(written).Attribute("fileName")).Should().Be("preserved_content.dcf");
        ((string?)NetworkBody(written).Attribute("fileCreationDate")).Should().Be("2026-06-01");
    }

    [Fact]
    public void ConvertToDcf_XddWithCommissioningAndActualValues_ModelWinsInXdc()
    {
        // Arrange — an XDD (not XDC) read does not model these, so they are kept.
        var xml = Mutate(FixtureXdd, doc =>
        {
            var management = Single(doc, "NetworkManagement");
            management.Add(new XElement("deviceCommissioning",
                new XAttribute("nodeID", "9"), new XAttribute("nodeName", "kept"), new XAttribute("actualBaudRate", "250 Kbps"),
                new XAttribute("networkNumber", "1"), new XAttribute("networkName", "net"), new XAttribute("CANopenManager", "false")));
            management.Add(new XElement("vendorExtension"));
            var obj = Object(doc, "1000");
            obj.SetAttributeValue("actualValue", "0x1");
            obj.SetAttributeValue("denotation", "kept denotation");
        });
        var eds = CanOpenFile.Xdd.ReadString(xml);

        // Act
        var xdd = XDocument.Parse(CanOpenFile.Xdd.WriteToString(eds));
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 5, timestamp: new DateTime(2026, 6, 1, 8, 0, 0));
        var xdc = XDocument.Parse(CanOpenFile.Xdc.WriteToString(dcf));

        // Assert
        ((string?)Single(xdd, "deviceCommissioning").Attribute("nodeName")).Should().Be("kept");
        ((string?)Object(xdd, "1000").Attribute("actualValue")).Should().Be("0x1");
        ((string?)Object(xdd, "1000").Attribute("denotation")).Should().Be("kept denotation");

        ((string?)Single(xdc, "deviceCommissioning").Attribute("nodeID")).Should().Be("5");
        Object(xdc, "1000").Attribute("actualValue").Should().BeNull();
        Object(xdc, "1000").Attribute("denotation").Should().BeNull();
        ((string?)Object(xdc, "1000").Attribute("rangeSelector")).Should().Be("base");
        Single(xdc, "NetworkManagement").Element("vendorExtension").Should().NotBeNull();
    }

    [Fact]
    public void WriteToString_XdcRoundTrip_KeepsUnmodelledContentAndModelledValuesOnce()
    {
        // Arrange
        var xml = Mutate(FixtureXdd, doc =>
        {
            Single(doc, "NetworkManagement").Add(new XElement("deviceCommissioning",
                new XAttribute("nodeID", "9"), new XAttribute("nodeName", "node"), new XAttribute("actualBaudRate", "250 Kbps"),
                new XAttribute("networkNumber", "1"), new XAttribute("networkName", "net"), new XAttribute("CANopenManager", "false")));
            Object(doc, "1000").SetAttributeValue("actualValue", "0x000F0191");
        });

        // Act
        var dcf = CanOpenFile.Xdc.ReadString(xml);
        dcf.DeviceCommissioning.NodeName = "changed";
        var written = CanOpenFile.Xdc.WriteToString(dcf);
        var document = XDocument.Parse(written);

        // Assert
        Cia311Schema.Validate(written).Should().BeEmpty();
        ((string?)Single(document, "deviceCommissioning").Attribute("nodeName")).Should().Be("changed");
        ((string?)Object(document, "1000").Attribute("actualValue")).Should().Be("0x000F0191");
        Single(document, "characteristicContent").Element("label")!.Value.Should().Be("8 digital");
        Attributes(NetworkBody(document)).Should().Equal(("7", "2026-03-01", "Network Editor"));
    }

    [Fact]
    public void CloneXddPreserved_KeptContent_IsCopiedWithoutSharedNodes()
    {
        // Arrange
        var eds = CanOpenFile.Xdd.ReadFile(FixtureXdd);
        var source = eds.XddPreserved!;

        // Act
        var clone = ModelCloner.CloneXddPreserved(source)!;
        var objectClone = ModelCloner.CloneObject(eds.ObjectDictionary.Objects[0x1000]);
        clone.Elements[XddPreservedContent.DeviceProfileBody][0].RemoveNodes();
        clone.NetworkFileInfo!.ModifiedBy = "changed";
        objectClone.XddPreservedAttributes![0].Value = "changed";

        // Assert
        clone.RootComments.Should().Equal(source.RootComments);
        clone.FileInfoBaseline.Should().Equal(source.FileInfoBaseline);
        clone.Attributes.Keys.Should().BeEquivalentTo(source.Attributes.Keys);
        source.Elements[XddPreservedContent.DeviceProfileBody][0].HasElements.Should().BeTrue();
        source.NetworkFileInfo!.ModifiedBy.Should().Be("Network Editor");
        ((string?)eds.ObjectDictionary.Objects[0x1000].XddPreservedAttributes![0]).Should().Be("base");
        ModelCloner.CloneXddPreserved(null).Should().BeNull();
    }

    // ── AdditionalSections mirror ────────────────────────────────────────────

    [Fact]
    public void ReadString_UnknownNetworkProfileChild_IsMirroredAndWrittenOnce()
    {
        // Arrange
        var xml = Mutate(FixtureXdd, doc => NetworkBody(doc).Add(new XElement("vendorBlock", new XAttribute("level", "2"))));

        // Act
        var eds = CanOpenFile.Xdd.ReadString(xml);
        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(eds));

        // Assert
        eds.AdditionalSections["vendorBlock"]["level"].Should().Be("2");
        NetworkBody(written).Elements("vendorBlock").Should().ContainSingle();
        NetworkBody(written).Elements().Last().Name.LocalName.Should().Be("vendorBlock");
    }

    // ── edge cases of the reader ─────────────────────────────────────────────

    [Fact]
    public void ReadString_DuplicateModelledElements_KeepsTheSecondOccurrence()
    {
        // Arrange — schema-invalid duplicates are not lost either.
        var xml = Mutate(FixtureXdd, doc =>
        {
            Single(doc, "DeviceIdentity").Add(new XElement(Co + "vendorName", "Second vendor"));
            Single(doc, "ApplicationLayers").Add(new XElement("dummyUsage", new XElement("dummy", new XAttribute("entry", "Dummy0002=1"))));
        });

        // Act
        var eds = CanOpenFile.Xdd.ReadString(xml);
        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(eds));

        // Assert
        eds.DeviceInfo.VendorName.Should().Be("Example Automation Inc.");
        Single(written, "DeviceIdentity").Elements(Co + "vendorName").Select(e => e.Value)
            .Should().Equal("Example Automation Inc.", "Second vendor");
        Single(written, "ApplicationLayers").Elements("dummyUsage").Should().ContainSingle();
    }

    [Fact]
    public void ReadString_NamespacedAndUnknownAttributes_AreKept()
    {
        // Arrange
        var xml = Mutate(FixtureXdd, doc =>
        {
            XNamespace ext = "urn:example:ext";
            DeviceBody(doc).SetAttributeValue(ext + "note", "body");
            var obj = Object(doc, "1000");
            obj.SetAttributeValue(XNamespace.Xmlns + "ext", ext.NamespaceName);
            obj.SetAttributeValue(ext + "note", "object");
        });

        // Act
        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(CanOpenFile.Xdd.ReadString(xml)));

        // Assert
        XNamespace extension = "urn:example:ext";
        ((string?)DeviceBody(written).Attribute(extension + "note")).Should().Be("body");
        ((string?)Object(written, "1000").Attribute(extension + "note")).Should().Be("object");
    }

    [Fact]
    public void WriteToString_FragmentWithQNameValues_KeepsTheBindingsTheyReferTo()
    {
        // Arrange — the prefixes are used only inside attribute values and text.
        XNamespace vendor = "urn:vendor";
        XNamespace other = "urn:other";
        var xml = Mutate(FixtureXdd, doc => Single(doc, "NetworkManagement").Add(
            new XElement("vendorExtension",
                new XAttribute(XNamespace.Xmlns + "vendor", vendor.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "co", other.NamespaceName),
                new XAttribute(Xsi + "type", "vendor:Extension"),
                new XAttribute("ref", "co:Thing"),
                new XElement("inner", new XAttribute("kind", "vendor:Inner"), "note: plain text"))));

        // Act
        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(CanOpenFile.Xdd.ReadString(xml)));

        // Assert
        var extension = Single(written, "vendorExtension");
        var inner = extension.Element("inner")!;
        extension.GetNamespaceOfPrefix("vendor").Should().Be(vendor);
        extension.GetNamespaceOfPrefix("co").Should().Be(other);
        extension.GetNamespaceOfPrefix("xsi").Should().Be(Xsi);
        extension.Attributes().Where(a => a.IsNamespaceDeclaration).Select(a => a.Name.LocalName)
            .Should().BeEquivalentTo("vendor", "co");
        inner.GetNamespaceOfPrefix("vendor").Should().Be(vendor);
        inner.Attributes().Should().NotContain(a => a.IsNamespaceDeclaration);
        written.Root!.GetNamespaceOfPrefix("co").Should().Be(Co);
    }

    [Fact]
    public void WriteToString_PreservedAttributesWithQNameValues_KeepTheBindingsTheyReferTo()
    {
        // Arrange — attributes kept apart from fragments: object, profile body, identity child, ApplicationLayers.
        XNamespace vendor = "urn:vendor";
        var xml = Mutate(FixtureXdd, doc =>
        {
            var obj = Object(doc, "1000");
            obj.SetAttributeValue(XNamespace.Xmlns + "vendor", vendor.NamespaceName);
            obj.SetAttributeValue("custom", "vendor:choice");
            obj.SetAttributeValue("other", "vendor:second");
            obj.SetAttributeValue("plain", "unbound: text");
            DeviceBody(doc).SetAttributeValue(XNamespace.Xmlns + "vendor", vendor.NamespaceName);
            DeviceBody(doc).SetAttributeValue("custom", "vendor:body");
            Single(doc, "DeviceIdentity").Element(Co + "vendorName")!.SetAttributeValue("custom", "vendor:identity");
            Single(doc, "ApplicationLayers").SetAttributeValue("custom", "xsi:string");
        });

        // Act
        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(CanOpenFile.Xdd.ReadString(xml)));

        // Assert
        var obj = Object(written, "1000");
        obj.GetNamespaceOfPrefix("vendor").Should().Be(vendor);
        ((string?)obj.Attribute("custom")).Should().Be("vendor:choice");
        obj.Attributes().Count(a => a.IsNamespaceDeclaration).Should().Be(1);
        DeviceBody(written).GetNamespaceOfPrefix("vendor").Should().Be(vendor);
        var vendorName = Single(written, "DeviceIdentity").Element(Co + "vendorName")!;
        ((string?)vendorName.Attribute(XNamespace.Xmlns + "vendor")).Should().Be(vendor.NamespaceName);
        Single(written, "ApplicationLayers").Attributes().Should().NotContain(a => a.IsNamespaceDeclaration);
    }

    [Fact]
    public void ReadString_MalformedCommentMarker_IsKeptAsToolComment()
    {
        // Arrange
        var xml = Mutate(FixtureXdd, doc => doc.Root!.AddBeforeSelf(new XComment("EdsDcfNet.Comment x: not a line")));

        // Act
        var eds = CanOpenFile.Xdd.ReadString(xml);
        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(eds));

        // Assert
        eds.Comments!.CommentLines.Should().ContainSingle();
        written.Nodes().OfType<XComment>().Select(c => c.Value).Should().Contain("EdsDcfNet.Comment x: not a line");
    }

    [Fact]
    public void WriteToString_SparseProfiles_KeepsWhatIsPresent()
    {
        // Arrange — device profile without DeviceIdentity, network profile with TransportLayers only.
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <ISO15745ProfileContainer xmlns="http://www.canopen.org/xml/1.1" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <ISO15745Profile>
                <ProfileBody xmlns="" xmlns:co="http://www.canopen.org/xml/1.1" xsi:type="co:ProfileBody_Device_CANopen" fileName="s.xdd" fileCreator="c" fileCreationDate="2026-01-01" fileVersion="1">
                  <co:DeviceFunction>
                    <co:capabilities>
                      <co:characteristicsList>
                        <co:characteristic>
                          <co:characteristicName><label lang="en">Kind</label></co:characteristicName>
                          <co:characteristicContent><label lang="en">sparse</label></co:characteristicContent>
                        </co:characteristic>
                      </co:characteristicsList>
                    </co:capabilities>
                  </co:DeviceFunction>
                </ProfileBody>
              </ISO15745Profile>
              <ISO15745Profile>
                <ProfileBody xmlns="" xmlns:co="http://www.canopen.org/xml/1.1" xsi:type="co:ProfileBody_CommunicationNetwork_CANopen" fileName="s.xdd" fileCreator="c" fileCreationDate="2026-01-01" fileVersion="1">
                  <TransportLayers />
                </ProfileBody>
              </ISO15745Profile>
            </ISO15745ProfileContainer>
            """;

        // Act
        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(CanOpenFile.Xdd.ReadString(xml)));

        // Assert
        Single(written, "characteristicContent").Element("label")!.Value.Should().Be("sparse");
    }

    [Fact]
    public void ConvertToDcf_NetworkProfileOnly_KeepsNetworkProfileHeader()
    {
        // Arrange — without a device profile, the network profile supplies FileInfo.
        var xml = Mutate(FixtureXdd, doc => DeviceBody(doc).Parent!.Remove());
        var eds = CanOpenFile.Xdd.ReadString(xml);

        // Act
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 5, timestamp: new DateTime(2026, 6, 1, 8, 0, 0));
        var written = XDocument.Parse(CanOpenFile.Xdc.WriteToString(dcf));

        // Assert
        Header(NetworkBody(written)).Element("ProfileIdentification")!.Value.Should().Be("preserved-content-network");
        ((string?)NetworkBody(written).Attribute("fileVersion")).Should().Be("7");
        Single(written, "characteristicContent").Element("label")!.Value.Should().BeEmpty("no DeviceIdentity was read, and nothing is invented");
    }

    // ── ExternalProfileHandle choice of the network profile ──────────────────

    private const string HandleOnlyXdd = """
        <?xml version="1.0" encoding="utf-8"?>
        <co:ISO15745ProfileContainer xmlns:co="http://www.canopen.org/xml/1.1" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
          <co:ISO15745Profile>
            <ProfileHeader>
              <ProfileIdentification>handle-device</ProfileIdentification>
              <ProfileRevision>1</ProfileRevision>
              <ProfileName />
              <ProfileSource />
              <ProfileClassID>Device</ProfileClassID>
              <ISO15745Reference>
                <ISO15745Part>1</ISO15745Part>
                <ISO15745Edition>1</ISO15745Edition>
                <ProfileTechnology>CANopen</ProfileTechnology>
              </ISO15745Reference>
            </ProfileHeader>
            <ProfileBody xsi:type="co:ProfileBody_Device_CANopen" fileName="handle.xdd" fileCreator="c" fileCreationDate="2026-01-01" fileVersion="1">
              <co:DeviceIdentity>
                <co:vendorName>Vendor</co:vendorName>
                <co:productName>Legacy device</co:productName>
              </co:DeviceIdentity>
              <co:DeviceFunction>
                <co:capabilities>
                  <co:characteristicsList>
                    <co:characteristic>
                      <co:characteristicName><label lang="en">Kind</label></co:characteristicName>
                      <co:characteristicContent><label lang="en">legacy</label></co:characteristicContent>
                    </co:characteristic>
                  </co:characteristicsList>
                </co:capabilities>
              </co:DeviceFunction>
            </ProfileBody>
          </co:ISO15745Profile>
          <co:ISO15745Profile>
            <ProfileHeader>
              <ProfileIdentification>handle-network</ProfileIdentification>
              <ProfileRevision>1</ProfileRevision>
              <ProfileName />
              <ProfileSource />
              <ProfileClassID>CommunicationNetwork</ProfileClassID>
              <ISO15745Reference>
                <ISO15745Part>1</ISO15745Part>
                <ISO15745Edition>1</ISO15745Edition>
                <ProfileTechnology>CANopen</ProfileTechnology>
              </ISO15745Reference>
            </ProfileHeader>
            <ProfileBody xsi:type="co:ProfileBody_CommunicationNetwork_CANopen" fileName="handle.xdd" fileCreator="c" fileCreationDate="2026-01-01" fileVersion="1" formatName="CANopen" formatVersion="1.0">
              <ExternalProfileHandle>
                <ProfileIdentification>legacy</ProfileIdentification>
                <ProfileRevision>1</ProfileRevision>
                <ProfileLocation>legacy/device.eds</ProfileLocation>
              </ExternalProfileHandle>
            </ProfileBody>
          </co:ISO15745Profile>
        </co:ISO15745ProfileContainer>
        """;

    [Fact]
    public void WriteToString_HandleOnlyNetworkProfileXddRoundTrip_WritesTheProfileBodyUnchanged()
    {
        // Arrange
        var source = XDocument.Parse(HandleOnlyXdd);

        // Act
        var xml = CanOpenFile.Xdd.WriteToString(CanOpenFile.Xdd.ReadString(HandleOnlyXdd));

        // Assert
        Cia311Schema.Validate(HandleOnlyXdd).Should().BeEmpty();
        Cia311Schema.Validate(xml).Should().BeEmpty();
        NetworkBody(XDocument.Parse(xml)).ToString(SaveOptions.DisableFormatting)
            .Should().Be(NetworkBody(source).ToString(SaveOptions.DisableFormatting));
    }

    [Fact]
    public void WriteToString_HandleOnlyNetworkProfileXdcRoundTrip_WritesTheProfileBodyUnchanged()
    {
        // Arrange
        var source = XDocument.Parse(HandleOnlyXdd);

        // Act
        var xml = CanOpenFile.Xdc.WriteToString(CanOpenFile.Xdc.ReadString(HandleOnlyXdd));

        // Assert
        Cia311Schema.Validate(xml).Should().BeEmpty();
        NetworkBody(XDocument.Parse(xml)).ToString(SaveOptions.DisableFormatting)
            .Should().Be(NetworkBody(source).ToString(SaveOptions.DisableFormatting));
    }

    [Fact]
    public void ConvertToDcf_HandleOnlyNetworkProfile_WritesCommissioningInGeneratedLayersWithoutHandle()
    {
        // Arrange — deviceCommissioning can only be written inside NetworkManagement.
        var dcf = CanOpenFile.Eds.ConvertToDcf(
            CanOpenFile.Xdd.ReadString(HandleOnlyXdd), nodeId: 5, timestamp: new DateTime(2026, 6, 1, 8, 0, 0));

        // Act
        var xml = CanOpenFile.Xdc.WriteToString(dcf);
        var body = NetworkBody(XDocument.Parse(xml));

        // Assert — the only schema problem is the empty CANopenObjectList: the model has no
        // object, and an object is not invented (plan decision E10).
        Cia311Schema.Validate(xml).Should().ContainSingle().Which.Should().Contain("CANopenObjectList");
        body.Element("ExternalProfileHandle").Should().BeNull();
        ((string?)body.Element("NetworkManagement")!.Element("deviceCommissioning")!.Attribute("nodeID")).Should().Be("5");
    }

    [Fact]
    public void WriteToString_HandleOnlyNetworkProfileXdcWithOmittedCommissioning_WritesHandleOnly()
    {
        // Arrange
        var dcf = CanOpenFile.Xdc.ReadString(HandleOnlyXdd);

        // Act
        var body = NetworkBody(XDocument.Parse(CanOpenFile.Xdc.WriteToString(dcf)));

        // Assert
        DeviceCommissioningSemantics.IsOmitted(dcf.DeviceCommissioning).Should().BeTrue();
        body.Elements().Select(e => e.Name.LocalName).Should().Equal("ExternalProfileHandle");
    }

    [Fact]
    public void WriteToString_HandleOnlyNetworkProfileWithObjectAddedInCode_WritesGeneratedLayersWithoutHandle()
    {
        // Arrange — the handle cannot express the new object, so the model wins.
        var eds = CanOpenFile.Xdd.ReadString(HandleOnlyXdd);
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000, ParameterName = "Device type", ObjectType = 7, DataType = 7, DefaultValue = "0",
        };

        // Act
        var xml = CanOpenFile.Xdd.WriteToString(eds);
        var body = NetworkBody(XDocument.Parse(xml));

        // Assert
        Cia311Schema.Validate(xml).Should().BeEmpty();
        body.Elements().Select(e => e.Name.LocalName).Should().Equal("ApplicationLayers", "TransportLayers", "NetworkManagement");
        body.Descendants("CANopenObject").Should().ContainSingle();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static XDocument RoundTrip(string path)
        => XDocument.Parse(CanOpenFile.Xdd.WriteToString(CanOpenFile.Xdd.ReadFile(path)));

    private static string Mutate(string path, Action<XDocument> edit)
    {
        var doc = XDocument.Load(path);
        edit(doc);
        return doc.ToString();
    }

    private static XElement Single(XDocument doc, string localName)
        => doc.Descendants().Single(e => e.Name.LocalName == localName);

    private static XElement DeviceBody(XDocument doc) => Body(doc, "ProfileBody_Device_CANopen");

    private static XElement NetworkBody(XDocument doc) => Body(doc, "ProfileBody_CommunicationNetwork_CANopen");

    private static XElement Body(XDocument doc, string type)
        => doc.Descendants().Single(e => e.Name.LocalName == "ProfileBody"
            && (e.Attribute(Xsi + "type")?.Value ?? string.Empty).EndsWith(type, StringComparison.Ordinal));

    private static XElement Header(XElement body) => body.Parent!.Element("ProfileHeader")!;

    private static XElement Object(XDocument doc, string index)
        => doc.Descendants().Single(e => e.Name.LocalName == "CANopenObject" && e.Attribute("index")?.Value == index);

    private static (string?, string?, string?)[] Attributes(XElement body) => new[]
    {
        (body.Attribute("fileVersion")?.Value, body.Attribute("fileModificationDate")?.Value, body.Attribute("fileModifiedBy")?.Value),
    };

    /// <summary>Copy without namespace declarations, so fragments compare by name and content only.</summary>
    private static XElement Comparable(XElement element)
    {
        var copy = new XElement(element);
        foreach (var e in copy.DescendantsAndSelf())
            e.Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
        return copy;
    }

    private static string WithoutNamespaces(XDocument doc)
    {
        foreach (var element in doc.Root!.DescendantsAndSelf().ToList())
        {
            element.Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
            element.Name = element.Name.LocalName;
            var type = element.Attribute(Xsi + "type");
            if (type != null)
                type.Value = type.Value.Substring(type.Value.IndexOf(':') + 1);
        }

        doc.Root.SetAttributeValue(XNamespace.Xmlns + "xsi", Xsi.NamespaceName);
        return doc.ToString();
    }
}
