namespace EdsDcfNet.Tests.Writers;

using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Tests.Utilities;
using EdsDcfNet.Validation;

/// <summary>
/// CiA 306-1 v1.4.0 Table 7 (validity of object-type dependent keys): the INI writers omit
/// keys marked "n" (not supported) for the object type, and the EDS/DCF readers report such a
/// key as <see cref="ParseDiagnosticCodes.IniObjectKeyNotSupported"/> (lenient warning, strict
/// exception). Review findings S3 and S12, #581.
/// </summary>
public class ObjectTypeKeyMatrixTests
{
    private static readonly CanOpenFileOptions Strict = new() { StrictParsing = true };

    private static readonly string[] StructuredValueKeys =
    {
        "DataType", "AccessType", "DefaultValue", "LowLimit", "HighLimit", "PDOMapping"
    };

    public static TheoryData<byte> StructuredObjectTypes => new()
    {
        CanOpenObjectType.DefStruct,
        CanOpenObjectType.Array,
        CanOpenObjectType.Record
    };

    public static TheoryData<byte> ObjectTypesWithoutSubIndexes => new()
    {
        CanOpenObjectType.Var,
        CanOpenObjectType.DefType,
        CanOpenObjectType.Domain
    };

    #region Writer

    [Theory]
    [MemberData(nameof(StructuredObjectTypes))]
    public void WriteToString_StructuredObjectWithoutCompactSubObj_OmitsNotSupportedKeys(byte objectType)
    {
        // Arrange
        var eds = EdsWith(StructuredObject(objectType));

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        var keys = SectionKeys(written, "2000");
        keys.Should().Contain(new[] { "SubNumber", "ParameterName", "ObjectType" });
        keys.Should().NotContain(StructuredValueKeys);
        SectionKeys(written, "2000sub1").Should().Contain(new[] { "DataType", "AccessType", "PDOMapping" });
    }

    [Fact]
    public void WriteToString_StructuredObjectWithoutCompactSubObj_DcfOmitsNotSupportedKeys()
    {
        // Arrange
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        AddManufacturerObject(dcf, StructuredObject(CanOpenObjectType.Record));

        // Act
        var written = CanOpenFile.Dcf.WriteToString(dcf);

        // Assert
        SectionKeys(written, "2000").Should().NotContain(StructuredValueKeys);
    }

