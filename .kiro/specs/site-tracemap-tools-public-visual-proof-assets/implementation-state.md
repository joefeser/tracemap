# Implementation State

Status: implemented
Readiness: implemented
Branch: codex/site-public-visual-proof-assets
Public claim level: demo

## Summary

This phase adds a public-safe visual proof-assets surface for the existing
public demo proof-upgrades evidence. The implemented route is
`/demo/proof-assets/`.

The page should make generated report shapes easier to scan without publishing
raw local artifacts. Visuals are orientation only; generated reports,
`demo-summary.*`, rule IDs, evidence tiers, coverage labels, counts,
limitations, `facts.ndjson`, and `index.sqlite` remain the source of truth.

## Scope Decisions

- Keep the claim level at `demo` because the visuals are based on checked-in
  public samples and the already published `/demo/proof-upgrades/` ledger.
- Use HTML/CSS visual representations instead of committing raw screenshots or
  generated scan artifacts.
- Keep the route under `/demo/` as `/demo/proof-assets/` because it extends the
  public demo proof-upgrades surface rather than broadening product claims.
- Keep this branch site-only under `site/` and this spec directory.
- Do not introduce a runtime service or new public artifact format.
- Link the page from `/demo/`, `/demo/proof-upgrades/`, `/demo/result/`,
  `/packets/`, and `/roadmap/` with demo-level wording.

## Claim Boundaries

Safe to say:

- The page shows public-safe visual examples of generated public demo report
  shapes.
- The visual examples are static orientation aids over deterministic,
  rule-backed demo evidence.
- The public demo evidence remains bounded by rule IDs, evidence tiers, counts,
  coverage labels, and limitations.

Not safe to say:

- TraceMap proves runtime behavior, production traffic, deployment state,
  endpoint performance, production dependency understanding, release safety, or
  AI impact analysis.
- Visuals are evidence source of truth.
- The demo proves full impact, release approval, runtime reachability, package
  compatibility, vulnerability status, or CI policy enforcement.

## Artifact Safety

Safe to show:

- Status labels, rule IDs, evidence tiers, coverage labels, demo counts,
  limitation labels, generated report family names, and relative public-demo
  surface names.

Do not show:

- Raw source snippets, raw SQL, config values, secrets, local absolute paths,
  raw repository remotes, private sample identities, raw `facts.ndjson`, raw
  SQLite rows, combined SQLite contents, generated scan directories, analyzer
  logs, or copied local report archives.

## Validation Plan

- Run the repo-supported Kiro spec review if available.
- Commit spec changes separately.
- Implement the static route and cross-links.
- Run `git diff --check`.
- Run `npm test` from `site/`.
- Run `npm run validate` from `site/`.
- Run desktop and mobile browser sanity checks for `/demo/proof-assets/`.

## Validation Results

- Passed: Kiro/Sonnet spec review with full coverage using
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-public-visual-proof-assets --kind spec --model claude-sonnet-4.6 --fresh --save-review-text`.
  The first wrapper attempt found the wrapper requires `design.md`, so a short
  design note was added before re-running. Medium+ spec clarity findings were
  patched by splitting backlink tasks, naming `/roadmap/` as the bounded
  roadmap/capability backlink surface, and documenting the intended
  `workflow-grid` plus scoped `proof-visual` styling approach.
- Passed: `git diff --check`.
- Passed: `npm test` from `site/` (19 tests).
- Passed: `npm run validate` from `site/` (26 HTML files, 552 internal
  references, 25 sitemap URLs).
- Passed: desktop browser sanity check for `/demo/proof-assets/` at the default
  wide viewport: four proof cards, claim-level text, required links, no
  horizontal overflow, and no console errors.
- Passed: mobile browser sanity check for `/demo/proof-assets/` at 390x844:
  stacked proof cards, claim-level text, roadmap link, no horizontal overflow,
  and no console errors.

## Oddities

- Branch creation used `origin/main` directly because `main` was already checked
  out in another local worktree.
- An unrelated untracked `c-sharp-sample-repos/` directory was present before
  this work began and is intentionally ignored.
- The Kiro review text reported a stale branch name (`codex/site-manager-packet`)
  from its session context. Local `git status` shows this work is on
  `codex/site-public-visual-proof-assets`.
- The default site dev port 4173 was already in use during browser validation,
  so the local server was started with `PORT=4174`.

## Follow-Up Items

- Update counts or labels if `/demo/proof-upgrades/` changes before this branch
  merges.
- Keep cross-links bounded so the page is discoverable without implying
  stronger public claims.
