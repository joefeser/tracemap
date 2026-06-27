# Swift Adapter v0 Symbol Identity And Relationships Requirements

Issue: [#381 Swift adapter v0: symbol identity and relationships](https://github.com/joefeser/tracemap/issues/381)

Parent: [#377 Swift adapter v0 runway](https://github.com/joefeser/tracemap/issues/377)

## Purpose

Define the Swift adapter v0 slice for deterministic symbol identity and direct
symbol relationships. This spec is planning-only: it does not implement Swift
analyzer/runtime code and does not create any public product claim by itself.

The implementation must follow TraceMap's language adapter contract:

- no conclusion without evidence;
- no evidence without a rule ID;
- no rule without documented limitations;
- no scan without repo and commit SHA;
- partial Swift analysis is useful, but must be labeled partial or reduced;
- no LLM calls, embeddings, vector databases, or prompt-based classification in
  scanner or reducer behavior;
- no target app execution, simulator/device inspection, or runtime dispatch
  proof.

## Public Claim Level

Spec PR claim: TraceMap has an implementation-ready design for Swift static
symbol identity and relationship evidence.

Future implementation claim, only after validation passes: Swift v0 can emit
deterministic static symbol and direct relationship evidence for supported Swift
source shapes, with explicit reduced-coverage gaps for unsupported language,
toolchain, and runtime behavior.

This slice must not claim runtime reachability, protocol witness selection,
dynamic dispatch targets, Objective-C selector resolution, macro expansion
semantics, Xcode build success, simulator/device behavior, generated-code
freshness, dependency-injection state, or production impact.

## Source Material

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

## Ownership Boundary

This issue #381 spec is the authority for Swift stable symbol identity,
duplicate/ambiguous identity behavior, and direct relationship facts. Companion
Swift v0 specs may own CLI scaffolding, project discovery, declaration/call
walking, or integration surfaces, but they should consume this identity model
and relationship vocabulary rather than defining competing symbol IDs or
relationship rule IDs.

Future implementation should reconcile to one Swift declaration rule and one
Swift relationship rule family. This spec assumes declaration facts/source
symbol IDs use `swift.syntax.declaration.v1` where a companion declaration
slice owns declaration walking, and direct relationship rows use
`swift.syntax.symbol-relationship.v1`. The previous names
`swift.syntax.symbol-identity.v1` and `swift.syntax.relationships.v1` should not
be introduced as duplicate emitters for the same evidence.

## Requirements

### Requirement 1: Stable Swift Symbol Identity

**User Story:** As a reviewer, I want stable Swift symbol IDs so cross-file
relationships and future path reports can reference the same declaration without
depending on display text alone.

#### Acceptance Criteria

1. WHEN the Swift adapter inventories Swift source files THEN it SHALL emit
   symbol rows for supported declarations using `language = "swift"` and the
   shared role-property convention from `docs/LANGUAGE_ADAPTER_CONTRACT.md`.
2. WHEN package or module identity is statically visible from SwiftPM package
   metadata, Xcode project metadata, or scan options THEN symbol IDs SHALL
   include the stable module/package component.
3. WHEN module identity is not statically visible THEN symbol IDs SHALL include
   a deterministic unknown-module component and the scan SHALL emit an
   `AnalysisGap` with reduced coverage.
4. WHEN a type declaration is parsed THEN the adapter SHALL create stable IDs
   for classes, structs, enums, actors, protocols, and extensions using module,
   lexical parent path, declaration kind, display name, generic arity where
   visible, file path, and source span as needed to prevent collisions.
5. WHEN function-like declarations are parsed THEN the adapter SHALL create
   stable IDs for functions, methods, initializers, deinitializers, subscripts,
   accessors when modeled, and operators using module, containing symbol,
   declaration kind, base name, argument labels, generic arity where visible,
   and a collision discriminator when needed.
6. WHEN property-like declarations are parsed THEN the adapter SHALL create
   stable IDs for stored properties, computed properties, static properties,
   enum cases, associated values where modeled, and protocol requirements using
   module, containing symbol, declaration kind, name, and source span when
   syntax identity is otherwise ambiguous.
7. WHEN two or more declarations produce the same candidate symbol ID THEN the
   adapter SHALL keep both symbols, add deterministic collision discriminators,
   and emit an `AnalysisGap` describing duplicate or ambiguous identity rather
   than overwriting rows or silently choosing one declaration.
8. WHEN an implementation cannot distinguish same-name overloads, generated
   declarations, macro-generated members, conditional declarations, or platform
   variants THEN it SHALL downgrade to syntax/textual evidence and emit
   explicit gap metadata.

### Requirement 2: Display Names And Reducer-Compatible Properties

**User Story:** As a reducer/report user, I want human-readable Swift symbol
properties while stable IDs stay separate for SQLite and relationship traversal.

#### Acceptance Criteria

1. WHEN emitting symbol-backed facts THEN the adapter SHALL populate stable role
   properties such as `sourceSymbolId`, `targetSymbolId`, `sourceSymbolKind`,
   `targetSymbolKind`, `sourceSymbolLanguage`, `targetSymbolLanguage`,
   `sourceSymbolDisplayName`, and `targetSymbolDisplayName`.
2. WHEN emitting reducer-compatible declaration or usage facts THEN the adapter
   SHALL also populate existing camelCase matching keys where applicable,
   including `name`, `typeName`, `namespace`, `memberName`, `methodName`,
   `propertyName`, `fieldName`, `containingType`, and `targetSymbol`.
3. WHEN Swift module identity is known THEN `namespace` or equivalent display
   properties SHALL carry the Swift module name, not an Xcode absolute path.
   WHEN it is unknown THEN display properties SHALL use a stable unknown-module
   label and emit a reduced-coverage gap instead of using an Xcode absolute
   path.
4. WHEN stable IDs and display names disagree because identity is reduced or
   ambiguous THEN the fact SHALL keep the stable ID evidence separate from the
   display fields and include gap/limitation metadata.
5. WHEN raw source text would be needed to render a display name THEN the
   adapter SHALL store a safe normalized name, hash, length, kind, and line span
   instead of storing raw snippets.

### Requirement 3: Direct Type Relationships

**User Story:** As a maintainer, I want direct Swift type relationships so
future reports can traverse inheritance and protocol conformance using
evidence-backed rows.

#### Acceptance Criteria

1. WHEN a class declaration names a superclass in a syntactically supported
   inheritance clause THEN the adapter SHALL emit `SymbolRelationship` with the
   shared canonical `relationshipKind = "InheritsFrom"` when both source and
   target identities are credible in the current scan scope.
2. WHEN a type declaration names adopted protocols in a syntactically supported
   inheritance clause THEN the adapter SHALL emit `SymbolRelationship` with
   the shared canonical `relationshipKind = "ImplementsInterface"` when source
   and target identities are credible in the current scan scope. Swift-specific
   display metadata MAY label the target as a protocol, but the persisted
   relationship kind must stay traversable by existing combined readers.
3. WHEN a protocol declaration inherits from another protocol THEN the adapter
   SHALL emit `SymbolRelationship` with the shared canonical
   `relationshipKind = "ExtendsInterface"` when the target protocol identity is
   credible.
4. WHEN a type or protocol relationship references an imported or unresolved
   external symbol THEN the adapter MAY emit lower-tier relationship evidence
   with a stable external display identity, but it SHALL mark the target
   identity status as unresolved or external and SHALL NOT claim source-local
   implementation proof.
5. WHEN the relationship target is ambiguous because multiple same-name symbols
   are visible, imports are unresolved, aliases are unsupported, or conditional
   compilation changes visibility THEN the adapter SHALL emit `AnalysisGap`
   rather than a definitive relationship.

### Requirement 4: Extension Membership

**User Story:** As a Swift reviewer, I want extension members attached to their
extended type when static evidence is credible, while ambiguous extensions stay
reviewable.

#### Acceptance Criteria

1. WHEN an extension target resolves to exactly one source-local type identity
   using deterministic module/package and syntax evidence THEN members declared
   inside the extension SHALL use the extended type as their containing symbol
   and SHALL preserve extension-file occurrence spans.
2. WHEN an extension declares adopted protocols and the extended type identity is
   credible THEN the adapter SHALL emit shared canonical
   `ImplementsInterface` relationships with supporting extension evidence.
3. WHEN an extension target is an unresolved imported type, typealias, nested
   type spelling, generic specialization spelling, or otherwise ambiguous target
   THEN the adapter SHALL emit extension member facts with an extension-local
   container identity and an `AnalysisGap` instead of attaching members to an
   invented type.
4. WHEN multiple extensions add same-name members or overloads THEN each member
   SHALL keep a stable declaration ID and occurrence span; duplicate display
   names SHALL NOT collapse into one member.
5. WHEN extension membership is syntax-only THEN relationship evidence SHALL be
   capped at `Tier3SyntaxOrTextual` unless future deterministic toolchain
   evidence upgrades it.
6. WHEN extension membership facts or gaps are emitted THEN implementation SHALL
   use `swift.syntax.extension-membership.v1`, `swift.syntax.declaration.v1`,
   or `swift.syntax.identity-gap.v1` according to the emitted fact shape and the
   rule catalog entry.

### Requirement 5: Override And Implementation Approximation

**User Story:** As a reviewer, I want override and protocol implementation
   candidates only when the scanner can explain the static evidence and its
   limitations.

#### Acceptance Criteria

1. WHEN a method, property, initializer, or subscript is explicitly marked
   `override` and the containing type has a credible direct superclass identity
   THEN the adapter SHALL emit a relationship with the shared canonical
   `relationshipKind = "Overrides"` only if the overridden target symbol can be
   identified without ambiguity. In SwiftSyntax-only v0 this is at most
   `Tier3SyntaxOrTextual` candidate evidence unless a future compiler-backed
   rule proves the target.
2. WHEN `override` is present but the target member cannot be resolved to one
   superclass member THEN the adapter SHALL emit `AnalysisGap` or lower-tier
   candidate evidence, not a definitive `Overrides` relationship.
3. WHEN a concrete type declares members whose signatures appear to satisfy
   protocol requirements using local source-only matching THEN the adapter MAY
   emit non-traversable lower-tier candidate evidence with a limitation note,
   but it SHALL NOT emit the existing member-level canonical
   `ImplementsInterfaceMember` relationship kind unless a future deterministic
   semantic rule proves the member-to-requirement target.
4. WHEN protocol witness selection depends on Swift compiler semantics,
   associated types, generic constraints, conditional conformances, extension
   members, default protocol implementations, Objective-C optional
   requirements, availability, or dynamic dispatch THEN the adapter SHALL NOT
   claim definitive implementation proof.
5. WHEN no target relationship is emitted because evidence is insufficient THEN
   the scanner SHALL prefer an `AnalysisGap` over implying no relationship
   exists.

### Requirement 6: Cross-File Identity And Ambiguity

**User Story:** As a maintainer, I want cross-file Swift declarations to be
joined deterministically without hiding ambiguity.

#### Acceptance Criteria

1. WHEN multiple files in the same module declare related types, extensions, or
   protocol conformances THEN the adapter SHALL build a deterministic module
   symbol map before relationship emission.
2. WHEN file traversal order differs between runs THEN emitted symbol IDs,
   relationship IDs, fact IDs, SQLite rows, and reports SHALL remain stable.
3. WHEN a symbol cannot be joined across files because imports, package/module
   metadata, typealiases, nested-type spellings, conditional compilation, or
   generated code are unresolved THEN the adapter SHALL emit an identity or
   relationship gap with the supporting file path and line span.
4. WHEN duplicate symbol display names exist across modules, targets, test
   bundles, or package products THEN the adapter SHALL preserve module/product
   identity where known and SHALL NOT merge symbols by display name alone.
5. WHEN scan scope excludes files that could affect identity or relationships
   THEN the manifest SHALL mark reduced coverage and the report SHALL explain
   that absence of relationship evidence is not proof of absence.

### Requirement 7: Relationship Facts And SQLite Compatibility

**User Story:** As a TraceMap report author, I want Swift relationships to work
with existing combined reports and future path/reverse/route workflows.

#### Acceptance Criteria

1. WHEN relationship evidence is emitted THEN it SHALL use the shared
   `SymbolRelationship` fact type and populate `symbol_relationships` rows using
   the shared SQLite DDL from `src/dotnet/TraceMap.Storage/SqliteIndexWriter.cs`
   and the contract notes in `docs/LANGUAGE_ADAPTER_CONTRACT.md`.
2. WHEN a relationship is intended to participate in existing combined
   inheritance/conformance/override traversal THEN it SHALL use the current
   canonical relationship kinds recognized by shared readers:
   `InheritsFrom`, `ImplementsInterface`, `ExtendsInterface`, and `Overrides`.
   Swift-specific vocabulary such as protocol conformance may be rendered in
   display metadata, not as a competing persisted relationship kind.
3. WHEN relationship facts are inserted into SQLite THEN each row SHALL preserve
   source symbol ID, target symbol ID, relationship kind, fact ID, rule ID,
   evidence tier, file path, line span, scan ID, commit SHA, extractor ID, and
   extractor version.
4. WHEN Swift facts participate in `tracemap combine` THEN combined outputs
   SHALL preserve source-local Swift symbol IDs and SHALL NOT infer equality
   with C#, TypeScript, JVM, or Python symbols without explicit boundary facts.
5. WHEN future path/reverse/route reports read Swift indexes THEN they SHALL be
   able to use relationship rows as direct source-local evidence and downgrade
   or stop when reduced-coverage gaps are present.
6. WHEN relationship evidence is syntax-only or approximate THEN facts and
   reports SHALL render that limitation so downstream consumers do not treat it
   as compiler-proven runtime dispatch.

### Requirement 8: Rule IDs And Limitations

**User Story:** As an auditor, I want every Swift symbol and relationship fact
to be tied to a documented rule ID and limitation.

#### Acceptance Criteria

1. WHEN implementation adds Swift symbol identity rules THEN it SHALL update
   `rules/rule-catalog.yml` or the active rule catalog source with emitted fact
   types, evidence tiers, required properties, limitations, false positives, and
   false negatives.
2. WHEN a rule emits `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`,
   or `Tier4Unknown` evidence THEN its rule catalog entry SHALL explain the
   exact evidence basis.
3. WHEN a rule covers SwiftSyntax-only extraction THEN it SHALL not claim
   compiler semantic identity.
4. WHEN a rule covers SourceKit/sourcekit-lsp or Swift compiler enrichment in a
   future slice THEN it SHALL document toolchain requirements, fallback behavior,
   and how unresolved diagnostics reduce coverage.
5. WHEN a limitation applies to Objective-C bridging, generic specialization,
   conditional compilation, protocol witness/runtime dispatch, macros,
   generated code, unresolved imports, typealiases, or availability conditions
   THEN the rule catalog and report SHALL use stable limitation labels.

### Requirement 9: Coverage, Gaps, And Reports

**User Story:** As a user, I want Swift scan reports to make partial analysis
clear instead of presenting silence as confidence.

#### Acceptance Criteria

1. WHEN Swift symbol or relationship extraction cannot prove an identity,
   relationship, or coverage boundary THEN the adapter SHALL emit
   `AnalysisGap` with `Tier4Unknown`, a rule ID, safe gap properties, file path,
   line span when available, commit SHA, and extractor version.
2. WHEN emitting a Swift identity or relationship gap THEN `gapKind` SHALL use a
   documented value from the design's gap-kind list or a rule-cataloged
   successor value.
3. WHEN any Swift identity or relationship gap exists in the selected scan scope
   THEN the manifest SHALL use reduced coverage unless a future implementation
   explicitly proves the gap is outside selected analysis scope.
4. WHEN no relationship evidence is found under reduced coverage THEN downstream
   reducers and reports SHALL treat that as no evidence under reduced coverage,
   not clean absence.
5. WHEN report output summarizes Swift symbols and relationships THEN it SHALL
   include fact counts by type/tier/rule, relationship counts by kind, known
   gaps, coverage labels, and limitations.
6. WHEN raw snippets, raw build settings, raw file contents, local absolute
   paths, raw remotes, credentials, or secrets would appear in output THEN they
   SHALL be omitted or hashed.

### Requirement 10: Determinism And Validation

**User Story:** As a maintainer, I want repeatable tests that prove the Swift
relationship model is deterministic and public-safe.

#### Acceptance Criteria

1. WHEN the same Swift fixture is scanned twice at the same commit with the same
   options THEN fact IDs, symbol IDs, relationship IDs, and SQLite rows SHALL
   match except documented timestamp fields.
2. WHEN fixture files are reordered or discovered in different filesystem order
   THEN symbol and relationship outputs SHALL remain stable.
3. WHEN fixtures include duplicate names, ambiguous extensions, unresolved
   imports, conditional compilation, macro syntax, generated-code markers, and
   protocol conformance candidates THEN tests SHALL assert explicit gaps or
   downgraded evidence.
4. WHEN implementation changes shared storage or combine behavior THEN .NET
   schema/export/combine/report tests SHALL pass.
5. WHEN implementation changes Swift adapter behavior THEN validation SHALL
   include local Swift fixtures and at least one reduced or unsupported path.
6. WHEN Swift relationship rows are emitted THEN tests SHALL prove that
   canonical relationship kinds are traversable by combine/path-style readers or
   explicitly non-traversable when marked as candidate evidence.
7. WHEN Swift indexes are combined with other language indexes THEN tests SHALL
   assert Swift source-local symbol IDs do not collide with existing language
   symbol prefixes and are not treated as cross-language identity.

## Explicit Limitations

The implementation must document and enforce these limitations in rules,
reports, and tests:

- Objective-C bridging and selectors are not resolved by SwiftSyntax-only
  evidence.
- Generic specialization does not create distinct proven runtime symbol
  identities in v0.
- Conditional compilation, platform availability, build configurations, target
  membership, and package products can change visible declarations.
- Protocol witness selection, dynamic dispatch, default protocol
  implementations, optional Objective-C protocol requirements, and runtime
  conformance behavior are not proven by local syntax.
- Macros and generated code are not expanded unless a future deterministic
  toolchain slice explicitly adds bounded support.
- Unresolved imports, typealiases, nested generic spellings, and external
  dependencies cap relationship confidence.
- Raw snippets and unsafe values are not stored by default.

## Spec PR Validation Commands

```bash
node scripts/kiro-review.mjs --phase swift-adapter-v0-symbol-identity-relationships --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase swift-adapter-v0-symbol-identity-relationships --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
git diff --check
./scripts/check-private-paths.sh
```

## Future Implementation Validation Commands

Do not run these commands for this spec-only PR. `src/swift` does not exist yet.
The implementation PR should run the relevant commands below after it creates a
documented Swift package path or update the command with the chosen path:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
swift test --package-path src/swift
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index <swift-scan>/index.sqlite --label swift --out <tmp>/combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index <tmp>/combined.sqlite --out <tmp>/combined-report
dotnet run --project src/dotnet/TraceMap.Cli -- paths --index <tmp>/combined.sqlite --out <tmp>/combined-paths
./scripts/check-private-paths.sh
git diff --check
```
