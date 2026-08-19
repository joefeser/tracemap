# Legacy EF6 EDMX Symbol Composition Implementation State

Status: spec-ready
Spec branch: `codex/spec-ef6-clr-edmx-composition`
Target base: `dev`
Base SHA: `1b79f4e62f9d544e120197d95b2d099e0300be1d` (fresh `origin/dev` at
spec start; worktree `../tracemap-spec-680`)
Primary issue: #680 (Part of; spec-only PR, issue stays open)
Public claim level: hidden until implemented and reviewed
Owner review: corrections applied on top of the initial spec commit
(`eb7cde831c414d08ee39c6c4e4a3550089a416a3`); owner-selected Luna xHigh
reviewed exact head `f72571ba7010452ce55ea110f420797f6960d087` read-only
(two P1, three P2 findings — all accepted as valid specification defects)
and its patch landed as one spec-only batch
(`81f23ee773d8ecbaec512525fc7f39dc487aa3e0`, authored by Luna xHigh).
Exact-head automated review findings on that batch (Codex P1 unsatisfiable
Tier2 EDMX bridge; Qodo High generated-link identity) were patched next
(`732d35e3db84ebf2f91a69e8f90b1720560366e2`); see Review Outcome. Owner
directive: the PR stays in draft until the final review after focused
validation.

## Scope State

Specification delivered; owner and Luna review corrections applied (namespace
evidence ladder and provenance, collision guard, deterministic SSDL storage
type identity, staged bounded property-symbol direction, association scope gap,
resolved Q1/Q2/Q4, editorial cleanup), followed by exact-head automated review
fixes (composition-owned `LegacyDataGeneratedFileScope` bridge). All five
Luna findings re-verified intact at the current head. The PR is held in draft
per owner direction; one final review is pending before it leaves draft.
Implementation is explicitly deferred to future PRs against this spec.
Nothing in this PR implements product code, changes extractors, rules, docs
outside this folder, or closes #680.

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
- `src/dotnet/TraceMap.Core/CSharpSemanticExtractor.cs` attribute-argument
  helpers (`GetAttributeStringArgument`, `GetAttributeConstantStringArgument`)
  — existing precedent for the bounded mechanism-1 attribute read; no
  EF generated-type attribute (`EdmEntityTypeAttribute` family) is read
  anywhere today.
- `src/dotnet/TraceMap.Core/ScanEngine.cs:791-793` — semantic facts
  materialize before `LegacyDataMetadataExtractor.Extract`, so composition
  eligibility cannot be keyed on EDMX descriptor facts (verified for D5).

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

- `MapsToConceptualEntity` / `MapsToConceptualProperty`: capped at
  Tier2Structural and emitted at the weakest supporting tier because the target
  CSDL descriptor remains Tier2. A Tier1 semantic attribute bridge does not
  upgrade that descriptor; proven generation metadata may reduce the edge
  further; the scoped equality convention is Tier2 through its
  `LegacyDataGeneratedFileScope` bridge fact. Tier3 syntax fallback is
  ineligible.
- `MapsToStorageTable` / `MapsToStorageColumn`: capped at Tier2Structural
  (weakest-link cap over Tier2 MSL/SSDL descriptors).
- Gaps: Tier4Unknown.
- EDMX descriptor facts, generated-link facts, and downstream classifications
  are never upgraded; descriptor ceiling stays Tier2Structural.
- Documented limitation: generated/custom CLR namespaces without a
  deterministic ladder bridge gap closed (`UnresolvedGeneratedNamespace`);
  the composition does not recover them by name similarity.

## Namespace Reconciliation Ladder (owner review correction)

Exact qualified-name equality is a documented convention, not the general
rule, because generated CLR namespaces may differ from the CSDL namespace
(T4 generation, custom-tool namespace configuration, generation style). The
controlling ladder (design D4/D4.1), tried in order:

1. Explicit compiler-resolved EF generated-type metadata exposing conceptual
   namespace/type identity (the
   `System.Data.Entity.Core.Objects.DataClasses.EdmEntityTypeAttribute`
   family with `NamespaceName`/`Name`, or equivalent compiler-visible
   identity) — requires a bounded semantic attribute read; not implemented
   today.
2. Deterministic checked-in generation/project metadata proving the
   generated CLR namespace/type relationship — no read exists today; the
   implementation must enumerate and prove exact sources before this
   mechanism ever matches.
