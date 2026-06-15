# Legacy Data Metadata Extraction Tasks

## Implementation Tasks

- [ ] 1. Add rule catalog and model constants. Requirements: 1, 2, 3, 4, 5, 6, 7, 8.
  - [ ] Add `legacy.data.metadata.inventory.v1`.
  - [ ] Add `legacy.data.dbml.v1`.
  - [ ] Add `legacy.data.edmx.v1`.
  - [ ] Add `legacy.data.typed-dataset.v1`.
  - [ ] Add `legacy.data.config.v1`.
  - [ ] Add `legacy.data.generated-link.v1`.
  - [ ] Add required fact type constants and bump the relevant .NET extractor version.
  - [ ] Add `ScannerVersions.LegacyDataExtractor` before emitting `LegacyData*`
        facts.
  - [ ] Document fact type selection policy and reducer-facing key compatibility
        in rule catalog entries.
  - [ ] Document limitations for static metadata, reduced coverage, parser safety,
        config secrecy, generated-code staleness, and no runtime data-flow or SQL
        execution proof.
  - [ ] Add all six rules to `rules/rule-catalog.yml` with fact types, evidence
        tiers, safe properties, limitations, and no-runtime-proof disclaimers
        before marking this task complete.

- [ ] 2. Extend inventory for legacy data metadata. Requirements: 1.
  - [ ] Inventory `.dbml` and `.edmx` files.
  - [ ] Gate typed DataSet `.xsd` files on deterministic typed DataSet or
        TableAdapter indicators.
  - [ ] Include generated `.designer.cs` files as linkage candidates without
        treating them as hand-authored logic.
  - [ ] Inventory checked-in config files relevant to provider/connection metadata.
  - [ ] Emit malformed, unreadable, too-large, and unsupported metadata gaps.
  - [ ] Add tests for inventory, `.xsd` gating, deterministic ordering, and
        privacy suppression.
  - [ ] Add test for unrelated `.xsd` files, such as WCF schemas or vendor specs,
        with no typed DataSet indicators producing no metadata facts.
  - [ ] Add inventory tests for `.dbml` malformed XML and `.edmx` valid XML with
        missing CSDL/SSDL/MSL sections.

- [ ] 3. Add XXE-safe XML/config parsing helpers. Requirements: 1, 2, 3, 4, 5.
  - [ ] Centralize XML reader settings that prohibit or ignore DTDs, set
        `XmlResolver = null`, preserve line info when practical, and avoid
        external entity resolution.
  - [ ] Add parser-bound handling for oversized or deeply nested documents if
        existing scanner infrastructure supports bounds.
  - [ ] Emit explicit parser security and malformed XML `AnalysisGap` facts.
  - [ ] Add tests for unclosed tags, invalid UTF-8, namespace errors, DTD
        rejection, entity expansion rejection, and no network/filesystem fetch
        during parse.
  - [ ] Add oversized/deeply-nested document tests if scanner infrastructure
        supports parser bounds.

- [ ] 4. Implement safe identifier and redaction policy. Requirements: 1, 2, 3, 4, 5, 6, 7, 8.
  - [ ] Reuse existing hash/truncation helpers where possible.
  - [ ] Retain safe local identifiers and safe repo-relative paths only.
  - [ ] Hash or omit raw SQL, connection strings, URLs, namespace URIs, server
        names, catalog names, usernames, passwords, local absolute paths, raw
        remotes, config values, and secret-looking values.
  - [ ] Add negative privacy tests for facts, reports, logs, validation summaries,
        and SQLite properties.
  - [ ] Extend privacy tests to inspect SQLite column values for facts with
        metadata-derived properties.
  - [ ] Add test for non-ASCII identifiers longer than 128 characters hashing
        rather than rendering cleartext.

- [ ] 5. Extract DBML metadata facts. Requirements: 2, 6, 7.
  - [ ] Parse DBML database, table, type, column, association, and routine metadata.
  - [ ] Emit entity, storage object, column, and mapping facts with safe keys.
  - [ ] Hash or omit provider, connection, database, routine, table, or column
        values when unsafe.
  - [ ] Emit gaps for malformed DBML, unsupported provider extensions, and
        ambiguous descriptors.
  - [ ] Add tests for simple mappings, associations, routines, unsafe names,
        generated-code hints, and reduced coverage.
  - [ ] Add tests for multiple DBML database descriptors, multiple DBML
        `<Database>` elements with ambiguity gaps, non-ASCII identifiers,
        descriptor tier ceilings, exact gap classification strings, and stable
        fact IDs across repeated scans.

- [ ] 6. Extract EDMX metadata facts. Requirements: 3, 6, 7.
  - [ ] Parse CSDL, SSDL, and MSL sections from checked-in EDMX.
  - [ ] Emit conceptual entity/property and storage table/column/routine facts.
  - [ ] Emit unambiguous entity-to-table and property-to-column mapping facts.
  - [ ] Emit gaps for unsupported inheritance, complex types, many-to-many,
        conditional mappings, duplicate names, provider extensions, and malformed
        EDMX.
  - [ ] Add tests for simple EDMX mapping, ambiguous mapping, unsafe namespaces,
        provider metadata redaction, and deterministic output.
  - [ ] Add tests for EDMX with no MSL, multiple containers, table-per-hierarchy
        or condition-based mapping, unsupported split/complex mapping, descriptor
        tier ceilings, and exact gap classification strings.
  - [ ] Add test for EDMX with valid outer XML but malformed CSDL schema.

