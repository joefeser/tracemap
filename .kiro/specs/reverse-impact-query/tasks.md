# Reverse Impact Query Tasks

## First PR Boundary

Recommended first implementation PR: complete tasks 1 through 8 plus the identity checks from task 10 for selected surfaces and endpoint roots. Rule catalog updates are already introduced by this spec PR and must stay current with the first implementation PR. Tasks 9 and the remaining task 10 caveats can land in the same PR if the graph refactor stays small; otherwise ship symbol/source/all targets in a follow-up. If task 2 requires non-trivial changes to shared path infrastructure, open a refactor-only PR for task 2 with byte-stable regression tests and smoke validation before opening the reverse feature PR.

## Implementation Tasks

- [x] 1. Confirm MVP command and terminology. Requirements: 1, 2, 4, 6.
  - [x] Confirm command name: `tracemap reverse`.
  - [x] Confirm "reverse" means static dependency-surface-to-root reachability, not runtime usage.
  - [x] Confirm input is one combined SQLite database.
  - [x] Confirm outputs are `reverse-report.md` and `reverse-report.json`.
  - [x] Confirm default target is `--to endpoints`.
  - [x] Confirm default caps for surfaces, depth, frontier, roots, paths per root, and gaps.
  - [x] Confirm `NoReversePathEvidence` requires credible full coverage.

- [x] 2. Refactor reusable path graph inventory without behavior changes. Requirements: 3, 5, 8, 10.
  - [x] Expose an internal path graph inventory builder that does not write files.
  - [x] Preserve current `tracemap paths` output exactly.
  - [x] Preserve current `tracemap impact --include-paths` behavior if it uses the path query engine.
  - [x] Share endpoint matching, source provenance, safe rendering, hashing, and output-path helpers.
  - [x] Add regression tests proving `tracemap paths` output remains byte-stable.

- [x] 3. Add reverse report models. Requirements: 3, 7, 8, 9.
  - [x] Define query, snapshot, summary, selected surface, reverse root, reverse path, gap, and limitation models.
  - [x] Define JSON version `1.0`.
  - [x] Include `reportType = combined-reverse-query`.
  - [x] Include source label, scan ID, commit SHA, rule ID, evidence tier, file span, supporting fact IDs, and supporting edge IDs where available.
  - [x] Use empty arrays and nulls consistently.

- [x] 4. Add selector handling. Requirements: 2, 6.
  - [x] Parse `--source`.
  - [x] Parse `--surface`.
  - [x] Parse `--surface-name`.
  - [x] Parse `--to`.
  - [x] Parse `--max-surfaces`.
  - [x] Validate allowed surface kinds.
  - [x] Validate allowed target kinds.
  - [x] Fail clearly for valid `--to` modes that are not implemented in the current release.
  - [x] Use exact case-insensitive reverse surface-name matching rules for MVP.
  - [x] Emit `SelectorNoMatch` for empty selector results.
  - [x] Record broad selectors in query metadata when `--surface-name` is used without `--surface`.

- [x] 5. Add selected-surface projection. Requirements: 2, 4, 5, 7, 8, 9.
  - [x] Select terminal dependency surfaces from graph inventory and combined facts.
  - [x] Generate stable surface IDs independent of row order.
  - [x] Carry safe structured metadata only.
  - [x] Classify selected surfaces.
  - [x] Downgrade duplicate, ambiguous, or Tier3 matches.
  - [x] Emit coverage and identity caveats.

- [x] 6. Implement endpoint-root reverse traversal. Requirements: 3, 4, 5, 6.
  - [x] Build reverse adjacency from path graph edges.
  - [x] Traverse from selected surfaces to endpoint roots.
  - [x] Respect `--max-surfaces`.
  - [x] Respect `--max-depth`.
  - [x] Respect `--max-frontier`.
  - [x] Respect `--max-roots`.
  - [x] Respect `--max-paths-per-root`.
  - [x] Deduplicate paths by stable path signature.
  - [x] Render paths root-to-surface.
  - [x] Sort roots and paths deterministically.

- [x] 7. Add reverse classifications and gaps. Requirements: 4, 5, 9.
  - [x] Add fixed classification constants.
  - [x] Add confidence mapping.
  - [x] Classify Tier1 semantic paths as `StrongStaticReversePath` only under credible full coverage.
  - [x] Classify structural paths as `ProbableStaticReversePath`.
  - [x] Classify ambiguous, syntax/textual, fallback, duplicate-identity paths as `NeedsReviewReversePath`.
  - [x] Emit `NoReversePathEvidence` only under credible full coverage.
  - [x] Emit `UnknownAnalysisGap` when gaps or reduced coverage prevent no-path conclusions.
  - [x] Emit `TruncatedByLimit` for depth, frontier, root, path, and gap caps.

