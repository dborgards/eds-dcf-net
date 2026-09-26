namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet;
using EdsDcfNet.Models;

/// <summary>
/// Unknown keys inside object and sub-object sections are kept on
/// <see cref="CanOpenObject.RemainingEntries"/> and survive write/read.
/// </summary>
public class RemainingEntriesTests
{
    [Fact]
    public void ReadString_EdsUnknownKeys_ArePreservedThroughWrite()
    {
        var content = @"
[DeviceInfo]
VendorName=Test Vendor

[MandatoryObjects]
SupportedObjects=1
1=0x1000

[1000]
ParameterName=Device Type
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0x191
PDOMapping=0

[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Vendor Object
Group=Motion
ObjectType=0x7
ParameterValue=eds-extension
DataType=0x0007
AccessType=rw
Lang-Bemerkung=Hinweis
PDOMAPPING=1
DefaultValue=0
SubNumber=1
ObjFlags=1

[2000sub1]
ParameterName=Vendor Sub
Group=SubGroup
ObjectType=0x7
Denotation=eds-sub-denotation
DataType=0x0005
AccessType=ro
Lang-Bemerkung=SubHinweis
PDOMapping=0
ObjFlags=2
";

        var eds = CanOpenFile.Eds.ReadString(content);

        eds.ObjectDictionary.Objects[0x1000].RemainingEntries.Should().BeEmpty();

        var obj = eds.ObjectDictionary.Objects[0x2000];
        obj.ParameterName.Should().Be("Vendor Object");
        obj.PdoMapping.Should().BeTrue();
        obj.ObjFlags.Should().Be(1);
        obj.ParameterValue.Should().BeNull();
        AssertEntries(
            obj.RemainingEntries,
            ("Group", "Motion"),
            ("ParameterValue", "eds-extension"),
            ("Lang-Bemerkung", "Hinweis"));
        obj.RemainingEntries.Should().NotContainKey("ParameterName");
        obj.RemainingEntries.Should().NotContainKey("PDOMAPPING");
        obj.RemainingEntries.Should().NotContainKey("ObjFlags");
        obj.RemainingEntries["group"].Should().Be("Motion");

        var sub = obj.SubObjects[1];
        sub.ParameterName.Should().Be("Vendor Sub");
        sub.Denotation.Should().BeNull();
        AssertEntries(
            sub.RemainingEntries,
            ("Group", "SubGroup"),
            ("Denotation", "eds-sub-denotation"),
            ("Lang-Bemerkung", "SubHinweis"),
            ("ObjFlags", "2"));
        sub.RemainingEntries.Should().NotContainKey("ParameterName");
        sub.RemainingEntries.Should().NotContainKey("PDOMapping");

        var written = CanOpenFile.Eds.WriteToString(eds);
        written.IndexOf("Group=Motion", StringComparison.Ordinal).Should().BeLessThan(
            written.IndexOf("ParameterValue=eds-extension", StringComparison.Ordinal));
        written.IndexOf("ParameterValue=eds-extension", StringComparison.Ordinal).Should().BeLessThan(
            written.IndexOf("Lang-Bemerkung=Hinweis", StringComparison.Ordinal));
        written.IndexOf("Group=SubGroup", StringComparison.Ordinal).Should().BeLessThan(
            written.IndexOf("Denotation=eds-sub-denotation", StringComparison.Ordinal));
        written.IndexOf("Denotation=eds-sub-denotation", StringComparison.Ordinal).Should().BeLessThan(
            written.IndexOf("Lang-Bemerkung=SubHinweis", StringComparison.Ordinal));
        written.Should().NotContain("PDOMAPPING=");

        var again = CanOpenFile.Eds.ReadString(written);
        var againObj = again.ObjectDictionary.Objects[0x2000];
        againObj.ParameterName.Should().Be("Vendor Object");
        againObj.PdoMapping.Should().BeTrue();
        againObj.ObjFlags.Should().Be(1);
        againObj.ParameterValue.Should().BeNull();
        AssertEntries(
            againObj.RemainingEntries,
            ("Group", "Motion"),
            ("ParameterValue", "eds-extension"),
            ("Lang-Bemerkung", "Hinweis"));

        var againSub = againObj.SubObjects[1];
        againSub.Denotation.Should().BeNull();
        AssertEntries(
            againSub.RemainingEntries,
            ("Group", "SubGroup"),
            ("Denotation", "eds-sub-denotation"),
            ("Lang-Bemerkung", "SubHinweis"),
            ("ObjFlags", "2"));
    }

    [Fact]
    public void ReadString_KnownAndUnknownKeysDifferentCasing_DoNotDoubleStore()
    {
        var content = @"
[DeviceInfo]
VendorName=Test Vendor

[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Vendor Object
parametername=Last Name
Group=First
group=Second
ObjectType=0x7
DataType=0x0007
AccessType=ro
PDOMapping=0
";

        var eds = CanOpenFile.Eds.ReadString(content);
        var obj = eds.ObjectDictionary.Objects[0x2000];

        obj.ParameterName.Should().Be("Last Name");
        obj.RemainingEntries.Should().ContainSingle();
        obj.RemainingEntries.Single().Key.Should().Be("Group");
        obj.RemainingEntries.Single().Value.Should().Be("Second");
        obj.RemainingEntries.Should().NotContainKey("ParameterName");
        obj.RemainingEntries.Should().NotContainKey("parametername");
        obj.RemainingEntries["GROUP"].Should().Be("Second");
    }

    [Fact]
    public void ReadString_DcfConfiguredValueKeys_StayOnPropertiesAndRoundTrip()
    {
        var content = @"
[DeviceInfo]
VendorName=Test Vendor

[DeviceCommissioning]
NodeID=5
NodeName=Node
Baudrate=250

[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Vendor Object
Group=Motion
ObjectType=0x7
ParameterValue=42
DataType=0x0007
AccessType=rw
Denotation=App
Lang-Bemerkung=Hinweis
PDOMapping=0
ParamRefd=X1
UploadFile=up.bin
DownloadFile=down.bin
SubNumber=1

[2000sub1]
ParameterName=Vendor Sub
Group=SubGroup
ObjectType=0x7
ParameterValue=7
DataType=0x0005
AccessType=ro
Denotation=AppSub
Lang-Bemerkung=SubHinweis
PDOMapping=0
ParamRefd=X1.1
";

        var dcf = CanOpenFile.Dcf.ReadString(content);
        var obj = dcf.ObjectDictionary.Objects[0x2000];

        obj.ParameterValue.Should().Be("42");
        obj.Denotation.Should().Be("App");
        obj.ParamRefd.Should().Be("X1");
        obj.UploadFile.Should().Be("up.bin");
        obj.DownloadFile.Should().Be("down.bin");
        AssertEntries(obj.RemainingEntries, ("Group", "Motion"), ("Lang-Bemerkung", "Hinweis"));
        obj.RemainingEntries.Should().NotContainKey("ParameterValue");
        obj.RemainingEntries.Should().NotContainKey("Denotation");
        obj.RemainingEntries.Should().NotContainKey("ParamRefd");
        obj.RemainingEntries.Should().NotContainKey("UploadFile");
        obj.RemainingEntries.Should().NotContainKey("DownloadFile");

        var sub = obj.SubObjects[1];
        sub.ParameterValue.Should().Be("7");
        sub.Denotation.Should().Be("AppSub");
        sub.ParamRefd.Should().Be("X1.1");
        AssertEntries(sub.RemainingEntries, ("Group", "SubGroup"), ("Lang-Bemerkung", "SubHinweis"));
        sub.RemainingEntries.Should().NotContainKey("ParameterValue");
        sub.RemainingEntries.Should().NotContainKey("Denotation");
        sub.RemainingEntries.Should().NotContainKey("ParamRefd");

        var written = CanOpenFile.Dcf.WriteToString(dcf);
        written.IndexOf("Group=Motion", StringComparison.Ordinal).Should().BeLessThan(
            written.IndexOf("Lang-Bemerkung=Hinweis", StringComparison.Ordinal));
        written.IndexOf("Group=SubGroup", StringComparison.Ordinal).Should().BeLessThan(
            written.IndexOf("Lang-Bemerkung=SubHinweis", StringComparison.Ordinal));

        var again = CanOpenFile.Dcf.ReadString(written);
        var againObj = again.ObjectDictionary.Objects[0x2000];
        againObj.ParameterValue.Should().Be("42");
        againObj.Denotation.Should().Be("App");
        againObj.ParamRefd.Should().Be("X1");
        againObj.UploadFile.Should().Be("up.bin");
        againObj.DownloadFile.Should().Be("down.bin");
        AssertEntries(againObj.RemainingEntries, ("Group", "Motion"), ("Lang-Bemerkung", "Hinweis"));

        var againSub = againObj.SubObjects[1];
        againSub.ParameterValue.Should().Be("7");
        againSub.Denotation.Should().Be("AppSub");
        againSub.ParamRefd.Should().Be("X1.1");
        AssertEntries(againSub.RemainingEntries, ("Group", "SubGroup"), ("Lang-Bemerkung", "SubHinweis"));
    }

    [Fact]
    public void WriteToString_CompactSubObjectUnknownKey_RoundTripsExpandedSection()
    {
        var content = @"
[DeviceInfo]
VendorName=Test Vendor

[MandatoryObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=StatusBits
Group=Motion
ObjectType=0x8
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=0
CompactSubObj=1

[2100sub1]
ParameterName=StatusBits1
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=0
Lang-Bemerkung=Sub note
";

        var eds = CanOpenFile.Eds.ReadString(content);
        var obj = eds.ObjectDictionary.Objects[0x2100];
        AssertEntries(obj.RemainingEntries, ("Group", "Motion"));
        obj.SubObjects[0].RemainingEntries.Should().BeEmpty();
        AssertEntries(obj.SubObjects[1].RemainingEntries, ("Lang-Bemerkung", "Sub note"));

        var written = CanOpenFile.Eds.WriteToString(eds);
        written.Should().Contain("[2100sub1]");
        written.Should().Contain("Lang-Bemerkung=Sub note");
        written.Should().Contain("Group=Motion");

        var again = CanOpenFile.Eds.ReadString(written);
        var againObj = again.ObjectDictionary.Objects[0x2100];
        againObj.CompactSubObj.Should().Be(1);
        AssertEntries(againObj.RemainingEntries, ("Group", "Motion"));
        againObj.SubObjects[0].RemainingEntries.Should().BeEmpty();
        AssertEntries(againObj.SubObjects[1].RemainingEntries, ("Lang-Bemerkung", "Sub note"));
    }

    private static void AssertEntries(
        OrderedStringDictionary entries,
        params (string Key, string Value)[] expected)
    {
        entries.Select(entry => (entry.Key, entry.Value)).Should().Equal(expected);
    }
}
