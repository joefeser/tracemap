# Legacy Flow Composition Reporting Tasks

## Implementation Tasks

- [ ] 1. Add rule catalog entries and report model constants. Requirements: 1, 4, 5, 6, 7.
  - [ ] Add `legacy.flow.input-availability.v1`.
  - [ ] Add `legacy.flow.root-selection.v1`.
  - [ ] Add `legacy.flow.static-traversal.v1`.
  - [ ] Add `legacy.flow.parameter-forward-unavailable.v1` for optional
        parameter-forward edge unavailability.
  - [ ] Add `legacy.flow.classification.v1`.
  - [ ] Add `legacy.flow.gap-propagation.v1`.
  - [ ] Add `legacy.flow.redaction.v1`.
  - [ ] Add `legacy.flow.report.v1`.
  - [ ] Document limitations for static composition, WebForms events, WCF/service
        metadata, SQL/data metadata, reduced coverage, missing extractors,
        traversal bounds, high fan-out, and redaction.
  - [ ] Document the high fan-out threshold rationale as a conservative v1
        placeholder requiring empirical calibration against redacted legacy
        validation data, with suggested false-positive and false-negative
        metrics by terminal kind.
  - [ ] Add versioned JSON schema/model constants for legacy-root path output
        before emitting output, including `SchemaVersion = "legacy-flow.v1"`.
  - [ ] Keep tasks unchecked until implemented; do not mark spec-delivery work as
        implementation completion.

- [ ] 2. Extract or reuse shared path graph helpers. Requirements: 1, 3, 7.
  - [ ] Prefer extending `CombinedDependencyPathReporter` and related
        `CombinedPath*` models instead of creating a separate graph engine.
  - [ ] Reuse `CombinedDependencyReporter` and `CombinedReportHelpers` for
        source inventory, endpoint/surface evidence, stable hashing, safe paths,
        metadata ordering, and output writing.
  - [ ] Use `TraceMap.Storage.FlowPathReporter` and `SqliteIndexWriter` table
        conventions when adding single-index path support.
  - [ ] Keep behavior-preserving refactors separate from legacy-root behavior.
  - [ ] Add tests proving existing `tracemap paths` output remains compatible.

- [ ] 3. Implement read-only legacy flow input reader. Requirements: 1, 7.
  - [ ] Detect single-index versus combined-index SQLite files using existing
        index detection behavior.
  - [ ] Read WebForms event/handler/flow facts when present.
  - [ ] Read call edges, object creations, symbol relationships, optional
        parameter forwarding, fact-symbol attachments, WCF/service-reference
        facts, HTTP/API facts, SQL/query facts, dependency surfaces, coverage
        metadata, extractor versions, commit SHA, and `AnalysisGap` facts.
  - [ ] Read queued `LegacyData*` facts when available without requiring them.
  - [ ] Emit `SchemaMissing`, `ExtractorUnavailable`, or availability gaps for
        missing fact families, old schemas, and absent graph tables.
  - [ ] Preserve source labels/source index IDs for combined indexes.
  - [ ] Neutralize private or unreviewed source labels before they enter report
        `sourceLabel` fields.
  - [ ] Add tests for old-index compatibility, missing extractor behavior, and
        read-only input enforcement.

- [ ] 4. Build deterministic evidence graph primitives. Requirements: 1, 3, 7.
  - [ ] Represent nodes for WebForms events, handlers, API routes, service
        operations, WCF clients, HTTP clients, SQL/query surfaces, legacy data
        metadata, dependency surfaces, and gaps.
  - [ ] Represent edges for calls, creations, symbol relationships, optional
        parameter-forwarding, WCF/service-reference mappings, HTTP dependencies,
        SQL/query attachments, legacy data generated-code links, and existing
        dependency edges.
  - [ ] Preserve supporting fact IDs, edge IDs, rule IDs, evidence tiers, line
        spans, source labels, commit SHA, and extractor identities.
  - [ ] Define stable `nodeId`, `edgeId`, and `flowId` hashes from ordered safe
        source labels, identities, fact IDs, edge IDs, paths, and line spans.
  - [ ] Treat `WebFormsEventFlowProjected` as an opaque single-hop edge only when
        primitive evidence is missing or as de-duplicated corroboration when
        primitive edges exist; never let it upgrade classification.
  - [ ] Scope aliases to a single source/index and reject ambiguous global
        short-name stitching.
  - [ ] Reject unaligned cross-source symbol-name matches instead of stitching
        them.
  - [ ] Add cycle-safety, deterministic ordering, projection de-duplication,
        parameter-forward preservation/unavailability/de-duplication, and
        input-row permutation tests.

