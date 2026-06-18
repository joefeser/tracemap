# Legacy Data Model Metadata Extraction Tasks

## Spec Authoring Tasks

- [x] 0.1 Inspect existing legacy data metadata, flow, combined report, vault,
      acceptance, validation, and rule catalog context.
- [x] 0.2 Draft requirements, design, tasks, and implementation-state files for
      the new `legacy-data-model-metadata-extraction` spec, including initial
      branch, scope, and validation notes in `implementation-state.md`.
- [x] 0.3 Run Kiro Opus and Sonnet spec reviews when locally available, with a
      10 minute timeout per review.
- [x] 0.4 Patch Medium+ actionable review findings, with at most two re-review
      cycles.
- [x] 0.5 Run spec delivery validation: `git diff --check`, private path guard
      if available, and any spec/docs validation scripts that exist.

## Implementation Tasks

- [x] 1. Add rule catalog entries and model constants. Requirements: 1, 2, 3, 4, 5, 6, 7, 8.
  - [x] Treat rule catalog ownership as the gate for implementation tasks 2-9;
        no extractor or reporter code should emit derived conclusions until
        emitted facts/rows, tiers, safe properties, and limitations are
        documented.
  - [x] Add `legacy.data.model.identity.v1` as a derived identity/projection
        semantics rule over existing source facts, not as a second emitter for
        existing DBML/EDMX/typed DataSet facts.
  - [x] Add `legacy.data.model.relationship.v1` as a derived relationship
        semantics rule over `LegacyDataMappingDeclared` source facts while
        preserving existing source `mappingKind` values such as `association`
        and `relation`.
  - [x] Add `legacy.data.orm.nhibernate.v1`.
  - [x] Add `legacy.data.orm.unsupported.v1`.
  - [x] Add `legacy.data.model.surface.v1` for derived `legacy-data`
        report/export rows and availability gaps, not scan-time projection
        facts.
  - [x] Decide whether `legacy.data.model.generated-link.v1` is needed or
        whether `legacy.data.generated-link.v1` remains sufficient.
  - [x] Do not add `LegacyDataModelSurfaceProjected` in MVP; derived surface rows
        must not be re-consumed by prefix-based legacy data fact readers.
  - [x] Document emitted fact types, evidence tiers, safe properties, and
        limitations in `rules/rule-catalog.yml`.

- [x] 2. Implement normalized legacy data model identity. Requirements: 1, 2, 7, 8.
  - [x] Build a deterministic model identity helper over safe metadata kind,
        descriptor role, file path, metadata scope, safe names, and hashes.
  - [x] Normalize DBML entity/table/column/association/routine descriptors.
  - [x] Normalize EDMX conceptual/storage/property/association/function mapping
        descriptors.
  - [x] Normalize typed DataSet DataSet/DataTable/DataColumn/relation/adapter
        descriptors while preserving unrelated `.xsd` gating.
  - [x] Preserve descriptor tier ceilings and avoid upgrading metadata by
        generated-code linkage.
  - [x] Include `metadataFormat` in stable keys so DBML, EDMX, typed DataSet,
        and NHibernate descriptors with the same display name remain distinct.
  - [x] Add deterministic ordering, stable-key, duplicate-identity, and privacy
        tests.

- [ ] 3. Add relationship extraction and ambiguity gaps. Requirements: 2, 5, 8.
  - [ ] Emit deterministic relationship evidence for DBML associations.
  - [ ] Emit deterministic relationship evidence for EDMX associations and
        unambiguous MSL relationship mapping where supported.
  - [ ] Emit deterministic relationship evidence for typed DataSet relations and
        constraints.
  - [ ] Represent relationships as `LegacyDataMappingDeclared` while preserving
        existing source `mappingKind` values and adding
        `modelRelationshipKind = relationship` or an equivalent derived surface
        field when deterministic.
  - [ ] Emit unidirectional relationship evidence with a limitation when only
        one endpoint is deterministic.
  - [ ] Emit needs-review or analysis-gap evidence for ambiguous, duplicate,
        inherited, split, conditional, many-to-many, or unsupported shapes.
  - [ ] Add exact gap classification tests, including relationship ambiguity and
        selector downgrade behavior.

