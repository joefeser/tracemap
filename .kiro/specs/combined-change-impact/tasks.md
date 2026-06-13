# Combined Change Impact Tasks

## First PR Boundary

Recommended first implementation PR: complete tasks 1 through 8 without path context. That ships `tracemap impact` as a deterministic diff-to-impact report with source, coverage, endpoint, surface, and edge items. Tasks 9 and 10 add `--include-paths` path context in a follow-up PR after no-write diff/path builders are stable.

## Implementation Tasks

- [ ] 1. Confirm MVP command and terminology. Requirements: 1, 2, 5, 6.
  - [ ] Confirm command name: `tracemap impact`.
  - [ ] Confirm "impact" wording means static change context, not runtime impact proof.
  - [ ] Confirm inputs are two combined SQLite databases.
  - [ ] Confirm outputs are `impact-report.md` and `impact-report.json`.
  - [ ] Confirm path search is off by default and opt-in with `--include-paths`.
  - [ ] Confirm default caps for impact items, paths per item, path query count, depth, frontier, and gaps.
  - [ ] Confirm `coverage` maps to diff's `sources` scope before coverage-row filtering.
  - [ ] Confirm `NoImpactEvidence` is report-level only, not an impact item classification.

- [ ] 2. Refactor reusable diff API without behavior changes. Requirements: 2, 8, 10.
  - [ ] Expose an internal diff report builder that does not write files.
  - [ ] Preserve current `tracemap diff` output exactly.
  - [ ] Share output-path handling, format normalization, hashing, safe path rendering, and Markdown escaping.
  - [ ] Add regression tests proving `tracemap diff` output remains byte-stable.

- [ ] 3. Add impact report models. Requirements: 3, 7, 8, 9.
  - [ ] Define query, summary, impact item, path context, path summary, gap, note, and limitation models.
  - [ ] Define JSON version `1.0`.
  - [ ] Include `reportType = combined-change-impact`.
  - [ ] Include before and after snapshot info from diff.
  - [ ] Include rule IDs and evidence tiers on every item and gap.
  - [ ] Include normalized top-level evidence tier, file span, supporting fact IDs, and supporting edge IDs on impact items.
  - [ ] Use empty arrays and nulls consistently.

- [ ] 4. Add impact item and report-level classifications. Requirements: 2, 6, 9.
  - [ ] Add fixed classification constants.
  - [ ] Add confidence mapping.
  - [ ] Add deterministic classification rank.
  - [ ] Keep impact item classifications separate from path-context classifications.
  - [ ] Emit `NoImpactEvidence` only as a report-level gap.
  - [ ] Downgrade for reduced coverage.
  - [ ] Downgrade for source identity conflicts or unverified identity.
  - [ ] Downgrade for Tier3 syntax/textual evidence.
  - [ ] Downgrade for duplicate stable identities.
  - [ ] Preserve propagated diff identity, schema, and truncation gaps.

- [ ] 5. Convert diff rows into impact candidates. Requirements: 2, 3, 6.
  - [ ] Source and coverage diff rows.
  - [ ] Endpoint diff rows.
  - [ ] Surface diff rows.
  - [ ] Edge diff rows.
  - [ ] Carry coverage caveats and notes from diff rows.
  - [ ] Preserve supporting fact IDs and edge IDs.
  - [ ] Generate stable impact IDs independent of row order.

- [ ] 6. Add selector handling and caps. Requirements: 5, 8.
  - [ ] Parse `--scope`.
  - [ ] Parse `--source`.
  - [ ] Parse `--endpoint`.
  - [ ] Parse `--surface`.
  - [ ] Parse `--surface-name`.
  - [ ] Parse `--include-paths`.
  - [ ] Parse numeric caps.
  - [ ] Parse `--max-path-queries`.
  - [ ] Parse `--allow-identity-mismatch`.
  - [ ] Parse `--exit-code`.
  - [ ] Map impact `coverage` scope to diff `sources` scope and filter coverage rows afterward.
  - [ ] Emit `SelectorNoMatch` for empty selector results.
  - [ ] Record ignored selectors where scopes make them irrelevant.

- [ ] 7. Add Markdown writer. Requirements: 7, 8, 9.
  - [ ] Summary section.
  - [ ] Query section.
  - [ ] Snapshot Sources section.
  - [ ] Impact Items section.
  - [ ] Path Context section with "not requested" state when `--include-paths` is omitted.
  - [ ] Gaps section.
  - [ ] Limitations section.
  - [ ] Escape Markdown table/link delimiters.
  - [ ] Use safe path rendering.
  - [ ] Avoid raw SQL, raw snippets, config values, connection strings, raw URLs, and local absolute paths.

