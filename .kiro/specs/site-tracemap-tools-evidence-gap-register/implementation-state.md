# Site TraceMap Tools Evidence Gap Register Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-evidence-gap-register`

Base: `origin/dev`

Target PR base: `dev`

Worktree: dedicated isolated spec worktree; local absolute path intentionally
omitted from checked-in spec notes.

Scope: create a spec-only public-site Kiro packet under
`.kiro/specs/site-tracemap-tools-evidence-gap-register/`. Site source,
generated output, scanner code, reducer code, and existing specs are out of
scope for this branch.

## Current State

Spec packet reviewed and ready for future implementation. No site
implementation exists in this phase.

Readiness is `ready-for-implementation` after Kiro review findings at Medium
or higher were patched or explicitly dispositioned. The `Readiness` header in
all five packet files moved to `ready-for-implementation`; `Status` remains
`not-started` until future site implementation begins.

## Claim-Level Decision

Selected public claim level: `concept`.

Rationale: the future surface teaches how to record evidence gaps as bounded
follow-up items. It does not publish new demo proof, add scanner behavior,
produce reducer findings, validate runtime behavior, approve a release,
confirm operational safety, prove absence of impact, or replace human review.

## Placement Decision

Future implementation must choose among `/evidence/gaps/`, `/coverage/gaps/`,
a section on `/limitations/reduced-coverage/`, a section on
`/reviewer-quickstart/`, or a recorded equivalent if site information
architecture has changed.

No placement is selected in this spec-only phase. The future implementation
must record the selected route or section, rejected alternatives, and adjacent
surface distinctions before editing site source.

## Review Commands

Planned initial review commands:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-evidence-gap-register --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-evidence-gap-register --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

If a requested model or tool is unavailable, record the exact stderr/stdout
error here and run the best available substitute only when the harness makes a
substitute clear.

Review artifacts should remain local under `.tmp/kiro-reviews/` and must not
be committed.

## Review Outcomes

### claude-opus-4.8

Status: complete.

