# Legacy EF6 EDMX Symbol Composition Implementation State

Status: spec-ready
Spec branch: `codex/spec-ef6-clr-edmx-composition`
Target base: `dev`
Base SHA: `1b79f4e62f9d544e120197d95b2d099e0300be1d` (fresh `origin/dev` at
spec start; worktree `../tracemap-spec-680`)
Primary issue: #680 (Part of; spec-only PR, issue stays open)
Public claim level: hidden until implemented and reviewed

## Scope State

Specification delivered; implementation explicitly deferred to future PRs
against this spec. Nothing in this PR implements product code, changes
extractors, rules, docs outside this folder, or closes #680.

## Inspected Files And Contracts (spec premises)

EF6 CLR side:

- `src/dotnet/TraceMap.Core/CSharpSemanticExtractor.cs` — `AddDbContextFacts`
  (`DbContextDeclared`/`DbSetDeclared` under `database.ef.v1`, Tier1, with the
  canonical `entityTypeSymbolId` block), `DerivesFromDbContext`
  (`System.Data.Entity.DbContext`), `IsDbSetType` (EF6 `DbSet<T>`/
  `IDbSet<T>`), `AddTypeDeclarationFacts` (Tier1 `TypeDeclared` with full
  symbol block), `CreateSymbolRelationshipFact` (property contract mirrored by
  the design).
- `src/dotnet/TraceMap.Core/CSharpSymbolIdentityProvider.cs` — canonical ID
  formats and `AssemblyKey` (`name@version`).
- `src/dotnet/TraceMap.Core/CSharpSyntaxExtractor.cs` — syntax
  `TypeDeclared`/`PropertyDeclared` carry no canonical identity (ineligible
  for composition).

EDMX side:

- `src/dotnet/TraceMap.Core/LegacyDataMetadataExtractor.cs` — `ExtractEdmx`,
  `AddEdmxMappings` (confirmed `EntityTypeMapping/@TypeName` is never read;
  `StoreEntitySet` never resolved through SSDL; single-fragment gate),
  `AddGeneratedCodeLinks` (filename/type-name/syntax linkage under
  `legacy.data.generated-link.v1`), gap classifications
  (`UnsupportedLegacyOrmMappingShape`, `AmbiguousLegacyDataModelIdentity`,
  `MissingGeneratedCode`, `AmbiguousGeneratedCodeLink`, malformed/security).
- `src/dotnet/TraceMap.Core/LegacyDataModelIdentity.cs` — `stableModelKey`
  (`ldm:` format), coverage label vocabulary.
- `src/dotnet/TraceMap.Core/LegacyDataSafeValues.cs` — safe identifier policy.

Persistence and consumers:

- `src/dotnet/TraceMap.Core/Models.cs` — `CodeFact`, `SymbolRelationship`,
  LegacyData fact types, `RuleIds`, `ScannerVersions`.
- `src/dotnet/TraceMap.Storage/SqliteIndexWriter.cs` —
  `InsertSymbolRelationship` (requires `relationshipKind` +
  `sourceSymbolId`/`targetSymbolId` properties), symbol role generalization
  for `{source|target|...}SymbolId` blocks.
- `src/dotnet/TraceMap.Storage/JsonlFactWriter.cs`, `ReverseImpactArtifactReader.cs`
  — ndjson schema and reverse-impact artifact contract.
- `src/dotnet/TraceMap.Combine/CombinedIndexBuilder.cs` —
  `combined_symbol_relationships` import and `combined_dependency_edges` view
  (left-join coalesce confirmed: descriptor endpoints survive without
  registered symbol rows).
- `src/dotnet/TraceMap.Core/ReverseImpactTraversal.cs` — closed filter set
  (`calls|database|http|inheritance|references`), `ImpactRelationshipKinds`,
  hop direction pair (`SourceToTarget`/`TargetToSource`), Tier1 canonical
  boundary gating for `database`/`http`.
- `src/dotnet/TraceMap.Reporting/CombinedDependencyPaths.cs` —
  `LegacyDataAttachmentSymbols` display-name attachment (the weakness this
  spec replaces for EF6), edge kind normalization, path graph construction.

Catalog and docs:

- `rules/rule-catalog.yml` — `legacy.data.edmx.v1`, `legacy.data.generated-link.v1`
  (including "future compiler-resolved semantic links require a documented
  implementation update"), `legacy.data.model.identity.v1`,
  `legacy.data.model.relationship.v1`, `legacy.data.model.surface.v1`.
- `docs/LANGUAGE_ADAPTER_CONTRACT.md`, `docs/ACCEPTANCE.md`, `docs/VALIDATION.md`
  (legacy data smoke matrix; no EF6-specific pinned smoke exists today).
- Neighbor specs: `legacy-data-model-metadata-extraction`,
  `legacy-data-model-orm-mapping-completion`,
  `legacy-data-model-relationship-completion`, `ef-core-mapping-v0`.
- Existing fixtures: `samples/legacy-data-relationship-edmx/Relationships.edmx`
  (associations-only); no EF6 fixture exists anywhere (`System.Data.Entity`
  occurs only in the semantic extractor).

Ownership check: no open PR references #680 and no spec under `.kiro/specs/`
covers EF6 CLR-to-EDMX composition.

## Rule Decision

New narrowly versioned rule: `legacy.data.edmx.symbol-composition.v1`.

- Rejected: extending `legacy.data.generated-link.v1` — wrong contract shape
  (descriptor-to-file linkage, Tier2/Tier3 model, active consumers), and its
  catalog entry calls for a documented implementation update rather than
  in-place mutation.
- Rejected: `legacy.data.model.generated-link.v1` — reserved/in use for
  NHibernate model-normalized syntax links.
- Evidence: catalog entries and contract docs listed above; see `design.md`
  D1.

## Evidence-Tier Decision

- `MapsToConceptualEntity` / `MapsToConceptualProperty`: Tier1Semantic only,
  never emitted from syntax-only evidence (gaps instead).
- `MapsToStorageTable` / `MapsToStorageColumn`: capped at Tier2Structural
  (weakest-link cap over Tier2 MSL/SSDL descriptors).
- Gaps: Tier4Unknown.
- EDMX descriptor facts, generated-link facts, and downstream classifications
  are never upgraded; descriptor ceiling stays Tier2Structural.

## Consumer And Persistence Decisions

- Composed facts are `SymbolRelationship` facts with canonical endpoint IDs
  (source = `csharp ...` symbol ID; target = `ldm:` stable model key plus an
  `edmx`-language target role block), riding the existing persistence path
  with no schema changes: ndjson, `symbol_relationships`, verbatim combined
  import, `combined_dependency_edges` (kind = relationship kind, direction
  preserved, no reverse rows).
- Path graphs consume them through the existing view; `NormalizeEdgeKind`
  must pass the four new kinds through unchanged.
- Reverse impact: recommended opt-in `mapping` filter (additive to the closed
  set; defaults unchanged); rejected reusing `database` to avoid diluting its
  Tier1 runtime-operation semantics. See Q1.
- Reducer untouched; reporting/release-review only through existing
  consumers; no new consumer invented.

## Unresolved Owner Questions

- Q1: Reverse-impact filter for composed edges — accept the recommended new
  opt-in `mapping` filter, or reuse `database`, or exclude reverse-impact
  traversal from the first implementation PR?
- Q2: Mechanism for the bounded CLR property inventory (post-pass lookup
  against the Roslyn compilation vs bounded semantic emission) — contract is
  fixed by D5, mechanism left to the implementation PR.
- Q3: Kiro model reviews (`claude-opus-4.8`, `claude-sonnet-4.6`) of this spec
  were not run in this draft (dispatch validation list did not include them
  and review tooling is owner-gated); prompts are ready in
  `review-prompts.md`. Owner to run or waive.
- Q4: Fixture home — `samples/ef6-edmx-composition/` as designed, or
  test-local fixtures only, given `samples/` participates in smoke catalogs.

## Validation Completed (spec PR)

- `git fetch origin`; `origin/dev` verified at
  `1b79f4e62f9d544e120197d95b2d099e0300be1d`; clean isolated worktree from
  that SHA; diff scope limited to this spec folder.
- `git diff --check` — clean.
- `./scripts/check-private-paths.sh` — clean.
- Focused existing EF/EDMX/legacy-data tests run to verify specification
  premises (see tasks 0.7):
  `dotnet test src/dotnet/TraceMap.sln --filter "FullyQualifiedName~LegacyDataMetadataExtractorTests|FullyQualifiedName~LegacyDataModelRuleCatalogTests|FullyQualifiedName~CSharpSemanticExtractorTests"`
  — Passed: 69, Failed: 0, Skipped: 0 (net10.0).
- No markdown lint tooling exists in the repository (checked for
  `.markdownlint*`, `.prettierrc*`, `.editorconfig`, and CI workflows);
  hand-formatting follows the neighboring spec style (hard-wrapped ~78–80
  columns).

## Explicitly Deferred

- All implementation (tasks 1–6): EDMX parsing additions, composition stage,
  persistence/consumer wiring, fixtures, tests, docs, and catalog entry
  creation.
- Issue #680 remains open; this PR is "Part of #680" only.