- [ ] 5. Select conservative flow roots. Requirements: 2, 4.
  - [ ] Add WebForms event roots only from supported event binding plus handler
        resolution evidence or approved auto-wireup evidence.
  - [ ] Label auto-wireup lifecycle roots such as `Page_Load`/`Page_Init` as
        lifecycle roots, not user-action roots.
  - [ ] Add API/service roots from route, endpoint, service host, or operation
        facts where existing rules support them.
  - [ ] Emit unresolved-root results for full-coverage WebForms events without
        resolved handlers.
  - [ ] Emit reduced-coverage or analysis gaps for ambiguous, syntax-only,
        missing code-behind, missing designer, or generated-code-limited roots.
  - [ ] Add tests proving arbitrary method names, raw markup strings, and global
        handler-name matches cannot become roots.

- [ ] 6. Implement bounded traversal and terminal detection. Requirements: 3, 5.
  - [ ] Traverse from selected roots using deterministic breadth-first search.
  - [ ] Support selectors for WebForms event, endpoint, symbol, `--to-surface`,
        classification, max depth, max paths, and max frontier using the shared
        paths selector grammar.
  - [ ] Stop at WCF operation, HTTP client, SQL/query, legacy data metadata, and
        dependency-surface terminals.
  - [ ] Treat WebForms handler to WCF client to normalized operation as exactly
        one path with WCF operation terminal context.
  - [ ] Stop traversal at WCF operation terminals with no outbound edges; v1
        does not traverse service-side implementation or any downstream evidence
        from WCF operations.
  - [ ] Emit `ExtractorUnavailable` gap when legacy data metadata facts are
        missing and no other terminal evidence is found.
  - [ ] Prevent unsupported links through runtime DI, reflection, serializer
        behavior, branch feasibility, event bubbling, deployment, network
        reachability, database existence, or multi-hop WCF chains.
  - [ ] Emit truncation gaps when depth/path/frontier limits are reached.
  - [ ] Add tests for direct paths, multi-hop static paths, `SelectorNoMatch`,
        `ClassificationFilterNoMatch`, recursive cycles, unaligned cross-source
        matches, parameter-forward availability gaps, and truncation.

- [ ] 7. Implement rule-backed flow classification. Requirements: 3, 4.
  - [ ] Classify results as `StrongStaticPath`, `ProbableStaticPath`,
        `NeedsReviewStaticPath`, `NoBackendEvidence`, `ReducedCoverage`, or
        `AnalysisGap`.
  - [ ] Cap classification by weakest required evidence tier and coverage.
  - [ ] Cap syntax-only, name-only, ambiguous, generated-code-uncertain, or high
        fan-out paths at `NeedsReviewStaticPath` or lower.
  - [ ] Define high fan-out as five or more inbound candidate paths from distinct
        roots to one terminal identity, or generic ambiguous terminal keys such
        as `status`, `id`, `name`, `value`, `result`, or `response`.
  - [ ] Treat missing extractor availability and reduced coverage as gaps, not
        clean absence.
  - [ ] Include contributing rule IDs, evidence tiers, supporting facts/edges,
        coverage labels, and limitations in every result.
  - [ ] Add tests proving weak evidence cannot produce `StrongStaticPath` and
        full-coverage absence is distinct from missing-extractor gaps.

- [ ] 8. Extend CLI path command and output writers. Requirements: 5, 6, 7.
  - [ ] Extend `tracemap paths --include-legacy-roots` rather than adding a
        separate command.
  - [ ] Add `--view legacy-flows` if a legacy-focused grouping/wording view is
        needed.
  - [ ] Keep current `paths` output semantics: file outputs honor
        `--format markdown|json`, and directory outputs write both Markdown and
        JSON artifacts regardless of `--format`.
  - [ ] Write deterministic path report artifacts using the shared paths output
        contract; any legacy-flow aliases must be documented and tested.
  - [ ] Add Markdown sections or grouped subsections for summary,
        coverage/availability, classifications, representative static paths,
        roots without backend evidence, gaps, and limitations.
  - [ ] Ensure wording never claims runtime execution, proven impact, guaranteed
        backend reachability, executed queries, or production dependency.
  - [ ] Add forbidden-wording assertions for generated Markdown/JSON.
  - [ ] Add CLI tests for options, invalid selectors, read-only input handling,
        and output path behavior.

