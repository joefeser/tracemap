# Interface, Override, and DI Approximation Implementation State

## Branch

- Branch: `codex/implement-interface-override-di-approximation`
- Base: `origin/dev`
- Issue: `Refs #35`
- PR: `https://github.com/joefeser/tracemap/pull/271`
- Scope: implementation slice 1

## Current Status

Implementation slice implemented in PR #271. The selected slice adds conservative
`tracemap paths` traversal over method-level interface and override dispatch
candidates derived from existing combined symbol relationship evidence.

This slice does not implement DI registration narrowing, scanner extraction
changes, reverse traversal, route-flow changes, impact/include-paths changes,
portfolio/report summaries, or export/vault rendering.

## Scope Decisions

- Treat relationship and DI evidence as static facts with rule IDs, evidence
  tiers, file spans, commit SHA provenance, and extractor versions.
- Preserve the existing TraceMap boundary: candidate dispatch edges are not
  runtime dispatch proof and DI registrations are not runtime container
  selection proof.
- Keep v1 focused on C# semantic evidence and combined-index consumption.
- Allow syntax fallback only as explicitly weaker Tier3 candidate evidence with
  separate rule limitations.
- Integrate with route-flow, paths, reverse, report, impact/include-paths, and
  export through shared graph/read helpers where possible.
- For this slice, only `tracemap paths` consumes derived candidates. Other
  consumers remain follow-up scope.
- Derived dispatch candidate edges are in-memory path edges only. No persisted
  schema rows are added.
- Candidate derivation is method-level only. Type-level inheritance/interface
  relationships are not used as member dispatch substitutes.
- Candidate paths are capped at `NeedsReviewPath` and carry explicit static
  dispatch limitations.
- Fan-out is bounded to 10 deterministic candidates per abstraction boundary;
  additional candidates produce `DispatchCandidateFanOut`.

## Files

- `.kiro/specs/interface-override-di-approximation/requirements.md`
- `.kiro/specs/interface-override-di-approximation/design.md`
- `.kiro/specs/interface-override-di-approximation/tasks.md`
- `.kiro/specs/interface-override-di-approximation/implementation-state.md`
- `.kiro/specs/interface-override-di-approximation/review-prompts.md`
- `rules/rule-catalog.yml`
- `src/dotnet/TraceMap.Reporting/CombinedDependencyPaths.cs`
- `src/dotnet/tests/TraceMap.Tests/CombinedDependencyPathTests.cs`

## Kiro Review State

- Opus spec review: completed with reduced coverage because Kiro reported
  denied tool access. Medium+ findings were patched in the spec:
  - Reframed `DependencyRegistered` and `DynamicDispatchCandidate` as existing
    scanner-level facts to audit/extend rather than greenfield facts.
  - Disambiguated scanner-level `DynamicDispatchCandidate` from derived
    combined dispatch candidate edges.
  - Added closed-set clean absence, fan-out/bounds, supporting fact ID, and
    DI-supported candidate test coverage.
- Sonnet spec review: completed with full coverage. Medium+ findings were
  patched in the spec:
  - Aligned relationship-kind vocabulary with the live scanner values
    `InheritsFrom`, `ImplementsInterface`, `ExtendsInterface`, `Overrides`, and
    `ImplementsInterfaceMember`.
  - Clarified that relationship extractor metadata and supporting fact identity
    can be supplied by joining precise relationship rows to backing facts when
    `relationship_id = fact_id`.
  - Defined the full-coverage precondition for `NoDispatchCandidateEvidence`.
  - Added ownership for dispatch fan-out thresholds and combined DI
    registration schema details.
