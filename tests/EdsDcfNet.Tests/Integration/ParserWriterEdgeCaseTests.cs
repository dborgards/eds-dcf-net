namespace EdsDcfNet.Tests.Integration;

using System.Globalization;
using System.Text;
using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;

/// <summary>
/// Edge-case matrix for issue #122:
/// - malformed numeric literals -> ReadDcfFromString_MalformedHexLiteral_ThrowsActionableParseException
/// - UTF-8 BOM and mixed line endings -> ReadDcf_FileWithUtf8Bom_ParsesSuccessfully, ReadDcfFromString_MixedLineEndings_ParsesSuccessfully
/// - large object dictionary round-trip -> RoundTrip_LargeObjectDictionary_PreservesObjectAndListCounts
/// - Unicode/non-ASCII round-trip -> RoundTrip_UnicodeAndNonAsciiValues_PreservesContent
/// - unsigned boundary handling -> ReadDcfFromString_UnsignedBoundaries_ParsesCorrectly, ReadDcfFromString_NegativeUnsignedValue_ThrowsParseException
/// </summary>
public class ParserWriterEdgeCaseTests
{
    [Fact]
    public void ReadDcfFromString_MalformedHexLiteral_ThrowsActionableParseException()
    {
        // Arrange
        var content = BuildMinimalDcf(
            ("VendorNumber", "0xGGG"));

        // Act
        var act = () => CanOpenFile.ReadDcfFromString(content);

        // Assert
        act.Should().Throw<EdsParseException>()
            .WithMessage("*0xGGG*");
    }

