# Implementation State

Status: implemented
Last verified: 2026-06-14
Branch: codex/site-examples-and-blog-runway
Source of truth: pending PR

## Summary

This slice adds public example pages for TraceMap artifact shapes and a use-cases
page that frames how the examples map to review work.

## Scope

- Add `/examples/`.
- Add `/examples/scan-packet/`.
- Add `/examples/contract-impact/`.
- Add `/use-cases/`.
- Update navigation and sitemap metadata.

## Validation

- `npm run build`
- local HTTP smoke for `/`, `/examples/`, `/examples/scan-packet/`,
  `/examples/contract-impact/`, `/use-cases/`, and `/sitemap.xml`
- sitemap contains examples and use-cases URLs
- browser desktop overflow check on `/examples/`
- browser mobile overflow checks on `/examples/scan-packet/` and `/use-cases/`

## Follow-ups

- Replace representative snippets with generated checked-in demo artifacts if
  the public demo later writes stable sanitized examples.
- Consider a tiny template/layout step if navigation drift continues across
  static HTML pages.
- Blog/editorial ideas remain tracked in
  `.kiro/specs/site-tracemap-tools-blog-runway/`.
