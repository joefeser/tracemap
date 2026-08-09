# Static Dispatch Candidate Bridges Implementation State

Status: implementation-task-8-complete-awaiting-pr
Readiness: ready-for-pr-review
Merged PR 1: #331 (`086ad376e387ea8d87e430175ef2673cbc74c0f1`)
Merged PR 2: #333 (`84f72e0faa9dd6c106c625de175a194d9c1515ff`)

## Branch

- Branch: `codex/static-dispatch-route-flow`
- Base: `origin/dev`
- Base SHA: `6a8b5bd14187e5258a9805ee73cad6ff66cb9079`
- Scope: Task 8 route-flow consumption of the existing shared dispatch
  candidate edges and gaps
- Suggested PR target: `dev`

## Task 7 Pre-Implementation Decision

- The shipped `DependencyRegistered` fact under
  `csharp.semantic.runtimeevidence.v1` already carries deterministic
  `registrationKind`, `serviceType`, and `implementationType` properties plus
  evidence tier, rule ID, file span, commit SHA, and extractor provenance.
- The audit found that display names alone are insufficient for safe
  cross-assembly correlation. The existing fact is therefore augmented with
  additive service/implementation type symbol IDs and a closed-set
  `registrationShape`; no combined-index schema change is required.
- Registration evidence may annotate or order an existing relationship-backed
  candidate only when the service and implementation type identities agree
  with that candidate. It must never create a candidate edge by itself.
- Unsupported, generic, or compatibility-unproven registration observations
  remain explicit rule-backed gaps and do not claim runtime binding.
- This slice does not implement type-level fallback candidates or later
  route-flow, reverse, impact, report, vault, or docs-export consumer tasks.

## Current Implementation State

This branch completes Task 8 over the Task 6/7 shared builder. Route-flow now
consumes the candidate edges and gaps already present in the combined path
inventory instead of reconstructing them from normalized `implements` and
`overrides` labels. This preserves candidate IDs, underlying dispatch rule IDs,
supporting relationship IDs, registration context, registration fact IDs, and
fan-out counts while keeping route-flow presentation under
`combined.route-flow.interface-bridge.v1`.

The slice intentionally does not add type-level fallback candidates,
reverse/impact/report/vault/docs-export consumption, or new persisted candidate
tables. It does not add extraction rules or infer runtime DI selection.

## Task 8 Pre-Implementation Decision

- The route-flow consumer already invoked `StaticDispatchCandidateBuilder`,
  but did so through a second adapter over normalized relationship edges.
  That adapter discarded the original relationship kind, canonical containing
  type identities, shared candidate ID, and Task 7 registration annotations.
- `CombinedDependencyPathReporter.BuildGraphInventoryAsync` already returns
  the shared builder's `combined.dispatch-candidate.v1` edges and
  `combined.dispatch-gap.v1` gaps. Task 8 therefore needs no new extraction or
  persisted schema; route-flow can consume those existing in-memory records.
- Route-flow candidate rows remain additive presentation rows. Existing entry,
  call, HTTP, SQL, data-surface, and logic row behavior remains unchanged.
- Fan-out gap records now retain structured candidate count and limit values so
  route-flow can report exact candidate and omitted counts without parsing
  human-readable gap messages.

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

- `src/dotnet/TraceMap.Core/CSharpSemanticExtractor.cs`
- `src/dotnet/TraceMap.Reporting/CombinedDependencyPaths.cs`
- `src/dotnet/TraceMap.Reporting/CombinedRouteFlowReport.cs`
- `src/dotnet/TraceMap.Reporting/StaticDispatchCandidateBuilder.cs`
- `src/dotnet/tests/TraceMap.Tests/CSharpSemanticExtractorTests.cs`
- `src/dotnet/tests/TraceMap.Tests/CombinedDependencyPathTests.cs`
- `src/dotnet/tests/TraceMap.Tests/CombinedRouteFlowTests.cs`
- `rules/rule-catalog.yml`
- `.kiro/specs/static-dispatch-candidate-bridges/tasks.md`
- `.kiro/specs/static-dispatch-candidate-bridges/implementation-state.md`

## Implementation Slice Notes

Task 8 route-flow slice:

- Removed route-flow's duplicate candidate reconstruction over normalized
  `implements`/`overrides` edges. Route-flow now consumes the shared
  `interface-candidate` and `override-candidate` edges by underlying dispatch
  rule ID.
- Preserved `combined.route-flow.interface-bridge.v1` as the presentation rule
  while carrying `combined.dispatch-candidate.v1` in supporting rule IDs.
