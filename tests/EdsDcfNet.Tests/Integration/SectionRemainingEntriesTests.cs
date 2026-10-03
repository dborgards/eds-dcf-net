namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;

/// <summary>
/// Unknown and reserved keys of the standard EDS/DCF sections survive read and write.
/// </summary>
/// <remarks>
/// CiA 306-1 v1.4.0 § 6.2 keeps additional entries "in order to support future extensions";
/// § 6.5 reserves the [DeviceInfo] entries ProductVersion, ProductRevision,
/// LMT_ManufacturerName, LMT_ProductName, ExtendedBootUpMaster and ExtendedBootUpSlave
/// "for compatibility reasons"; § 6.6.3.1 numbers object list entries from 1 to
/// SupportedObjects.
/// </remarks>
public class SectionRemainingEntriesTests
{
    // Raw literals take the line endings of the checkout (CRLF on Windows). The tests edit these
    // fixtures with "\n" patterns, so they are normalized to LF first.
    private static readonly string Base = Lf("""
        [FileInfo]
        FileName=test.eds
        FileVersion=1
        FileRevision=0
        EDSVersion=4.0

        [DeviceInfo]
        VendorName=Vendor
        ProductName=Product

        [MandatoryObjects]
        SupportedObjects=1
        1=0x1000

        [1000]
        ParameterName=Device type
        ObjectType=0x7
        DataType=0x0007
        AccessType=ro
        DefaultValue=0
        PDOMapping=0

        """);

    private static readonly string Dcf = Lf("""
        [DeviceComissioning]
        NodeID=2
        NodeName=Node
        Baudrate=250
        NetNumber=1
        NetworkName=Net
        CANopenManager=0

        """);

    private static string Lf(string text) => text.Replace("\r\n", "\n");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadString_DeviceInfoReservedKeys_ArePreservedInOrderOnWrite(bool isDcf)
    {
        // Arrange — CiA 306-1 v1.4.0 § 6.5 reserved entries.
        var content = Base.Replace(
            "ProductName=Product\n",
            "ProductName=Product\nProductVersion=3\nProductRevision=4\nLMT_ManufacturerName=ACME\nLMT_ProductName=Widget\nExtendedBootUpMaster=1\nExtendedBootUpSlave=0\n");
        if (isDcf)
            content += Dcf;

        // Act
        var deviceInfo = isDcf
            ? CanOpenFile.Dcf.ReadString(content).DeviceInfo
            : CanOpenFile.Eds.ReadString(content).DeviceInfo;
        var written = isDcf
            ? CanOpenFile.Dcf.WriteToString(CanOpenFile.Dcf.ReadString(content))
            : CanOpenFile.Eds.WriteToString(CanOpenFile.Eds.ReadString(content));

        // Assert
        deviceInfo.RemainingEntries.Should().Equal(new Dictionary<string, string>
        {
            ["ProductVersion"] = "3",
            ["ProductRevision"] = "4",
            ["LMT_ManufacturerName"] = "ACME",
            ["LMT_ProductName"] = "Widget",
            ["ExtendedBootUpMaster"] = "1",
            ["ExtendedBootUpSlave"] = "0",
        });
        deviceInfo.RemainingEntries.Keys.Should().ContainInOrder(
            "ProductVersion", "ProductRevision", "LMT_ManufacturerName", "LMT_ProductName",
            "ExtendedBootUpMaster", "ExtendedBootUpSlave");
        var lines = SectionLines(written, "DeviceInfo");
        lines.Should().ContainInOrder(
            "ProductVersion=3", "ProductRevision=4", "LMT_ManufacturerName=ACME",
            "LMT_ProductName=Widget", "ExtendedBootUpMaster=1", "ExtendedBootUpSlave=0");
        lines.IndexOf("ProductVersion=3").Should().BeGreaterThan(lines.IndexOf("LSS_Supported=0"),
            "unknown keys follow the keys the writer generates");
    }

