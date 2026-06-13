# Combined Change Impact Tasks

## Implementation Tasks

- [ ] Confirm MVP command and terminology.
  - [ ] Confirm command name: `tracemap impact`.
  - [ ] Confirm "impact" wording means static change context, not runtime impact proof.
  - [ ] Confirm inputs are two combined SQLite databases.
  - [ ] Confirm outputs are `impact-report.md` and `impact-report.json`.
  - [ ] Confirm path context is opt-in with `--include-paths`.
  - [ ] Confirm default caps for impact items, paths per item, depth, frontier, and gaps.

- [ ] Refactor reusable diff/path APIs.
  - [ ] Expose an internal diff report builder that does not write files.
  - [ ] Preserve current `tracemap diff` output exactly.
  - [ ] Expose an internal path query/report builder that does not write files.
  - [ ] Preserve current `tracemap paths` output exactly.
  - [ ] Share output-path handling, format normalization, hashing, safe path rendering, and Markdown escaping.
  - [ ] Add regression tests proving refactor output remains byte-stable.

- [ ] Add impact report models.
  - [ ] Define query, summary, impact item, path context, path summary, gap, note, and limitation models.
  - [ ] Define JSON version `1.0`.
  - [ ] Include `reportType = combined-change-impact`.
  - [ ] Include before and after snapshot info from diff.
  - [ ] Include rule IDs and evidence tiers on every item and gap.
  - [ ] Use empty arrays and nulls consistently.

- [ ] Add impact classifications.
  - [ ] Add fixed classification constants.
  - [ ] Add confidence mapping.
  - [ ] Add deterministic classification rank.
  - [ ] Downgrade for reduced coverage.
  - [ ] Downgrade for source identity conflicts or unverified identity.
  - [ ] Downgrade for Tier3 syntax/textual evidence.
  - [ ] Downgrade for duplicate stable identities.
  - [ ] Mark truncated path context as partial.

- [ ] Convert diff rows into impact candidates.
  - [ ] Source and coverage diff rows.
  - [ ] Endpoint diff rows.
  - [ ] Surface diff rows.
  - [ ] Edge diff rows.
  - [ ] Optional path diff rows.
  - [ ] Carry coverage caveats and notes from diff rows.
  - [ ] Preserve supporting fact IDs and edge IDs.
  - [ ] Generate stable impact IDs independent of row order.

- [ ] Add selector handling.
  - [ ] Parse `--scope`.
  - [ ] Parse `--source`.
  - [ ] Parse `--endpoint`.
  - [ ] Parse `--surface`.
  - [ ] Parse `--surface-name`.
  - [ ] Parse `--include-paths`.
  - [ ] Parse numeric caps.
  - [ ] Parse `--allow-identity-mismatch`.
  - [ ] Parse `--exit-code`.
  - [ ] Emit `SelectorNoMatch` for empty selector results.
  - [ ] Record ignored selectors where scopes make them irrelevant.

- [ ] Add path context planner.
  - [ ] Map changed endpoints to endpoint path queries.
  - [ ] Map changed surfaces to terminal surface queries when possible.
  - [ ] Map changed edges to source-symbol queries when possible.
  - [ ] Skip source/coverage rows unless a future rule defines useful path context.
  - [ ] Emit `PathContextUnavailable` when no safe selector can be built.
  - [ ] Run bounded before and after path queries.
  - [ ] Cap path results per item.
  - [ ] Sort path context deterministically.
  - [ ] Preserve path IDs, classifications, source transitions, supporting IDs, and terminal metadata.

- [ ] Add path-context classification.
  - [ ] Detect before-only paths.
  - [ ] Detect after-only paths.
  - [ ] Detect comparable paths with changed evidence.
  - [ ] Downgrade reachability claims under reduced coverage.
  - [ ] Emit `UnknownAnalysisGap` when no-path conclusions are not credible.
  - [ ] Emit truncation gaps for depth, frontier, row, and per-item caps.

- [ ] Add Markdown writer.
  - [ ] Summary section.
  - [ ] Query section.
  - [ ] Snapshot Sources section.
  - [ ] Impact Items section.
  - [ ] Path Context section.
  - [ ] Gaps section.
  - [ ] Limitations section.
  - [ ] Escape Markdown table/link delimiters.
  - [ ] Use safe path rendering.
  - [ ] Avoid raw SQL, raw snippets, config values, connection strings, raw URLs, and local absolute paths.

- [ ] Add JSON writer.
  - [ ] Emit stable top-level shape.
  - [ ] Sort all arrays deterministically.
  - [ ] Sort metadata keys deterministically.
  - [ ] Include impact items and path context.
  - [ ] Include gaps and limitations.
  - [ ] Exclude timestamps and unsafe raw values.
  - [ ] Verify byte-stability.

- [ ] Wire CLI.
  - [ ] Add `tracemap impact --help`.
  - [ ] Validate required arguments.
  - [ ] Validate selector combinations.
  - [ ] Validate numeric caps.
  - [ ] Print completion summary.
  - [ ] Implement opt-in `--exit-code`.

- [ ] Update rule catalog and docs.
  - [ ] Add `combined.impact.source.v1`.
  - [ ] Add `combined.impact.endpoint.v1`.
  - [ ] Add `combined.impact.surface.v1`.
  - [ ] Add `combined.impact.edge.v1`.
  - [ ] Add `combined.impact.path.v1`.
  - [ ] Add `combined.impact.selector.v1`.
  - [ ] Add `combined.impact.truncation.v1`.
  - [ ] Document limitations for every new rule.
  - [ ] Update README quickstart.
  - [ ] Update `docs/ACCEPTANCE.md`.
  - [ ] Update `docs/VALIDATION.md`.

- [ ] Add tests.
  - [ ] Non-combined input rejection.
  - [ ] Directory output writes Markdown and JSON.
  - [ ] File output respects `--format`.
  - [ ] Byte-stable repeated output.
  - [ ] Identical snapshots emit `NoImpactEvidence`.
  - [ ] Endpoint change creates endpoint impact item.
  - [ ] Surface change creates surface impact item.
  - [ ] Edge change creates edge impact item.
  - [ ] Moved evidence span is preserved as changed evidence.
  - [ ] Reduced coverage downgrades added/removed impact.
  - [ ] Allowed source identity mismatch downgrades strong claims.
  - [ ] Selector no match emits a gap.
  - [ ] Impact item cap emits truncation gap.
  - [ ] Gap cap emits truncation gap.
  - [ ] Path context for endpoint change.
  - [ ] Path context for surface change where selector is safe.
  - [ ] Path context unavailable gap for unsupported mapping.
  - [ ] Path context cap emits truncation gap.
  - [ ] Markdown and JSON omit unsafe values.
  - [ ] Input databases are not mutated.
  - [ ] `--exit-code` is opt-in.

- [ ] Validate.
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

