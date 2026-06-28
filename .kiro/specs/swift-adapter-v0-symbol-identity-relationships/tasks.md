# Swift Adapter v0 Symbol Identity And Relationships Tasks

Issue: [#381](https://github.com/joefeser/tracemap/issues/381)

Parent: [#377](https://github.com/joefeser/tracemap/issues/377)

Implementation PR: [#412](https://github.com/joefeser/tracemap/pull/412)

Status: Swift v0 implemented. The original checklist included future compiler
semantic enrichment, protocol witness matching, and cross-language traversal
depth. Those are recorded below as deferred follow-ups rather than active
unchecked v0 tasks.

## Implemented V0 Scope

- [x] Confirm implementation branch, issue linkage, Swift adapter package path,
  and SwiftSyntax-only v0 scope.
- [x] Re-read the adapter contract, validation docs, acceptance docs, issue
  #377, issue #381, and companion Swift v0 specs before implementation.
- [x] Add rule-catalog entries and limitations for every emitted Swift symbol,
  relationship, override-candidate, and identity-gap rule ID.
- [x] Preserve reducer-friendly display names separately from stable symbol IDs.
- [x] Add deterministic duplicate and ambiguous identity handling with stable
  collision discriminators.
- [x] Emit `AnalysisGap` facts for unresolved imports, ambiguous identity,
  duplicate identity, unsupported typealias resolution, conditional
  compilation, macros, generated code, and unknown module identity.
- [x] Add a deterministic declaration prepass over sorted Swift files.
- [x] Build source-local symbol maps before relationship emission.
- [x] Emit `SymbolRelationship` facts and `symbol_relationships` rows for
  direct class inheritance with canonical `relationshipKind = "InheritsFrom"`.
- [x] Emit direct type-to-protocol conformance relationships with canonical
  `relationshipKind = "ImplementsInterface"` when evidence is unambiguous.
- [x] Emit protocol inheritance relationships with canonical
  `relationshipKind = "ExtendsInterface"`.
- [x] Emit extension membership evidence through containing symbols,
  occurrences, supporting metadata, or gaps without v0 traversable
  `ExtensionOf` overclaims.
- [x] Emit definitive `Overrides` only when a target superclass member can be
  identified without ambiguity.
- [x] Emit override-target gaps when `override` syntax is visible but the target
  member cannot be resolved.
- [x] Populate shared SQLite tables: `symbols`, `symbol_occurrences`,
  `fact_symbols`, and `symbol_relationships`.
- [x] Preserve repo-relative file paths, one-based line spans, commit SHA, rule
  ID, evidence tier, extractor ID, extractor version, and supporting fact IDs.
- [x] Add report sections for Swift symbol counts, relationship counts, gaps,
  coverage labels, and limitations.
- [x] Validate export/combine/report/paths over generated Swift fixture indexes
  without cross-language or runtime-dispatch overclaims.
- [x] Add tests proving SwiftSyntax-only relationship rules do not emit
  `Tier1Semantic`.
- [x] Run Swift and shared validation:

```bash
swift build --package-path src/swift
swift run --package-path src/swift tracemap-swift-smoke-tests
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-package-basic --out /tmp/tracemap-swift-package-basic
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index /tmp/tracemap-swift-package-basic/index.sqlite --label swift --out /tmp/tracemap-swift-combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index /tmp/tracemap-swift-combined.sqlite --out /tmp/tracemap-swift-report
dotnet run --project src/dotnet/TraceMap.Cli -- paths --index /tmp/tracemap-swift-combined.sqlite --out /tmp/tracemap-swift-paths
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

## Deferred Beyond Swift V0

- Compiler/SourceKit semantic relationship proof.
- Protocol witness selection, protocol requirement implementation matching,
  default implementation matching, conditional conformance proof, and
  `ImplementsInterfaceMember` emission.
- Objective-C selector/bridge resolution, optional protocol requirements,
  availability reasoning, macro expansion, property-wrapper synthesis, and
  generated-code freshness.
- Traversable `ExtensionOf` edges and source-local extension-member reparenting
  to the extended type.
- Complex nested/generic/typealias relationship resolution and richer
  cross-target/module relationship resolution.
- Runtime dispatch, simulator/device inspection, Xcode builds, and public site
  claims beyond static evidence-backed Swift discovery.
