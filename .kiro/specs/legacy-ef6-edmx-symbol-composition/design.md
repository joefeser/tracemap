# Legacy EF6 EDMX Symbol Composition Design

Part of #680. Specification only: this design defines the deterministic
composition between compiler-resolved EF6 CLR entity/property symbols and the
shipped EDMX CSDL/SSDL/MSL evidence. It must not be implemented in the spec PR.

## Overview

Add one narrowly versioned composition rule, `legacy.data.edmx.symbol-composition.v1`,
that joins canonical CLR symbols to EDMX descriptors through exact
reconciliation and emits `SymbolRelationship` facts whose endpoints are the
canonical CLR symbol ID (source) and the EDMX descriptor `stableModelKey`
(target). The composed facts reuse the shipped symbol-relationship persistence
path (`symbol_relationships`, `combined_symbol_relationships`,
`combined_dependency_edges`) so no new consumer is required. Every ambiguous or
unsupported join fails closed with a rule-backed `AnalysisGap`.

Governing principle: a loud incomplete mapping is safer than a plausible wrong
entity-to-table path.

## Goals

- Explain the bounded static chains of issue #680 with canonical identity at
  every hop.
- Honor `EntityTypeMapping/@TypeName` and resolve `StoreEntitySet` through SSDL
  before any table descriptor is reported.
- Preserve descriptor tier ceilings: EDMX descriptors stay Tier2Structural;
  composition never upgrades them.
- Keep direction, provenance, spans, and supporting fact IDs through
  serialization, persistence, combination, and reverse traversal.
- Fail closed, loudly, everywhere the join is not provably unique.

## Non-Goals

- No runtime EF model loading, generated-code execution, database access, SQL
  or migration execution.
- No claim that the EDMX is deployed, current, provider-compatible, or used
  at runtime.
- No runtime reachability, query behavior, lazy-loading, change-tracking,
  schema-existence, or production-state claims (all non-claims of #680 are
  preserved).
- No second EDMX parser; EDMX reads extend `LegacyDataMetadataExtractor` only.
- No duplication or re-tiering of existing `LegacyData*` descriptor facts.
- No association, function-import, or modification-function composition.
- No reducer classification changes and no LLM/embedding/vector matching.

## Existing Foundation (inspected on `origin/dev` @ `1b79f4e6`)

EF6 CLR side:

- `CSharpSemanticExtractor.AddDbContextFacts`
  (`src/dotnet/TraceMap.Core/CSharpSemanticExtractor.cs:3343`) emits
  `DbContextDeclared` and `DbSetDeclared` under `database.ef.v1` at
  Tier1Semantic. `DbSetDeclared` carries the canonical entity symbol block
  (`entityTypeSymbolId`, `entityTypeSymbolLanguage`, `entityTypeSymbolKind`,
  `entityTypeSymbolDisplayName`, `entityTypeSymbolAssemblyName`,
  `entityTypeSymbolAssemblyVersion`, `entityTypeContainingSymbolId`).
- `DerivesFromDbContext` (`CSharpSemanticExtractor.cs:4828`) matches
  `System.Data.Entity.DbContext`; `IsDbSetType` (`:4842`) recognizes EF6
  `DbSet<T>` and `IDbSet<T>` in `System.Data.Entity`.
- `AddTypeDeclarationFacts` (`CSharpSemanticExtractor.cs:774`) emits Tier1
  `TypeDeclared` facts under `csharp.semantic.declarations.v1` with the full
  canonical symbol property block (`targetSymbolId`, assembly name/version,
  namespace, name). This is the canonical CLR entity inventory the composition
  consumes. The syntax fallback `TypeDeclared`
  (`CSharpSyntaxExtractor.cs:99`) carries no namespace, assembly, or symbol ID
  and is NOT eligible.
- `CSharpSymbolIdentityProvider` builds IDs such as
  `csharp type {AssemblyKey} {DisplayString}` with
  `AssemblyKey = {name}@{version}`; `SymbolIdentity`
  (`src/dotnet/TraceMap.Core/Models.cs:106`) is the shared record.
- Semantic `PropertyDeclared` facts do not exist today; only Tier3 syntax
  `PropertyDeclared` (`CSharpSyntaxExtractor.cs:134`) without symbol IDs.
  Property-level canonical identity is therefore a bounded input the
  composition stage must build (Decision D5).

EDMX side (`src/dotnet/TraceMap.Core/LegacyDataMetadataExtractor.cs`):

- `ExtractEdmx` (`:580`) parses CSDL entity types/properties/entity sets, SSDL
  entity sets/columns, and functions under `legacy.data.edmx.v1` at
  Tier2Structural, with `stableModelKey` model identity
  (`LegacyDataModelIdentity.Apply`, `LegacyDataModelIdentity.cs:19`, key
  format `ldm:{hash}` seeded from format/modelKind/role/path/scope/parts).
