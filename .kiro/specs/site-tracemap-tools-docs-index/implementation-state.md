# Implementation State

Status: implemented
Readiness: implemented
Public claim level: demo

Last verified: 2026-06-15
Branch: codex/site-docs-index
Source of truth: origin/main

## Summary

This spec adds a static `/docs/` page to `tracemap.tools`. The page maps the
core public repository docs: PRD, validation guide, acceptance plan, language
adapter contract, decisions, and next execution report. It is a discovery layer,
not a replacement for repository docs.

The homepage source-of-truth section and validation page footer now link to the
docs index, and `/docs/` is included in generated sitemap metadata.

## Validation

- `npm test`
- `npm run validate`

## Follow-ups

- Consider rendering selected docs into site pages if repository docs become a
  primary public documentation surface rather than source-of-truth references.
