# Legacy EF6 EDMX Symbol Composition Requirements

Part of #680. This specification is specification-only: it defines the
deterministic composition between compiler-resolved EF6 CLR entity/property
symbols and TraceMap's already-shipped EDMX CSDL/SSDL/MSL evidence. It does not
implement product code and does not close #680.

## Introduction

TraceMap already recognizes EF6 `System.Data.Entity.DbContext`, `DbSet<T>`, and
`IDbSet<T>` with Roslyn semantic evidence and canonical entity symbol identity,
and already parses checked-in EDMX CSDL, SSDL, and unambiguous MSL mapping
evidence under `legacy.data.edmx.v1`. Today the two evidence families are only
connected by filename/type-name/syntax-based generated-code linkage
(`legacy.data.generated-link.v1`, Tier2/Tier3) and by display-name attachment in
combined path graphs. Neither is a canonical identity relationship, so
entity-to-table and property-to-column impact paths cannot be explained with
compiler-resolved identity.

This spec defines a deterministic, static, design-time-only composition that
joins canonical CLR symbols to EDMX descriptors through explicit reconciliation
rules, emits rule-backed composed relationships with bounded evidence tiers,
and fails closed with `AnalysisGap` facts wherever the join is ambiguous,
unsupported, or missing required evidence.

Public claim level: hidden until implemented and reviewed. Nothing in this spec
claims the EDMX is deployed, current, provider-compatible, or used at runtime.

## Current Context

- EF6 semantic recognition exists: `DbContextDeclared` and `DbSetDeclared` under
  `database.ef.v1` (Tier1Semantic), including canonical
  `entityTypeSymbolId`/assembly identity on `DbSetDeclared`
  (`src/dotnet/TraceMap.Core/CSharpSemanticExtractor.cs`, `AddDbContextFacts`).
- Canonical CLR symbol identity machinery exists:
  `CSharpSymbolIdentityProvider` emits `SymbolId`, language, kind, display name,
  assembly name/version, containing symbol; Tier1 `TypeDeclared` facts already
  carry the full canonical symbol property block.
- EDMX evidence exists: CSDL entities/properties/entity sets, SSDL entity
  sets/columns, and unambiguous MSL entity-table and property-column mappings
  under `legacy.data.edmx.v1` (Tier2Structural), with `stableModelKey` model
  identity and reduced-coverage gaps for unsupported shapes
  (`src/dotnet/TraceMap.Core/LegacyDataMetadataExtractor.cs`).
- `EntityTypeMapping/@TypeName` is currently never read; the conceptual type of
  an MSL mapping is only implied by `EntitySetMapping/@Name`.
- There is no CSDL-to-SSDL canonical resolution: MSL facts copy
  `EntitySetMapping/@Name` and `MappingFragment/@StoreEntitySet` name strings;
  `StoreEntitySet` is never resolved through the SSDL storage container.
- Generated-code linkage is filename/type-name/syntax based
  (`legacy.data.generated-link.v1`); its catalog entry states that future
  compiler-resolved semantic links require a documented implementation update.
- `SymbolRelationship` facts with `relationshipKind` plus
  `sourceSymbolId`/`targetSymbolId` properties already persist to
  `symbol_relationships`, import into `combined_symbol_relationships`, and
  surface in the `combined_dependency_edges` view with direction preserved.
- Reverse-impact traversal (`tracemap.reverse-impact.v1`) currently traverses
  only `calls`, `database`, `http`, `inheritance`, and `references` filters and
  only recognizes a closed set of inheritance-style relationship kinds.

## Relationship To Existing Specs

- `legacy-data-model-metadata-extraction`: introduced EDMX/DBML/typed DataSet
  descriptor extraction and generated-code linkage tiers.
- `legacy-data-model-orm-mapping-completion`: added unambiguous MSL mapping
  facts and defined the inherited/split/conditional fail-closed boundary this
  spec reuses.