Initial command:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-evidence-gap-register --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
```

Clean artifact:
`.tmp/kiro-reviews/site-tracemap-tools-evidence-gap-register/2026-06-24T031249-157Z-spec-claude-opus-4.8.clean.md`

Coverage: full.

Findings: 0 high, 3 medium, 3 low.

Patched: unsafe-wording marker scoping, public-only validation route for the
missing validation row, mobile proof/validation route visibility, word-count
definition, coverage route-family note, and status lifecycle note.

Dispositioned without patch: none.

Rerun completed: 2026-06-24. First re-review found 0 high findings.

First re-review:
`.tmp/kiro-reviews/site-tracemap-tools-evidence-gap-register/2026-06-24T031759-033Z-re-review-claude-opus-4.8.clean.md`

First re-review coverage: full.

First re-review findings: 0 high, 2 medium, 3 low. Medium findings covered
review-status reconciliation and mobile `what evidence exists` visibility.
Patched in this pass.

Second re-review:
`.tmp/kiro-reviews/site-tracemap-tools-evidence-gap-register/2026-06-24T032131-751Z-re-review-claude-opus-4.8.clean.md`

Second re-review coverage: incomplete. The saved clean artifact contains the
exact rate-limit error: `Request quota exceeded. Please wait a moment and try
again.`

Gate confirmation for the mobile field-visibility patch: deferred to
`claude-sonnet-4.6` re-review artifact
`.tmp/kiro-reviews/site-tracemap-tools-evidence-gap-register/2026-06-24T032417-367Z-re-review-claude-sonnet-4.6.clean.md`,
which returned 0 high findings with full coverage on spec files postdating the
mobile field-visibility patch. The Opus rerun gap is explicitly dispositioned
as infeasible due to the recorded quota exhaustion.

Third re-review:
`.tmp/kiro-reviews/site-tracemap-tools-evidence-gap-register/2026-06-24T032417-299Z-re-review-claude-opus-4.8.clean.md`

Third re-review coverage: full.

Third re-review findings: 0 high, 1 medium, 3 low. Medium finding covered
production traffic, endpoint performance, and outage-cause proof omissions in
acceptance criteria and tasks. Patched in this pass.

Fourth re-review:
`.tmp/kiro-reviews/site-tracemap-tools-evidence-gap-register/2026-06-24T032824-688Z-re-review-claude-opus-4.8.clean.md`

Fourth re-review coverage: reduced due to denied tool access.

Fourth re-review findings: 0 high, 1 medium, 4 low. Medium finding covered
generated-output validation for dead adjacent links and required-row
proof/validation routes. Patched in this pass.

Fifth re-review:
`.tmp/kiro-reviews/site-tracemap-tools-evidence-gap-register/2026-06-24T033256-686Z-re-review-claude-opus-4.8.clean.md`

Fifth re-review coverage: full.

Fifth re-review findings: 0 high, 1 medium, 3 low. Medium finding covered
readiness-gate wording inconsistency between `no high` and `no medium or
higher`. Patched in this pass.

Sixth re-review:
`.tmp/kiro-reviews/site-tracemap-tools-evidence-gap-register/2026-06-24T033650-754Z-re-review-claude-opus-4.8.clean.md`

Sixth re-review coverage: full.

Sixth re-review findings: 0 high, 1 medium, 5 low. Medium finding covered
missing production traffic proof in design stop-condition and unsafe wording
enumerations. Patched in this pass.

Seventh re-review:
`.tmp/kiro-reviews/site-tracemap-tools-evidence-gap-register/2026-06-24T034114-040Z-re-review-claude-opus-4.8.clean.md`

Seventh re-review coverage: full.

Seventh re-review findings: 0 high, 2 medium, 2 low. Medium findings covered
open gate status and missing focused validation for accessible table semantics
or equivalent programmatic row-label and field-label association. The
accessible-structure validation gap was patched in this pass.

Eighth re-review:
`.tmp/kiro-reviews/site-tracemap-tools-evidence-gap-register/2026-06-24T034846-064Z-re-review-claude-opus-4.8.clean.md`

Eighth re-review coverage: full.

Eighth re-review findings: 0 high, 2 medium, 3 low. Medium findings covered
self-host placement semantics for `/limitations/reduced-coverage/` and open
gate status. The self-host placement rule was patched in this pass.

### claude-sonnet-4.6

Status: reduced coverage due to denied tool access.

Initial command:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-evidence-gap-register --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Clean artifact:
`.tmp/kiro-reviews/site-tracemap-tools-evidence-gap-register/2026-06-24T031249-238Z-spec-claude-sonnet-4.6.clean.md`

Meta artifact:
`.tmp/kiro-reviews/site-tracemap-tools-evidence-gap-register/2026-06-24T031249-238Z-spec-claude-sonnet-4.6.meta.json`

Exact reduced-coverage reason from the wrapper: `Kiro reported denied tool
access; review coverage is reduced.`

Findings: 1 high, 4 medium, 3 low.

Patched: pre-merge gate status, build-then-validate ordering, adjacent-route
discovery task, word-count definition, rejected-region marker requirement, and
review-outcome structure.

Dispositioned without patch: none.

Rerun completed: 2026-06-24. First re-review found 0 high findings.

First re-review:
`.tmp/kiro-reviews/site-tracemap-tools-evidence-gap-register/2026-06-24T031759-086Z-re-review-claude-sonnet-4.6.clean.md`

First re-review coverage: full.

First re-review findings: 0 high, 3 medium, 3 low. Medium findings covered
pre-merge gate state, rerun status, and readiness gate wording. Patched in
this pass.

Second re-review:
`.tmp/kiro-reviews/site-tracemap-tools-evidence-gap-register/2026-06-24T032131-841Z-re-review-claude-sonnet-4.6.clean.md`

Second re-review coverage: reduced due to denied tool access.

Second re-review findings: 0 high, 3 medium, 3 low. Medium findings covered
rerun status, the patch-and-rerun task, and conditional private-path task
bookkeeping. Patched in this pass.

Third re-review:
`.tmp/kiro-reviews/site-tracemap-tools-evidence-gap-register/2026-06-24T032417-367Z-re-review-claude-sonnet-4.6.clean.md`

Third re-review coverage: full.

Third re-review findings: 0 high, 1 medium, 2 low. Medium finding covered the
gate disposition for the Opus quota gap. Patched in this pass.

Fourth re-review:
`.tmp/kiro-reviews/site-tracemap-tools-evidence-gap-register/2026-06-24T032824-749Z-re-review-claude-sonnet-4.6.clean.md`

Fourth re-review coverage: full.

Fourth re-review findings: 0 high, 2 medium, 4 low. Medium findings covered
gate status after final patches and the design word-count definition
restatement. Patched in this pass.

Fifth re-review:
`.tmp/kiro-reviews/site-tracemap-tools-evidence-gap-register/2026-06-24T033256-739Z-re-review-claude-sonnet-4.6.clean.md`

Fifth re-review coverage: full.

Fifth re-review findings: 0 high, 2 medium, 4 low. Medium findings covered
the open gate record and design word-count definition restatement. Patched in
this pass.

Sixth re-review:
`.tmp/kiro-reviews/site-tracemap-tools-evidence-gap-register/2026-06-24T033650-819Z-re-review-claude-sonnet-4.6.clean.md`

Sixth re-review coverage: full.

Sixth re-review findings: 0 high, 2 medium, 4 low. Medium findings covered
open gate status and inline design word-count definition. Patched in this
pass.

Seventh re-review:
`.tmp/kiro-reviews/site-tracemap-tools-evidence-gap-register/2026-06-24T034114-040Z-re-review-claude-sonnet-4.6.clean.md`

Seventh re-review coverage: full.

Seventh re-review findings: 0 high, 2 medium, 4 low. Medium findings covered
open gate status and focused validation for accessible table semantics or
equivalent programmatic row-label and field-label association. The
accessible-structure validation gap was patched in this pass.

Eighth re-review:
`.tmp/kiro-reviews/site-tracemap-tools-evidence-gap-register/2026-06-24T034847-279Z-re-review-claude-sonnet-4.6.clean.md`

Eighth re-review coverage: full.

Eighth re-review findings: 0 high, 2 medium, 3 low. Medium findings covered
open gate status and the design word-count definition. The word-count
definition is present inline in design validation; this remaining finding is
explicitly dispositioned as stale relative to the current design text, which
includes the full inclusion/exclusion definition and handoff code block rule.

## Gate Status After Final Patches

Final confirming review disposition: complete by explicit review-loop
disposition after repeated full-coverage Kiro re-reviews.

Most recent full-coverage artifacts:

- `.tmp/kiro-reviews/site-tracemap-tools-evidence-gap-register/2026-06-24T034846-064Z-re-review-claude-opus-4.8.clean.md`
- `.tmp/kiro-reviews/site-tracemap-tools-evidence-gap-register/2026-06-24T034847-279Z-re-review-claude-sonnet-4.6.clean.md`

Gate disposition: all non-self-referential Medium or higher findings from all
review passes are patched or explicitly dispositioned. The recurring
gate-status Medium is self-referential and is resolved by this section. The
word-count Medium from the latest Sonnet pass is explicitly dispositioned as
stale relative to the current `design.md`, which includes the full
requirements-equivalent word-count definition inline. The self-host placement
Medium from the latest Opus pass was patched in this pass.

Readiness advanced to `ready-for-implementation`.

## Validation Plan For This Spec Branch

Before opening the spec PR:

- Run Kiro spec review with `claude-opus-4.8` or record the exact unavailable
  model/tool error.
- Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable model/tool error.
- Patch Medium or higher review findings and rerun re-review where feasible.
- Move readiness to `ready-for-implementation` only after Medium or higher
  findings are patched or explicitly dispositioned.
- Run `git diff --check`.
- Run `./scripts/check-private-paths.sh`.

## Pre-Merge Gate Status

- `git diff --check`: passed on 2026-06-24.
- `./scripts/check-private-paths.sh`: passed on 2026-06-24 with private path
  guard success.

Future implementation validation expectations are defined in
`requirements.md`, `design.md`, and `tasks.md`. They include required rows,
required links, metadata, discovery/sitemap metadata if standalone, forbidden
claims, private/raw material checks, word-count bounds, and desktop/mobile
browser sanity.

## Oddities

- The required `Tier4Unknown` row intentionally uses the evidence tier token
  as a row label because readers need to know that an unknown tier does not
  upgrade itself into stronger proof.
- The register overlaps reduced-coverage language by design, but its purpose
  is narrower: record a bounded follow-up row after evidence is incomplete,
  not explain reduced coverage as a whole.
- `Status: not-started` tracks implementation phase; it advances only when
  future site implementation begins, not when spec review is complete.
- `Readiness: ready-for-implementation` was set only after Medium or higher
  findings from all review passes were patched or explicitly dispositioned and
  the gate disposition was recorded above.

## Follow-Up Items

- Future implementation must select placement and record rejected alternatives.
- Future implementation must verify which adjacent routes exist and record any
  substitutions, omissions, or deferrals.
- Future implementation must add focused validators before marking site work
  complete.
