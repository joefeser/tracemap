# Site TraceMap Tools Manager Problem Brief Implementation State

Status: not-started
Readiness: ready-for-review
Public claim level: concept

## Branch

Spec branch: `codex/site-manager-problem-brief-spec`

Planned implementation branch: not started.

## Scope

This branch only creates the spec for a future public-safe manager/problem
brief page or article. No site source, generated output, scripts, docs, or
unrelated specs are implemented or modified in this phase.

The future page should help managers and technical leads understand the
recurring coordination problem TraceMap addresses: manual dependency questions,
cross-team review pressure, and the cost of reconstructing static dependency
context during change review.

## Planned Claim Boundaries

Safe to say:

- TraceMap is intended to produce deterministic static evidence packets.
- Evidence packets can include rule IDs, evidence tiers, coverage labels,
  limitations, file paths or public proof paths, line spans, hashes, and
  generated public-safe summaries.
- Public-safe summaries and demo evidence can help teams inspect change risk
  and review dependency questions with less manual indexing.
- Static evidence can reduce review burden without eliminating judgment,
  uncertainty, or follow-up investigation.

Not safe to say:

- TraceMap proves runtime behavior, production traffic, endpoint performance,
  outage cause, release safety, operational safety, AI impact analysis, LLM
  analysis, or complete product coverage.
- TraceMap replaces human review or approves releases.
- A generated summary is a substitute for underlying facts, rule IDs, evidence
  tiers, coverage labels, and limitations.

## Public-Safe Publishing Boundary

Future implementation should use public-safe generated summaries and demo
evidence. It must not publish raw `facts.ndjson`, `index.sqlite`, analyzer logs,
raw source snippets, raw SQL, config values, secrets, local absolute paths, raw
repository remotes, generated scan directories, or private sample identities.

## Implementation Status

No implementation has started. Tasks remain unchecked because this is a
spec-only branch.

## Validation

Planned and run before PR:

- Passed: `node scripts/kiro-review.mjs --phase site-tracemap-tools-manager-problem-brief --kind spec --model auto --fresh --timeout-ms 600000 --save-review-text`
  - Coverage: Full
  - Session: `f49337c7-bb95-4b50-838c-491b74c11987`
  - Result: merge-ready with three Low optional findings.
  - Patched: explicit forbidden AI/LLM positioning validation, missing-route
    handling for proof links, and manager-readable length guidance.
- Passed: `node scripts/kiro-review.mjs --phase site-tracemap-tools-manager-problem-brief --kind re-review --model auto --fresh --timeout-ms 600000 --save-review-text`
  - Coverage: Full
  - Session: `cd609d15-e1ed-4ce6-955e-1d2b8d75eb92`
  - Result: merge-ready with three Low optional findings.
  - Patched: no-disparagement guidance for current tools/practices, existing
    site accessibility baseline, and an explicit spec-review gate task.
- Passed: `node scripts/kiro-review.mjs --phase site-tracemap-tools-manager-problem-brief --kind re-review --model auto --fresh --timeout-ms 600000 --save-review-text`
  - Coverage: Full
  - Session: `d51ee220-243b-4c4a-b128-87fd38bca89f`
  - Result: merge-ready with four Low optional findings.
  - Patched: explicit forbidden-positioning pattern, 1500-word ceiling, alt-text
    claim boundary, and human-review sign-off placeholder.
- Completed with findings: `node scripts/kiro-review.mjs --phase site-tracemap-tools-manager-problem-brief --kind re-review --model auto --fresh --timeout-ms 600000 --save-review-text`
  - Coverage: Full
  - Session: `5df65f34-8c00-4299-b08a-9d462523a529`
  - Result: one Medium and four Low findings; wrapper exited nonzero.
  - Patched: broader soft AI/LLM marketing forbidden-positioning pattern,
    immutable pattern floor without spec amendment, rendered word-count check,
    route-existence precheck, and social metadata length guidance.
- Passed: `node scripts/kiro-review.mjs --phase site-tracemap-tools-manager-problem-brief --kind re-review --model auto --fresh --timeout-ms 600000 --save-review-text`
  - Coverage: Full
  - Session: `c3192c88-fbc3-4365-9e2c-b71ade535edf`
  - Result: merge-ready with no Medium or higher findings; four Low optional
    informational findings were left for future editorial hardening.
- Passed: `git diff --check`

## Review Sign-Off

- Automated Kiro review: full coverage, merge-ready.
- Human reviewer: pending PR review.

Implementation-phase validation still to run when site code changes:

- `npm test` from `site/`
- `npm run validate` from `site/`
- `npm run build` from `site/`
- Desktop and mobile browser sanity checks for layout or interaction changes.

## Follow-Ups

- Future implementation should update this file with scope decisions, validation
  output, page route, discovery metadata changes, and any remaining public-claim
  or proof-path follow-ups.
- If Kiro review raises actionable spec feedback, patch the spec and record the
  re-review result here before implementation begins.
