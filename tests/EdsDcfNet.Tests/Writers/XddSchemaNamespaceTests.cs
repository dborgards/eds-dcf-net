namespace EdsDcfNet.Tests.Writers;

using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Tests.Infrastructure;
using EdsDcfNet.Writers;

/// <summary>
/// CiA 311 element namespaces (WP-17). Globally declared elements belong to
/// <c>http://www.canopen.org/xml/1.1</c>; locally declared elements belong to
/// no namespace. The choice depends on the parent, not on the local name alone.
/// </summary>
public class XddSchemaNamespaceTests
{
    private const string CorpusXdd = "Fixtures/Corpus/canopen-node/basicDevice.xdd";

    private static readonly XNamespace Cia = XddNames.Namespace;

    [Fact]
    public void GenerateString_UsesMixedElementNamespacesRequiredByCia311()
    {
        // Arrange
        var writer = new XddWriter();

        // Act
        var document = XDocument.Parse(writer.GenerateString(CreateSampleEds()));
        var root = document.Root!;

        // Assert — global elements are qualified, local structure elements are not.
        root.Name.Should().Be(XddNames.ProfileContainer);
        var deviceIdentity = root.Descendants().Single(element => element.Name.LocalName == "DeviceIdentity");
        deviceIdentity.Name.Should().Be(Cia + "DeviceIdentity");
        deviceIdentity.Elements().Single(element => element.Name.LocalName == "vendorName")
            .Name.Should().Be(Cia + "vendorName");

        root.Descendants().Where(element => element.Name.LocalName == "ProfileHeader")
            .Should().OnlyContain(element => element.Name.Namespace == XNamespace.None)
            .And.HaveCount(2);
        root.Descendants().Where(element => element.Name.LocalName == "ProfileBody")
            .Should().OnlyContain(element => element.Name.Namespace == XNamespace.None);
        root.Descendants().Single(element => element.Name.LocalName == "CANopenObject")
            .Name.Namespace.Should().Be(XNamespace.None);
        root.Descendants().Single(element => element.Name.LocalName == "CANopenObjectList")
            .Name.Should().Be(Cia + "CANopenObjectList");

        var typeValues = root.Descendants()
            .Where(element => element.Name.LocalName == "ProfileBody")
            .Select(element => (string?)element.Attribute(XddNames.Xsi + "type"))
            .ToList();
        typeValues.Should().Equal(
            XddNames.Prefix + ":ProfileBody_Device_CANopen",
            XddNames.Prefix + ":ProfileBody_CommunicationNetwork_CANopen");

        root.Attribute(XNamespace.Xmlns + XddNames.Prefix)!.Value.Should().Be(XddNames.NamespaceUri);
        root.Descendants().Should().Contain(element => element.Name.Namespace == XNamespace.None);
        root.Descendants().Should().Contain(element => element.Name.Namespace == Cia);
    }

    [Fact]
    public void XddNames_FourCollisions_DependOnParentContext()
    {
        // Device profile: global element references. Network profile: local declarations.
        XddNames.Child(Cia + "DeviceManager", "moduleManagement")
            .Should().Be(Cia + "moduleManagement");
        XddNames.Child("ApplicationLayers", "moduleManagement")
            .Namespace.Should().Be(XNamespace.None);

        XddNames.Child(Cia + "functionType", "interfaceList")
            .Should().Be(Cia + "interfaceList");
        XddNames.Child("moduleManagement", "interfaceList")
            .Namespace.Should().Be(XNamespace.None);

        XddNames.Child(Cia + "moduleInterfaceList", "interface")
            .Should().Be(Cia + "interface");
        XddNames.Child("interfaceList", "interface")
            .Namespace.Should().Be(XNamespace.None);

        XddNames.Child(Cia + "allowedValues", "range").Should().Be(Cia + "range");
        XddNames.Child(Cia + "allowedValuesTemplate", "range").Should().Be(Cia + "range");
        XddNames.Child("rangeList", "range").Namespace.Should().Be(XNamespace.None);
    }