- `legacy-data-model-relationship-composition` and
  `legacy-data-model-relationship-completion`: added the relationship gap
  classifier vocabulary this spec reuses for EDMX-side ambiguity.
- `ef-core-mapping-v0`: EF Core fluent-mapping extraction only; no EF6 EDMX
  composition.

This spec does not reopen those scopes. It consumes their shipped facts and
adds one narrowly versioned composition rule.

## Scope

In scope:

- Deterministic reconciliation between canonical EF6 CLR entity/property
  symbols (Roslyn-resolved, namespace- and assembly-qualified) and checked-in
  EDMX CSDL/SSDL/MSL descriptors.
- Exactly four composed relationship kinds: CLR entity to CSDL entity type, CLR
  property to CSDL property, CLR entity to SSDL entity set/table descriptor,
  CLR property to SSDL column descriptor.
- One new narrowly versioned rule with catalog entry, limitations, and gap
  classifications.
- Fail-closed behavior for every ambiguous, unsupported, partial, or missing
  join, with rule-backed `AnalysisGap` facts.
- Persistence and readback through `facts.ndjson`, `index.sqlite`, combined
  indexes, path reporting, and reverse-impact traversal under existing consumer
  contracts.
- A synthetic EF6 database-first fixture matrix proving identity, endpoints,
  provenance, spans, tiers, rule IDs, supporting fact IDs, gaps, and coverage.

Out of scope (do not build in this spec):

- Any runtime EF model loading, generated-code execution, database connection,
  row access, SQL execution, or migration execution.
- Any claim that the EDMX is deployed, current, provider-compatible, or
  selected at runtime.
- A second EDMX parser; all EDMX reads extend the existing
  `LegacyDataMetadataExtractor`.
- Changes to existing `LegacyData*` descriptor facts beyond additive
  properties; no descriptor fact is duplicated, upgraded, or re-tiered.
- Association, function-import, and modification-function mapping composition;
  these remain existing descriptor/gap behavior.
- Reducer impact classification changes; composed facts are not added to
  reducer allowlists.
- EF Core fluent mappings, DBML, typed DataSet, and NHibernate composition.

## Requirements

### Requirement 1: Canonical Identity Chain

**User story:** As an impact reviewer, I want entity-to-table and
property-to-column paths that use canonical identity rather than display-name
attachment, so that a path step is provable rather than plausible.

Acceptance criteria:

1. The spec SHALL define, and the implementation SHALL be able to explain, the
   bounded static chain: canonical CLR entity symbol -> CSDL entity type ->
   CSDL entity set -> MSL `EntitySetMapping`/`EntityTypeMapping`/
   `MappingFragment` -> SSDL entity set -> physical table descriptor.
2. The spec SHALL define, and the implementation SHALL be able to explain, the
   bounded static chain: canonical CLR property symbol -> CSDL property -> MSL
   `ScalarProperty` -> SSDL column descriptor.
3. Every hop SHALL preserve rule ID, evidence tier, file span, commit SHA,
   extractor version, supporting fact IDs, coverage label, and limitations,
   either on the composed fact or on its supporting descriptor facts.
   The exact namespace-bridge fact selected by the reconciliation ladder SHALL
   be present in the ordered supporting-fact chain and SHALL participate in the
   composed fact's weakest-link tier and coverage calculation.
4. The chain SHALL be static design-time evidence only, with
   `runtimeProof=False` and no runtime claims.

### Requirement 2: Deterministic Reconciliation Rules

**User story:** As a scanner author, I want exact reconciliation rules for every
join, so that composition is reproducible and auditable.

Acceptance criteria:

