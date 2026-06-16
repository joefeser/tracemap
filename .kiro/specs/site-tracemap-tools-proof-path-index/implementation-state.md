# Site TraceMap Tools Proof Path Index Implementation State

Status: implemented
Readiness: ready-for-review
Public claim level: demo

## Branch

Spec drafting branch: `codex/site-proof-path-index`

Implementation branch: `codex/site-proof-path-index`

## Scope

This implementation adds a public site feature named
`site-tracemap-tools-proof-path-index`. The page helps managers, reviewers,
engineers, and bots find the public/demo evidence trail behind TraceMap pages
and demo sections.

The implementation edits only site source/metadata/scripts and this spec state:
`site/src/**`, `site/scripts/validate-demo-summary.mjs`, and
`.kiro/specs/site-tracemap-tools-proof-path-index/{tasks.md,implementation-state.md}`.
It does not edit scanner, reducer, or core code. Generated `site/dist/` was
created by validation/build commands and was not edited by hand.

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

- Status is `implemented`; readiness is ready for PR review.
- Public claim level is `demo`.
- Final route is standalone: `/proof-paths/`.
- The route is included in `site/src/_site/pages.json` for sitemap generation.
- The route is included in `site/src/_site/discovery.json` with
  `publicClaimLevel: demo`, `sourceType: site-page`, and
  `preferredProofPath: /demo/proof-upgrades/`.
- Page-level `demo` claim level and per-entry public status are separate axes;
  entries use `demo` for available public-demo rows and `future` for the
  incident-review concept route. No dev-only entries were included.
- Route-only entries explicitly say they have no generated scan coverage label;
  they cite `publicClaimLevel` route metadata instead of normalizing `demo` into
  a scan coverage label.
- The implementation uses existing site styles and adds no runtime services or
  JavaScript.
- Existing proof-heavy routes link back to `/proof-paths/`: demo result, demo
  proof upgrades, demo proof assets, evidence packets, manager packet,
  capabilities, and docs.
- `site/scripts/validate-demo-summary.mjs` now validates the proof-path index
  against the checked-in public demo summary fixture, artifact vocabulary,
  route links, and non-claims.

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

Spec-prep remaining local validation was completed during implementation; see
the implementation validation section below.

## Future Implementation Validation

Completed implementation validation:

- Passed: `git diff --check`
- Passed: `cd site && npm test`
  - Result: 40 tests passed.
- Passed: `cd site && npm run validate`
  - Result: built static site, validated 29 HTML files, 735 internal
    references, and 28 sitemap URLs.
- Passed: `cd site && npm run build`
  - Result: built static site to `site/dist/`.
- Passed: `./scripts/check-private-paths.sh`
  - Result: private path guard passed.
- Verified proof paths resolve to existing checked-in source artifacts,
  public-safe generated summary fixture references, or public routes in the
  implementation branch.
- Public demo generated summaries were not refreshed; the demo-public assertion
  workflow was not required.
- Desktop browser sanity check at 1280x900 on `/proof-paths/`:
  - Title and H1 loaded.
  - Canonical top navigation rendered.
  - 16 proof/index cards rendered.
  - No horizontal overflow detected.
- Mobile browser sanity check at 390x844 on `/proof-paths/`:
  - Hero buttons fit.
  - Top navigation wrapped.
  - No horizontal overflow detected.

## Follow-Up Items

- If future public-safe generated reports are checked in, add direct artifact
  links from `/proof-paths/` and extend validation to assert those links.
- If dev-only capabilities are later included, label them `dev-only` and keep
  them separate from page-level `demo` status.
- Keep any future generated proof-path data deterministic and reviewable.