- `AddEdmxMappings` (`:712`) emits MSL `entity-table` and `property-column`
  `LegacyDataMappingDeclared` facts. `EntitySetMapping/@Name` and
  `MappingFragment/@StoreEntitySet` are copied as name strings;
  `EntityTypeMapping/@TypeName` is never read; `StoreEntitySet` is never
  resolved through SSDL; more than one `MappingFragment` per
  `EntitySetMapping` is an `AmbiguousLegacyDataModelIdentity` gap (`:730`).
- Unsupported shapes already gap: inherited CSDL entities (`:621`), MSL
  `Condition`/`ComplexProperty`/`FunctionImportMapping` (`:715`), duplicate
  containers (`:602`), missing sections (`:595`).
- Generated-code linkage (`AddGeneratedCodeLinks`, `:1383`) is
  filename/type-name/syntax based under `legacy.data.generated-link.v1`
  (Tier2 `explicit-generated-file`, Tier3 `type-name-syntax-fallback`,
  Tier4 gaps `AmbiguousGeneratedCodeLink`/`MissingGeneratedCode`).

Persistence and consumers:

- `CodeFact` (`src/dotnet/TraceMap.Core/Models.cs:87`) carries fact ID, rule,
  tier, source/target symbols, evidence span with extractor ID/version, and a
  properties bag. Fact IDs are SHA-derived stable hashes (`FactFactory`).
- `SymbolRelationship` facts whose properties contain `relationshipKind`,
  `sourceSymbolId`, and `targetSymbolId` persist to `symbol_relationships`
  (`src/dotnet/TraceMap.Storage/SqliteIndexWriter.cs:436`); symbol role blocks
  (`{role}SymbolId` + `Language`/`Kind`/`DisplayName`/`AssemblyName`/...)
  register rows in `symbols`/`fact_symbols`/`symbol_occurrences` for the roles
  `source|target|argument|parameter|origin|constructor` (`:498`).
- Combined indexes import relationships verbatim
  (`CombinedIndexBuilder.ImportSymbolRelationshipsAsync`) and the
  `combined_dependency_edges` view exposes `relationship_kind` as edge kind
  with `left join combined_symbols ... coalesce(display_name, symbol_id)`
  (`src/dotnet/TraceMap.Combine/CombinedIndexBuilder.cs:445`), so descriptor
  endpoints survive even without registered symbol rows.
- Reverse impact (`src/dotnet/TraceMap.Core/ReverseImpactTraversal.cs`) has a
  closed filter contract `calls|database|http|inheritance|references`
  (`SupportedRelationshipFilters`), traverses edges upstream by indexing on
  target and walking to source, records `OriginalDirection=SourceToTarget`,
  `TraversalDirection=TargetToSource` (`ToHop`), and only recognizes
  SymbolRelationship kinds in `ImpactRelationshipKinds` (inheritance-style,
  `:211`).
- The semantic `CreateSymbolRelationshipFact` shape
  (`CSharpSemanticExtractor.cs`) defines the property contract to mirror:
  `relationshipKind`, `relationshipSource`, display `sourceSymbol`/
  `targetSymbol`, plus canonical role blocks for both endpoints.

## Design Decisions

### D1. Rule decision: new `legacy.data.edmx.symbol-composition.v1`

Decision: one new narrowly versioned rule owns the composition.

- Emits: `SymbolRelationship` (composed edges), `AnalysisGap` (fail-closed
  outcomes).
- Evidence tiers: weakest-link composition capped at `Tier2Structural` for
  both CLR-to-conceptual and CLR-to-storage edges because every chain includes
  a Tier2 EDMX descriptor; `Tier4Unknown` for gaps.

Rejected alternative: extend `legacy.data.generated-link.v1` with a
compiler-resolved `linkKind`. Rejected because:

1. That rule's documented contract is descriptor-to-generated-file/type
   linkage with a Tier2/Tier3/Tier4 tier model, and its catalog entry says
   compiler-resolved semantic links "require a documented implementation
   update" — the narrowest documented update is a versioned rule, not a
   mutation of an active rule's emits and tier model.
2. The composition is a multi-hop canonical identity join with different
   endpoints (model descriptors, not designer files), different evidence
   semantics, and its own gap vocabulary; folding it into generated-link
   semantics would force every existing consumer of that rule
   (projection exclusions, attachment symbols, catalog tests) to re-derive
   behavior.
3. House precedent separates legacy-data concerns into narrowly versioned
   rules (`legacy.data.model.identity.v1`, `...relationship.v1`,
   `...surface.v1`); a composition rule follows that pattern.

Also rejected: `legacy.data.model.generated-link.v1` — reserved for
model-normalized mapped-type syntax links (in use by NHibernate, always Tier3
reduced); different join semantics.

