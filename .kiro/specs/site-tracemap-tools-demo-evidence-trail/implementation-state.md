# Site TraceMap Tools Demo Evidence Trail Implementation State

Status: implemented
Readiness: ready-for-review
Public claim level: demo

## Branch

Spec branch: `codex/spec-site-demo-evidence-trail`
Implementation branch: `codex/impl-site-demo-evidence-trail`
Base: `origin/dev`

## Scope

This implementation branch adds a public site page for the demo evidence trail.
It implements a bounded route, route metadata, discovery metadata, cross-links,
focused dist validation, tests, and spec bookkeeping. It does not change
scanner, reducer, generated demo artifacts, or public demo summary generation.

The future implementation should define a demo page or section that walks one
public-safe question through a static evidence trail:
changed surface, endpoint or route, static path or surface, package, config, or
SQL-facing evidence, then coverage and limitations.

The key public message is that the same deterministic evidence packet is made
easier to follow, not stronger. The demo must not claim runtime behavior,
production traffic, endpoint performance, outage cause, release safety,
operational safety, AI impact analysis, LLM analysis, or complete product
coverage.

## Current State

- Selected route: `/demo/evidence-trail/`.
- Selected proof source: `site/src/_data/demo-public-summary.json`.
- Selected question: `What static evidence connects a changed demo surface to a
  route and downstream surfaces?`
- Site code is implemented on `codex/impl-site-demo-evidence-trail`.

## Scope Decisions

- Claim level is `demo`, not concept and not production proof.
- The route is `/demo/evidence-trail/` because it is demo-level, adjacent to
  `/demo/result/`, `/demo/start-here/`, `/demo/proof-upgrades/`, and
  `/demo/proof-assets/`, and does not require changing the canonical top
  navigation.
- Proof sources must be checked-in samples, checked-in public demo summaries,
  or public-safe generated summaries. If none exists, implementation should
  stop and record the gap.
- `site/src/_data/demo-public-summary.json` is selected as the only proof
  source. It is a checked-in public demo summary with demo-level rows, rule IDs,
  evidence tiers, coverage labels, counts, generated report-family paths, and a
  public output-root hash.
- The checklist requires confirming the resolved proof source is checked in,
  contains no local absolute paths, raw remotes, connection-string tokens,
  private sample names, secrets, raw SQL, raw source snippets, or config
  values, does not cause `./scripts/check-private-paths.sh` to fail, and passes
  the forbidden-copy patterns used by the dedicated dist validator. If no
  candidate passes, the implementation stops and records the gap here.
- Public copy must avoid raw facts, raw source snippets, raw SQL, config
  values, secrets, local absolute paths, raw remotes, generated scan
  directories, and private sample names.
- If a new route is added, the page route should follow existing sitemap
  metadata conventions while discovery hint entries continue to follow the
  discovery validator's separate sitemap-exclusion rules.
- If top navigation changes are needed, update the shared navigation source
  consistently so generated pages keep the canonical navigation pattern.
- Downstream package, config, and SQL-facing surfaces are rendered as explicit
  coverage gaps in this slice. The selected summary contains dependency surface
  counts and endpoint/path report families, but it does not expose public-safe
  per-package, per-config, or per-SQL surface items with separate rule IDs and
  evidence tiers. The validator asserts each in-scope downstream surface type
  is present as either an evidence item or a gap.
- Stable marker scheme: each downstream surface uses
  `data-trail-surface-type="package"`, `data-trail-surface-type="config"`, or
  `data-trail-surface-type="sql-facing"` and each explicit gap uses
  `data-trail-gap="package"`, `data-trail-gap="config"`, or
  `data-trail-gap="sql-facing"`.

## Public-Safety Checklist for Candidate Proof Source

Candidate: `site/src/_data/demo-public-summary.json`.

- [x] File is checked into the repository.
- [x] Contains no local absolute paths.
- [x] Contains no raw repository remotes.
- [x] Contains no connection-string tokens.
- [x] Contains no private sample names.
- [x] Contains no secrets.
- [x] Contains no raw SQL.
- [x] Contains no raw source snippets.
- [x] Contains no config values.
- [x] `./scripts/check-private-paths.sh` passes for the candidate.
- [x] Candidate passes forbidden-copy patterns in the dedicated dist validator.

Result: passed before site code was written. Commands:
`git ls-files --error-unmatch site/src/_data/demo-public-summary.json`,
targeted `rg` forbidden-pattern scan of the candidate, and
`./scripts/check-private-paths.sh`.

## Evidence-Sufficiency Check

Complete before selecting a proof source or changing site code.

- [x] Changed surface present.
- [x] Endpoint or route present.
- [x] Static path present, or explicit static-path coverage gap available.
- [x] Package surface item present with rule ID and evidence tier, or explicit
  package coverage gap available.
- [x] Config surface item present with rule ID and evidence tier, or explicit
  config coverage gap available.
