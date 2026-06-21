# Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

This spec-only packet defines a future public blog/content phase for one or
more TraceMap proof-path articles. The future phase should explain why
deterministic evidence matters, how to read proof paths, and what TraceMap
cannot prove, while staying inside concept-level public claims unless a
specific article has public demo proof-path backing.

Readiness `ready-for-implementation` reflects disposition of available
spec-review findings. Spec-packet validation, commit, PR creation, and PR-loop
steps remain tracked in `tasks.md`.

`Status: not-started` tracks the future site/content implementation phase.
Spec-packet authoring and review progress is tracked in `tasks.md` and this
state note.

No site source, generated output, scanner code, reducer code, existing specs,
or validation scripts were changed by this spec packet.

## Branch And Worktree

- Branch: `codex/spec-site-blog-proof-path-series`
- Worktree: isolated spec worktree requested by the operator; exact local path
  is omitted from the checked-in spec packet to satisfy the private-path guard.
- Base: `origin/main`
- Target PR base: `main`

## Scope Decisions

- Scope is limited to
  `.kiro/specs/site-tracemap-tools-blog-proof-path-series/`.
- The phase is spec-only and records future implementation requirements.
- Default article claim level is `concept`.
- `demo` is allowed only for an article with public proof-path or public demo
  backing recorded in metadata and implementation-state.
- Candidate articles are intentionally optional so a future implementer can
  choose one strong article or a short series.
- Future implementation must check existing blog slugs:
  `why-tracemap-exists`, `what-tracemap-solves-for-engineering-teams`, and
  `building-tracemap-with-codex-kiro-qodo`.
- Future implementation must record selected article count, final slugs,
  rejected article ideas, claim levels, and rationale.
- Current blog metadata does not expose a claim-level field, so future
  implementation must choose visible article-body claim-level text or a
  `publicClaimLevel` metadata/rendering/validation extension.
- Existing public-site content validation usually uses dedicated modules under
  `site/scripts/` with matching `*.test.mjs` files; future focused article
  validation should follow that convention.

## Current Claim Boundaries

Safe to specify for future public copy:

- Proof paths keep public claims attached to reviewable public surfaces.
- Static evidence can help reviewers and managers ask better questions before
  runtime evidence is available.
- Rule IDs, evidence tiers, coverage labels, limitations, and proof surfaces
  are useful when deciding whether a public claim can be repeated.

Not safe to claim:

- Runtime behavior, production traffic, endpoint performance, outage cause,
  release safety, operational safety, complete coverage, AI/LLM impact
  analysis, embedding/vector-database analysis, or prompt classification.
- Replacement of telemetry, logs, traces, tests, owners, human review, or
  release process.
- Publication of raw facts, SQLite content, analyzer logs, source snippets,
  SQL, config values, secrets, local paths, private remotes, generated scan
  dirs, private names, or hidden validation details.

## Review Log

- Review artifacts: `.tmp/kiro-reviews/site-tracemap-tools-blog-proof-path-series/`
  (not committed).
  Inline summaries in this file are the authoritative resume record because
  `.tmp` review artifacts are local-only and not committed.
- Completed with reduced coverage due to denied tool access:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-blog-proof-path-series --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`.
  Review artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-blog-proof-path-series/2026-06-21T023538-172Z-spec-claude-opus-4.8.clean.md`.
  Opus found two Medium issues: missing per-route validation-module
  convention, and undefined claim-level mechanism for blog articles. Both were
  patched in `requirements.md`, `design.md`, `tasks.md`, and this state note.
  Low sitemap/discovery/schema/state-note clarity items were also patched.
- Completed with reduced coverage due to denied tool access:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-blog-proof-path-series --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`.
  Review artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-blog-proof-path-series/2026-06-21T023538-253Z-spec-claude-sonnet-4.6.clean.md`.
  Sonnet found no Medium or High findings. Low word-count, review-packet, task
  gate, and review-artifact clarity items were patched.
