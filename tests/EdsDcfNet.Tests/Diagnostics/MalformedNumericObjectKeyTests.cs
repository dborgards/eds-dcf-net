namespace EdsDcfNet.Tests.Diagnostics;

using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;

/// <summary>
/// Lenient EDS/DCF reads keep an object when a numeric key is malformed and report a
/// <see cref="ParseDiagnostic"/> (#557). Strict mode still throws
/// <see cref="EdsParseException"/> with the same code.
/// </summary>
public class MalformedNumericObjectKeyTests
{
    private static readonly CanOpenFileOptions Strict = new() { StrictParsing = true };

    private const string Header =
        "[FileInfo]\n" +
        "FileName=broken.eds\n" +
        "EDSVersion=4.0\n" +
        "[DeviceInfo]\n" +
        "VendorName=Test\n";

    [Fact]
    public void ReadStringWithDiagnostics_MalformedObjectType_KeepsObjectAndContinues()
    {
        var content = Header +
            "[ManufacturerObjects]\n" +
            "SupportedObjects=2\n" +
            "1=0x2005\n" +
            "2=0x2006\n" +
            "[2005]\n" +
            "ParameterName=Garbage object type\n" +
            "ObjectType=VAR\n" +
            "DataType=0x0007\n" +
            "AccessType=rw\n" +
            "DefaultValue=0\n" +
            "[2006]\n" +
            "ParameterName=Later object\n" +
            "ObjectType=0x7\n" +
            "DataType=0x0005\n" +
            "AccessType=ro\n";

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.InvalidObjectType);
        diagnostic.Severity.Should().Be(ParseSeverity.Warning);
        diagnostic.Path.Should().Be("2005.ObjectType");
        diagnostic.Line.Should().Be(SourceLine(content, "ObjectType=VAR"));
        diagnostic.RawValue.Should().Be("VAR");
        diagnostic.CoercedTo.Should().Be("0x7");
        diagnostic.Message.Should().Contain("Treated as VAR (0x7).");

        var broken = result.Model.ObjectDictionary.Objects[0x2005];
        broken.ParameterName.Should().Be("Garbage object type");
        broken.ObjectType.Should().Be(CanOpenObjectType.Var);
        broken.DataType.Should().Be((ushort)0x0007);
        broken.DefaultValue.Should().Be("0");
        result.Model.ObjectDictionary.Objects[0x2006].ParameterName.Should().Be("Later object");
        result.Model.ObjectDictionary.ManufacturerObjects.Should().Equal((ushort)0x2005, (ushort)0x2006);

