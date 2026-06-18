# Static HTML Evidence Explorer Tasks

Status: spec-ready
Readiness: ready-for-implementation
Public claim level: concept

## Spec-Only PR Scope

- [x] Add Kiro spec files under
  `.kiro/specs/static-html-evidence-explorer/`.
- [x] Keep this PR limited to the new spec folder and do not implement product
  code.
- [x] Run Kiro spec review with `claude-opus-4.8` or the best available Opus
  model, or record the exact unavailable-tool/model error in
  `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Patch Medium or higher spec-review findings and run at most two
  re-review cycles, choosing Sonnet for final re-review unless Opus is clearly
  needed.
- [x] Run spec PR validation checks: `git diff --check` and
  `./scripts/check-private-paths.sh` if present.

## Implementation Tasks

- [ ] 1. Confirm current artifact contracts. Requirements: 1, 2, 9.
  - [ ] Inventory current scan manifest, facts, SQLite index, Markdown report,
    JSON report, combined index/report, reducer output, and rule catalog
    schemas that can feed the explorer.
  - [ ] Record which artifacts carry repo/commit SHA, source labels, coverage
    labels, extractor versions, rule IDs, evidence tiers, snippet hashes, gaps,
    limitations, and reducer classifications.
  - [ ] Identify unsupported or optional artifact families and define their
    partial/unavailable/gap behavior.
  - [ ] Inventory the existing public/demo and docs-export safety policy as
    the source of truth for explorer redaction, omission, and rejection.
  - [ ] Derive closed vocabularies from existing TraceMap schemas, enums, rule
    catalog values, and report contracts where available.
  - [ ] Add initial rule catalog stubs for explorer-specific rule IDs needed
    by the first implementation slice.
  - [ ] Confirm the explorer does not alter the required `tracemap scan`
    output contract.

- [ ] 2. Design and implement explorer generation entry point. Requirements:
  1, 2, 9.
  - [ ] Choose the CLI command or option name according to existing CLI
    conventions.
  - [ ] Define safety profile selection, including `public-demo` and
    `hidden-local` behavior or the equivalent current TraceMap profile names.
  - [ ] Load generated TraceMap artifacts from an input artifact/report
    directory without reading live source repository files.
  - [ ] Write a local static output directory with `index.html`, local assets,
    explorer data, and an explorer manifest.
  - [ ] Preserve generated-file sentinel and collision behavior where current
    report exporters already provide it.
  - [ ] Add documentation that distinguishes the local generated explorer from
    the public `tracemap.tools` site.
  - [ ] Record the chosen command name, output layout, and safety profile
    selection in `implementation-state.md` and user-facing docs.

- [ ] 3. Build provenance reconciliation and manifest output. Requirements: 2,
  3, 4.
  - [ ] Create typed artifact/source records with artifact kind, safe label,
    content hash, schema version, claim level, coverage labels, commit SHA
    where safe, and source IDs.
  - [ ] Detect missing commit metadata, unsupported schema versions, provenance
    conflicts, and claim-level conflicts.
  - [ ] Emit rule-backed gaps or limitations for partial or incompatible input.
  - [ ] Add tests for supported inputs, unsupported inputs, missing commit
    metadata, conflicting provenance, and affected-section partial/stopped UI
    labels.

- [ ] 4. Build safe explorer view models. Requirements: 3, 4, 5, 6, 7.
  - [ ] Map sources, artifacts, surfaces, paths, reducer rows, gaps,
    limitations, rule catalog entries, and evidence rows into safe records.
  - [ ] Preserve stable IDs, rule IDs, evidence tiers, support IDs, coverage
    labels, claim level, extractor versions, file spans, snippet hashes, and
    limitations.
  - [ ] Normalize closed vocabularies for section names, surface kinds, gap
    kinds, limitation kinds, coverage labels, claim levels, and evidence tiers.
  - [ ] Emit gaps for unknown vocabulary values or unsupported input shapes
    rather than inventing display categories.

- [ ] 5. Render overview, sources, coverage, and artifacts. Requirements: 2,
  3, 4, 8.
  - [ ] Render `index.html` overview with claim level, coverage status,
    counts, redaction/omission totals, and section links.
  - [ ] Render source and artifact tables with safe labels, commit SHA,
    extractor versions, artifact IDs, coverage, gaps, limitations, and omitted
    counts.
  - [ ] Show partial/reduced/unsupported coverage near the top of the page and
    in affected sections.
  - [ ] Add tests for overview counts, source safety, artifact provenance, and
    JavaScript-disabled baseline content.
  - [ ] Add tests that render distinct UI wording for not-provided,
    unsupported, and no-evidence-under-credible-coverage states.

- [ ] 6. Render surfaces, paths, and reducer-backed results. Requirements: 5,
  6, 8.
  - [ ] Group surfaces by closed surface kind, safe display title, and stable
    ID.
  - [ ] Render path rows with deterministic hop order, edge kind, rule ID,
    evidence tier, support IDs, coverage labels, and limitations.
  - [ ] Render reducer-backed impact classifications only when reducer output
    exists and supporting rule/evidence fields are present.
  - [ ] Ensure scanner-only sections use static evidence, candidate, nearby
    evidence, path evidence, gap, or needs-review wording instead of impact
    claims.
  - [ ] Add tests that lower-tier or syntax-only evidence remains visibly weak
    and never promotes to definite impact.

- [ ] 7. Render gaps, limitations, rules, and evidence rows. Requirements: 6,
  8, 9.
  - [ ] Render gap and limitation sections with rule IDs, kinds, affected
    sections, source/artifact scope, evidence tier, coverage label, and claim
    effect.
  - [ ] Render rule catalog rows when available and catalog-unavailable gaps
    when not available.
  - [ ] Render evidence rows with rule ID, evidence tier, support ID, artifact
    ID, safe file span, snippet hash, extractor version, coverage labels, and
    limitations.
  - [ ] Add deterministic filtering/sorting over safe fields only.
  - [ ] Add tests for rule rendering, gap rendering, limitation rendering,
    stable row ordering, and safe search/filter fields.

- [ ] 8. Enforce safety profiles and no-network behavior. Requirements: 1, 4,
  7, 9.
  - [ ] Reuse existing public/demo and hidden/local safety policy where
    possible.
  - [ ] Validate generated HTML, attributes, embedded JSON, downloadable data,
    CSS, JavaScript, comments, source maps, manifests, and README text for
    unsafe values.
  - [ ] Ensure public/demo output rejects or omits raw snippets, raw SQL,
    config values, secrets, local absolute paths, raw remotes, raw endpoint
    addresses, raw query strings, hostnames, private names, analyzer logs, raw
    facts, and raw SQLite content.
  - [ ] Ensure hidden/local output is visibly labeled and records redaction,
    hash, category-only, and omission counts.
  - [ ] Add parity tests proving explorer safety behavior matches existing
    strict public/demo policy for the same unsafe input classes.
  - [ ] Add a safety-failure-path test that injects an unsafe value and asserts
    the command reports the safety rule ID and generated artifact path without
    printing the unsafe raw value.
  - [ ] Add downloadable-data and copy-text tests proving generated blobs and
    copied values are no less redacted than the visible UI.
  - [ ] Add embedded-data safety tests for fields that are present in JSON but
    not displayed in the visible UI.
  - [ ] Add no-network tests for HTML, CSS, JavaScript, fonts, images, source
    maps, and runtime browser behavior.

- [ ] 9. Ensure determinism, accessibility, and progressive enhancement.
  Requirements: 8, 9.
  - [ ] Use stable sorting, stable anchors, deterministic JSON serialization,
    deterministic asset names, normalized line endings, and no random IDs.
  - [ ] Keep wall-clock timestamps out of byte-stable files or isolate them in
    documented manifest fields.
  - [ ] Provide semantic HTML, headings, table captions, labels, visible focus
    states, and accessible text for evidence strength.
  - [ ] Keep overview, evidence tables, gaps, limitations, and rule IDs useful
    when JavaScript is disabled.
  - [ ] Add tests for byte-stable reruns across `index.html`, CSS,
    JavaScript, source maps if any, `explorer-manifest.json`,
    `explorer-data.json`, inline embedded JSON, line endings, and
    accessibility-relevant markup.
  - [ ] Add tests proving duplicated no-JavaScript HTML evidence and
    JavaScript data agree under the same safety profile.

- [ ] 10. Update rules, docs, and validation. Requirements: 7, 8, 9.
  - [ ] Add explorer-specific rule IDs and limitations to the rule catalog or
    equivalent rule docs.
  - [ ] Document CLI usage, output layout, manifest schema, supported inputs,
    safety profiles, deterministic ordering, partial coverage labels, and
    compatibility expectations.
  - [ ] Add public/demo fixture validation for safe claim levels and forbidden
    private/raw material.
  - [ ] Run focused explorer tests.
  - [ ] Run relevant .NET build/test checks.
  - [ ] Run `./scripts/check-private-paths.sh`.
  - [ ] Run `git diff --check`.

## Recommended PR Slices

- [ ] PR 1: CLI skeleton, artifact discovery, initial rule catalog stubs,
  safety-profile selection, provenance reconciliation, manifest schema, and
  no-network asset bundling.
- [ ] PR 2: Safe view models and overview/source/artifact rendering.
- [ ] PR 3: Surfaces, paths, reducer results, gaps, limitations, rules, and
  evidence rows. Public/demo claim-level output is not complete until the
  safety validation dependency in PR 4 lands, unless PR 3 also includes the
  relevant minimal safety gate.
- [ ] PR 4: Safety-profile validation, hidden/local redaction labeling, and
  public/demo fixture checks.
- [ ] PR 5: Determinism, accessibility, progressive enhancement, docs, and
  browser sanity validation.

## Deferred Follow-Ups

- Hosted explorer service or public web deployment.
- Public `tracemap.tools` site integration.
- External sharing workflow or artifact upload.
- Runtime telemetry ingestion or production observability integration.
- LLM summaries, embeddings, vector search, semantic search, or prompt-based
  classification.
- Source-snippet display beyond existing explicit hidden/local opt-in.
