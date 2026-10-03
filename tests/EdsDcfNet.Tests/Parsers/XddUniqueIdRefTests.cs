namespace EdsDcfNet.Tests.Parsers;

using System.Text;
using System.Xml.Linq;
using EdsDcfNet;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using AwesomeAssertions;
using Xunit;

/// <summary>
/// WP-02: <c>uniqueIDRef</c> on <c>CANopenObject</c> / <c>CANopenSubObject</c> is resolved
/// against the application-process <c>parameter</c> and kept for a later XDD/XDC write.
/// </summary>
public class XddUniqueIdRefTests
{
    private const string IndirectMessage = "variableRef/templateIDRef";

    private static readonly CanOpenFileOptions Strict = new() { StrictParsing = true };

    [Fact]
    public void ReadString_UniqueIdRefOnObject_FillsDataTypeAccessDefaultAndKeepsReference()
    {
        var xml = BuildXdd(
            Parameter("P1", "readWrite", "<UDINT/><defaultValue value=\"42\"/>", "<label lang=\"en\">Vendor</label>"),
            Object("2000", "Vendor", "7", "uniqueIDRef=\"P1\" PDOmapping=\"no\""));

        var model = CanOpenFile.Xdd.ReadString(xml);
        var obj = model.ObjectDictionary.Objects[0x2000];

        obj.UniqueIdRef.Should().Be("P1");
        obj.DataType.Should().Be(CanOpenDataType.Unsigned32);
        obj.AccessType.Should().Be(AccessType.ReadWrite);
        obj.DefaultValue.Should().Be("42");
        obj.ParameterName.Should().Be("Vendor");
    }

    [Fact]
    public void ReadFile_BasicDevice1018Sub1_ResolvesUnsigned32ReadOnly()
    {
        var path = Path.Combine("Fixtures", "Corpus", "canopen-node", "basicDevice.xdd");
        var model = CanOpenFile.Xdd.ReadFile(path);
        var sub = model.ObjectDictionary.Objects[0x1018].SubObjects[0x01];

        sub.UniqueIdRef.Should().Be("UID_SUB_101801");
        sub.DataType.Should().Be(CanOpenDataType.Unsigned32);
        sub.AccessType.Should().Be(AccessType.ReadOnly);
        sub.DefaultValue.Should().Be("0x00000000");
        sub.ParameterName.Should().Be("Vendor-ID");
    }

    [Fact]
    public void WriteToString_BasicDevice_HasNoZeroDataTypeForReferencedObjects()
    {
        var path = Path.Combine("Fixtures", "Corpus", "canopen-node", "basicDevice.xdd");
        var model = CanOpenFile.Xdd.ReadFile(path);

        var eds = CanOpenFile.Eds.WriteToString(model);

        eds.Should().NotContain("DataType=0x0\n", "referenced objects must not fall back to data type 0");
        eds.Should().Contain("[1018sub1]");
        var section = Section(eds, "1018sub1");
        section.Should().Contain("DataType=0x7");
        section.Should().Contain("AccessType=ro");
        section.Should().Contain("DefaultValue=0x00000000");
    }

    [Theory]
    [InlineData("const", AccessType.Constant)]
    [InlineData("read", AccessType.ReadOnly)]
    [InlineData("write", AccessType.WriteOnly)]
    [InlineData("readWrite", AccessType.ReadWrite)]
    [InlineData("readWriteInput", AccessType.ReadWriteInput)]
    [InlineData("readWriteOutput", AccessType.ReadWriteOutput)]
    public void ReadString_ParameterAccess_MapsAllSevenSchemaValues(string access, AccessType expected)
    {
        var xml = BuildXdd(
            Parameter("P1", access, "<UINT/>"),
            Object("2000", "P", "7", "uniqueIDRef=\"P1\""));

        var obj = CanOpenFile.Xdd.ReadString(xml).ObjectDictionary.Objects[0x2000];

        obj.AccessType.Should().Be(expected);
        obj.DataType.Should().Be(CanOpenDataType.Unsigned16);
    }

    [Fact]
    public void ReadString_NoAccess_LeavesAccessTypeAndReportsDiagnosticInStrictMode()
    {
        var xml = BuildXdd(
            Parameter("P1", "noAccess", "<UINT/>"),
            Object("2000", "P", "7", "uniqueIDRef=\"P1\" accessType=\"wo\""));

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml, Strict);

