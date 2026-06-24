# Route Flow Service/Data Composition Next Tasks

Status: implemented-pr1-followups-deferred

## Spec Delivery Tasks

- [x] 1. Draft the continuation spec packet for issue #179.
  - [x] Add `requirements.md`.
  - [x] Add `design.md`.
  - [x] Add `tasks.md`.
  - [x] Add `implementation-state.md`.
  - [x] Add `review-packet.md`.
  - [x] Confirm the scope is a continuation over the shipped
        `route-flow-service-data-composition` slices, not a duplicate.
  - [x] Keep all wording generic and public-safe.

- [x] 2. Review and patch the spec.
  - [x] Run Opus Kiro spec review.
  - [x] Run Sonnet Kiro spec review.
  - [x] Patch Medium+ merge-readiness findings or document why none apply.
  - [x] Run at most two re-review cycles.
  - [x] Record review artifacts and dispositions in `implementation-state.md`.

- [x] 3. Validate the spec-only change.
  - [x] Run `git diff --check`.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Run any obvious spec/docs validation script if present.
  - [x] Confirm no product code, site files, generated outputs, private paths,
        private names, raw SQL, config values, snippets, secrets, raw remotes,
        or local-only artifacts are committed.

## Recommended Implementation Slice

- [x] 4. Verify live route-flow state before product edits. Requirements: 1.
  - [x] Confirm the existing `tracemap route-flow` command, JSON report type,
        classification vocabulary, and rule namespace remain the extension
        points.
  - [x] Confirm which route-flow rows already exist for flow, logic,
        dependency surfaces, touched files, touched symbols, argument
        projection, fact-symbol projection, and parameter-forward bridges.
  - [x] Stop or narrow scope if the selected continuation is already complete.
  - [x] Reconcile overlap with
        `.kiro/specs/route-centered-endpoint-trace-completeness` tasks 8-10 by
        recording the exact sub-scope owned here: grouped service/data
        presentation over already-selected route-flow rows, joined
        data/query/dependency/value-origin context, and downgrade tests for
        those grouped rows. Do not claim touched-file or touched-symbol
        summaries, selector trace metadata, broad endpoint-trace completeness,
        or unrelated route-flow backlog.

- [x] 5. Add service/data grouping view-model support. Requirements: 2, 5.
  - [x] Reuse existing `flowRows`, `logicRows`, `dependencySurfaces`,
        `touchedFiles`, `touchedSymbols`, and gaps rather than duplicating
        conclusions.
  - [x] Add safe grouping labels for method, service, interface-candidate,
        repository, query, data-surface, dependency, legacy-data,
        value-origin, and gap context where evidence supports them.
  - [x] Choose the JSON placement for grouping metadata and record it in
        `implementation-state.md`.
  - [x] Preserve supporting fact IDs, edge IDs, rule IDs, evidence
        tiers, file spans, source labels, coverage, commit SHA, extractor
        identity, and limitations.
  - [x] Ensure grouped context inherits weakest classification, weakest tier,
        and weakest coverage from contributing rows.

- [ ] 6. Harden data/query/dependency and value-origin composition.
      Requirements: 3, 4.
  - [x] Render object-shape, DTO/projection, query-shape, repository/data,
        package/config, HTTP, queue/event, storage, WCF, ASMX, Remoting,
        legacy-data, SQL/persistence, and generic dependency evidence only
        when joined to selected route-flow evidence.
  - [x] Render value-origin context from `combined_argument_flows` and
        `combined_parameter_forward_edges` only as bounded review context.
  - [x] Render fact-symbol context only when selected source-local symbols
        support the join.
  - [ ] Emit narrower gaps for adjacent but unjoinable data/query/dependency
        evidence. Deferred beyond PR 1; see `implementation-state.md`.
  - [x] Preserve candidate wording and review-tier caps for interface,
        override, and DI-adjacent evidence.

