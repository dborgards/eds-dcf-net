namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;
using EdsDcfNet.Models;

/// <summary>
/// Round-trip tests: Read DCF → Write DCF string → Read back → verify equality.
/// Ensures the parser and writer are symmetric for all major features.
/// </summary>
public class RoundTripDcfTests
{
    #region Minimal DCF Round-Trip

    [Fact]
    public void RoundTrip_MinimalDcf_PreservesCommissioning()
    {
        // Arrange
        var original = CanOpenFile.ReadDcf("Fixtures/minimal.dcf");

        // Act
        var dcfString = CanOpenFile.WriteDcfToString(original);
        var roundTripped = CanOpenFile.ReadDcfFromString(dcfString);

        // Assert
        roundTripped.DeviceCommissioning.NodeId.Should().Be(original.DeviceCommissioning.NodeId);
        roundTripped.DeviceCommissioning.Baudrate.Should().Be(original.DeviceCommissioning.Baudrate);
        roundTripped.DeviceCommissioning.NodeName.Should().Be(original.DeviceCommissioning.NodeName);
        roundTripped.DeviceCommissioning.NetNumber.Should().Be(original.DeviceCommissioning.NetNumber);
        roundTripped.DeviceCommissioning.NetworkName.Should().Be(original.DeviceCommissioning.NetworkName);
        roundTripped.DeviceCommissioning.CANopenManager.Should().Be(original.DeviceCommissioning.CANopenManager);
    }

    [Fact]
    public void RoundTrip_MinimalDcf_PreservesFileInfo()
    {
        // Arrange
        var original = CanOpenFile.ReadDcf("Fixtures/minimal.dcf");

        // Act
        var dcfString = CanOpenFile.WriteDcfToString(original);
        var roundTripped = CanOpenFile.ReadDcfFromString(dcfString);

        // Assert
        roundTripped.FileInfo.FileName.Should().Be(original.FileInfo.FileName);
        roundTripped.FileInfo.FileVersion.Should().Be(original.FileInfo.FileVersion);
        roundTripped.FileInfo.FileRevision.Should().Be(original.FileInfo.FileRevision);
        roundTripped.FileInfo.EdsVersion.Should().Be(original.FileInfo.EdsVersion);
        roundTripped.FileInfo.Description.Should().Be(original.FileInfo.Description);
    }

    [Fact]
    public void RoundTrip_MinimalDcf_PreservesObjectLists()
    {
        // Arrange
        var original = CanOpenFile.ReadDcf("Fixtures/minimal.dcf");

        // Act
        var dcfString = CanOpenFile.WriteDcfToString(original);
        var roundTripped = CanOpenFile.ReadDcfFromString(dcfString);

        // Assert
        roundTripped.ObjectDictionary.MandatoryObjects.Should()
            .BeEquivalentTo(original.ObjectDictionary.MandatoryObjects);
        roundTripped.ObjectDictionary.Objects.Keys.Should()
            .BeEquivalentTo(original.ObjectDictionary.Objects.Keys);
    }

    #endregion

    #region Full Features Round-Trip

    [Fact]
    public void RoundTrip_FullFeaturesDcf_PreservesDeviceInfo()
    {
        // Arrange
        var original = CanOpenFile.ReadDcf("Fixtures/full_features.dcf");

        // Act
        var dcfString = CanOpenFile.WriteDcfToString(original);
        var roundTripped = CanOpenFile.ReadDcfFromString(dcfString);

        // Assert
        roundTripped.DeviceInfo.VendorName.Should().Be(original.DeviceInfo.VendorName);
        roundTripped.DeviceInfo.ProductName.Should().Be(original.DeviceInfo.ProductName);
        roundTripped.DeviceInfo.VendorNumber.Should().Be(original.DeviceInfo.VendorNumber);
        roundTripped.DeviceInfo.ProductNumber.Should().Be(original.DeviceInfo.ProductNumber);
        roundTripped.DeviceInfo.LssSupported.Should().Be(original.DeviceInfo.LssSupported);
    }

