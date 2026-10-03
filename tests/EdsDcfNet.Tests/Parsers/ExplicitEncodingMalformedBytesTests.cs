namespace EdsDcfNet.Tests.Parsers;

using System.Text;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;

/// <summary>
/// An explicit <see cref="CanOpenFileOptions.Encoding"/> is used as supplied, but invalid byte
/// sequences are rejected instead of being replaced with U+FFFD, even when the instance (such
/// as <see cref="Encoding.UTF8"/>) carries a replacement fallback. Covers all five formats.
/// </summary>
public class ExplicitEncodingMalformedBytesTests
{
    private static readonly byte[] Malformed = { 0xFF, 0xFE };

    private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

    private const string Placeholder = "@@";

    private static byte[] WithMalformedProductName(string asciiTemplate)
    {
        var at = asciiTemplate.IndexOf(Placeholder, StringComparison.Ordinal);
        at.Should().BeGreaterThan(0);
        return Encoding.ASCII.GetBytes(asciiTemplate.Substring(0, at))
            .Concat(Malformed)
            .Concat(Encoding.ASCII.GetBytes(asciiTemplate.Substring(at + Placeholder.Length)))
            .ToArray();
    }

    private static string FixtureWithMalformedProductName(string fixture, string productName)
        => File.ReadAllText(Path.Combine("Fixtures", fixture))
            .Replace("<productName>" + productName + "</productName>", "<productName>" + productName + Placeholder + "</productName>");

