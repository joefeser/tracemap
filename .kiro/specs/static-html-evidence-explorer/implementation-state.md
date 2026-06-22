# Static HTML Evidence Explorer Implementation State

Status: implemented-pr1-with-follow-ups
Readiness: follow-up-slices-available
Public claim level: concept

Post-promotion note: PR #231 landed the first static HTML evidence explorer
slice and PR #247 promoted it to `main`. Remaining unchecked tasks in this spec
are follow-up slices, not evidence that the first explorer slice is still
unmerged.

## Branch

Spec branch: `codex/spec-static-html-evidence-explorer`
Base: `origin/dev`

Implementation branch: `codex/implement-static-html-evidence-explorer`
Implementation base: `origin/dev` at `6168afa7` when the branch was created.

## Scope

This implementation branch contains the first local static HTML evidence
explorer slice. The feature generates a local browser artifact from existing
TraceMap outputs so reviewers can navigate supported sources, artifacts, gaps,
rules, limitations, and evidence rows.

The implementation must not add hosted services, hidden telemetry, live
backends, runtime code analysis, LLM calls, embeddings, vector databases, or
prompt-based classification.

## Claim Level

Selected level: `concept`.

Rationale: the explorer is proposed but not implemented or validated with
public-safe fixtures. The spec requires public/demo safety validation before
any stronger demo claim.

## Scope Decisions

- The explorer is a local generated artifact, not the public `tracemap.tools`
  site.
- The explorer renders existing generated TraceMap artifacts and does not
  rescan source code or derive new conclusions.
- Core evidence boundaries remain unchanged: claims require rule IDs, evidence
  tiers, support IDs, coverage labels where available, and visible limitations.
- Scanner-only facts and path evidence must not use impact wording. Impact
  labels are allowed only for reducer-backed rows with supporting evidence.
- Public/demo output remains strict. Hidden/local output may redact, hash,
  category-label, or omit values only with visible labeling and manifest
  counts.
- No raw snippets are rendered by default. Any future snippet display must be
  explicit, hidden/local, and recorded in the explorer manifest.
- The spec intentionally includes accessibility, JavaScript-disabled baseline,
  no-network assets, and byte-stability requirements because local demos and
  review sessions should not depend on external services.

## Spec Review Commands And Results

Planned commands:

- `node scripts/kiro-review.mjs --phase static-html-evidence-explorer --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
- `node scripts/kiro-review.mjs --phase static-html-evidence-explorer --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`

Results:

- `claude-opus-4.8` initial spec review:
  `node scripts/kiro-review.mjs --phase static-html-evidence-explorer --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with full coverage. Saved artifacts under
  `.tmp/kiro-reviews/static-html-evidence-explorer/2026-06-18T233536-940Z-spec-claude-opus-4.8.*`.
  Findings: 2 blocking and 4 important non-blocking items, plus suggested
  test and wording improvements. Patched safety wording, readiness consistency,
  manifest policy definitions, safety-policy reuse, closed-vocabulary source of
  truth, three absence-state tests, generated-site separation, data parity, and
  validation gaps.
- `claude-sonnet-4.6` initial spec review:
  `node scripts/kiro-review.mjs --phase static-html-evidence-explorer --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with reduced coverage because Kiro reported denied tool access.
  Saved artifacts under
  `.tmp/kiro-reviews/static-html-evidence-explorer/2026-06-18T233537-002Z-spec-claude-sonnet-4.6.*`.
  Findings: 4 blocking and 8 important non-blocking items. Patched rule catalog
  anchoring, review bookkeeping, endpoint-address wording, byte-stability test
  scope, provenance conflict policy, no-JavaScript baseline scope, safe-label
  derivation, safety-failure testing, safety slice ordering, and safety profile
  definition.

Re-review limit: at most two re-review cycles. Use `claude-sonnet-4.6` for the
final re-review unless Opus is clearly needed.

Follow-up re-review:

- `claude-sonnet-4.6` re-review:
  `node scripts/kiro-review.mjs --phase static-html-evidence-explorer --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with reduced coverage because Kiro reported denied tool access.
  Saved artifacts under
  `.tmp/kiro-reviews/static-html-evidence-explorer/2026-06-18T234122-308Z-re-review-claude-sonnet-4.6.*`.
  Findings: no blocking issues. Patched requested handoff clarifications for
  `facts.ndjson` raw-fact filtering, PR 1 rule-catalog gating, provenance
  conflict subtyping, and no-JavaScript row-threshold determinism.
