namespace EdsDcfNet.Tests.Writers;

using System.Reflection;
using System.Xml.Linq;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Writers;

public class XddWriterTests
{
    private readonly XddWriter _writer = new();

    private static ElectronicDataSheet CreateSampleEds()
    {
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo
            {
                FileName = "test.xdd",
                FileVersion = 1,
                CreatedBy = "TestCreator",
                CreationDate = "01-15-2025"
            },
            DeviceInfo = new DeviceInfo
            {
                VendorName = "Test Vendor",
                VendorNumber = 0x00000100,
                ProductName = "Test Product",
                ProductNumber = 0x00001001,
                Granularity = 8,
                NrOfRxPdo = 2,
                NrOfTxPdo = 2,
                SimpleBootUpSlave = true,
                SupportedBaudRates = new BaudRates
                {
                    BaudRate250 = true,
                    BaudRate500 = true
                }
            }
        };

        var obj = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "0x00000191",
            PdoMapping = false
        };
        eds.ObjectDictionary.Objects[0x1000] = obj;
        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);

        return eds;
    }

    #region WriteFile Tests

    [Fact]
    public void WriteFile_CreatesValidXmlFile()
    {
        // Arrange
        var eds = CreateSampleEds();
        var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xdd");

        try
        {
            // Act
            _writer.WriteFile(eds, filePath);

            // Assert
            File.Exists(filePath).Should().BeTrue();
            var content = File.ReadAllText(filePath);
            content.Should().Contain("ISO15745ProfileContainer");
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public void WriteFile_InvalidPath_ThrowsXddWriteException()
    {
        // Arrange
        var eds = CreateSampleEds();
        var invalidPath = "/invalid/path/that/does/not/exist/test.xdd";

        // Act
        var act = () => _writer.WriteFile(eds, invalidPath);

        // Assert
        act.Should().Throw<XddWriteException>()
            .WithMessage("*Failed to write XDD file*");
    }

    [Fact]
    public void WriteFile_GenerationThrowsXddWriteException_Rethrows()
    {
        var eds = CreateSampleEds();
        eds.DeviceInfo = null!;
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xdd");

        try
        {
            var act = () => _writer.WriteFile(eds, tempFile);

            var ex = act.Should().Throw<XddWriteException>().Which;
            ex.SectionName.Should().Be("DeviceProfile");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteFileAsync_InvalidPath_ThrowsXddWriteException()
    {
        var eds = CreateSampleEds();
        var invalidPath = "/invalid/path/that/does/not/exist/async.xdd";

        var act = () => _writer.WriteFileAsync(eds, invalidPath);

        (await act.Should().ThrowAsync<XddWriteException>())
            .WithMessage("*Failed to write XDD file*");
    }

    [Fact]
    public async Task WriteFileAsync_Cancelled_ThrowsOperationCanceledException()
    {
        var eds = CreateSampleEds();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xdd");

        try
        {
            var act = () => _writer.WriteFileAsync(eds, tempFile, cts.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void WriteStream_RoundTripsAndLeavesStreamOpen()
    {
        var eds = CreateSampleEds();
        using var stream = new MemoryStream();

        _writer.WriteStream(eds, stream);
        stream.CanWrite.Should().BeTrue();
        stream.Position = 0;
        var parsed = new EdsDcfNet.Parsers.XddReader().ReadStream(stream);

        parsed.DeviceInfo.ProductName.Should().Be("Test Product");
    }

    [Fact]
    public async Task WriteStreamAsync_RoundTripsAndLeavesStreamOpen()
    {
        var eds = CreateSampleEds();
        using var stream = new MemoryStream();

        await _writer.WriteStreamAsync(eds, stream);
        stream.CanWrite.Should().BeTrue();
        stream.Position = 0;
        var parsed = await new EdsDcfNet.Parsers.XddReader().ReadStreamAsync(stream);

        parsed.DeviceInfo.ProductName.Should().Be("Test Product");
    }

    [Fact]
    public void WriteStream_UnwritableStream_ThrowsArgumentException()
    {
        var eds = CreateSampleEds();
        using var stream = new MemoryStream(new byte[16], writable: false);

        var act = () => _writer.WriteStream(eds, stream);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("stream");
    }

    [Fact]
    public async Task WriteStreamAsync_UnwritableStream_ThrowsArgumentException()
    {
        var eds = CreateSampleEds();
        using var stream = new MemoryStream(new byte[16], writable: false);

        var act = () => _writer.WriteStreamAsync(eds, stream);

        await act.Should().ThrowAsync<ArgumentException>()
            .Where(ex => ex.ParamName == "stream");
    }

    [Fact]
    public async Task WriteStreamAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        var eds = CreateSampleEds();
        using var stream = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _writer.WriteStreamAsync(eds, stream, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region GenerateString Tests

    [Fact]
    public void GenerateString_ContainsISO15745ProfileContainer()
    {
        // Act
        var result = _writer.GenerateString(CreateSampleEds());

        // Assert
        result.Should().Contain("ISO15745ProfileContainer");
    }

    [Fact]
    public void GenerateString_InvalidDeviceInfo_ThrowsXddWriteExceptionWithSectionName()
    {
        // Arrange
        var eds = CreateSampleEds();
        eds.DeviceInfo = null!;

        // Act
        var act = () => _writer.GenerateString(eds);

        // Assert
        var ex = act.Should().Throw<XddWriteException>().Which;
        ex.SectionName.Should().Be("DeviceProfile");
        ex.Message.Should().Contain("DeviceProfile");
    }

    [Fact]
    public void GenerateString_WhenSubclassThrowsXdcWriteException_PreservesXdcContext()
    {
        var writer = new ThrowingNetworkManagementWriter();

        var act = () => writer.GenerateString(CreateSampleEds());

        var ex = act.Should().Throw<XdcWriteException>().Which;
        ex.SectionName.Should().Be("deviceCommissioning");
        ex.Message.Should().Contain("forced");
    }

    [Fact]
    public void GenerateString_WhenSubclassThrowsXddWriteException_RethrowsOriginal()
    {
        var writer = new ThrowingXddDocumentWriter();

        var act = () => writer.GenerateString(CreateSampleEds());

        var ex = act.Should().Throw<XddWriteException>().Which;
        ex.Should().BeSameAs(writer.ExpectedException);
    }

    [Fact]
    public async Task WriteFileAsync_WhenGenerationThrowsXddWriteException_Rethrows()
    {
        var writer = new ThrowingXddDocumentWriter();
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xdd");

        var act = () => writer.WriteFileAsync(CreateSampleEds(), tempFile);

        var ex = (await act.Should().ThrowAsync<XddWriteException>()).Which;
        ex.Should().BeSameAs(writer.ExpectedException);
    }

    [Fact]
    public void GenerateString_ContainsTwoProfiles()
    {
        // Act
        var result = _writer.GenerateString(CreateSampleEds());

        // Assert
        result.Should().Contain("ProfileBody_Device_CANopen");
        result.Should().Contain("ProfileBody_CommunicationNetwork_CANopen");
    }

    [Fact]
    public void GenerateString_DeviceIdentity_CorrectVendorAndProduct()
    {
        // Act
        var result = _writer.GenerateString(CreateSampleEds());
        var document = XDocument.Parse(result);

        // Assert
        GetSingleElement(document, "vendorName").Value.Should().Be("Test Vendor");
        GetSingleElement(document, "productName").Value.Should().Be("Test Product");
        result.Should().Contain("0x00000100");
        result.Should().Contain("0x00001001");
    }

    [Fact]
    public void GenerateString_CANopenObjectList_CorrectCounts()
    {
        // Arrange
        var eds = CreateSampleEds();

        // Act
        var result = _writer.GenerateString(eds);
        var document = XDocument.Parse(result);
        var objectList = GetSingleElement(document, "CANopenObjectList");

        // Assert
        GetAttributeValue(objectList, "mandatoryObjects").Should().Be("1");
        GetAttributeValue(objectList, "optionalObjects").Should().Be("0");
        GetAttributeValue(objectList, "manufacturerObjects").Should().Be("0");
    }

    [Fact]
    public void GenerateString_Objects_AllAttributesPresent()
    {
        // Act
        var result = _writer.GenerateString(CreateSampleEds());
        var document = XDocument.Parse(result);
        var canOpenObject = document.Descendants()
            .Single(e => e.Name.LocalName == "CANopenObject" && GetAttributeValue(e, "index") == "1000");

        // Assert
        GetAttributeValue(canOpenObject, "name").Should().Be("Device Type");
        GetAttributeValue(canOpenObject, "objectType").Should().Be("7");
        GetAttributeValue(canOpenObject, "dataType").Should().Be("0007");
        GetAttributeValue(canOpenObject, "accessType").Should().Be("ro");
        GetAttributeValue(canOpenObject, "defaultValue").Should().Be("0x00000191");
        GetAttributeValue(canOpenObject, "PDOmapping").Should().Be("no");
    }

    [Fact]
    public void GenerateString_SubObjects_WrittenCorrectly()
    {
        // Arrange
        var eds = CreateSampleEds();
        var parentObj = new CanOpenObject
        {
            Index = 0x1018,
            ParameterName = "Identity Object",
            ObjectType = 0x9,
            SubNumber = 2
        };
        var subObj = new CanOpenSubObject
        {
            SubIndex = 0,
            ParameterName = "Number of Entries",
            ObjectType = 0x7,
            DataType = 0x0005,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "4"
        };
        parentObj.SubObjects[0] = subObj;
        eds.ObjectDictionary.Objects[0x1018] = parentObj;
        eds.ObjectDictionary.OptionalObjects.Add(0x1018);

        // Act
        var result = _writer.GenerateString(eds);
        var document = XDocument.Parse(result);
        var subObject = document.Descendants()
            .Single(e => e.Name.LocalName == "CANopenSubObject" && GetAttributeValue(e, "subIndex") == "00");

        // Assert
        GetAttributeValue(subObject, "name").Should().Be("Number of Entries");
    }

    [Fact]
    public void GenerateString_SubObjectPdoMappingTrue_WrittenAsOptional()
    {
        // Arrange
        var eds = CreateSampleEds();
        var parentObj = new CanOpenObject
        {
            Index = 0x1018,
            ParameterName = "Identity Object",
            ObjectType = 0x9,
            SubNumber = 2
        };
        var subObj = new CanOpenSubObject
        {
            SubIndex = 0,
            ParameterName = "Number of Entries",
            ObjectType = 0x7,
            DataType = 0x0005,
            AccessType = AccessType.ReadOnly,
            PdoMapping = true
        };
        parentObj.SubObjects[0] = subObj;
        eds.ObjectDictionary.Objects[0x1018] = parentObj;
        eds.ObjectDictionary.OptionalObjects.Add(0x1018);

        // Act
        var result = _writer.GenerateString(eds);
        var document = XDocument.Parse(result);
        var subObject = document.Descendants()
            .Single(e => e.Name.LocalName == "CANopenSubObject" && GetAttributeValue(e, "subIndex") == "00");

        // Assert
        GetAttributeValue(subObject, "PDOmapping").Should().Be("optional");
    }

    [Fact]
    public void GenerateString_DummyUsage_WrittenCorrectly()
    {
        // Arrange
        var eds = CreateSampleEds();
        eds.ObjectDictionary.DummyUsage[0x0002] = true;
        eds.ObjectDictionary.DummyUsage[0x0005] = false;

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("dummyUsage");
        result.Should().Contain("Dummy0002=1");
        result.Should().Contain("Dummy0005=0");
    }

    [Fact]
    public void GenerateString_SupportedBaudRates_WrittenAsStrings()
    {
        // Act
        var result = _writer.GenerateString(CreateSampleEds());
        var document = XDocument.Parse(result);
        var supportedRates = document.Descendants()
            .Where(e => e.Name.LocalName == "supportedBaudRate")
            .Select(e => GetAttributeValue(e, "value"))
            .ToList();

        // Assert
        supportedRates.Should().Contain("250 Kbps");
        supportedRates.Should().Contain("500 Kbps");
    }

    [Fact]
    public void GenerateString_GeneralFeatures_GroupMessagingAndLssSupported_WrittenAsTrue()
    {
        // Arrange
        var eds = CreateSampleEds();
        eds.DeviceInfo.GroupMessaging = true;
        eds.DeviceInfo.LssSupported = true;

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("groupMessaging=\"true\"");
        result.Should().Contain("layerSettingServiceSlave=\"true\"");
    }

    [Fact]
    public void GenerateString_MasterFeatures_BootUpMaster_WrittenAsTrue()
    {
        // Arrange
        var eds = CreateSampleEds();
        eds.DeviceInfo.SimpleBootUpMaster = true;

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("bootUpMaster=\"true\"");
    }

    [Fact]
    public void GenerateString_ApplicationProcess_WritesTypedModel()
    {
        // Arrange
        var eds = CreateSampleEds();
        var ap = new ApplicationProcess();
        var dtl = new ApDataTypeList();
        dtl.Structs.Add(new ApStructType { Name = "MyStruct", UniqueId = "uid_s1" });
        ap.DataTypeList = dtl;
        ap.ParameterList.Add(new ApParameter { UniqueId = "uid_p1", Access = "read" });
        eds.ApplicationProcess = ap;

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("ApplicationProcess");
        result.Should().Contain("dataTypeList");
        result.Should().Contain("MyStruct");
        result.Should().Contain("uid_p1");
    }

    [Fact]
    public void GenerateString_AccessType_ReadWriteInput_WrittenAsRw()
    {
        // Arrange
        var eds = CreateSampleEds();
        eds.ObjectDictionary.Objects[0x1000].AccessType = AccessType.ReadWriteInput;

        // Act
        var result = _writer.GenerateString(eds);

        // Assert — ReadWriteInput has no XDD equivalent, maps to "rw"
        result.Should().Contain("accessType=\"rw\"");
    }

    [Fact]
    public void GenerateString_PdoMapping_false_WrittenAsNo()
    {
        // Arrange
        var eds = CreateSampleEds();
        eds.ObjectDictionary.Objects[0x1000].PdoMapping = false;

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("PDOmapping=\"no\"");
    }

    [Fact]
    public void GenerateString_PdoMapping_true_WrittenAsOptional()
    {
        // Arrange
        var eds = CreateSampleEds();
        eds.ObjectDictionary.Objects[0x1000].PdoMapping = true;

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("PDOmapping=\"optional\"");
    }

    [Fact]
    public void GenerateString_ValidXml_CanBeParsedBack()
    {
        // Act
        var result = _writer.GenerateString(CreateSampleEds());

        // Assert — output must be parseable as valid XML
        var act = () => XDocument.Parse(result);
        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateString_DateConversion_EdsToXsd()
    {
        // Arrange — CreationDate in EDS format "MM-DD-YYYY"
        var eds = CreateSampleEds();
        eds.FileInfo.CreationDate = "03-15-2025";

        // Act
        var result = _writer.GenerateString(eds);

        // Assert — date written in XSD format "YYYY-MM-DD"
        result.Should().Contain("fileCreationDate=\"2025-03-15\"");
    }

    [Fact]
    public void GenerateString_FileInfo_OptionalAttributes_Written()
    {
        // Arrange — set all optional FileInfo fields
        var eds = CreateSampleEds();
        eds.FileInfo.ModificationDate = "04-20-2025";
        eds.FileInfo.ModificationTime = "14:00";
        eds.FileInfo.ModifiedBy = "Engineer";

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("fileModificationDate=\"2025-04-20\"");
        result.Should().Contain("fileModificationTime=\"14:00\"");
        result.Should().Contain("fileModifiedBy=\"Engineer\"");
    }

    [Fact]
    public void GenerateString_NonStandardDateFormat_PassedThrough()
    {
        // Arrange — date that doesn't match MM-DD-YYYY format
        var eds = CreateSampleEds();
        eds.FileInfo.CreationDate = "2025/01/15";

        // Act
        var result = _writer.GenerateString(eds);

        // Assert — non-standard date is written as-is (fallback)
        result.Should().Contain("fileCreationDate=\"2025/01/15\"");
    }

    [Fact]
    public void GenerateString_DynamicChannels_Written()
    {
        // Arrange
        var eds = CreateSampleEds();
        eds.DynamicChannels = new DynamicChannels();
        eds.DynamicChannels.Segments.Add(new DynamicChannelSegment
        {
            Type = 0x0007,
            Dir = AccessType.ReadOnly,
            Range = "1600-17FF",
            PPOffset = 5
        });

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("dynamicChannels");
        result.Should().Contain("dynamicChannel");
        result.Should().Contain("dataType=\"0007\"");
        result.Should().Contain("accessType=\"ro\"");
        result.Should().Contain("startIndex=\"1600\"");
        result.Should().Contain("endIndex=\"17FF\"");
        result.Should().Contain("pDOmappingIndex=\"5\"");
    }

    [Fact]
    public void GenerateString_DynamicChannel_NoEndIndex_WhenSingleRangePart()
    {
        // Arrange — Range with no '-' separator → only startIndex written
        var eds = CreateSampleEds();
        eds.DynamicChannels = new DynamicChannels();
        eds.DynamicChannels.Segments.Add(new DynamicChannelSegment
        {
            Type = 0x0004,
            Dir = AccessType.ReadWrite,
            Range = "2000"
        });

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("startIndex=\"2000\"");
        result.Should().NotContain("endIndex");
    }

    [Fact]
    public void GenerateString_AccessType_Constant_WrittenAsConst()
    {
        // Arrange
        var eds = CreateSampleEds();
        eds.ObjectDictionary.Objects[0x1000].AccessType = AccessType.Constant;

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("accessType=\"const\"");
    }

    [Fact]
    public void GenerateString_AccessType_WriteOnly_WrittenAsWo()
    {
        // Arrange
        var eds = CreateSampleEds();
        eds.ObjectDictionary.Objects[0x1000].AccessType = AccessType.WriteOnly;

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("accessType=\"wo\"");
    }

    [Fact]
    public void GenerateString_AccessType_ReadWriteOutput_WrittenAsRw()
    {
        // Arrange
        var eds = CreateSampleEds();
        eds.ObjectDictionary.Objects[0x1000].AccessType = AccessType.ReadWriteOutput;

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("accessType=\"rw\"");
    }

    [Fact]
    public void GenerateString_AccessType_UnknownEnumValue_DefaultsToRw()
    {
        var eds = CreateSampleEds();
        eds.ObjectDictionary.Objects[0x1000].AccessType = (AccessType)99;

        var result = _writer.GenerateString(eds);

        result.Should().Contain("accessType=\"rw\"");
    }

    [Fact]
    public void GenerateString_Object_LowLimitHighLimit_Written()
    {
        // Arrange
        var eds = CreateSampleEds();
        eds.ObjectDictionary.Objects[0x1000].LowLimit = "0";
        eds.ObjectDictionary.Objects[0x1000].HighLimit = "100";
        eds.ObjectDictionary.Objects[0x1000].ObjFlags = 1;

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("lowLimit=\"0\"");
        result.Should().Contain("highLimit=\"100\"");
        result.Should().Contain("objFlags=\"1\"");
    }

    [Fact]
    public void GenerateString_SubObject_LowLimitHighLimit_Written()
    {
        // Arrange
        var eds = CreateSampleEds();
        var parentObj = new CanOpenObject
        {
            Index = 0x1018, ParameterName = "Identity Object", ObjectType = 0x9, SubNumber = 2
        };
        var subObj = new CanOpenSubObject
        {
            SubIndex = 1, ParameterName = "Vendor ID",
            ObjectType = 0x7, DataType = 0x0007, AccessType = AccessType.ReadOnly,
            LowLimit = "0", HighLimit = "0xFFFFFFFF"
        };
        parentObj.SubObjects[1] = subObj;
        eds.ObjectDictionary.Objects[0x1018] = parentObj;
        eds.ObjectDictionary.OptionalObjects.Add(0x1018);

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("lowLimit=\"0\"");
        result.Should().Contain("highLimit=\"0xFFFFFFFF\"");
    }

    [Fact]
    public void GenerateString_NullApplicationProcess_OmitsElement()
    {
        // Arrange — ApplicationProcess is null (default); no element should be emitted
        var eds = CreateSampleEds();
        eds.ApplicationProcess = null;

        // Act
        var result = _writer.GenerateString(eds);

        // Assert — output is valid XML and contains no ApplicationProcess element
        result.Should().NotContain("ApplicationProcess");
        var act = () => XDocument.Parse(result);
        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateString_DefaultBaudRate_OnlyBaudRate500_ReturnsCorrectDefault()
    {
        // Arrange — only BaudRate500 set (BaudRate250 is false)
        var eds = CreateSampleEds();
        eds.DeviceInfo.SupportedBaudRates = new BaudRates { BaudRate500 = true };

        // Act
        var result = _writer.GenerateString(eds);

        // Assert — defaultValue should be "500 Kbps"
        result.Should().Contain("defaultValue=\"500 Kbps\"");
    }

    [Fact]
    public void GenerateString_DefaultBaudRate_OnlyBaudRate125()
    {
        var eds = CreateSampleEds();
        eds.DeviceInfo.SupportedBaudRates = new BaudRates { BaudRate125 = true };

        var result = _writer.GenerateString(eds);

        result.Should().Contain("defaultValue=\"125 Kbps\"");
    }

    [Fact]
    public void GenerateString_DefaultBaudRate_OnlyBaudRate1000()
    {
        var eds = CreateSampleEds();
        eds.DeviceInfo.SupportedBaudRates = new BaudRates { BaudRate1000 = true };

        var result = _writer.GenerateString(eds);

        result.Should().Contain("defaultValue=\"1000 Kbps\"");
    }

    [Fact]
    public void GenerateString_DefaultBaudRate_OnlyBaudRate800()
    {
        var eds = CreateSampleEds();
        eds.DeviceInfo.SupportedBaudRates = new BaudRates { BaudRate800 = true };

        var result = _writer.GenerateString(eds);

        result.Should().Contain("defaultValue=\"800 Kbps\"");
        result.Should().Contain("800 Kbps");
    }

    [Fact]
    public void GenerateString_DefaultBaudRate_OnlyBaudRate50()
    {
        var eds = CreateSampleEds();
        eds.DeviceInfo.SupportedBaudRates = new BaudRates { BaudRate50 = true };

        var result = _writer.GenerateString(eds);

        result.Should().Contain("defaultValue=\"50 Kbps\"");
    }

    [Fact]
    public void GenerateString_DefaultBaudRate_OnlyBaudRate20()
    {
        var eds = CreateSampleEds();
        eds.DeviceInfo.SupportedBaudRates = new BaudRates { BaudRate20 = true };

        var result = _writer.GenerateString(eds);

        result.Should().Contain("defaultValue=\"20 Kbps\"");
    }

    [Fact]
    public void GenerateString_DefaultBaudRate_OnlyBaudRate10()
    {
        var eds = CreateSampleEds();
        eds.DeviceInfo.SupportedBaudRates = new BaudRates { BaudRate10 = true };

        var result = _writer.GenerateString(eds);

        result.Should().Contain("defaultValue=\"10 Kbps\"");
    }

    [Fact]
    public void GenerateString_DefaultBaudRate_NoneSet_FallsBackTo250AndEmitsSupportedEntry()
    {
        // When no baud-rate flags are set the fallback "250 Kbps" must appear both as
        // defaultValue AND as a supportedBaudRate child so the XML is self-consistent.
        var eds = CreateSampleEds();
        eds.DeviceInfo.SupportedBaudRates = new BaudRates();

        var result = _writer.GenerateString(eds);

        result.Should().Contain("defaultValue=\"250 Kbps\"");
        result.Should().Contain("<supportedBaudRate value=\"250 Kbps\"");
    }

    [Fact]
    public void StringBuilderWriter_WriteCharAndString_AreCallable()
    {
        var nestedType = typeof(XddWriter).GetNestedType("StringBuilderWriter", BindingFlags.NonPublic);
        nestedType.Should().NotBeNull();

        var instance = Activator.CreateInstance(nestedType!);
        instance.Should().NotBeNull();

        var writeChar = nestedType!.GetMethod("Write", new[] { typeof(char) });
        var writeString = nestedType.GetMethod("Write", new[] { typeof(string) });
        var toString = nestedType.GetMethod("ToString");

        writeChar.Should().NotBeNull();
        writeString.Should().NotBeNull();
        toString.Should().NotBeNull();

        writeChar!.Invoke(instance, new object[] { 'A' });
        writeString!.Invoke(instance, new object[] { "BC" });

        var value = toString!.Invoke(instance, Array.Empty<object>()) as string;
        value.Should().Be("ABC");
    }

    #endregion

    #region BuildDocument virtual hook

    [Fact]
    public void BuildDocument_Override_IsCalledByGenerateString()
    {
        // Verify the virtual BuildDocument(eds, commissioning) hook is in the call chain
        // so subclasses can genuinely customise document generation.
        var writer = new CustomDocumentWriter();

        writer.GenerateString(CreateSampleEds());

        writer.WasCalled.Should().BeTrue();
    }

    private sealed class CustomDocumentWriter : XddWriter
    {
        public bool WasCalled { get; private set; }

        protected override System.Xml.Linq.XDocument BuildDocument(
            ElectronicDataSheet eds,
            EdsDcfNet.Models.DeviceCommissioning? commissioning)
        {
            WasCalled = true;
            return base.BuildDocument(eds, commissioning);
        }
    }

    private sealed class ThrowingNetworkManagementWriter : XddWriter
    {
        protected override XElement BuildNetworkManagement(
            ElectronicDataSheet eds,
            DeviceCommissioning? commissioning)
        {
            throw new XdcWriteException("forced", "deviceCommissioning");
        }
    }

    private sealed class ThrowingXddDocumentWriter : XddWriter
    {
        public XddWriteException ExpectedException { get; } = new("forced-xdd", "Document");

        protected override XDocument BuildDocument(
            ElectronicDataSheet eds,
            DeviceCommissioning? commissioning)
        {
            throw ExpectedException;
        }
    }

    private static XElement GetSingleElement(XContainer container, string localName)
    {
        return container.Descendants().Single(e => e.Name.LocalName == localName);
    }

    private static string GetAttributeValue(XElement element, string attributeName)
    {
        return element.Attributes().Single(a => a.Name.LocalName == attributeName).Value;
    }

    #endregion
}
