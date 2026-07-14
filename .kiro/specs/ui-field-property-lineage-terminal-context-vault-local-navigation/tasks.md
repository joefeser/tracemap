# UI Field Property Lineage Terminal Context Vault Local Navigation Tasks

Status: ready-for-implementation
Readiness: validated-spec-only

## Spec-Only PR Scope

- [x] Create `.kiro/specs/ui-field-property-lineage-terminal-context-vault-local-navigation/`.
- [x] Fetch `origin/dev` and create an isolated worktree from latest
  `origin/dev`.
- [x] Draft requirements, design, tasks, review prompts, and
  implementation-state files.
- [x] Keep the spec scoped to hidden/local vault navigation for
  `terminalContextKind`.
- [x] Explicitly exclude docs-export consumer implementation.
- [x] Run Kiro spec review with Opus, or record exact unavailable/tool/timeout
  evidence in `implementation-state.md`.
- [x] Run Kiro spec review with Sonnet, or record exact unavailable/tool/timeout
  evidence in `implementation-state.md`.
- [x] Patch Medium+ actionable findings; patch Low findings only when narrow
  and safe.
- [x] Update this tasks file and `implementation-state.md` after review fixes.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Confirm diff is limited to this spec folder.
- [x] Commit the spec branch.
- [x] Push the branch and open a PR to `dev`.
- [x] Wait 3 minutes, then run ACK PR loop.
- [x] Follow ACK-authorized actions only; do not manually tag review bots, do
  not force-push, do not squash, and do not target `main`.

## PR 1: Audit And Decision Gate

- [x] 1. Audit current vault input seams for property-flow reports.
  Requirements: 1, 2, 3.
  - [x] Confirm whether vault export currently accepts property-flow report
    JSON directly or only through combined/path report inputs.
  - [x] Identify the smallest product-code seam for reading
    `safeMetadata["terminalContextKind"]`.
  - [x] Confirm existing hidden/demo/public claim-level filtering behavior for
    path-like report evidence.
  - [x] Confirm existing vault safety contexts cover terminal-context metadata,
    path IDs, node IDs, file spans, and display names.

- [x] 2. Record the implementation decision before product edits.
  Requirements: 2, 4.
  - [x] In this spec's `implementation-state.md`, choose one:
    `hidden-local-render`, `omission-gap-only`, or `ignore-with-schema-gap`.
  - [x] Record whether new rule IDs are needed or existing rules will be
    reused.
  - [x] Record the focused test set made mandatory by the chosen decision.
  - [x] Record any schema compatibility assumptions.
  - [x] Keep the first implementation on the explicit property-flow report JSON
    seam unless the implementation updates this spec with a reviewed narrower
    alternative before product edits.

## PR 2: Hidden/Local Graph And Markdown Navigation

- [ ] 3. Implement terminal-context evidence ingestion.
  Requirements: 1, 3, 6.
  - [ ] Read structured `terminalContextKind` only from compatible
    property-flow evidence.
  - [ ] Treat `StaticTerminalContext` prose as bounded display text only.
  - [ ] Preserve path ID, terminal node ID, source identity, commit SHA,
    extractor version, rule IDs, evidence tiers, supporting IDs, coverage, and
    limitations where available.
  - [ ] Treat absent metadata as unknown/unavailable, not negative evidence.

- [ ] 4. Implement hidden/local vault navigation.
  Requirements: 2, 3, 4, 5, 6.
  - [ ] Add terminal-context node/edge/tag rendering only for hidden output, or
    implement the chosen omission-gap-only behavior.
  - [ ] Generate stable context-separated IDs from safe components only.
  - [ ] Link terminal-context navigation back to property-flow path/source,
    rules, gaps, and limitations.
  - [ ] Mark output partial when schema/safety/claim filtering affects graph
    interpretation.
  - [ ] Keep all wording static and non-impact.

- [ ] 5. Preserve public/demo strictness.
  Requirements: 2, 3, 6.
  - [ ] Omit or gap terminal-context evidence for `demo-safe` and
    `public-safe`.
  - [ ] Prove source claim catalog promotion does not promote this hidden
    navigation.
  - [ ] Keep existing no-visible-evidence failure behavior.

- [ ] 6. Update rule catalog and docs when output semantics change.
  Requirements: 4, 6.
  - [ ] Reuse existing vault/property-flow rules where sufficient.
  - [ ] Add `vault-export.graph.property-flow-terminal-context.v1` only if a
    new graph rule is emitted.
  - [ ] Add `vault-export.gap.terminal-context-omitted.v1` only if existing
    omission/safety rules do not fit.
  - [ ] Add a guard test that terminal-context graph nodes or edges are not
    emitted unless required source and vault packaging rules exist in
    `rules/rule-catalog.yml`.
  - [ ] Update `docs/VAULT_EXPORT.md` for user-visible behavior.
  - [ ] Do not update docs-export docs or site copy in this implementation PR.

## PR 3: Tests And Validation

- [ ] 7. Add focused vault tests.
  Requirements: 1, 2, 3, 5, 6, 7.
  - [ ] Hidden fixture renders or records the chosen terminal-context behavior
    from structured metadata.
  - [ ] Absent-key fixture proves no negative no-surface language.
  - [ ] Structured/prose mismatch fixture prefers structured metadata and emits
    or reuses a gap.
  - [ ] Unknown safe value fixture is category-labeled or gap-backed.
  - [ ] Rule-catalog guard fixture proves terminal-context graph emission is
    blocked or tested until required rule IDs are catalogued.
  - [ ] Demo/public fixture omits or gaps terminal-context navigation.
  - [ ] Source-claim-catalog fixture proves no accidental promotion.
  - [ ] Multiple-paths fixture proves paths with the same terminal context kind
    stay path-scoped in counts and are not merged into a stronger claim.
  - [ ] Unsafe metadata fixture proves unsafe values do not reach generated
    Markdown or `graph.json`.
  - [ ] Determinism fixture proves byte-stable Markdown and `graph.json`.
  - [ ] Collision/stale-hash fixture proves generated-file safety remains
    intact.
  - [ ] Wording assertions reject runtime, execution, impact, and complete
    coverage claims.
  - [ ] Demo/public omission fixture asserts a rule-backed gap when hidden
    terminal-context evidence was present but filtered.

- [ ] 8. Validate implementation PRs.
  Requirements: 7.
  - [ ] Run focused vault export tests.
  - [ ] Run `dotnet test src/dotnet/TraceMap.sln` unless explicitly deferred
    with a recorded reason.
  - [ ] Run `./scripts/check-private-paths.sh`.
  - [ ] Run `git diff --check`.
  - [ ] Update this spec's `tasks.md` checkboxes as tasks are completed.

## Deferred Follow-Ups

- Docs-export terminal-context retrieval metadata.
- Public/demo concept rendering for terminal context.
- Public site copy.
- New scanner facts or producer terminal-context mappings.
- Reducer impact conclusions.
- Runtime/browser/live HTTP/database validation.
- Raw source snippet export.
- AI/LLM classification, embeddings, vector databases, or prompt-based
  analysis in TraceMap core.
