# Static Dispatch Candidate Bridges Implementation State

## Branch

- Branch: `codex/impl-static-dispatch-candidate-bridges`
- Base: `origin/dev`
- Scope: first product slice for paths behavior preservation and shared static
  dispatch candidate builder extraction
- Suggested PR target: `dev`

## Current Status

Status: `implementation-slice-complete-awaiting-pr-loop`

This branch implements a coherent first product slice: it preserves existing
`tracemap paths` candidate behavior while extracting the in-memory interface
and override candidate derivation into an internal reporting-layer
`StaticDispatchCandidateBuilder` model that future consumers can adapt.

The slice intentionally does not add DI registration annotations, type-level
fallback candidates, reverse/impact/report/vault/docs-export consumption, new
persisted candidate tables, or new scanner behavior.

## Scope Decisions

- Candidate bridges are static candidate evidence only. They do not prove
  runtime dispatch, runtime DI binding, selected implementations, production
  traffic, or runtime impact.
- Reuse `combined.dispatch-candidate.v1` and `combined.dispatch-gap.v1` for
  shared candidate edge/gap semantics unless a future implementation adds a
  documented successor rule before emitting product behavior.
- Route-flow, reverse, impact, report, vault, and docs-export should preserve
  their consumer-specific presentation rule IDs while carrying dispatch
  candidate rule IDs in supporting evidence.
- DI registration support is an annotation on relationship-backed candidates,
  not proof of runtime service selection.
- Open generics, factories, scanning, keyed/named services, decorators, service
  locators, reflection, config, dynamic branches, and custom containers remain
  review context or gaps in v1.
- Candidate output must be deterministic, capped, stable-ID-backed,
  public-safe, and review-tier.

## Files

- `src/dotnet/TraceMap.Reporting/StaticDispatchCandidateBuilder.cs`
- `src/dotnet/TraceMap.Reporting/CombinedDependencyPaths.cs`
- `src/dotnet/tests/TraceMap.Tests/CombinedDependencyPathTests.cs`
- `.kiro/specs/static-dispatch-candidate-bridges/requirements.md`
- `.kiro/specs/static-dispatch-candidate-bridges/design.md`
- `.kiro/specs/static-dispatch-candidate-bridges/tasks.md`
- `.kiro/specs/static-dispatch-candidate-bridges/implementation-state.md`
- `.kiro/specs/static-dispatch-candidate-bridges/review-prompts.md`

## Implementation Slice Notes

- Audited the existing `tracemap paths` implementation in
  `CombinedDependencyPaths.cs`: it derived in-memory `interface-candidate` and
  `override-candidate` graph edges from method-level symbol relationship
  evidence, capped fan-out at 10, emitted `DispatchCandidateFanOut` gaps, and
  capped candidate paths at `NeedsReviewPath`.
- Added `StaticDispatchCandidateBuilder` with internal candidate edge/gap
  records carrying stable IDs, algorithm ID, state, source labels, supporting
  edge IDs, relationship IDs, registration fact ID slots, file spans, rule IDs,
  evidence tiers, registration context, limitations, and gap metadata.
- Preserved current paths output shape by adapting builder results back into
  the existing `CombinedPathEdge` and `CombinedPathGap` records. No public
  paths JSON or Markdown field was added.
- Preserved the original relationship kind from combined dependency edge rows
  before normalization, so candidate derivation filters on
  `ImplementsInterfaceMember` and `Overrides` rather than inferred
  `implements` or `overrides` graph kinds.
- Added focused tests for override candidate traversal, candidate output
  byte-stability, and documented `DispatchCandidateFanOut` catalog coverage.
  Existing interface traversal and fan-out tests were preserved.
- Audited `combined.dispatch-gap.v1`: the shared builder currently emits only
  `DispatchCandidateFanOut`, which is already documented in the rule catalog.
  No broader gap vocabulary is emitted in this slice.

## Kiro Review State

Initial reviews completed with full wrapper coverage.

Review commands:

```bash
node scripts/kiro-review.mjs --phase static-dispatch-candidate-bridges --kind spec --model claude-opus-4.8 --fresh --save-review-text
node scripts/kiro-review.mjs --phase static-dispatch-candidate-bridges --kind spec --model claude-sonnet-4.5 --fresh --save-review-text
```

After patching Medium+ findings, run one bounded re-review:

```bash
node scripts/kiro-review.mjs --phase static-dispatch-candidate-bridges --kind re-review --model claude-sonnet-4.5 --fresh --save-review-text --timeout-ms 900000
```

Exact artifacts:

- Opus spec review:
  `.tmp/kiro-reviews/static-dispatch-candidate-bridges/2026-06-24T030855-125Z-spec-claude-opus-4.8.clean.md`
  and
  `.tmp/kiro-reviews/static-dispatch-candidate-bridges/2026-06-24T030855-125Z-spec-claude-opus-4.8.meta.json`
- Sonnet spec review:
  `.tmp/kiro-reviews/static-dispatch-candidate-bridges/2026-06-24T031244-230Z-spec-claude-sonnet-4.5.clean.md`
  and
  `.tmp/kiro-reviews/static-dispatch-candidate-bridges/2026-06-24T031244-230Z-spec-claude-sonnet-4.5.meta.json`