- Final `claude-sonnet-4.6` re-review:
  `node scripts/kiro-review.mjs --phase static-html-evidence-explorer --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with reduced coverage because Kiro reported denied tool access.
  Saved artifacts under
  `.tmp/kiro-reviews/static-html-evidence-explorer/2026-06-18T234314-708Z-re-review-claude-sonnet-4.6.*`.
  Findings: no blocking issues. Patched final bookkeeping and minor
  cross-reference items: spec-review task status, source-map byte-stability
  wording, and manifest policy allowed-value cross-reference. No further Kiro
  re-review was run because the requested maximum of two re-review cycles had
  been reached.

## Validation

- `git diff --check` passed.
- `./scripts/check-private-paths.sh` passed: `Private path guard passed.`

## Oddities

- The feature overlaps with existing Markdown, vault, docs-export, report, and
  public demo workflows, but this spec keeps the HTML explorer local and
  generated rather than hosted or marketing-oriented.
- The future implementation may choose a command name during CLI design. The
  requirements define behavior and safety boundaries rather than forcing a
  specific command spelling.
- No `review-prompts.md` is used for this spec because the standard
  `scripts/kiro-review.mjs` prompt includes the four spec files and the
  requirements/design/tasks are self-contained.

## Follow-Ups

- Future implementation should choose the exact CLI surface and update this
  state file with validation results.
- Future implementation should update rule catalog documentation for any
  explorer-specific rule IDs that are added.
- PR 1 must not ship generated explorer output until explorer rule catalog
  stubs are present for any new explorer-specific gaps, limitations, or
  validation failures.
- If PR 1 reads `facts.ndjson` directly, it must treat fact values as raw
  evidence and route them through the existing safety policy before rendering
  or embedding them.

## Implementation PR 1 Slice

Selected slice: recommended PR 1.

Implemented command:

```text
tracemap explorer generate --input <artifact-dir> --out <explorer-output> [--safety-profile <public-demo|hidden-local>] [--force]
```

Output layout:

```text
index.html
assets/explorer.css
assets/explorer.js
data/explorer-manifest.json
data/explorer-data.json
README.md
```

Safety profile choices:

- Default profile is `public-demo`.
- `hidden-local` is accepted and visibly labeled; this first slice still uses
  the same conservative redacted/hash/omission path and records counts in the
  manifest.
- Repository identity policy is `commit-sha-only` when a safe commit SHA is
  available, otherwise `omitted-for-safety`.
- Generation timestamp policy is `omitted-deterministic`.

Supported first-slice inputs:

- `scan-manifest.json`: parsed for safe commit, coverage, and extractor
  provenance.
- `facts.ndjson`: parsed into safe evidence rows. Raw fact properties, source
  snippets, raw SQL/config values, raw remotes, and raw absolute paths are not
  embedded.
- `index.sqlite`: hashed as provenance only; raw SQLite content is not read or
  embedded.
- `report.md`: hashed as provenance only.
- Other top-level JSON artifacts: labeled unsupported with
  `explorer.input.unsupported-schema.v1` gaps.

Deferred beyond PR 1:

- Full combined index/report, reducer output, path, surface, and rule-catalog
  artifact readers.
- Claim-level conflict weakening beyond the implemented safety-profile and
  commit/schema compatibility checks.
- Public/demo fixture validation beyond focused unit and CLI smoke coverage.

Validation status:

- Focused explorer tests passed:
  `dotnet test src/dotnet/TraceMap.sln --filter StaticHtmlEvidenceExplorerTests`
  with 15 tests passing after PR review-loop fixes.
- Required .NET build passed:
  `dotnet build src/dotnet/TraceMap.sln` with 0 warnings and 0 errors.
- Required .NET tests passed:
  `dotnet test src/dotnet/TraceMap.sln` with 549 tests passing after PR
  review-loop fixes.
- Private-path guard passed:
  `./scripts/check-private-paths.sh` reported `Private path guard passed.`
- Whitespace check passed:
  `git diff --check`.
- CLI/sample smoke passed:
  `dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/modern-sample --out /tmp/tracemap-explorer-smoke/scan`
  followed by
  `dotnet run --project src/dotnet/TraceMap.Cli -- explorer generate --input /tmp/tracemap-explorer-smoke/scan --out /tmp/tracemap-explorer-smoke/explorer`.
  The smoke wrote 6 explorer files with 4 artifacts, 27 evidence rows, and 1
  explicit gap for raw SQLite content not rendered.
- Browser sanity passed via a temporary local static server and Playwright CLI:
  `http://127.0.0.1:8765/index.html` rendered the overview, sources,
  artifacts, gaps, rules, and evidence rows with no console errors.

