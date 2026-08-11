# Static HTML Evidence Explorer Follow-Up Implementation State

Status: implemented-pr1
Readiness: pr-ready
Public claim level: hidden

## Branch

Spec branch: `codex/static-html-evidence-explorer-followup`
Base: `origin/dev`
Base SHA checked for this spec: `6bec000244340311cc385e4ebdeee4655a7251d4`

## Scope

This is a spec-only PR for a future generated static HTML evidence explorer
implementation slice. It must only change files under:

```text
.kiro/specs/static-html-evidence-explorer-followup/
```

Selected future implementation slice:

```text
Explorer compatibility ledger and safety profile conflict hardening.
```

The slice is for generated static evidence explorer artifacts, not the public
`tracemap.tools` site.

## Current Context Notes

- The predecessor spec `.kiro/specs/static-html-evidence-explorer/` is
  `implemented-pr1-with-follow-ups`.
- The first implementation slice and several follow-ups are already reflected
  in live code on current `origin/dev`.
- Live code currently supports local explorer generation from selected
  generated TraceMap artifacts and renders safe overview, coverage, sources,
  artifacts, gaps, limitations, safety/redactions, rules, and evidence rows.
- Current code already treats `index.sqlite` and `report.md` as
  provenance-only, other unknown JSON as unsupported, and compatible
  `rule-catalog.yml` as a bounded catalog input.
- Claim-level conflict detection across multiple compatible structured
  artifacts is still documented as deferred in predecessor state and docs.

## Claim Level

Selected level: `hidden`.

Rationale: this follow-up is a local generated-artifact safety/profile
hardening spec and does not create a public site or demo claim. It should stay
hidden until implemented and validated against public-safe generated fixtures.

## Scope Decisions

- Keep the PR 1 implementation slice narrow: compatibility ledger plus
  safety/profile conflict hardening.
- Do not add surface, path, reducer, SQLite, or broad report JSON readers in
  PR 1.
- Do not change scanner/reducer/language-adapter behavior.
- Do not touch `site/` or create a `site-*` spec.
- Preserve public/demo strictness and hidden/local labeling.
- Preserve no raw snippets, raw SQL, config values, secrets, local absolute
  paths, raw remotes, raw URLs, hostnames, private labels, or generated scan
  directory names in public/demo output.
- Require rule catalog entries and documented limitations before any new
  explorer rule/gap/limitation/validation ID is emitted.

## Spec Review Commands And Results

Planned commands:

- `node scripts/kiro-review.mjs --phase static-html-evidence-explorer-followup --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
- `node scripts/kiro-review.mjs --phase static-html-evidence-explorer-followup --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`

Results:

- `claude-opus-4.8` initial spec review:
  `node scripts/kiro-review.mjs --phase static-html-evidence-explorer-followup --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with reduced coverage because Kiro reported denied tool access.
  Artifacts:
  `.tmp/kiro-reviews/static-html-evidence-explorer-followup/2026-06-27T165018-586Z-spec-claude-opus-4.8.*`.
  Findings: 2 blocking issues and several important/missing-test items.
  Patched PR 1 conflict-dimension scope, forward-compatible claim/profile
  hooks, rule-catalog deferred-limitation update requirements,
  profile-vs-claim namespace separation, ledger/sectionStatus relationship,
  unknown-claim tests, no-JavaScript ledger tests, wording-denylist tests, and
  HTML/downloadable parity tests.
- `claude-sonnet-4.6` initial spec review:
  `node scripts/kiro-review.mjs --phase static-html-evidence-explorer-followup --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with full coverage.
  Artifacts:
  `.tmp/kiro-reviews/static-html-evidence-explorer-followup/2026-06-27T165018-609Z-spec-claude-sonnet-4.6.*`.
  Findings: 3 blocking issues and several non-blocking items. Patched the
  schema-version decision gate, deterministic ledger subject ID conventions,
  current-artifact claim metadata limits, closed conflict vocabulary task,
  generated smoke safety wording, status field scoping, section-order test
  update, and ledger safe-label/message constraints.

Re-review plan:

- Patch Medium+ actionable findings.
- Patch Low findings only when narrow and safe.
- Run one bounded re-review if feasible and record the exact command, status,
  artifact path, and outcome here.

Re-review results:

- `claude-sonnet-4.6` re-review:
  `node scripts/kiro-review.mjs --phase static-html-evidence-explorer-followup --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with reduced coverage because Kiro reported denied tool access.
  Artifacts:
  `.tmp/kiro-reviews/static-html-evidence-explorer-followup/2026-06-27T165500-875Z-re-review-claude-sonnet-4.6.*`.
  Findings: 2 blocking issues and several non-blocking clarifications. Patched
  by removing the ambiguous ledger `available` status, choosing a required v2
  schema bump for the top-level compatibility ledger, qualifying requirement
  language for PR 1 conflict dimensions, clarifying unknown claim/profile rows,
  tightening section-order test requirements, and documenting generated smoke
  output safety scope.

