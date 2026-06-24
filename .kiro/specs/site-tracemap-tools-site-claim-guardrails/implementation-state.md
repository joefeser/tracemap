# Site TraceMap Tools Site Claim Guardrails Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-claim-guardrails`

Implementation branch: not started

Base: `origin/dev`

Target PR base: `dev`

Worktree: isolated spec worktree; local absolute path intentionally omitted
from checked-in spec notes.

Scope: spec-only public-site or contributor-facing guardrails packet. Changes
are limited to this spec directory. Site source, generated output, scanner
code, reducer code, validation scripts, existing specs, runtime telemetry,
review automation behavior, and AI/LLM behavior remain out of scope.

## Current State

This packet is in spec review. Required spec files:

- `requirements.md`
- `design.md`
- `tasks.md`
- `implementation-state.md`
- `review-packet.md`

## Claim-Level Decision

Provisional public claim level: `concept`.

Rationale: the preferred future surface is public-facing governance guidance
for site copy. It explains how to keep public claims attached to deterministic
static evidence, public-safe proof paths, limitations, and review handoffs. It
does not publish a new TraceMap finding, scanner capability, reducer result,
demo result, runtime observation, release decision, operational safety claim,
or shipped workflow.

Contributor-only alternative: a future implementation may choose
`Public claim level: hidden` only for a strictly contributor-only docs page
that is excluded from public discovery, sitemap metadata, external-facing route
output, and public navigation. That decision must be recorded before
implementation.

## Placement Decision

Provisional recommended placement for future implementation:
`/site-claim-guardrails/`.

Reason: the guardrails are broad copy-governance rules. A standalone public
route gives enough space for claim levels, evidence references, forbidden raw
material, downgrade rules, and validation expectations without crowding the
canonical claim checklist.

Candidate placements to reevaluate at implementation time:

- `/site-claim-guardrails/`: recommended public standalone route.
- `/docs/site-claim-guardrails/`: allowed if the site docs family is the
  better home for contributor-facing guidance.
- Section on `/review-claim-checklist/`: allowed only if the required content
  fits without turning the checklist into a general policy page.
- Contributor-only docs page linked from `/docs/`: allowed only if hidden and
  excluded from public sitemap and discovery output.

## Scope Decisions

- Create a guardrails spec, not site implementation.
- Require visible `Public claim level: concept` for public-facing output.
- Require visible `No public conclusion without evidence`.
- Require sections for public claim levels, proof-path requirements, allowed
  evidence references, forbidden raw material, non-claim patterns, downgrade
  and hidden rules, validation expectations, and review handoff.
- Require guardrail rows for shipped, demo, concept, hidden, raw artifact
  reference, dev-only feature, reduced coverage, runtime/release wording,
  AI/LLM wording, and private-only support.
- Keep public claim levels to `shipped`, `demo`, `concept`, and `hidden`.
- Forbid new product capability claims, runtime proof, release approval,
  release safety, operational safety, complete coverage, AI/LLM analysis, and
  replacement of human review.
- Forbid raw facts, SQLite, analyzer logs, source snippets, SQL, config
  values, secrets, local paths, remotes, generated scan directories, private
  sample names, command output, hidden validation details, and
  credential-like values.
- Avoid blame language.

## Review Commands

Planned spec review commands:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-site-claim-guardrails --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-site-claim-guardrails --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

If a requested model or tool is unavailable, record the exact error in this
file. If both requested models are unavailable but the review harness can run a
substitute model, run the best available review and record the substitution.

## Review Outcomes

### Initial spec review

