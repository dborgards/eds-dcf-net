namespace EdsDcfNet.Tests.Parsers;

using System.Xml.Linq;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Writers;

/// <summary>
/// Comprehensive tests for the ApplicationProcess typed model:
/// parsing (XddReader), writing (XddWriter), and parser/writer round-trips.
/// Covers all sub-constructs defined in CiA DS311 §6.4.5.
/// </summary>
public class ApplicationProcessTests
{
    private static readonly XddReader Reader = new();
    private static readonly XddWriter Writer = new();

    // ── Helper: wrap an ApplicationProcess fragment in a minimal valid XDD ─────

    private static string WrapInXdd(string applicationProcessXml) => $@"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <DeviceIdentity><vendorName>T</vendorName><vendorID>0x1</vendorID><productName>T</productName><productID>0x1</productID></DeviceIdentity>
      <DeviceManager/>
      <DeviceFunction/>
      {applicationProcessXml}
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007"" accessType=""ro"" PDOmapping=""no""/>
        </CANopenObjectList>
      </ApplicationLayers>
      <TransportLayers><PhysicalLayer><baudRate defaultValue=""250 Kbps""/></PhysicalLayer></TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""0"" nrOfTxPDO=""0"" bootUpSlave=""false"" layerSettingServiceSlave=""false"" groupMessaging=""false"" dynamicChannels=""0""/>
        <CANopenMasterFeatures bootUpMaster=""false""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

    private static ElectronicDataSheet ParseAp(string applicationProcessXml) =>
        Reader.ReadString(WrapInXdd(applicationProcessXml));

    private static ElectronicDataSheet CreateBaseEds()
    {
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.xdd", FileVersion = 1, CreatedBy = "Test" },
            DeviceInfo = new DeviceInfo { VendorName = "T", VendorNumber = 1, ProductName = "T", ProductNumber = 1 },
        };
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000, ParameterName = "Device Type", ObjectType = 0x7,
            DataType = 0x0007, AccessType = AccessType.ReadOnly, PdoMapping = false,
        };
        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);
        return eds;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Reader — absence / presence
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_NoApplicationProcess_ReturnsNull()
    {
        var result = Reader.ReadString(WrapInXdd(""));
        result.ApplicationProcess.Should().BeNull();
    }

    [Fact]
    public void Parse_EmptyApplicationProcess_ReturnsNonNull()
    {
        var result = ParseAp("<ApplicationProcess/>");
        result.ApplicationProcess.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Reader — dataTypeList / array
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_ArrayType_SetsNameUniqueIdSubrangesAndElementType()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <dataTypeList>
    <array name=""ByteArray10"" uniqueID=""uid_arr1"">
      <subrange lowerLimit=""0"" upperLimit=""9""/>
      <USINT/>
    </array>
  </dataTypeList>
  <parameterList/>
</ApplicationProcess>");

        var ap = result.ApplicationProcess!;
        ap.DataTypeList.Should().NotBeNull();
        ap.DataTypeList!.Arrays.Should().HaveCount(1);

        var arr = ap.DataTypeList.Arrays[0];
        arr.Name.Should().Be("ByteArray10");
        arr.UniqueId.Should().Be("uid_arr1");
        arr.Subranges.Should().HaveCount(1);
        arr.Subranges[0].LowerLimit.Should().Be(0);
        arr.Subranges[0].UpperLimit.Should().Be(9);
        arr.ElementType.Should().NotBeNull();
        arr.ElementType!.SimpleTypeName.Should().Be("USINT");
        arr.ElementType.DataTypeIdRef.Should().BeNull();
    }

    [Fact]
    public void Parse_ArrayType_WithDataTypeIdRef()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <dataTypeList>
    <array name=""StructArray"" uniqueID=""uid_arr2"">
      <subrange lowerLimit=""1"" upperLimit=""5""/>
      <dataTypeIDRef uniqueIDRef=""uid_struct1""/>
    </array>
  </dataTypeList>
  <parameterList/>
</ApplicationProcess>");

        var arr = result.ApplicationProcess!.DataTypeList!.Arrays[0];
        arr.ElementType!.DataTypeIdRef.Should().Be("uid_struct1");
        arr.ElementType.SimpleTypeName.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Reader — dataTypeList / struct
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_StructType_WithVarDeclarations()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <dataTypeList>
    <struct name=""StatusReg"" uniqueID=""uid_str1"">
      <varDeclaration name=""Enable"" uniqueID=""uid_vd1""><BOOL/></varDeclaration>
      <varDeclaration name=""Counter"" uniqueID=""uid_vd2"" signed=""true"" initialValue=""0""><INT/></varDeclaration>
    </struct>
  </dataTypeList>
  <parameterList/>
</ApplicationProcess>");

        var st = result.ApplicationProcess!.DataTypeList!.Structs[0];
        st.Name.Should().Be("StatusReg");
        st.UniqueId.Should().Be("uid_str1");
        st.VarDeclarations.Should().HaveCount(2);

        st.VarDeclarations[0].Name.Should().Be("Enable");
        st.VarDeclarations[0].Type!.SimpleTypeName.Should().Be("BOOL");
        st.VarDeclarations[0].IsSigned.Should().BeNull();

        st.VarDeclarations[1].Name.Should().Be("Counter");
        st.VarDeclarations[1].IsSigned.Should().BeTrue();
        st.VarDeclarations[1].InitialValue.Should().Be("0");
        st.VarDeclarations[1].Type!.SimpleTypeName.Should().Be("INT");
    }

    [Fact]
    public void Parse_VarDeclaration_WithAllOptionalAttributes()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <dataTypeList>
    <struct name=""S"" uniqueID=""uid_s"">
      <varDeclaration name=""Val"" uniqueID=""uid_v"" start=""0"" size=""8""
                      offset=""100"" multiplier=""0.1"" signed=""false"">
        <USINT/>
      </varDeclaration>
    </struct>
  </dataTypeList>
  <parameterList/>
</ApplicationProcess>");

        var vd = result.ApplicationProcess!.DataTypeList!.Structs[0].VarDeclarations[0];
        vd.Start.Should().Be("0");
        vd.Size.Should().Be("8");
        vd.Offset.Should().Be("100");
        vd.Multiplier.Should().Be("0.1");
        vd.IsSigned.Should().BeFalse();
    }

    [Fact]
    public void Parse_VarDeclaration_WithConditionalSupportDefaultAllowedValuesAndUnit()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <dataTypeList>
    <struct name=""S"" uniqueID=""uid_s"">
      <varDeclaration name=""Cfg"" uniqueID=""uid_cfg"">
        <UINT/>
        <conditionalSupport paramIDRef=""uid_dep_cfg""/>
        <defaultValue value=""4""/>
        <allowedValues>
          <value value=""4""/>
        </allowedValues>
        <unit multiplier=""1.0"" unitURI=""urn:example:cfg""/>
      </varDeclaration>
    </struct>
  </dataTypeList>
  <parameterList/>
</ApplicationProcess>");

        var vd = result.ApplicationProcess!.DataTypeList!.Structs[0].VarDeclarations[0];
        vd.ConditionalSupports.Should().ContainSingle().Which.Should().Be("uid_dep_cfg");
        vd.DefaultValue.Should().NotBeNull();
        vd.DefaultValue!.Value.Should().Be("4");
        vd.AllowedValues.Should().NotBeNull();
        vd.AllowedValues!.Values.Should().ContainSingle();
        vd.AllowedValues.Values[0].Value.Should().Be("4");
        vd.Unit.Should().NotBeNull();
        vd.Unit!.Multiplier.Should().Be("1.0");
        vd.Unit.UnitUri.Should().Be("urn:example:cfg");
    }

    [Fact]
    public void Parse_WithSparseAndMalformedOptionalAttributes_UsesSafeDefaults()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <dataTypeList>
    <array>
      <subrange lowerLimit=""bad"" upperLimit=""also-bad""/>
    </array>
    <struct>
      <varDeclaration signed=""0"">
        <conditionalSupport/>
        <defaultValue/>
        <allowedValues><value/></allowedValues>
        <unit/>
      </varDeclaration>
    </struct>
    <enum>
      <enumValue/>
      <NOT_A_SIMPLE_TYPE/>
    </enum>
    <derived>
      <count>
        <allowedValues><value/></allowedValues>
      </count>
    </derived>
  </dataTypeList>
  <functionTypeList>
    <functionType>
      <versionInfo/>
      <interfaceList>
        <inputVars><varDeclaration/></inputVars>
      </interfaceList>
      <functionInstanceList>
        <functionInstance/>
        <connection/>
      </functionInstanceList>
    </functionType>
  </functionTypeList>
  <templateList>
    <parameterTemplate persistent=""false"">
      <conditionalSupport/>
      <actualValue/>
      <substituteValue/>
      <allowedValues><value/></allowedValues>
      <unit/>
      <property/>
    </parameterTemplate>
    <allowedValuesTemplate>
      <value/>
      <range><minValue/><maxValue/></range>
    </allowedValuesTemplate>
  </templateList>
  <parameterList>
    <parameter persistent=""false"">
      <variableRef position=""not-a-byte"">
        <instanceIDRef/>
        <variableIDRef/>
        <memberRef index=""not-an-int""/>
      </variableRef>
      <conditionalSupport/>
      <denotation>
        <label/>
        <description/>
        <labelRef dictID="""" textID=""""/>
        <descriptionRef dictID="""" textID=""""/>
      </denotation>
      <actualValue/>
      <allowedValues><range/></allowedValues>
      <unit/>
      <property/>
    </parameter>
  </parameterList>
  <parameterGroupList>
    <parameterGroup>
      <parameterRef/>
      <parameterGroup/>
    </parameterGroup>
  </parameterGroupList>
