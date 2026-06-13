# Combined Change Impact Tasks

## First PR Boundary

Recommended first implementation PR: complete tasks 1 through 8 without path context. That ships `tracemap impact` as a deterministic diff-to-impact report with source, coverage, endpoint, surface, and edge items. Tasks 9 and 10 add `--include-paths` path context in a follow-up PR after no-write diff/path builders are stable.

## Implementation Tasks

- [x] 1. Confirm MVP command and terminology. Requirements: 1, 2, 5, 6.
  - [x] Confirm command name: `tracemap impact`.
  - [x] Confirm "impact" wording means static change context, not runtime impact proof.
  - [x] Confirm inputs are two combined SQLite databases.
  - [x] Confirm outputs are `impact-report.md` and `impact-report.json`.
  - [x] Confirm path search is off by default and opt-in with `--include-paths`.
  - [x] Confirm default caps for impact items, paths per item, path query count, depth, frontier, and gaps.
  - [x] Confirm `coverage` maps to diff's `sources` scope before coverage-row filtering.
  - [x] Confirm `NoImpactEvidence` is report-level only, not an impact item classification.

- [x] 2. Refactor reusable diff API without behavior changes. Requirements: 2, 8, 10.
  - [x] Expose an internal diff report builder that does not write files.
  - [x] Preserve current `tracemap diff` output exactly.
  - [x] Share output-path handling, format normalization, hashing, safe path rendering, and Markdown escaping.
  - [x] Add regression tests proving `tracemap diff` output remains byte-stable.

- [x] 3. Add impact report models. Requirements: 3, 7, 8, 9.
  - [x] Define query, summary, impact item, path context, path summary, gap, note, and limitation models.
  - [x] Define JSON version `1.0`.
  - [x] Include `reportType = combined-change-impact`.
  - [x] Include before and after snapshot info from diff.
  - [x] Include rule IDs and evidence tiers on every item and gap.
  - [x] Include normalized top-level evidence tier, file span, supporting fact IDs, and supporting edge IDs on impact items.
  - [x] Use empty arrays and nulls consistently.

- [x] 4. Add impact item and report-level classifications. Requirements: 2, 6, 9.
  - [x] Add fixed classification constants.
  - [x] Add confidence mapping.
  - [x] Add deterministic classification rank.
  - [x] Keep impact item classifications separate from path-context classifications.
  - [x] Emit `NoImpactEvidence` only as a report-level gap.
  - [x] Downgrade for reduced coverage.
  - [x] Downgrade for source identity conflicts or unverified identity.
  - [x] Downgrade for Tier3 syntax/textual evidence.
  - [x] Downgrade for duplicate stable identities.
  - [x] Preserve propagated diff identity, schema, and truncation gaps.

- [x] 5. Convert diff rows into impact candidates. Requirements: 2, 3, 6.
  - [x] Source and coverage diff rows.
  - [x] Endpoint diff rows.
  - [x] Surface diff rows.
  - [x] Edge diff rows.
  - [x] Carry coverage caveats and notes from diff rows.
  - [x] Preserve supporting fact IDs and edge IDs.
  - [x] Generate stable impact IDs independent of row order.

- [x] 6. Add selector handling and caps. Requirements: 5, 8.
  - [x] Parse `--scope`.
  - [x] Parse `--source`.
  - [x] Parse `--endpoint`.
  - [x] Parse `--surface`.
  - [x] Parse `--surface-name`.
  - [x] Parse `--include-paths`.
  - [x] Parse numeric caps.
  - [x] Parse `--max-path-queries`.
  - [x] Parse `--allow-identity-mismatch`.
  - [x] Parse `--exit-code`.
  - [x] Map impact `coverage` scope to diff `sources` scope and filter coverage rows afterward.
  - [x] Emit `SelectorNoMatch` for empty selector results.
  - [x] Record ignored selectors where scopes make them irrelevant.

- [x] 7. Add Markdown writer. Requirements: 7, 8, 9.
  - [x] Summary section.
  - [x] Query section.
  - [x] Snapshot Sources section.
  - [x] Impact Items section.
  - [x] Path Context section with "not requested" state when `--include-paths` is omitted.
  - [x] Gaps section.
  - [x] Limitations section.
  - [x] Escape Markdown table/link delimiters.
  - [x] Use safe path rendering.
  - [x] Avoid raw SQL, raw snippets, config values, connection strings, raw URLs, and local absolute paths.

- [x] 8. Add JSON writer and CLI. Requirements: 1, 5, 8.
  - [x] Emit stable top-level JSON shape.
  - [x] Sort all arrays deterministically.
  - [x] Sort metadata keys deterministically.
  - [x] Include impact items, empty path context when not requested, gaps, and limitations.
  - [x] Exclude timestamps and unsafe raw values.
  - [x] Add `tracemap impact --help`.
  - [x] Validate required arguments.
  - [x] Validate selector combinations.
  - [x] Validate numeric caps.
  - [x] Print completion summary.
  - [x] Implement opt-in `--exit-code`.

