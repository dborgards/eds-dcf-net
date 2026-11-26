namespace EdsDcfNet.Tests.Parsers;

using System.Text;
using EdsDcfNet;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;

public partial class XddReaderTests
{
    private readonly XddReader _reader = new();

    private const string MinimalXdd = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileHeader>
      <ProfileClassID>Device</ProfileClassID>
    </ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen""
                 fileName=""test.xdd"" fileCreator=""TestCreator""
                 fileCreationDate=""2025-01-15"" fileVersion=""1"">
      <DeviceIdentity>
        <vendorName>Test Vendor</vendorName>
        <vendorID>0x00000100</vendorID>
        <productName>Test Product</productName>
        <productID>0x00001001</productID>
      </DeviceIdentity>
      <DeviceManager/>
      <DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileHeader>
      <ProfileClassID>CommunicationNetwork</ProfileClassID>
    </ProfileHeader>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen""
                 fileName=""test.xdd"" fileCreator=""TestCreator""
                 fileCreationDate=""2025-01-15"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007""
                         accessType=""ro"" defaultValue=""0x00000000"" PDOmapping=""no""/>
        </CANopenObjectList>
      </ApplicationLayers>
      <TransportLayers>
        <PhysicalLayer>
          <baudRate defaultValue=""250 Kbps"">
            <supportedBaudRate value=""250 Kbps""/>
            <supportedBaudRate value=""500 Kbps""/>
          </baudRate>
        </PhysicalLayer>
      </TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""2"" nrOfTxPDO=""2""
                                bootUpSlave=""true"" layerSettingServiceSlave=""false""
                                groupMessaging=""false"" dynamicChannels=""0""/>
        <CANopenMasterFeatures bootUpMaster=""false""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

    #region ReadFile Tests

    [Fact]
    public void ReadFile_ValidXddFile_ParsesSuccessfully()
    {
        // Act
        var result = _reader.ReadFile("Fixtures/sample_device.xdd");

        // Assert
        result.Should().NotBeNull();
        result.FileInfo.Should().NotBeNull();
        result.DeviceInfo.Should().NotBeNull();
        result.ObjectDictionary.Should().NotBeNull();
    }

    [Fact]
    public void ReadFile_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Act
        var act = () => _reader.ReadFile("NonExistent.xdd");

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public async Task ReadFileAsync_ValidXddFile_ParsesSuccessfully()
    {
        // Act
        var result = await _reader.ReadFileAsync("Fixtures/sample_device.xdd");

        // Assert
        result.Should().NotBeNull();
        result.FileInfo.Should().NotBeNull();
        result.DeviceInfo.Should().NotBeNull();
        result.ObjectDictionary.Should().NotBeNull();
    }

    [Fact]
    public async Task ReadFileAsync_Utf16EncodedXdd_ParsesSuccessfully()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(tempFile, MinimalXdd, System.Text.Encoding.Unicode);

            var syncResult = _reader.ReadFile(tempFile);
            var asyncResult = await _reader.ReadFileAsync(tempFile);

            asyncResult.FileInfo.FileName.Should().Be(syncResult.FileInfo.FileName);
            asyncResult.DeviceInfo.VendorName.Should().Be("Test Vendor");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadFileAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        const string filePath = "NonExistent.xdd";