- [ ] 4. Add NHibernate mapping XML MVP. Requirements: 1, 3, 7, 8.
  - [ ] Safely parse checked-in `.hbm.xml` files with DTD/entity resolution
        disabled.
  - [ ] Reuse the same parser helper and bounds as
        `LegacyDataMetadataExtractor`, currently `SafeXml`: 2 MiB XML file size,
        4 MiB maximum characters in document, 100,000 descendant nodes, and
        depth 128.
  - [ ] Inventory mapping documents and emit parser gaps for malformed or unsafe
        XML.
  - [ ] Extract class, id, version, property, component, collection,
        many-to-one, one-to-one, many-to-many, key, table, and column descriptors
        where deterministic.
  - [ ] Hash or omit schema, catalog, formula, filter, SQL, query, dialect,
        connection, and provider-specific unsafe values.
  - [ ] Cap per-class property/column-like descriptor emission at 500 rows and
        relationship/collection descriptor emission at 200 rows, then emit a
        deterministic too-large/truncation gap for skipped descriptors.
  - [ ] Emit unsupported-shape gaps for inheritance, joined/union subclass,
        composite id, dynamic component, custom SQL, filters, named queries, and
        provider extensions unless implemented explicitly.
  - [ ] Add fixture tests for simple mappings, relationships, formula/filter
        redaction, unsafe values, parser safety, reduced coverage, and
        deterministic output.
  - [ ] Add parser-bound and classification tests proving NHibernate uses the
        same safety/malformed/too-large gap strings as the existing legacy data
        family.

- [ ] 5. Add unsupported old ORM descriptor gaps. Requirements: 1, 3, 8.
  - [ ] Recognize public-safe indicators for unsupported old ORM descriptor
        families such as LLBLGen, SubSonic, iBATIS.NET/MyBatis.NET, Castle
        ActiveRecord, and project-local mapping DSLs.
  - [ ] Emit `AnalysisGap` facts under `legacy.data.orm.unsupported.v1` with
        safe descriptor family labels and limitations.
  - [ ] Avoid storing raw mapping content, config values, SQL, paths, URLs, or
        provider values.
  - [ ] Add tests proving unsupported descriptors produce gaps, not invented
        entity/table facts.

- [ ] 6. Harden generated-code and mapped-symbol linkage. Requirements: 4, 5, 8.
  - [ ] Resolve generated data model symbols semantically when project load
        succeeds.
  - [ ] Add structural fallback for explicit generated output, custom tool, and
        scoped metadata file links.
  - [ ] Add scoped syntax fallback for partial classes, DataSet row/table/adapter
        types, context types, and ORM mapped classes.
  - [ ] Emit gaps for missing generated code, duplicate candidates, ambiguous
        partial classes, and stale generated-code hints.
  - [ ] Prove `Tier1Semantic` links do not upgrade descriptor facts above their
        descriptor tier ceiling.

- [ ] 7. Project model-enriched `legacy-data` dependency surfaces. Requirements: 5, 6, 8.
  - [ ] Add a safe surface projection for entity, storage object, column,
        relationship, adapter, routine, and mapped-type descriptors.
  - [ ] Reuse the existing `legacy-data` surface kind with
        `surfaceSubtype = data-model`; do not introduce a parallel
        `legacy-data-model` kind in MVP.
  - [ ] Preserve source labels, scan IDs, commit SHAs, rule IDs, evidence tiers,
        file spans, supporting fact IDs, and limitations.
  - [ ] Exclude `AnalysisGap` facts under `legacy.data.*` rule IDs from terminal
        surface projection; render them only as gaps, caveats, or limitations.
  - [ ] Exclude already-derived projection rows from prefix-based legacy data
        fact projection to prevent duplicate surfaces.
  - [ ] Cap downstream classifications for syntax-only, ambiguous, high fan-out,
        missing generated code, or reduced-coverage evidence.
  - [ ] Add tests for surface projection, duplicate surface gaps, selector
        behavior, gap exclusion, no-double-projection, backward compatibility
        for existing `legacy-data` surfaces, and report redaction.
  - [ ] Include a gap-exclusion regression for a pre-existing source rule such
        as `legacy.data.dbml.v1`, not only new old ORM gaps.
  - [ ] Add graph/vault projection tests proving NHibernate formula, filter, and
        query redaction survives through surface projection and export.

