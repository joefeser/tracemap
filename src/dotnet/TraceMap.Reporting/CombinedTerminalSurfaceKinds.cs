namespace TraceMap.Reporting;

internal static class CombinedTerminalSurfaceKinds
{
    public static readonly string[] All =
    [
        "sql-query",
        "sql-persistence",
        "http-route",
        "http-client",
        "package-config",
        "wcf-operation",
        "asmx-service",
        "asmx-operation",
        "asmx-client",
        "asmx-config",
        "asmx-metadata",
        "remoting-endpoint",
        "remoting-registration",
        "remoting-channel",
        "remoting-object",
        "remoting-api",
        "legacy-data",
        "dependency-surface",
        "message-queue",
        "message-topic",
        "message-subscription",
        "message-exchange",
        "message-stream",
        "message-event",
        "message-channel",
        "message-unknown"
    ];

    public static readonly HashSet<string> AllSet = new(All, StringComparer.Ordinal);

    public static string ValidationList => FormatAllowedValues(All);

    private static string FormatAllowedValues(IReadOnlyList<string> values)
    {
        return values.Count switch
        {
            0 => string.Empty,
            1 => values[0],
            2 => $"{values[0]} or {values[1]}",
            _ => $"{string.Join(", ", values.Take(values.Count - 1))}, or {values[^1]}"
        };
    }
}