        // Act
        var act = () => _reader.ReadFileAsync(filePath);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>()
            .Where(ex => ex.FileName == filePath);
    }

    [Fact]
    public async Task ReadFileAsync_ContentExceedsCustomMaximumSize_ThrowsEdsParseException()
    {
        var act = () => _reader.ReadFileAsync("Fixtures/sample_device.xdd", maxInputSize: 256);

        await act.Should().ThrowAsync<EdsParseException>()
            .WithMessage("*too large*");
    }

    #endregion

    #region ReadString Tests

    [Fact]
    public void ReadString_ValidXddContent_ParsesSuccessfully()
    {
        // Act
        var result = _reader.ReadString(MinimalXdd);

        // Assert
        result.Should().NotBeNull();
        result.FileInfo.FileName.Should().Be("test.xdd");
        result.DeviceInfo.VendorName.Should().Be("Test Vendor");
    }

    [Fact]
    public void ReadString_MissingCommunicationNetworkProfile_ThrowsException()
    {
        // Arrange — only Device profile, no CommunicationNetwork profile
        const string xddNoCommNet = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""test.xdd"" fileVersion=""1"">
      <DeviceIdentity>
        <vendorName>Test</vendorName>
        <vendorID>0x00000001</vendorID>
        <productName>Test</productName>
        <productID>0x00000001</productID>
      </DeviceIdentity>
      <DeviceManager/>
      <DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        // Act
        var act = () => _reader.ReadString(xddNoCommNet);

        // Assert
        act.Should().Throw<EdsParseException>();
    }

    #endregion

    #region Security Hardening Tests

    [Fact]
    public void ReadString_WithDoctype_ThrowsEdsParseException()
    {
        var xdd = MinimalXdd.Replace(
            @"<?xml version=""1.0"" encoding=""utf-8""?>",
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<!DOCTYPE ISO15745ProfileContainer [<!ENTITY test ""x"">]>");

        var act = () => _reader.ReadString(xdd);

        act.Should().Throw<EdsParseException>();
    }

    [Fact]
    public void ReadString_ContentExceedsMaximumSize_ThrowsEdsParseException()
    {
        var oversizedContent = new string('A', checked((int)IniParser.DefaultMaxInputSize + 1));

        var act = () => _reader.ReadString(oversizedContent);

        act.Should().Throw<EdsParseException>()
            .WithMessage("*too large*");
    }

    [Fact]
    public void ReadString_ContentExceedsCustomMaximumSize_ThrowsEdsParseException()
    {
        var act = () => _reader.ReadString(MinimalXdd, maxInputSize: 128);

        act.Should().Throw<EdsParseException>()
            .WithMessage("*too large*");
    }

    [Fact]
    public void ReadString_ContentWithinCustomMaximumSize_ParsesSuccessfully()
    {
        var result = _reader.ReadString(MinimalXdd, maxInputSize: MinimalXdd.Length + 10);

        result.FileInfo.FileName.Should().Be("test.xdd");
    }

    [Fact]
    public void ReadFile_ContentExceedsMaximumSize_ThrowsEdsParseException()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            using (var stream = new FileStream(tempFile, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                stream.SetLength(IniParser.DefaultMaxInputSize + 1);
            }

            var act = () => _reader.ReadFile(tempFile);

            act.Should().Throw<EdsParseException>()
                .WithMessage("*too large*");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ReadFile_ContentExceedsCustomMaximumSize_ThrowsEdsParseException()
    {
        var act = () => _reader.ReadFile("Fixtures/sample_device.xdd", maxInputSize: 256);

        act.Should().Throw<EdsParseException>()
            .WithMessage("*too large*");
    }

    [Fact]
    public void ReadFile_ContentAtExactMaxInputSize_ParsesSuccessfully()
    {
        // Arrange — FileInfo pre-check passes at exact size; bounded stream read
        // enforces the character limit while loading.
        var tempFile = Path.GetTempFileName();

        try
        {
            File.WriteAllText(tempFile, MinimalXdd);

            var result = _reader.ReadFile(tempFile, maxInputSize: Encoding.UTF8.GetByteCount(MinimalXdd));

            result.FileInfo.FileName.Should().Be("test.xdd");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ReadFile_ContentOneOverMaxInputSize_ThrowsEdsParseException()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            File.WriteAllText(tempFile, MinimalXdd);

            var act = () => _reader.ReadFile(tempFile, maxInputSize: Encoding.UTF8.GetByteCount(MinimalXdd) - 1);

            act.Should().Throw<EdsParseException>()
                .WithMessage("*too large*");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadFileAsync_ContentAtExactMaxInputSize_ParsesSuccessfully()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(tempFile, MinimalXdd);

            var result = await _reader.ReadFileAsync(tempFile, maxInputSize: Encoding.UTF8.GetByteCount(MinimalXdd));

            result.FileInfo.FileName.Should().Be("test.xdd");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadFileAsync_ContentOneOverMaxInputSize_ThrowsEdsParseException()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(tempFile, MinimalXdd);

            var act = () => _reader.ReadFileAsync(tempFile, maxInputSize: Encoding.UTF8.GetByteCount(MinimalXdd) - 1);

            await act.Should().ThrowAsync<EdsParseException>()
                .WithMessage("*too large*");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ReadStream_ContentExceedsCustomMaximumSize_ThrowsEdsParseException()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(MinimalXdd));
        var act = () => _reader.ReadStream(stream, maxInputSize: 128);

        act.Should().Throw<EdsParseException>()
            .WithMessage("*too large*");
    }

    [Fact]
    public async Task ReadStreamAsync_ContentExceedsCustomMaximumSize_ThrowsEdsParseException()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(MinimalXdd));
        var act = () => _reader.ReadStreamAsync(stream, maxInputSize: 128);

        await act.Should().ThrowAsync<EdsParseException>()
            .WithMessage("*too large*");
    }

    [Fact]
    public void ReadStream_ValidContent_ParsesSuccessfully()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(MinimalXdd));

        var result = _reader.ReadStream(stream, maxInputSize: MinimalXdd.Length + 64);

        result.FileInfo.FileName.Should().Be("test.xdd");
        result.DeviceInfo.VendorName.Should().Be("Test Vendor");
        stream.CanRead.Should().BeTrue();
    }

    [Fact]
    public async Task ReadStreamAsync_ValidContent_ParsesSuccessfully()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(MinimalXdd));

        var result = await _reader.ReadStreamAsync(stream, maxInputSize: MinimalXdd.Length + 64);

        result.FileInfo.FileName.Should().Be("test.xdd");
        result.DeviceInfo.VendorName.Should().Be("Test Vendor");
        stream.CanRead.Should().BeTrue();
    }

    [Fact]
    public void ReadStream_NonSeekableReadableStream_ParsesSuccessfully()
    {
        using var stream = new NonSeekableReadStream(Encoding.UTF8.GetBytes(MinimalXdd));

        var result = _reader.ReadStream(stream, maxInputSize: MinimalXdd.Length + 64);

        result.FileInfo.FileName.Should().Be("test.xdd");
        result.DeviceInfo.VendorName.Should().Be("Test Vendor");
    }

    [Fact]
    public void ReadStream_NullStream_ThrowsArgumentNullException()
    {
        var act = () => _reader.ReadStream(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("stream");
    }

    #endregion

    #region FileInfo Tests

    [Fact]
    public void FileInfo_ParsesAllFields()
    {
        // Act
        var result = _reader.ReadString(MinimalXdd);

        // Assert
        result.FileInfo.FileName.Should().Be("test.xdd");
        result.FileInfo.CreatedBy.Should().Be("TestCreator");
        result.FileInfo.CreationDate.Should().Be("01-15-2025");
        result.FileInfo.FileVersion.Should().Be(1);
    }

    #endregion

    #region DeviceInfo Tests

    [Fact]
    public void DeviceInfo_ParsesVendorAndProduct()
    {
        // Act
        var result = _reader.ReadString(MinimalXdd);

        // Assert
        result.DeviceInfo.VendorName.Should().Be("Test Vendor");
        result.DeviceInfo.VendorNumber.Should().Be(0x00000100u);
        result.DeviceInfo.ProductName.Should().Be("Test Product");
        result.DeviceInfo.ProductNumber.Should().Be(0x00001001u);
    }

    [Fact]
    public void DeviceInfo_ParsesGeneralFeatures()
    {
        // Act
        var result = _reader.ReadString(MinimalXdd);

        // Assert
        result.DeviceInfo.Granularity.Should().Be(8);
        result.DeviceInfo.NrOfRxPdo.Should().Be(2);
        result.DeviceInfo.NrOfTxPdo.Should().Be(2);
        result.DeviceInfo.SimpleBootUpSlave.Should().BeTrue();
        result.DeviceInfo.LssSupported.Should().BeFalse();
        result.DeviceInfo.GroupMessaging.Should().BeFalse();
        result.DeviceInfo.SimpleBootUpMaster.Should().BeFalse();
    }

    [Fact]
    public void DeviceInfo_ParsesSupportedBaudRates()
    {
        // Act
        var result = _reader.ReadString(MinimalXdd);

        // Assert
        result.DeviceInfo.SupportedBaudRates.BaudRate250.Should().BeTrue();
        result.DeviceInfo.SupportedBaudRates.BaudRate500.Should().BeTrue();
        result.DeviceInfo.SupportedBaudRates.BaudRate125.Should().BeFalse();
    }

    #endregion

    #region ObjectDictionary Tests

    [Fact]
    public void ObjectDictionary_ParsesMandatoryObjects()
    {
        // Act
        var result = _reader.ReadString(MinimalXdd);

        // Assert
        result.ObjectDictionary.MandatoryObjects.Should().Contain(0x1000);
        result.ObjectDictionary.Objects.Should().ContainKey(0x1000);
    }

    [Fact]
    public void ObjectDictionary_ParsesOptionalObjects()
    {
        // Arrange — XDD with optional object 1008h
        const string xdd = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <DeviceIdentity><vendorName>T</vendorName><vendorID>0x1</vendorID><productName>T</productName><productID>0x1</productID></DeviceIdentity>
      <DeviceManager/><DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""0"" optionalObjects=""1"" manufacturerObjects=""0"">
          <CANopenObject index=""1008"" name=""Device Name"" objectType=""7"" dataType=""0009""
                         accessType=""ro"" defaultValue=""Test"" PDOmapping=""no""/>
        </CANopenObjectList>
      </ApplicationLayers>
      <TransportLayers><PhysicalLayer><baudRate defaultValue=""250 Kbps""/></PhysicalLayer></TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""0"" nrOfTxPDO=""0""
                                bootUpSlave=""false"" layerSettingServiceSlave=""false""
                                groupMessaging=""false"" dynamicChannels=""0""/>
        <CANopenMasterFeatures bootUpMaster=""false""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        // Act
        var result = _reader.ReadString(xdd);

        // Assert
        result.ObjectDictionary.OptionalObjects.Should().Contain(0x1008);
    }

    [Fact]
    public void ObjectDictionary_ParsesObjectAllProperties()
    {
        // Arrange — XDD with object having all standard properties
        const string xdd = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <DeviceIdentity><vendorName>T</vendorName><vendorID>0x1</vendorID><productName>T</productName><productID>0x1</productID></DeviceIdentity>
      <DeviceManager/><DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007""
                         accessType=""ro"" defaultValue=""0x00000191""
                         lowLimit=""0"" highLimit=""0xFFFFFFFF""
                         PDOmapping=""no"" objFlags=""1"" subNumber=""0""/>
        </CANopenObjectList>
      </ApplicationLayers>
      <TransportLayers><PhysicalLayer><baudRate defaultValue=""250 Kbps""/></PhysicalLayer></TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""0"" nrOfTxPDO=""0""
                                bootUpSlave=""false"" layerSettingServiceSlave=""false""
                                groupMessaging=""false"" dynamicChannels=""0""/>
        <CANopenMasterFeatures bootUpMaster=""false""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        // Act
        var result = _reader.ReadString(xdd);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x1000];
        obj.Index.Should().Be(0x1000);
        obj.ParameterName.Should().Be("Device Type");
        obj.ObjectType.Should().Be(7);
        obj.DataType.Should().Be(0x0007);
        obj.AccessType.Should().Be(AccessType.ReadOnly);
        obj.DefaultValue.Should().Be("0x00000191");
        obj.LowLimit.Should().Be("0");
        obj.HighLimit.Should().Be("0xFFFFFFFF");
        obj.PdoMapping.Should().BeFalse();
        obj.ObjFlags.Should().Be(1u);
        obj.SubNumber.Should().Be(0);
    }

    [Fact]
    public void ObjectDictionary_ParsesSubObjects()
    {
        // Act — sample_device.xdd has Identity Object with sub-objects
        var result = _reader.ReadFile("Fixtures/sample_device.xdd");

        // Assert
        var obj = result.ObjectDictionary.Objects[0x1018];
        obj.SubObjects.Should().ContainKey(0);
        obj.SubObjects.Should().ContainKey(1);
        obj.SubObjects[0].ParameterName.Should().Be("Number of Entries");
        obj.SubObjects[1].ParameterName.Should().Be("Vendor ID");
    }

    [Fact]
    public void ObjectDictionary_ParsesDummyUsage()
    {
        // Act
        var result = _reader.ReadFile("Fixtures/sample_device.xdd");

        // Assert
        result.ObjectDictionary.DummyUsage.Should().ContainKey(0x0002);
        result.ObjectDictionary.DummyUsage[0x0002].Should().BeTrue();
        result.ObjectDictionary.DummyUsage.Should().ContainKey(0x0005);
        result.ObjectDictionary.DummyUsage[0x0005].Should().BeTrue();
    }

    #endregion

    #region AccessType Tests

    [Theory]
    [InlineData("const", AccessType.Constant)]
    [InlineData("ro", AccessType.ReadOnly)]
    [InlineData("wo", AccessType.WriteOnly)]
    [InlineData("rw", AccessType.ReadWrite)]
    public void AccessTypes_AllMappedCorrectly(string xddType, AccessType expected)
    {
        // Arrange
        var xdd = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <DeviceIdentity><vendorName>T</vendorName><vendorID>0x1</vendorID><productName>T</productName><productID>0x1</productID></DeviceIdentity>
      <DeviceManager/><DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Test"" objectType=""7"" dataType=""0007""
                         accessType=""{xddType}"" PDOmapping=""no""/>
        </CANopenObjectList>
      </ApplicationLayers>
      <TransportLayers><PhysicalLayer><baudRate defaultValue=""250 Kbps""/></PhysicalLayer></TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""0"" nrOfTxPDO=""0""
                                bootUpSlave=""false"" layerSettingServiceSlave=""false""
                                groupMessaging=""false"" dynamicChannels=""0""/>
        <CANopenMasterFeatures bootUpMaster=""false""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        // Act
        var result = _reader.ReadString(xdd);

        // Assert
        result.ObjectDictionary.Objects[0x1000].AccessType.Should().Be(expected);
    }

    [Fact]
    public void PdoMapping_no_MappedToFalse()
    {
        // Act
        var result = _reader.ReadString(MinimalXdd);

        // Assert
        result.ObjectDictionary.Objects[0x1000].PdoMapping.Should().BeFalse();
        result.ObjectDictionary.Objects[0x1000].PdoMappingMode.Should().Be(PdoMappingMode.No);
    }

    [Fact]
    public void PdoMapping_optional_MappedToTrue()
    {
        // Arrange
        const string xdd = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <DeviceIdentity><vendorName>T</vendorName><vendorID>0x1</vendorID><productName>T</productName><productID>0x1</productID></DeviceIdentity>
      <DeviceManager/><DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""0"" optionalObjects=""1"" manufacturerObjects=""0"">
          <CANopenObject index=""1001"" name=""Error Register"" objectType=""7"" dataType=""0005""
                         accessType=""ro"" PDOmapping=""optional""/>
        </CANopenObjectList>
      </ApplicationLayers>
      <TransportLayers><PhysicalLayer><baudRate defaultValue=""250 Kbps""/></PhysicalLayer></TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""0"" nrOfTxPDO=""0""
                                bootUpSlave=""false"" layerSettingServiceSlave=""false""
                                groupMessaging=""false"" dynamicChannels=""0""/>
        <CANopenMasterFeatures bootUpMaster=""false""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        // Act
        var result = _reader.ReadString(xdd);

        // Assert
        result.ObjectDictionary.Objects[0x1001].PdoMapping.Should().BeTrue();
        result.ObjectDictionary.Objects[0x1001].PdoMappingMode.Should().Be(PdoMappingMode.Optional);
    }

    [Theory]
    [InlineData("default", PdoMappingMode.Default)]
    [InlineData("TPDO", PdoMappingMode.Tpdo)]
    [InlineData("RPDO", PdoMappingMode.Rpdo)]
    [InlineData("tpdo", PdoMappingMode.Tpdo)]
    public void PdoMapping_KnownMappableTokens_PreserveMode(string token, PdoMappingMode expected)
    {
        var xdd = MinimalXdd.Replace(@"PDOmapping=""no""", $@"PDOmapping=""{token}""");

        var result = _reader.ReadString(xdd);

        result.ObjectDictionary.Objects[0x1000].PdoMapping.Should().BeTrue();
        result.ObjectDictionary.Objects[0x1000].PdoMappingMode.Should().Be(expected);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("yes")]
    [InlineData("unknown")]
    public void PdoMapping_UnknownToken_ThrowsEdsParseException(string token)
    {
        var xdd = MinimalXdd.Replace(@"PDOmapping=""no""", $@"PDOmapping=""{token}""");

        var act = () => _reader.ReadString(xdd);

        act.Should().Throw<EdsParseException>()
            .WithMessage("*PDOmapping*");
    }

    [Fact]
    public void PdoMapping_ValidatedRoundTrip_PreservesTpdoMode()
    {
        var eds = _reader.ReadString(MinimalXdd.Replace(@"PDOmapping=""no""", @"PDOmapping=""TPDO"""));
        eds.ObjectDictionary.Objects[0x1000].PdoMappingMode.Should().Be(PdoMappingMode.Tpdo);

        var written = new EdsDcfNet.Writers.XddWriter().GenerateString(eds);
        written.Should().Contain(@"PDOmapping=""TPDO""");

        var roundTripped = _reader.ReadString(written);
        roundTripped.ObjectDictionary.Objects[0x1000].PdoMappingMode.Should().Be(PdoMappingMode.Tpdo);
        roundTripped.ObjectDictionary.Objects[0x1000].PdoMapping.Should().BeTrue();
    }

    #endregion

    #region ApplicationProcess Tests

    [Fact]
    public void ApplicationProcess_ParsedToTypedModel()
    {
        // Arrange
        const string xdd = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <DeviceIdentity><vendorName>T</vendorName><vendorID>0x1</vendorID><productName>T</productName><productID>0x1</productID></DeviceIdentity>
      <DeviceManager/>
      <DeviceFunction/>
      <ApplicationProcess>
        <dataTypeList>
          <struct name=""MyStruct"" uniqueID=""uid_s1"">
            <varDeclaration name=""Field1"" uniqueID=""uid_v1""><BOOL/></varDeclaration>
          </struct>
          <enum name=""MyEnum"" uniqueID=""uid_e1"">
            <enumValue value=""0""/>
            <enumValue value=""1""/>
          </enum>
        </dataTypeList>
        <parameterList>
          <parameter uniqueID=""uid_p1"" access=""read"">
            <UINT/>
          </parameter>
        </parameterList>
      </ApplicationProcess>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007""
                         accessType=""ro"" PDOmapping=""no""/>
        </CANopenObjectList>
      </ApplicationLayers>
      <TransportLayers><PhysicalLayer><baudRate defaultValue=""250 Kbps""/></PhysicalLayer></TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""0"" nrOfTxPDO=""0""
                                bootUpSlave=""false"" layerSettingServiceSlave=""false""
                                groupMessaging=""false"" dynamicChannels=""0""/>
        <CANopenMasterFeatures bootUpMaster=""false""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        // Act
        var result = _reader.ReadString(xdd);

        // Assert
        result.ApplicationProcess.Should().NotBeNull();
        result.ApplicationProcess!.DataTypeList.Should().NotBeNull();
        result.ApplicationProcess.DataTypeList!.Structs.Should().HaveCount(1);
        result.ApplicationProcess.DataTypeList.Structs[0].Name.Should().Be("MyStruct");
        result.ApplicationProcess.DataTypeList.Structs[0].UniqueId.Should().Be("uid_s1");
        result.ApplicationProcess.DataTypeList.Structs[0].VarDeclarations.Should().HaveCount(1);
        result.ApplicationProcess.DataTypeList.Structs[0].VarDeclarations[0].Name.Should().Be("Field1");
        result.ApplicationProcess.DataTypeList.Enums.Should().HaveCount(1);
        result.ApplicationProcess.DataTypeList.Enums[0].Name.Should().Be("MyEnum");
        result.ApplicationProcess.DataTypeList.Enums[0].EnumValues.Should().HaveCount(2);
        result.ApplicationProcess.ParameterList.Should().HaveCount(1);
        result.ApplicationProcess.ParameterList[0].UniqueId.Should().Be("uid_p1");
        result.ApplicationProcess.ParameterList[0].Access.Should().Be("read");
    }

    [Fact]
    public void ApplicationProcess_FunctionTypeTemplateAndInstanceLists_AreParsed()
    {
        // Arrange
        const string xdd = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ISO15745ProfileContainer xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_Device_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <DeviceIdentity><vendorName>T</vendorName><vendorID>0x1</vendorID><productName>T</productName><productID>0x1</productID></DeviceIdentity>
      <DeviceManager/>
      <DeviceFunction/>
      <ApplicationProcess>
        <functionTypeList>
          <functionType name=""Controller"" uniqueID=""uid_ft1"" package=""pkg.ctrl"">
            <functionInstanceList>
              <functionInstance name=""nested1"" uniqueID=""uid_fi_nested"" typeIDRef=""uid_ft1""/>
            </functionInstanceList>
          </functionType>
        </functionTypeList>
        <functionInstanceList>
          <functionInstance name=""top1"" uniqueID=""uid_fi_top"" typeIDRef=""uid_ft1""/>
          <connection source=""top1.Out"" destination=""top1.In"" description=""loop""/>
        </functionInstanceList>
        <templateList>
          <parameterTemplate uniqueID=""uid_tpl1"" access=""readWrite"">
            <UINT/>
            <defaultValue value=""7""/>
          </parameterTemplate>
          <allowedValuesTemplate uniqueID=""uid_avt1"">
            <value value=""1""/>
            <value value=""2""/>
          </allowedValuesTemplate>
        </templateList>
        <parameterList>
          <parameter uniqueID=""uid_p1"" access=""read"" templateIDRef=""uid_tpl1""><UINT/></parameter>
        </parameterList>
      </ApplicationProcess>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileBody xsi:type=""ProfileBody_CommunicationNetwork_CANopen"" fileName=""t.xdd"" fileVersion=""1"">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects=""1"" optionalObjects=""0"" manufacturerObjects=""0"">
          <CANopenObject index=""1000"" name=""Device Type"" objectType=""7"" dataType=""0007""
                         accessType=""ro"" PDOmapping=""no""/>
        </CANopenObjectList>
      </ApplicationLayers>
      <TransportLayers><PhysicalLayer><baudRate defaultValue=""250 Kbps""/></PhysicalLayer></TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity=""8"" nrOfRxPDO=""0"" nrOfTxPDO=""0""
                                bootUpSlave=""false"" layerSettingServiceSlave=""false""
                                groupMessaging=""false"" dynamicChannels=""0""/>
        <CANopenMasterFeatures bootUpMaster=""false""/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>";

        // Act
        var result = _reader.ReadString(xdd);

        // Assert
        var ap = result.ApplicationProcess;
        ap.Should().NotBeNull();
        ap!.FunctionTypeList.Should().ContainSingle();
        ap.FunctionTypeList[0].Name.Should().Be("Controller");
        ap.FunctionTypeList[0].UniqueId.Should().Be("uid_ft1");
        ap.FunctionTypeList[0].Package.Should().Be("pkg.ctrl");
        ap.FunctionTypeList[0].FunctionInstanceList.Should().NotBeNull();
        ap.FunctionTypeList[0].FunctionInstanceList!.FunctionInstances.Should().ContainSingle(fi => fi.Name == "nested1");

        ap.FunctionInstanceList.Should().NotBeNull();
        ap.FunctionInstanceList!.FunctionInstances.Should().ContainSingle(fi => fi.Name == "top1");
        ap.FunctionInstanceList.Connections.Should().ContainSingle(c => c.Source == "top1.Out" && c.Destination == "top1.In");

        ap.TemplateList.Should().NotBeNull();
        ap.TemplateList!.ParameterTemplates.Should().ContainSingle(t => t.UniqueId == "uid_tpl1");
        ap.TemplateList.AllowedValuesTemplates.Should().ContainSingle(t => t.UniqueId == "uid_avt1");
        ap.ParameterList.Should().ContainSingle(p => p.UniqueId == "uid_p1" && p.TemplateIdRef == "uid_tpl1");
    }

    #endregion

    #region Sample File Integration Test

    [Fact]
    public void SampleDeviceXdd_ParsesSuccessfully()
    {
        // Act
        var result = _reader.ReadFile("Fixtures/sample_device.xdd");

        // Assert — FileInfo
        result.FileInfo.FileName.Should().Be("sample_device.xdd");
        result.FileInfo.FileVersion.Should().Be(1);

        // Assert — DeviceInfo
        result.DeviceInfo.VendorName.Should().Be("Example Automation Inc.");
        result.DeviceInfo.ProductName.Should().Be("IO-Module 16x16");
        result.DeviceInfo.VendorNumber.Should().Be(0x00000100u);
        result.DeviceInfo.ProductNumber.Should().Be(0x00001001u);

        // Assert — ObjectDictionary
        result.ObjectDictionary.MandatoryObjects.Should().Contain(0x1000);
        result.ObjectDictionary.MandatoryObjects.Should().Contain(0x1001);
        result.ObjectDictionary.Objects[0x1000].ParameterName.Should().Be("Device Type");
        result.ObjectDictionary.Objects[0x1001].ParameterName.Should().Be("Error Register");

        // Assert — DummyUsage
        result.ObjectDictionary.DummyUsage.Should().ContainKey(0x0002);

        // Assert — General Features
        result.DeviceInfo.Granularity.Should().Be(8);
        result.DeviceInfo.NrOfRxPdo.Should().Be(4);
        result.DeviceInfo.NrOfTxPdo.Should().Be(4);
        result.DeviceInfo.SimpleBootUpSlave.Should().BeTrue();

        // Assert — BaudRates
        result.DeviceInfo.SupportedBaudRates.BaudRate250.Should().BeTrue();
        result.DeviceInfo.SupportedBaudRates.BaudRate500.Should().BeTrue();

        // Assert — ApplicationProcess (typed model)
        result.ApplicationProcess.Should().NotBeNull();
        var ap = result.ApplicationProcess!;
        ap.DataTypeList.Should().NotBeNull();
        ap.DataTypeList!.Structs.Should().ContainSingle(s => s.Name == "IoStatus");
        ap.DataTypeList.Enums.Should().ContainSingle(e => e.Name == "ControlMode");
        ap.DataTypeList.Enums[0].EnumValues.Should().HaveCount(3);
        ap.ParameterList.Should().HaveCount(2);
        ap.ParameterList.Should().Contain(p => p.UniqueId == "uid_p_mode");
        ap.ParameterList.Should().Contain(p => p.UniqueId == "uid_p_status");
        ap.ParameterGroupList.Should().HaveCount(1);
        ap.ParameterGroupList[0].UniqueId.Should().Be("uid_pg_config");
        ap.ParameterGroupList[0].SubGroups.Should().HaveCount(1);
    }

    #endregion

    private sealed class NonSeekableReadStream : Stream
    {
        private readonly MemoryStream _inner;

        public NonSeekableReadStream(byte[] data)
        {
            _inner = new MemoryStream(data);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