    [Fact]
    public void RoundTrip_FullFeaturesDcf_PreservesLssSerialNumber()
    {
        // Arrange
        var original = CanOpenFile.ReadDcf("Fixtures/full_features.dcf");

        // Act
        var dcfString = CanOpenFile.WriteDcfToString(original);
        var roundTripped = CanOpenFile.ReadDcfFromString(dcfString);

        // Assert
        roundTripped.DeviceCommissioning.LssSerialNumber.Should()
            .Be(original.DeviceCommissioning.LssSerialNumber);
        roundTripped.DeviceCommissioning.CANopenManager.Should()
            .Be(original.DeviceCommissioning.CANopenManager);
    }

    [Fact]
    public void RoundTrip_FullFeaturesDcf_PreservesDcfSpecificFields()
    {
        // Arrange
        var original = CanOpenFile.ReadDcf("Fixtures/full_features.dcf");

        // Act
        var dcfString = CanOpenFile.WriteDcfToString(original);
        var roundTripped = CanOpenFile.ReadDcfFromString(dcfString);

        // Assert – ParameterValue, UploadFile, DownloadFile on object 0x2000
        var origObj = original.ObjectDictionary.Objects[0x2000];
        var rtObj = roundTripped.ObjectDictionary.Objects[0x2000];
        rtObj.ParameterValue.Should().Be(origObj.ParameterValue);
        rtObj.UploadFile.Should().Be(origObj.UploadFile);
        rtObj.DownloadFile.Should().Be(origObj.DownloadFile);
        rtObj.ObjFlags.Should().Be(origObj.ObjFlags);
    }

    [Fact]
    public void RoundTrip_FullFeaturesDcf_PreservesSubObjectDcfFields()
    {
        // Arrange
        var original = CanOpenFile.ReadDcf("Fixtures/full_features.dcf");

        // Act
        var dcfString = CanOpenFile.WriteDcfToString(original);
        var roundTripped = CanOpenFile.ReadDcfFromString(dcfString);

        // Assert – sub1 of 0x1018 has ParameterValue and Denotation
        var origSub = original.ObjectDictionary.Objects[0x1018].SubObjects[1];
        var rtSub = roundTripped.ObjectDictionary.Objects[0x1018].SubObjects[1];
        rtSub.ParameterValue.Should().Be(origSub.ParameterValue);
        rtSub.Denotation.Should().Be(origSub.Denotation);
    }

    [Fact]
    public void RoundTrip_FullFeaturesDcf_PreservesDummyUsage()
    {
        // Arrange
        var original = CanOpenFile.ReadDcf("Fixtures/full_features.dcf");

        // Act
        var dcfString = CanOpenFile.WriteDcfToString(original);
        var roundTripped = CanOpenFile.ReadDcfFromString(dcfString);

        // Assert
        roundTripped.ObjectDictionary.DummyUsage.Should()
            .BeEquivalentTo(original.ObjectDictionary.DummyUsage);
    }

    [Fact]
    public void RoundTrip_FullFeaturesDcf_PreservesComments()
    {
        // Arrange
        var original = CanOpenFile.ReadDcf("Fixtures/full_features.dcf");

        // Act
        var dcfString = CanOpenFile.WriteDcfToString(original);
        var roundTripped = CanOpenFile.ReadDcfFromString(dcfString);

        // Assert
        roundTripped.Comments.Should().NotBeNull();
        roundTripped.Comments!.Lines.Should().Be(original.Comments!.Lines);
        roundTripped.Comments.CommentLines.Should()
            .BeEquivalentTo(original.Comments.CommentLines);
    }

