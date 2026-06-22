# Site TraceMap Tools Release Review Boundary Implementation State

Status: implementation-complete
Readiness: ready-for-owner-merge
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-release-review-boundary`

Base: `origin/dev`

Target PR base: `dev`

Implementation branch: `codex/impl-site-release-review-boundary`

Implementation PR: `https://github.com/joefeser/tracemap/pull/291`

Worktree: isolated worktree requested by the operator. The machine-local path
is not repeated in this committed spec state to keep repository docs free of
private local path material.

Scope: implement the public-site release-review boundary surface from this
spec, keep the public claim level at `concept`, and add focused validation for
the route and its public-copy boundaries.

Out of scope: generated `site/dist`, generated `site/output`, scanner code,
reducer code, release automation, runtime telemetry, AI/LLM analysis,
embeddings, vector databases, prompt classification, generated evidence
artifacts, raw facts, raw SQLite content, analyzer logs, and release approval
or safety claims.

## Current State

Initial packet files were created for spec review:

- `requirements.md`
- `design.md`
- `tasks.md`
- `implementation-state.md`
- `review-packet.md`

The packet is intentionally future-facing. It does not implement a public page
or validation scripts in this branch.

## Claim-Level Decision

Selected public claim level: `concept`.

Rationale: the future surface explains release-review boundaries and owner
handoffs. It does not publish new demo evidence, prove a TraceMap capability,
approve a release, prove release safety, prove runtime behavior, prove
deployment success, or replace release controls.

Future implementation must include visible `Public claim level: concept` and
visible `No public conclusion without evidence`.

## Placement Guidance

Candidate placements:

- `/release-review-boundary/`
- `/review-room/release-boundary/`
- Section on `/limitations/`
- Section on `/static-vs-runtime/`

Future implementation must record the selected placement and rejected
alternatives before changing site source.

Rejected-by-default replacements:

- `/limitations/`: site-wide boundaries.
- `/static-vs-runtime/`: runtime telemetry distinction.
- `/review-claim-checklist/`: repeatability checklist.
- `/deploy-audit/`: deployment-adjacent audit boundary.
- `/validation/`: validation evidence boundary.
- `/manager-packet/`: manager-facing evidence story.
- `/questions/objections/`: skeptical objection handling.

The future release-review boundary surface must remain a static-evidence
release-review handoff, not a release gate, approval system, safety proof,
deploy audit, validation proof, runtime workflow, checklist replacement,
manager packet, or objection guide.

## Scope Decisions

- Required files are limited to this spec directory.
- Initial header state started as `Status: not-started`, readiness for spec
  review, and `Public claim level: concept`. Current committed headers are
  `Status: not-started`, `Readiness: ready-for-implementation`, and
  `Public claim level: concept`.
- `Readiness` may move to `ready-for-implementation` only after spec review
  findings are handled or dispositioned.
- Future implementation tasks stay unchecked in `tasks.md`.
- Future page copy must visibly state `Public claim level: concept`.
- Future page copy must visibly state `No public conclusion without evidence`.
- The release-boundary matrix must include changed source surface,
  package/config surface, route/endpoint adjacency, SQL/data surface,
  coverage gap, validation evidence, runtime telemetry need, and
  release-owner decision rows.
- The matrix must route each row to a required next owner using role
  categories rather than private names.
- No raw facts, SQLite, analyzer logs, source snippets, SQL, config values,
  secrets, local paths, raw remotes, generated scan directories, private
  sample names, command output, hidden validation details, or
  credential-like values may appear in future public content.
- No blame language may appear in future public content.

## Review Commands

Planned spec review commands:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-release-review-boundary --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-release-review-boundary --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

If a model or tool is unavailable, record the exact command and error here.

## Review Results

