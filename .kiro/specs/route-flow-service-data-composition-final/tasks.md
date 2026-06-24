# Route Flow Service/Data Composition Final Tasks

Status: ready-for-implementation

## Spec Delivery Tasks

- [x] 1. Draft the final route-flow service/data composition spec packet.
  - [x] Add `requirements.md`.
  - [x] Add `design.md`.
  - [x] Add `tasks.md`.
  - [x] Add `implementation-state.md`.
  - [x] Add `review-prompts.md`.
  - [x] Keep this PR limited to this spec folder.
  - [x] Keep examples synthetic/public-safe and avoid private paths, private
        labels, raw SQL/config values, source snippets, secrets, raw remotes,
        and private route strings.

- [x] 2. Review and patch the spec.
  - [x] Run Kiro Opus spec review or record the exact unavailable-tool/model
        evidence.
  - [x] Run Kiro Sonnet spec review or record the exact unavailable-tool/model
        evidence.
  - [x] Patch Medium+ actionable findings.
  - [x] Run one bounded Sonnet or Opus re-review after patches.
  - [x] Record review commands, artifacts, coverage, findings, dispositions,
        and final readiness in `implementation-state.md`.

- [x] 3. Validate the spec-only change.
  - [x] Run `git diff --check`.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Run any obvious spec/docs validation command if present, or record that
        none exists.
  - [x] Confirm the diff is limited to
        `.kiro/specs/route-flow-service-data-composition-final/`.
  - [x] Record validation results in `implementation-state.md`.

## Next Implementation PR Tasks

Current state: product implementation has not started for this final spec.
Implementation tasks below are sequenced across the Suggested PR Boundaries
section, not intended as one giant product PR. Do not check them off in this
spec-only PR unless live audit during a future implementation proves they are
already complete on `dev`.

- [x] 4. Audit live route-flow composition state. Requirements: 1.
      Suggested boundary: PR 1 prerequisite.
  - [x] Inspect `CombinedRouteFlowReport`, route-flow tests, route-flow rule
        catalog entries, selector/root handling, context groups, bridge states,
        gap constants, and the requested predecessor specs to confirm ownership
        boundaries.
  - [x] Map each required final-slice behavior to existing code or an unchecked
        implementation task.
  - [x] Confirm which gap codes in the design taxonomy already exist under
        `combined.route-flow.gap.v1` in `rules/rule-catalog.yml` and which
        require a rule-catalog limitation update before first use.
  - [x] Verify emitted top-level JSON field names against the live
        `RouteFlowReport` serializer before treating the field list in this
        spec as confirmed.
  - [x] Record the selected implementation scope in this spec's
        `implementation-state.md`.
  - [x] Confirm no new command, report type, JSON version, traversal engine, or
        rule namespace is needed.

- [ ] 5. Complete endpoint/root method to service call stitching. Requirements:
      2, 5, 6.
      Suggested boundary: PR 1.
  - [ ] Stitch endpoint/root methods to direct service/helper/repository calls
        only through source-local symbol, graph node, fact, or edge identity.
  - [ ] Avoid same-file, directory, short-name, display-name, textual, or route
        string joins.
  - [ ] Emit `MissingCallEdge` or an existing equivalent gap when full-coverage
        call-like evidence cannot stitch from the selected root.
  - [ ] Emit reduced/unknown gaps when coverage, schema, extractor, identity,
        or commit evidence prevents a clean conclusion.
  - [x] Add focused duplicate-root coverage proving ambiguous normalized route
        roots emit a deterministic selector gap and downgrade the report.
  - [ ] Add tests for direct service calls, no direct call under full coverage,
        no direct call under reduced coverage, duplicate roots, cycles, and
        deterministic ordering.

- [ ] 6. Harden implementation-candidate continuation. Requirements: 3, 5.
      Suggested boundary: PR 2.
  - [ ] Reuse source-local `combined_symbol_relationships` evidence for
        implementation candidates.
  - [ ] Continue through a single compiler-backed candidate only as review-tier
        static evidence.
  - [ ] Emit deterministic multiple-candidate, no-candidate, high-fan-out,
        syntax-only, name-only, cross-source, cross-language, runtime-binding,
        and reduced-coverage gaps.
  - [ ] Prove candidate bridges cannot produce `StrongStaticRouteFlow`.
  - [ ] Add tests proving no runtime DI target, service locator, factory,
        reflection, or dynamic dispatch proof is claimed.

- [ ] 7. Complete service/data/query/dependency attachment precision.
      Requirements: 4, 5, 6.
      Suggested boundary: PR 3.
  - [ ] Attach service, repository, object/projection, query-shape, SQL-shape,
        legacy-data, package/config, HTTP client, WCF, ASMX/SOAP, remoting,
        event/message, storage, validation/guard, serializer/contract,
        async/callback, and flow-boundary facts only through selected route-flow
        evidence.
  - [ ] Render argument-flow and parameter-forward value-origin rows only when
        joined to selected static route-flow rows.
  - [ ] Render fact-symbol context only for selected source-local symbols.
  - [ ] Emit `ArgumentProjectionUnavailable`,
        `FactSymbolProjectionUnavailable`, `DataSurfaceAttachmentMissing`, or
        shipped equivalents for adjacent but unjoinable facts.
  - [ ] Add tests for attached surfaces, unjoinable surfaces, attached versus
        path-context labeling, and deterministic stable IDs.