    [Fact]
    public void RoundTrip_FullFeaturesDcf_PreservesObjectLinks()
    {
        // Arrange
        var original = CanOpenFile.ReadDcf("Fixtures/full_features.dcf");

        // Act
        var dcfString = CanOpenFile.WriteDcfToString(original);
        var roundTripped = CanOpenFile.ReadDcfFromString(dcfString);

        // Assert – ObjectLinks are parsed and attached to the object
        var origLinks = original.ObjectDictionary.Objects[0x2000].ObjectLinks;
        var rtLinks = roundTripped.ObjectDictionary.Objects[0x2000].ObjectLinks;
        rtLinks.Should().BeEquivalentTo(origLinks);
        
        // ObjectLinks sections for existing objects are emitted via the object itself
        // and are filtered from AdditionalSections to avoid duplicates.
        roundTripped.AdditionalSections.Should().NotContainKey("2000ObjectLinks");
    }

    [Fact]
    public void RoundTrip_FullFeaturesDcf_PreservesUnknownSections()
    {
        // Arrange
        var original = CanOpenFile.ReadDcf("Fixtures/full_features.dcf");

        // Act
        var dcfString = CanOpenFile.WriteDcfToString(original);
        var roundTripped = CanOpenFile.ReadDcfFromString(dcfString);

        // Assert
        roundTripped.AdditionalSections.Should().ContainKey("VendorSpecificSection");
        roundTripped.AdditionalSections["VendorSpecificSection"]
            .Should().BeEquivalentTo(original.AdditionalSections["VendorSpecificSection"]);
    }

    #endregion

    #region Modular Device Round-Trip

    [Fact]
    public void RoundTrip_ModularDcf_PreservesSupportedModules()
    {
        // Arrange
        var original = CanOpenFile.ReadDcf("Fixtures/modular_device.dcf");

        // Act
        var dcfString = CanOpenFile.WriteDcfToString(original);
        var roundTripped = CanOpenFile.ReadDcfFromString(dcfString);

        // Assert
        roundTripped.SupportedModules.Should().HaveCount(original.SupportedModules.Count);

        for (int i = 0; i < original.SupportedModules.Count; i++)
        {
            var origMod = original.SupportedModules[i];
            var rtMod = roundTripped.SupportedModules[i];
            rtMod.ProductName.Should().Be(origMod.ProductName);
            rtMod.OrderCode.Should().Be(origMod.OrderCode);
            rtMod.ProductVersion.Should().Be(origMod.ProductVersion);
            rtMod.ProductRevision.Should().Be(origMod.ProductRevision);
            rtMod.FixedObjects.Should().BeEquivalentTo(origMod.FixedObjects);
        }
    }

    [Fact]
    public void RoundTrip_ModularDcf_PreservesConnectedModules()
    {
        // Arrange
        var original = CanOpenFile.ReadDcf("Fixtures/modular_device.dcf");

        // Act
        var dcfString = CanOpenFile.WriteDcfToString(original);
        var roundTripped = CanOpenFile.ReadDcfFromString(dcfString);

        // Assert
        roundTripped.ConnectedModules.Should()
            .BeEquivalentTo(original.ConnectedModules, opts => opts.WithStrictOrdering());
    }

    #endregion

    #region String-based Round-Trip

    [Fact]
    public void RoundTrip_FromString_ParameterValuesPreserved()
    {
        // Arrange
        var content = @"
[FileInfo]
FileName=roundtrip.dcf
FileVersion=1
FileRevision=0
EDSVersion=4.0

[DeviceInfo]
VendorName=RT Vendor
ProductName=RT Product
VendorNumber=0x100

[DeviceCommissioning]
NodeID=7
Baudrate=125
NodeName=RTNode

[MandatoryObjects]
SupportedObjects=1
1=0x1000

[1000]
ParameterName=Device Type
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0x191
ParameterValue=0x999
PDOMapping=0
";

        // Act
        var first = CanOpenFile.ReadDcfFromString(content);
        var written = CanOpenFile.WriteDcfToString(first);
        var second = CanOpenFile.ReadDcfFromString(written);

        // Assert
        second.ObjectDictionary.Objects[0x1000].ParameterValue.Should().Be("0x999");
        second.DeviceCommissioning.NodeId.Should().Be(7);
        second.DeviceCommissioning.Baudrate.Should().Be(125);
    }

    #endregion
}