## Validation

Planned spec PR validation:

- `git diff --check`
- `./scripts/check-private-paths.sh`
- Confirm diff limited to
  `.kiro/specs/static-html-evidence-explorer-followup/`

Results:

- `git diff --cached --check` passed.
- Initial `./scripts/check-private-paths.sh` failed because
  `implementation-state.md` recorded the local worktree path. Patched the note
  to use a generic placeholder.
- Final `./scripts/check-private-paths.sh` passed:
  `Private path guard passed.`
- Confirmed staged diff is limited to
  `.kiro/specs/static-html-evidence-explorer-followup/`.

## Oddities

- The working checkout had another agent's Swift spec branch and untracked
  files. This work was moved to a separate worktree at
  `<tracemap-static-html-evidence-explorer-followup-worktree>`
  to avoid touching unrelated changes.
- The predecessor spec still contains implementation follow-up notes for older
  branches and PRs. This spec uses current live code on `origin/dev` as the
  authority before defining the next slice.
- The branch was refreshed after `origin/dev` advanced to
  `6bec000244340311cc385e4ebdeee4655a7251d4` (`Spec Swift adapter scaffold
  output contract (#395)`).

## Follow-Ups

- Future implementation should update this state file with branch, exact code
  scope, validation, Kiro implementation review results, PR URL, and PR-loop
  outcome.
- Future implementation should apply this spec's conservative schema decision:
  adding a top-level compatibility ledger requires bumping the explorer schema
  to `tracemap-static-html-evidence-explorer.v2` and updating docs/tests in the
  same implementation PR.
- Future implementation should avoid new rule IDs unless existing explorer
  rules cannot accurately describe the new ledger/conflict rows.

## Implementation PR 1 — Compatibility Ledger

Branch: `codex/static-html-explorer-compatibility-ledger`

Starting point:

- Base: fresh `origin/dev`
- Base SHA: `f38172a86975da9fd0c8c5b9b3111834b78b1bb1`
- The base includes the completed `main` promotion fixes synchronized back to
  `dev` by PR #634.

Implemented scope:

- Bumped newly generated explorer manifest and data contracts to
  `tracemap-static-html-evidence-explorer.v2`.
- Preserved safe `--force` replacement of a prior v1 TraceMap-generated bundle
  by recognizing only the closed v1/v2 generated-manifest markers.
- Added a deterministic `compatibilityLedger` safe view model and matching
  no-JavaScript HTML table after Coverage.
- Added artifact rows for rendered-compatible, compatible-empty,
  provenance-only, not-provided, unsupported-schema, unsupported-artifact, and
  partial states.
- Added section rows without replacing or reinterpreting the existing
  `sectionStatuses` contract.
- Added one closed selected-output safety-profile row and one
  `claim-level:unknown` row. Current compatible inputs expose no independent
  claim/profile field, so unknown metadata remains a limitation and never
  manufactures a conflict.
- Preserved the existing real `commit-conflict` dimension and projects it into
  a partial fact-artifact ledger row backed by
  `explorer.input.provenance-conflict.v1`.
- Added `explorer.render.compatibility-ledger.v1` before emitting ledger rows,
  with explicit compatibility-only limitations and safe-ID requirements.
- Did not add source scanning, SQLite readers, report readers, reducer logic,
  site changes, remote assets, runtime claims, LLM behavior, or new extraction.

Closed vocabularies used by this slice:

