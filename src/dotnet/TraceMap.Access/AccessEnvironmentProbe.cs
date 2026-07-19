namespace TraceMap.Access;

public static class AccessEnvironmentProbe
{
    public static Type RequireAccessApplicationType()
    {
        if (!OperatingSystem.IsWindows()) throw new AccessScanException("AccessUnsupportedPlatform");
        return Type.GetTypeFromProgID("Access.Application", throwOnError: false)
            ?? throw new AccessScanException("AccessComUnavailable");
    }

    internal static Type Probe(bool isWindows, Func<Type?> resolveType)
    {
        if (!isWindows) throw new AccessScanException("AccessUnsupportedPlatform");
        return resolveType() ?? throw new AccessScanException("AccessComUnavailable");
    }
}
