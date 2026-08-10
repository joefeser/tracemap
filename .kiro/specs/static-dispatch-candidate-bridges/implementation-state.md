# Static Dispatch Candidate Bridges Implementation State

Status: implementation-complete-awaiting-review
Readiness: ready-for-review
Merged PR 1: #331 (`086ad376e387ea8d87e430175ef2673cbc74c0f1`)
Merged PR 2: #333 (`84f72e0faa9dd6c106c625de175a194d9c1515ff`)
Merged PR 3: #610 (`6a8b5bd14187e5258a9805ee73cad6ff66cb9079`)
Merged PR 4: #611 (`b3fde3aa1c6c53a3fbae55df58313d2609fcdcfb`)
Merged PR 5: #614 (`36b133c43876811a048b046f4f5cb6eec2595a53`)
Merged PR 6: #615 (`92548cab13aeeec0e818b774828194db57990047`)

## Branch

- Branch: `codex/reconcile-static-dispatch-runway`
- Base: `origin/dev`
- Base SHA: `92548cab13aeeec0e818b774828194db57990047`
- Scope: post-merge bookkeeping and final Task 6 fail-closed gap behavior
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

Tasks 6 through 11 are complete. PR #615 completed Task 10 by projecting the same
shared in-memory dispatch candidate inventory into combined dependency reports,
portfolio reports, vault graphs, and evidence docs. The final Task 6 slice on
this branch makes absence truthfulness explicit without adding a scanner,
extractor, persisted combined-index schema, runtime probe, or DI execution path.

Every consumer keeps candidate evidence review-tier, preserves the underlying
dispatch rules, and states that runtime dispatch, selected implementation,
reachability, execution, and impact are not proven.

## Task 6 Final Boundary

- Type-level `ImplementsInterface`, `InheritsFrom`, and `ExtendsInterface`
  evidence never creates a guessed member edge. When a called member has
  matching type context but no member relationship, the builder emits
  `MemberCandidateUnavailable` with the call and type-relationship fact IDs.
- If the call or type relationship lacks canonical member/type identity, the
  builder fails closed as `DispatchCandidateIdentityUnverified`; display text
  is not treated as identity.
- Reduced semantic analysis, failed/partial builds, unsupported adapters,
  missing commit identity, or missing extractor identity emit one bounded
  `DispatchCandidateReducedCoverage` gap per relevant source. Existing member
  candidates from that source are downgraded to `WeakerCandidate` / Tier4.
- Existing report, portfolio, vault, and docs consumers already emit
  `DispatchCandidateSchemaUnavailable` when required combined schema is absent.
  The rule catalog now documents the complete closed gap vocabulary.
- Direct regressions prove deterministic IDs, supporting provenance, file
  spans, rule IDs, evidence tiers, fail-closed identity behavior, and reduced
  coverage downgrades. No runtime binding or candidate absence conclusion is
  added.

Validation for the final Task 6 slice:

- Locked solution restore: passed.
- Solution build: passed with 0 warnings and 0 errors.
- Focused static-dispatch builder and end-to-end gap tests: 10/10 passed.
- Paths, route-flow, combined report, portfolio, vault, and evidence-doc tests:
  237/237 passed.
- Full .NET solution tests: 1,343/1,343 passed.
- Targeted changed-file formatting: passed.
- Public combined paths/reverse smoke: passed after installing the locked
  TypeScript dependencies in the isolated worktree; deterministic targeted
  output and public-path safety assertions passed.
- Private-path guard and `git diff --check`: passed.

The exact-head Codex review found three Task 6 truthfulness defects. The review
patch rejects member-level relationship facts that lack canonical source or
target symbol IDs, reads the persisted per-fact extractor version instead of
substituting the source scanner version, and removes stale follow-up text that
still described Task 6 as incomplete. Focused malformed-identity and combined
readback regressions prove both product corrections.

A subsequent exact-head Codex review found that `CombinedPathGap` retained only
the first supporting fact ID even though the shared builder preserved the full
call-plus-relationship evidence set. The additive path gap contract now carries
the complete deterministic supporting-fact list while retaining the legacy
single `combinedFactId` projection. Combined report, route-flow, reverse,
release review, database design review, vault, and evidence-doc consumers use
the complete list. End-to-end coverage proves the same two supporting facts
survive paths, report summary, vault gap, and evidence-doc gap projections.

Validation after this correction:

- Focused static-dispatch and gap-projection tests: 10/10 passed.
- Paths/route-flow/report/reverse/release-review/database-design/portfolio/vault/docs
  tests: 293/293 passed.