Initial Kiro review commands:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-release-review-boundary --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-release-review-boundary --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- `claude-opus-4.8`: command completed with reduced coverage because Kiro
  reported denied tool access. Artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-release-review-boundary/2026-06-22T040104-367Z-spec-claude-opus-4.8.clean.md`.
  Meta recorded `toolDenied: true`, `reviewComplete: true`, and
  `reviewCoverage: "Reduced"`.
- `claude-sonnet-4.6`: command completed with reduced coverage because Kiro
  reported denied tool access. Artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-release-review-boundary/2026-06-22T040104-500Z-spec-claude-sonnet-4.6.clean.md`.
  Meta recorded `toolDenied: true`, `reviewComplete: false`, and
  `reviewCoverage: "Reduced"`.

Review findings and dispositions:

- Opus Medium: the design matrix used `database owner role` and
  `incident/release owner`, which were outside the canonical public role
  categories. Patched the matrix to use existing role categories:
  `service owner`, `security owner`, `runtime observability owner`, and
  `release owner`.
- Opus Medium and Sonnet Medium: the 900 to 2,400 word-count validation bound
  excluded `row-field label headers` without naming what that meant. Patched
  requirements, design, and tasks to clarify that only release-boundary matrix
  column header labels are excluded; row names and data-cell values count.
- Sonnet Low: placement decision and placement recording tasks were not
  explicitly sequenced before site edits. Patched tasks to say both happen
  before changing site source.
- Sonnet Low: browser sanity did not name a viewport convention. Patched
  requirements, design, and tasks to use existing site browser-check patterns
  when available, otherwise record one wide desktop and one narrow mobile
  viewport.
- Opus Low: supporting-route dependency risk remains an implementation-time
  sequencing risk. Requirements, design, and tasks already require link
  verification, substitution, deferral, or blocking before publishing.
- Opus Low: validation must avoid false positives on the legitimate
  validation evidence row and `/validation/` link. Requirements and design
  already allow bounded validation-evidence contexts and forbid only
  release-strength positive claims.

Re-review commands:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-release-review-boundary --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-release-review-boundary --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- `claude-opus-4.8` re-review: command completed with reduced coverage
  because Kiro reported denied tool access. Artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-release-review-boundary/2026-06-22T040445-673Z-re-review-claude-opus-4.8.clean.md`.
  Meta recorded `toolDenied: true`, `reviewComplete: false`, and
  `reviewCoverage: "Reduced"`.
- `claude-sonnet-4.6` re-review: command completed with reduced coverage
  because Kiro reported denied tool access. Artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-release-review-boundary/2026-06-22T040445-862Z-re-review-claude-sonnet-4.6.clean.md`.
  Meta recorded `toolDenied: true`, `reviewComplete: false`, and
  `reviewCoverage: "Reduced"`.

Re-review findings and dispositions:

- Opus Medium: the design matrix still used non-canonical role labels
  `TraceMap owner`, `build/tooling owner`, `reviewer`, and an unqualified
  validation row `reviewer`. Patched those cells to use canonical public role
  categories: `TraceMap site owner`, `build or tooling owner`,
  `code reviewer`, `test owner`, `service owner`, and `release owner`.
- Sonnet Medium: the design stop conditions did not use the exact
  `absence-of-impact proof` phrase. Patched the stop-condition list to name
  absence-of-impact proof explicitly.
- Sonnet Medium: the word-count exclusion rule named only example column
  headers. Patched requirements, design, and tasks to exclude all
  release-boundary matrix column header row cells.
- Opus Low/Medium: the word-count upper bound remains an implementation-time
  feasibility risk. Disposition: requirements, design, and tasks retain the
  `unless amended in implementation-state.md` escape hatch and now force row
  content to count, so future implementation must either write tightly or
  record an evidence-backed amendment.
- Sonnet Low: future implementers should verify `./scripts/check-private-paths.sh`
  exists before checking validation complete. Disposition: spec-branch
  validation runs this script, and implementation tasks require rerunning it
  before the task is checked.