- Added optional route-flow row fields for shared candidate ID, supporting call
  and relationship edge IDs, registration context, registration fact IDs,
  candidate count, omitted count, candidate limit, and cap reason. Non-candidate
  row JSON remains unchanged because optional fields are omitted when absent.
- Propagated shared registration, generic, compatibility, fan-out, and
  truncation gap kinds instead of collapsing them into an unknown route-flow
  gap.
- Added structured candidate count and limit fields to dispatch fan-out gaps;
  route-flow derives exact omitted counts without parsing gap prose.
- Added end-to-end route-flow tests for DI-context provenance and override
  candidates. Existing focused tests continue to cover single/multiple/no
  candidates, high fan-out, reduced Tier3 evidence, deterministic output, and
  forbidden runtime wording.
- The PR #611 review pass fixed three current-head findings: call provenance now
  scans backward across intermediate traversable edges to the nearest bounded
  call; shared dispatch gaps reduce report coverage and fan-out/truncation marks
  the summary partial; and Markdown now renders the same candidate identity,
  provenance, registration, count, limit, and cap metadata exposed in JSON.
- The exact-head Codex follow-up fixed three additional P2 findings: nullable
  candidate gap metadata is omitted from unrelated path JSON; override-depth
  truncation carries its limit/reason while leaving unknowable total and
  omitted counts unset; and shared dispatch gaps affect route-flow only when
  their abstraction participates in a selected route path.
- A later exact-head Codex P2 tightened that scope boundary: dispatch gaps now
  follow bounded nodes reached from selected traversal roots, including
  incomplete branches omitted from successful terminal paths. Disconnected
  graph-wide gaps remain excluded.
- The next exact-head follow-up aligned the auxiliary reachability walk with
  traversal queue-frontier semantics rather than treating the frontier as a
  cumulative node cap, and generalized duplicate removal to every shared
  dispatch gap kind using gap kind plus affected abstraction identity.

- Audited the shipped `DependencyRegistered` shape and added deterministic
  service/implementation type symbol IDs plus a closed-set registration shape
  so same-name types in different assemblies cannot be correlated by label.
- Added registration projection into the shared candidate builder. Only
  closed, strong registration facts whose canonical type IDs agree with the
  containing-type IDs on an existing member relationship may annotate a
  candidate.
- Candidate IDs now incorporate sorted registration fact IDs; candidate
  supporting fact IDs and optional paths JSON fields preserve those IDs.
- Added rule-backed `RegistrationCompatibilityUnproven`,
  `UnsupportedRegistrationShape`, and `GenericCandidateNeedsReview` gaps with
  commit, span, extractor, scope, and supporting-fact provenance.
- Old indexes or syntax-only observations without canonical type identities
  fail closed as compatibility gaps. Registrations never create candidate
  edges.
- Added coverage for matching and mismatched registrations, same-display
  cross-assembly identities, multiple registrations, open generics,
  unsupported/keyed-style shapes, syntax-only observations, deterministic
  ordering, safe wording, and end-to-end paths projection.

- Preserved current paths output shape. No public paths JSON or Markdown field
  was added.
- Kept interface member candidate derivation tied to
  `ImplementsInterfaceMember` relationship endpoints. Added an explicit
  interface implementation fixture where the implementation member display name
  differs from the interface member, proving the path candidate is derived from
  relationship identity rather than display-name equality.
- Split interface and override derivation in the shared builder so override
  candidates are derived only from `Overrides` relationship evidence.
- Added bounded override-chain traversal with deterministic ordering, cycle
  protection, candidate cap reuse, weakest-evidence-tier propagation, and a
  documented max override traversal depth of 5.
- After ACK-authorized review, precomputed the override target map once per
  build, pruned duplicate override subtree traversal, normalized unknown
  evidence tiers to `Tier4Unknown`, and emitted a documented
  `DispatchCandidateTruncatedByLimit` gap when override traversal reaches the
  depth cap while deeper `Overrides` evidence exists.
- Added focused tests for explicit interface candidate traversal, override
  chain traversal under a tight path depth, override-chain Markdown/JSON byte
  stability, direct builder depth/cycle protection, and the override-depth
  truncation gap.
- Updated `combined.dispatch-candidate.v1` limitations in
  `rules/rule-catalog.yml` to document the override-chain depth bound and
  cycle protection.
- The shared builder now emits `DispatchCandidateFanOut` and
  `DispatchCandidateTruncatedByLimit` gaps. Broader missing/identity/generic
  and reduced-coverage gap vocabulary remains deferred.

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

- Task 8 focused route-flow/path/catalog validation: passed, 115 tests.
- Full `.NET` solution validation after Task 8: passed, 1,306 tests.
- ACK-authorized PR #611 review-patch validation: focused route-flow/path tests
  passed, 112 tests; full `.NET` solution passed, 1,307 tests.