1. CLR entity reconciliation SHALL resolve the generated CLR type to the CSDL
   conceptual type through a deterministic evidence ladder, tried in order:
   1. explicit compiler-resolved EF generated-type metadata exposing the exact
      conceptual namespace and type identity — for example the
      `EdmEntityType(NamespaceName=..., Name=...)` attribute family emitted by
      attribute-bearing EF6 generation styles — read through bounded semantic
      attribute evidence;
   2. deterministic checked-in generation or project metadata, only where the
      scanner can actually read it and only when it proves the generated CLR
      namespace/type relationship to the EDMX;
   3. exact equality between the CSDL namespace-qualified entity type name
      (`Schema/@Namespace` + `EntityType/@Name`) and the compiler-resolved CLR
      namespace-qualified type name, applied only as a documented supported
      convention and only over Tier1 declarations whose declaring files are
      scoped to that EDMX by a persisted, composition-owned generated-file
      scope bridge fact using the tightened same-directory designer-file rule
      (only `{edmxBaseName}.Designer.cs` beside the EDMX, ordinal comparison);
      unrelated designer files in other directories or projects and
      prefix-sibling files SHALL never be scope candidates; that scope
      evidence and any `legacy.data.generated-link.v1` fact SHALL be file
      scoping or corroboration only and SHALL NEVER authorize CLR identity;
   4. if no mechanism proves a unique mapping, an explicit reduced-coverage
      `AnalysisGap` SHALL be emitted and no composed edge SHALL be produced.
2. The ladder SHALL never fall back to global simple-name matching, and
   display labels SHALL never serve as identity.
3. Duplicate qualified types across assemblies SHALL fail closed regardless of
   which ladder mechanism produced the match. Because the shipped canonical
   symbol ID contains assembly name and version but not a project/compilation
   discriminator, composition SHALL also detect declarations with the same
   canonical symbol ID in distinct scan-relative compilation scopes and fail
   closed rather than treating the collapsed ID as unique.
4. The spec SHALL clearly separate currently available evidence, evidence
   requiring a bounded extractor addition, unsupported shapes, and future
   possibilities, and SHALL NOT claim support for metadata mechanisms the
   scanner cannot read.
5. CLR property reconciliation SHALL resolve the member symbol within the
   already-reconciled containing entity type only, by exact member name over
   compiler-resolved property symbol evidence; global or cross-type name
   matching SHALL NOT occur.
6. CSDL `EntitySet/@EntityType` SHALL resolve either as a namespace-qualified
   name across conceptual schemas or as a simple name within the containing
   schema, requiring exactly one candidate.
7. MSL `EntityTypeMapping/@TypeName` SHALL be read and honored as the
   conceptual type of the mapping; `EntitySetMapping/@Name` SHALL be treated as
   a conceptual entity-set name, never as a CLR type name.
8. `MappingFragment/@StoreEntitySet` SHALL be resolved through the SSDL storage
   entity container to exactly one SSDL entity set before any physical table
   descriptor is reported; the table name SHALL come from the resolved SSDL
   entity set (`Table` attribute, else `Name`). The SSDL entity-set descriptor
   SHALL carry a deterministic storage-entity-type identity key derived from
   its exact `EntityType` reference and document scope.
9. MSL `ScalarProperty/@Name` SHALL resolve to exactly one CSDL property on
   the reconciled entity type, and `ScalarProperty/@ColumnName` SHALL resolve
   to exactly one SSDL column on the storage entity type referenced by the
   resolved store entity set. SSDL column descriptors SHALL carry the same
   storage-entity-type identity key so this join does not depend on a global
   column name or an undocumented in-memory side channel.
10. No join anywhere in the composition SHALL use global short-name matching,
    case folding, prefix or suffix trimming, or fuzzy matching of any kind.

### Requirement 3: Fail-Closed Behavior

**User story:** As a reviewer, I want ambiguous and unsupported mappings to be
loud gaps rather than plausible wrong paths, so that I never trust a guessed
entity-to-table edge.

Acceptance criteria:

1. Duplicate simple names across namespaces SHALL NOT collide; each reconciles
   only against its exact namespace-qualified counterpart, and unmatched names
   produce gaps, not fallbacks.
