using System.Buffers.Binary;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class IndexedGraphPayloadTests
{
    [Theory]
    [InlineData("small")]
    [InlineData("\u0000\uE000\U00010000\"\\")]
    public void Raw_frames_preserve_exact_unicode_and_escaping(string value)
    {
        var frame = IndexedGraphPayload.Encode(value);
        Assert.Equal(0, frame[4]);
        Assert.Equal(value, IndexedGraphPayload.Decode<string>(frame));
        Assert.Equal(frame, IndexedGraphPayload.Encode(value));
    }

    [Fact]
    public void Large_frames_preserve_every_repeated_identity_and_null_property()
    {
        var value = new Dictionary<string, string?>
        {
            ["symbol"] = string.Concat(Enumerable.Repeat("assembly:public.synthetic|method:Unused012345|", 100)),
            ["null"] = null, ["unicode"] = "\u0000\uE000\U00010000"
        };
        var frame = IndexedGraphPayload.Encode(value);
        Assert.Equal(0, frame[4]);
        Assert.Equal(IndexedGraphPayload.DecodedLength(frame) + 9, frame.Length);
        Assert.Equal(value, IndexedGraphPayload.Decode<Dictionary<string, string?>>(frame));
        Assert.Equal(frame, IndexedGraphPayload.Encode(value));
    }

    [Fact]
    public void Large_frames_reject_wrong_length_truncation_and_trailing_bytes()
    {
        var frame = IndexedGraphPayload.Encode(new string('x', 4096));
        Assert.Equal(0, frame[4]);
        var wrongLength = frame.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(wrongLength.AsSpan(5, 4), IndexedGraphPayload.DecodedLength(frame) + 1);
        Assert.Throws<InvalidDataException>(() => IndexedGraphPayload.Decode<string>(wrongLength));
        Assert.Throws<InvalidDataException>(() => IndexedGraphPayload.Decode<string>(frame[..^1]));
        Assert.Throws<InvalidDataException>(() => IndexedGraphPayload.Decode<string>([.. frame, 0]));
    }

    [Fact]
    public void Frames_reject_unknown_encoding_and_oversize_declarations_before_decode_allocation()
    {
        var frame = IndexedGraphPayload.Encode("small");
        var unknown = frame.ToArray(); unknown[4] = 2;
        Assert.Throws<InvalidDataException>(() => IndexedGraphPayload.Decode<string>(unknown));
        var oversize = frame.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(oversize.AsSpan(5, 4), IndexedGraphPayload.MaxDecodedBytes + 1);
        Assert.Throws<InvalidDataException>(() => IndexedGraphPayload.Decode<string>(oversize));
        var zero = frame.ToArray(); BinaryPrimitives.WriteInt32LittleEndian(zero.AsSpan(5, 4), 0);
        Assert.Throws<InvalidDataException>(() => IndexedGraphPayload.Decode<string>(zero));
        Assert.Throws<InvalidDataException>(() => IndexedGraphPayload.Decode<string>(frame[..8]));
        var wrongMagic = frame.ToArray(); wrongMagic[0] = 0;
        Assert.Throws<InvalidDataException>(() => IndexedGraphPayload.Decode<string>(wrongMagic));
        Assert.Throws<InvalidDataException>(() => IndexedGraphPayload.Decode<string>([.. frame, 0]));
    }
}