- Subject kinds: `artifact`, `section`, `safety-profile`, `claim-level`.
- Compatibility statuses: `rendered-compatible`, `compatible-empty`,
  `provenance-only`, `not-provided`, `unsupported-schema`,
  `unsupported-artifact`, `profile-incompatible`, `safety-omitted`, `partial`,
  `compatible`. The last two future statuses remain reserved and are not
  emitted without compatible structured profile metadata.
- Production conflict kind: `commit-conflict`. Claim-level, input-profile,
  source-identity, and structured schema conflicts remain future hooks.

Validation:

- Focused `StaticHtmlEvidenceExplorerTests`: 25/25 passed after the v2 upgrade
  and compatibility-ledger tests.
- `dotnet build src/dotnet/TraceMap.sln --no-restore`: passed with 0 warnings
  and 0 errors.
- A first concurrent full-test invocation overlapped the build and logged one
  transient assembly-copy retry. The build remained green; an unchanged
  sequential rerun provided the authoritative result below.
- `dotnet test src/dotnet/TraceMap.sln --no-build --no-restore`: 1,367/1,367
  passed.
- Targeted `dotnet format --verify-no-changes`: passed for the reporting and
  focused test files.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- CLI/sample smoke: scanned `samples/modern-sample` with 27 facts and semantic
  coverage, then generated a six-file public/demo explorer bundle. The data
  contract was v2 and contained 16 deterministic ledger rows spanning all four
  subject kinds. Generated-output safety validation passed as part of the
  command.
- Direct smoke inspection confirmed only the expected generated HTML, local
  CSS/JavaScript, manifest/data JSON, and README files; the HTML and JSON both
  contained the expected ledger states and no source input was modified.
- Desktop (1440x1000) and mobile (390x844) Playwright snapshots rendered the
  Compatibility Ledger and existing sections with zero console warnings or
  errors. Network inspection showed only three local static requests:
  `index.html`, `assets/explorer.css`, and `assets/explorer.js`.

Initial PR review follow-up:

- ACK authorized three related exact-head threads: Qodo found that partial
  section ledger rows dropped their concrete gap IDs and that the unknown
  claim-metadata limitation used the generic partial-section rule; Codex
  independently confirmed the rule/limitation mismatch.
- The patch now attributes the unknown claim-metadata limitation and ledger row
  to `explorer.render.compatibility-ledger.v1` and projects sorted concrete gap
  IDs into partial section rows. The commit-conflict test pins both artifact and
  affected-section traceability.
- Post-patch focused tests first remained 24/24, full tests remained 1,366/1,366,
  targeted formatting passed, private-path validation passed, and
  `git diff --check` passed.
- A fresh Codex review then found that an empty fact stream without manifest
  provenance could be projected as `compatible-empty` despite its
  `missing-commit-facts` gap. Gap-backed partial state now takes precedence over
  empty-compatible state for section rows, and a focused regression pins both
  artifact and section gap traceability. Focused coverage is now 25/25 and the
  full suite is 1,367/1,367.

## Implementation PR 2 — Release Review Compatibility Reader

Branch: `codex/static-html-explorer-release-review-reader`

Starting point:

- Base: fresh `origin/dev`
- Base SHA: `0d4fc027446f5f2c0e3b0b34fb7cdbb780ffd5a7`
- The base includes merged PR #635 and explorer schema v2.

Selected artifact family:

- `release-review.json` only.
- Accepted identity: `reportType: release-review`, version `1.2`.
- Accepted modes: `ReleaseReviewSingleV1` and `ReleaseReviewCombinedV1`, with
  matching single/combined before/after snapshot kinds.

Implemented boundary:

- The reader validates exact report identity/version, mode, before/after side
  and index kind, closed `Full`/`Reduced` coverage values, non-empty source
  collections, valid-or-null source commit identities, a boolean truncation
  field, and a non-negative summary gap count.
- Required properties must occur exactly once. Malformed JSON, duplicate
  required properties, unsupported versions/modes, inconsistent snapshots,
  invalid commit identities, and inputs above the 16 MiB reader limit become
  sanitized `explorer.input.unsupported-schema.v1` gaps.
- Compatible artifacts contribute a deterministic SHA-256 content identity,
  `release-review/1.2` schema label, closed coverage labels, and a
  rule-backed compatibility limitation.