2. Duplicate types across assemblies with the same qualified name SHALL produce
   an explicit composition-owned gap and no composed edges for that type,
   including distinct projects/compilations whose assemblies share the same
   name and version and therefore currently produce the same canonical symbol
   ID.
3. Partial classes within one assembly SHALL reconcile to the single merged
   Roslyn symbol; partial declarations spanning multiple assemblies SHALL fail
   closed per criterion 2.
4. Missing generated code SHALL be classified deterministically against
   divergent namespaces: scoped files that are confirmed semantically
   covered but declare no CLR type for the CSDL entity SHALL produce a
   `MissingGeneratedCode` gap, keep the scan partial, and leave existing
   descriptor facts unchanged; scoped files that DO contain Tier1
   declarations under a divergent namespace with no deterministic bridge
   SHALL instead produce the reduced-coverage `UnresolvedGeneratedNamespace`
   gap of criterion 6.
5. Missing compiler evidence SHALL be evaluated per EDMX scope, not
   scan-wide: when the scan has no Tier1 symbol evidence at all, every EDMX
   produces an explicit composition-unavailable gap with no composed edges;
   and when an EDMX's scoped generated files carry no Tier1 declarations
   because they or their project were not semantically analyzed (for example
   a failed MSBuild load in a multi-project scan where other projects
   produced Tier1 facts), that EDMX SHALL produce
   `ClrSymbolEvidenceUnavailable` — failed analysis SHALL NEVER be reported
   as missing generated code. The composition SHALL NOT fall back to
   syntax-only or short-name joins.
6. A generated or custom CLR namespace that no ladder mechanism can
   deterministically bridge to the CSDL conceptual identity SHALL produce an
   explicit reduced-coverage gap and no composed edge; this is a documented
   limitation, not a recovered join.
7. A reconciled entity member without compiler-resolved property symbol
   evidence SHALL produce a typed gap rather than name attachment.
8. Inherited entities (CSDL `BaseType`), split mappings, multiple mapping
   fragments for one reconciled type, `IsTypeOf(...)` type names, conditional
   mappings (`Condition`), complex types (`ComplexProperty`), function imports
   and modification-function mappings, association mappings, provider
   extensions, and malformed or incomplete EDMX SHALL each fail closed with an
   existing or spec-defined gap classification and no composed edge for the
   affected chain.
9. Every fail-closed outcome SHALL be an `AnalysisGap` fact with a rule ID,
   classification, message, span, and `coverage=reduced`; none SHALL silently
   drop evidence.

### Requirement 4: Evidence Contract For Composed Relationships

**User story:** As a downstream consumer, I want every composed relationship to
carry the full evidence envelope, so that I can audit any edge to its sources.

Acceptance criteria:

1. Every composed relationship SHALL define direction (source = canonical CLR
  symbol, target = EDMX descriptor), kind, rule ID, evidence tier, file span,
  commit SHA, extractor ID and version, supporting fact IDs, coverage label,
  and limitations.
2. Composed CLR-to-conceptual relationships (entity and property) SHALL be
  capped at `Tier2Structural` because the target CSDL descriptor is Tier2.
  Their emitted tier SHALL be the weakest tier in the complete supporting
  chain. Syntax-only generated-link fallback SHALL NOT authorize a composed
  edge.
3. Composed end-to-end CLR-to-storage relationships (table and column) SHALL be
  capped at `Tier2Structural` because they transit Tier2 EDMX descriptor
  evidence.
4. Composition SHALL NOT upgrade any existing EDMX descriptor fact, generated
  link fact, or downstream classification beyond its existing tier ceiling;
  descriptor facts remain `Tier2Structural` under `legacy.data.edmx.v1`.
