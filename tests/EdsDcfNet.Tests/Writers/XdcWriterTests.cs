namespace EdsDcfNet.Tests.Writers;

using System.Xml.Linq;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Writers;

public class XdcWriterTests
{
    private readonly XdcWriter _writer = new();

    private static DeviceConfigurationFile CreateSampleDcf()
    {
        var dcf = new DeviceConfigurationFile
        {
            FileInfo = new EdsFileInfo
            {
                FileName = "test.xdc",
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
            },
            DeviceCommissioning = new DeviceCommissioning
            {
                NodeId = 3,
                NodeName = "MyDevice",
                Baudrate = 500,
                NetNumber = 1,
                NetworkName = "CANopen Network",
                CANopenManager = false
            }
        };

        var obj = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "0x00000000",
            PdoMapping = false,
            ParameterValue = "0x00000191",
            Denotation = "MyDeviceType"
        };
        dcf.ObjectDictionary.Objects[0x1000] = obj;
        dcf.ObjectDictionary.MandatoryObjects.Add(0x1000);

        return dcf;
    }

    #region GenerateString Tests

    [Fact]
    public void GenerateString_ContainsISO15745ProfileContainer()
    {
        // Act
        var result = _writer.GenerateString(CreateSampleDcf());

        // Assert
        result.Should().Contain("ISO15745ProfileContainer");
    }

    [Fact]
    public void GenerateString_ContainsTwoProfiles()
    {
        // Act
        var result = _writer.GenerateString(CreateSampleDcf());

        // Assert
        result.Should().Contain("ProfileBody_Device_CANopen");
        result.Should().Contain("ProfileBody_CommunicationNetwork_CANopen");
    }

    [Fact]
    public void GenerateString_ParameterValue_WrittenAsActualValue()
    {
        // Act
        var result = _writer.GenerateString(CreateSampleDcf());
        var document = XDocument.Parse(result);
        var canOpenObject = document.Descendants()
            .Single(e => e.Name.LocalName == "CANopenObject" && GetAttributeValue(e, "index") == "1000");

        // Assert
        GetAttributeValue(canOpenObject, "actualValue").Should().Be("0x00000191");
    }

    [Fact]
    public void GenerateString_Denotation_WrittenCorrectly()
    {
        // Act
        var result = _writer.GenerateString(CreateSampleDcf());
        var document = XDocument.Parse(result);
        var canOpenObject = document.Descendants()
            .Single(e => e.Name.LocalName == "CANopenObject" && GetAttributeValue(e, "index") == "1000");

        // Assert
        GetAttributeValue(canOpenObject, "denotation").Should().Be("MyDeviceType");
    }

    [Fact]
    public void GenerateString_DeviceCommissioning_WrittenCorrectly()
    {
        // Act
        var result = _writer.GenerateString(CreateSampleDcf());
        var document = XDocument.Parse(result);
        var deviceCommissioning = GetSingleElement(document, "deviceCommissioning");

        // Assert
        GetAttributeValue(deviceCommissioning, "nodeID").Should().Be("3");
        GetAttributeValue(deviceCommissioning, "nodeName").Should().Be("MyDevice");
        GetAttributeValue(deviceCommissioning, "actualBaudRate").Should().Be("500 Kbps");
        GetAttributeValue(deviceCommissioning, "networkNumber").Should().Be("1");
        GetAttributeValue(deviceCommissioning, "networkName").Should().Be("CANopen Network");
        GetAttributeValue(deviceCommissioning, "CANopenManager").Should().Be("false");
    }

    [Fact]
    public void GenerateString_DeviceCommissioning_OptionalFieldsOmitted_AndManagerTrue()
    {
        // Arrange
        var dcf = CreateSampleDcf();
        dcf.DeviceCommissioning = new DeviceCommissioning
        {
            NodeId = 5,
            Baudrate = 0,
            NetNumber = 7,
            CANopenManager = true
        };

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("deviceCommissioning");
        result.Should().Contain("nodeID=\"5\"");
        result.Should().Contain("networkNumber=\"7\"");
        result.Should().Contain("CANopenManager=\"true\"");
        result.Should().NotContain("nodeName=");
        result.Should().NotContain("actualBaudRate=");
        result.Should().NotContain("networkName=");
    }

    [Fact]
    public void GenerateString_DeviceCommissioning_Null_ElementOmitted()
    {
        // Arrange
        var dcf = CreateSampleDcf();
        dcf.DeviceCommissioning = null!;

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().NotContain("deviceCommissioning");
    }

    [Fact]
    public void GenerateString_NoActualValue_AttributeOmitted()
    {
        // Arrange
        var dcf = CreateSampleDcf();
        dcf.ObjectDictionary.Objects[0x1000].ParameterValue = null;

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().NotContain("actualValue");
    }

    [Fact]
    public void WriteFile_CreatesValidXmlFile()
    {
        // Arrange
        var dcf = CreateSampleDcf();
        var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xdc");

        try
        {
            // Act
            _writer.WriteFile(dcf, filePath);

            // Assert
            File.Exists(filePath).Should().BeTrue();
            var content = File.ReadAllText(filePath);
            content.Should().Contain("ISO15745ProfileContainer");
            content.Should().Contain("deviceCommissioning");
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public void WriteFile_InvalidPath_ThrowsXdcWriteException()
    {
        // Arrange
        var dcf = CreateSampleDcf();
        var invalidPath = "/invalid/path/that/does/not/exist/test.xdc";

        // Act
        var act = () => _writer.WriteFile(dcf, invalidPath);

        // Assert
        act.Should().Throw<XdcWriteException>()
            .WithMessage("*Failed to write XDC file*");
    }

    [Fact]
    public void WriteStream_RoundTripsAndLeavesStreamOpen()
    {
        // Arrange
        var dcf = CreateSampleDcf();
        using var stream = new MemoryStream();

        // Act
        _writer.WriteStream(dcf, stream);
        stream.CanWrite.Should().BeTrue();
        stream.Position = 0;
        var parsed = new EdsDcfNet.Parsers.XdcReader().ReadStream(stream);

        // Assert
        parsed.DeviceCommissioning.NodeId.Should().Be(3);
        parsed.ObjectDictionary.Objects.Should().ContainKey(0x1000);
    }

    [Fact]
    public async Task WriteStreamAsync_RoundTripsAndLeavesStreamOpen()
    {
        // Arrange
        var dcf = CreateSampleDcf();
        using var stream = new MemoryStream();

        // Act
        await _writer.WriteStreamAsync(dcf, stream);
        stream.CanWrite.Should().BeTrue();
        stream.Position = 0;
        var parsed = await new EdsDcfNet.Parsers.XdcReader().ReadStreamAsync(stream);

        // Assert
        parsed.DeviceCommissioning.NodeId.Should().Be(3);
        parsed.ObjectDictionary.Objects.Should().ContainKey(0x1000);
    }

    [Fact]
    public void GenerateString_SubObjectActualValue_WrittenCorrectly()
    {
        // Arrange
        var dcf = CreateSampleDcf();
        var parentObj = new CanOpenObject
        {
            Index = 0x1018,
            ParameterName = "Identity Object",
            ObjectType = 0x9,
            SubNumber = 1
        };
        var sub = new CanOpenSubObject
        {
            SubIndex = 0,
            ParameterName = "Number of Entries",
            ObjectType = 0x7,
            DataType = 0x0005,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "4",
            ParameterValue = "4"
        };
        parentObj.SubObjects[0] = sub;
        dcf.ObjectDictionary.Objects[0x1018] = parentObj;
        dcf.ObjectDictionary.OptionalObjects.Add(0x1018);

        // Act
        var result = _writer.GenerateString(dcf);
        var document = XDocument.Parse(result);
        var subObject = document.Descendants()
            .Single(e => e.Name.LocalName == "CANopenSubObject" && GetAttributeValue(e, "subIndex") == "00");

        // Assert
        GetAttributeValue(subObject, "actualValue").Should().Be("4");
    }

    [Fact]
    public void GenerateString_SubObjectDenotation_WrittenCorrectly()
    {
        // Arrange — sub-object with Denotation set (covers AddCanOpenSubObjectXdcAttributes denotation branch)
        var dcf = CreateSampleDcf();
        var parentObj = new CanOpenObject
        {
            Index = 0x1018, ParameterName = "Identity Object", ObjectType = 0x9, SubNumber = 2
        };
        var sub = new CanOpenSubObject
        {
            SubIndex = 1,
            ParameterName = "Vendor ID",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "0x00000100",
            ParameterValue = "0x00000100",
            Denotation = "VendorIdentifier"
        };
        parentObj.SubObjects[1] = sub;
        dcf.ObjectDictionary.Objects[0x1018] = parentObj;
        dcf.ObjectDictionary.OptionalObjects.Add(0x1018);

        // Act
        var result = _writer.GenerateString(dcf);
        var document = XDocument.Parse(result);
        var subObject = document.Descendants()
            .Single(e => e.Name.LocalName == "CANopenSubObject" && GetAttributeValue(e, "subIndex") == "01");

        // Assert
        GetAttributeValue(subObject, "denotation").Should().Be("VendorIdentifier");
    }

    [Fact]
    public void GenerateString_DcfWithAdditionalSections_CreateEdsViewCoversAllPaths()
    {
        // Arrange — DCF with AdditionalSections to cover CreateEdsView kvp loop (line 114)
        var dcf = CreateSampleDcf();
        dcf.AdditionalSections["CustomSection"] = new System.Collections.Generic.Dictionary<string, string> { { "key", "value" } };

        // Act — should not throw; AdditionalSections are mapped through CreateEdsView
        var result = _writer.GenerateString(dcf);

        // Assert — output is valid
        result.Should().Contain("ISO15745ProfileContainer");
    }

    #endregion

    private static XElement GetSingleElement(XContainer container, string localName)
    {
        return container.Descendants().Single(e => e.Name.LocalName == localName);
    }

    private static string GetAttributeValue(XElement element, string attributeName)
    {
        return element.Attributes().Single(a => a.Name.LocalName == attributeName).Value;
    }

    #region NodeId validation

    [Theory]
    [InlineData(128)]
    [InlineData(255)]
    public void GenerateString_OutOfRangeNodeId_ThrowsXdcWriteExceptionWithSectionName(byte nodeId)
    {
        var dcf = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning { NodeId = nodeId }
        };

        var act = () => _writer.GenerateString(dcf);

        var ex = act.Should().Throw<XdcWriteException>().Which;
        ex.SectionName.Should().Be("deviceCommissioning");
        ex.Message.Should().Contain("NodeId");
    }

    [Fact]
    public void GenerateString_NodeIdZero_OmitsDeviceCommissioningElement()
    {
        // NodeId == 0 means "no commissioning configured"; the element must be omitted
        // so that read→write round-trips for XDC files without commissioning succeed.
        var dcf = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning { NodeId = 0 }
        };

        var result = _writer.GenerateString(dcf);

        result.Should().NotContain("deviceCommissioning");
    }

    [Fact]
    public void WriteFile_OutOfRangeNodeId_ThrowsXdcWriteException()
    {
        var dcf = CreateSampleDcf();
        dcf.DeviceCommissioning!.NodeId = 128;
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xdc");

        try
        {
            var act = () => _writer.WriteFile(dcf, tempFile);

            var ex = act.Should().Throw<XdcWriteException>().Which;
            ex.Message.Should().Contain("NodeId");
            ex.SectionName.Should().Be("deviceCommissioning");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteFileAsync_InvalidPath_ThrowsXdcWriteException()
    {
        var dcf = CreateSampleDcf();
        var invalidPath = "/invalid/path/that/does/not/exist/async.xdc";

        var act = () => _writer.WriteFileAsync(dcf, invalidPath);

        (await act.Should().ThrowAsync<XdcWriteException>())
            .WithMessage("*Failed to write XDC file*");
    }

    [Fact]
    public async Task WriteFileAsync_Cancelled_ThrowsOperationCanceledException()
    {
        var dcf = CreateSampleDcf();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xdc");

        try
        {
            var act = () => _writer.WriteFileAsync(dcf, tempFile, cts.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteFileAsync_OutOfRangeNodeId_RethrowsXdcWriteException()
    {
        var dcf = CreateSampleDcf();
        dcf.DeviceCommissioning!.NodeId = 128;
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xdc");

        try
        {
            var act = () => _writer.WriteFileAsync(dcf, tempFile);

            var ex = (await act.Should().ThrowAsync<XdcWriteException>()).Which;
            ex.SectionName.Should().Be("deviceCommissioning");
            ex.Message.Should().Contain("NodeId");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void GenerateString_NullDeviceInfo_WrapsXddWriteExceptionAsXdcWriteException()
    {
        var dcf = CreateSampleDcf();
        dcf.DeviceInfo = null!;

        var act = () => _writer.GenerateString(dcf);

        var ex = act.Should().Throw<XdcWriteException>().Which;
        ex.InnerException.Should().NotBeNull();
        ex.SectionName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateString_NullDcf_UsesDocumentFallbackSection()
    {
        var act = () => _writer.GenerateString(null!);

        var ex = act.Should().Throw<XdcWriteException>().Which;
        ex.SectionName.Should().Be("Document");
        ex.Message.Should().Contain("Failed to write section [Document]");
        ex.InnerException.Should().NotBeNull();
    }

    [Fact]
    public void GenerateString_XddExceptionWithoutInner_UsesOriginalExceptionAsInner()
    {
        var writer = new ThrowingXddWithoutInnerXdcWriter();

        var act = () => writer.GenerateString(CreateSampleDcf());

        var ex = act.Should().Throw<XdcWriteException>().Which;
        ex.SectionName.Should().Be("Document");
        ex.InnerException.Should().BeSameAs(writer.ExpectedException);
    }

    private sealed class ThrowingXddWithoutInnerXdcWriter : XdcWriter
    {
        public XddWriteException ExpectedException { get; } = new("forced-xdd", "Document");

        protected override XDocument BuildDocument(
            ElectronicDataSheet eds,
            DeviceCommissioning? commissioning)
        {
            throw ExpectedException;
        }
    }

    #endregion
}
