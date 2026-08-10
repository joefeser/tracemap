# Site Graph History Bugs Implementation State

- Status: implementation and local validation complete; stacked PR publication pending
- Branch: codex/site-graph-history-bugs
- Stack base: codex/site-csharp-extraction-truth at 392d8bea
- Public claim level: demo

## Boundary

Site-only article, reciprocal link, metadata, focused validator/tests, and this
spec. No scanner changes, fixture publication, analyzer output, generated site
output, deployment, merge, or promotion. Claims remain bounded by issue #623.

## Validation

- Focused validator: 16 tests passed.
- Site build: passed.
- Full site tests: 867 passed after merging the reviewed #628 validator hardening.
- Site validation: 104 HTML files, 3,524 internal references, and 103 sitemap URLs.
- Private-path guard and git diff check: passed.
- Desktop (1440 by 1000) and mobile (390 by 844): blog discovery, article route, and reciprocal companion link rendered with 12 required sections, no horizontal overflow, and no console errors or warnings.

## Stack

This branch is intentionally based on issue #622 / PR #628 so the two companion
articles can cross-link. Rebase the PR base to dev after #628 lands.
