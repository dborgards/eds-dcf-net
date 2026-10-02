namespace EdsDcfNet.Tests.Integration;

using System.Text;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;

/// <summary>
/// Encoding option for every format: ISO-8859-1 read fallback for INI, and byte
/// output (including the XML declaration) for EDS, DCF, CPJ, XDD, and XDC.
/// </summary>
public class FileEncodingOptionTests
{
    private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly CanOpenWriteOptions Latin1Write = new() { Encoding = Latin1 };

    private const string Geraet = "Gerät";

    private const string Cyrillic = "Привет";

    private const string Cjk = "中";

    [Fact]
    public void ReadFile_Utf8Fixture_IsUnchangedAndDoesNotReportFallback()
    {
        var result = CanOpenFile.Eds.ReadFileWithDiagnostics("Fixtures/sample_device.eds");

        result.Model.DeviceInfo.ProductName.Should().Be("IO-Module 16x16");
        result.Diagnostics.Should().NotContain(d => d.Code == ParseDiagnosticCodes.IniDecodedAsIso88591);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_Latin1Geraet_AllIniFormats_FileAndStream_SyncAndAsync(bool strictParsing)
    {
        var options = new CanOpenFileOptions { StrictParsing = strictParsing };
        var dir = CreateTempDir();
        try
        {
            await AssertIniReadAsync<ElectronicDataSheet>(dir, "geraet.eds", EdsWithProduct(Geraet), options, eds =>
                eds.DeviceInfo.ProductName.Should().Be(Geraet));
            await AssertIniReadAsync<DeviceConfigurationFile>(dir, "geraet.dcf", DcfWithProduct(Geraet), options, dcf =>
                dcf.DeviceInfo.ProductName.Should().Be(Geraet));
            await AssertIniReadAsync<NodelistProject>(dir, "geraet.cpj", CpjWithNet(Geraet), options, cpj =>
                cpj.Networks[0].NetName.Should().Be(Geraet));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Read_Latin1Geraet_ReportsDiagnostic_OnFileAndStream()
    {
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "geraet.eds");
            var bytes = Latin1.GetBytes(EdsWithProduct(Geraet));
            File.WriteAllBytes(path, bytes);

            AssertFallback(CanOpenFile.Eds.ReadFileWithDiagnostics(path));
            AssertFallback(await CanOpenFile.Eds.ReadFileWithDiagnosticsAsync(path));

            using (var stream = new ForwardOnlyStream(bytes))
                AssertFallback(CanOpenFile.Eds.ReadStreamWithDiagnostics(stream));

            using (var stream = new ForwardOnlyStream(bytes))
                AssertFallback(await CanOpenFile.Eds.ReadStreamWithDiagnosticsAsync(stream));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Read_ExplicitLatin1_DecodesLegacyBytesWithoutFallbackDiagnostic()
    {
        var bytes = Latin1.GetBytes(EdsWithProduct(Geraet));
        using var stream = new MemoryStream(bytes);
        var result = CanOpenFile.Eds.ReadStreamWithDiagnostics(
            stream,
            new CanOpenFileOptions { Encoding = Latin1 });

        result.Model.DeviceInfo.ProductName.Should().Be(Geraet);
        result.Diagnostics.Should().NotContain(d => d.Code == ParseDiagnosticCodes.IniDecodedAsIso88591);
    }

    [Fact]
    public void Read_ExplicitStrictUtf8_RejectsLatin1Bytes()
    {
        var bytes = Latin1.GetBytes(EdsWithProduct(Geraet));
        using var stream = new MemoryStream(bytes);

        var act = () => CanOpenFile.Eds.ReadStream(
            stream,
            new CanOpenFileOptions { Encoding = StrictUtf8 });

        act.Should().Throw<DecoderFallbackException>();
    }

    [Fact]
    public void Read_Utf8Geraet_RoundTripsAndDoesNotFallBack()
    {
        var bytes = Encoding.UTF8.GetBytes(EdsWithProduct(Geraet));
        using var stream = new MemoryStream(bytes);
        var result = CanOpenFile.Eds.ReadStreamWithDiagnostics(stream);

        result.Model.DeviceInfo.ProductName.Should().Be(Geraet);
        result.Diagnostics.Should().NotContain(d => d.Code == ParseDiagnosticCodes.IniDecodedAsIso88591);
    }

    [Fact]
    public void Read_ExplicitLatin1_OnUtf8Geraet_DoesNotRepair()
    {
        var bytes = Encoding.UTF8.GetBytes(EdsWithProduct(Geraet));
        using var stream = new MemoryStream(bytes);
        var eds = CanOpenFile.Eds.ReadStream(stream, new CanOpenFileOptions { Encoding = Latin1 });

        eds.DeviceInfo.ProductName.Should().Be(Latin1.GetString(Encoding.UTF8.GetBytes(Geraet)));
        eds.DeviceInfo.ProductName.Should().NotBe(Geraet);
    }

    [Fact]
    public void Read_BomStillSelectsUtf16AndUtf8()
    {
        var text = EdsWithProduct(Geraet);

        using (var utf16 = new MemoryStream(WithPreamble(Encoding.Unicode, Encoding.Unicode.GetBytes(text))))
        {
            CanOpenFile.Eds.ReadStream(utf16).DeviceInfo.ProductName.Should().Be(Geraet);
        }

        using (var utf8 = new MemoryStream(WithUtf8Bom(Encoding.UTF8.GetBytes(text))))
        {
            var result = CanOpenFile.Eds.ReadStreamWithDiagnostics(utf8);
            result.Model.DeviceInfo.ProductName.Should().Be(Geraet);
            result.Diagnostics.Should().NotContain(d => d.Code == ParseDiagnosticCodes.IniDecodedAsIso88591);
        }
    }

    [Fact]
    public async Task Read_Latin1ThenWrite_PreservesGeraet()
    {
        var dir = CreateTempDir();
        try
        {
            var source = Path.Combine(dir, "in.eds");
            var utf8Path = Path.Combine(dir, "utf8.eds");
            var latin1Path = Path.Combine(dir, "latin1.eds");
            File.WriteAllBytes(source, Latin1.GetBytes(EdsWithProduct(Geraet)));

            var eds = CanOpenFile.Eds.ReadFile(source);
            CanOpenFile.Eds.WriteFile(eds, utf8Path);
            CanOpenFile.Eds.WriteFile(eds, latin1Path, Latin1Write);

            CanOpenFile.Eds.ReadFile(utf8Path).DeviceInfo.ProductName.Should().Be(Geraet);
            StrictUtf8.GetString(File.ReadAllBytes(utf8Path)).Should().Contain("ProductName=" + Geraet);

            CanOpenFile.Eds.ReadFile(latin1Path).DeviceInfo.ProductName.Should().Be(Geraet);
            Latin1.GetString(File.ReadAllBytes(latin1Path)).Should().Contain("ProductName=" + Geraet);
            var latin1Act = () => StrictUtf8.GetString(File.ReadAllBytes(latin1Path));
            latin1Act.Should().Throw<DecoderFallbackException>();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Write_AllFormats_FileAndStream_SyncAndAsync_UseLatin1()
    {
        var dir = CreateTempDir();
        try
        {
            var eds = new ElectronicDataSheet { DeviceInfo = { ProductName = Geraet, VendorName = "V" } };
            await AssertLatin1BytesAsync(
                dir,
                "out.eds",
                (path) => CanOpenFile.Eds.WriteFile(eds, path, Latin1Write),
                (path) => CanOpenFile.Eds.WriteFileAsync(eds, path, Latin1Write),
                (stream) => CanOpenFile.Eds.WriteStream(eds, stream, Latin1Write),
                (stream) => CanOpenFile.Eds.WriteStreamAsync(eds, stream, Latin1Write),
                "ProductName=" + Geraet,
                expectXmlDeclaration: false);

            var dcf = new DeviceConfigurationFile { DeviceInfo = { ProductName = Geraet, VendorName = "V" } };
            await AssertLatin1BytesAsync(
                dir,
                "out.dcf",
                (path) => CanOpenFile.Dcf.WriteFile(dcf, path, Latin1Write),
                (path) => CanOpenFile.Dcf.WriteFileAsync(dcf, path, Latin1Write),
                (stream) => CanOpenFile.Dcf.WriteStream(dcf, stream, Latin1Write),
                (stream) => CanOpenFile.Dcf.WriteStreamAsync(dcf, stream, Latin1Write),
                "ProductName=" + Geraet,
                expectXmlDeclaration: false);

            var cpj = new NodelistProject();
            cpj.Networks.Add(new NetworkTopology { NetName = Geraet });
            await AssertLatin1BytesAsync(
                dir,
                "out.cpj",
                (path) => CanOpenFile.Cpj.WriteFile(cpj, path, Latin1Write),
                (path) => CanOpenFile.Cpj.WriteFileAsync(cpj, path, Latin1Write),
                (stream) => CanOpenFile.Cpj.WriteStream(cpj, stream, Latin1Write),
                (stream) => CanOpenFile.Cpj.WriteStreamAsync(cpj, stream, Latin1Write),
                "NetName=" + Geraet,
                expectXmlDeclaration: false);

            var xdd = new ElectronicDataSheet { DeviceInfo = { ProductName = Geraet, VendorName = "V" } };
            await AssertLatin1BytesAsync(
                dir,
                "out.xdd",
                (path) => CanOpenFile.Xdd.WriteFile(xdd, path, Latin1Write),
                (path) => CanOpenFile.Xdd.WriteFileAsync(xdd, path, Latin1Write),
                (stream) => CanOpenFile.Xdd.WriteStream(xdd, stream, Latin1Write),
                (stream) => CanOpenFile.Xdd.WriteStreamAsync(xdd, stream, Latin1Write),
                Geraet,
                expectXmlDeclaration: true);

            var xdc = new DeviceConfigurationFile
            {
                DeviceInfo = { ProductName = Geraet, VendorName = "V" },
                DeviceCommissioning = { NodeId = 1 }
            };
            await AssertLatin1BytesAsync(
                dir,
                "out.xdc",
                (path) => CanOpenFile.Xdc.WriteFile(xdc, path, Latin1Write),
                (path) => CanOpenFile.Xdc.WriteFileAsync(xdc, path, Latin1Write),
                (stream) => CanOpenFile.Xdc.WriteStream(xdc, stream, Latin1Write),
                (stream) => CanOpenFile.Xdc.WriteStreamAsync(xdc, stream, Latin1Write),
                Geraet,
                expectXmlDeclaration: true);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData(Cyrillic, @"\u041F")]
    [InlineData(Cjk, @"\u4E2D")]
    public async Task Write_IniFormats_RejectUnrepresentableCharacters_OnEveryBytePath(string text, string escaped)
    {
        var dir = CreateTempDir();
        try
        {
            var eds = new ElectronicDataSheet { DeviceInfo = { ProductName = text } };
            await AssertIniRejectsAsync(
                dir,
                "bad.eds",
                "kept-eds",
                (path) => CanOpenFile.Eds.WriteFile(eds, path, Latin1Write),
                (path) => CanOpenFile.Eds.WriteFileAsync(eds, path, Latin1Write),
                (stream) => CanOpenFile.Eds.WriteStream(eds, stream, Latin1Write),
                (stream) => CanOpenFile.Eds.WriteStreamAsync(eds, stream, Latin1Write),
                typeof(EdsWriteException),
                escaped);

            var dcf = new DeviceConfigurationFile { DeviceInfo = { ProductName = text } };
            await AssertIniRejectsAsync(
                dir,
                "bad.dcf",
                "kept-dcf",
                (path) => CanOpenFile.Dcf.WriteFile(dcf, path, Latin1Write),
                (path) => CanOpenFile.Dcf.WriteFileAsync(dcf, path, Latin1Write),
                (stream) => CanOpenFile.Dcf.WriteStream(dcf, stream, Latin1Write),
                (stream) => CanOpenFile.Dcf.WriteStreamAsync(dcf, stream, Latin1Write),
                typeof(DcfWriteException),
                escaped);

            var cpj = new NodelistProject();
            cpj.Networks.Add(new NetworkTopology { NetName = text });
            await AssertIniRejectsAsync(
                dir,
                "bad.cpj",
                "kept-cpj",
                (path) => CanOpenFile.Cpj.WriteFile(cpj, path, Latin1Write),
                (path) => CanOpenFile.Cpj.WriteFileAsync(cpj, path, Latin1Write),
                (stream) => CanOpenFile.Cpj.WriteStream(cpj, stream, Latin1Write),
                (stream) => CanOpenFile.Cpj.WriteStreamAsync(cpj, stream, Latin1Write),
                typeof(CpjWriteException),
                escaped);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Write_XmlFormats_UnrepresentableCharacters_RoundTripAsCharacterReferences()
    {
        var dir = CreateTempDir();
        try
        {
            var xdd = new ElectronicDataSheet
            {
                DeviceInfo = { VendorName = "V", ProductName = Cyrillic }
            };
            xdd.ObjectDictionary.Objects[0x2000] = new CanOpenObject
            {
                Index = 0x2000,
                ParameterName = Cjk,
                ObjectType = 0x7,
                DataType = 0x0007,
                AccessType = AccessType.ReadOnly
            };

            await AssertXmlRoundTripAsync(
                dir,
                "round.xdd",
                (path) => CanOpenFile.Xdd.WriteFile(xdd, path, Latin1Write),
                (path) => CanOpenFile.Xdd.WriteFileAsync(xdd, path, Latin1Write),
                (stream) => CanOpenFile.Xdd.WriteStream(xdd, stream, Latin1Write),
                (stream) => CanOpenFile.Xdd.WriteStreamAsync(xdd, stream, Latin1Write),
                bytes =>
                {
                    var read = CanOpenFile.Xdd.ReadStream(new MemoryStream(bytes));
                    read.DeviceInfo.ProductName.Should().Be(Cyrillic);
                    read.ObjectDictionary.Objects[0x2000].ParameterName.Should().Be(Cjk);
                });

            var xdc = new DeviceConfigurationFile
            {
                DeviceInfo = { VendorName = "V", ProductName = Cjk },
                DeviceCommissioning = { NodeId = 1, NodeName = Cyrillic }
            };

            await AssertXmlRoundTripAsync(
                dir,
                "round.xdc",
                (path) => CanOpenFile.Xdc.WriteFile(xdc, path, Latin1Write),
                (path) => CanOpenFile.Xdc.WriteFileAsync(xdc, path, Latin1Write),
                (stream) => CanOpenFile.Xdc.WriteStream(xdc, stream, Latin1Write),
                (stream) => CanOpenFile.Xdc.WriteStreamAsync(xdc, stream, Latin1Write),
                bytes =>
                {
                    var read = CanOpenFile.Xdc.ReadStream(new MemoryStream(bytes));
                    read.DeviceInfo.ProductName.Should().Be(Cjk);
                    read.DeviceCommissioning.NodeName.Should().Be(Cyrillic);
                });
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Write_XddAndXdc_Utf16DeclarationMatchesBytes()
    {
        var utf16 = new CanOpenWriteOptions { Encoding = Encoding.Unicode };
        var xdd = new ElectronicDataSheet { DeviceInfo = { ProductName = Geraet, VendorName = "V" } };
        var xdc = new DeviceConfigurationFile
        {
            DeviceInfo = { ProductName = Geraet, VendorName = "V" },
            DeviceCommissioning = { NodeId = 1 }
        };

        AssertUtf16(stream => CanOpenFile.Xdd.WriteStream(xdd, stream, utf16));
        AssertUtf16(stream => CanOpenFile.Xdc.WriteStream(xdc, stream, utf16));
    }

    [Fact]
    public void WriteToString_IgnoresEncodingOption()
    {
        var xdd = new ElectronicDataSheet { DeviceInfo = { ProductName = Geraet, VendorName = "V" } };
        var text = CanOpenFile.Xdd.WriteToString(xdd, Latin1Write);

        text.Should().Contain("encoding=\"utf-8\"");
        text.Should().Contain(Geraet);
        text.Should().NotContain("encoding=\"iso-8859-1\"");
    }

    [Fact]
    public void ReadStream_ExplicitEncoding_UsesThatEncodingsByteCap()
    {
        const int charLimit = 8;
        var explicitCap = InputBufferLimit.GetMaxBufferedByteCount(charLimit, Latin1);
        var autoCap = InputBufferLimit.GetMaxBufferedByteCount(charLimit, encoding: null);
        explicitCap.Should().BeLessThan(autoCap);

        var stream = new BoundedCountingStream(length: 10_000);
        var act = () => CanOpenFile.Eds.ReadStream(
            stream,
            new CanOpenFileOptions { MaxInputSize = charLimit, Encoding = Latin1 });

        act.Should().Throw<EdsParseException>().WithMessage("*too large*");
        stream.BytesRead.Should().Be((int)explicitCap + 1);
        stream.BytesRead.Should().BeLessThan((int)autoCap);
    }

    [Fact]
    public async Task ReadStreamAsync_ExplicitEncoding_UsesThatEncodingsByteCap()
    {
        const int charLimit = 8;
        var explicitCap = InputBufferLimit.GetMaxBufferedByteCount(charLimit, Latin1);
        var autoCap = InputBufferLimit.GetMaxBufferedByteCount(charLimit, encoding: null);
        var stream = new BoundedCountingStream(length: 10_000);

        var act = () => CanOpenFile.Eds.ReadStreamAsync(
            stream,
            new CanOpenFileOptions { MaxInputSize = charLimit, Encoding = Latin1 });

        await act.Should().ThrowAsync<EdsParseException>().WithMessage("*too large*");
        stream.BytesRead.Should().Be((int)explicitCap + 1);
        stream.BytesRead.Should().BeLessThan((int)autoCap);
    }

    [Fact]
    public void ReadStream_ExplicitUtf8_AcceptsBomPlusMaxWidthCharactersAtCharLimit()
    {
        // Three-byte UTF-8 characters plus a BOM sit on the GetMaxByteCount boundary.
        // The text is not a device file; the parser must accept the size and only then
        // ignore the line. A full EDS read would fail later for a missing section.
        const int count = 5;
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        var bytes = WithUtf8Bom(Encoding.UTF8.GetBytes(new string('\u4F60', count)));
        bytes.Length.Should().Be(encoding.GetMaxByteCount(count) + encoding.GetPreamble().Length);

        using (FileEncodingScope.EnterRead(encoding))
        {
            IniParser.ParseStream(new MemoryStream(bytes), maxInputSize: count).Should().BeEmpty();

            var act = () => IniParser.ParseStream(new MemoryStream(bytes), maxInputSize: count - 1);
            act.Should().Throw<EdsParseException>().WithMessage("*too large*");
        }
    }

    [Fact]
    public void FileEncodingScope_Dispose_RestoresPreviousAndIsIdempotent()
    {
        FileEncodingScope.CurrentRead.Should().BeNull();
        var outer = FileEncodingScope.EnterRead(Latin1);
        using (FileEncodingScope.EnterRead(Encoding.UTF8))
            FileEncodingScope.CurrentRead.Should().BeSameAs(Encoding.UTF8);

        FileEncodingScope.CurrentRead.Should().BeSameAs(Latin1);
        outer.Dispose();
        outer.Dispose();
        FileEncodingScope.CurrentRead.Should().BeNull();

        var write = FileEncodingScope.EnterWrite(Latin1);
        FileEncodingScope.CurrentWrite.Should().BeSameAs(Latin1);
        write.Dispose();
        write.Dispose();
        FileEncodingScope.CurrentWrite.Should().BeNull();
    }

    private static async Task AssertIniReadAsync<T>(
        string dir,
        string fileName,
        string text,
        CanOpenFileOptions options,
        Action<T> assert)
    {
        var bytes = Latin1.GetBytes(text);
        var path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, bytes);

        if (typeof(T) == typeof(ElectronicDataSheet))
        {
            assert((T)(object)CanOpenFile.Eds.ReadFile(path, options));
            assert((T)(object)await CanOpenFile.Eds.ReadFileAsync(path, options));
            using (var stream = new ForwardOnlyStream(bytes))
                assert((T)(object)CanOpenFile.Eds.ReadStream(stream, options));
            using (var stream = new ForwardOnlyStream(bytes))
                assert((T)(object)await CanOpenFile.Eds.ReadStreamAsync(stream, options));
            return;
        }

        if (typeof(T) == typeof(DeviceConfigurationFile))
        {
            assert((T)(object)CanOpenFile.Dcf.ReadFile(path, options));
            assert((T)(object)await CanOpenFile.Dcf.ReadFileAsync(path, options));
            using (var stream = new ForwardOnlyStream(bytes))
                assert((T)(object)CanOpenFile.Dcf.ReadStream(stream, options));
            using (var stream = new ForwardOnlyStream(bytes))
                assert((T)(object)await CanOpenFile.Dcf.ReadStreamAsync(stream, options));
            return;
        }

        assert((T)(object)CanOpenFile.Cpj.ReadFile(path, options));
        assert((T)(object)await CanOpenFile.Cpj.ReadFileAsync(path, options));
        using (var stream = new ForwardOnlyStream(bytes))
            assert((T)(object)CanOpenFile.Cpj.ReadStream(stream, options));
        using (var stream = new ForwardOnlyStream(bytes))
            assert((T)(object)await CanOpenFile.Cpj.ReadStreamAsync(stream, options));
    }

    private static void AssertFallback(CanOpenReadResult<ElectronicDataSheet> result)
    {
        result.Model.DeviceInfo.ProductName.Should().Be(Geraet);
        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == ParseDiagnosticCodes.IniDecodedAsIso88591 &&
            d.Message == IniTextDecoder.Iso88591FallbackMessage);
    }

    private static async Task AssertLatin1BytesAsync(
        string dir,
        string fileName,
        Action<string> writeFile,
        Func<string, Task> writeFileAsync,
        Action<Stream> writeStream,
        Func<Stream, Task> writeStreamAsync,
        string marker,
        bool expectXmlDeclaration)
    {
        var syncPath = Path.Combine(dir, "sync-" + fileName);
        var asyncPath = Path.Combine(dir, "async-" + fileName);
        writeFile(syncPath);
        await writeFileAsync(asyncPath);
        AssertLatin1Payload(File.ReadAllBytes(syncPath), marker, expectXmlDeclaration);
        AssertLatin1Payload(File.ReadAllBytes(asyncPath), marker, expectXmlDeclaration);

        using (var stream = new MemoryStream())
        {
            writeStream(stream);
            AssertLatin1Payload(stream.ToArray(), marker, expectXmlDeclaration);
        }

        using (var stream = new MemoryStream())
        {
            await writeStreamAsync(stream);
            AssertLatin1Payload(stream.ToArray(), marker, expectXmlDeclaration);
        }
    }

    private static void AssertLatin1Payload(byte[] bytes, string marker, bool expectXmlDeclaration)
    {
        var text = Latin1.GetString(bytes);
        text.Should().Contain(marker);
        if (expectXmlDeclaration)
            text.Should().Contain("encoding=\"iso-8859-1\"");

        var act = () => StrictUtf8.GetString(bytes);
        act.Should().Throw<DecoderFallbackException>();
    }

    private static async Task AssertIniRejectsAsync(
        string dir,
        string fileName,
        string preserved,
        Action<string> writeFile,
        Func<string, Task> writeFileAsync,
        Action<Stream> writeStream,
        Func<Stream, Task> writeStreamAsync,
        Type exceptionType,
        string escapedCharacter)
    {
        var syncPath = Path.Combine(dir, "sync-" + fileName);
        var asyncPath = Path.Combine(dir, "async-" + fileName);
        File.WriteAllText(syncPath, preserved);
        File.WriteAllText(asyncPath, preserved);

        var syncAct = () => writeFile(syncPath);
        syncAct.Should().Throw<Exception>().Which.Should().BeAssignableTo(exceptionType);
        File.ReadAllText(syncPath).Should().Be(preserved);
        AssertRejectionMessage(syncAct, escapedCharacter);
        Directory.EnumerateFiles(dir, "*.tmp").Should().BeEmpty();
        Directory.EnumerateFiles(dir).Should().NotContain(path => Path.GetFileName(path).Contains("edsdcf"));

        Func<Task> asyncAct = () => writeFileAsync(asyncPath);
        (await asyncAct.Should().ThrowAsync<Exception>()).Which.Should().BeAssignableTo(exceptionType);
        File.ReadAllText(asyncPath).Should().Be(preserved);
        (await asyncAct.Should().ThrowAsync<Exception>()).Which.ToString().Should().Contain(escapedCharacter).And.Contain("index");

        using (var stream = new MemoryStream())
        {
            var act = () => writeStream(stream);
            act.Should().Throw<Exception>().Which.Should().BeAssignableTo(exceptionType);
            act.Should().Throw<Exception>().Which.ToString().Should().Contain(escapedCharacter).And.Contain("index");
        }

        using (var stream = new MemoryStream())
        {
            Func<Task> act = () => writeStreamAsync(stream);
            (await act.Should().ThrowAsync<Exception>()).Which.Should().BeAssignableTo(exceptionType);
            (await act.Should().ThrowAsync<Exception>()).Which.ToString().Should().Contain(escapedCharacter).And.Contain("index");
        }
    }

    private static void AssertRejectionMessage(Action act, string escapedCharacter)
    {
        act.Should().Throw<Exception>().Which.ToString().Should().Contain(escapedCharacter).And.Contain("index");
    }

    private static async Task AssertXmlRoundTripAsync(
        string dir,
        string fileName,
        Action<string> writeFile,
        Func<string, Task> writeFileAsync,
        Action<Stream> writeStream,
        Func<Stream, Task> writeStreamAsync,
        Action<byte[]> assertRoundTrip)
    {
        var syncPath = Path.Combine(dir, "sync-" + fileName);
        var asyncPath = Path.Combine(dir, "async-" + fileName);
        writeFile(syncPath);
        await writeFileAsync(asyncPath);
        AssertXmlReferences(File.ReadAllBytes(syncPath), assertRoundTrip);
        AssertXmlReferences(File.ReadAllBytes(asyncPath), assertRoundTrip);

        using (var stream = new MemoryStream())
        {
            writeStream(stream);
            AssertXmlReferences(stream.ToArray(), assertRoundTrip);
        }

        using (var stream = new MemoryStream())
        {
            await writeStreamAsync(stream);
            AssertXmlReferences(stream.ToArray(), assertRoundTrip);
        }
    }

    private static void AssertXmlReferences(byte[] bytes, Action<byte[]> assertRoundTrip)
    {
        var text = Latin1.GetString(bytes);
        text.Should().Contain("encoding=\"iso-8859-1\"");
        text.Should().Contain("&#x41F;").And.Contain("&#x4E2D;");
        StrictUtf8.GetString(bytes).Should().NotContain(Cyrillic);
        assertRoundTrip(bytes);
    }

    private static void AssertUtf16(Action<Stream> write)
    {
        using var stream = new MemoryStream();
        write(stream);
        var bytes = stream.ToArray();
        bytes.Length.Should().BeGreaterThan(2);
        bytes[0].Should().Be(0xFF);
        bytes[1].Should().Be(0xFE);
        Encoding.Unicode.GetString(bytes).Should().Contain("encoding=\"utf-16\"");
    }

    private static string EdsWithProduct(string productName)
        => "[FileInfo]\nFileName=geraet.eds\nFileVersion=1\nFileRevision=0\nEDSVersion=4.0\n"
           + "[DeviceInfo]\nVendorName=Beispiel\nProductName=" + productName
           + "\nVendorNumber=1\nProductNumber=1\n";

    private static string DcfWithProduct(string productName)
        => "[FileInfo]\nFileName=geraet.dcf\nFileVersion=1\nFileRevision=0\n"
           + "[DeviceInfo]\nVendorName=Beispiel\nProductName=" + productName + "\n"
           + "[DeviceCommissioning]\nNodeID=1\n";

    private static string CpjWithNet(string netName)
        => "[Topology]\nNetName=" + netName + "\nNodes=0\n";

    private static byte[] WithPreamble(Encoding encoding, byte[] payload)
    {
        var preamble = encoding.GetPreamble();
        var bytes = new byte[preamble.Length + payload.Length];
        preamble.CopyTo(bytes, 0);
        payload.CopyTo(bytes, preamble.Length);
        return bytes;
    }

    private static byte[] WithUtf8Bom(byte[] payload)
    {
        var bytes = new byte[payload.Length + 3];
        bytes[0] = 0xEF;
        bytes[1] = 0xBB;
        bytes[2] = 0xBF;
        payload.CopyTo(bytes, 3);
        return bytes;
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "eds-enc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private sealed class ForwardOnlyStream : Stream
    {
        private readonly Stream _inner;

        internal ForwardOnlyStream(byte[] bytes) => _inner = new MemoryStream(bytes);

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
            => _inner.ReadAsync(buffer, offset, count, cancellationToken);

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

    private sealed class BoundedCountingStream : Stream
    {
        private readonly int _length;

        internal BoundedCountingStream(int length) => _length = length;

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
                buffer[offset + i] = 0xE4;

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
