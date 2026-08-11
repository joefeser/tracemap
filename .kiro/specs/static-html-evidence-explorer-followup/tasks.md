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

- [x] 1. Confirm current explorer artifact contracts. Requirements: 1, 2, 4.
  - [x] Re-read `StaticHtmlEvidenceExplorer.cs`, its focused tests,
    `docs/STATIC_HTML_EVIDENCE_EXPLORER.md`, and existing explorer rule catalog
    entries on the implementation branch.
  - [x] Confirm supported, provenance-only, unsupported, and missing artifact
    families on current `origin/dev`.
  - [x] Confirm current safety profile aliases and claim-level labels.
  - [x] Decide whether the new ledger is additive under the current explorer
    schema version or requires a schema version bump; this spec requires a bump
    to `tracemap-static-html-evidence-explorer.v2`, and implementation must
    record the final schema/version update in `implementation-state.md` before
    task 2 starts.
  - [x] Confirm which conflict dimensions are backed by currently parsed
    artifact fields and which remain future hooks.
  - [x] Record implementation-scope decisions in this spec's
    `implementation-state.md`.

- [x] 2. Add a safe compatibility ledger model. Requirements: 1, 4, 5.
  - [x] Define additive ledger rows in `ExplorerData` or equivalent safe view
    data.
  - [x] Use closed compatibility statuses for rendered-compatible,
    compatible-empty, provenance-only, not-provided, unsupported-schema,
    unsupported-artifact, profile-incompatible, safety-omitted, partial, and
    compatible.
  - [x] Include rule ID, evidence tier, support IDs, coverage labels, safe
    scope, and limitations for every row.
  - [x] Use deterministic subject IDs from the design's closed conventions for
    artifact, section, safety-profile, and claim-level rows.
  - [x] Keep ledger labels and messages closed explorer-authored strings unless
    a future user-derived field is explicitly routed through safety validation.
  - [x] Sort rows deterministically with ordinal tie-breakers.
  - [x] Avoid raw paths, remotes, private names, scan directory names, raw
    snippets, SQL, config values, and secrets in ledger fields.

- [x] 3. Harden profile and claim-level conflict handling. Requirements: 2, 3,
  5.
  - [x] Normalize safety profile aliases through the existing public/demo or
    hidden/local paths.
  - [x] Detect explicit artifact claim-level or profile metadata only from
    compatible generated artifacts that expose safe structured fields.
  - [x] Treat missing claim metadata as unknown with a visible limitation when
    interpretation is affected, and do not emit a conflict row for unknown
    metadata alone.
  - [x] Implement real PR 1 conflict detection only for dimensions available
    from currently parsed artifacts, such as scan-manifest/facts commit SHA
    disagreement.
  - [x] Keep claim-level, profile, schema, and source-identity conflicts as
    forward-compatible hooks until compatible artifacts expose safe structured
    fields for them.
  - [x] Define the closed `conflictKind` vocabulary before emitting it and
    record the values in design docs or `implementation-state.md`.
  - [x] Stop, omit, or mark affected sections partial rather than silently
    merging incompatible artifacts.
  - [x] Keep safety profile names and claim-level names in separate namespaces;
    do not compare profile aliases directly to claim-level values.
  - [x] Keep diagnostics sanitized and rule-backed.

- [x] 4. Render deterministic ledger/navigation. Requirements: 1, 4, 5.
  - [x] Render ledger rows in HTML or enhance the existing Coverage table with
    equivalent detail.
  - [x] Mirror the same safe rows in `data/explorer-data.json`.
  - [x] Treat the ledger as additive to existing `sectionStatuses`, not a
    replacement.
  - [x] Update the existing pinned section-status order test if a new
    Compatibility Ledger section is added, or confirm it remains correct if
    ledger rows are folded into Coverage.
  - [x] Preserve no-JavaScript access to ledger rows, section statuses, gaps,
    limitations, rules, and the evidence-row baseline.
  - [x] Use stable anchors and deterministic navigation labels.
  - [x] Ensure unsupported/missing/provenance-only states do not read as
    evidence absence.