    [Fact]
    public void ReadString_ToolsWithZeroItemsAndVendorKey_KeepsToolsSection()
    {
        // Arrange
        var content = Base + """
            [Tools]
            Items=0
            Vendor=kept

            """;

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        eds.Tools.Should().BeEmpty();
        eds.SectionRemainingEntries["Tools"].Should().Equal(new Dictionary<string, string> { ["Vendor"] = "kept" });
        SectionLines(written, "Tools").Should().Equal("Items=0", "Vendor=kept");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadString_SameVendorKeyInTwoModuleSections_KeepsValuePerSection(bool isDcf)
    {
        // Arrange — one module spans several sections (CiA 306-1 v1.4.0 § 8.3).
        var content = Base + """
            [SupportedModules]
            NrOfEntries=1

            [M1ModuleInfo]
            ProductName=Module
            ProductVersion=1
            ProductRevision=0
            OrderCode=M-1
            Vendor=info

            [M1SubExtends]
            NrOfEntries=1
            1=0x2000

            [M1SubExt2000]
            ParameterName=Extension
            DataType=0x0005
            AccessType=ro
            PDOMapping=0
            Count=1
            Vendor=extension

            """;
        if (isDcf)
            content += Dcf;

        // Act
        var store = isDcf
            ? CanOpenFile.Dcf.ReadString(content).SectionRemainingEntries
            : CanOpenFile.Eds.ReadString(content).SectionRemainingEntries;
        var written = isDcf
            ? CanOpenFile.Dcf.WriteToString(CanOpenFile.Dcf.ReadString(content))
            : CanOpenFile.Eds.WriteToString(CanOpenFile.Eds.ReadString(content));

        // Assert
        store["M1ModuleInfo"]["Vendor"].Should().Be("info");
        store["M1SubExt2000"]["Vendor"].Should().Be("extension");
        SectionLines(written, "M1ModuleInfo").Should().Contain("Vendor=info");
        SectionLines(written, "M1SubExt2000").Should().Contain("Vendor=extension");
    }

    [Fact]
    public void ReadString_ObjectListWithEntryAboveCount_KeepsEntryLiterally()
    {
        // Arrange — SupportedObjects=1 but two numbered entries (CiA 306-1 v1.4.0 § 6.6.3.1).
        var content = Base.Replace("1=0x1000\n", "1=0x1000\n2=0x1001\n");

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        eds.ObjectDictionary.MandatoryObjects.Should().Equal((ushort)0x1000);
        eds.SectionRemainingEntries["MandatoryObjects"].Should().Equal(new Dictionary<string, string> { ["2"] = "0x1001" });
        SectionLines(written, "MandatoryObjects").Should().Equal("SupportedObjects=1", "1=0x1000", "2=0x1001");
    }

    [Fact]
    public void WriteString_ObjectAddedOverPreservedEntry_GeneratedEntryWins()
    {
        // Arrange
        var content = Base.Replace("1=0x1000\n", "1=0x1000\n2=0x1001\n");
        var eds = CanOpenFile.Eds.ReadString(content);

        // Act — the caller extends the list, so the writer now generates entry 2 itself.
        eds.ObjectDictionary.MandatoryObjects.Add(0x1018);
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        var lines = SectionLines(written, "MandatoryObjects");
        lines.Count(line => line.StartsWith("2=", StringComparison.Ordinal)).Should().Be(1);
        lines.Should().Equal("SupportedObjects=2", "1=0x1000", "2=0x1018");
    }

    [Fact]
    public void ReadString_CommentLineAboveLinesCount_IsKeptLiterally()
    {
        // Arrange
        var content = Base + """
            [Comments]
            Lines=1
            Line1=first
            Line2=beyond count

            """;

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        eds.Comments!.CommentLines.Should().ContainSingle();
        eds.Comments.RemainingEntries.Should().Equal(new Dictionary<string, string> { ["Line2"] = "beyond count" });
        SectionLines(written, "Comments").Should().Equal("Lines=1", "Line1=first", "Line2=beyond count");
    }

    [Fact]
    public void WriteString_CommentLineAddedOverPreservedLine_GeneratedLineWins()
    {
        // Arrange
        var content = Base + """
            [Comments]
            Lines=1
            Line1=first
            Line2=beyond count

            """;
        var eds = CanOpenFile.Eds.ReadString(content);

        // Act
        eds.Comments!.Lines = 2;
        eds.Comments.CommentLines[2] = "generated";
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        SectionLines(written, "Comments").Should().Equal("Lines=2", "Line1=first", "Line2=generated");
    }

    [Fact]
    public void ReadString_DummyUsageWithForeignKey_KeepsKey()
    {
        // Arrange
        var content = Base + """
            [DummyUsage]
            Dummy0002=1
            Vendor=dummy

            """;

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        eds.ObjectDictionary.DummyUsage.Should().ContainKey(0x0002);
        SectionLines(written, "DummyUsage").Should().Equal("Dummy0002=1", "Vendor=dummy");
    }

    [Fact]
    public void ReadString_DynamicChannelsWithoutSegmentsButVendorKey_KeepsSection()
    {
        // Arrange
        var content = Base + """
            [DynamicChannels]
            NrOfSeg=0
            Vendor=channels

            """;

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        eds.DynamicChannels.Should().NotBeNull();
        eds.DynamicChannels!.Segments.Should().BeEmpty();
        SectionLines(written, "DynamicChannels").Should().Equal("NrOfSeg=0", "Vendor=channels");
    }

    [Fact]
    public void ReadString_DcfFileInfoAndCommissioningVendorKeys_ArePreserved()
    {
        // Arrange
        var content = Base.Replace("EDSVersion=4.0\n", "EDSVersion=4.0\nLastEDS=source.eds\nVendor=file\n")
            + Dcf.Replace("CANopenManager=0\n", "CANopenManager=0\nVendor=node\n")
            + """
            [ConnectedModules]
            NrOfEntries=0
            Vendor=connected

            """;

        // Act
        var dcf = CanOpenFile.Dcf.ReadString(content);
        var written = CanOpenFile.Dcf.WriteToString(dcf);

        // Assert
        dcf.FileInfo.LastEds.Should().Be("source.eds");
        dcf.FileInfo.RemainingEntries.Should().Equal(new Dictionary<string, string> { ["Vendor"] = "file" });
        dcf.DeviceCommissioning.RemainingEntries.Should().Equal(new Dictionary<string, string> { ["Vendor"] = "node" });
        var fileInfo = SectionLines(written, "FileInfo");
        fileInfo.Should().ContainInOrder("LastEDS=source.eds", "Vendor=file");
        fileInfo.Count(line => line.StartsWith("LastEDS=", StringComparison.Ordinal)).Should().Be(1);
        SectionLines(written, "DeviceComissioning").Should().EndWith("Vendor=node");
        SectionLines(written, "ConnectedModules").Should().Equal("NrOfEntries=0", "Vendor=connected");
    }

    [Fact]
    public void ReadString_EdsFileInfoLastEdsKey_IsKeptAsUnknownKey()
    {
        // Arrange — LastEDS is a DCF keyword (CiA 306-1 v1.4.0 § 7.2); an EDS keeps it as an unknown entry.
        var content = Base.Replace("EDSVersion=4.0\n", "EDSVersion=4.0\nLastEDS=source.eds\n");

        // Act
        var eds = CanOpenFile.Eds.ReadString(content);
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        eds.FileInfo.RemainingEntries.Should().Equal(new Dictionary<string, string> { ["LastEDS"] = "source.eds" });
        SectionLines(written, "FileInfo").Should().EndWith("LastEDS=source.eds");
    }

    [Fact]
    public void ConvertToDcf_SectionVendorKeys_AreCopiedAndWritten()
    {
        // Arrange
        var content = Base.Replace("EDSVersion=4.0\n", "EDSVersion=4.0\nVendor=file\n")
            .Replace("ProductName=Product\n", "ProductName=Product\nVendor=device\n")
            .Replace("1=0x1000\n", "1=0x1000\nVendor=list\n")
            + """
            [Comments]
            Lines=1
            Line1=c
            Vendor=comments

            [Tools]
            Items=1
            Vendor=tools

            [Tool1]
            Name=T
            Command=c
            Vendor=tool

            [DynamicChannels]
            NrOfSeg=0
            Vendor=channels

            """;
        var eds = CanOpenFile.Eds.ReadString(content);

        // Act
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 5, timestamp: new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc));
        var written = CanOpenFile.Dcf.WriteToString(dcf);

