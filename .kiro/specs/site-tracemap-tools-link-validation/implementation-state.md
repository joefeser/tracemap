# Implementation State

Status: implemented
Readiness: implemented
Public claim level: hidden

Last verified: 2026-06-14
Branch: codex/site-link-validation
Source of truth: origin/main

## Summary

This spec adds a local `npm run validate` command for `tracemap.tools`.
Validation builds the site, then checks generated `dist` output for sitemap URL
targets, internal HTML `href`/`src` targets, and the canonical `robots.txt`
sitemap directive.

The validation step does not crawl external URLs and does not add dependencies.
It uses Node.js standard library APIs only and is intended as a local/CI quality
gate before publishing static output.

## Validation

- `npm test`
- `npm run build`
- `npm run validate`

## Follow-ups

- Deployment and pull request validation are handled by
  `.kiro/specs/site-tracemap-tools-deploy-validation/`.