### D2. Fact model: `SymbolRelationship` with canonical endpoints

Composed facts are `FactTypes.SymbolRelationship` under the new rule, mirroring
the shipped property contract:

- `relationshipKind` in the closed set:
  - `MapsToConceptualEntity` — CLR entity type symbol -> CSDL `EntityType`
    descriptor (`edmx-csdl-entity` scope).
  - `MapsToConceptualProperty` — CLR property symbol -> CSDL `Property`
    descriptor (`edmx-csdl-property` scope).
  - `MapsToStorageTable` — CLR entity type symbol -> SSDL entity set/table
    descriptor (`edmx-ssdl-entity-set` scope).
  - `MapsToStorageColumn` — CLR property symbol -> SSDL column descriptor
    (`edmx-ssdl-column` scope).
- `relationshipSource = "edmx-symbol-composition"` on this rule.
- Direction: source is always the canonical CLR symbol; target is always the
  EDMX descriptor (`stableModelKey`). `SourceToTarget` is the mapping
  direction; reverse consumers walk it backwards exactly like other edges.
- Source role block: canonical `sourceSymbolId`
  (`csharp type {assembly} {display}` or `csharp property ...`), language
  `csharp`, kind, display name, assembly name/version, containing symbol ID.
- Target role block: `targetSymbolId = stableModelKey` (`ldm:...`),
  `targetSymbolLanguage = "edmx"`, `targetSymbolKind` from the closed set
  `EdmxConceptualEntityType | EdmxConceptualProperty | EdmxStorageEntitySet |
  EdmxStorageColumn`, `targetSymbolDisplayName` from the existing safe
  display-name policy (clear when `LegacyDataSafeValues.IsSafeIdentifier`
  passes, `hash:` form otherwise). Registering the target block lets
  `symbols`/`combined_symbols` display joins resolve; the view's `coalesce`
  guarantees edge presence regardless.
- End-to-end edges carry `supportingFactIds` listing the complete ordered
  static chain of upstream facts: CLR declaration evidence, the exact selected
  namespace-bridge evidence,
  `edmx-csdl-entity`, `edmx-csdl-entity-set`, `edmx-msl-entity-table`,
  `edmx-ssdl-entity-set` for `MapsToStorageTable`; CLR property evidence,
  the entity's selected namespace-bridge evidence, `edmx-csdl-property`,
  `edmx-msl-property-column`, `edmx-ssdl-column` for
  `MapsToStorageColumn`. Conceptual-only edges carry their prefix chains. The
  bridge evidence is the bounded semantic attribute fact for mechanism 1, the
  proven generation/project metadata fact for mechanism 2, or the explicit
  Tier2 generated-file link for mechanism 3. A bridge is never implicit.
  `namespaceBridgeFactId` identifies that fact and must occur in
  `supportingFactIds`; `namespaceBridgeMechanism` is one of the closed values
  `semantic-attribute | generation-metadata | explicit-generated-file`. For
  mechanism 1, the bounded attribute read may enrich the Tier1 declaration
  fact, in which case the same fact ID serves both declaration and bridge roles
  and occurs once in the ordered list.

This rides the existing persistence path unchanged: `facts.ndjson` standard
serialization, `symbol_relationships` rows, verbatim combined import, view
edge kind = relationship kind. No schema changes are required.

Rejected alternative: emitting a `LegacyData*Linked`-style fact (like
`LegacyDataGeneratedCodeLinked`) that stays out of the edge tables and reaches
graphs only through display-name projection. Rejected because it would
reproduce exactly the display-name attachment weakness issue #680 exists to
remove, and would require new consumer plumbing (a new consumer) to traverse.

Rejected alternative: also emitting intra-EDMX edges (CSDL entity set ->
SSDL entity set, etc.) as graph edges. Rejected because the chain hops are
already represented by shipped descriptor facts with spans, tiers, and rule
IDs, and the composed edge's `supportingFactIds` makes every hop auditable
without flooding graphs with intra-document edges. Descriptor facts are not
duplicated.

### D3. Canonical identity chains

Entity chain (each arrow is either a composed edge or a supporting descriptor
fact, all auditable from the composed edges):

```text
canonical CLR entity symbol            (Tier1 TypeDeclared symbol ID)
  -> CSDL entity type                  (MapsToConceptualEntity, Tier2 ceiling)
  -> CSDL entity set                   (supporting edmx-csdl-entity-set fact)
  -> MSL EntitySetMapping / EntityTypeMapping / MappingFragment
                                        (supporting edmx-msl-entity-table fact)
  -> SSDL entity set                   (MapsToStorageTable, Tier2)
  -> physical table descriptor          (storageObjectName on the SSDL set)
```

Property chain:

