namespace EdsDcfNet.Tests.Parsers;

using System.Text;
using EdsDcfNet;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;

/// <summary>Integration tests for indirect CiA 311 parameter references.</summary>
public class XddIndirectUniqueIdRefTests
{
    private static readonly CanOpenFileOptions Strict = new() { StrictParsing = true };

    [Fact]
    public void ReadString_ParameterTemplateChain_ResolvesTypeDefaultRangeAndRoundTrips()
    {
        const string application = @"
          <templateList>
            <parameterTemplate uniqueID=""T0"">
              <UINT/><defaultValue value=""11""/><allowedValues templateIDRef=""AV0""/>
            </parameterTemplate>
            <parameterTemplate uniqueID=""T1"" templateIDRef=""T0""><defaultValue value=""12""/></parameterTemplate>
            <allowedValuesTemplate uniqueID=""AV0""><range><minValue value=""1""/><maxValue value=""99""/></range></allowedValuesTemplate>
          </templateList>
          <parameterList><parameter uniqueID=""P"" templateIDRef=""T1""><label lang=""en"">P</label></parameter></parameterList>";

        var first = CanOpenFile.Xdd.ReadString(BuildXdd(application, Object("2000", "P")));
        var obj = first.ObjectDictionary.Objects[0x2000];
        obj.DataType.Should().Be(CanOpenDataType.Unsigned16);
        obj.DefaultValue.Should().Be("12");
        obj.LowLimit.Should().Be("1");
        obj.HighLimit.Should().Be("99");
        first.ApplicationProcess!.TemplateList!.ParameterTemplates[1].TemplateIdRef.Should().Be("T0");

        var written = CanOpenFile.Xdd.WriteToString(first);
        written.Should().Contain("templateIDRef=\"T0\"");
        var second = CanOpenFile.Xdd.ReadString(written).ObjectDictionary.Objects[0x2000];
        second.DataType.Should().Be(obj.DataType);
        second.DefaultValue.Should().Be("12");
        second.LowLimit.Should().Be("1");
        second.HighLimit.Should().Be("99");
    }

    [Fact]
    public void ReadString_NestedVariableRef_InterfaceValuesOverrideParameterValues()
    {
        var application = FunctionTypes(
            @"<inputVars><varDeclaration name=""Value"" uniqueID=""V""><UDINT/><defaultValue value=""9""/>" +
            @"<allowedValues><range><minValue value=""1""/><maxValue value=""20""/></range></allowedValues>" +
            "</varDeclaration></inputVars>")
            + @"
          <functionInstanceList><functionInstance name=""Outer"" uniqueID=""OUTER"" typeIDRef=""FT_OUTER""/></functionInstanceList>
          <parameterList><parameter uniqueID=""P""><label lang=""en"">P</label>
            <variableRef><instanceIDRef uniqueIDRef=""OUTER""/><instanceIDRef uniqueIDRef=""INNER""/><variableIDRef uniqueIDRef=""V""/></variableRef>
            <defaultValue value=""3""/><allowedValues><range><minValue value=""2""/><maxValue value=""3""/></range></allowedValues>
          </parameter></parameterList>";

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(BuildXdd(application, Object("2000", "P")));
        var obj = result.Model.ObjectDictionary.Objects[0x2000];
        result.Diagnostics.Should().BeEmpty();
        obj.DataType.Should().Be(CanOpenDataType.Unsigned32);
        obj.DefaultValue.Should().Be("9");
        obj.LowLimit.Should().Be("1");
        obj.HighLimit.Should().Be("20");
    }