- [ ] 8. Enforce coverage, classification, and gap downgrade behavior.
      Requirements: 5.
      Suggested boundary: PR 1 for rows touched by Task 5; otherwise PR 4.
  - [ ] Require full relevant route-flow coverage for
        `StrongStaticRouteFlow`.
  - [ ] Require full relevant route-flow coverage and no unresolved bridge,
        projection, identity, schema, extractor, selector, reduced-coverage, or
        truncation gaps for `NoRouteFlowEvidence`.
  - [ ] Ensure `UnknownAnalysisGap` wins over clean absence when coverage,
        schema, extractor, identity, commit, generated-code, unsupported-shape,
        or truncation evidence blocks a credible conclusion.
  - [ ] Cap syntax-only, textual, name-only, dynamic, ambiguous,
        high-fan-out, candidate, generated-code uncertain, unjoined, and
        reduced-coverage rows at review-tier or unknown.
  - [ ] Assert Tier3-only stitched downstream rows cannot satisfy
        `StrongStaticRouteFlow`.
  - [ ] Ensure every emitted gap includes rule ID, evidence tier,
        classification, coverage, safe scope, supporting IDs where available,
        file span where available, and limitations.

- [ ] 9. Preserve deterministic JSON/Markdown compatibility. Requirements: 6.
      Suggested boundary: PR 1 for changed fields; otherwise PR 4.
  - [ ] Preserve `reportType = "route-flow"` and JSON `version = "1.0"`.
  - [ ] Add only backward-compatible fields or collections.
  - [ ] Use explicit `null`, empty arrays, and closed-set placeholders for
        unavailable values.
  - [ ] Sort rows, context groups, metadata maps, and gaps deterministically.
  - [ ] Add byte-stable JSON and Markdown-ordering tests.
  - [ ] Add `--exit-code` regression tests for review/unknown/no-evidence
        classifications and error-precedence behavior if this slice touches
        summary or classification rollup.
  - [ ] Add tests for any emitted `classificationCap` field and context-group
        weakest classification/tier/coverage rollup.
  - [ ] Add rule-catalog resolution tests for every emitted route-flow rule ID.

- [ ] 10. Enforce safety and public-safe validation. Requirements: 7, 8.
      Suggested boundary: PR 3 for attached rows; otherwise PR 4.
  - [ ] Reuse safe rendering/hash helpers for route selectors, source labels,
        file paths, SQL/query metadata, config/package metadata, URLs, remotes,
        snippets, and secret-like values.
  - [ ] Cite `combined.route-flow.redaction.v1` when unsafe values are hashed
        or omitted.
  - [ ] Assert redacted or hashed rows cite `combined.route-flow.redaction.v1`
        where the report model supports row-level or supporting rule IDs.
  - [ ] Add Markdown, JSON, log, and fixture-metadata negative tests for raw
        SQL, raw config, URLs/query strings, connection strings, secrets,
        snippets, local absolute paths, raw remotes, hostnames, private labels,
        and private route values.
  - [ ] Run focused route-flow tests, full .NET build/test, private-path guard,
        `git diff --check`, and relevant `docs/VALIDATION.md` pinned smokes or
        record explicit deferrals.
  - [ ] Record any private legacy smoke as local-only generic evidence, with no
        private paths, names, routes, SQL/config values, snippets, secrets,
        remotes, or generated private outputs.

## Suggested PR Boundaries

- PR 1: Task 4 plus Task 5, with Task 8/9/10 coverage only for the fields,
  classifications, and safety behavior touched by endpoint/root-to-direct-call
  stitching.
- PR 2: harden implementation-candidate continuation and candidate downgrade
  tests from Task 6, plus any directly affected Task 8/9 checks.
- PR 3: complete attached versus unjoinable service/data/query/dependency/
  value-origin rows from Task 7 and the directly affected Task 10 safety
  matrix.
- PR 4: final downgrade/truncation/schema/extractor/public-smoke coverage if
  PR 1-3 would otherwise grow too large, especially remaining Task 8/9/10
  checks not tied to an earlier product change.

## Deferred Follow-Ups

- Runtime DI/container binding proof.
- Runtime execution, branch feasibility, symbolic execution, taint/mutation
  analysis, SQL execution, row-count, or production traffic proof.
- Batch query files, HTML graph visualization, persisted derived rows, site
  copy, and public marketing claims.
- Scanner/language-adapter expansion unless a future spec explicitly selects
  that scope.
- LLM calls, embeddings, vector databases, semantic search, fuzzy matching, or
  prompt-based classification.
