# Site TraceMap Tools Stakeholder Objection Guide Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-stakeholder-objection-guide`

Base: `origin/dev`

Target PR base: `dev`

Worktree:
`/Users/josephfeser/src/gh-joe/tracemap-spec-stakeholder-objection-guide`

Scope: spec-only packet for a future public-site stakeholder objection guide.
Allowed files are limited to
`.kiro/specs/site-tracemap-tools-stakeholder-objection-guide/`.

## Current State

Initial spec packet created. No site source, site scripts, generated output,
scanner code, reducer code, existing specs, or public copy have been changed.

The future surface is an objection-to-evidence handoff guide. It should help
managers, reviewers, engineers, and skeptical stakeholders ask hard questions
and get bounded answers without upgrading static evidence into runtime proof,
release approval, production traffic, endpoint performance, AI analysis,
absence-of-impact proof, complete coverage, or organizational decisions.

## Claim-Level Decision

Selected public claim level: `concept`.

Rationale: the guide is explanatory and navigational. It does not publish new
demo evidence or prove a new capability. It routes objections to public-safe
proof surfaces, limitations, stop conditions, and owners.

Future implementation must include visible `Public claim level: concept` and
visible `No public conclusion without evidence`.

## Placement Guidance

Candidate placements:

- `/objections/`
- `/questions/objections/`
- Section on `/questions/`
- Section on `/manager-faq/`

Final placement: not selected in this spec-only phase.

Future implementation must record the final placement and rejected
alternatives before changing site source.

Initial preference: `/questions/objections/` if `/questions/` remains the
public entry point for stakeholder questions. This keeps the guide near the
question-index surface while making objections addressable. `/objections/`
may be preferable if implementation needs a short standalone URL for review
handoffs. Section placement on `/questions/` or `/manager-faq/` should be used
only if standalone metadata would overstate maturity or duplicate nearby
routes.

## Relationship Decisions

The objection guide must distinguish itself from:

- `/questions/`: question routing; the guide is objection handling.
- `/manager-faq/`: concise manager answers; the guide uses a row schema with
  evidence, stop condition, owner, and limitation fields.
- `/limitations/`: site-wide boundaries; the guide applies boundaries to
  concrete skeptical questions.
- `/static-vs-runtime/`: static/runtime comparison; the guide links there for
  runtime, traffic, and performance objections.
- `/review-claim-checklist/`: repeatability checklist; the guide tells readers
  what answer shape is safe before a claim can be repeated.
- `/proof-paths/tour/`: proof-path education; the guide sends readers there
  when proof-path literacy is needed.
- `/manager-demo-script/` or `/demo/manager-script/`: live presentation
  script; the guide is a reference surface for hard questions.

## Scope Decisions

- Required files: `requirements.md`, `design.md`, `tasks.md`,
  `implementation-state.md`, and `review-packet.md`.
- Required objection categories are fixed by the user request and appear in
  requirements, design, and tasks.
- Every objection row must include safe short answer, evidence to check, stop
  condition, next owner, public claim level, limitation/non-claim, and a
  supporting public route.
- The row-level public claim level defaults to `concept`.
- Owner values use public role categories, not people or private teams.
- Tone must be calm, professional, and non-defensive.
- No raw artifacts, private material, hidden validation details, local paths,
  raw command output, or credential-like values may appear in public output.
- Future validation must cover required rows, required links, metadata,
  discovery/sitemap metadata if standalone, forbidden claims, private/raw
  material, word count bounds, and desktop/mobile browser sanity.

## Review Commands

Planned spec review commands:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-stakeholder-objection-guide --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-stakeholder-objection-guide --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

If a model or tool is unavailable, record the exact command and error here.

## Review Results

Initial Kiro review commands:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-stakeholder-objection-guide --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-stakeholder-objection-guide --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- `claude-opus-4.8`: command completed with reduced coverage because Kiro
  reported denied tool access. Artifacts:
  `.tmp/kiro-reviews/site-tracemap-tools-stakeholder-objection-guide/2026-06-21T205009-351Z-spec-claude-opus-4.8.clean.md`
  and matching `.meta.json`. Meta recorded `toolDenied: true`,
  `reviewComplete: true`, and `reviewCoverage: "Reduced"`.
