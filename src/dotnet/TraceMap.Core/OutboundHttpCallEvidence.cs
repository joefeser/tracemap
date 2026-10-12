namespace TraceMap.Core;

/// <summary>Privacy-safe bounded API metadata; no URLs, source strings or destination hashes.</summary>
public sealed record OutboundHttpCallEvidence(string MethodFamily, string MethodName,
    string DestinationAvailability, string ProtocolKind, string CoverageLabel, string Limitations)
{
    internal static OutboundHttpCallEvidence FromProperties(IReadOnlyDictionary<string, string> properties)
    {
        string Bounded(string key, params string[] allowed) => properties.TryGetValue(key, out var value)
            && allowed.Contains(value, StringComparer.Ordinal) ? value : "unavailable";
        var method = properties.GetValueOrDefault("methodName", string.Empty);
        return new(Bounded("methodFamily", "HttpClient", "JsonHttpExtension", "WebClient", "WebRequest", "unresolved"),
            OutboundHttpApi.IsCandidate(method) ? method : "unavailable",
            Bounded("urlKind", "compile-time-constant-path", "compile-time-constant-hash", "unavailable"),
            Bounded("protocolKind", "unproven-address-protocol", "framework-http-api"),
            Bounded("coverageLabel", "bounded-static-http-call", "reduced-name-only-candidate"),
            OutboundHttpApi.Limitations);
    }
}