5. Supporting fact IDs SHALL reference the complete ordered chain of upstream
  facts (CLR declaration evidence, the selected namespace-bridge evidence,
  CSDL entity/property, CSDL entity set where applicable, MSL mapping, SSDL
  entity set or column) so every hop is traceable. Coverage SHALL be the weakest
  coverage label in that same chain; absent bridge evidence SHALL fail closed.
6. Every composed fact SHALL carry `namespaceBridgeFactId` and the closed-code
   `namespaceBridgeMechanism`; the bridge ID SHALL occur in
   `supportingFactIds`. When a Tier1 declaration fact also carries the bounded
   semantic attribute evidence, that fact MAY fill both roles without being
   duplicated in the ordered ID list.

### Requirement 5: Rule Decision

**User story:** As a catalog maintainer, I want a single documented rule
decision, so that rule ownership and tier ceilings are unambiguous.

Acceptance criteria:

1. The composition SHALL be owned by one new narrowly versioned rule,
   `legacy.data.edmx.symbol-composition.v1`, with a `rules/rule-catalog.yml`
   entry defining emitted fact types, evidence tiers, gap classifications,
  safe properties, and documented limitations.
2. The spec SHALL document the rejected alternative (extending
   `legacy.data.generated-link.v1` with a compiler-resolved link kind) and the
   reasons for rejection.
3. Existing rules `legacy.data.edmx.v1`, `legacy.data.generated-link.v1`,
   `legacy.data.model.identity.v1`, `legacy.data.model.relationship.v1`, and
   `legacy.data.model.surface.v1` SHALL keep their current ownership, emits,
   and tier ceilings.

### Requirement 6: Consumer Behavior

**User story:** As a path and reverse-impact user, I want the composed
relationships to flow through existing consumers without a new consumer, so
that contracts stay stable.

Acceptance criteria:

1. Composed facts SHALL serialize to `facts.ndjson` in the standard fact schema
   with canonical endpoint IDs preserved.
2. Composed facts SHALL persist to `index.sqlite` `symbol_relationships` rows
   (source symbol ID, target model key, relationship kind, rule, tier, span)
   with direction preserved as source-to-target and no reverse duplicate rows.
3. Combined indexes SHALL import composed relationships verbatim into
   `combined_symbol_relationships`, and the `combined_dependency_edges` view
   SHALL expose the relationship kind as the edge kind with direction
   unchanged.
4. Path reporting SHALL traverse composed edges with rule and tier evidence
   retained per hop.
5. Reverse-impact traversal SHALL traverse composed relationships upstream
   (storage descriptor toward CLR symbol toward code) under a new opt-in
   `mapping` relationship filter with existing default filters unchanged; the
   filter SHALL preserve the existing contract's direct/transitive
   distinction, per-hop evidence, deterministic cycle handling, and
   fail-closed selector behavior, and SHALL NOT add runtime claims.
6. Hops over composed edges SHALL retain `supportingFactIds` and the
   namespace bridge fact ID through an additive reverse-impact hop-contract
   extension (serialized in `tracemap.reverse-impact.v1`; consumers ignoring
   the new fields are unaffected), and the `mapping` filter SHALL
   deterministically expand a CLR entity type reached mid-traversal to its
   bounded contained members so table seeds can reach callers.
7. Reporting and release-review surfaces SHALL only consume composed facts
   where existing consumer contracts already apply; no new consumer SHALL be
   introduced to complete this spec.
8. Edge direction SHALL survive serialization, persistence, combination, and
   readback unchanged in every consumer.

### Requirement 7: Privacy And Safe Values

**User story:** As a security reviewer, I want the composition to leak nothing,
so that public artifacts stay safe.

Acceptance criteria:

1. The composition SHALL NOT store source snippets, connection strings,
   provider secrets, local paths, or private identifiers.
2. Display names on composed facts SHALL follow the existing legacy-data safe
   identifier policy: clear values only when safe, hash forms otherwise.
3. Namespace URIs, provider manifests, and provider details SHALL NOT be
   emitted by the composition; they remain hashed or omitted per existing
   EDMX behavior.

