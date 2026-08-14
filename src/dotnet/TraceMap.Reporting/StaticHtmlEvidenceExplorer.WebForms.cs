using System.Text;
using System.Text.Json;

namespace TraceMap.Reporting;

public sealed record ExplorerWebFormsData(
    ExplorerWebFormsSummary Summary,
    IReadOnlyList<ExplorerWebFormsProject> Projects,
    IReadOnlyList<ExplorerWebFormsSurface> Surfaces,
    IReadOnlyList<ExplorerWebFormsEventChain> EventChains,
    IReadOnlyList<ExplorerWebFormsBoundary> DownstreamBoundaries,
    IReadOnlyList<ExplorerWebFormsIdentityState> IdentityStateInventory,
    IReadOnlyList<ExplorerWebFormsBatchDataMovement> BatchDataMovementInventory,
    IReadOnlyList<ExplorerWebFormsCandidate> StructuralCandidates,
    IReadOnlyList<string> OwnerQuestions);

public sealed record ExplorerWebFormsSummary(
    string PacketId,
    string Coverage,
    int ProjectCount,
    int SurfaceCount,
    int EventChainCount,
    int DownstreamBoundaryCount,
    int IdentityStateCount,
    int BatchDataMovementCount,
    int StructuralCandidateCount,
    int GapCount,
    bool Truncated);

public sealed record ExplorerWebFormsProject(
    string ProjectId,
    int SurfaceCount,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<string> SupportIds);

public sealed record ExplorerWebFormsSurface(
    string SurfaceId,
    string SurfaceKind,
    string ProjectId,
    IReadOnlyList<string> CompositionTargetIds,
    IReadOnlyList<string> ControlIds,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<string> SupportIds);

public sealed record ExplorerWebFormsEventChain(
    string ChainId,
    string SurfaceId,
    string EventSourceId,
    string? HandlerId,
    string Classification,
    string? TerminalKind,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<string> SupportIds,
    IReadOnlyList<string> RuleIds,
    IReadOnlyList<string> EvidenceTiers,
    IReadOnlyList<string> CoverageLabels,
    IReadOnlyList<string> LimitationIds);

public sealed record ExplorerWebFormsBoundary(
    string BoundaryId,
    string ChainId,
    string SurfaceId,
    string? HandlerId,
    string BoundaryCategory,
    string BoundaryKind,
    string BoundaryTargetId,
    string Classification,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<string> SupportIds,
    IReadOnlyList<string> RuleIds,
    IReadOnlyList<string> EvidenceTiers,
    IReadOnlyList<string> CoverageLabels,
    IReadOnlyList<string> LimitationIds);

public sealed record ExplorerWebFormsIdentityState(
    string IdentityStateId,
    string IdentityKind,
    string Classification,
    string? SurfaceId,
    IReadOnlyDictionary<string, string> SafeMetadata,
    string EvidenceId,
    IReadOnlyList<string> SupportIds,
    IReadOnlyList<string> LimitationIds);

public sealed record ExplorerWebFormsBatchDataMovement(
    string BatchDataMovementId,
    string SurfaceKind,
    string Mechanism,
    string OperationKind,
    string OwnerStatus,
    string ProjectResolution,
    string? ProjectId,
    IReadOnlyDictionary<string, string> SafeMetadata,
    string EvidenceId,
    IReadOnlyList<string> SupportIds,
    IReadOnlyList<string> LimitationIds);

public sealed record ExplorerWebFormsCandidate(
    string CandidateId,
    string Classification,
    string RuleId,
    string EvidenceTier,
    string? OwnerLabel,
    bool OwnerNamingRequired,
    IReadOnlyList<string> SurfaceIds,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<string> SupportIds,
    IReadOnlyList<string> CoverageLabels,
    IReadOnlyList<string> LimitationIds);

public static partial class StaticHtmlEvidenceExplorer
{
    public const string WebFormsPacketInputRuleId = "explorer.input.webforms-modernization-packet.v1";
    private const long MaxWebFormsPacketBytes = 33_554_432;
    private const int MaxWebFormsRowsPerCollection = 10_000;
    private const int MaxWebFormsEvidenceRows = 100_000;
    private const string WebFormsArtifactId = "artifact:webforms-modernization";
    private const string WebFormsSourceId = "source:webforms-modernization";

