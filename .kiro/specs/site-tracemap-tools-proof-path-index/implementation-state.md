# Site TraceMap Tools Proof Path Index Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: demo

## Branch

Spec drafting branch: `codex/site-proof-path-index`

Planned implementation branch: not started

## Scope

This spec queues a future public site feature named
`site-tracemap-tools-proof-path-index`. The future feature should help managers,
reviewers, engineers, and bots find the public/demo evidence trail behind
TraceMap pages and demo sections.

This spec-prep PR only creates the Kiro spec files. It does not edit site
source, create implementation code, change generated site output, or touch any
other spec.

## Shared Site Principle

No public conclusion without evidence.

## Claim Boundaries

- Safe to say: the future page is a demo-level index over public-safe proof
  paths and checked-in public/demo artifacts.
- Safe to say: entries can organize artifact type, rule ID, evidence tier,
  coverage label, proof path, and limitation for human and bot review.
- Safe to say: dev-only capabilities must be labeled `dev-only` or omitted until
  promotion to `main`.
- Not safe to say: TraceMap proves runtime behavior, production traffic,
  endpoint performance, deployment state, release safety, or AI impact analysis.
- Not safe to say: a public site index replaces the underlying facts, reports,
  manifests, rule IDs, coverage labels, limitations, or source proof paths.

## Public-Safe Artifact Boundary

Future implementation may summarize or link public/demo artifacts and
public-safe generated summaries. It must not publish raw facts, SQLite
databases, analyzer logs, source snippets, raw SQL, config values, secrets,
local absolute paths, raw repository remotes, generated scan directories, or
private sample identities.

## Current Decisions

- Status is `not-started`; readiness is `ready-for-implementation`.
- Public claim level is `demo`.
- The final route is not yet chosen; a standalone route should be included in
  sitemap metadata if selected.
- Page-level `demo` claim level and per-entry public status are separate axes;
  entries may still be `demo`, `dev-only`, `future`, or omitted depending on the
  proof available in the implementation branch.
- The implementation should use existing site styles and avoid adding runtime
  services.
- The implementation should treat public pages, demo sections, and generated
  public-safe summaries as orientation surfaces over underlying static evidence.

## Spec-Prep Validation

- Attempted: `node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-path-index --kind spec --model claude-opus-4.8 --fresh`
  - Result: the default wrapper prompt requires `design.md`; this spec-prep
    scope intentionally tracks only `requirements.md`, `tasks.md`, and
    `implementation-state.md`.
- Passed with full review coverage: `node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-path-index --kind spec --model claude-opus-4.8 --fresh --prompt-file /tmp/tracemap-proof-path-index-spec-review.md --save-review-text`
  - Medium+ findings patched: coverage-label vocabulary, proof-path resolution,
    and public-safe boundary validation.
- Attempted: `node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-path-index --kind spec --model claude-sonnet-4.8 --fresh --prompt-file /tmp/tracemap-proof-path-index-spec-review.md --save-review-text`
  - Result: `claude-sonnet-4.8` was not available in this environment.
- Passed with full review coverage: `node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-path-index --kind spec --model auto --fresh --prompt-file /tmp/tracemap-proof-path-index-spec-review.md --save-review-text`
  - Result: no blockers; low-severity sequencing and bot-label suggestions
    patched.

Remaining local validation:

- `git diff --check`
- `scripts/check-private-paths.sh`

## Future Implementation Validation

- `npm test` from `site/`
- `npm run validate` from `site/`
- Verify proof paths resolve to existing checked-in artifacts, public-safe
  generated summaries, or public routes
- Run the public demo sentinel scan through the existing demo-public assertion
  workflow if public demo generated summaries are refreshed
- Desktop and mobile browser sanity checks if layout or interaction changes

## Follow-Up Items

- Choose the final URL or section placement before implementation.
- Build the first index from public/demo routes that already have public-safe
  proof paths.
- Re-check main/dev promotion state before writing public copy.
- Keep any future generated proof-path data deterministic and reviewable.
