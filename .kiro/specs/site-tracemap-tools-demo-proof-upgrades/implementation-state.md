# Site TraceMap Tools Demo Proof Upgrades Implementation State

Public claim level: concept

## Status

Not started.

## Branch

Planned implementation branch: `codex/site-demo-proof-upgrades`

Spec drafting branch: `codex/site-demo-proof-upgrades-spec`

## Scope

This spec queues a public `/demo/proof-upgrades/` page. The page should explain how the deferred rows currently visible on `/demo/result/` can earn stronger public demo claims.

## Scope Decisions

- Treat the overall page as concept-level until the deferred rows have checked-in public proof.
- Do not upgrade any row to demo unless the supporting sample, generated summary, or public-safe artifact exists.
- Keep the page focused on the proof ladder, not a broad product roadmap.
- Use `/roadmap/` for the general claim ledger and `/demo/proof-upgrades/` for deferred demo-row graduation criteria.

## Claim Boundaries

- Safe to say: the current public demo has deferred rows for combine/report, paths/reverse, portfolio, diff, impact, and release-review.
- Safe to say: each deferred row needs checked-in proof, generated public-safe summaries, rule IDs, evidence tiers, coverage labels, supporting IDs or counts where applicable, and limitations before public demo upgrade.
- Not safe to say: any deferred row is currently demonstrated unless implementation and public-safe generated evidence have landed.
- Not safe to say: TraceMap proves runtime behavior, production traffic, deployment state, endpoint performance, release safety, or AI impact analysis.

## Validation Plan

When implemented:

- `npm test` from `site/`
- `npm run validate` from `site/`
- `git diff --check`
- Desktop browser sanity check for `/demo/proof-upgrades/`
- Mobile browser sanity check for `/demo/proof-upgrades/`

## Follow-Up Items

- Coordinate with core/demo agents on which deferred rows have enough checked-in evidence to become demo rows.
- If generated summaries for any deferred row land first, update this spec before implementing the page copy.
- Keep implementation copy aligned with `/demo/result/` and `/roadmap/`.