    private static async Task<WebFormsExplorerLoad> AddWebFormsPacketArtifactAsync(
        string inputDirectory,
        string safetyProfile,
        string? expectedCommitSha,
        Dictionary<(string RuleId, string Category, string Location, string Action), int> redactions,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(inputDirectory, "webforms-modernization.json");
        if (!File.Exists(path))
        {
            return WebFormsExplorerLoad.Empty;
        }

        var snapshot = await ReadBoundedArtifactAsync(path, MaxWebFormsPacketBytes, cancellationToken);
        if (snapshot.Content is null)
        {
            return UnsupportedWebForms(snapshot.ContentHash, safetyProfile, "artifact-too-large",
                "The Web Forms modernization packet exceeded the bounded reader size and was not parsed.");
        }

        try
        {
            using var document = JsonDocument.Parse(
                snapshot.Content,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 96
                });
            var root = RequireObject(document.RootElement, "root");
            RejectDuplicateJsonProperties(root);
            var packet = JsonSerializer.Deserialize<WebFormsModernizationPacket>(snapshot.Content, JsonOptions)
                ?? throw new InvalidDataException("empty packet");
            ValidateWebFormsPacket(packet, expectedCommitSha);
            var source = packet.Sources.Single();
            var coverage = SafeCoverageLabel(packet.Coverage);
            var limitationRows = new Dictionary<string, ExplorerLimitation>(StringComparer.Ordinal);
            var evidenceRows = new Dictionary<string, ExplorerEvidenceRow>(StringComparer.Ordinal);

            IReadOnlyList<string> RegisterLimitations(IEnumerable<string> messages, string scope)
            {
                var ids = new List<string>();
                foreach (var raw in messages ?? [])
                {
                    var message = SafeClosedText(raw, "webforms.limitations", redactions);
                    var id = $"limitation:webforms:{Hash(scope + "|" + message, 20)}";
                    ids.Add(id);
                    limitationRows.TryAdd(id, new ExplorerLimitation(
                        id,
                        WebFormsPacketInputRuleId,
                        Tier4Unknown,
                        "webforms-static-boundary",
                        "webforms",
                        scope,
                        "Prevents runtime, business-intent, parity, migration, architecture, security, cloud-readiness, and release conclusions.",
                        message,
                        [WebFormsArtifactId]));
                }
                return ids.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            }

            string AddEvidence(WebFormsModernizationEvidence evidence, string evidenceKind)
            {
                var id = $"evidence:webforms:{Hash(evidence.FactId + "|" + evidenceKind + "|" + evidence.FilePath + "|" + evidence.StartLine, 24)}";
                var limitationIds = RegisterLimitations(evidence.Limitations, evidence.FactId);
                evidenceRows.TryAdd(id, new ExplorerEvidenceRow(
                    id,
                    SafeClosedText(evidence.RuleId, "rule-id", redactions),
                    SafeEvidenceTier(evidence.EvidenceTier, []),
                    evidenceKind,
                    SafeClosedText(evidence.FactId, "support-id", redactions),
                    WebFormsArtifactId,
                    WebFormsSourceId,
                    evidence.CommitSha,
                    SafeRepositoryPath(evidence.FilePath, redactions),
                    evidence.StartLine,
                    evidence.EndLine,
                    null,
                    SafeCoverageLabel(evidence.CoverageLabel),
                    SafeClosedText(evidence.ExtractorVersion, "extractor-version", redactions),
                    limitationIds,
                    SafeClosedText(evidence.ExtractorId, "extractor-id", redactions),
                    SafeSupportIds(evidence.SupportingFactIds),
                    SafeSupportIds(evidence.SupportingEdgeIds)));
                return id;
            }

            string AddPathEvidence(WebFormsModernizationPathEvidence evidence)
            {
                var id = $"evidence:webforms-path:{Hash(evidence.EvidenceId, 24)}";
                var limitationIds = RegisterLimitations(evidence.Limitations, evidence.EvidenceId);
                evidenceRows.TryAdd(id, new ExplorerEvidenceRow(
                    id,
                    SafeClosedText(evidence.RuleId, "rule-id", redactions),
                    SafeEvidenceTier(evidence.EvidenceTier, []),
                    SafeClosedText(evidence.EvidenceKind, "evidence-kind", redactions),
                    SafeClosedText(evidence.EvidenceId, "support-id", redactions),
                    WebFormsArtifactId,
                    WebFormsSourceId,
                    evidence.CommitSha,
                    evidence.FilePath is null ? null : SafeRepositoryPath(evidence.FilePath, redactions),
                    evidence.StartLine,
                    evidence.EndLine,
                    null,
                    SafeCoverageLabel(evidence.CoverageLabel),
                    SafeClosedText(evidence.ExtractorVersion, "extractor-version", redactions),
                    limitationIds,
                    SafeClosedText(evidence.ExtractorId, "extractor-id", redactions),
                    SafeSupportIds(evidence.SupportingFactIds),
                    []));
                return id;
            }

            var projects = packet.Projects.Select(project => new ExplorerWebFormsProject(
                SafeClosedText(project.ProjectId, "webforms-project-id", redactions),
                project.SurfaceCount,
                project.Evidence.Select(evidence => AddEvidence(evidence, "webforms-project-evidence")).ToArray(),
                SafeSupportIds(project.SupportingFactIds))).ToArray();
            var surfaces = packet.Surfaces.Select(surface => new ExplorerWebFormsSurface(
                SafeClosedText(surface.SurfaceId, "webforms-surface-id", redactions),
                SafeClosedText(surface.SurfaceKind, "webforms-surface-kind", redactions),
                SafeClosedText(surface.ProjectId, "webforms-project-id", redactions),
                surface.CompositionTargetIds.Select(value => SafeClosedText(value, "webforms-composition-target-id", redactions)).ToArray(),
                surface.ControlIds.Select(value => SafeClosedText(value, "webforms-control-id", redactions)).ToArray(),
                new[] { AddEvidence(surface.Evidence, "webforms-surface-evidence") }
                    .Concat(surface.SupportingEvidence.Select(evidence => AddEvidence(evidence, "webforms-surface-support")))
                    .Distinct(StringComparer.Ordinal).ToArray(),
                SafeSupportIds(surface.SupportingFactIds))).ToArray();
            var chains = packet.EventChains.Select(chain => new ExplorerWebFormsEventChain(
                SafeClosedText(chain.ChainId, "webforms-chain-id", redactions),
                SafeClosedText(chain.SurfaceId, "webforms-surface-id", redactions),
                SafeClosedText(chain.EventSourceId, "webforms-event-source-id", redactions),
                string.IsNullOrWhiteSpace(chain.HandlerId) ? null : SafeClosedText(chain.HandlerId, "webforms-handler-id", redactions),
                SafeClosedText(chain.Classification, "webforms-classification", redactions),
                string.IsNullOrWhiteSpace(chain.TerminalKind) ? null : SafeClosedText(chain.TerminalKind, "webforms-terminal-kind", redactions),
                chain.Evidence.Select(evidence => AddEvidence(evidence, "webforms-event-chain-evidence"))
                    .Concat(chain.PathEvidence.Select(AddPathEvidence)).Distinct(StringComparer.Ordinal).ToArray(),
                SafeSupportIds(chain.SupportingFactIds.Concat(chain.SupportingEdgeIds)),
                chain.RuleIds.Select(value => SafeClosedText(value, "rule-id", redactions)).ToArray(),
                chain.EvidenceTiers.Select(value => SafeEvidenceTier(value, [])).ToArray(),
                chain.CoverageLabels.Select(SafeCoverageLabel).ToArray(),
                RegisterLimitations(chain.Limitations, chain.ChainId))).ToArray();
            var boundaries = packet.DownstreamBoundaries.Select(boundary => new ExplorerWebFormsBoundary(
                SafeClosedText(boundary.BoundaryId, "webforms-boundary-id", redactions),
                SafeClosedText(boundary.ChainId, "webforms-chain-id", redactions),
                SafeClosedText(boundary.SurfaceId, "webforms-surface-id", redactions),
                string.IsNullOrWhiteSpace(boundary.HandlerId) ? null : SafeClosedText(boundary.HandlerId, "webforms-handler-id", redactions),
                SafeClosedText(boundary.BoundaryCategory, "webforms-boundary-category", redactions),
                SafeClosedText(boundary.BoundaryKind, "webforms-boundary-kind", redactions),
                SafeClosedText(boundary.BoundaryTargetId, "webforms-boundary-target-id", redactions),
                SafeClosedText(boundary.Classification, "webforms-classification", redactions),
                boundary.Evidence.Select(evidence => AddEvidence(evidence, "webforms-boundary-evidence"))
                    .Concat(boundary.PathEvidence.Select(AddPathEvidence)).Distinct(StringComparer.Ordinal).ToArray(),
                SafeSupportIds(boundary.SupportingFactIds.Concat(boundary.SupportingEdgeIds)),
                boundary.RuleIds.Select(value => SafeClosedText(value, "rule-id", redactions)).ToArray(),
                boundary.EvidenceTiers.Select(value => SafeEvidenceTier(value, [])).ToArray(),
                boundary.CoverageLabels.Select(SafeCoverageLabel).ToArray(),
                RegisterLimitations(boundary.Limitations, boundary.BoundaryId))).ToArray();
            var identityStates = packet.IdentityStateInventory.Select(state => new ExplorerWebFormsIdentityState(
                SafeClosedText(state.IdentityStateId, "webforms-identity-state-id", redactions),
                SafeClosedText(state.IdentityKind, "webforms-identity-kind", redactions),
                SafeClosedText(state.Classification, "webforms-classification", redactions),
                string.IsNullOrWhiteSpace(state.SurfaceId) ? null : SafeClosedText(state.SurfaceId, "webforms-surface-id", redactions),
                state.SafeMetadata.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(
                    pair => SafeClosedText(pair.Key, "webforms-metadata-key", redactions),
                    pair => SafeClosedText(pair.Value, "webforms-metadata-value", redactions),
                    StringComparer.Ordinal),
                AddEvidence(state.Evidence, "webforms-identity-state-evidence"),
                SafeSupportIds(state.SupportingFactIds),
                RegisterLimitations(state.Limitations, state.IdentityStateId))).ToArray();
            var batchDataMovement = packet.BatchDataMovementInventory.Select(item => new ExplorerWebFormsBatchDataMovement(
                SafeClosedText(item.BatchDataMovementId, "webforms-batch-data-movement-id", redactions),
                SafeClosedText(item.SurfaceKind, "webforms-batch-surface-kind", redactions),
                SafeClosedText(item.Mechanism, "webforms-batch-mechanism", redactions),
                SafeClosedText(item.OperationKind, "webforms-batch-operation-kind", redactions),
                SafeClosedText(item.OwnerStatus, "webforms-batch-owner-status", redactions),
                SafeClosedText(item.ProjectResolution, "webforms-batch-project-resolution", redactions),
                string.IsNullOrWhiteSpace(item.ProjectId) ? null : SafeClosedText(item.ProjectId, "webforms-project-id", redactions),
                item.SafeMetadata.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(
                    pair => SafeClosedText(pair.Key, "webforms-metadata-key", redactions),
                    pair => SafeClosedText(pair.Value, "webforms-metadata-value", redactions),
                    StringComparer.Ordinal),
                AddEvidence(item.Evidence, "webforms-batch-data-movement-evidence"),
                SafeSupportIds(item.SupportingFactIds),
                RegisterLimitations(item.Limitations, item.BatchDataMovementId))).ToArray();
            var candidates = packet.StructuralSliceCandidates.Select(candidate => new ExplorerWebFormsCandidate(
                SafeClosedText(candidate.CandidateId, "webforms-candidate-id", redactions),
                SafeClosedText(candidate.Classification, "webforms-classification", redactions),
                SafeClosedText(candidate.RuleId, "rule-id", redactions),
                SafeEvidenceTier(candidate.EvidenceTier, []),
                string.IsNullOrWhiteSpace(candidate.OwnerLabel) ? null : SafeClosedText(candidate.OwnerLabel, "webforms-owner-label", redactions),
                candidate.OwnerNamingRequired,
                candidate.SurfaceIds.Select(value => SafeClosedText(value, "webforms-surface-id", redactions)).ToArray(),
                candidate.Evidence.Select(evidence => AddEvidence(evidence, "webforms-structural-candidate-evidence")).ToArray(),
                SafeSupportIds(candidate.SupportingFactIds),
                candidate.CoverageLabels.Select(SafeCoverageLabel).ToArray(),
                RegisterLimitations(candidate.Limitations, candidate.CandidateId))).ToArray();

            var gapRows = packet.Gaps.Select(gap =>
            {
                var evidenceId = $"evidence:webforms-gap:{Hash(gap.GapId, 24)}";
                var limitationIds = RegisterLimitations(gap.Limitations, gap.GapId);
                evidenceRows.TryAdd(evidenceId, new ExplorerEvidenceRow(
                    evidenceId,
                    SafeClosedText(gap.RuleId, "rule-id", redactions),
                    SafeEvidenceTier(gap.EvidenceTier, []),
                    "webforms-gap",
                    SafeClosedText(gap.GapId, "support-id", redactions),
                    WebFormsArtifactId,
                    WebFormsSourceId,
                    gap.CommitSha,
                    gap.FilePath is null ? null : SafeRepositoryPath(gap.FilePath, redactions),
                    gap.StartLine,
                    gap.EndLine,
                    null,
                    SafeCoverageLabel(gap.CoverageLabel),
                    SafeClosedText(gap.ExtractorVersion, "extractor-version", redactions),
                    limitationIds,
                    SafeClosedText(gap.ExtractorId!, "extractor-id", redactions),
                    SafeSupportIds(gap.SupportingFactIds),
                    []));
                return new ExplorerGap(
                    $"webforms-gap:{Hash(gap.GapId, 20)}",
                    SafeClosedText(gap.RuleId, "rule-id", redactions),
                    SafeEvidenceTier(gap.EvidenceTier, []),
                    SafeClosedText(gap.Classification, "webforms-gap-classification", redactions),
                    SafeClosedText(gap.ScopeId ?? gap.ScopeKind, "webforms-gap-scope", redactions),
                    "webforms",
                    SafeCoverageLabel(gap.CoverageLabel),
                    "The compatible Web Forms packet records a bounded analysis gap. No evidence-absence conclusion is inferred.",
                    SafeSupportIds(gap.SupportingFactIds.Append(evidenceId)));
            }).ToArray();

            var packetLimitations = RegisterLimitations(packet.Limitations, packet.PacketId);
            var data = new ExplorerWebFormsData(
                new ExplorerWebFormsSummary(
                    SafeClosedText(packet.PacketId, "webforms-packet-id", redactions),
                    coverage,
                    packet.Summary.ProjectCount,
                    packet.Summary.SurfaceCount,
                    packet.Summary.EventChainCount,
                    packet.Summary.DownstreamBoundaryCount,
                    packet.Summary.IdentityStateCount,
                    packet.Summary.BatchDataMovementCount,
                    packet.Summary.StructuralSliceCandidateCount,
                    packet.Summary.GapCount,
                    packet.Summary.Truncated),
                projects,
                surfaces,
                chains,
                boundaries,
                identityStates,
                batchDataMovement,
                candidates,
                packet.OwnerQuestions.Select(value => SafeClosedText(value, "webforms-owner-question", redactions)).ToArray());
            var artifact = new ExplorerInputArtifact(
                WebFormsArtifactId,
                "webforms-modernization-packet",
                "Web Forms modernization packet",
                snapshot.ContentHash,
                WebFormsModernizationPacketReporter.SchemaVersion,
                packet.ClaimLevel,
                [coverage],
                [WebFormsSourceId],
                packetLimitations,
                gapRows.Select(gap => gap.GapId).ToArray(),
                packet.Summary.Truncated || packet.Gaps.Count > 0 ? "supported-partial" : "supported");
            var explorerSource = new ExplorerSource(
                WebFormsSourceId,
                "Web Forms packet source",
                "webforms-modernization-packet",
                packet.ClaimLevel,
                packet.Summary.Truncated || packet.Gaps.Count > 0 ? "partial" : "complete",
                source.CommitSha,
                evidenceRows.Values.Select(row => row.ExtractorVersion).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                [WebFormsArtifactId],
                gapRows.Length,
                limitationRows.Count,
                0,
                0);
            return new WebFormsExplorerLoad(
                data,
                source.CommitSha,
                artifact,
                explorerSource,
                evidenceRows.Values.OrderBy(row => row.EvidenceId, StringComparer.Ordinal).ToArray(),
                gapRows,
                limitationRows.Values.OrderBy(row => row.LimitationId, StringComparer.Ordinal).ToArray(),
                [coverage]);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or InvalidOperationException or ArgumentException or NullReferenceException)
        {
            return UnsupportedWebForms(snapshot.ContentHash, safetyProfile, "unsupported-schema",
                "The Web Forms modernization packet did not match the supported bounded schema, identity, provenance, claim-level, or collection contract and was not rendered.");
        }
    }

    private static void ValidateWebFormsPacket(WebFormsModernizationPacket packet, string? expectedCommitSha)
    {
        if (packet.SchemaVersion != WebFormsModernizationPacketReporter.SchemaVersion
            || packet.RuleId != WebFormsModernizationPacketReporter.PacketRuleId
            || packet.ClaimLevel != "local-only"
            || packet.Coverage is not ("bounded-static-webforms-modernization" or "reduced-static-webforms-modernization")
            || packet.Sources?.Count != 1
            || !IsUsableCommitSha(packet.Sources[0].CommitSha)
            || !IsWebFormsHashedId(packet.Sources[0].SourceId, "source-")
            || !IsWebFormsHashedId(packet.Sources[0].RepositoryId, "repository-")
            || string.IsNullOrWhiteSpace(packet.Sources[0].ScanId)
            || string.IsNullOrWhiteSpace(packet.Sources[0].AnalysisLevel)
            || string.IsNullOrWhiteSpace(packet.Sources[0].BuildStatus)
            || !IsWebFormsHashedId(packet.PacketId, "packet-")
            || (expectedCommitSha is not null && !packet.Sources[0].CommitSha.Equals(expectedCommitSha, StringComparison.OrdinalIgnoreCase))
            || packet.Projects is null || packet.Surfaces is null || packet.EventChains is null
            || packet.DownstreamBoundaries is null || packet.IdentityStateInventory is null
            || packet.BatchDataMovementInventory is null
            || packet.StructuralSliceCandidates is null || packet.Gaps is null
            || packet.OwnerQuestions is null || packet.Limitations is null
            || new[]
            {
                packet.Projects.Count, packet.Surfaces.Count, packet.EventChains.Count,
                packet.DownstreamBoundaries.Count, packet.IdentityStateInventory.Count,
                packet.BatchDataMovementInventory.Count,
                packet.StructuralSliceCandidates.Count, packet.Gaps.Count,
                packet.OwnerQuestions.Count, packet.Limitations.Count
            }.Any(count => count > MaxWebFormsRowsPerCollection)
            || packet.Summary.ProjectCount != packet.Projects.Count
            || packet.Summary.SurfaceCount != packet.Surfaces.Count
            || packet.Summary.EventChainCount != packet.EventChains.Count
            || packet.Summary.DownstreamBoundaryCount != packet.DownstreamBoundaries.Count
            || packet.Summary.IdentityStateCount != packet.IdentityStateInventory.Count
            || packet.Summary.BatchDataMovementCount != packet.BatchDataMovementInventory.Count
            || packet.Summary.StructuralSliceCandidateCount != packet.StructuralSliceCandidates.Count
            || packet.Summary.GapCount != packet.Gaps.Count)
        {
            throw new InvalidDataException("unsupported Web Forms packet");
        }

        var projectIds = packet.Projects.Select(project => project.ProjectId).ToHashSet(StringComparer.Ordinal);
        var surfaceIds = packet.Surfaces.Select(surface => surface.SurfaceId).ToHashSet(StringComparer.Ordinal);
        var chainIds = packet.EventChains.Select(chain => chain.ChainId).ToHashSet(StringComparer.Ordinal);
        if (projectIds.Count != packet.Projects.Count
            || surfaceIds.Count != packet.Surfaces.Count
            || chainIds.Count != packet.EventChains.Count
            || packet.DownstreamBoundaries.Select(boundary => boundary.BoundaryId).Distinct(StringComparer.Ordinal).Count() != packet.DownstreamBoundaries.Count
            || packet.IdentityStateInventory.Select(state => state.IdentityStateId).Distinct(StringComparer.Ordinal).Count() != packet.IdentityStateInventory.Count
            || packet.BatchDataMovementInventory.Select(item => item.BatchDataMovementId).Distinct(StringComparer.Ordinal).Count() != packet.BatchDataMovementInventory.Count
            || packet.StructuralSliceCandidates.Select(candidate => candidate.CandidateId).Distinct(StringComparer.Ordinal).Count() != packet.StructuralSliceCandidates.Count
            || packet.Gaps.Select(gap => gap.GapId).Distinct(StringComparer.Ordinal).Count() != packet.Gaps.Count
            || packet.Surfaces.Any(surface => !projectIds.Contains(surface.ProjectId))
            || packet.EventChains.Any(chain => !surfaceIds.Contains(chain.SurfaceId))
            || packet.DownstreamBoundaries.Any(boundary => !surfaceIds.Contains(boundary.SurfaceId) || !chainIds.Contains(boundary.ChainId))
            || packet.IdentityStateInventory.Any(state => state.SurfaceId is not null && !surfaceIds.Contains(state.SurfaceId))
            || packet.StructuralSliceCandidates.Any(candidate => candidate.SurfaceIds is null || candidate.SurfaceIds.Any(id => !surfaceIds.Contains(id))))
        {
            throw new InvalidDataException("inconsistent Web Forms packet identity graph");
        }

        var sourceCommitSha = packet.Sources[0].CommitSha;
        var evidenceCount = 0;
        void ValidateEvidenceCollection(IReadOnlyList<WebFormsModernizationEvidence>? rows)
        {
            if (rows is null) throw new InvalidDataException("missing Web Forms evidence collection");
            foreach (var row in rows)
            {
                ValidateWebFormsEvidence(row, sourceCommitSha);
                evidenceCount++;
            }
        }

        foreach (var project in packet.Projects)
        {
            if (string.IsNullOrWhiteSpace(project.ProjectId) || project.SurfaceCount < 0 || project.SupportingFactIds is null)
                throw new InvalidDataException("invalid Web Forms project");
            ValidateEvidenceCollection(project.Evidence);
        }
        foreach (var surface in packet.Surfaces)
        {
            if (string.IsNullOrWhiteSpace(surface.SurfaceId) || string.IsNullOrWhiteSpace(surface.SurfaceKind)
                || string.IsNullOrWhiteSpace(surface.ProjectId) || surface.CompositionTargetIds is null
                || surface.ControlIds is null || surface.Evidence is null || surface.SupportingFactIds is null)
                throw new InvalidDataException("invalid Web Forms surface");
            ValidateWebFormsEvidence(surface.Evidence, sourceCommitSha);
            evidenceCount++;
            ValidateEvidenceCollection(surface.SupportingEvidence);
        }
        foreach (var chain in packet.EventChains)
        {
            if (string.IsNullOrWhiteSpace(chain.ChainId) || string.IsNullOrWhiteSpace(chain.SurfaceId)
                || string.IsNullOrWhiteSpace(chain.EventSourceId) || string.IsNullOrWhiteSpace(chain.Classification)
                || chain.SupportingFactIds is null || chain.SupportingEdgeIds is null || chain.RuleIds is null
                || chain.EvidenceTiers is null || chain.CoverageLabels is null || chain.Limitations is null
                || chain.RuleIds.Any(rule => !IsSafeRuleId(rule))
                || chain.EvidenceTiers.Any(tier => !IsSupportedEvidenceTier(tier)))
                throw new InvalidDataException("invalid Web Forms event chain");
            ValidateEvidenceCollection(chain.Evidence);
            ValidateWebFormsPathEvidence(chain.PathEvidence, sourceCommitSha, ref evidenceCount);
        }
        foreach (var boundary in packet.DownstreamBoundaries)
        {
            if (string.IsNullOrWhiteSpace(boundary.BoundaryId) || string.IsNullOrWhiteSpace(boundary.ChainId)
                || string.IsNullOrWhiteSpace(boundary.SurfaceId) || string.IsNullOrWhiteSpace(boundary.BoundaryCategory)
                || string.IsNullOrWhiteSpace(boundary.BoundaryKind) || string.IsNullOrWhiteSpace(boundary.BoundaryTargetId)
                || string.IsNullOrWhiteSpace(boundary.Classification) || boundary.SupportingFactIds is null
                || boundary.SupportingEdgeIds is null || boundary.RuleIds is null || boundary.EvidenceTiers is null
                || boundary.CoverageLabels is null || boundary.Limitations is null
                || boundary.RuleIds.Any(rule => !IsSafeRuleId(rule))
                || boundary.EvidenceTiers.Any(tier => !IsSupportedEvidenceTier(tier)))
                throw new InvalidDataException("invalid Web Forms downstream boundary");
            ValidateEvidenceCollection(boundary.Evidence);
            ValidateWebFormsPathEvidence(boundary.PathEvidence, sourceCommitSha, ref evidenceCount);
        }
        foreach (var state in packet.IdentityStateInventory)
        {
            if (string.IsNullOrWhiteSpace(state.IdentityStateId) || string.IsNullOrWhiteSpace(state.IdentityKind)
                || string.IsNullOrWhiteSpace(state.Classification) || state.SafeMetadata is null
                || state.Evidence is null || state.SupportingFactIds is null || state.Limitations is null)
                throw new InvalidDataException("invalid Web Forms identity state");
            ValidateWebFormsEvidence(state.Evidence, sourceCommitSha);
            evidenceCount++;
        }
        foreach (var item in packet.BatchDataMovementInventory)
        {
            if (string.IsNullOrWhiteSpace(item.BatchDataMovementId) || string.IsNullOrWhiteSpace(item.SurfaceKind)
                || string.IsNullOrWhiteSpace(item.Mechanism) || string.IsNullOrWhiteSpace(item.OperationKind)
                || string.IsNullOrWhiteSpace(item.OwnerStatus) || string.IsNullOrWhiteSpace(item.ProjectResolution)
                || item.SafeMetadata is null || item.Evidence is null || item.SupportingFactIds is null
                || item.Limitations is null)
                throw new InvalidDataException("invalid Web Forms batch/data-movement row");
            ValidateWebFormsEvidence(item.Evidence, sourceCommitSha);
            evidenceCount++;
        }
        foreach (var candidate in packet.StructuralSliceCandidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.CandidateId) || string.IsNullOrWhiteSpace(candidate.Classification)
                || !IsSafeRuleId(candidate.RuleId) || !IsSupportedEvidenceTier(candidate.EvidenceTier)
                || candidate.SurfaceIds is null || candidate.SupportingFactIds is null
                || candidate.CoverageLabels is null || candidate.Limitations is null)
                throw new InvalidDataException("invalid Web Forms structural candidate");
            ValidateEvidenceCollection(candidate.Evidence);
        }
        foreach (var gap in packet.Gaps)
        {
            ValidateWebFormsGap(gap, sourceCommitSha);
            evidenceCount++;
        }
        if (evidenceCount > MaxWebFormsEvidenceRows)
            throw new InvalidDataException("Web Forms packet evidence limit exceeded");
    }

    private static void ValidateWebFormsEvidence(WebFormsModernizationEvidence evidence, string commitSha)
    {
        if (string.IsNullOrWhiteSpace(evidence.FactId) || !IsSafeRuleId(evidence.RuleId)
            || !IsSupportedEvidenceTier(evidence.EvidenceTier) || !IsSafeWebFormsLabel(evidence.CoverageLabel)
            || !evidence.CommitSha.Equals(commitSha, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(evidence.FilePath) || evidence.StartLine < 1 || evidence.EndLine < evidence.StartLine
            || string.IsNullOrWhiteSpace(evidence.ExtractorId) || string.IsNullOrWhiteSpace(evidence.ExtractorVersion)
            || evidence.SupportingFactIds is null || evidence.SupportingEdgeIds is null || evidence.Limitations is null)
            throw new InvalidDataException("invalid Web Forms evidence provenance");
    }

    private static void ValidateWebFormsPathEvidence(
        IReadOnlyList<WebFormsModernizationPathEvidence>? rows,
        string commitSha,
        ref int evidenceCount)
    {
        if (rows is null) throw new InvalidDataException("missing Web Forms path evidence");
        foreach (var evidence in rows)
        {
            var hasPath = !string.IsNullOrWhiteSpace(evidence.FilePath);
            if (string.IsNullOrWhiteSpace(evidence.EvidenceId) || string.IsNullOrWhiteSpace(evidence.EvidenceKind)
                || !IsSafeRuleId(evidence.RuleId) || !IsSupportedEvidenceTier(evidence.EvidenceTier)
                || !IsSafeWebFormsLabel(evidence.CoverageLabel)
                || !evidence.CommitSha.Equals(commitSha, StringComparison.OrdinalIgnoreCase)
                || hasPath != (evidence.StartLine.HasValue && evidence.EndLine.HasValue)
                || (hasPath && (evidence.StartLine!.Value < 1 || evidence.EndLine!.Value < evidence.StartLine.Value))
                || string.IsNullOrWhiteSpace(evidence.ExtractorId) || string.IsNullOrWhiteSpace(evidence.ExtractorVersion)
                || evidence.SupportingFactIds is null || evidence.Limitations is null)
                throw new InvalidDataException("invalid Web Forms path provenance");
            evidenceCount++;
        }
    }

    private static void ValidateWebFormsGap(WebFormsModernizationGap gap, string commitSha)
    {
        var hasPath = !string.IsNullOrWhiteSpace(gap.FilePath);
        if (string.IsNullOrWhiteSpace(gap.GapId) || string.IsNullOrWhiteSpace(gap.Classification)
            || string.IsNullOrWhiteSpace(gap.ScopeKind) || !IsSafeRuleId(gap.RuleId)
            || !IsSupportedEvidenceTier(gap.EvidenceTier) || !IsSafeWebFormsLabel(gap.CoverageLabel)
            || !gap.CommitSha.Equals(commitSha, StringComparison.OrdinalIgnoreCase)
            || hasPath != gap.StartLine.HasValue
            || (!hasPath && gap.EndLine.HasValue)
            || (hasPath && (gap.StartLine!.Value < 1 || (gap.EndLine.HasValue && gap.EndLine.Value < gap.StartLine.Value)))
            || string.IsNullOrWhiteSpace(gap.ExtractorId) || string.IsNullOrWhiteSpace(gap.ExtractorVersion)
            || gap.SupportingFactIds is null || gap.Limitations is null)
            throw new InvalidDataException("invalid Web Forms gap provenance");
    }

    private static bool IsSafeWebFormsLabel(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or ':' or '-');

    private static bool IsWebFormsHashedId(string? value, string prefix) =>
        value is not null
        && value.Length == prefix.Length + 24
        && value.StartsWith(prefix, StringComparison.Ordinal)
        && value[prefix.Length..].All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static WebFormsExplorerLoad UnsupportedWebForms(
        string contentHash,
        string safetyProfile,
        string gapKind,
        string message)
    {
        var artifact = new ExplorerInputArtifact(
            WebFormsArtifactId,
            "webforms-modernization-packet",
            "Web Forms modernization packet",
            contentHash,
            "webforms-modernization-packet/unsupported",
            ClaimLevelForSafetyProfile(safetyProfile),
            [],
            [],
            [],
            [UnsupportedSchemaRuleId],
            "unsupported");
        var gap = CreateGap(
            "webforms-modernization-unsupported",
            UnsupportedSchemaRuleId,
            gapKind,
            WebFormsArtifactId,
            "webforms",
            "PartialAnalysis",
            message,
            [WebFormsArtifactId]);
        return new WebFormsExplorerLoad(null, null, artifact, null, [], [gap], [], []);
    }

    private static void RenderWebForms(StringBuilder builder, ExplorerWebFormsData? data)
    {
        builder.AppendLine("    <section id=\"webforms\" aria-labelledby=\"webforms-heading\">");
        builder.AppendLine("      <h2 id=\"webforms-heading\">Web Forms Modernization</h2>");
        builder.AppendLine("      <p>This section preserves bounded static packet evidence and structural candidates. It does not prove runtime execution, workflow completion, business intent, parity, migration effort, target architecture, security approval, cloud readiness, or release approval.</p>");
        if (data is null)
        {
            builder.AppendLine("      <p>No compatible Web Forms modernization packet was provided. This is an unavailable input state, not evidence that the application has no Web Forms surfaces.</p>");
            builder.AppendLine("    </section>");
            return;
        }

        builder.AppendLine("      <dl class=\"summary-grid\">");
        SummaryItem(builder, "Packet", data.Summary.PacketId);
        SummaryItem(builder, "Coverage", data.Summary.Coverage);
        SummaryItem(builder, "Projects", data.Summary.ProjectCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        SummaryItem(builder, "Surfaces", data.Summary.SurfaceCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        SummaryItem(builder, "Event chains", data.Summary.EventChainCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        SummaryItem(builder, "Downstream boundaries", data.Summary.DownstreamBoundaryCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        SummaryItem(builder, "Identity/session rows", data.Summary.IdentityStateCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        SummaryItem(builder, "Batch/data-movement rows", data.Summary.BatchDataMovementCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        SummaryItem(builder, "Structural candidates", data.Summary.StructuralCandidateCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        SummaryItem(builder, "Packet gaps", data.Summary.GapCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        SummaryItem(builder, "Truncated", data.Summary.Truncated.ToString().ToLowerInvariant());
        builder.AppendLine("      </dl>");

        builder.AppendLine("      <h3>Application coverage</h3>");
        builder.AppendLine("      <table data-filterable=\"true\" data-filter-name=\"Web Forms projects\"><caption>Packet-declared project coverage</caption><thead><tr><th>Project</th><th>Surfaces</th><th>Evidence IDs</th><th>Support IDs</th></tr></thead><tbody>");
        foreach (var row in data.Projects)
        {
            builder.AppendLine($"        <tr data-filter-row=\"true\"><th scope=\"row\">{Html(row.ProjectId)}</th><td>{row.SurfaceCount}</td><td>{Html(string.Join(", ", row.EvidenceIds))}</td><td>{Html(string.Join(", ", row.SupportIds))}</td></tr>");
        }
        if (data.Projects.Count == 0) builder.AppendLine("        <tr data-empty-row=\"true\"><td colspan=\"4\">No project rows were present under the compatible packet coverage.</td></tr>");
        builder.AppendLine("      </tbody></table>");

        builder.AppendLine("      <h3>Application surfaces and composition</h3>");
        builder.AppendLine("      <table data-filterable=\"true\" data-filter-name=\"Web Forms surfaces\"><caption>Packet-declared surfaces and bounded composition IDs</caption><thead><tr><th>Surface</th><th>Kind</th><th>Project</th><th>Composition targets</th><th>Controls</th><th>Evidence IDs</th><th>Support IDs</th></tr></thead><tbody>");
        foreach (var row in data.Surfaces)
        {
            builder.AppendLine($"        <tr data-filter-row=\"true\"><th scope=\"row\">{Html(row.SurfaceId)}</th><td>{Html(row.SurfaceKind)}</td><td>{Html(row.ProjectId)}</td><td>{Html(string.Join(", ", row.CompositionTargetIds))}</td><td>{Html(string.Join(", ", row.ControlIds))}</td><td>{Html(string.Join(", ", row.EvidenceIds))}</td><td>{Html(string.Join(", ", row.SupportIds))}</td></tr>");
        }
        if (data.Surfaces.Count == 0) builder.AppendLine("        <tr data-empty-row=\"true\"><td colspan=\"7\">No surface rows were present under the compatible packet coverage.</td></tr>");
        builder.AppendLine("      </tbody></table>");

        builder.AppendLine("      <h3>Event and handler chains</h3>");
        builder.AppendLine("      <table data-filterable=\"true\" data-filter-name=\"Web Forms event chains\"><caption>Static event/handler chain rows</caption><thead><tr><th>Chain</th><th>Surface</th><th>Event source</th><th>Handler</th><th>Classification</th><th>Terminal</th><th>Rules</th><th>Tiers</th><th>Coverage</th><th>Evidence IDs</th><th>Support IDs</th><th>Limitations</th></tr></thead><tbody>");
        foreach (var row in data.EventChains)
        {
            builder.AppendLine($"        <tr data-filter-row=\"true\"><th scope=\"row\">{Html(row.ChainId)}</th><td>{Html(row.SurfaceId)}</td><td>{Html(row.EventSourceId)}</td><td>{Html(row.HandlerId ?? "unresolved")}</td><td>{Html(row.Classification)}</td><td>{Html(row.TerminalKind ?? "unavailable")}</td><td>{Html(string.Join(", ", row.RuleIds))}</td><td>{Html(string.Join(", ", row.EvidenceTiers))}</td><td>{Html(string.Join(", ", row.CoverageLabels))}</td><td>{Html(string.Join(", ", row.EvidenceIds))}</td><td>{Html(string.Join(", ", row.SupportIds))}</td><td>{Html(string.Join(", ", row.LimitationIds))}</td></tr>");
        }
        if (data.EventChains.Count == 0) builder.AppendLine("        <tr data-empty-row=\"true\"><td colspan=\"12\">No event-chain rows were present under the compatible packet coverage.</td></tr>");
        builder.AppendLine("      </tbody></table>");

        builder.AppendLine("      <h3>Downstream boundaries</h3>");
        builder.AppendLine("      <table data-filterable=\"true\" data-filter-name=\"Web Forms downstream boundaries\"><caption>Static downstream-boundary rows</caption><thead><tr><th>Boundary</th><th>Chain</th><th>Surface</th><th>Category</th><th>Kind</th><th>Target</th><th>Classification</th><th>Rules</th><th>Tiers</th><th>Coverage</th><th>Evidence IDs</th><th>Support IDs</th><th>Limitations</th></tr></thead><tbody>");
        foreach (var row in data.DownstreamBoundaries)
        {
            builder.AppendLine($"        <tr data-filter-row=\"true\"><th scope=\"row\">{Html(row.BoundaryId)}</th><td>{Html(row.ChainId)}</td><td>{Html(row.SurfaceId)}</td><td>{Html(row.BoundaryCategory)}</td><td>{Html(row.BoundaryKind)}</td><td>{Html(row.BoundaryTargetId)}</td><td>{Html(row.Classification)}</td><td>{Html(string.Join(", ", row.RuleIds))}</td><td>{Html(string.Join(", ", row.EvidenceTiers))}</td><td>{Html(string.Join(", ", row.CoverageLabels))}</td><td>{Html(string.Join(", ", row.EvidenceIds))}</td><td>{Html(string.Join(", ", row.SupportIds))}</td><td>{Html(string.Join(", ", row.LimitationIds))}</td></tr>");
        }
        if (data.DownstreamBoundaries.Count == 0) builder.AppendLine("        <tr data-empty-row=\"true\"><td colspan=\"13\">No downstream-boundary rows were present under the compatible packet coverage.</td></tr>");
        builder.AppendLine("      </tbody></table>");

        builder.AppendLine("      <h3>Identity and session inventory</h3>");
        builder.AppendLine("      <table data-filterable=\"true\" data-filter-name=\"Web Forms identity and session inventory\"><caption>Bounded identity/session rows and safe metadata</caption><thead><tr><th>Identity row</th><th>Kind</th><th>Classification</th><th>Surface</th><th>Safe metadata</th><th>Evidence</th><th>Support IDs</th><th>Limitations</th></tr></thead><tbody>");
        foreach (var row in data.IdentityStateInventory)
        {
            var metadata = string.Join(", ", row.SafeMetadata.Select(pair => $"{pair.Key}={pair.Value}"));
            builder.AppendLine($"        <tr data-filter-row=\"true\"><th scope=\"row\">{Html(row.IdentityStateId)}</th><td>{Html(row.IdentityKind)}</td><td>{Html(row.Classification)}</td><td>{Html(row.SurfaceId ?? "unavailable")}</td><td>{Html(metadata)}</td><td>{Html(row.EvidenceId)}</td><td>{Html(string.Join(", ", row.SupportIds))}</td><td>{Html(string.Join(", ", row.LimitationIds))}</td></tr>");
        }
        if (data.IdentityStateInventory.Count == 0) builder.AppendLine("        <tr data-empty-row=\"true\"><td colspan=\"8\">No identity/session rows were present under the compatible packet coverage.</td></tr>");
        builder.AppendLine("      </tbody></table>");

        builder.AppendLine("      <h3>Batch and data-movement inventory</h3>");
        builder.AppendLine("      <p>These are static declarations only. They do not prove scheduling, execution, successful or complete movement, retries, idempotency, monitoring, target state, or production use.</p>");
        builder.AppendLine("      <table data-filterable=\"true\" data-filter-name=\"Web Forms batch and data-movement inventory\"><caption>Packet-preserved batch and data-movement declarations</caption><thead><tr><th>Declaration</th><th>Surface</th><th>Mechanism</th><th>Operation</th><th>Owner</th><th>Project resolution</th><th>Project</th><th>Safe metadata</th><th>Evidence</th><th>Support IDs</th><th>Limitations</th></tr></thead><tbody>");
        foreach (var row in data.BatchDataMovementInventory)
        {
            var metadata = string.Join(", ", row.SafeMetadata.Select(pair => $"{pair.Key}={pair.Value}"));
            builder.AppendLine($"        <tr data-filter-row=\"true\"><th scope=\"row\">{Html(row.BatchDataMovementId)}</th><td>{Html(row.SurfaceKind)}</td><td>{Html(row.Mechanism)}</td><td>{Html(row.OperationKind)}</td><td>{Html(row.OwnerStatus)}</td><td>{Html(row.ProjectResolution)}</td><td>{Html(row.ProjectId ?? "unavailable")}</td><td>{Html(metadata)}</td><td>{Html(row.EvidenceId)}</td><td>{Html(string.Join(", ", row.SupportIds))}</td><td>{Html(string.Join(", ", row.LimitationIds))}</td></tr>");
        }
        if (data.BatchDataMovementInventory.Count == 0) builder.AppendLine("        <tr data-empty-row=\"true\"><td colspan=\"11\">No supported batch/data-movement declaration was present. This is not proof of absence.</td></tr>");
        builder.AppendLine("      </tbody></table>");

        builder.AppendLine("      <h3>Structural slice candidates</h3>");
        builder.AppendLine("      <p>These are static structural candidates requiring owner naming and validation; they are not capability, parity, scope, effort, or architecture conclusions.</p>");
        builder.AppendLine("      <table data-filterable=\"true\" data-filter-name=\"Web Forms structural candidates\"><caption>Packet-preserved structural candidates</caption><thead><tr><th>Candidate</th><th>Classification</th><th>Rule</th><th>Tier</th><th>Owner label</th><th>Naming required</th><th>Surfaces</th><th>Coverage</th><th>Evidence IDs</th><th>Support IDs</th><th>Limitations</th></tr></thead><tbody>");
        foreach (var row in data.StructuralCandidates)
        {
            builder.AppendLine($"        <tr data-filter-row=\"true\"><th scope=\"row\">{Html(row.CandidateId)}</th><td>{Html(row.Classification)}</td><td>{Html(row.RuleId)}</td><td>{Html(row.EvidenceTier)}</td><td>{Html(row.OwnerLabel ?? "not-provided")}</td><td>{row.OwnerNamingRequired.ToString().ToLowerInvariant()}</td><td>{Html(string.Join(", ", row.SurfaceIds))}</td><td>{Html(string.Join(", ", row.CoverageLabels))}</td><td>{Html(string.Join(", ", row.EvidenceIds))}</td><td>{Html(string.Join(", ", row.SupportIds))}</td><td>{Html(string.Join(", ", row.LimitationIds))}</td></tr>");
        }
        if (data.StructuralCandidates.Count == 0) builder.AppendLine("        <tr data-empty-row=\"true\"><td colspan=\"11\">No structural-candidate rows were present under the compatible packet coverage.</td></tr>");
        builder.AppendLine("      </tbody></table>");

        builder.AppendLine("      <h3>Owner questions</h3><ol>");
        foreach (var question in data.OwnerQuestions)
        {
            builder.AppendLine("        <li>");
            builder.AppendLine($"          {Html(question)}");
            builder.AppendLine("        </li>");
        }
        if (data.OwnerQuestions.Count == 0) builder.AppendLine("        <li>No owner questions were present in the compatible packet.</li>");
        builder.AppendLine("      </ol>");
        builder.AppendLine("    </section>");
    }

    private sealed record WebFormsExplorerLoad(
        ExplorerWebFormsData? Data,
        string? CommitSha,
        ExplorerInputArtifact? Artifact,
        ExplorerSource? Source,
        IReadOnlyList<ExplorerEvidenceRow> EvidenceRows,
        IReadOnlyList<ExplorerGap> Gaps,
        IReadOnlyList<ExplorerLimitation> Limitations,
        IReadOnlyList<string> CoverageLabels)
    {
        public static WebFormsExplorerLoad Empty { get; } = new(null, null, null, null, [], [], [], []);
    }
}
