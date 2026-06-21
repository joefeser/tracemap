# Site TraceMap Tools Roadmap Implementation State

Status: implemented
Readiness: implemented
Public claim level: concept

## Branch

`codex/site-roadmap`

## Scope

This phase adds a site-facing roadmap page at `/roadmap/`. The page frames site work as a public claim ledger, not a delivery promise board.

## Claim Boundaries

- Safe to say: public site work is gated by current `main` evidence, demo evidence, concept framing, and hidden evidence that is not suitable for public copy yet.
- Safe to say: future public upgrades need proof paths such as checked-in samples, generated public-safe summaries, rule IDs, evidence tiers, coverage labels, and limitations.
- Not safe to say: TraceMap proves runtime behavior, production traffic, deployment state, endpoint performance, release safety, or AI impact analysis.
- Not safe to publish: raw local validation details, raw scan artifacts from private repositories, source snippets, SQL/config values, secrets, local absolute paths, or raw repository remotes.

## Validation

- `npm test`
- `npm run validate`
- Desktop and mobile browser sanity checks against local site preview

## Follow-Up

Upgrade roadmap rows only when the relevant proof lands on `main` or exists as a reproducible public demo artifact.
