# SwiftSyntax Declarations and Basic Call Facts Implementation State

Status: implemented-partial

## Branch

- Intended branch: `codex/spec-swift-swiftsyntax-declarations-calls`
- Base: `origin/dev`
- Issue: [#380](https://github.com/joefeser/tracemap/issues/380)
- Parent issue: [#377](https://github.com/joefeser/tracemap/issues/377)
- Prerequisite issue: [#378](https://github.com/joefeser/tracemap/issues/378)
- Module-context issue: [#379](https://github.com/joefeser/tracemap/issues/379)
- Symbol/relationship owner issue:
  [#381](https://github.com/joefeser/tracemap/issues/381)
- Scope: implementation PR

## Current State

This implementation adds SwiftSyntax-backed declaration, import, basic call, and construction candidate extraction. It emits rule-backed facts, safe syntax hashes, reduced-coverage gaps, and shared SQLite rows for symbols, occurrences, fact-symbol links, call edges, and construction candidates. Canonical symbol relationship semantics remain owned by issue #381.

Implementation branch: `codex/implement-swift-swiftsyntax-declarations-calls`

Issue #378 and #379 are merged into `dev`. This slice consumes the current inventory output for deterministic module context where possible. It uses interim `swift-syntax:v0:<sha256-lower-64>` syntax IDs compatible with the #381 migration boundary; canonical relationship rows remain deferred to issue #381.

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

## Implementation Decisions

- Added SwiftPM dependency `swift-syntax` pinned to `603.0.2`, matching the
  local Swift 6.3 toolchain family used for validation.
- Added `SwiftSyntaxEvidenceExtractor` under `src/swift/Sources/TraceMapSwift/`
  for imports, declarations, basic call candidates, and construction
  candidates.
- The scan ID now includes selected file content hashes in addition to
  repo/commit/options/inventory metadata so syntax-derived fact output tracks
  checked-in source content.
- Emitted Swift-specific fact types rather than shared semantic fact types:
  `SwiftDeclarationDeclared`, `SwiftImportDeclared`, `SwiftCallCandidate`, and
  `SwiftConstructionCandidate`.
- Reused shared SQLite tables only for navigation/storage compatibility:
  `symbols`, `symbol_occurrences`, `fact_symbols`, `call_edges`, and
  `object_creations`. These rows retain Swift syntax rule IDs and syntax-tier
  evidence; they do not imply semantic dispatch.
- `AnalysisGap` remains the shared gap fact type. Swift syntax parser,
  conditional compilation, module-context, and unsupported-shape gaps use
  `swift.syntax.analysis-gap.v1`.
- Canonical symbol relationship extraction, inheritance/protocol conformance
  rows, overrides, and fully qualified Swift symbol semantics remain deferred
  to issue #381.
- No `MethodInvoked`, `PropertyAccessed`, or shared `ObjectCreated` facts are
  emitted by this slice.
- `fact_id` is the supporting fact identifier for Swift-derived
  `call_edges` and `object_creations` rows in the current shared TraceMap
  schema. Smoke tests assert those rows join back to their
  `SwiftCallCandidate` or `SwiftConstructionCandidate` facts.
- SwiftSyntax facts remain `Tier3SyntaxOrTextual` in this slice. Tier2
  promotion from deterministic package/target identity is deferred until a
  later #379/#381 integration proves the backing evidence.
- Syntax hashes normalize comments, string literals, numeric literals, and
  whitespace before hashing, then persist full 64-character lowercase SHA-256
  digests.
- Conditional compilation blocks emit `ConditionalCompilationAmbiguous` gaps;
  `#if canImport(...)` also emits `CanImportConditionalAmbiguous`, and nested
  evidence includes `conditionalCompilation=true`.
- Recoverable Swift parser diagnostics emit `SwiftParseDiagnostics` gaps with
  hashed diagnostic messages. Raw SwiftSyntax diagnostic text is not stored.
- Source-root facts derived only from repo-relative path shape are
  `Tier3SyntaxOrTextual`; Tier2 promotion remains deferred until deterministic
  package/target backing evidence is proven by a later slice.
- SwiftPM XCTest/Testing modules are unavailable in the local Swift 6.3.3
  toolchain, so this branch keeps the focused safety tests in the
  `tracemap-swift-smoke-tests` executable rather than adding a non-runnable
  SwiftPM test target.
- Swift fact IDs are derived from stable structural fields
  (`scanId`, fact type, rule ID, evidence tier, file path, span, target symbol,
  and contract element) rather than the mutable properties bag.
- Source inventory coverage labels use an explicit degrading-gap allowlist
  rather than treating every non-semantic-deferred gap as reduced inventory
  coverage.
- PR-loop fixes added:
  - analyzer logs now include hashed repo identity context without raw local
    paths;
  - Swift fact IDs include source symbol and an explicit stable syntax-position
    discriminator for call/construction facts;
  - function declaration signatures use Swift call-site parameter labels;
  - local variables inside executable scopes are not emitted as property
    declarations;
  - static member calls are not treated as construction candidates unless the
    syntax is an explicit initializer call.

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

Validation completed on implementation branch:

- `swift build --package-path src/swift`: passed.
- `swift run --package-path src/swift tracemap-swift-smoke-tests`: passed,
  including catalog-gate, parser diagnostic hashing, syntax-only tier,
  documented interim symbol ID, conditional import, module-context gap,
  optional-chain gap, and exported-import wording coverage.
- `samples/swift-package-basic`: scan passed with 19 facts, including
  SwiftSyntax declaration/import/call/construction facts, 3 symbols, 1 call
  edge, and 1 object creation row.
- `samples/swift-metadata-reduced`: scan passed with 26 facts.
- `samples/swift-metadata-unsupported`: scan passed with 14 facts.
- `samples/no-swift`: scan passed with 3 facts,
  `Level3SyntaxAnalysis`, and `FailedOrPartial`.
- Downstream export/combine/report passed over
  `/tmp/tracemap-swift-package-basic/index.sqlite`.
- Generated-output redaction sentinel passed.
- `dotnet build src/dotnet/TraceMap.sln`: passed.
- `dotnet test src/dotnet/TraceMap.sln`: passed with 693 tests.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- Sonnet implementation review:
  `.tmp/kiro-reviews/swift-adapter-v0-swiftsyntax-declarations-calls/2026-06-27T191527-829Z-implementation-claude-sonnet-4.6.clean.md`
  completed with reduced coverage because Kiro reported denied tool access.
  Blocking findings patched:
  - normalized syntax hash inputs instead of hashing raw `trimmedDescription`;
  - removed path-derived Tier2 promotion for SwiftSyntax facts;
  - documented/tested existing `fact_id` supporting-fact convention for
    derived Swift call/object rows;
  - added conditional compilation and `canImport` gaps with conditional fact
    flags.
- Sonnet implementation re-review:
  `.tmp/kiro-reviews/swift-adapter-v0-swiftsyntax-declarations-calls/2026-06-27T192322-529Z-implementation-claude-sonnet-4.6.clean.md`
  completed with reduced coverage because Kiro reported denied tool access.
  Blocking findings patched:
  - added focused smoke-test coverage for catalog gates, parser diagnostic
    hashing, interim symbol ID format, syntax-only tiers, conditional imports,
    module-context gaps, optional chaining gaps, and exported-import wording;
  - aligned syntax gap rule ID to `swift.syntax.analysis-gap.v1`;
  - changed interim symbol IDs to `swift-syntax:v0:<sha256-lower-64>`;
  - emitted `SwiftParseDiagnostics` gaps with hashed diagnostic messages;
  - downgraded path-derived `SwiftSourceRootDeclared` evidence to
    `Tier3SyntaxOrTextual`.
- Final Sonnet implementation re-review:
  `.tmp/kiro-reviews/swift-adapter-v0-swiftsyntax-declarations-calls/2026-06-27T194042-618Z-implementation-claude-sonnet-4.6.clean.md`
  completed with full coverage. Blocking findings patched:
  - replaced inverted coverage-label logic with an explicit degrading-gap list;
  - added orphan-row assertions for `call_edges`, `object_creations`, and
    `symbol_occurrences`;
  - removed the mutable properties bag from Swift fact ID inputs.
- PR #411 review-loop findings patched:
  - Qodo analyzer-log provenance and fact-ID collision findings;
  - Codex duplicate call fact ID, call-site label, local variable/property, and
    construction false-positive findings.

Run additional `docs/VALIDATION.md` smokes when shared schema, reducer,
combine, report, paths, reverse, diff, impact, release-review, or export
behavior changes.

## Implementation Validation Commands

```bash
swift build --package-path src/swift
swift run --package-path src/swift tracemap-swift-smoke-tests
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-package-basic --out /tmp/tracemap-swift-package-basic
dotnet run --project src/dotnet/TraceMap.Cli -- export --index /tmp/tracemap-swift-package-basic/index.sqlite --out /tmp/tracemap-swift-export --format json
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index /tmp/tracemap-swift-package-basic/index.sqlite --label swift --out /tmp/tracemap-swift-combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index /tmp/tracemap-swift-combined.sqlite --out /tmp/tracemap-swift-report
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

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