- [x] 9. Refactor reusable path API without behavior changes. Requirements: 4, 8, 10.
  - [x] Expose an internal path query/report builder that does not write files.
  - [x] Preserve current `tracemap paths` output exactly.
  - [x] Add regression tests proving `tracemap paths` output remains byte-stable.

- [x] 10. Add opt-in path context planner. Requirements: 4, 5, 6.
  - [x] Map changed endpoints to endpoint path queries.
  - [x] Map changed surfaces to terminal surface queries when possible.
  - [x] Map changed edges to source-symbol queries when possible.
  - [x] Skip source/coverage rows unless a future rule defines useful path context.
  - [x] Emit `PathContextUnavailable` when no safe selector can be built.
  - [x] Run bounded before and after path queries.
  - [x] Cap path results per item.
  - [x] Cap total before/after path queries with `--max-path-queries`.
  - [x] Sort path context deterministically.
  - [x] Preserve path IDs, classifications, source transitions, supporting IDs, and terminal metadata.

- [x] 11. Add path-context classification. Requirements: 4, 6, 9.
  - [x] Detect before-only paths.
  - [x] Detect after-only paths.
  - [x] Detect comparable paths with changed evidence.
  - [x] Keep `ReachabilityChanged`, `ReachabilityEvidenceChanged`, `PathContextUnavailable`, and `NoPathEvidence` path-context only.
  - [x] Downgrade reachability claims under reduced coverage.
  - [x] Emit `UnknownAnalysisGap` when no-path conclusions are not credible.
  - [x] Emit truncation gaps for depth, frontier, row, per-item, and global path-query caps.

- [x] 12. Update rule catalog and docs. Requirements: 7, 9.
  - [x] Add `combined.impact.source.v1`.
  - [x] Add `combined.impact.coverage.v1`.
  - [x] Add `combined.impact.endpoint.v1`.
  - [x] Add `combined.impact.surface.v1`.
  - [x] Add `combined.impact.edge.v1`.
  - [x] Add `combined.impact.path.v1`.
  - [x] Add `combined.impact.path-context.v1`.
  - [x] Add `combined.impact.selector.v1`.
  - [x] Add `combined.impact.truncation.v1`.
  - [x] Document propagated `combined.diff.identity.v1` and `combined.diff.schema.v1` use.
  - [x] Document limitations for every new rule.
  - [x] Update README quickstart.
  - [x] Update `docs/ACCEPTANCE.md`.
  - [x] Update `docs/VALIDATION.md`.

- [x] 13. Add tests. Requirements: 1-10.
  - [x] Non-combined input rejection.
  - [x] Directory output writes Markdown and JSON.
  - [x] File output respects `--format`.
  - [x] Byte-stable repeated output.
  - [x] Identical snapshots emit `NoImpactEvidence`.
  - [x] Coverage change creates coverage impact item.
  - [x] Endpoint change creates endpoint impact item.
  - [x] Surface change creates surface impact item.
  - [x] Edge change creates edge impact item.
  - [x] Moved evidence span is preserved as changed evidence.
  - [x] Reduced coverage downgrades added/removed impact.
  - [x] Allowed source identity mismatch downgrades strong claims.
  - [x] Duplicate stable identity cites duplicate identity gap and does not select arbitrary winner.
  - [x] Optional precision schema absence propagates schema gaps.
  - [x] Selector no match emits a gap.
  - [x] Ignored selectors are recorded when irrelevant to the chosen scope.
  - [x] Impact item cap emits truncation gap.
  - [x] Gap cap emits truncation gap.
  - [x] `--exit-code` returns 1 with impact items and 0 without impact items.
  - [x] Confidence mapping is deterministic for each classification.
  - [x] JSON empty-array and null conventions are stable.
  - [x] Path context for endpoint change.
  - [x] Path context for surface change where selector is safe.
  - [x] Path context unavailable gap for unsupported mapping.
  - [x] `ReachabilityChanged` vs `ReachabilityEvidenceChanged`.
  - [x] Path context per-item and global query caps emit truncation gaps.
  - [x] Markdown and JSON omit unsafe values.
  - [x] Input databases are not mutated.

- [x] 14. Validate. Requirements: 10.
  - [x] `dotnet build src/dotnet/TraceMap.sln`
  - [x] `dotnet test src/dotnet/TraceMap.sln`
  - [x] `./scripts/check-private-paths.sh`
  - [x] `git diff --check`
  - [x] `./scripts/smoke-combined-paths.sh` if path context code changes.

## Deferred Follow-Ups

- HTML report or graph viewer.
- `--diff-json <path>` input.
- Persisted impact rows behind explicit `--write-derived`.
- Reverse impact queries from dependency surface to endpoints.
- Query file or batch mode.
- Business/domain weighting configured by user-owned rules.
- Runtime telemetry import as explicitly labeled external evidence.
- Deeper SQL parser-backed schema/table relationship analysis.
- Package vulnerability or license impact overlays.
- CI annotations for changed impact items.