</ApplicationProcess>");

        var ap = result.ApplicationProcess!;

        var arr = ap.DataTypeList!.Arrays[0];
        arr.Name.Should().BeEmpty();
        arr.UniqueId.Should().BeEmpty();
        arr.Subranges[0].LowerLimit.Should().Be(0);
        arr.Subranges[0].UpperLimit.Should().Be(0);

        var structVar = ap.DataTypeList.Structs[0].VarDeclarations[0];
        structVar.Name.Should().BeEmpty();
        structVar.UniqueId.Should().BeEmpty();
        structVar.IsSigned.Should().BeFalse();
        structVar.ConditionalSupports.Should().BeEmpty();
        structVar.DefaultValue!.Value.Should().BeEmpty();
        structVar.AllowedValues!.Values[0].Value.Should().BeEmpty();
        structVar.Unit!.Multiplier.Should().BeEmpty();

        var en = ap.DataTypeList.Enums[0];
        en.Name.Should().BeEmpty();
        en.UniqueId.Should().BeEmpty();
        en.EnumValues[0].Value.Should().BeNull();
        en.SimpleTypeName.Should().BeNull();

        var derivedCount = ap.DataTypeList.Derived[0].Count!;
        derivedCount.UniqueId.Should().BeEmpty();
        derivedCount.Access.Should().Be("read");
        derivedCount.AllowedValues!.Values[0].Value.Should().BeEmpty();

        var ft = ap.FunctionTypeList[0];
        ft.Name.Should().BeEmpty();
        ft.UniqueId.Should().BeEmpty();
        ft.Package.Should().BeNull();
        ft.VersionInfos[0].Organization.Should().BeEmpty();
        ft.FunctionInstanceList!.FunctionInstances[0].Name.Should().BeEmpty();
        ft.FunctionInstanceList.Connections[0].Source.Should().BeEmpty();
        ft.FunctionInstanceList.Connections[0].Destination.Should().BeEmpty();
        ft.FunctionInstanceList.Connections[0].Description.Should().BeNull();

        var pt = ap.TemplateList!.ParameterTemplates[0];
        pt.UniqueId.Should().BeEmpty();
        pt.Persistent.Should().BeFalse();
        pt.ConditionalSupports.Should().BeEmpty();
        pt.ActualValue!.Value.Should().BeEmpty();
        pt.SubstituteValue!.Value.Should().BeEmpty();
        pt.AllowedValues!.Values[0].Value.Should().BeEmpty();
        pt.Unit!.Multiplier.Should().BeEmpty();
        pt.Properties[0].Name.Should().BeEmpty();
        pt.Properties[0].Value.Should().BeEmpty();

        var avt = ap.TemplateList.AllowedValuesTemplates[0];
        avt.UniqueId.Should().BeEmpty();
        avt.Values[0].Value.Should().BeEmpty();
        avt.Ranges[0].MinValue.Should().NotBeNull();
        avt.Ranges[0].MinValue!.Value.Should().BeEmpty();
        avt.Ranges[0].MaxValue.Should().NotBeNull();
        avt.Ranges[0].MaxValue!.Value.Should().BeEmpty();
        avt.Ranges[0].Step.Should().BeNull();

        var parameter = ap.ParameterList[0];
        parameter.UniqueId.Should().BeEmpty();
        parameter.Persistent.Should().BeFalse();
        parameter.ConditionalSupports.Should().BeEmpty();
        parameter.ActualValue!.Value.Should().BeEmpty();
        parameter.TypeRef.Should().BeNull();
        parameter.Properties[0].Name.Should().BeEmpty();
        parameter.Properties[0].Value.Should().BeEmpty();
        parameter.Unit!.Multiplier.Should().BeEmpty();
        parameter.Denotation!.Labels[0].Text.Should().BeEmpty();
        parameter.Denotation.Descriptions[0].Text.Should().BeEmpty();
        parameter.Denotation.TextRefs.Should().HaveCount(2);
        parameter.Denotation.TextRefs[0].Uri.Should().BeNull();
        parameter.Denotation.TextRefs[1].Uri.Should().BeNull();
        // <range/> has no minValue/maxValue children → both are null after making them nullable
        parameter.AllowedValues!.Ranges[0].MinValue.Should().BeNull();
        parameter.AllowedValues.Ranges[0].MaxValue.Should().BeNull();
        parameter.AllowedValues.Ranges[0].Step.Should().BeNull();

        var variableRef = parameter.VariableRefs[0];
        variableRef.Position.Should().Be(1);
        variableRef.InstanceIdRefs.Should().BeEmpty();
        variableRef.VariableIdRef.Should().BeEmpty();
        variableRef.MemberRef.Should().NotBeNull();
        variableRef.MemberRef!.UniqueIdRef.Should().BeNull();
        variableRef.MemberRef.Index.Should().BeNull();

        var pg = ap.ParameterGroupList[0];
        pg.UniqueId.Should().BeEmpty();
        pg.KindOfAccess.Should().BeNull();
        pg.ParameterRefs.Should().BeEmpty();
        pg.SubGroups.Should().ContainSingle();
        pg.SubGroups[0].UniqueId.Should().BeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Reader — dataTypeList / enum
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_EnumType_WithSizeAndValues()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <dataTypeList>
    <enum name=""OnOff"" uniqueID=""uid_enum1"" size=""2"">
      <USINT/>
      <enumValue value=""0""><label lang=""en"">Off</label></enumValue>
      <enumValue value=""1""><label lang=""en"">On</label></enumValue>
    </enum>
  </dataTypeList>
  <parameterList/>
</ApplicationProcess>");

        var en = result.ApplicationProcess!.DataTypeList!.Enums[0];
        en.Name.Should().Be("OnOff");
        en.UniqueId.Should().Be("uid_enum1");
        en.Size.Should().Be("2");
        en.SimpleTypeName.Should().Be("USINT");
        en.EnumValues.Should().HaveCount(2);
        en.EnumValues[0].Value.Should().Be("0");
        en.EnumValues[0].LabelGroup.Labels[0].Text.Should().Be("Off");
        en.EnumValues[1].Value.Should().Be("1");
        en.EnumValues[1].LabelGroup.Labels[0].Text.Should().Be("On");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Reader — dataTypeList / derived
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_DerivedType_WithCountAndBaseType()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <dataTypeList>
    <derived name=""NodeList"" uniqueID=""uid_der1"">
      <count uniqueID=""uid_cnt1"" access=""readWrite"">
        <defaultValue value=""3""/>
      </count>
      <USINT/>
    </derived>
  </dataTypeList>
  <parameterList/>