    [Fact]
    public void XddNames_MatchesEveryCompiledSchemaParticle()
    {
        // Arrange
        var particles = SchemaParticles();

        // Act / Assert — the only names that are qualified in one parent and not in another.
        particles
            .GroupBy(particle => particle.Child)
            .Where(group => group.Select(particle => particle.Qualified).Distinct().Count() > 1)
            .Select(group => group.Key)
            .Should().BeEquivalentTo("moduleManagement", "interfaceList", "interface", "range");

        foreach (var particle in particles)
        {
            var actual = particle.ParentType == null
                ? XddNames.Child(particle.ParentElement!, particle.Child)
                : XddNames.ChildOfType(particle.ParentType, particle.Child);
            var expected = particle.Qualified ? Cia + particle.Child : (XName)particle.Child;
            actual.Should().Be(expected, "parent {0} declares {1}", particle.ParentDisplay, particle.Child);
        }

        particles.Should().HaveCount(XddNames.DeclarationCount);
        particles.Should().OnlyHaveUniqueItems(particle => particle.ParentDisplay + "\n" + particle.Child);
    }

    [Fact]
    public void XddNames_SimpleTypeOutsideTheSchemaList_StaysUnqualified()
    {
        XddNames.SimpleType(Cia + "enum", "UDINT").Namespace.Should().Be(XNamespace.None);
        XddNames.SimpleType(Cia + "enum", "DATE").Namespace.Should().Be(XNamespace.None);

        var act = () => XddNames.SimpleType(XddNames.ProfileContainer, "DATE");
        act.Should().Throw<InvalidOperationException>().WithMessage("*DATE*");
    }

    [Fact]
    public void XddNames_UnknownChild_ThrowsInsteadOfGuessingANamespace()
    {
        var act = () => XddNames.Child(XddNames.ProfileContainer, "notAnElement");

        act.Should().Throw<InvalidOperationException>().WithMessage("*notAnElement*");
    }

    [Theory]
    [MemberData(nameof(CollisionCases))]
    public void CollisionElement_SchemaAcceptsOnlyTheDeclarationForThatParent(
        string anchor,
        string insertion,
        bool valid,
        string token)
    {
        // Arrange
        var xml = InsertBefore(File.ReadAllText(CorpusXdd), anchor, insertion);

        // Act
        var problems = Cia311Schema.Validate(xml);

        // Assert
        if (valid)
        {
            problems.Should().BeEmpty();
        }
        else
        {
            problems.Should().Contain(problem => problem.Contains(token));
        }
    }

    [Fact]
    public void Corpus_WithLocalElementsQualifiedByDroppingNamespaceResets_IsRejected()
    {
        // The rejected "put xmlns on the root and let every element inherit it" rule.
        var xml = File.ReadAllText(CorpusXdd).Replace(" xmlns=\"\"", "");

        var problems = Cia311Schema.Validate(xml);

        problems.Should().Contain(problem => problem.Contains("ProfileHeader"));
    }

    [Theory]
    [InlineData("Fixtures/sample_device.xdd", "xdd")]
    [InlineData("Fixtures/sample_device.eds", "eds")]
    [InlineData(CorpusXdd, "xdd")]
    [InlineData("Fixtures/minimal.xdc", "xdc")]
    public void WriterOutput_ElementNamesMatchTheSchemaTable(string path, string kind)
    {
        var xml = Write(path, kind);
        var document = XDocument.Parse(xml);

        AssertTreeNamespaces(document.Root!);
    }

    [Theory]
    [InlineData("Fixtures/sample_device.xdd", "xdd", 3)]
    [InlineData("Fixtures/sample_device.eds", "eds", 3)]
    [InlineData(CorpusXdd, "xdd", 1)]
    [InlineData("Fixtures/minimal.xdc", "xdc", 1)]
    public void WriterOutput_SchemaProblems_AreTheKnownNonNamespaceRemainder(string path, string kind, int count)
    {
        // Arrange — content gaps that later work packages shrink. None of them are
        // "this element is in the wrong namespace".
        var expected = ExpectedRemainder(path);

        // Act
        var problems = Cia311Schema.Validate(Write(path, kind));
        var messages = problems.Select(Message).ToList();

        // Assert
        problems.Should().HaveCount(count);
        messages.Distinct().Should().BeEquivalentTo(expected);
        messages.Should().NotContain(message => IsQualificationError(message));
    }

