namespace EdsDcfNet.Tests.Writers;

using System.Xml.Linq;
using EdsDcfNet.Tests.Infrastructure;

/// <summary>
/// CiA 311 Annex A.1.3: <c>enum</c> is <c>g_labels?, enumValue+, g_simple?</c> and
/// <c>parameterGroup</c> is <c>g_labels, parameterGroup*, parameterRef*</c>.
/// </summary>
public class XddApplicationProcessElementOrderTests
{
    private const string SampleXdd = "Fixtures/sample_device.xdd";

    [Fact]
    public void WriteToString_EnumWithSimpleType_WritesEnumValuesBeforeTheSimpleType()
    {
        // Act
        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(CanOpenFile.Xdd.ReadFile(SampleXdd)));

        // Assert
        written.Descendants().Single(e => e.Name.LocalName == "enum").Elements().Select(e => e.Name.LocalName)
            .Should().Equal("label", "enumValue", "enumValue", "enumValue", "USINT");
    }

    [Fact]
    public void WriteToString_NestedParameterGroup_WritesNestedGroupsBeforeParameterRefs()
    {
        // Act
        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(CanOpenFile.Xdd.ReadFile(SampleXdd)));

        // Assert
        var outer = written.Descendants().Single(e => e.Name.LocalName == "parameterGroup"
            && (string?)e.Attribute("uniqueID") == "uid_pg_config");
        outer.Elements().Select(e => e.Name.LocalName).Should().Equal("label", "parameterGroup", "parameterRef");
    }

    [Fact]
    public void WriteToString_SampleXddWithEnumAndNestedGroups_ValidatesAgainstSchema()
    {
        // Act
        var problems = Cia311Schema.Validate(CanOpenFile.Xdd.WriteToString(CanOpenFile.Xdd.ReadFile(SampleXdd)));

        // Assert
        problems.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_WrittenEnumAndNestedGroups_RoundTripsTheModel()
    {
        // Arrange
        var source = CanOpenFile.Xdd.ReadFile(SampleXdd);

        // Act
        var reread = CanOpenFile.Xdd.ReadString(CanOpenFile.Xdd.WriteToString(source));

        // Assert
        var enumType = reread.ApplicationProcess!.DataTypeList!.Enums.Single();
        enumType.SimpleTypeName.Should().Be("USINT");
        enumType.EnumValues.Select(v => v.Value).Should().Equal("0", "1", "2");
        var group = reread.ApplicationProcess.ParameterGroupList.Single();
        group.ParameterRefs.Should().Equal("uid_p_mode");
        group.SubGroups.Single().ParameterRefs.Should().Equal("uid_p_status");
    }
}