- Completed with reduced coverage due to denied tool access:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-blog-proof-path-series --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`.
  Review artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-blog-proof-path-series/2026-06-21T023924-807Z-re-review-claude-sonnet-4.6.clean.md`.
  Sonnet re-review found no Medium or High findings.
- Completed with full coverage:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-blog-proof-path-series --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`.
  Review artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-blog-proof-path-series/2026-06-21T023924-749Z-re-review-claude-opus-4.8.clean.md`.
  Opus re-review found two Medium consistency issues: readiness advanced while
  a re-review line still said pending, and reduced-coverage review caveats were
  not surfaced near readiness. Both were patched here and in
  `review-packet.md`. Low single-article route coverage, safe-term validation,
  and stale initial-readiness task wording were also patched.
- Completed with reduced coverage due to denied tool access:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-blog-proof-path-series --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`.
  Review artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-blog-proof-path-series/2026-06-21T024249-189Z-re-review-claude-sonnet-4.6.clean.md`.
  Sonnet found two Medium consistency issues: the spec-review gate task was
  still open despite readiness being set to `ready-for-implementation`, and the
  review log still had a pending final re-review line. Both were patched in
  `tasks.md` and this state note. Low single-article route coverage and
  word-count-tier clarity items were patched.
- Completed with reduced coverage due to denied tool access:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-blog-proof-path-series --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`.
  Review artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-blog-proof-path-series/2026-06-21T024249-085Z-re-review-claude-opus-4.8.clean.md`.
  Opus found one Medium consistency issue: the stale pending final re-review
  line conflicted with readiness. It was patched here. Low status, sitemap,
  local-artifact, and word-count notes were patched or already covered.
- Disposition: no further spec-only Kiro re-review is required before commit.
  The latest findings were state/checklist consistency items introduced by
  recording the in-progress review loop. The PR review loop is the confirming
  pass for the ready PR.

## Validation Log

- Passed: `git diff --check`.
- Initial staged run of `./scripts/check-private-paths.sh` failed because this
  state note included the machine-local isolated worktree path. Patched by
  replacing the checked-in path with a generic isolated-worktree note.
- Passed final rerun: `./scripts/check-private-paths.sh`.
- Passed: focused Node text checks for required headers, required files,
  required links, duplicate-slug guards, required content blocks, forbidden
  claim topics, `publicClaimLevel` metadata option, and the dedicated
  `site/scripts/<name>.mjs` plus `<name>.test.mjs` validation convention.

## PR Loop Log

- Initial PR loop command:
  `npm run dev -- pr-loop --repo joefeser/tracemap --pr 252 --base main --require-codex-review --quiet --json`.
- Initial PR loop decision: `actionable_findings`.
- Initial PR loop stop reason: `ACTIONABLE_BOT_FINDINGS`.
- Initial PR loop next action: `patch_actionable_findings`.
- Actionable finding patched: Qodo reported the spec-delivery checkbox in
  `tasks.md` still showed the PR/PR-loop step as unchecked after that work had
  begun. The checkbox is now checked to keep Kiro task state aligned with the
  delivery status.

## Oddities

- Initial review wrappers completed with reduced coverage because Kiro denied a
  shell tool request to create review-output directories. The wrapper still
  saved prompt/raw/clean/meta artifacts.
- The review-packet is the standing review prompt and gate checklist; review
  findings and dispositions are recorded in this implementation-state note and
  in uncommitted `.tmp/kiro-reviews/` artifacts.
- `site/src/_blog/articles.json` currently has no claim-level field.
- Existing blog articles currently appear in sitemap output, while blog
  articles are not represented in discovery metadata or `llms` outputs.

## Follow-Up Items For Future Implementation

- Choose article count and final slugs.
- Record rejected article ideas.
- Verify required public routes before publishing links.
- Add deterministic content validation for required blocks, metadata,
  forbidden claims, private/raw material, and word count bounds.
- Use the existing dedicated `site/scripts/<name>.mjs` plus
  `<name>.test.mjs` validation pattern for focused article checks.
- Run desktop and mobile browser sanity checks when article pages are
  implemented.
