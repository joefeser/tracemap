# Static HTML Evidence Explorer Follow-Up Tasks

Status: spec-ready
Readiness: implementation-ready
Public claim level: hidden

## Spec-Only PR Scope

- [x] Create `.kiro/specs/static-html-evidence-explorer-followup/`.
- [x] Draft `requirements.md`.
- [x] Draft `design.md`.
- [x] Draft `tasks.md`.
- [x] Draft `implementation-state.md`.
- [x] Draft `review-prompts.md`.
- [x] Run Kiro spec review with `claude-opus-4.8`, or record the exact
  command/status/artifact/blocker in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6`, or record the exact
  command/status/artifact/blocker in `implementation-state.md`.
- [x] Patch Medium+ actionable spec-review findings. Patch Low findings only
  when narrow and safe.
- [x] Run one bounded re-review if feasible after patching and record the
  result in `implementation-state.md`.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Confirm the diff is limited to
  `.kiro/specs/static-html-evidence-explorer-followup/`.

## Implementation Tasks

- [ ] 1. Confirm current explorer artifact contracts. Requirements: 1, 2, 4.
  - [ ] Re-read `StaticHtmlEvidenceExplorer.cs`, its focused tests,
    `docs/STATIC_HTML_EVIDENCE_EXPLORER.md`, and existing explorer rule catalog
    entries on the implementation branch.
  - [ ] Confirm supported, provenance-only, unsupported, and missing artifact
    families on current `origin/dev`.
  - [ ] Confirm current safety profile aliases and claim-level labels.
  - [ ] Decide whether the new ledger is additive under the current explorer
    schema version or requires a schema version bump; this spec requires a bump
    to `tracemap-static-html-evidence-explorer.v2`, and implementation must
    record the final schema/version update in `implementation-state.md` before
    task 2 starts.
  - [ ] Confirm which conflict dimensions are backed by currently parsed
    artifact fields and which remain future hooks.
  - [ ] Record implementation-scope decisions in this spec's
    `implementation-state.md`.

- [ ] 2. Add a safe compatibility ledger model. Requirements: 1, 4, 5.
  - [ ] Define additive ledger rows in `ExplorerData` or equivalent safe view
    data.
  - [ ] Use closed compatibility statuses for rendered-compatible,
    compatible-empty, provenance-only, not-provided, unsupported-schema,
    unsupported-artifact, profile-incompatible, safety-omitted, partial, and
    compatible.
  - [ ] Include rule ID, evidence tier, support IDs, coverage labels, safe
    scope, and limitations for every row.
  - [ ] Use deterministic subject IDs from the design's closed conventions for
    artifact, section, safety-profile, and claim-level rows.
  - [ ] Keep ledger labels and messages closed explorer-authored strings unless
    a future user-derived field is explicitly routed through safety validation.
  - [ ] Sort rows deterministically with ordinal tie-breakers.
  - [ ] Avoid raw paths, remotes, private names, scan directory names, raw
    snippets, SQL, config values, and secrets in ledger fields.

- [ ] 3. Harden profile and claim-level conflict handling. Requirements: 2, 3,
  5.
  - [ ] Normalize safety profile aliases through the existing public/demo or
    hidden/local paths.
  - [ ] Detect explicit artifact claim-level or profile metadata only from
    compatible generated artifacts that expose safe structured fields.
  - [ ] Treat missing claim metadata as unknown with a visible limitation when
    interpretation is affected, and do not emit a conflict row for unknown
    metadata alone.
  - [ ] Implement real PR 1 conflict detection only for dimensions available
    from currently parsed artifacts, such as scan-manifest/facts commit SHA
    disagreement.
  - [ ] Keep claim-level, profile, schema, and source-identity conflicts as
    forward-compatible hooks until compatible artifacts expose safe structured
    fields for them.
  - [ ] Define the closed `conflictKind` vocabulary before emitting it and
    record the values in design docs or `implementation-state.md`.
  - [ ] Stop, omit, or mark affected sections partial rather than silently
    merging incompatible artifacts.
  - [ ] Keep safety profile names and claim-level names in separate namespaces;
    do not compare profile aliases directly to claim-level values.
  - [ ] Keep diagnostics sanitized and rule-backed.

- [ ] 4. Render deterministic ledger/navigation. Requirements: 1, 4, 5.
  - [ ] Render ledger rows in HTML or enhance the existing Coverage table with
    equivalent detail.
  - [ ] Mirror the same safe rows in `data/explorer-data.json`.
  - [ ] Treat the ledger as additive to existing `sectionStatuses`, not a
    replacement.
  - [ ] Update the existing pinned section-status order test if a new
    Compatibility Ledger section is added, or confirm it remains correct if
    ledger rows are folded into Coverage.
  - [ ] Preserve no-JavaScript access to ledger rows, section statuses, gaps,
    limitations, rules, and the evidence-row baseline.
  - [ ] Use stable anchors and deterministic navigation labels.
  - [ ] Ensure unsupported/missing/provenance-only states do not read as
    evidence absence.

- [ ] 5. Preserve generated artifact safety. Requirements: 3.
  - [ ] Run post-render validation across HTML, CSS, JavaScript, JSON,
    manifest, README, generated paths, and diagnostics.
  - [ ] Add tests proving public/demo output rejects or omits unsafe profile
    conflict data without printing raw values.
  - [ ] Add tests proving hidden/local output is visibly labeled and no less
    safe for this slice.
  - [ ] Add tests for downloadable or embedded data parity if new fields are
    added.

- [ ] 6. Update rules and docs. Requirements: 2, 5, 6.
  - [ ] Reuse existing explorer rules where their documented limitations cover
    the emitted rows.
  - [ ] Add rule catalog entries before emitting any new rule ID, gap kind,
    limitation kind, redaction kind, or validation failure.
  - [ ] If `explorer.input.provenance-conflict.v1` is reused for a subtype
    currently documented as deferred, update that rule's limitation text in
    `rules/rule-catalog.yml` before emitting the subtype.
  - [ ] Add or update tests so emitted conflict kinds are not still documented
    as deferred in the rule catalog.
  - [ ] Update `docs/STATIC_HTML_EVIDENCE_EXPLORER.md` with the compatibility
    ledger schema, statuses, profile conflict behavior, and validation
    expectations.

- [ ] 7. Add focused tests. Requirements: 1, 2, 3, 4, 6.
  - [ ] Test supported rendered artifact rows.
  - [ ] Test provenance-only artifact rows.
  - [ ] Test missing artifact rows.
  - [ ] Test unsupported JSON rows.
  - [ ] Test compatible-empty rows.
  - [ ] Test the all-unknown claim metadata path: unknown metadata produces a
    limitation when relevant and does not emit a profile-incompatible,
    claim-level-conflict, or equivalent conflict row by itself.
  - [ ] Test real PR 1 commit-conflict rows from currently parsed artifacts.
  - [ ] Do not require profile-incompatible or claim-level conflict fixture
    tests in PR 1 unless implementation adds a compatible artifact fixture with
    safe structured metadata; otherwise pin the all-unknown no-conflict path.
  - [ ] Test deterministic ordering of ledger rows, support IDs, section
    statuses, anchors, and downloadable data.
  - [ ] Test no-JavaScript ledger inspectability in generated `index.html`.
  - [ ] Test HTML and downloadable-data parity for ledger rows.
  - [ ] Test scanner-only output does not contain forbidden impact/runtime
    wording except in explicit non-claim limitations.
  - [ ] Test sanitized diagnostics and generated output safety for conflict
    inputs.

- [ ] 8. Validate the implementation PR. Requirements: 3, 6.
  - [ ] Run
    `dotnet test src/dotnet/TraceMap.sln --filter StaticHtmlEvidenceExplorerTests`.
  - [ ] Run broader `dotnet test src/dotnet/TraceMap.sln` when shared helpers
    or rule/report contracts change, or explicitly record why focused tests are
    sufficient.
  - [ ] Run a CLI/sample explorer smoke if rendering changed.
  - [ ] Inspect the generated explorer smoke output directory directly, or use
    the explorer post-render validator, because `./scripts/check-private-paths.sh`
    checks tracked repository files rather than `/tmp` smoke artifacts.
  - [ ] Run desktop and mobile browser sanity checks if JavaScript or browser
    behavior changed, or explicitly record deferral.
  - [ ] Run `git diff --check`.
  - [ ] Run `./scripts/check-private-paths.sh`.

## Recommended PR Slices

- [ ] PR 1: Compatibility ledger, profile conflict hardening, docs, rule
  catalog updates only if needed, and focused tests.
- [ ] PR 2: Richer supported report JSON compatibility readers, one artifact
  family at a time.
- [ ] PR 3: Surface/path/reducer readers, preserving reducer-only impact
  wording and public-safe validation.
- [ ] PR 4: Browser accessibility and no-JavaScript validation expansion.

## Deferred Follow-Ups

- Public `tracemap.tools` integration.
- Hosted explorer service, external sharing workflow, artifact upload, or
  remote browser app.
- Full SQLite relationship browsing.
- Broad visual redesign or graph visualization.
- Runtime telemetry ingestion, production observability, or runtime proof.
- LLM summaries, embeddings, vector search, semantic search, prompt-based
  classification, or AI impact analysis.
- Raw snippet display beyond an explicit hidden/local opt-in governed by a
  future spec.