```text
canonical CLR property symbol          (Tier1 member symbol ID)
  -> CSDL property                     (MapsToConceptualProperty, Tier2 ceiling)
  -> MSL ScalarProperty                (supporting edmx-msl-property-column fact)
  -> SSDL column descriptor            (MapsToStorageColumn, Tier2)
```

### D4. Exact reconciliation rules

General resolution principle: every resolution step below must produce
exactly one candidate; zero or multiple candidates fail closed with the
classification stated for that step. All joins are exact ordinal string
comparisons.

1. CLR entity pool: Tier1 `TypeDeclared` facts from
   `csharp.semantic.declarations.v1` (compiler-resolved symbol ID + namespace
   + assembly identity). Syntax-only declarations are ineligible. When the
   scan has no Tier1 symbol evidence at all, emit one composition-unavailable
   gap per EDMX file and stop (no edges).
2. CLR-to-CSDL entity reconciliation follows the namespace evidence ladder
   (owner review correction; controlling rule). Generated CLR namespaces may
   legitimately differ from the CSDL namespace (T4 generation, custom-tool
   namespace configuration, generation style), so exact qualified equality is
   only one ladder mechanism, not the general rule. The ladder is tried in
   order and stops at the first mechanism that proves a unique mapping:
   - Mechanism 1 (preferred): explicit compiler-resolved EF generated-type
     metadata exposing the exact conceptual namespace and type identity —
     the `System.Data.Entity.Core.Objects.DataClasses.EdmEntityTypeAttribute`
     family with `NamespaceName`/`Name` named arguments, or equivalent
     compiler-visible conceptual identity emitted by attribute-bearing EF6
     generation styles (ObjectContext/EntityObject and self-tracking
     styles). The conceptual identity comes from the attribute, so a divergent
     CLR namespace still reconciles. Requires a bounded semantic attribute
     read (the extractor's existing attribute-argument helpers are the
     precedent); not implemented today. The read adds safe/hash conceptual-
     identity properties to the Tier1 declaration evidence under its existing
     rule/catalog contract, so that declaration fact is also the selected
     bridge fact; raw attribute text is not retained.
   - Mechanism 2: deterministic checked-in generation or project metadata
     that proves the generated CLR namespace/type relationship to the EDMX.
     No such read exists today. The implementation PR must enumerate and
     prove the exact sources it reads; until such an addition lands, this
     mechanism contributes no matches and must not be assumed to exist.
   - Mechanism 3: exact equality between
     `{CSDL Schema/@Namespace}.{EntityType/@Name}` and the CLR
     namespace-qualified type name — a documented supported convention, not a
     general truth. It applies only over generated-code candidates already
     scoped to that EDMX by the shipped designer/generated-file convention
     (`legacy.data.generated-link.v1` scoping), requires the explicit-generated-
     file Tier2 link (the Tier3 type-name syntax fallback is insufficient and
     yields `UnresolvedGeneratedNamespace`), and
     it is always a qualified full-name comparison, never a simple-name
     comparison.
   - Mechanism 4 (fallback): if no mechanism proves a unique mapping, emit a
     reduced-coverage gap classified `UnresolvedGeneratedNamespace` and no
     composed edge.
   Regardless of mechanism: matches across more than one assembly identity
   fail closed (`AmbiguousClrSymbolReconciliation`). Before uniqueness is
   accepted, declarations are grouped by canonical symbol ID and distinct
   scan-relative compilation scope (`CodeFact.ProjectPath` from the semantic
   fact). Two scopes sharing the same canonical ID — including assemblies with
   identical name/version — are ambiguous and produce no edge. Missing scope
   evidence when duplicate declarations exist also fails closed. This is a
   composition guard, not a silent change to the repository-wide symbol-ID
   format. No candidate at all
   within the mechanism-3 scope yields `MissingGeneratedCode`;
   `DbSetDeclared`/`DbContextDeclared` facts may corroborate (their
   `entityTypeSymbolId` must equal the reconciled ID when present) but are
   not required; entities without a `DbSet` still compose.
3. CLR property pool: canonical member symbols declared on the reconciled
   entity type, emitted as bounded semantic property-symbol evidence during
   the existing semantic pass (see D5), keyed by containing symbol ID + exact
   member name. No cross-type lookup. Missing semantic property evidence for
   a reconciled member yields a typed gap
   (`MissingSemanticPropertyEvidence`), never name attachment.
4. CSDL `EntitySet/@EntityType` resolution: if the attribute value contains a
   namespace qualifier, resolve across conceptual schemas by exact qualified
   name; otherwise resolve by exact simple name within the containing schema.
   Failures classify `AmbiguousLegacyDataModelIdentity`.
