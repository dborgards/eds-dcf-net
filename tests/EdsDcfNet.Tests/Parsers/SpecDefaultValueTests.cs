namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet.Diagnostics;
using EdsDcfNet.Models;
using EdsDcfNet.Tests.Utilities;
using EdsDcfNet.Validation;

/// <summary>
/// CiA 306-1 v1.4.0 default and count values: Table 6 (<c>SubNumber</c> is the number of
/// sub-indexes, "including the sub-index 00h" per clause 6.6.3.2), Table 7 NOTE 1 (an empty
/// <c>ObjectType</c> equals VAR), Table 1 footnote a (a missing <c>EDSVersion</c> equals "3.0"),
/// and Table 7 DOMAIN replacement values (<c>AccessType</c> rw, <c>DataType</c> DOMAIN 000Fh).
/// Review findings S11, S1a, S1b, S1c, #581.
/// </summary>
public class SpecDefaultValueTests
{
    private static readonly CanOpenFileOptions Strict = new() { StrictParsing = true };

    #region S11 SubNumber is the sub-index count

    [Fact]
    public void WriteToString_RecordWithoutSubNumber_WritesCountIncludingSubIndexZero()
    {
        // Arrange: sub-indexes 00h, 01h, 02h and no SubNumber -> the count is 3, not the highest index 2.
        var record = Record(0, 1, 2);

        // Act
        var written = CanOpenFile.Eds.WriteToString(EdsWith(record));

        // Assert
        SectionValue(written, "2000", "SubNumber").Should().Be("3");
    }

    [Fact]
    public void WriteToString_RecordWithGapInSubIndexes_WritesCountNotHighestSubIndex()
    {
        // Arrange: 00h, 01h and 04h are described -> three entries.
        var record = Record(0, 1, 4);

        // Act
        var written = CanOpenFile.Eds.WriteToString(EdsWith(record));

        // Assert
        SectionValue(written, "2000", "SubNumber").Should().Be("3");
        CanOpenFile.Eds.ReadString(written).ObjectDictionary.Objects[0x2000].SubObjects.Should().HaveCount(3);
    }

    [Fact]
    public void WriteToString_RecordWithSubIndexFfAndNoSubNumber_DoesNotCountSubIndexFf()
    {
        // Arrange: Table 6 excludes sub-index FFh from SubNumber.
        var record = Record(0, 1, 0xFF);

        // Act
        var written = CanOpenFile.Eds.WriteToString(EdsWith(record));

        // Assert
        SectionValue(written, "2000", "SubNumber").Should().Be("2");
    }

    [Fact]
    public void WriteToString_RecordWithoutSubNumber_StrictValidatorAcceptsReread()
    {
        // Arrange: previously SubNumber=2 for three sub-objects failed CheckSubNumberCount.
        var record = Record(0, 1, 2);

        // Act
        var written = CanOpenFile.Eds.WriteToString(EdsWith(record));
        var reread = CanOpenFile.Eds.ReadString(written, Strict);
        var issues = CanOpenFile.Validate(reread, CanOpenValidationOptions.Strict);

        // Assert: the minimal fixture lacks unrelated mandatory entries, so only the SubNumber check is asserted.
        issues.Where(i => i.Path.EndsWith(".SubNumber", StringComparison.Ordinal)).Should().BeEmpty();
    }

    [Fact]
    public void WriteToString_VarWhoseOnlySubObjectIsSubIndexZero_WritesSubNumberOneAndRoundTrips()
    {
        // Arrange (#581)
        var variable = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "Var",
            ObjectType = CanOpenObjectType.Var,
            DataType = 0x0007,
            AccessType = AccessType.ReadWrite
        };
        variable.SubObjects[0] = SubVar(0);

        // Act
        var written = CanOpenFile.Eds.WriteToString(EdsWith(variable));
        var reread = CanOpenFile.Eds.ReadString(written);

