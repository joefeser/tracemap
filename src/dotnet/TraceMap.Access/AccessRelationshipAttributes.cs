namespace TraceMap.Access;

public sealed record AccessRelationshipAttributeProjection(
    IReadOnlyList<string> Flags,
    uint UnknownBits);

public static class AccessRelationshipAttributes
{
    private static readonly (int Mask, string Flag)[] KnownFlags =
    [
        (1, "unique-one-to-one"),
        (2, "not-enforced"),
        (4, "inherited"),
        (256, "update-cascade"),
        (4096, "delete-cascade"),
        (16_777_216, "left-default-join"),
        (33_554_432, "right-default-join")
    ];

    private const int KnownMask = 1 | 2 | 4 | 256 | 4096 | 16_777_216 | 33_554_432;

    public static AccessRelationshipAttributeProjection Decode(int attributes)
    {
        var flags = KnownFlags
            .Where(item => (attributes & item.Mask) != 0)
            .Select(item => item.Flag)
            .ToArray();
        return new AccessRelationshipAttributeProjection(flags, unchecked((uint)(attributes & ~KnownMask)));
    }
}
