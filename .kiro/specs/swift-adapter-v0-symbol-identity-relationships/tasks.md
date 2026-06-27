# Swift Adapter v0 Symbol Identity And Relationships Tasks

Issue: [#381](https://github.com/joefeser/tracemap/issues/381)

All tasks are intentionally unchecked. This spec PR is not an implementation PR.

## Phase 0: Scope And Contracts

- [ ] Confirm implementation branch name and scope for the future Swift symbol relationship PR.
- [ ] Re-read `docs/LANGUAGE_ADAPTER_CONTRACT.md`, `docs/VALIDATION.md`, `docs/ACCEPTANCE.md`, issue #377, and issue #381 before implementation.
- [ ] Confirm the Swift adapter package path and CLI name in the implementation PR, and update validation commands in this spec if the path is not `src/swift`.
- [ ] Confirm whether v0 uses SwiftSyntax only or adds any deterministic SourceKit/compiler enrichment; do not emit `Tier1Semantic` without compiler-backed proof.
- [ ] Confirm rule catalog update location and add rule entries before emitting new Swift facts.
- [ ] Reconcile with companion Swift v0 specs so declaration walking, project discovery, symbol identity, and relationship rows use one shared Swift identity scheme and one relationship rule family.

## Phase 1: Symbol Identity Model

- [ ] Implement deterministic Swift symbol ID construction for module/package, file, type, extension, function/method, initializer, deinitializer, subscript, operator, property, enum case, parameter, and protocol requirement declarations.
- [ ] Implement the design's file/span discriminator decision table for same-label overloads, unknown-module duplicates, extension-local containers, generated/macro/conditional declarations, and normal unique symbols.
- [ ] Add module/package identity extraction from supported local metadata, with unknown-module gaps when identity cannot be proven.
- [ ] Preserve reducer-friendly display names separately from stable symbol IDs.
- [ ] Add duplicate and ambiguous identity detection with deterministic collision discriminators.
- [ ] Emit `AnalysisGap` facts for unresolved imports, ambiguous identity, duplicate identity, unsupported typealias resolution, conditional compilation, macros, generated code, and unknown module identity.
- [ ] Add tests proving symbol IDs are stable across repeated scans and file discovery order changes.

## Phase 2: Cross-File Prepass

- [ ] Add a deterministic declaration prepass over sorted Swift files.
- [ ] Build a module-local symbol map before relationship emission.
- [ ] Preserve source occurrences for declarations and extension members.
- [ ] Add tests for cross-file types, nested types, duplicate names, and extension member identity.

## Phase 3: Relationship Facts

- [ ] Emit `SymbolRelationship` facts and `symbol_relationships` rows for direct class inheritance using shared canonical `relationshipKind = "InheritsFrom"`.
- [ ] Emit direct type-to-protocol conformance relationships using shared canonical `relationshipKind = "ImplementsInterface"` when evidence is credible.
- [ ] Emit protocol inheritance relationships using shared canonical `relationshipKind = "ExtendsInterface"`.
- [ ] Emit extension membership evidence through containing symbols, occurrences, supporting metadata, or gaps; do not emit a v0 traversable `ExtensionOf` edge.
- [ ] Emit lower-tier or gap evidence for unresolved external targets instead of definitive source-local relationships.
- [ ] Add tests for relationship fact properties, evidence tiers, rule IDs, spans, commit SHA, extractor version, and SQLite rows.

## Phase 4: Override And Implementation Approximation

- [ ] Emit definitive `Overrides` only when a target superclass member can be identified without ambiguity.
- [ ] Emit `swift-override-target-unresolved` gaps when `override` is present but the target member cannot be resolved.
- [ ] Add optional protocol requirement candidate evidence only for documented local source-only signature matches, capped at `Tier3SyntaxOrTextual` and non-traversable by dispatch readers.
- [ ] Add gaps for protocol witness/runtime dispatch boundaries, associated types, conditional conformances, default protocol implementations, Objective-C optional requirements, availability, and generic constraints.
- [ ] Add tests that candidate protocol implementations never render as runtime proof.
- [ ] Add a negative test proving syntax-only protocol implementation candidates do not feed `ImplementsInterfaceMember` dispatch-candidate behavior.

## Phase 5: Storage, Reports, And Combine Compatibility

- [ ] Populate shared SQLite tables: `symbols`, `symbol_occurrences`, `fact_symbols`, and `symbol_relationships`.
- [ ] Ensure all Swift facts preserve repo-relative file paths, one-based line spans, commit SHA, rule ID, evidence tier, extractor ID, and extractor version.
- [ ] Ensure `facts.ndjson` uses stable JSON schema and sorted properties.
- [ ] Verify `scan-manifest.json` always includes a concrete commit SHA before successful scan output; missing SHA must fail the scan or emit only explicitly reduced/failed artifacts according to the adapter contract.
- [ ] Add report sections for Swift symbol counts, relationship counts by kind, coverage labels, gaps, and limitations.
- [ ] Validate `tracemap combine`, `tracemap report`, and `tracemap paths` can read a Swift index without cross-language identity overclaims.
- [ ] Add a combine/path-style test that proves canonical Swift relationship kinds are traversed and unknown/candidate relationship kinds are not promoted.
- [ ] Add a cross-language combine test asserting Swift symbol IDs do not collide with C#, TypeScript, JVM, or Python symbol IDs.

## Phase 6: Rule Catalog And Documentation

- [ ] Add rule catalog entries for every emitted Swift rule ID and gap rule.
- [ ] Add rule catalog guardrails or tests that block SwiftSyntax-only rules from emitting `Tier1Semantic`; reserve `Tier1Semantic` for a separately validated SourceKit/compiler enrichment slice with separate rule IDs.
- [ ] Document limitations for Objective-C bridging, generic specialization, conditional compilation, protocol witness/runtime dispatch, macros, generated code, unresolved imports, typealiases, availability, and unknown module identity.
- [ ] Update validation guidance with Swift fixture and reduced-coverage commands.
- [ ] Ensure public copy stays bounded to deterministic static evidence and reduced-coverage labels.

## Phase 7: Validation

- [ ] Run Swift adapter unit tests for symbol identity, relationships, gaps, deterministic IDs, and SQLite rows.
- [ ] Run shared .NET validation when storage/combine/report behavior changes:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
```

- [ ] Run Swift package tests once the implementation package exists:

```bash
swift test --package-path src/swift
```

- [ ] Run combined report/path smoke against a generated Swift fixture scan:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index <swift-scan>/index.sqlite --label swift --out <tmp>/combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index <tmp>/combined.sqlite --out <tmp>/combined-report
dotnet run --project src/dotnet/TraceMap.Cli -- paths --index <tmp>/combined.sqlite --out <tmp>/combined-paths
```

- [ ] Run safety checks:

```bash
./scripts/check-private-paths.sh
git diff --check
```

## Spec PR Review Commands

These commands are for this spec-only PR:

```bash
node scripts/kiro-review.mjs --phase swift-adapter-v0-symbol-identity-relationships --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase swift-adapter-v0-symbol-identity-relationships --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
git diff --check
./scripts/check-private-paths.sh
```
