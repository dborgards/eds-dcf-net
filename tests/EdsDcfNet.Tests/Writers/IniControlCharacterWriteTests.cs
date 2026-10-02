namespace EdsDcfNet.Tests.Writers;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Tests.Utilities;
using EdsDcfNet.Validation;
using EdsDcfNet.Writers;

/// <summary>
/// INI text that cannot survive <c>IniParser</c> (CiA 306-1 §6.2 trims the line, the section
/// name, the key and the value, and treats line breaks as record separators) must be rejected
/// on write. XML writes escape the same characters and are not subject to those rules.
/// </summary>
public class IniControlCharacterWriteTests
{
    private const string RoundTripMarker = "round-trip";

    public static IEnumerable<object[]> ControlCharacterPositions()
    {
        for (var codePoint = 0; codePoint <= 0x1F; codePoint++)
        {
            yield return new object[] { codePoint, "start" };
            yield return new object[] { codePoint, "middle" };
            yield return new object[] { codePoint, "end" };
        }

        yield return new object[] { 0x7F, "start" };
        yield return new object[] { 0x7F, "middle" };
        yield return new object[] { 0x7F, "end" };
    }

    [Theory]
    [MemberData(nameof(ControlCharacterPositions))]
    public void WriteToString_ControlCharacterInParameterName_ThrowsOrRoundTrips(int codePoint, string position)
    {
        var text = Embed((char)codePoint, position);
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.ObjectDictionary.Objects[0x1000].ParameterName = text;

        AssertThrowsOrRoundTrips(
            () => CanOpenFile.Eds.WriteToString(eds),
            written => CanOpenFile.Eds.ReadString(written).ObjectDictionary.Objects[0x1000].ParameterName,
            text);
    }

    [Theory]
    [MemberData(nameof(ControlCharacterPositions))]
    public void WriteToString_ControlCharacterInAdditionalKey_ThrowsOrRoundTrips(int codePoint, string position)
    {
        var text = Embed((char)codePoint, position);
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.AdditionalSections["Vendor"] = new Dictionary<string, string> { [text] = "kept" };

        AssertThrowsOrRoundTrips(
            () => CanOpenFile.Eds.WriteToString(eds),
            written => CanOpenFile.Eds.ReadString(written).AdditionalSections["Vendor"].Keys.Single(),
            text);
    }

    [Theory]
    [MemberData(nameof(ControlCharacterPositions))]
    public void WriteToString_ControlCharacterInSectionName_ThrowsOrRoundTrips(int codePoint, string position)
    {
        var text = Embed((char)codePoint, position);
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.AdditionalSections[text] = new Dictionary<string, string> { ["Key"] = "kept" };

        AssertThrowsOrRoundTrips(
            () => CanOpenFile.Eds.WriteToString(eds),
            written => CanOpenFile.Eds.ReadString(written).AdditionalSections.Keys.Single(),
            text);
    }

    [Fact]
    public void WriteToString_NewlineInParameterName_ThrowsEdsWriteException()
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.ObjectDictionary.Objects[0x1000].ParameterName = "Evil\n[FileInfo]\nFileName=pwned.eds";

        var act = () => CanOpenFile.Eds.WriteToString(eds);

        var exception = act.Should().Throw<EdsWriteException>().Which;
        exception.Message.Should().Contain(RoundTripMarker);
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void WriteToString_ValidatedNewlineInParameterName_ThrowsModelValidationException()
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.ObjectDictionary.Objects[0x1000].ParameterName = "Evil\n[FileInfo]\nFileName=pwned.eds";

