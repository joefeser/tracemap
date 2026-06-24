# Static Dispatch Candidate Bridges Tasks

Status: spec-ready

Readiness: ready-for-implementation after recorded Kiro reviews and validation.

## Spec Delivery Tasks

- [x] 1. Create the spec folder.
  - [x] Add `requirements.md`.
  - [x] Add `design.md`.
  - [x] Add `tasks.md`.
  - [x] Add `implementation-state.md`.
  - [x] Add `review-prompts.md`.

- [x] 2. Draft the spec scope.
  - [x] Reframe the broad interface/override/DI approximation backlog into a
        shared static candidate bridge slice.
  - [x] State that candidates are static evidence only and not runtime binding
        proof.
  - [x] Define internal candidate states and emitted consumer classification
        caps.
  - [x] Specify rule IDs, limitations, deterministic ordering, caps, stable
        IDs, output safety, and consumer downgrades.
  - [x] Cover interface implementations, explicit interface implementations,
        overrides, generics/open generics, multiple candidates, high fan-out,
        reduced semantic coverage, and DI registration caveats.
  - [x] Cover route-flow, reverse, impact/include-paths, report/portfolio,
        vault export, and docs-export/RAG-oriented consumption.

- [x] 3. Review the spec before implementation starts.
  - [x] Run Opus Kiro spec review when available, or record the exact timeout
        or auth/tooling blocker.
  - [x] Run Sonnet Kiro spec review when available, or record the exact
        timeout or auth/tooling blocker.
  - [x] Patch all Medium+ actionable findings.
  - [x] Patch Low findings only when narrow and safe.
  - [x] Run one bounded Sonnet or Opus re-review after patches.
  - [x] Record commands, artifacts, coverage, findings, and dispositions in
        `implementation-state.md`.

- [x] 4. Validate the spec-only change.
  - [x] Run `git diff --check`.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Confirm the diff is limited to
        `.kiro/specs/static-dispatch-candidate-bridges/`.
  - [x] Record validation results in `implementation-state.md`.

## Implementation Tasks

Implementation tasks are intentionally unchecked. They are future product work,
not part of this spec-only branch.

- [ ] 5. Audit and preserve existing paths behavior.
  - [ ] Inspect the current `tracemap paths` candidate implementation and tests
        from the interface/override/DI paths slice.
  - [ ] Add or preserve focused tests for interface member and override
        candidate derivation before refactoring shared code.
  - [ ] Confirm `combined.dispatch-candidate.v1` and
        `combined.dispatch-gap.v1` catalog entries cover the shared builder
        limitations, or update the catalog before behavior changes.
  - [ ] Preserve existing path Markdown/JSON output unless an additive field is
        explicitly covered by tests.
  - [ ] Add a byte-identical characterization test proving shared-builder
        extraction preserves existing paths output before additive candidate
        behavior lands.
  - [ ] Audit `combined.dispatch-gap.v1` in `rules/rule-catalog.yml`; if it
        only lists `DispatchCandidateFanOut`, expand it or add a successor rule
        for the complete closed gap vocabulary before implementation emits new
        candidate gaps.
  - [ ] Add rule-catalog resolution tests for all candidate gap kinds emitted
        by the shared builder.

- [ ] 6. Extract a shared candidate builder.
  - [ ] Define an internal candidate edge/gap model with stable IDs,
        supporting fact IDs, supporting edge IDs, relationship IDs,
        registration fact IDs, file spans, rule IDs, evidence tiers, coverage
        state, and limitations.
  - [ ] Read original `relationship_kind` metadata from
        `combined_symbol_relationships` or equivalent relationship rows; do
        not rely only on normalized graph edge kinds such as `implements` or
        `inherits`.
  - [ ] Derive member-level interface candidates only from
        `ImplementsInterfaceMember` evidence.
  - [ ] Derive explicit interface implementation candidates by symbol ID, not
        display-name equality.
  - [ ] Derive override candidates only from `Overrides` evidence and bounded
        override-chain traversal.
  - [ ] Keep type-level fallback candidates as `WeakerCandidate` with
        type-level-bridge-only limitations.
  - [ ] Emit gaps for missing candidates, ambiguous identities, high fan-out,
        reduced coverage, missing schema, generic caveats, and truncation.
  - [ ] Add deterministic ordering, caps, cycle protection, and byte-stability
        tests.