- Sonnet Low: all five headers should move together when readiness changes.
  Disposition: after successful re-review, update `Readiness` in all five
  files in the same patch.

Second re-review commands:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-release-review-boundary --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-release-review-boundary --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- `claude-opus-4.8` second re-review: command completed with reduced coverage
  because Kiro reported denied tool access. Artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-release-review-boundary/2026-06-22T040813-126Z-re-review-claude-opus-4.8.clean.md`.
  Meta recorded `toolDenied: true`, `reviewComplete: true`, and
  `reviewCoverage: "Reduced"`. No Medium or higher content findings.
- `claude-sonnet-4.6` second re-review: command completed with reduced
  coverage because Kiro reported denied tool access. Artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-release-review-boundary/2026-06-22T040813-181Z-re-review-claude-sonnet-4.6.clean.md`.
  Meta recorded `toolDenied: true`, `reviewComplete: true`, and
  `reviewCoverage: "Reduced"`. No Medium or higher findings.

Second re-review findings and dispositions:

- Opus process caveat: automated review coverage remained reduced because
  Kiro reported denied tool access. Disposition: accepted as residual review
  coverage risk because both requested models ran, all review artifacts and
  metadata are recorded, Medium findings were patched, and the final
  re-review outputs reported no Medium or higher content findings.
- Opus Low: `review-packet.md` still said review had not run. Patched review
  packet status to summarize the reduced-coverage review and re-review
  outcome.
- Opus Low: `/review-room/` was only in supporting-route guidance, not the
  relationship section. Patched requirements to distinguish `/review-room/`
  as meeting context when present.
- Sonnet Low: validation should hard-fail dead supporting routes with no
  recorded substitution or deferral. Patched requirements, design, and tasks
  to make that failure mode explicit.

Readiness decision: `ready-for-implementation`. Medium and higher findings
were patched and re-reviewed where feasible. Remaining risk is reduced Kiro
review coverage due to tool-denied analysis gaps recorded above.

## Implementation Results

Final placement: standalone route `/release-review-boundary/`.

Rejected alternatives:

- `/review-room/release-boundary/`: too nested for a cross-surface boundary
  page and harder to discover from non-review-room contexts.
- Section on `/limitations/`: would blur the route's release-owner handoff
  purpose with site-wide limitations.
- Section on `/static-vs-runtime/`: would collapse release-review ownership
  into the runtime/static distinction.

The selected route does not replace `/limitations/`, `/static-vs-runtime/`,
`/review-claim-checklist/`, `/deploy-audit/`, `/validation/`,
`/manager-packet/`, or `/questions/objections/`. It links to those adjacent
surfaces to help readers choose the right evidence boundary.

Navigation decision: no primary navigation entry was added. Discovery uses
sitemap and route metadata plus contextual inbound links from `/review-room/`
and `/use-cases/change-review/`.

Implemented files:

- `site/src/release-review-boundary/index.html`
- `site/src/_site/pages.json`
- `site/src/_site/discovery.json`
- `site/src/review-room/index.html`
- `site/src/use-cases/change-review/index.html`
- `site/scripts/release-review-boundary.mjs`
- `site/scripts/release-review-boundary.test.mjs`
- `site/scripts/validate.mjs`
- `site/scripts/validate.test.mjs`

Claim-boundary decisions:

- Public claim level remains `concept`.
- The page visibly states `Public claim level: concept`.
- The page visibly states `No public conclusion without evidence`.
- Release-boundary matrix rows stay bounded to static evidence contribution,
  gaps, next-owner routing, and stop conditions.
- Public copy does not claim release approval, release safety, operational
  safety, production proof, runtime behavior proof, endpoint performance
  proof, deployment success proof, absence-of-impact proof, complete coverage,
  AI/LLM analysis, or replacement of release controls or human judgment.

Validation results:

