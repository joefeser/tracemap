# Swift Adapter v0 Symbol Identity And Relationships Implementation State

Status: implemented-v0

Issue: [#381](https://github.com/joefeser/tracemap/issues/381)

Parent: [#377](https://github.com/joefeser/tracemap/issues/377)

Implementation branch: `codex/implement-swift-symbol-identity-relationships`

Implementation PR: [#412](https://github.com/joefeser/tracemap/pull/412)

Merged to `dev`: 2026-06-28, merge commit
`45e4dd7eb001d5b3ee55f6807e683d29ea616c20`.

## Current Scope

This implementation lands the SwiftSyntax-only v0 direct relationship subset.
It emits source-local `SymbolRelationship` facts and `symbol_relationships`
rows for unambiguous class inheritance, protocol conformance, protocol
inheritance, and resolvable explicit overrides. Unsupported, unresolved, or
ambiguous relationship targets emit identity/relationship gaps instead of
definitive edges.

## Public Claim Level

Current PR: SwiftSyntax-only implementation subset.

After validation, TraceMap may claim deterministic static Swift direct
relationship evidence for supported source-local shapes. It must still not
claim runtime proof, protocol witness resolution, Objective-C bridging, macro
expansion, Xcode build success, or AI impact analysis.

## Source Material Paths

- `docs/LANGUAGE_ADAPTER_CONTRACT.md`
- `docs/VALIDATION.md`
- `docs/ACCEPTANCE.md`
- `.kiro/specs/jvm-indexer/requirements.md`
- `.kiro/specs/jvm-indexer/design.md`
- `.kiro/specs/python-indexer/requirements.md`
- `.kiro/specs/python-indexer/design.md`
- Companion Swift v0 specs in this implementation series, when present:
  - `.kiro/specs/swift-adapter-v0-scaffold-cli-output-contract/`
  - `.kiro/specs/swift-adapter-v0-inventory-project-discovery/`
  - `.kiro/specs/swift-adapter-v0-swiftsyntax-declarations-calls/`
- GitHub issue #377 Swift adapter v0 runway
- GitHub issue #381 Swift adapter v0 symbol identity and relationships

## Scope Decisions

- Implemented SwiftSyntax-backed static declaration and relationship evidence.
- Reserve `Tier1Semantic` for future deterministic compiler/SourceKit evidence;
  SwiftSyntax-only symbol and relationship facts are not semantic proof.
- Used shared role-symbol properties and shared SQLite tables so combine/report
  and future path/reverse/route workflows can consume Swift rows.
- Used existing canonical relationship kinds for traversable rows:
  `InheritsFrom`, `ImplementsInterface`, `ExtendsInterface`, and `Overrides`.
  Swift-specific protocol/conformance language may appear in display metadata,
  not as a competing persisted relationship kind.
- Treated ambiguous identity, unresolved imports, conditional compilation, macros,
  generated code, typealias uncertainty, Objective-C bridging, generic
  specialization, protocol witness selection, and runtime dispatch as explicit
  gaps or lower-tier candidate evidence.

## Implemented In This Slice

- Added SwiftSyntax inheritance/protocol/extension conformance candidates.
- Resolved relationship endpoints only against exactly one source-local symbol.
- Emitted `SymbolRelationship` facts with shared role properties:
  `sourceSymbolId`, `targetSymbolId`, symbol language/kind/display names, and
  canonical `relationshipKind`.
- Populated `symbol_relationships` and source/target `fact_symbols` rows.
- Added Swift scan report relationship counts by kind.
- Added catalog entries for `swift.syntax.symbol-relationship.v1`,
  `swift.syntax.override-candidate.v1`, and
  `swift.syntax.identity-gap.v1`.
- Added smoke validation for relationship facts, SQLite rows, unresolved-target
  gaps, and no `ExtensionOf`/`ImplementsInterfaceMember` promotion.
- Added public-safe Swift sample relationship declarations under
  `samples/swift-package-basic`.

## Deferred / Out Of Scope

- Compiler/SourceKit semantic relationship proof.
- Protocol witness selection, default implementation matching, conditional
  conformance proof, Objective-C selector/bridge resolution, macro expansion,
  generated-code freshness, property-wrapper synthesis, availability reasoning,
  and runtime dispatch.
- `ImplementsInterfaceMember` and protocol requirement implementation
  relationships.
- Optional protocol requirement candidate facts remain deferred; the v0 smoke
  fixture asserts that signature-shaped protocol satisfaction does not emit
  `ImplementsInterfaceMember` or synthetic override evidence.
- Traversable `ExtensionOf` edges.
- Source-local extension-member reparenting to the extended type remains
  deferred. Extension declarations and members keep extension-local/container
  evidence in v0 unless a direct protocol-adoption relationship is proven.
- Complex nested/generic/typealias relationship resolution.
- Cross-target/module relationship resolution is limited. Source-local loose
  extensions may resolve a unique target across modules, but qualified names,
  typealiases, generic specialization, and ambiguous cross-module names remain
  gaps.
- Running app code, Xcode builds, simulators, devices, or SourceKit.
- Making public site/product claims.

## Validation Commands

```bash
swift build --package-path src/swift
swift run --package-path src/swift tracemap-swift-smoke-tests
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-package-basic --out /tmp/tracemap-swift-package-basic
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index /tmp/tracemap-swift-package-basic/index.sqlite --label swift --out /tmp/tracemap-swift-combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index /tmp/tracemap-swift-combined.sqlite --out /tmp/tracemap-swift-report
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
git diff --check
./scripts/check-private-paths.sh
```

Latest local validation:

- `swift build --package-path src/swift` passed.
- `swift run --package-path src/swift tracemap-swift-smoke-tests` passed.
- Checked-in Swift sample scans passed for `samples/swift-package-basic`,
  `samples/swift-metadata-reduced`, `samples/swift-metadata-unsupported`, and
  `samples/no-swift`.
- Swift package basic scan emitted 33 facts, `Level3SyntaxAnalysis`,
  `NotRun`, and 4 exported/imported relationships.
- `tracemap export`, `tracemap combine`, `tracemap report`, and
  `tracemap paths` passed against the generated Swift sample index.
- `dotnet build src/dotnet/TraceMap.sln` passed.
- `dotnet test src/dotnet/TraceMap.sln` passed: 696 tests.
- `./scripts/check-private-paths.sh` passed.
- `git diff --check` passed.
- `swift test --package-path src/swift` reports no test targets; Swift
  assertions are currently run by `tracemap-swift-smoke-tests`.
- Kiro Sonnet implementation review initially reported blockers around analysis
  level, symbol ID stability, gap vocabulary, module identity, relationship DDL,
  and deferred protocol/extension scope. Those items were patched or explicitly
  documented. The final local validation above was rerun after the patches.
- PR-loop review on PR #412 reported three current findings:
  duplicate inheritance lookup could trap, relationship candidates could retain
  pre-dedup source IDs, and overload duplicate IDs still used body-sensitive
  syntax hashes. Those were patched by body-independent duplicate
  discriminators, post-dedup relationship candidate rewrites, non-trapping
  relationship lookups, and focused smoke assertions. Validation above was
  rerun after these PR-loop fixes.

## Safe / No-Overclaim Boundaries

Safe language:

- deterministic static Swift symbol evidence;
- syntax-backed direct relationship evidence;
- package/module structural evidence;
- reduced coverage;
- candidate protocol implementation evidence;
- explicit identity or relationship gap.

Unsafe language:

- runtime call target;
- protocol witness proven;
- Objective-C selector resolved;
- macro-generated member indexed;
- Xcode build succeeded;
- app behavior impacted;
- production usage detected;
- AI impact analysis.

## Follow-Up Items

- Consider a future SourceKit/compiler enrichment slice if Tier1 Swift symbol
  relationship proof becomes worth the toolchain cost.
- Add richer nested/generic/typealias fixtures if future route/path work needs
  those relationships.
- Keep downstream reports conservative: these rows are static syntax evidence,
  not runtime dispatch proof.
