using TraceMap.Core;

namespace TraceMap.EndpointAlignment;

internal static class EndpointMatcher
{
    public static EndpointAlignmentReport Match(EndpointIndex clientIndex, EndpointIndex serverIndex)
    {
        var findings = new List<EndpointFinding>();
        var matchedServerFactIds = new HashSet<string>(StringComparer.Ordinal);

        if (HasCredibilityGap(clientIndex) || HasCredibilityGap(serverIndex))
        {
            findings.Add(CreateAnalysisGapFinding(clientIndex, serverIndex));
        }

        foreach (var client in clientIndex.Endpoints)
        {
            if (client.IsDynamic || string.IsNullOrWhiteSpace(client.NormalizedPathKey))
            {
                findings.Add(CreateFinding(clientIndex.Source, serverIndex.Source, 
                    EndpointClassifications.DynamicClientUrlNeedsReview,
                    client,
                    null,
                    "Low",
                    [string.IsNullOrWhiteSpace(client.DynamicReason) ? "Client URL could not be statically normalized." : $"Client URL dynamic reason: {client.DynamicReason}."]));
                continue;
            }

            var pathMatches = serverIndex.Endpoints
                .Where(server => server.ExpandedPathKeys.Contains(client.NormalizedPathKey, StringComparer.Ordinal))
                .ToArray();
            var methodMatches = pathMatches
                .Where(server => MethodsCompatible(client.Method, server.Method))
                .ToArray();

            if (methodMatches.Length == 1)
            {
                var server = methodMatches[0];
                matchedServerFactIds.Add(server.FactId);
                var optional = server.NormalizedPathKey != client.NormalizedPathKey && server.ExpandedPathKeys.Contains(client.NormalizedPathKey, StringComparer.Ordinal);
                findings.Add(CreateFinding(clientIndex.Source, serverIndex.Source, 
                    optional ? EndpointClassifications.OptionalSegmentMatch : EndpointClassifications.MatchedEndpoint,
                    client,
                    server,
                    optional ? "Medium" : "High",
                    NotesFor(client, server, optional)));
                continue;
            }

            if (methodMatches.Length > 1)
            {
                foreach (var server in methodMatches)
                {
                    matchedServerFactIds.Add(server.FactId);
                }
                findings.Add(CreateFinding(clientIndex.Source, serverIndex.Source, 
                    EndpointClassifications.AmbiguousMatch,
                    client,
                    methodMatches.OrderBy(server => server.FilePath, StringComparer.Ordinal).ThenBy(server => server.StartLine).First(),
                    "Medium",
                    [$"More than one server endpoint matched {client.Method} {client.NormalizedPathKey}."]));
                continue;
            }

            if (pathMatches.Length > 0)
            {
                foreach (var server in pathMatches)
                {
                    matchedServerFactIds.Add(server.FactId);
                }
                findings.Add(CreateFinding(clientIndex.Source, serverIndex.Source, 
                    EndpointClassifications.MethodMismatch,
                    client,
                    pathMatches.OrderBy(server => server.Method, StringComparer.Ordinal).ThenBy(server => server.FilePath, StringComparer.Ordinal).First(),
                    "Medium",
                    [$"Client method {client.Method} did not match server method(s): {string.Join(", ", pathMatches.Select(server => server.Method).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))}."]));
                continue;
            }

            findings.Add(CreateFinding(clientIndex.Source, serverIndex.Source, 
                EndpointClassifications.ClientCallNoServerEndpoint,
                client,
                null,
                "Medium",
                ["Coverage-relative finding only: this is not proof of a broken client call."]));
        }

        foreach (var server in serverIndex.Endpoints.Where(server => !matchedServerFactIds.Contains(server.FactId)))
        {
            findings.Add(CreateFinding(clientIndex.Source, serverIndex.Source, 
                EndpointClassifications.ServerEndpointNoClientMatch,
                null,
                server,
                "Medium",
                ["Coverage-relative finding only: this is not proof of dead code or an unused endpoint."]));
        }

        var warnings = CoverageWarnings(clientIndex.Source, serverIndex.Source);
        return new EndpointAlignmentReport(
            RuleIds.EndpointAlignment,
            ScannerVersions.EndpointAlignment,
            warnings.Count == 0 ? "FullEvidenceForScannedIndexes" : "ReducedEvidenceForScannedIndexes",
            warnings,
            clientIndex.Source,
            serverIndex.Source,
            findings
                .OrderBy(finding => finding.Classification, StringComparer.Ordinal)
                .ThenBy(finding => finding.NormalizedPathKey, StringComparer.Ordinal)
                .ThenBy(finding => finding.Method, StringComparer.Ordinal)
                .ToArray());
    }

    private static bool MethodsCompatible(string clientMethod, string serverMethod)
    {
        return serverMethod == "ANY" || clientMethod == serverMethod;
    }

