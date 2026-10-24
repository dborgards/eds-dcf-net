namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Tests.Utilities;
using EdsDcfNet.Validation;

public class IniSubObjectObjFlagsTests
{
    private const string EdsWithSubObjectFlags =
        "[FileInfo]\nFileName=sub.eds\nEDSVersion=4.0\n" +
        "[DeviceInfo]\nVendorName=Test\n" +
        "[MandatoryObjects]\nSupportedObjects=1\n1=0x2000\n" +
        "[2000]\nParameterName=Array\nObjectType=0x8\nDataType=0x5\nAccessType=rw\nSubNumber=1\n" +
        "[2000sub1]\nParameterName=Element\nObjectType=0x7\nDataType=0x5\nAccessType=rw\nObjFlags=3\n";

    [Theory]
    [InlineData("Eds")]
    [InlineData("Dcf")]
    public void ReadWrite_SubObjectObjFlags_RoundTripsAsDedicatedProperty(string format)
    {
        var content = format == "Dcf"
            ? EdsWithSubObjectFlags + "[DeviceComissioning]\nNodeID=5\nBaudrate=500\n"
            : EdsWithSubObjectFlags;

        ICanOpenFileModel read = format == "Eds"
            ? CanOpenFile.Eds.ReadString(content)
            : CanOpenFile.Dcf.ReadString(content);
        var subObject = read.ObjectDictionary.Objects[0x2000].SubObjects[1];
        subObject.ObjFlags.Should().Be(3u);
        subObject.RemainingEntries.Keys.Should().NotContain("ObjFlags");

        var written = format == "Eds"
            ? CanOpenFile.Eds.WriteToString((ElectronicDataSheet)read)
            : CanOpenFile.Dcf.WriteToString((DeviceConfigurationFile)read);
        written.Should().Contain("[2000sub1]").And.Contain("ObjFlags=0x3");

        ICanOpenFileModel again = format == "Eds"
            ? CanOpenFile.Eds.ReadString(written)
            : CanOpenFile.Dcf.ReadString(written);
        again.ObjectDictionary.Objects[0x2000].SubObjects[1].ObjFlags.Should().Be(3u);
    }

    [Fact]
    public void ReadStringWithDiagnostics_SubObjectObjFlags_UsesExistingNumericDiagnostics()
    {
        var malformed = CanOpenFile.Eds.ReadStringWithDiagnostics(EdsWithSubObjectFlags.Replace("ObjFlags=3", "ObjFlags=bad"));
        var reserved = CanOpenFile.Eds.ReadStringWithDiagnostics(EdsWithSubObjectFlags.Replace("ObjFlags=3", "ObjFlags=4"));

        malformed.Model.ObjectDictionary.Objects[0x2000].SubObjects[1].ObjFlags.Should().Be(0u);
        malformed.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.InvalidObjFlags &&
            diagnostic.Path == "2000sub1.ObjFlags");
        reserved.Model.ObjectDictionary.Objects[0x2000].SubObjects[1].ObjFlags.Should().Be(4u);
        reserved.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Code == ParseDiagnosticCodes.IniObjFlagsReservedBits &&
            diagnostic.Path == "2000sub1.ObjFlags");
    }

    [Fact]
    public void ConvertToDcf_SubObjectObjFlags_ClonesValue()
    {
        var eds = CanOpenFile.Eds.ReadString(EdsWithSubObjectFlags);

        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 5, timestamp: new DateTime(2026, 1, 2), baudrate: 250);

        dcf.ObjectDictionary.Objects[0x2000].SubObjects[1].ObjFlags.Should().Be(3u);
        dcf.ObjectDictionary.Objects[0x2000].SubObjects[1].Should().NotBeSameAs(
            eds.ObjectDictionary.Objects[0x2000].SubObjects[1]);
    }

    [Fact]
    public void WriteToString_CompactSubObjectWithObjFlags_ExpandsSectionInsteadOfLosingFlags()
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        var obj = eds.ObjectDictionary.Objects[0x1000];
        obj.ObjectType = CanOpenObjectType.Array;
        obj.DataType = CanOpenDataType.Unsigned8;
        obj.AccessType = AccessType.ReadWrite;
        obj.CompactSubObj = 1;
        obj.SubObjects[1] = new CanOpenSubObject
        {
            SubIndex = 1,
            ParameterName = "Device Type1",
            ObjectType = CanOpenObjectType.Var,
            DataType = CanOpenDataType.Unsigned8,
            AccessType = AccessType.ReadWrite,
            ObjFlags = 1,
        };

        var written = CanOpenFile.Eds.WriteToString(eds);
        var again = CanOpenFile.Eds.ReadString(written);

        written.Should().Contain("[1000sub1]").And.Contain("ObjFlags=0x1");
        again.ObjectDictionary.Objects[0x1000].SubObjects[1].ObjFlags.Should().Be(1u);
    }

    [Fact]
    public void WriteToString_SubObjectReservedObjFlags_ValidatedIniWriteRejects()
    {
        var eds = CanOpenFile.Eds.ReadString(EdsWithSubObjectFlags.Replace("ObjFlags=3", "ObjFlags=4"));

        var act = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        act.Should().Throw<ModelValidationException>().Which.Issues.Should().Contain(issue =>
            issue.Code == ValidationIssueCodes.IniObjFlagsReservedBits &&
            issue.Path == "ObjectDictionary.Objects[0x2000].SubObjects[0x01].ObjFlags");
    }
}
