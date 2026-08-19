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
- No claim that the EDMX is deployed, current, provider-compatible, or used at
  runtime.
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
- Evidence tiers: `Tier1Semantic` for CLR-to-conceptual edges,
  `Tier2Structural` for end-to-end CLR-to-storage edges, `Tier4Unknown` for
  gaps.

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
- End-to-end edges carry `supportingFactIds` listing the ordered chain of
  upstream facts: CLR declaration evidence, `edmx-csdl-entity`,
  `edmx-csdl-entity-set`, `edmx-msl-entity-table`, `edmx-ssdl-entity-set` for
  `MapsToStorageTable`; CLR property evidence, `edmx-csdl-property`,
  `edmx-msl-property-column`, `edmx-ssdl-column` for `MapsToStorageColumn`.
  Conceptual-only edges carry their prefix chains.

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
  -> CSDL entity type                  (MapsToConceptualEntity, Tier1)
  -> CSDL entity set                   (supporting edmx-csdl-entity-set fact)
  -> MSL EntitySetMapping / EntityTypeMapping / MappingFragment
                                        (supporting edmx-msl-entity-table fact)
  -> SSDL entity set                   (MapsToStorageTable, Tier2)
  -> physical table descriptor          (storageObjectName on the SSDL set)
```

Property chain:

```text
canonical CLR property symbol          (Tier1 member symbol ID)
  -> CSDL property                     (MapsToConceptualProperty, Tier1)
  -> MSL ScalarProperty                (supporting edmx-msl-property-column fact)
  -> SSDL column descriptor            (MapsToStorageColumn, Tier2)
