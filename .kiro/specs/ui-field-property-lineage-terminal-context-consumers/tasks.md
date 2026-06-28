# UI Field Property Lineage Terminal Context Consumers Tasks

Status: implemented-pending-pr-review
Readiness: docs-export-validation-complete

## Spec-Only PR Scope

- [x] Create `.kiro/specs/ui-field-property-lineage-terminal-context-consumers/`.
- [x] Fetch `origin/dev` and draft from the current `dev` baseline.
- [x] Verify the spec follows PR #400 and the property-flow terminal-context
  gate on `dev`.
- [x] Inspect `src/dotnet/TraceMap.Reporting/PropertyFlowReport.cs`.
- [x] Inspect docs/vault/export/report consumer seams enough to avoid stale
  scope.
- [x] Inspect existing Kiro specs around UI field property lineage terminal
  context, vault export, docs export, and hidden safety.
- [x] Keep this PR limited to the assigned new spec folder.
- [x] Run Kiro spec review with Opus, or record exact unavailable/tool/timeout
  evidence in `implementation-state.md`. Completed with reduced coverage:
  Kiro reported denied shell/tool access.
- [x] Run Kiro spec review with Sonnet, or record exact unavailable/tool/timeout
  evidence in `implementation-state.md`. Completed with reduced coverage:
  Kiro reported denied shell/tool access.
- [x] Patch Medium+ actionable findings; patch Low findings only when narrow
  and safe.
- [x] Update `implementation-state.md` status/readiness after review fixes.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Confirm diff is limited to this spec folder.
- [x] Commit the spec branch.
- [x] Push the branch and open a PR to `dev`.
- [x] Wait 3 minutes, then run ACK PR loop.
- [x] Follow ACK-authorized actions only; do not manually tag review bots, do
  not force-push, and never squash.

## PR 1: Docs Export Compatibility And Static Metadata

- [x] 1. Audit live property-flow report and docs-export schemas.
  Requirements: 1, 2, 3, 5.
  - [x] Confirm `terminalContextKind` is present only in path-scoped node safe
    metadata after the PR #400 selected-property bridge.
  - [x] Confirm `StaticTerminalContext` path notes are additive display text.
  - [x] Confirm current docs-export report-family handling for property-flow
    reports and safe metadata.
  - [x] Decide whether PR 1 safely ignores terminal context or renders it as
    retrieval metadata.
  - [x] Decide whether terminal context augments the existing
    `docs-export.chunk.property-flow.v1` family or creates a new catalogued
    chunk family before any `EvidenceDocsExport` product edits.
  - [x] Record the decision and compatibility implications in this spec's
    `implementation-state.md` before product edits. The recorded render/ignore
    decision makes the matching test set mandatory for this PR.

- [x] 2. Implement docs-export consumer behavior.
  Requirements: 1, 2, 3, 6, 7.
  - [x] Preserve rule IDs, evidence tiers, supporting IDs, commit SHA,
    extractor version, source identity, file spans, coverage, and limitations
    where input provides them.
  - [x] Treat terminal context as static retrieval metadata, not a new finding.
  - [x] Prefer structured `terminalContextKind` over note-text parsing.
  - [x] Sanitize, hash, category-label, omit, or gap unsafe terminal metadata
    and note text under existing safety rules.
  - [x] Add or update rule-catalog/docs entries before emitting any new chunk
    family, gap code, limitation, or redaction category. No new chunk family,
    gap code, limitation, or redaction category was emitted; docs were updated
    for the existing property-flow family.

- [x] 3. Test docs-export behavior.
  Requirements: 3, 7, 8.
  - [x] Add a positive fixture where docs export safely renders or indexes
    terminal context from structured metadata.
  - [x] Add a compatibility fixture where unknown additive safe metadata is
    ignored without failing.
  - [x] Add a malformed fixture where
    `safeMetadata["terminalContextKind"]` is a valid closed-vocabulary value but
    `StaticTerminalContext` prose names a different kind; assert structured
    metadata wins and a schema/consistency gap is emitted or reused. Covered by
    the docs-export decision not to parse prose notes; structured metadata is
    the only rendered source.
  - [x] Add an absent-context fixture where the `terminalContextKind` key is
    absent; assert no negative no-surface language is emitted.
  - [x] Add consumer fixtures for known closed-vocabulary
    `terminalContextKind` values and unknown safe values; do not re-test the
    producer's `surfaceKind` to `terminalContextKind` switch in docs-export
    tests.
  - [x] Add absent-metadata fixtures, such as HTTP route/client reports or a
    malformed-empty-path report, proving docs export does not infer terminal
    context when `terminalContextKind` is missing.
  - [x] Add a multi-note fixture with both `StaticRouteFlowContext` and
    `StaticTerminalContext` to validate stable ordinal note ordering.
    Not applicable to this PR because docs export does not parse or render
    property-flow path notes; note ordering stays with the producer/reporting
    tests.
  - [x] Add an unsafe metadata/note fixture that omits, hashes,
    category-labels, or gaps without echoing unsafe values.
  - [x] Assert output does not contain runtime, database execution, dependency
    execution, impact, or complete coverage claims.
  - [x] Assert deterministic JSONL, Markdown, and manifest output for repeated
    equivalent inputs.

