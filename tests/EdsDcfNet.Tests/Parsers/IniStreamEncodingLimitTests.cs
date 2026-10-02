namespace EdsDcfNet.Tests.Parsers;

using System.Text;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Parsers;

/// <summary>
/// Stream reads limit decoded characters, with a raw-byte ceiling large enough for
/// multibyte encodings and a leading byte-order mark.
/// </summary>
public class IniStreamEncodingLimitTests
{
    [Fact]
    public void GetMaxBufferedByteCount_AutoAndExplicit_SaturatesAndAddsPreamble()
    {
        InputBufferLimit.GetMaxBufferedByteCount(0, null).Should().Be(0);
        InputBufferLimit.GetMaxBufferedByteCount(-1, null).Should().Be(0);
        InputBufferLimit.GetMaxBufferedByteCount(10, null).Should().Be(10 * 4 + 4);
        InputBufferLimit.GetMaxBufferedByteCount(long.MaxValue, null).Should().Be(long.MaxValue);

        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        InputBufferLimit.GetMaxBufferedByteCount(5, utf8)
            .Should().Be(utf8.GetMaxByteCount(5) + utf8.GetPreamble().Length);

        var utf16 = Encoding.Unicode;
        InputBufferLimit.GetMaxBufferedByteCount(5, utf16)
            .Should().Be(utf16.GetMaxByteCount(5) + utf16.GetPreamble().Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParseStream_Utf8NonAsciiAtCharLimit_AcceptsAndRejectsOneBelow(bool withBom)
    {
        const int count = 32;
        var bytes = Utf8Bytes(new string('ä', count), withBom);

        var accepted = IniParser.ParseStream(new MemoryStream(bytes), maxInputSize: count);
        accepted.Should().BeEmpty("a line of non-ASCII text is ignored, but it is within the character limit");

        var act = () => IniParser.ParseStream(new MemoryStream(bytes), maxInputSize: count - 1);
        act.Should().Throw<EdsParseException>().WithMessage("*too large*");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParseStreamAsync_Utf8NonAsciiAtCharLimit_AcceptsAndRejectsOneBelow(bool withBom)
    {
        const int count = 32;
        var bytes = Utf8Bytes(new string('ä', count), withBom);

        var accepted = await IniParser.ParseStreamAsync(new MemoryStream(bytes), maxInputSize: count);
        accepted.Should().BeEmpty();

        var act = () => IniParser.ParseStreamAsync(new MemoryStream(bytes), maxInputSize: count - 1);
        await act.Should().ThrowAsync<EdsParseException>().WithMessage("*too large*");
    }

    [Fact]
    public void ParseStream_OverlongStream_StopsBeforeBufferingTheRemainder()
    {
        const int charLimit = 32;
        var cap = InputBufferLimit.GetMaxBufferedByteCount(charLimit, encoding: null);
        var stream = new CountingStream(length: 1_000_000);

        var act = () => IniParser.ParseStream(stream, maxInputSize: charLimit);

        act.Should().Throw<EdsParseException>().WithMessage("*too large*");
        stream.BytesRead.Should().Be((int)cap + 1);
        stream.BytesRead.Should().BeLessThan(stream.LengthBudget);
    }

    [Fact]
    public async Task ParseStreamAsync_OverlongStream_StopsBeforeBufferingTheRemainder()
    {
        const int charLimit = 32;
        var cap = InputBufferLimit.GetMaxBufferedByteCount(charLimit, encoding: null);
        var stream = new CountingStream(length: 1_000_000);

        var act = () => IniParser.ParseStreamAsync(stream, maxInputSize: charLimit);

        await act.Should().ThrowAsync<EdsParseException>().WithMessage("*too large*");
        stream.BytesRead.Should().Be((int)cap + 1);
        stream.BytesRead.Should().BeLessThan(stream.LengthBudget);
    }

    private static byte[] Utf8Bytes(string text, bool withBom)
    {
        var payload = Encoding.UTF8.GetBytes(text);
        if (!withBom)
            return payload;

        var bytes = new byte[payload.Length + 3];
        bytes[0] = 0xEF;
        bytes[1] = 0xBB;
        bytes[2] = 0xBF;
        payload.CopyTo(bytes, 3);
        return bytes;
    }

    /// <summary>Non-seekable stream that yields ASCII bytes up to a large budget.</summary>
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
