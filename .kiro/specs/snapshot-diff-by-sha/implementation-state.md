# Snapshot Diff By SHA Implementation State

Status: mostly implemented; single-index gap-diff follow-up implemented locally and pending PR review.

Branch/PR:

- Implemented across snapshot-diff work already merged into `dev`.
- Current follow-up branch: `codex/snapshot-diff-gap-diffs`.

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
- This branch adds single-index `AnalysisGap` fact comparison for `gapDiffs`, preserving `snapshot.diff.evidence.v1`, source fact rule IDs, evidence tiers, supporting fact IDs, safe file spans, and hashed raw messages.

Open Follow-Ups:

- Single-index contract-shape projectors for type/property/method/DTO evidence.
- Single-index graph projectors for call edges, object creations, symbol relationships, argument flows, and parameter forwarding.
- Expanded surface projector support if future adapters add storage or event/message fact vocabularies outside the current combined surface reader.
- Duplicate-identity edge-case tests for single-index endpoint and surface records.
- Adapter-specific validation if future projector work changes language adapter outputs. This slice consumes existing facts and does not change adapter output behavior, so adapter tests are deferred.

Scope Decisions:

- Gap diff rows expose safe `gapKind`, file spans, source labels, rule IDs, evidence tiers, and deterministic hashes.
- Raw `AnalysisGap.message` text is not rendered and is not used in cleartext stable keys because messages can contain exception text, SQL, local paths, or config values.
- Gap diff classifications remain `UnknownAnalysisGap`; the row says indexed analysis coverage changed, not that product behavior changed.
- Combined-index `gapDiffs` remain unavailable until a combined gap projector is specified.

Validation:

- Focused snapshot diff tests: `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter SnapshotDiffTests` passed locally on this branch, including duplicate same-span gap rows and existing adapter-provided `messageHash` fallback coverage.
- Solution build: `dotnet build src/dotnet/TraceMap.sln` passed locally.
- Full solution tests: `dotnet test src/dotnet/TraceMap.sln` passed locally with 241 passing tests after PR review fixes.
- CLI sample smoke: `dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/modern-sample --out /tmp/tracemap-snapshot-gap-smoke` passed and emitted `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log`.
- Private-path guard: `./scripts/check-private-paths.sh` passed locally.
- Whitespace check: `git diff --check` passed locally.

Review:

- Local `kiro` is installed but only exposed an editor/chat launcher path in this shell.
- Local `claude --model sonnet` was installed but unavailable because the CLI is not logged in.
- Completed a bounded self-review; fixed one finding where gap-diff-only reports could roll up `UnknownAnalysisGap` while `reportCoverage` stayed `Full`, and aligned snapshot confidence strings for unknown/review-tier classifications.
- PR review loop fixed Qodo/Codex findings by preserving existing adapter-provided `messageHash` fingerprints and including the safe message fingerprint in gap stable keys to avoid same-span duplicate identity collisions.