```bash
git diff --check
./scripts/check-private-paths.sh
cd site && npm test
cd site && npm run validate
cd site && npm run build
```

All commands passed after the final review-fix patch. Site tests reported
401 passing tests. Aggregate validation reported 60 HTML files, 2034 internal
references, 59 sitemap URLs, 1 legacy story safety target, and 13 legacy
modernization evidence-map rows.

Browser sanity:

- Desktop viewport `1440x1100`: `/release-review-boundary/` rendered the hero
  and release-boundary matrix without page-level horizontal overflow.
- Mobile viewport `390x900`: `/release-review-boundary/` rendered the hero and
  matrix; the wide matrix scrolls inside its table wrapper without page-level
  horizontal overflow.
- Local browser screenshots were temporary verification artifacts and were not
  committed.

Review findings and dispositions:

- Gemini thread on `getAttribute` spacing: patched in
  `5f8b656bf3a480c55b94bd9e5be43add05a6f3db` by allowing whitespace around
  attribute assignment and adding a spaced-attribute regression test.
- Gemini thread on metadata content extraction: patched in
  `5f8b656bf3a480c55b94bd9e5be43add05a6f3db` by extracting `content`
  attributes from each meta tag directly and adding a reordered-metadata
  regression test.
- Codex thread on negation scope: patched in
  `5f8b656bf3a480c55b94bd9e5be43add05a6f3db` by limiting negation checks to
  the current sentence prefix and adding a regression test.
- Codex/Qodo private-section scan concern: patched in
  `5f8b656bf3a480c55b94bd9e5be43add05a6f3db` by scanning hard private
  material against full page content and keeping softer boundary examples
  scoped to stripped boundary sections.
- Qodo top-level head-text and tag-split private-material findings: patched in
  `aa89c1c7a138b45861f7938534fb91c64cb76c6a` by scanning full-document
  rendered text for forbidden/private/blame checks and checking decoded,
  rendered, and tight tag-stripped private-material variants. Added
  regressions for forbidden title text and tag-split hard private material.
- Evidence-backed PR-loop disposition comments were posted for the two Gemini
  threads, the outdated Codex negation thread, and the Qodo top-level comment.

Latest PR-loop outcome before this bookkeeping update:

```bash
agent-control pr-loop --repo joefeser/tracemap --pr 291 --base dev --require-codex-review --quiet --json
```

- Head: `aa89c1c7a138b45861f7938534fb91c64cb76c6a`.
- Decision: `merge_ready`.
- Stop reason: `NONE`.
- Can merge: `true`.
- Next action: `merge_ready`.
- Merge state: `CLEAN`.
- Unresolved review threads: `0`.
- Pending checks: `0`.
- Failed checks: `0`.
- Actionable bot findings: `0`.
- Qodo state: `dispositioned`.
- Codex state: stale result from reviewed head
  `4387e75a3c7ae5507b74441092eadf8d749404ec`, treated as medium residual
  risk under the configured `dev` quorum policy because no stale actionable
  Codex findings were found and Qodo returned.
- Optional Gemini and Sourcery reviews were absent or not requested as
  residual risk, not merge blockers by policy.

## Oddities

- The committed implementation state intentionally omits local absolute
  worktree paths to satisfy private-path guard rules.
- During implementation, an initial local patch was accidentally applied in
  the root checkout, then transferred to the isolated implementation worktree;
  the root checkout was restored clean before continuing.
- The final bookkeeping commit changes only spec state and task status. Rerun
  PR-loop on that exact final head before merge and treat any clean `dev`
  stale-review posture according to the repo-local lane policy instead of
  retagging reviewers by hand.

## Follow-ups

- No code follow-up is open from the latest merge-ready PR-loop result.
- Joe should merge only the current PR head reported by the final PR-loop run
  after this bookkeeping commit is pushed.
