# Combined Dependency Diff Tasks

## Implementation Tasks

- [ ] Confirm MVP command and scope.
  - [ ] Confirm command shape: `tracemap diff --before <combined.sqlite> --after <combined.sqlite> --out <path>`.
  - [ ] Confirm default scopes are sources, coverage, endpoints, surfaces, and edges.
  - [ ] Confirm `--include-paths` is required for path diffing.
  - [ ] Confirm selectors: `--source`, `--endpoint`, `--surface`, `--surface-name`, `--scope`, `--max-depth`, `--max-paths`, and `--max-frontier`.
  - [ ] Confirm path comparison reuses `tracemap paths` semantics and limits.
  - [ ] Confirm non-combined indexes are rejected.
  - [ ] Confirm both inputs are opened read-only.

- [ ] Refactor shared combined readers if needed.
  - [ ] Identify reusable pieces from combined report and path query code.
  - [ ] Extract combined index validation without changing report/path behavior.
  - [ ] Extract source inventory and coverage reading if not already shared.
  - [ ] Reuse endpoint matching from the existing combined report/path implementation.
  - [ ] Reuse dependency surface projection where possible.
  - [ ] Reuse safe path and Markdown escaping helpers.
  - [ ] Preserve existing report/path output before adding diff behavior.

- [ ] Add diff models.
  - [ ] Define query, snapshot, summary, diff row, evidence, caveat, gap, and limitation models.
  - [ ] Define JSON version `1.0`.
  - [ ] Define closed-set classification values.
  - [ ] Define confidence mapping.
  - [ ] Define deterministic classification ordering.
  - [ ] Ensure required JSON fields use `null` or empty arrays consistently.
  - [ ] Ensure no generated timestamp is emitted.

- [ ] Add snapshot projector.
  - [ ] Read and normalize source inventory.
  - [ ] Project comparable endpoint records.
  - [ ] Project comparable dependency surface records.
  - [ ] Project comparable dependency edge records.
  - [ ] Optionally project comparable path records through the path query engine.
  - [ ] Preserve fact IDs, edge IDs, scan IDs, commit SHAs, rule IDs, evidence tiers, file paths, and line spans.
  - [ ] Parse JSON property bags defensively.
  - [ ] Strip or hash unsafe values.
  - [ ] Emit schema errors with side-specific table names.

- [ ] Implement stable identity construction.
  - [ ] Pair sources by exact source label.
  - [ ] Build source identity summaries and identity-change warnings.
  - [ ] Build endpoint stable keys from source label, endpoint kind, method, normalized path key, and handler identity where available.
  - [ ] Build surface stable keys from source label, surface kind, normalized metadata, and structured metadata hash.
  - [ ] Build edge stable keys from source label, edge kind, source identity, target identity, rule family, and metadata hash.
  - [ ] Build path signatures from ordered node/edge descriptors and terminal surface identity.
  - [ ] Ignore volatile database row IDs when comparing.
  - [ ] Detect duplicate stable identities within each side and kind.
  - [ ] Emit `DuplicateIdentity` gaps.

- [ ] Implement selector filtering.
  - [ ] Filter sources by exact source label.
  - [ ] Filter endpoints by method and normalized path key.
  - [ ] Filter surfaces by kind.
  - [ ] Filter surface names by exact case-insensitive match.
  - [ ] Support leading/trailing `*` wildcard semantics for surface names.
  - [ ] Apply selectors symmetrically to both before and after snapshots.
  - [ ] Emit `SelectorNoMatch` when selectors match neither snapshot.
  - [ ] Validate unsupported or invalid selector combinations with clear errors.

- [ ] Implement diff engine.
  - [ ] Compare source key sets.
  - [ ] Compare coverage metadata for paired sources.
  - [ ] Compare endpoint key sets.
  - [ ] Compare surface key sets.
  - [ ] Compare edge key sets.
  - [ ] Compare path signature sets when `--include-paths` is set.
  - [ ] Classify `Added`, `Removed`, and `ChangedEvidence`.
  - [ ] Apply `AddedWithBeforeGap` and `RemovedWithAfterGap` downgrade rules.
  - [ ] Apply `NeedsReviewDiff` for ambiguous, duplicate, hashed, syntax-only, or name-only identities.
  - [ ] Apply `UnknownAnalysisGap` when source identity, commit SHA, schema, or coverage prevents credible comparison.
  - [ ] Emit `NoDiffEvidence` when comparable evidence exists and no changes are found.
  - [ ] Sort diff rows deterministically.

