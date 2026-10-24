namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;

/// <summary>
/// DCF compact value and denotation lists cannot be applied to a VAR without sub-objects.
/// Their complete original sections therefore belong to <c>AdditionalSections</c>.
/// </summary>
public class CompactListWithoutSubObjectsTests
{
    private const string Content = """
        [DeviceInfo]
        VendorName=Vendor
        ProductName=Device

        [DeviceComissioning]
        NodeID=5
        NodeName=TestNode
        Baudrate=500
        NetNumber=1
        NetworkName=TestNetwork
        CANopenManager=0

        [OptionalObjects]
        SupportedObjects=1
        1=0x2000

        [2000]
        ParameterName=Plain
        ObjectType=0x7
        DataType=0x0005
        AccessType=rw
        DefaultValue=0
        PDOMapping=0

        [2000Value]
        NrOfEntries=2
        1=7
        2=FutureValue
        VendorKey=kept

        [2000Denotation]
        NrOfEntries=1
        1=OriginalDenotation

        """;

    [Fact]
    public void ReadString_CompactListsOfVarWithoutSubObjects_PreservesCompleteSections()
    {
        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(Content);

        result.Model.ObjectDictionary.Objects[0x2000].SubObjects.Should().BeEmpty();
        result.Model.AdditionalSections["2000Value"].Should().Equal(new Dictionary<string, string>
        {
            ["NrOfEntries"] = "2",
            ["1"] = "7",
            ["2"] = "FutureValue",
            ["VendorKey"] = "kept"
        });
        result.Model.AdditionalSections["2000Denotation"].Should().Equal(new Dictionary<string, string>
        {
            ["NrOfEntries"] = "1",
            ["1"] = "OriginalDenotation"
        });
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniOrphanCompanionSection)
            .Which.Path.Should().Be("2000Value");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteToString_CompactListsOfVarWithoutSubObjects_RoundTripsOriginalCount(bool validated)
    {
        var original = CanOpenFile.Dcf.ReadString(Content);
        var options = validated ? CanOpenWriteOptions.Validated : null;

        var written = CanOpenFile.Dcf.WriteToString(original, options);
        var again = CanOpenFile.Dcf.ReadString(written);

        written.Should().NotContain("NrOfEntries=0");
        again.AdditionalSections["2000Value"].Should().Equal(original.AdditionalSections["2000Value"]);
        again.AdditionalSections["2000Denotation"].Should().Equal(original.AdditionalSections["2000Denotation"]);
        again.ObjectDictionary.Objects[0x2000].SubObjects.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_CompactListOfVarWithoutSubObjects_StrictParsingThrows()
    {
        var act = () => CanOpenFile.Dcf.ReadString(Content, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>().Which.Should().Match<EdsParseException>(error =>
            error.Code == ParseDiagnosticCodes.IniOrphanCompanionSection && error.SectionName == "2000Value");
    }

    [Fact]
    public void WriteToString_SubObjectsAddedForPreservedCompactLists_GeneratedSectionsWin()
    {
        var model = CanOpenFile.Dcf.ReadString(Content);
        var obj = model.ObjectDictionary.Objects[0x2000];
        obj.ObjectType = CanOpenObjectType.Array;
        obj.CompactSubObj = 1;
        obj.SubObjects[0] = new CanOpenSubObject
        {
            SubIndex = 0,
            ParameterName = "Count",
            DataType = 0x0005,
            DefaultValue = "1"
        };
        obj.SubObjects[1] = new CanOpenSubObject
        {
            SubIndex = 1,
            ParameterName = "Generated",
            DataType = 0x0005,
            AccessType = AccessType.ReadWrite,
            DefaultValue = "0",
            ParameterValue = "9",
            Denotation = "GeneratedDenotation"
        };

        var written = CanOpenFile.Dcf.WriteToString(model);
        var again = CanOpenFile.Dcf.ReadString(written);
        var subObject = again.ObjectDictionary.Objects[0x2000].SubObjects[1];

        subObject.ParameterValue.Should().Be("9");
        subObject.Denotation.Should().Be("GeneratedDenotation");
        again.AdditionalSections.Should().NotContainKeys("2000Value", "2000Denotation");
        written.Should().NotContain("FutureValue", "OriginalDenotation");
    }
}