- Patch Medium or higher findings and rerun re-review where feasible.
- Update `requirements.md`, `design.md`, `tasks.md`, `review-packet.md`, and
  this file to `Readiness: ready-for-implementation` together in one patch
  only after review findings are handled.

All five packet files now carry `Readiness: ready-for-implementation`.

## Implementation Phase

Implementation branch: `codex/impl-site-release-review-boundary`

Target base: `dev` from `origin/dev`

Worktree: isolated worktree requested by the operator. The machine-local path
is not repeated in this committed spec state.

Scope implemented:

- Added a standalone `/release-review-boundary/` public route under `site/src/`.
- Added concept-level page metadata, sitemap metadata, and discovery metadata
  with `publicClaimLevel: concept`.
- Added the required release-boundary rows: changed source surface,
  package/config surface, route/endpoint adjacency, SQL/data surface,
  coverage gap, validation evidence, runtime telemetry need, and
  release-owner decision.
- Added focused validation for visible claim level, shared evidence principle,
  required sections, required rows, row fields, supporting links, route
  metadata, sitemap/discovery coverage, inbound links, forbidden positive
  release claims, private/raw leakage outside sanctioned boundary sections,
  blame language, and word-count bounds.
- Registered the focused validator in the aggregate site validation workflow.
- Added inbound discovery links from `/review-room/` and
  `/use-cases/change-review/`.

Final placement: standalone `/release-review-boundary/`.

Placement rationale: the route is a direct release-review handoff reference for
release owners, service owners, runtime owners, test owners, security owners,
managers, and engineers. It is not a release gate, release approval, safety
claim, deploy audit, validation proof, runtime workflow, checklist
replacement, manager packet, objection guide, or replacement for human release
controls.

Rejected alternatives:

- `/review-room/release-boundary/`: rejected because the release boundary
  should be linkable outside meeting-room context and should not imply the
  review-room agenda owns release decisions.
- Section on `/limitations/`: rejected because limitations are site-wide
  non-claims, while this surface applies those limits to release-review roles
  and owner handoff.
- Section on `/static-vs-runtime/`: rejected because runtime separation is one
  row in the release-boundary matrix, not the whole release-review ownership
  question.
- Replacing `/review-claim-checklist/`, `/deploy-audit/`, `/validation/`,
  `/manager-packet/`, or `/questions/objections/`: rejected because those
  routes answer adjacent claim-repeatability, deploy-output, validation,
  manager-facing, and objection-handling questions.

Navigation decision: the route was not added to primary navigation. Discovery
uses sitemap and route metadata plus inbound links from adjacent review
surfaces where the release-boundary distinction improves reader routing
without bloating the primary nav.

Claim-level decision: page-level and row-level public claim level remain
`concept`. No row was upgraded because the page is an orientation and handoff
surface, not a new proof source.

Supporting-route decision: all supporting routes used by the matrix and
adjacent-surfaces section exist in generated output. No substitutions or
deferrals were needed.

Validation results:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- `cd site && npm test`: passed.
- `cd site && npm run validate`: passed.
- `cd site && npm run build`: passed.
- Browser sanity: passed for wide desktop and narrow mobile viewports. The
  release-boundary page rendered without page-level horizontal overflow; on
  mobile the release-boundary matrix scrolls inside its table wrapper.

PR-loop outcome: pending until a ready PR exists.

Oddities:

- During implementation, an initial patch was accidentally applied to the root
  checkout instead of the isolated worktree. Before continuing, the exact edits
  were transferred to the isolated worktree and the root checkout was restored
  to clean status for those files. No unrelated root changes were reverted.
- The standalone `npx playwright screenshot` command could not use a local
  browser binary in this environment. Browser sanity used the configured
  Playwright wrapper instead.

Follow-ups:

- Create the ready PR to `dev`.
- Run the repo-local PR loop after the requested wait period.
- Update this section with the exact PR-loop decision, stop reason, residual
  risk, unresolved threads, checks, and actionable findings before final
  handoff.
