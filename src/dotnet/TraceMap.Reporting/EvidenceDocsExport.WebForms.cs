using System.Text;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Reporting;

public static partial class EvidenceDocsExporter
{
    private const long MaxWebFormsPacketBytes = 128L * 1024 * 1024;

    private static async Task<WebFormsPacketProjection> ProjectWebFormsPacketChunksAsync(
        IReadOnlyList<string> paths,
        IReadOnlyList<DocSource> indexSources,
        IReadOnlyList<string> selectedFamilies,
        CancellationToken cancellationToken)
    {
        var chunks = new List<EvidenceDocChunk>();
        var inputs = new List<EvidenceDocsInputSummary>();
        foreach (var path in paths.OrderBy(value => value, StringComparer.Ordinal))
        {
            if (!File.Exists(path) || new FileInfo(path).Length > MaxWebFormsPacketBytes)
            {
                throw new InvalidOperationException("InputUnreadable: docs-export --webforms-packet must point to a readable bounded packet.");
            }

            WebFormsModernizationPacket packet;
            string json;
            try
            {
                json = await File.ReadAllTextAsync(path, cancellationToken);
                using var document = JsonDocument.Parse(
                    json,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = false,
                        CommentHandling = JsonCommentHandling.Disallow,
                        MaxDepth = 96
                    });
                StaticHtmlEvidenceExplorer.RejectDuplicateJsonProperties(document.RootElement);
                packet = JsonSerializer.Deserialize<WebFormsModernizationPacket>(json, JsonOptions)
                    ?? throw new JsonException("empty packet");
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException)
            {
                throw new InvalidOperationException("InputSchemaUnsupported: docs-export requires webforms-modernization-packet.v1 JSON.", exception);
            }

            try
            {
                StaticHtmlEvidenceExplorer.ValidateWebFormsPacket(packet, expectedCommitSha: null);
            }
            catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or ArgumentException or NullReferenceException)
            {
                throw new InvalidOperationException("InputSchemaUnsupported: docs-export requires a valid webforms-modernization-packet.v1 contract.", exception);
            }

            var sourceMap = MatchPacketSources(packet, indexSources);
            var sourceRefs = sourceMap.Values.Select(ToSourceRef).DistinctBy(value => value.SourceId).OrderBy(value => value.SourceId, StringComparer.Ordinal).ToArray();
            inputs.Add(new EvidenceDocsInputSummary(
                "webforms-modernization-packet",
                $"packet:{Hash(packet.PacketId, 24)}",
                "hidden",
                "compatible",
                sourceRefs.Select(value => value.SourceLabel).ToArray(),
                DistinctSorted([packet.Coverage, .. sourceRefs.Select(value => value.CoverageLabel)]),
                packet.Limitations.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                sourceRefs,
                packet.SchemaVersion));

            if (selectedFamilies.Contains("webforms-modernization", StringComparer.Ordinal))
            {
                chunks.Add(CreateWebFormsOverviewChunk(packet, sourceRefs));
                chunks.AddRange(packet.Projects.OrderBy(value => value.ProjectId, StringComparer.Ordinal)
                    .Select(value => CreateWebFormsProjectChunk(packet, value, sourceMap)));
                if (packet.SurfaceSelection is not null)
                {
                    chunks.Add(CreateWebFormsSurfaceSelectionChunk(packet, packet.SurfaceSelection, sourceRefs));
                }
                chunks.AddRange(packet.Surfaces.OrderBy(value => value.SurfaceId, StringComparer.Ordinal)
                    .Select(value => CreateWebFormsSurfaceChunk(packet, value, SourceFor(value.Evidence, sourceMap))));
                chunks.AddRange(packet.EventChains.OrderBy(value => value.ChainId, StringComparer.Ordinal)
                    .Select(value => CreateWebFormsEventChainChunk(packet, value, sourceMap)));
                chunks.AddRange(packet.DownstreamBoundaries.OrderBy(value => value.BoundaryId, StringComparer.Ordinal)
                    .Select(value => CreateWebFormsBoundaryChunk(packet, value, sourceMap)));
                chunks.AddRange(packet.IdentityStateInventory.OrderBy(value => value.IdentityStateId, StringComparer.Ordinal)
                    .Select(value => CreateWebFormsIdentityChunk(packet, value, SourceFor(value.Evidence, sourceMap))));
                chunks.AddRange(packet.BatchDataMovementInventory.OrderBy(value => value.BatchDataMovementId, StringComparer.Ordinal)
                    .Select(value => CreateWebFormsBatchChunk(packet, value, SourceFor(value.Evidence, sourceMap))));
                chunks.AddRange(packet.StructuralSliceCandidates.OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                    .Select(value => CreateWebFormsSliceChunk(packet, value, sourceMap)));
            }

