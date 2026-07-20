# Legacy Data Model Relationship Completion Tasks

Current state: implementation PR 1 in progress on the shared relationship gap
classifier plus narrow DBML integration.

## Spec Authoring Tasks

- [x] 0.1 Fetch `origin/dev` and work from current `dev`.
- [x] 0.2 Inspect predecessor specs:
      `legacy-data-model-orm-mapping-completion`,
      `legacy-data-model-metadata-extraction`, and
      `legacy-data-metadata-extraction`.
- [x] 0.3 Inspect live legacy data relationship extractors, tests, reporting
      touchpoints, and `rules/rule-catalog.yml` enough to avoid duplicate or
      stale scope.
- [x] 0.4 Draft `requirements.md`, `design.md`, `tasks.md`,
      `implementation-state.md`, and `review-prompts.md`.
- [x] 0.5 Run Kiro spec review with `claude-opus-4.8`, or record exact
      unavailable/timeout/blocker details.
- [x] 0.6 Run Kiro spec review with `claude-sonnet-4.6`, or record exact
      unavailable/timeout/blocker details.
- [x] 0.7 Patch Medium+ actionable review findings. Patch Low findings only
      when narrow and safe.
- [x] 0.8 Run one bounded re-review if feasible and record the result.
- [x] 0.9 Run spec delivery validation:
      `git diff --check`, `./scripts/check-private-paths.sh`, and a diff-scope
      check confirming changes are limited to this assigned spec folder.
- [x] 0.10 Commit, push, open PR to `dev`, run initial ACK, and update this
      file plus `implementation-state.md` with the current ACK state.
- [x] 0.11 Follow ACK to terminal merge-ready, merged, or handoff state, and
      update this file plus `implementation-state.md` with final readiness.

## Implementation Tasks

- [x] 1. Confirm and repair rule catalog ownership before relationship gap
      emissions.
      Requirements: 1, 8, 9.
  - [x] Treat these currently emitted but not literally cataloged strings as
        catalog repair work before any relationship follow-up code reuses or
        expands them: `AmbiguousLegacyDataModelIdentity`,
        `UnsupportedLegacyOrmMappingShape`, and
        `UnsupportedLegacyOrmDescriptor`.
  - [x] Confirm or add documentation under the owning rule for:
        `AmbiguousLegacyDataModelIdentity`,
        `UnsupportedLegacyOrmMappingShape`,
        `UnsupportedLegacyOrmDescriptor`, and
        `AmbiguousLegacyDataModelSelector`.
  - [x] Use the existing projection vocabulary `DuplicateIdentity` with reason
        `duplicate-surface`, or add catalog coverage before any projection or
        report code emits a new `DuplicateLegacyDataModelSurface` string.
  - [x] Add or confirm a machine-readable reason-code registry, or equivalent
        testable catalog mechanism, so tests can prove reason-code-to-rule
        ownership without brittle prose-only substring checks.
  - [x] Catalog the closed `safeReasonCode` set for the relationship classifier
        under `legacy.data.model.relationship.v1` before code emits it.
  - [x] Add or amend catalog entries before emitting any new gap string,
        relationship classifier, needs-review caveat, coverage label, or
        limitation.
  - [x] Add rule-catalog tests for every new or newly reused relationship gap
        string before extractor code emits it.
  - [x] Ensure catalog limitations state no runtime ORM/database behavior,
        referential integrity, query execution, or impact proof.

- [x] 2. Add a shared relationship gap classifier or equivalent decision helper
      when PR 1 takes the preferred shared-helper path.
      Requirements: 1, 2, 3, 9.
  - [x] Define deterministic inputs for relationship family, descriptor kind,
        endpoint state, join/key state, unsafe value state, parser coverage, and
        unsupported-shape flags.
  - [x] Return deterministic decisions for full relationship evidence, reduced
        relationship evidence, `AnalysisGap` only, or no in-scope relationship.
  - [x] Normalize classification, coverage label, endpoint coverage,
        limitations, evidence tier, rule ID, and safe reason code.
  - [x] Add focused unit tests for missing endpoint, duplicate identity,
        ambiguous endpoint candidates, unsupported shape, reduced parser
        coverage, unsafe endpoint identity, and the no-relationship/no-gap
        `not-in-scope` decision.
        The `not-in-scope` decision must be distinguishable from a silent
        extractor bug through a return value, debug-only trace, or focused unit
        assertion; production outputs must still emit no gap for unrelated
        descriptors.
  - [x] Add an overlapping-condition determinism test proving precedence,
        classification, limitations, fact order, and fact IDs stay stable.
  - [x] Prove the helper does not use runtime providers, SQL execution, external
        resources, LLM calls, embeddings, fuzzy matching, or prompt logic.

- [x] 3. Wire PR 1 to one narrow relationship family. Requirements:
      2, 3, 4, 6, 9, 10.
  - [x] Select one family for PR 1 after code inspection, preferably DBML
        association ambiguity or typed DataSet constraint ambiguity.
  - [x] Preserve existing deterministic facts and `mappingKind` values for
        already-supported relationship shapes.
  - [x] Add one or more ambiguity/unsupported-shape fixtures that produce
        `AnalysisGap` or reduced needs-review relationship evidence.
  - [x] Add or extend a synthetic committed smoke fixture for the touched family
        so the CLI smoke exercises at least one new relationship gap or
        needs-review shape.
  - [x] Assert ambiguous or unsupported shapes do not produce invented endpoint
        surfaces.
  - [x] Add privacy assertions for unsafe relationship, endpoint, key, storage,
        provider, path, URL, remote, SQL/config-like, and secret-looking values
        in the touched family.
  - [x] Shared helper added in PR 1; no alternate-path deferral rationale is
        required. Remaining family wiring stays explicit for later PRs.