- `claude-sonnet-4.6`: command completed with reduced coverage because Kiro
  reported denied tool access. Artifacts:
  `.tmp/kiro-reviews/site-tracemap-tools-stakeholder-objection-guide/2026-06-21T205009-468Z-spec-claude-sonnet-4.6.clean.md`
  and matching `.meta.json`. Meta recorded `toolDenied: true`,
  `reviewComplete: false`, and `reviewCoverage: "Reduced"`.

Review findings and dispositions:

- Opus Low-Medium: the forbidden-positioning regex could false-positive on
  required objection titles such as `Can I use this for release approval?`
  because only the overclaim regex had an explicit objection-boundary
  carve-out. Patched Requirement 8, design validation guidance, and tasks so
  both patterns exclude objection titles and bounded safe-answer,
  stop-condition, non-claim, limitation, and objection-boundary contexts.
- Sonnet Medium: the 900 to 2,400 visible-word bound did not define what
  counted as visible words. Patched Requirement 8, design validation guidance,
  and tasks to count rendered body prose and objection row cell content while
  excluding page-level navigation, breadcrumbs, site headers, site footers,
  metadata blocks, and row-field label headers.
- Opus Low: supporting routes `/capabilities/` and `/proof-source-catalog/`
  appeared in the design matrix but not the requirements relationship list.
  Patched the relationship section to clarify supporting routes may extend
  beyond that list when verified or recorded as deferred.
- Sonnet Low: stop-condition column text needed explicit exclusion from
  overclaim regex scans. Patched design validation guidance.

Re-review commands:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-stakeholder-objection-guide --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-stakeholder-objection-guide --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- `claude-opus-4.8` re-review: command completed with reduced coverage because
  Kiro reported denied tool access. Artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-stakeholder-objection-guide/2026-06-21T205357-824Z-re-review-claude-opus-4.8.clean.md`.
  Meta recorded `toolDenied: true`, `reviewComplete: true`, and
  `reviewCoverage: "Reduced"`. No Medium or higher findings.
- `claude-sonnet-4.6` re-review: command completed with reduced coverage
  because Kiro reported denied tool access. Artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-stakeholder-objection-guide/2026-06-21T205357-939Z-re-review-claude-sonnet-4.6.clean.md`.
  Meta recorded `toolDenied: true`, `reviewComplete: false`, and
  `reviewCoverage: "Reduced"`. No Medium or higher findings were returned in
  the available review output.

Re-review findings and dispositions:

- Opus Low: automated review coverage is partial, and Sonnet's meta recorded
  `reviewComplete: false`. Disposition: recorded as residual review coverage
  risk; no spec patch needed because both requested commands ran and exact
  metadata is recorded.
- Opus Low: `review-packet.md` review status was stale after review and
  re-review. Patched review-packet status.
- Opus Low: route availability remains the largest future implementation
  risk. Disposition: requirements, design, and tasks already require
  implementation-time route verification or recorded deferral before linking.
- Sonnet Low observation: `approval` wording could collide with stricter
  future validation. Disposition: existing objection-title and bounded-context
  carve-outs are sufficient; keep this note for implementers.

Readiness decision: `ready-for-implementation`. Medium and higher findings
from the initial reviews were patched, re-review was attempted with both
requested models, and remaining findings are Low or residual coverage risks
recorded above.

## Validation

Spec-only validation:

```bash
git diff --check
./scripts/check-private-paths.sh
```

Results:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- Focused text sanity checked status/readiness/public claim metadata across
  the five spec files, required objection category text, visible
  `No public conclusion without evidence`, and future implementation task
  checkboxes.

Future implementation validation is listed in `requirements.md` and
`tasks.md`.

## Oddities

- The root checkout may be dirty or used by other work. This spec uses an
  isolated worktree from `origin/dev`.
- The spec intentionally does not choose a final route. Route choice belongs
  to the future implementation phase after checking live site information
  architecture.

## PR Loop

Pending. After the spec-only PR is opened, run:

```bash
agent-control pr-loop --repo joefeser/tracemap --pr <PR_NUMBER> --base dev --require-codex-review --quiet --json
```

Use repo-local `.agent-control/lanes/pr-review-loop.yaml` from this worktree.

## Follow-Ups

- Run Kiro spec review with both requested models or record exact errors.
- Patch or disposition Medium+ review findings.
- Move readiness to `ready-for-implementation` only after review findings are
  patched or dispositioned.
- Keep future implementation tasks unchecked on this spec-only branch.