    [Fact]
    public void ReadString_ArrayAndStructMemberRefs_ResolveScalarMembers()
    {
        const string application = @"
          <dataTypeList>
            <array name=""Bytes"" uniqueID=""ARR""><subrange lowerLimit=""2"" upperLimit=""4""/><USINT/></array>
            <struct name=""Record"" uniqueID=""REC""><varDeclaration name=""Field"" uniqueID=""M""><INT/><defaultValue value=""7""/></varDeclaration></struct>
            <derived name=""BytesAlias"" uniqueID=""ARR_ALIAS""><dataTypeIDRef uniqueIDRef=""ARR""/></derived>
            <derived name=""RecordAlias"" uniqueID=""REC_ALIAS""><dataTypeIDRef uniqueIDRef=""REC""/></derived>
          </dataTypeList>
          <functionTypeList><functionType name=""F"" uniqueID=""FT""><versionInfo organization=""O"" version=""1"" author=""A"" date=""2026-01-01""/>
            <interfaceList><inputVars>
              <varDeclaration name=""Array"" uniqueID=""VA""><dataTypeIDRef uniqueIDRef=""ARR_ALIAS""/></varDeclaration>
              <varDeclaration name=""Struct"" uniqueID=""VS""><dataTypeIDRef uniqueIDRef=""REC_ALIAS""/></varDeclaration>
            </inputVars></interfaceList>
          </functionType></functionTypeList>
          <functionInstanceList><functionInstance name=""F"" uniqueID=""I"" typeIDRef=""FT""/></functionInstanceList>
          <parameterList>
            <parameter uniqueID=""PA""><label lang=""en"">A</label><variableRef><instanceIDRef uniqueIDRef=""I""/><variableIDRef uniqueIDRef=""VA""/><memberRef index=""3""/></variableRef></parameter>
            <parameter uniqueID=""PS""><label lang=""en"">S</label><variableRef><instanceIDRef uniqueIDRef=""I""/><variableIDRef uniqueIDRef=""VS""/><memberRef uniqueIDRef=""M""/></variableRef></parameter>
          </parameterList>";

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(BuildXdd(application,
            Object("2000", "A", "PA") + Object("2001", "S", "PS")));
        result.Diagnostics.Should().BeEmpty();
        result.Model.ObjectDictionary.Objects[0x2000].DataType.Should().Be(CanOpenDataType.Unsigned8);
        result.Model.ObjectDictionary.Objects[0x2001].DataType.Should().Be(CanOpenDataType.Integer16);
        result.Model.ObjectDictionary.Objects[0x2001].DefaultValue.Should().Be("7");
    }