Kiro implementation review status:

- Initial implementation review:
  `node scripts/kiro-review.mjs --phase static-html-evidence-explorer --kind implementation --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with reduced coverage because Kiro reported denied tool access.
  Artifacts:
  `.tmp/kiro-reviews/static-html-evidence-explorer/2026-06-20T181509-274Z-implementation-claude-sonnet-4.6.*`.
  Patched Medium+ findings around generated-file collision handling,
  no-network validation, task status, hidden-local optional artifact labels,
  coverage wording, missing-manifest rule IDs, and additional focused tests.
- First re-review:
  `node scripts/kiro-review.mjs --phase static-html-evidence-explorer --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with reduced coverage because Kiro reported denied tool access.
  Artifacts:
  `.tmp/kiro-reviews/static-html-evidence-explorer/2026-06-20T182058-413Z-re-review-claude-sonnet-4.6.*`.
  Patched remaining concrete findings for coverage fallthrough, no-evidence
  wording, force/user-file behavior, and absence-state tests.
- Second and final re-review:
  `node scripts/kiro-review.mjs --phase static-html-evidence-explorer --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with reduced coverage because Kiro reported denied tool access.
  Artifacts:
  `.tmp/kiro-reviews/static-html-evidence-explorer/2026-06-20T182605-887Z-re-review-claude-sonnet-4.6.*`.
  Patched final concrete findings for rule-backed omission of raw fact
  properties and manifest identity, independent post-render validator tests,
  and narrowed no-network regex behavior. No further Kiro re-review was run
  because the requested maximum of two re-review cycles had been reached.

PR status:

- Ready PR opened against `dev`: https://github.com/joefeser/tracemap/pull/231
- Initial pushed implementation commit: `11cdfedd`.
- Bookkeeping commit recording PR URL: `3ce25972`.
- First PR review-loop fix commit: `1bf9c4bc`.
- Second PR review-loop manifest/evidence fix commit: `16613eab`.
- Bookkeeping commit recording the second review-loop fix: `46a0fc9d`.
- PR review-loop result for `46a0fc9d`: `merge_ready`, `canMerge: true`.
  Required gates were clean by configured policy: no unresolved review threads,
  no pending or failed checks, merge state `CLEAN`, required Codex review
  completed on the current head, Qodo actionable findings dispositioned with
  evidence, and optional Gemini/Sourcery reviews absent only as residual risk.

## Implementation Follow-Up Slice: Section Status And Safety Rendering

Branch: `codex/implement-static-html-explorer-followup`
Base: `origin/dev`
Selected slice: narrow PR 2/PR 4 crossover follow-up for richer local explorer
rendering and safety visibility.

Scope completed:

- Added rule-backed `ExplorerSectionStatus` rows to `explorer-data.json`.
- Rendered a new no-JavaScript `Coverage` table in `index.html` for
  deterministic section availability labels.
- Rendered a new `Safety & Redactions` table in `index.html` using existing
  safe redaction rows.
- Added `explorer.render.section-status.v1` to the rule catalog.
- Recorded omitted scan manifest branch, solution, and project identity fields
  as safe omission/redaction rows.
- Added a visible `explorer.input.provenance-conflict.v1` limitation for the
  deferred claim-level conflict detection subtype.
- Renamed the production generated-output safety validator from the
  test-oriented name to `ValidateGeneratedFilesForSafety`.
- Documented the `public-demo` safety profile versus `public-safe` claim-level
  vocabulary in `docs/STATIC_HTML_EVIDENCE_EXPLORER.md`.

Kiro implementation review:

- Initial implementation review:
  `node scripts/kiro-review.mjs --phase static-html-evidence-explorer --kind implementation --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with reduced coverage because Kiro reported denied tool access.
  Artifacts:
  `.tmp/kiro-reviews/static-html-evidence-explorer/2026-06-21T200430-696Z-implementation-claude-sonnet-4.6.*`.
  Patched blocking findings for the generated-output validator naming and the
  nullable legacy artifact evidence-span boundary. Also patched scanner-version
  redaction accounting and validator local-path coverage.