- [ ] 7. Enforce coverage, gap, and classification downgrades.
      Requirements: 4.
  - [ ] Require full relevant route-flow coverage before strong or clean
        no-evidence conclusions. Deferred beyond PR 1; see
        `implementation-state.md`.
  - [ ] Downgrade or gap reduced coverage, missing optional tables, missing
        schema columns, unknown commit SHA, missing extractor identity, stale
        generated code, unsupported shapes, unjoinable projection rows,
        ambiguous service/data matches, high fan-out, and truncation. Deferred
        beyond PR 1; see `implementation-state.md`.
  - [ ] Ensure truncation gaps are deterministic and do not imply omitted rows
        are absent. Deferred beyond PR 1; see `implementation-state.md`.
  - [ ] Ensure every gap has a rule ID, classification, safe scope, supporting
        IDs where available, and limitations. Deferred beyond PR 1; see
        `implementation-state.md`.
  - [ ] Use existing `SchemaMissing`, `ExtractorUnavailable`, and
        `TruncatedByLimit` gap codes for missing optional schema, missing
        extractor-family evidence, and cap truncation unless a future rule
        catalog update documents a narrower code before use. Deferred beyond
        PR 1; see `implementation-state.md`.

- [x] 8. Protect output safety and determinism. Requirements: 5.
  - [x] Keep Markdown, JSON, logs, and safe metadata free of raw local paths,
        raw remotes, private names, private routes, raw SQL, config values,
        endpoint URLs, connection strings, snippets, generated local artifact
        paths, and secrets.
  - [x] Cite `combined.route-flow.redaction.v1` for rows where unsafe values
        are hashed or omitted.
  - [x] Sort arrays and metadata maps deterministically.
  - [x] Derive stable IDs only from safe deterministic inputs.
  - [x] Preserve explicit `null`, empty arrays, and closed-set gap codes for
        uncertainty.

- [ ] 9. Add focused tests and validation. Requirements: 6.
  - [x] Add route-flow tests for direct service/repository grouping.
  - [ ] Add tests for interface single candidate, multiple candidate, no
        candidate, syntax-only candidate, and high fan-out cases where the
        selected slice touches that behavior.
  - [x] Add tests for data/query/dependency context rows from existing
        route-flow evidence.
  - [ ] Add tests for unjoinable projection rows, unsupported attached context,
        missing optional schema, old combined indexes, reduced coverage,
        unknown commit SHA, and clean full-coverage no-evidence behavior.
  - [x] Add deterministic JSON and stable-ID tests.
  - [x] Add closed-set metadata tests for `groupKind`, `matchKind`, and
        `valueSafety` values emitted by the selected slice.
  - [x] If `evidenceKind` is emitted, add closed-set metadata tests for it;
        otherwise assert it remains omitted or `null` and is not used in
        stable IDs.
  - [x] Add a backward-compatibility test proving `reportType = "route-flow"`
        and the existing JSON version remain unchanged for this additive slice.
  - [x] Add a multi-extractor grouping test when the selected implementation
        emits extractor identity at grouped-context level; otherwise assert the
        grouped context relies on supporting row references instead of choosing
        one extractor arbitrarily.
  - [x] Add redaction tests for raw SQL, config, URL, route, snippet, path,
        remote, and secret-like values.
  - [x] Assert every emitted rule ID resolves to the rule catalog.
  - [x] Run focused route-flow tests, solution build/tests when product code
        changes, `git diff --check`, `./scripts/check-private-paths.sh`, and
        relevant pinned smokes or explicit deferrals.
  - [x] Record the private legacy route-flow smoke deferral explicitly in
        `implementation-state.md` if that smoke is not run during
        implementation.

## Deferred Follow-Ups

- Runtime DI registration proof, service locator proof, reflection target
  resolution, branch feasibility, mutation semantics, database execution, query
  plan inference, row counts, production traffic, and release approval remain
  out of scope.
- Browser/computer-use evidence may be considered only as explicitly labeled
  external context in a future spec and must not upgrade static conclusions.
- Public site copy for this feature requires a separate site spec after the
  implementation is merged and validated.

## Suggested PR Boundaries

- PR 1: Record the exact endpoint-trace-completeness overlap boundary and add
  service/data grouping view-model support over existing route-flow rows.
- PR 2: Add richer data/query/dependency and value-origin context rendering
  from already-joined route-flow evidence.
- PR 3: Add downgrade, missing-schema, truncation, redaction, and deterministic
  output hardening fixtures if PR 1 or PR 2 grows too large.
