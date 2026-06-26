# Legacy Data Model ORM Mapping Completion Tasks

Current state: ready-for-implementation after reviewed spec loop. Product
implementation has not started in this branch.

## Spec Authoring Tasks

- [x] 0.1 Inspect the source specs for legacy data metadata extraction,
      legacy data model metadata extraction, legacy data model reporting
      integration, and legacy codebase validation.
- [x] 0.2 Draft `requirements.md`, `design.md`, `tasks.md`,
      `implementation-state.md`, and optional review prompts for this spec.
- [x] 0.3 Run Kiro spec review with `claude-opus-4.8` if available, or record
      the exact timeout/unavailable evidence.
- [x] 0.4 Run Kiro spec review with `claude-sonnet-4.6` if available, or record
      the exact timeout/unavailable evidence.
- [x] 0.5 Patch Medium+ actionable review findings without implementing
      product code.
- [x] 0.6 Run one bounded final Kiro re-review after patches.
- [x] 0.7 Run spec-delivery validation: `git diff --check`,
      `./scripts/check-private-paths.sh`, and a review that only this spec
      folder is staged for commit.

## Implementation Tasks

- [ ] 1. Confirm rule catalog ownership and exact gap vocabulary. Requirements:
      1, 2, 3, 4, 5, 6, 7, 8, 9.
  - [ ] Reuse existing `legacy.data.*` rules where they already own the source
        fact or gap.
  - [ ] Confirm whether `legacy.data.model.generated-link.v1` is needed for new
        model-normalized linkage semantics or whether
        `legacy.data.generated-link.v1` remains sufficient.
  - [ ] Add or amend rule catalog entries before emitting any new relationship,
        unsupported descriptor, selector, projection, or exporter gap.
  - [x] Confirm or add `DuplicateLegacyDataModelSurface`,
        `AmbiguousLegacyDataModelSelector`, and
        `UnknownLegacyDataModelDescriptorRole` catalog coverage before emitting
        them from projection/report compatibility code.
  - [ ] Document limitations for every changed rule: static evidence only,
        reduced coverage, parser safety, redaction, unsupported shapes, and no
        runtime ORM/database proof.
  - [x] Add exact classification tests for any new or reused gap strings.

- [ ] 2. Harden safe normalized descriptor identity. Requirements: 2, 9.
  - [ ] Centralize identity fields for metadata format, source artifact type,
        model kind, descriptor role, stable model key, safe display/hash fields,
        coverage label, and limitations.
  - [ ] Ensure stable keys do not include local absolute paths, raw remotes,
        timestamps, SQLite row IDs, raw SQL/config values, or runtime
        environment values.
  - [ ] Preserve separate identities for descriptors that share display names
        but differ by namespace, container, source file, format, or mapping
        scope.
  - [ ] Emit ambiguity gaps when downstream selectors cannot distinguish
        duplicate identities.
  - [ ] Add determinism, duplicate identity, safe-name, hash-only, and SQLite
        privacy tests.
  - [x] Add a cross-format identity collision test proving identical display
        names in different metadata formats or source artifact types keep
        separate stable keys and downgrade ambiguous selectors.

- [ ] 3. Complete relationship extraction and gap behavior. Requirements: 3, 8.
  - [ ] Add or harden deterministic DBML association relationship evidence and
        duplicate/ambiguous association gaps.
  - [ ] Add or harden EDMX CSDL/MSL relationship evidence and gaps for multiple
        containers, inherited/split/conditional/complex/many-to-many shapes, and
        ambiguous association sets.
  - [ ] Add or harden typed DataSet relation/constraint endpoint evidence and
        gaps for incomplete or ambiguous relation metadata.
  - [ ] Add or harden NHibernate relation evidence for many-to-one, one-to-one,
        collections, keys, one-to-many, and deterministic many-to-many shapes.
  - [ ] Represent relationships as `LegacyDataMappingDeclared` source evidence
        where appropriate while preserving existing `mappingKind` values.
  - [ ] Add tests proving ambiguous or unsupported relationship shapes produce
        `AnalysisGap` rows and downstream needs-review/reduced-coverage labels,
        not arbitrary relationships.
  - [x] Add a no-double-count regression proving source relationship facts and
        normalized relationship projection do not create duplicate terminal
        `legacy-data` surfaces.
        The regression must assert the SQLite/report surface-count path, not
        only an in-memory helper, so a fixture with one relationship descriptor
        produces one terminal `legacy-data` surface count with the source fact
        cited as support.

- [ ] 4. Complete NHibernate `.hbm.xml` deterministic descriptor depth.
      Requirements: 1, 2, 4, 9.
  - [ ] Reuse shared safe XML parser settings and bounds for NHibernate mapping
        XML.
  - [ ] Extract deterministic class, table, id, version, discriminator,
        property, component, nested component, key, element, collection, and
        column descriptors.
  - [ ] Extract deterministic relationship descriptors where endpoints and
        join/key metadata are safely scoped.
  - [ ] Hash or omit schema, catalog, formula, filter, SQL, named query, query
        text, dialect, connection, provider, and config-like values.
  - [ ] Enforce per-class descriptor caps and emit deterministic truncation gaps.
  - [ ] Emit unsupported-shape gaps for inheritance, composite IDs,
        dynamic components, custom SQL, filters, named queries, custom user
        types, provider extensions, and config-referenced mappings that require
        runtime loading.
  - [ ] Add fixture tests for simple mappings, components, collections,
        relationships, unsafe values, parser safety, caps, reduced coverage, and
        deterministic output.
  - [ ] Add explicit parser-bound and per-class cap tests proving oversized or
        truncated `.hbm.xml` evidence emits `LegacyDataMetadataTooLarge` or the
        documented truncation gap, not clean absence.

