namespace EdsDcfNet.Tests.Parsers;

using System.Xml.Linq;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Tests.Utilities;
using EdsDcfNet.Validation;

/// <summary>
/// CiA 311 v1.1.0 <c>deviceCommissioning</c>: <c>nodeID</c> is an <c>xsd:unsignedByte</c> (surrounding
/// whitespace and a leading plus sign are part of its lexical space), and <c>100 Kbps</c> is a value of the
/// baud-rate vocabulary (Tables 55/56) that the model holds as <c>Baudrate = 100</c>.
/// </summary>
public class XddUnsignedByteAndHundredKbpsTests
{
    private static readonly CanOpenFileOptions Strict = new() { StrictParsing = true };

    [Theory]
    [InlineData(" 5", 5)]
    [InlineData("5 ", 5)]
    [InlineData("\t5\n", 5)]
    [InlineData("+5", 5)]
    [InlineData(" +5 ", 5)]
    [InlineData("005", 5)]
    [InlineData("0x1F", 31)]
    [InlineData("127", 127)]
    public void ReadString_XdcNodeIdInUnsignedByteLexicalSpace_ParsesInBothModes(string nodeId, int expected)
    {
        // Arrange
        var xml = Xdc(nodeId: nodeId);

        // Act
        var lenient = CanOpenFile.Xdc.ReadString(xml);
        var strict = CanOpenFile.Xdc.ReadString(xml, Strict);

        // Assert
        lenient.DeviceCommissioning.NodeId.Should().Be((byte)expected);
        strict.DeviceCommissioning.NodeId.Should().Be((byte)expected);
    }

    [Theory]
    [InlineData("256")]
    [InlineData("+256")]
    [InlineData("-1")]
    [InlineData("5.0")]
    [InlineData("banana")]
    public void ReadString_XdcNodeIdOutsideUnsignedByteLexicalSpace_StrictThrows(string nodeId)
    {
        // Arrange
        var xml = Xdc(nodeId: nodeId);

        // Act
        var act = () => CanOpenFile.Xdc.ReadString(xml, Strict);

        // Assert
        act.Should().Throw<EdsParseException>().WithMessage("*nodeID*");
    }

    [Fact]
    public void ReadString_XdcNodeIdAboveUnsignedByte_LenientAlsoThrows()
    {
        // Arrange
        var xml = Xdc(nodeId: "256");

        // Act
        var act = () => CanOpenFile.Xdc.ReadString(xml);

        // Assert
        act.Should().Throw<EdsParseException>().WithMessage("*nodeID*");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteToString_XdcHundredKbps_RoundTripsCharacterExactAndMapsToHundred(bool strict)
    {
        // Arrange
        var xml = Xdc(actualBaudRate: "100 Kbps");
        var options = strict ? Strict : null;

        // Act
        var dcf = CanOpenFile.Xdc.ReadString(xml, options);
        var plain = CanOpenFile.Xdc.WriteToString(dcf);
        var validated = CanOpenFile.Xdc.WriteToString(dcf, CanOpenWriteOptions.Validated);

        // Assert
        dcf.DeviceCommissioning.Baudrate.Should().Be(100);
        Attribute(plain, "actualBaudRate").Should().Be("100 Kbps");
        Attribute(validated, "actualBaudRate").Should().Be("100 Kbps");
    }

    [Fact]
    public void ReadStringWithDiagnostics_XdcHundredKbps_ReportsNoBaudRateDiagnostic()
    {
        // Act
        var result = CanOpenFile.Xdc.ReadStringWithDiagnostics(Xdc(actualBaudRate: " 100 Kbps "));

        // Assert
        result.Model.DeviceCommissioning.Baudrate.Should().Be(100);
        result.Diagnostics.Should().NotContain(d => d.Path == "actualBaudRate");
    }

    [Fact]
    public void WriteToString_XdcModelWithBaudrateHundred_ValidatedWritesHundredKbps()
    {
        // Arrange
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        dcf.DeviceCommissioning.Baudrate = 100;

        // Act
        var written = CanOpenFile.Xdc.WriteToString(dcf, CanOpenWriteOptions.Validated);

        // Assert
        Attribute(written, "actualBaudRate").Should().Be("100 Kbps");
    }

    [Fact]
    public void WriteToString_DcfModelWithBaudrateHundred_ValidatedWritesHundred()
    {
        // Arrange
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        dcf.DeviceCommissioning.Baudrate = 100;

        // Act
        var written = CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);
        var again = CanOpenFile.Dcf.ReadString(written);

        // Assert
        again.DeviceCommissioning.Baudrate.Should().Be(100);
    }

    [Fact]
    public void Validate_DcfBaudrateNotInVocabulary_StillReportsIssueListingHundred()
    {
        // Arrange
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        dcf.DeviceCommissioning.Baudrate = 99;

        // Act
        var issues = CanOpenModelValidator.Validate(dcf);

        // Assert
        issues.Should().Contain(i => i.Path == "DeviceCommissioning.Baudrate" && i.Message.Contains("100", StringComparison.Ordinal));
    }

    private static string? Attribute(string xml, string attribute)
        => XDocument.Parse(xml).Descendants()
            .First(e => e.Name.LocalName == "deviceCommissioning")
            .Attribute(attribute)?.Value;

    private static string Xdc(string nodeId = "5", string actualBaudRate = "250 Kbps")
    {
        const string File = " fileName=\"t.xdc\" fileCreator=\"me\" fileCreationDate=\"2026-01-01\" fileVersion=\"1\"";
        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
               "<ISO15745ProfileContainer xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
               "<ISO15745Profile><ProfileBody xsi:type=\"ProfileBody_Device_CANopen\"" + File + ">" +
               "<DeviceIdentity><vendorName>T</vendorName><vendorID>0x1</vendorID><productName>T</productName><productID>0x1</productID></DeviceIdentity>" +
               "<DeviceManager/><DeviceFunction/></ProfileBody></ISO15745Profile>" +
               "<ISO15745Profile><ProfileBody xsi:type=\"ProfileBody_CommunicationNetwork_CANopen\"" + File + ">" +
               "<ApplicationLayers><CANopenObjectList mandatoryObjects=\"1\" optionalObjects=\"0\" manufacturerObjects=\"0\">" +
               "<CANopenObject index=\"1000\" name=\"Device Type\" objectType=\"7\" dataType=\"0007\" accessType=\"ro\" PDOmapping=\"no\"/>" +
               "</CANopenObjectList></ApplicationLayers>" +
               "<TransportLayers><PhysicalLayer><baudRate defaultValue=\"250 Kbps\"/></PhysicalLayer></TransportLayers>" +
               "<NetworkManagement><CANopenGeneralFeatures granularity=\"8\" nrOfRxPDO=\"0\" nrOfTxPDO=\"0\"/>" +
               "<deviceCommissioning nodeID=\"" + nodeId + "\" nodeName=\"n\" actualBaudRate=\"" + actualBaudRate +
               "\" networkNumber=\"1\" networkName=\"net\" CANopenManager=\"false\"/>" +
               "</NetworkManagement></ProfileBody></ISO15745Profile></ISO15745ProfileContainer>";
    }
}
