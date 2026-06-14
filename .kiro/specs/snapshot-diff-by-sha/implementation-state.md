# Snapshot Diff By SHA Implementation State

Status: mostly implemented; single-index endpoint/surface follow-up slice implemented locally and pending PR review.

Branch/PR:

- Implemented across snapshot-diff work already merged into `dev`.
- Current follow-up branch: `codex/snapshot-diff-single-index-followups`.

Scope Implemented:

- `tracemap snapshot-diff --before <path> --after <path> --out <path>`.
- Single-index and combined-index input detection.
- Snapshot metadata, identity, commit SHA, coverage, schema, and extractor validation.
- Combined-index delegation to the combined diff engine for source, coverage, endpoint, surface, edge, graph, and opt-in path evidence.
- Deterministic Markdown and JSON output.
- Snapshot-specific rule catalog entries and limitations.
- Redaction for raw URLs, repository names, local roots, private paths, unsafe values, and delegated combined metadata.
- Current follow-up slice adds single-index endpoint diffs for `HttpRouteBinding` and `HttpCallDetected` facts using the required `endpoint:{sourceLabel}:{endpointKind}:{normalizedMethod}:{normalizedPathKey}:{handlerIdentityOrNone}` stable-key shape.
- Current follow-up slice adds single-index surface diffs for safe dependency-surface facts already understood by `CombinedDependencyReporter.BuildSurfaces`, including SQL/query, package/config, HTTP route/client, config-binding, and related surface rows where those facts exist.
- Current follow-up slice emits `MalformedMetadataGap` for malformed `scan_manifest.manifest_json`, combined `index_sources.manifest_json`, and single-index `facts.properties_json`, omitting unsafe malformed metadata while continuing with reduced coverage.
- Current follow-up slice adds `SameCommitShaDivergentEvidence` row notes with `snapshot.diff.identity.v1` when single-index endpoint/surface evidence changes while paired snapshots report the same known commit SHA.

Open Follow-Ups:

- Single-index contract-shape projectors for type/property/method/DTO evidence.
- Single-index graph projectors for call edges, object creations, symbol relationships, argument flows, and parameter forwarding.
- Single-index analysis-gap diffs from `AnalysisGap` facts beyond coverage summaries and malformed metadata gaps.
- Expanded surface projector support if future adapters add storage or event/message fact vocabularies outside the current combined surface reader.
- Duplicate-identity edge-case tests for single-index endpoint and surface records.
- Adapter-specific validation if future projector work changes language adapter outputs. This slice consumes existing facts and does not change adapter output behavior, so adapter tests are deferred.

Validation:

- Focused snapshot diff tests: `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter SnapshotDiffTests` passes locally on this branch.
- Solution build: `dotnet build src/dotnet/TraceMap.sln` passes locally.
- Full solution tests: `dotnet test src/dotnet/TraceMap.sln` passes locally.
- Private-path guard: `./scripts/check-private-paths.sh` passes locally.
- Whitespace check: `git diff --check` passes locally.
