namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;

/// <summary>
/// Round-trip tests: Read EDS -> Write EDS string -> Read back -> verify equality.
/// Ensures parser and writer symmetry for core EDS features.
/// </summary>
public class RoundTripEdsTests
{
    [Fact]
    public void RoundTrip_SampleEds_PreservesFileInfo()
    {
        // Arrange
        var original = CanOpenFile.ReadEds("Fixtures/sample_device.eds");

        // Act
        var edsString = CanOpenFile.WriteEdsToString(original);
        var roundTripped = CanOpenFile.ReadEdsFromString(edsString);

        // Assert
        roundTripped.FileInfo.FileName.Should().Be(original.FileInfo.FileName);
        roundTripped.FileInfo.FileVersion.Should().Be(original.FileInfo.FileVersion);
        roundTripped.FileInfo.FileRevision.Should().Be(original.FileInfo.FileRevision);
        roundTripped.FileInfo.EdsVersion.Should().Be(original.FileInfo.EdsVersion);
    }

    [Fact]
    public void RoundTrip_SampleEds_PreservesDeviceInfoAndObjectLists()
    {
        // Arrange
        var original = CanOpenFile.ReadEds("Fixtures/sample_device.eds");

        // Act
        var edsString = CanOpenFile.WriteEdsToString(original);
        var roundTripped = CanOpenFile.ReadEdsFromString(edsString);

        // Assert
        roundTripped.DeviceInfo.VendorName.Should().Be(original.DeviceInfo.VendorName);
        roundTripped.DeviceInfo.ProductName.Should().Be(original.DeviceInfo.ProductName);
        roundTripped.ObjectDictionary.MandatoryObjects.Should()
            .BeEquivalentTo(original.ObjectDictionary.MandatoryObjects);
        roundTripped.ObjectDictionary.OptionalObjects.Should()
            .BeEquivalentTo(original.ObjectDictionary.OptionalObjects);
    }

    [Fact]
    public void RoundTrip_SampleEds_PreservesSubObjectsAndComments()
    {
        // Arrange
        var original = CanOpenFile.ReadEds("Fixtures/sample_device.eds");

        // Act
        var edsString = CanOpenFile.WriteEdsToString(original);
        var roundTripped = CanOpenFile.ReadEdsFromString(edsString);

        // Assert
        roundTripped.ObjectDictionary.Objects[0x1018].SubObjects[1].DefaultValue.Should()
            .Be(original.ObjectDictionary.Objects[0x1018].SubObjects[1].DefaultValue);
        roundTripped.Comments.Should().NotBeNull();
        roundTripped.Comments!.CommentLines.Should()
            .BeEquivalentTo(original.Comments!.CommentLines);
    }

    [Fact]
    public void RoundTrip_UnknownSections_ArePreserved()
    {
        // Arrange
        var content = @"
[FileInfo]
FileName=test.eds

[DeviceInfo]
VendorName=Test Vendor
ProductName=Test Product

[MandatoryObjects]
SupportedObjects=1
1=0x1000

[1000]
ParameterName=Device Type
ObjectType=0x7
DataType=0x0007
AccessType=ro
PDOMapping=0

[VendorSpecificSection]
Foo=Bar
";

        // Act
        var original = CanOpenFile.ReadEdsFromString(content);
        var roundTripped = CanOpenFile.ReadEdsFromString(CanOpenFile.WriteEdsToString(original));

        // Assert
        roundTripped.AdditionalSections.Should().ContainKey("VendorSpecificSection");
        roundTripped.AdditionalSections["VendorSpecificSection"]["Foo"].Should().Be("Bar");
    }

    [Fact]
    public void RoundTrip_DcfSpecificFields_AreNotWritten()
    {
        // Arrange
        var content = @"
[FileInfo]
FileName=test.eds

[DeviceInfo]
VendorName=Test Vendor
ProductName=Test Product

[MandatoryObjects]
SupportedObjects=1
1=0x1000

[1000]
ParameterName=Device Type
ObjectType=0x7
DataType=0x0007
AccessType=ro
PDOMapping=0
";

        var eds = CanOpenFile.ReadEdsFromString(content);
        eds.ObjectDictionary.Objects[0x1000].ParameterValue = "0x123";
        eds.ObjectDictionary.Objects[0x1000].Denotation = "Configured";
        eds.ObjectDictionary.Objects[0x1000].ParamRefd = "X1.A1";

        // Act
        var written = CanOpenFile.WriteEdsToString(eds);

        // Assert
        written.Should().NotContain("ParameterValue=");
        written.Should().NotContain("Denotation=");
        written.Should().NotContain("ParamRefd=");
    }
}
