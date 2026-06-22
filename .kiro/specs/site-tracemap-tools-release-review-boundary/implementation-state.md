# Site TraceMap Tools Release Review Boundary Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-release-review-boundary`

Base: `origin/dev`

Target PR base: `dev`

Worktree: isolated worktree requested by the operator. The machine-local path
is not repeated in this committed spec state to keep repository docs free of
private local path material.

Scope: create a spec-only Kiro packet for a future public-site page or section
explaining TraceMap's boundary in release review. This branch changes only
`.kiro/specs/site-tracemap-tools-release-review-boundary/`.

Out of scope: `site/src`, generated `site/dist`, generated `site/output`,
scanner code, reducer code, existing specs, release automation, runtime
telemetry, AI/LLM analysis, embeddings, vector databases, prompt
classification, generated evidence artifacts, and public copy changes.

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

## Validation Results

Not run yet.

Planned spec-branch validation:

```bash
git diff --check
./scripts/check-private-paths.sh
```

Before checking the future implementation validation task, verify
`./scripts/check-private-paths.sh` still exists and run it from the repository
root.

Future implementation validation also requires:

```bash
npm test
npm run validate
npm run build
```

from `site/`, plus desktop and mobile browser sanity checks when layout or
interaction changes are made.

## Oddities

- The committed implementation state intentionally omits the local absolute
  worktree path to satisfy private-path guard rules.
- This branch is spec-only, so site validation and browser sanity checks are
  future implementation tasks rather than spec-branch requirements.

## Follow-ups

- Run Kiro spec review with `claude-opus-4.8` and `claude-sonnet-4.6` if
  available.
- Patch Medium or higher findings and rerun re-review where feasible.
- Update `requirements.md`, `design.md`, `tasks.md`, `review-packet.md`, and
  this file to `Readiness: ready-for-implementation` together in one patch
  only after review findings are handled.

All five packet files now carry `Readiness: ready-for-implementation`.