    [Fact]
    public void Reader_ReadsNamespaceLessMixedAndFullyQualifiedDocuments()
    {
        var reader = new XddReader();

        var plain = reader.ReadString(NamespaceLessDocument);
        var mixed = reader.ReadString(MixedDocument);
        var qualified = reader.ReadString(FullyQualifiedDocument);
        var fixture = reader.ReadFile("Fixtures/sample_device.xdd");

        foreach (var eds in new[] { plain, mixed, qualified })
        {
            eds.DeviceInfo.VendorName.Should().Be("Test Vendor");
            eds.ObjectDictionary.Objects.Should().ContainKey((ushort)0x1000);
            eds.ObjectDictionary.Objects[0x1000].ParameterName.Should().Be("Device Type");
        }

        fixture.DeviceInfo.VendorName.Should().NotBeNull();
        fixture.ObjectDictionary.Objects.Should().NotBeEmpty();
    }

    [Fact]
    public void XddAndXdcRoundTrips_AreStable()
    {
        var xddReader = new XddReader();
        var xddWriter = new XddWriter();
        var fromFixture = xddReader.ReadFile("Fixtures/sample_device.xdd");
        var first = xddWriter.GenerateString(fromFixture);
        var second = xddWriter.GenerateString(xddReader.ReadString(first));
        second.Should().Be(first);
        xddReader.ReadString(first).DeviceInfo.VendorName.Should().Be(fromFixture.DeviceInfo.VendorName);

        var xdcReader = new XdcReader();
        var xdcWriter = new XdcWriter();
        var fromXdc = xdcReader.ReadFile("Fixtures/minimal.xdc");
        var xdcFirst = xdcWriter.GenerateString(fromXdc);
        var xdcSecond = xdcWriter.GenerateString(xdcReader.ReadString(xdcFirst));
        xdcSecond.Should().Be(xdcFirst);
        xdcReader.ReadString(xdcFirst).DeviceInfo.VendorName.Should().Be(fromXdc.DeviceInfo.VendorName);
    }

    public static IEnumerable<object[]> CollisionCases()
    {
        yield return new object[] { "<q1:DeviceFunction>", DeviceModuleManagementQualified, true, "" };
        yield return new object[] { "<q1:DeviceFunction>", DeviceModuleManagementUnqualified, false, "moduleManagement" };
        yield return new object[] { "</ApplicationLayers>", NetworkModuleFragment, true, "" };
        yield return new object[] { "</ApplicationLayers>", NetworkModuleFragment.Replace("<moduleManagement>", "<q2:moduleManagement>").Replace("</moduleManagement>", "</q2:moduleManagement>"), false, "moduleManagement" };
        yield return new object[] { "<q1:parameterList>", FunctionTypeWithInterfaceList, true, "" };
        yield return new object[] { "<q1:parameterList>", FunctionTypeWithInterfaceList.Replace("<q1:interfaceList/>", "<interfaceList/>"), false, "interfaceList" };
        yield return new object[] { "</ApplicationLayers>", NetworkModuleFragment.Replace("<interfaceList>", "<q2:interfaceList>").Replace("</interfaceList>", "</q2:interfaceList>"), false, "interfaceList" };
        yield return new object[] { "<q1:DeviceFunction>", DeviceInterfaceQualified, true, "" };
        yield return new object[] { "<q1:DeviceFunction>", DeviceInterfaceQualified.Replace("<q1:interface ", "<interface ").Replace("</q1:interface>", "</interface>"), false, "interface" };
        yield return new object[] { "</ApplicationLayers>", NetworkModuleFragment.Replace("<interface ", "<q2:interface ").Replace("</interface>", "</q2:interface>"), false, "interface" };
        yield return new object[] { "<q1:property name=\"CO_countLabel\" value=\"NMT\" />", AllowedValuesWithRange, true, "" };
        yield return new object[] { "<q1:property name=\"CO_countLabel\" value=\"NMT\" />", AllowedValuesWithRange.Replace("<q1:range>", "<range>").Replace("</q1:range>", "</range>"), false, "range" };
        yield return new object[] { "</ApplicationLayers>", NetworkModuleFragment.Replace("<range ", "<q2:range "), false, "range" };
    }

