# Site Graphify Lessons Implementation State

- Branch: `codex/site-graphify-lessons`
- Stack base: `codex/site-gap-becomes-rule` at `bbb3f09e` / PR #631, which itself stacks on PR #630.
- Scope: issue #624 only; one article, metadata, validator, tests, and this spec.
- Claim level: concept. External Graphify material is research context, never TraceMap evidence.
- Research note: current Graphify public documentation describes deterministic local code parsing and a traversable graph rather than a vector index; the article avoids freezing older product descriptions into current claims.
- Validation: focused validator 15/15; cumulative site tests 938/938; build and validate passed with 109 HTML files, 3,658 internal references, and 108 sitemap URLs; desktop 1,440×1,000 and mobile 390×844 browser checks passed without overflow or console warnings/errors; private-path and diff checks pending commit review.
- Stack rule: this child must not merge into either parent branch; after #630 and #631 merge in order, retarget this PR to `dev`.