Coverage:

- Opus: full wrapper coverage, `reviewComplete = true`, `timedOut = false`.
- Sonnet: full wrapper coverage, `reviewComplete = true`, `timedOut = false`.
  The Sonnet wrapper metadata unexpectedly recorded git branch
  `codex/spec-route-flow-service-data-composition-final`, and the checkout was
  found on `codex/spec-legacy-data-model-orm-mapping-completion` after the
  review. The checkout was switched back to
  `codex/spec-static-dispatch-candidate-bridges` before patching. No product
  code was edited.
- Final Sonnet re-review:
  `.tmp/kiro-reviews/static-dispatch-candidate-bridges/2026-06-24T032048-194Z-re-review-claude-sonnet-4.5.clean.md`
  and
  `.tmp/kiro-reviews/static-dispatch-candidate-bridges/2026-06-24T032048-194Z-re-review-claude-sonnet-4.5.meta.json`.
  The re-review completed with `reviewComplete = true`, `timedOut = false`,
  and reduced coverage because Kiro reported denied `execute_bash` tool access
  under `kiro.review.wrapper.v1`. It found no remaining blockers and said the
  spec was ready to merge after local validation.

## Review Results

Medium+ findings patched:

- Mandated reading original `relationship_kind` metadata from
  `combined_symbol_relationships` rather than relying only on normalized graph
  edge kinds such as `implements` or `inherits`.
- Replaced shared emitted classification labels with internal candidate states:
  `SymbolBackedCandidate`, `WeakerCandidate`, and `CandidateGap`, plus
  consumer-specific caps.
- Clarified that existing `StaticDispatchCandidate` is a paths note code, not a
  strengthening shared classification.
- Added catalog gate language for expanding `combined.dispatch-gap.v1` or
  adding a successor before emitting registration, generic, schema, identity,
  or missing-candidate gaps.
- Added gap reconciliation for `RuntimeBindingNotProven`,
  `DynamicDispatchBoundary`, `RegistrationCompatibilityUnproven`,
  `UnsupportedRegistrationShape`, and `DispatchCandidateFanOut`.
- Renamed DI wording from registration-supported to registration-context so
  static registration evidence does not imply runtime binding.
- Added type-level-only bridge behavior, explicit interface implementation
  symbol guidance, override depth bounds, volatile ID handling, route-flow
  `interface-bridge` row schema, vault/docs-export rule audits, and missing
  tests for byte stability, forbidden wording, and DI compatibility gaps.
- Final re-review non-blocking suggestion patched by clarifying that existing
  gap codes should be reused where semantics already match rather than
  creating parallel aliases.

## Validation State

Current implementation validation:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedDependencyPathTests
git diff --check
./scripts/check-private-paths.sh
dotnet test src/dotnet/TraceMap.sln
./scripts/smoke-combined-paths.sh
```

Results:

- Focused `CombinedDependencyPathTests`: passed, 27 tests.
- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- `dotnet test src/dotnet/TraceMap.sln`: passed, 644 tests.
- `./scripts/smoke-combined-paths.sh`: initially stopped because `tsc` was
  unavailable. `node`/`npm` were present, Homebrew `typescript` was not
  installed, and `src/typescript/node_modules` was missing. Restored pinned
  dependencies with `npm ci --prefix src/typescript`, then reran the smoke
  successfully.
- NuGet emitted the existing `SQLitePCLRaw.lib.e_sqlite3` NU1903 high-severity
  advisory warning during restore/build.
- The combined paths/reverse smoke completed against checked-in samples and
  verified scan/combine/report/paths/reverse behavior plus repeated targeted
  paths JSON byte stability.
- After the ACK-authorized review patch, the same focused tests, `git diff
  --check`, private-path scan, full `.NET` test suite, and combined paths smoke
  passed again.

## Safety Notes

The spec avoids private local paths in examples, raw source snippets, raw SQL,
raw config values, URLs, hostnames, raw remotes, private labels, and secrets.
Implementation PRs must keep generated artifacts public-safe or hidden/local as
appropriate.

## Follow-Up Items

- DI registration-context annotations remain deferred to implementation task 7.
- Type-level fallback candidates, generic/reduced-coverage/schema/identity gaps,
  and broader gap vocabulary remain deferred.
- Route-flow, reverse, impact, report/portfolio, vault, and docs-export
  consumption remain deferred to later slices.
- Record the final PR-loop ACK decision in the handoff.

## PR Review Loop Notes

- Initial ACK run with installed `agent-control` returned
  `environment_blocked` / `LOCAL_BUILD_STALE` because local `agent-control-kit`
  `dist` output was stale relative to source.
- Ran `npm run build` in `<agent-control-kit checkout>`,
  then reran the installed `agent-control pr-loop`.
- ACK posted/observed the required Codex review request, waited for the
  configured Codex/Qodo required-review batch, then returned
  `decision=actionable_findings`, `stopReason=UNRESOLVED_REVIEW_THREADS`, and
  `patchAuthorized=true`.
- Patched authorized review findings by reducing the candidate node map to
  relationship endpoint nodes only, deferring extractor-version lookup through
  a resolver used only for gaps, using direct node lookup after filtering, and
  reusing a static default limitations array.
- Final ACK decision is pending after the review-fix push.
