# Implementation State

Status: implemented
Readiness: implemented
Public claim level: concept

Last verified: 2026-06-14
Branch: codex/site-vault-export-concept
Source of truth: origin/main

## Summary

This spec adds a site-facing concept page for a future optional vault export
demo. The page is explicitly future-facing: it explains how TraceMap evidence
could be exported as linked Markdown notes for human exploration without
claiming that the feature is shipped in core TraceMap.

The source of truth remains the TraceMap evidence packet: facts, SQLite index,
reports, logs, rule catalog, commit metadata, coverage labels, supporting IDs,
and limitations. Obsidian-compatible vaults are framed only as a human
exploration/export layer.

## Scope

- Add `/vault-export/`.
- Link it from `/demo/` and `/workflows/` as a future concept.
- Update sitemap and site README validation URLs.

## Boundaries

- Do not claim runtime proof, release approval, production usage, or AI impact
  analysis.
- Do not imply Obsidian is required.
- Do not imply the feature is shipped until core implementation lands.
- Do not export raw source snippets, raw SQL, config values, secrets, local
  absolute paths, or raw repo remotes.

## Validation

- `npm run build`
- Browser smoke at desktop and mobile widths