- [x] SQL-facing surface item present with rule ID and evidence tier, or
  explicit SQL-facing coverage gap available.
- [x] Per-surface coverage labels and limitation notes available.
- [x] Marker scheme chosen for downstream surface types and coverage gaps.

Result: sufficient for a bounded demo trail. The selected source supplies
`diff.surfaceDiffs = 12`, `combine-and-dependency-report.endpointFindings = 14`,
`paths-and-reverse.paths = 12`, `paths-and-reverse.pathGaps = 37`,
`public.demo.summary.v1`, `Tier2Structural`, and `PartialAnalysis`. Package,
config, and SQL-facing surfaces are explicit gaps because the public summary
does not expose per-type item rows.

## Required Target Route Resolution

Confirm against built `site/dist/` output before site code is written.

- [x] `/proof-paths/` - source: `site/src/proof-paths/index.html` - resolved
  dist path: `site/dist/proof-paths/index.html`.
- [x] `/evidence/` - source: `site/src/evidence/index.html` - resolved dist
  path: `site/dist/evidence/index.html`.
- [x] `/validation/` - source: `site/src/validation/index.html` - resolved
  dist path: `site/dist/validation/index.html`.
- [x] `/limitations/` - source: `site/src/limitations/index.html` - resolved
  dist path: `site/dist/limitations/index.html`.

If any route is renamed before implementation, record the renamed path here and
update proof-path links accordingly.

## Spec Review Status

- `claude-opus-4.8` initial spec review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-demo-evidence-trail --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with full coverage and saved review artifacts. Medium findings
  patched: discovery metadata fields, dedicated validator wiring, and
  downstream trail-step ambiguity.
- `claude-sonnet-4.6` initial spec review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-demo-evidence-trail --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with full coverage and saved review artifacts. Critical, High, and
  Medium findings patched: required route resolution, proof-source checklist,
  validator fallback, static-path gap handling, discovery validator run,
  review-unavailability rule, and candidate proof-source specificity.
- `claude-sonnet-4.6` re-review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-demo-evidence-trail --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with full coverage and saved review artifacts. High and Medium
  findings patched: no preselected proof source, enumerated downstream surface
  types, explicit `impacted` validator pattern, and review-status recording
  slots.
- `claude-sonnet-4.6` second re-review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-demo-evidence-trail --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with full coverage and saved review artifacts. High and Medium
  findings patched: downstream surface validator contract, AI/LLM full-output
  enforcement, checklist slots, route-resolution slots, single exported
  validator-function wording, and task fallback alignment.
- `claude-opus-4.8` re-review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-demo-evidence-trail --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with full coverage and saved review artifacts. High and Medium
  findings patched: evidence-sufficiency gate, machine-detectable marker
  scheme, public-safe representation for package/config/SQL surfaces, and
  static path versus downstream surface wording.
- A `claude-sonnet-4.6` review is sufficient to unblock implementation if
  `claude-opus-4.8` is unavailable and the unavailability is recorded here.
- Medium or higher findings must be patched and re-reviewed before
  implementation starts.

## Validation

- Pre-code proof-source checks passed:
  `git ls-files --error-unmatch site/src/_data/demo-public-summary.json`,
  targeted `rg` forbidden-pattern scan of the candidate source, and
  `./scripts/check-private-paths.sh`.
- Required target-route resolution passed after `npm run build`:
  `/proof-paths/`, `/evidence/`, `/validation/`, and `/limitations/`.
- `git diff --check` passed.
- `npm test` from `site/` passed: 127 tests.
- `npm run validate` from `site/` passed:
  38 HTML files, 1048 internal references, 37 sitemap URLs, and 1 legacy story
  safety target.
- `npm run build` from `site/` passed.
- `./scripts/check-private-paths.sh` passed.
- Desktop browser sanity passed at 1440x1100:
  title `Demo Evidence Trail | TraceMap`, expected H1, no horizontal overflow
  (`scrollWidth=1440`, `clientWidth=1440`).
- Mobile browser sanity passed at 390x900 with no horizontal overflow
  (`scrollWidth=390`, `clientWidth=390`).

## Oddities

- The selected public summary is intentionally aggregate-level. It can support
  the changed-surface, endpoint, and static-path trail steps with counts and
  report-family proof paths, but it does not expose public-safe per-package,
  per-configuration, or per-SQL-facing item rows. The page renders those three
  downstream surface types as explicit gaps with stable markers instead of
  inventing stronger evidence.
- The dedicated validator bans the exact rendered word `impacted` for this
  route, per the spec. Existing neighboring pages may still use other
  reducer/report language outside this route's validation scope.

## Follow-Ups

- If future demo summaries add sanitized per-package, per-configuration, or
  per-SQL-facing items with their own rule IDs and evidence tiers, promote the
  corresponding downstream gap card into a bounded evidence item and extend the
  validator fixture.
- Keep `/demo/evidence-trail/` focused on one question; add additional trails
  as future routes or sections only when each has its own public-safe proof
  source and validation contract.
