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
    public void ReadString_NestedStructMemberRefs_ResolveLeafAndRoundTripCompletePath()
    {
        var application = @"
          <dataTypeList>
            <struct name=""Inner"" uniqueID=""INNER""><varDeclaration name=""Field"" uniqueID=""FIELD""><INT/><defaultValue value=""7""/></varDeclaration></struct>
            <struct name=""Outer"" uniqueID=""OUTER""><varDeclaration name=""Child"" uniqueID=""CHILD""><dataTypeIDRef uniqueIDRef=""INNER""/></varDeclaration></struct>
          </dataTypeList>"
            + FunctionTypes(@"<inputVars>
              <varDeclaration name=""Record"" uniqueID=""VS""><dataTypeIDRef uniqueIDRef=""OUTER""/></varDeclaration>
              <varDeclaration name=""Defaulted"" uniqueID=""VDEF""><dataTypeIDRef uniqueIDRef=""OUTER""/><defaultValue value=""4""/></varDeclaration>
              <varDeclaration name=""EmptyDefault"" uniqueID=""VEMPTY""><dataTypeIDRef uniqueIDRef=""OUTER""/><defaultValue/></varDeclaration>
            </inputVars>") + @"
          <functionInstanceList><functionInstance name=""F"" uniqueID=""I"" typeIDRef=""FT_LEAF""/></functionInstanceList>
          <parameterList>
            <parameter uniqueID=""P""><label lang=""en"">P</label><variableRef><instanceIDRef uniqueIDRef=""I""/><variableIDRef uniqueIDRef=""VS""/><memberRef uniqueIDRef=""CHILD""/><memberRef uniqueIDRef=""FIELD""/></variableRef></parameter>
            <parameter uniqueID=""PDEF""><label lang=""en"">D</label><variableRef><instanceIDRef uniqueIDRef=""I""/><variableIDRef uniqueIDRef=""VDEF""/><memberRef uniqueIDRef=""CHILD""/><memberRef uniqueIDRef=""FIELD""/></variableRef></parameter>
            <parameter uniqueID=""PEMPTY""><label lang=""en"">E</label><variableRef><instanceIDRef uniqueIDRef=""I""/><variableIDRef uniqueIDRef=""VEMPTY""/><memberRef uniqueIDRef=""CHILD""/><memberRef uniqueIDRef=""FIELD""/></variableRef><defaultValue/></parameter>
          </parameterList>";

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(BuildXdd(application,
            Object("2000", "Nested") + Object("2001", "Default", "PDEF") + Object("2002", "Empty", "PEMPTY")));

        result.Diagnostics.Should().BeEmpty();
        result.Model.ObjectDictionary.Objects[0x2000].DataType.Should().Be(CanOpenDataType.Integer16);
        result.Model.ObjectDictionary.Objects[0x2000].DefaultValue.Should().Be("7");
        result.Model.ObjectDictionary.Objects[0x2001].DefaultValue.Should().Be("4");
        result.Model.ObjectDictionary.Objects[0x2002].DefaultValue.Should().BeEmpty();
        var reference = result.Model.ApplicationProcess!.ParameterList[0].VariableRefs[0];
        reference.MemberRefs.Select(member => member.UniqueIdRef).Should().Equal("CHILD", "FIELD");
        reference.MemberRef.Should().BeSameAs(reference.MemberRefs[1]);
        var converted = CanOpenFile.Eds.ConvertToDcf(result.Model, nodeId: 1,
            timestamp: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var convertedReference = converted.ApplicationProcess!.ParameterList[0].VariableRefs[0];
        convertedReference.MemberRef.Should().BeSameAs(convertedReference.MemberRefs[1]);
        convertedReference.MemberRef!.UniqueIdRef = "FIELD_CHANGED";
        CanOpenFile.Xdc.WriteToString(converted).Should().Contain("uniqueIDRef=\"FIELD_CHANGED\"");

        var written = CanOpenFile.Xdd.WriteToString(result.Model);
        var childPosition = written.IndexOf("uniqueIDRef=\"CHILD\"", StringComparison.Ordinal);
        var fieldPosition = written.IndexOf("uniqueIDRef=\"FIELD\"", StringComparison.Ordinal);
        childPosition.Should().BeGreaterThanOrEqualTo(0);
        fieldPosition.Should().BeGreaterThan(childPosition);
        var second = CanOpenFile.Xdd.ReadStringWithDiagnostics(written);
        second.Diagnostics.Should().BeEmpty();
        second.Model.ObjectDictionary.Objects[0x2000].DataType.Should().Be(CanOpenDataType.Integer16);
        second.Model.ObjectDictionary.Objects[0x2000].DefaultValue.Should().Be("7");
        CanOpenFile.Xdd.ReadString(written, Strict).ObjectDictionary.Objects[0x2000].DataType
            .Should().Be(CanOpenDataType.Integer16);

        reference.MemberRef = new ApMemberRef { UniqueIdRef = "FIELD_REPLACED" };
        reference.MemberRef.Should().BeSameAs(reference.MemberRefs[1]);
        CanOpenFile.Xdd.WriteToString(result.Model).Should().Contain("uniqueIDRef=\"FIELD_REPLACED\"");
        reference.MemberRef = null;
        reference.MemberRefs.Should().BeEmpty();
        CanOpenFile.Xdd.WriteToString(result.Model).Should().NotContain("FIELD_REPLACED");

        reference.MemberRefs.Add(new ApMemberRef { UniqueIdRef = "CHILD_ONLY" });
        reference.MemberRefs.Add(new ApMemberRef { UniqueIdRef = "LEAF_REMOVED" });
        reference.MemberRefs.RemoveAt(1);
        reference.MemberRef.Should().BeSameAs(reference.MemberRefs[0]);
        var withoutLeaf = CanOpenFile.Xdd.WriteToString(result.Model);
        withoutLeaf.Should().Contain("uniqueIDRef=\"CHILD_ONLY\"");
        withoutLeaf.Should().NotContain("LEAF_REMOVED");

        reference.MemberRefs.Clear();
        reference.MemberRef.Should().BeNull();
        CanOpenFile.Xdd.WriteToString(result.Model).Should().NotContain("CHILD_ONLY");
    }

    [Fact]
    public void ReadString_AnonymousArrayDefaultStartAndMissingType_AreHandled()
    {
        var application = FunctionTypes(@"<inputVars>
          <varDeclaration name=""Bytes"" uniqueID=""V"" size=""3""><USINT/></varDeclaration>
          <varDeclaration name=""Missing"" uniqueID=""VNOTYPE"" size=""3""/>
        </inputVars>") + @"
          <functionInstanceList><functionInstance name=""F"" uniqueID=""I"" typeIDRef=""FT_LEAF""/></functionInstanceList>
          <parameterList>"
            + Parameter("P", VariableRef("V", member: " index=\"0\""))
            + Parameter("PNOTYPE", VariableRef("VNOTYPE"))
            + "</parameterList>";

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(BuildXdd(application,
            Object("2000", "Bytes") + Object("2001", "Missing", "PNOTYPE")));

        result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Message.Contains("whole-array", StringComparison.Ordinal));
        result.Model.ObjectDictionary.Objects[0x2000].DataType.Should().Be(CanOpenDataType.Unsigned8);
        result.Model.ObjectDictionary.Objects[0x2001].DataType.Should().BeNull();
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
            + Parameter("PTEMPLATE", VariableRef("VS", member: " uniqueIDRef=\"M\"")).Replace("uniqueID=\"PTEMPLATE\"", "uniqueID=\"PTEMPLATE\" templateIDRef=\"T\"")
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
            + Parameter("PBADTYPE", VariableRef("VSCALAR").Replace("instanceIDRef uniqueIDRef=\"I\"", "instanceIDRef uniqueIDRef=\"IBAD\""))
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
    public void ReadString_WholeArrayVariableRefs_DoNotProjectElementType()
    {
        var application = @"
          <dataTypeList><array name=""Bytes"" uniqueID=""ARR""><subrange lowerLimit=""0"" upperLimit=""2""/><USINT/></array></dataTypeList>
          <functionTypeList><functionType name=""F"" uniqueID=""FT""><versionInfo organization=""O"" version=""1"" author=""A"" date=""2026-01-01""/>
            <interfaceList><inputVars>
              <varDeclaration name=""Named"" uniqueID=""VNAMED""><dataTypeIDRef uniqueIDRef=""ARR""/></varDeclaration>
              <varDeclaration name=""Anonymous"" uniqueID=""VANON"" size=""3""><USINT/></varDeclaration>
              <varDeclaration name=""Text"" uniqueID=""VTEXT"" size=""3""><STRING/></varDeclaration>
            </inputVars></interfaceList>
          </functionType></functionTypeList>
          <functionInstanceList><functionInstance name=""F"" uniqueID=""I"" typeIDRef=""FT""/></functionInstanceList>
          <parameterList>"
            + Parameter("PNAMED", VariableRef("VNAMED"))
            + Parameter("PANON", VariableRef("VANON"))
            + Parameter("PTEXT", VariableRef("VTEXT"))
            + "</parameterList>";

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(BuildXdd(application,
            Object("2000", "Named", "PNAMED") + Object("2001", "Anonymous", "PANON")
            + Object("2002", "Text", "PTEXT")));

        result.Diagnostics.Should().HaveCount(2).And.OnlyContain(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.XddUnresolvedIndirectUniqueIdRef
            && diagnostic.Message.Contains("whole-array", StringComparison.Ordinal));
        result.Model.ObjectDictionary.Objects[0x2000].DataType.Should().BeNull();
        result.Model.ObjectDictionary.Objects[0x2001].DataType.Should().BeNull();
        result.Model.ObjectDictionary.Objects[0x2002].DataType.Should().Be(CanOpenDataType.VisibleString);
    }

    [Fact]
    public void ReadString_AnonymousArrayMemberRefs_ResolveElementTypeAndCheckBounds()
    {
        var application = @"
          <functionTypeList><functionType name=""F"" uniqueID=""FT""><versionInfo organization=""O"" version=""1"" author=""A"" date=""2026-01-01""/>
            <interfaceList><inputVars>
              <varDeclaration name=""Bytes"" uniqueID=""V"" start=""2"" size=""3"" initialValue=""parent""><USINT/></varDeclaration>
            </inputVars></interfaceList>
          </functionType></functionTypeList>
          <functionInstanceList><functionInstance name=""F"" uniqueID=""I"" typeIDRef=""FT""/></functionInstanceList>
          <parameterList>"
            + Parameter("P", VariableRef("V", member: " index=\"3\""))
            + Parameter("PDEFAULT", VariableRef("V", member: " index=\"4\"") + "<defaultValue value=\"6\"/>")
            + Parameter("POUT", VariableRef("V", member: " index=\"5\""))
            + "</parameterList>";

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(BuildXdd(application,
            Object("2000", "Element") + Object("2001", "Default", "PDEFAULT")
            + Object("2002", "Outside", "POUT")));

        result.Diagnostics.Should().ContainSingle(d => d.Message.Contains("memberRef index", StringComparison.Ordinal));
        result.Model.ObjectDictionary.Objects[0x2000].DataType.Should().Be(CanOpenDataType.Unsigned8);
        result.Model.ObjectDictionary.Objects[0x2000].DefaultValue.Should().BeNull();
        result.Model.ObjectDictionary.Objects[0x2001].DefaultValue.Should().Be("6");
        result.Model.ObjectDictionary.Objects[0x2002].DataType.Should().BeNull();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ReadString_ArrayMemberDefaults_ExcludeCompositeInitializerAndRoundTrip(
        bool namedArray, bool structElement)
    {
        var elementType = structElement ? "<dataTypeIDRef uniqueIDRef=\"REC\"/>" : "<USINT/>";
        var declarationType = namedArray ? "<dataTypeIDRef uniqueIDRef=\"ARR_ALIAS\"/>" : elementType;
        var bounds = namedArray ? string.Empty : " start=\"2\" size=\"3\"";
        var memberPath = "<memberRef index=\"2\"/>"
            + (structElement ? "<memberRef uniqueIDRef=\"FIELD\"/>" : string.Empty);
        var reference = VariableRef("V").Replace("</variableRef>", memberPath + "</variableRef>");
        var application = @"
          <templateList><parameterTemplate uniqueID=""T""><defaultValue value=""5""/></parameterTemplate></templateList>
          <dataTypeList>
            <struct name=""Record"" uniqueID=""REC""><varDeclaration name=""Field"" uniqueID=""FIELD""><USINT/><defaultValue value=""7""/></varDeclaration></struct>
            <array name=""Array"" uniqueID=""ARR""><subrange lowerLimit=""2"" upperLimit=""4""/>"
            + elementType + @"</array>
            <derived name=""Alias"" uniqueID=""ARR_ALIAS""><dataTypeIDRef uniqueIDRef=""ARR""/></derived>
          </dataTypeList>"
            + FunctionTypes("<inputVars><varDeclaration name=\"Values\" uniqueID=\"V\"" + bounds + ">"
                + declarationType + "<defaultValue value=\"[1,2,3]\"/>"
                + "<allowedValues><range><minValue value=\"0\"/><maxValue value=\"255\"/></range></allowedValues>"
                + "</varDeclaration></inputVars>") + @"
          <functionInstanceList><functionInstance name=""F"" uniqueID=""I"" typeIDRef=""FT_LEAF""/></functionInstanceList>
          <parameterList>"
            + Parameter("P", reference)
            + Parameter("PLOCAL", reference + "<defaultValue value=\"6\"/>")
            + Parameter("PEMPTY", reference + "<defaultValue/>")
            + Parameter("PTEMPLATE", reference).Replace("uniqueID=\"PTEMPLATE\"", "uniqueID=\"PTEMPLATE\" templateIDRef=\"T\"")
            + "</parameterList>";
        var xml = BuildXdd(application,
            Object("2000", "Element") + Object("2001", "Local", "PLOCAL")
            + Object("2002", "Empty", "PEMPTY") + Object("2003", "Template", "PTEMPLATE")
            + Object("2004", "Explicit").Replace("/>", " defaultValue=\"8\"/>"));

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml, Strict);
        result.Diagnostics.Should().BeEmpty();
        AssertDefaults(result.Model.ObjectDictionary);
        var written = CanOpenFile.Xdd.WriteToString(result.Model);
        written.Should().Contain("value=\"[1,2,3]\"");
        AssertDefaults(CanOpenFile.Xdd.ReadString(written, Strict).ObjectDictionary);

        var xdc = CanOpenFile.Xdc.ReadString(xml, Strict);
        AssertDefaults(xdc.ObjectDictionary);
        AssertDefaults(CanOpenFile.Xdc.ReadString(CanOpenFile.Xdc.WriteToString(xdc), Strict).ObjectDictionary);

        // The writer must compare against the scalar projection as well.
        result.Model.ObjectDictionary.Objects[0x2000].DefaultValue = "[1,2,3]";
        var overridden = System.Xml.Linq.XDocument.Parse(CanOpenFile.Xdd.WriteToString(result.Model))
            .Descendants().Single(element => element.Name.LocalName == "CANopenObject"
                && (string?)element.Attribute("index") == "2000");
        ((string?)overridden.Attribute("defaultValue")).Should().Be("[1,2,3]");

        void AssertDefaults(ObjectDictionary dictionary)
        {
            dictionary.Objects[0x2000].DataType.Should().Be(CanOpenDataType.Unsigned8);
            dictionary.Objects[0x2000].DefaultValue.Should().Be(structElement ? "7" : null);
            dictionary.Objects[0x2000].LowLimit.Should().Be("0");
            dictionary.Objects[0x2000].HighLimit.Should().Be("255");
            dictionary.Objects[0x2001].DefaultValue.Should().Be("6");
            dictionary.Objects[0x2002].DefaultValue.Should().BeEmpty();
            dictionary.Objects[0x2003].DefaultValue.Should().Be("5");
            dictionary.Objects[0x2004].DefaultValue.Should().Be("8");
        }
    }

    [Fact]
    public void ReadString_NestedNamedArrayElement_DoesNotProjectInnerScalarType()
    {
        var application = @"
          <dataTypeList>
            <array name=""Inner"" uniqueID=""ARR_INNER""><subrange lowerLimit=""0"" upperLimit=""2""/><USINT/></array>
            <array name=""Outer"" uniqueID=""ARR_OUTER""><subrange lowerLimit=""0"" upperLimit=""1""/><dataTypeIDRef uniqueIDRef=""ARR_INNER""/></array>
            <array name=""Scalar"" uniqueID=""ARR_SCALAR""><subrange lowerLimit=""0"" upperLimit=""2""/><USINT/></array>
          </dataTypeList>"
            + FunctionTypes(@"<inputVars>
              <varDeclaration name=""Nested"" uniqueID=""VNESTED""><dataTypeIDRef uniqueIDRef=""ARR_OUTER""/></varDeclaration>
              <varDeclaration name=""Scalar"" uniqueID=""VSCALAR""><dataTypeIDRef uniqueIDRef=""ARR_SCALAR""/></varDeclaration>
            </inputVars>") + @"
          <functionInstanceList><functionInstance name=""F"" uniqueID=""I"" typeIDRef=""FT_LEAF""/></functionInstanceList>
          <parameterList>"
            + Parameter("PNESTED", VariableRef("VNESTED", member: " index=\"1\""))
            + Parameter("PSCALAR", VariableRef("VSCALAR", member: " index=\"1\""))
            + "</parameterList>";

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(BuildXdd(application,
            Object("2000", "Nested", "PNESTED") + Object("2001", "Scalar", "PSCALAR")));

        result.Diagnostics.Should().ContainSingle(d => d.Message.Contains("selects an array element", StringComparison.Ordinal));
        result.Model.ObjectDictionary.Objects[0x2000].DataType.Should().BeNull();
        result.Model.ObjectDictionary.Objects[0x2001].DataType.Should().Be(CanOpenDataType.Unsigned8);
    }

    [Fact]
    public void ReadString_WholeArrayStructureComponents_DoNotProjectElementType()
    {
        var application = @"
          <dataTypeList>
            <array name=""Bytes"" uniqueID=""ARR""><subrange lowerLimit=""0"" upperLimit=""2""/><USINT/></array>
            <struct name=""Record"" uniqueID=""REC"">
              <varDeclaration name=""Anonymous"" uniqueID=""MANON"" size=""3""><USINT/></varDeclaration>
              <varDeclaration name=""Named"" uniqueID=""MNAMED""><dataTypeIDRef uniqueIDRef=""ARR""/></varDeclaration>
              <varDeclaration name=""Scalar"" uniqueID=""MSCALAR""><USINT/></varDeclaration>
            </struct>
          </dataTypeList>"
            + FunctionTypes(@"<inputVars><varDeclaration name=""Record"" uniqueID=""V""><dataTypeIDRef uniqueIDRef=""REC""/></varDeclaration></inputVars>") + @"
          <functionInstanceList><functionInstance name=""F"" uniqueID=""I"" typeIDRef=""FT_LEAF""/></functionInstanceList>
          <parameterList>"
            + Parameter("PANON", VariableRef("V", member: " uniqueIDRef=\"MANON\""))
            + Parameter("PNAMED", VariableRef("V", member: " uniqueIDRef=\"MNAMED\""))
            + Parameter("PSCALAR", VariableRef("V", member: " uniqueIDRef=\"MSCALAR\""))
            + "</parameterList>";

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(BuildXdd(application,
            Object("2000", "Anonymous", "PANON") + Object("2001", "Named", "PNAMED")
            + Object("2002", "Scalar", "PSCALAR")));

        result.Diagnostics.Should().HaveCount(2).And.OnlyContain(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.XddUnresolvedIndirectUniqueIdRef
            && diagnostic.Message.Contains("whole-array structure component", StringComparison.Ordinal));
        result.Model.ObjectDictionary.Objects[0x2000].DataType.Should().BeNull();
        result.Model.ObjectDictionary.Objects[0x2001].DataType.Should().BeNull();
        result.Model.ObjectDictionary.Objects[0x2002].DataType.Should().Be(CanOpenDataType.Unsigned8);
    }

    [Fact]
    public void ReadString_AnonymousStructureArrayMemberRefWithoutIndex_IsRejected()
    {
        var application = @"
          <dataTypeList><struct name=""Record"" uniqueID=""REC"">
            <varDeclaration name=""Field"" uniqueID=""M""><USINT/></varDeclaration>
          </struct></dataTypeList>"
            + FunctionTypes(@"<inputVars><varDeclaration name=""Records"" uniqueID=""V"" size=""3""><dataTypeIDRef uniqueIDRef=""REC""/></varDeclaration></inputVars>") + @"
          <functionInstanceList><functionInstance name=""F"" uniqueID=""I"" typeIDRef=""FT_LEAF""/></functionInstanceList>
          <parameterList>" + Parameter("P", VariableRef("V", member: " uniqueIDRef=\"M\"")) + "</parameterList>";

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(BuildXdd(application, Object("2000", "Records")));

        result.Diagnostics.Should().ContainSingle(d => d.Message.Contains("enclosing array", StringComparison.Ordinal));
        result.Model.ObjectDictionary.Objects[0x2000].DataType.Should().BeNull();
    }

    [Fact]
    public void ReadString_DeclarationInitialValues_ProjectAsDefaultFallbacks()
    {
        var application = @"
          <templateList><parameterTemplate uniqueID=""T""><defaultValue value=""4""/></parameterTemplate></templateList>
          <dataTypeList><struct name=""Record"" uniqueID=""REC"">
            <varDeclaration name=""Field"" uniqueID=""M"" initialValue=""8""><INT/></varDeclaration>
          </struct></dataTypeList>
          <functionTypeList><functionType name=""F"" uniqueID=""FT""><versionInfo organization=""O"" version=""1"" author=""A"" date=""2026-01-01""/>
            <interfaceList><inputVars>
              <varDeclaration name=""Value"" uniqueID=""V"" initialValue=""7""><USINT/></varDeclaration>
              <varDeclaration name=""Struct"" uniqueID=""VS"" initialValue=""composite""><dataTypeIDRef uniqueIDRef=""REC""/></varDeclaration>
            </inputVars></interfaceList>
          </functionType></functionTypeList>
          <functionInstanceList><functionInstance name=""F"" uniqueID=""I"" typeIDRef=""FT""/></functionInstanceList>
          <parameterList>"
            + Parameter("PDIRECT", VariableRef("V") + "<defaultValue value=\"6\"/>")
            + Parameter("PMEMBER", VariableRef("VS", member: " uniqueIDRef=\"M\""))
            + Parameter("POVERRIDE", VariableRef("VS", member: " uniqueIDRef=\"M\"") + "<defaultValue value=\"5\"/>")
                .Replace("uniqueID=\"POVERRIDE\"", "uniqueID=\"POVERRIDE\" templateIDRef=\"T\"")
            + Parameter("PTEMPLATE", VariableRef("V"))
                .Replace("uniqueID=\"PTEMPLATE\"", "uniqueID=\"PTEMPLATE\" templateIDRef=\"T\"")
            + "</parameterList>";

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(BuildXdd(application,
            Object("2000", "Direct", "PDIRECT") + Object("2001", "Member", "PMEMBER")
            + Object("2002", "Override", "POVERRIDE") + Object("2003", "Template", "PTEMPLATE")));

        result.Diagnostics.Should().BeEmpty();
        result.Model.ObjectDictionary.Objects[0x2000].DefaultValue.Should().Be("6");
        result.Model.ObjectDictionary.Objects[0x2001].DefaultValue.Should().Be("8");
        result.Model.ObjectDictionary.Objects[0x2002].DefaultValue.Should().Be("5");
        result.Model.ObjectDictionary.Objects[0x2003].DefaultValue.Should().Be("4");
    }

    [Fact]
    public void ReadString_SizedStringAndBitstringDeclarations_RemainScalar()
    {
        var application = FunctionTypes(@"<inputVars>
          <varDeclaration name=""Text"" uniqueID=""VW"" size=""3""><WSTRING/></varDeclaration>
          <varDeclaration name=""Bits"" uniqueID=""VB"" size=""3""><BITSTRING/></varDeclaration>
        </inputVars>") + @"
          <functionInstanceList><functionInstance name=""F"" uniqueID=""I"" typeIDRef=""FT_LEAF""/></functionInstanceList>
          <parameterList>"
            + Parameter("PW", VariableRef("VW")) + Parameter("PB", VariableRef("VB"))
            + "</parameterList>";

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(BuildXdd(application,
            Object("2000", "Text", "PW") + Object("2001", "Bits", "PB")));

        result.Diagnostics.Should().BeEmpty();
        result.Model.ObjectDictionary.Objects[0x2000].DataType.Should().Be(CanOpenDataType.UnicodeString);
        result.Model.ObjectDictionary.Objects[0x2001].DataType.Should().Be(CanOpenDataType.OctetString);
    }

    [Fact]
    public void ReadString_TemplateChain_NearerValuesWinAndDuplicateTargetsAreReported()
    {
        const string application = @"
          <templateList>
            <parameterTemplate uniqueID=""T0""><UINT/><defaultValue value=""11""/>
              <allowedValues><range><minValue value=""1""/><maxValue value=""99""/></range></allowedValues>
            </parameterTemplate>
            <parameterTemplate uniqueID=""T1"" templateIDRef=""T0""><USINT/><defaultValue value=""12""/>
              <allowedValues><range><minValue value=""2""/><maxValue value=""3""/></range></allowedValues>
            </parameterTemplate>
            <parameterTemplate uniqueID=""DUP""><USINT/></parameterTemplate>
            <parameterTemplate uniqueID=""DUP""><UINT/></parameterTemplate>
          </templateList>
          <parameterList><parameter uniqueID=""P"" templateIDRef=""T1""><label lang=""en"">P</label></parameter>
            <parameter uniqueID=""PDUP"" templateIDRef=""DUP""><label lang=""en"">Duplicate</label></parameter>
          </parameterList>";

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(BuildXdd(application,
            Object("2000", "P") + Object("2001", "Duplicate", "PDUP")));

        result.Diagnostics.Should().ContainSingle(d => d.Message.Contains("identifies 2 targets", StringComparison.Ordinal));
        var obj = result.Model.ObjectDictionary.Objects[0x2000];
        obj.DataType.Should().Be(CanOpenDataType.Unsigned8);
        obj.DefaultValue.Should().Be("12");
        obj.LowLimit.Should().Be("2");
        obj.HighLimit.Should().Be("3");
    }

    [Fact]
    public void ReadString_MultidimensionalNamedArrayMemberRef_IsRejected()
    {
        var application = @"
          <dataTypeList><array name=""Matrix"" uniqueID=""ARR"">
            <subrange lowerLimit=""0"" upperLimit=""1""/><subrange lowerLimit=""0"" upperLimit=""1""/><USINT/>
          </array></dataTypeList>"
            + FunctionTypes(@"<inputVars><varDeclaration name=""Matrix"" uniqueID=""V""><dataTypeIDRef uniqueIDRef=""ARR""/></varDeclaration></inputVars>") + @"
          <functionInstanceList><functionInstance name=""F"" uniqueID=""I"" typeIDRef=""FT_LEAF""/></functionInstanceList>
          <parameterList>" + Parameter("P", VariableRef("V", member: " index=\"1\"")) + "</parameterList>";

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(BuildXdd(application, Object("2000", "Matrix")));

        result.Diagnostics.Should().ContainSingle(d => d.Message.Contains("one-dimensional array", StringComparison.Ordinal));
        result.Model.ObjectDictionary.Objects[0x2000].DataType.Should().BeNull();
    }

    [Theory]
    [InlineData("start=\"bad\" size=\"3\"", "3")]
    [InlineData("start=\"2\" size=\"bad\"", "3")]
    [InlineData("start=\"2\" size=\"0\"", "2")]
    [InlineData("start=\"2\" size=\"3\"", "1")]
    public void ReadString_AnonymousArrayWithInvalidBounds_IsRejected(string bounds, string index)
    {
        var application = FunctionTypes(
            "<inputVars><varDeclaration name=\"Bytes\" uniqueID=\"V\" " + bounds + "><USINT/></varDeclaration></inputVars>") + @"
          <functionInstanceList><functionInstance name=""F"" uniqueID=""I"" typeIDRef=""FT_LEAF""/></functionInstanceList>
          <parameterList>" + Parameter("P", VariableRef("V", member: " index=\"" + index + "\"")) + "</parameterList>";

        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(BuildXdd(application, Object("2000", "Bytes")));

        result.Diagnostics.Should().ContainSingle(d => d.Message.Contains("memberRef index", StringComparison.Ordinal));
        result.Model.ObjectDictionary.Objects[0x2000].DataType.Should().BeNull();
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

    [Fact]
    public void Resolver_NullDefaultValues_FallBackToVariableInitialValue()
    {
        var application = new ApplicationProcess
        {
            FunctionInstanceList = new ApFunctionInstanceList(),
            TemplateList = new ApTemplateList(),
        };
        application.TemplateList.ParameterTemplates.Add(new ApParameterTemplate
        {
            UniqueId = "T",
            DefaultValue = new ApParameterValue { Value = null! },
        });
        var function = new ApFunctionType
        {
            UniqueId = "FT",
            InterfaceList = new ApInterfaceList(),
        };
        function.InterfaceList.InputVars.Add(new ApVarDeclaration
        {
            UniqueId = "V",
            Type = new ApTypeRef { SimpleTypeName = "USINT" },
            DefaultValue = new ApParameterValue { Value = null! },
            InitialValue = "8",
        });
        application.FunctionTypeList.Add(function);
        application.FunctionInstanceList.FunctionInstances.Add(new ApFunctionInstance
        {
            UniqueId = "I",
            TypeIdRef = "FT",
        });
        var parameter = new ApParameter
        {
            UniqueId = "P",
            TemplateIdRef = "T",
            DefaultValue = new ApParameterValue { Value = null! },
        };
        var reference = new ApVariableRef { VariableIdRef = "V" };
        reference.InstanceIdRefs.Add("I");
        parameter.VariableRefs.Add(reference);
        application.ParameterList.Add(parameter);
        var obj = new CanOpenObject { Index = 0x2000, UniqueIdRef = "P" };

        var resolver = EdsDcfNet.Parsers.XddUniqueIdResolver.Create(application);
        resolver.ApplyObject(obj, default);

        obj.DataType.Should().Be(CanOpenDataType.Unsigned8);
        obj.DefaultValue.Should().Be("8");

        function.InterfaceList.InputVars[0].Size = "1";
        reference.MemberRef = new ApMemberRef { Index = 0 };
        var legacyMemberObj = new CanOpenObject { Index = 0x2001, UniqueIdRef = "P" };
        resolver.ApplyObject(legacyMemberObj, default);
        legacyMemberObj.DataType.Should().Be(CanOpenDataType.Unsigned8);
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