5. MSL resolution per `EntitySetMapping`:
   - `EntitySetMapping/@Name` must resolve to exactly one CSDL entity set
     (conceptual container uniqueness is already enforced).
   - `EntityTypeMapping/@TypeName` must be present and plain (not
     `IsTypeOf(...)`); it must resolve by rule 4 semantics to the same CSDL
     entity type referenced by that entity set. `IsTypeOf(...)` -> gap
     `UnsupportedLegacyOrmMappingShape` (hierarchy mapping). Missing,
     unresolvable, or mismatched TypeName -> gap
     `AmbiguousLegacyDataModelIdentity`.
   - The reconciled type must be covered by exactly one `MappingFragment`
     across its EntityTypeMapping(s); failures -> gap
     `AmbiguousLegacyDataModelIdentity` (split mappings stay unsupported).
   - Existing whole-EDMX gates stay in force (single conceptual and storage
     container, sections present, no `Condition`/`ComplexProperty`/
     `FunctionImportMapping` under the mapping).
6. `MappingFragment/@StoreEntitySet` resolution: resolve to exactly one SSDL
   entity set by exact name within the single storage container; failures
   classify `AmbiguousLegacyDataModelIdentity`. The physical table descriptor
   is the resolved set's `storageObjectName` (`Table` attribute, else
   `Name`) — never the raw MSL string. Parsing adds a
   `storageEntityTypeIdentity` value to the SSDL entity-set descriptor: a
   deterministic, non-display key derived from document identity, SSDL schema
   namespace, and the exact `EntitySet/@EntityType` reference.
7. `ScalarProperty` resolution: `Name` must resolve to exactly one CSDL
   `Property` on the reconciled entity type (NavigationProperty is not a
   scalar property and does not compose); `ColumnName` must resolve to
   exactly one SSDL `Property` whose descriptor carries the same
   `storageEntityTypeIdentity` as the resolved store entity set. The parser adds
   that key to both descriptor families; composition never performs a global
   column-name join and never depends on an unpersisted side channel.
   Per-member failures classify
   `AmbiguousLegacyDataModelIdentity`; sibling members compose independently.

#### D4.1 Namespace reconciliation evidence availability

To avoid claiming support for metadata the scanner cannot read, the ladder's
evidence is bucketed explicitly:

- Currently available (no extractor change): Tier1 `TypeDeclared` symbol
  blocks, `DbSetDeclared` entity symbol blocks, designer/generated-file
  scoping from `legacy.data.generated-link.v1`, and the EDMX descriptors
  themselves. Mechanism 3 (scoped qualified equality) is evaluable with
  today's evidence once the composition stage exists.
- Requires a bounded extractor addition: mechanism 1 attribute reads
  (`EdmEntityTypeAttribute` family) and any mechanism 2 generation/project
  metadata read. Each addition is an explicit implementation task with its
  own tests and catalog notes.
- Unsupported shapes: attribute-less POCO generation (the common
  DbContext-generator output) with a CLR namespace differing from the CSDL
  namespace and no readable deterministic bridge — gap
  (`UnresolvedGeneratedNamespace`), never a guessed join.
- Future possibilities (explicitly not claimed): generic T4 template
  interpretation, EDMX designer custom annotations, provider manifest
  details.

### D5. Composition stage and the bounded CLR property inventory

Owner resolution (Q2): the preferred direction is bounded semantic
property-symbol emission during the existing C# semantic analysis. Current
compilations are not retained as a general post-extraction service, so a
compilation-backed post-pass is not an "easy option" and is not assumed.

- During the existing semantic pass, entity types proven eligible or boundedly
  candidate for EF/EDMX composition emit canonical property-symbol evidence:
  member symbol
  ID, containing type symbol ID, exact member name, assembly identity, source
  span, and compiler provenance (Tier1). Eligibility is compiler- or
  inventory-visible and bounded to any of:
  (a) the type appears as a `DbSet<T>`/`IDbSet<T>` entity type argument;
  (b) the type carries supported generated conceptual identity attributes
      (mechanism 1 of the D4 ladder);
  (c) the declaring file is an inventory-visible generated/designer candidate
      under the same bounded file-shape convention used by the shipped
      generated-link extractor.
  No global semantic property inventory is added merely for this feature.
- Ordering note (verified on the base commit): semantic facts materialize
  before `LegacyDataMetadataExtractor.Extract` runs
  (`src/dotnet/TraceMap.Core/ScanEngine.cs:791-793`), so eligibility cannot be
  keyed on EDMX descriptor facts or a generated-link fact. Signals (a)–(c) do
  not require those later facts. After `LegacyDataMetadataExtractor.Extract`
  emits the actual generated-file links, composition intersects candidates
  admitted by (c) with one exact Tier2 `explicit-generated-file` link to the
  current EDMX. Unlinked, ambiguous, or Tier3 syntax-fallback candidates do not
  compose. This staged intersection is explicit and requires no retained
  compilation.