        result.Model.ObjectDictionary.Objects[0x2000].AccessType.Should().Be(AccessType.WriteOnly);
        result.Model.ObjectDictionary.Objects[0x2000].DataType.Should().Be(CanOpenDataType.Unsigned16);
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.XddUniqueIdRefNoAccess);
    }

    [Fact]
    public void ReadString_NoAccessWithoutObjectAccess_DoesNotOverwriteDefaultAccess()
    {
        var xml = BuildXdd(
            Parameter("P1", "noAccess", "<UINT/>"),
            Object("2000", "P", "7", "uniqueIDRef=\"P1\""));

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml, Strict);

        result.Model.ObjectDictionary.Objects[0x2000].AccessType.Should().Be(AccessType.ReadOnly);
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.XddUniqueIdRefNoAccess);
    }

    [Fact]
    public void ReadString_VariableRef_LeavesDataTypeUnsetAndReportsDiagnosticInStrictMode()
    {
        var parameter = @"<parameter uniqueID=""P1"" access=""read"">
            <label lang=""en"">Indirect</label>
            <variableRef>
              <instanceIDRef uniqueIDRef=""INST""/>
              <variableIDRef uniqueIDRef=""VAR""/>
            </variableRef>
          </parameter>";
        var xml = BuildXdd(parameter, Object("2000", "Indirect", "7", "uniqueIDRef=\"P1\""));

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml, Strict);
        var obj = result.Model.ObjectDictionary.Objects[0x2000];

        obj.DataType.Should().BeNull();
        obj.AccessType.Should().Be(AccessType.ReadOnly);
        obj.UniqueIdRef.Should().Be("P1");
        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == ParseDiagnosticCodes.XddUniqueIdRefIndirect &&
            d.RawValue == "variableRef" &&
            d.Message.Contains(IndirectMessage));
    }

    [Fact]
    public void ReadString_TemplateIdRef_LeavesDefaultAndLimitsUnsetAndReportsDiagnosticInStrictMode()
    {
        var xml = BuildXdd(
            Parameter("P1", "read", "<UDINT/>", templateIdRef: "TPL1"),
            Object("2000", "P", "7", "uniqueIDRef=\"P1\""));

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml, Strict);
        var obj = result.Model.ObjectDictionary.Objects[0x2000];

        obj.DataType.Should().Be(CanOpenDataType.Unsigned32);
        obj.DefaultValue.Should().BeNull();
        obj.LowLimit.Should().BeNull();
        obj.HighLimit.Should().BeNull();
        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == ParseDiagnosticCodes.XddUniqueIdRefIndirect &&
            d.RawValue == "templateIDRef" &&
            d.Message.Contains(IndirectMessage));
    }

    [Fact]
    public void ReadString_ExplicitAttributes_WinOverReference()
    {
        var body = Parameter(
            "P1",
            "readWrite",
            "<UDINT/><defaultValue value=\"0\"/><allowedValues><range><minValue value=\"0\"/><maxValue value=\"10\"/></range></allowedValues>",
            "<label lang=\"en\">FromLabel</label>");
        var xml = BuildXdd(
            body,
            Object(
                "2000",
                "Explicit",
                "7",
                "dataType=\"0005\" accessType=\"wo\" defaultValue=\"1\" lowLimit=\"2\" highLimit=\"3\" uniqueIDRef=\"P1\""));

        var obj = CanOpenFile.Xdd.ReadString(xml).ObjectDictionary.Objects[0x2000];

        obj.ParameterName.Should().Be("Explicit");
        obj.DataType.Should().Be(CanOpenDataType.Unsigned8);
        obj.AccessType.Should().Be(AccessType.WriteOnly);
        obj.DefaultValue.Should().Be("1");
        obj.LowLimit.Should().Be("2");
        obj.HighLimit.Should().Be("3");
        obj.UniqueIdRef.Should().Be("P1");
    }

    [Fact]
    public void ReadString_MissingName_UsesEnglishLabel()
    {
        var xml = BuildXdd(
            Parameter("P1", "read", "<USINT/>", "<label lang=\"de\">Kennung</label><label lang=\"en\">Vendor</label>"),
            "<CANopenObject index=\"2000\" objectType=\"7\" uniqueIDRef=\"P1\"/>");

        var obj = CanOpenFile.Xdd.ReadString(xml).ObjectDictionary.Objects[0x2000];

        obj.ParameterName.Should().Be("Vendor");
    }

    [Fact]
    public void ReadString_SingleAllowedRange_FillsLowAndHighLimit()
    {
        var values = "<UINT/><allowedValues><range><minValue value=\"1\"/><maxValue value=\"9\"/><step value=\"1\"/></range></allowedValues>";
        var xml = BuildXdd(
            Parameter("P1", "read", values),
            Object("2000", "P", "7", "uniqueIDRef=\"P1\""));

        var obj = CanOpenFile.Xdd.ReadString(xml).ObjectDictionary.Objects[0x2000];

        obj.LowLimit.Should().Be("1");
        obj.HighLimit.Should().Be("9");
    }

    [Theory]
    [InlineData("<value value=\"1\"/><value value=\"2\"/>")]
    [InlineData("<range><minValue value=\"0\"/><maxValue value=\"1\"/></range><range><minValue value=\"5\"/><maxValue value=\"6\"/></range>")]
    [InlineData("<value value=\"1\"/><range><minValue value=\"0\"/><maxValue value=\"1\"/></range>")]
    public void ReadString_AmbiguousAllowedValues_DoesNotSetLimits(string inner)
    {
        var xml = BuildXdd(
            Parameter("P1", "read", "<UINT/><allowedValues>" + inner + "</allowedValues>"),
            Object("2000", "P", "7", "uniqueIDRef=\"P1\""));

        var obj = CanOpenFile.Xdd.ReadString(xml).ObjectDictionary.Objects[0x2000];

        obj.LowLimit.Should().BeNull();
        obj.HighLimit.Should().BeNull();
        obj.DataType.Should().Be(CanOpenDataType.Unsigned16);
    }

    [Fact]
    public void ReadString_UnresolvedUniqueIdRef_LenientDiagnosticStrictException()
    {
        var xml = BuildXdd(
            Parameter("P1", "read", "<UINT/>"),
            Object("2000", "P", "7", "uniqueIDRef=\"MISSING\""));

        var lenient = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml);
        lenient.Model.ObjectDictionary.Objects[0x2000].UniqueIdRef.Should().Be("MISSING");
        lenient.Model.ObjectDictionary.Objects[0x2000].DataType.Should().BeNull();
        lenient.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.XddUnresolvedUniqueIdRef);

        var act = () => CanOpenFile.Xdd.ReadString(xml, Strict);
        act.Should().Throw<EdsParseException>()
            .Which.Code.Should().Be(ParseDiagnosticCodes.XddUnresolvedUniqueIdRef);
    }

    [Fact]
    public void ReadString_DataTypeIdRef_ResolvesArrayDerivedAndEnumAndLeavesStructUnset()
    {
        var types = @"
          <dataTypeList>
            <array name=""Bytes"" uniqueID=""ARR""><subrange lowerLimit=""0"" upperLimit=""4""/><USINT/></array>
            <struct name=""Rec"" uniqueID=""REC""><varDeclaration name=""a"" uniqueID=""M1""><USINT/></varDeclaration></struct>
            <derived name=""Vendor"" uniqueID=""DER""><UDINT/></derived>
            <enum name=""Mode"" uniqueID=""EN""><UINT/><enumValue value=""1""><label lang=""en"">On</label></enumValue></enum>
          </dataTypeList>";
        var parameters = types + @"
          <parameterList>
            <parameter uniqueID=""P_ARR""><dataTypeIDRef uniqueIDRef=""ARR""/></parameter>
            <parameter uniqueID=""P_REC""><dataTypeIDRef uniqueIDRef=""REC""/></parameter>
            <parameter uniqueID=""P_DER""><dataTypeIDRef uniqueIDRef=""DER""/></parameter>
            <parameter uniqueID=""P_EN""><dataTypeIDRef uniqueIDRef=""EN""/></parameter>
          </parameterList>";
        var objects = Object("2000", "Arr", "8", "uniqueIDRef=\"P_ARR\"")
            + Object("2001", "Rec", "9", "uniqueIDRef=\"P_REC\"")
            + Object("2002", "Der", "7", "uniqueIDRef=\"P_DER\"")
            + Object("2003", "En", "7", "uniqueIDRef=\"P_EN\"");

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(BuildXdd(parameters, objects));
        var od = result.Model.ObjectDictionary;

        od.Objects[0x2000].DataType.Should().Be(CanOpenDataType.Unsigned8);
        od.Objects[0x2001].DataType.Should().BeNull();
        od.Objects[0x2002].DataType.Should().Be(CanOpenDataType.Unsigned32);
        od.Objects[0x2003].DataType.Should().Be(CanOpenDataType.Unsigned16);
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_DanglingDataTypeIdRef_LenientDiagnosticStrictException()
    {
        var xml = BuildXdd(
            Parameter("P1", "read", "<dataTypeIDRef uniqueIDRef=\"MISSING\"/>"),
            Object("2000", "P", "7", "uniqueIDRef=\"P1\""));

        var lenient = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml);
        lenient.Model.ObjectDictionary.Objects[0x2000].DataType.Should().BeNull();
        lenient.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.XddUnresolvedDataTypeIdRef);

        var act = () => CanOpenFile.Xdd.ReadString(xml, Strict);
        act.Should().Throw<EdsParseException>()
            .Which.Code.Should().Be(ParseDiagnosticCodes.XddUnresolvedDataTypeIdRef);
    }

    [Theory]
    [InlineData("BOOL", CanOpenDataType.Boolean)]
    [InlineData("SINT", CanOpenDataType.Integer8)]
    [InlineData("INT", CanOpenDataType.Integer16)]
    [InlineData("DINT", CanOpenDataType.Integer32)]
    [InlineData("LINT", CanOpenDataType.Integer64)]
    [InlineData("USINT", CanOpenDataType.Unsigned8)]
    [InlineData("UINT", CanOpenDataType.Unsigned16)]
    [InlineData("UDINT", CanOpenDataType.Unsigned32)]
    [InlineData("ULINT", CanOpenDataType.Unsigned64)]
    [InlineData("REAL", CanOpenDataType.Real32)]
    [InlineData("LREAL", CanOpenDataType.Real64)]
    [InlineData("STRING", CanOpenDataType.VisibleString)]
    [InlineData("WSTRING", CanOpenDataType.UnicodeString)]
    [InlineData("CHAR", CanOpenDataType.VisibleString)]
    [InlineData("WCHAR", CanOpenDataType.UnicodeString)]
    [InlineData("BYTE", CanOpenDataType.Unsigned8)]
    [InlineData("WORD", CanOpenDataType.Unsigned16)]
    [InlineData("DWORD", CanOpenDataType.Unsigned32)]
    [InlineData("LWORD", CanOpenDataType.Unsigned64)]
    [InlineData("BITSTRING", CanOpenDataType.OctetString)]
    [InlineData("TIME", CanOpenDataType.TimeDifference)]
    [InlineData("TIME_OF_DAY", CanOpenDataType.TimeOfDay)]
    public void ReadString_SimpleType_MapsToCanOpenDataType(string iecType, ushort expected)
    {
        var xml = BuildXdd(
            Parameter("P1", "const", "<" + iecType + "/>"),
            Object("2000", "P", "7", "uniqueIDRef=\"P1\""));

        CanOpenFile.Xdd.ReadString(xml).ObjectDictionary.Objects[0x2000].DataType.Should().Be(expected);
    }

    [Fact]
    public void ReadString_SubObjectUniqueIdRef_FillsSubObject()
    {
        var parameters = Parameter("P_OBJ", "read", "<dataTypeIDRef uniqueIDRef=\"REC\"/>")
            + Parameter("P_SUB", "readWrite", "<UDINT/><defaultValue value=\"7\"/>");
        var types = @"<dataTypeList><struct name=""Rec"" uniqueID=""REC"">
            <varDeclaration name=""a"" uniqueID=""M1""><UDINT/></varDeclaration>
          </struct></dataTypeList>";
        var obj = @"<CANopenObject index=""2000"" name=""Rec"" objectType=""9"" uniqueIDRef=""P_OBJ"" subNumber=""1"">
            <CANopenSubObject subIndex=""01"" name=""Field"" objectType=""7"" uniqueIDRef=""P_SUB""/>
          </CANopenObject>";
        var model = CanOpenFile.Xdd.ReadString(BuildXdd(types + "<parameterList>" + parameters + "</parameterList>", obj));
        var sub = model.ObjectDictionary.Objects[0x2000].SubObjects[0x01];

        sub.UniqueIdRef.Should().Be("P_SUB");
        sub.DataType.Should().Be(CanOpenDataType.Unsigned32);
        sub.AccessType.Should().Be(AccessType.ReadWrite);
        sub.DefaultValue.Should().Be("7");
        model.ObjectDictionary.Objects[0x2000].DataType.Should().BeNull();
    }

    [Fact]
    public void WriteString_RoundTrip_KeepsReferenceAndResolvedValues()
    {
        var xml = BuildXdd(
            Parameter("P1", "readWriteInput", "<UDINT/><defaultValue value=\"5\"/>"),
            Object("2000", "P", "7", "uniqueIDRef=\"P1\" PDOmapping=\"optional\""));

        var first = CanOpenFile.Xdd.ReadString(xml);
        var written = CanOpenFile.Xdd.WriteToString(first);
        var objElem = CanOpenObject(written, "2000");
        objElem.Attribute("uniqueIDRef")!.Value.Should().Be("P1");
        objElem.Attribute("dataType").Should().BeNull();
        objElem.Attribute("accessType").Should().BeNull();
        objElem.Attribute("defaultValue").Should().BeNull();

        var second = CanOpenFile.Xdd.ReadString(written).ObjectDictionary.Objects[0x2000];
        second.UniqueIdRef.Should().Be("P1");
        second.DataType.Should().Be(CanOpenDataType.Unsigned32);
        second.AccessType.Should().Be(AccessType.ReadWriteInput);
        second.DefaultValue.Should().Be("5");
    }

    [Fact]
    public void WriteString_DataTypeChangedAfterRead_ExplicitAttributeWinsOnRoundTrip()
    {
        var xml = BuildXdd(
            Parameter("P1", "read", "<UDINT/>"),
            Object("2000", "P", "7", "uniqueIDRef=\"P1\""));
        var model = CanOpenFile.Xdd.ReadString(xml);
        model.ObjectDictionary.Objects[0x2000].DataType = CanOpenDataType.Unsigned8;

        var written = CanOpenFile.Xdd.WriteToString(model);
        var objElem = CanOpenObject(written, "2000");
        objElem.Attribute("uniqueIDRef")!.Value.Should().Be("P1");
        objElem.Attribute("dataType")!.Value.Should().Be("0005");

        var again = CanOpenFile.Xdd.ReadString(written).ObjectDictionary.Objects[0x2000];
        again.DataType.Should().Be(CanOpenDataType.Unsigned8);
        again.UniqueIdRef.Should().Be("P1");
    }

    [Fact]
    public void WriteString_AccessTypeChangedAfterRead_ExplicitAttributeWinsOnRoundTrip()
    {
        var xml = BuildXdd(
            Parameter("P1", "readWriteInput", "<UINT/>"),
            Object("2000", "P", "7", "uniqueIDRef=\"P1\""));
        var model = CanOpenFile.Xdd.ReadString(xml);
        model.ObjectDictionary.Objects[0x2000].AccessType = AccessType.WriteOnly;

        var written = CanOpenFile.Xdd.WriteToString(model);
        CanOpenObject(written, "2000").Attribute("accessType")!.Value.Should().Be("wo");

        CanOpenFile.Xdd.ReadString(written).ObjectDictionary.Objects[0x2000].AccessType
            .Should().Be(AccessType.WriteOnly);
    }

    [Fact]
    public void WriteString_RecordAccessChangedAfterRead_ExplicitAttributeWins()
    {
        var parameters = Parameter("P_OBJ", "read", "<dataTypeIDRef uniqueIDRef=\"REC\"/>");
        var types = @"<dataTypeList><struct name=""Rec"" uniqueID=""REC"">
            <varDeclaration name=""a"" uniqueID=""M1""><UDINT/></varDeclaration>
          </struct></dataTypeList>";
        var model = CanOpenFile.Xdd.ReadString(BuildXdd(
            types + "<parameterList>" + parameters + "</parameterList>",
            Object("2000", "Rec", "9", "uniqueIDRef=\"P_OBJ\"")));
        model.ObjectDictionary.Objects[0x2000].DataType.Should().BeNull();
        model.ObjectDictionary.Objects[0x2000].AccessType = AccessType.ReadWrite;

        var written = CanOpenFile.Xdd.WriteToString(model);
        var objElem = CanOpenObject(written, "2000");
        objElem.Attribute("uniqueIDRef")!.Value.Should().Be("P_OBJ");
        objElem.Attribute("dataType").Should().BeNull();
        objElem.Attribute("accessType")!.Value.Should().Be("rw");

        CanOpenFile.Xdd.ReadString(written).ObjectDictionary.Objects[0x2000].AccessType
            .Should().Be(AccessType.ReadWrite);
    }

    [Fact]
    public void WriteString_NoAccessWithoutExplicitAccess_DoesNotInventReadOnlyOverride()
    {
        var xml = BuildXdd(
            Parameter("P1", "noAccess", "<UINT/>") + Parameter("P_SUB", "noAccess", "<USINT/>"),
            @"<CANopenObject index=""2000"" name=""P"" objectType=""7"" uniqueIDRef=""P1"">
                <CANopenSubObject subIndex=""01"" name=""S"" objectType=""7"" uniqueIDRef=""P_SUB""/>
              </CANopenObject>");

        var written = CanOpenFile.Xdd.WriteToString(CanOpenFile.Xdd.ReadString(xml));
        var objElem = CanOpenObject(written, "2000");
        var subElem = objElem.Elements().Single(e => e.Name.LocalName == "CANopenSubObject");

        objElem.Attribute("uniqueIDRef")!.Value.Should().Be("P1");
        objElem.Attribute("accessType").Should().BeNull();
        subElem.Attribute("uniqueIDRef")!.Value.Should().Be("P_SUB");
        subElem.Attribute("accessType").Should().BeNull();

        var again = CanOpenFile.Xdd.ReadStringWithDiagnostics(written, Strict);
        again.Model.ObjectDictionary.Objects[0x2000].AccessType.Should().Be(AccessType.ReadOnly);
        again.Model.ObjectDictionary.Objects[0x2000].SubObjects[0x01].AccessType.Should().Be(AccessType.ReadOnly);
        again.Diagnostics.Should().OnlyContain(d => d.Code == ParseDiagnosticCodes.XddUniqueIdRefNoAccess);
        again.Diagnostics.Should().HaveCount(2);
    }

    [Fact]
    public void WriteString_NoAccessThenCallerSetsAccess_WritesExplicitAccess()
    {
        var xml = BuildXdd(
            Parameter("P1", "noAccess", "<UINT/>"),
            Object("2000", "P", "7", "uniqueIDRef=\"P1\""));
        var model = CanOpenFile.Xdd.ReadString(xml);
        model.ObjectDictionary.Objects[0x2000].AccessType = AccessType.WriteOnly;

        var written = CanOpenFile.Xdd.WriteToString(model);
        CanOpenObject(written, "2000").Attribute("accessType")!.Value.Should().Be("wo");
        CanOpenFile.Xdd.ReadString(written).ObjectDictionary.Objects[0x2000].AccessType
            .Should().Be(AccessType.WriteOnly);
    }

    [Fact]
    public void WriteString_RecordReferenceRemoved_KeepsResolvedAccessWithoutDataType()
    {
        var parameters = Parameter("P_OBJ", "readWrite", "<dataTypeIDRef uniqueIDRef=\"REC\"/>");
        var types = @"<dataTypeList><struct name=""Rec"" uniqueID=""REC"">
            <varDeclaration name=""a"" uniqueID=""M1""><UDINT/></varDeclaration>
          </struct></dataTypeList>";
        var model = CanOpenFile.Xdd.ReadString(BuildXdd(
            types + "<parameterList>" + parameters + "</parameterList>",
            Object("2000", "Rec", "9", "uniqueIDRef=\"P_OBJ\"")));
        model.ObjectDictionary.Objects[0x2000].DataType.Should().BeNull();
        model.ObjectDictionary.Objects[0x2000].AccessType.Should().Be(AccessType.ReadWrite);
        model.ApplicationProcess!.ParameterList.Clear();

        var written = CanOpenFile.Xdd.WriteToString(model);
        var objElem = CanOpenObject(written, "2000");
        objElem.Attribute("uniqueIDRef").Should().BeNull();
        objElem.Attribute("dataType").Should().BeNull();
        objElem.Attribute("accessType")!.Value.Should().Be("rw");

        var again = CanOpenFile.Xdd.ReadString(written).ObjectDictionary.Objects[0x2000];
        again.AccessType.Should().Be(AccessType.ReadWrite);
        again.DataType.Should().BeNull();
    }

    [Fact]
    public void WriteString_ParameterRemoved_OmitsUniqueIdRefAndKeepsResolvedDataType()
    {
        var xml = BuildXdd(
            Parameter("P1", "readWrite", "<UDINT/><defaultValue value=\"8\"/>"),
            Object("2000", "P", "7", "uniqueIDRef=\"P1\""));
        var model = CanOpenFile.Xdd.ReadString(xml);
        model.ApplicationProcess!.ParameterList.Clear();

        var written = CanOpenFile.Xdd.WriteToString(model);
        var objElem = CanOpenObject(written, "2000");
        objElem.Attribute("uniqueIDRef").Should().BeNull();
        objElem.Attribute("dataType")!.Value.Should().Be("0007");
        objElem.Attribute("accessType")!.Value.Should().Be("rw");

        var again = CanOpenFile.Xdd.ReadString(written).ObjectDictionary.Objects[0x2000];
        again.UniqueIdRef.Should().BeNull();
        again.DataType.Should().Be(CanOpenDataType.Unsigned32);
        again.AccessType.Should().Be(AccessType.ReadWrite);
        again.DefaultValue.Should().Be("8");
    }

    [Fact]
    public void ConvertToDcf_UniqueIdRefSurvivesCloneAndXdcWrite()
    {
        var xml = BuildXdd(
            Parameter("P1", "read", "<UDINT/>"),
            Object("2000", "P", "7", "uniqueIDRef=\"P1\""));
        var eds = CanOpenFile.Xdd.ReadString(xml);

        var dcf = CanOpenFile.Eds.ConvertToDcf(
            eds,
            nodeId: 1,
            timestamp: new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            baudrate: 250,
            nodeName: "N1");

        dcf.ObjectDictionary.Objects[0x2000].UniqueIdRef.Should().Be("P1");
        dcf.ObjectDictionary.Objects[0x2000].DataType.Should().Be(CanOpenDataType.Unsigned32);

        var xdc = CanOpenFile.Xdc.WriteToString(dcf);
        xdc.Should().Contain("uniqueIDRef=\"P1\"");
        var reread = CanOpenFile.Xdc.ReadString(xdc).ObjectDictionary.Objects[0x2000];
        reread.UniqueIdRef.Should().Be("P1");
        reread.DataType.Should().Be(CanOpenDataType.Unsigned32);
    }

    [Fact]
    public void ReadString_Xdc_ResolvesUniqueIdRef()
    {
        var xml = BuildXdd(
            Parameter("P1", "write", "<INT/>"),
            Object("2000", "P", "7", "uniqueIDRef=\"P1\""));

        var obj = CanOpenFile.Xdc.ReadString(xml).ObjectDictionary.Objects[0x2000];

        obj.UniqueIdRef.Should().Be("P1");
        obj.DataType.Should().Be(CanOpenDataType.Integer16);
        obj.AccessType.Should().Be(AccessType.WriteOnly);
    }

    [Fact]
    public void WriteString_ValidatedRoundTrip_PreservesUniqueIdRef()
    {
        var xml = BuildXdd(
            Parameter("P1", "const", "<UDINT/><defaultValue value=\"1\"/>"),
            Object("1000", "Device type", "7", "uniqueIDRef=\"P1\" PDOmapping=\"no\""));
        var model = CanOpenFile.Xdd.ReadString(xml);

        var written = CanOpenFile.Xdd.WriteToString(model, CanOpenWriteOptions.Validated);
        var again = CanOpenFile.Xdd.ReadString(written).ObjectDictionary.Objects[0x1000];

        again.UniqueIdRef.Should().Be("P1");
        again.DataType.Should().Be(CanOpenDataType.Unsigned32);
        again.AccessType.Should().Be(AccessType.Constant);
    }

    [Fact]
    public void ReadString_BasicDeviceRoundTrip_KeepsResolved1018Sub1()
    {
        var path = Path.Combine("Fixtures", "Corpus", "canopen-node", "basicDevice.xdd");
        var first = CanOpenFile.Xdd.ReadFile(path);
        var second = CanOpenFile.Xdd.ReadString(CanOpenFile.Xdd.WriteToString(first));
        var sub = second.ObjectDictionary.Objects[0x1018].SubObjects[0x01];

        sub.UniqueIdRef.Should().Be("UID_SUB_101801");
        sub.DataType.Should().Be(CanOpenDataType.Unsigned32);
        sub.AccessType.Should().Be(AccessType.ReadOnly);
        sub.DefaultValue.Should().Be("0x00000000");
        second.ObjectDictionary.Objects.Count.Should().Be(first.ObjectDictionary.Objects.Count);
    }

    [Fact]
    public void ReadString_SubObjectRange_FillsLimitsAndRoundTripsChangedDefault()
    {
        var values = "<UINT/><defaultValue value=\"3\"/><allowedValues><range><minValue value=\"1\"/><maxValue value=\"9\"/></range></allowedValues>";
        var xml = BuildXdd(
            Parameter("P_SUB", "read", values),
            @"<CANopenObject index=""2000"" name=""Rec"" objectType=""9"">
                <CANopenSubObject subIndex=""01"" name=""Field"" objectType=""7"" uniqueIDRef=""P_SUB""/>
                <CANopenSubObject subIndex=""02"" name=""Bare"" objectType=""7""/>
              </CANopenObject>");
        var model = CanOpenFile.Xdd.ReadString(xml);
        var sub = model.ObjectDictionary.Objects[0x2000].SubObjects[0x01];
        sub.LowLimit.Should().Be("1");
        sub.HighLimit.Should().Be("9");
        sub.DefaultValue = "4";
        model.ObjectDictionary.Objects[0x2000].SubObjects[0x01].DataType = 0;

        var written = CanOpenFile.Xdd.WriteToString(model);
        var objElem = CanOpenObject(written, "2000");
        var field = objElem.Elements().Single(e => (string?)e.Attribute("subIndex") == "01");
        var bare = objElem.Elements().Single(e => (string?)e.Attribute("subIndex") == "02");
        field.Attribute("defaultValue")!.Value.Should().Be("4");
        field.Attribute("lowLimit").Should().BeNull();
        field.Attribute("dataType")!.Value.Should().Be("0000");
        bare.Attribute("dataType")!.Value.Should().Be("0000");
        bare.Attribute("accessType")!.Value.Should().Be("ro");
    }

    [Fact]
    public void ReadString_PartialRangeAndExplicitLimit_FillsOnlyTheMissingSide()
    {
        var onlyMin = Parameter("P_MIN", "read", "<UINT/><allowedValues><range><minValue value=\"2\"/></range></allowedValues>");
        var onlyMax = Parameter("P_MAX", "read", "<UINT/><allowedValues><range><maxValue value=\"8\"/></range></allowedValues>");
        var both = Parameter(
            "P_BOTH",
            "read",
            "<UINT/><allowedValues><range><minValue value=\"1\"/><maxValue value=\"5\"/></range></allowedValues>");
        var xml = BuildXdd(
            onlyMin + onlyMax + both,
            Object("2000", "Min", "7", "uniqueIDRef=\"P_MIN\"")
            + Object("2001", "Max", "7", "uniqueIDRef=\"P_MAX\"")
            + Object("2002", "Both", "7", "lowLimit=\"0\" uniqueIDRef=\"P_BOTH\""));

        var od = CanOpenFile.Xdd.ReadString(xml).ObjectDictionary;
        od.Objects[0x2000].LowLimit.Should().Be("2");
        od.Objects[0x2000].HighLimit.Should().BeNull();
        od.Objects[0x2001].LowLimit.Should().BeNull();
        od.Objects[0x2001].HighLimit.Should().Be("8");
        od.Objects[0x2002].LowLimit.Should().Be("0");
        od.Objects[0x2002].HighLimit.Should().Be("5");
    }

    [Fact]
    public void ReadString_UnmappedTypesAndEmptyRefs_LeaveDataTypeUnset()
    {
        var types = @"
          <dataTypeList>
            <array name=""Bare"" uniqueID=""ARR""><subrange lowerLimit=""0"" upperLimit=""1""/></array>
            <derived name=""Loop"" uniqueID=""LOOP""><dataTypeIDRef uniqueIDRef=""LOOP""/></derived>
            <derived name=""Open"" uniqueID=""OPEN""/>
            <enum name=""BareEnum"" uniqueID=""EN0""><enumValue value=""1""/></enum>
            <enum name=""DateEnum"" uniqueID=""EN1""><DATE/></enum>
          </dataTypeList>";
        var parameters = types + @"
          <parameterList>
            <parameter uniqueID=""P_DATE"" access=""read""><DATE/></parameter>
            <parameter uniqueID=""P_NONE"" access=""read""><label lang=""en"">None</label></parameter>
            <parameter uniqueID=""P_EMPTY"" access=""read""><dataTypeIDRef uniqueIDRef=""""/></parameter>
            <parameter uniqueID=""P_ARR"" access=""read""><dataTypeIDRef uniqueIDRef=""ARR""/></parameter>
            <parameter uniqueID=""P_LOOP"" access=""read""><dataTypeIDRef uniqueIDRef=""LOOP""/></parameter>
            <parameter uniqueID=""P_OPEN"" access=""read""><dataTypeIDRef uniqueIDRef=""OPEN""/></parameter>
            <parameter uniqueID=""P_EN0"" access=""read""><dataTypeIDRef uniqueIDRef=""EN0""/></parameter>
            <parameter uniqueID=""P_EN1"" access=""read""><dataTypeIDRef uniqueIDRef=""EN1""/></parameter>
            <parameter uniqueID=""P_BAD"" access=""sideways""><UINT/></parameter>
            <parameter uniqueID=""P_NOLABEL"" access=""read""><USINT/></parameter>
          </parameterList>";
        var objects = Object("2000", "Date", "7", "uniqueIDRef=\"P_DATE\"")
            + Object("2001", "None", "7", "uniqueIDRef=\"P_NONE\"")
            + Object("2002", "Empty", "7", "uniqueIDRef=\"P_EMPTY\"")
            + Object("2003", "Arr", "8", "uniqueIDRef=\"P_ARR\"")
            + Object("2004", "Loop", "7", "uniqueIDRef=\"P_LOOP\"")
            + Object("2005", "Open", "7", "uniqueIDRef=\"P_OPEN\"")
            + Object("2006", "En0", "7", "uniqueIDRef=\"P_EN0\"")
            + Object("2007", "En1", "7", "uniqueIDRef=\"P_EN1\"")
            + Object("2008", "Bad", "7", "uniqueIDRef=\"P_BAD\"")
            + "<CANopenObject index=\"2009\" objectType=\"7\" uniqueIDRef=\"P_NOLABEL\"/>";

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(BuildXdd(parameters, objects));
        var od = result.Model.ObjectDictionary;
        od.Objects[0x2000].DataType.Should().BeNull();
        od.Objects[0x2001].DataType.Should().BeNull();
        od.Objects[0x2002].DataType.Should().BeNull();
        od.Objects[0x2003].DataType.Should().BeNull();
        od.Objects[0x2004].DataType.Should().BeNull();
        od.Objects[0x2005].DataType.Should().BeNull();
        od.Objects[0x2006].DataType.Should().BeNull();
        od.Objects[0x2007].DataType.Should().BeNull();
        od.Objects[0x2008].AccessType.Should().Be(AccessType.ReadOnly);
        od.Objects[0x2009].ParameterName.Should().BeEmpty();
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.XddUnresolvedDataTypeIdRef);

        var written = CanOpenFile.Xdd.WriteToString(result.Model);
        CanOpenObject(written, "2008").Attribute("accessType").Should().BeNull();

        var act = () => CanOpenFile.Xdd.ReadString(BuildXdd(parameters, objects), Strict);
        act.Should().Throw<EdsParseException>()
            .Which.Code.Should().Be(ParseDiagnosticCodes.XddUnresolvedDataTypeIdRef);
    }

    [Fact]
    public void ReadString_EmptyRangeAndAllowedValuesTemplate_DoNotSetLimits()
    {
        var emptyRange = Parameter("P_EMPTY", "read", "<UINT/><allowedValues><range/></allowedValues>");
        var emptyAllowed = Parameter("P_BARE", "read", "<UINT/><allowedValues/>");
        var templated = Parameter("P_TPL", "read", "<UINT/><allowedValues templateIDRef=\"AV1\"/>");
        var xml = BuildXdd(
            emptyRange + emptyAllowed + templated,
            Object("2000", "E", "7", "uniqueIDRef=\"P_EMPTY\"")
            + Object("2001", "B", "7", "uniqueIDRef=\"P_BARE\"")
            + Object("2002", "T", "7", "uniqueIDRef=\"P_TPL\""));

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml, Strict);
        result.Model.ObjectDictionary.Objects[0x2000].LowLimit.Should().BeNull();
        result.Model.ObjectDictionary.Objects[0x2000].HighLimit.Should().BeNull();
        result.Model.ObjectDictionary.Objects[0x2001].LowLimit.Should().BeNull();
        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == ParseDiagnosticCodes.XddUniqueIdRefIndirect &&
            d.RawValue == "templateIDRef");
    }

    [Fact]
    public void WriteString_ExplicitDefaultSurvivesAndStructSubObjectOmitsDataType()
    {
        var types = @"<dataTypeList><struct name=""Rec"" uniqueID=""REC"">
            <varDeclaration name=""a"" uniqueID=""M1""><UDINT/></varDeclaration>
          </struct></dataTypeList>";
        var parameters = Parameter("P_DEF", "read", "<UDINT/>")
            + Parameter("P_REC", "read", "<dataTypeIDRef uniqueIDRef=\"REC\"/>");
        var xml = BuildXdd(
            types + "<parameterList>" + parameters + "</parameterList>",
            Object("2000", "Def", "7", "defaultValue=\"4\" uniqueIDRef=\"P_DEF\"")
            + @"<CANopenObject index=""2001"" name=""Rec"" objectType=""9"">
                 <CANopenSubObject subIndex=""01"" name=""Field"" objectType=""7"" uniqueIDRef=""P_REC""/>
               </CANopenObject>");

        var written = CanOpenFile.Xdd.WriteToString(CanOpenFile.Xdd.ReadString(xml));
        CanOpenObject(written, "2000").Attribute("defaultValue")!.Value.Should().Be("4");
        var sub = CanOpenObject(written, "2001").Elements().Single(e => e.Name.LocalName == "CANopenSubObject");
        sub.Attribute("dataType").Should().BeNull();
        sub.Attribute("uniqueIDRef")!.Value.Should().Be("P_REC");
    }

    [Fact]
    public void WriteString_DynamicChannels_AreEmittedOnlyWhenASegmentExists()
    {
        var model = CanOpenFile.Xdd.ReadString(BuildXdd(
            Parameter("P1", "read", "<UINT/>"),
            Object("2000", "P", "7", "uniqueIDRef=\"P1\"")));

        model.DynamicChannels = new DynamicChannels();
        CanOpenFile.Xdd.WriteToString(model).Should().NotContain("<dynamicChannel");

        model.DynamicChannels.Segments.Add(new DynamicChannelSegment
        {
            Type = CanOpenDataType.Unsigned16,
            Dir = AccessType.ReadOnly,
            Range = "2000-2001"
        });
        CanOpenFile.Xdd.WriteToString(model).Should().Contain("<dynamicChannel");
    }

    [Fact]
    public void ReadString_LongDerivedAndArrayChain_ResolvesWithoutRecursion()
    {
        const int links = 4096;
        var types = new StringBuilder();
        types.Append("<dataTypeList>");
        types.Append("<derived name=\"T0\" uniqueID=\"T0\"><UDINT/></derived>");
        for (var i = 1; i < links; i++)
        {
            types.Append("<derived name=\"T").Append(i).Append("\" uniqueID=\"T").Append(i)
                .Append("\"><dataTypeIDRef uniqueIDRef=\"T").Append(i - 1).Append("\"/></derived>");
        }

        types.Append("<derived name=\"Mid\" uniqueID=\"MID\"><USINT/></derived>");
        types.Append("<array name=\"Wrapped\" uniqueID=\"ARR\"><subrange lowerLimit=\"0\" upperLimit=\"1\"/><dataTypeIDRef uniqueIDRef=\"MID\"/></array>");
        types.Append("</dataTypeList><parameterList>");
        types.Append("<parameter uniqueID=\"P_LONG\" access=\"read\"><dataTypeIDRef uniqueIDRef=\"T")
            .Append(links - 1).Append("\"/></parameter>");
        types.Append("<parameter uniqueID=\"P_ARR\" access=\"readWrite\"><dataTypeIDRef uniqueIDRef=\"ARR\"/></parameter>");
        types.Append("</parameterList>");

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(BuildXdd(
            types.ToString(),
            Object("2000", "Long", "7", "uniqueIDRef=\"P_LONG\"")
            + Object("2001", "Arr", "8", "uniqueIDRef=\"P_ARR\"")));

        result.Diagnostics.Should().BeEmpty();
        result.Model.ObjectDictionary.Objects[0x2000].DataType.Should().Be(CanOpenDataType.Unsigned32);
        result.Model.ObjectDictionary.Objects[0x2001].DataType.Should().Be(CanOpenDataType.Unsigned8);
    }

    [Fact]
    public void WriteString_EmptyExplicitValues_OverrideProjectedReference()
    {
        var body = "<UDINT/><defaultValue value=\"42\"/><allowedValues><range><minValue value=\"1\"/><maxValue value=\"9\"/></range></allowedValues>";
        var xml = BuildXdd(
            Parameter("P1", "readWrite", body) + Parameter("P_SUB", "read", body),
            @"<CANopenObject index=""2000"" name=""P"" objectType=""7"" defaultValue="""" lowLimit="""" highLimit="""" uniqueIDRef=""P1"">
                <CANopenSubObject subIndex=""01"" name=""S"" objectType=""7"" defaultValue="""" lowLimit="""" highLimit="""" uniqueIDRef=""P_SUB""/>
              </CANopenObject>");

        var written = CanOpenFile.Xdd.WriteToString(CanOpenFile.Xdd.ReadString(xml));
        var objElem = CanOpenObject(written, "2000");
        var subElem = objElem.Elements().Single(e => e.Name.LocalName == "CANopenSubObject");
        objElem.Attribute("uniqueIDRef")!.Value.Should().Be("P1");
        objElem.Attribute("defaultValue")!.Value.Should().BeEmpty();
        objElem.Attribute("lowLimit")!.Value.Should().BeEmpty();
        objElem.Attribute("highLimit")!.Value.Should().BeEmpty();
        subElem.Attribute("defaultValue")!.Value.Should().BeEmpty();
        subElem.Attribute("lowLimit")!.Value.Should().BeEmpty();
        subElem.Attribute("highLimit")!.Value.Should().BeEmpty();

        var again = CanOpenFile.Xdd.ReadString(written).ObjectDictionary.Objects[0x2000];
        again.DefaultValue.Should().BeEmpty();
        again.LowLimit.Should().BeEmpty();
        again.HighLimit.Should().BeEmpty();
        again.SubObjects[0x01].DefaultValue.Should().BeEmpty();
        again.SubObjects[0x01].LowLimit.Should().BeEmpty();
        again.SubObjects[0x01].HighLimit.Should().BeEmpty();
    }

    [Fact]
    public void WriteString_AssignedUniqueIdRef_DoesNotInventReadOnlyAccess()
    {
        var xml = BuildXdd(
            Parameter("P1", "readWriteInput", "<UDINT/>") + Parameter("P_SUB", "write", "<USINT/>"),
            @"<CANopenObject index=""2000"" name=""P"" objectType=""7"">
                <CANopenSubObject subIndex=""01"" name=""S"" objectType=""7""/>
              </CANopenObject>");
        var model = CanOpenFile.Xdd.ReadString(xml);
        var obj = model.ObjectDictionary.Objects[0x2000];
        obj.UniqueIdRef = "P1";
        obj.SubObjects[0x01].UniqueIdRef = "P_SUB";
        obj.AccessTypeSpecified.Should().BeFalse();
        obj.SubObjects[0x01].AccessTypeSpecified.Should().BeFalse();

        var written = CanOpenFile.Xdd.WriteToString(model);
        var objElem = CanOpenObject(written, "2000");
        var subElem = objElem.Elements().Single(e => e.Name.LocalName == "CANopenSubObject");
        objElem.Attribute("uniqueIDRef")!.Value.Should().Be("P1");
        objElem.Attribute("accessType").Should().BeNull();
        subElem.Attribute("uniqueIDRef")!.Value.Should().Be("P_SUB");
        subElem.Attribute("accessType").Should().BeNull();

        var again = CanOpenFile.Xdd.ReadString(written).ObjectDictionary.Objects[0x2000];
        again.AccessType.Should().Be(AccessType.ReadWriteInput);
        again.DataType.Should().Be(CanOpenDataType.Unsigned32);
        again.SubObjects[0x01].AccessType.Should().Be(AccessType.WriteOnly);

        var assigned = CanOpenFile.Xdd.ReadString(written);
        assigned.ObjectDictionary.Objects[0x2000].AccessType = AccessType.Constant;
        var withOverride = CanOpenFile.Xdd.WriteToString(assigned);
        CanOpenObject(withOverride, "2000").Attribute("accessType")!.Value.Should().Be("const");
        CanOpenFile.Xdd.ReadString(withOverride).ObjectDictionary.Objects[0x2000].AccessType
            .Should().Be(AccessType.Constant);
    }

    [Fact]
    public void WriteScope_SecondDisposeAndProjectionOutsideWrite_AreIdle()
    {
        var scope = XddUniqueIdResolver.EnterWriteScope(applicationProcess: null);
        scope.Dispose();
        scope.Dispose();
        XddUniqueIdResolver.CurrentWriteProjection("P1").Should().BeNull();
    }

    private static string Parameter(
        string id,
        string access,
        string body,
        string? labels = null,
        string? templateIdRef = null)
    {
        var template = string.IsNullOrEmpty(templateIdRef)
            ? string.Empty
            : " templateIDRef=\"" + templateIdRef + "\"";
        return "<parameter uniqueID=\"" + id + "\" access=\"" + access + "\"" + template + ">"
            + (labels ?? "<label lang=\"en\">" + id + "</label>")
            + body
            + "</parameter>";
    }

    private static string Object(string index, string name, string objectType, string extra) =>
        "<CANopenObject index=\"" + index + "\" name=\"" + name + "\" objectType=\"" + objectType + "\" " + extra + "/>";

    private static string BuildXdd(string parameters, string objects) =>
        @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>Device</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdd"" fileCreator=""t"" fileCreationDate=""2026-01-01"" fileVersion=""1"">
      <DeviceIdentity>
        <vendorName>V</vendorName><vendorID>1</vendorID>
        <productName>P</productName><productID>1</productID>
      </DeviceIdentity>
      <ApplicationProcess>" + WrapParameters(parameters) + @"</ApplicationProcess>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileHeader><ProfileClassID>CommunicationNetwork</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdd"" fileCreator=""t"" fileCreationDate=""2026-01-01"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList>" + objects + @"</CANopenObjectList>
      </ApplicationLayers>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

    private static string WrapParameters(string parameters)
    {
        if (parameters.Contains("<parameterList>") || parameters.Contains("<dataTypeList>"))
            return parameters;
        return "<parameterList>" + parameters + "</parameterList>";
    }

    private static XElement CanOpenObject(string xml, string index)
    {
        var doc = XDocument.Parse(xml);
        return doc.Descendants().Single(e =>
            e.Name.LocalName == "CANopenObject" &&
            string.Equals((string?)e.Attribute("index"), index, StringComparison.Ordinal));
    }

    private static string Section(string eds, string name)
    {
        var header = "[" + name + "]";
        var start = eds.IndexOf(header, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);
        var next = eds.IndexOf("\n[", start + header.Length, StringComparison.Ordinal);
        return next < 0 ? eds.Substring(start) : eds.Substring(start, next - start);
    }
}
