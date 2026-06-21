# Implementation State

Status: implemented
Readiness: implemented
Branch: codex/site-navigation-paths
Public claim level: demo

## Summary

This phase is an information-architecture cleanup for the public site. It
should connect existing pages into clearer reader paths instead of adding a new
public route or expanding the primary navigation.

## Scope

- Add role-based pathing to the homepage.
- Update existing page links and copy so the manager packet, proof assets,
  proof upgrades, packet guide, capability matrix, and demo pages read as one
  evidence journey.
- Keep the work site-only.

## Scope Decisions

- No new route is needed for this slice.
- Do not add new top-navigation items; use in-page pathing and cross-links.
- Keep all wording at demo claim level.
- The homepage first-look section now routes by reader question instead of by
  artifact sequence.
- `/packets/`, `/use-cases/`, `/manager-packet/`, and `/demo/proof-assets/`
  received narrow cross-links back into the role-based path.

## Claim Boundaries

- Safe: TraceMap public pages can guide managers, reviewers, engineers, and demo
  readers through static evidence, generated summaries, coverage labels, rule
  IDs, proof assets, and limitations.
- Not safe: claiming runtime behavior, production traffic, deployment state,
  endpoint performance, production dependency understanding, release safety,
  incident root cause, or AI impact analysis.

## Validation Plan

- Passed: `git diff --check`
- Passed: `npm test` from `site/`
- Passed: `npm run validate` from `site/`
- Passed: desktop browser sanity check for `/` at 1440x1000, with four role cards, four evidence-journey rows, no page overflow, and no console errors.
- Passed: mobile browser sanity check for `/` at 390x844, with four stacked role cards, no page overflow, and no console errors.
- Passed: mobile browser sanity check for `/packets/` at 390x844, with the role-based first-look callout/link present, no page overflow, and no console errors.

## Follow-Up Items

- Consider a later incident/review use-case page only if it can stay bounded to
  static evidence and explicit non-claims.