Review command:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-site-claim-guardrails --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
```

Saved clean output:
`.tmp/kiro-reviews/site-tracemap-tools-site-claim-guardrails/2026-06-24T031343-783Z-spec-claude-opus-4.8.clean.md`

Coverage: reduced. Kiro reported denied write-tool access after reading all
five spec files and checking adjacent route/script grounding.

Findings summary: 0 High, 2 Medium, 5 Low.

Medium findings:

- M1: `design.md` showed a 3-column digest table even though requirements
  mandate six machine-checkable fields per guardrail row.
- M2: forbidden-claim and forbidden-material validation needed stable
  machine-distinguishable markers for all allowed contexts, not just unsafe
  examples.

Low findings: echo readiness lifecycle in `requirements.md`, include the
explicit term `stop condition`, clarify folded-section word-count semantics,
address folded-section anchor collision risk, and frame claim-level/placement
decisions as provisional while status is `not-started`.

Disposition: patched M1 and M2 in `requirements.md` and `design.md`; patched
the Low advisory items where they were low-risk clarifications. Readiness
remained `spec-review` pending the required `claude-sonnet-4.6` review and
re-review where feasible.

### Second spec review

Review command:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-site-claim-guardrails --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Saved clean output:
`.tmp/kiro-reviews/site-tracemap-tools-site-claim-guardrails/2026-06-24T031901-704Z-spec-claude-sonnet-4.6.clean.md`

Coverage: reduced. Kiro reported denied write-tool access after reading all
five spec files.

Findings summary: 0 High, 2 Medium, 4 Low.

Medium findings:

- M1: reviewer challenged the `claude-opus-4.8` model slug in tasks and review
  commands and suggested changing to another opus slug.
- M2: completed Opus review was not reflected in `tasks.md` checkboxes.

Low findings: record the Opus model-slug challenge in Oddities, enumerate the
six row fields in validation prose, include `stop condition` in Claim
Boundaries, and reorder spec-phase `git diff --check` and private-path checks
before the readiness-gate task.

Disposition:

- M1: explicitly dispositioned without changing the command because this
  packet's user instruction requires `claude-opus-4.8`, and the wrapper
  accepted and saved an Opus review artifact under that requested slug.
  Changing the command would diverge from the requested review workflow.
- M2: patched by marking completed Opus and Sonnet review tasks checked.
- Low items: patched where useful by recording this oddity, enumerating the
  six row fields in validation, adding `stop condition` to Claim Boundaries,
  and reordering validation commands before readiness upgrade.

Readiness remained `spec-review` pending a final re-review where feasible.

### Final confirmation re-review

Review command:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-site-claim-guardrails --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Saved clean output:
`.tmp/kiro-reviews/site-tracemap-tools-site-claim-guardrails/2026-06-24T032137-162Z-re-review-claude-sonnet-4.6.clean.md`

Coverage: reduced. Kiro reported denied write-tool access after reading all
five spec files.

Findings summary: 0 High, 1 Medium, 4 Low.

Medium finding:

- M1: `tasks.md` still had the "Patch Medium or higher findings and rerun
  re-review where feasible" task unchecked even though prior Medium findings
  were patched or dispositioned and this review was the rerun.

Low findings: script checks were still pending before readiness promotion;
Requirement 10 did not validate the primary-navigation constraint; reviewer
suggested removing `kiro-review.mjs` lines from design validation, but no such
lines were present in `design.md`; fallback unavailable-model task was
inapplicable after both reviews completed.

Disposition:

- M1: patched by checking the patch-and-rerun task.
- Primary-navigation validation Low: patched in Requirement 10.
- Pending script checks Low: will be handled before readiness promotion.
- `kiro-review.mjs` design note: dispositioned as stale; the design validation
  section names site/root validation commands, not Kiro review commands.
- Unavailable-model fallback Low: annotated as N/A because both requested
  reviews completed with reduced coverage.

Readiness remained `spec-review` until `git diff --check` and
`./scripts/check-private-paths.sh` passed and were recorded.

## Spec Validation

Completed spec-phase validation:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.

Readiness upgraded to `ready-for-implementation` after Medium or higher review
findings were patched or explicitly dispositioned, the reduced-coverage
re-review completed, and the spec-phase validation checks passed.

## Future Implementation Validation

Expected future implementation validation:

- `npm test` from `site/`.
- `npm run validate` from `site/`.
- `npm run build` from `site/`.
- `git diff --check`.
- `./scripts/check-private-paths.sh`.
- Desktop browser sanity if route, layout, or interaction changes are made.
- Mobile browser sanity if route, layout, or interaction changes are made.

Focused validation should cover required guardrail rows, required adjacent
links, metadata, discovery or sitemap metadata if public standalone,
forbidden claims, private/raw material, word count bounds, and desktop/mobile
browser sanity.

## Oddities

- This spec intentionally overlaps several governance surfaces. The distinct
  axis is copy guardrails: what site copy may say, what proof must stay
  attached, and when the copy must be downgraded or hidden.
- `claude-sonnet-4.6` review challenged the `claude-opus-4.8` model slug, but
  the user explicitly required that exact model string and the Kiro wrapper
  accepted it, produced saved artifacts, and recorded reduced coverage. The
  command and artifact names remain as run.

## Follow-Up Items

- Future implementation should confirm the final route or contributor-only
  placement before changing site code.
- Future implementation should rerun the site validation and browser sanity
  checks listed above after adding any public or contributor-facing surface.
