# Interface, Override, and DI Approximation Implementation State

## Branch

- Branch: `codex/spec-interface-override-di-approximation`
- Base: `origin/dev`
- Issue: `Refs #35`
- Scope: spec-only

## Current Status

This branch creates a Kiro spec for deterministic interface, override,
inheritance, and DI registration approximation. It intentionally does not
implement scanner, reducer, reporting, export, rule catalog, docs, or site
product-code changes.

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
- Keep implementation tasks unchecked. Only spec-authoring and review-process
  tasks should be checked off during this branch.

## Files

- `.kiro/specs/interface-override-di-approximation/requirements.md`
- `.kiro/specs/interface-override-di-approximation/design.md`
- `.kiro/specs/interface-override-di-approximation/tasks.md`
- `.kiro/specs/interface-override-di-approximation/implementation-state.md`
- `.kiro/specs/interface-override-di-approximation/review-prompts.md`

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
- Final re-review preference: Sonnet, unless Opus is clearly needed.

## Validation State

Passed:

```bash
git diff --check
./scripts/check-private-paths.sh
```

Product-code validation such as `dotnet test` and CLI scan smoke tests is
deferred for the future implementation branch because this branch is spec-only.

## Safety Notes

The spec avoids private local paths, private repository names, raw source
snippets, raw config values, raw SQL values, secrets, URLs, hostnames, and raw
remotes in committed artifacts. Future PR descriptions should use `Refs #35`,
not `Closes #35`.

## Follow-Up Items

- Patch Medium+ Kiro review findings.
- Keep implementation tasks unchecked after review patches.
- Validate whitespace and private-path guard before commit.
- Open a ready PR to `dev`, request Codex review through the configured PR-loop
  workflow, and do not merge.
