# Implementation State

Status: not-started
Branch: codex/site-demo-proof-upgrades-spec
Public claim level: demo

## Summary

This spec is ready for implementation, but the `/demo/proof-upgrades/` page has
not been added yet. The unchecked boxes in `tasks.md` are the current ready
implementation checklist for the next site PR.

## Branch

Planned implementation branch: `codex/site-demo-proof-upgrades`

Spec drafting branch: `codex/site-demo-proof-upgrades-spec`

## Scope

This spec queues a public `/demo/proof-upgrades/` page. The page should explain how the rows previously deferred on `/demo/result/` now have reproducible demo evidence on `main`, while keeping `PartialAnalysis`, static-evidence boundaries, and public-safe artifact rules visible.

## Evidence State

Coordinator checked current `origin/main` and `origin/dev` after `main` was merged into `dev`. A fresh run of `./scripts/demo-public.sh <ignored-out>` passed and produced available sections for all six rows.

All six rows are safe to describe as `demo`:

- combine-and-dependency-report
- paths-and-reverse
- portfolio
- diff
- impact
- release-review

The reports are demo evidence with `PartialAnalysis` coverage where applicable. They are not full proof, runtime proof, release approval, or production usage evidence.

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

- `npm test` from `site/`
- `npm run validate` from `site/`
- `git diff --check`
- Confirm `site/package.json` still defines `validate` before relying on `npm run validate`.
- Confirm `samples/public-demo/before/` and `samples/public-demo/after/` exist before publishing before/after fixture copy.
- Desktop browser sanity check for `/demo/proof-upgrades/`
- Mobile browser sanity check for `/demo/proof-upgrades/`

## Follow-Up Items

- Keep coordinating with core/demo agents if evidence counts, generated report paths, or claim levels change.
- If generated summaries or counts change, update this spec before implementing the page copy.
- Keep implementation copy aligned with `/demo/result/` and `/roadmap/`.