        // Assert
        SectionValue(written, "2000", "SubNumber").Should().Be("1");
        reread.ObjectDictionary.Objects[0x2000].SubObjects.Should().ContainKey(0);
    }

    [Fact]
    public void WriteToString_VarWhoseOnlySubObjectIsSubIndexFf_WritesSubNumberOneAndKeepsSubObject()
    {
        // Arrange: FFh is not counted by SubNumber, but the reader loads VAR sub-objects only for SubNumber > 0.
        var variable = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "Var",
            ObjectType = CanOpenObjectType.Var,
            DataType = 0x0007,
            AccessType = AccessType.ReadWrite
        };
        variable.SubObjects[0xFF] = SubVar(0xFF);

        // Act
        var written = CanOpenFile.Eds.WriteToString(EdsWith(variable));
        var reread = CanOpenFile.Eds.ReadString(written);

        // Assert
        SectionValue(written, "2000", "SubNumber").Should().Be("1");
        reread.ObjectDictionary.Objects[0x2000].SubObjects.Should().ContainKey(0xFF);
    }

    [Fact]
    public void WriteToString_NullObjectWhoseOnlySubObjectIsSubIndexFf_WritesSubNumberOneAndKeepsSubObject()
    {
        // Arrange: ObjectType NULL is not composite, so the reader needs SubNumber > 0 to load sub-objects.
        var obj = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "Null",
            ObjectType = CanOpenObjectType.Null
        };
        obj.SubObjects[0xFF] = SubVar(0xFF);

        // Act
        var written = CanOpenFile.Eds.WriteToString(EdsWith(obj));
        var reread = CanOpenFile.Eds.ReadString(written);

        // Assert
        SectionValue(written, "2000", "SubNumber").Should().Be("1");
        reread.ObjectDictionary.Objects[0x2000].SubObjects.Should().ContainKey(0xFF);
    }

    [Fact]
    public void WriteToString_RecordWhoseOnlySubObjectIsSubIndexFf_KeepsSubNumberZeroAndSubObject()
    {
        // Arrange: composite objects are scanned regardless of SubNumber, so no floor applies.
        var record = Record(0xFF);

        // Act
        var written = CanOpenFile.Eds.WriteToString(EdsWith(record));
        var reread = CanOpenFile.Eds.ReadString(written);

        // Assert
        SectionValue(written, "2000", "SubNumber").Should().Be("0");
        reread.ObjectDictionary.Objects[0x2000].SubObjects.Should().ContainKey(0xFF);
    }

    [Fact]
    public void WriteToString_ExplicitSubNumber_IsWrittenAsStored()
    {
        // Arrange
        var record = Record(0, 1, 2);
        record.SubNumber = 5;

        // Act
        var written = CanOpenFile.Eds.WriteToString(EdsWith(record));

        // Assert
        SectionValue(written, "2000", "SubNumber").Should().Be("5");
    }

    #endregion

    #region S1a empty ObjectType

    [Fact]
    public void ReadString_EmptyObjectType_IsVar()
    {
        // Arrange: Table 7 NOTE 1, "In case, the value of ObjectType is empty, it equals ObjectType VAR."
        var content = Eds("[2000]", "ParameterName=P", "ObjectType=", "DataType=0x0007", "AccessType=rw");

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);

        // Assert
        eds.ObjectDictionary.Objects[0x2000].ObjectType.Should().Be(CanOpenObjectType.Var);
    }

    [Fact]
    public void ReadStringWithDiagnostics_EmptyObjectTypeInStrictMode_DoesNotThrowOrReport()
    {
        // Arrange
        var content = Eds("[2000]", "ParameterName=P", "ObjectType=  ", "DataType=0x0007", "AccessType=rw");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content, Strict);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        result.Model.ObjectDictionary.Objects[0x2000].ObjectType.Should().Be(CanOpenObjectType.Var);
    }

    [Fact]
    public void ReadString_EmptyObjectTypeInSubIndexSection_IsVar()
    {
        // Arrange
        var content = Eds(
            "[2000]", "SubNumber=2", "ParameterName=R", "ObjectType=0x9",
            "[2000sub0]", "ParameterName=Count", "ObjectType=", "DataType=0x0005", "AccessType=ro",
            "[2000sub1]", "ParameterName=V", "ObjectType=", "DataType=0x0007", "AccessType=rw");

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);

        // Assert
        var subObjects = eds.ObjectDictionary.Objects[0x2000].SubObjects;
        subObjects[0].ObjectType.Should().Be(CanOpenObjectType.Var);
        subObjects[1].ObjectType.Should().Be(CanOpenObjectType.Var);
    }

    #endregion

    #region S1b EDSVersion

    [Fact]
    public void ReadString_MissingEdsVersion_ReadsThreePointZero()
    {
        // Arrange: Table 1 footnote a, "If the entry is missing, this is equal to 3.0".
        var content = string.Join("\n", "[FileInfo]", "FileName=t.eds", "[DeviceInfo]", "VendorName=Test");

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);

        // Assert
        eds.FileInfo.EdsVersion.Should().Be("3.0");
    }

    [Fact]
    public void ReadString_MissingFileInfoSection_ReadsEdsVersionThreePointZero()
    {
        // Arrange: an absent section means a missing EDSVersion entry.
        var content = string.Join("\n", "[DeviceInfo]", "VendorName=Test");

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);

        // Assert
        eds.FileInfo.EdsVersion.Should().Be("3.0");
    }

    [Fact]
    public void ReadString_ExplicitEdsVersion_IsKept()
    {
        // Arrange
        var content = string.Join("\n", "[FileInfo]", "EDSVersion=4.0", "[DeviceInfo]", "VendorName=Test");

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);

        // Assert
        eds.FileInfo.EdsVersion.Should().Be("4.0");
    }

    [Fact]
    public void Constructor_NewFileInfo_DefaultsToFourPointZeroForWrittenFiles()
    {
        // Arrange / Act: files written by this library use 4.0 ("shall use 4.0").
        var info = new EdsFileInfo();

        // Assert
        info.EdsVersion.Should().Be("4.0");
    }

    #endregion

    #region S1c DOMAIN defaults

    [Fact]
    public void ReadString_DomainWithoutAccessTypeAndDataType_AppliesTableSevenDefaults()
    {
        // Arrange: Table 7 DOMAIN: DataType o (DOMAIN), AccessType o (rw).
        var content = Eds("[2000]", "ParameterName=D", "ObjectType=0x2");

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);

        // Assert
        var domain = eds.ObjectDictionary.Objects[0x2000];
        domain.AccessType.Should().Be(AccessType.ReadWrite);
        domain.DataType.Should().Be(0x000F);
    }

    [Fact]
    public void ReadStringWithDiagnostics_DomainWithMalformedDataType_ReportsAndDoesNotApplyDefault()
    {
        // Arrange: the default applies to an absent entry only, not to a malformed one.
        var content = Eds("[2000]", "ParameterName=D", "ObjectType=0x2", "DataType=nope", "AccessType=rw");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Code == ParseDiagnosticCodes.InvalidDataType);
        result.Model.ObjectDictionary.Objects[0x2000].DataType.Should().BeNull();
    }

    [Fact]
    public void ReadStringWithDiagnostics_DomainWithoutDataType_AppliesDefaultWithoutDiagnostic()
    {
        // Arrange
        var content = Eds("[2000]", "ParameterName=D", "ObjectType=0x2", "AccessType=rw");

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        result.Model.ObjectDictionary.Objects[0x2000].DataType.Should().Be(0x000F);
    }

    [Fact]
    public void ReadString_DomainWithExplicitAccessTypeAndDataType_KeepsThem()
    {
        // Arrange
        var content = Eds("[2000]", "ParameterName=D", "ObjectType=0x2", "DataType=0x0007", "AccessType=ro");

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);

        // Assert
        var domain = eds.ObjectDictionary.Objects[0x2000];
        domain.AccessType.Should().Be(AccessType.ReadOnly);
        domain.DataType.Should().Be(0x0007);
    }

    [Fact]
    public void ReadString_VarWithoutAccessTypeAndDataType_KeepsReadOnlyAndNoDataType()
    {
        // Arrange: the DOMAIN replacement values do not apply to VAR (AccessType and DataType are "m").
        var content = Eds("[2000]", "ParameterName=V", "ObjectType=0x7");

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);

        // Assert
        var variable = eds.ObjectDictionary.Objects[0x2000];
        variable.AccessType.Should().Be(AccessType.ReadOnly);
        variable.DataType.Should().BeNull();
    }

    [Fact]
    public void WriteToString_DomainReadWithoutDefaults_ValidatedRoundTrip()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Eds("[2000]", "ParameterName=D", "ObjectType=0x2"));

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);
        var reread = CanOpenFile.Eds.ReadString(written, Strict);

        // Assert
        var domain = reread.ObjectDictionary.Objects[0x2000];
        domain.AccessType.Should().Be(AccessType.ReadWrite);
        domain.DataType.Should().Be(0x000F);
    }

    #endregion

    private static CanOpenObject Record(params byte[] subIndexes)
    {
        var obj = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "Record",
            ObjectType = CanOpenObjectType.Record
        };
        foreach (var subIndex in subIndexes)
            obj.SubObjects[subIndex] = SubVar(subIndex);
        return obj;
    }

    private static CanOpenSubObject SubVar(byte subIndex)
        => new()
        {
            SubIndex = subIndex,
            ParameterName = "Sub" + subIndex,
            ObjectType = CanOpenObjectType.Var,
            DataType = 0x0005,
            AccessType = AccessType.ReadWrite
        };

    private static ElectronicDataSheet EdsWith(CanOpenObject obj)
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.DeviceInfo.VendorName = "Test";
        eds.ObjectDictionary.ManufacturerObjects.Add(obj.Index);
        eds.ObjectDictionary.Objects[obj.Index] = obj;
        return eds;
    }

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

    private static string? SectionValue(string text, string section, string key)
    {
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
            if (inSection && separator > 0 && string.Equals(line[..separator], key, StringComparison.OrdinalIgnoreCase))
                return line[(separator + 1)..];
        }

        return null;
    }
}