- [ ] 8. Add JSON writer and CLI. Requirements: 1, 5, 8.
  - [ ] Emit stable top-level JSON shape.
  - [ ] Sort all arrays deterministically.
  - [ ] Sort metadata keys deterministically.
  - [ ] Include impact items, empty path context when not requested, gaps, and limitations.
  - [ ] Exclude timestamps and unsafe raw values.
  - [ ] Add `tracemap impact --help`.
  - [ ] Validate required arguments.
  - [ ] Validate selector combinations.
  - [ ] Validate numeric caps.
  - [ ] Print completion summary.
  - [ ] Implement opt-in `--exit-code`.

- [ ] 9. Refactor reusable path API without behavior changes. Requirements: 4, 8, 10.
  - [ ] Expose an internal path query/report builder that does not write files.
  - [ ] Preserve current `tracemap paths` output exactly.
  - [ ] Add regression tests proving `tracemap paths` output remains byte-stable.

- [ ] 10. Add opt-in path context planner. Requirements: 4, 5, 6.
  - [ ] Map changed endpoints to endpoint path queries.
  - [ ] Map changed surfaces to terminal surface queries when possible.
  - [ ] Map changed edges to source-symbol queries when possible.
  - [ ] Skip source/coverage rows unless a future rule defines useful path context.
  - [ ] Emit `PathContextUnavailable` when no safe selector can be built.
  - [ ] Run bounded before and after path queries.
  - [ ] Cap path results per item.
  - [ ] Cap total before/after path queries with `--max-path-queries`.
  - [ ] Sort path context deterministically.
  - [ ] Preserve path IDs, classifications, source transitions, supporting IDs, and terminal metadata.

- [ ] 11. Add path-context classification. Requirements: 4, 6, 9.
  - [ ] Detect before-only paths.
  - [ ] Detect after-only paths.
  - [ ] Detect comparable paths with changed evidence.
  - [ ] Keep `ReachabilityChanged`, `ReachabilityEvidenceChanged`, `PathContextUnavailable`, and `NoPathEvidence` path-context only.
  - [ ] Downgrade reachability claims under reduced coverage.
  - [ ] Emit `UnknownAnalysisGap` when no-path conclusions are not credible.
  - [ ] Emit truncation gaps for depth, frontier, row, per-item, and global path-query caps.

- [ ] 12. Update rule catalog and docs. Requirements: 7, 9.
  - [ ] Add `combined.impact.source.v1`.
  - [ ] Add `combined.impact.coverage.v1`.
  - [ ] Add `combined.impact.endpoint.v1`.
  - [ ] Add `combined.impact.surface.v1`.
  - [ ] Add `combined.impact.edge.v1`.
  - [ ] Add `combined.impact.path.v1`.
  - [ ] Add `combined.impact.path-context.v1`.
  - [ ] Add `combined.impact.selector.v1`.
  - [ ] Add `combined.impact.truncation.v1`.
  - [ ] Document propagated `combined.diff.identity.v1` and `combined.diff.schema.v1` use.
  - [ ] Document limitations for every new rule.
  - [ ] Update README quickstart.
  - [ ] Update `docs/ACCEPTANCE.md`.
  - [ ] Update `docs/VALIDATION.md`.

- [ ] 13. Add tests. Requirements: 1-10.
  - [ ] Non-combined input rejection.
  - [ ] Directory output writes Markdown and JSON.
  - [ ] File output respects `--format`.
  - [ ] Byte-stable repeated output.
  - [ ] Identical snapshots emit `NoImpactEvidence`.
  - [ ] Coverage change creates coverage impact item.
  - [ ] Endpoint change creates endpoint impact item.
  - [ ] Surface change creates surface impact item.
  - [ ] Edge change creates edge impact item.
  - [ ] Moved evidence span is preserved as changed evidence.
  - [ ] Reduced coverage downgrades added/removed impact.
  - [ ] Allowed source identity mismatch downgrades strong claims.
  - [ ] Duplicate stable identity cites duplicate identity gap and does not select arbitrary winner.
  - [ ] Optional precision schema absence propagates schema gaps.
  - [ ] Selector no match emits a gap.
  - [ ] Ignored selectors are recorded when irrelevant to the chosen scope.
  - [ ] Impact item cap emits truncation gap.
  - [ ] Gap cap emits truncation gap.
  - [ ] `--exit-code` returns 1 with impact items and 0 without impact items.
  - [ ] Confidence mapping is deterministic for each classification.
  - [ ] JSON empty-array and null conventions are stable.
  - [ ] Path context for endpoint change.
  - [ ] Path context for surface change where selector is safe.
  - [ ] Path context unavailable gap for unsupported mapping.
  - [ ] `ReachabilityChanged` vs `ReachabilityEvidenceChanged`.
  - [ ] Path context per-item and global query caps emit truncation gaps.
  - [ ] Markdown and JSON omit unsafe values.
  - [ ] Input databases are not mutated.

- [ ] 14. Validate. Requirements: 10.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`
  - [ ] `dotnet test src/dotnet/TraceMap.sln`
  - [ ] `./scripts/check-private-paths.sh`
  - [ ] `git diff --check`
  - [ ] `./scripts/smoke-combined-paths.sh` if path context code changes.

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
