namespace EdsDcfNet.Tests.Parsers;

using System.Xml.Linq;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Tests.Utilities;
using EdsDcfNet.Utilities;

/// <summary>
/// Opt-in CiA 306-1 Table 7 <c>DataType</c> default for DOMAIN sub-index sections.
/// </summary>
public class DomainSubIndexDataTypeDefaultTests
{
    private static readonly CanOpenFileOptions ApplyDefault = new() { ApplyDomainDataTypeDefault = true };

    private static readonly CanOpenWriteOptions Lf = new() { NewLine = "\n" };

    private static readonly CanOpenWriteOptions ValidatedLf = new()
    {
        ValidateBeforeWrite = true,
        NewLine = "\n"
    };

    [Fact]
    public void ReadString_DomainSubIndexWithoutDataType_StaysZeroByDefault()
    {
        // Arrange
        var content = RecordWithDomainSub();

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);

        // Assert
        DomainSub(eds).DataType.Should().Be(0);
        DomainSub(eds).DataTypeDefaultBaseline.Should().BeNull();
    }

    [Fact]
    public void WriteToString_DomainSubIndexWithoutDataType_DefaultOption_WritesZero()
    {
        // Arrange: the flag is off, so the historical DataType=0x0 write stays.
        var eds = CanOpenFile.Eds.ReadString(RecordWithDomainSub());

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds, Lf);

        // Assert
        Section(written, "2000sub1").Should().Contain("DataType=0x0");
    }

    [Fact]
    public void ReadString_DomainSubIndexWithoutDataType_ApplyDomainDataTypeDefault_ExposesDomain()
    {
        // Arrange
        var content = RecordWithDomainSub();

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content, ApplyDefault);

        // Assert
        var sub = DomainSub(result.Model);
        sub.DataType.Should().Be(CanOpenDataType.Domain);
        sub.DataTypeDefaultBaseline.Should().Be(CanOpenDataType.Domain);
        sub.ObjectType.Should().Be(CanOpenObjectType.Domain);
        result.Diagnostics.Should().NotContain(d => d.Code == ParseDiagnosticCodes.InvalidDataType);
    }

    [Fact]
    public void WriteToString_UnchangedDomainSubIndex_ValidatedRoundTrip_OmitsDataType()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(RecordWithDomainSub(), ApplyDefault);

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds, ValidatedLf);
        var again = CanOpenFile.Eds.ReadString(written, ApplyDefault);
        var rewritten = CanOpenFile.Eds.WriteToString(again, ValidatedLf);

        // Assert
        Section(written, "2000sub1").Should().NotContain("DataType");
        DomainSub(again).DataType.Should().Be(CanOpenDataType.Domain);
        rewritten.Should().Be(written);
    }

    [Fact]
    public void ReadString_ApplyDefault_DoesNotLeakIntoTheNextRead()
    {
        // Arrange
        var content = RecordWithDomainSub();
        CanOpenFile.Eds.ReadString(content, ApplyDefault);

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);

        // Assert
        DomainSub(eds).DataType.Should().Be(0);
    }

    [Fact]
    public void ReadString_ExplicitDataTypeZero_ApplyDomainDataTypeDefault_StaysZero()
    {
        // Arrange
        var content = RecordWithDomainSub("DataType=0");

        // Act
        var eds = CanOpenFile.Eds.ReadString(content, ApplyDefault);
        var written = CanOpenFile.Eds.WriteToString(eds, Lf);

        // Assert
        DomainSub(eds).DataType.Should().Be(0);
        DomainSub(eds).DataTypeDefaultBaseline.Should().BeNull();
        Section(written, "2000sub1").Should().Contain("DataType=0x0");
    }

    [Fact]
    public void ReadString_ExplicitDomainDataType_ApplyDomainDataTypeDefault_WritesIt()
    {
        // Arrange: a present 0x000F is the caller's value, not the filled-in default.
        var content = RecordWithDomainSub("DataType=0x000F");

        // Act
        var eds = CanOpenFile.Eds.ReadString(content, ApplyDefault);
        var written = CanOpenFile.Eds.WriteToString(eds, Lf);

        // Assert
        DomainSub(eds).DataType.Should().Be(CanOpenDataType.Domain);
        DomainSub(eds).DataTypeDefaultBaseline.Should().BeNull();
        Section(written, "2000sub1").Should().Contain("DataType=0xF");
    }

    [Fact]
    public void ReadString_ExplicitDataTypeAtMaxValue_ApplyDomainDataTypeDefault_RoundTrips()
    {
        // Arrange
        var content = RecordWithDomainSub("DataType=0xFFFF");

        // Act
        var eds = CanOpenFile.Eds.ReadString(content, ApplyDefault);
        var written = CanOpenFile.Eds.WriteToString(eds, Lf);
        var reread = CanOpenFile.Eds.ReadString(written, ApplyDefault);

        // Assert
        DomainSub(eds).DataType.Should().Be(ushort.MaxValue);
        Section(written, "2000sub1").Should().Contain("DataType=0xFFFF");
        DomainSub(reread).DataType.Should().Be(ushort.MaxValue);
        DomainSub(reread).DataTypeDefaultBaseline.Should().BeNull();
    }

    [Fact]
    public void ReadString_EmptyDataType_ApplyDomainDataTypeDefault_UsesDomainAndOmitsKey()
    {
        // Arrange: Table 7 treats an empty entry like a missing one, as object sections do.
        var content = RecordWithDomainSub("DataType=");

        // Act
        var eds = CanOpenFile.Eds.ReadString(content, ApplyDefault);
        var written = CanOpenFile.Eds.WriteToString(eds, Lf);

        // Assert
        DomainSub(eds).DataType.Should().Be(CanOpenDataType.Domain);
        Section(written, "2000sub1").Should().NotContain("DataType");
    }

    [Fact]
    public void ReadString_EmptyDataType_DefaultOption_StaysZero()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(RecordWithDomainSub("DataType="));

        // Act / Assert
        DomainSub(eds).DataType.Should().Be(0);
    }

    [Fact]
    public void ReadString_WhitespaceDataType_ApplyDomainDataTypeDefault_UsesDomainAndOmitsKey()
    {
        // Arrange: the INI parser trims the value, so whitespace-only is an empty entry.
        var content = RecordWithDomainSub("DataType= ");

        // Act
        var withDefault = CanOpenFile.Eds.ReadString(content, ApplyDefault);
        var withoutDefault = CanOpenFile.Eds.ReadString(content);
        var written = CanOpenFile.Eds.WriteToString(withDefault, Lf);

        // Assert
        DomainSub(withDefault).DataType.Should().Be(CanOpenDataType.Domain);
        DomainSub(withoutDefault).DataType.Should().Be(0);
        Section(written, "2000sub1").Should().NotContain("DataType");
    }

    [Fact]
    public void ReadString_TrimmedExplicitZero_ApplyDomainDataTypeDefault_StaysZero()
    {
        // Arrange: a value that is non-empty after trimming is not the omission.
        var content = RecordWithDomainSub("DataType= 0 ");

        // Act
        var eds = CanOpenFile.Eds.ReadString(content, ApplyDefault);
        var written = CanOpenFile.Eds.WriteToString(eds, Lf);

        // Assert
        DomainSub(eds).DataType.Should().Be(0);
        DomainSub(eds).DataTypeDefaultBaseline.Should().BeNull();
        Section(written, "2000sub1").Should().Contain("DataType=0x0");
    }

    [Fact]
    public void ReadString_MalformedDataType_ApplyDomainDataTypeDefault_DoesNotReplace()
    {
        // Arrange
        var content = RecordWithDomainSub("DataType=nope");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content, ApplyDefault);

        // Assert
        DomainSub(result.Model).DataType.Should().Be(0);
        DomainSub(result.Model).DataTypeDefaultBaseline.Should().BeNull();
        result.Diagnostics.Should().Contain(d => d.Code == ParseDiagnosticCodes.InvalidDataType);
    }

    [Fact]
    public void ReadString_MalformedDataType_StrictAndApplyDefault_ThrowsEdsParseException()
    {
        // Arrange
        var options = new CanOpenFileOptions
        {
            StrictParsing = true,
            ApplyDomainDataTypeDefault = true
        };

        // Act
        var act = () => CanOpenFile.Eds.ReadString(RecordWithDomainSub("DataType=nope"), options);

        // Assert
        act.Should().Throw<EdsParseException>()
            .Which.Code.Should().Be(ParseDiagnosticCodes.InvalidDataType);
    }

    [Fact]
    public void ReadString_VarSubIndexWithoutDataType_ApplyDomainDataTypeDefault_StaysZero()
    {
        // Arrange
        var content = Eds(
            "[2000]",
            "ParameterName=Rec",
            "ObjectType=0x9",
            "SubNumber=2",
            "[2000sub1]",
            "ParameterName=Count",
            "ObjectType=0x7",
            "AccessType=ro",
            "[2000sub2]",
            "ParameterName=Blob",
            "ObjectType=0x2",
            "AccessType=rw");

        // Act
        var eds = CanOpenFile.Eds.ReadString(content, ApplyDefault);
        var written = CanOpenFile.Eds.WriteToString(eds, Lf);

        // Assert
        eds.ObjectDictionary.Objects[0x2000].SubObjects[1].DataType.Should().Be(0);
        eds.ObjectDictionary.Objects[0x2000].SubObjects[2].DataType.Should().Be(CanOpenDataType.Domain);
        Section(written, "2000sub1").Should().Contain("DataType=0x0");
        Section(written, "2000sub2").Should().NotContain("DataType");
    }

    [Fact]
    public void ReadString_MissingObjectType_ApplyDomainDataTypeDefault_StaysVarAndZero()
    {
        // Arrange: Table 7 NOTE 1, a missing ObjectType is VAR, not DOMAIN.
        var content = Eds(
            "[2000]",
            "ParameterName=Rec",
            "ObjectType=0x9",
            "SubNumber=1",
            "[2000sub1]",
            "ParameterName=Blob",
            "AccessType=rw");

        // Act
        var eds = CanOpenFile.Eds.ReadString(content, ApplyDefault);

        // Assert
        var sub = DomainSub(eds);
        sub.ObjectType.Should().Be(CanOpenObjectType.Var);
        sub.DataType.Should().Be(0);
    }

    [Fact]
    public void ReadString_DecimalObjectTypeDomain_ApplyDomainDataTypeDefault_ExposesDomain()
    {
        // Arrange
        var content = RecordWithDomainSub(objectType: "2");

        // Act
        var eds = CanOpenFile.Eds.ReadString(content, ApplyDefault);

        // Assert
        DomainSub(eds).DataType.Should().Be(CanOpenDataType.Domain);
    }

    [Fact]
    public void ReadString_DomainObjectWithoutDataType_StillWritesObjectDataType()
    {
        // Arrange: the object-level default is independent of the sub-index flag.
        var content = Eds(
            "[2000]",
            "ParameterName=D",
            "ObjectType=0x2",
            "SubNumber=1",
            "[2000sub1]",
            "ParameterName=Blob",
            "ObjectType=0x2",
            "AccessType=rw");

        // Act
        var eds = CanOpenFile.Eds.ReadString(content, ApplyDefault);
        var written = CanOpenFile.Eds.WriteToString(eds, Lf);

        // Assert
        eds.ObjectDictionary.Objects[0x2000].DataType.Should().Be(CanOpenDataType.Domain);
        Section(written, "2000").Should().Contain("DataType=0xF");
        Section(written, "2000sub1").Should().NotContain("DataType");
    }

    [Fact]
    public void AssignDataType_IncludingDomain_WritesKey()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(RecordWithDomainSub(), ApplyDefault);
        var sub = DomainSub(eds);

        // Act
        sub.DataType = CanOpenDataType.Domain;
        var writtenSame = CanOpenFile.Eds.WriteToString(eds, Lf);
        sub.DataType = CanOpenDataType.Unsigned32;
        var writtenOther = CanOpenFile.Eds.WriteToString(eds, Lf);

        // Assert
        sub.DataTypeDefaultBaseline.Should().BeNull();
        Section(writtenSame, "2000sub1").Should().Contain("DataType=0xF");
        Section(writtenOther, "2000sub1").Should().Contain("DataType=0x7");
    }

    [Fact]
    public void ChangeObjectType_WritesSuppliedDataType()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(RecordWithDomainSub(), ApplyDefault);
        DomainSub(eds).ObjectType = CanOpenObjectType.Var;

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds, Lf);

        // Assert
        Section(written, "2000sub1").Should().Contain("DataType=0xF");
    }

    [Fact]
    public void WriteToString_BaselineDiffersFromDataType_WritesDataType()
    {
        // Arrange: the writer omits the key only while the value still equals the supplied default.
        var eds = CanOpenFile.Eds.ReadString(RecordWithDomainSub(), ApplyDefault);
        DomainSub(eds).DataTypeDefaultBaseline = CanOpenDataType.Unsigned8;

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds, Lf);

        // Assert
        Section(written, "2000sub1").Should().Contain("DataType=0xF");
    }

    [Fact]
    public void WriteToString_ConstructedDomainSubIndex_WritesDataType()
    {
        // Arrange
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        var record = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "Rec",
            ObjectType = CanOpenObjectType.Record,
            SubNumber = 1
        };
        record.SubObjects[1] = new CanOpenSubObject
        {
            SubIndex = 1,
            ParameterName = "Blob",
            ObjectType = CanOpenObjectType.Domain,
            DataType = CanOpenDataType.Domain,
            AccessType = AccessType.ReadWrite
        };
        eds.ObjectDictionary.ManufacturerObjects.Add(0x2000);
        eds.ObjectDictionary.Objects[0x2000] = record;

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds, Lf);

        // Assert
        Section(written, "2000sub1").Should().Contain("DataType=0xF");
    }

    [Fact]
    public void Clone_PreservesOmissionUntilDataTypeAssigned()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(RecordWithDomainSub(), ApplyDefault);
        var clone = ModelCloner.CloneObject(eds.ObjectDictionary.Objects[0x2000]);
        eds.ObjectDictionary.Objects[0x2000] = clone;

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds, Lf);
        clone.SubObjects[1].DataType = CanOpenDataType.Domain;
        var afterAssign = CanOpenFile.Eds.WriteToString(eds, Lf);

        // Assert
        Section(written, "2000sub1").Should().NotContain("DataType");
        Section(afterAssign, "2000sub1").Should().Contain("DataType=0xF");
    }

    [Fact]
    public void EdsReader_DirectRead_StaysZero()
    {
        // Arrange: direct readers do not take CanOpenFileOptions.
        var reader = new EdsReader();

        // Act
        var eds = reader.ReadString(RecordWithDomainSub());

        // Assert
        DomainSub(eds).DataType.Should().Be(0);
    }

    [Fact]
    public void Dcf_ReadString_DomainSubIndexWithoutDataType_ApplyDefault_OmitsOnWrite()
    {
        // Arrange
        var content = Dcf(RecordBody("ParameterValue=blob"));

        // Act
        var dcf = CanOpenFile.Dcf.ReadString(content, ApplyDefault);
        var written = CanOpenFile.Dcf.WriteToString(dcf, ValidatedLf);
        var again = CanOpenFile.Dcf.ReadString(written, ApplyDefault);

        // Assert
        var sub = dcf.ObjectDictionary.Objects[0x2000].SubObjects[1];
        sub.DataType.Should().Be(CanOpenDataType.Domain);
        sub.ParameterValue.Should().Be("blob");
        Section(written, "2000sub1").Should().NotContain("DataType");
        Section(written, "2000sub1").Should().Contain("ParameterValue=blob");
        again.ObjectDictionary.Objects[0x2000].SubObjects[1].DataType.Should().Be(CanOpenDataType.Domain);
    }

    [Fact]
    public void Dcf_ReadString_DomainSubIndexWithoutDataType_DefaultOption_StaysZero()
    {
        // Act
        var dcf = CanOpenFile.Dcf.ReadString(Dcf(RecordBody()));

        // Assert
        dcf.ObjectDictionary.Objects[0x2000].SubObjects[1].DataType.Should().Be(0);
    }

    [Fact]
    public void ReadString_ModuleFixedDomainSubIndex_ApplyDefault_OmitsOnWrite()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(ModuleEds(), ApplyDefault);

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds, Lf);
        var again = CanOpenFile.Eds.ReadString(written, ApplyDefault);

        // Assert
        var sub = eds.SupportedModules[0].FixedObjectDefinitions[0x2000].SubObjects[1];
        sub.DataType.Should().Be(CanOpenDataType.Domain);
        Section(written, "M1Fixed2000sub1").Should().NotContain("DataType");
        again.SupportedModules[0].FixedObjectDefinitions[0x2000].SubObjects[1].DataType.Should().Be(CanOpenDataType.Domain);
    }

    [Fact]
    public void Dcf_ReadString_ModuleFixedDomainSubIndex_ApplyDefault_OmitsOnWrite()
    {
        // Arrange
        var dcf = CanOpenFile.Dcf.ReadString(Dcf(ModuleBody()), ApplyDefault);

        // Act
        var written = CanOpenFile.Dcf.WriteToString(dcf, Lf);

        // Assert
        dcf.SupportedModules[0].FixedObjectDefinitions[0x2000].SubObjects[1].DataType.Should().Be(CanOpenDataType.Domain);
        Section(written, "M1Fixed2000sub1").Should().NotContain("DataType");
    }

    [Fact]
    public void ReadString_ModuleFixedDomainSubIndex_DefaultOption_StaysZero()
    {
        // Act
        var eds = CanOpenFile.Eds.ReadString(ModuleEds());

        // Assert
        eds.SupportedModules[0].FixedObjectDefinitions[0x2000].SubObjects[1].DataType.Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Xdd_DomainSubObjectWithoutDataType_IgnoresOption(bool applyDefault)
    {
        // Arrange: CiA 311 has no Table 7 sub-index default. A missing dataType stays 0.
        var options = new CanOpenFileOptions { ApplyDomainDataTypeDefault = applyDefault };

        // Act
        var xdd = CanOpenFile.Xdd.ReadString(DomainSubXdd(), options);
        var written = CanOpenFile.Xdd.WriteToString(xdd, Lf);

        // Assert
        var sub = xdd.ObjectDictionary.Objects[0x2000].SubObjects[1];
        sub.ObjectType.Should().Be(CanOpenObjectType.Domain);
        sub.DataType.Should().Be(0);
        ((string?)SubElement(written, "01").Attribute("dataType")).Should().Be("0000");
    }

    [Fact]
    public void Xdc_DomainSubObjectWithoutDataType_IgnoresOption()
    {
        // Arrange: XDC uses the same CANopenSubObject reader as XDD.
        var xdc = CanOpenFile.Xdc.ReadString(DomainSubXdd(), ApplyDefault);

        // Act
        var written = CanOpenFile.Xdc.WriteToString(xdc, Lf);

        // Assert
        xdc.ObjectDictionary.Objects[0x2000].SubObjects[1].DataType.Should().Be(0);
        ((string?)SubElement(written, "01").Attribute("dataType")).Should().Be("0000");
    }

    [Fact]
    public void Scope_DisposeTwice_RestoresPreviousValue()
    {
        // Arrange
        var scope = DomainDataTypeDefaultScope.Enter(true);

        // Act
        scope.Dispose();
        scope.Dispose();

        // Assert
        DomainDataTypeDefaultScope.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Scope_NestedEnter_RestoresOuterValue()
    {
        // Act / Assert
        using (DomainDataTypeDefaultScope.Enter(true))
        {
            DomainDataTypeDefaultScope.IsEnabled.Should().BeTrue();
            using (DomainDataTypeDefaultScope.Enter(false))
            {
                DomainDataTypeDefaultScope.IsEnabled.Should().BeFalse();
            }

            DomainDataTypeDefaultScope.IsEnabled.Should().BeTrue();
        }

        DomainDataTypeDefaultScope.IsEnabled.Should().BeFalse();
    }

    private static CanOpenSubObject DomainSub(ElectronicDataSheet eds)
        => eds.ObjectDictionary.Objects[0x2000].SubObjects[1];

    private static string RecordWithDomainSub(string? dataTypeLine = null, string objectType = "0x2")
        => Eds(RecordBody(dataTypeLine, objectType));

    private static IEnumerable<string> RecordBody(string? dataTypeLine = null, string objectType = "0x2")
    {
        yield return "[2000]";
        yield return "ParameterName=Rec";
        yield return "ObjectType=0x9";
        yield return "SubNumber=1";
        yield return "[2000sub1]";
        yield return "ParameterName=Blob";
        yield return "ObjectType=" + objectType;
        if (dataTypeLine != null)
            yield return dataTypeLine;
        yield return "AccessType=rw";
    }

    private static string Eds(IEnumerable<string> body)
    {
        var lines = new List<string>
        {
            "[FileInfo]",
            "FileName=t.eds",
            "EDSVersion=4.0",
            "[DeviceInfo]",
            "VendorName=Test",
            "[ManufacturerObjects]",
            "SupportedObjects=1",
            "1=0x2000"
        };
        lines.AddRange(body);
        return string.Join("\n", lines);
    }

    private static string Eds(params string[] body) => Eds((IEnumerable<string>)body);

    private static string Dcf(IEnumerable<string> body)
    {
        var lines = new List<string>
        {
            "[FileInfo]",
            "FileName=t.dcf",
            "FileVersion=1",
            "[DeviceInfo]",
            "VendorName=Test",
            "[DeviceComissioning]",
            "NodeID=5",
            "Baudrate=250",
            "[ManufacturerObjects]",
            "SupportedObjects=1",
            "1=0x2000"
        };
        lines.AddRange(body);
        return string.Join("\n", lines);
    }

    private static string ModuleEds() => Eds(ModuleBody());

    private static IEnumerable<string> ModuleBody()
    {
        yield return "[SupportedModules]";
        yield return "NrOfEntries=1";
        yield return "[M1ModuleInfo]";
        yield return "ProductName=Module";
        yield return "[M1FixedObjects]";
        yield return "NrOfEntries=1";
        yield return "1=0x2000";
        yield return "[M1Fixed2000]";
        yield return "ParameterName=Rec";
        yield return "ObjectType=0x9";
        yield return "SubNumber=1";
        yield return "[M1Fixed2000sub1]";
        yield return "ParameterName=Blob";
        yield return "ObjectType=0x2";
        yield return "AccessType=rw";
    }

    private static string Section(string text, string name)
    {
        var normalized = text.Replace("\r\n", "\n");
        var header = "[" + name + "]";
        var start = 0;
        while (true)
        {
            var at = normalized.IndexOf(header, start, StringComparison.Ordinal);
            at.Should().BeGreaterThanOrEqualTo(0, "section " + name + " should be present");
            var boundary = at == 0 || normalized[at - 1] == '\n';
            if (boundary)
            {
                var next = normalized.IndexOf("\n[", at + header.Length, StringComparison.Ordinal);
                return next < 0 ? normalized[at..] : normalized[at..next];
            }

            start = at + header.Length;
        }
    }

    private static string DomainSubXdd() =>
        """
        <?xml version="1.0" encoding="utf-8"?>
        <ISO15745ProfileContainer xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
          <ISO15745Profile>
            <ProfileHeader><ProfileClassID>Device</ProfileClassID></ProfileHeader>
            <ProfileBody xsi:type="ProfileBody_Device_CANopen" fileName="test.xdd" fileCreator="TestCreator" fileCreationDate="2025-01-15" fileVersion="1">
              <DeviceIdentity>
                <vendorName>Test Vendor</vendorName>
                <vendorID>0x00000100</vendorID>
                <productName>Test Product</productName>
                <productID>0x00001001</productID>
              </DeviceIdentity>
              <DeviceManager/>
              <DeviceFunction/>
            </ProfileBody>
          </ISO15745Profile>
          <ISO15745Profile>
            <ProfileHeader><ProfileClassID>CommunicationNetwork</ProfileClassID></ProfileHeader>
            <ProfileBody xsi:type="ProfileBody_CommunicationNetwork_CANopen" fileName="test.xdd" fileCreator="TestCreator" fileCreationDate="2025-01-15" fileVersion="1">
              <ApplicationLayers>
                <CANopenObjectList mandatoryObjects="0" optionalObjects="0" manufacturerObjects="1">
                  <CANopenObject index="2000" name="Rec" objectType="9">
                    <CANopenSubObject subIndex="01" name="Blob" objectType="2" accessType="rw" PDOmapping="no"/>
                  </CANopenObject>
                </CANopenObjectList>
              </ApplicationLayers>
              <TransportLayers>
                <PhysicalLayer>
                  <baudRate defaultValue="250 Kbps">
                    <supportedBaudRate value="250 Kbps"/>
                  </baudRate>
                </PhysicalLayer>
              </TransportLayers>
              <NetworkManagement>
                <CANopenGeneralFeatures granularity="8" nrOfRxPDO="0" nrOfTxPDO="0"/>
              </NetworkManagement>
            </ProfileBody>
          </ISO15745Profile>
        </ISO15745ProfileContainer>
        """;

    private static XElement SubElement(string xml, string subIndex)
    {
        var document = XDocument.Parse(xml);
        return document.Descendants()
            .Single(e => e.Name.LocalName == "CANopenSubObject" && (string?)e.Attribute("subIndex") == subIndex);
    }
}