        // Assert
        dcf.FileInfo.RemainingEntries.Should().NotBeSameAs(eds.FileInfo.RemainingEntries);
        dcf.SectionRemainingEntries["MandatoryObjects"].Should().NotBeSameAs(eds.SectionRemainingEntries["MandatoryObjects"]);
        SectionLines(written, "FileInfo").Should().Contain("Vendor=file");
        SectionLines(written, "DeviceInfo").Should().Contain("Vendor=device");
        SectionLines(written, "MandatoryObjects").Should().Contain("Vendor=list");
        SectionLines(written, "Comments").Should().Contain("Vendor=comments");
        SectionLines(written, "Tools").Should().Contain("Vendor=tools");
        SectionLines(written, "Tool1").Should().Contain("Vendor=tool");
        SectionLines(written, "DynamicChannels").Should().Contain("Vendor=channels");
    }

    [Fact]
    public void WriteToString_ValidatedRoundTrip_RejectsUnwritableSectionRemainingKey()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Base);
        eds.DeviceInfo.RemainingEntries["Bad=Key"] = "x";

        // Act
        var validated = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);
        var unvalidated = () => CanOpenFile.Eds.WriteToString(eds);

        // Assert
        validated.Should().Throw<ModelValidationException>()
            .Which.Issues.Should().Contain(issue => issue.Path == "DeviceInfo.RemainingEntries[Bad=Key]");
        unvalidated.Should().Throw<EdsWriteException>();
    }

    [Fact]
    public void WriteToString_ValidatedRoundTrip_RejectsUnwritableSharedStoreValue()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Base);
        eds.SectionRemainingEntries["MandatoryObjects"] = new OrderedStringDictionary { ["Vendor"] = "line\nbreak" };

        // Act
        var validated = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        validated.Should().Throw<ModelValidationException>()
            .Which.Issues.Should().Contain(issue => issue.Path == "SectionRemainingEntries[MandatoryObjects][Vendor]");
    }

    [Fact]
    public void ReadString_StrictParsingWithVendorKeys_KeepsEntriesWithoutDiagnostics()
    {
        // Arrange — an additional entry is valid input (§ 6.2), not a parse error.
        var content = Base.Replace("ProductName=Product\n", "ProductName=Product\nLMT_ProductName=Widget\n")
            + """
            [Tools]
            Items=0
            Vendor=kept

            """;

        // Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(content, new CanOpenFileOptions { StrictParsing = true });

        // Assert
        result.Diagnostics.Should().BeEmpty();
        result.Model.DeviceInfo.RemainingEntries["LMT_ProductName"].Should().Be("Widget");
        result.Model.SectionRemainingEntries["Tools"]["Vendor"].Should().Be("kept");
    }

    [Fact]
    public void ReadString_DcfValueSectionOfNonCompactObjectWithVendorKey_KeepsSection()
    {
        // Arrange — the DCF reader applies [xxxxValue] to every object (CiA 306-1 § 7.3.4.3),
        // compact or not; the vendor key must survive although no compact list is written.
        var content = Base.Replace("SupportedObjects=1\n1=0x1000\n", "SupportedObjects=2\n1=0x1000\n2=0x1018\n")
            + Dcf
            + """
            [1018]
            ParameterName=Identity
            ObjectType=0x9
            SubNumber=2

            [1018sub0]
            ParameterName=Highest
            ObjectType=0x7
            DataType=0x0005
            AccessType=ro
            DefaultValue=1
            PDOMapping=0

            [1018sub1]
            ParameterName=Vendor-ID
            ObjectType=0x7
            DataType=0x0007
            AccessType=ro
            DefaultValue=0
            PDOMapping=0

            [1018Value]
            NrOfEntries=1
            1=0x42
            Vendor=value

            """;

        // Act
        var dcf = CanOpenFile.Dcf.ReadString(content);
        var written = CanOpenFile.Dcf.WriteToString(dcf);
        var again = CanOpenFile.Dcf.ReadString(written);

        // Assert
        dcf.ObjectDictionary.Objects[0x1018].SubObjects[1].ParameterValue.Should().Be("0x42");
        SectionLines(written, "1018Value").Should().Equal("NrOfEntries=0", "Vendor=value");
        again.ObjectDictionary.Objects[0x1018].SubObjects[1].ParameterValue.Should().Be("0x42");
        again.SectionRemainingEntries["1018Value"]["Vendor"].Should().Be("value");
    }

    [Fact]
    public void WriteToString_XddWithUnwritableIniRemainingKey_IsNotAffectedByIniRules()
    {
        // Arrange — the INI text rules belong to the EDS/DCF/CPJ guard only.
        var eds = CanOpenFile.Eds.ReadString(Base);
        eds.DeviceInfo.RemainingEntries["Bad=Key"] = "x";
        eds.SectionRemainingEntries["Tools"] = new OrderedStringDictionary { ["Vendor"] = "line\nbreak" };

        // Act
        var act = () => CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ReadString_StandardSectionWithoutUnknownKeys_LeavesStoresEmpty()
    {
        // Act
        var eds = CanOpenFile.Eds.ReadString(Base);

        // Assert
        eds.FileInfo.RemainingEntries.Should().BeEmpty();
        eds.DeviceInfo.RemainingEntries.Should().BeEmpty();
        eds.SectionRemainingEntries.Should().BeEmpty();
    }

    private static List<string> SectionLines(string ini, string section)
    {
        var lines = ini.Replace("\r\n", "\n").Split('\n');
        var result = new List<string>();
        var inSection = false;
        foreach (var line in lines)
        {
            if (line.StartsWith("[", StringComparison.Ordinal))
            {
                inSection = line.Equals("[" + section + "]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (inSection && line.Length > 0)
                result.Add(line);
        }

        return result;
    }
}
