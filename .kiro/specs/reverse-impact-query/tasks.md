# Reverse Impact Query Tasks

## First PR Boundary

Recommended first implementation PR: complete tasks 1 through 8 plus task 11 for selected surfaces and endpoint roots. Rule catalog updates must land with the first PR that emits reverse rule IDs. Tasks 9 and 10 can land in the same PR if the graph refactor stays small; otherwise ship symbol/source/all targets in a follow-up. If task 2 requires non-trivial changes to shared path infrastructure, open a refactor-only PR for task 2 with byte-stable regression tests and smoke validation before opening the reverse feature PR.

## Implementation Tasks

- [ ] 1. Confirm MVP command and terminology. Requirements: 1, 2, 4, 6.
  - [ ] Confirm command name: `tracemap reverse`.
  - [ ] Confirm "reverse" means static dependency-surface-to-root reachability, not runtime usage.
  - [ ] Confirm input is one combined SQLite database.
  - [ ] Confirm outputs are `reverse-report.md` and `reverse-report.json`.
  - [ ] Confirm default target is `--to endpoints`.
  - [ ] Confirm default caps for surfaces, depth, frontier, roots, paths per root, and gaps.
  - [ ] Confirm `NoReversePathEvidence` requires credible full coverage.

- [ ] 2. Refactor reusable path graph inventory without behavior changes. Requirements: 3, 5, 8, 10.
  - [ ] Expose an internal path graph inventory builder that does not write files.
  - [ ] Preserve current `tracemap paths` output exactly.
  - [ ] Preserve current `tracemap impact --include-paths` behavior if it uses the path query engine.
  - [ ] Share endpoint matching, source provenance, safe rendering, hashing, and output-path helpers.
  - [ ] Add regression tests proving `tracemap paths` output remains byte-stable.

- [ ] 3. Add reverse report models. Requirements: 3, 7, 8, 9.
  - [ ] Define query, snapshot, summary, selected surface, reverse root, reverse path, gap, and limitation models.
  - [ ] Define JSON version `1.0`.
  - [ ] Include `reportType = combined-reverse-query`.
  - [ ] Include source label, scan ID, commit SHA, rule ID, evidence tier, file span, supporting fact IDs, and supporting edge IDs where available.
  - [ ] Use empty arrays and nulls consistently.

- [ ] 4. Add selector handling. Requirements: 2, 6.
  - [ ] Parse `--source`.
  - [ ] Parse `--surface`.
  - [ ] Parse `--surface-name`.
  - [ ] Parse `--to`.
  - [ ] Parse `--max-surfaces`.
  - [ ] Validate allowed surface kinds.
  - [ ] Validate allowed target kinds.
  - [ ] Fail clearly for valid `--to` modes that are not implemented in the current release.
  - [ ] Use existing safe surface-name matching rules.
  - [ ] Emit `SelectorNoMatch` for empty selector results.
  - [ ] Record broad selectors in query metadata when `--surface-name` is used without `--surface`.

- [ ] 5. Add selected-surface projection. Requirements: 2, 4, 5, 7, 8, 9.
  - [ ] Select terminal dependency surfaces from graph inventory and combined facts.
  - [ ] Generate stable surface IDs independent of row order.
  - [ ] Carry safe structured metadata only.
  - [ ] Classify selected surfaces.
  - [ ] Downgrade duplicate, ambiguous, or Tier3 matches.
  - [ ] Emit coverage and identity caveats.

- [ ] 6. Implement endpoint-root reverse traversal. Requirements: 3, 4, 5, 6.
  - [ ] Build reverse adjacency from path graph edges.
  - [ ] Traverse from selected surfaces to endpoint roots.
  - [ ] Respect `--max-surfaces`.
  - [ ] Respect `--max-depth`.
  - [ ] Respect `--max-frontier`.
  - [ ] Respect `--max-roots`.
  - [ ] Respect `--max-paths-per-root`.
  - [ ] Deduplicate paths by stable path signature.
  - [ ] Render paths root-to-surface.
  - [ ] Sort roots and paths deterministically.

- [ ] 7. Add reverse classifications and gaps. Requirements: 4, 5, 9.
  - [ ] Add fixed classification constants.
  - [ ] Add confidence mapping.
  - [ ] Classify Tier1 semantic paths as `StrongStaticReversePath` only under credible full coverage.
  - [ ] Classify structural paths as `ProbableStaticReversePath`.
  - [ ] Classify ambiguous, syntax/textual, fallback, duplicate-identity paths as `NeedsReviewReversePath`.
  - [ ] Emit `NoReversePathEvidence` only under credible full coverage.
  - [ ] Emit `UnknownAnalysisGap` when gaps or reduced coverage prevent no-path conclusions.
  - [ ] Emit `TruncatedByLimit` for depth, frontier, root, path, and gap caps.