- Re-review cycles used: 2 of 2. The first Sonnet re-review completed with
  reduced coverage due to denied tool access, found no blockers, and requested
  small clarity edits. Those edits were patched:
  - Canonicalized `ImplementsInterfaceMember` in candidate derivation prose.
  - Inlined the `relationship_id = fact_id` supporting fact ID join strategy in
    Requirement 5.
  - Added example closed-set placeholders for self/unknown registration sides.
  - Added route-flow interface bridge successor fallback guidance.
  The second and final Sonnet re-review also completed with reduced coverage
  due to denied tool access, found no blockers, and declared the spec ready to
  merge after local validation and small accuracy edits. Those edits were
  patched:
  - Canonicalized `Overrides` in candidate derivation prose.
  - Clarified opaque instance registrations with unresolvable static type emit
    `UnsupportedRegistrationShape`.
  - Added a non-normative fan-out threshold anchor.
  - Clarified combined DI registration extractor metadata join behavior.
- PR-loop Codex review found one remaining task-ordering issue: the original
  implementation plan updated the rule catalog after phases that could emit or
  change rule IDs. Patched by moving the catalog update and validation gate
  into Phase 1 before scanner, graph, export, or consumer work emits changed
  evidence, and by narrowing Phase 7 to a catalog drift check.
- Final re-review preference: Sonnet, unless Opus is clearly needed.

## Validation State

Implementation slice validation so far:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedDependencyPathTests
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Results:

- Focused path tests passed: 24 tests.
- `dotnet build` passed.
- Full .NET suite passed: 588 tests.
- Private path guard passed.
- Whitespace diff check passed.

Existing NU1903 warning for `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 is unchanged.

## Safety Notes

The spec avoids private local paths, private repository names, raw source
snippets, raw config values, raw SQL values, secrets, URLs, hostnames, and raw
remotes in committed artifacts. Future PR descriptions should use `Refs #35`,
not `Closes #35`.

## Follow-Up Items

- Add DI registration-supported candidate annotations.
- Add reverse traversal over candidate edges with `NeedsReviewReversePath`.
- Add route-flow reuse of a shared candidate builder rather than the current
  route-flow-specific bridge logic.
- Add impact/include-paths, report/portfolio, and export/vault candidate
  rendering.
- Add full missing-candidate, reduced-coverage, syntax-only, and scanner-level
  `DynamicDispatchCandidate` non-conflation tests.
- Add broader scanner-level relationship extraction hardening for syntax-only
  and reduced-coverage cases.

## Implementation Review Notes

- Sonnet implementation review ran twice with full wrapper coverage:
  - `.tmp/kiro-reviews/interface-override-di-approximation/2026-06-21T230600-991Z-implementation-claude-sonnet-4.5.clean.md`
  - `.tmp/kiro-reviews/interface-override-di-approximation/2026-06-21T230830-712Z-implementation-claude-sonnet-4.5.clean.md`
- The wrapper prompt includes requirements/design/tasks/review-prompts but not
  implementation diff or this implementation-state file, so both reviews judged
  the entire multi-phase spec rather than the selected `tracemap paths`
  implementation slice.
- Actionable review item applied: added explicit rule-catalog coverage for
  `combined.dispatch-candidate.v1` and `combined.dispatch-gap.v1`.
- Remaining review findings are full-spec follow-up work: DI extraction,
  reverse/route-flow/impact/export integration, broader scanner hardening,
  and full-spec validation. Those remain unchecked in `tasks.md`.

## PR Review Loop Notes

- First `agent-control pr-loop` run for PR #271 returned
  `actionable_findings` with unresolved Codex/Gemini review threads.
- Patched actionable findings:
  - Materialized dispatch relationship groups before adding candidate edges so
    candidate derivation does not enumerate a mutating edge collection.
  - Replaced `string.Contains(char, StringComparison)` with
    `IndexOf(char, StringComparison)` for the target framework used here.
  - Suppressed immediate candidate-to-relationship backtracking so derived
    interface/override candidate edges do not immediately traverse back over
    the backing relationship and create misleading cycle truncation.
  - Added source scanner version and end-line metadata to
    `DispatchCandidateFanOut` gaps, with focused test coverage.
- Re-ran focused and full validation after the review-loop patch.