```

### D4. Exact reconciliation rules

Order of resolution (all joins exact-string, ordinal comparison):

1. CLR entity pool: Tier1 `TypeDeclared` facts from
   `csharp.semantic.declarations.v1` (compiler-resolved symbol ID + namespace
   + assembly identity). Syntax-only declarations are ineligible. When the
   scan has no Tier1 symbol evidence at all, emit one composition-unavailable
   gap per EDMX file and stop (no edges).
2. CLR-to-CSDL entity join: exact equality between
   `{CSDL Schema/@Namespace}.{EntityType/@Name}` and the CLR namespace
   qualified type name. All matches must agree on a single assembly identity
   (`{assemblyName}` plus version when present):
   - exactly one symbol -> reconciled;
   - matches in more than one assembly -> gap `AmbiguousClrSymbolReconciliation`;
   - no match -> gap `MissingGeneratedCode` (scan stays partial).
   `DbSetDeclared`/`DbContextDeclared` facts may corroborate (their
   `entityTypeSymbolId` must equal the reconciled ID when present) but are not
   required; entities without a `DbSet` still compose.
3. CLR property pool: members declared on the reconciled entity symbol,
   resolved from the same Roslyn compilation (see D5), keyed by containing
   symbol ID + exact member name. No cross-type lookup.
4. CSDL `EntitySet/@EntityType` resolution: if the attribute value contains a
   namespace qualifier, resolve across conceptual schemas by exact qualified
   name; otherwise resolve by exact simple name within the containing schema.
   Zero or multiple candidates -> gap `AmbiguousLegacyDataModelIdentity`.
5. MSL resolution per `EntitySetMapping`:
   - `EntitySetMapping/@Name` must resolve to exactly one CSDL entity set
     (conceptual container uniqueness is already enforced).
   - `EntityTypeMapping/@TypeName` must be present and plain (not
     `IsTypeOf(...)`); it must resolve by rule 4 semantics to the same CSDL
     entity type referenced by that entity set. `IsTypeOf(...)` -> gap
     `UnsupportedLegacyOrmMappingShape` (hierarchy mapping). Missing or
     unresolvable TypeName -> gap `AmbiguousLegacyDataModelIdentity`.
     Mismatch between TypeName and the entity set's type -> gap
     `AmbiguousLegacyDataModelIdentity`.
   - The reconciled type must be covered by exactly one `MappingFragment`
     across its EntityTypeMapping(s); zero or multiple -> gap
     `AmbiguousLegacyDataModelIdentity` (split mappings stay unsupported).
   - Existing whole-EDMX gates stay in force (single conceptual and storage
     container, sections present, no `Condition`/`ComplexProperty`/
     `FunctionImportMapping` under the mapping).
6. `MappingFragment/@StoreEntitySet` resolution: resolve to exactly one SSDL
   entity set by exact name within the single storage container. Zero or
   multiple candidates -> gap `AmbiguousLegacyDataModelIdentity`. The physical
   table descriptor is the resolved set's `storageObjectName`
   (`Table` attribute, else `Name`) — never the raw MSL string.
7. `ScalarProperty` resolution: `Name` must resolve to exactly one CSDL
   `Property` on the reconciled entity type (NavigationProperty is not a
   scalar property and does not compose); `ColumnName` must resolve to exactly
   one SSDL `Property` on the storage entity type referenced by the resolved
   store entity set. Zero or multiple candidates -> gap
   `AmbiguousLegacyDataModelIdentity` for that member; other members compose
   independently.

### D5. Composition stage and the bounded CLR property inventory

The composition runs as a dedicated post-extraction stage (recommended: a new
`LegacyDataSymbolComposition` pass invoked from `ScanEngine` after
`LegacyDataMetadataExtractor.Extract`, with access to the per-project Roslyn
compilation the semantic extractor already uses). It:

- consumes emitted LegacyData descriptor facts (never re-parsing the EDMX) and
  the Tier1 CLR symbol inventory;
- resolves property member symbols for reconciled entity types only, bounded
  to those types (this closes the "no semantic PropertyDeclared today" input
  gap without emitting a global member inventory);
- emits the four composed kinds plus gaps;
- records provenance under its own extractor identity, recommended
  `ScannerVersions.LegacyDataSymbolComposition = "legacy-data-composition/0.1.0"`
  so spans and tier ceilings are attributable to the composition rather than
  to the metadata parser.

Implementation mechanism for the bounded property inventory (in-extractor
emission vs post-pass lookup) is left to the implementation PR provided the
contract holds: canonical member symbol IDs, single-compilation resolution,
no syntax fallback.

### D6. Tier model and ceilings

- `MapsToConceptualEntity`, `MapsToConceptualProperty`: `Tier1Semantic` — the
  CLR endpoint is compiler-resolved and every join is exact. These edges are
  never emitted from syntax-only evidence; syntax-only situations emit gaps
  instead. This satisfies "compiler-resolved composition may be
  Tier1Semantic".
- `MapsToStorageTable`, `MapsToStorageColumn`: capped at `Tier2Structural` —
  they transit MSL/SSDL descriptor evidence that is Tier2 structural
  (weakest-link cap, matching the `legacy.data.model.relationship.v1`
  precedent that conclusions are capped at the weakest supporting source).
- Gaps: `Tier4Unknown`.
- Ceilings preserved: EDMX descriptors remain Tier2 under
  `legacy.data.edmx.v1`; `LegacyDataGeneratedCodeLinked` keeps its Tier2/Tier3
  model; linkage tiers never upgrade descriptor tiers; composed edges never
  upgrade any supporting fact.

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
| supportingFactIds | ordered upstream chain (D2) |
| coverageLabel | `full`, or `reduced` when any supporting descriptor is reduced |
| limitations | closed codes: `edmx-static-design-time` (always), `generated-code-freshness-unverified` (always), `storage-join-structural` (storage edges) |

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
- Reverse impact: decision — traverse the four kinds upstream under a new
  `mapping` filter value added to
  `ReverseImpactContract.SupportedRelationshipFilters` (additive to the closed
  set; default filters unchanged, so `mapping` is opt-in exactly like
  `http`/`database`). Hop kind = relationship kind; hop directions reuse the
  shipped `OriginalDirection`/`TraversalDirection` pair.
  Rejected alternative: reusing the `database` filter. Rejected because
  today's `database` hops are Tier1 canonical runtime-operation boundary facts
  (`IsCanonicalSemanticBoundaryFact` gating); mixing static design-time
  mapping edges would blur runtime-operation semantics — the overclaim the
  non-claims boundary forbids. Recorded as owner question Q1.
- Reducer: no change. Composed facts are deliberately absent from
  `DefiniteUsageFactTypes`/`ProbableSemanticFactTypes`; adding them is a
  separate reducer decision (deferred follow-up).
- Reporting/release-review: consume only through the existing path/reverse
  surfaces above; no new report is introduced by this spec.

### D9. Fail-closed decision table

| Situation | Outcome | Classification |
| --- | --- | --- |
| Same simple name in multiple namespaces | qualified join only; unmatched side gaps | `AmbiguousClrSymbolReconciliation` or `MissingGeneratedCode` |
| Same qualified name in multiple assemblies | gap, no edge | `AmbiguousClrSymbolReconciliation` |
| Partial classes in one assembly | merges to one symbol; composes | — |
| Missing generated code | gap, partial scan, descriptors unchanged | `MissingGeneratedCode` |
| No Tier1 compiler evidence in scan | one gap per EDMX file, no edges | `ClrSymbolEvidenceUnavailable` (new) |
| Inherited CSDL entity (BaseType) | no composed chain (existing gap stands) | `UnsupportedLegacyOrmMappingShape` |
| Split mapping / multiple fragments | gap, no edge | `AmbiguousLegacyDataModelIdentity` |
| `IsTypeOf(...)` TypeName | gap, no edge | `UnsupportedLegacyOrmMappingShape` |
| Conditional mapping (`Condition`) | gap, no edge (existing) | `UnsupportedLegacyOrmMappingShape` |
| Complex types (`ComplexProperty`) | gap, no edge (existing) | `UnsupportedLegacyOrmMappingShape` |
| Function imports / modification functions | gap, no edge (existing) | `UnsupportedLegacyOrmMappingShape` |
| Association mappings | out of composed scope; existing behavior unchanged | existing |
| Provider extensions | out of composed scope; hashed/omitted as today | existing |
| Malformed/incomplete EDMX | gap, no edge (existing) | `MalformedLegacyDataMetadata` / `UnsupportedLegacyDataMetadataVersion` |
| TypeName missing/mismatched/unresolvable | gap, no edge | `AmbiguousLegacyDataModelIdentity` |
| Store entity set unresolved in SSDL | gap, no edge | `AmbiguousLegacyDataModelIdentity` |
| ScalarProperty/column unresolved | per-member gap; siblings compose | `AmbiguousLegacyDataModelIdentity` |

New gap classifications owned by the composition rule:
`AmbiguousClrSymbolReconciliation`, `ClrSymbolEvidenceUnavailable`. All others
are existing closed vocabulary reused unchanged.

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

Fixture matrix (synthetic EF6 database-first repo under
`samples/ef6-edmx-composition/`, plus inline test repositories in tests;
`System.Data.Entity` stubs defined in fixture code so no EF6 package is
required):

| # | Case | Proves |
| --- | --- | --- |
| F1 | Happy path entity | exact CLR entity -> CSDL type -> set -> MSL -> SSDL set -> table; endpoints, span, tier, rule, supporting IDs |
| F2 | Happy path property | exact CLR property -> CSDL property -> ScalarProperty -> SSDL column |
| F3 | Decoy type name | `EntityTypeMapping/@TypeName="Model.Customer"` with `EntitySetMapping/@Name="Customers"` and a decoy CLR type `Customers`; only `Customer` composes |
| F4 | Table via SSDL | `StoreEntitySet="Customers"` where the SSDL set has `Table="dbo.CustomerTable"`; composed edge targets the SSDL set and reports that table |
| F5 | Namespaces | `ModelA.Customer` and `ModelB.Customer` both compose to their own chains; no cross-wiring |
| F6 | Assemblies | two assemblies each declaring `Model.Customer`; gap `AmbiguousClrSymbolReconciliation`, no edge |
| F7 | Ambiguity | entity set referencing a missing type; TypeName mismatch; unresolved store set; per-member scalar mismatches — explicit gaps |
| F8 | Unsupported shapes | split fragments, inherited entity, `Condition`, `IsTypeOf`, `ComplexProperty`, `FunctionImportMapping`/modification functions, association mapping, provider extension — fail closed, no edges, existing gaps intact |
| F9 | Missing generated code | EDMX entity with no CLR type anywhere; `MissingGeneratedCode`, partial coverage, descriptors unchanged |
| F10 | No compiler evidence | build-failure fixture; one composition-unavailable gap, no short-name fallback |
| F11 | Persistence round-trip | `facts.ndjson` reload + `index.sqlite` readback + combined import; direction source=CLR target=descriptor unchanged; view rows carry the kind |
| F12 | Traversal | reverse impact from table selector reaches CLR entity then callers (direct + transitive); member-level property/column path retained in path reporting |
| F13 | Determinism | two scans of the same commit produce identical fact IDs and properties |

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
