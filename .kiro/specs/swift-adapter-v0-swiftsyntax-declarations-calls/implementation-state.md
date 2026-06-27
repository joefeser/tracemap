# SwiftSyntax Declarations and Basic Call Facts Implementation State

Status: ready-for-implementation

## Branch

- Intended branch: `codex/spec-swift-swiftsyntax-declarations-calls`
- Base: `origin/dev`
- Issue: [#380](https://github.com/joefeser/tracemap/issues/380)
- Parent issue: [#377](https://github.com/joefeser/tracemap/issues/377)
- Prerequisite issue: [#378](https://github.com/joefeser/tracemap/issues/378)
- Module-context issue: [#379](https://github.com/joefeser/tracemap/issues/379)
- Symbol/relationship owner issue:
  [#381](https://github.com/joefeser/tracemap/issues/381)
- Scope: spec-only

## Current State

This spec is ready for implementation planning. No Swift analyzer/runtime code
has been implemented in this branch, and no tasks in `tasks.md` are marked
complete.

Product-code implementation should start only after the Swift scaffold/output
contract from issue #378 has landed on the implementation base. If the Swift
inventory/project/package slice from issue #379 is not yet available,
declaration and call extraction must remain file-scoped and emit module/package
context gaps. Canonical Swift symbol IDs and relationship rows belong to issue
#381; this slice must remain compatible with that contract.

## Source Material

- GitHub issue #380: SwiftSyntax declarations and basic call facts.
- GitHub issue #377: Swift adapter v0 runway.
- GitHub issue #378: Swift scaffold CLI and output contract.
- GitHub issue #379: Swift inventory and project/package discovery.
- GitHub issue #381: Swift symbol identity and relationships.
- `docs/ADAPTER_RUNWAY.md`
- `docs/LANGUAGE_ADAPTER_CONTRACT.md`
- `docs/VALIDATION.md`
- `docs/NEXT_EXECUTION_REPORT.md`
- `.kiro/specs/jvm-indexer/`
- `.kiro/specs/python-depth-pass/`
- `.kiro/specs/interface-override-di-approximation/`

## Scope Decisions

- Use SwiftSyntax parsing for v0 declaration and basic call extraction.
- Treat issue #381 as the owner of canonical Swift symbol identity and
  relationship semantics. This slice supplies declaration/call evidence and
  must stay compatible with that contract.
- Treat SwiftSyntax declaration/call evidence as `Tier3SyntaxOrTextual` unless
  a future implementation has deterministic structural evidence from project
  inventory.
- Emit `Tier4Unknown` gaps for unavailable SwiftSyntax/tooling, parser
  diagnostics, macros, generated code, Objective-C bridging, conditional
  compilation ambiguity, missing module context, and runtime-only behavior.
- Preserve file spans, #381-compatible syntax-backed symbol IDs/signatures,
  supporting fact IDs, rule IDs, evidence tiers, commit SHA, extractor ID, and
  extractor version.
- Reuse shared TraceMap fact and SQLite contracts where semantics match.
- Add rule catalog entries before any product code emits Swift-specific rule
  IDs.
- Do not emit `MethodInvoked` or `PropertyAccessed` from Tier3 syntax-only
  Swift call/member evidence unless reducer-safety tests prove no semantic
  upgrade.
- Store hashes, kinds, lengths, safe labels, and repo-relative paths by
  default; do not store raw snippets or raw expressions.

## Safe and No-Overclaim Boundaries

Allowed public claim:

- Swift v0 can emit deterministic syntax-backed declarations and basic call or
  construction candidates with reduced-coverage gaps.

Forbidden public claims:

- Runtime call target, selected overload, protocol witness, override target,
  Objective-C selector target, SwiftUI navigation, UIKit storyboard wiring,
  dependency-injection state, app execution, simulator/device behavior, branch
  feasibility, production usage, or impact conclusions.
- Full Swift semantic analysis.
- AI/LLM/vector/embedding impact analysis.

## Files In This Spec

- `.kiro/specs/swift-adapter-v0-swiftsyntax-declarations-calls/requirements.md`
- `.kiro/specs/swift-adapter-v0-swiftsyntax-declarations-calls/design.md`
- `.kiro/specs/swift-adapter-v0-swiftsyntax-declarations-calls/tasks.md`
- `.kiro/specs/swift-adapter-v0-swiftsyntax-declarations-calls/implementation-state.md`
- `.kiro/specs/swift-adapter-v0-swiftsyntax-declarations-calls/review-prompts.md`

## Exact Spec Validation Commands

```bash
node scripts/kiro-review.mjs --phase swift-adapter-v0-swiftsyntax-declarations-calls --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase swift-adapter-v0-swiftsyntax-declarations-calls --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
git diff --check
./scripts/check-private-paths.sh
```

If Medium or higher review findings are patched, run one re-review with Sonnet
or Opus before opening or updating the PR.

## Future Implementation Validation Commands

Exact Swift commands depend on the scaffold/output-contract slice. Once that
slice chooses command names, replace placeholders with real commands:

```bash
<swift-adapter-build-command>
<swift-adapter-test-command>
<swift-adapter-scan-command> --repo <swift-fixture> --out <tmp>/swift-fixture
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Run additional `docs/VALIDATION.md` smokes when shared schema, reducer,
combine, report, paths, reverse, diff, impact, release-review, or export
behavior changes.

## Review State

- Opus spec review: completed with reduced coverage because Kiro reported
  denied tool access:
  `.tmp/kiro-reviews/swift-adapter-v0-swiftsyntax-declarations-calls/2026-06-27T162737-920Z-spec-claude-opus-4.8.clean.md`
- Opus Medium+ findings patched:
  - Declared issue #381 the owner of canonical Swift symbol identity and
    relationship semantics.
  - Tightened shared fact-type reuse so Tier3 Swift call/member evidence does
    not default to `MethodInvoked` or `PropertyAccessed`.
  - Aligned rule-catalog wording to the existing `rules/rule-catalog.yml`
    shape.
  - Added missing negative and tier/supported-row tests.
- Sonnet spec review: completed with full coverage:
  `.tmp/kiro-reviews/swift-adapter-v0-swiftsyntax-declarations-calls/2026-06-27T163432-412Z-spec-claude-sonnet-4.6.clean.md`
- Sonnet Medium+ findings patched:
  - Confirmed the live branch must be
    `codex/spec-swift-swiftsyntax-declarations-calls`.
  - Added a concrete interim `swift-syntax:v0:<sha256-lower-64>` ID format for
    use only if #381 has not landed.
  - Clarified rule-catalog validation hook ownership.
  - Bounded `Tier2Structural` promotion to deterministic #379 module evidence.
  - Defaulted construction evidence to a Swift-specific candidate instead of
    shared `ObjectCreated`.
  - Added diagnostic-message hashing, scan ID inputs, chained-call handling,
    `#if canImport(...)`, `ModuleContextAmbiguous`, and `@_exported import`
    coverage.
- Re-review: completed with reduced coverage because Kiro reported denied tool
  access:
  `.tmp/kiro-reviews/swift-adapter-v0-swiftsyntax-declarations-calls/2026-06-27T163838-429Z-spec-claude-sonnet-4.6.clean.md`
- Re-review Medium+ findings patched:
  - Added hard Phase 0 gates for exact `scanId` formula documentation and #381
    interim-ID skip behavior.
  - Required a written shared fact-type reuse decision before Phase 3.
  - Added requirements/tasks for Tier2 promotion and downgrade checks,
    `SwiftConstructionCandidate` cataloging, parser diagnostic SHA-256
    hashing, `@_exported import` design metadata, chained-call gaps, and
    spec-only path scope.
- PR review loop: first run returned `not_merge_ready` with actionable
  bot-review findings and merge state `UNKNOWN`. Patched current actionable
  spec comments by:
  - removing `lineSpan` from the default interim symbol-ID hash input while
    keeping line span as evidence metadata;
  - adding `actor` declaration coverage to requirements, design, and tasks;
  - clarifying Tier2Structural promotion covers both declaration and call facts;
  - standardizing Swift persisted/spec hashes on full 64-character lowercase
    SHA-256.

## Follow-Up Items

- Wait for the Swift scaffold/output contract from issue #378 to land before
  product-code implementation of this slice.
- Implement or consume Swift inventory/project/package discovery for module and
  target context.
- Coordinate with issue #381 for canonical Swift symbol IDs and relationship
  rows.
- Implement SwiftSyntax declaration/call extraction in a product-code branch.
- Add Swift rule catalog entries before emitted facts.
- Add Swift fixture, determinism, gap, SQLite, report, and safety tests.
- Update validation docs after exact Swift commands exist.