- [ ] 8. Add Markdown, JSON, and CLI. Requirements: 1, 6, 7, 8.
  - [ ] Emit stable top-level JSON shape.
  - [ ] Sort arrays deterministically.
  - [ ] Sort metadata keys deterministically.
  - [ ] Exclude timestamps and unsafe raw values.
  - [ ] Add Markdown sections: Summary, Query, Snapshot Sources, Selected Surfaces, Reverse Roots, Paths, Gaps, Limitations.
  - [ ] Escape Markdown table/link delimiters.
  - [ ] Add `tracemap reverse --help`.
  - [ ] Validate required arguments.
  - [ ] Validate numeric caps.
  - [ ] Print completion summary.
  - [ ] Implement opt-in `--exit-code`.

- [ ] 9. Add symbol/source/all targets. Requirements: 3, 4, 6.
  - [ ] Implement `--to symbols`.
  - [ ] Implement `--to sources`.
  - [ ] Implement `--to all`.
  - [ ] Avoid duplicate path evidence when multiple target groups share the same path.
  - [ ] Add target-specific grouping summaries.
  - [ ] Ensure `--to endpoints` does not silently return symbols when endpoint roots are unavailable.

- [ ] 10. Add identity and schema caveats. Requirements: 2, 5, 9.
  - [ ] Detect duplicate stable selected surfaces.
  - [ ] Detect duplicate stable roots.
  - [ ] Detect duplicate stable path nodes where relevant.
  - [ ] Detect unverified source identity or missing commit SHA.
  - [ ] Preserve missing optional precision schema gaps.
  - [ ] Downgrade affected rows and paths.

- [ ] 11. Update rule catalog and docs. Requirements: 7, 9.
  - [ ] Add `combined.reverse.surface.v1`.
  - [ ] Add `combined.reverse.root.v1`.
  - [ ] Add `combined.reverse.path.v1`.
  - [ ] Add `combined.reverse.selector.v1`.
  - [ ] Add `combined.reverse.truncation.v1`.
  - [ ] Add `combined.reverse.identity.v1`.
  - [ ] Document propagated `combined.paths.*.v1` supporting rule IDs.
  - [ ] Document propagated `combined.diff.identity.v1` identity caveats.
  - [ ] Document limitations for every new rule.
  - [ ] Update README quickstart.
  - [ ] Add a reverse-command entry to `docs/ACCEPTANCE.md` with expected artifacts, output paths, safety checks, and coverage conditions.
  - [ ] Update `docs/VALIDATION.md`.
  - [ ] Confirm whether `./scripts/smoke-combined-paths.sh` should gain a reverse step or whether a new reverse smoke script is warranted.

- [ ] 12. Add tests. Requirements: 1-10.
  - [ ] Non-combined input rejection.
  - [ ] Directory output writes Markdown and JSON.
  - [ ] File output respects `--format`.
  - [ ] Byte-stable repeated output.
  - [ ] Input database is not mutated.
  - [ ] SQL surface reverse query returns endpoint root.
  - [ ] HTTP client surface selection omits raw URLs.
  - [ ] HTTP route surface selection preserves safe route metadata.
  - [ ] Package/config surface selection omits config values.
  - [ ] Repository remote URLs are omitted or hashed in Markdown and JSON.
  - [ ] Selector no match emits a gap.
  - [ ] `--surface-name` without `--surface` matches multiple surface kinds and records broad selector metadata.
  - [ ] Reduced coverage downgrades no-path conclusion to `UnknownAnalysisGap`.
  - [ ] Full credible coverage with no path emits `NoReversePathEvidence`.
  - [ ] Depth cap emits truncation gap.
  - [ ] Frontier cap emits truncation gap.
  - [ ] Root cap emits truncation gap.
  - [ ] Per-root path cap emits truncation gap.
  - [ ] Gap cap emits truncation gap.
  - [ ] Duplicate stable identity cites duplicate identity gap and downgrades strong classification.
  - [ ] Missing source identity or commit SHA emits warning.
  - [ ] Forward `tracemap paths` endpoint-to-surface evidence can be found by matching reverse surface-to-endpoint query under the same coverage assumptions.
  - [ ] `--to endpoints`, `--to symbols`, `--to sources`, and `--to all` grouping semantics.
  - [ ] `--to all` does not duplicate identical path evidence across endpoint, symbol, and source groups.
  - [ ] `--exit-code` returns 1 with reverse roots or paths and 0 without roots or paths.
  - [ ] Markdown and JSON omit unsafe values.

- [ ] 13. Validate. Requirements: 10.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`
  - [ ] `dotnet test src/dotnet/TraceMap.sln`
  - [ ] `./scripts/check-private-paths.sh`
  - [ ] `git diff --check`
  - [ ] Pinned combined-path smoke checks from `docs/VALIDATION.md` if traversal internals change.

## Deferred Follow-Ups

- Batch query file mode for multiple surfaces.
- `--from-fact-id` or `--from-stable-key` expert selectors.
- HTML graph viewer.
- Persisted reverse query rows behind explicit `--write-derived`.
- Reverse queries over two snapshots with before/after comparison.