- The reader does not read or render finding bodies, source labels, paths,
  messages, metadata, checklist text, SQL, or reducer conclusions.
- No explorer schema bump is required because this slice populates the
  existing v2 artifact and compatibility-ledger contracts without adding
  top-level fields.
- Added `explorer.input.release-review.v1` to code and the rule catalog before
  emitting its limitation.

Deferred:

- Dependency, route-flow, property-flow, reverse, impact, portfolio, snapshot,
  and other report JSON families.
- Release-review finding, surface, path, reducer-result, checklist, and
  priority rendering; those belong to separately bounded PR 3 readers.
- Any runtime, production, release-approval, deployment-safety, or complete
  analysis claim.

Validation:

- Focused `StaticHtmlEvidenceExplorerTests`: 34/34 passed after adding public
  and hidden compatibility, unsupported-version, duplicate-property,
  invalid-commit, oversized-input, rule-catalog, no-leak, determinism, exact
  source binding, commit-mismatch, and missing-authority coverage.
- Locked dependency restore and solution build passed with 0 warnings and 0
  errors.
- Full .NET solution: 1,376/1,376 passed.
- Targeted `dotnet format --verify-no-changes`: passed.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- Real CLI smoke: scanned `samples/modern-sample` with 27 facts, generated a
  same-snapshot release review with 4 explicit gaps, and generated a six-file
  public/demo explorer bundle. The explorer accepted the report as
  `release-review/1.2`, recorded its SHA-256 identity, emitted four closed
  coverage labels, and retained the content-not-rendered limitation.
- Direct generated-output inspection found no local/private paths, URLs, raw
  SQL, credentials, or release-review body values.
- Browser revalidation was deferred because this slice changes no HTML, CSS,
  JavaScript, navigation, or interaction code; it only adds rows through the
  already browser-validated v2 artifact and compatibility-ledger renderers.

Review follow-up:

- ACK authorized one Qodo performance finding and one Codex P2 provenance
  finding on exact head `3c185b9b3ac163fba4c53692a3519a0528332f65`.
- Oversized inputs now stop before reading when file length already exceeds the
  bound, with a max-plus-one byte fallback for concurrent growth. They use the
  closed `unavailable:artifact-too-large` placeholder instead of hashing to
  end-of-file.
- The reader now retains validated after-snapshot commits and binds the report
  to `source:scan-output` only when they include the authoritative usable scan
  manifest commit or single unambiguous fact-stream commit. Mismatch and
  missing authority emit sanitized rule-backed gaps and leave the report
  unbound and partial.
- Post-fix build remained clean, focused tests passed 34/34, full tests passed
  1,376/1,376, and the real CLI smoke confirmed exact source binding for the
  matching same-snapshot report.
- A fresh exact-head Codex review found two additional P2 contract defects.
  Manifest-less fact streams now establish commit authority only when every
  fact carries the same usable commit; mixed usable/unusable provenance emits
  explicit missing-commit and source-association gaps and leaves the release
  review unbound. Release-review artifacts and compatibility rows now reference
  the exact emitted `limitation:release-review-content-not-rendered` identity.
- The second focused review regression run passed 35/35; the full .NET suite
  passed 1,377/1,377, and targeted formatting, private-path, and diff checks
  remained clean.

## Implementation PR 3a — Static Dependency Paths Reader

Branch: `codex/static-html-explorer-paths-reader`

Starting point:

- Base: fresh `origin/dev`
- Base SHA: `e7b649150a56c158071dbc24d9b8f3086f4786c8`
- The base includes merged PR #636 and explorer schema v2.

Selected artifact family:

- Ordinary `paths-report.json` version `1.0` only.
- Legacy-flow schema/view variants, route-flow, reverse, property-flow,
  release-review details, and reducer results remain separate readers.

Implemented boundary:

- The bounded reader requires exact version and ordinary schema identity,
  usable and unique source commits, matching summary counts, closed path
  classifications/confidence, contiguous ordered node/edge topology, closed
  edge and surface kinds, usable rule IDs and evidence tiers, and valid spans.
- The reader is bounded at 32 MiB, 1,000 sources, 1,000 paths, and 10,000 hops;
  over-limit artifacts remain unsupported rather than producing an unbounded
  generated HTML/data bundle.