    private static EndpointFinding CreateFinding(EndpointSource clientSource, EndpointSource serverSource, string classification, EndpointCandidate? client, EndpointCandidate? server, string staticMatchQuality, IReadOnlyList<string> notes)
    {
        return new EndpointFinding(
            classification,
            client?.Method ?? server?.Method ?? "ANY",
            client?.NormalizedPathKey ?? server?.NormalizedPathKey,
            client?.NormalizedPathTemplate,
            server?.NormalizedPathTemplate,
            client?.FactId,
            server?.FactId,
            client?.ScanId ?? clientSource.ScanId,
            server?.ScanId ?? serverSource.ScanId,
            client?.CommitSha ?? clientSource.CommitSha,
            server?.CommitSha ?? serverSource.CommitSha,
            RuleIds.EndpointAlignment,
            staticMatchQuality,
            client?.EvidenceTier,
            server?.EvidenceTier,
            client is null ? null : ToEvidence(client),
            server is null ? null : ToEvidence(server),
            notes);
    }

    private static EndpointFinding CreateAnalysisGapFinding(EndpointIndex clientIndex, EndpointIndex serverIndex)
    {
        var clientGap = RepresentativeGap(clientIndex);
        var serverGap = RepresentativeGap(serverIndex);
        return new EndpointFinding(
            EndpointClassifications.UnknownAnalysisGap,
            "ANY",
            null,
            null,
            null,
            clientGap?.FactId,
            serverGap?.FactId,
            clientGap?.ScanId ?? clientIndex.Source.ScanId,
            serverGap?.ScanId ?? serverIndex.Source.ScanId,
            clientGap?.CommitSha ?? clientIndex.Source.CommitSha,
            serverGap?.CommitSha ?? serverIndex.Source.CommitSha,
            RuleIds.EndpointAlignment,
            "Low",
            clientGap?.EvidenceTier,
            serverGap?.EvidenceTier,
            clientGap is null ? null : ToEvidence(clientGap),
            serverGap is null ? null : ToEvidence(serverGap),
            AnalysisGapNotes(clientGap, serverGap));
    }

    private static EndpointCandidate? RepresentativeGap(EndpointIndex index)
    {
        return index.AnalysisGaps
            .OrderBy(gap => gap.FilePath, StringComparer.Ordinal)
            .ThenBy(gap => gap.StartLine)
            .ThenBy(gap => gap.FactId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static IReadOnlyList<string> AnalysisGapNotes(EndpointCandidate? clientGap, EndpointCandidate? serverGap)
    {
        var notes = new List<string> { "One or both source indexes contain analysis gaps. Endpoint findings remain coverage-relative." };
        if (clientGap is not null)
        {
            notes.Add($"Client analysis gap evidence: {clientGap.FilePath}:{clientGap.StartLine}.");
        }
        if (serverGap is not null)
        {
            notes.Add($"Server analysis gap evidence: {serverGap.FilePath}:{serverGap.StartLine}.");
        }
        return notes;
    }

    private static EndpointEvidence ToEvidence(EndpointCandidate candidate)
    {
        return new EndpointEvidence(
            candidate.FactId,
            candidate.FactType,
            candidate.RuleId,
            candidate.EvidenceTier,
            candidate.FilePath,
            candidate.StartLine,
            candidate.EndLine,
            candidate.CommitSha,
            candidate.Properties.TryGetValue("extractorVersion", out var extractorVersion) ? extractorVersion : string.Empty);
    }

    private static IReadOnlyList<string> NotesFor(EndpointCandidate client, EndpointCandidate server, bool optional)
    {
        var notes = new List<string>();
        if (optional)
        {
            notes.Add("Matched through a server optional route segment.");
        }
        var clientParams = string.Join(";", client.ParameterNames);
        var serverParams = string.Join(";", server.ParameterNames);
        if (!string.Equals(clientParams, serverParams, StringComparison.Ordinal) && client.ParameterNames.Count > 0 && server.ParameterNames.Count > 0)
        {
            notes.Add($"Parameter names differ: client `{clientParams}`, server `{serverParams}`.");
        }
        if (client.HasQueryParameters)
        {
            notes.Add($"Client query parameters are side evidence only: {string.Join(", ", client.QueryParameterNames)}.");
        }
        return notes;
    }

    private static bool HasCredibilityGap(EndpointIndex index)
    {
        return index.Source.CommitSha == "unknown" && index.Endpoints.Count == 0 && index.AnalysisGaps.Count > 0;
    }

    private static IReadOnlyList<string> CoverageWarnings(EndpointSource client, EndpointSource server)
    {
        var warnings = new List<string>();
        AddSourceWarnings("client", client, warnings);
        AddSourceWarnings("server", server, warnings);
        return warnings;
    }

    private static void AddSourceWarnings(string side, EndpointSource source, List<string> warnings)
    {
        if (!source.AnalysisLevel.Equals("Level1SemanticAnalysis", StringComparison.Ordinal) || !source.BuildStatus.Equals("Succeeded", StringComparison.Ordinal))
        {
            warnings.Add($"{side} index reports {source.AnalysisLevel}/{source.BuildStatus}; endpoint conclusions are reduced coverage.");
        }
        if (source.CommitSha == "unknown")
        {
            warnings.Add($"{side} index commit SHA is unknown; long-term snapshot comparisons are not credible.");
        }
    }
}
