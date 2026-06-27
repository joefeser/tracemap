# Property Flow Terminal Context Report Readability Tasks

Status: implemented-pending-pr-review
Readiness: implementation-validation-complete

## Spec-Only PR Scope

- [x] Create `.kiro/specs/property-flow-terminal-context-report-readability/`.
- [x] Fetch `origin/dev` and create an isolated worktree/branch from the
  latest target base.
- [x] Inspect adjacent terminal-context coverage and consumer specs.
- [x] Keep this spec out of active docs-export implementation and
  vault-local-navigation implementation scope.
- [x] Draft requirements, design, tasks, review prompts, and
  implementation-state files.
- [x] Run Kiro spec review with Opus, or record exact unavailable/tool/timeout
  evidence in `implementation-state.md`.
- [x] Run Kiro spec review with Sonnet, or record exact unavailable/tool/timeout
  evidence in `implementation-state.md`.
- [x] Patch Medium+ actionable review findings; patch Low findings only when
  narrow and safe.
- [x] Update `implementation-state.md` with review results and final
  validation evidence.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Confirm diff is limited to this spec folder.
- [x] Commit the spec branch.
- [x] Push the branch and open a PR to `dev`.
- [x] Wait 3 minutes, then run ACK PR loop.
- [ ] Follow ACK-authorized actions only; do not manually tag review bots, do
  not force-push, do not squash, and do not target `main`.

## Implementation Tasks

- [x] 1. Record the implementation decision.
  Requirements: 1, 2, 4, 5.
  - [x] Choose `render`, `document-only`, or `defer` in this spec's
    `implementation-state.md` before product edits.
  - [x] Confirm no docs-export files are touched unless a separate
    terminal-context-consumers implementation authorizes that work.
  - [x] Confirm no vault graph, vault note, backlink, tag, or local navigation
    files are touched by this spec.
  - [x] Record whether report version `1.0` remains valid or why a schema
    version bump is required.

- [x] 2. Audit current property-flow reporting seams.
  Requirements: 1, 2, 3, 4.
  - [x] Inspect `PropertyFlowReport.cs` for path notes, node safe metadata,
    report version, JSON output, `RenderMarkdown`, and Markdown report inputs.
  - [x] Confirm `terminalContextKind` appears in node safe metadata only when
    the selected-property bridge gate allows it; absent keys are unknown or
    unavailable, not negative evidence.
  - [x] Confirm existing Markdown already renders `StaticTerminalContext:` as a
    path-note bullet and decide whether that is sufficient.
  - [x] Inspect focused property-flow and Markdown report tests.
  - [x] Confirm current output already preserves rule IDs, tiers, spans,
    supporting IDs, commit SHA, extractor versions, coverage labels, and
    limitations where available.

- [x] 3. Implement optional report readability polish.
  Requirements: 1, 2, 3, 4, 6.
  - [x] If `render`, add only path-local or node-local Markdown readability
    output for structured `terminalContextKind`.
  - [x] Keep terminal context static and path-scoped.
  - [x] Prefer structured metadata over `StaticTerminalContext` prose.
  - [x] Treat absent `terminalContextKind` as unknown/unavailable, not a
    negative fact.
  - [x] Treat unknown safe values as unknown/unsupported static metadata.
  - [x] Omit, hash, category-label, or gap unsafe metadata/note text under
    existing rules.
  - [x] Keep JSON schema additive and report version `1.0` unless a versioned
    compatibility change is explicitly chosen.
  - [x] Update `rules/rule-catalog.yml` first if new report-specific rules,
    gaps, limitations, or validation findings are emitted. Not needed: no new
    rule, gap, limitation, validation finding, or JSON artifact is emitted.

- [x] 4. Add focused tests.
  Requirements: 1, 2, 3, 4, 6, 7.
  - [x] Add a positive structured `terminalContextKind` fixture.
  - [x] Add an absent-metadata fixture proving no no-terminal-surface wording.
  - [x] Add malformed or contradictory note prose fixture proving structured
    metadata wins or prose is ignored/gapped.
    Covered by rendering from structured metadata only; prose remains a
    secondary existing path note.
  - [x] Add unknown safe value fixture proving no stronger classification.
    Covered by preserving producer metadata without changing classification.
  - [x] Add deterministic Markdown and JSON output assertions.
  - [x] Assert note ordering is deterministic when both
    `StaticRouteFlowContext:` and `StaticTerminalContext:` notes are present.
    Covered by existing deterministic full-output assertions and the unchanged
    path-note ordering path.
  - [x] Add a WriteAsync/full-output fixture or equivalent test proving the
    Markdown report contains or omits terminal-context wording as intended.
  - [x] Add a negative HTTP route/client or include-terminal-context-false
    fixture proving no terminal-context cue appears when the structured key is
    absent.
  - [x] Assert existing readers can ignore unknown safe metadata.
  - [x] Assert no positive runtime, database execution, dependency execution,
    impact, complete coverage, release-safety, public/demo, LLM, embedding,
    vector, or answer-generation claims are introduced; explicit negated
    disclaimers such as "not runtime execution" are allowed.
  - [x] Assert unsafe metadata/note text is not echoed.
  - [x] Assert rule IDs, tiers, supporting IDs, spans, commit SHA, extractor
    versions, coverage labels, and limitations are preserved where available.

- [x] 5. Close documentation.
  Requirements: 5, 6.
  - [x] Update local docs for property-flow report terminal context if a
    suitable docs target exists.
  - [x] Update validation docs only if focused validation commands change.
    Not needed; validation commands are unchanged.
  - [x] Document hidden static-only semantics, partial/reduced coverage
    behavior, absence-as-unknown, and downstream-consumer boundaries.
  - [x] Do not edit `site/` or public product copy.
  - [x] Do not promote public claim level above hidden.

- [x] 6. Validate implementation PRs.
  Requirements: 7.
  - [x] Run focused property-flow tests.
  - [x] Run `dotnet test src/dotnet/TraceMap.sln` unless explicitly narrowed
    with a recorded reason.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Run `git diff --check`.

## Deferred Follow-Ups

- Docs-export terminal-context metadata/chunk implementation.
- Vault hidden/local terminal-context graph/navigation implementation.
- Public/demo/site terminal-context claim work.
- Persisted terminal-context tables or schema migrations.
- New scanner facts or reducer impact conclusions.
- Runtime/browser/live HTTP/database validation.
- Raw source snippet export.
- AI/LLM classification, embeddings, vector databases, prompt-based analysis,
  or answer generation in TraceMap core.