- [ ] 9. Enforce privacy and redaction. Requirements: 5, 6, 8.
  - [ ] Reuse existing safe path, hash, and display helpers where possible.
  - [ ] Add a final report-output guard for local absolute paths, raw remotes,
        private labels, raw SQL, URLs, endpoint addresses, WSDL/SOAP addresses,
        connection strings, config values, source snippets, and secret-looking
        tokens.
  - [ ] Ensure logs do not echo unsafe selector or display values.
  - [ ] Validate that `displayLabel` and `sourceLabel` fields do not contain
        private repository names, unreviewed sample identifiers, or internal
        architecture labels.
  - [ ] Include `legacy.flow.redaction.v1` in notes when flow output hashes or
        omits unsafe display values while preserving source fact rule IDs.
  - [ ] Add negative privacy tests for Markdown, JSON, logs, SQLite-derived
        display properties, private source labels, and raw SQL/query text.
  - [ ] Run `./scripts/check-private-paths.sh` before PR.

- [ ] 10. Integrate with existing reporting behavior safely. Requirements: 5, 7.
  - [ ] Reuse shared models/helpers with existing `report` and `paths` code where
        practical.
  - [ ] Ensure existing report/path/reverse/impact/release-review/portfolio
        commands either ignore new legacy-flow output safely or emit explicit
        availability gaps where relevant.
  - [ ] Preserve backward compatibility for existing facts and JSON schemas.
  - [ ] Update docs or language adapter contract only where user-visible command
        behavior, schema, fact consumption, or rule catalog behavior changes.
  - [ ] Add compatibility tests for older indexes and combined indexes.

- [ ] 11. Add focused fixtures and smoke guidance. Requirements: 8.
  - [ ] Add fixtures for WebForms-to-service, WebForms-to-WCF metadata,
        WebForms-to-SQL/query, API-to-backend, legacy data metadata when
        available, unresolved handlers, ambiguous paths, missing schema, reduced
        coverage, and redaction.
  - [ ] Add byte-stability tests for JSON output.
  - [ ] Add input row permutation tests for stable `nodeId`, `edgeId`, `flowId`,
        and byte-identical JSON.
  - [ ] Add tests for same combined index with same labels producing identical
        JSON and different labels producing different JSON with stable ordering.
  - [ ] Add tests for reused `WebFormsEventFlowProjected` evidence that does not
        duplicate supporting IDs or upgrade classification.
  - [ ] Add tests for indexes with no root facts producing a valid empty report
        with `NoRootsFound` gap, not a command error or malformed output.
  - [ ] Update `docs/VALIDATION.md` with legacy-root path validation guidance if
        implementation changes validation workflow.
  - [ ] Keep local legacy smoke manifests and outputs ignored/local-only.
  - [ ] Use neutral labels in any committed summaries.

- [ ] 12. Validate implementation. Requirements: 8.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`
  - [ ] `dotnet test src/dotnet/TraceMap.sln`
  - [ ] Relevant pinned smoke checks from `docs/VALIDATION.md`, or explicit
        deferral with rationale in implementation state.
  - [ ] `./scripts/check-private-paths.sh`
  - [ ] Forbidden-wording guard for generated flow/path reports, either as a
        dedicated script or focused tests.
  - [ ] `git diff --check`
  - [ ] Optional ignored local legacy flow smoke with redacted label/count
        comparison only.

## Suggested PR Boundaries

- PR 1: Tasks 1-4, covering rule catalog, schema/model constants, shared graph
  helpers, read-only input reader, availability gaps, and graph primitives.
- PR 2: Tasks 5-7, covering root selection, traversal, terminal detection, and
  classification.
- PR 3: Tasks 8-10, covering CLI, Markdown/JSON writers, privacy guard, and
  compatibility.
- PR 4: Tasks 11-12, covering fixtures, docs, smoke guidance, and final
  validation.

## Deferred Follow-Ups

- Runtime tracing, browser automation, IIS hosting, service execution, or
  database introspection.
- Visual graph UI or public site claims.
- Full DI, reflection, serializer, branch feasibility, or permission analysis.
- Arbitrary ORM DSL composition beyond deterministic legacy data metadata facts.
- Cross-repository symbol stitching without explicit combined evidence.
- Persisting composed flow results into scan indexes by default.
