# site-tracemap-tools-evidence-packets implementation state

Status: implemented
Readiness: implemented
Public claim level: demo

## Branch

`codex/site-evidence-packets`

## Scope

This is a website/content phase only. It adds `/packets/` as a public
explanation of how a TraceMap evidence packet can be read by managers,
reviewers, architects, and engineers.

The page does not add scanner, reducer, export, or runtime behavior. It treats
the generated artifacts as the source of truth and uses the site as a human
orientation layer.

## Claim boundaries

- Do not say TraceMap proves runtime behavior.
- Do not say TraceMap proves production traffic, endpoint performance,
  deployment state, or release safety.
- Do not say this is AI analysis.
- Do not imply private SQLite/facts/log artifacts are safe to publish.
- Keep rule IDs, evidence tiers, commit SHA, coverage labels, supporting IDs,
  and limitations as the vocabulary for public claims.

## Validation

- `npm test`
- `npm run validate`
- `git diff --check`

## Follow-ups

- Add a generated packet screenshot or downloadable public sample only after a
  checked-in public-safe packet exists.
- Consider linking future vault export copy back to `/packets/` once an
  implementation lands.
