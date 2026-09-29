using System.Buffers.Binary;
using System.Text.Json;

namespace TraceMap.Reporting;

/// <summary>Lossless bounded row encoding for an owned, unpublished scratch database.</summary>
internal static class IndexedGraphPayload
{
    internal const int MaxDecodedBytes = 512 * 1024 * 1024;
    private const int HeaderBytes = 9;

    internal static byte[] Encode<T>(T value)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(value);
        if (json.Length > MaxDecodedBytes) throw new ReportInputLimitException("graph-storage-bytes");
        var frame = new byte[checked(HeaderBytes + json.Length)];
        "TGP2"u8.CopyTo(frame);
        frame[4] = 0;
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(5, 4), json.Length);
        json.CopyTo(frame.AsSpan(HeaderBytes));
        return frame;
    }

    internal static T Decode<T>(byte[] frame)
    {
        var length = DecodedLength(frame);
        var payload = frame.AsSpan(HeaderBytes);
        if (payload.Length != length) throw Invalid();
        return JsonSerializer.Deserialize<T>(payload) ?? throw Invalid();
    }

    internal static int DecodedLength(byte[] frame)
    {
        if (frame.Length < HeaderBytes || !frame.AsSpan(0, 4).SequenceEqual("TGP2"u8)
            || frame[4] != 0) throw Invalid();
        var length = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(5, 4));
        if (length <= 0 || length > MaxDecodedBytes) throw Invalid();
        return length;
    }
    private static InvalidDataException Invalid() => new("COMBINED_GRAPH_PAYLOAD_INVALID");
}
