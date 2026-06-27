# Swift Adapter v0 Symbol Identity And Relationships Design

Issue: [#381](https://github.com/joefeser/tracemap/issues/381)

## Overview

This slice defines how a future Swift adapter should assign stable symbol IDs
and emit direct relationship evidence. It is intentionally limited to static
repository evidence. It does not run app code, inspect simulators/devices,
execute Xcode builds, call LLMs, use embeddings, or prove runtime dispatch.

The implementation should start with SwiftSyntax-backed declaration extraction
plus package/project metadata. Future SourceKit or compiler-backed enrichment
may upgrade selected evidence, but v0 must remain useful when those tools are
absent or incomplete.

Outputs stay adapter-contract compatible:

- `scan-manifest.json`
- `facts.ndjson`
- `index.sqlite`
- `report.md`
- `logs/analyzer.log`

## Intended Branch

Spec PR branch: `codex/spec-swift-symbol-identity-relationships`.

Future implementation branches may use a new `codex/` branch name, but should
reference this spec and issue #381.

## Relationship To Companion Swift Specs

This issue #381 spec owns the stable identity and relationship contract. It
does not own CLI scaffolding, project discovery, general declaration walking,
call extraction, or integration-surface extraction. Companion Swift v0 specs in
the same implementation series should use this model instead of defining a
second identity scheme or second relationship vocabulary.

Ownership assumptions:

- CLI/output contract: companion scaffold spec.
- Package/project/module discovery: companion inventory/project discovery spec.
- SwiftSyntax declaration/call walking: companion declarations/calls spec.
- Stable symbol identity, duplicate/ambiguous identity behavior, direct
  `SymbolRelationship` vocabulary, and relationship gaps: this spec.

Rule ID reconciliation:

- Declaration facts and source symbol IDs produced by a declaration-walking
  slice should use `swift.syntax.declaration.v1`.
- Direct relationship rows should use `swift.syntax.symbol-relationship.v1`.
- Module/package identity should use `swift.package.module-identity.v1` or a
  documented successor from the project-discovery slice.
- Identity/relationship gaps should use `swift.syntax.identity-gap.v1` or a
  documented successor.
- Do not introduce duplicate emitters named `swift.syntax.symbol-identity.v1`
  or `swift.syntax.relationships.v1` for the same evidence.

## Non-Goals

- No Swift analyzer/runtime implementation in this spec PR.
- No runtime proof of protocol witness tables, dynamic dispatch targets,
  Objective-C bridging/selectors, SwiftUI/UIKit navigation, dependency
  injection, branch feasibility, or production use.
- No Xcode build execution as a prerequisite for basic scan output.
- No macro expansion, generated-code synthesis, or SourceKit/compiler semantic
  claim unless a future deterministic enrichment slice explicitly implements
  and validates it.
- No raw source snippets by default.
- No public claim that Swift support is complete or runtime accurate.

## Evidence Model

Swift v0 relationship facts should use these evidence tiers:

| Tier | Swift v0 use |
| --- | --- |
| `Tier1Semantic` | Reserved for future compiler/SourceKit-resolved symbol evidence. SwiftSyntax-only extraction must not use it. |
| `Tier2Structural` | Package/project structure, manifest target membership, or known framework/project metadata evidence. |
| `Tier3SyntaxOrTextual` | SwiftSyntax declaration, inheritance-clause, extension, and name/signature-shape evidence. |
| `Tier4Unknown` | Analysis gaps, ambiguous identity, unresolved imports, unsupported language features, and unable-to-prove states. |

For a SwiftSyntax-only implementation, symbol declarations and relationships are
usually `Tier3SyntaxOrTextual`; package/module facts may be `Tier2Structural`
when local metadata proves them. The adapter must not upgrade evidence merely
because a relationship looks likely.

## Symbol Identity Shape

Stable IDs should be deterministic strings derived from normalized components,
then hashed or encoded using a documented adapter version. The exact encoding is
an implementation detail, but the logical identity inputs should be stable and
testable.

Suggested identity key:

```text
swift-symbol/v1|
module=<module-or-unknown>|
target=<package-target-or-unknown>|
kind=<symbol-kind>|
parent=<parent-symbol-id-or-hash-or-root>|
name=<normalized-display-name>|
signature=<normalized-signature-or-empty>|
genericArity=<n-or-unknown>|
file=<repo-relative-path-if-needed>|
span=<startLine:startColumn-endLine:endColumn-if-needed>
```

Use source path and span as collision discriminators only when the identity
cannot otherwise be unique. This preserves stable cross-file identity for normal
types and members while preventing duplicate declarations from overwriting each
other.

`parent` must use a previously encoded parent symbol ID or a hash of the parent
logical key, not the raw unescaped parent key string. Nested raw key material
would introduce `|` and `=` delimiters into the outer key and make flat parsing
ambiguous.

Decision table:

| Scenario | Include file/span in stable identity? | Rationale |
| --- | --- | --- |
| Unique source-local type in known module | No | Module, lexical parent, kind, and name are enough for cross-file references. |
| Unique member overload with distinct labels in one containing type | No | Containing symbol plus normalized signature identifies the member. |
| Same-type same-label overload where parameter types are unresolved | Yes | SwiftSyntax-only labels cannot distinguish the overload safely. |
| Extension-local member attached to one known source-local type | Member: only if needed by normal overload rules; occurrence keeps extension span | Logical container is the extended type, but source occurrence stays in the extension file. |
| Extension target unresolved or ambiguous | Yes, for extension-local container | Prevents guessed attachment to an invented type. |
| Unknown module with duplicate type display names | Yes | Unknown module removes the normal namespace discriminator. |
| Generated, macro, or conditional declaration variant | Yes plus gap | The source-visible declaration may not be the runtime/compiler-expanded declaration. |

### Supported Symbol Kinds

| Kind | v0 status |
| --- | --- |
| `package` | Required when metadata proves it. |
| `module` | Required; unknown module is a reduced-coverage identity. |
| `target` | Required when metadata proves it. |
| `file` | Required for occurrences and fallback identity. |
| `class` | Required. |
| `struct` | Required. |
| `enum` | Required. |
| `actor` | Required when SwiftSyntax exposes it. |
| `protocol` | Required. |
| `extension` | Required as an occurrence/container fallback. |
| `function` | Required. |
| `method` | Required. |
| `initializer` | Required. |
| `deinitializer` | Required when SwiftSyntax exposes it. |
| `subscript` | Required when modeled in relationship/member rows. |
| `operator` | Required for declaration identity when parsed. |
| `property` | Required. |
| `enumCase` | Required. |
| `associatedValue` | Deferred unless implementation explicitly models case payload identity. |
| `accessor` | Deferred unless implementation explicitly models getter/setter identity. |
| `parameter` | Deferred unless needed for protocol requirement candidate evidence; must be scoped to the containing callable and label/index. |
| `protocolRequirement` | Required for protocol member declarations when parsed. |
| `external` | Required for unresolved/external target placeholders. |
| `unknown` | Required for gaps and fallback rows. |

The implementation may defer some kinds, but unsupported declarations must be
represented by gaps rather than silently disappearing when they affect identity
or relationship confidence.

### Display Names

Display names are reducer/report metadata, not identity. Suggested display
forms:

- Module: `PackageName.TargetName` or `ModuleName`
- Type: `Module.TypeName`
- Nested type: `Module.Outer.Inner`
- Extension-local container: `extension Module.TypeName @ path:line`
- Function/method: `Module.Type.method(label1:label2:)`
- Initializer: `Module.Type.init(label1:label2:)`
- Property: `Module.Type.property`
- Protocol requirement: `Module.Protocol.requirement(label:)`

Raw source text must not be stored to preserve these names. If a signature needs
unsafe or unsupported text, use a safe normalized shape plus hash, length, kind,
and line span.

## Module And Package Identity

Preferred module evidence order:

1. SwiftPM `Package.swift` and target membership parsed as local metadata.
2. Xcode project or workspace metadata when a deterministic parser is added.
3. Explicit scanner option in a future CLI design.
4. Unknown module identity with reduced coverage.

Module/package identity is important for duplicate names across app targets,
test targets, package products, and extensions. Unknown module identity should
not block all extraction, but it must lower confidence and emit an `AnalysisGap`.

## Declaration Prepass

The adapter should perform a deterministic prepass before relationship emission:

1. Normalize scan root, include/exclude options, and Git commit SHA.
2. Inventory Swift and local package/project metadata files in sorted
   repo-relative path order.
3. Parse package/project metadata where supported.
4. Parse Swift files with SwiftSyntax and record declarations, lexical parents,
   inheritance clauses, extension targets, attributes, generic parameter counts,
   and line spans.
5. Build a module-local symbol map with collision detection.
6. Resolve direct source-local relationship targets where possible.
7. Emit facts, symbol rows, relationship rows, gaps, and report summaries in
   deterministic order.

The prepass is what lets extension members and cross-file conformances attach to
known source-local types without relying on filesystem traversal order.

## Relationship Kinds

Use the shared `SymbolRelationship` fact type and `symbol_relationships` table.
Relationship rows that should work with existing combine/path/reverse/route
readers must use the canonical relationship kinds those readers already
normalize:

| Relationship kind | Meaning | Evidence floor |
| --- | --- | --- |
| `InheritsFrom` | Swift class inherits from a superclass. | `Tier3SyntaxOrTextual` for SwiftSyntax; `Tier1Semantic` only with future compiler evidence. |
| `ImplementsInterface` | Swift type or extension declares protocol adoption. | `Tier3SyntaxOrTextual` unless target identity is structurally/semantically stronger. |
| `ExtendsInterface` | Swift protocol inherits another protocol. | `Tier3SyntaxOrTextual` unless target identity is stronger. |
| `Overrides` | Member overrides a specific superclass member. | `Tier3SyntaxOrTextual` only when the target is unambiguous in local static evidence; `Tier1Semantic` only with future compiler evidence. |
| `ImplementsInterfaceMember` | Existing member-level canonical kind for proven member-to-interface/protocol requirement implementation. | Not emitted by SwiftSyntax-only v0; reserved for a future deterministic semantic rule that proves the requirement target. |

Swift-specific names such as `ConformsToProtocol` may appear as display
metadata, but they should not replace the persisted canonical relationship kind
unless shared readers are updated in the same implementation slice.

Extension membership should be represented through member containing symbols,
source occurrences, supporting properties, and gaps. Do not emit `ExtensionOf`
as a v0 traversable relationship unless a future spec adds reader behavior and
tests.

Protocol requirement implementation matching is not a traversable canonical
relationship in SwiftSyntax-only v0. If local source-only evidence is useful,
emit non-traversable candidate evidence capped at `Tier3SyntaxOrTextual` with a
rule limitation, and prove it does not feed dispatch-candidate output as runtime
proof.

Shared DDL source of truth:

- `docs/LANGUAGE_ADAPTER_CONTRACT.md` documents the adapter storage contract.
- `src/dotnet/TraceMap.Storage/SqliteIndexWriter.cs` defines the source index
  DDL for `symbols`, `symbol_occurrences`, `fact_symbols`, and
  `symbol_relationships`.
- `src/dotnet/TraceMap.Combine/CombinedIndexBuilder.cs` defines the combined
  table import shape for `combined_symbols`, `combined_fact_symbols`, and
  `combined_symbol_relationships`.

Do not invent a definitive relationship when the target cannot be identified.
Emit `AnalysisGap` with safe metadata such as `gapKind`, `identityStatus`,
`relationshipKind`, `candidateCount`, and `limitation`.

## Extension Membership

Extensions need special handling because the lexical container is an extension
declaration while the logical member container may be the extended type.

Recommended behavior:

- If the extension target resolves to one source-local type, attach member
  symbols to the extended type while preserving occurrence spans from the
  extension file.
- If the extension target is a known external type such as a standard-library
  type or dependency type, create a stable external target symbol and attach
  extension members to an extension-local container that references that
  external target. Do not treat known external extensions as source-local type
  proof, and do not collapse them into unresolved ambiguity when the external
  display identity is credible.
- Preserve extension membership through occurrence metadata and supporting
  properties; do not emit `ExtensionOf` as a traversable v0 relationship.
- If the extension target cannot be classified as exactly one source-local type
  or one credible known external type, create an extension-local container
  symbol and emit a gap. Do not attach members to a guessed type. Known external
  targets are handled by the previous bullet; they are not automatically
  "unresolved" merely because their implementation source is outside the scan.
- If the extension adds protocol adoption, emit canonical `ImplementsInterface`
  only when source and target identities are credible; otherwise gap or
  candidate evidence.

## Override And Protocol Implementation Approximation

SwiftSyntax can see the `override` modifier, inheritance clauses, and local
member signatures. That is not the same as compiler witness resolution.

Safe v0 approach:

- Explicit `override` without unambiguous superclass target member becomes a
  gap or lower-tier candidate.
- Exact local source signature matches against protocol requirements may become
  non-traversable candidate evidence, not definitive implementation.
- Default protocol implementations, associated types, conditional conformances,
  generic constraints, optional Objective-C requirements, availability, and
  extension dispatch stop definitive claims.
- Future compiler-backed enrichment may promote selected relationships to
  `Tier1Semantic`, with separate rule IDs and validation.

## SQLite And Fact Properties

Swift facts that participate in shared storage should use the role-property
contract:

```text
sourceSymbolId
sourceSymbolLanguage
sourceSymbolKind
sourceSymbolDisplayName
targetSymbolId
targetSymbolLanguage
targetSymbolKind
targetSymbolDisplayName
```

Additional reducer-friendly display keys may include:

```text
name
typeName
namespace
memberName
methodName
propertyName
fieldName
containingType
targetSymbol
relationshipKind
swiftRelationshipDisplayKind
identityStatus
coverageLabel
limitations
```

`swiftRelationshipDisplayKind` is a fact property, not a SQLite column. It is a
closed display vocabulary for Swift-specific wording such as
`ClassInheritance`, `ProtocolConformance`, `ProtocolInheritance`, and
`OverrideCandidate`; it must not replace the persisted canonical
`relationshipKind`.

`index.sqlite` should include at least:

- `scan_manifest`
- `facts`
- `symbols`
- `symbol_occurrences`
- `fact_symbols`
- `symbol_relationships`

Call edges, object creation, argument flows, aliases, HTTP/UI/storage facts, and
package dependency surfaces belong to other Swift v0 child slices unless an
implementation review intentionally expands scope.

## Proposed Rule IDs

These IDs are proposed for the future implementation. They must be added to the
rule catalog before emitted facts ship.

| Rule ID | Fact types | Tier expectations | Limitation summary |
| --- | --- | --- | --- |
| `swift.syntax.declaration.v1` | `TypeDeclared`, `MethodDeclared`, `PropertyDeclared`, `FieldDeclared`, `ParameterDeclared`, symbol rows | `Tier3SyntaxOrTextual` | SwiftSyntax declaration identity, not compiler semantic identity. |
| `swift.package.module-identity.v1` | package/module facts or properties | `Tier2Structural`, `Tier4Unknown` gaps | Local metadata only; target membership and build settings may be incomplete. |
| `swift.syntax.symbol-relationship.v1` | `SymbolRelationship` | `Tier3SyntaxOrTextual` | Direct inheritance/protocol/extension syntax only, persisted with canonical shared relationship kinds. |
| `swift.syntax.extension-membership.v1` | declaration facts, `AnalysisGap` | `Tier3SyntaxOrTextual`, `Tier4Unknown` gaps | Ambiguous extension targets remain gaps; no v0 `ExtensionOf` traversable edge. |
| `swift.syntax.override-candidate.v1` | `SymbolRelationship`, `AnalysisGap` | `Tier3SyntaxOrTextual`, `Tier4Unknown` gaps | Does not prove runtime dispatch or witness selection. |
| `swift.syntax.identity-gap.v1` | `AnalysisGap` | `Tier4Unknown` | Duplicate, ambiguous, unresolved, conditional, macro, generated, or unsupported identity. |

## Gap Kinds

Recommended `gapKind` values:

- `swift-module-identity-unknown`
- `swift-duplicate-symbol-identity`
- `swift-ambiguous-symbol-identity`
- `swift-unresolved-import`
- `swift-unresolved-external-symbol`
- `swift-ambiguous-extension-target`
- `swift-conditional-compilation`
- `swift-macro-expansion-unsupported`
- `swift-generated-code-unsupported`
- `swift-property-wrapper-synthesis-unsupported`
- `swift-typealias-resolution-unsupported`
- `swift-generic-specialization-unsupported`
- `swift-objective-c-bridging-unsupported`
- `swift-protocol-witness-unsupported`
- `swift-override-target-unresolved`

Gap properties must be safe: file paths are repo-relative, spans are line-based,
candidate names are safe display strings or hashes, and no source snippets are
stored by default.

`candidateCount` is an integer count of distinct source-local candidate symbols
considered before a gap was emitted. Zero is valid and means no local candidates
were visible.

Property wrappers such as `@State`, `@Published`, or similar wrapper attributes
can synthesize backing storage and projected properties that are not visible as
ordinary declarations in SwiftSyntax, including conventional `_property`
backing storage and `$property` projected values. SwiftSyntax-only v0 should
emit visible wrapper attributes as declaration metadata or gap evidence, but it
must not invent those synthesized symbols unless a future compiler-backed rule
proves them.

## Determinism Rules

- Sort files by normalized repo-relative path.
- Sort declarations by module, lexical parent, start span, kind, and name.
- Sort relationship rows by source symbol ID, relationship kind, target symbol
  ID, fact ID.
- Sort fact-symbol join rows by fact ID, role, symbol ID, and occurrence span.
- Sort gap facts by rule ID, repo-relative file path, start line, start column,
  gap kind, and fact ID.
- Include extractor ID and version in fact IDs.
- Do not include timestamps, process IDs, output paths, absolute local paths, or
  filesystem enumeration order in IDs.
- Hash unsafe or long display components with SHA-256 and a documented prefix.

## Manifest Coverage

Swift v0 coverage should follow the shared adapter meanings:

| Condition | `analysisLevel` | `buildStatus` |
| --- | --- | --- |
| Future compiler/SourceKit semantic scope covers every selected Swift source without known gaps and commit SHA is concrete | `Level1SemanticAnalysis` | `Succeeded` |
| Some deterministic semantic enrichment exists but selected scope has unresolved imports, toolchain diagnostics, unsupported features, skipped files, or syntax fallback | `Level1SemanticAnalysisReduced` | `FailedOrPartial` |
| SwiftSyntax-only v0, semantic enrichment disabled, or no semantic scope loads | `Level3SyntaxAnalysis` | `NotRun` |

SwiftSyntax-only v0 should normally produce `Level3SyntaxAnalysis` unless a
future implementation defines a stronger shared reduced-coverage label. Unknown
module identity, ambiguous symbols, unresolved imports, macro/generated-code
boundaries, conditional compilation uncertainty, and relationship gaps should
surface in `knownGaps`.

If `Level1SemanticAnalysisReduced` is not accepted by the shared manifest
contract at implementation time, the implementation must update
`docs/LANGUAGE_ADAPTER_CONTRACT.md` and manifest tests before using it.

## Validation Strategy

Future implementation fixtures should cover:

- SwiftPM module/package identity.
- Classes, structs, enums, actors, protocols, nested types, methods,
  initializers, properties, enum cases, and protocol requirements.
- Cross-file extensions that attach to source-local types.
- Extension protocol conformances.
- Duplicate type/member names across modules, targets, and test fixtures.
- Ambiguous extension target and unresolved import gaps.
- Conditional compilation blocks.
- Macro syntax and generated-code markers.
- Explicit `override` with resolvable and unresolvable targets.
- Protocol implementation candidates that stay approximate.
- Determinism across repeated scans and reordered file discovery.
- SQLite `symbols`, `fact_symbols`, `symbol_occurrences`, and
  `symbol_relationships` rows.
- Combined index import preserving Swift source-local identity.
- Combined path/reverse/report traversal of canonical `InheritsFrom`,
  `ImplementsInterface`, `ExtendsInterface`, and `Overrides` Swift rows.
- A negative fixture proving protocol implementation candidates and unresolved
  external targets do not become runtime dispatch proof.
- Cross-language combine fixture proving Swift symbol IDs do not collide with
  C#, TypeScript, JVM, or Python symbols and are not equated across languages.

Suggested fixture path for the implementation PR:

```text
samples/swift-symbol-relationships-sample/
```

Spec PR validation remains limited to:

```bash
node scripts/kiro-review.mjs --phase swift-adapter-v0-symbol-identity-relationships --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase swift-adapter-v0-symbol-identity-relationships --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
git diff --check
./scripts/check-private-paths.sh
```

## Safe Boundaries

The adapter and public reports may say:

- "static Swift declaration evidence";
- "syntax-backed direct relationship evidence";
- "reduced coverage";
- "candidate protocol implementation evidence";
- "unknown or ambiguous identity gap".

They must not say:

- "runtime call target";
- "protocol witness proven";
- "Objective-C selector resolved";
- "macro-generated member indexed";
- "Xcode build succeeded";
- "app behavior impacted";
- "production usage detected";
- "AI impact analysis".
