# Route Flow Service/Data Composition Tasks

## Spec Delivery Tasks

- [x] 1. Draft public-safe Kiro spec files for issue 179.
  - [x] Add `requirements.md`.
  - [x] Add `design.md`.
  - [x] Add `tasks.md`.
  - [x] Add `implementation-state.md`.
  - [x] Add `review-prompts.md`.
  - [x] Keep wording generic for private validation, using labels such as
        "private legacy ASP.NET smoke sample" and "normalized API route".
  - [x] Avoid private local paths, private sample names, private route strings,
        raw SQL, raw config values, source snippets, secrets, and raw remotes.

- [x] 2. Review the spec before opening the PR.
  - [x] Run Opus Kiro review when local Kiro tooling is available, or document
        the blocker in `implementation-state.md`.
  - [x] Run Sonnet Kiro review when local Kiro tooling is available, or
        document the blocker in `implementation-state.md`.
  - [x] Patch Medium+ or blocking review findings, or document that none were
        reported.
  - [x] Run a self-review focused on evidence boundaries, redaction, and
        implementability.

- [x] 3. Validate the spec-only change.
  - [x] Run `git diff --check`.
  - [x] Run the private path guard if available.
  - [x] Run any repo spec lint/check script if present.
  - [x] Record validation results in `implementation-state.md`.

## Implementation Tasks

Task 4 must land before Tasks 5-11 begin. The rule catalog and emitted gap codes
are the contract that later implementation and tests validate.

- [x] 4. Extend route-flow rule catalog entries and limitations. Requirements: 4.
  - [x] Reuse existing `combined.route-flow.*` rules for selector, entry, path,
        interface bridge, logic surface, dependency surface, classification,
        redaction, and report envelope behavior.
  - [x] Add `combined.route-flow.argument-projection.v1`.
  - [x] Add `combined.route-flow.fact-symbol-projection.v1`.
  - [x] Extend `combined.route-flow.gap.v1` emits for any new gap codes.
  - [x] Add a test or catalog assertion that every emitted rule ID resolves and
        no parallel `route.flow.*` family is introduced.
  - [x] Document limitations for static composition, route reachability,
        runtime DI, dynamic dispatch, argument flow, query/data surfaces,
        reduced coverage, missing extractors, and redaction.

- [x] 5. Extend read-only combined evidence input projection. Requirements: 1,
      2, 5.
  - [x] Detect and read route entry evidence attached to controller or handler
        symbols.
  - [x] Read `combined_argument_flows` and project direct argument evidence into
        route-flow detail rows.
  - [x] Read `combined_fact_symbols` and project fact-to-symbol attachments into
        route-flow detail rows.
  - [x] Bound projection reads to selected route-flow caller/callee pairs and
        source-local symbols, with combined-index lookup indexes for new scans.
  - [x] Read call edges, object creations, parameter-forwarding edges, symbol
        relationships, `FactTypes.ObjectShapeInferred`,
        `FactTypes.QueryPatternDetected`, dependency/data facts in
        `combined_facts`, `combined_dependency_edges`, coverage metadata, and
        `AnalysisGap` facts.
  - [x] Remove or conditionally suppress the current present-but-unprojected
        `ExtractorUnavailable` gaps for `combined_argument_flows` and
        `combined_fact_symbols` once projection is active.
  - [x] Tolerate missing optional tables and emit availability gaps.
  - [x] Preserve source labels, source index IDs, scan IDs, commit SHAs,
        extractor identities, supporting fact IDs, supporting edge IDs, rule
        IDs, evidence tiers, file paths, and line spans.
  - [x] Keep SQLite inputs read-only.

- [x] 6. Compose route entry to downstream method evidence. Requirements: 2, 4,
      5.
  - [x] Select route-flow roots only from rule-backed route entry evidence
        attached to controller or handler methods.
  - [x] Traverse bounded static call, creation, argument, parameter-forward, and
        symbol edges from the route root.
  - [x] Emit downstream method detail rows with supporting evidence and coverage
        labels.
  - [x] Emit specific bridge gaps when service/repository/data facts exist but
        cannot be connected credibly from the route root.
  - [x] Avoid global short-name stitching and unsupported cross-source symbol
        merges.
  - [x] Add cycle safety, max depth, max row, and max frontier behavior using
        deterministic ordering.

- [x] 7. Handle interface calls and implementation candidates. Requirements:
      3, 4.
  - [x] Preserve interface method calls as explicit evidence rows.
  - [x] Use static implements/inherits/override/symbol relationships to find
        candidate implementation methods.
  - [x] Emit zero, one, or many candidate rows without claiming runtime binding.
  - [x] Continue downstream traversal through candidates only with a candidate
        classification cap.
  - [x] Emit `ImplementationCandidateUnavailable` and
        `AmbiguousImplementationCandidates` gaps where appropriate.
  - [x] Add tests for single candidate, multiple candidates, no candidate,
        syntax-only candidates, name-only candidates, and high fan-out.