    [Fact]
    public void ReadDcf_FileWithUtf8Bom_ParsesSuccessfully()
    {
        // Arrange
        var content = BuildMinimalDcf();
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dcf");

        try
        {
            File.WriteAllText(tempFile, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            // Act
            var result = CanOpenFile.ReadDcf(tempFile);

            // Assert
            result.FileInfo.FileName.Should().Be("edgecase.dcf");
            result.DeviceCommissioning.NodeId.Should().Be(7);
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
    public void ReadDcfFromString_MixedLineEndings_ParsesSuccessfully()
    {
        // Arrange
        var content = string.Join(
            "\n",
            "[FileInfo]\r",
            "FileName=edgecase.dcf",
            "FileVersion=1\r",
            "FileRevision=0",
            "EDSVersion=4.0\r",
            "",
            "[DeviceInfo]",
            "VendorName=Vendor\r",
            "ProductName=Product",
            "VendorNumber=0x100\r",
            "ProductNumber=0x1001",
            "",
            "[DeviceCommissioning]\r",
            "NodeID=7",
            "Baudrate=250\r",
            "NodeName=Node7",
            "",
            "[MandatoryObjects]",
            "SupportedObjects=1\r",
            "1=0x1000",
            "",
            "[1000]\r",
            "ParameterName=Device Type",
            "ObjectType=0x7\r",
            "DataType=0x7",
            "AccessType=ro\r",
            "DefaultValue=0x191",
            "PDOMapping=0");

        // Act
        var result = CanOpenFile.ReadDcfFromString(content);

        // Assert
        result.DeviceCommissioning.NodeId.Should().Be(7);
        result.ObjectDictionary.Objects.Should().ContainKey((ushort)0x1000);
    }

    [Fact]
    public void RoundTrip_LargeObjectDictionary_PreservesObjectAndListCounts()
    {
        // Arrange
        var original = CreateBaseDcfModel();
        for (ushort i = 0; i < 500; i++)
        {
            var index = (ushort)(0x2000 + i);
            original.ObjectDictionary.ManufacturerObjects.Add(index);
            original.ObjectDictionary.Objects[index] = new CanOpenObject
            {
                Index = index,
                ParameterName = $"Obj{index:X4}",
                ObjectType = 0x7,
                DataType = 0x0007,
                AccessType = AccessType.ReadWrite,
                DefaultValue = "0",
                PdoMapping = false
            };
        }

        // Act
        var written = CanOpenFile.WriteDcfToString(original);
        var roundTripped = CanOpenFile.ReadDcfFromString(written);

        // Assert
        roundTripped.ObjectDictionary.Objects.Count.Should().Be(original.ObjectDictionary.Objects.Count);
        roundTripped.ObjectDictionary.ManufacturerObjects.Count.Should().Be(original.ObjectDictionary.ManufacturerObjects.Count);
    }

    [Fact]
    public void RoundTrip_UnicodeAndNonAsciiValues_PreservesContent()
    {
        // Arrange
        var original = CreateBaseDcfModel();
        original.FileInfo.Description = "Gerät über CANopen";
        original.DeviceInfo.ProductName = "Prüfgerät ÄÖÜ";
        original.DeviceCommissioning.NodeName = "Knoten-ß";
        original.ObjectDictionary.Objects[0x1000].ParameterName = "Temperaturfühler °C";

        // Act
        var written = CanOpenFile.WriteDcfToString(original);
        var roundTripped = CanOpenFile.ReadDcfFromString(written);

        // Assert
        roundTripped.FileInfo.Description.Should().Be("Gerät über CANopen");
        roundTripped.DeviceInfo.ProductName.Should().Be("Prüfgerät ÄÖÜ");
        roundTripped.DeviceCommissioning.NodeName.Should().Be("Knoten-ß");
        roundTripped.ObjectDictionary.Objects[0x1000].ParameterName.Should().Be("Temperaturfühler °C");
    }

    [Fact]
    public void ReadDcfFromString_UnsignedBoundaries_ParsesCorrectly()
    {
        // Arrange
        var content = BuildMinimalDcf(
            ("FileVersion", byte.MaxValue.ToString(CultureInfo.InvariantCulture)),
            ("VendorNumber", uint.MaxValue.ToString(CultureInfo.InvariantCulture)),
            ("ProductNumber", "0xFFFFFFFF"),
            ("NodeID", "127"),
            ("Baudrate", ushort.MaxValue.ToString(CultureInfo.InvariantCulture)));

        // Act
        var result = CanOpenFile.ReadDcfFromString(content);

        // Assert
        result.FileInfo.FileVersion.Should().Be(byte.MaxValue);
        result.DeviceInfo.VendorNumber.Should().Be(uint.MaxValue);
        result.DeviceInfo.ProductNumber.Should().Be(uint.MaxValue);
        result.DeviceCommissioning.NodeId.Should().Be(127);
        result.DeviceCommissioning.Baudrate.Should().Be(ushort.MaxValue);
    }

    [Fact]
    public void ReadDcfFromString_NegativeUnsignedValue_ThrowsParseException()
    {
        // Arrange
        var content = BuildMinimalDcf(
            ("NodeID", "-1"));

        // Act
        var act = () => CanOpenFile.ReadDcfFromString(content);

        // Assert
        act.Should().Throw<EdsParseException>()
            .WithMessage("*-1*");
    }

    private static string BuildMinimalDcf(params (string Key, string Value)[] overrides)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["FileName"] = "edgecase.dcf",
            ["FileVersion"] = "1",
            ["FileRevision"] = "0",
            ["EDSVersion"] = "4.0",
            ["VendorName"] = "Vendor",
            ["ProductName"] = "Product",
            ["VendorNumber"] = "0x100",
            ["ProductNumber"] = "0x1001",
            ["NodeID"] = "7",
            ["Baudrate"] = "250",
            ["NodeName"] = "Node7"
        };

        foreach (var (key, value) in overrides)
        {
            values[key] = value;
        }

        return $"""
[FileInfo]
FileName={values["FileName"]}
FileVersion={values["FileVersion"]}
FileRevision={values["FileRevision"]}
EDSVersion={values["EDSVersion"]}

[DeviceInfo]
VendorName={values["VendorName"]}
ProductName={values["ProductName"]}
VendorNumber={values["VendorNumber"]}
ProductNumber={values["ProductNumber"]}

[DeviceCommissioning]
NodeID={values["NodeID"]}
Baudrate={values["Baudrate"]}
NodeName={values["NodeName"]}

[MandatoryObjects]
SupportedObjects=1
1=0x1000

[1000]
ParameterName=Device Type
ObjectType=0x7
DataType=0x7
AccessType=ro
DefaultValue=0x191
PDOMapping=0
""";
    }

    private static DeviceConfigurationFile CreateBaseDcfModel()
    {
        var dcf = new DeviceConfigurationFile
        {
            FileInfo = new EdsFileInfo
            {
                FileName = "edgecase.dcf",
                FileVersion = 1,
                FileRevision = 0,
                EdsVersion = "4.0",
                Description = "Edge-case test file"
            },
            DeviceInfo = new DeviceInfo
            {
                VendorName = "Vendor",
                ProductName = "Product",
                VendorNumber = 0x100,
                ProductNumber = 0x1001
            },
            DeviceCommissioning = new DeviceCommissioning
            {
                NodeId = 7,
                Baudrate = 250,
                NodeName = "Node7"
            },
            ObjectDictionary = new ObjectDictionary()
        };

        dcf.ObjectDictionary.MandatoryObjects.Add(0x1000);
        dcf.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "0x191",
            PdoMapping = false
        };

        return dcf;
    }
}