- [ ] 4. Add DBML relationship follow-ups. Requirements: 2, 4, 8, 9.
  - [x] Harden duplicate association name handling without choosing a winner.
  - [ ] Add gaps or reduced evidence for missing/ambiguous `Type`, `ThisKey`,
        `OtherKey`, duplicate table/type scopes, provider extensions, and unsafe
        endpoint identity.
  - [x] Prove deterministic association evidence remains stable.
  - [x] Prove DBML relationship gaps do not infer referential integrity,
        generated runtime relationship behavior, or table existence.

- [ ] 5. Add EDMX relationship follow-ups. Requirements: 2, 5, 8, 9.
  - [ ] Harden ambiguous CSDL association endpoint and MSL association-set
        mapping behavior.
  - [ ] Add gaps or reduced evidence for multiple containers, inherited
        endpoints, split entity mappings, conditional mappings, complex
        mappings, many-to-many shapes without deterministic join evidence,
        provider extensions, and missing MSL relationship metadata.
  - [ ] Prove EDMX gaps do not claim EF runtime load, lazy loading, change
        tracking, referential integrity, provider compatibility, or query
        execution.

- [ ] 6. Add typed DataSet relationship follow-ups. Requirements:
      2, 6, 8, 9.
  - [ ] Harden `msdata:Relationship`, `xs:key`, `xs:unique`, and `xs:keyref`
        endpoint decisions.
  - [ ] Add gaps or reduced evidence for missing parent/child endpoints,
        duplicate constraints, ambiguous selectors, unsupported composite field
        matching, schema indicators without DataSet content, and SQL-only
        relationship hints.
  - [ ] Prove TableAdapter SQL text never becomes relationship evidence.

- [ ] 7. Add NHibernate relationship follow-ups. Requirements: 2, 7, 8, 9.
  - [ ] Harden `many-to-one`, `one-to-one`, collection/key, `one-to-many`, and
        deterministic `many-to-many` endpoint decisions.
  - [ ] Add gaps or reduced evidence for composite IDs, composite keys,
        formula-only joins, filters, custom SQL, dynamic components,
        inheritance/joined/union subclass, custom user types, provider
        extensions, ambiguous collection children, missing targets, and
        runtime-loaded config references.
  - [ ] Reuse safe XML bounds and per-class caps. Bounds or caps must emit gaps
        and reduced coverage.
  - [ ] Assert formula/filter/query/config/provider/path/URL/remote/secret-like
        values are omitted or hashed.

- [ ] 8. Add bounded downstream needs-review safeguards. Requirements: 8, 9.
  - [ ] Add downstream tests only for workflows touched by the implementation
        slice.
  - [ ] Prove `AnalysisGap` facts under `legacy.data.*` do not become terminal
        `legacy-data` surfaces.
  - [ ] Prove reduced relationship evidence renders as needs-review or reduced
        coverage where touched.
  - [ ] Avoid repeating the merged slice-1 no-double-count regression unless
        projection/report code changes.
  - [ ] Keep broad combined/path/reverse/diff/impact/release-review/portfolio/
        vault/RAG/static HTML expansion deferred unless directly touched.

- [x] 9. Validate implementation. Requirements: 10.
  - [x] Run focused tests for touched extractor family, classifier/helper, rule
        catalog, privacy, and touched downstream workflows.
  - [x] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [x] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [x] Run CLI scan smoke against a synthetic committed fixture repository or
        existing public-safe committed fixture and verify:
        `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and
        `logs/analyzer.log`.
  - [x] Record the scanned fixture commit SHA and coverage labels.
  - [x] Run relevant pinned checks from `docs/VALIDATION.md` when implementation
        touches language adapters, shared graph/report behavior, vault/RAG
        export, portfolio, impact, release-review, or static HTML; otherwise
        explicitly defer with rationale.
        For legacy data relationship extractor-only changes, expected pinned
        guidance is the "legacy data metadata changes in the .NET adapter"
        section: focused `LegacyDataMetadataExtractorTests`, full .NET build,
        full .NET test suite, private-path guard, and diff check. If model
        surface projection or `surfaceSubtype` reporting changes, also run the
        documented focused projection/report/query/export test filter.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Run `git diff --check`.

## Deferred Follow-Ups

- Exhaustive relationship shape coverage for every DBML, EDMX, typed DataSet,
  and NHibernate relationship construct.
- Runtime ORM model loading, live database introspection, query execution,
  schema validation, referential-integrity proof, or provider behavior.
- Arbitrary Fluent mapping execution or project-local ORM DSL parsing.
- Broad downstream relationship expansion across combined/path/reverse/diff/
  impact/release-review/portfolio/vault/RAG/static HTML.
- Public site or marketing claims about legacy relationship coverage.
- If PR 1 takes the single-family alternate path, PR 2 must either wire the
  shared helper to remaining families or justify permanent per-family divergence
  with cataloged reason-code tests.
