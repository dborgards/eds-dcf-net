namespace EdsDcfNet.Tests.Integration;

using System.Xml.Linq;
using EdsDcfNet;
using EdsDcfNet.Models;
using EdsDcfNet.Writers;

/// <summary>Regression tests for the remaining WP-43 fragment-preservation boundaries.</summary>
public class XddPreservedContentEdgeTests
{
    private const string FixtureXdd = "Fixtures/preserved_content.xdd";

    private static readonly XNamespace Co = "http://www.canopen.org/xml/1.1";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    [Fact]
    public void WriteToString_NamespacelessRootWithVendorNamespace_QualifiesOnlyLegacyElements()
    {
        XNamespace vendor = "urn:vendor";
        var source = XDocument.Parse(WithoutNamespaces(XDocument.Load(FixtureXdd)));
        Single(source, "NetworkManagement").Add(new XElement(vendor + "vendorExtension",
            new XElement(vendor + "note", "kept")));

        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(CanOpenFile.Xdd.ReadString(source.ToString())));

        Single(written, "DeviceManager").Element(Co + "moduleManagement").Should().NotBeNull();
        var extension = Single(written, "NetworkManagement").Element(vendor + "vendorExtension");
        extension.Should().NotBeNull();
        extension!.Element(vendor + "note")!.Value.Should().Be("kept");
    }

    [Fact]
    public void WriteToString_TwoDerivedDeviceFunctions_KeepsBothOccurrences()
    {
        var source = XDocument.Load(FixtureXdd);
        var function = Single(source, "DeviceFunction");
        function.AddAfterSelf(new XElement(function));

        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(CanOpenFile.Xdd.ReadString(source.ToString())));

        var functions = written.Descendants(Co + "DeviceFunction").ToList();
        functions.Should().HaveCount(2);
        XNode.DeepEquals(Comparable(functions[0]), Comparable(functions[1])).Should().BeTrue();
        written.Descendants(Co + "characteristicContent").Select(element => element.Element("label")!.Value)
            .Should().Equal("8 digital", "8 digital");
    }

    [Fact]
    public void WriteToString_UnprefixedQNameWithDefaultNamespace_KeepsBindingAndNamespaceReset()
    {
        XNamespace vendor = "urn:vendor";
        var source = XDocument.Load(FixtureXdd);
        Single(source, "NetworkManagement").Add(new XElement(vendor + "vendorExtension",
            new XAttribute("xmlns", vendor.NamespaceName),
            new XAttribute(Xsi + "type", "Extension"),
            new XElement(vendor + "typedChild", "kept"),
            new XElement("plainChild", new XAttribute("xmlns", string.Empty), "unqualified")));

        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(CanOpenFile.Xdd.ReadString(source.ToString())));

        var extension = Single(written, "vendorExtension");
        extension.GetDefaultNamespace().Should().Be(vendor);
        ((string?)extension.Attribute(Xsi + "type")).Should().Be("Extension");
        extension.Element(vendor + "typedChild")!.Value.Should().Be("kept");
        var plain = extension.Element("plainChild");
        plain.Should().NotBeNull();
        plain!.GetDefaultNamespace().Should().Be(XNamespace.None);
        plain.Value.Should().Be("unqualified");
    }

    [Fact]
    public void WriteToString_AdditionalIso15745Profile_IsPreservedWithItsContents()
    {
        var source = XDocument.Load(FixtureXdd);
        var extra = new XElement(source.Root!.Elements(Co + "ISO15745Profile").First());
        extra.Descendants("ProfileIdentification").First().Value = "additional-profile";
        var body = extra.Descendants("ProfileBody").Single();
        body.SetAttributeValue(XNamespace.Xmlns + "vendor", "urn:vendor");
        body.SetAttributeValue(Xsi + "type", "vendor:AdditionalProfile");
        source.Root.Add(extra);

        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(CanOpenFile.Xdd.ReadString(source.ToString())));

        var profiles = written.Root!.Elements(Co + "ISO15745Profile").ToList();
        profiles.Should().HaveCount(3);
        var preserved = profiles.Single(p => p.Descendants("ProfileIdentification").First().Value == "additional-profile");
        XNode.DeepEquals(Comparable(preserved), Comparable(extra)).Should().BeTrue();
    }

    [Fact]
    public void WriteToString_DuplicateModelledProfiles_SecondReadKeepsSelectedModel()
    {
        var source = XDocument.Load(FixtureXdd);
        var profiles = source.Root!.Elements(Co + "ISO15745Profile").ToList();
        var staleDevice = new XElement(profiles[0]);
        staleDevice.Descendants().Single(e => e.Name.LocalName == "productName").Value = "stale-device";
        var staleNetwork = new XElement(profiles[1]);
        staleNetwork.Descendants().Single(e => e.Name.LocalName == "CANopenObject"
                && (string?)e.Attribute("index") == "1000")
            .SetAttributeValue("defaultValue", "stale-network");
        profiles[0].AddBeforeSelf(staleDevice);
        profiles[1].AddBeforeSelf(staleNetwork);

        var firstRead = CanOpenFile.Xdd.ReadString(source.ToString());
        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(firstRead));
        var secondRead = CanOpenFile.Xdd.ReadString(written.ToString());

        written.Root!.Elements(Co + "ISO15745Profile")
            .Select(p => p.Descendants("ProfileClassID").Single().Value)
            .Should().Equal("Device", "CommunicationNetwork", "Device", "CommunicationNetwork");
        secondRead.DeviceInfo.ProductName.Should().Be("IO-Module 8x8");
        secondRead.ObjectDictionary.Objects[0x1000].DefaultValue.Should().Be("0x000F0191");
    }

    [Fact]
    public void WriteToString_DuplicateGeneratedDeviceFunctions_RegeneratesFromChangedModel()
    {
        var initial = CanOpenFile.Xdd.ReadFile(FixtureXdd);
        var generated = XDocument.Parse(CanOpenFile.Xdd.WriteToString(initial));
        var function = Single(generated, "DeviceFunction");
        function.ReplaceWith(XddProfileBuilder.BuildDefaultDeviceFunction(initial.DeviceInfo));
        function = Single(generated, "DeviceFunction");
        function.AddAfterSelf(new XElement(function));
        var model = CanOpenFile.Xdd.ReadString(generated.ToString());
        model.DeviceInfo.ProductName.Should().Be("IO-Module 8x8");
        model.DeviceInfo.ProductName = "Changed product";

        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(model));

        written.Descendants(Co + "DeviceFunction").Should().ContainSingle();
        Single(written, "characteristicContent").Element("label")!.Value.Should().Be("Changed product");
    }

    [Fact]
    public void WriteToString_NamespacelessAdditionalProfile_IsPreserved()
    {
        var source = XDocument.Parse(WithoutNamespaces(XDocument.Load(FixtureXdd)));
        var extra = new XElement(source.Root!.Elements().First());
        extra.Descendants("ProfileIdentification").First().Value = "namespaceless-additional";
        extra.Descendants("ProfileBody").Single().SetAttributeValue(Xsi + "type", "AdditionalProfile");
        source.Root.Add(extra);

        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(CanOpenFile.Xdd.ReadString(source.ToString())));

        written.Root!.Elements(Co + "ISO15745Profile").Should().HaveCount(3);
        written.Descendants("ProfileIdentification").Select(element => element.Value)
            .Should().Contain("namespaceless-additional");
    }

    private static XElement Single(XDocument doc, string localName)
        => doc.Descendants().Single(e => e.Name.LocalName == localName);

    private static XElement Comparable(XElement element)
    {
        var copy = new XElement(element);
        foreach (var descendant in copy.DescendantsAndSelf())
            descendant.Attributes().Where(attribute => attribute.IsNamespaceDeclaration).Remove();
        return copy;
    }

    private static string WithoutNamespaces(XDocument doc)
    {
        foreach (var element in doc.Root!.DescendantsAndSelf().ToList())
        {
            element.Attributes().Where(attribute => attribute.IsNamespaceDeclaration).Remove();
            element.Name = element.Name.LocalName;
            var type = element.Attribute(Xsi + "type");
            if (type != null)
                type.Value = type.Value.Substring(type.Value.IndexOf(':') + 1);
        }

        doc.Root.SetAttributeValue(XNamespace.Xmlns + "xsi", Xsi.NamespaceName);
        return doc.ToString();
    }
}
