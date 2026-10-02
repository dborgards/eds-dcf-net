namespace EdsDcfNet.Tests.Parsers;

using System.Text;
using System.Xml;
using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;

/// <summary>
/// XDD/XDC byte reads follow the BOM and the XML declaration.
/// File reads stay byte-capped. Stream reads stay a decoded-character limit,
/// with a raw-byte ceiling wide enough for a preamble.
/// </summary>
public class XmlDeclaredEncodingTests
{
    private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private const string Geraet = "Gerät";

    [Fact]
    public async Task Read_Iso88591Geraet_XddAndXdc_FileStreamAndForwardOnly_SyncAndAsync()
    {
        await AssertDeclaredReadAsync(xdc: false, "ISO-8859-1", Latin1, bom: false, Geraet);
        await AssertDeclaredReadAsync(xdc: true, "ISO-8859-1", Latin1, bom: false, Geraet);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Read_Utf8Geraet_WithAndWithoutBom_PreservesText(bool xdc, bool bom)
    {
        var text = Written(xdc, Geraet);
        var bytes = Encode(text, StrictUtf8, bom);
        bytes.Length.Should().BeGreaterThan(text.Length);
        await AssertReadsAsync(xdc, bytes, Geraet);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_Utf16Bom_PreservesTextEvenWhenDeclarationSaysUtf8(bool xdc)
    {
        // Writers that transcode a UTF-8 declaration with Encoding.Unicode keep the
        // declaration and add a BOM. The mark selects UTF-16.
        var text = Written(xdc, Geraet);
        text.Should().Contain("encoding=\"utf-8\"");
        var bytes = Encode(text, Encoding.Unicode, bom: true);
        bytes[0].Should().Be(0xFF);
        bytes[1].Should().Be(0xFE);
        await AssertReadsAsync(xdc, bytes, Geraet);
    }

    [Fact]
    public void ReadString_AlreadyDecodedText_IgnoresDeclaration()
    {
        var text = WithDeclaration(Written(xdc: false, Geraet), "ISO-8859-1");

        CanOpenFile.Xdd.ReadString(text).DeviceInfo.ProductName.Should().Be(Geraet);
    }

    [Fact]
    public void Read_ExplicitEncoding_OverridesDeclaration()
    {
        var utf8DeclaredAsLatin1 = Encode(WithDeclaration(Written(xdc: false, Geraet), "ISO-8859-1"), StrictUtf8, bom: false);
        var auto = CanOpenFile.Xdd.ReadStream(new MemoryStream(utf8DeclaredAsLatin1));
        auto.DeviceInfo.ProductName.Should().NotBe(Geraet);

        var forcedUtf8 = CanOpenFile.Xdd.ReadStream(
            new MemoryStream(utf8DeclaredAsLatin1),
            new CanOpenFileOptions { Encoding = StrictUtf8 });
        forcedUtf8.DeviceInfo.ProductName.Should().Be(Geraet);

        var latin1DeclaredAsUtf8 = Latin1.GetBytes(Written(xdc: false, Geraet));
        var rejected = () => CanOpenFile.Xdd.ReadStream(new MemoryStream(latin1DeclaredAsUtf8));
        rejected.Should().Throw<EdsParseException>().WithMessage("*utf-8*");

        var forcedLatin1 = CanOpenFile.Xdd.ReadStream(
            new MemoryStream(latin1DeclaredAsUtf8),
            new CanOpenFileOptions { Encoding = Latin1 });
        forcedLatin1.DeviceInfo.ProductName.Should().Be(Geraet);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Read_DeclaredWindows1252_NamesEncodingWhenRuntimeLacksIt(bool xdc)
    {
        var bytes = Encoding.ASCII.GetBytes(WithDeclaration(Written(xdc, "Plain"), "windows-1252"));
        var supported = TryGetEncoding("windows-1252", out var encoding);

        if (!supported)
        {
            var act = () => ReadStream(xdc, bytes, options: null);
            act.Should().Throw<EdsParseException>().WithMessage("*windows-1252*");
            return;
        }

        ReadStream(xdc, bytes, options: null);
        var product = ReadProduct(xdc, bytes, options: null);
        product.Should().Be("Plain");
        encoding.Should().NotBeNull();
    }

    [Theory]
    [InlineData(false, "utf-8", false)]
    [InlineData(false, "utf-8", true)]
    [InlineData(false, "utf-16", true)]
    [InlineData(true, "utf-8", false)]
    [InlineData(true, "utf-8", true)]
    [InlineData(true, "utf-16", true)]
    public async Task ReadStream_DocumentAtCharLimit_AcceptsAndRejectsOneBelow(bool xdc, string encodingName, bool bom)
    {
        var text = Written(xdc, Geraet);
        text.Should().Contain(Geraet);
        var encoding = encodingName == "utf-16" ? Encoding.Unicode : StrictUtf8;
        var bytes = Encode(text, encoding, bom);
        if (encodingName == "utf-8")
            bytes.Length.Should().BeGreaterThan(text.Length, "non-ASCII text must occupy more than one byte per character");

        await AssertLimitAsync(xdc, bytes, text.Length);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ReadStream_ExactlyNNonAsciiCharacters_AcceptsAndRejectsOneBelow(bool utf16, bool bom)
    {
        const int count = 32;
        var text = new string('ä', count);
        byte[] bytes;
        if (utf16)
            bytes = Encode(text, Encoding.Unicode, bom: true);
        else
            bytes = Encode(text, StrictUtf8, bom);

        bytes.Length.Should().BeGreaterThan(count);
        await AssertSizeOnlyAsync(bytes, count);
    }

    [Fact]
    public void ReadStream_Utf32BomAtRawByteCeiling_AcceptsAndRejectsOneBelow()
    {
        const int count = 8;
        var text = new string('ä', count);
        var bytes = Encode(text, Encoding.UTF32, bom: true);
        var ceiling = InputBufferLimit.GetMaxBufferedByteCount(count, encoding: null);
        bytes.Length.Should().Be((int)ceiling, "UTF-32 with a BOM is the widest buffered form");

        var accepted = () => new XddReader().ReadStream(new MemoryStream(bytes), count);
        accepted.Should().Throw<EdsParseException>().WithMessage("*Failed to parse*").Which.Message.Should().NotContain("too large");

        var rejected = () => new XddReader().ReadStream(new MemoryStream(bytes), count - 1);
        rejected.Should().Throw<EdsParseException>().WithMessage("*too large*");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadFile_Utf16_IsByteCappedWhileStreamIsCharacterCapped(bool xdc)
    {
        var text = Written(xdc, Geraet);
        var bytes = Encode(text, Encoding.Unicode, bom: true);
        bytes.Length.Should().BeGreaterThan(text.Length);

        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, xdc ? "device.xdc" : "device.xdd");
            File.WriteAllBytes(path, bytes);
            var byteOptions = new CanOpenFileOptions { MaxInputSize = text.Length };
            var act = () => ReadFile(xdc, path, byteOptions);
            act.Should().Throw<EdsParseException>().WithMessage("*bytes*");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }

        var charOptions = new CanOpenFileOptions { MaxInputSize = text.Length };
        ReadProduct(xdc, bytes, charOptions).Should().Be(Geraet);
    }

    [Fact]
    public void ReadStream_ExplicitUtf16_UsesThatEncodingsByteCeiling()
    {
        const int count = 32;
        var bytes = Encode(new string('ä', count), Encoding.Unicode, bom: true);
        var accepted = () => CanOpenFile.Xdd.ReadStream(
            new MemoryStream(bytes),
            new CanOpenFileOptions { Encoding = Encoding.Unicode, MaxInputSize = count });
        accepted.Should().Throw<EdsParseException>().WithMessage("*Failed to parse*").Which.Message.Should().NotContain("too large");

        var rejected = () => CanOpenFile.Xdd.ReadStream(
            new MemoryStream(bytes),
            new CanOpenFileOptions { Encoding = Encoding.Unicode, MaxInputSize = count - 1 });
        rejected.Should().Throw<EdsParseException>().WithMessage("*too large*");
    }

    [Fact]
    public void ReadStream_OverlongInput_StopsBeforeBufferingTheRemainder()
    {
        const int charLimit = 32;
        var cap = InputBufferLimit.GetMaxBufferedByteCount(charLimit, encoding: null);
        var stream = new CountingStream(length: 1_000_000);

        var act = () => CanOpenFile.Xdd.ReadStream(stream, new CanOpenFileOptions { MaxInputSize = charLimit });

        act.Should().Throw<EdsParseException>().WithMessage("*too large*");
        stream.BytesRead.Should().Be((int)cap + 1);
        stream.BytesRead.Should().BeLessThan(stream.LengthBudget);
    }

    [Fact]
    public async Task ReadStreamAsync_OverlongInput_StopsBeforeBufferingTheRemainder()
    {
        const int charLimit = 32;
        var cap = InputBufferLimit.GetMaxBufferedByteCount(charLimit, encoding: null);
        var stream = new CountingStream(length: 1_000_000);

        var act = () => CanOpenFile.Xdd.ReadStreamAsync(stream, new CanOpenFileOptions { MaxInputSize = charLimit });

        await act.Should().ThrowAsync<EdsParseException>().WithMessage("*too large*");
        stream.BytesRead.Should().Be((int)cap + 1);
        stream.BytesRead.Should().BeLessThan(stream.LengthBudget);
    }

    [Fact]
    public void ReadStream_Doctype_IsStillProhibited()
    {
        const string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><!DOCTYPE r [<!ENTITY x \"y\">]><r/>";
        var act = () => new XddReader().ReadStream(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

        act.Should().Throw<EdsParseException>()
            .Where(ex => ex.InnerException is XmlException && ex.InnerException.Message.Contains("DTD", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadStream_NestedPastDepthLimit_IsRejected()
    {
        var xml = new StringBuilder();
        xml.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        for (var i = 0; i < 70; i++)
            xml.Append("<a>");
        for (var i = 0; i < 70; i++)
            xml.Append("</a>");

        var act = () => new XddReader().ReadStream(new MemoryStream(Encoding.UTF8.GetBytes(xml.ToString())));

        act.Should().Throw<EdsParseException>().WithMessage("*maximum XML nesting depth of 64*");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RoundTrip_Iso88591AndUtf16_PreservesGeraet(bool utf16)
    {
        var encoding = utf16 ? Encoding.Unicode : Latin1;
        var writeOptions = new CanOpenWriteOptions { Encoding = encoding };
        await RoundTripAsync(xdc: false, writeOptions, utf16);
        await RoundTripAsync(xdc: true, writeOptions, utf16);
    }

    private static async Task AssertDeclaredReadAsync(bool xdc, string declaredName, Encoding encoding, bool bom, string expected)
    {
        var text = WithDeclaration(Written(xdc, expected), declaredName);
        var bytes = Encode(text, encoding, bom);
        var roundTrip = encoding.GetString(bytes);
        roundTrip.Should().Contain(expected);
        var strict = () => StrictUtf8.GetString(bytes);
        strict.Should().Throw<DecoderFallbackException>();
        await AssertReadsAsync(xdc, bytes, expected);
    }

    private static async Task AssertReadsAsync(bool xdc, byte[] bytes, string expected)
    {
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, xdc ? "device.xdc" : "device.xdd");
            File.WriteAllBytes(path, bytes);

            ReadProductFile(xdc, path, options: null).Should().Be(expected);
            (await ReadProductFileAsync(xdc, path)).Should().Be(expected);

            ReadProduct(xdc, bytes, options: null).Should().Be(expected);
            (await ReadProductAsync(xdc, bytes)).Should().Be(expected);

            using var forward = new ForwardOnlyStream(bytes);
            ReadProductStream(xdc, forward, options: null).Should().Be(expected);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static async Task AssertLimitAsync(bool xdc, byte[] bytes, int charLimit)
    {
        ReadProduct(xdc, bytes, new CanOpenFileOptions { MaxInputSize = charLimit }).Should().Be(Geraet);
        (await ReadProductAsync(xdc, bytes, new CanOpenFileOptions { MaxInputSize = charLimit })).Should().Be(Geraet);

        var syncRejected = () => ReadProduct(xdc, bytes, new CanOpenFileOptions { MaxInputSize = charLimit - 1 });
        syncRejected.Should().Throw<EdsParseException>().WithMessage("*too large*");

        var asyncRejected = () => ReadProductAsync(xdc, bytes, new CanOpenFileOptions { MaxInputSize = charLimit - 1 });
        await asyncRejected.Should().ThrowAsync<EdsParseException>().WithMessage("*too large*");
    }

    private static async Task AssertSizeOnlyAsync(byte[] bytes, int charLimit)
    {
        var accepted = () => new XddReader().ReadStream(new MemoryStream(bytes), charLimit);
        accepted.Should().Throw<EdsParseException>().WithMessage("*Failed to parse*").Which.Message.Should().NotContain("too large");

        var acceptedAsync = () => new XddReader().ReadStreamAsync(new MemoryStream(bytes), charLimit);
        (await acceptedAsync.Should().ThrowAsync<EdsParseException>()).Which.Message.Should().NotContain("too large");

        var rejected = () => new XddReader().ReadStream(new MemoryStream(bytes), charLimit - 1);
        rejected.Should().Throw<EdsParseException>().WithMessage("*too large*");

        var rejectedAsync = () => new XddReader().ReadStreamAsync(new MemoryStream(bytes), charLimit - 1);
        await rejectedAsync.Should().ThrowAsync<EdsParseException>().WithMessage("*too large*");
    }

    private static async Task RoundTripAsync(bool xdc, CanOpenWriteOptions writeOptions, bool utf16)
    {
        var dir = CreateTempDir();
        try
        {
            if (!xdc)
            {
                var model = new ElectronicDataSheet { DeviceInfo = { ProductName = Geraet, VendorName = "V" } };
                await WriteAndReadAsync(
                    dir,
                    "device.xdd",
                    path => CanOpenFile.Xdd.WriteFile(model, path, writeOptions),
                    path => CanOpenFile.Xdd.WriteFileAsync(model, path, writeOptions),
                    stream => CanOpenFile.Xdd.WriteStream(model, stream, writeOptions),
                    stream => CanOpenFile.Xdd.WriteStreamAsync(model, stream, writeOptions),
                    bytes => CanOpenFile.Xdd.ReadStream(new MemoryStream(bytes)).DeviceInfo.ProductName.Should().Be(Geraet),
                    utf16);
            }
            else
            {
                var model = new DeviceConfigurationFile
                {
                    DeviceInfo = { ProductName = Geraet, VendorName = "V" },
                    DeviceCommissioning = { NodeId = 1, NodeName = Geraet }
                };
                await WriteAndReadAsync(
                    dir,
                    "device.xdc",
                    path => CanOpenFile.Xdc.WriteFile(model, path, writeOptions),
                    path => CanOpenFile.Xdc.WriteFileAsync(model, path, writeOptions),
                    stream => CanOpenFile.Xdc.WriteStream(model, stream, writeOptions),
                    stream => CanOpenFile.Xdc.WriteStreamAsync(model, stream, writeOptions),
                    bytes =>
                    {
                        var read = CanOpenFile.Xdc.ReadStream(new MemoryStream(bytes));
                        read.DeviceInfo.ProductName.Should().Be(Geraet);
                        read.DeviceCommissioning.NodeName.Should().Be(Geraet);
                    },
                    utf16);
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static async Task WriteAndReadAsync(
        string dir,
        string fileName,
        Action<string> writeFile,
        Func<string, Task> writeFileAsync,
        Action<Stream> writeStream,
        Func<Stream, Task> writeStreamAsync,
        Action<byte[]> assertRoundTrip,
        bool utf16)
    {
        var syncPath = Path.Combine(dir, "sync-" + fileName);
        var asyncPath = Path.Combine(dir, "async-" + fileName);
        writeFile(syncPath);
        await writeFileAsync(asyncPath);
        AssertWrittenBytes(File.ReadAllBytes(syncPath), assertRoundTrip, utf16);
        AssertWrittenBytes(File.ReadAllBytes(asyncPath), assertRoundTrip, utf16);

        using (var stream = new MemoryStream())
        {
            writeStream(stream);
            AssertWrittenBytes(stream.ToArray(), assertRoundTrip, utf16);
        }

        using (var stream = new MemoryStream())
        {
            await writeStreamAsync(stream);
            AssertWrittenBytes(stream.ToArray(), assertRoundTrip, utf16);
        }
    }

    private static void AssertWrittenBytes(byte[] bytes, Action<byte[]> assertRoundTrip, bool utf16)
    {
        if (utf16)
        {
            bytes[0].Should().Be(0xFF);
            bytes[1].Should().Be(0xFE);
            Encoding.Unicode.GetString(bytes).Should().Contain("encoding=\"utf-16\"");
        }
        else
        {
            Latin1.GetString(bytes).Should().Contain("encoding=\"iso-8859-1\"");
            var strict = () => StrictUtf8.GetString(bytes);
            strict.Should().Throw<DecoderFallbackException>();
        }

        assertRoundTrip(bytes);
    }

    private static string Written(bool xdc, string product)
    {
        if (!xdc)
        {
            var xdd = new ElectronicDataSheet { DeviceInfo = { ProductName = product, VendorName = "V" } };
            return CanOpenFile.Xdd.WriteToString(xdd);
        }

        var file = new DeviceConfigurationFile
        {
            DeviceInfo = { ProductName = product, VendorName = "V" },
            DeviceCommissioning = { NodeId = 1, NodeName = product }
        };
        return CanOpenFile.Xdc.WriteToString(file);
    }

    private static string WithDeclaration(string text, string encodingName)
    {
        const string marker = "encoding=\"utf-8\"";
        text.Should().Contain(marker);
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        return text.Substring(0, index) + "encoding=\"" + encodingName + "\"" + text.Substring(index + marker.Length);
    }

    private static byte[] Encode(string text, Encoding encoding, bool bom)
    {
        var payload = encoding.GetBytes(text);
        if (!bom)
            return payload;

        var preamble = encoding.GetPreamble();
        if (preamble.Length == 0 && encoding.WebName == "utf-8")
            preamble = new byte[] { 0xEF, 0xBB, 0xBF };

        var bytes = new byte[preamble.Length + payload.Length];
        preamble.CopyTo(bytes, 0);
        payload.CopyTo(bytes, preamble.Length);
        return bytes;
    }

    private static string ReadProduct(bool xdc, byte[] bytes, CanOpenFileOptions? options)
    {
        using var stream = new MemoryStream(bytes);
        return ReadProductStream(xdc, stream, options);
    }

    private static async Task<string> ReadProductAsync(bool xdc, byte[] bytes, CanOpenFileOptions? options = null)
    {
        using var stream = new MemoryStream(bytes);
        if (xdc)
            return (await CanOpenFile.Xdc.ReadStreamAsync(stream, options)).DeviceInfo.ProductName;

        return (await CanOpenFile.Xdd.ReadStreamAsync(stream, options)).DeviceInfo.ProductName;
    }

    private static string ReadProductStream(bool xdc, Stream stream, CanOpenFileOptions? options)
    {
        if (xdc)
            return CanOpenFile.Xdc.ReadStream(stream, options).DeviceInfo.ProductName;

        return CanOpenFile.Xdd.ReadStream(stream, options).DeviceInfo.ProductName;
    }

    private static string ReadProductFile(bool xdc, string path, CanOpenFileOptions? options)
    {
        if (xdc)
            return CanOpenFile.Xdc.ReadFile(path, options).DeviceInfo.ProductName;

        return CanOpenFile.Xdd.ReadFile(path, options).DeviceInfo.ProductName;
    }

    private static async Task<string> ReadProductFileAsync(bool xdc, string path)
    {
        if (xdc)
            return (await CanOpenFile.Xdc.ReadFileAsync(path)).DeviceInfo.ProductName;

        return (await CanOpenFile.Xdd.ReadFileAsync(path)).DeviceInfo.ProductName;
    }

    private static object ReadFile(bool xdc, string path, CanOpenFileOptions options)
    {
        if (xdc)
            return CanOpenFile.Xdc.ReadFile(path, options);

        return CanOpenFile.Xdd.ReadFile(path, options);
    }

    private static object ReadStream(bool xdc, byte[] bytes, CanOpenFileOptions? options)
    {
        using var stream = new MemoryStream(bytes);
        if (xdc)
            return CanOpenFile.Xdc.ReadStream(stream, options);

        return CanOpenFile.Xdd.ReadStream(stream, options);
    }

    private static bool TryGetEncoding(string name, out Encoding? encoding)
    {
        try
        {
            encoding = Encoding.GetEncoding(name);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            encoding = null;
            return false;
        }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "eds-xml-enc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private sealed class ForwardOnlyStream : Stream
    {
        private readonly MemoryStream _inner;

        public ForwardOnlyStream(byte[] data) => _inner = new MemoryStream(data);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read(buffer, offset, count));
        }

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

    private sealed class CountingStream : Stream
    {
        private readonly int _length;

        internal CountingStream(int length) => _length = length;

        internal int LengthBudget => _length;

        internal int BytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => BytesRead;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = Math.Min(count, _length - BytesRead);
            if (n <= 0)
                return 0;

            for (var i = 0; i < n; i++)
                buffer[offset + i] = (byte)'A';

            BytesRead += n;
            return n;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read(buffer, offset, count));
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