- Compatible reports add safe static `surfaces` and ordered `paths`/hop rows.
  Stable report IDs, source-index IDs, node IDs, fact IDs, and edge IDs are
  projected as hashed explorer-local identities. File evidence uses the
  existing safe repository-path policy.
- Query selectors, source labels, display symbols, surface names, SQL text,
  notes, free-text gap messages, and free-text limitations are not rendered.
- The reader copies existing static classifications only. It does not create
  reducer results or claim runtime reachability, execution, production use,
  impact, release safety, or complete analysis.
- Adding top-level `surfaces` and `paths` arrays advances the explorer schema
  to `tracemap-static-html-evidence-explorer.v3`; generated v1/v2 bundles stay
  recognized for guarded `--force` replacement.
- Added `explorer.input.paths-report.v1` to code and the rule catalog before
  emitting reader limitations.

Deferred:

- Route-flow report compatibility, because that contract flattens flow rows
  and does not retain path boundaries suitable for lossless path projection.
- Reducer-backed impact rows and every other report family.
- Raw SQLite graph browsing and any graph visualization or ranking.

Validation:

- Locked dependency restore passed.
- Solution build passed with 0 warnings and 0 errors.
- Focused `StaticHtmlEvidenceExplorerTests`: 42/42 passed.
- Full .NET solution: 1,384/1,384 passed on the authoritative unchanged
  sequential run. Two earlier runs each exposed a different unrelated
  diagnostic-harness flake; both failing tests passed independently before the
  clean full-suite result.
- Targeted `dotnet format --verify-no-changes`: passed.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- Real two-source CLI smoke scanned a TypeScript endpoint client and .NET
  endpoint server, combined 221 facts, and produced an ordinary v1.0 paths
  report with 12 paths, 60 hops, 4 terminal surfaces, and 45 upstream gaps.
  The explorer rendered schema v3 with all 12 paths and 4 surfaces, retained
  the reduced static coverage, and added only rule-backed compatibility gaps.
- Direct generated-output inspection found no local/private paths, raw SQL,
  URLs, credentials, connection strings, or private-source labels.
- Upstream free-text gap kinds and messages are also omitted; retained report
  gaps use the closed `paths-report-gap` explorer category with their safe rule,
  tier, coverage, and hashed support evidence.
- Desktop/mobile in-app browser validation was attempted because this slice
  changes navigation and HTML. The browser runtime rejected the local
  `file://` artifact under its URL policy; no alternate route was used. The
  generated HTML remains covered by focused structural/no-JavaScript tests,
  but interactive viewport validation is explicitly deferred.

## Implementation PR 3b — Contract-Delta Reducer Reader

Branch: `codex/static-html-explorer-reducer-reader`

Starting point:

- Base: fresh `origin/dev`
- Base SHA: `4dee1473bad3cf6afbdbc7a39a41c7fb59852164`
- The base includes merged PR #638 and explorer schema v3.

Selected artifact family:

- `impact-report.json` with report type `contract-delta-impact-single` or
  `contract-delta-impact-combined`, version `2.0`, and reducer algorithm
  `contract-delta-fact-match/2.0` only.
- SQL impact, combined-change-impact, route-flow, package, and other report or
  reducer families remain separate bounded readers.

Implemented boundary:

- The reader validates bounded source/result/evidence/gap counts, summary
  agreement, unique identities, exact reducer identity, closed classifications
  and confidence values, rule IDs, evidence tiers, commit provenance, and
  valid spans. Unsupported shapes fail closed.
- Compatible reports produce safe reducer-result rows plus linked evidence
  rows. Upstream classifications are preserved; the explorer does not execute
  reduction or create new impact conclusions.
- Finding/change labels, reasons, warnings, references, source labels, scan
  IDs, symbols, evidence metadata, gap text, and free-text limitations are
  omitted. Raw identities are hashed and paths use the existing safe-path
  projection.
- The reader is bounded at 32 MiB, 1,000 sources, 1,000 results, 10,000
  evidence rows, and 1,000 gaps.
- Populating reducer results advances the generated explorer schema to
  `tracemap-static-html-evidence-explorer.v4`; v1-v3 generated bundles remain
  recognized for guarded replacement.
- Added `explorer.input.contract-delta-impact.v1` to code and the rule catalog
  with explicit static-evidence limitations.