- Exact-head Codex follow-up validation: focused route-flow/path tests passed,
  114 tests; full `.NET` solution passed, 1,309 tests; targeted format,
  private-path guard, and diff check passed.
- Reachable-incomplete-branch follow-up validation: focused route-flow/path
  tests passed, 115 tests; full `.NET` solution passed, 1,310 tests; targeted
  format, private-path guard, and diff check passed.
- Frontier/deduplication follow-up validation: focused route-flow/path tests
  passed, 121 tests; full `.NET` solution passed, 1,316 tests; targeted format,
  private-path guard, and diff check passed.
- `dotnet format --verify-no-changes` over changed C# files: passed.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- Restored the pinned TypeScript toolchain with `npm ci` and built it for the
  required combined smoke. npm repeated two existing high-severity dependency
  advisories; no dependency files changed.
- `./scripts/smoke-combined-paths.sh
  /tmp/tracemap-static-dispatch-route-flow-smoke`: passed over checked-in
  public samples.
- Ran `tracemap route-flow` twice against that generated combined index with
  the same output path and verified Markdown/JSON byte identity. The sample
  correctly reported reduced coverage and `UnknownAnalysisGap`; it contains no
  member relationship facts, so shared candidate projection is covered by the
  focused end-to-end fixtures rather than claimed from this smoke.

- Focused `CombinedDependencyPathTests` plus
  `CSharpSemanticExtractorTests`: passed, 44 tests after review patches.
- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- `dotnet test src/dotnet/TraceMap.sln`: passed, 1,303 tests after review
  patches.
- `./scripts/smoke-combined-paths.sh`: initially stopped because `tsc` was
  unavailable. Homebrew did not have `typescript` installed/listed, and
  `src/typescript/node_modules` was missing. Restored pinned dependencies with
  `npm ci --prefix src/typescript`, then reran the smoke successfully.
- `npm ci --prefix src/typescript` repeated two existing high-severity npm
  dependency advisories; no dependency mutation was made in this slice.
- The combined paths/reverse smoke completed against checked-in samples and
  verified scan/combine/report/paths/reverse behavior plus repeated targeted
  paths JSON byte stability.
- After the ACK-authorized review patch, focused
  `CombinedDependencyPathTests`, `git diff --check`, private-path scan, full
  `.NET` solution tests, and combined paths/reverse smoke passed again.

## Safety Notes

The spec avoids private local paths in examples, raw source snippets, raw SQL,
raw config values, URLs, hostnames, raw remotes, private labels, and secrets.
Implementation PRs must keep generated artifacts public-safe or hidden/local as
appropriate.

## Follow-Up Items

- Type-level fallback candidates remain deferred within task 6.
- Missing-candidate, ambiguous-identity, reduced-coverage, schema, and generic
  gaps remain deferred within task 6.
- Reverse, impact, report/portfolio, vault, and docs-export consumption remain
  deferred to later slices. Route-flow consumption is complete in PR #611.
- The selected Task 6 slice merged through PR #333; later tasks remain explicit
  follow-ups.

## PR Review Loop Notes

- PR #610 initial ACK at `a50e93fbe8fd515e517051433a045d5f739956b2`
  returned `actionable_findings / UNRESOLVED_REVIEW_THREADS` with five
  unresolved threads and one actionable finding.
- Patched the two Codex P2 findings by ranking registration-compatible
  candidates before the fan-out cap and preserving semantically declared
  closed constructed generic registrations. Exact constructed type display
  and canonical type identity must both agree.
- Also removed the guarded Roslyn null-forgiving operator and indexed strong
  closed registration facts once, addressing Qodo's narrow reliability and
  performance findings. Qodo's generic finding was a duplicate of the Codex
  P2.
- Added regressions for a registered candidate at position 11 under a cap of
  10 and a closed constructed generic registration.

- Initial ACK returned `decision=actionable_findings`,
  `stopReason=UNRESOLVED_REVIEW_THREADS`, `patchAuthorized=true`, and
  `canMerge=false` for PR #333 at head
  `73d4289836da9d31b41ea02e3325b7679f1a48a9`.
- Patched the authorized findings by precomputing the override target map,
  pruning repeated override traversal, normalizing unknown evidence tiers to
  `Tier4Unknown`, and adding a depth-cap truncation gap.
- Final state: PR #333 merged into `dev` as
  `84f72e0faa9dd6c106c625de175a194d9c1515ff` from exact reviewed head
  `c5d79cbdb2ef9e01d1265e3ff8218bf96d2e88e8`.