3. Exact qualified-name equality, only as a documented supported convention
   over Tier1 declarations in files scoped to that EDMX by the
   composition-owned `LegacyDataGeneratedFileScope` bridge fact (D2; the
   deterministic designer-file convention, file-level scoping only). Neither
   that scope fact nor any `legacy.data.generated-link.v1` fact (any tier)
   authorizes CLR identity; EDMX links are always the Tier3
   `type-name-syntax-fallback` today because EDMX descriptors never carry
   `generatedCodeFileName`.
4. Otherwise: reduced-coverage gap `UnresolvedGeneratedNamespace`, no edge.

Never global simple-name matching; display labels are never identity. The
selected bridge fact is required in `supportingFactIds` and determines
weakest-link tier/coverage. Duplicate qualified types across assemblies fail
closed under every mechanism; distinct scan-relative project/compilation
scopes sharing a canonical ID also fail closed, covering identical assembly
name/version. Fixture coverage: namespace parity and bridge provenance (F1),
attribute bridge with divergent namespace (F14), no-bridge gap (F15), simple
names across namespaces (F5), same-ID assembly collisions (F6).

## Resolved Owner Decisions (Review Corrections)

- Q1 resolved: reverse impact gains a new opt-in `mapping` filter; defaults
  unchanged; the existing `database` filter is not reused. Existing
  `DatabaseOperationCandidate` edges are deterministic static compiler
  evidence of database operation call patterns — not runtime proof. The
  filter preserves direct/transitive distinction, per-hop evidence,
  deterministic cycles, and fail-closed selectors. No reducer or runtime
  claims.
- Q2 resolved: bounded semantic property-symbol emission during the existing
  C# semantic pass for types proven eligible or inventory-visible as generated
  file candidates (DbSet/IDbSet entity arguments, supported generated identity
  attributes, or the bounded generated/designer file-shape convention). After
  the metadata extractor runs, composition intersects file-shape candidates
  with the EDMX's `LegacyDataGeneratedFileScope` bridge fact (file scoping
  only; generated-link facts are corroboration, never identity). No global
  property inventory and no
  implied compilation-backed post-pass seam (any such seam is an explicit task
  with lifecycle, memory, determinism, and cancellation requirements). Missing
  semantic property evidence is a typed gap
  (`MissingSemanticPropertyEvidence`), never name attachment.
- Q4 resolved: test-local synthetic fixtures for the first implementation; no
  maintained `samples/` fixture; a future sample/demo fixture requires a
  separate public-proof and smoke-maintenance decision.

## Consumer And Persistence Decisions

- Composed facts are `SymbolRelationship` facts with canonical endpoint IDs
  (source = `csharp ...` symbol ID; target = `ldm:` stable model key plus an
  `edmx`-language target role block), riding the existing persistence path
  with no schema changes: ndjson, `symbol_relationships`, verbatim combined
  import, `combined_dependency_edges` (kind = relationship kind, direction
  preserved, no reverse rows).
- Path graphs consume them through the existing view; `NormalizeEdgeKind`
  must pass the four new kinds through unchanged.
- Reverse impact: opt-in `mapping` filter per the resolved Q1 above.
- Reducer untouched; reporting/release-review only through existing
  consumers; no new consumer invented.

## Review Outcome

- Q3 resolved by owner direction: one fresh read-only Luna xHigh review ran
  against exact head `f72571ba7010452ce55ea110f420797f6960d087` (no files or
  GitHub state changed during that review).
- Accepted findings: bridge provenance/tier/coverage (P1); same-name/version
  assembly collision (P1); deterministic SSDL storage-type identity (P2);
  semantic-pass/generated-link ordering (P2); association gap contract (P2).
  The owner confirmed all five are valid specification defects, not review
  noise; Luna xHigh corrected them together as the spec-only batch
  `81f23ee773d8ecbaec512525fc7f39dc487aa3e0`. All five corrections were
  re-verified intact at head `732d35e3db84ebf2f91a69e8f90b1720560366e2`
  after the automated-review fixes.
