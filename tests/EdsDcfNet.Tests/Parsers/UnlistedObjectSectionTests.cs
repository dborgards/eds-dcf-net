namespace EdsDcfNet.Tests.Parsers;

using System.Globalization;
using EdsDcfNet;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;

/// <summary>
/// Hexadecimal section names are object indexes only when an object list cites them.
/// An unlisted index, including a name that is only accidentally hexadecimal, stays in
/// <c>AdditionalSections</c> and is reported.
/// </summary>
public class UnlistedObjectSectionTests
{
    private static readonly CanOpenFileOptions Strict = new() { StrictParsing = true };

    [Fact]
    public void ReadString_UnlistedObjectSection_PreservesSectionAndReportsDiagnostic()
    {
        // Arrange — [2000] is a real object body, but no object list cites 0x2000.
        // ObjectType is deliberately not a number so a parse-as-object path would warn.
        var content = Eds("""
            [2000]
            ParameterName=Hidden
            ObjectType=not-a-type
            DataType=0x0005
            AccessType=ro
            DefaultValue=1
            PDOMapping=0
            """);

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.ObjectDictionary.Objects.Should().ContainKey(0x1000);
        result.Model.ObjectDictionary.Objects.Should().NotContainKey(0x2000);
        result.Model.ObjectDictionary.MandatoryObjects.Should().NotContain((ushort)0x2000);
        result.Model.ObjectDictionary.OptionalObjects.Should().NotContain((ushort)0x2000);
        result.Model.ObjectDictionary.ManufacturerObjects.Should().NotContain((ushort)0x2000);
        result.Model.AdditionalSections.Should().ContainKey("2000");
        result.Model.AdditionalSections["2000"]["ParameterName"].Should().Be("Hidden");
        result.Model.AdditionalSections["2000"]["ObjectType"].Should().Be("not-a-type");
        result.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.IniUnlistedObjectSection &&
            diagnostic.Severity == ParseSeverity.Warning &&
            diagnostic.Path == "2000" &&
            diagnostic.Message == "object section 0x2000 not listed in any object list");
        result.Diagnostics.Should().NotContain(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.InvalidObjectType);
    }

    [Fact]
    public void ReadString_UnlistedObjectSection_StrictParsing_ThrowsEdsParseException()
    {
        // Arrange
        var content = Eds("""
            [2000]
            ParameterName=Hidden
            ObjectType=0x7
            """);

        // Act
        var act = () => CanOpenFile.Eds.ReadString(content, Strict);

        // Assert
        var exception = act.Should().Throw<EdsParseException>().Which;
        exception.Code.Should().Be(ParseDiagnosticCodes.IniUnlistedObjectSection);
        exception.SectionName.Should().Be("2000");
        exception.Message.Should().Be("object section 0x2000 not listed in any object list");
    }

    [Fact]
    public void ReadString_AccidentallyHexSectionNames_PreservedInAdditionalSections()
    {
        // Arrange — [Face] and [Bad] are words that also parse as hexadecimal indexes.
        var content = Eds("""
            [Face]
            VendorKey=kept

            [Bad]
            Also=kept
            """);

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.ObjectDictionary.Objects.Should().NotContainKey(0xFACE);
        result.Model.ObjectDictionary.Objects.Should().NotContainKey(0x0BAD);
        result.Model.AdditionalSections.Should().ContainKey("Face");
        result.Model.AdditionalSections["Face"]["VendorKey"].Should().Be("kept");
        result.Model.AdditionalSections.Should().ContainKey("Bad");
        result.Model.AdditionalSections["Bad"]["Also"].Should().Be("kept");
        result.Diagnostics.Select(diagnostic => diagnostic.Message).Should().Equal(
            "object section 0xFACE not listed in any object list",
            "object section 0x0BAD not listed in any object list");
        result.Diagnostics.Should().OnlyContain(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.IniUnlistedObjectSection &&
            diagnostic.Severity == ParseSeverity.Warning);
    }

    [Fact]
    public void ReadString_AccidentallyHexSectionName_StrictParsing_ThrowsEdsParseException()
    {
        // Arrange
        var content = Eds("""
            [Face]
            VendorKey=kept
            """);

        // Act
        var act = () => CanOpenFile.Eds.ReadString(content, Strict);

        // Assert
        var exception = act.Should().Throw<EdsParseException>().Which;
        exception.Code.Should().Be(ParseDiagnosticCodes.IniUnlistedObjectSection);
        exception.SectionName.Should().Be("Face");
        exception.Message.Should().Be("object section 0xFACE not listed in any object list");
    }

    [Theory]
    [InlineData("MandatoryObjects")]
    [InlineData("OptionalObjects")]
    [InlineData("ManufacturerObjects")]
    public void ReadString_IndexListedInObjectList_ParsesObjectAndOmitsUnlistedDiagnostic(string listSection)
    {
        // Arrange — the same body is an object when any object list cites the index.
        var content = $"""
            [DeviceInfo]
            VendorName=Test
            ProductName=Test

            [{listSection}]
            SupportedObjects=1
            1=0x2000

            [2000]
            ParameterName=Listed
            ObjectType=0x7
            DataType=0x0005
            AccessType=rw
            DefaultValue=0
            PDOMapping=0
            """;

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.ObjectDictionary.Objects.Should().ContainKey(0x2000);
        result.Model.ObjectDictionary.Objects[0x2000].ParameterName.Should().Be("Listed");
        result.Model.AdditionalSections.Should().NotContainKey("2000");
        result.Diagnostics.Should().NotContain(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.IniUnlistedObjectSection);
    }

    [Fact]
    public void ReadString_IndexPastSupportedObjectsCount_PreservedAsUnlistedSection()
    {
        // Arrange — a numbered entry above SupportedObjects is not part of the list.
        var content = Eds("""
            [ManufacturerObjects]
            SupportedObjects=0
            1=0x2000

            [2000]
            ParameterName=PastCount
            ObjectType=0x7
            """);

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.ObjectDictionary.ManufacturerObjects.Should().BeEmpty();
        result.Model.ObjectDictionary.Objects.Should().NotContainKey(0x2000);
        result.Model.AdditionalSections["2000"]["ParameterName"].Should().Be("PastCount");
        result.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.IniUnlistedObjectSection &&
            diagnostic.Message == "object section 0x2000 not listed in any object list");
    }

    [Theory]
    [InlineData("0", "0x0000")]
    [InlineData("FFFF", "0xFFFF")]
    public void ReadString_UnlistedObjectIndexAtMinAndMaxValue_PreservesSectionAndReportsDiagnostic(
        string sectionName,
        string indexText)
    {
        // Arrange
        var content = Eds($"""
            [{sectionName}]
            ParameterName=Boundary
            """);

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        ushort.TryParse(sectionName, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var index)
            .Should().BeTrue();
        result.Model.AdditionalSections.Should().ContainKey(sectionName);
        result.Model.AdditionalSections[sectionName]["ParameterName"].Should().Be("Boundary");
        result.Model.ObjectDictionary.Objects.Should().NotContainKey(index);
        result.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.IniUnlistedObjectSection &&
            diagnostic.Message == "object section " + indexText + " not listed in any object list");
    }

    [Fact]
    public void ReadString_HexNameBeyondUInt16_PreservedWithoutObjectDiagnostic()
    {
        // Arrange — 0x10000 does not fit in a CANopen object index.
        var content = Eds("""
            [10000]
            ParameterName=TooWide
            """);

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.AdditionalSections.Should().ContainKey("10000");
        result.Model.AdditionalSections["10000"]["ParameterName"].Should().Be("TooWide");
        result.Diagnostics.Should().NotContain(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.IniUnlistedObjectSection);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteToString_UnlistedSections_RoundTripsPreservedEntries(bool validated)
    {
        // Arrange
        var content = Eds("""
            [2000]
            ParameterName=Hidden
            ObjectType=0x7
            DataType=0x0005
            AccessType=ro
            DefaultValue=1
            PDOMapping=0

            [Face]
            VendorKey=kept

            [Bad]
            Also=kept
            """);
        var read = CanOpenFile.Eds.ReadStringWithDiagnostics(content);
        var options = validated ? CanOpenWriteOptions.Validated : null;

        // Act
        var written = CanOpenFile.Eds.WriteToString(read.Model, options);
        var again = CanOpenFile.Eds.ReadStringWithDiagnostics(written);

        // Assert
        again.Model.ObjectDictionary.Objects.Should().NotContainKey(0x2000);
        again.Model.ObjectDictionary.Objects.Should().NotContainKey(0xFACE);
        again.Model.ObjectDictionary.Objects.Should().NotContainKey(0x0BAD);
        again.Model.ObjectDictionary.Objects.Should().ContainKey(0x1000);
        again.Model.AdditionalSections["2000"]["ParameterName"].Should().Be("Hidden");
        again.Model.AdditionalSections["2000"]["DefaultValue"].Should().Be("1");
        again.Model.AdditionalSections["Face"]["VendorKey"].Should().Be("kept");
        again.Model.AdditionalSections["Bad"]["Also"].Should().Be("kept");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteToString_DcfUnlistedSections_RoundTripsPreservedEntries(bool validated)
    {
        // Arrange
        var content = Dcf("""
            [2000]
            ParameterName=Hidden
            ParameterValue=42
            ObjectType=0x7

            [Face]
            VendorKey=kept
            """);
        var read = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);
        var options = validated ? CanOpenWriteOptions.Validated : null;

        // Act
        var written = CanOpenFile.Dcf.WriteToString(read.Model, options);
        var again = CanOpenFile.Dcf.ReadStringWithDiagnostics(written);

        // Assert
        read.Model.ObjectDictionary.Objects.Should().NotContainKey(0x2000);
        read.Diagnostics.Should().Contain(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.IniUnlistedObjectSection &&
            diagnostic.Message == "object section 0x2000 not listed in any object list");
        again.Model.ObjectDictionary.Objects.Should().NotContainKey(0x2000);
        again.Model.ObjectDictionary.Objects.Should().NotContainKey(0xFACE);
        again.Model.AdditionalSections["2000"]["ParameterName"].Should().Be("Hidden");
        again.Model.AdditionalSections["2000"]["ParameterValue"].Should().Be("42");
        again.Model.AdditionalSections["Face"]["VendorKey"].Should().Be("kept");
    }

    [Fact]
    public void ReadString_CpjHexSection_PreservedWithoutObjectDiagnostic()
    {
        // Arrange — CPJ has no object lists; a hex section is an ordinary extra section.
        var content = """
            [Topology]
            NetName=Line

            [2000]
            ParameterName=Hidden

            [Face]
            VendorKey=kept
            """;

        // Act
        var result = CanOpenFile.Cpj.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.AdditionalSections.Should().ContainKey("2000");
        result.Model.AdditionalSections["2000"]["ParameterName"].Should().Be("Hidden");
        result.Model.AdditionalSections.Should().ContainKey("Face");
        result.Model.AdditionalSections["Face"]["VendorKey"].Should().Be("kept");
        result.Diagnostics.Should().NotContain(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.IniUnlistedObjectSection);
    }

    private static string Eds(string extraSections)
    {
        return """
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

            """ + extraSections;
    }

    private static string Dcf(string extraSections)
    {
        return """
            [DeviceInfo]
            VendorName=Test
            ProductName=Test

            [DeviceCommissioning]
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

            """ + extraSections;
    }
}
