namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Validation;

/// <summary>
/// CiA 306-1 Table 7 applies to module objects as well: <c>[MxFixedxxxx]</c> and
/// <c>[MxFixedxxxxsubx]</c> have "the same contents as object descriptions in clause 6.6.3.2"
/// and <c>[MxSubExtxxxx]</c> "the same entries as in a standard object description" (§ 8.3).
/// The reader reports "n" keys, the writer omits them, and a validated write checks only what is
/// written. Compact-list <c>NrOfEntries</c> is read leniently.
/// </summary>
public class ModuleObjectKeyMatrixTests
{
    private static readonly CanOpenFileOptions Strict = new() { StrictParsing = true };

    // ---------------------------------------------------------------------------------------
    // Reader: "n" keys in [MxFixedxxxx] / [MxFixedxxxxsubx] / [MxSubExtxxxx]
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("0x7", "SubNumber=2", "SubNumber")]
    [InlineData("0x5", "CompactSubObj=2", "CompactSubObj")]
    [InlineData("0x9", "AccessType=rw", "AccessType")]
    [InlineData("0x9", "PDOMapping=0", "PDOMapping")]
    [InlineData("0x9", "DataType=0x0005", "DataType")]
    [InlineData("0x8", "DefaultValue=1", "DefaultValue")]
    [InlineData("0x2", "PDOMapping=0", "PDOMapping")]
    [InlineData("0x2", "LowLimit=1", "LowLimit")]
    public void ReadString_ModuleFixedObjectWithNotSupportedKey_ReportsKeyAndKeepsObject(
        string objectType, string entry, string key)
    {
        // Arrange
        var content = Eds(Module("[M1Fixed6000]\nParameterName=Fixed\nObjectType=" + objectType + "\n" + entry + "\n"));

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.SupportedModules[0].FixedObjectDefinitions[0x6000].ParameterName.Should().Be("Fixed");
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniObjectKeyNotSupported)
            .Which.Should().Match<ParseDiagnostic>(d =>
                d.Path == "M1Fixed6000." + key &&
                d.Severity == ParseSeverity.Warning &&
                d.Line != null);
    }

    [Fact]
    public void ReadString_ModuleFixedObjectWithSupportedKeys_ReportsNothing()
    {
        // Arrange — VAR with every key Table 7 allows, RECORD with SubNumber, DOMAIN with DataType.
        var content = Eds(Module(
            "[M1Fixed6000]\nParameterName=Var\nObjectType=0x7\nDataType=0x0005\nAccessType=rw\nDefaultValue=0\nLowLimit=0\nHighLimit=9\nPDOMapping=1\n\n"
            + "[M1Fixed6100]\nParameterName=Record\nObjectType=0x9\nSubNumber=1\n\n"
            + "[M1Fixed6100sub0]\nParameterName=Count\nObjectType=0x7\nDataType=0x0005\nAccessType=ro\nDefaultValue=0\nPDOMapping=0\n\n"
            + "[M1Fixed6200]\nParameterName=Domain\nObjectType=0x2\nDataType=0x000F\nAccessType=rw\n"));

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Code == ParseDiagnosticCodes.IniObjectKeyNotSupported);
    }

    [Fact]
    public void ReadString_ModuleFixedSubObjectWithSubNumber_ReportsAndDoesNotKeepEntry()
    {
        // Arrange
        var content = Eds(Module(
            "[M1Fixed6100]\nParameterName=Record\nObjectType=0x9\nSubNumber=1\n\n"
            + "[M1Fixed6100sub0]\nParameterName=Count\nObjectType=0x7\nDataType=0x0005\nAccessType=ro\nSubNumber=3\n"));

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        var sub = result.Model.SupportedModules[0].FixedObjectDefinitions[0x6100].SubObjects[0];
        sub.RemainingEntries.Should().NotContainKey("SubNumber");
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniObjectKeyNotSupported)
            .Which.Path.Should().Be("M1Fixed6100sub0.SubNumber");
    }

    [Fact]
    public void ReadString_ModuleFixedObjectWithNotSupportedKey_StrictParsing_Throws()
    {
        // Arrange
        var content = Eds(Module("[M1Fixed6000]\nParameterName=Record\nObjectType=0x9\nAccessType=rw\n"));

        // Act
        var act = () => CanOpenFile.Eds.ReadString(content, Strict);

        // Assert
        act.Should().Throw<EdsParseException>().Which.Should().Match<EdsParseException>(e =>
            e.Code == ParseDiagnosticCodes.IniObjectKeyNotSupported &&
            e.SectionName == "M1Fixed6000" &&
            e.LineNumber != null);
    }

    [Theory]
    [InlineData("ObjectType=0x9\nAccessType=rw\n", "AccessType")]
    [InlineData("SubNumber=1\nDataType=0x0005\n", "SubNumber")]
    [InlineData("ObjectType=0x2\nPDOMapping=1\n", "PDOMapping")]
    public void ReadString_ModuleSubExtensionWithNotSupportedKey_ReportsKey(string entries, string key)
    {
        // Arrange — a missing ObjectType is VAR (CiA 306-1 Table 7 NOTE 1).
        var content = Eds(Module("[M1SubExt6000]\nParameterName=Ext\n" + entries + "Count=1\n"));

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.SupportedModules[0].SubExtensionDefinitions[0x6000].ParameterName.Should().Be("Ext");
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniObjectKeyNotSupported)
            .Which.Path.Should().Be("M1SubExt6000." + key);
    }

    [Fact]
    public void ReadString_SpecExampleSubExtension_ReportsNothing()
    {
        // Arrange — CiA 306-1 Annex example [M1SubExt6000] (VAR without ObjectType).
        var content = Eds(Module(
            "[M1SubExt6000]\nParameterName=ReadState8InputLines\nDataType=5\nDefaultValue=0\nPDOMapping=1\nAccessType=ro\nCount=1\n"));

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Code == ParseDiagnosticCodes.IniObjectKeyNotSupported);
    }

    [Fact]
    public void ReadString_ModuleSubExtensionWithNotSupportedKey_StrictParsing_Throws()
    {
        // Arrange
        var content = Eds(Module("[M1SubExt6000]\nParameterName=Ext\nObjectType=0x9\nAccessType=rw\nCount=1\n"));

        // Act
        var act = () => CanOpenFile.Eds.ReadString(content, Strict);

        // Assert
        act.Should().Throw<EdsParseException>().Which.Should().Match<EdsParseException>(e =>
            e.Code == ParseDiagnosticCodes.IniObjectKeyNotSupported &&
            e.SectionName == "M1SubExt6000");
    }

    // ---------------------------------------------------------------------------------------
    // Writer: "n" keys omitted
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void WriteToString_ModuleFixedRecord_OmitsNotSupportedKeys()
    {
        // Arrange
        var eds = ModelWithModule(new CanOpenObject
        {
            Index = 0x6100,
            ParameterName = "Record",
            ObjectType = CanOpenObjectType.Record,
            DataType = 0x0005,
            AccessType = AccessType.ReadWrite,
            DefaultValue = "1",
            LowLimit = "0",
            HighLimit = "9",
            PdoMapping = true,
            SubObjects =
            {
                [0] = new CanOpenSubObject { SubIndex = 0, ParameterName = "Count", DataType = 0x0005, DefaultValue = "1" }
            }
        });

        // Act
        var section = Section(CanOpenFile.Eds.WriteToString(eds), "M1Fixed6100");

        // Assert
        section.Should().Contain("SubNumber=1").And.Contain("ObjectType=0x9");
        section.Should().NotContain("DataType=").And.NotContain("AccessType=").And.NotContain("PDOMapping=")
            .And.NotContain("DefaultValue=").And.NotContain("LowLimit=").And.NotContain("HighLimit=");
    }

    [Fact]
    public void WriteToString_ModuleFixedVarAndDomain_OmitNotSupportedKeys()
    {
        // Arrange
        var eds = ModelWithModule(
            new CanOpenObject
            {
                Index = 0x6000,
                ParameterName = "Var",
                ObjectType = CanOpenObjectType.Var,
                DataType = 0x0005,
                SubNumber = 3,
                CompactSubObj = 2
            },
            new CanOpenObject
            {
                Index = 0x6200,
                ParameterName = "Domain",
                ObjectType = CanOpenObjectType.Domain,
                DataType = 0x000F,
                LowLimit = "0",
                HighLimit = "9",
                PdoMapping = true
            });

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        var var = Section(written, "M1Fixed6000");
        var.Should().Contain("DataType=0x5").And.Contain("AccessType=").And.Contain("PDOMapping=0");
        var.Should().NotContain("SubNumber=").And.NotContain("CompactSubObj=");
        var domain = Section(written, "M1Fixed6200");
        domain.Should().Contain("DataType=0xF").And.Contain("AccessType=");
        domain.Should().NotContain("PDOMapping=").And.NotContain("LowLimit=").And.NotContain("HighLimit=");
    }

    [Fact]
    public void WriteToString_ModuleFixedVarWithSubObjects_KeepsSubNumberAndValidatedWriteRejects()
    {
        // Arrange — decision E10: SubNumber of a VAR with sub-objects is still written unvalidated.
        var obj = new CanOpenObject { Index = 0x6000, ParameterName = "Var", ObjectType = CanOpenObjectType.Var, DataType = 0x0005 };
        obj.SubObjects[1] = new CanOpenSubObject { SubIndex = 1, ParameterName = "First", DataType = 0x0005 };
        var eds = ModelWithModule(obj);

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds);
        var validated = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        Section(written, "M1Fixed6000").Should().Contain("SubNumber=1");
        CanOpenFile.Eds.ReadString(written).SupportedModules[0].FixedObjectDefinitions[0x6000].SubObjects
            .Should().ContainKey(1);
        validated.Should().Throw<ModelValidationException>().Which.Issues.Should().Contain(issue =>
            issue.Path == "SupportedModules[0].FixedObjectDefinitions[0x6000].SubObjects" &&
            issue.Code == ValidationIssueCodes.IniSubObjectsNotSupported);
    }

    [Fact]
    public void WriteToString_ModuleFixedSubObject_OmitsNotSupportedKeysAndKeptSubNumber()
    {
        // Arrange — a RECORD sub-index: Table 7 for the sub-object's own type; SubNumber never.
        var obj = new CanOpenObject { Index = 0x6100, ParameterName = "Record", ObjectType = CanOpenObjectType.Record };
        var sub = new CanOpenSubObject
        {
            SubIndex = 1,
            ParameterName = "Domain sub",
            ObjectType = CanOpenObjectType.Domain,
            DataType = 0x000F,
            PdoMapping = true,
            LowLimit = "0"
        };
        sub.RemainingEntries.Add("SubNumber", "3");
        sub.RemainingEntries.Add("VendorKey", "kept");
        obj.SubObjects[1] = sub;
        var eds = ModelWithModule(obj);

        // Act
        var section = Section(CanOpenFile.Eds.WriteToString(eds), "M1Fixed6100sub1");

        // Assert
        section.Should().Contain("DataType=0xF").And.Contain("VendorKey=kept");
        section.Should().NotContain("PDOMapping=").And.NotContain("LowLimit=").And.NotContain("SubNumber=");
    }

    [Fact]
    public void WriteToString_ModuleSubExtensionRecord_OmitsNotSupportedKeys()
    {
        // Arrange
        var eds = ModelWithModule();
        eds.SupportedModules[0].SubExtensionDefinitions[0x6000] = new ModuleSubExtension
        {
            Index = 0x6000,
            ParameterName = "Ext",
            ObjectType = CanOpenObjectType.Record,
            SubNumber = 2,
            DataType = 0x0005,
            AccessType = AccessType.ReadWrite,
            DefaultValue = "1",
            LowLimit = "0",
            HighLimit = "9",
            PdoMapping = true,
            Count = "1"
        };

        // Act
        var section = Section(CanOpenFile.Eds.WriteToString(eds), "M1SubExt6000");

        // Assert
        section.Should().Contain("SubNumber=2").And.Contain("ObjectType=0x9").And.Contain("Count=1");
        section.Should().NotContain("DataType=").And.NotContain("AccessType=").And.NotContain("PDOMapping=")
            .And.NotContain("DefaultValue=").And.NotContain("LowLimit=").And.NotContain("HighLimit=");
    }

    [Fact]
    public void WriteToString_ModuleSubExtensionVar_OmitsSubNumberAndCompactSubObj()
    {
        // Arrange — no ObjectType is VAR.
        var eds = ModelWithModule();
        eds.SupportedModules[0].SubExtensionDefinitions[0x6000] = new ModuleSubExtension
        {
            Index = 0x6000,
            ParameterName = "Ext",
            SubNumber = 2,
            CompactSubObj = 2,
            DataType = 0x0005,
            AccessType = AccessType.ReadOnly,
            PdoMapping = true,
            Count = "1"
        };

        // Act
        var section = Section(CanOpenFile.Eds.WriteToString(eds), "M1SubExt6000");

        // Assert
        section.Should().Contain("DataType=0x5").And.Contain("AccessType=ro").And.Contain("PDOMapping=1");
        section.Should().NotContain("SubNumber=").And.NotContain("CompactSubObj=");
    }

    // ---------------------------------------------------------------------------------------
    // Validated write checks only what is written
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void WriteToString_ModuleObjectsWithInvalidTextInOmittedKeys_ValidatedWriteSucceeds()
    {
        // Arrange — DefaultValue is "n" for a RECORD without CompactSubObj, LowLimit for a DOMAIN.
        var eds = ModelWithModule(
            new CanOpenObject
            {
                Index = 0x6100,
                ParameterName = "Record",
                ObjectType = CanOpenObjectType.Record,
                SubNumber = 1,
                DefaultValue = "bad\nvalue",
                SubObjects =
                {
                    [0] = new CanOpenSubObject
                    {
                        SubIndex = 0,
                        ParameterName = "Domain sub",
                        ObjectType = CanOpenObjectType.Domain,
                        DataType = 0x000F,
                        LowLimit = "bad\nlow"
                    }
                }
            });
        eds.SupportedModules[0].SubExtensionDefinitions[0x6000] = new ModuleSubExtension
        {
            Index = 0x6000,
            ParameterName = "Ext",
            ObjectType = CanOpenObjectType.Record,
            SubNumber = 1,
            HighLimit = "bad\nhigh",
            Count = "1"
        };

        eds.SupportedModules[0].SubExtends.Add(0x6000);

        // Act
        var act = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void WriteToString_ModuleObjectsWithInvalidTextInWrittenKeys_ValidatedWriteRejects()
    {
        // Arrange
        var eds = ModelWithModule(new CanOpenObject
        {
            Index = 0x6000,
            ParameterName = "Var",
            ObjectType = CanOpenObjectType.Var,
            DataType = 0x0005,
            DefaultValue = "bad\nvalue"
        });
        eds.SupportedModules[0].SubExtensionDefinitions[0x6000] = new ModuleSubExtension
        {
            Index = 0x6000,
            ParameterName = "Ext",
            DataType = 0x0005,
            LowLimit = "bad\nlow",
            Count = "1"
        };

        eds.SupportedModules[0].SubExtends.Add(0x6000);

        // Act
        var act = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        act.Should().Throw<ModelValidationException>().Which.Issues.Select(issue => issue.Path).Should().Contain(new[]
        {
            "SupportedModules[0].FixedObjectDefinitions[0x6000].DefaultValue",
            "SupportedModules[0].SubExtensionDefinitions[0x6000].LowLimit"
        });
    }

    // ---------------------------------------------------------------------------------------
    // Round trip
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteToString_ModuleObjectsOfEachType_ValidatedRoundTrip(bool validated)
    {
        // Arrange — "n" keys in the file are reported on read and not written back.
        var content = Eds(Module(
            "[M1FixedObjects]\nNrOfEntries=3\n1=0x6000\n2=0x6100\n3=0x6200\n\n"
            + "[M1SubExtends]\nNrOfEntries=1\n1=0x6000\n\n"
            + "[M1Fixed6000]\nParameterName=Var\nObjectType=0x7\nDataType=0x0005\nAccessType=rw\nDefaultValue=3\nPDOMapping=1\nCompactSubObj=2\n\n"
            + "[M1Fixed6100]\nParameterName=Record\nObjectType=0x9\nSubNumber=1\nAccessType=rw\nPDOMapping=0\n\n"
            + "[M1Fixed6100sub0]\nParameterName=Count\nObjectType=0x7\nDataType=0x0005\nAccessType=ro\nDefaultValue=0\nPDOMapping=0\n\n"
            + "[M1Fixed6200]\nParameterName=Domain\nObjectType=0x2\nDataType=0x000F\nAccessType=rw\nPDOMapping=0\n\n"
            + "[M1SubExt6000]\nParameterName=Ext\nDataType=0x0005\nAccessType=ro\nPDOMapping=1\nSubNumber=4\nCount=1\n"));
        var read = CanOpenFile.Eds.ReadStringWithDiagnostics(content);
        var options = validated ? CanOpenWriteOptions.Validated : null;

        // Act
        var written = CanOpenFile.Eds.WriteToString(read.Model, options);
        var again = CanOpenFile.Eds.ReadStringWithDiagnostics(written);

        // Assert
        read.Diagnostics.Where(d => d.Code == ParseDiagnosticCodes.IniObjectKeyNotSupported)
            .Select(d => d.Path).Should().BeEquivalentTo(
                "M1Fixed6000.CompactSubObj",
                "M1Fixed6100.AccessType",
                "M1Fixed6100.PDOMapping",
                "M1Fixed6200.PDOMapping",
                "M1SubExt6000.SubNumber");
        again.Diagnostics.Should().NotContain(d => d.Code == ParseDiagnosticCodes.IniObjectKeyNotSupported);
        var module = again.Model.SupportedModules[0];
        module.FixedObjectDefinitions[0x6000].DefaultValue.Should().Be("3");
        module.FixedObjectDefinitions[0x6000].PdoMapping.Should().BeTrue();
        module.FixedObjectDefinitions[0x6100].SubObjects[0].ParameterName.Should().Be("Count");
        module.FixedObjectDefinitions[0x6200].DataType.Should().Be((ushort)0x000F);
        module.SubExtensionDefinitions[0x6000].PdoMapping.Should().BeTrue();
        module.SubExtensionDefinitions[0x6000].SubNumber.Should().BeNull();
    }

    [Fact]
    public void WriteToString_DcfModuleFixedRecord_OmitsNotSupportedKeys()
    {
        // Arrange
        var dcf = CanOpenFile.Dcf.ReadString(Dcf(Module(
            "[M1Fixed6100]\nParameterName=Record\nObjectType=0x9\nSubNumber=1\nAccessType=rw\nPDOMapping=0\nParameterValue=1\n")));

        // Act
        var section = Section(CanOpenFile.Dcf.WriteToString(dcf), "M1Fixed6100");

        // Assert
        section.Should().Contain("ParameterValue=1");
        section.Should().NotContain("AccessType=").And.NotContain("PDOMapping=");
    }

    // ---------------------------------------------------------------------------------------
    // Compact-list NrOfEntries
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("abc")]
    [InlineData("65536")]
    public void ReadString_MalformedCompactNameCount_ReportsAndAppliesNames(string count)
    {
        // Arrange
        var content = Eds(CompactArray + "[2000Name]\nNrOfEntries=" + count + "\n1=First\n", optionalObjects: true);

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.ObjectDictionary.Objects[0x2000].SubObjects[1].ParameterName.Should().Be("First");
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.InvalidCompactListCount)
            .Which.Should().Match<ParseDiagnostic>(d =>
                d.Path == "2000Name.NrOfEntries" &&
                d.RawValue == count &&
                d.Line != null);
    }

    [Fact]
    public void ReadString_CompactNameCountAtMaxValue_ReportsNothing()
    {
        // Arrange
        var content = Eds(CompactArray + "[2000Name]\nNrOfEntries=65535\n1=First\n", optionalObjects: true);

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Model.ObjectDictionary.Objects[0x2000].SubObjects[1].ParameterName.Should().Be("First");
        result.Diagnostics.Should().NotContain(d => d.Code == ParseDiagnosticCodes.InvalidCompactListCount);
    }

    [Theory]
    [InlineData("Value")]
    [InlineData("Denotation")]
    public void ReadString_DcfMalformedCompactValueCount_ReportsAndAppliesEntries(string suffix)
    {
        // Arrange
        var content = Dcf(CompactArray + "[2000" + suffix + "]\nNrOfEntries=abc\n1=42\n", optionalObjects: true);

        // Act
        var result = CanOpenFile.Dcf.ReadStringWithDiagnostics(content);

        // Assert
        var sub = result.Model.ObjectDictionary.Objects[0x2000].SubObjects[1];
        (suffix == "Value" ? sub.ParameterValue : sub.Denotation).Should().Be("42");
        result.Diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.InvalidCompactListCount)
            .Which.Path.Should().Be("2000" + suffix + ".NrOfEntries");
    }

    [Fact]
    public void ReadString_MalformedCompactListCount_StrictParsing_Throws()
    {
        // Arrange
        var content = Eds(CompactArray + "[2000Name]\nNrOfEntries=abc\n1=First\n", optionalObjects: true);

        // Act
        var act = () => CanOpenFile.Eds.ReadString(content, Strict);

        // Assert
        act.Should().Throw<EdsParseException>().Which.Should().Match<EdsParseException>(e =>
            e.Code == ParseDiagnosticCodes.InvalidCompactListCount &&
            e.SectionName == "2000Name" &&
            e.LineNumber != null);
    }

    [Fact]
    public void WriteToString_MalformedCompactNameCount_RoundTripsNames()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Eds(CompactArray + "[2000Name]\nNrOfEntries=abc\n1=First\n", optionalObjects: true));

        // Act
        var again = CanOpenFile.Eds.ReadStringWithDiagnostics(CanOpenFile.Eds.WriteToString(eds));

        // Assert
        again.Model.ObjectDictionary.Objects[0x2000].SubObjects[1].ParameterName.Should().Be("First");
        again.Diagnostics.Should().NotContain(d => d.Code == ParseDiagnosticCodes.InvalidCompactListCount);
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------

    private const string CompactArray =
        "[2000]\nParameterName=Array\nObjectType=0x8\nDataType=0x0005\nAccessType=rw\nDefaultValue=0\nPDOMapping=0\nCompactSubObj=2\n\n";

    private static ElectronicDataSheet ModelWithModule(params CanOpenObject[] fixedObjects)
    {
        var eds = CanOpenFile.Eds.ReadString(Eds(string.Empty));
        var module = new ModuleInfo { ModuleNumber = 1, ProductName = "Module", OrderCode = "M-1" };
        foreach (var obj in fixedObjects)
        {
            module.FixedObjects.Add(obj.Index);
            module.FixedObjectDefinitions[obj.Index] = obj;
        }

        eds.SupportedModules.Add(module);
        return eds;
    }

    /// <summary>The lines of <paramref name="sectionName"/> in <paramref name="text"/>, up to the next header.</summary>
    private static string Section(string text, string sectionName)
    {
        var header = "[" + sectionName + "]";
        var start = text.IndexOf(header, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, "the writer emits " + header);
        var end = text.IndexOf("\n[", start + header.Length, StringComparison.Ordinal);
        return end < 0 ? text[start..] : text[start..end];
    }

    private static string Module(string sections)
        => "[SupportedModules]\nNrOfEntries=1\n1=0x0001\n\n[M1ModuleInfo]\nProductName=Module\nOrderCode=M-1\n\n" + sections + "\n";

    private static string Eds(string extraSections, bool optionalObjects = false)
        => "[DeviceInfo]\nVendorName=Test\nProductName=Test\n\n"
           + "[MandatoryObjects]\nSupportedObjects=1\n1=0x1000\n\n"
           + (optionalObjects ? "[OptionalObjects]\nSupportedObjects=1\n1=0x2000\n\n" : string.Empty)
           + "[1000]\nParameterName=Device Type\nObjectType=0x7\nDataType=0x0007\nAccessType=ro\nDefaultValue=0\nPDOMapping=0\n\n"
           + extraSections;

    private static string Dcf(string extraSections, bool optionalObjects = false)
        => "[DeviceInfo]\nVendorName=Test\nProductName=Test\n\n"
           + "[DeviceComissioning]\nNodeID=5\nNodeName=Node\nBaudrate=500\nNetNumber=1\nNetworkName=Net\nCANopenManager=0\n\n"
           + "[MandatoryObjects]\nSupportedObjects=1\n1=0x1000\n\n"
           + (optionalObjects ? "[OptionalObjects]\nSupportedObjects=1\n1=0x2000\n\n" : string.Empty)
           + "[1000]\nParameterName=Device Type\nObjectType=0x7\nDataType=0x0007\nAccessType=ro\nDefaultValue=0\nPDOMapping=0\n\n"
           + extraSections;
}
