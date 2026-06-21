# site-tracemap-tools-legacy-validation-concept implementation state

Status: implemented
Readiness: implemented
Public claim level: concept

## Branch

`codex/site-legacy-validation-concept`

## Scope

This is a site/content phase only. It publishes `/legacy-validation/` as a
concept page that explains the legacy codebase validation plan without
presenting validation results or claiming shipped legacy support.

The underlying core spec says: `Public claim level: hidden until a redacted
validation summary exists.` The site page therefore describes the validation
problem and safety boundaries, not completed validation evidence.

## Claim Boundaries

- Do not claim TraceMap already supports arbitrary old or large .NET codebases.
- Do not publish local sample paths, private repo names, raw remotes, source
  snippets, raw SQL, config values, connection strings, or secrets.
- Do not claim runtime behavior, UI reachability, production traffic,
  deployment state, endpoint performance, incident cause, or release safety.
- Do not upgrade this page to `demo` until redacted validation summaries exist
  and pass pre-publish safety checks.

## Validation

- `npm test`
- `npm run validate`
- `git diff --check`
- Browser sanity for `/legacy-validation/` desktop and mobile
- Browser sanity for linked doorway pages

## Follow-Ups

- Upgrade or add a `/legacy-validation/result/` page only after core produces a
  public-safe redacted validation summary.
- Consider a blog article after the concept page lands: "Why messy legacy code
  is the real test for static evidence."
- Link future `legacy-ui-event-surfaces` or `legacy-scan-performance-bounds`
  specs if core creates them.