    private static byte[] Content(string format) => format switch
    {
        "eds" => WithMalformedProductName(
            "[FileInfo]\r\nFileName=x.eds\r\nFileVersion=1\r\nFileRevision=0\r\nEDSVersion=4.0\r\n"
            + "[DeviceInfo]\r\nVendorName=V\r\nProductName=P" + Placeholder + "\r\nVendorNumber=1\r\nProductNumber=1\r\n"),
        "dcf" => WithMalformedProductName(
            "[FileInfo]\r\nFileName=x.dcf\r\nFileVersion=1\r\nFileRevision=0\r\n"
            + "[DeviceInfo]\r\nVendorName=V\r\nProductName=P" + Placeholder + "\r\n[DeviceCommissioning]\r\nNodeID=1\r\n"),
        "cpj" => WithMalformedProductName("[Topology]\r\nNetName=P" + Placeholder + "\r\nNodes=0x00\r\n"),
        "xdd" => WithMalformedProductName(FixtureWithMalformedProductName("sample_device.xdd", "IO-Module 16x16")),
        "xdc" => WithMalformedProductName(FixtureWithMalformedProductName("minimal.xdc", "Minimal Device")),
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    private static string ReadProductName(string format, Stream stream, CanOpenFileOptions options) => format switch
    {
        "eds" => CanOpenFile.Eds.ReadStream(stream, options).DeviceInfo.ProductName,
        "dcf" => CanOpenFile.Dcf.ReadStream(stream, options).DeviceInfo.ProductName,
        "cpj" => CanOpenFile.Cpj.ReadStream(stream, options).Networks[0].NetName!,
        "xdd" => CanOpenFile.Xdd.ReadStream(stream, options).DeviceInfo.ProductName,
        "xdc" => CanOpenFile.Xdc.ReadStream(stream, options).DeviceInfo.ProductName,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    private static async Task<string> ReadProductNameFromFileAsync(string format, string path, CanOpenFileOptions options) => format switch
    {
        "eds" => (await CanOpenFile.Eds.ReadFileAsync(path, options)).DeviceInfo.ProductName,
        "dcf" => (await CanOpenFile.Dcf.ReadFileAsync(path, options)).DeviceInfo.ProductName,
        "cpj" => (await CanOpenFile.Cpj.ReadFileAsync(path, options)).Networks[0].NetName!,
        "xdd" => (await CanOpenFile.Xdd.ReadFileAsync(path, options)).DeviceInfo.ProductName,
        "xdc" => (await CanOpenFile.Xdc.ReadFileAsync(path, options)).DeviceInfo.ProductName,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    public static TheoryData<string, bool> FormatsAndModes()
    {
        var data = new TheoryData<string, bool>();
        foreach (var format in new[] { "eds", "dcf", "cpj", "xdd", "xdc" })
        {
            data.Add(format, false);
            data.Add(format, true);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(FormatsAndModes))]
    public void ReadStream_ExplicitUtf8WithReplacementFallback_MalformedBytes_ThrowsWithCode(string format, bool strictParsing)
    {
        // Arrange
        var options = new CanOpenFileOptions { Encoding = Encoding.UTF8, StrictParsing = strictParsing };
        using var stream = new MemoryStream(Content(format));

        // Act
        var act = () => ReadProductName(format, stream, options);

        // Assert
        var exception = act.Should().Throw<EdsParseException>().Which;
        exception.Code.Should().Be(ParseDiagnosticCodes.InvalidEncodedBytes);
        exception.Message.Should().Contain("content could not be decoded with encoding 'utf-8'");
        exception.InnerException.Should().BeOfType<DecoderFallbackException>();
    }

    [Theory]
    [MemberData(nameof(FormatsAndModes))]
    public async Task ReadFileAsync_ExplicitUtf8WithReplacementFallback_MalformedBytes_ThrowsWithCode(string format, bool strictParsing)
    {
        // Arrange
        var options = new CanOpenFileOptions { Encoding = Encoding.UTF8, StrictParsing = strictParsing };
        var path = Path.Combine(Path.GetTempPath(), $"malformed-{Guid.NewGuid():N}.{format}");
        File.WriteAllBytes(path, Content(format));

        try
        {
            // Act
            var act = () => ReadProductNameFromFileAsync(format, path, options);

            // Assert
            var exception = (await act.Should().ThrowAsync<EdsParseException>()).Which;
            exception.Code.Should().Be(ParseDiagnosticCodes.InvalidEncodedBytes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [MemberData(nameof(FormatsAndModes))]
    public void ReadStream_ExplicitLatin1_SameBytes_ReadsEveryByte(string format, bool strictParsing)
    {
        // Arrange
        var options = new CanOpenFileOptions { Encoding = Latin1, StrictParsing = strictParsing };
        using var stream = new MemoryStream(Content(format));

        // Act
        var productName = ReadProductName(format, stream, options);

        // Assert
        productName.Should().EndWith("ÿþ").And.NotContain("�");
    }

    [Theory]
    [InlineData("xdd")]
    [InlineData("xdc")]
    public void ReadStream_XmlAutomaticEncoding_MalformedBytes_ThrowsWithSameCode(string format)
    {
        // Arrange: no option, so the declaration (utf-8) selects the encoding.
        using var stream = new MemoryStream(Content(format));

        // Act
        var act = () => ReadProductName(format, stream, new CanOpenFileOptions());

        // Assert
        act.Should().Throw<EdsParseException>()
            .Which.Code.Should().Be(ParseDiagnosticCodes.InvalidEncodedBytes);
    }

    [Fact]
    public void ReadStreamWithDiagnostics_ExplicitUtf8_MalformedBytes_ThrowsInsteadOfReplacing()
    {
        // Arrange
        var options = new CanOpenFileOptions { Encoding = Encoding.UTF8 };
        using var stream = new MemoryStream(Content("eds"));

        // Act
        var act = () => CanOpenFile.Eds.ReadStreamWithDiagnostics(stream, options);

        // Assert
        act.Should().Throw<EdsParseException>().Which.Code.Should().Be(ParseDiagnosticCodes.InvalidEncodedBytes);
    }

    [Fact]
    public void ReadStream_ExplicitUtf8_ValidBytesWithBom_ReadsAndLeavesCallerEncodingUnchanged()
    {
        // Arrange
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(Encoding.UTF8.GetBytes("[DeviceInfo]\r\nProductName=Gerät\r\n"))
            .ToArray();
        using var stream = new MemoryStream(bytes);

        // Act
        var eds = CanOpenFile.Eds.ReadStream(stream, new CanOpenFileOptions { Encoding = encoding });

        // Assert
        eds.DeviceInfo.ProductName.Should().Be("Gerät");
        encoding.DecoderFallback.Should().BeOfType<DecoderReplacementFallback>("the caller's instance must not be changed");
    }
}