    private static string Write(string path, string kind) => kind switch
    {
        "xdd" => new XddWriter().GenerateString(new XddReader().ReadFile(path)),
        "eds" => new XddWriter().GenerateString(new EdsReader().ReadFile(path)),
        "xdc" => new XdcWriter().GenerateString(new XdcReader().ReadFile(path)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown writer kind."),
    };

    private static string[] ExpectedRemainder(string path) => path switch
    {
        "Fixtures/sample_device.xdd" => new[]
        {
            "The element 'DeviceFunction' in namespace 'http://www.canopen.org/xml/1.1' has incomplete content. List of possible elements expected: 'capabilities' in namespace 'http://www.canopen.org/xml/1.1'.",
            "The element 'enum' in namespace 'http://www.canopen.org/xml/1.1' has invalid child element 'USINT'. List of possible elements expected: 'label, description, labelRef, descriptionRef' as well as 'enumValue' in namespace 'http://www.canopen.org/xml/1.1'.",
            "The element 'parameterGroup' in namespace 'http://www.canopen.org/xml/1.1' has invalid child element 'parameterGroup' in namespace 'http://www.canopen.org/xml/1.1'. List of possible elements expected: 'parameterRef' in namespace 'http://www.canopen.org/xml/1.1'.",
        },
        "Fixtures/sample_device.eds" => new[]
        {
            "The 'fileCreationTime' attribute is invalid - The value '10:00AM' is invalid according to its datatype 'http://www.w3.org/2001/XMLSchema:time' - The string '10:00AM' is not a valid Time value.",
            "The element 'DeviceFunction' in namespace 'http://www.canopen.org/xml/1.1' has incomplete content. List of possible elements expected: 'capabilities' in namespace 'http://www.canopen.org/xml/1.1'.",
        },
        CorpusXdd => new[]
        {
            "The element 'DeviceFunction' in namespace 'http://www.canopen.org/xml/1.1' has incomplete content. List of possible elements expected: 'capabilities' in namespace 'http://www.canopen.org/xml/1.1'.",
        },
        "Fixtures/minimal.xdc" => new[]
        {
            "The element 'DeviceFunction' in namespace 'http://www.canopen.org/xml/1.1' has incomplete content. List of possible elements expected: 'capabilities' in namespace 'http://www.canopen.org/xml/1.1'.",
        },
        _ => throw new ArgumentOutOfRangeException(nameof(path), path, "No expected schema remainder."),
    };

    private static void AssertTreeNamespaces(XElement element)
    {
        foreach (var attribute in element.Attributes())
            attribute.Name.Namespace.Should().NotBe(Cia);

        var typeValue = (string?)element.Attribute(XddNames.Xsi + "type");
        string? childType = null;
        if (element.Name.LocalName == "ProfileBody")
        {
            typeValue.Should().StartWith(XddNames.Prefix + ":");
            childType = typeValue!.Substring(XddNames.Prefix.Length + 1);
        }

        foreach (var child in element.Elements())
        {
            var actual = childType == null
                ? XddNames.Child(element.Name, child.Name.LocalName)
                : XddNames.ChildOfType(childType, child.Name.LocalName);
            child.Name.Should().Be(actual);
            AssertTreeNamespaces(child);
        }
    }

    private static bool IsQualificationError(string message)
    {
        if (message.Contains("is not declared", StringComparison.Ordinal)
            || message.Contains("Could not find schema information", StringComparison.Ordinal))
        {
            return true;
        }

        const string childMarker = "invalid child element '";
        var childAt = message.IndexOf(childMarker, StringComparison.Ordinal);
        if (childAt < 0)
            return false;

        var nameStart = childAt + childMarker.Length;
        var nameEnd = message.IndexOf('\'', nameStart);
        if (nameEnd < 0)
            return false;

        var childName = message.Substring(nameStart, nameEnd - nameStart);
        var childNamespace = NamespaceAfter(message, nameEnd);
        var expectedAt = message.IndexOf("expected:", nameEnd, StringComparison.Ordinal);
        if (expectedAt < 0 || childNamespace == null)
            return false;

        var expectedNameAt = message.IndexOf('\'', expectedAt);
        if (expectedNameAt < 0)
            return false;

        var expectedNameEnd = message.IndexOf('\'', expectedNameAt + 1);
        if (expectedNameEnd < 0)
            return false;

        var expectedName = message.Substring(expectedNameAt + 1, expectedNameEnd - expectedNameAt - 1);
        var expectedNamespace = NamespaceAfter(message, expectedNameEnd);
        return expectedName == childName
            && expectedNamespace != null
            && !string.Equals(childNamespace, expectedNamespace, StringComparison.Ordinal);
    }

    private static string? NamespaceAfter(string message, int from)
    {
        const string marker = "in namespace '";
        var at = message.IndexOf(marker, from, StringComparison.Ordinal);
        if (at < 0 || at > from + 40)
            return null;

        var start = at + marker.Length;
        var end = message.IndexOf('\'', start);
        return end < 0 ? null : message.Substring(start, end - start);
    }

    private static string Message(string problem)
    {
        const string marker = "): ";
        var index = problem.IndexOf(marker, StringComparison.Ordinal);
        return index < 0 ? problem : problem.Substring(index + marker.Length);
    }

    private static string InsertBefore(string text, string anchor, string insertion)
    {
        var position = text.IndexOf(anchor, StringComparison.Ordinal);
        position.Should().BeGreaterThanOrEqualTo(0, "the corpus file must contain '{0}'", anchor);
        text.IndexOf(anchor, position + anchor.Length, StringComparison.Ordinal)
            .Should().Be(-1, "anchor '{0}' must be unique", anchor);
        return text.Substring(0, position) + insertion + text.Substring(position);
    }

    private static List<SchemaParticle> SchemaParticles()
    {
        var set = Cia311Schema.SchemaSet;
        var elements = new List<XmlSchemaElement>();
        var visitedTypes = new HashSet<XmlSchemaType>();
        var xsiTypes = new List<XmlSchemaComplexType>();

        void Collect(XmlSchemaElement element)
        {
            elements.Add(element);
            if (element.ElementSchemaType is not XmlSchemaComplexType complexType)
                return;
            if (!visitedTypes.Add(complexType))
                return;
            foreach (var child in ChildElements(complexType))
                Collect(child);
        }

        foreach (XmlSchemaElement global in set.GlobalElements.Values)
            Collect(global);

        var candidates = new List<XmlSchemaComplexType>();
        foreach (XmlSchemaType type in set.GlobalTypes.Values)
        {
            if (type is not XmlSchemaComplexType complexType)
                continue;
            if (visitedTypes.Contains(complexType))
                continue;
            if (!ChildElements(complexType).Any())
                continue;
            candidates.Add(complexType);
        }

        foreach (var complexType in candidates)
        {
            foreach (var child in ChildElements(complexType))
                Collect(child);
        }

        var declaredByElement = new HashSet<XmlSchemaType>(
            elements.Select(element => element.ElementSchemaType).OfType<XmlSchemaType>());
        foreach (var complexType in candidates)
        {
            if (!declaredByElement.Contains(complexType))
                xsiTypes.Add(complexType);
        }

        var particles = new Dictionary<string, SchemaParticle>();
        void Add(string key, SchemaParticle particle)
        {
            if (particles.TryGetValue(key, out var existing))
            {
                existing.Qualified.Should().Be(particle.Qualified, "particle {0} has one form", key);
                return;
            }

            particles.Add(key, particle);
        }

        foreach (var element in elements)
        {
            if (element.ElementSchemaType is not XmlSchemaComplexType complexType)
                continue;
            foreach (var child in ChildElements(complexType))
            {
                var qualified = child.QualifiedName.Namespace == Cia.NamespaceName;
                Add(
                    "e:" + element.QualifiedName + "\n" + child.QualifiedName.Name,
                    new SchemaParticle(ElementName(element.QualifiedName), null, child.QualifiedName.Name, qualified, element.QualifiedName.ToString()));
            }
        }

        foreach (var complexType in xsiTypes)
        {
            foreach (var child in ChildElements(complexType))
            {
                var qualified = child.QualifiedName.Namespace == Cia.NamespaceName;
                Add(
                    "t:" + complexType.QualifiedName.Name + "\n" + child.QualifiedName.Name,
                    new SchemaParticle(null, complexType.QualifiedName.Name, child.QualifiedName.Name, qualified, complexType.QualifiedName.Name));
            }
        }

        return particles.Values.ToList();
    }

    private static XName ElementName(XmlQualifiedName name)
        => string.IsNullOrEmpty(name.Namespace) ? name.Name : XName.Get(name.Name, name.Namespace);

    private static IEnumerable<XmlSchemaElement> ChildElements(XmlSchemaComplexType complexType)
    {
        foreach (var particle in Flatten(complexType.ContentTypeParticle))
        {
            if (particle is XmlSchemaElement element)
                yield return element;
            else
                throw new InvalidOperationException("Unexpected schema particle " + particle.GetType().Name);
        }
    }

    private static IEnumerable<XmlSchemaParticle> Flatten(XmlSchemaParticle? particle)
    {
        if (particle == null || particle is XmlSchemaAny)
            yield break;

        if (particle is XmlSchemaElement element)
        {
            yield return element;
            yield break;
        }

        if (particle is XmlSchemaGroupBase group)
        {
            foreach (XmlSchemaParticle item in group.Items)
            {
                foreach (var child in Flatten(item))
                    yield return child;
            }
        }
    }

    private static ElectronicDataSheet CreateSampleEds()
    {
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo
            {
                FileName = "test.xdd",
                FileVersion = 1,
                CreatedBy = "TestCreator",
                CreationDate = "01-15-2025",
            },
            DeviceInfo = new DeviceInfo
            {
                VendorName = "Test Vendor",
                VendorNumber = 0x00000100,
                ProductName = "Test Product",
                ProductNumber = 0x00001001,
                Granularity = 8,
                NrOfRxPdo = 2,
                NrOfTxPdo = 2,
                SupportedBaudRates = new BaudRates { BaudRate250 = true },
            },
        };

        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "0",
            PdoMapping = false,
        };
        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);
        return eds;
    }

    private const string DeviceModuleManagementQualified =
        "<q1:DeviceManager><q1:moduleManagement><moduleInterface childID=\"Mod00000001\" type=\"ModA\"/></q1:moduleManagement></q1:DeviceManager>";

    private const string DeviceModuleManagementUnqualified =
        "<q1:DeviceManager><moduleManagement><moduleInterface childID=\"Mod00000001\" type=\"ModA\"/></moduleManagement></q1:DeviceManager>";

    private const string DeviceInterfaceQualified =
        "<q1:DeviceManager><q1:moduleManagement><q1:moduleInterfaceList><q1:interface uniqueID=\"UID_IF_NS17\" maxChilds=\"1\" unusedSlots=\"false\" multipleChilds=\"false\"><label lang=\"en\">Slot</label><q1:fileList><q1:file URI=\"mod.xdd\"/></q1:fileList><q1:moduleTypeList><q1:moduleType type=\"ModA\"/></q1:moduleTypeList></q1:interface></q1:moduleInterfaceList></q1:moduleManagement></q1:DeviceManager>";

    private const string FunctionTypeWithInterfaceList =
        "<q1:functionTypeList><q1:functionType name=\"NsCollision\" uniqueID=\"UID_FN_NS17\"><q1:versionInfo organization=\"CiA\" version=\"1.0\" author=\"t\" date=\"2020-01-01\"/><q1:interfaceList/></q1:functionType></q1:functionTypeList>";

    private const string AllowedValuesWithRange =
        "<q1:allowedValues><q1:range><minValue value=\"0\"/><maxValue value=\"10\"/></q1:range></q1:allowedValues>";

    private const string NetworkModuleFragment =
        "<moduleManagement><interfaceList><interface uniqueIDRef=\"UID_OBJ_1000\" addressing=\"auto\"><rangeList><range name=\"NsRange\" moduleType=\"ModTypeNs17\" baseIndex=\"2000\" maxIndex=\"20FF\" sortMode=\"index\" sortNumber=\"continuous\"/></rangeList></interface></interfaceList></moduleManagement>";

    private const string NamespaceLessDocument = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>Device</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <DeviceIdentity><vendorName>Test Vendor</vendorName><vendorID>0x1</vendorID><productName>P</productName><productID>0x1</productID></DeviceIdentity>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>CommunicationNetwork</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007"" accessType=""ro"" PDOmapping=""no""/>
        </CANopenObjectList>
      </ApplicationLayers>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

    private const string MixedDocument = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns=""http://www.canopen.org/xml/1.1"">
  <ISO15745Profile>
    <ProfileHeader xmlns=""""><ProfileClassID>Device</ProfileClassID></ProfileHeader>
    <ProfileBody xmlns:q1=""http://www.canopen.org/xml/1.1"" xsi:type=""q1:ProfileBody_Device_CANopen"" fileName=""t.xdd"" fileVersion=""1"" xmlns="""">
      <q1:DeviceIdentity><q1:vendorName>Test Vendor</q1:vendorName><q1:vendorID>0x1</q1:vendorID><q1:productName>P</q1:productName><q1:productID>0x1</q1:productID></q1:DeviceIdentity>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileHeader xmlns=""""><ProfileClassID>CommunicationNetwork</ProfileClassID></ProfileHeader>
    <ProfileBody xmlns:q2=""http://www.canopen.org/xml/1.1"" xsi:type=""q2:ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdd"" fileVersion=""1"" xmlns="""">
      <ApplicationLayers>
        <q2:CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007"" accessType=""ro"" PDOmapping=""no""/>
        </q2:CANopenObjectList>
      </ApplicationLayers>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

    private const string FullyQualifiedDocument = @"<?xml version=""1.0"" encoding=""utf-8""?>