- Syntax-only `PropertyDeclared` facts remain ineligible for canonical
  composition. A reconciled entity member without semantic property evidence
  produces a typed gap (`MissingSemanticPropertyEvidence`) rather than name
  attachment.
- If the implementation later needs a compilation-backed composition seam (a
  retained compilation available after extraction), that seam is an explicit
  separate task with documented lifecycle, memory bounds, determinism, and
  cancellation requirements. No such seam exists today and this design does
  not imply one.
- The composition stage itself consumes the emitted symbol evidence and the
  LegacyData descriptor facts (never re-parsing the EDMX), emits the four
  composed kinds plus gaps, and records provenance under its own extractor
  identity, recommended `ScannerVersions.LegacyDataSymbolComposition =
  "legacy-data-composition/0.1.0"`, so spans and tier ceilings are
  attributable to the composition rather than to the metadata parser.

### D6. Tier model and ceilings

- `MapsToConceptualEntity`, `MapsToConceptualProperty`: capped at
  `Tier2Structural`, with the emitted tier equal to the weakest supporting
  fact. Even mechanism 1 is Tier2 because the target CSDL descriptor remains
  Tier2. Mechanism 2 may reduce the edge further when its proved metadata fact
  is weaker; mechanism 3 is Tier2 through its required explicit generated-file
  link. Tier3 generated-link fallback is ineligible and emits a gap rather
  than an edge.
- `MapsToStorageTable`, `MapsToStorageColumn`: capped at `Tier2Structural` —
  they transit MSL/SSDL descriptor evidence that is Tier2 structural
  (weakest-link cap, matching the `legacy.data.model.relationship.v1`
  precedent that conclusions are capped at the weakest supporting source).
- Gaps: `Tier4Unknown`.
- Ceilings preserved: EDMX descriptors remain Tier2 under
  `legacy.data.edmx.v1`; `LegacyDataGeneratedCodeLinked` keeps its Tier2/Tier3
  model; linkage tiers never upgrade descriptor tiers; composed edges never
  upgrade any supporting fact. Compiler-resolved CLR endpoints remain Tier1
  source evidence, but the composed conceptual edge is Tier2 or weaker because
  it joins that source to a Tier2 CSDL descriptor.
- Documented limitation: generated or custom CLR namespaces that no D4
  ladder mechanism can deterministically bridge to the conceptual identity
  gap closed (`UnresolvedGeneratedNamespace`); the composition does not
  recover them by name similarity.

### D7. Evidence contract per composed fact

| Field | Value |
| --- | --- |
| factType | `SymbolRelationship` |
| ruleId | `legacy.data.edmx.symbol-composition.v1` |
| relationshipKind | one of the four closed kinds (D2) |
| direction | source = canonical CLR symbol, target = EDMX descriptor |
| evidenceTier | per D6 |
| evidence span | CLR declaration span for conceptual edges; MSL `MappingFragment`/`ScalarProperty` span for storage edges (the load-bearing join) |
| extractor | `legacy-data-composition/0.1.0` (span extractor ID/version) |
| commitSha | scan manifest commit SHA (as all facts) |
| supportingFactIds | ordered upstream chain including `namespaceBridgeFactId` (D2); duplicate IDs occur once |
| namespaceBridgeFactId | exact supporting fact that proves the selected bridge; required and present in `supportingFactIds` |
| namespaceBridgeMechanism | closed code: `semantic-attribute`, `generation-metadata`, or `explicit-generated-file` |
| coverageLabel | weakest coverage in the complete supporting chain; `reduced` when the bridge or any descriptor is reduced |
| limitations | closed codes: `edmx-static-design-time` (always), `generated-code-freshness-unverified` (always), `conceptual-descriptor-structural` (conceptual edges), `namespace-bridge-structural` (mechanism 2/3), `storage-join-structural` (storage edges) |

Gap facts carry `classification`, `message`, `coverage=reduced`, the anchor
span, and `runtimeProof=False`, matching the shipped `AddGap` envelope.

### D8. Persistence and consumers

- `facts.ndjson`: standard fact serialization; canonical endpoint IDs and all
  envelope fields survive (existing `JsonlFactWriter` contract).
- `index.sqlite`: `symbol_relationships` rows keyed by fact ID with
  source/target IDs, kind, rule, tier, span; role blocks additionally register
  the descriptor endpoint in `symbols` (additive; no schema change).
- Combined indexes: verbatim import; `combined_dependency_edges` exposes the
  kind as edge kind; the `coalesce` fallback keeps descriptor endpoints
  visible; direction unchanged; no reverse duplicate rows.
- Path reporting: composed edges join the graph through the existing view
  read; `NormalizeEdgeKind` must pass the new kinds through unchanged with
  rule/tier evidence retained per hop.