- [ ] 7. Extract typed DataSet and TableAdapter metadata. Requirements: 4, 6, 7.
  - [ ] Detect typed DataSet `.xsd` files using deterministic indicators.
  - [ ] Emit DataSet, DataTable, DataColumn, relation, constraint, TableAdapter,
        command, and generated-type descriptor facts.
  - [ ] Reuse `SqlTextUsed` hash/length and `QueryPatternDetected` shape evidence
        for complete static TableAdapter command text where safe.
  - [ ] Emit routine/command metadata without claiming execution or existence.
  - [ ] Emit gaps for dynamic command text, stale generated code, incomplete
        schemas, unsupported provider metadata, and malformed XML.
  - [ ] Add tests for typed DataSet detection, unrelated `.xsd` suppression,
        TableAdapter command hashing, routine metadata, and generated-code hints.
  - [ ] Add tests for stale or missing `.designer.cs`, dynamic stored-procedure
        command metadata with no command text, descriptor tier ceilings, and
        exact gap classification strings.
  - [ ] Add test for `.xsd` with `msdata:` prefix but no actual DataSet or
        TableAdapter content producing a gate or unsupported-shape gap rather
        than descriptor facts.

- [ ] 8. Extract config provider and connection metadata. Requirements: 5, 6, 7.
  - [ ] Parse checked-in config files with safe XML settings.
  - [ ] Emit provider, connection-name, provider factory, EF provider, and ORM
        config metadata facts.
  - [ ] Link metadata descriptors to named connection evidence when deterministic.
  - [ ] Emit gaps for config transforms, encrypted sections, external config,
        dynamic connection construction, and unsupported ORM sections.
  - [ ] Add tests for redacted connection strings, provider names, named
        connection linkage, transforms, encrypted sections, and unsafe values.
  - [ ] Add tests for config files with no `connectionStrings` or provider
        sections producing no provider facts and no gaps.
  - [ ] Add tests for `configSource` / external config includes and exact gap
        classification strings.
  - [ ] Add test for `web.config` with
        `<connectionStrings configSource="external.config" />` producing an
        external include gap without loading the external file.

- [ ] 9. Link metadata to generated code and existing surfaces. Requirements: 6, 7.
  - [ ] Resolve metadata-to-generated-code links semantically when builds succeed.
  - [ ] Add structural and syntax fallback for generated filenames, namespaces,
        partial classes, DataSet/table/row/adapter types, contexts, and entities.
  - [ ] Preserve supporting fact IDs, metadata hashes, symbol role properties, and
        evidence tiers.
  - [ ] Emit ambiguity gaps instead of global short-name matching.
  - [ ] Integrate metadata surfaces with existing report/reducer keys without
        changing existing reducer semantics.
  - [ ] Add tests for semantic linkage, syntax fallback, missing/stale generated
        files, duplicate generated types, and downstream surface context.
  - [ ] Add tests proving metadata descriptor tiers are not upgraded by
        `Tier1Semantic` generated-code links.

- [ ] 10. Update scan report, SQLite, docs, and validation harness. Requirements: 7, 8.
  - [ ] Add scan report counts and known-gap summaries for legacy data metadata.
  - [ ] Ensure `facts.ndjson` and `index.sqlite` include new facts deterministically.
  - [ ] Update `docs/LANGUAGE_ADAPTER_CONTRACT.md` for new fact types and safe keys.
  - [ ] Update `docs/VALIDATION.md` with legacy data metadata smoke guidance.
  - [ ] Update `docs/ACCEPTANCE.md` if required artifacts, report sections, or
        reducer-facing behaviors change.
  - [ ] Extend `scripts/legacy_codebase_validation.py` and tests only if the
        implementation adds legacy data metadata to existing legacy summary
        output; document in implementation state if deferred.
  - [ ] Keep public claim level hidden until redacted evidence is reviewed.
  - [ ] Add compatibility tests that combine, report, paths, reverse, impact,
        release-review, and portfolio either ignore new `LegacyData*` facts safely
        or emit explicit availability gaps without failing.

- [ ] 11. Validate implementation. Requirements: 8.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`
  - [ ] `dotnet test src/dotnet/TraceMap.sln`
  - [ ] `python3 -m unittest scripts.tests.test_legacy_codebase_validation` if
        Task 10 extended the validation harness; otherwise skip.
  - [ ] Relevant pinned smoke checks from `docs/VALIDATION.md`, or explicitly
        defer with rationale in implementation state.
  - [ ] `./scripts/check-private-paths.sh`
  - [ ] `git diff --check`
  - [ ] Optional ignored local legacy data metadata smoke with redacted
        label/count comparison only.

## Deferred Follow-Ups

- Runtime EF model loading or database schema introspection.
- LINQ expression evaluation or query-plan inference.
- Rich EDMX inheritance/complex-type/condition mapping beyond MVP.
- Schema-to-DTO mapping for WCF/XSD service metadata.
- NHibernate Fluent mappings, custom ORM descriptors, or project-specific
  mapping DSLs unless future specs define deterministic extractors.
- Public site claims about legacy data metadata coverage.