        AssertStrict(content, ParseDiagnosticCodes.InvalidObjectType, "2005", "Invalid byte value: 'VAR'");
    }

    [Fact]
    public void ReadString_MalformedObjectType_LenientFacadeDoesNotThrow()
    {
        var content = ObjectSection("ParameterName=Garbage\nObjectType=VAR\nDataType=0x0007\nAccessType=rw\n");

        var eds = CanOpenFile.Eds.ReadString(content);

        eds.ObjectDictionary.Objects[0x2005].ObjectType.Should().Be(CanOpenObjectType.Var);
    }

    [Fact]
    public void ReadFileWithDiagnostics_MalformedObjectType_ReturnsModelAndDiagnostic()
    {
        var content = ObjectSection("ParameterName=Garbage\nObjectType=VAR\nDataType=0x0007\nAccessType=rw\n");
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, content);

            var result = CanOpenFile.Eds.ReadFileWithDiagnostics(tempFile);

            result.Diagnostics.Should().ContainSingle(d =>
                d.Code == ParseDiagnosticCodes.InvalidObjectType &&
                d.Path == "2005.ObjectType" &&
                d.Line == SourceLine(content, "ObjectType=VAR"));
            result.Model.ObjectDictionary.Objects[0x2005].ObjectType.Should().Be(CanOpenObjectType.Var);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ReadStringWithDiagnostics_MalformedObjectType_DcfKeepsObject()
    {
        var content =
            "[FileInfo]\n" +
            "FileName=broken.dcf\n" +
            "[DeviceInfo]\n" +
            "VendorName=Test\n" +
            "[DeviceCommissioning]\n" +
            "NodeID=1\n" +
            "Baudrate=250\n" +
            "[ManufacturerObjects]\n" +
            "SupportedObjects=1\n" +
            "1=0x2005\n" +
            "[2005]\n" +
            "ParameterName=Garbage\n" +
            "ObjectType=VAR\n" +
            "DataType=0x0007\n" +
            "AccessType=rw\n" +
            "ParameterValue=1\n";

        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.InvalidObjectType);
        result.Model.ObjectDictionary.Objects[0x2005].ObjectType.Should().Be(CanOpenObjectType.Var);
        result.Model.ObjectDictionary.Objects[0x2005].ParameterValue.Should().Be("1");
        result.Model.DeviceCommissioning.NodeId.Should().Be(1);

        var act = () => CanOpenFile.Dcf.ReadStringWithDiagnostics(content, Strict);
        act.Should().Throw<EdsParseException>()
            .Which.Code.Should().Be(ParseDiagnosticCodes.InvalidObjectType);
    }

    [Theory]
    [InlineData("0", (byte)0)]
    [InlineData("255", (byte)255)]
    [InlineData("0x7", (byte)0x7)]
    [InlineData("0x9", (byte)0x9)]
    public void ReadStringWithDiagnostics_ObjectTypeAtValidRange_ParsesWithoutDiagnostic(string raw, byte expected)
    {
        var content = ObjectSection(
            "ParameterName=Edges\nObjectType=" + raw + "\nDataType=0x0007\nAccessType=ro\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.HasDiagnostics.Should().BeFalse();
        result.Model.ObjectDictionary.Objects[0x2005].ObjectType.Should().Be(expected);
    }

    [Theory]
    [InlineData("256")]
    [InlineData("0x100")]
    public void ReadStringWithDiagnostics_ObjectTypeAboveMaxValue_FallsBackToVar(string raw)
    {
        var content = ObjectSection(
            "ParameterName=Edges\nObjectType=" + raw + "\nDataType=0x0007\nAccessType=ro\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.InvalidObjectType);
        diagnostic.RawValue.Should().Be(raw);
        diagnostic.CoercedTo.Should().Be("0x7");
        result.Model.ObjectDictionary.Objects[0x2005].ObjectType.Should().Be(CanOpenObjectType.Var);
        result.Model.ObjectDictionary.Objects[0x2005].ParameterName.Should().Be("Edges");

        AssertStrict(content, ParseDiagnosticCodes.InvalidObjectType, "2005", "Invalid byte value: '" + raw + "'");
    }

    [Fact]
    public void ReadStringWithDiagnostics_EmptyObjectType_StaysZeroWithoutDiagnostic()
    {
        // A present empty key is 0 (NULL), the same as ValueConverter.ParseByte(""), not the omitted-key VAR default.
        var content = ObjectSection("ParameterName=Empty\nObjectType=\nDataType=0x0007\nAccessType=ro\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.HasDiagnostics.Should().BeFalse();
        result.Model.ObjectDictionary.Objects[0x2005].ObjectType.Should().Be(0);
    }

    [Fact]
    public void ReadStringWithDiagnostics_OmittedObjectType_DefaultsToVarWithoutDiagnostic()
    {
        var content = ObjectSection("ParameterName=Omitted\nDataType=0x0007\nAccessType=ro\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.HasDiagnostics.Should().BeFalse();
        result.Model.ObjectDictionary.Objects[0x2005].ObjectType.Should().Be(CanOpenObjectType.Var);
    }

    [Fact]
    public void ReadStringWithDiagnostics_MalformedObjectType_DoesNotScanSubObjects()
    {
        var content = ObjectSection(
            "ParameterName=Parent\n" +
            "ObjectType=VAR\n" +
            "AccessType=ro\n" +
            "[2005sub1]\n" +
            "ParameterName=Child\n" +
            "ObjectType=0x7\n" +
            "DataType=0x0005\n" +
            "AccessType=ro\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.InvalidObjectType);
        result.Model.ObjectDictionary.Objects[0x2005].SubObjects.Should().BeEmpty();
    }

    [Fact]
    public void ReadStringWithDiagnostics_MalformedObjectTypeWithSubNumber_StillParsesSubObjects()
    {
        var content = ObjectSection(
            "ParameterName=Parent\n" +
            "ObjectType=VAR\n" +
            "SubNumber=1\n" +
            "AccessType=ro\n" +
            "[2005sub1]\n" +
            "ParameterName=Child\n" +
            "ObjectType=0x7\n" +
            "DataType=0x0005\n" +
            "AccessType=ro\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.InvalidObjectType);
        result.Model.ObjectDictionary.Objects[0x2005].ObjectType.Should().Be(CanOpenObjectType.Var);
        result.Model.ObjectDictionary.Objects[0x2005].SubObjects[1].ParameterName.Should().Be("Child");
    }

    [Fact]
    public void ReadStringWithDiagnostics_MalformedDataType_LeavesDataTypeUnset()
    {
        var content = ObjectSection(
            "ParameterName=Bad type\nObjectType=0x7\nDataType=nope\nAccessType=rw\nDefaultValue=1\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.InvalidDataType);
        diagnostic.Path.Should().Be("2005.DataType");
        diagnostic.Line.Should().Be(SourceLine(content, "DataType=nope"));
        diagnostic.RawValue.Should().Be("nope");
        diagnostic.CoercedTo.Should().BeNull();
        diagnostic.Message.Should().Contain("left unset");

        var obj = result.Model.ObjectDictionary.Objects[0x2005];
        obj.DataType.Should().BeNull();
        obj.ObjectType.Should().Be(CanOpenObjectType.Var);
        obj.DefaultValue.Should().Be("1");

        AssertStrict(content, ParseDiagnosticCodes.InvalidDataType, "2005", "Invalid UInt16 value: 'nope'");
    }

    [Theory]
    [InlineData("0", (ushort)0)]
    [InlineData("0xFFFF", (ushort)0xFFFF)]
    [InlineData("65535", (ushort)65535)]
    public void ReadStringWithDiagnostics_DataTypeAtValidRange_ParsesWithoutDiagnostic(string raw, ushort expected)
    {
        var content = ObjectSection(
            "ParameterName=Edges\nObjectType=0x7\nDataType=" + raw + "\nAccessType=ro\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.HasDiagnostics.Should().BeFalse();
        result.Model.ObjectDictionary.Objects[0x2005].DataType.Should().Be(expected);
    }

    [Theory]
    [InlineData("65536")]
    [InlineData("0x10000")]
    public void ReadStringWithDiagnostics_DataTypeAboveMaxValue_LeavesUnset(string raw)
    {
        var content = ObjectSection(
            "ParameterName=Edges\nObjectType=0x7\nDataType=" + raw + "\nAccessType=ro\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == ParseDiagnosticCodes.InvalidDataType && d.RawValue == raw && d.CoercedTo == null);
        result.Model.ObjectDictionary.Objects[0x2005].DataType.Should().BeNull();

        AssertStrict(content, ParseDiagnosticCodes.InvalidDataType, "2005", "Invalid UInt16 value: '" + raw + "'");
    }

    [Fact]
    public void ReadStringWithDiagnostics_OmittedDataType_StaysNullWithoutDiagnostic()
    {
        var content = ObjectSection("ParameterName=Omitted\nObjectType=0x7\nAccessType=ro\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.HasDiagnostics.Should().BeFalse();
        result.Model.ObjectDictionary.Objects[0x2005].DataType.Should().BeNull();
    }

    [Fact]
    public void ReadStringWithDiagnostics_MalformedSubObjectDataType_FallsBackToZero()
    {
        var content = ObjectSection(
            "ParameterName=Record\n" +
            "ObjectType=0x9\n" +
            "SubNumber=1\n" +
            "[2005sub1]\n" +
            "ParameterName=Elem\n" +
            "ObjectType=VAR\n" +
            "DataType=nope\n" +
            "AccessType=rw\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().HaveCount(2);
        result.Diagnostics.Should().Contain(d =>
            d.Code == ParseDiagnosticCodes.InvalidObjectType &&
            d.Path == "2005sub1.ObjectType" &&
            d.RawValue == "VAR" &&
            d.CoercedTo == "0x7" &&
            d.Line == SourceLine(content, "ObjectType=VAR"));
        result.Diagnostics.Should().Contain(d =>
            d.Code == ParseDiagnosticCodes.InvalidDataType &&
            d.Path == "2005sub1.DataType" &&
            d.RawValue == "nope" &&
            d.CoercedTo == "0" &&
            d.Line == SourceLine(content, "DataType=nope"));

        var sub = result.Model.ObjectDictionary.Objects[0x2005].SubObjects[1];
        sub.ParameterName.Should().Be("Elem");
        sub.ObjectType.Should().Be(CanOpenObjectType.Var);
        sub.DataType.Should().Be(0);

        AssertStrict(content, ParseDiagnosticCodes.InvalidObjectType, "2005sub1", "Invalid byte value: 'VAR'");
    }

    [Fact]
    public void ReadStringWithDiagnostics_MalformedSubObjectDataTypeOnly_FallsBackToZero()
    {
        var content = ObjectSection(
            "ParameterName=Record\n" +
            "ObjectType=0x9\n" +
            "SubNumber=1\n" +
            "[2005sub1]\n" +
            "ParameterName=Elem\n" +
            "ObjectType=0x7\n" +
            "DataType=0x10000\n" +
            "AccessType=ro\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.InvalidDataType);
        diagnostic.Path.Should().Be("2005sub1.DataType");
        diagnostic.CoercedTo.Should().Be("0");
        result.Model.ObjectDictionary.Objects[0x2005].SubObjects[1].DataType.Should().Be(0);
        result.Model.ObjectDictionary.Objects[0x2005].SubObjects[1].ObjectType.Should().Be(CanOpenObjectType.Var);

        AssertStrict(content, ParseDiagnosticCodes.InvalidDataType, "2005sub1", "Invalid UInt16 value: '0x10000'");
    }

    [Theory]
    [InlineData("0", (byte)0)]
    [InlineData("255", (byte)255)]
    public void ReadStringWithDiagnostics_SubNumberAtValidRange_ParsesWithoutDiagnostic(string raw, byte expected)
    {
        var content = ObjectSection(
            "ParameterName=Edges\nObjectType=0x8\nSubNumber=" + raw + "\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.HasDiagnostics.Should().BeFalse();
        result.Model.ObjectDictionary.Objects[0x2005].SubNumber.Should().Be(expected);
    }

    [Fact]
    public void ReadStringWithDiagnostics_SubNumberAboveMaxValue_LeavesUnset()
    {
        var content = ObjectSection("ParameterName=Edges\nObjectType=0x8\nSubNumber=256\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.InvalidSubNumber);
        diagnostic.Path.Should().Be("2005.SubNumber");
        diagnostic.RawValue.Should().Be("256");
        diagnostic.CoercedTo.Should().BeNull();
        diagnostic.Line.Should().Be(SourceLine(content, "SubNumber=256"));
        result.Model.ObjectDictionary.Objects[0x2005].SubNumber.Should().BeNull();
        result.Model.ObjectDictionary.Objects[0x2005].ParameterName.Should().Be("Edges");

        AssertStrict(content, ParseDiagnosticCodes.InvalidSubNumber, "2005", "Invalid byte value: '256'");
    }

    [Fact]
    public void ReadStringWithDiagnostics_MalformedSubNumber_LeavesUnset()
    {
        var content = ObjectSection("ParameterName=Edges\nObjectType=0x7\nSubNumber=many\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == ParseDiagnosticCodes.InvalidSubNumber && d.RawValue == "many" && d.CoercedTo == null);
        result.Model.ObjectDictionary.Objects[0x2005].SubNumber.Should().BeNull();

        AssertStrict(content, ParseDiagnosticCodes.InvalidSubNumber, "2005", "Invalid byte value: 'many'");
    }

    [Theory]
    [InlineData("0", (byte)0)]
    [InlineData("255", (byte)255)]
    public void ReadStringWithDiagnostics_CompactSubObjAtValidRange_ParsesWithoutDiagnostic(string raw, byte expected)
    {
        var content = ObjectSection(
            "ParameterName=Edges\nObjectType=0x8\nCompactSubObj=" + raw + "\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.HasDiagnostics.Should().BeFalse();
        result.Model.ObjectDictionary.Objects[0x2005].CompactSubObj.Should().Be(expected);
    }

    [Fact]
    public void ReadStringWithDiagnostics_CompactSubObjAboveMaxValue_LeavesUnset()
    {
        var content = ObjectSection("ParameterName=Edges\nObjectType=0x7\nCompactSubObj=256\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.InvalidCompactSubObj);
        diagnostic.Path.Should().Be("2005.CompactSubObj");
        diagnostic.RawValue.Should().Be("256");
        diagnostic.CoercedTo.Should().BeNull();
        result.Model.ObjectDictionary.Objects[0x2005].CompactSubObj.Should().BeNull();
        result.Model.ObjectDictionary.Objects[0x2005].SubObjects.Should().BeEmpty();

        AssertStrict(content, ParseDiagnosticCodes.InvalidCompactSubObj, "2005", "Invalid byte value: '256'");
    }

    [Fact]
    public void ReadStringWithDiagnostics_MalformedCompactSubObj_LeavesUnsetAndContinues()
    {
        var content = Header +
            "[ManufacturerObjects]\n" +
            "SupportedObjects=2\n" +
            "1=0x2005\n" +
            "2=0x2006\n" +
            "[2005]\n" +
            "ParameterName=Compact\n" +
            "ObjectType=0x7\n" +
            "CompactSubObj=nope\n" +
            "[2006]\n" +
            "ParameterName=Later\n" +
            "ObjectType=0x7\n" +
            "AccessType=ro\n";

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == ParseDiagnosticCodes.InvalidCompactSubObj &&
            d.Path == "2005.CompactSubObj" &&
            d.RawValue == "nope");
        result.Model.ObjectDictionary.Objects[0x2005].CompactSubObj.Should().BeNull();
        result.Model.ObjectDictionary.Objects[0x2006].ParameterName.Should().Be("Later");

        AssertStrict(content, ParseDiagnosticCodes.InvalidCompactSubObj, "2005", "Invalid byte value: 'nope'");
    }

    [Theory]
    [InlineData("0", 0u)]
    [InlineData("4294967295", 4294967295u)]
    [InlineData("0xFFFFFFFF", 4294967295u)]
    public void ReadStringWithDiagnostics_ObjFlagsAtValidRange_ParsesWithoutDiagnostic(string raw, uint expected)
    {
        var content = ObjectSection(
            "ParameterName=Edges\nObjectType=0x7\nObjFlags=" + raw + "\nAccessType=ro\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.HasDiagnostics.Should().BeFalse();
        result.Model.ObjectDictionary.Objects[0x2005].ObjFlags.Should().Be(expected);
    }

    [Theory]
    [InlineData("4294967296")]
    [InlineData("0x100000000")]
    [InlineData("flags")]
    public void ReadStringWithDiagnostics_MalformedObjFlags_FallsBackToZero(string raw)
    {
        var content = ObjectSection(
            "ParameterName=Edges\nObjectType=0x7\nObjFlags=" + raw + "\nAccessType=ro\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.InvalidObjFlags);
        diagnostic.Path.Should().Be("2005.ObjFlags");
        diagnostic.RawValue.Should().Be(raw);
        diagnostic.CoercedTo.Should().Be("0");
        diagnostic.Line.Should().Be(SourceLine(content, "ObjFlags=" + raw));
        result.Model.ObjectDictionary.Objects[0x2005].ObjFlags.Should().Be(0);
        result.Model.ObjectDictionary.Objects[0x2005].ParameterName.Should().Be("Edges");

        AssertStrict(content, ParseDiagnosticCodes.InvalidObjFlags, "2005", "Invalid uint value: '" + raw + "'");
    }

    [Fact]
    public void ReadStringWithDiagnostics_MalformedObjectListIndex_SkipsEntryAndKeepsSiblings()
    {
        var content = Header +
            "[ManufacturerObjects]\n" +
            "SupportedObjects=3\n" +
            "1=0x2005\n" +
            "2=bogus\n" +
            "3=0xFFFF\n" +
            "[2005]\n" +
            "ParameterName=First\n" +
            "ObjectType=0x7\n" +
            "AccessType=ro\n" +
            "[FFFF]\n" +
            "ParameterName=Last\n" +
            "ObjectType=0x7\n" +
            "AccessType=ro\n";

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.InvalidObjectIndex);
        diagnostic.Path.Should().Be("ManufacturerObjects.2");
        diagnostic.RawValue.Should().Be("bogus");
        diagnostic.CoercedTo.Should().BeNull();
        diagnostic.Line.Should().Be(SourceLine(content, "2=bogus"));
        diagnostic.Message.Should().Contain("skipped");
        result.Model.ObjectDictionary.ManufacturerObjects.Should().Equal((ushort)0x2005, (ushort)0xFFFF);
        result.Model.ObjectDictionary.Objects[0x2005].ParameterName.Should().Be("First");
        result.Model.ObjectDictionary.Objects[0xFFFF].ParameterName.Should().Be("Last");

        AssertStrict(content, ParseDiagnosticCodes.InvalidObjectIndex, "ManufacturerObjects", "Invalid UInt16 value: 'bogus'");
    }

    [Fact]
    public void ReadStringWithDiagnostics_ObjectIndexAboveMaxValue_SkipsEntry()
    {
        var content = Header +
            "[OptionalObjects]\n" +
            "SupportedObjects=1\n" +
            "1=0x10000\n" +
            "[ManufacturerObjects]\n" +
            "SupportedObjects=1\n" +
            "1=0x2006\n" +
            "[2006]\n" +
            "ParameterName=Kept\n" +
            "ObjectType=0x7\n" +
            "AccessType=ro\n";

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == ParseDiagnosticCodes.InvalidObjectIndex &&
            d.Path == "OptionalObjects.1" &&
            d.RawValue == "0x10000");
        result.Model.ObjectDictionary.OptionalObjects.Should().BeEmpty();
        result.Model.ObjectDictionary.Objects[0x2006].ParameterName.Should().Be("Kept");

        AssertStrict(content, ParseDiagnosticCodes.InvalidObjectIndex, "OptionalObjects", "Invalid UInt16 value: '0x10000'");
    }

    [Fact]
    public void ReadStringWithDiagnostics_MalformedSupportedObjects_TreatsCountAsZero()
    {
        var content = Header +
            "[ManufacturerObjects]\n" +
            "SupportedObjects=nope\n" +
            "1=0x2005\n" +
            "[OptionalObjects]\n" +
            "SupportedObjects=1\n" +
            "1=0x1000\n" +
            "[1000]\n" +
            "ParameterName=Device Type\n" +
            "ObjectType=0x7\n" +
            "DataType=0x0007\n" +
            "AccessType=ro\n" +
            "[2005]\n" +
            "ParameterName=Unlisted\n" +
            "ObjectType=0x7\n" +
            "AccessType=rw\n";

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.InvalidObjectListCount);
        diagnostic.Path.Should().Be("ManufacturerObjects.SupportedObjects");
        diagnostic.RawValue.Should().Be("nope");
        diagnostic.CoercedTo.Should().Be("0");
        diagnostic.Line.Should().Be(SourceLine(content, "SupportedObjects=nope"));
        result.Model.ObjectDictionary.ManufacturerObjects.Should().BeEmpty();
        result.Model.ObjectDictionary.Objects.Should().NotContainKey((ushort)0x2005);
        result.Model.ObjectDictionary.Objects[0x1000].ParameterName.Should().Be("Device Type");

        AssertStrict(
            content,
            ParseDiagnosticCodes.InvalidObjectListCount,
            "ManufacturerObjects",
            "Invalid UInt16 value: 'nope'");
    }

    [Fact]
    public void ReadStringWithDiagnostics_SupportedObjectsAboveMaxValue_TreatsCountAsZero()
    {
        var content = Header +
            "[MandatoryObjects]\n" +
            "SupportedObjects=65536\n" +
            "1=0x1000\n" +
            "[1000]\n" +
            "ParameterName=Device Type\n" +
            "ObjectType=0x7\n" +
            "AccessType=ro\n";

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == ParseDiagnosticCodes.InvalidObjectListCount &&
            d.RawValue == "65536" &&
            d.CoercedTo == "0");
        result.Model.ObjectDictionary.MandatoryObjects.Should().BeEmpty();
        result.Model.DeviceInfo.VendorName.Should().Be("Test");

        AssertStrict(content, ParseDiagnosticCodes.InvalidObjectListCount, "MandatoryObjects", "Invalid UInt16 value: '65536'");
    }

    [Fact]
    public void ReadStringWithDiagnostics_SupportedObjectsAtMaxValue_ParsesWithoutDiagnostic()
    {
        var content = Header +
            "[MandatoryObjects]\n" +
            "SupportedObjects=65535\n" +
            "[OptionalObjects]\n" +
            "SupportedObjects=0\n";

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.HasDiagnostics.Should().BeFalse();
        result.Model.ObjectDictionary.MandatoryObjects.Should().BeEmpty();
    }

    [Fact]
    public void ReadStringWithDiagnostics_MalformedObjectLinks_SkipsBadEntryAndKeepsObject()
    {
        var content = ObjectSection(
            "ParameterName=Linked\n" +
            "ObjectType=0x7\n" +
            "AccessType=ro\n" +
            "[2005ObjectLinks]\n" +
            "ObjectLinks=2\n" +
            "1=0x1000\n" +
            "2=bogus\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.InvalidObjectIndex);
        diagnostic.Path.Should().Be("2005ObjectLinks.2");
        diagnostic.RawValue.Should().Be("bogus");
        diagnostic.Line.Should().Be(SourceLine(content, "2=bogus"));
        var obj = result.Model.ObjectDictionary.Objects[0x2005];
        obj.ParameterName.Should().Be("Linked");
        obj.ObjectLinks.Should().Equal((ushort)0x1000);

        AssertStrict(content, ParseDiagnosticCodes.InvalidObjectIndex, "2005ObjectLinks", "Invalid UInt16 value: 'bogus'");
    }

    [Fact]
    public void ReadStringWithDiagnostics_MalformedObjectLinkCount_TreatsCountAsZero()
    {
        var content = ObjectSection(
            "ParameterName=Linked\n" +
            "ObjectType=0x7\n" +
            "AccessType=ro\n" +
            "[2005ObjectLinks]\n" +
            "ObjectLinks=many\n" +
            "1=0x1000\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == ParseDiagnosticCodes.InvalidObjectListCount &&
            d.Path == "2005ObjectLinks.ObjectLinks" &&
            d.RawValue == "many" &&
            d.CoercedTo == "0");
        result.Model.ObjectDictionary.Objects[0x2005].ObjectLinks.Should().BeEmpty();
        result.Model.ObjectDictionary.Objects[0x2005].ParameterName.Should().Be("Linked");

        AssertStrict(content, ParseDiagnosticCodes.InvalidObjectListCount, "2005ObjectLinks", "Invalid UInt16 value: 'many'");
    }

    [Fact]
    public void ReadStringWithDiagnostics_MalformedFixedObjectIndex_SkipsEntryAndKeepsModule()
    {
        var content = Header +
            "[MandatoryObjects]\n" +
            "SupportedObjects=0\n" +
            "[SupportedModules]\n" +
            "NrOfEntries=1\n" +
            "[M1ModuleInfo]\n" +
            "ProductName=Mod\n" +
            "[M1FixedObjects]\n" +
            "NrOfEntries=2\n" +
            "1=0x1000\n" +
            "2=bogus\n";

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.InvalidObjectIndex);
        diagnostic.Path.Should().Be("M1FixedObjects.2");
        diagnostic.RawValue.Should().Be("bogus");
        diagnostic.Line.Should().Be(SourceLine(content, "2=bogus"));
        result.Model.SupportedModules.Should().ContainSingle();
        result.Model.SupportedModules[0].ProductName.Should().Be("Mod");
        result.Model.SupportedModules[0].FixedObjects.Should().Equal((ushort)0x1000);

        AssertStrict(content, ParseDiagnosticCodes.InvalidObjectIndex, "M1FixedObjects", "Invalid UInt16 value: 'bogus'");
    }

    [Fact]
    public void ReadStringWithDiagnostics_MalformedFixedObjectCount_TreatsCountAsZero()
    {
        var content = Header +
            "[MandatoryObjects]\n" +
            "SupportedObjects=0\n" +
            "[SupportedModules]\n" +
            "NrOfEntries=1\n" +
            "[M1ModuleInfo]\n" +
            "ProductName=Mod\n" +
            "[M1FixedObjects]\n" +
            "NrOfEntries=nope\n" +
            "1=0x1000\n";

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == ParseDiagnosticCodes.InvalidObjectListCount &&
            d.Path == "M1FixedObjects.NrOfEntries" &&
            d.RawValue == "nope" &&
            d.CoercedTo == "0");
        result.Model.SupportedModules[0].FixedObjects.Should().BeEmpty();
        result.Model.SupportedModules[0].ProductName.Should().Be("Mod");

        AssertStrict(content, ParseDiagnosticCodes.InvalidObjectListCount, "M1FixedObjects", "Invalid UInt16 value: 'nope'");
    }

    [Fact]
    public void ReadStringWithDiagnostics_SeveralMalformedKeys_ReportsEachAndKeepsObject()
    {
        var content = Header +
            "[ManufacturerObjects]\n" +
            "SupportedObjects=2\n" +
            "1=0x2005\n" +
            "2=0x2006\n" +
            "[2005]\n" +
            "ParameterName=Garbage\n" +
            "ObjectType=VAR\n" +
            "DataType=nope\n" +
            "AccessType=nope\n" +
            "ObjFlags=flags\n" +
            "SubNumber=many\n" +
            "CompactSubObj=nope\n" +
            "DefaultValue=4\n" +
            "[2006]\n" +
            "ParameterName=Later object\n" +
            "ObjectType=0x7\n" +
            "AccessType=ro\n";

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        result.Diagnostics.Select(d => d.Code).Should().Equal(
            ParseDiagnosticCodes.InvalidObjectType,
            ParseDiagnosticCodes.InvalidDataType,
            ParseDiagnosticCodes.UnknownAccessTypeToken,
            ParseDiagnosticCodes.InvalidObjFlags,
            ParseDiagnosticCodes.InvalidSubNumber,
            ParseDiagnosticCodes.InvalidCompactSubObj);

        var obj = result.Model.ObjectDictionary.Objects[0x2005];
        obj.ParameterName.Should().Be("Garbage");
        obj.ObjectType.Should().Be(CanOpenObjectType.Var);
        obj.DataType.Should().BeNull();
        obj.AccessType.Should().Be(AccessType.ReadOnly);
        obj.ObjFlags.Should().Be(0);
        obj.SubNumber.Should().BeNull();
        obj.CompactSubObj.Should().BeNull();
        obj.DefaultValue.Should().Be("4");
        result.Model.ObjectDictionary.Objects[0x2006].ParameterName.Should().Be("Later object");
    }

    [Fact]
    public void ReadStringWithDiagnostics_MalformedObjectType_RoundTripPreservesCoercedDefaults()
    {
        var content = ObjectSection(
            "ParameterName=Garbage object type\n" +
            "ObjectType=VAR\n" +
            "DataType=nope\n" +
            "AccessType=rw\n" +
            "DefaultValue=0\n");

        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);
        var written = CanOpenFile.Eds.WriteToString(result.Model);
        var again = CanOpenFile.Eds.ReadStringWithDiagnostics(written);

        again.HasDiagnostics.Should().BeFalse();
        var obj = again.Model.ObjectDictionary.Objects[0x2005];
        obj.ParameterName.Should().Be("Garbage object type");
        obj.ObjectType.Should().Be(CanOpenObjectType.Var);
        obj.DataType.Should().BeNull();
        obj.AccessType.Should().Be(AccessType.ReadWrite);
        obj.DefaultValue.Should().Be("0");
        written.Should().Contain("ObjectType=0x7");
        written.Should().NotContain("DataType=");
    }

    [Fact]
    public void IniKeyLines_TryGetLine_ReturnsNullWhenSectionOrKeyWasNotRecorded()
    {
        var unparsed = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        IniKeyLines.TryGetLine(unparsed, "FileInfo", "FileName").Should().BeNull();

        var content =
            "[FileInfo]\n" +
            "FileName=a.eds\n" +
            "FileVersion=1\n" +
            "FileName=b.eds\n";
        var sections = IniParser.ParseString(content);

        IniKeyLines.TryGetLine(sections, "fileinfo", "filename").Should().Be(SourceLine(content, "FileName=b.eds"));
        IniKeyLines.TryGetLine(sections, "FileInfo", "FileVersion").Should().Be(SourceLine(content, "FileVersion=1"));
        IniKeyLines.TryGetLine(sections, "Missing", "FileName").Should().BeNull();
        IniKeyLines.TryGetLine(sections, "FileInfo", "Missing").Should().BeNull();
    }

    private static string ObjectSection(string body)
        => Header +
           "[ManufacturerObjects]\n" +
           "SupportedObjects=1\n" +
           "1=0x2005\n" +
           "[2005]\n" +
           body;

    /// <summary>
    /// Line numbers match <see cref="EdsDcfNet.Parsers.IniParser"/> string parsing, which
    /// drops empty segments produced by splitting on CR and LF.
    /// </summary>
    private static int SourceLine(string content, string exactTrimmedLine)
    {
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim() == exactTrimmedLine)
                return i + 1;
        }

        throw new InvalidOperationException("Line not found: " + exactTrimmedLine);
    }

    private static void AssertStrict(string content, string code, string sectionName, string messageFragment)
    {
        var act = () => CanOpenFile.Eds.ReadStringWithDiagnostics(content, Strict);

        var ex = act.Should().Throw<EdsParseException>().Which;
        ex.Code.Should().Be(code);
        ex.SectionName.Should().Be(sectionName);
        ex.LineNumber.Should().NotBeNull();
        ex.Message.Should().Contain(messageFragment);
    }
}