- Reverse impact: owner resolution (Q1) — add a new opt-in `mapping` filter
  value to
  `ReverseImpactContract.SupportedRelationshipFilters` (additive to the closed
  set; default filters unchanged, so `mapping` is opt-in exactly like
  `http`/`database`). Hop kind = relationship kind; hop directions reuse the
  shipped `OriginalDirection`/`TraversalDirection` pair. The filter must
  preserve the existing contract's direct/transitive distinction, per-hop
  evidence, deterministic cycle handling, and fail-closed selector behavior.
  The existing `database` filter is NOT reused: its
  `DatabaseOperationCandidate` edges are deterministic static compiler
  evidence of database operation call patterns — not proof of runtime
  execution — and merging static design-time mapping edges into that filter
  would blur two distinct static evidence families. No reducer or runtime
  claims are added.
- Reducer: no change. Composed facts are deliberately absent from
  `DefiniteUsageFactTypes`/`ProbableSemanticFactTypes`; adding them is a
  separate reducer decision (deferred follow-up).
- Reporting/release-review: consume only through the existing path/reverse
  surfaces above; no new report is introduced by this spec.

### D9. Fail-closed decision table

| Situation | Outcome | Classification |
| --- | --- | --- |
| Same simple name in multiple namespaces | qualified join only; unmatched side gaps | `AmbiguousClrSymbolReconciliation` or `MissingGeneratedCode` |
| Same qualified name in multiple assemblies or distinct compilation scopes sharing one canonical ID | gap, no edge (any ladder mechanism) | `AmbiguousClrSymbolReconciliation` |
| Partial classes in one assembly | merges to one symbol; composes | — |
| Generated/custom namespace with no deterministic bridge | reduced-coverage gap, no edge | `UnresolvedGeneratedNamespace` (new) |
| Missing generated code | gap, partial scan, descriptors unchanged | `MissingGeneratedCode` |
| No Tier1 compiler evidence in scan | one gap per EDMX file, no edges | `ClrSymbolEvidenceUnavailable` (new) |
| Reconciled member without semantic property evidence | typed gap, no property edge | `MissingSemanticPropertyEvidence` (new) |
| Inherited CSDL entity (BaseType) | no composed chain (existing gap stands) | `UnsupportedLegacyOrmMappingShape` |
| Split mapping / multiple fragments | gap, no edge | `AmbiguousLegacyDataModelIdentity` |
| `IsTypeOf(...)` TypeName | gap, no edge | `UnsupportedLegacyOrmMappingShape` |
| Conditional mapping (`Condition`) | gap, no edge (existing) | `UnsupportedLegacyOrmMappingShape` |
| Complex types (`ComplexProperty`) | gap, no edge (existing) | `UnsupportedLegacyOrmMappingShape` |
| Function imports / modification functions | gap, no edge (existing) | `UnsupportedLegacyOrmMappingShape` |
| Association mappings | explicit composition-owned scope gap, no composed edge; existing descriptor/relationship facts unchanged | `UnsupportedLegacyOrmMappingShape` |
| Provider extensions | explicit composition-owned scope gap, no composed edge; hashed/omitted source handling unchanged | `UnsupportedLegacyOrmMappingShape` |
| Malformed/incomplete EDMX | gap, no edge (existing) | `MalformedLegacyDataMetadata` / `UnsupportedLegacyDataMetadataVersion` |
| TypeName missing/mismatched/unresolvable | gap, no edge | `AmbiguousLegacyDataModelIdentity` |
| Store entity set unresolved in SSDL | gap, no edge | `AmbiguousLegacyDataModelIdentity` |
| ScalarProperty/column unresolved | per-member gap; siblings compose | `AmbiguousLegacyDataModelIdentity` |

Gap classification ownership: `AmbiguousClrSymbolReconciliation`,
`ClrSymbolEvidenceUnavailable`, `UnresolvedGeneratedNamespace`, and
`MissingSemanticPropertyEvidence` are new classifications owned and
catalogued by `legacy.data.edmx.symbol-composition.v1`. The reused
classifications (`AmbiguousLegacyDataModelIdentity`,
`UnsupportedLegacyOrmMappingShape`, `MissingGeneratedCode`,
`MalformedLegacyDataMetadata`, `UnsupportedLegacyDataMetadataVersion`) keep
their primary ownership and documented meaning under their current rules
(`legacy.data.edmx.v1` and `legacy.data.generated-link.v1`); the composition
rule's catalog entry lists them as reused values it may emit, so every
emitted gap has exactly one rule ID and a documented classification source.

### D10. Privacy and safe values

- No snippets, connection strings, provider secrets, local paths, or private
  identifiers in composed facts.