        var act = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        var exception = act.Should().Throw<ModelValidationException>().Which;
        exception.Issues.Should().Contain(issue =>
            issue.Path == "ObjectDictionary.Objects[0x1000].ParameterName" &&
            issue.Code == ValidationIssueCodes.IniTextNotRoundTrippable);
    }

    [Fact]
    public async Task WriteStreamAsync_ValidatedNewlineInParameterName_ThrowsModelValidationException()
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.ObjectDictionary.Objects[0x1000].ParameterName = "Line\nbreak";
        using var stream = new MemoryStream();

        var act = () => CanOpenFile.Eds.WriteStreamAsync(eds, stream, CanOpenWriteOptions.Validated);

        (await act.Should().ThrowAsync<ModelValidationException>()).Which.Issues.Should().Contain(issue =>
            issue.Code == ValidationIssueCodes.IniTextNotRoundTrippable);
    }

    [Fact]
    public void EnsureValid_NewlineInParameterName_DoesNotApplyIniWriteRules()
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.ObjectDictionary.Objects[0x1000].ParameterName = "Line\nbreak";

        var act = () => CanOpenFile.EnsureValid(eds);

        act.Should().NotThrow();
    }

    [Fact]
    public void WriteToString_ValidatedXddNewlineInParameterName_RoundTrips()
    {
        const string parameterName = "Evil\n[FileInfo]\nFileName=pwned.eds";
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.ObjectDictionary.Objects[0x1000].ParameterName = parameterName;

        var written = CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);
        var read = CanOpenFile.Xdd.ReadString(written);

        read.ObjectDictionary.Objects[0x1000].ParameterName.Should().Be(parameterName);
    }

    [Fact]
    public void WriteToString_UnvalidatedXddNewlineInParameterName_RoundTrips()
    {
        const string parameterName = "Evil\n[FileInfo]\nFileName=pwned.eds";
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.ObjectDictionary.Objects[0x1000].ParameterName = parameterName;

        var written = CanOpenFile.Xdd.WriteToString(eds);
        var read = CanOpenFile.Xdd.ReadString(written);

        read.ObjectDictionary.Objects[0x1000].ParameterName.Should().Be(parameterName);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("middle")]
    [InlineData("end")]
    public void WriteToString_TabInParameterName_RejectsEdgesAndRoundTripsInterior(string position)
    {
        var text = Embed('\t', position);
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.ObjectDictionary.Objects[0x1000].ParameterName = text;

        if (position == "middle")
        {
            var written = CanOpenFile.Eds.WriteToString(eds);
            CanOpenFile.Eds.ReadString(written).ObjectDictionary.Objects[0x1000].ParameterName.Should().Be(text);
            return;
        }

        var act = () => CanOpenFile.Eds.WriteToString(eds);
        act.Should().Throw<EdsWriteException>().Which.Message.Should().Contain(RoundTripMarker);
    }

    [Theory]
    [InlineData(" name")]
    [InlineData("name ")]
    [InlineData("\tname")]
    [InlineData("name\t")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void WriteToString_EdgeWhitespaceInParameterName_ThrowsEdsWriteException(string parameterName)
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.ObjectDictionary.Objects[0x1000].ParameterName = parameterName;

        var act = () => CanOpenFile.Eds.WriteToString(eds);

        act.Should().Throw<EdsWriteException>().Which.Message.Should().Contain(RoundTripMarker);
    }

    [Fact]
    public void WriteToString_InteriorTabInValue_RoundTrips()
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.ObjectDictionary.Objects[0x1000].ParameterName = "Devi\tce";

        var written = CanOpenFile.Eds.WriteToString(eds);

        CanOpenFile.Eds.ReadString(written).ObjectDictionary.Objects[0x1000].ParameterName.Should().Be("Devi\tce");
    }

    [Theory]
    [InlineData("a=b")]
    [InlineData("a[b")]
    [InlineData("a]b")]
    [InlineData(";comment")]
    [InlineData("a\tb")]
    public void WriteToString_IllegalAdditionalKey_ThrowsEdsWriteException(string key)
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.AdditionalSections["Vendor"] = new Dictionary<string, string> { [key] = "kept" };

        var act = () => CanOpenFile.Eds.WriteToString(eds);

        act.Should().Throw<EdsWriteException>().Which.Message.Should().Contain(RoundTripMarker);
    }

    [Fact]
    public void WriteToString_EqualsAndSemicolonInsideValue_RoundTrip()
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.ObjectDictionary.Objects[0x1000].ParameterName = "a=b; c";

        var written = CanOpenFile.Eds.WriteToString(eds);

        CanOpenFile.Eds.ReadString(written).ObjectDictionary.Objects[0x1000].ParameterName.Should().Be("a=b; c");
    }

    [Theory]
    [InlineData("Foo]Bar")]
    [InlineData("Foo\nBar")]
    [InlineData("Foo\tBar")]
    [InlineData(" Foo")]
    [InlineData("Foo ")]
    public void WriteToString_IllegalSectionName_ThrowsEdsWriteException(string sectionName)
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.AdditionalSections[sectionName] = new Dictionary<string, string> { ["Key"] = "kept" };

        var act = () => CanOpenFile.Eds.WriteToString(eds);

        act.Should().Throw<EdsWriteException>().Which.Message.Should().Contain(RoundTripMarker);
    }

    [Fact]
    public void WriteToString_NewlineInNodeName_ThrowsDcfWriteException()
    {
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        dcf.DeviceCommissioning.NodeName = "Node\nTwo";

        var act = () => CanOpenFile.Dcf.WriteToString(dcf);

        var exception = act.Should().Throw<DcfWriteException>().Which;
        exception.Message.Should().Contain(RoundTripMarker);
        exception.Message.Should().NotContain("Failed to write section");
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void WriteToString_ValidatedNewlineInNodeName_ThrowsModelValidationException()
    {
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        dcf.DeviceCommissioning.NodeName = "Node\nTwo";

        var act = () => CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);

        act.Should().Throw<ModelValidationException>().Which.Issues.Should().Contain(issue =>
            issue.Path == "DeviceCommissioning.NodeName" &&
            issue.Code == ValidationIssueCodes.IniTextNotRoundTrippable);
    }

    [Theory]
    [InlineData("NetName")]
    [InlineData("NetRefd")]
    [InlineData("NodeName")]
    [InlineData("NodeRefd")]
    [InlineData("NodeDcfName")]
    [InlineData("EdsBaseName")]
    [InlineData("AdditionalSection")]
    [InlineData("AdditionalKey")]
    [InlineData("AdditionalValue")]
    public void WriteToString_NewlineInCpjField_ThrowsCpjWriteException(string field)
    {
        var cpj = CpjWithNewline(field);

        var act = () => CanOpenFile.Cpj.WriteToString(cpj);

        var exception = act.Should().Throw<CpjWriteException>().Which;
        exception.Message.Should().Contain(RoundTripMarker);
        exception.Message.Should().NotContain("Failed to write section");
        exception.InnerException.Should().BeNull();
    }

    [Theory]
    [InlineData("NetName")]
    [InlineData("NetRefd")]
    [InlineData("NodeName")]
    [InlineData("NodeRefd")]
    [InlineData("NodeDcfName")]
    [InlineData("EdsBaseName")]
    [InlineData("AdditionalSection")]
    [InlineData("AdditionalKey")]
    [InlineData("AdditionalValue")]
    public void CpjWriter_NewlineInCpjField_ThrowsCpjWriteException(string field)
    {
        var cpj = CpjWithNewline(field);

        var act = () => new CpjWriter().GenerateString(cpj);

        act.Should().Throw<CpjWriteException>().Which.Message.Should().Contain(RoundTripMarker);
    }

    [Fact]
    public void WriteToString_ValidatedNewlineInNetName_ThrowsModelValidationException()
    {
        var cpj = CpjWithNewline("NetName");

        var act = () => CanOpenFile.Cpj.WriteToString(cpj, CanOpenWriteOptions.Validated);

        act.Should().Throw<ModelValidationException>().Which.Issues.Should().Contain(issue =>
            issue.Path == "Networks[0].NetName" &&
            issue.Code == ValidationIssueCodes.IniTextNotRoundTrippable);
    }

    private static void AssertThrowsOrRoundTrips(
        Func<string> write,
        Func<string, string> readBack,
        string expected)
    {
        string written;
        try
        {
            written = write();
        }
        catch (EdsWriteException)
        {
            return;
        }

        readBack(written).Should().Be(expected);
    }

    private static string Embed(char character, string position) => position switch
    {
        "start" => character + "core",
        "middle" => "co" + character + "re",
        "end" => "core" + character,
        _ => throw new ArgumentOutOfRangeException(nameof(position), position, "Unknown embed position.")
    };

    private static NodelistProject CpjWithNewline(string field)
    {
        var cpj = ValidCanOpenModelBuilder.CreateValidCpj();
        var network = cpj.Networks[0];
        var node = network.Nodes[2];
        switch (field)
        {
            case "NetName":
                network.NetName = "Net\nWork";
                break;
            case "NetRefd":
                network.NetRefd = "Ref\nD";
                break;
            case "NodeName":
                node.Name = "Node\nName";
                break;
            case "NodeRefd":
                node.Refd = "Node\nRef";
                break;
            case "NodeDcfName":
                node.DcfFileName = "node\n.dcf";
                break;
            case "EdsBaseName":
                network.EdsBaseName = "base\n.eds";
                break;
            case "AdditionalSection":
                cpj.AdditionalSections["Sec\ntion"] = new Dictionary<string, string> { ["Key"] = "kept" };
                break;
            case "AdditionalKey":
                cpj.AdditionalSections["Vendor"] = new Dictionary<string, string> { ["Ke\ny"] = "kept" };
                break;
            case "AdditionalValue":
                cpj.AdditionalSections["Vendor"] = new Dictionary<string, string> { ["Key"] = "val\nue" };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown CPJ field.");
        }

        return cpj;
    }
}
