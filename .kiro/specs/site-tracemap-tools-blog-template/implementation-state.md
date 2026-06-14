# Implementation State

Status: implemented
Last verified: 2026-06-14
Branch: codex/site-blog-template-build
Source of truth: pending PR

## Summary

This spec adds a small static blog generation step for `tracemap.tools`.
Articles are now described by metadata in `site/src/_blog/articles.json`, with
body HTML fragments under `site/src/_blog/articles/`. The build script excludes
underscore-prefixed private source folders from `dist`, then generates the blog
index and each article page with shared navigation, metadata, article structure,
callouts, and footer.

The output is still plain static HTML under `site/dist`. No framework, backend,
database, auth, or runtime service was added.

## Validation

- `npm run build`
- Browser smoke at desktop and mobile widths

## Follow-ups

- Consider generating `sitemap.xml` from the same blog metadata if the article
  list grows enough for manual sitemap edits to become noisy.