- Clear display names only when the existing safe-identifier policy passes;
  hash forms otherwise; namespace URIs and provider details stay hashed or
  omitted exactly as today.
- Fixture material is synthetic; no private repository names, object names,
  paths, schemas, or extracted evidence.

### D11. Determinism

- Enumeration order: EDMX document line order, then ordinal attribute/name
  order (the extractor's existing `OrderBy(GetLine).ThenBy(name)` pattern);
  CLR pool ordered by symbol ID ordinal.
- Fact IDs derive from the stable content hash (scan ID, fact type, rule,
  span, symbols, sorted properties), so repeated scans of the same commit are
  byte-identical.
- Gaps deduplicate per (classification, anchor span, affected identity).

## Testing Strategy

Fixture matrix (owner resolution Q4: test-local synthetic repositories in the
tests project for the first implementation — no maintained `samples/` fixture;
a future sample/demo fixture requires a separate public-proof and
smoke-maintenance decision; `System.Data.Entity` stubs defined in fixture code
so no EF6 package is required):

| # | Case | Proves |
| --- | --- | --- |
| F1 | Happy path entity (namespace parity via the documented equality convention, D4 mechanism 3) | exact CLR entity -> CSDL type -> set -> MSL -> SSDL set -> table; explicit generated-file bridge ID in supporting chain; Tier2 bridge cap; endpoints, span, tier, rule, coverage |
| F2 | Happy path property | exact CLR property -> CSDL property -> ScalarProperty -> SSDL column; decoy same-named column on a different SSDL storage type does not cross-wire |
| F3 | Decoy type name | `EntityTypeMapping/@TypeName="Model.Customer"` with `EntitySetMapping/@Name="Customers"` and a decoy CLR type `Customers`; only `Customer` composes |
| F4 | Table via SSDL | `StoreEntitySet="Customers"` where the SSDL set has `Table="dbo.CustomerTable"`; composed edge targets the SSDL set and reports that table |
| F5 | Namespaces | `ModelA.Customer` and `ModelB.Customer` both compose to their own chains; no cross-wiring |
| F6 | Assemblies | two distinct project/compilation scopes with identical assembly name/version each declare `Model.Customer` and therefore share the shipped canonical ID; gap `AmbiguousClrSymbolReconciliation`, no edge |
| F7 | Ambiguity | entity set referencing a missing type; TypeName mismatch; unresolved store set; per-member scalar mismatches — explicit gaps |
| F8 | Unsupported shapes | split fragments, inherited entity, `Condition`, `IsTypeOf`, `ComplexProperty`, `FunctionImportMapping`/modification functions, association mapping, provider extension — fail closed and no edges; association/provider shapes emit an explicit composition-owned `UnsupportedLegacyOrmMappingShape` gap while existing facts remain intact |
| F9 | Missing generated code | EDMX entity with no CLR type anywhere; `MissingGeneratedCode`, partial coverage, descriptors unchanged |
| F10 | No compiler evidence | build-failure fixture; one composition-unavailable gap, no short-name fallback |
| F11 | Persistence round-trip | `facts.ndjson` reload + `index.sqlite` readback + combined import; direction source=CLR target=descriptor unchanged; view rows carry the kind |
| F12 | Traversal | reverse impact from table selector reaches CLR entity then callers (direct + transitive); member-level property/column path retained in path reporting |
| F13 | Determinism | two scans of the same commit produce identical fact IDs and properties |
| F14 | Attribute bridge, divergent namespace | CLR namespace `App.Data` differs from CSDL namespace `Model`; generated type carries `EdmEntityType(NamespaceName="Model", Name="Customer")`; composes via D4 mechanism 1 with the Tier1 attribute fact in `supportingFactIds`; the composed edge remains Tier2 because the CSDL descriptor is Tier2 |
| F15 | Divergent namespace, no bridge | CLR namespace `App.Data` differs from CSDL namespace `Model`, attribute-less POCO generation, no deterministic generation metadata; gap `UnresolvedGeneratedNamespace`, no edge, descriptors unchanged |

Assertions must check identity, endpoints, provenance (extractor ID/version),
spans, tiers, rule IDs, supporting fact IDs, gap classifications, and coverage
labels — not only counts.

## Out Of Design (Explicit)

- Runtime model verification, schema existence checks, query/lazy-loading or
  change-tracking behavior.
- EDMX versioning/freshness checks (whether generated code matches the EDMX).
- Association, function-import, modification-function, and query-view
  composition.
- Reducer classification of composed edges.
- EF Core, DBML, typed DataSet, NHibernate composition.
- Any UI, site, or release-reporting changes beyond existing consumers.
- A maintained `samples/` or demo fixture (requires a separate public-proof
  and smoke-maintenance decision).
- Generic T4 template interpretation and EDMX designer custom-annotation
  reads (future possibilities, explicitly not claimed).