- [ ] Add Markdown writer.
  - [ ] Render sections: Summary, Compared Snapshots, Sources, Coverage Changes, Endpoint Diffs, Surface Diffs, Edge Diffs, Path Diffs, Gaps, Limitations.
  - [ ] Render path-not-run notice when `--include-paths` is omitted.
  - [ ] Render coverage caveats near affected rows.
  - [ ] Render safe before/after evidence summaries.
  - [ ] Render deterministic row caps and truncation notices.
  - [ ] Escape Markdown table/link delimiters.
  - [ ] Avoid raw SQL, raw URLs, config values, connection strings, raw snippets, and local absolute paths.

- [ ] Add JSON writer.
  - [ ] Emit stable top-level shape.
  - [ ] Include normalized query metadata.
  - [ ] Include before and after snapshot metadata.
  - [ ] Include source, coverage, endpoint, surface, edge, and path diffs.
  - [ ] Include gaps and limitations.
  - [ ] Exclude timestamps and raw input property bags.
  - [ ] Use `null` and empty arrays consistently.
  - [ ] Produce byte-stable output for identical inputs.

- [ ] Wire CLI.
  - [ ] Add `tracemap diff --help`.
  - [ ] Parse `--before`, `--after`, `--out`, `--format`, `--scope`, `--include-paths`, selectors, and path limits.
  - [ ] Validate required arguments.
  - [ ] Validate scope values.
  - [ ] Treat missing-extension output paths as directories.
  - [ ] Print useful completion summary.
  - [ ] Return non-zero exit codes for invalid inputs and schema errors.

- [ ] Add tests.
  - [ ] Non-combined input rejection for before and after.
  - [ ] Missing required table names side-specific schema error.
  - [ ] Read-only database byte unchanged after diff.
  - [ ] Markdown output.
  - [ ] JSON output.
  - [ ] Byte-stable repeated output.
  - [ ] Source added.
  - [ ] Source removed.
  - [ ] Source identity changed.
  - [ ] Coverage changed.
  - [ ] Endpoint added.
  - [ ] Endpoint removed.
  - [ ] Endpoint changed evidence.
  - [ ] Surface added.
  - [ ] Surface removed.
  - [ ] Surface changed evidence.
  - [ ] Edge added.
  - [ ] Edge removed.
  - [ ] Stable identity ignores volatile row ID churn.
  - [ ] Metadata hash change produces `ChangedEvidence`.
  - [ ] Reduced before coverage produces `AddedWithBeforeGap`.
  - [ ] Reduced after coverage produces `RemovedWithAfterGap`.
  - [ ] Unknown commit SHA produces `UnknownAnalysisGap` where relevant.
  - [ ] Duplicate stable identities produce gap and down-ranked classification.
  - [ ] `--include-paths` reports added path signatures.
  - [ ] `--include-paths` reports removed path signatures.
  - [ ] Omitted `--include-paths` renders path-not-run notice.
  - [ ] `--source` filters both snapshots.
  - [ ] `--endpoint` filters endpoint and path diffs.
  - [ ] `--surface` filters surface and path diffs.
  - [ ] `--surface-name` exact and wildcard matching.
  - [ ] Selector no-match gap.
  - [ ] Markdown escaping for pipes, line endings, brackets, and parentheses.
  - [ ] No raw SQL, raw URL, config value, connection string, snippet, local absolute path, or private repo name output.

- [ ] Update docs.
  - [ ] README quickstart for `combine -> report -> paths -> diff`.
  - [ ] `docs/ACCEPTANCE.md` diff acceptance criteria.
  - [ ] `docs/VALIDATION.md` local smoke command for diffing two public sample snapshots.
  - [ ] `docs/LANGUAGE_ADAPTER_CONTRACT.md` if diff identity requirements expose adapter contract gaps.
  - [ ] Rule catalog entries for new `combined.diff.*.v1` rule IDs.

- [ ] Validate.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`
  - [ ] `dotnet test src/dotnet/TraceMap.sln`
  - [ ] `./scripts/check-private-paths.sh`
  - [ ] `git diff --check`

## Deferred Follow-Ups

- HTML graph diff viewer.
- SARIF output for CI annotations.
- Persisted diff rows.
- Batch diff matrix for many commits or branches.
- Single-language index diffing.
- Source label rename maps.
- Deeper SQL parser-backed table, column, procedure, view, and function diffing.
- Package lockfile resolver diffing.
- Runtime trace import and static/runtime comparison.
