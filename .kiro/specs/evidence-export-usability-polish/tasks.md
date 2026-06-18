# Evidence Export Usability Polish Tasks

## Spec-Only PR Scope

- [x] Add Kiro spec files under `.kiro/specs/evidence-export-usability-polish/`.
- [x] Keep this PR limited to the new spec folder.
- [x] Run Opus and Sonnet Kiro spec reviews when local tooling is available, or document the blocker and complete a rigorous self-review.
- [x] Patch blocking and Medium+ spec-review findings.
- [x] Run spec PR validation checks: `./scripts/check-private-paths.sh` and `git diff --check`.

## Implementation Tasks

- [x] 1. Confirm current export behavior. Requirements: 1, 2, 3, 7, 9, 11.
  - [x] Inventory current `tracemap vault export` layout, generated sentinels, graph schema, strict public/demo validation, hidden/local safety behavior, and collision handling.
  - [x] Inventory current `tracemap docs-export` manifest, chunk families, JSONL schema, Markdown rendering, redaction checks, and collision handling.
  - [x] Record compatibility constraints for existing vault and docs consumers.

- [ ] 2. Design shared navigation metadata models. Requirements: 3, 4, 5, 7, 9.
  - [x] Define safe display-title records with stable ID preservation.
  - [x] Define bounded aliases and closed tag vocabularies for evidence tiers, coverage labels, surface kinds, classifications, needs-review, gaps, and limitations.
  - [x] Document alias category ordering and tag lexicographic ordering for byte-stable reruns.
  - [x] Define additive graph `navigationCategory` or equivalent fields without changing evidence identity semantics.
  - [x] Define docs-export `questionFamilies`, `claim`, `citations`, `redactions`, and `sectionTitle` fields or compatibility aliases.

- [x] 3. Polish vault top-level navigation. Requirements: 1, 2, 3, 4, 11.
  - [x] Add or improve a generated `Start Here` entry note.
  - [x] Preserve or explicitly version existing generated `README.md` and `index.md` entry contracts.
  - [x] Add deterministic links to category indexes and review queues.
  - [x] Show claim level, coverage, partial status, visible counts, omitted counts, gaps, and limitations near the top.
  - [x] Add tests for deterministic output and strict public/demo safety.

- [ ] 4. Polish vault folder and index organization. Requirements: 2, 3, 4, 11.
  - [x] Add or improve generated indexes for endpoints, routes, symbols, dependency surfaces, packages, rules, gaps, limitations, reports, and sources.
  - [x] Group dependency surfaces by closed `surfaceKind`.
  - [ ] Distinguish route, route-flow, static path, and route gap evidence.
  - [x] Preserve user-file collision and stale generated-file behavior.
  - [x] Add byte-stability and collision tests.

- [ ] 5. Add safe titles, aliases, and tags. Requirements: 3, 4, 10, 11.
  - [x] Implement safe display-title selection and deterministic fallback titles.
  - [x] Add bounded aliases to frontmatter where safe.
  - [x] Add closed-vocabulary tags for evidence tiers, coverage labels, claim levels, surface kinds, classifications, needs-review, gaps, and limitations.
  - [ ] Add hidden/local redaction, hash, category, or omission records where title or alias values are unsafe.
  - [x] Add rerun tests proving alias and tag ordering is deterministic.
  - [x] Add public/demo rejection and hidden/local redaction tests.

- [ ] 6. Polish graph categories and optional review mode. Requirements: 5, 6, 11.
  - [ ] Add safe node and edge navigation categories while preserving canonical IDs, rule IDs, tiers, supporting IDs, coverage, and limitations.
  - [ ] Emit gaps for duplicate, ambiguous, unsafe, or unsupported graph identity.
  - [ ] If implemented, add deterministic `full|review` graph mode with recorded selection predicates and omitted counts.
  - [ ] If filtered-only review output is supported, label it partial and test that full output remains available by default.
  - [ ] Defer review-friendly mode if deterministic predicates are not clear.
  - [ ] Add tests proving graph inclusion does not promote evidence strength.