- First re-review:
  `node scripts/kiro-review.mjs --phase static-html-evidence-explorer --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with reduced coverage because Kiro reported denied tool access.
  Artifacts:
  `.tmp/kiro-reviews/static-html-evidence-explorer/2026-06-21T201024-265Z-re-review-claude-sonnet-4.6.*`.
  Patched the task 3 bookkeeping blocker by splitting implemented provenance
  checks from deferred claim-level conflict detection.
- Second and final re-review:
  `node scripts/kiro-review.mjs --phase static-html-evidence-explorer --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with reduced coverage because Kiro reported denied tool access.
  Artifacts:
  `.tmp/kiro-reviews/static-html-evidence-explorer/2026-06-21T201414-603Z-re-review-claude-sonnet-4.6.*`.
  Patched the remaining concrete findings for omitted scan manifest
  branch/solution/project identity fields and visible claim-level conflict
  detection deferral. No further Kiro re-review was run because the requested
  maximum of two re-review cycles had been reached.

Validation:

- Focused explorer tests passed:
  `dotnet test src/dotnet/TraceMap.sln --filter StaticHtmlEvidenceExplorerTests`
  with 16 passing tests.
- Required .NET build passed:
  `dotnet build src/dotnet/TraceMap.sln` with 0 errors and existing
  `SQLitePCLRaw.lib.e_sqlite3` advisory warnings.
- Required .NET tests passed:
  `dotnet test src/dotnet/TraceMap.sln` with 585 passing tests.
- Private-path guard passed:
  `./scripts/check-private-paths.sh`.
- Whitespace check passed:
  `git diff --check`.
- Sample smoke passed:
  `dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/modern-sample --out <tmp>/scan`
  followed by
  `dotnet run --project src/dotnet/TraceMap.Cli -- explorer generate --input <tmp>/scan --out <tmp>/explorer`.
  The smoke verified `index.html`, `data/explorer-data.json`, the `Coverage`
  section, `Safety & Redactions`, `sectionStatuses`,
  `not-rendered-in-current-slice`, and the
  `claim-level-conflict-detection-deferred` limitation.

PR status:

- Ready PR opened against `dev`: https://github.com/joefeser/tracemap/pull/264
- Initial pushed follow-up commit: `a67e50e8`.
- Bookkeeping commit recording PR URL: `8f39ad8d`.
- PR loop for `8f39ad8d` returned `merge_ready` in 33 seconds with merge
  state `CLEAN`, unresolved threads `0`, pending checks `0`, failed checks
  `0`, actionable bot findings `0`, Qodo returned, and Codex satisfied by the
  configured `trustedCodeReview` quorum as residual medium risk.

Follow-ups still intentionally deferred:

- Full combined index/report, reducer output, path, surface, and rule-catalog
  artifact readers.
- Claim-level conflict detection across multiple compatible structured
  artifacts.
- Comprehensive public/demo fixture parity tests, copy/download affordance
  tests, and browser accessibility/no-JavaScript validation.
- Broader hostname and Unix absolute path classification beyond the existing
  generated-output safety validator categories.