### Requirement 8: Determinism

**User story:** As a CI user, I want identical scans of the same commit to
produce identical composition output.

Acceptance criteria:

1. Repeated scans of the same repository and commit SHALL produce identical
   composed fact IDs, properties, spans, tiers, and supporting fact IDs.
2. Composition ordering SHALL be deterministic (document line order, then
   ordinal name order) and independent of file system enumeration order.
3. Gap emission SHALL be deterministic and deduplicated per classification,
   anchor span, and affected identity.

### Requirement 9: Synthetic Fixture Matrix

**User story:** As a maintainer, I want a fixture matrix that proves each
guarantee, so that regressions are caught at the assertion level.

Acceptance criteria:

1. Synthetic EF6 database-first fixtures (checked-in EDMX plus generated
   entity code plus a derived `DbContext` with `DbSet<T>`/`IDbSet<T>` stubs
   in the `System.Data.Entity` namespace) SHALL be test-local in the first
   implementation; a maintained `samples/` fixture SHALL require a separate
   public-proof and smoke-maintenance decision.
2. The matrix SHALL cover, at minimum: exact entity-to-table composition;
   exact property-to-column composition; `EntityTypeMapping/@TypeName`
   honored over `EntitySetMapping/@Name`; `StoreEntitySet` resolved through
   SSDL to the physical table; CLR namespace equal to CSDL namespace under
   the documented equality convention with the generated-file scope bridge
   fact recorded as the namespace-bridge evidence; CLR namespace
   intentionally different but bridged by explicit supported generated
   metadata; custom/generated namespace with no deterministic bridge
   producing a gap; same simple names across namespaces not colliding; same
   names across assemblies failing closed, including identical assembly
   name/version in distinct compilation scopes and duplicate distinct symbol
   IDs within one scope set; unrelated cross-directory, cross-project, and
   prefix-sibling designer files never entering composition scope; per-EDMX
   compiler unavailability in multi-project scans classifying as
   composition-unavailable rather than missing code; ambiguous joins
   producing explicit gaps; an SSDL decoy column with the same name on a different storage type not
   cross-wiring; split, inherited,
   conditional, `IsTypeOf`, complex, function, provider extension, and
   association shapes producing explicit composition-owned scope gaps and no
   edges; missing generated code remaining partial; missing compiler evidence
   failing closed; direction surviving
   serialization and persistence; reverse-impact and path traversal retaining
   member-level mapping evidence; deterministic output across repeated
   scans.
3. Tests SHALL assert identity, endpoints, provenance, spans, tiers, rule
   IDs, supporting fact IDs, gaps, and coverage labels — not only counts.

### Requirement 10: Documentation And Catalog Updates

**User story:** As a future contributor, I want docs to explain the composition
and its limits, so that I do not re-derive or over-trust it.

Acceptance criteria:

1. `rules/rule-catalog.yml` SHALL gain the new rule entry with limitations,
   including the static-design-time-only boundary, the no-runtime-claim rule,
   and the documented limitation that generated/custom CLR namespace
   reconciliation without explicit metadata gaps closed rather than guessing.
2. `docs/LANGUAGE_ADAPTER_CONTRACT.md` SHALL document the composed fact
   contract and tier ceilings.
3. `docs/VALIDATION.md` SHALL gain the focused validation commands and fixture
   expectations for this family.
4. Applicable spec implementation-state notes SHALL be updated when
   implementation lands.

### Requirement 11: Validation Gates

**User story:** As a releaser, I want pinned validation for this family, so
that changes are checked before merge.

Acceptance criteria:

1. Focused legacy-data, EF mapping, graph-correctness, combined-path, and
   reverse-impact test filters SHALL be documented and runnable.
2. `./scripts/check-private-paths.sh` and `git diff --check` SHALL pass.
3. Validation SHALL NOT require database connections, network access, or
   generated-code execution.
