# Event Message Flow Composition Tasks

Status: spec-ready-reviewed

## Spec Authoring Tasks

- [x] Create requirements for hidden event/message flow composition.
- [x] Create design covering claim boundaries, vocabulary, rule catalog
      obligations, PR 1 scope, safety, classifications, tests, and deferrals.
- [x] Create implementation-state and review-prompts files.
- [x] Keep implementation tasks unchecked for this spec-only PR.

## Implementation Tasks

- [ ] 1. Verify current context. Requirements: 1, 2, 3.
  - [ ] Fetch `origin/dev` and confirm the implementation branch is based on
        current `dev`.
  - [ ] Re-read `.kiro/specs/event-message-surfaces/implementation-state.md`
        and confirm it remains `implemented-v1-with-follow-ups`.
  - [ ] Inspect live `rules/rule-catalog.yml`, message surface code, combined
        report/query code, and existing message tests before editing product
        code.
  - [ ] Confirm whether `message.flow.context.v1`, `message.flow.gap.v1`, or
        equivalent existing composition rules already exist in the live catalog;
        document the decision to add new, reuse existing, or use a hybrid in
        `implementation-state.md` before proceeding to Task 2.
  - [ ] Record branch, base SHA, scope choices, and validation in this spec's
        `implementation-state.md`.

- [ ] 2. Add shared flow-context vocabulary and catalog entries. Requirements:
      1, 2, 5, 6.
  - [ ] Add or reuse rule catalog entries before emitting any new rule IDs,
        context kinds, gap kinds, or classification strings.
  - [ ] If new rules are needed, add `message.flow.context.v1` and
        `message.flow.gap.v1` with limitations for static-only review context,
        no delivery proof, no payload compatibility, no impact proof, partial
        coverage, and unsafe value omission.
  - [ ] Add closed gap-kind constants for the PR 1 vocabulary or document reuse
        of existing closed strings.
  - [ ] Add catalog tests proving every emitted rule ID and limitation exists.
  - [ ] Do not add LLM, embedding, vector database, broker, telemetry, schema
        registry, or runtime execution behavior.

- [ ] 3. Implement exactly one report/query consumer path for PR 1.
      Requirements: 3, 4, 5, 6.
  - [ ] Use `tracemap report` hidden `messageReviewContext` as the default
        first consumer unless Task 1 inspection finds a narrower existing
        report/query path that already handles message surfaces; document the
        choice and rationale in `implementation-state.md` before implementation.
  - [ ] Read only existing combined artifacts and derived rows.
  - [ ] Emit context rows for existing message surface inventory, static
        `message-publish-consume` candidate edges, one-sided evidence, and
        binding-only evidence.
  - [ ] Emit explicit gaps for no compatible evidence, unsupported schema,
        reduced coverage, dynamic destination, hashed destination, unsafe
        omission, ambiguity, duplicate identity, no static destination match,
        and truncation.
  - [ ] Preserve rule IDs, evidence tiers, supporting fact IDs, supporting edge
        IDs, source labels, commit SHAs, extractor versions, file spans,
        coverage labels, caveats, and limitations.
  - [ ] Keep classifications review-tier or unknown; do not emit impact,
        delivery, payload-compatibility, or runtime claims.

- [ ] 4. Keep output deterministic and safe. Requirements: 4, 5, 6.
  - [ ] Add stable JSON fields with `claimLevel`, status, coverage label, rows,
        gaps, and limitations.
  - [ ] Render Markdown using existing safe escaping helpers.
  - [ ] Preserve deterministic ordering for rows and gaps.
  - [ ] Apply bounded caps and emit truncation gaps when caps are reached.
  - [ ] Confirm output excludes raw payload values, secrets, config values,
        connection strings, raw remotes, local absolute paths, raw source
        snippets, raw broker URLs, raw hostnames, raw subscription group IDs,
        and unsafe raw destination values.

- [ ] 5. Add focused tests. Requirements: 2, 4, 5, 6, 7.
  - [ ] Test rule catalog coverage for any new rule IDs.
  - [ ] Test JSON empty-section shape and hidden claim level.
  - [ ] Test that `claimLevel` is always `hidden`.
  - [ ] Test JSON field-set stability across empty, partial, and populated
        states.
  - [ ] Test Markdown static-only wording and escaping.
  - [ ] Test Markdown/JSON do not contain forbidden runtime or impact wording.
  - [ ] Test candidate edge context with publisher and consumer support IDs.
  - [ ] Test candidate edge context is labeled static destination-match evidence,
        not a call edge, delivery edge, or runtime subscription edge.
  - [ ] Test one-sided publisher evidence and one-sided consumer evidence.
  - [ ] Test binding-only evidence.
  - [ ] Test dynamic, hashed, unsafe, ambiguous, duplicate, and reduced-coverage
        gap behavior.
  - [ ] Test `MessageContextNoStaticDestinationMatch` when publisher and
        consumer evidence exists without a safe shared destination.
  - [ ] Test `MessageContextDirectionUnsupported` when the chosen consumer path
        accepts a direction filter but cannot honor it; if deferred, document
        the reason and deferral in `implementation-state.md` rather than leaving
        the checkbox silently satisfied.
  - [ ] Test `message.flow.gap.v1` composition gaps remain distinct from
        extraction-layer `message.surface.gap.v1` reasons.
  - [ ] Test `message.flow.context.v1` rows keep `evidenceTier =
        Tier4Unknown` even when all supporting message surface facts carry
        Tier1Semantic or Tier2Structural evidence.
  - [ ] Test no-compatible-evidence under credible full coverage separately
        from reduced-coverage gaps.
  - [ ] Test truncation and deterministic ordering.
  - [ ] Test private-output safety for generated report/query artifacts.

- [ ] 6. Validate implementation. Requirements: 7.
  - [ ] Run focused message/report tests for the chosen consumer path.
  - [ ] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [ ] Run `./scripts/check-private-paths.sh`.
  - [ ] Run `git diff --check`.
  - [ ] Confirm the diff touches only intended product, test, docs, and spec
        files for the implementation slice.
  - [ ] Update this spec's `tasks.md` checkboxes and
        `implementation-state.md` after implementation completes.

## Deferred Follow-Ups

- [ ] Reducer context over message evidence for contract deltas.
- [ ] Release-review import of message context as review checklist/context.
- [ ] Route-flow async message boundary rendering.
- [ ] Deeper path/reverse graph composition beyond existing message selectors.
- [ ] Roslyn Tier1 message extraction.
- [ ] TypeScript, Python, and JVM event/message adapter support.
- [ ] Public-safe docs/site promotion after hidden output has been reviewed.