- Exact-head automated review follow-up on `81f23ee773d8ecbaec512525fc7f39dc487aa3e0`:
  - Codex P1 (unsatisfiable bridge): the Luna batch required mechanism 3 to
    present an `explicit-generated-file` Tier2 generated-link, but
    `AddGeneratedCodeLinks` emits that linkKind only when the descriptor
    carries `generatedCodeFileName`, and `ExtractEdmx` never sets it (only
    DBML's `AddGeneratedHints` does) — so EDMX links are always the Tier3
    `type-name-syntax-fallback` and the required bridge could never exist.
    Verified against `LegacyDataMetadataExtractor.cs:1383-1448`/`:2395-2400`
    and the full `ExtractEdmx` body.
  - Qodo High (no CLR identity in the link): the generated-link contract
    persists only metadata fact ID, expected type name, generated filename,
    and link kind — no canonical symbol ID, namespace, assembly identity, or
    compilation scope — so it cannot authorize candidate-to-symbol identity
    at any tier.
  - Resolution: mechanism 3's bridge is now the composition-owned
    `LegacyDataGeneratedFileScope` fact (Tier2, persisted, auditable via
    `namespaceBridgeFactId`/`namespaceBridgeMechanism=generated-file-scope`),
    computing file-level scope with the exact shipped designer-file
    convention. It carries no CLR identity; identity is proven solely by
    Tier1 declarations in scoped files (qualified equality, canonical-ID
    dedup, compilation-scope guard, single-assembly uniqueness). Generated-link
    facts of any tier are corroboration only. Fixture F16 added for
    scoped-candidate duplicate/collision fail-closed behavior; F1 asserts the
    bridge provenance and candidate-to-symbol identity.
- Kiro prompts remain available in `review-prompts.md` for an optional later
  advisory review. No Kiro, ACK, or hosted reviewer loop was run by the spec
  author for this correction pass; the Codex/Qodo findings above were posted
  on the PR by repository automation and were addressed spec-only.

## Validation Completed (spec PR)

- `git fetch origin`; `origin/dev` verified at
  `1b79f4e62f9d544e120197d95b2d099e0300be1d`; clean isolated worktree from
  that SHA; diff scope limited to this spec folder.
- `git diff --check` — clean (initial spec commit and correction commit).
- `./scripts/check-private-paths.sh` — clean (both commits).
- Focused existing EF/EDMX/legacy-data tests run to verify specification
  premises (see tasks 0.7):
  `dotnet test src/dotnet/TraceMap.sln --filter "FullyQualifiedName~LegacyDataMetadataExtractorTests|FullyQualifiedName~LegacyDataModelRuleCatalogTests|FullyQualifiedName~CSharpSemanticExtractorTests"`
  — Passed: 69, Failed: 0, Skipped: 0 (net10.0).
- Correction-pass premise checks (no product code changed): confirmed no EF
  generated-type attribute read exists today
  (attribute-argument helpers are the precedent), and confirmed the
  semantic-before-legacy-data extraction ordering at
  `src/dotnet/TraceMap.Core/ScanEngine.cs:791-793`.
- Post-Luna correction validation reran the focused existing
  EF/EDMX/legacy-data filter above: Passed 69, Failed 0, Skipped 0. Private-path
  guard, spec-only diff-scope check, and `git diff --check` also passed.
- Automated-review-fix validation (this pass): no product code changed; the
  new premises were verified directly against source (EDMX never emits
  `explicit-generated-file`; the generated-link property set). `git diff
  --check`, `./scripts/check-private-paths.sh`, and the spec-only diff-scope
  check rerun clean.
- Draft-hold validation (owner directive after Luna attribution): reran the
  focused filter extended with reverse-impact traversal —
  `LegacyDataMetadataExtractorTests | LegacyDataModelRuleCatalogTests |
  CSharpSemanticExtractorTests | ReverseImpactTraversalTests` —
  Passed: 110, Failed: 0, Skipped: 0 (net10.0); `git diff --check`,
  private-path guard, and spec-only diff scope clean.
- No markdown lint tooling exists in the repository (checked for
  `.markdownlint*`, `.prettierrc*`, `.editorconfig`, and CI workflows);
  hand-formatting follows the neighboring spec style (hard-wrapped ~78–80
  columns).

## Explicitly Deferred

- All implementation (tasks 1–6): EDMX parsing additions, bounded semantic
  property emission, namespace ladder mechanisms 1–2 reads, composition
  stage, persistence/consumer wiring, test-local fixtures, tests, docs, and
  catalog entry creation.
- Issue #680 remains open; this PR is "Part of #680" only.
- PR stays in draft per owner directive until one final review follows the
  focused validation; the owner-selected Luna xHigh review and its accepted
  corrections are complete, as are the exact-head automated review fixes.