</ApplicationProcess>");

        var dt = result.ApplicationProcess!.DataTypeList!.Derived[0];
        dt.Name.Should().Be("NodeList");
        dt.UniqueId.Should().Be("uid_der1");
        dt.Count.Should().NotBeNull();
        dt.Count!.UniqueId.Should().Be("uid_cnt1");
        dt.Count.Access.Should().Be("readWrite");
        dt.Count.DefaultValue!.Value.Should().Be("3");
        dt.BaseType!.SimpleTypeName.Should().Be("USINT");
    }

    [Fact]
    public void Parse_DerivedType_CountWithAllowedValues_IsParsed()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <dataTypeList>
    <derived name=""NodeList2"" uniqueID=""uid_der2"">
      <count uniqueID=""uid_cnt2"">
        <allowedValues>
          <value value=""1""/>
          <range>
            <minValue value=""1""/>
            <maxValue value=""10""/>
          </range>
        </allowedValues>
      </count>
      <USINT/>
    </derived>
  </dataTypeList>
  <parameterList/>
</ApplicationProcess>");

        var count = result.ApplicationProcess!.DataTypeList!.Derived[0].Count!;
        count.AllowedValues.Should().NotBeNull();
        count.AllowedValues!.Values.Should().ContainSingle();
        count.AllowedValues.Values[0].Value.Should().Be("1");
        count.AllowedValues.Ranges.Should().ContainSingle();
        count.AllowedValues.Ranges[0].MinValue!.Value.Should().Be("1");
        count.AllowedValues.Ranges[0].MaxValue!.Value.Should().Be("10");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Reader — LabelGroup
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_LabelGroup_ParsesAllTextElementTypes()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <dataTypeList>
    <struct name=""S"" uniqueID=""uid_s"">
      <label lang=""en"">My Struct</label>
      <label lang=""de"">Meine Struktur</label>
      <description lang=""en"">A test structure</description>
      <labelRef dictID=""dict1"" textID=""text1""/>
      <descriptionRef dictID=""dict2"" textID=""text2""/>
    </struct>
  </dataTypeList>
  <parameterList/>
</ApplicationProcess>");

        var lg = result.ApplicationProcess!.DataTypeList!.Structs[0].LabelGroup;
        lg.Labels.Should().HaveCount(2);
        lg.Labels[0].Lang.Should().Be("en");
        lg.Labels[0].Text.Should().Be("My Struct");
        lg.Labels[1].Lang.Should().Be("de");

        lg.Descriptions.Should().HaveCount(1);
        lg.Descriptions[0].Lang.Should().Be("en");
        lg.Descriptions[0].Text.Should().Be("A test structure");

        lg.TextRefs.Should().HaveCount(2);
        lg.TextRefs[0].DictId.Should().Be("dict1");
        lg.TextRefs[0].TextId.Should().Be("text1");
        lg.TextRefs[0].IsDescriptionRef.Should().BeFalse();
        lg.TextRefs[1].DictId.Should().Be("dict2");
        lg.TextRefs[1].IsDescriptionRef.Should().BeTrue();
    }

    [Fact]
    public void Parse_LabelGroup_GetDisplayName_PrefersEnglish()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <dataTypeList>
    <struct name=""S"" uniqueID=""uid_s"">
      <label lang=""de"">Deutsch</label>
      <label lang=""en"">English</label>
    </struct>
  </dataTypeList>
  <parameterList/>
</ApplicationProcess>");

        var lg = result.ApplicationProcess!.DataTypeList!.Structs[0].LabelGroup;
        lg.GetDisplayName().Should().Be("English");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Reader — functionTypeList
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_FunctionType_WithVersionInfoAndInterfaceList()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <functionTypeList>
    <functionType name=""Ramp"" uniqueID=""uid_ft1"" package=""myPkg"">
      <label lang=""en"">Ramp Function</label>
      <versionInfo organization=""TestOrg"" version=""1.0"" author=""Dev"" date=""2024-01-01""/>
      <interfaceList>
        <inputVars>
          <varDeclaration name=""Target"" uniqueID=""uid_in1""><DINT/></varDeclaration>
          <varDeclaration name=""Rate"" uniqueID=""uid_in2""><REAL/></varDeclaration>
        </inputVars>
        <outputVars>
          <varDeclaration name=""Current"" uniqueID=""uid_out1""><DINT/></varDeclaration>
        </outputVars>
        <configVars>
          <varDeclaration name=""MaxStep"" uniqueID=""uid_cfg1""><UINT/></varDeclaration>
        </configVars>
      </interfaceList>
    </functionType>
  </functionTypeList>
  <parameterList/>
</ApplicationProcess>");

        var ft = result.ApplicationProcess!.FunctionTypeList[0];
        ft.Name.Should().Be("Ramp");
        ft.UniqueId.Should().Be("uid_ft1");
        ft.Package.Should().Be("myPkg");
        ft.LabelGroup.GetDisplayName().Should().Be("Ramp Function");

        ft.VersionInfos.Should().HaveCount(1);
        ft.VersionInfos[0].Organization.Should().Be("TestOrg");
        ft.VersionInfos[0].Version.Should().Be("1.0");
        ft.VersionInfos[0].Author.Should().Be("Dev");
        ft.VersionInfos[0].Date.Should().Be("2024-01-01");

        ft.InterfaceList.Should().NotBeNull();
        ft.InterfaceList!.InputVars.Should().HaveCount(2);
        ft.InterfaceList.InputVars[0].Name.Should().Be("Target");
        ft.InterfaceList.InputVars[0].Type!.SimpleTypeName.Should().Be("DINT");
        ft.InterfaceList.OutputVars.Should().HaveCount(1);
        ft.InterfaceList.OutputVars[0].Name.Should().Be("Current");
        ft.InterfaceList.ConfigVars.Should().HaveCount(1);
        ft.InterfaceList.ConfigVars[0].Name.Should().Be("MaxStep");
    }

    [Fact]
    public void Parse_FunctionType_WithNestedFunctionInstanceList_IsParsed()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <functionTypeList>
    <functionType name=""Linker"" uniqueID=""uid_ft_link"">
      <functionInstanceList>
        <functionInstance name=""fi1"" uniqueID=""uid_fi1"" typeIDRef=""uid_ft_link""/>
        <connection source=""fi1.Out"" destination=""fi1.In"" description=""self-loop""/>
      </functionInstanceList>
    </functionType>
  </functionTypeList>
  <parameterList/>
</ApplicationProcess>");

        var fil = result.ApplicationProcess!.FunctionTypeList[0].FunctionInstanceList;
        fil.Should().NotBeNull();
        fil!.FunctionInstances.Should().ContainSingle();
        fil.FunctionInstances[0].Name.Should().Be("fi1");
        fil.Connections.Should().ContainSingle();
        fil.Connections[0].Description.Should().Be("self-loop");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Reader — functionInstanceList
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_FunctionInstanceList_WithInstancesAndConnection()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <functionInstanceList>
    <functionInstance name=""ramp1"" uniqueID=""uid_fi1"" typeIDRef=""uid_ft1""/>
    <functionInstance name=""ramp2"" uniqueID=""uid_fi2"" typeIDRef=""uid_ft1""/>
    <connection source=""ramp1.Current"" destination=""ramp2.Target"" description=""link""/>
  </functionInstanceList>
  <parameterList/>
</ApplicationProcess>");

        var fil = result.ApplicationProcess!.FunctionInstanceList;
        fil.Should().NotBeNull();
        fil!.FunctionInstances.Should().HaveCount(2);
        fil.FunctionInstances[0].Name.Should().Be("ramp1");
        fil.FunctionInstances[0].UniqueId.Should().Be("uid_fi1");
        fil.FunctionInstances[0].TypeIdRef.Should().Be("uid_ft1");
        fil.Connections.Should().HaveCount(1);
        fil.Connections[0].Source.Should().Be("ramp1.Current");
        fil.Connections[0].Destination.Should().Be("ramp2.Target");
        fil.Connections[0].Description.Should().Be("link");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Reader — templateList
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_TemplateList_ParameterTemplateWithAllowedRange()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <templateList>
    <parameterTemplate uniqueID=""uid_tpl1"" access=""readWrite"" persistent=""true"">
      <label lang=""en"">Speed Tpl</label>
      <UINT/>
      <defaultValue value=""100""/>
      <allowedValues>
        <range>
          <minValue value=""0""/>
          <maxValue value=""1000""/>
          <step value=""10""/>
        </range>
      </allowedValues>
      <unit multiplier=""1.0"">
        <label lang=""en"">rpm</label>
      </unit>
      <property name=""Category"" value=""Motion""/>
    </parameterTemplate>
    <allowedValuesTemplate uniqueID=""uid_avt1"">
      <value value=""0""/>
      <value value=""1""/>
      <value value=""2""/>
    </allowedValuesTemplate>
  </templateList>
  <parameterList/>
