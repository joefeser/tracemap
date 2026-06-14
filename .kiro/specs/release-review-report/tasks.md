# Release Review Report Tasks

## Implementation Tasks

- [x] 1. Confirm existing report and diff surfaces. Requirements: 1, 2, 3.
  - [ ] Inspect `tracemap diff`, `impact`, `reduce`, `paths`, `reverse`, and combined report outputs.
  - [ ] Identify which workflows are implemented on `dev` and which must render as unavailable sections.
  - [ ] Inventory existing safe rendering helpers for Markdown and JSON.

- [x] 2. Add command and report model. Requirements: 1, 8, 9.
  - [ ] Add `tracemap release-review --before --after --out`.
  - [ ] Validate single-vs-combined mode and reject mixed inputs.
  - [ ] Emit sanitized mixed-mode and invalid-index errors to stderr without raw paths.
  - [ ] Open indexes read-only.
  - [ ] Add deterministic JSON model with section statuses.
  - [ ] Add Markdown writer with required section order.
  - [ ] Implement the output path and `--format` matrix for directory, extensionless path, Markdown file, and JSON file.
  - [ ] Keep `--exit-code` deferred in v1; return non-zero only for command/input failures.

- [x] 3. Implement source identity and coverage summary. Requirements: 2, 4, 5.
  - [ ] Pair single or combined sources.
  - [ ] Render before-only and after-only sources as source evidence with caveats.
  - [ ] Emit source identity, commit SHA, coverage, and extractor metadata.
  - [ ] Emit identity, unknown SHA, missing precision table, and reduced coverage gaps.
  - [ ] Add checklist items derived from source/coverage gaps.

- [x] 4. Compose dependency diff evidence. Requirements: 3, 4, 5.
  - [ ] Reuse combined dependency diff semantics where available.
  - [ ] Reuse combined change impact conversion and downgrade semantics where available.
  - [ ] Render top changed surfaces deterministically.
  - [ ] Preserve underlying classifications, rule IDs, evidence tiers, supporting IDs, and limitations.
  - [ ] Keep coverage-relative rows coverage-relative.
  - [ ] Map `--scope coverage` to source/coverage summary and compatible diff source rows rather than passing an unsupported scope through unchanged.

- [x] 5. Compose contract/API/SQL/package sections. Requirements: 3, 4, 8.
  - [ ] Include contract delta impact v2 when `--contract-delta` is provided and workflow is available.
  - [ ] Include API/DTO contract diff when workflow is available.
  - [ ] Include SQL/schema impact when `--sql-schema-delta` is provided and workflow is available.
  - [ ] Include indexed package-surface diffs where available.
  - [ ] Validate a provided `--package-delta` file is readable before rendering deferred/unavailable package-upgrade context.
  - [ ] Render `--package-delta` package-upgrade impact as deferred/unavailable until a package-upgrade workflow exists.
  - [ ] Render unavailable/deferred sections explicitly when workflows are not implemented.

- [x] 6. Add optional path and reverse context. Requirements: 3, 6.
  - [ ] Keep path and reverse off by default.
  - [ ] Reuse existing bounded path/reverse readers.
  - [ ] Render single-index path/reverse requests as unavailable/deferred with gaps in v1.
  - [ ] Emit `not_requested`, `unavailable`, or truncation statuses as appropriate.
  - [ ] Preserve path IDs, edge IDs, fact IDs, classifications, and caveats.

- [x] 7. Implement reviewer checklist. Requirements: 4, 5, 8, 9.
  - [ ] Generate checklist items only from findings and gaps.
  - [ ] Use fixed severity mapping.
  - [ ] Use fixed release rollup precedence and prove coverage-relative rows are not promoted to actionable evidence.
  - [ ] Include triggering finding IDs or gap IDs.
  - [ ] Avoid approval, readiness, or runtime risk language.

- [x] 8. Add safety and determinism tests. Requirements: 7, 8, 10.
  - [ ] Prove raw SQL, snippets, config values, connection strings, raw URLs, and local absolute paths do not render.
  - [ ] Prove Markdown and JSON byte-stability.
  - [ ] Prove deterministic sorting and stable IDs.
  - [ ] Prove input indexes are not mutated.
  - [ ] Prove section status rendering for `available`, `not_requested`, `unavailable`, `deferred`, and `truncated`.
  - [ ] Prove truncation omitted counts and stable ordering.

- [x] 9. Add rules and docs. Requirements: 9, 10.
  - [ ] Add release-review rule catalog entries in implementation PR 1 before emitting new rows/gaps.
  - [ ] Add rule coverage for rollups, checklist items, unavailable workflow gaps, selector gaps, truncation gaps, source/coverage/schema gaps, and unsupported-mode gaps.
  - [ ] Document limitations and static-analysis boundary.
  - [ ] Update acceptance/validation docs if implementation changes public workflow.

- [x] 10. Validate. Requirements: 10.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`
  - [ ] `dotnet test src/dotnet/TraceMap.sln`
  - [ ] `./scripts/check-private-paths.sh`
  - [ ] `git diff --check`
  - [ ] Relevant smoke checks if combined/path/reverse behavior changes.
  - [ ] Tests for mixed-mode rejection, invalid index schema errors, selector no-match/ignored selector metadata, and single-index path/reverse unavailable behavior.

## Suggested PR Slices

- [x] PR 1: Command shell, report model, source identity/coverage summary, unavailable section statuses.
- [x] PR 1 must include release-review rule catalog entries before any release-review row or gap is emitted.
- [x] PR 2: Compose combined dependency diff, combined change impact conversion where available, and top changed surfaces.
- [x] PR 3: Add contract delta/API/DTO/SQL/package section composition as workflows land; keep package-upgrade delta deferred until specified.
- [x] PR 4: Add optional path/reverse context and reviewer checklist, including single-index unavailable behavior.
- [x] PR 5: Public sample workflow and byte-stability/safety hardening.

## Adjacent Follow-Ups

- HTML release review explorer.
- Hosted/dashboard workflow.
- Deterministic risk-scoring consumer in a separate spec, if it remains evidence-bound and documented.

## Out of Scope for TraceMap Core

- CI gate policy.
- Auto-approval, merge, or release decisions.
- Runtime risk prediction.
- LLM-generated narrative summaries.
