# Implementation State

Status: implemented
Last verified: 2026-06-14
Branch: codex/site-sitemap-generation
Source of truth: working tree

## Summary

This spec makes `site/dist/sitemap.xml` generated output. Static page metadata
lives in `site/src/_site/pages.json`; generated blog article URLs are derived
from `site/src/_blog/articles.json`. The build validates page paths,
changefreq values, priorities, and duplicate URLs before writing the sitemap.

The output remains plain static XML under `site/dist`. No framework, backend,
database, auth, runtime service, crawler, or external package was added.

## Validation

- `npm test`
- `npm run build`
- `curl -fsS http://localhost:4187/sitemap.xml | rg "https://tracemap\\.tools/(vault-export|blog/why-tracemap-exists)/"`

## Follow-ups

- Consider deriving the static page metadata from a broader generated page
  manifest if the site later moves more shared chrome out of hand-authored HTML.