- [ ] 8. Integrate with combined reports, paths, reverse, impact, release-review, and portfolio. Requirements: 5, 6, 8.
  - [ ] Teach combined reports to render safe model-enriched `legacy-data` surfaces or
        emit explicit availability gaps.
  - [ ] Teach path and reverse queries to select model surfaces only from stable
        identities.
  - [ ] Update hardcoded surface allow-lists and user-facing "must be one of"
        messages in reverse and diff command validators if `legacy-data` is
        selectable there.
  - [ ] Teach diff, impact, release-review, and portfolio readers to consume or
        safely ignore new surfaces without failing.
  - [ ] Add safe default branches for combined report, path, reverse, diff,
        impact, release-review, portfolio, graph, and vault readers that switch
        on surface kind.
  - [ ] Add compatibility tests proving older indexes and missing fact families
        produce availability gaps rather than clean absence.
  - [ ] Add selector acceptance tests for paths/reverse/diff where `legacy-data`
        is supported, or explicit availability-gap tests where it is deferred.
  - [ ] Add no-double-count tests proving model properties and NHibernate rule
        IDs do not duplicate summary, combined, or portfolio rows.
  - [ ] Add tests proving report/export readers with surface-kind switches use
        safe defaults or availability gaps for unrecognized future model
        metadata, not exceptions.
  - [ ] Keep "impact" wording static and evidence-backed.

- [ ] 9. Integrate with evidence graph and vault export. Requirements: 6, 7, 8.
  - [ ] Export safe model document, descriptor, generated-symbol, surface, and
        gap nodes.
  - [ ] Export evidence-backed edges with rule IDs, evidence tiers, commit SHAs,
        supporting IDs, coverage labels, and limitations.
  - [ ] Respect hidden claim level and generated-output sentinel checks.
  - [ ] Add tests for redaction and graph/vault schema compatibility.
  - [ ] Add vault export tests proving connection strings, raw SQL-like formula
        or filter text, URLs, remotes, local paths, and private labels are absent.

- [ ] 10. Update docs and validation guidance. Requirements: 8.
  - [ ] Update `docs/ACCEPTANCE.md` for new data model identity, ORM descriptor,
        and surface behavior.
  - [ ] Update `docs/LANGUAGE_ADAPTER_CONTRACT.md` for any new facts, properties,
        or surface keys.
  - [ ] Update `docs/VALIDATION.md` with focused legacy data model test and
        smoke guidance.
  - [ ] Update legacy smoke catalog guidance when a public-safe fixture or
        neutral sample is added.
  - [ ] If fixtures are added, place them under `samples/legacy-data-model/`
        using neutral synthetic names and do not commit scan outputs, SQLite
        indexes, analyzer logs, private metadata, raw connection strings, or
        local paths.
  - [ ] Leave existing DBML, EDMX, and typed DataSet fixtures in their current
        sample locations for backward compatibility unless a separate cleanup
        spec authorizes moving them.
  - [ ] Keep public site copy unchanged unless a future site spec authorizes it.

- [ ] 11. Validate implementation. Requirements: 8.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`
  - [ ] `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyDataModel`
  - [ ] `dotnet test src/dotnet/TraceMap.sln`
  - [ ] CLI scan against at least one public-safe sample repository or checked-in
        synthetic fixture.
  - [ ] Relevant pinned smoke checks from `docs/VALIDATION.md`, or explicit
        implementation-state deferral with rationale.
  - [ ] `./scripts/check-private-paths.sh`
  - [ ] `git diff --check`

## Deferred Follow-Ups

- EF runtime model loading, database schema introspection, or migration
  execution.
- Deep provider-specific NHibernate runtime behavior, Fluent-only mappings, or
  session factory analysis.
- Rich LLBLGen, SubSonic, iBATIS.NET/MyBatis.NET, Castle ActiveRecord, or
  project-specific mapping DSL parsers beyond unsupported-descriptor gaps.
- LINQ expression evaluation or query-plan inference.
- Schema-to-DTO proof or serializer/runtime binding proof.
- Public site claims about legacy data model coverage.