</ApplicationProcess>");

        var tl = result.ApplicationProcess!.TemplateList;
        tl.Should().NotBeNull();
        tl!.ParameterTemplates.Should().HaveCount(1);
        var pt = tl.ParameterTemplates[0];
        pt.UniqueId.Should().Be("uid_tpl1");
        pt.Access.Should().Be("readWrite");
        pt.Persistent.Should().BeTrue();
        pt.LabelGroup.GetDisplayName().Should().Be("Speed Tpl");
        pt.TypeRef!.SimpleTypeName.Should().Be("UINT");
        pt.DefaultValue!.Value.Should().Be("100");
        pt.AllowedValues.Should().NotBeNull();
        pt.AllowedValues!.Ranges.Should().HaveCount(1);
        pt.AllowedValues.Ranges[0].MinValue!.Value.Should().Be("0");
        pt.AllowedValues.Ranges[0].MaxValue!.Value.Should().Be("1000");
        pt.AllowedValues.Ranges[0].Step!.Value.Should().Be("10");
        pt.Unit.Should().NotBeNull();
        pt.Unit!.Multiplier.Should().Be("1.0");
        pt.Unit.LabelGroup.GetDisplayName().Should().Be("rpm");
        pt.Properties.Should().HaveCount(1);
        pt.Properties[0].Name.Should().Be("Category");
        pt.Properties[0].Value.Should().Be("Motion");

        tl.AllowedValuesTemplates.Should().HaveCount(1);
        var avt = tl.AllowedValuesTemplates[0];
        avt.UniqueId.Should().Be("uid_avt1");
        avt.Values.Should().HaveCount(3);
        avt.Values[2].Value.Should().Be("2");
    }

    [Fact]
    public void Parse_TemplateList_ParameterTemplate_WithConditionalSupportActualAndSubstituteValues()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <templateList>
    <parameterTemplate uniqueID=""uid_tpl2"">
      <dataTypeIDRef uniqueIDRef=""uid_type_1""/>
      <conditionalSupport paramIDRef=""uid_dep_1""/>
      <actualValue value=""11""/>
      <substituteValue value=""0""/>
    </parameterTemplate>
  </templateList>
  <parameterList/>
</ApplicationProcess>");

        var pt = result.ApplicationProcess!.TemplateList!.ParameterTemplates[0];
        pt.TypeRef.Should().NotBeNull();
        pt.TypeRef!.DataTypeIdRef.Should().Be("uid_type_1");
        pt.ConditionalSupports.Should().ContainSingle().Which.Should().Be("uid_dep_1");
        pt.ActualValue.Should().NotBeNull();
        pt.ActualValue!.Value.Should().Be("11");
        pt.SubstituteValue.Should().NotBeNull();
        pt.SubstituteValue!.Value.Should().Be("0");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Reader — parameterList
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_Parameter_WithAllCommonAttributes()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <parameterList>
    <parameter uniqueID=""uid_p1"" access=""readWrite"" accessList=""1 2"" support=""optional""
               persistent=""true"" offset=""5"" multiplier=""2.0"" templateIDRef=""uid_tpl1"">
      <label lang=""en"">My Param</label>
      <DINT/>
    </parameter>
  </parameterList>
</ApplicationProcess>");

        var p = result.ApplicationProcess!.ParameterList[0];
        p.UniqueId.Should().Be("uid_p1");
        p.Access.Should().Be("readWrite");
        p.AccessList.Should().Be("1 2");
        p.Support.Should().Be("optional");
        p.Persistent.Should().BeTrue();
        p.Offset.Should().Be("5");
        p.Multiplier.Should().Be("2.0");
        p.TemplateIdRef.Should().Be("uid_tpl1");
        p.LabelGroup.GetDisplayName().Should().Be("My Param");
        p.TypeRef!.SimpleTypeName.Should().Be("DINT");
    }

    [Fact]
    public void Parse_Parameter_WithConditionalSupportAndActualValue_AndWithoutTypeRef()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <parameterList>
    <parameter uniqueID=""uid_p_cond"" access=""readWrite"">
      <conditionalSupport paramIDRef=""uid_dep_p""/>
      <actualValue value=""77""/>
    </parameter>
  </parameterList>
</ApplicationProcess>");

        var p = result.ApplicationProcess!.ParameterList[0];
        p.TypeRef.Should().BeNull();
        p.ConditionalSupports.Should().ContainSingle().Which.Should().Be("uid_dep_p");
        p.ActualValue.Should().NotBeNull();
        p.ActualValue!.Value.Should().Be("77");
    }

    [Fact]
    public void Parse_Parameter_WithDataTypeRefMissingUniqueId_ReturnsNullTypeRef()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <parameterList>
    <parameter uniqueID=""uid_p_missing_ref"" access=""read"">
      <dataTypeIDRef/>
    </parameter>
  </parameterList>
</ApplicationProcess>");

        result.ApplicationProcess!.ParameterList[0].TypeRef.Should().BeNull();
    }

    [Theory]
    [InlineData("BOOL")]
    [InlineData("SINT")]
    [InlineData("INT")]
    [InlineData("DINT")]
    [InlineData("LINT")]
    [InlineData("USINT")]
    [InlineData("UINT")]
    [InlineData("UDINT")]
    [InlineData("ULINT")]
    [InlineData("REAL")]
    [InlineData("LREAL")]
    [InlineData("BYTE")]
    [InlineData("WORD")]
    [InlineData("DWORD")]
    [InlineData("LWORD")]
    [InlineData("STRING")]
    [InlineData("WSTRING")]
    [InlineData("CHAR")]
    [InlineData("WCHAR")]
    [InlineData("TIME")]
    [InlineData("DATE")]
    [InlineData("DATE_AND_TIME")]
    [InlineData("TIME_OF_DAY")]
    [InlineData("BITSTRING")]
    public void Parse_TypeRef_AllKnownSimpleTypes_AreRecognized(string simpleTypeName)
    {
        var result = ParseAp($@"
<ApplicationProcess>
  <parameterList>
    <parameter uniqueID=""uid_t"" access=""read"">
      <{simpleTypeName}/>
    </parameter>
  </parameterList>
</ApplicationProcess>");

        result.ApplicationProcess!.ParameterList[0].TypeRef!.SimpleTypeName.Should().Be(simpleTypeName);
    }

    [Fact]
    public void Parse_Parameter_WithDefaultAllowedValuesAndUnit()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <parameterList>
    <parameter uniqueID=""uid_p2"" access=""read"">
      <UINT/>
      <defaultValue value=""42""/>
      <substituteValue value=""0""/>
      <allowedValues>
        <value value=""10""/>
        <value value=""42""/>
        <value value=""100""/>
      </allowedValues>
      <unit multiplier=""0.001"" unitURI=""urn:example:ms"">
        <label lang=""en"">ms</label>
      </unit>
      <property name=""Tag"" value=""Timing""/>
      <property name=""Group"" value=""Config""/>
    </parameter>
  </parameterList>
</ApplicationProcess>");

        var p = result.ApplicationProcess!.ParameterList[0];
        p.DefaultValue!.Value.Should().Be("42");
        p.SubstituteValue!.Value.Should().Be("0");
        p.AllowedValues.Should().NotBeNull();
        p.AllowedValues!.Values.Should().HaveCount(3);
        p.AllowedValues.Values[1].Value.Should().Be("42");
        p.Unit!.Multiplier.Should().Be("0.001");
        p.Unit.UnitUri.Should().Be("urn:example:ms");
        p.Unit.LabelGroup.GetDisplayName().Should().Be("ms");
        p.Properties.Should().HaveCount(2);
        p.Properties[0].Name.Should().Be("Tag");
        p.Properties[0].Value.Should().Be("Timing");
    }

    [Fact]
    public void Parse_Parameter_WithAllowedValuesTemplateIdRef()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <parameterList>
    <parameter uniqueID=""uid_p3"" access=""read"">
      <UINT/>
      <allowedValues templateIDRef=""uid_avt1""/>
    </parameter>
  </parameterList>
</ApplicationProcess>");

        var p = result.ApplicationProcess!.ParameterList[0];
        p.AllowedValues!.TemplateIdRef.Should().Be("uid_avt1");
        p.AllowedValues.Values.Should().BeEmpty();
    }

    [Fact]
    public void Parse_Parameter_WithVariableRef()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <parameterList>
    <parameter uniqueID=""uid_p4"" access=""read"">
      <DINT/>
      <variableRef position=""2"">
        <instanceIDRef uniqueIDRef=""uid_fi1""/>
        <instanceIDRef uniqueIDRef=""uid_fi2""/>
        <variableIDRef uniqueIDRef=""uid_var1""/>
        <memberRef uniqueIDRef=""uid_vd1"" index=""3""/>
      </variableRef>
    </parameter>
  </parameterList>
