# Implementation State

Status: implemented
Last verified: 2026-06-14
Branch: codex/site-tracemap-tools-next
Source of truth: merged site discovery PR

## Summary

This slice extends `tracemap.tools` from a single homepage into a crawlable
static discovery surface. The target pages are evidence, outputs, workflows,
validation, limitations, and demo.

## Scope

- Add stable static URLs under `site/src`.
- Keep public claims bounded to deterministic static analysis evidence.
- Update sitemap and deployment docs.
- Add a static 404 page.

## Validation

- `npm run build`
- local HTTP smoke for `/`, `/evidence/`, `/outputs/`, `/workflows/`,
  `/validation/`, `/limitations/`, `/demo/`, `/404/`, `/robots.txt`, and
  `/sitemap.xml`
- browser desktop overflow check on `/`
- browser mobile overflow checks on `/` and `/workflows/`

## Follow-ups

- Consider a later `site-docs-content-import` slice if repo docs should be
  rendered directly as site pages instead of linked to GitHub.
- Consider generated report examples once demo artifacts are stable enough for
  public publishing.