- [x] 8. Attach object-shape, query-shape, repository, and data-surface rows.
      Requirements: 2, 4, 6.
  - [x] Attach object-shape and DTO/type facts through credible fact-symbol or
        method-symbol evidence.
  - [x] Attach query-shape facts through credible repository/data-access method
        evidence.
  - [x] Attach dependency/data-surface facts through rule-backed source-local
        symbol evidence.
  - [x] Render only safe descriptors, hashes, and labels for query/data/config
        evidence.
  - [x] Emit data-surface attachment gaps when downstream method evidence exists
        but terminal data evidence cannot be connected.

- [x] 9. Implement classification and gap labeling. Requirements: 1, 3, 4, 7.
  - [x] Classify rows using existing values: `StrongStaticRouteFlow`,
        `ProbableStaticRouteFlow`, `NeedsReviewStaticRouteFlow`,
        `NoRouteFlowEvidence`, and `UnknownAnalysisGap`.
  - [x] Treat reduced coverage as a coverage label or gap reason, not a summary
        classification.
  - [x] Cap classification by weakest evidence tier and coverage.
  - [x] Cap interface candidates, syntax-only evidence, name-only evidence,
        ambiguity, high fan-out, generated-code uncertainty, missing extractors,
        and reduced coverage.
  - [x] Distinguish no downstream evidence under complete coverage from unknown
        or reduced-coverage absence.
  - [x] Include rule IDs, evidence tiers, supporting IDs, coverage labels, and
        limitations in every result and gap.

- [x] 10. Extend Markdown and JSON output safely. Requirements: 5, 6.
  - [x] Extend the existing `route-flow-report.json` contract
        backward-compatibly, preserving `reportType = "route-flow"` and the
        existing version unless a future breaking schema spec changes it.
  - [x] Align Markdown with the existing route-flow renderer and add new rows to
        compatible sections or narrowly named subsections.
  - [x] Sort rows deterministically and generate stable row IDs.
  - [x] Use explicit nulls, empty arrays, and closed-set gap codes for missing
        values.
  - [x] Ensure wording says static evidence and avoids runtime proof claims.
  - [x] Add byte-stability tests for JSON output.
  - [x] Add tests proving projected argument/fact-symbol rows suppress the old
        present-but-unprojected `ExtractorUnavailable` gap for that route/table,
        while unjoinable rows emit the narrower projection-unavailable gap.
  - [x] Add or explicitly defer focused coverage for
        `combined_parameter_forward_edges` as a bridge in route-flow traversal.

- [x] 11. Enforce privacy and redaction. Requirements: 6, 7.
  - [x] Reuse shared safe path, hashing, display, and redaction helpers where
        practical.
  - [x] Add or extend report-output guards for local absolute paths, private
        sample names, private repository names, private route strings, raw SQL,
        raw config values, raw URLs, raw remotes, source snippets, connection
        strings, and secrets.
  - [x] Ensure logs do not echo unsafe selector or display values.
  - [x] Include `combined.route-flow.redaction.v1` in rows where unsafe values
        are hashed or omitted.
  - [x] Add negative privacy tests for Markdown, JSON, logs, and
        SQLite-derived display fields.

- [ ] 12. Validate implementation. Requirements: 7.
  - [x] Run `dotnet test` for the implementation slice.
  - [x] Run relevant pinned smoke checks from `docs/VALIDATION.md`, or document
        a deferral with rationale.
  - [x] Run `git diff --check`.
  - [x] Run the private path guard if available.
  - [x] Run any route-flow/reporting privacy guard or forbidden-wording tests.
  - [x] Run non-regression tests for existing route-flow CLI wiring, rule IDs,
        classifications, JSON report type/version, and Markdown compatibility.
  - [ ] Run an ignored local smoke against a private legacy ASP.NET smoke sample
        using generic labels only; do not commit private outputs.

## Suggested PR Boundaries

- PR 1: Rule catalog entries, route-flow schema/model constants, input reader
  compatibility, and projection of `combined_argument_flows` and
  `combined_fact_symbols`.
- PR 2: Route root to downstream method traversal, bridge gaps, and deterministic
  ordering.
- PR 3: Interface implementation candidates and candidate classification caps.
- PR 4: Object-shape, query-shape, repository/data-surface attachments, output
  rendering, redaction, tests, and smoke guidance.
