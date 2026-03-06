namespace EdsDcfNet.Tests.Writers;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Writers;

public class EdsWriterTests
{
    private readonly EdsWriter _writer = new();

    private static ElectronicDataSheet CreateMinimalEds()
    {
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo
            {
                FileName = "test.eds",
                FileVersion = 1,
                FileRevision = 0,
                EdsVersion = "4.0",
                Description = "Test EDS"
            },
            DeviceInfo = new DeviceInfo
            {
                VendorName = "Test Vendor",
                ProductName = "Test Product",
                VendorNumber = 0x100,
                ProductNumber = 0x1001
            },
            ObjectDictionary = new ObjectDictionary()
        };

        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "0x191",
            PdoMapping = false
        };

        return eds;
    }

    [Fact]
    public void GenerateString_MinimalEds_GeneratesValidContent()
    {
        // Arrange
        var eds = CreateMinimalEds();

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("[FileInfo]");
        result.Should().Contain("[DeviceInfo]");
        result.Should().Contain("[MandatoryObjects]");
        result.Should().NotContain("[DeviceCommissioning]");
        result.Should().NotContain("[ConnectedModules]");
    }

    [Fact]
    public void GenerateString_FileInfo_DoesNotWriteLastEds()
    {
        // Arrange
        var eds = CreateMinimalEds();
        eds.FileInfo.LastEds = "template.eds";

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("FileName=test.eds");
        result.Should().Contain("EDSVersion=4.0");
        result.Should().NotContain("LastEDS=");
    }

    [Fact]
    public void GenerateString_DcfSpecificObjectFields_AreOmitted()
    {
        // Arrange
        var eds = CreateMinimalEds();
        var obj = eds.ObjectDictionary.Objects[0x1000];
        obj.ParameterValue = "0x1234";
        obj.Denotation = "Configured";
        obj.ParamRefd = "X1.A1";
        obj.UploadFile = "in.bin";
        obj.DownloadFile = "out.bin";

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().NotContain("ParameterValue=");
        result.Should().NotContain("Denotation=");
        result.Should().NotContain("ParamRefd=");
        result.Should().NotContain("UploadFile=");
        result.Should().NotContain("DownloadFile=");
    }

    [Fact]
    public void GenerateString_DcfSpecificSubObjectFields_AreOmitted()
    {
        // Arrange
        var eds = CreateMinimalEds();
        eds.ObjectDictionary.OptionalObjects.Add(0x1018);
        eds.ObjectDictionary.Objects[0x1018] = new CanOpenObject
        {
            Index = 0x1018,
            ParameterName = "Identity",
            ObjectType = 0x9,
            SubNumber = 1
        };

        eds.ObjectDictionary.Objects[0x1018].SubObjects[0] = new CanOpenSubObject
        {
            SubIndex = 0,
            ParameterName = "Entries",
            ObjectType = 0x7,
            DataType = 0x0005,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "1",
            ParameterValue = "5",
            Denotation = "Configured",
            ParamRefd = "X1.A2"
        };

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("[1018sub0]");
        result.Should().NotContain("ParameterValue=");
        result.Should().NotContain("Denotation=");
        result.Should().NotContain("ParamRefd=");
    }

    [Fact]
    public void GenerateString_DynamicChannelsAndTools_WritesSections()
    {
        // Arrange
        var eds = CreateMinimalEds();
        eds.DynamicChannels = new DynamicChannels();
        eds.DynamicChannels.Segments.Add(new DynamicChannelSegment
        {
            Type = 0x0007,
            Dir = AccessType.ReadOnly,
            Range = "0xA000-0xA0FF",
            PPOffset = 0
        });
        eds.Tools.Add(new ToolInfo { Name = "EDS Checker", Command = "checker.exe $EDS" });

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("[DynamicChannels]");
        result.Should().Contain("NrOfSeg=1");
        result.Should().Contain("Type1=0x7");
        result.Should().Contain("[Tools]");
        result.Should().Contain("Items=1");
        result.Should().Contain("[Tool1]");
        result.Should().Contain("Command=checker.exe $EDS");
    }

    [Fact]
    public void GenerateString_SupportedModules_WritesSections()
    {
        // Arrange
        var eds = CreateMinimalEds();
        eds.SupportedModules.Add(new ModuleInfo
        {
            ModuleNumber = 1,
            ProductName = "Input Module",
            ProductVersion = 1,
            ProductRevision = 0,
            OrderCode = "MOD-IN-8",
            FixedObjects = { 0x6000 }
        });

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("[SupportedModules]");
        result.Should().Contain("NrOfEntries=1");
        result.Should().Contain("[M1ModuleInfo]");
        result.Should().Contain("[M1FixedObjects]");
        result.Should().Contain("1=0x6000");
    }

    [Fact]
    public void GenerateString_WithOptionalWriterFields_WritesConditionalSectionsAndKeys()
    {
        // Arrange
        var eds = CreateMinimalEds();
        eds.DeviceInfo.CompactPdo = 2;
        eds.DeviceInfo.CANopenSafetySupported = true;

        eds.ObjectDictionary.ManufacturerObjects.Add(0x2000);
        eds.ObjectDictionary.Objects[0x2000] = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "Manufacturer Parameter",
            ObjectType = 0x9,
            DataType = 0x0006,
            AccessType = AccessType.ReadWrite,
            DefaultValue = "5",
            LowLimit = "1",
            HighLimit = "10",
            PdoMapping = true,
            SrdoMapping = true,
            InvertedSrad = "0x2000",
            ObjFlags = 0x10,
            CompactSubObj = 2,
            SubNumber = 1
        };

        eds.ObjectDictionary.Objects[0x2000].SubObjects[1] = new CanOpenSubObject
        {
            SubIndex = 1,
            ParameterName = "Sub Parameter",
            ObjectType = 0x7,
            DataType = 0x0005,
            AccessType = AccessType.ReadWrite,
            DefaultValue = "3",
            LowLimit = "0",
            HighLimit = "5",
            PdoMapping = true,
            SrdoMapping = true,
            InvertedSrad = "0x3000"
        };

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("CompactPDO=0x2");
        result.Should().Contain("CANopenSafetySupported=1");
        result.Should().Contain("[ManufacturerObjects]");
        result.Should().Contain("[2000]");
        result.Should().Contain("LowLimit=1");
        result.Should().Contain("HighLimit=10");
        result.Should().Contain("SRDOMapping=1");
        result.Should().Contain("InvertedSRAD=0x2000");
        result.Should().Contain("ObjFlags=0x10");
        result.Should().Contain("CompactSubObj=2");
        result.Should().Contain("[2000sub1]");
        result.Should().Contain("LowLimit=0");
        result.Should().Contain("HighLimit=5");
        result.Should().Contain("InvertedSRAD=0x3000");
    }

    [Fact]
    public void GenerateString_RoundTripWithEdsReader_PreservesCoreData()
    {
        // Arrange
        var original = CreateMinimalEds();
        original.Comments = new Comments { Lines = 1 };
        original.Comments.CommentLines[1] = "Created by test";

        // Act
        var output = _writer.GenerateString(original);
        var parsed = new EdsReader().ReadString(output);

        // Assert
        parsed.FileInfo.FileName.Should().Be(original.FileInfo.FileName);
        parsed.DeviceInfo.ProductName.Should().Be(original.DeviceInfo.ProductName);
        parsed.ObjectDictionary.MandatoryObjects.Should().BeEquivalentTo(original.ObjectDictionary.MandatoryObjects);
        parsed.Comments.Should().NotBeNull();
        parsed.Comments!.CommentLines[1].Should().Be("Created by test");
    }

    [Fact]
    public void GenerateString_ObjectLinksInAdditionalSections_FilteredForExistingObjects()
    {
        // Arrange
        var eds = CreateMinimalEds();
        eds.ObjectDictionary.Objects[0x1000].ObjectLinks.Add(0x2000);
        eds.AdditionalSections["1000ObjectLinks"] = new Dictionary<string, string>
        {
            { "ObjectLinks", "1" },
            { "1", "0x2000" }
        };

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        var matches = result.Split(new[] { "[1000ObjectLinks]" }, StringSplitOptions.None);
        matches.Should().HaveCount(2);
    }

    [Fact]
    public void GenerateString_ObjectLinksInAdditionalSections_KeptForOrphanObjects()
    {
        // Arrange
        var eds = CreateMinimalEds();
        eds.AdditionalSections["9999ObjectLinks"] = new Dictionary<string, string>
        {
            { "ObjectLinks", "1" },
            { "1", "0x1000" }
        };

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("[9999ObjectLinks]");
    }

    [Fact]
    public void GenerateString_ObjectLinksInAdditionalSections_KeptForInvalidIndexFormats()
    {
        // Arrange
        var eds = CreateMinimalEds();
        eds.AdditionalSections["ObjectLinks"] = new Dictionary<string, string>
        {
            { "ObjectLinks", "1" },
            { "1", "0x1000" }
        };
        eds.AdditionalSections["ZZZZObjectLinks"] = new Dictionary<string, string>
        {
            { "ObjectLinks", "1" },
            { "1", "0x1000" }
        };

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("[ObjectLinks]");
        result.Should().Contain("[ZZZZObjectLinks]");
    }

    [Fact]
    public void GenerateString_AdditionalSections_AreWrittenDeterministically()
    {
        // Arrange
        var eds = CreateMinimalEds();
        eds.AdditionalSections["zSection"] = new Dictionary<string, string>
        {
            { "zKey", "Z" },
            { "AKey", "A" }
        };
        eds.AdditionalSections["ASection"] = new Dictionary<string, string>
        {
            { "bKey", "B" },
            { "aKey", "A" }
        };

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        var aSectionIndex = result.IndexOf("[ASection]", StringComparison.Ordinal);
        var zSectionIndex = result.IndexOf("[zSection]", StringComparison.Ordinal);
        aSectionIndex.Should().BeGreaterThanOrEqualTo(0);
        zSectionIndex.Should().BeGreaterThanOrEqualTo(0);
        aSectionIndex.Should().BeLessThan(zSectionIndex);

        var aSectionStart = aSectionIndex;
        aSectionStart.Should().BeGreaterThanOrEqualTo(0);
        var aKeyPos = result.IndexOf("aKey=A", aSectionStart, StringComparison.Ordinal);
        var bKeyPos = result.IndexOf("bKey=B", aSectionStart, StringComparison.Ordinal);
        aKeyPos.Should().BeGreaterThanOrEqualTo(0);
        bKeyPos.Should().BeGreaterThanOrEqualTo(0);
        aKeyPos.Should().BeLessThan(bKeyPos);
    }

    [Fact]
    public void GenerateString_NestedSectionFailure_WrapsUnexpectedExceptionWithSectionName()
    {
        // Arrange
        var eds = CreateMinimalEds();
        eds.ObjectDictionary.OptionalObjects.Add(0x2001);
        var failingObject = new CanOpenObject
        {
            Index = 0x2001,
            ParameterName = "Failing object",
            ObjectType = 0x9,
            SubNumber = 1,
            AccessType = AccessType.ReadOnly,
            PdoMapping = false
        };
        failingObject.SubObjects[1] = null!;
        eds.ObjectDictionary.Objects[0x2001] = failingObject;

        // Act
        var act = () => _writer.GenerateString(eds);

        // Assert
        var ex = act.Should().Throw<EdsWriteException>().Which;
        ex.SectionName.Should().Be("2001sub1");
        ex.Message.Should().Contain("2001sub1");
        ex.InnerException.Should().NotBeNull();
        ex.InnerException.Should().BeOfType<NullReferenceException>();
    }

    [Fact]
    public void WriteFile_ValidPath_CreatesFile()
    {
        // Arrange
        var eds = CreateMinimalEds();
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            _writer.WriteFile(eds, tempFile);

            // Assert
            File.Exists(tempFile).Should().BeTrue();
            var content = File.ReadAllText(tempFile);
            content.Should().Contain("[FileInfo]");
            content.Should().Contain("[DeviceInfo]");
            content.Should().NotContain("[DeviceCommissioning]");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void WriteFile_InvalidPath_ThrowsEdsWriteException()
    {
        // Arrange
        var eds = CreateMinimalEds();
        var invalidPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "test.eds");

        // Act
        var act = () => _writer.WriteFile(eds, invalidPath);

        // Assert
        act.Should().Throw<EdsWriteException>()
            .WithMessage("*Failed to write EDS file*");
    }

    [Fact]
    public void GenerateString_InvalidDeviceInfo_ThrowsEdsWriteExceptionWithSectionName()
    {
        // Arrange
        var eds = CreateMinimalEds();
        eds.DeviceInfo = null!;

        // Act
        var act = () => _writer.GenerateString(eds);

        // Assert
        var ex = act.Should().Throw<EdsWriteException>().Which;
        ex.SectionName.Should().Be("DeviceInfo");
        ex.Message.Should().Contain("DeviceInfo");
    }

    [Fact]
    public void WriteFile_GenerationFailure_PreservesSectionName()
    {
        // Arrange
        var eds = CreateMinimalEds();
        eds.DeviceInfo = null!;
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            var act = () => _writer.WriteFile(eds, tempFile);

            // Assert
            var ex = act.Should().Throw<EdsWriteException>().Which;
            ex.SectionName.Should().Be("DeviceInfo");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void WriteFile_NonAsciiCharacters_PreservesCharacters()
    {
        // Arrange
        var eds = CreateMinimalEds();
        eds.DeviceInfo.VendorName = "Müller GmbH & Söhne";
        eds.DeviceInfo.ProductName = "Schütz-Relä Ä5";
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            _writer.WriteFile(eds, tempFile);

            // Assert
            var content = File.ReadAllText(tempFile, System.Text.Encoding.UTF8);
            content.Should().Contain("Müller GmbH & Söhne");
            content.Should().Contain("Schütz-Relä Ä5");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void WriteStream_RoundTripsAndLeavesStreamOpen()
    {
        var eds = CreateMinimalEds();
        using var stream = new MemoryStream();

        _writer.WriteStream(eds, stream);
        stream.CanWrite.Should().BeTrue();
        stream.Position = 0;
        var parsed = new EdsReader().ReadStream(stream);

        parsed.DeviceInfo.ProductName.Should().Be(eds.DeviceInfo.ProductName);
        parsed.ObjectDictionary.Objects.Should().ContainKey(0x1000);
    }

    [Fact]
    public async Task WriteStreamAsync_RoundTripsAndLeavesStreamOpen()
    {
        var eds = CreateMinimalEds();
        using var stream = new MemoryStream();

        await _writer.WriteStreamAsync(eds, stream);
        stream.CanWrite.Should().BeTrue();
        stream.Position = 0;
        var parsed = await new EdsReader().ReadStreamAsync(stream);

        parsed.DeviceInfo.ProductName.Should().Be(eds.DeviceInfo.ProductName);
        parsed.ObjectDictionary.Objects.Should().ContainKey(0x1000);
    }

    [Fact]
    public void WriteStream_UnwritableStream_ThrowsArgumentException()
    {
        var eds = CreateMinimalEds();
        using var stream = new MemoryStream(new byte[16], writable: false);

        var act = () => _writer.WriteStream(eds, stream);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("stream");
    }

    [Fact]
    public async Task WriteStreamAsync_UnwritableStream_ThrowsArgumentException()
    {
        var eds = CreateMinimalEds();
        using var stream = new MemoryStream(new byte[16], writable: false);

        var act = () => _writer.WriteStreamAsync(eds, stream);

        await act.Should().ThrowAsync<ArgumentException>()
            .Where(ex => ex.ParamName == "stream");
    }

    [Fact]
    public void WriteStream_NullStream_ThrowsArgumentNullException()
    {
        var eds = CreateMinimalEds();

        var act = () => _writer.WriteStream(eds, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("stream");
    }

    [Fact]
    public void WriteStream_GenerationThrowsEdsWriteException_Rethrows()
    {
        var eds = CreateMinimalEds();
        eds.DeviceInfo = null!;
        using var stream = new MemoryStream();

        var act = () => _writer.WriteStream(eds, stream);

        var ex = act.Should().Throw<EdsWriteException>().Which;
        ex.SectionName.Should().Be("DeviceInfo");
    }

    [Fact]
    public void WriteStream_StreamWriteThrows_WrapsInEdsWriteException()
    {
        var eds = CreateMinimalEds();
        using var stream = new ThrowingWritableStream();

        var act = () => _writer.WriteStream(eds, stream);

        var ex = act.Should().Throw<EdsWriteException>().Which;
        ex.Message.Should().Contain("Failed to write EDS content to stream.");
        ex.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task WriteStreamAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        var eds = CreateMinimalEds();
        using var stream = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _writer.WriteStreamAsync(eds, stream, cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WriteStreamAsync_GenerationThrowsEdsWriteException_Rethrows()
    {
        var eds = CreateMinimalEds();
        eds.DeviceInfo = null!;
        using var stream = new MemoryStream();

        var act = () => _writer.WriteStreamAsync(eds, stream);

        var ex = (await act.Should().ThrowAsync<EdsWriteException>()).Which;
        ex.SectionName.Should().Be("DeviceInfo");
    }

    [Fact]
    public async Task WriteStreamAsync_StreamWriteThrows_WrapsInEdsWriteException()
    {
        var eds = CreateMinimalEds();
        using var stream = new ThrowingWritableStream();

        var act = () => _writer.WriteStreamAsync(eds, stream);

        var ex = (await act.Should().ThrowAsync<EdsWriteException>()).Which;
        ex.Message.Should().Contain("Failed to write EDS content to stream.");
        ex.InnerException.Should().BeOfType<InvalidOperationException>();
    }
}