<co:ISO15745ProfileContainer xmlns:co=""http://www.canopen.org/xml/1.1"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <co:ISO15745Profile>
    <co:ProfileHeader><co:ProfileClassID>Device</co:ProfileClassID></co:ProfileHeader>
    <co:ProfileBody xsi:type=""co:ProfileBody_Device_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <co:DeviceIdentity><co:vendorName>Test Vendor</co:vendorName><co:vendorID>0x1</co:vendorID><co:productName>P</co:productName><co:productID>0x1</co:productID></co:DeviceIdentity>
    </co:ProfileBody>
  </co:ISO15745Profile>
  <co:ISO15745Profile>
    <co:ProfileHeader><co:ProfileClassID>CommunicationNetwork</co:ProfileClassID></co:ProfileHeader>
    <co:ProfileBody xsi:type=""co:ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <co:ApplicationLayers>
        <co:CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <co:CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007"" accessType=""ro"" PDOmapping=""no""/>
        </co:CANopenObjectList>
      </co:ApplicationLayers>
    </co:ProfileBody>
  </co:ISO15745Profile>
</co:ISO15745ProfileContainer>";

    private sealed class SchemaParticle
    {
        internal SchemaParticle(XName? parentElement, string? parentType, string child, bool qualified, string parentDisplay)
        {
            ParentElement = parentElement;
            ParentType = parentType;
            Child = child;
            Qualified = qualified;
            ParentDisplay = parentDisplay;
        }

        internal XName? ParentElement { get; }

        internal string? ParentType { get; }

        internal string Child { get; }

        internal bool Qualified { get; }

        internal string ParentDisplay { get; }
    }
}