- [x] 8. Add Markdown, JSON, and CLI. Requirements: 1, 6, 7, 8.
  - [x] Emit stable top-level JSON shape.
  - [x] Sort arrays deterministically.
  - [x] Sort metadata keys deterministically.
  - [x] Exclude timestamps and unsafe raw values.
  - [x] Add Markdown sections: Summary, Query, Snapshot Sources, Selected Surfaces, Reverse Roots, Paths, Gaps, Limitations.
  - [x] Escape Markdown table/link delimiters.
  - [x] Add `tracemap reverse --help`.
  - [x] Validate required arguments.
  - [x] Validate numeric caps.
  - [x] Print completion summary.
  - [x] Implement opt-in `--exit-code`.

- [x] 9. Add symbol/source/all targets. Requirements: 3, 4, 6.
  - [x] Implement `--to symbols`.
  - [x] Implement `--to sources`.
  - [x] Implement `--to all`.
  - [x] Avoid duplicate path evidence when multiple target groups share the same path.
  - [x] Add target-specific grouping summaries.
  - [x] Ensure `--to endpoints` does not silently return symbols when endpoint roots are unavailable.

- [x] 10. Add identity and schema caveats. Requirements: 2, 5, 9.
  - [x] Detect duplicate stable selected surfaces in the first PR that emits selected surfaces.
  - [x] Detect duplicate stable roots in the first PR that emits reverse roots.
  - [x] Detect duplicate stable path nodes in the first PR that emits reverse paths.
  - [x] Detect unverified source identity or missing commit SHA in the first PR that emits reverse roots or paths.
  - [x] Preserve missing optional precision schema gaps.
  - [x] Downgrade affected rows and paths.

- [x] 11. Update rule catalog and docs. Requirements: 7, 9.
  - [x] Verify `combined.reverse.surface.v1`.
  - [x] Verify `combined.reverse.root.v1`.
  - [x] Verify `combined.reverse.path.v1`.
  - [x] Verify `combined.reverse.selector.v1`.
  - [x] Verify `combined.reverse.truncation.v1`.
  - [x] Verify `combined.reverse.identity.v1`.
  - [x] Document propagated `combined.paths.*.v1` supporting rule IDs.
  - [x] Document propagated `combined.diff.identity.v1` identity caveats.
  - [x] Document limitations for every new rule.
  - [x] Update README quickstart.
  - [x] Add a reverse-command entry to `docs/ACCEPTANCE.md` with expected artifacts, output paths, safety checks, and coverage conditions.
  - [x] Update `docs/VALIDATION.md`.
  - [x] Confirm whether `./scripts/smoke-combined-paths.sh` should gain a reverse step or whether a new reverse smoke script is warranted.

- [x] 12. Add tests. Requirements: 1-10.
  - [x] Non-combined input rejection.
  - [x] Directory output writes Markdown and JSON.
  - [x] File output respects `--format`.
  - [x] Byte-stable repeated output.
  - [x] Input database is not mutated.
  - [x] SQL surface reverse query returns endpoint root.
  - [x] HTTP client surface selection omits raw URLs.
  - [x] HTTP route surface selection preserves safe route metadata.
  - [x] Package/config surface selection omits config values.
  - [x] Repository remote URLs are omitted or hashed in Markdown and JSON.
  - [x] Selector no match emits a gap.
  - [x] `--surface-name` without `--surface` matches multiple surface kinds and records broad selector metadata.
  - [x] Reduced coverage downgrades no-path conclusion to `UnknownAnalysisGap`.
  - [x] Full credible coverage with no path emits `NoReversePathEvidence`.
  - [x] Depth cap emits truncation gap.
  - [x] Frontier cap emits truncation gap.
  - [x] Root cap emits truncation gap.
  - [x] Per-root path cap emits truncation gap.
  - [x] Gap cap emits truncation gap.
  - [x] Duplicate stable identity cites duplicate identity gap and downgrades strong classification.
  - [x] Missing source identity or commit SHA emits warning.
  - [x] Forward `tracemap paths` endpoint-to-surface evidence can be found by matching reverse surface-to-endpoint query under the same coverage assumptions.
  - [x] `--to endpoints`, `--to symbols`, `--to sources`, and `--to all` grouping semantics.
  - [x] `--to all` does not duplicate identical path evidence across endpoint, symbol, and source groups.
  - [x] `--exit-code` returns 1 with reverse roots or paths and 0 without roots or paths.
  - [x] Markdown and JSON omit unsafe values.

- [x] 13. Validate. Requirements: 10.
  - [x] `dotnet build src/dotnet/TraceMap.sln`
  - [x] `dotnet test src/dotnet/TraceMap.sln`
  - [x] `./scripts/check-private-paths.sh`
  - [x] `git diff --check`
  - [x] Pinned combined-path smoke checks from `docs/VALIDATION.md` if traversal internals change.

## Deferred Follow-Ups

- Batch query file mode for multiple surfaces.
- `--from-fact-id` or `--from-stable-key` expert selectors.
- HTML graph viewer.
- Persisted reverse query rows behind explicit `--write-derived`.
- Reverse queries over two snapshots with before/after comparison.
