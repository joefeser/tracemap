# Legacy EF6 EDMX Symbol Composition Tasks

Part of #680. This PR is specification-only: Spec Authoring Tasks are
performed here; Implementation Tasks are intentionally unchecked and are the
runway for future implementation PRs. Do not close #680 with this PR.

## Spec Authoring Tasks

- [x] 0.1 Fetch `origin/dev` and work from a clean isolated worktree based on
      fresh `origin/dev` (base `1b79f4e62f9d544e120197d95b2d099e0300be1d`).
- [x] 0.2 Read issue #680 completely and confirm no active spec or PR already
      owns it.
- [x] 0.3 Inspect the shipped EF6 semantic extraction, canonical symbol
      identity, EDMX CSDL/SSDL/MSL parsing, generated-code linkage, rule
      catalog entries, persistence, and reverse-impact contracts.
- [x] 0.4 Draft `requirements.md`, `design.md`, `tasks.md`,
      `review-prompts.md`, and `implementation-state.md`.
- [x] 0.5 Record the rule decision, tier decision, consumer/persistence
      decisions, and rejected alternatives with repository evidence.
- [x] 0.6 Run spec delivery validation: `git diff --check`,
      `./scripts/check-private-paths.sh`, and a diff-scope check confirming
      changes are limited to this spec folder.
- [x] 0.7 Run focused existing EF/EDMX/legacy-data tests to verify
      specification premises, and record results in
      `implementation-state.md`.
- [x] 0.8 Commit, push, and open a draft PR to `dev` marked "Part of #680"
      (not "Closes"); do not tag reviewers; do not run ACK; do not merge.
- [ ] 0.9 Run Kiro spec reviews (`claude-opus-4.8`, `claude-sonnet-4.6`) via
      `scripts/kiro-review.mjs`, or leave to the owner; record the outcome in
      `implementation-state.md`. (Deferred until owner corrections are
      committed and re-reviewed; see Q3.)
- [x] 0.10 Apply the owner's specification review corrections: the namespace
      evidence ladder (D4/D4.1), bounded semantic property emission (D5),
      resolved Q1/Q2/Q4 decisions, and editorial/contract cleanup.

## Implementation Tasks

- [ ] 1. Add the rule catalog entry and constants.
      Requirements: 5, 10.
  - [ ] 1.1 Add `RuleIds.LegacyDataEdmxSymbolComposition` and the four
        relationship kind constants; assert them in the legacy-data rule
        catalog tests.
  - [ ] 1.2 Add the `legacy.data.edmx.symbol-composition.v1` entry to
        `rules/rule-catalog.yml` with emits, tiers, gap classifications
        (new: `AmbiguousClrSymbolReconciliation`,
        `ClrSymbolEvidenceUnavailable`, `UnresolvedGeneratedNamespace`,
        `MissingSemanticPropertyEvidence`; reused:
        `AmbiguousLegacyDataModelIdentity`,
        `UnsupportedLegacyOrmMappingShape`, `MissingGeneratedCode`,
        `MalformedLegacyDataMetadata`,
        `UnsupportedLegacyDataMetadataVersion` — with explicit ownership
        notes), safe properties, and limitations (static design time only;
        no runtime claims; no global short-name matching; descriptor
        ceilings not upgraded; generated/custom namespaces without a
        deterministic bridge gap closed).
  - [ ] 1.3 Add the `legacy-data-composition/0.1.0` scanner version constant.

- [ ] 2. Extend EDMX parsing with canonical resolution inputs.
      Requirements: 2, 3.
  - [ ] 2.1 Read `EntityTypeMapping/@TypeName` in `AddEdmxMappings` (existing
        extractor; no second parser), preserve `IsTypeOf` detection, and
        surface the resolved conceptual type on the mapping facts.
  - [ ] 2.2 Resolve `MappingFragment/@StoreEntitySet` through the SSDL
        storage container and carry the resolved SSDL entity set identity
        (stable model key) and table descriptor on the mapping facts.
  - [ ] 2.3 Keep existing descriptor facts, tiers, and gap behavior unchanged
        apart from additive properties.