## PR 2: Vault Hidden/Local Navigation

- [ ] 4. Audit vault export graph and claim-level seams.
  Requirements: 1, 2, 4, 6.
  - [ ] Confirm how property-flow reports are currently accepted, rejected, or
    represented by vault export.
  - [ ] Decide whether terminal context becomes a hidden/local graph node/edge,
    tag, note section, or explicit omission gap.
  - [ ] Record the render/ignore/omission-gap decision in this spec's
    `implementation-state.md` before product edits. The recorded decision makes
    the matching test set mandatory for this PR.
  - [ ] Record whether new vault-export rule IDs or gap codes are needed before
    product edits.

- [ ] 5. Implement hidden/local vault consumer behavior.
  Requirements: 2, 4, 6, 7.
  - [ ] Keep terminal context path-scoped and linked back to property-flow
    path, source, rule, gap, and limitation pages.
  - [ ] Preserve public/demo strictness unless a separate reviewed demo/concept
    policy explicitly allows static concept rendering.
  - [ ] Emit or reuse rule-backed omission/safety gaps when claim-level or
    safety filtering removes meaningful terminal context.
  - [ ] Generate stable context-separated IDs without unsafe raw values.
  - [ ] Update vault docs and rule catalog if new node, edge, tag, or gap kinds
    are emitted.

- [ ] 6. Test vault behavior.
  Requirements: 4, 7, 8.
  - [ ] Add hidden/local fixture that renders terminal context without
    overclaiming.
  - [ ] Add public/demo fixture proving terminal context remains hidden or
    explicitly omitted unless reviewed policy allows it.
  - [ ] Add claim-level filtering fixture proving omitted terminal context emits
    or reuses a named rule-backed omission gap, such as
    `TerminalContextClaimLevelOmitted`, instead of silently disappearing.
  - [ ] Add unsafe metadata fixture proving raw URLs, raw SQL, raw config,
    local absolute paths, source snippets, secrets, raw remotes, production
    data, and private identifiers are rejected or omitted safely.
  - [ ] Assert generated Markdown and `graph.json` are deterministic.
  - [ ] Assert generated-file collision and content-hash behavior are not
    weakened.

## PR 3: Reporting Readability And Documentation

- [ ] 7. Optional property-flow/report rendering polish.
  Requirements: 1, 2, 5, 7.
  - [ ] Decide whether existing report rendering is sufficient because
    path notes and node safe metadata are already present.
  - [ ] If rendering changes, keep report version `1.0` only for additive
    display changes; otherwise version the consumer schema.
  - [ ] Keep wording to static terminal context, not runtime, DB execution,
    dependency execution, impact, or complete coverage.
  - [ ] Add compatibility and deterministic rendering tests.

- [ ] 8. Documentation and rule-catalog closure.
  Requirements: 3, 4, 6, 7.
  - [x] Update `docs/EVIDENCE_DOCS_EXPORT.md` if docs-export behavior changes.
  - [ ] Update `docs/VAULT_EXPORT.md` if vault behavior changes.
  - [ ] Update `rules/rule-catalog.yml` before emitting any new consumer rule,
    gap, limitation, redaction category, chunk family, graph node kind, or
    graph edge kind.
  - [ ] Document that downstream RAG/vector systems may consume TraceMap
    evidence but cannot become evidence for TraceMap conclusions.
  - [ ] Keep public claim level hidden unless a separate spec justifies
    demo/concept rendering.

- [ ] 9. Validate implementation PRs.
  Requirements: 8.
  - [x] Run focused docs-export tests for docs-export changes.
  - [x] Run focused vault-export tests for vault changes. Not applicable; this
    PR does not change vault export.
  - [x] Run focused property-flow/reporting tests for report rendering changes.
    Not applicable; this PR does not change report rendering.
  - [x] Run `dotnet test src/dotnet/TraceMap.sln` unless explicitly narrowed
    with a recorded reason.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Run `git diff --check`.
  - [x] Run `docs/VALIDATION.md` adapter checks only if scanner or language
    adapter behavior changes; this spec expects none.

## Deferred Follow-Ups

- Public site copy or product claims for terminal-context consumers.
- Persisted property-flow terminal-context tables or schema migrations.
- New scanner facts for terminal context.
- Reducer impact conclusions.
- Runtime/browser/live HTTP/database validation.
- Raw source snippet export.
- AI/LLM classification, embeddings, vector databases, or prompt-based
  analysis in TraceMap core.