- Full .NET solution tests: 1,343/1,343 passed.

## Task 10 Implementation

- Added one shared `DispatchCandidateEvidenceSummary` projection over the
  existing graph inventory. Combined reports summarize candidate, symbol-backed,
  weaker, registration-context, source, bridge-kind, gap, fan-out, coverage,
  supporting ID, rule, tier, and limitation state.
- Portfolio reports expose the same bounded summary under
  `portfolio.context.dispatch-candidate.v1`. Single-index portfolio inputs emit
  `DispatchCandidateSchemaUnavailable` rather than silently claiming that no
  candidates exist.
- Vault export keeps `interface-candidate` and `override-candidate` edge kinds,
  wraps them with `vault-export.graph.dispatch-candidate.v1`, and preserves
  `combined.dispatch-candidate.v1` as supporting provenance. Shared candidate
  gaps use the corresponding vault gap rule and remain review-only.
- Evidence docs emit a `weak-static-evidence` dependency-surface chunk with
  file spans, commit SHAs, extractor versions, supporting fact/edge IDs,
  coverage labels, tiers, rules, gaps, and limitations. Compatible vault graph
  inputs retain candidate classification and rules instead of being flattened
  into generic static graph metadata.
- Older/minimal combined-index shapes that cannot support graph composition
  emit a docs schema gap. Candidate absence is never inferred from an
  unavailable schema.
- Added catalog entries for the combined-report, portfolio, vault-edge,
  vault-gap, docs-chunk, and docs-gap presentation rules, each with explicit
  static-only limitations.
- Added consumer-focused tests for deterministic Markdown/JSON/docs output,
  vault provenance and edge kind, weak docs claims, schema gaps, catalog
  resolution, and forbidden stronger wording. Existing vault/docs safety suites
  continue to cover raw snippets, SQL, configuration, URLs, hostnames, remotes,
  local paths, private labels, and secrets.
- Exact-head hosted review found six bounded follow-ups. The corrected
  implementation now treats every non-`Succeeded` or non-Level1-semantic
  source as reduced, includes candidate-gap fact IDs in summaries, reuses the
  already-loaded combined read for report and portfolio graph construction,
  and includes concrete supporting fact/edge IDs in portfolio metadata.
- Vault-to-docs composition now reads canonical vault `id` fields (with the
  legacy `edgeId`/`gapId` aliases accepted), preserves matching edge and gap
  IDs plus both candidate and gap rule pairs, and recognizes legacy vault
  graphs whose top-level rule remains `combined.dispatch-candidate.v1` or
  `combined.dispatch-gap.v1`. An end-to-end generated vault fixture prevents
  hand-authored JSON from masking serialization drift.
- Fresh exact-head Codex review then identified three consumer truthfulness
  gaps. Combined report coverage now becomes reduced when dispatch derivation
  records gaps even if the ordinary warning list is empty. Portfolio dispatch
  summaries and gaps are projected per source and filtered by the active
  source/max-source selection instead of leaking excluded inputs. Vault edges
  now retain candidate state, bridge kind, and registration context, and the
  vault-to-docs projection reports those strength categories without upgrading
  them into runtime claims.
- A second exact-head review found three remaining projection inconsistencies.
  Dispatch portfolio sections now roll up complete candidates as review
  recommended and reduced/gapped candidates as partial rather than actionable.
  Portfolio gap identity includes the underlying dispatch gap ID, preventing
  same-kind gaps from collapsing during capping. Vault docs now derive their
  evidence-tier set from the matching serialized edges and gaps, so syntax or
  unknown evidence cannot be upgraded to structural evidence.
- A third exact-head review found three multi-source provenance leaks. The
  portfolio top-level gap collection now removes dispatch gaps owned only by
  excluded sources before applying the gap cap; dispatch context IDs include
  source identity; and evidence-doc candidate gaps reference only their owning
  combined source. Multi-source regressions cover all three boundaries.

Validation for Task 10:

- `dotnet restore src/dotnet/TraceMap.sln --locked-mode`: passed.
- `dotnet build src/dotnet/TraceMap.sln --no-restore`: passed with 0 warnings.
- Focused paths/report/portfolio/vault/docs tests after review corrections:
  141/141 passed.
- Full .NET solution tests: 1,333/1,333 passed.
- After the fresh exact-head Codex corrections, focused
  report/portfolio/vault/docs tests passed 105/105 and the full .NET solution
  passed 1,334/1,334 with a clean zero-warning build.