</ApplicationProcess>");

        var vr = result.ApplicationProcess!.ParameterList[0].VariableRefs[0];
        vr.Position.Should().Be(2);
        vr.InstanceIdRefs.Should().HaveCount(2);
        vr.InstanceIdRefs[0].Should().Be("uid_fi1");
        vr.InstanceIdRefs[1].Should().Be("uid_fi2");
        vr.VariableIdRef.Should().Be("uid_var1");
        vr.MemberRef.Should().NotBeNull();
        vr.MemberRef!.UniqueIdRef.Should().Be("uid_vd1");
        vr.MemberRef.Index.Should().Be(3);
    }

    [Fact]
    public void Parse_Parameter_WithDenotation()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <parameterList>
    <parameter uniqueID=""uid_p5"" access=""read"">
      <UINT/>
      <denotation>
        <label lang=""en"">Speed set-point</label>
      </denotation>
    </parameter>
  </parameterList>
</ApplicationProcess>");

        var p = result.ApplicationProcess!.ParameterList[0];
        p.Denotation.Should().NotBeNull();
        p.Denotation!.Labels[0].Text.Should().Be("Speed set-point");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Reader — parameterGroupList
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_ParameterGroup_WithNestedGroupsAndRefs()
    {
        var result = ParseAp(@"
<ApplicationProcess>
  <parameterList>
    <parameter uniqueID=""uid_p1"" access=""read""><UINT/></parameter>
    <parameter uniqueID=""uid_p2"" access=""read""><UINT/></parameter>
    <parameter uniqueID=""uid_p3"" access=""read""><UINT/></parameter>
  </parameterList>
  <parameterGroupList>
    <parameterGroup uniqueID=""uid_pg1"" kindOfAccess=""read"">
      <label lang=""en"">Group A</label>
      <parameterRef uniqueIDRef=""uid_p1""/>
      <parameterGroup uniqueID=""uid_pg2"">
        <parameterRef uniqueIDRef=""uid_p2""/>
        <parameterRef uniqueIDRef=""uid_p3""/>
      </parameterGroup>
    </parameterGroup>
  </parameterGroupList>
</ApplicationProcess>");

        var ap = result.ApplicationProcess!;
        ap.ParameterGroupList.Should().HaveCount(1);

        var pg = ap.ParameterGroupList[0];
        pg.UniqueId.Should().Be("uid_pg1");
        pg.KindOfAccess.Should().Be("read");
        pg.LabelGroup.GetDisplayName().Should().Be("Group A");
        pg.ParameterRefs.Should().HaveCount(1);
        pg.ParameterRefs[0].Should().Be("uid_p1");

        pg.SubGroups.Should().HaveCount(1);
        var sub = pg.SubGroups[0];
        sub.UniqueId.Should().Be("uid_pg2");
        sub.ParameterRefs.Should().HaveCount(2);
        sub.ParameterRefs[0].Should().Be("uid_p2");
        sub.ParameterRefs[1].Should().Be("uid_p3");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Writer — generates correct XML elements
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Write_NullApplicationProcess_OmitsElement()
    {
        var eds = CreateBaseEds();
        eds.ApplicationProcess = null;

        var xml = Writer.GenerateString(eds);

        xml.Should().NotContain("ApplicationProcess");
        var act = () => XDocument.Parse(xml);
        act.Should().NotThrow();
    }

    [Fact]
    public void Write_StructType_EmitsNameAndUniqueId()
    {
        var eds = CreateBaseEds();
        var ap = new ApplicationProcess();
        var dtl = new ApDataTypeList();
        dtl.Structs.Add(new ApStructType { Name = "SomeStruct", UniqueId = "uid_ss" });
        ap.DataTypeList = dtl;
        eds.ApplicationProcess = ap;

        var xml = Writer.GenerateString(eds);

        xml.Should().Contain("SomeStruct");
        xml.Should().Contain("uid_ss");
        xml.Should().Contain("dataTypeList");
        xml.Should().Contain("struct");
    }

    [Fact]
    public void Write_ArrayType_EmitsSubrangeAndElementType()
    {
        var eds = CreateBaseEds();
        var ap = new ApplicationProcess();
        var dtl = new ApDataTypeList();
        dtl.Arrays.Add(new ApArrayType
        {
            Name = "MyArr",
            UniqueId = "uid_a",
            Subranges = { new ApSubrange { LowerLimit = 0, UpperLimit = 7 } },
            ElementType = new ApTypeRef { SimpleTypeName = "BYTE" },
        });
        ap.DataTypeList = dtl;
        eds.ApplicationProcess = ap;

        var xml = Writer.GenerateString(eds);

        xml.Should().Contain("MyArr");
        xml.Should().Contain("lowerLimit=\"0\"");
        xml.Should().Contain("upperLimit=\"7\"");
        xml.Should().Contain("<BYTE");
    }

    [Fact]
    public void Write_EnumType_EmitsValuesAndSimpleType()
    {
        var eds = CreateBaseEds();
        var ap = new ApplicationProcess();
        var dtl = new ApDataTypeList();
        var en = new ApEnumType { Name = "MyEnum", UniqueId = "uid_en", Size = "2", SimpleTypeName = "USINT" };
        en.EnumValues.Add(new ApEnumValue { Value = "0" });
        en.EnumValues.Add(new ApEnumValue { Value = "1" });
        dtl.Enums.Add(en);
        ap.DataTypeList = dtl;
        eds.ApplicationProcess = ap;

        var xml = Writer.GenerateString(eds);

        xml.Should().Contain("MyEnum");
        xml.Should().Contain("size=\"2\"");
        xml.Should().Contain("<USINT");
        xml.Should().Contain("enumValue");
        xml.Should().Contain("value=\"0\"");
        xml.Should().Contain("value=\"1\"");
    }

    [Fact]
    public void Write_DerivedType_EmitsCountAndBaseType()
    {
        var eds = CreateBaseEds();
        var ap = new ApplicationProcess();
        var dtl = new ApDataTypeList();
        var dt = new ApDerivedType
        {
            Name = "MyDerived",
            UniqueId = "uid_d",
            Count = new ApDerivedCount
            {
                UniqueId = "uid_cnt",
                Access = "readWrite",
                DefaultValue = new ApParameterValue { Value = "5" },
            },
            BaseType = new ApTypeRef { SimpleTypeName = "UINT" },
        };
        dtl.Derived.Add(dt);
        ap.DataTypeList = dtl;
        eds.ApplicationProcess = ap;

        var xml = Writer.GenerateString(eds);

        xml.Should().Contain("MyDerived");
        xml.Should().Contain("count");
        xml.Should().Contain("uid_cnt");
        xml.Should().Contain("value=\"5\"");
        xml.Should().Contain("<UINT");
    }

    [Fact]
    public void Write_Parameter_EmitsAccessAndTypeRef()
    {
        var eds = CreateBaseEds();
        var ap = new ApplicationProcess();
        ap.ParameterList.Add(new ApParameter
        {
            UniqueId = "uid_p1",
            Access = "readWrite",
            TypeRef = new ApTypeRef { SimpleTypeName = "INT" },
            DefaultValue = new ApParameterValue { Value = "0" },
        });
        eds.ApplicationProcess = ap;

        var xml = Writer.GenerateString(eds);

        xml.Should().Contain("uid_p1");
        xml.Should().Contain("access=\"readWrite\"");
        xml.Should().Contain("<INT");
        xml.Should().Contain("defaultValue");
        xml.Should().Contain("value=\"0\"");
    }

    [Fact]
    public void Write_ParameterGroup_EmitsGroupAndRefs()
    {
        var eds = CreateBaseEds();
        var ap = new ApplicationProcess();
        ap.ParameterList.Add(new ApParameter { UniqueId = "uid_p1", Access = "read" });

        var pg = new ApParameterGroup { UniqueId = "uid_pg1", KindOfAccess = "read" };
        pg.ParameterRefs.Add("uid_p1");
        ap.ParameterGroupList.Add(pg);
        eds.ApplicationProcess = ap;

        var xml = Writer.GenerateString(eds);

        xml.Should().Contain("parameterGroup");
        xml.Should().Contain("uid_pg1");
        xml.Should().Contain("kindOfAccess=\"read\"");
        xml.Should().Contain("parameterRef");
        xml.Should().Contain("uid_p1");
    }

    [Fact]
    public void Write_LabelGroup_EmitsLabelElements()
    {
        var eds = CreateBaseEds();
        var ap = new ApplicationProcess();
        var param = new ApParameter { UniqueId = "uid_p1", Access = "read" };
        param.LabelGroup.Labels.Add(new ApLabel { Lang = "en", Text = "Speed" });
        param.LabelGroup.Labels.Add(new ApLabel { Lang = "de", Text = "Drehzahl" });
        ap.ParameterList.Add(param);
        eds.ApplicationProcess = ap;

        var xml = Writer.GenerateString(eds);

        xml.Should().Contain("lang=\"en\"");
        xml.Should().Contain("Speed");
        xml.Should().Contain("lang=\"de\"");
        xml.Should().Contain("Drehzahl");
    }

    [Fact]
    public void Write_AllowedValues_EmitsRangeWithMinMaxStep()
    {
        var eds = CreateBaseEds();
        var ap = new ApplicationProcess();
        var p = new ApParameter { UniqueId = "uid_p1", Access = "read" };
        p.AllowedValues = new ApAllowedValues();
        p.AllowedValues.Ranges.Add(new ApAllowedRange
        {
            MinValue = new ApParameterValue { Value = "0" },
            MaxValue = new ApParameterValue { Value = "100" },
            Step = new ApParameterValue { Value = "5" },
        });
        ap.ParameterList.Add(p);
        eds.ApplicationProcess = ap;

        var xml = Writer.GenerateString(eds);

        xml.Should().Contain("range");
        xml.Should().Contain("minValue");
        xml.Should().Contain("maxValue");
        xml.Should().Contain("step");
    }

    [Fact]
    public void Write_ComplexApplicationProcess_EmitsFunctionTemplateAndParameterDetails()
    {
        var eds = CreateBaseEds();
        var ap = new ApplicationProcess();

        var dataTypeList = new ApDataTypeList();
        var derived = new ApDerivedType
        {
            Name = "DerivedCounter",
            UniqueId = "uid_der_writer",
            Count = new ApDerivedCount
            {
                UniqueId = "uid_cnt_writer",
                AllowedValues = new ApAllowedValues(),
            },
            BaseType = new ApTypeRef { DataTypeIdRef = "uid_base_type" },
        };
        derived.Count!.AllowedValues!.Values.Add(new ApParameterValue { Value = "1" });
        dataTypeList.Derived.Add(derived);
        ap.DataTypeList = dataTypeList;

        var functionType = new ApFunctionType
        {
            Name = "Controller",
            UniqueId = "uid_ft_writer",
            Package = "pkg.ctrl",
            InterfaceList = new ApInterfaceList(),
            FunctionInstanceList = new ApFunctionInstanceList(),
        };
        functionType.VersionInfos.Add(new ApVersionInfo
        {
            Organization = "Org",
            Version = "1.0",
            Author = "Dev",
            Date = "2025-01-01",
        });
        var cfgVar = new ApVarDeclaration
        {
            Name = "Cfg",
            UniqueId = "uid_cfg_writer",
            Start = "0",
            Size = "16",
            IsSigned = false,
            Offset = "1",
            Multiplier = "2",
            InitialValue = "3",
            Type = new ApTypeRef { SimpleTypeName = "UINT" },
            DefaultValue = new ApParameterValue { Value = "4", Offset = "1", Multiplier = "2" },
            AllowedValues = new ApAllowedValues(),
            Unit = new ApUnit { Multiplier = "1.0", UnitUri = "urn:example:cfg" },
        };
        cfgVar.ConditionalSupports.Add("uid_dep_cfg");
        cfgVar.AllowedValues!.Values.Add(new ApParameterValue { Value = "4" });
        functionType.InterfaceList.ConfigVars.Add(cfgVar);
        var nestedInstance = new ApFunctionInstance
        {
            Name = "fiNested",
            UniqueId = "uid_fi_nested",
            TypeIdRef = "uid_ft_writer",
        };
        nestedInstance.LabelGroup.Labels.Add(new ApLabel { Lang = "en", Text = "Nested" });
        functionType.FunctionInstanceList.FunctionInstances.Add(nestedInstance);
        functionType.FunctionInstanceList.Connections.Add(new ApConnection
        {
            Source = "fiNested.Out",
            Destination = "fiNested.In",
            Description = "loop",
        });
        ap.FunctionTypeList.Add(functionType);

        ap.FunctionInstanceList = new ApFunctionInstanceList();
        ap.FunctionInstanceList.FunctionInstances.Add(new ApFunctionInstance
        {
            Name = "fiTop",
            UniqueId = "uid_fi_top",
            TypeIdRef = "uid_ft_writer",
        });
        ap.FunctionInstanceList.Connections.Add(new ApConnection
        {
            Source = "fiTop.Out",
            Destination = "fiTop.In",
            Description = "top-loop",
        });

        ap.TemplateList = new ApTemplateList();
        var parameterTemplate = new ApParameterTemplate
        {
            UniqueId = "uid_tpl_writer",
            Access = "readWrite",
            AccessList = "read write",
            Support = "optional",
            Persistent = true,
            Offset = "10",
            Multiplier = "0.5",
            TypeRef = new ApTypeRef { DataTypeIdRef = "uid_data_type_writer" },
            ActualValue = new ApParameterValue { Value = "12", Offset = "1", Multiplier = "3" },
            DefaultValue = new ApParameterValue { Value = "8" },
            SubstituteValue = new ApParameterValue { Value = "0" },
            AllowedValues = new ApAllowedValues { TemplateIdRef = "uid_avt_writer" },
            Unit = new ApUnit { Multiplier = "1.0", UnitUri = "urn:example:rpm" },
        };
        parameterTemplate.ConditionalSupports.Add("uid_dep_tpl");
        parameterTemplate.AllowedValues!.Values.Add(new ApParameterValue { Value = "8" });
        parameterTemplate.AllowedValues.Ranges.Add(new ApAllowedRange
        {
            MinValue = new ApParameterValue { Value = "0" },
            MaxValue = new ApParameterValue { Value = "100" },
            Step = new ApParameterValue { Value = "5" },
        });
        parameterTemplate.Properties.Add(new ApProperty { Name = "Category", Value = "Motion" });
        parameterTemplate.LabelGroup.Labels.Add(new ApLabel { Lang = "en", Text = "Template Label" });
        parameterTemplate.LabelGroup.Descriptions.Add(new ApDescription
        {
            Lang = "en",
            Text = "Template description",
            Uri = "https://example.com/template",
        });
        parameterTemplate.LabelGroup.TextRefs.Add(new ApTextRef
        {
            DictId = "dict1",
            TextId = "txt1",
            Uri = "urn:label",
        });
        parameterTemplate.LabelGroup.TextRefs.Add(new ApTextRef
        {
            DictId = "dict1",
            TextId = "txt2",
            IsDescriptionRef = true,
            Uri = "urn:desc",
        });
        ap.TemplateList.ParameterTemplates.Add(parameterTemplate);
        var allowedValuesTemplate = new ApAllowedValuesTemplate { UniqueId = "uid_avt_writer" };
        allowedValuesTemplate.Values.Add(new ApParameterValue { Value = "1" });
        allowedValuesTemplate.Ranges.Add(new ApAllowedRange
        {
            MinValue = new ApParameterValue { Value = "1" },
            MaxValue = new ApParameterValue { Value = "9" },
            Step = new ApParameterValue { Value = "1" },
        });
        ap.TemplateList.AllowedValuesTemplates.Add(allowedValuesTemplate);

        var parameter = new ApParameter
        {
            UniqueId = "uid_param_writer",
            Access = "write",
            AccessList = "write",
            Support = "mandatory",
            Persistent = true,
            Offset = "2",
            Multiplier = "4",
            TemplateIdRef = "uid_tpl_writer",
            TypeRef = new ApTypeRef { DataTypeIdRef = "uid_data_type_writer" },
            Denotation = new ApLabelGroup(),
            ActualValue = new ApParameterValue { Value = "15", Offset = "1", Multiplier = "2" },
            DefaultValue = new ApParameterValue { Value = "10" },
            SubstituteValue = new ApParameterValue { Value = "0" },
            AllowedValues = new ApAllowedValues { TemplateIdRef = "uid_avt_writer" },
            Unit = new ApUnit { Multiplier = "0.1", UnitUri = "urn:example:param" },
        };
        parameter.ConditionalSupports.Add("uid_cond_param");
        parameter.Denotation!.Labels.Add(new ApLabel { Lang = "de", Text = "Sollwert" });
        parameter.AllowedValues!.Values.Add(new ApParameterValue { Value = "10" });
        parameter.Properties.Add(new ApProperty { Name = "Group", Value = "Runtime" });
        parameter.LabelGroup.Labels.Add(new ApLabel { Lang = "en", Text = "Parameter Label" });
        parameter.LabelGroup.Descriptions.Add(new ApDescription
        {
            Lang = "en",
            Text = "Parameter description",
            Uri = "https://example.com/parameter",
        });
        parameter.LabelGroup.TextRefs.Add(new ApTextRef
        {
            DictId = "dictP",
            TextId = "txtP",
            Uri = "urn:param-label",
        });
        var variableRef = new ApVariableRef
        {
            Position = 2,
            VariableIdRef = "uid_var_writer",
            MemberRef = new ApMemberRef { UniqueIdRef = "uid_member_writer", Index = 3 },
        };
        variableRef.InstanceIdRefs.Add("uid_inst_writer");
        parameter.VariableRefs.Add(variableRef);
        ap.ParameterList.Add(parameter);

        var rootGroup = new ApParameterGroup
        {
            UniqueId = "uid_pg_root",
            KindOfAccess = "read",
        };
        rootGroup.ParameterRefs.Add("uid_param_writer");
        rootGroup.SubGroups.Add(new ApParameterGroup
        {
            UniqueId = "uid_pg_child",
            ParameterRefs = { "uid_param_writer" },
        });
        ap.ParameterGroupList.Add(rootGroup);

        eds.ApplicationProcess = ap;

        var xml = Writer.GenerateString(eds);

        xml.Should().Contain("functionTypeList");
        xml.Should().Contain("package=\"pkg.ctrl\"");
        xml.Should().Contain("configVars");
        xml.Should().Contain("paramIDRef=\"uid_dep_cfg\"");
        xml.Should().Contain("functionInstance name=\"fiTop\"");
        xml.Should().Contain("description=\"top-loop\"");
        xml.Should().Contain("templateList");
        xml.Should().Contain("parameterTemplate");
        xml.Should().Contain("templateIDRef=\"uid_avt_writer\"");
        xml.Should().Contain("actualValue value=\"12\"");
        xml.Should().Contain("descriptionRef");
        xml.Should().Contain("urn:desc");
        xml.Should().Contain("labelRef");
        xml.Should().Contain("uid_param_writer");
        xml.Should().Contain("templateIDRef=\"uid_tpl_writer\"");
        xml.Should().Contain("variableRef position=\"2\"");
        xml.Should().Contain("instanceIDRef");
        xml.Should().Contain("memberRef uniqueIDRef=\"uid_member_writer\" index=\"3\"");
        xml.Should().Contain("denotation");
        xml.Should().Contain("unitURI=\"urn:example:param\"");
        xml.Should().Contain("parameterGroup uniqueID=\"uid_pg_root\"");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Round-trip tests — parse → write → parse; key fields survive
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RoundTrip_DataTypeList_StructAndEnum()
    {
        const string apXml = @"
<ApplicationProcess>
  <dataTypeList>
    <struct name=""Cfg"" uniqueID=""uid_str1"">
      <label lang=""en"">Config</label>
      <varDeclaration name=""Enable"" uniqueID=""uid_v1""><BOOL/></varDeclaration>
      <varDeclaration name=""Level"" uniqueID=""uid_v2"" signed=""true""><INT/></varDeclaration>
    </struct>
    <enum name=""Mode"" uniqueID=""uid_enum1"" size=""1"">
      <USINT/>
      <enumValue value=""0""><label lang=""en"">Off</label></enumValue>
      <enumValue value=""1""><label lang=""en"">On</label></enumValue>
    </enum>
  </dataTypeList>
  <parameterList/>
</ApplicationProcess>";

        var eds1 = ParseAp(apXml);
        var written = Writer.GenerateString(eds1);
        var eds2 = Reader.ReadString(written);

        var ap = eds2.ApplicationProcess!;
        ap.DataTypeList!.Structs.Should().HaveCount(1);
        var st = ap.DataTypeList.Structs[0];
        st.Name.Should().Be("Cfg");
        st.UniqueId.Should().Be("uid_str1");
        st.LabelGroup.GetDisplayName().Should().Be("Config");
        st.VarDeclarations.Should().HaveCount(2);
        st.VarDeclarations[0].Name.Should().Be("Enable");
        st.VarDeclarations[0].Type!.SimpleTypeName.Should().Be("BOOL");
        st.VarDeclarations[1].IsSigned.Should().BeTrue();

        ap.DataTypeList.Enums.Should().HaveCount(1);
        var en = ap.DataTypeList.Enums[0];
        en.Name.Should().Be("Mode");
        en.Size.Should().Be("1");
        en.SimpleTypeName.Should().Be("USINT");
        en.EnumValues.Should().HaveCount(2);
        en.EnumValues[0].LabelGroup.GetDisplayName().Should().Be("Off");
    }

    [Fact]
    public void RoundTrip_Parameter_WithAllowedValuesAndUnit()
    {
        const string apXml = @"
<ApplicationProcess>
  <parameterList>
    <parameter uniqueID=""uid_spd"" access=""readWrite"" persistent=""true"">
      <label lang=""en"">Target Speed</label>
      <UINT/>
      <defaultValue value=""500""/>
      <allowedValues>
        <range>
          <minValue value=""0""/>
          <maxValue value=""3000""/>
        </range>
      </allowedValues>
      <unit multiplier=""1.0"" unitURI=""urn:example:rpm"">
        <label lang=""en"">rpm</label>
      </unit>
      <property name=""Group"" value=""Motion""/>
    </parameter>
  </parameterList>
</ApplicationProcess>";

        var eds1 = ParseAp(apXml);
        var written = Writer.GenerateString(eds1);
        var eds2 = Reader.ReadString(written);

        var p = eds2.ApplicationProcess!.ParameterList[0];
        p.UniqueId.Should().Be("uid_spd");
        p.Access.Should().Be("readWrite");
        p.Persistent.Should().BeTrue();
        p.LabelGroup.GetDisplayName().Should().Be("Target Speed");
        p.TypeRef!.SimpleTypeName.Should().Be("UINT");
        p.DefaultValue!.Value.Should().Be("500");
        p.AllowedValues!.Ranges[0].MinValue!.Value.Should().Be("0");
        p.AllowedValues.Ranges[0].MaxValue!.Value.Should().Be("3000");
        p.Unit!.Multiplier.Should().Be("1.0");
        p.Unit.UnitUri.Should().Be("urn:example:rpm");
        p.Unit.LabelGroup.GetDisplayName().Should().Be("rpm");
        p.Properties[0].Name.Should().Be("Group");
        p.Properties[0].Value.Should().Be("Motion");
    }

    [Fact]
    public void RoundTrip_ParameterGroup_Nested()
    {
        const string apXml = @"
<ApplicationProcess>
  <parameterList>
    <parameter uniqueID=""uid_p1"" access=""read""><UINT/></parameter>
    <parameter uniqueID=""uid_p2"" access=""read""><UINT/></parameter>
    <parameter uniqueID=""uid_p3"" access=""read""><UINT/></parameter>
  </parameterList>
  <parameterGroupList>
    <parameterGroup uniqueID=""uid_root"" kindOfAccess=""read"">
      <label lang=""en"">Root</label>
      <parameterRef uniqueIDRef=""uid_p1""/>
      <parameterGroup uniqueID=""uid_child"">
        <parameterRef uniqueIDRef=""uid_p2""/>
        <parameterRef uniqueIDRef=""uid_p3""/>
      </parameterGroup>
    </parameterGroup>
  </parameterGroupList>
</ApplicationProcess>";

        var eds1 = ParseAp(apXml);
        var written = Writer.GenerateString(eds1);
        var eds2 = Reader.ReadString(written);

        var ap = eds2.ApplicationProcess!;
        ap.ParameterGroupList.Should().HaveCount(1);
        var root = ap.ParameterGroupList[0];
        root.UniqueId.Should().Be("uid_root");
        root.KindOfAccess.Should().Be("read");
        root.LabelGroup.GetDisplayName().Should().Be("Root");
        root.ParameterRefs.Should().HaveCount(1);
        root.ParameterRefs[0].Should().Be("uid_p1");
        root.SubGroups.Should().HaveCount(1);
        var child = root.SubGroups[0];
        child.UniqueId.Should().Be("uid_child");
        child.ParameterRefs.Should().HaveCount(2);
    }

    [Fact]
    public void RoundTrip_FunctionType_WithInterfaceList()
    {
        const string apXml = @"
<ApplicationProcess>
  <functionTypeList>
    <functionType name=""Filter"" uniqueID=""uid_ft1"" package=""Pkg"">
      <versionInfo organization=""Org"" version=""2.0"" author=""A"" date=""2025-01-01""/>
      <interfaceList>
        <inputVars>
          <varDeclaration name=""Raw"" uniqueID=""uid_in""><REAL/></varDeclaration>
        </inputVars>
        <outputVars>
          <varDeclaration name=""Filtered"" uniqueID=""uid_out""><REAL/></varDeclaration>
        </outputVars>
      </interfaceList>
    </functionType>
  </functionTypeList>
  <parameterList/>
</ApplicationProcess>";

        var eds1 = ParseAp(apXml);
        var written = Writer.GenerateString(eds1);
        var eds2 = Reader.ReadString(written);

        var ft = eds2.ApplicationProcess!.FunctionTypeList[0];
        ft.Name.Should().Be("Filter");
        ft.UniqueId.Should().Be("uid_ft1");
        ft.Package.Should().Be("Pkg");
        ft.VersionInfos[0].Organization.Should().Be("Org");
        ft.VersionInfos[0].Version.Should().Be("2.0");
        ft.InterfaceList!.InputVars[0].Name.Should().Be("Raw");
        ft.InterfaceList.OutputVars[0].Name.Should().Be("Filtered");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Branch-coverage gap closers — targeted tests for partial branches
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_Subrange_MissingLowerAndUpperLimit_DefaultsToZero()
    {
        // subrange with no lowerLimit/upperLimit attributes → TryParse(null) → false → stay at 0
        var result = ParseAp(@"
<ApplicationProcess>
  <dataTypeList>
    <array name=""A"" uniqueID=""uid_a"">
      <subrange/>
      <UINT/>
    </array>
  </dataTypeList>
  <parameterList/>
</ApplicationProcess>");

        var sr = result.ApplicationProcess!.DataTypeList!.Arrays[0].Subranges[0];
        sr.LowerLimit.Should().Be(0);
        sr.UpperLimit.Should().Be(0);
    }

    [Fact]
    public void Parse_ParameterTemplate_WithAccessListSupportOffsetMultiplier_ParsesCorrectly()
    {
        // parameterTemplate with accessList, support, offset, and multiplier attributes present
        var result = ParseAp(@"
<ApplicationProcess>
  <templateList>
    <parameterTemplate uniqueID=""uid_tpl3"" access=""readWrite""
                       accessList=""1 2 3"" support=""optional""
                       offset=""10"" multiplier=""0.5"">
      <UINT/>
    </parameterTemplate>
  </templateList>
  <parameterList/>
</ApplicationProcess>");

        var pt = result.ApplicationProcess!.TemplateList!.ParameterTemplates[0];
        pt.AccessList.Should().Be("1 2 3");
        pt.Support.Should().Be("optional");
        pt.Offset.Should().Be("10");
        pt.Multiplier.Should().Be("0.5");
    }

    [Fact]
    public void Parse_VariableRef_WithoutPosition_DefaultsToOne()
    {
        // variableRef with no position attribute → posStr is null → Position stays at default 1
        var result = ParseAp(@"
<ApplicationProcess>
  <parameterList>
    <parameter uniqueID=""uid_p"" access=""read"">
      <UINT/>
      <variableRef>
        <variableIDRef uniqueIDRef=""uid_var1""/>
      </variableRef>
    </parameter>
  </parameterList>
</ApplicationProcess>");

        var vr = result.ApplicationProcess!.ParameterList[0].VariableRefs[0];
        vr.Position.Should().Be(1);
        vr.VariableIdRef.Should().Be("uid_var1");
    }

    [Fact]
    public void Parse_MemberRef_WithoutIndex_LeavesIndexNull()
    {
        // memberRef with no index attribute → idxStr is null → MemberRef.Index stays null
        var result = ParseAp(@"
<ApplicationProcess>
  <parameterList>
    <parameter uniqueID=""uid_p"" access=""read"">
      <UINT/>
      <variableRef>
        <variableIDRef uniqueIDRef=""uid_var1""/>
        <memberRef uniqueIDRef=""uid_member1""/>
      </variableRef>
    </parameter>
  </parameterList>
</ApplicationProcess>");

        var mr = result.ApplicationProcess!.ParameterList[0].VariableRefs[0].MemberRef!;
        mr.UniqueIdRef.Should().Be("uid_member1");
        mr.Index.Should().BeNull();
    }

    [Fact]
    public void Parse_ParameterValue_WithOffsetAndMultiplier_ParsesCorrectly()
    {
        // defaultValue/value elements with offset and multiplier attributes (non-null paths)
        var result = ParseAp(@"
<ApplicationProcess>
  <parameterList>
    <parameter uniqueID=""uid_p"" access=""read"">
      <UINT/>
      <defaultValue value=""42"" offset=""5"" multiplier=""2.0""/>
      <allowedValues>
        <value value=""10"" offset=""1"" multiplier=""0.5""/>
      </allowedValues>
    </parameter>
  </parameterList>
</ApplicationProcess>");

        var p = result.ApplicationProcess!.ParameterList[0];
        p.DefaultValue!.Value.Should().Be("42");
        p.DefaultValue.Offset.Should().Be("5");
        p.DefaultValue.Multiplier.Should().Be("2.0");
        p.AllowedValues!.Values[0].Offset.Should().Be("1");
        p.AllowedValues.Values[0].Multiplier.Should().Be("0.5");
    }

    [Fact]
    public void Parse_LabelGroup_UriContent_InLabelAndDescriptionRefs()
    {
        // labelRef/descriptionRef with non-empty text content → used as URI (true branch of ternary)
        // description with URI attribute → non-null URI path
        var result = ParseAp(@"
<ApplicationProcess>
  <dataTypeList>
    <struct name=""S"" uniqueID=""uid_s"">
      <description lang=""en"" URI=""urn:struct-desc"">A structure</description>
      <labelRef dictID=""d1"" textID=""t1"">urn:label-uri</labelRef>
      <descriptionRef dictID=""d2"" textID=""t2"">urn:desc-uri</descriptionRef>
    </struct>
  </dataTypeList>
  <parameterList/>
</ApplicationProcess>");

        var lg = result.ApplicationProcess!.DataTypeList!.Structs[0].LabelGroup;
        lg.Descriptions[0].Uri.Should().Be("urn:struct-desc");
        lg.TextRefs[0].Uri.Should().Be("urn:label-uri");
        lg.TextRefs[0].IsDescriptionRef.Should().BeFalse();
        lg.TextRefs[1].Uri.Should().Be("urn:desc-uri");
        lg.TextRefs[1].IsDescriptionRef.Should().BeTrue();
    }

    [Fact]
    public void Parse_LabelGroup_LabelRefWithoutDictIdOrTextId_DefaultsToEmpty()
    {
        // labelRef/descriptionRef with no dictID or textID attributes → null paths → default to empty
        var result = ParseAp(@"
<ApplicationProcess>
  <dataTypeList>
    <struct name=""S"" uniqueID=""uid_s"">
      <labelRef/>
      <descriptionRef/>
    </struct>
  </dataTypeList>
  <parameterList/>
</ApplicationProcess>");

        var lg = result.ApplicationProcess!.DataTypeList!.Structs[0].LabelGroup;
        lg.TextRefs.Should().HaveCount(2);
        lg.TextRefs[0].DictId.Should().BeEmpty();
        lg.TextRefs[0].TextId.Should().BeEmpty();
        lg.TextRefs[0].Uri.Should().BeNull();
        lg.TextRefs[1].IsDescriptionRef.Should().BeTrue();
        lg.TextRefs[1].Uri.Should().BeNull();
    }
}