- [ ] 7. Polish docs-export chunk titles and section names. Requirements: 7, 8, 9, 10, 11.
  - [x] Add safe chunk `title` and `sectionTitle` fields.
  - [x] Render deterministic Markdown headings from structured fields.
  - [x] Add question-oriented family or `questionFamilies` metadata for endpoints, data surfaces, packages, snapshot changes, weak evidence, gaps, and limitations.
  - [x] Support multiple `questionFamilies` memberships for canonical chunks that also belong to cross-cutting weak-evidence, gap, or limitation views.
  - [x] Treat weak-evidence, gap, and limitation question families as additive views over canonical chunks unless a future schema explicitly requires distinct records.
  - [ ] Emit rule-backed gaps when requested families are unsupported by input schema.
  - [ ] Source snapshot-change chunks only from compatible release-review input or a future explicitly supported diff/snapshot input; emit `docs-export.gap.unsupported-question-family.v1` when neither is available.
  - [ ] Prove `docs-export.gap.unsupported-question-family.v1` does not duplicate existing `docs-export.gap.unsupported-family.v1` for canonical family failures.
  - [x] Add JSONL and Markdown parity tests.

- [ ] 8. Implement claim/citation-first docs-export records. Requirements: 8, 9, 10, 11.
  - [x] Add structured `claim` records for static evidence, weak evidence, and gap statements.
  - [x] Add structured `citations` carrying rule IDs, evidence tiers, supporting fact IDs, supporting edge IDs, supporting report IDs, safe source spans, coverage labels, and limitations.
  - [x] Ensure `bodyMarkdown` is rendered from structured fields.
  - [x] Add tests for reduced coverage, lower-tier evidence, and gap chunks.
  - [x] Verify external RAG/vector import remains consumer-only and is never evidence for TraceMap conclusions.

- [ ] 9. Update rules and docs. Requirements: 4, 6, 8, 9, 10, 12.
  - [x] Add or update exporter rule IDs and limitations in `rules/rule-catalog.yml`.
  - [x] Add `docs-export.gap.unsupported-question-family.v1` to `rules/rule-catalog.yml`.
  - [x] Document the boundary between existing `docs-export.gap.unsupported-family.v1` and additive `docs-export.gap.unsupported-question-family.v1`.
  - [x] Update vault export docs for start page, indexes, titles, aliases, tags, graph categories, optional review mode, and hidden/local redaction.
  - [x] Update docs-export docs for question families, claim/citation schema, chunk titles, redaction, and downstream import boundaries.
  - [x] Document concrete hidden/local redactable, hashable, category-only, omitted, and hard-fail examples without raw private values.
  - [ ] Update validation docs if new focused exporter tests or smoke checks are added.

- [ ] 10. Validate implementation. Requirements: 11, 12.
  - [ ] Run focused vault export tests.
  - [ ] Run focused docs-export tests.
  - [ ] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [ ] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [ ] Run `./scripts/check-private-paths.sh`.
  - [ ] Run `git diff --check`.

## Recommended PR Slices

- [x] PR 1: Shared navigation metadata model, safe display-title helper, and tests.
- [x] PR 2a: Vault `Start Here` and top-level navigation.
- [ ] PR 2b: Vault folder and index organization.
- [x] PR 2c: Safe titles, aliases, and tags.
- [ ] PR 3: Graph navigation categories and deterministic review-mode decision or explicit deferral.
- [ ] PR 4: Docs-export title/section polish and question-family metadata.
- [ ] PR 5: Claim/citation-first docs-export schema and Markdown/JSONL parity tests.
- [ ] PR 6: Documentation, rule catalog, and validation matrix updates.

## Deferred Follow-Ups

- Hosted site demos or public marketing pages.
- External RAG import adapters.
- Vector database writers.
- LLM answer generation or prompt classification.
- Runtime telemetry, production reachability, ownership, vulnerability, or release-approval evidence.
- Scanner/reducer inference changes not required for exporter presentation.