    [Fact]
    public void WriteToString_ArrayWithCompactSubObj_KeepsTemplateKeysAndOmitsSubNumber()
    {
        // Arrange
        var array = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "Compact",
            ObjectType = CanOpenObjectType.Array,
            DataType = 0x0007,
            AccessType = AccessType.ReadWrite,
            DefaultValue = "0",
            LowLimit = "0",
            HighLimit = "100",
            PdoMapping = true,
            CompactSubObj = 2
        };
        var eds = EdsWith(array);

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        var keys = SectionKeys(written, "2000");
        keys.Should().Contain(StructuredValueKeys);
        keys.Should().Contain("CompactSubObj");
        keys.Should().NotContain("SubNumber");
    }

    [Fact]
    public void WriteToString_CompactArrayWithSubObjectAboveCompactRange_KeepsSubNumber()
    {
        // Arrange: S18 — SubNumber keeps the expanded sub-object above the compact range reachable.
        var array = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "Compact",
            ObjectType = CanOpenObjectType.Array,
            DataType = 0x0007,
            AccessType = AccessType.ReadWrite,
            CompactSubObj = 2
        };
        array.SubObjects[5] = Var(5, "Extra", 0x0005);
        var eds = EdsWith(array);

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds);
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(written);

        // Assert
        SectionKeys(written, "2000").Should().Contain("SubNumber");
        result.Diagnostics.Should().BeEmpty();
        result.Model.ObjectDictionary.Objects[0x2000].SubObjects.Should().ContainKey(5);
    }

    [Theory]
    [MemberData(nameof(ObjectTypesWithoutSubIndexes))]
    public void WriteToString_ObjectTypeWithoutSubIndexes_OmitsSubNumber(byte objectType)
    {
        // Arrange
        var obj = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "Plain",
            ObjectType = objectType,
            DataType = 0x0007,
            AccessType = AccessType.ReadWrite,
            SubNumber = 2
        };
        obj.SubObjects[0] = Var(0, "Count", 0x0005);
        obj.SubObjects[1] = Var(1, "Value", 0x0007);
        var eds = EdsWith(obj);

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        SectionKeys(written, "2000").Should().NotContain("SubNumber");
    }

    [Theory]
    [MemberData(nameof(ObjectTypesWithoutSubIndexes))]
    public void WriteToString_ObjectTypeWithoutSubIndexes_OmitsCompactSubObj(byte objectType)
    {
        // Arrange
        var obj = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "Plain",
            ObjectType = objectType,
            DataType = 0x0007,
            AccessType = AccessType.ReadWrite,
            CompactSubObj = 3
        };
        var eds = EdsWith(obj);

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        SectionKeys(written, "2000").Should().NotContain(new[] { "CompactSubObj", "SubNumber" });
        written.Should().NotContain("[2000Name]");
    }

    [Fact]
    public void WriteToString_Domain_OmitsPdoMappingAndLimits()
    {
        // Arrange
        var domain = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "Domain",
            ObjectType = CanOpenObjectType.Domain,
            DataType = 0x000F,
            AccessType = AccessType.ReadWrite,
            DefaultValue = "x",
            LowLimit = "0",
            HighLimit = "1",
            PdoMapping = true
        };
        var eds = EdsWith(domain);

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        var keys = SectionKeys(written, "2000");
        keys.Should().Contain(new[] { "DataType", "AccessType", "DefaultValue" });
        keys.Should().NotContain(new[] { "PDOMapping", "LowLimit", "HighLimit" });
    }

    [Fact]
    public void WriteToString_DomainSubObject_OmitsPdoMappingAndLimits()
    {
        // Arrange
        var record = StructuredObject(CanOpenObjectType.Record);
        record.SubObjects[1].ObjectType = CanOpenObjectType.Domain;
        record.SubObjects[1].LowLimit = "0";
        record.SubObjects[1].HighLimit = "1";
        record.SubObjects[1].PdoMapping = true;
        var eds = EdsWith(record);

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        var keys = SectionKeys(written, "2000sub1");
        keys.Should().Contain(new[] { "DataType", "AccessType" });
        keys.Should().NotContain(new[] { "PDOMapping", "LowLimit", "HighLimit" });
    }

    [Fact]
    public void WriteToString_StructuredSubObject_OmitsNotSupportedKeys()
    {
        // Arrange
        var record = StructuredObject(CanOpenObjectType.Record);
        record.SubObjects[1].ObjectType = CanOpenObjectType.Record;
        record.SubObjects[1].DefaultValue = "1";
        record.SubObjects[1].LowLimit = "0";
        record.SubObjects[1].HighLimit = "1";
        var eds = EdsWith(record);

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        SectionKeys(written, "2000sub1").Should().NotContain(StructuredValueKeys);
    }

    [Fact]
    public void WriteToString_Var_KeepsAllValueKeys()
    {
        // Arrange
        var variable = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "Var",
            ObjectType = CanOpenObjectType.Var,
            DataType = 0x0007,
            AccessType = AccessType.ReadWrite,
            DefaultValue = "1",
            LowLimit = "0",
            HighLimit = "2",
            PdoMapping = true
        };
        var eds = EdsWith(variable);

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        SectionKeys(written, "2000").Should().Contain(StructuredValueKeys);
    }

    [Fact]
    public void WriteToString_UndefinedObjectType_KeepsAllKeys()
    {
        // Arrange: Table 7 has no column for NULL or an unassigned code, so nothing is dropped.
        var obj = StructuredObject(0x0A);
        var eds = EdsWith(obj);

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        SectionKeys(written, "2000").Should().Contain(StructuredValueKeys);
        SectionKeys(written, "2000").Should().Contain("SubNumber");
    }

    #endregion

    #region Reader

    [Theory]
    [InlineData("0x6")]
    [InlineData("0x8")]
    [InlineData("0x9")]
    public void ReadStringWithDiagnostics_StructuredObjectWithValueKeys_ReportsEachKey(string objectType)
    {
        // Arrange
        var content = Eds(
            "[2000]",
            "SubNumber=2",
            "ParameterName=Record",
            "ObjectType=" + objectType,
            "DataType=0x0007",
            "AccessType=rw",
            "DefaultValue=1",
            "LowLimit=0",
            "HighLimit=2",
            "PDOMapping=1",
            "[2000sub0]",
            "ParameterName=Count",
            "ObjectType=0x7",
            "DataType=0x0005",
            "AccessType=ro",
            "DefaultValue=1",
            "PDOMapping=0",
            "[2000sub1]",
            "ParameterName=Value",
            "ObjectType=0x7",
            "DataType=0x0007",
            "AccessType=rw",
            "PDOMapping=1");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Diagnostics.Should().OnlyContain(d =>
            d.Code == ParseDiagnosticCodes.IniObjectKeyNotSupported && d.Severity == ParseSeverity.Warning);
        result.Diagnostics.Select(d => d.Path).Should().Equal(
            "2000.DataType",
            "2000.AccessType",
            "2000.DefaultValue",
            "2000.LowLimit",
            "2000.HighLimit",
            "2000.PDOMapping");
        var accessType = result.Diagnostics[1];
        accessType.Line.Should().Be(SourceLine(content, "AccessType=rw"));
        accessType.RawValue.Should().Be("rw");
        accessType.Message.Should().Contain("CiA 306-1 Table 7");

        var obj = result.Model.ObjectDictionary.Objects[0x2000];
        obj.RemainingEntries.Should().BeEmpty();
        obj.SubObjects.Should().HaveCount(2);
    }

    [Fact]
    public void ReadStringWithDiagnostics_StructuredObjectWithEmptyKey_ReportsPresence()
    {
        // Arrange: "DefaultValue=" is present, even though its value is empty.
        var content = Eds(
            "[2000]",
            "SubNumber=1",
            "ParameterName=Record",
            "ObjectType=0x9",
            "DefaultValue=",
            "[2000sub0]",
            "ParameterName=Count",
            "DataType=0x0005",
            "AccessType=ro");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Path.Should().Be("2000.DefaultValue");
        diagnostic.RawValue.Should().BeEmpty();
    }

    [Theory]
    [InlineData("0x7")]
    [InlineData("0x5")]
    [InlineData("0x2")]
    public void ReadStringWithDiagnostics_ObjectTypeWithoutSubIndexesAndSubNumber_ReportsSubNumber(string objectType)
    {
        // Arrange
        var content = Eds(
            "[2000]",
            "SubNumber=0",
            "ParameterName=Plain",
            "ObjectType=" + objectType,
            "DataType=0x0007",
            "AccessType=rw");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.IniObjectKeyNotSupported);
        diagnostic.Path.Should().Be("2000.SubNumber");
        diagnostic.Line.Should().Be(SourceLine(content, "SubNumber=0"));
    }

    [Fact]
    public void ReadStringWithDiagnostics_VarWithoutObjectTypeAndCompactSubObj_ReportsCompactSubObj()
    {
        // Arrange: a missing ObjectType is VAR (Table 7, NOTE 1).
        var content = Eds(
            "[2000]",
            "ParameterName=Plain",
            "DataType=0x0007",
            "AccessType=rw",
            "CompactSubObj=2");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == ParseDiagnosticCodes.IniObjectKeyNotSupported && d.Path == "2000.CompactSubObj");
    }

    [Fact]
    public void ReadStringWithDiagnostics_DomainWithPdoMappingAndLimits_ReportsEachKey()
    {
        // Arrange
        var content = Eds(
            "[2000]",
            "ParameterName=Domain",
            "ObjectType=0x2",
            "DataType=0x000F",
            "AccessType=rw",
            "LowLimit=0",
            "HighLimit=1",
            "PDOMapping=0");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Diagnostics.Select(d => d.Path).Should().Equal(
            "2000.LowLimit",
            "2000.HighLimit",
            "2000.PDOMapping");
    }

    [Fact]
    public void ReadStringWithDiagnostics_DomainSubObjectWithPdoMapping_ReportsSubObjectKey()
    {
        // Arrange
        var content = Eds(
            "[2000]",
            "SubNumber=2",
            "ParameterName=Record",
            "ObjectType=0x9",
            "[2000sub0]",
            "ParameterName=Count",
            "ObjectType=0x7",
            "DataType=0x0005",
            "AccessType=ro",
            "DefaultValue=1",
            "PDOMapping=0",
            "[2000sub1]",
            "ParameterName=Domain",
            "ObjectType=0x2",
            "DataType=0x000F",
            "AccessType=rw",
            "PDOMapping=0");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.IniObjectKeyNotSupported);
        diagnostic.Path.Should().Be("2000sub1.PDOMapping");
        diagnostic.Line.Should().Be(SourceLine(content, "PDOMapping=0", occurrence: 2));
        result.Model.ObjectDictionary.Objects[0x2000].SubObjects[1].RemainingEntries.Should().BeEmpty();
    }

    [Fact]
    public void ReadStringWithDiagnostics_StructuredSubObjectWithValueKeys_ReportsSubObjectKeys()
    {
        // Arrange
        var content = Eds(
            "[2000]",
            "SubNumber=1",
            "ParameterName=Record",
            "ObjectType=0x9",
            "[2000sub0]",
            "ParameterName=Nested",
            "ObjectType=0x9",
            "DataType=0x0005",
            "AccessType=ro");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Diagnostics.Select(d => d.Path).Should().Equal("2000sub0.DataType", "2000sub0.AccessType");
    }

    [Fact]
    public void ReadStringWithDiagnostics_CompactArrayWithSubNumber_DoesNotReport()
    {
        // Arrange: S18 — "nc" (Table 7, footnote c); the writer keeps SubNumber for expanded
        // sub-objects above the compact range, so the reader accepts it.
        var content = Eds(
            "[2000]",
            "SubNumber=6",
            "ParameterName=Compact",
            "ObjectType=0x8",
            "DataType=0x0007",
            "AccessType=rw",
            "DefaultValue=0",
            "PDOMapping=1",
            "CompactSubObj=2",
            "[2000sub5]",
            "ParameterName=Extra",
            "ObjectType=0x7",
            "DataType=0x0005",
            "AccessType=ro");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        result.Model.ObjectDictionary.Objects[0x2000].SubObjects.Keys.Should().Equal((byte)0, (byte)1, (byte)2, (byte)5);
    }

    [Fact]
    public void ReadStringWithDiagnostics_RecordWithZeroCompactSubObj_DoesNotReportCompactSubObj()
    {
        // Arrange: "nc" — CompactSubObj=0 selects the column without CompactSubObj.
        var content = Eds(
            "[2000]",
            "SubNumber=1",
            "ParameterName=Record",
            "ObjectType=0x9",
            "CompactSubObj=0",
            "[2000sub0]",
            "ParameterName=Count",
            "DataType=0x0005",
            "AccessType=ro");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ReadStringWithDiagnostics_NullObjectType_DoesNotReport()
    {
        // Arrange: Table 7 has no column for NULL.
        var content = Eds(
            "[2000]",
            "SubNumber=0",
            "ParameterName=Null",
            "ObjectType=0x0",
            "DataType=0x0007",
            "AccessType=rw",
            "PDOMapping=0");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ReadStringWithDiagnostics_StrictMode_ThrowsOnNotSupportedKey()
    {
        // Arrange
        var content = Eds(
            "[2000]",
            "SubNumber=1",
            "ParameterName=Record",
            "ObjectType=0x9",
            "AccessType=rw",
            "[2000sub0]",
            "ParameterName=Count",
            "DataType=0x0005",
            "AccessType=ro");

        // Act
        var act = () => CanOpenFile.Eds.ReadStringWithDiagnostics(content, Strict);

        // Assert
        var ex = act.Should().Throw<EdsParseException>().Which;
        ex.Code.Should().Be(ParseDiagnosticCodes.IniObjectKeyNotSupported);
        ex.SectionName.Should().Be("2000");
        ex.LineNumber.Should().Be(SourceLine(content, "AccessType=rw"));
        ex.Message.Should().Contain("AccessType").And.Contain("CiA 306-1 Table 7");
    }

    [Fact]
    public void ReadStringWithDiagnostics_StrictModeDomainSubObject_ThrowsWithSubObjectSection()
    {
        // Arrange
        var content = Eds(
            "[2000]",
            "SubNumber=1",
            "ParameterName=Record",
            "ObjectType=0x9",
            "[2000sub0]",
            "ParameterName=Domain",
            "ObjectType=0x2",
            "PDOMapping=0");

        // Act
        var act = () => CanOpenFile.Eds.ReadStringWithDiagnostics(content, Strict);

        // Assert
        var ex = act.Should().Throw<EdsParseException>().Which;
        ex.Code.Should().Be(ParseDiagnosticCodes.IniObjectKeyNotSupported);
        ex.SectionName.Should().Be("2000sub0");
    }

    [Fact]
    public void ReadStringWithDiagnostics_DcfRecordWithAccessType_ReportsKey()
    {
        // Arrange
        var content = string.Join(
            "\n",
            "[FileInfo]",
            "FileName=t.dcf",
            "[DeviceInfo]",
            "VendorName=Test",
            "[DeviceComissioning]",
            "NodeID=1",
            "Baudrate=250",
            "[ManufacturerObjects]",
            "SupportedObjects=1",
            "1=0x2000",
            "[2000]",
            "SubNumber=1",
            "ParameterName=Record",
            "ObjectType=0x9",
            "PDOMapping=0",
            "[2000sub0]",
            "ParameterName=Count",
            "DataType=0x0005",
            "AccessType=ro",
            "ParameterValue=0");

        // Act
        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);

        // Assert
        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == ParseDiagnosticCodes.IniObjectKeyNotSupported && d.Path == "2000.PDOMapping");
    }

    #endregion

    #region Round trip and validated write

    [Fact]
    public void WriteToString_ReadFileWithNotSupportedKeys_RoundTripDropsThemWithoutDiagnostics()
    {
        // Arrange
        var content = Eds(
            "[2000]",
            "SubNumber=2",
            "ParameterName=Record",
            "ObjectType=0x9",
            "DataType=0x0007",
            "AccessType=rw",
            "PDOMapping=0",
            "[2000sub0]",
            "ParameterName=Count",
            "ObjectType=0x7",
            "DataType=0x0005",
            "AccessType=ro",
            "DefaultValue=1",
            "PDOMapping=0",
            "[2000sub1]",
            "ParameterName=Domain",
            "ObjectType=0x2",
            "DataType=0x000F",
            "AccessType=rw",
            "PDOMapping=0");
        var eds = CanOpenFile.Eds.ReadStringWithDiagnostics(content).Model;

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds);
        var reread = CanOpenFile.Eds.ReadStringWithDiagnostics(written);

        // Assert
        reread.Diagnostics.Should().BeEmpty();
        var record = reread.Model.ObjectDictionary.Objects[0x2000];
        record.SubNumber.Should().Be(2);
        record.SubObjects.Keys.Should().Equal((byte)0, (byte)1);
        record.SubObjects[1].ObjectType.Should().Be(CanOpenObjectType.Domain);
        record.SubObjects[1].AccessType.Should().Be(AccessType.ReadWrite);
        CanOpenFile.Eds.WriteToString(reread.Model).Should().Be(written);
    }

    [Fact]
    public void WriteToString_StructuredObjects_ValidatedRoundTrip()
    {
        // Arrange
        var eds = EdsWith(StructuredObject(CanOpenObjectType.Record));

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);
        var reread = CanOpenFile.Eds.ReadStringWithDiagnostics(written, Strict);

        // Assert
        reread.Diagnostics.Should().BeEmpty();
        reread.Model.ObjectDictionary.Objects[0x2000].SubObjects.Should().HaveCount(2);
    }

    [Theory]
    [MemberData(nameof(ObjectTypesWithoutSubIndexes))]
    public void WriteToString_ObjectTypeWithoutSubIndexesHasSubObjects_ValidatedWriteRejects(byte objectType)
    {
        // Arrange
        var obj = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "Plain",
            ObjectType = objectType,
            DataType = 0x0007,
            AccessType = AccessType.ReadWrite,
            SubNumber = 1
        };
        obj.SubObjects[0] = Var(0, "Count", 0x0005);
        var eds = EdsWith(obj);

        // Act
        var act = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        act.Should().Throw<ModelValidationException>().Which.Issues.Should().ContainSingle(issue =>
            issue.Path == "ObjectDictionary.Objects[0x2000].SubObjects" &&
            issue.Code == ValidationIssueCodes.IniSubObjectsNotSupported);
    }

    [Fact]
    public void WriteToString_DcfVarWithSubObjects_ValidatedWriteRejects()
    {
        // Arrange
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        dcf.ObjectDictionary.Objects[0x1000].SubNumber = 1;
        dcf.ObjectDictionary.Objects[0x1000].SubObjects[0] = Var(0, "Count", 0x0005);

        // Act
        var act = () => CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);

        // Assert
        act.Should().Throw<ModelValidationException>().Which.Issues.Should().Contain(issue =>
            issue.Path == "ObjectDictionary.Objects[0x1000].SubObjects" &&
            issue.Code == ValidationIssueCodes.IniSubObjectsNotSupported);
    }

    [Fact]
    public void WriteToString_VarWithSubObjects_UnvalidatedWriteStillSucceeds()
    {
        // Arrange
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.ObjectDictionary.Objects[0x1000].SubObjects[0] = Var(0, "Count", 0x0005);

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        written.Should().Contain("[1000sub0]");
        SectionKeys(written, "1000").Should().NotContain("SubNumber");
    }

    [Fact]
    public void WriteToString_XddVarWithSubObjects_ValidatedWriteIsNotAffected()
    {
        // Arrange: Table 7 is a CiA 306 (INI) rule; CiA 311 does not tie sub-objects to the object type.
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.ObjectDictionary.Objects[0x1000].SubNumber = 1;
        eds.ObjectDictionary.Objects[0x1000].SubObjects[0] = Var(0, "Count", 0x0005);

        // Act
        var act = () => CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    private static CanOpenObject StructuredObject(byte objectType)
    {
        var obj = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "Structured",
            ObjectType = objectType,
            DataType = 0x0007,
            AccessType = AccessType.ReadWrite,
            DefaultValue = "1",
            LowLimit = "0",
            HighLimit = "2",
            PdoMapping = true,
            SubNumber = 2
        };
        obj.SubObjects[0] = Var(0, "Count", 0x0005);
        obj.SubObjects[0].DefaultValue = "1";
        obj.SubObjects[1] = Var(1, "Value", 0x0007);
        obj.SubObjects[1].PdoMapping = true;
        return obj;
    }

    private static CanOpenSubObject Var(byte subIndex, string name, ushort dataType)
        => new()
        {
            SubIndex = subIndex,
            ParameterName = name,
            ObjectType = CanOpenObjectType.Var,
            DataType = dataType,
            AccessType = AccessType.ReadOnly
        };

    private static ElectronicDataSheet EdsWith(CanOpenObject obj)
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        AddManufacturerObject(eds, obj);
        return eds;
    }

    private static void AddManufacturerObject(ICanOpenFileModel model, CanOpenObject obj)
    {
        model.ObjectDictionary.ManufacturerObjects.Add(obj.Index);
        model.ObjectDictionary.Objects[obj.Index] = obj;
    }

    /// <summary>Builds an EDS with one manufacturer object 0x2000 from <paramref name="objectLines"/>.</summary>
    private static string Eds(params string[] objectLines)
    {
        var lines = new List<string>
        {
            "[FileInfo]",
            "FileName=t.eds",
            "EDSVersion=4.0",
            "[DeviceInfo]",
            "VendorName=Test",
            "[ManufacturerObjects]",
            "SupportedObjects=1",
            "1=0x2000"
        };
        lines.AddRange(objectLines);
        return string.Join("\n", lines);
    }

    /// <summary>Keys of <c>[<paramref name="section"/>]</c> in written INI text, in file order.</summary>
    private static List<string> SectionKeys(string text, string section)
    {
        var keys = new List<string>();
        var inSection = false;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("[", StringComparison.Ordinal))
            {
                inSection = string.Equals(line, "[" + section + "]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            var separator = line.IndexOf('=');
            if (inSection && separator > 0)
                keys.Add(line[..separator]);
        }

        return keys;
    }

    private static int SourceLine(string content, string exactLine, int occurrence = 1)
    {
        var lines = content.Split('\n');
        var seen = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i] == exactLine && ++seen == occurrence)
                return i + 1;
        }

        throw new InvalidOperationException("Line not found: " + exactLine);
    }
}