- [ ] 3. Build the composition stage.
      Requirements: 1, 2, 3, 4.
  - [ ] 3.1 Add bounded semantic property-symbol emission during the existing
        C# semantic pass for entity types proven eligible for EF/EDMX
        composition (`DbSet<T>`/`IDbSet<T>` entity arguments, supported
        generated conceptual identity attributes, or designer/generated-file
        scoping); canonical member symbol IDs, containing type identity,
        assembly identity, source span, and compiler provenance. No global
        property inventory.
  - [ ] 3.2 Implement the namespace evidence ladder exactly as designed
        (D4/D4.1): mechanism 1 bounded semantic attribute read
        (`EdmEntityTypeAttribute` family — an explicit bounded extractor
        addition), mechanism 2 only with enumerated, proven deterministic
        generation/project metadata reads, mechanism 3 scoped qualified-name
        equality convention, mechanism 4 typed gap. Single-assembly
        uniqueness, exact member lookup, and entity-set/type/fragment/
        store-set/scalar resolution per D4.
  - [ ] 3.3 Emit `MapsToConceptualEntity`, `MapsToConceptualProperty` at
        Tier1 and `MapsToStorageTable`, `MapsToStorageColumn` at Tier2 with
        the full evidence envelope and complete ordered supporting fact
        chains.
  - [ ] 3.4 Emit fail-closed gaps for every D9 table row; no edge may be
        emitted from syntax-only or ambiguous joins; missing semantic
        property evidence is a typed gap, never name attachment.
  - [ ] 3.5 Only if a compilation-backed composition seam proves unavoidable:
        add it as an explicit separate task with documented lifecycle, memory
        bounds, determinism, and cancellation requirements. No such seam
        exists today and none is implied.

- [ ] 4. Wire persistence and consumers.
      Requirements: 6, 8.
  - [ ] 4.1 Verify `symbol_relationships` rows, combined import, and
        `combined_dependency_edges` rows with direction and kind preserved;
        extend only if a gap is proven.
  - [ ] 4.2 Ensure `NormalizeEdgeKind` passes the four new kinds through
        unchanged in path graphs.
  - [ ] 4.3 Add the opt-in `mapping` reverse-impact filter (resolved Q1) and
        traverse the four kinds upstream with hop provenance, preserving
        direct/transitive distinction, per-hop evidence, deterministic cycle
        handling, and fail-closed selectors; default filters unchanged and
        `database` untouched.
  - [ ] 4.4 Confirm no reducer allowlist changes and no new consumer.

- [ ] 5. Add the fixture matrix and tests.
      Requirements: 9.
  - [ ] 5.1 Create test-local synthetic EF6 database-first fixtures (EDMX +
        generated entities + `DbContext` with `DbSet<T>`/`IDbSet<T>` stubs
        in `System.Data.Entity`); no maintained `samples/` fixture in the
        first implementation (resolved Q4).
  - [ ] 5.2 Implement cases F1–F15 asserting identity, endpoints, provenance,
        spans, tiers, rule IDs, supporting fact IDs, gaps, and coverage.
  - [ ] 5.3 Add persistence round-trip, reverse-impact, path, and determinism
        tests.

- [ ] 6. Update documentation and validation.
      Requirements: 10, 11.
  - [ ] 6.1 Update `docs/LANGUAGE_ADAPTER_CONTRACT.md` with the composed fact
        contract and tier ceilings.
  - [ ] 6.2 Add focused validation filters and fixture expectations to
        `docs/VALIDATION.md`.
  - [ ] 6.3 Run the pinned checks: build, full .NET suite, focused legacy
        data/EF/graph/combined-path/reverse-impact filters,
        `./scripts/check-private-paths.sh`, `git diff --check`.
  - [ ] 6.4 Update this file and `implementation-state.md` as implementation
        lands.

## Definition Of Done (implementation PRs)

- The four composed kinds flow from scan to combined consumers with
  direction, tiers, spans, rule IDs, and supporting fact IDs intact.
- Every fail-closed case in the D9 table has a fixture proving no edge and an
  explicit gap.
- No existing descriptor fact, tier, or gap changed semantics.
- Rule catalog, adapter contract, and validation docs updated.
- `dotnet test` passes and the pinned checks are green.

## Deferred Follow-Ups

- Reducer classification of composed edges (separate reducer decision).
- Association, function-import, and modification-function mapping composition.
- Freshness/consistency checks between EDMX and generated code.
- Mechanism 2 enumeration: proven deterministic generation/project metadata
  reads for namespace bridging (bounded extractor addition when justified).
- A maintained `samples/` or demo fixture (separate public-proof and
  smoke-maintenance decision).
- Extending the identity chain to DBML, typed DataSet, and NHibernate models.
- Kiro model reviews of this spec (owner-run; prompts in
  `review-prompts.md`).
