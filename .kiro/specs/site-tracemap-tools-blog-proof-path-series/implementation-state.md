# Implementation State

Status: implemented
Readiness: implemented
Public claim level: concept

## Summary

This implementation publishes one concept-level public blog article for the
proof-path series and adds deterministic validation for the article. The work
stays inside the existing static site blog system under `site/src/_blog/` and
does not add scanner, reducer, runtime service, analytics, AI/LLM, embeddings,
vector database, or prompt-classification behavior.

The article is a practical proof-path reading guide. It teaches readers how to
move from a public claim to proof surfaces, evidence vocabulary, limitations,
safe wording, wording to avoid, and a human handoff when runtime or ownership
questions remain.

## Branch And Worktree

- Branch: `codex/impl-site-blog-proof-path-series`
- Worktree: isolated implementation worktree requested by the operator; exact
  local path is omitted from the checked-in state note to satisfy the
  private-path guard.
- Base: `origin/dev`
- Target PR base: `dev`
- Spec sync note: `.kiro/specs/site-tracemap-tools-blog-proof-path-series/`
  was absent from the `origin/dev` worktree and was restored from
  `origin/main` before implementation because main and dev were temporarily out
  of sync.

## Article Decisions

- Selected article count: one article.
- Final slug: `what-a-proof-path-is`.
- Final title: `What a Proof Path Is`.
- Public claim level: `concept`.
- Claim-level mechanism: visible article-body text,
  `Public claim level: concept`. This avoids extending the blog metadata schema
  for a single concept article while still making the claim level rendered and
  machine-checkable by the focused validator.
- Target word count range: 900 to 1,800 rendered words for the single primary
  article. The focused validator enforces this range.
- Rationale: one strong definition and reading article is the smallest
  reviewable content phase and avoids overlapping with the existing origin,
  engineering-team, and project-workflow posts.

## Rejected Or Deferred Article Ideas

- `How to read static evidence without overclaiming`: deferred because the
  selected article already includes the reading steps, safe wording, unsafe
  wording, and non-claim boundary for this first phase.
- `What TraceMap can bring to a review before runtime telemetry`: deferred
  because it risks drifting into runtime-review positioning unless it gets its
  own narrow follow-up spec.
- `Why no public conclusion without evidence matters`: deferred because the
  shared principle is covered inside the selected proof-path article and the
  existing site already repeats that boundary across evidence and review pages.

## Implemented Scope

- Added `site/src/_blog/articles/what-a-proof-path-is.html`.
- Registered the article in `site/src/_blog/articles.json` with conservative
  metadata and June 21, 2026 publication date.
- Added a contextual inbound link from `/proof-paths/` to the new article.
  Primary navigation was not changed because the generated blog index already
  exposes the article and the proof-path page is the relevant discovery surface.
- Added `site/scripts/blog-proof-path-series.mjs` with deterministic checks for
  required article blocks, required proof-surface links, metadata, blog index
  registration, sitemap entry, rendered claim-level text, word count, forbidden
  claims, and private/raw material.
- Added `site/scripts/blog-proof-path-series.test.mjs` with acceptance and
  regression tests for missing blocks, missing links, forbidden claims,
  sanctioned wording-to-avoid examples, raw material, and metadata regressions.
- Registered the focused validator in `site/scripts/validate.mjs`.
- Updated the aggregate validation fixture in `site/scripts/validate.test.mjs`
  so full-site validation exercises the new blog article route.

## Verified Links

The required public routes were present in `site/src/` before publication:

- `/proof-paths/`
- `/proof-paths/tour/`
- `/proof-source-catalog/`
- `/evidence/`
- `/packets/`
- `/packets/assembly/`
- `/review-claim-checklist/`
- `/static-vs-runtime/`
- `/limitations/`
- `/validation/`
- `/demo/result/`
- `/questions/`

The article links the full route set because the single article is broad enough
to serve as the first proof-path reading guide.

## Public-Safety Decisions

- The article uses sanitized explanatory examples only.
- Unsafe wording is isolated in an explicit wording-to-avoid section marked
  with `data-tm-boundary`.
- Raw artifact names are mentioned only as local output families in a bounded
  limitations/non-claims section.
- No raw facts, raw SQLite content, analyzer logs, source snippets, SQL, config
  values, secrets, local paths, raw remotes, generated scan directories, private
  sample names, hidden validation details, or raw command output are published.
- No runtime behavior proof, production traffic proof, endpoint performance
  proof, outage cause proof, release safety proof, operational safety proof,
  complete coverage proof, AI/LLM impact analysis, embeddings, vector
  databases, prompt classification, autonomous approval, or replacement of
  tests/code review/source review/runtime observability/human judgment is
  claimed.

## Validation Log

- Passed: `cd site && npm test -- --test-name-pattern='BlogProofPath|blog proof|validateDist accepts'`.
- Passed: `cd site && npm run validate`.
- Passed: `git diff --check`.
- Passed: `./scripts/check-private-paths.sh`.
- Passed: `cd site && npm test` with 315 tests.
- Passed: `cd site && npm run validate`; generated output checked 53 HTML
  files, 1,769 internal references, 52 sitemap URLs, one legacy story safety
  target, and 13 legacy modernization evidence-map rows.
- Passed: `cd site && npm run build`.
- Passed browser sanity with Playwright against `http://localhost:4187`:
  desktop and mobile checks for `/blog/what-a-proof-path-is/` and `/blog/`
  showed expected H1/card/claim-level text and no horizontal overflow.
- Passed: `dotnet test src/dotnet/TraceMap.sln` with 584 tests. Existing
  `SQLitePCLRaw.lib.e_sqlite3` advisory warnings were emitted during restore.
- Passed: `dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo
  samples/modern-sample --out <temporary-output>`; the smoke produced
  `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and
  `logs/analyzer.log` outside the worktree.

## PR Loop Log

- Initial PR: `https://github.com/joefeser/tracemap/pull/263`.
- Initial PR loop command:
  `agent-control pr-loop --repo joefeser/tracemap --pr 263 --base dev --require-codex-review --quiet --json`.
- Initial PR loop head:
  `d9b661f47896329191d94c48bb2ddbb5c8abb468`.
- Initial PR loop decision: `merge_ready`.
- Initial PR loop stop reason: `NONE`.
- Initial PR loop next action: `merge_ready`.
- Initial PR loop residual risk: `medium`; required Codex review was satisfied
  by configured `trustedCodeReview` quorum after Qodo returned, with missing
  Codex review recorded as residual risk by policy.
- Initial PR loop gates: merge state `CLEAN`, unresolved threads `0`, pending
  checks `0`, failed checks `0`, actionable bot findings `0`.
- Follow-up: this state note was updated to record the PR-loop outcome, so the
  branch needs a normal follow-up push and a fresh PR-loop run on the new head.

## Oddities

- The implementation branch targets `dev`, but the spec source came from
  `origin/main` because `origin/dev` did not yet contain this spec packet.
- Existing blog articles are auto-emitted into `sitemap.xml`; comparable blog
  articles are not represented in `discovery.json`, `llms.txt`, or
  `llms-full.txt`, so no discovery metadata entry was added.

## Follow-Up Items

- A future article can cover the review-before-telemetry angle if it gets a
  separate spec with tight runtime-boundary language.