- After the second exact-head review corrections, focused consumer tests passed
  106/106 and the full .NET solution passed 1,335/1,335 with a clean
  zero-warning build.
- After the third exact-head review corrections, focused consumer tests passed
  108/108 and the full .NET solution passed 1,337/1,337 with a clean
  zero-warning build.
- Targeted changed-file whitespace formatting and verification: passed.
- Repo-wide `dotnet format --verify-no-changes`: deferred because existing
  unrelated formatting violations remain in unchanged files; no unrelated
  files were reformatted.
- Public `./scripts/smoke-combined-paths.sh`: passed after locked TypeScript
  dependency installation. The reduced public fixture rendered a deterministic
  zero-candidate summary with coverage-relative absence limitations.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.

## Task 9 Implementation

- Combined reverse already traversed shared candidate edges in root-to-surface
  order. It now explicitly caps candidate-dependent paths and roots at
  `NeedsReviewReversePath`, preserves `combined.dispatch-candidate.v1`, and
  emits a bounded static-candidate limitation note.
- Combined impact path summaries now preserve path rule IDs. Candidate paths
  cap both path context and any otherwise stronger impact item at
  `NeedsReviewImpact`; unknown coverage remains weaker and is never upgraded.
- Candidate-dependent impact IDs are rebuilt from the downgraded
  classification so deterministic identity and classification agree. Path
  gaps are then rebuilt from that final identity, preventing stale gap IDs.
- Mixed candidate and unknown path coverage remains `UnknownAnalysisGap`; the
  weaker coverage result wins while the candidate limitation stays explicit.
- The rule catalog now documents the consumer-specific reverse and impact caps
  and the mixed unknown-coverage behavior.
- Focused reverse and impact fixtures prove interface candidates remain review
  evidence and cannot produce runtime-target, selected-implementation, or
  impact-proof wording. Existing reverse/impact suites retain no-candidate,
  reduced-coverage, truncation, and byte-stability coverage.
- No candidate rows are persisted and no scanner, reducer, extraction, DI
  execution, or runtime reachability behavior is added.

Validation for Task 9 and its review corrections:

- Focused reverse/impact/path tests: 65/65 passed.
- Full .NET solution tests: 1,329/1,329 passed.
- Targeted `dotnet format --verify-no-changes`: passed.
- Private-path guard: passed.
- `git diff --check`: passed.

The exact-head Codex review then identified two presentation/aggregation gaps.
Impact Markdown now renders candidate rule IDs and the bounded candidate note
already present in JSON. Reverse root aggregation now applies the candidate cap
when any supporting path is candidate-dependent, even when a sibling path is
stronger; a direct aggregation regression proves that mixed-path invariant.

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
- `src/dotnet/TraceMap.Reporting/CombinedDependencyReport.cs`
- `src/dotnet/TraceMap.Reporting/CombinedRouteFlowReport.cs`
- `src/dotnet/TraceMap.Reporting/DispatchCandidateEvidenceProjection.cs`
- `src/dotnet/TraceMap.Reporting/EvidenceDocsExport.cs`
- `src/dotnet/TraceMap.Reporting/PortfolioReport.cs`
- `src/dotnet/TraceMap.Reporting/StaticDispatchCandidateBuilder.cs`
- `src/dotnet/TraceMap.Reporting/VaultExport.cs`
- `src/dotnet/tests/TraceMap.Tests/CSharpSemanticExtractorTests.cs`
- `src/dotnet/tests/TraceMap.Tests/CombinedDependencyReportTests.cs`
- `src/dotnet/tests/TraceMap.Tests/CombinedDependencyPathTests.cs`
- `src/dotnet/tests/TraceMap.Tests/CombinedRouteFlowTests.cs`
- `src/dotnet/tests/TraceMap.Tests/EvidenceDocsExportTests.cs`
- `src/dotnet/tests/TraceMap.Tests/PortfolioReportTests.cs`
- `src/dotnet/tests/TraceMap.Tests/StaticDispatchCandidateConsumerFixture.cs`
- `src/dotnet/tests/TraceMap.Tests/VaultExportTests.cs`
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
- The final hosted follow-up removed that auxiliary walker entirely. Route-flow
  now consumes the path engine's internal reached-node scope, so all selected
  roots, queue/frontier limits, path-local cycles, and dispatch cross-hop rules
  are shared by construction instead of reimplemented.