Deferred:

- Every reducer/report family outside contract-delta impact v2.
- Running the reducer, rescanning source, SQLite graph browsing, runtime
  evidence, risk ranking, and inferred impact.
- PR 4 browser accessibility and no-JavaScript expansion beyond the current
  deterministic baseline.

Validation:

- Locked dependency restore: passed.
- Solution build: passed with 0 warnings and 0 errors.
- Focused `StaticHtmlEvidenceExplorerTests`: 58/58 passed after review fixes.
- Full .NET solution tests: passed.
- Targeted `dotnet format`: passed.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- Real CLI smoke scanned `samples/modern-sample` with full semantic coverage,
  wrote 27 facts, reduced two changes into two findings with five evidence
  rows and no reducer gaps, and generated a schema-v4 explorer. The explorer
  rendered both reducer rows and all five linked reducer evidence rows while
  retaining full static coverage and the exact reducer algorithm identity.
- Desktop (1440x900) and mobile (390x844) local HTTP browser checks passed:
  the reducer navigation target, heading, and two result rows were present,
  body width remained bounded to the viewport, and no browser warnings or
  errors were reported.
- Review fixes admit the producer's valid `Partial` coverage state, keep that
  state distinct from `Reduced`, and prevent `ReducerNotTruncated` from
  matching the aggregate truncation predicate. `Partial` also implies the
  reducer output was truncated even when the producer summary flag remains
  false. Coverage-relative no-evidence
  results retain the reducer artifact as support without inventing source-fact
  evidence; result rows continue to link every available location/provenance
  row by evidence ID.

## Implementation PR 4 — Accessibility And No-JavaScript Validation

Branch: `codex/static-html-explorer-accessibility`

Starting point:

- Base: fresh `origin/dev`
- Base SHA: `e088f8a5fc4c635bb1cd1877f39b92978f147876`
- The base includes the complete compatibility, report-reader, path-reader,
  and reducer-reader slices through explorer schema v4.

Implemented boundary:

- Added a keyboard-visible skip link targeting the explorer's main evidence
  content and preserved stable section anchors and ordering.
- Added an explicit no-JavaScript notice while retaining every evidence,
  coverage, compatibility, gap, limitation, rule, and empty-state table in the
  static HTML baseline.
- Assigned stable IDs and closed safe labels to each filterable table. Local
  progressive enhancement now creates associated search labels, `aria-controls`
  relationships, and polite live visible-row counts.
- Marked real filter targets separately from coverage-relative empty rows, so
  filtering cannot conceal why a table has no compatible evidence.
- Retained the first 200 deterministic evidence rows in HTML and the complete
  safe row set in `data/explorer-data.json`; regression coverage pins both sides
  of that contract.
- Added narrow-viewport spacing and touch-target rules without changing the
  explorer data schema, evidence contracts, rule IDs, or public claim boundary.

Deferred:

- Broad visual redesign, graph visualization, hosted sharing, remote assets,
  and full SQLite browsing remain outside this completed follow-up runway.

Validation:

- Locked dependency restore: passed.
- Focused `StaticHtmlEvidenceExplorerTests`: 60/60 passed.
- Solution build: passed with 0 warnings and 0 errors.
- Full .NET solution tests: passed.
- Targeted `dotnet format --verify-no-changes`: passed.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- Real CLI smoke scanned `samples/modern-sample` with full semantic coverage,
  wrote 27 facts, and generated the six-file local explorer bundle with all 27
  safe evidence rows.
- Desktop (1440x900) and mobile (390x844) local HTTP browser checks passed:
  four distinct filters were available, the evidence count updated from 27 to
  0 for a non-match, empty coverage rows remained visible, page width stayed
  bounded, mobile inputs and navigation links measured 44 pixels, no remote
  assets were requested, and no browser warnings or errors were reported.
- The generated skip link, focusable main target, and no-JavaScript notice are
  pinned by focused structural tests. The in-app browser did not dispatch its
  synthetic Tab/fragment gesture reliably, so no automated keyboard-navigation
  success is claimed beyond those semantic contracts.
- Review fix: the focusable main target retains an explicit inset focus outline
  after skip-link navigation; focused tests pin that the generated CSS never
  suppresses focus outlines.
