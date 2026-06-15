# Site Demo Summary Refresh Implementation State

Status: implemented
Branch: codex/site-demo-summary-refresh
Target PR base: main
Public claim level: hidden

## Summary

Implemented the conservative fixture-and-validation approach. The site now has
a committed public-safe demo fixture at
`site/src/_data/demo-public-summary.json`, an explicit maintainer refresh
command at `site/scripts/refresh-demo-summary.mjs`, and cheap validation wired
into `site/scripts/validate.mjs` through
`site/scripts/validate-demo-summary.mjs`.

The implementation keeps the refresh mechanism hidden: no public page claims
automation as a product feature. The site remains static, with no backend,
runtime service, client-side local artifact fetch, or build-time dependency on a
fresh local demo run.

## Scope Decisions

- Use a generated public-safe fixture rather than validation-only or build-time
  local artifact reads.
- Keep the mechanism hidden because it is maintainer tooling, not a public
  product claim.
- Let affected public pages remain demo-level when they summarize checked-in
  sample evidence.
- Keep the future fixture under an underscore-prefixed `site/src/` directory so
  validation can read committed public-safe data and the current plain static
  copier does not publish the fixture to `site/dist/`.
- Validate current static HTML against the fixture in the first implementation;
  do not add a site templating/data layer in this spec's scope.
- Map from the real `demo-summary.json` section fields: `name`, `status`,
  `classification`, `evidenceTier`, `ruleIds`, `reportCoverage`,
  `artifactPaths`, `counts`, and `reason`.
- Use a known-name mapping table if the fixture adds stable `id` values.
- Do not invent limitations from `demo-summary.json`; only extract them from
  approved public-safe reports with a bounded extractor, or leave them as page
  prose validated against status and coverage.
- Read the source summary `version` field, not a nonexistent
  `summaryVersion` field.
- Preserve the current `ruleIds: ["public.demo.summary.v1"]` array verbatim for
  all sections.
- Validate `portfolio-manifest.json` `indexPath` values as approved relative
  paths before using any manifest-derived field.
- Do not read or publish raw generated internals such as scans, facts, SQLite
  indexes, combined SQLite files, manifests, logs, copied reports, raw source,
  raw SQL, config values, local paths, raw remotes, or private identities.
- The refresh command reads `demo-summary.json`, requires `demo-summary.md`,
  checks approved public files using the existing public-demo sentinel shape,
  validates `portfolio-manifest.json` relative `indexPath` values, maps known
  section names to stable IDs, and writes deterministic JSON with a trailing
  newline.
- The committed fixture preserves status, classification, evidence tier, rule
  IDs, coverage labels, counts, reasons, public-safe artifacts, source summary
  version, and the hashed output-root label.
- `sample-scans` raw `scans/.../report.md` paths are omitted from committed
  `artifacts`; the fixture records only `localOnlyArtifactFamilies:
  ["scan-reports"]`.
- The site validator extracts hard-coded values from the affected HTML sources
  and compares section statuses, coverage labels, evidence tiers, counts, and
  public-safe artifact families to the fixture.
- The fixture intentionally remains under `site/src/_data/`; the build test
  verifies underscore directories are excluded from `site/dist/`.

## Affected Future Pages

- `/demo/result/`
- `/demo/proof-upgrades/`
- `/demo/proof-assets/`
- `/packets/`
- `/manager-packet/`
- `/capabilities/`

## Review Artifacts

- Read `AGENTS.md`.
- Reviewed existing site spec patterns for `/demo/result/`, `/demo/start-here/`,
  `/demo/proof-upgrades/`, `/packets/`, `/manager-packet/`, and
  `/capabilities/`.
- Inspected `scripts/kiro-review.mjs` and `site/package.json` for available
  review and validation commands.
- Ran Kiro Opus spec review with
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-demo-summary-refresh --kind spec --model claude-opus-4.8 --fresh --timeout-ms 900000`.
- Opus returned blocking findings that the original draft used fixture fields
  that did not match the real `demo-summary.json` writer, assumed stable IDs
  that were not present, and treated limitations as if they existed in the
  summary.
- Patched the spec to use real source fields, add explicit mapping rules, choose
  validation of static HTML against the fixture, document the current static
  copier behavior, and add a page fact mapping table.
- Ran Kiro Sonnet spec review with
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-demo-summary-refresh --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 900000`.
- Sonnet returned reduced coverage because shell access was denied inside the
  review, but it still inspected the relevant files and returned actionable
  findings.
- Patched the Sonnet findings by naming the real `version` source field,
  requiring relative `portfolio-manifest.json` `indexPath` validation,
  documenting the constant current rule ID array, clarifying underscore
  directory exclusion, co-locating the known-section mapping table with the
  refresh script, and requiring HTML-source extraction in page validation.

## Validation Completed

- Passed: `./scripts/demo-public.sh .tracemap-demo`; regenerated current public
  demo output for fixture refresh.
- Passed: `node scripts/refresh-demo-summary.mjs ../.tracemap-demo` from
  `site/`; wrote `site/src/_data/demo-public-summary.json`.
- Passed: `npm test` from `site/` with 29 tests passing.
- Passed: `npm run validate` from `site/`; it built `dist/`, validated the demo
  summary fixture and affected pages, then validated 27 HTML files, 605
  internal references, and 26 sitemap URLs.
- Passed: `git diff --check`.
- Passed: `./scripts/check-private-paths.sh`.
- Passed browser sanity checks on `http://localhost:4174/demo/proof-upgrades/`
  and `/demo/proof-assets/` at 1440x1000 and 390x844: updated text present, no
  horizontal overflow, visible hero, and no console errors.

Core scanner/reducer suites are intentionally deferred because this branch only
adds site/tooling/docs changes and runs the public demo as the source fixture.

## Oddities

- The local worktree had an unrelated untracked `c-sharp-sample-repos/`
  directory before this branch's spec work. It is intentionally left untouched.
- The requested repo checkout had `main` checked out in a sibling site
  worktree, so this branch was created directly from `origin/main` in the
  delivery worktree.
- The earlier ignored `.tracemap-demo` output was stale and still showed
  deferred proof-upgrade rows. Reran `./scripts/demo-public.sh .tracemap-demo`
  before refreshing the committed fixture.
- The refreshed demo showed `paths-and-reverse.reversePaths=29` and
  `reverseRoots=7`, while two public pages still showed the previous 25/6
  values. Updated `/demo/proof-upgrades/` and `/demo/proof-assets/` so page copy
  validates against the fixture.
- Current generated public report JSON files still include
  `remoteUrl: git@github.com:joefeser/tracemap.git`. The existing
  `scripts/demo-public-assert.mjs sentinel-scan` accepts those local generated
  reports, and they are not committed or copied into the fixture. The fixture
  validator still rejects raw repository remotes in committed fixture data.
- Added explicit `portfolio-manifest.json` and `reports/portfolio/**`
  references to proof pages so portfolio artifact families are validated
  against the fixture instead of only described in prose.
- The Opus review completed with full coverage, but the Kiro wrapper emitted a
  post-review MCP-settings warning and one failed tool-parameter retry. The
  review text was still returned successfully.
- The Sonnet review completed with reduced coverage because a shell command was
  denied by Kiro's non-interactive tool policy. The review still produced
  concrete findings, and those findings were patched.

## Follow-Ups For Implementation

- Consider teaching generated public report JSON writers to omit or hash
  `remoteUrl` if future specs want the refresh command to read report bodies
  with stricter-than-sentinel raw remote rejection.
- Keep browser sanity checks scoped to future page layout or visible site-copy
  changes.