- [ ] 5. Add unsupported old ORM descriptor gap coverage. Requirements: 1, 5, 9.
  - [ ] Recognize documented public-safe indicators for unsupported descriptor
        families such as LLBLGen, SubSonic, iBATIS.NET/MyBatis.NET, Castle
        ActiveRecord, Fluent-only mappings, and provider-specific descriptors.
  - [ ] Define a conservative deterministic signal taxonomy before recognizing
        project-local mapping DSLs.
  - [ ] If no project-local DSL taxonomy candidate is ready, emit no
        project-local DSL recognition beyond a deferred
        `UnsupportedLegacyOrmDescriptor` gap with a safe closed family label
        such as `project-local-dsl-deferred`; do not ship partial DSL
        recognition without cataloged signals and tests.
  - [ ] Emit gaps under `legacy.data.orm.unsupported.v1` with safe family labels
        or hashes, `Tier4Unknown`, spans where available, coverage labels, and
        limitations.
  - [ ] Prove unsupported descriptors do not emit entity, table, column, or
        relationship descriptor facts.
  - [ ] Add privacy tests for raw mapping content, SQL/config/provider values,
        URLs, raw remotes, local paths, private labels, and secrets.

- [ ] 6. Close deterministic DBML, EDMX, typed DataSet, and TableAdapter
      precision gaps. Requirements: 6, 9.
  - [ ] Add DBML tests and gaps for multiple database descriptors, provider
        extensions, ambiguous descriptors, non-ASCII/unsafe identifiers,
        routines, associations, tier ceilings, and stable fact IDs.
  - [ ] Add EDMX tests and gaps for missing MSL, multiple containers, inherited,
        split, conditional, complex, many-to-many, duplicate, provider-extension,
        and malformed-inner-section shapes.
  - [ ] Add typed DataSet tests and gaps for stale/missing designer code,
        dynamic stored-procedure metadata, typed DataSet prefixes without actual
        dataset content, relation precision, tier ceilings, and exact
        classifications.
  - [ ] Keep unrelated `.xsd` gating XSD-intrinsic and prove generated code or
        filenames alone do not qualify a schema.
  - [ ] Keep TableAdapter SQL evidence hash/shape-only.

- [ ] 7. Harden generated-code linkage boundaries. Requirements: 7, 8, 9.
  - [ ] Resolve generated or mapped symbols semantically when project load
        succeeds and scoped descriptor identity is available.
  - [ ] Preserve structural and scoped syntax fallback for generated files,
        custom tool outputs, partial classes, DataSet table/row/adapter types,
        contexts, and ORM mapped classes when semantics are unavailable.
  - [ ] Emit gaps for missing generated code, stale generated hints, duplicate
        partial types, ambiguous mapped classes, and global short-name-only
        candidates.
  - [ ] Preserve supporting fact IDs, descriptor identity hashes, symbol role
        properties, evidence tiers, and reduced coverage labels.
  - [ ] Add tests proving `Tier1Semantic` links do not upgrade source descriptor
        tiers and generated designer code is not treated as hand-authored logic.

- [ ] 8. Add bounded downstream compatibility safeguards. Requirements: 8, 9.
  - [ ] Confirm `tracemap scan` still emits `scan-manifest.json`,
        `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log`.
  - [ ] Ensure existing combined/report/path/reverse/export readers safely
        ignore, render, or gap new descriptor roles and relationship properties.
  - [ ] Add at least one non-crash regression for an unknown or future
        descriptor role/property in a touched reader, producing a documented
        schema or availability gap.
  - [ ] Keep `AnalysisGap` facts under `legacy.data.*` out of terminal
        `legacy-data` surfaces.
  - [x] Add compatibility tests only for workflows touched by this slice.
  - [ ] Record broad diff, impact, release-review, portfolio, evidence graph,
        vault, RAG/docs-export, and static HTML expansion as follow-up unless
        touched directly.

- [ ] 9. Add public-safe fixtures and implementation validation. Requirements:
      9.
  - [ ] Add small synthetic fixtures for DBML, EDMX, typed DataSet/TableAdapter,
        NHibernate, unsupported ORM gaps, generated-code links, ambiguity, parser
        safety, and unsafe value suppression.
  - [x] Add focused tests for extractor behavior, relationship evidence,
        unsupported gaps, generated-link boundaries, descriptor tier ceilings,
        deterministic ordering, and privacy suppression.
  - [ ] Add SQLite privacy assertions for metadata-derived properties.
  - [ ] Run focused tests for touched layers.
  - [ ] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [ ] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [ ] Run CLI scan smoke against a public-safe or synthetic committed fixture
        repository and verify required artifacts plus concrete commit SHA.
  - [ ] Run `./scripts/check-private-paths.sh`.
  - [ ] Run `git diff --check`.
  - [ ] Run or explicitly defer relevant pinned smoke checks from
        `docs/VALIDATION.md` when shared graph/report/export behavior changes.

## Deferred Follow-Ups

- Runtime ORM model loading, live database introspection, SQL execution, or
  provider behavior emulation.
- Fluent mapping execution or arbitrary project-local DSL parsing.
- New `legacy-data-model` surface kind.
- Broad model-specific reverse selectors.
- Persisted derived surface tables and full no-double-count behavior across all
  downstream workflows.
- Full diff, impact, release-review, portfolio, evidence graph, vault,
  RAG/docs-export, and static HTML model-descriptor expansion beyond safeguards
  needed for this extraction slice.
- Public site claims about legacy ORM/data-model support.
