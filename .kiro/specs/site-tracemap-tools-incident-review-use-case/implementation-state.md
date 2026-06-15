# Site TraceMap Tools Incident Review Use Case Implementation State

Status: spec-ready
Last verified: 2026-06-15
Current branch: codex/spec-site-incident-review-use-case
Planned implementation branch: codex/site-incident-review-use-case
Source of truth: spec packet
Public claim level: concept

## Summary

This spec queues a future public site page at
`/use-cases/incident-review/`. The page should help engineers, reviewers,
managers, and architects use TraceMap static evidence to orient code questions
during review or incident follow-up without claiming runtime proof, production
state, incident root cause, or release safety.

This PR is spec-only. It intentionally does not implement the route, edit
`site/src/`, update sitemap metadata, change generated outputs, or touch core
scanner/reducer code.

## Scope Decisions

- Use `Public claim level: concept` because incident-adjacent copy can easily be
  mistaken for observability, incident response, root-cause, or release-safety
  claims.
- Recommend `/use-cases/incident-review/` instead of `/incident-review/` so the
  page stays nested under the existing use-case information architecture.
- Require public copy to stay tied to static evidence, rule IDs or status
  framing, evidence tiers, coverage labels, proof paths, and limitations.
- Require links to `/manager-packet/`, `/packets/`, `/demo/proof-assets/`,
  `/demo/proof-upgrades/`, `/capabilities/`, `/limitations/`, `/roadmap/`,
  `/evidence/`, and `/outputs/` in the future implementation.
- Leave all implementation tasks unchecked because this branch only delivers the
  ready spec packet.

## Boundaries

- Do not claim runtime behavior, production traffic, deployment state, endpoint
  performance, production dependency understanding, P1 root cause, release
  safety, release approval, or AI impact analysis.
- Do not imply TraceMap replaces Dynatrace, logs, traces, incident response,
  ownership, tests, human review, code review approval, or release approval.
- Do not publish raw source snippets, raw SQL, config values, secrets, local
  absolute paths, raw repo remotes, raw `facts.ndjson`, `index.sqlite`,
  combined SQLite files, generated scan directories, analyzer logs, or private
  sample identities.
- Do not upgrade this route to `demo` until a future checked-in public-safe
  demo slice supports the route-specific examples.

## Validation Completed For Spec-Only Work

- Read `AGENTS.md`.
- Reviewed current site spec patterns, including manager packet, proof assets,
  demo walkthrough, public demo result, legacy validation concept, vault export
  concept, and navigation path specs.
- Confirmed no open PRs targeting `main` before creating this branch.
- Fast-forwarded `codex/spec-site-incident-review-use-case` to current
  `origin/main` before staging the spec.
- Passed `git diff --check`.
- Passed `node scripts/kiro-review.mjs --self-test`.
- Passed `npm test` from `site/`.
- Passed `npm run validate` from `site/`.
- Re-ran the same four validation commands after patching PR review-loop
  feedback.
- Deferred `npm run build` and browser checks because this PR is spec-only and
  does not change `site/src/`.

## Kiro Review Artifacts

- Opus spec review completed with full coverage:
  `.tmp/kiro-reviews/site-tracemap-tools-incident-review-use-case/2026-06-15T213802-853Z-spec-claude-opus-4.8.clean.md`.
- Patched Opus Important findings:
  - stated canonical top-navigation requirements in requirements and design;
  - made manual claim/safety review explicit because site validation does not
    check public-claim language;
  - normalized the canonical back-link set across requirements, design, and
    tasks.
- Sonnet spec review completed with full coverage:
  `.tmp/kiro-reviews/site-tracemap-tools-incident-review-use-case/2026-06-15T214336-708Z-spec-claude-sonnet-4.6.clean.md`.
- Patched Sonnet must-fix findings:
  - clarified top-navigation link-set matching and `aria-current` behavior;
  - documented that the sitemap entry must land with the HTML page;
  - recorded the Sonnet review artifact in this implementation state.
- Also patched narrow Sonnet suggestions around `/use-cases/` placement,
  conditional cross-link asymmetry, roadmap handling, `hero-note`, proof-path
  descriptions, and durable manual claim review.
- Both Kiro reviews emitted an MCP settings warning, but the wrapper completed
  with full coverage and saved prompt/raw/clean/meta review artifacts.
- PR review loop patched current Gemini Medium findings:
  - clarified that future source HTML should include a placeholder
    `<header class="site-header">` for build-time replacement instead of
    hand-synchronizing navigation links;
  - added `/evidence/` and `/outputs/` to the future page's canonical proof
    link set.

## Future Manual Review Checklist

- Confirm the page visibly says `Public claim level: concept`.
- Confirm copy does not claim runtime behavior, production traffic, deployment
  state, endpoint performance, production dependency understanding, P1 root
  cause, release safety, release approval, or AI impact analysis.
- Confirm copy does not imply TraceMap replaces Dynatrace, logs, traces,
  incident response, ownership, tests, human review, code review approval, or
  release approval.
- Confirm no raw source snippets, raw SQL, config values, secrets, local
  absolute paths, raw repo remotes, raw facts, SQLite files, combined SQLite
  files, generated scan directories, analyzer logs, or private sample
  identities are published.
- Record each cross-link actually added, especially conditional links from
  `/demo/proof-assets/`, `/demo/proof-upgrades/`, `/limitations/`, and
  `/roadmap/`, with the bounded wording used.
- If content-only implementation exposes a navigation edge not covered by
  existing tests, consider adding a focused `currentNavValue` unit test for a
  nested `/use-cases/*/` route; this is optional unless build navigation logic
  changes.

## Follow-Ups

- Future implementation should create `site/src/use-cases/incident-review/index.html`.
- Future implementation should add `/use-cases/incident-review/` to
  `site/src/_site/pages.json`.
- Future implementation should add bounded cross-links from the existing public
  site pages named in `requirements.md`.
- Future implementation should run the site validation and browser sanity checks
  listed in `tasks.md`.