- [x] 5. Preserve generated artifact safety. Requirements: 3.
  - [x] Run post-render validation across HTML, CSS, JavaScript, JSON,
    manifest, README, generated paths, and diagnostics.
  - [x] Add tests proving public/demo output rejects or omits unsafe profile
    conflict data without printing raw values.
  - [x] Add tests proving hidden/local output is visibly labeled and no less
    safe for this slice.
  - [x] Add tests for downloadable or embedded data parity if new fields are
    added.

- [x] 6. Update rules and docs. Requirements: 2, 5, 6.
  - [x] Reuse existing explorer rules where their documented limitations cover
    the emitted rows.
  - [x] Add rule catalog entries before emitting any new rule ID, gap kind,
    limitation kind, redaction kind, or validation failure.
  - [x] If `explorer.input.provenance-conflict.v1` is reused for a subtype
    currently documented as deferred, update that rule's limitation text in
    `rules/rule-catalog.yml` before emitting the subtype.
  - [x] Add or update tests so emitted conflict kinds are not still documented
    as deferred in the rule catalog.
  - [x] Update `docs/STATIC_HTML_EVIDENCE_EXPLORER.md` with the compatibility
    ledger schema, statuses, profile conflict behavior, and validation
    expectations.

- [x] 7. Add focused tests. Requirements: 1, 2, 3, 4, 6.
  - [x] Test supported rendered artifact rows.
  - [x] Test provenance-only artifact rows.
  - [x] Test missing artifact rows.
  - [x] Test unsupported JSON rows.
  - [x] Test compatible-empty rows.
  - [x] Test the all-unknown claim metadata path: unknown metadata produces a
    limitation when relevant and does not emit a profile-incompatible,
    claim-level-conflict, or equivalent conflict row by itself.
  - [x] Test real PR 1 commit-conflict rows from currently parsed artifacts.
  - [x] Do not require profile-incompatible or claim-level conflict fixture
    tests in PR 1 unless implementation adds a compatible artifact fixture with
    safe structured metadata; otherwise pin the all-unknown no-conflict path.
  - [x] Test deterministic ordering of ledger rows, support IDs, section
    statuses, anchors, and downloadable data.
  - [x] Test no-JavaScript ledger inspectability in generated `index.html`.
  - [x] Test HTML and downloadable-data parity for ledger rows.
  - [x] Test scanner-only output does not contain forbidden impact/runtime
    wording except in explicit non-claim limitations.
  - [x] Test sanitized diagnostics and generated output safety for conflict
    inputs.

- [x] 8. Validate the implementation PR. Requirements: 3, 6.
  - [x] Run
    `dotnet test src/dotnet/TraceMap.sln --filter StaticHtmlEvidenceExplorerTests`.
  - [x] Run broader `dotnet test src/dotnet/TraceMap.sln` when shared helpers
    or rule/report contracts change, or explicitly record why focused tests are
    sufficient.
  - [x] Run a CLI/sample explorer smoke if rendering changed.
  - [x] Inspect the generated explorer smoke output directory directly, or use
    the explorer post-render validator, because `./scripts/check-private-paths.sh`
    checks tracked repository files rather than `/tmp` smoke artifacts.
  - [x] Run desktop and mobile browser sanity checks if JavaScript or browser
    behavior changed, or explicitly record deferral.
  - [x] Run `git diff --check`.
  - [x] Run `./scripts/check-private-paths.sh`.

## Recommended PR Slices

- [x] PR 1: Compatibility ledger, profile conflict hardening, docs, rule
  catalog updates only if needed, and focused tests.
- [x] PR 2: First richer supported report JSON compatibility reader:
  `release-review.json` v1.2 compatibility metadata only. Additional report
  families remain separate slices.
- [x] PR 3a: First surface/path reader: ordinary `paths-report.json` v1.0,
  preserving ordered static hops, closed surfaces, provenance, and public-safe
  wording. Other path families and reducer readers remain separate slices.
- [ ] PR 3b: Reducer readers, preserving reducer-only impact wording and
  public-safe validation.
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
