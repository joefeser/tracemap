# Implementation State

Status: implemented
Readiness: implemented
Branch: codex/site-demo-proof-upgrades
Public claim level: demo

## Summary

The `/demo/proof-upgrades/` page has been implemented with linked updates from
`/demo/`, `/demo/result/`, `/roadmap/`, `/demo/start-here/`, and sitemap
metadata. Site validation and browser sanity checks passed in this branch.

## Branch

Planned implementation branch: `codex/site-demo-proof-upgrades`

Spec drafting branch: `codex/site-demo-proof-upgrades-spec`

## Scope

This spec queues a public `/demo/proof-upgrades/` page. The page should explain how the rows previously deferred on `/demo/result/` now have reproducible demo evidence on `main`, while keeping `PartialAnalysis`, static-evidence boundaries, and public-safe artifact rules visible.

## Evidence State

Coordinator checked current `origin/main` and `origin/dev` after `main` was merged into `dev`. A fresh Codex run of `./scripts/demo-public.sh /tmp/tracemap-demo-proof-upgrades` passed on June 15, 2026 and produced available sections for all six rows.

All six rows are safe to describe as `demo`:

- combine-and-dependency-report
- paths-and-reverse
- portfolio
- diff
- impact
- release-review

The reports are demo evidence with `PartialAnalysis` coverage where applicable. They are not full proof, runtime proof, release approval, or production usage evidence.

Current refreshed counts from `/tmp/tracemap-demo-proof-upgrades/demo-summary.json`:

- combine-and-dependency-report: 6 sources, 14 endpoint findings, 62 dependency surfaces, 77 dependency edges, 2 gaps.
- paths-and-reverse: 12 paths, 37 path gaps, 25 reverse paths, 6 reverse roots, 23 reverse gaps, 5 selected surfaces.
- portfolio: 2 portfolio inputs, 6 sources, 9 dependency surfaces, 10 dependency edges, 4 endpoint findings, 4 gaps.
- diff: 14 diff rows, 12 surface diffs, 0 endpoint diffs, 0 edge diffs, 0 gaps.
- impact: 14 diff rows considered, 12 impact items, 12 surface impacts, 0 endpoint impacts, 0 edge impacts, 0 gaps.
- release-review: 50 findings, 50 top changed surfaces, 0 contract findings, 3 gaps, 53 checklist items.

## Scope Decisions

- Treat the overall page as demo-level because checked-in samples and generated public-safe summaries now exist for the six rows.
- Keep the page focused on the demo evidence ledger, not a broad product roadmap.
- Use `/roadmap/` for the general claim ledger and `/demo/proof-upgrades/` for demo-row evidence details and remaining boundaries.
- Preserve promotion gates for stronger claims: static demo evidence is not runtime proof, production proof, release approval, or endpoint performance proof.
- Implement the page using the existing `/demo/result/` long-form pattern: page hero, split sections, repeated evidence cards or detail rows, boundary section, and source-material link section.
- Update `/demo/result/` and `/roadmap/` in the same implementation slice so they do not contradict the new demo-evidence page.

## Claim Boundaries

- Safe to say: the public demo has reproducible demo evidence for combine/report, paths/reverse, portfolio, diff, impact, and release-review.
- Safe to say: generated reports are static, rule-backed, coverage-labeled, and public-safe when produced from checked-in public samples.
- Safe to say: `PartialAnalysis` and gaps remain part of the evidence and should stay visible.
- Not safe to say: demo evidence proves full impact, runtime topology, deployment, auth, traffic, production behavior, release approval, endpoint performance, production dependency understanding, or production usage.
- Not safe to say: TraceMap proves runtime behavior, production traffic, deployment state, endpoint performance, release safety, or AI impact analysis.

## Public-Safe Publishing Boundary

Safe to publish or summarize:

- `demo-summary.md`
- `demo-summary.json`
- generated report Markdown/JSON under `<out>/reports/...`

Do not publish:

- generated scan directories
- `facts.ndjson`
- `index.sqlite`
- combined SQLite files
- analyzer logs
- local absolute output roots
- raw SQL/config/source snippets

## Recommended Wording

Use:

> These rows have reproducible demo evidence from checked-in samples. The generated reports are static, rule-backed, and coverage-labeled. They do not prove runtime behavior or release safety.

Avoid:

- TraceMap proves impact.
- TraceMap validates a release.
- TraceMap traces runtime calls.
- TraceMap understands production dependencies.

## Validation Plan

When implemented:

- Passed: `npm test` from `site/`
- Passed: `npm run validate` from `site/`
- Passed: `git diff --check`
- Confirm `site/package.json` still defines `validate` before relying on `npm run validate`.
- Confirmed `site/package.json` defines `validate` as `node scripts/validate.mjs`.
- Confirmed `samples/public-demo/before/` and `samples/public-demo/after/` exist before publishing before/after fixture copy.
- Passed: desktop browser sanity check for `/demo/proof-upgrades/` at 1440x1000, with six cards, no horizontal overflow, and no console errors.
- Passed: mobile browser sanity check for `/demo/proof-upgrades/` at 390x844, with six stacked cards, no page-level horizontal overflow, and no console errors.

## Follow-Up Items

- Keep coordinating with core/demo agents if evidence counts, generated report paths, or claim levels change.
- If generated summaries or counts change, update this spec before implementing the page copy.
- Keep implementation copy aligned with `/demo/result/` and `/roadmap/`.