    [Fact]
    public void ReadString_PackedVariablesWithSameComponents_DoNotProjectComponentMetadata()
    {
        var application = @"
          <templateList><allowedValuesTemplate uniqueID=""AV""><range><minValue value=""0""/><maxValue value=""255""/></range></allowedValuesTemplate></templateList>
          <functionTypeList><functionType name=""F"" uniqueID=""FT""><versionInfo organization=""O"" version=""1"" author=""A"" date=""2026-01-01""/>
            <interfaceList><inputVars>" + Variable("V1") + Variable("V2") + @"</inputVars></interfaceList>
          </functionType></functionTypeList>
          <functionInstanceList><functionInstance name=""F"" uniqueID=""I"" typeIDRef=""FT""/></functionInstanceList>
          <parameterList><parameter uniqueID=""P""><label lang=""en"">P</label>"
            + VariableRef("V1", 1) + VariableRef("V2", 2) + "</parameter></parameterList>";

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(BuildXdd(application, Object("2000", "P")));
        var obj = result.Model.ObjectDictionary.Objects[0x2000];
        result.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.XddUnresolvedIndirectUniqueIdRef &&
            diagnostic.Message.Contains("packed variables", StringComparison.Ordinal));
        obj.DataType.Should().BeNull();
        obj.DefaultValue.Should().BeNull();
        obj.LowLimit.Should().BeNull();
        obj.HighLimit.Should().BeNull();
    }

    [Fact]
    public void ReadString_InvalidIndirectTargets_ReportDiagnosticsAndStrictThrows()
    {
        var application = @"
          <dataTypeList>
            <array name=""Bytes"" uniqueID=""ARR""><subrange lowerLimit=""0"" upperLimit=""1""/><USINT/></array>
            <struct name=""Record"" uniqueID=""REC""><varDeclaration name=""Field"" uniqueID=""M""><USINT/></varDeclaration></struct>
          </dataTypeList>
          <functionTypeList><functionType name=""F"" uniqueID=""FT""><versionInfo organization=""O"" version=""1"" author=""A"" date=""2026-01-01""/>
            <interfaceList><inputVars>
              <varDeclaration name=""A"" uniqueID=""VA""><dataTypeIDRef uniqueIDRef=""ARR""/></varDeclaration>
              <varDeclaration name=""S"" uniqueID=""VS""><dataTypeIDRef uniqueIDRef=""REC""/></varDeclaration>
              <varDeclaration name=""U8"" uniqueID=""V8""><USINT/><defaultValue value=""1""/></varDeclaration>
              <varDeclaration name=""U16"" uniqueID=""V16""><UINT/><defaultValue value=""2""/></varDeclaration>
            </inputVars></interfaceList>
          </functionType></functionTypeList>
          <functionInstanceList><functionInstance name=""F"" uniqueID=""I"" typeIDRef=""FT""/></functionInstanceList>
          <parameterList>"
            + Parameter("PTYPES", VariableRef("V8") + VariableRef("V16"))
            + Parameter("PDEFAULTS", VariableRef("V8") + VariableRef("V16"))
            + Parameter("PINDEX", VariableRef("VA", member: " index=\"9\""))
            + Parameter("PMISSINGMEMBER", VariableRef("VS", member: " uniqueIDRef=\"NO\""))
            + Parameter("PVARIABLE", VariableRef("NO"))
            + "</parameterList>";
        var xml = BuildXdd(application,
            Object("2000", "Types", "PTYPES") + Object("2001", "Defaults", "PDEFAULTS")
            + Object("2002", "Index", "PINDEX") + Object("2003", "Member", "PMISSINGMEMBER")
            + Object("2004", "Variable", "PVARIABLE"));

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml);
        result.Model.ObjectDictionary.Objects[0x2000].DataType.Should().BeNull();
        result.Model.ObjectDictionary.Objects[0x2001].DefaultValue.Should().BeNull();
        result.Diagnostics.Should().HaveCount(5).And.OnlyContain(d =>
            d.Code == ParseDiagnosticCodes.XddUnresolvedIndirectUniqueIdRef && d.RawValue == "variableRef");
        var act = () => CanOpenFile.Xdd.ReadString(xml, Strict);
        act.Should().Throw<EdsParseException>().Which.Code.Should().Be(ParseDiagnosticCodes.XddUnresolvedIndirectUniqueIdRef);
    }

    [Fact]
    public void ReadString_ConflictingPackedRangesAndMemberKindErrors_AreNotProjected()
    {
        var application = @"
          <functionTypeList><functionType name=""F"" uniqueID=""FT""><versionInfo organization=""O"" version=""1"" author=""A"" date=""2026-01-01""/>
            <interfaceList><inputVars>
              <varDeclaration name=""A"" uniqueID=""VA""><USINT/><allowedValues><range><minValue value=""0""/><maxValue value=""1""/></range></allowedValues></varDeclaration>
              <varDeclaration name=""B"" uniqueID=""VB""><USINT/><allowedValues><range><minValue value=""2""/><maxValue value=""3""/></range></allowedValues></varDeclaration>
            </inputVars></interfaceList>
          </functionType></functionTypeList>
          <functionInstanceList><functionInstance name=""F"" uniqueID=""I"" typeIDRef=""FT""/></functionInstanceList>
          <parameterList>"
            + Parameter("PRANGE", VariableRef("VA") + VariableRef("VB"))
            + Parameter("PKIND", VariableRef("VA", member: " uniqueIDRef=\"NO\""))
            + Parameter("PNOPATH", "<variableRef><variableIDRef uniqueIDRef=\"VA\"/></variableRef>")
            + "</parameterList>";
        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(BuildXdd(application,
            Object("2000", "R", "PRANGE") + Object("2001", "K", "PKIND") + Object("2002", "N", "PNOPATH")));

        result.Model.ObjectDictionary.Objects[0x2000].LowLimit.Should().BeNull();
        result.Diagnostics.Should().HaveCount(3);
        result.Diagnostics.Select(d => d.Message).Should().Contain(m => m.Contains("packed variables", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadString_MemberValues_ParameterAndTemplateOverrideComponentDefaultsAndRanges()
    {
        var application = @"
          <templateList><parameterTemplate uniqueID=""T""><USINT/><defaultValue value=""5""/>
            <allowedValues><range><minValue value=""4""/><maxValue value=""5""/></range></allowedValues>
          </parameterTemplate></templateList>
          <dataTypeList><struct name=""Record"" uniqueID=""REC""><varDeclaration name=""Field"" uniqueID=""M""><INT/>
            <defaultValue value=""7""/><allowedValues><range><minValue value=""0""/><maxValue value=""7""/></range></allowedValues>
          </varDeclaration></struct></dataTypeList>
          <functionTypeList><functionType name=""F"" uniqueID=""FT""><versionInfo organization=""O"" version=""1"" author=""A"" date=""2026-01-01""/>
            <interfaceList><inputVars><varDeclaration name=""Struct"" uniqueID=""VS""><dataTypeIDRef uniqueIDRef=""REC""/></varDeclaration></inputVars></interfaceList>
          </functionType></functionTypeList>
          <functionInstanceList><functionInstance name=""F"" uniqueID=""I"" typeIDRef=""FT""/></functionInstanceList>
          <parameterList>"
            + Parameter("PLOCAL", VariableRef("VS", member: " uniqueIDRef=\"M\"")
                + "<defaultValue value=\"6\"/><allowedValues><range><minValue value=\"5\"/><maxValue value=\"6\"/></range></allowedValues>")
            + Parameter("PTEMPLATE", VariableRef("VS", member: " uniqueIDRef=\"M\"")).Replace("uniqueID=\"PTEMPLATE\"", "uniqueID=\"PTEMPLATE\" templateIDRef=\"T\"", StringComparison.Ordinal)
            + Parameter("PCOMPONENT", VariableRef("VS", member: " uniqueIDRef=\"M\""))
            + "</parameterList>";

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(BuildXdd(application,
            Object("2000", "Local", "PLOCAL") + Object("2001", "Template", "PTEMPLATE")
            + Object("2002", "Component", "PCOMPONENT")));

        result.Diagnostics.Should().BeEmpty();
        var objects = result.Model.ObjectDictionary.Objects;
        objects[0x2000].DataType.Should().Be(CanOpenDataType.Integer16);
        objects[0x2000].DefaultValue.Should().Be("6");
        objects[0x2000].LowLimit.Should().Be("5");
        objects[0x2000].HighLimit.Should().Be("6");
        objects[0x2001].DataType.Should().Be(CanOpenDataType.Integer16);
        objects[0x2001].DefaultValue.Should().Be("5");
        objects[0x2001].LowLimit.Should().Be("4");
        objects[0x2001].HighLimit.Should().Be("5");
        objects[0x2002].DefaultValue.Should().Be("7");
        objects[0x2002].LowLimit.Should().Be("0");
        objects[0x2002].HighLimit.Should().Be("7");
    }

    [Fact]
    public void ReadString_PartiallyResolvedVariables_DoNotProjectSuccessfulSubset()
    {
        var application = @"
          <functionTypeList><functionType name=""F"" uniqueID=""FT""><versionInfo organization=""O"" version=""1"" author=""A"" date=""2026-01-01""/>
            <interfaceList><inputVars><varDeclaration name=""Value"" uniqueID=""V""><UDINT/><defaultValue value=""9""/>
              <allowedValues><range><minValue value=""1""/><maxValue value=""20""/></range></allowedValues>
            </varDeclaration></inputVars></interfaceList>
          </functionType></functionTypeList>
          <functionInstanceList><functionInstance name=""F"" uniqueID=""I"" typeIDRef=""FT""/></functionInstanceList>
          <parameterList>"
            + Parameter("PDIRECT", VariableRef("V") + VariableRef("NO")
                + "<USINT/><defaultValue value=\"3\"/><allowedValues><range><minValue value=\"2\"/><maxValue value=\"3\"/></range></allowedValues>")
            + Parameter("PUNSET", VariableRef("V") + VariableRef("NO"))
            + "</parameterList>";

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(BuildXdd(application,
            Object("2000", "Direct", "PDIRECT") + Object("2001", "Unset", "PUNSET")));

        result.Diagnostics.Should().HaveCount(2).And.OnlyContain(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.XddUnresolvedIndirectUniqueIdRef);
        var direct = result.Model.ObjectDictionary.Objects[0x2000];
        direct.DataType.Should().Be(CanOpenDataType.Unsigned8);
        direct.DefaultValue.Should().Be("3");
        direct.LowLimit.Should().Be("2");
        direct.HighLimit.Should().Be("3");
        var unset = result.Model.ObjectDictionary.Objects[0x2001];
        unset.DataType.Should().BeNull();
        unset.DefaultValue.Should().BeNull();
        unset.LowLimit.Should().BeNull();
        unset.HighLimit.Should().BeNull();
    }

    [Fact]
    public void ReadString_UnresolvableVariableShapes_LeaveTypeUnsetAndReportDiagnostics()
    {
        var application = @"
          <dataTypeList>
            <struct name=""Record"" uniqueID=""REC""><varDeclaration name=""Field"" uniqueID=""M""><USINT/></varDeclaration></struct>
            <derived name=""Cycle1"" uniqueID=""C1""><dataTypeIDRef uniqueIDRef=""C2""/></derived>
            <derived name=""Cycle2"" uniqueID=""C2""><dataTypeIDRef uniqueIDRef=""C1""/></derived>
          </dataTypeList>
          <functionTypeList><functionType name=""F"" uniqueID=""FT""><versionInfo organization=""O"" version=""1"" author=""A"" date=""2026-01-01""/>
            <interfaceList><inputVars>
              <varDeclaration name=""Struct"" uniqueID=""VSTRUCT""><dataTypeIDRef uniqueIDRef=""REC""/></varDeclaration>
              <varDeclaration name=""NoType"" uniqueID=""VNOTYPE""/>
              <varDeclaration name=""Scalar"" uniqueID=""VSCALAR""><dataTypeIDRef uniqueIDRef=""REC""/></varDeclaration>
              <varDeclaration name=""Cycle"" uniqueID=""VCYCLE""><dataTypeIDRef uniqueIDRef=""C1""/></varDeclaration>
            </inputVars></interfaceList>
          </functionType></functionTypeList>
          <functionInstanceList><functionInstance name=""F"" uniqueID=""I"" typeIDRef=""FT""/>
            <functionInstance name=""Bad"" uniqueID=""IBAD"" typeIDRef=""MISSING""/>
          </functionInstanceList>
          <parameterList>"
            + Parameter("PSTRUCT", VariableRef("VSTRUCT"))
            + Parameter("PNOTYPE", VariableRef("VNOTYPE"))
            + Parameter("PBADTYPE", VariableRef("VSCALAR").Replace("instanceIDRef uniqueIDRef=\"I\"", "instanceIDRef uniqueIDRef=\"IBAD\"", StringComparison.Ordinal))
            + Parameter("PNOTARRAY", VariableRef("VSCALAR", member: " index=\"1\""))
            + Parameter("PCYCLE", VariableRef("VCYCLE", member: " uniqueIDRef=\"M\""))
            + "</parameterList>";

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(BuildXdd(application,
            Object("2000", "Struct", "PSTRUCT") + Object("2001", "NoType", "PNOTYPE")
            + Object("2002", "BadType", "PBADTYPE") + Object("2003", "NotArray", "PNOTARRAY")
            + Object("2004", "Cycle", "PCYCLE")));

        result.Diagnostics.Should().HaveCount(4).And.OnlyContain(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.XddUnresolvedIndirectUniqueIdRef);
        result.Model.ObjectDictionary.Objects.Values.Should().OnlyContain(obj => obj.DataType == null);
    }

    [Fact]
    public void ReadString_LongTemplateChainAndCycle_AreHandledIteratively()
    {
        const int links = 2048;
        var templates = new StringBuilder("<templateList><parameterTemplate uniqueID=\"T0\"><UDINT/></parameterTemplate>");
        for (var i = 1; i < links; i++)
            templates.Append("<parameterTemplate uniqueID=\"T").Append(i).Append("\" templateIDRef=\"T").Append(i - 1).Append("\"/>");
        templates.Append("<parameterTemplate uniqueID=\"C1\" templateIDRef=\"C2\"/><parameterTemplate uniqueID=\"C2\" templateIDRef=\"C1\"/></templateList>");
        templates.Append("<parameterList><parameter uniqueID=\"P\" templateIDRef=\"T").Append(links - 1).Append("\"><label lang=\"en\">P</label></parameter>")
            .Append("<parameter uniqueID=\"C\" templateIDRef=\"C1\"><label lang=\"en\">C</label><USINT/></parameter></parameterList>");
        var xml = BuildXdd(templates.ToString(), Object("2000", "P") + Object("2001", "C", "C"));

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml);
        result.Model.ObjectDictionary.Objects[0x2000].DataType.Should().Be(CanOpenDataType.Unsigned32);
        result.Diagnostics.Should().ContainSingle(d => d.Message.Contains("cycle", StringComparison.Ordinal));
    }

    private static string Variable(string id) =>
        "<varDeclaration name=\"" + id + "\" uniqueID=\"" + id + "\"><USINT/><defaultValue value=\"4\"/><allowedValues templateIDRef=\"AV\"/></varDeclaration>";

    private static string VariableRef(string id, byte position = 1, string? member = null)
    {
        var positionAttribute = position == 1 ? string.Empty : " position=\"" + position + "\"";
        return "<variableRef" + positionAttribute + "><instanceIDRef uniqueIDRef=\"I\"/><variableIDRef uniqueIDRef=\"" + id + "\"/>"
            + (member == null ? string.Empty : "<memberRef" + member + "/>") + "</variableRef>";
    }

    private static string Parameter(string id, string body) =>
        "<parameter uniqueID=\"" + id + "\"><label lang=\"en\">" + id + "</label>" + body + "</parameter>";

    private static string FunctionTypes(string leafVariables) => @"
          <functionTypeList>
            <functionType name=""Leaf"" uniqueID=""FT_LEAF""><versionInfo organization=""O"" version=""1"" author=""A"" date=""2026-01-01""/><interfaceList>" + leafVariables + @"</interfaceList></functionType>
            <functionType name=""Outer"" uniqueID=""FT_OUTER""><versionInfo organization=""O"" version=""1"" author=""A"" date=""2026-01-01""/><interfaceList/>
              <functionInstanceList><functionInstance name=""Inner"" uniqueID=""INNER"" typeIDRef=""FT_LEAF""/></functionInstanceList>
            </functionType>
          </functionTypeList>";

    private static string Object(string index, string name, string parameter = "P") =>
        "<CANopenObject index=\"" + index + "\" name=\"" + name + "\" objectType=\"7\" uniqueIDRef=\"" + parameter + "\"/>";

    private static string BuildXdd(string application, string objects) =>
        @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile><ProfileHeader><ProfileClassID>Device</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdd"" fileCreator=""t"" fileCreationDate=""2026-01-01"" fileVersion=""1"">
      <DeviceIdentity><vendorName>V</vendorName><vendorID>1</vendorID><productName>P</productName><productID>1</productID></DeviceIdentity>
      <ApplicationProcess>" + application + @"</ApplicationProcess>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile><ProfileHeader><ProfileClassID>CommunicationNetwork</ProfileClassID></ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdd"" fileCreator=""t"" fileCreationDate=""2026-01-01"" fileVersion=""1"">
      <ApplicationLayers><CANopenObjectList>" + objects + @"</CANopenObjectList></ApplicationLayers>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";
}