            if (selectedFamilies.Contains("gap", StringComparer.Ordinal))
            {
                chunks.AddRange(packet.Gaps.OrderBy(value => value.GapId, StringComparer.Ordinal)
                    .Select(value => CreateWebFormsGapChunk(packet, value, sourceMap)));
            }
        }

        return new WebFormsPacketProjection(chunks, inputs);
    }

    private static IReadOnlyDictionary<string, DocSource> MatchPacketSources(WebFormsModernizationPacket packet, IReadOnlyList<DocSource> indexSources)
    {
        var result = new Dictionary<string, DocSource>(StringComparer.Ordinal);
        foreach (var packetSource in packet.Sources)
        {
            var packetCommitSha = CommitOrNull(packetSource.CommitSha);
            var matches = indexSources.Where(source =>
                    source.ScanId == packetSource.ScanId
                    && packetCommitSha is not null
                    && string.Equals(source.CommitSha, packetCommitSha, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException("InputSourceIdentityMismatch: Web Forms packet source does not uniquely match the supplied index.");
            }

            result[packetSource.SourceId] = matches[0];
        }

        return result;
    }

    private static DocSource SourceFor(WebFormsModernizationEvidence evidence, IReadOnlyDictionary<string, DocSource> sourceMap)
        => SourceFor(evidence.CommitSha, sourceMap);

    private static DocSource SourceFor(WebFormsModernizationPathEvidence evidence, IReadOnlyDictionary<string, DocSource> sourceMap)
        => SourceFor(evidence.CommitSha, sourceMap);

    private static DocSource SourceFor(string commitSha, IReadOnlyDictionary<string, DocSource> sourceMap)
    {
        var normalized = CommitOrNull(commitSha);
        return sourceMap.Values.Single(source => normalized is not null && string.Equals(source.CommitSha, normalized, StringComparison.Ordinal));
    }

    private static EvidenceDocChunk CreateWebFormsOverviewChunk(WebFormsModernizationPacket packet, IReadOnlyList<EvidenceDocSourceRef> sourceRefs)
    {
        var summary = packet.Summary;
        var citation = new EvidenceDocCitation(
            StableId("citation", "docs-export/webforms-packet/v1", [new("packetId", packet.PacketId)]),
            sourceRefs.First().SourceLabel,
            sourceRefs.First().SourceScope,
            sourceRefs.First().ScanId,
            sourceRefs.First().CommitSha,
            packet.Coverage,
            null,
            null,
            null,
            DistinctSorted([packet.RuleId, WebFormsModernizationRuleId]),
            EvidenceTiers.Tier2Structural,
            "webforms-modernization-packet",
            WebFormsModernizationPacketReporter.SchemaVersion,
            [],
            [],
            [packet.PacketId]);
        var body = new StringBuilder()
            .AppendLine("## Web Forms modernization evidence packet")
            .AppendLine()
            .AppendLine("This chunk inventories the bounded deterministic evidence carried by the supplied Web Forms packet.")
            .AppendLine()
            .AppendLine("| Field | Value |")
            .AppendLine("| --- | --- |")
            .AppendLine($"| Packet ID | `{EscapeInline(packet.PacketId)}` |")
            .AppendLine($"| Coverage | `{EscapeInline(packet.Coverage)}` |")
            .AppendLine($"| Projects | `{summary.ProjectCount}` |")
            .AppendLine($"| Surfaces | `{summary.SurfaceCount}` |")
            .AppendLine($"| Event chains | `{summary.EventChainCount}` |")
            .AppendLine($"| Downstream boundaries | `{summary.DownstreamBoundaryCount}` |")
            .AppendLine($"| Identity and state records | `{summary.IdentityStateCount}` |")
            .AppendLine($"| Batch and data-movement records | `{summary.BatchDataMovementCount}` |")
            .AppendLine($"| Structural slices | `{summary.StructuralSliceCandidateCount}` |")
            .AppendLine($"| Gaps | `{summary.GapCount}` |")
            .AppendLine($"| Truncated | `{summary.Truncated.ToString().ToLowerInvariant()}` |")
            .AppendLine()
            .AppendLine("### Owner questions")
            .AppendLine();
        foreach (var question in packet.OwnerQuestions.OrderBy(value => value, StringComparer.Ordinal))
        {
            body.AppendLine($"- {EscapeText(question)}");
        }

        return CreateWebFormsChunk(packet, "overview", "Web Forms evidence packet overview", body.ToString(), [citation], sourceRefs, [packet.PacketId], [packet.RuleId], [EvidenceTiers.Tier2Structural], [packet.Coverage], packet.Limitations);
    }

    private static EvidenceDocChunk CreateWebFormsProjectChunk(WebFormsModernizationPacket packet, WebFormsModernizationProject value, IReadOnlyDictionary<string, DocSource> sources)
    {
        var citations = value.Evidence.Select(evidence => Citation(evidence, SourceFor(evidence, sources))).DistinctBy(citation => citation.CitationId).ToArray();
        var sourceRefs = value.Evidence.Select(evidence => ToSourceRef(SourceFor(evidence, sources))).DistinctBy(source => source.SourceId).ToArray();
        var body = $"""
            ## Web Forms project inventory

            | Field | Value |
            | --- | --- |
            | Project ID | `{EscapeInline(value.ProjectId)}` |
            | Surface count | `{value.SurfaceCount}` |
            """;
        return CreateWebFormsChunk(packet, "project", "Web Forms project evidence", body, citations, SourceRefsOrPacketSources(sourceRefs, sources), [value.ProjectId, .. value.SupportingFactIds], value.Evidence.Select(evidence => evidence.RuleId).ToArray(), value.Evidence.Select(evidence => evidence.EvidenceTier).ToArray(), value.Evidence.Select(evidence => evidence.CoverageLabel).ToArray(), value.Evidence.SelectMany(evidence => evidence.Limitations).ToArray());
    }

    private static EvidenceDocChunk CreateWebFormsSurfaceSelectionChunk(WebFormsModernizationPacket packet, WebFormsModernizationSurfaceSelection value, IReadOnlyList<EvidenceDocSourceRef> sourceRefs)
    {
        var body = new StringBuilder()
            .AppendLine("## Requested Web Forms surface coverage")
            .AppendLine()
            .AppendLine("| Field | Value |")
            .AppendLine("| --- | --- |")
            .AppendLine($"| Requested | `{value.RequestedCount}` |")
            .AppendLine($"| Matched | `{value.MatchedCount}` |")
            .AppendLine($"| Unmatched | `{value.UnmatchedCount}` |")
            .AppendLine($"| Ambiguous | `{value.AmbiguousCount}` |")
            .AppendLine($"| Unavailable | `{value.UnavailableCount}` |")
            .AppendLine()
            .AppendLine("| Alias | Status | Surface IDs |")
            .AppendLine("| --- | --- | --- |");
        foreach (var item in value.Items.OrderBy(item => item.Alias, StringComparer.Ordinal))
        {
            body.AppendLine($"| `{EscapeInline(item.Alias)}` | `{EscapeInline(item.Status)}` | `{EscapeInline(string.Join(", ", item.SurfaceIds.OrderBy(id => id, StringComparer.Ordinal)))}` |");
        }

        var citation = new EvidenceDocCitation(
            StableId("citation", "docs-export/webforms-surface-selection/v1", [new("packetId", packet.PacketId), new("ruleId", value.RuleId)]),
            sourceRefs.First().SourceLabel,
            sourceRefs.First().SourceScope,
            sourceRefs.First().ScanId,
            sourceRefs.First().CommitSha,
            packet.Coverage,
            null,
            null,
            null,
            DistinctSorted([value.RuleId, WebFormsModernizationRuleId]),
            EvidenceTiers.Tier2Structural,
            "webforms-modernization-packet",
            WebFormsModernizationPacketReporter.SchemaVersion,
            [],
            [],
            [packet.PacketId]);
        return CreateWebFormsChunk(packet, "surface-selection", "Requested Web Forms surface coverage", body.ToString(), [citation], sourceRefs, [packet.PacketId, .. value.Items.Select(item => item.RequestId)], [value.RuleId], [EvidenceTiers.Tier2Structural], [packet.Coverage], value.Limitations);
    }

    private static EvidenceDocChunk CreateWebFormsSurfaceChunk(WebFormsModernizationPacket packet, WebFormsModernizationSurface value, DocSource source)
    {
        var evidence = new[] { value.Evidence }.Concat(value.SupportingEvidence)
            .DistinctBy(item => item.FactId)
            .OrderBy(item => item.FactId, StringComparer.Ordinal)
            .ToArray();
        var citations = evidence.Select(item => Citation(item, source)).ToArray();
        var body = $"""
            ## Web Forms surface

            | Field | Value |
            | --- | --- |
            | Surface ID | `{EscapeInline(value.SurfaceId)}` |
            | Surface kind | `{EscapeInline(value.SurfaceKind)}` |
            | Project ID | `{EscapeInline(value.ProjectId)}` |
            | Composition targets | `{EscapeInline(string.Join(", ", value.CompositionTargetIds.OrderBy(item => item, StringComparer.Ordinal)))}` |
            | Controls | `{value.ControlIds.Count}` |
            | File span | `{EscapeInline(FormatSpan(value.Evidence))}` |
            """;
        var chunk = CreateWebFormsChunk(
            packet,
            "surface",
            "Web Forms surface evidence",
            body,
            citations,
            [ToSourceRef(source)],
            [value.SurfaceId, .. value.SupportingFactIds],
            evidence.Select(item => item.RuleId).ToArray(),
            evidence.Select(item => item.EvidenceTier).ToArray(),
            evidence.Select(item => item.CoverageLabel).ToArray(),
            evidence.SelectMany(item => item.Limitations).ToArray());
        return WithRetrievalHints(chunk, [Hint("webforms-surface-facts", "Retrieve retained facts explicitly associated with this Web Forms surface.", [("surface_id", value.SurfaceId), ("limit", "250")], [value.SurfaceId])]);
    }

    private static EvidenceDocChunk CreateWebFormsEventChainChunk(WebFormsModernizationPacket packet, WebFormsModernizationEventChain value, IReadOnlyDictionary<string, DocSource> sources)
    {
        var citations = value.Evidence.Select(evidence => Citation(evidence, SourceFor(evidence, sources)))
            .Concat(value.PathEvidence.Select(evidence => Citation(evidence, SourceFor(evidence, sources))))
            .DistinctBy(citation => citation.CitationId).ToArray();
        var traversal = value.TraversalObservation;
        var body = $"""
            ## Web Forms event chain

            | Field | Value |
            | --- | --- |
            | Chain ID | `{EscapeInline(value.ChainId)}` |
            | Surface ID | `{EscapeInline(value.SurfaceId)}` |
            | Event source ID | `{EscapeInline(value.EventSourceId)}` |
            | Handler ID | `{EscapeInline(value.HandlerId ?? "handler-unavailable")}` |
            | Classification | `{EscapeInline(value.Classification)}` |
            | Terminal kind | `{EscapeInline(value.TerminalKind ?? "terminal-unavailable")}` |
            | Traversal stop state | `{EscapeInline(traversal?.StopState ?? "unavailable")}` |
            | Traversed edges | `{traversal?.TraversedEdgeCount ?? 0}` |
            | Truncated | `{(traversal?.Truncated ?? false).ToString().ToLowerInvariant()}` |
            """;
        var sourceRefs = value.Evidence.Select(evidence => ToSourceRef(SourceFor(evidence, sources)))
            .Concat(value.PathEvidence.Select(evidence => ToSourceRef(SourceFor(evidence, sources))))
            .DistinctBy(source => source.SourceId).ToArray();
        var limitations = value.Limitations
            .Concat(value.Evidence.SelectMany(evidence => evidence.Limitations))
            .Concat(value.PathEvidence.SelectMany(evidence => evidence.Limitations))
            .Concat(value.TraversalObservation?.Limitations ?? [])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(message => message, StringComparer.Ordinal)
            .ToArray();
        var chunk = CreateWebFormsChunk(packet, "event-chain", "Web Forms event-chain evidence", body, citations, SourceRefsOrPacketSources(sourceRefs, sources), [value.ChainId, .. value.SupportingFactIds, .. value.SupportingEdgeIds], value.RuleIds, value.EvidenceTiers, value.CoverageLabels, limitations);
        return string.IsNullOrWhiteSpace(value.HandlerSymbol)
            ? chunk
            : WithRetrievalHints(chunk,
            [
                Hint("calls-from-handler", "Retrieve retained direct call edges from this handler.", [("handler_symbol", value.HandlerSymbol), ("limit", "250")], [value.ChainId]),
                Hint("database-evidence-by-handler", "Retrieve database-shaped facts directly retained for this handler.", [("handler_symbol", value.HandlerSymbol), ("limit", "250")], [value.ChainId]),
                Hint("stored-procedure-candidate-context", "Retrieve command, property, invocation, and argument evidence retained for this handler without assuming object identity.", [("method_symbol", value.HandlerSymbol), ("limit", "250")], [value.ChainId])
            ]);
    }

    private static EvidenceDocChunk CreateWebFormsBoundaryChunk(WebFormsModernizationPacket packet, WebFormsModernizationDownstreamBoundary value, IReadOnlyDictionary<string, DocSource> sources)
    {
        var citations = value.Evidence.Select(evidence => Citation(evidence, SourceFor(evidence, sources)))
            .Concat(value.PathEvidence.Select(evidence => Citation(evidence, SourceFor(evidence, sources))))
            .DistinctBy(citation => citation.CitationId).ToArray();
        var body = $"""
            ## Web Forms downstream boundary

            | Field | Value |
            | --- | --- |
            | Boundary ID | `{EscapeInline(value.BoundaryId)}` |
            | Chain ID | `{EscapeInline(value.ChainId)}` |
            | Category | `{EscapeInline(value.BoundaryCategory)}` |
            | Kind | `{EscapeInline(value.BoundaryKind)}` |
            | Target ID | `{EscapeInline(value.BoundaryTargetId)}` |
            | Classification | `{EscapeInline(value.Classification)}` |
            """;
        var sourceRefs = value.Evidence.Select(evidence => ToSourceRef(SourceFor(evidence, sources)))
            .Concat(value.PathEvidence.Select(evidence => ToSourceRef(SourceFor(evidence, sources))))
            .DistinctBy(source => source.SourceId).ToArray();
        var chainLimitations = packet.EventChains.Single(chain => chain.ChainId == value.ChainId).TraversalObservation?.Limitations ?? [];
        var limitations = value.Limitations
            .Concat(value.Evidence.SelectMany(evidence => evidence.Limitations))
            .Concat(value.PathEvidence.SelectMany(evidence => evidence.Limitations))
            .Concat(chainLimitations)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(message => message, StringComparer.Ordinal)
            .ToArray();
        var chunk = CreateWebFormsChunk(packet, "downstream-boundary", "Web Forms downstream-boundary evidence", body, citations, SourceRefsOrPacketSources(sourceRefs, sources), [value.BoundaryId, value.ChainId, .. value.SupportingFactIds, .. value.SupportingEdgeIds], value.RuleIds, value.EvidenceTiers, value.CoverageLabels, limitations);
        return !value.TerminalEvidenceIsFact
            ? chunk
            : WithRetrievalHints(chunk,
        [
            Hint("boundary-supporting-facts", "Retrieve the retained terminal evidence row cited by this downstream boundary.", [("terminal_evidence_id", value.TerminalEvidenceId), ("limit", "1")], [value.BoundaryId, value.TerminalEvidenceId])
        ]);
    }

    private static EvidenceDocChunk CreateWebFormsIdentityChunk(WebFormsModernizationPacket packet, WebFormsModernizationIdentityState value, DocSource source)
    {
        var body = $"""
            ## Web Forms identity and state declaration

            | Field | Value |
            | --- | --- |
            | Identity/state ID | `{EscapeInline(value.IdentityStateId)}` |
            | Kind | `{EscapeInline(value.IdentityKind)}` |
            | Classification | `{EscapeInline(value.Classification)}` |
            | Surface ID | `{EscapeInline(value.SurfaceId ?? "unassociated")}` |
            | File span | `{EscapeInline(FormatSpan(value.Evidence))}` |
            """;
        var chunk = CreateWebFormsChunk(packet, "identity-state", "Web Forms identity/state evidence", body, [Citation(value.Evidence, source)], [ToSourceRef(source)], [value.IdentityStateId, .. value.SupportingFactIds], [value.Evidence.RuleId], [value.Evidence.EvidenceTier], [value.Evidence.CoverageLabel], value.Limitations);
        return value.SurfaceId is null
            ? chunk
            : WithRetrievalHints(chunk, [Hint("webforms-surface-facts", "Retrieve other facts associated with this identity/state record's Web Forms surface.", [("surface_id", value.SurfaceId), ("limit", "250")], [value.IdentityStateId])]);
    }

    private static EvidenceDocChunk CreateWebFormsBatchChunk(WebFormsModernizationPacket packet, WebFormsModernizationBatchDataMovement value, DocSource source)
    {
        var body = $"""
            ## Web Forms batch and data-movement declaration

            | Field | Value |
            | --- | --- |
            | Record ID | `{EscapeInline(value.BatchDataMovementId)}` |
            | Surface kind | `{EscapeInline(value.SurfaceKind)}` |
            | Mechanism | `{EscapeInline(value.Mechanism)}` |
            | Operation kind | `{EscapeInline(value.OperationKind)}` |
            | Owner status | `{EscapeInline(value.OwnerStatus)}` |
            | Project resolution | `{EscapeInline(value.ProjectResolution)}` |
            | File span | `{EscapeInline(FormatSpan(value.Evidence))}` |
            """;
        return CreateWebFormsChunk(packet, "batch-data-movement", "Web Forms batch/data-movement evidence", body, [Citation(value.Evidence, source)], [ToSourceRef(source)], [value.BatchDataMovementId, .. value.SupportingFactIds], [value.Evidence.RuleId], [value.Evidence.EvidenceTier], [value.Evidence.CoverageLabel], value.Limitations);
    }

    private static EvidenceDocChunk CreateWebFormsSliceChunk(WebFormsModernizationPacket packet, WebFormsModernizationSliceCandidate value, IReadOnlyDictionary<string, DocSource> sources)
    {
        var citations = value.Evidence.Select(evidence => Citation(evidence, SourceFor(evidence, sources))).DistinctBy(citation => citation.CitationId).ToArray();
        var body = $"""
            ## Web Forms structural slice candidate

            | Field | Value |
            | --- | --- |
            | Candidate ID | `{EscapeInline(value.CandidateId)}` |
            | Classification | `{EscapeInline(value.Classification)}` |
            | Owner naming required | `{value.OwnerNamingRequired.ToString().ToLowerInvariant()}` |
            | Surface IDs | `{EscapeInline(string.Join(", ", value.SurfaceIds.OrderBy(item => item, StringComparer.Ordinal)))}` |
            """;
        var sourceRefs = value.Evidence.Select(evidence => ToSourceRef(SourceFor(evidence, sources))).DistinctBy(source => source.SourceId).ToArray();
        return CreateWebFormsChunk(packet, "structural-slice", "Web Forms structural-slice evidence", body, citations, SourceRefsOrPacketSources(sourceRefs, sources), [value.CandidateId, .. value.SupportingFactIds], [value.RuleId], [value.EvidenceTier], value.CoverageLabels, value.Limitations);
    }

    private static EvidenceDocChunk CreateWebFormsGapChunk(
        WebFormsModernizationPacket packet,
        WebFormsModernizationGap value,
        IReadOnlyDictionary<string, DocSource> sources)
    {
        var source = SourceFor(value.CommitSha, sources);
        var sourceRef = ToSourceRef(source);
        var safePath = SafeRelativePathOrNull(value.FilePath);
        var exportedGapId = StableId("gap", "docs-export/webforms-gap/v1", [new("sourceId", source.SourceId), new("gapId", value.GapId)]);
        var gap = new EvidenceDocGap(
            exportedGapId,
            value.RuleId,
            value.EvidenceTier,
            value.Classification,
            "webforms-modernization",
            [sourceRef],
            DistinctSorted([value.GapId, .. value.SupportingFactIds]),
            value.Limitations.OrderBy(item => item, StringComparer.Ordinal).ToArray())
        {
            FilePath = safePath,
            StartLine = safePath is null ? null : value.StartLine,
            EndLine = safePath is null ? null : value.EndLine,
            CommitSha = CommitOrNull(value.CommitSha),
            ExtractorName = value.ExtractorId,
            ExtractorVersion = value.ExtractorVersion
        };
        var citations = safePath is null
            ? Array.Empty<EvidenceDocCitation>()
            : [new EvidenceDocCitation(
                StableId("citation", "docs-export/webforms-gap-evidence/v1", [new("sourceId", source.SourceId), new("gapId", value.GapId)]),
                source.Label,
                source.Scope,
                source.ScanId,
                CommitOrNull(value.CommitSha),
                value.CoverageLabel,
                safePath,
                value.StartLine,
                value.EndLine,
                [value.RuleId, WebFormsModernizationRuleId],
                value.EvidenceTier,
                value.ExtractorId,
                value.ExtractorVersion,
                DistinctSorted(value.SupportingFactIds),
                [],
                [packet.PacketId])];
        var body = $"""
            ## Web Forms evidence gap

            | Field | Value |
            | --- | --- |
            | Gap ID | `{EscapeInline(value.GapId)}` |
            | Classification | `{EscapeInline(value.Classification)}` |
            | Scope | `{EscapeInline(value.ScopeKind)}` |
            | Scope ID | `{EscapeInline(value.ScopeId ?? "unavailable")}` |
            | Rule ID | `{EscapeInline(value.RuleId)}` |
            | Evidence tier | `{EscapeInline(value.EvidenceTier)}` |
            | Coverage | `{EscapeInline(value.CoverageLabel)}` |

            This gap preserves bounded uncertainty and does not prove evidence or behavior is absent.
            """;
        var limitationRecords = value.Limitations.Distinct(StringComparer.Ordinal).OrderBy(message => message, StringComparer.Ordinal).Select(message => new EvidenceDocLimitation(
            StableId("limitation", "docs-export/webforms-gap-limitation/v1", [new("sourceId", source.SourceId), new("gapId", value.GapId), new("message", message)]),
            value.RuleId,
            EvidenceTiers.Tier4Unknown,
            message,
            "webforms-modernization",
            gap.SupportingIds)).ToArray();
        var chunk = CreateChunk(
            "gap",
            "gap",
            "hidden",
            "Web Forms evidence gap",
            $"Web Forms packet gap: {value.Classification}.",
            body,
            citations,
            [sourceRef],
            DistinctSorted([packet.PacketId, .. gap.SupportingIds]),
            DistinctSorted([GapChunkRuleId, WebFormsModernizationRuleId, value.RuleId]),
            [value.EvidenceTier],
            [value.CoverageLabel],
            [gap],
            limitationRecords);
        return safePath is null || value.StartLine is null || value.EndLine is null
            ? chunk
            : WithRetrievalHints(chunk, [Hint("gap-neighborhood", "Retrieve static evidence overlapping this gap's source span without treating it as gap closure.", [("file_path", safePath), ("start_line", value.StartLine.Value.ToString()), ("end_line", value.EndLine.Value.ToString()), ("limit", "100")], [exportedGapId])]);
    }

    private static EvidenceDocChunk CreateWebFormsChunk(
        WebFormsModernizationPacket packet,
        string type,
        string title,
        string body,
        IReadOnlyList<EvidenceDocCitation> citations,
        IReadOnlyList<EvidenceDocSourceRef> sourceRefs,
        IReadOnlyList<string> supportingIds,
        IReadOnlyList<string> ruleIds,
        IReadOnlyList<string> tiers,
        IReadOnlyList<string> coverage,
        IReadOnlyList<string> limitations)
    {
        var allSupporting = DistinctSorted([packet.PacketId, .. supportingIds]);
        var limitationRecords = limitations.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)
            .Select(message => new EvidenceDocLimitation(
                StableId("limitation", "docs-export/webforms-limitation/v1", [new("packetId", packet.PacketId), new("message", message)]),
                WebFormsModernizationRuleId,
                EvidenceTiers.Tier4Unknown,
                message,
                "webforms-modernization",
                allSupporting))
            .ToArray();
        return CreateChunk(
            "webforms-modernization",
            tiers.Any(tier => tier is EvidenceTiers.Tier3SyntaxOrTextual or EvidenceTiers.Tier4Unknown) ? "weak-static-evidence" : "claim",
            "hidden",
            title,
            "Deterministic Web Forms packet evidence with TraceMap citations.",
            body,
            citations,
            sourceRefs.DistinctBy(value => value.SourceId).OrderBy(value => value.SourceId, StringComparer.Ordinal).ToArray(),
            allSupporting,
            DistinctSorted([WebFormsModernizationRuleId, packet.RuleId, .. ruleIds]),
            tiers,
            DistinctSorted([packet.Coverage, .. coverage]),
            [],
            limitationRecords);
    }

    private static EvidenceDocCitation Citation(WebFormsModernizationEvidence evidence, DocSource source)
        => new(
            StableId("citation", "docs-export/webforms-evidence/v1", [new("factId", evidence.FactId), new("filePath", evidence.FilePath), new("startLine", evidence.StartLine.ToString())]),
            source.Label,
            source.Scope,
            source.ScanId,
            CommitOrNull(evidence.CommitSha) ?? source.CommitSha,
            evidence.CoverageLabel,
            SafeRelativePathOrNull(evidence.FilePath),
            evidence.StartLine,
            evidence.EndLine,
            [evidence.RuleId, WebFormsModernizationRuleId],
            evidence.EvidenceTier,
            evidence.ExtractorId,
            evidence.ExtractorVersion,
            DistinctSorted([evidence.FactId, .. evidence.SupportingFactIds]),
            DistinctSorted(evidence.SupportingEdgeIds),
            []);

    private static EvidenceDocCitation Citation(WebFormsModernizationPathEvidence evidence, DocSource source)
        => new(
            StableId("citation", "docs-export/webforms-path-evidence/v1", [new("evidenceId", evidence.EvidenceId)]),
            source.Label,
            source.Scope,
            source.ScanId,
            CommitOrNull(evidence.CommitSha) ?? source.CommitSha,
            evidence.CoverageLabel,
            SafeRelativePathOrNull(evidence.FilePath),
            evidence.StartLine,
            evidence.EndLine,
            [evidence.RuleId, WebFormsModernizationRuleId],
            evidence.EvidenceTier,
            evidence.ExtractorId,
            evidence.ExtractorVersion,
            DistinctSorted(evidence.SupportingFactIds),
            [],
            []);

    private static string FormatSpan(WebFormsModernizationEvidence evidence)
        => SafeRelativePathOrNull(evidence.FilePath) is { } path
            ? $"{path}:{evidence.StartLine}-{evidence.EndLine}"
            : "source-span-unavailable";

    private static IReadOnlyList<EvidenceDocSourceRef> SourceRefsOrPacketSources(
        IReadOnlyList<EvidenceDocSourceRef> sourceRefs,
        IReadOnlyDictionary<string, DocSource> sources)
        => sourceRefs.Count > 0
            ? sourceRefs
            : sources.Values.Select(ToSourceRef).DistinctBy(source => source.SourceId).OrderBy(source => source.SourceId, StringComparer.Ordinal).ToArray();

    private sealed record WebFormsPacketProjection(
        IReadOnlyList<EvidenceDocChunk> Chunks,
        IReadOnlyList<EvidenceDocsInputSummary> Inputs);
}
