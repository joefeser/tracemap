# Release Review Report Tasks

## Implementation Tasks

- [x] 1. Confirm existing report and diff surfaces. Requirements: 1, 2, 3.
  - [x] Inspect `tracemap diff`, `impact`, `reduce`, `paths`, `reverse`, and combined report outputs.
  - [x] Identify which workflows are implemented on `dev` and which must render as unavailable sections.
  - [x] Inventory existing safe rendering helpers for Markdown and JSON.

- [x] 2. Add command and report model. Requirements: 1, 8, 9.
  - [x] Add `tracemap release-review --before --after --out`.
  - [x] Validate single-vs-combined mode and reject mixed inputs.
  - [x] Emit sanitized mixed-mode and invalid-index errors to stderr without raw paths.
  - [x] Open indexes read-only.
  - [x] Add deterministic JSON model with section statuses.
  - [x] Add Markdown writer with required section order.
  - [x] Implement the output path and `--format` matrix for directory, extensionless path, Markdown file, and JSON file.
  - [x] Keep `--exit-code` deferred in v1; return non-zero only for command/input failures.

- [x] 3. Implement source identity and coverage summary. Requirements: 2, 4, 5.
  - [x] Pair single or combined sources.
  - [x] Render before-only and after-only sources as source evidence with caveats.
  - [x] Emit source identity, commit SHA, coverage, and extractor metadata.
  - [x] Emit identity, unknown SHA, missing precision table, and reduced coverage gaps.
  - [x] Add checklist items derived from source/coverage gaps.

- [x] 4. Compose dependency diff evidence. Requirements: 3, 4, 5.
  - [x] Reuse combined dependency diff semantics where available.
  - [x] Reuse combined change impact conversion and downgrade semantics where available.
  - [x] Render top changed surfaces deterministically.
  - [x] Preserve underlying classifications, rule IDs, evidence tiers, supporting IDs, and limitations.
  - [x] Keep coverage-relative rows coverage-relative.
  - [x] Map `--scope coverage` to source/coverage summary and compatible diff source rows rather than passing an unsupported scope through unchanged.

- [x] 5. Compose contract/API/SQL/package sections. Requirements: 3, 4, 8.
  - [x] Include contract delta impact v2 when `--contract-delta` is provided and workflow is available.
  - [x] Include API/DTO contract diff when workflow is available.
  - [x] Include SQL/schema impact when `--sql-schema-delta` is provided and workflow is available.
  - [x] Include indexed package-surface diffs where available.
  - [x] Validate a provided `--package-delta` file is readable before rendering deferred/unavailable package-upgrade context.
  - [x] Render `--package-delta` package-upgrade impact as deferred/unavailable until a package-upgrade workflow exists.
  - [x] Render unavailable/deferred sections explicitly when workflows are not implemented.

- [x] 6. Add optional path and reverse context. Requirements: 3, 6.
  - [x] Keep path and reverse off by default.
  - [x] Reuse existing bounded path/reverse readers.
  - [x] Render single-index path/reverse requests as unavailable/deferred with gaps in v1.
  - [x] Emit `not_requested`, `unavailable`, or truncation statuses as appropriate.
  - [x] Preserve path IDs, edge IDs, fact IDs, classifications, and caveats.

- [x] 7. Implement reviewer checklist. Requirements: 4, 5, 8, 9.
  - [x] Generate checklist items only from findings and gaps.
  - [x] Use fixed severity mapping.
  - [x] Use fixed release rollup precedence and prove coverage-relative rows are not promoted to actionable evidence.
  - [x] Include triggering finding IDs or gap IDs.
  - [x] Avoid approval, readiness, or runtime risk language.

- [x] 8. Add safety and determinism tests. Requirements: 7, 8, 10.
  - [x] Prove raw SQL, snippets, config values, connection strings, raw URLs, and local absolute paths do not render.
  - [x] Prove Markdown and JSON byte-stability.
  - [x] Prove deterministic sorting and stable IDs.
  - [x] Prove input indexes are not mutated.
  - [x] Prove section status rendering for `available`, `not_requested`, `unavailable`, `deferred`, and `truncated`.
  - [x] Prove truncation omitted counts and stable ordering.

- [x] 9. Add rules and docs. Requirements: 9, 10.
  - [x] Add release-review rule catalog entries in implementation PR 1 before emitting new rows/gaps.
  - [x] Add rule coverage for rollups, checklist items, unavailable workflow gaps, selector gaps, truncation gaps, source/coverage/schema gaps, and unsupported-mode gaps.
  - [x] Document limitations and static-analysis boundary.
  - [x] Update acceptance/validation docs if implementation changes public workflow.

- [x] 10. Validate. Requirements: 10.
  - [x] `dotnet build src/dotnet/TraceMap.sln`
  - [x] `dotnet test src/dotnet/TraceMap.sln`
  - [x] `./scripts/check-private-paths.sh`
  - [x] `git diff --check`
  - [x] Relevant smoke checks if combined/path/reverse behavior changes.
  - [x] Tests for mixed-mode rejection, invalid index schema errors, selector no-match/ignored selector metadata, and single-index path/reverse unavailable behavior.

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
