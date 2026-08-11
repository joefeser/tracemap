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
