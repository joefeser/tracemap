# Legacy EF6 EDMX Symbol Composition Implementation State

Status: implemented
Implementation branch: `codex/implement-ef6-edmx-symbol-composition`
Target base: `dev` — the specification merged via PR #696 at
`61e7062ea7e22a0c7d18da22cb77490cd162f9c0`. PR #696 was merged by the owner;
Joe does not use draft PRs, so the implementation PR also opens
ready-for-review and never as a draft.
Primary issue: #680
Public claim level: hidden pending review of the implementation PR

## Scope State

The complete bounded runway from the merged specification is implemented on
the branch above:

- rule constants, the closed vocabulary, the catalog entry, and the
  `legacy-data-composition/0.1.0` scanner version;
- EDMX parser additions (additive): `EntityTypeMapping/@TypeName` read and
  resolved, `MappingFragment/@StoreEntitySet` resolved through the SSDL
  container, deterministic `storageEntityTypeIdentity` on SSDL entity-set,
  SSDL column, and MSL mapping facts, the raw conceptual
  `EntitySet/@EntityType` reference on entity-set facts, and
  `ModificationFunctionMapping` added to the unsupported-shape gap scan;
- bounded semantic evidence: mechanism-1 `EdmEntityTypeAttribute`
  safe-or-hash conceptual identity properties on Tier1 `TypeDeclared` facts
  and bounded `PropertyDeclared` emission for EF/EDMX candidate types only;
- the composition stage: `LegacyDataGeneratedFileScope` bridge fact
  (same-directory exact-base rule only), the namespace ladder (mechanism 2
  contributes no matches pending proven reads), per-EDMX compiler
  availability, canonical-ID/compilation-scope uniqueness, the four composed
  relationship kinds at Tier2 with `namespaceBridgeFactId` provenance and
  weakest-link coverage, and the full fail-closed gap table including
  composition-owned association and provider scope gaps;
- persistence and consumers: unchanged schema ride-through
  (`symbol_relationships`, verbatim combined import,
  `combined_dependency_edges` with the four edge kinds preserved by
  `NormalizeEdgeKind`), and the opt-in reverse-impact `mapping` filter with
  additive hop fields (`SupportingFactIds`, `NamespaceBridgeFactId`) and
  deterministic bounded contained-member expansion for entity types reached
  mid-traversal; defaults unchanged, `database` untouched, reducer untouched;
- the F1-F18 test-local fixture matrix plus catalog and documentation updates
  (`docs/LANGUAGE_ADAPTER_CONTRACT.md`, `docs/VALIDATION.md`).

## Implementation PR Commits

1. Add EF6 EDMX symbol-composition rule constants and catalog entry.
2. Resolve EDMX TypeName and StoreEntitySet with storage type identity.
3. Add bounded semantic evidence for EDMX composition candidates.
4. Add the EF6 EDMX symbol-composition stage (with F1-F10, F13-F18 tests).
5. Add the opt-in mapping reverse-impact filter with hop provenance (F11-F12).
6. Documentation, catalog cross-references, spec bookkeeping, and this file.

## Implementation Decisions To Record

- Mechanism 2 (deterministic generation/project metadata) is intentionally
  unimplemented: no proven deterministic read exists today, so it contributes
  no matches and those shapes gap closed per the spec. Enumerating a proven
  source remains a deferred follow-up with its own task.
- The `NamespaceBridgeMechanism` closed code `generation-metadata` is
  catalogued and validated but currently unreachable.
- Compiler availability uses the scan's `semanticallyAnalyzedFiles` record
  (`ScanEngine.GetSemanticallyAnalyzedFiles`); uncovered scopes emit
  `ClrSymbolEvidenceUnavailable`, covered-but-declaration-free scopes emit
  `MissingGeneratedCode`, and scoped same-simple-name declarations without a
  qualified match or bridge emit `UnresolvedGeneratedNamespace`.
- The empty-result classification is deterministic per entity: the
  declaration-presence distinction fixes the earlier
  `UnresolvedGeneratedNamespace`/`MissingGeneratedCode` conflict.
- Provider-extension gaps are emitted for SSDL routine descriptors
  (`Function`/`FunctionImport` facts), the only deterministic provider-defined
  shape the EDMX extractor emits; function-import and modification-function
  mappings keep their existing `legacy.data.edmx.v1` gaps.
- Reverse-impact expansion applies only to CLR entity types reached through
  `mapping` edges, mirroring the seed expansion bounds and ordering; the
  hop-contract fields are additive and serialize in
  `tracemap.reverse-impact.v1` output.

## Validation

- `dotnet build src/dotnet/TraceMap.sln` — 0 errors.
- `dotnet test src/dotnet/TraceMap.sln` — Passed: 1604, Failed: 0, Skipped: 0
  (net10.0) after the final code commit.
- Focused suites: `LegacyDataEdmxSymbolCompositionTests` (17: F1-F18),
  `LegacyDataMetadataExtractorTests` (59), `LegacyDataModelRuleCatalogTests`
  (6), `CSharpSemanticExtractorTests`, `ReverseImpactTraversalTests`,
  `CombineTests`, `CliTests` — all green.
- Synthetic end-to-end CLI scan (scan, sqlite readback, combine,
  `combined_dependency_edges` inspection, reverse-impact `mapping` traversal)
  recorded in the implementation PR body.
- `./scripts/check-private-paths.sh` — clean; `git diff --check` — clean.

## Deferred Follow-Ups

- Mechanism 2 enumeration: proven deterministic generation/project metadata
  reads for namespace bridging.
- Reducer classification of composed edges (separate reducer decision).
- Association, function-import, and modification-function mapping composition.
- Freshness/consistency checks between EDMX and generated code.
- A maintained `samples/` or demo fixture (separate public-proof and
  smoke-maintenance decision).
- Extending the identity chain to DBML, typed DataSet, and NHibernate models.

## Specification History (PR #696, merged)

- Initial spec `eb7cde83`, owner correction batch `f72571ba` (namespace
  evidence ladder, resolved Q1/Q2/Q4), Luna xHigh review of `f72571ba`
  (two P1, three P2 findings, all accepted) patched as `81f23ee7`
  (bridge provenance, canonical-ID collision guard, SSDL storage-type
  identity, staged ordering, association gap contract), exact-head automated
  review fixes as `732d35e3` (satisfiable composition-owned
  `LegacyDataGeneratedFileScope` bridge after Codex P1/Qodo High) and
  `295d9bc1` (scope decoys, per-EDMX availability, classification
  distinction, hop provenance, mid-traversal expansion; fixtures F17/F18).
- Merged by the owner as `61e7062e` on `dev`. The stale draft-hold task 0.12
  from `4e4ceb2` was reverted on the implementation branch per owner
  direction; Joe never uses draft PRs.
