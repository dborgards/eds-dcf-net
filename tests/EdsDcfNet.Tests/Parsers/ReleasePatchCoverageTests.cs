namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet.Parsers;

/// <summary>
/// Closes the branch gaps the release patch left in the module section name classifier
/// and the XDD <c>objFlags</c> reader.
/// </summary>
public class ReleasePatchCoverageTests
{
    [Theory]
    [InlineData("M1SubExt/000")]
    [InlineData("M1SubExt:000")]
    [InlineData("M1SubExt@000")]
    [InlineData("M1SubExt[000")]
    [InlineData("M1SubExt`000")]
    [InlineData("M1SubExt{000")]
    [InlineData("M1SubExt00G0")]
    [InlineData("M1SubExt00g0")]
    [InlineData("M1Fixed100G")]
    [InlineData("M1Fixed100g")]
    [InlineData("M1Fixed1000subG")]
    [InlineData("M1Fixed1000subg")]
    [InlineData("M1Fixed1000sub:")]
    [InlineData("M1Fixed1000sub@")]
    [InlineData("M1Fixed1000sub`")]
    [InlineData("M1FixedG000sub1")]
    public void TryClassifyModuleSection_NonHexCharacterAdjacentToHexRange_IsRejected(string sectionName)
    {
        // Act
        var classified = CanOpenSectionParsers.TryClassifyModuleSection(sectionName, out _, out _);

        // Assert
        classified.Should().BeFalse();
    }

    [Theory]
    [InlineData("M1SubExt00aF", true)]
    [InlineData("M1Fixed09aF", false)]
    public void TryClassifyModuleSection_HexBoundaryCharacters_IsAccepted(string sectionName, bool isSubExtension)
    {
        // Act
        var classified = CanOpenSectionParsers.TryClassifyModuleSection(sectionName, out var moduleNumber, out var kind);

        // Assert
        classified.Should().BeTrue();
        moduleNumber.Should().Be(1);
        kind.Should().Be(isSubExtension ? ModuleSectionKind.SubExtension : ModuleSectionKind.FixedObject);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ReadString_EmptyObjFlagsAttribute_LeavesObjFlagsUnset(string objFlags)
    {
        // Arrange
        var xml = ProfileWithObjFlags(objFlags);

        // Act
        var result = CanOpenFile.Xdd.ReadStringWithDiagnostics(xml);

        // Assert
        result.Model.ObjectDictionary.Objects[0x1000].ObjFlags.Should().Be(0u);
    }

    private static string ProfileWithObjFlags(string objFlags) =>
        @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <DeviceIdentity><vendorName>T</vendorName><vendorID>0x1</vendorID><productName>T</productName><productID>0x1</productID></DeviceIdentity>
      <DeviceManager/><DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007""
                         accessType=""ro"" PDOmapping=""no"" objFlags=""" + objFlags + @"""/>
        </CANopenObjectList>
      </ApplicationLayers>
      <TransportLayers><PhysicalLayer><baudRate defaultValue=""250 Kbps""/></PhysicalLayer></TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""0"" nrOfTxPDO=""0""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";
}