- An exact-head read-only Kiro review using `claude-opus-5` then identified
  mixed fan-out/depth metadata precedence, inconsistent shared-gap coverage,
  missing route-flow catalog emissions, and a redundant graph build. Those
  valid findings were corrected together: route-flow now reuses one graph for
  the path report and bounded reached-node scope, preserves known fan-out
  counts and the applied candidate cap when depth truncation also exists, and
  labels every shared dispatch gap as reduced coverage with the static/runtime
  limitation. The suggested same-endpoint client/server "leak" was not a
  defect: the combined graph intentionally connects matching client and route
  endpoints, so downstream server dispatch evidence belongs to that client
  flow. Start roots are nevertheless filtered to the requested selector side
  before computing the auxiliary scope.
- The exact-head follow-up also corrected registration-gap anchoring and
  wording: unsupported/generic/compatibility gaps are emitted for every
  deterministic matching service member, allowing node-scoped consumers to
  retain the gap for the member they actually traverse, and non-fan-out gaps no
  longer claim candidate fan-out was capped.

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
  `DispatchCandidateTruncatedByLimit` gaps. The final Task 6 slice additionally
  emits missing-member, identity-unverified, and reduced-coverage gaps; generic
  registration caveats and consumer schema gaps retain their existing explicit
  gap records.

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

- Task 9 focused reverse/impact/path validation: passed, 62 tests.
- Full `.NET` solution validation after Task 9: passed, 1,326 tests.
- `dotnet format --verify-no-changes` over changed C# files: passed.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- `./scripts/smoke-combined-paths.sh
  /private/tmp/tracemap-static-dispatch-reverse-impact-smoke`: passed over
  checked-in public samples, including repeated deterministic paths output and
  combined reverse output. The sample correctly remains reduced coverage.
- The fresh worktree initially lacked `tsc`; Homebrew did not provide the
  formula. Restored the pinned toolchain using `npm ci --prefix
  src/typescript`, then reran unchanged. npm repeated two existing
  high-severity dependency advisories; no dependency files changed.
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
- Shared traversal-scope follow-up validation: focused route-flow/path tests
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

- Type-level member guesses remain intentionally unsupported. Type-only
  relationship context emits `MemberCandidateUnavailable` instead of creating
  a weaker traversable edge.
- Member relationships without canonical source and target symbol IDs are
  rejected as `DispatchCandidateIdentityUnverified`. Missing per-fact extractor
  identity emits `DispatchCandidateReducedCoverage` and downgrades retained
  candidates.
- Generic registration caveats and unavailable consumer schemas retain their
  existing explicit gap records. No mandatory Task 6 gap work remains.
- Route-flow consumption is complete in PR #611; reverse and impact consumption
  are complete in PR #614; report, portfolio, vault, and docs-export consumption
  are complete in PR #615.
- Runtime dispatch, selected implementation, dynamic container behavior, and
  type-only candidate expansion remain outside this static-evidence runway.

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

- PR #616 exact-head Codex follow-up found two additional fail-closed gaps.
  Calls without canonical target member/type identity are now reported as
  `DispatchCandidateIdentityUnverified`, and relationship-backed candidates
  for that target are withheld rather than reached through display-derived
  node equality. Registration facts without per-fact extractor identity now
  participate in reduced-coverage aggregation, preserve their supporting fact
  IDs, and downgrade annotated candidates to review-only `Tier4Unknown`.
- Added direct regressions for both boundaries, including identity/provenance,
  source span, supporting-fact, candidate-state, and evidence-tier assertions.
- Exact-head follow-up validation passed: 17 focused static-dispatch tests,
  115 route-flow/path tests, 1,345 full `.NET` solution tests, targeted format,
  combined paths/reverse public smoke, private-path guard, and diff check.
- A fresh read-only Kiro CLI review used explicit model `claude-opus-5` against
  exact head `82ce95312bad2d1b74c60c9f6dc36c121ecb8e6c`. It found no P1s and four
  actionable P2s. The bounded patch keeps older combined indexes readable and
  emits `DispatchCandidateSchemaUnavailable` when per-fact extractor schema is
  absent; admits normalized `inherits` relationships as type-only gap context;
  preserves registration provenance when identity-less calls withhold a
  candidate; and groups/caps repeated call-context gaps with deterministic
  count metadata. Kiro remains advisory unless ACK verifies and admits its
  exact-head evidence.
- The Kiro follow-up patch passed 20 focused dispatch/schema tests, the full
  `.NET` solution at 1,347 tests, targeted format, the public combined
  paths/reverse smoke, private-path guard, and diff check.

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