- [ ] 7. Add DI registration-context annotations.
  - [ ] Audit existing `DependencyRegistered` fact shape and fact-symbol
        attachments.
  - [ ] Annotate candidates as `registration-context-candidate` only when DI
        service/implementation evidence and relationship compatibility agree.
  - [ ] Emit `RegistrationCompatibilityUnproven` when registration evidence
        lacks relationship compatibility.
  - [ ] Emit unsupported registration gaps for factories, scanning,
        keyed/named services, decorators, service locators, reflection,
        config, dynamic branches, custom containers, and open generics.
  - [ ] Add tests for multiple registrations, open generics, syntax-only
        registrations, deterministic ordering, and safety.

- [ ] 8. Thread candidates into route-flow.
  - [ ] Reuse the shared candidate builder for route-flow interface/override
        bridge rows.
  - [ ] Define `interface-implementation-candidate` row kind schema compatible
        with existing route-flow row processing.
  - [ ] Preserve existing route-flow row kinds such as `method`,
        `http-client`, and `sql-query`.
  - [ ] Preserve `combined.route-flow.interface-bridge.v1` presentation rows
        with supporting `combined.dispatch-candidate.v1` rule IDs.
  - [ ] Cap affected rows at `NeedsReviewStaticRouteFlow` or
        `UnknownAnalysisGap`.
  - [ ] Add tests for single candidate, multiple candidate, no candidate,
        high fan-out, DI-context candidate, and reduced coverage.
  - [ ] Add forbidden-wording tests for runtime binding and selected
        implementation language.

- [ ] 9. Thread candidates into reverse and impact context.
  - [ ] Let reverse traversal cross candidate edges while preserving root to
        surface path order.
  - [ ] Cap candidate-dependent reverse roots/paths at
        `NeedsReviewReversePath` or weaker.
  - [ ] Ensure `tracemap impact --include-paths` and future reverse context
        preserve candidate paths as review context only.
  - [ ] Prevent candidate context from producing `StaticImpactEvidence`,
        `DefiniteImpact`, or runtime impact wording.
  - [ ] Add focused reverse and impact tests for candidate paths, no-candidate
        gaps, reduced coverage, and truncation.

- [ ] 10. Add report, portfolio, vault, and docs-export consumption.
  - [ ] Summarize candidate counts, registration-context counts, gaps,
        fan-out caps, source coverage, and limitations in combined report and
        portfolio report.
  - [ ] Audit existing `vault-export.*` and `docs-export.*` rule IDs in the
        catalog.
  - [ ] Add new vault/docs candidate edge or gap rules to the catalog if no
        existing presentation rule can honestly wrap candidate evidence.
  - [ ] Ensure vault graph and docs-export candidate artifacts cite both the
        consumer presentation rule and the underlying dispatch candidate/gap
        rules.
  - [ ] Export candidate edges and gaps to vault graph as review-context edges,
        not ordinary call edges.
  - [ ] Include candidate chunks in docs-export/RAG-ready evidence docs with
        rule IDs, tiers, supporting IDs, coverage labels, and limitations.
  - [ ] Add safety tests for raw snippets, SQL, config, URLs, hostnames, raw
        remotes, local paths, private labels, and secrets.
  - [ ] Add byte-stability tests for JSON, Markdown, vault graph, and
        docs-export outputs where those commands promise stability.

- [ ] 11. Validate implementation.
  - [ ] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [ ] Run the CLI against at least one public-safe sample and confirm
        candidate output/gaps are deterministic and safe.
  - [ ] Follow `docs/VALIDATION.md` and run or explicitly defer relevant
        pinned smoke checks with evidence.
  - [ ] Run `./scripts/check-private-paths.sh`.
  - [ ] Run `git diff --check`.
  - [ ] Confirm generated outputs do not contain unsafe values or runtime proof
        language.

## Deferred Follow-Ups

- Persisted candidate edge tables in combined indexes.
- Runtime DI container execution or validation.
- Dynamic proxy or reflection target expansion.
- Custom container plugin model.
- Cross-language runtime binding approximation.
- UI or interactive graph visualization.
- Site copy describing the shipped capability.
