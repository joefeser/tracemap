# Site C# Extraction Truth Implementation State

- Status: implementation and local validation complete; PR publication pending
- Branch: `codex/site-csharp-extraction-truth`
- Base: `origin/dev` at `330683d859b8958450bc1f25da1f0f8779f633ed`
- Public claim level: `demo`

## Boundary

Site-only article, metadata, focused validator/tests, and this spec. No scanner
changes, source fixtures, analyzer output, generated site output, deployment,
or promotion. Claims remain bounded by issue #622.

## Validation

- Focused validator: 22 tests passed.
- Site build: passed.
- Full site tests: 851 passed.
- Site validation: 103 HTML files, 3,498 internal references, and 102 sitemap URLs.
- Private-path guard and `git diff --check`: passed.
- Desktop (1440 by 1000) and mobile (390 by 844): blog discovery and article route rendered with 11 required sections, no horizontal overflow, and no console errors or warnings.

## Follow-up

- Issue #623 will provide the companion historical-defect article and reciprocal link. It may stack on this branch while review is pending.
- The planned static-dispatch articles from issue #617 are not public on this base, so this article does not publish a broken forward link.
