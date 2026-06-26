# Site TraceMap Tools Proof Path Story Gallery Implementation State

Status: implemented
Readiness: ready-for-implementation
Public claim level: concept

Last verified: 2026-06-25
Branch: codex/impl-site-proof-path-story-gallery-20260625220151
Source of truth: origin/dev

## Summary

This implementation adds a concept-level `tracemap.tools` proof-path story
gallery at `/proof-path-stories/`. The page uses synthetic public-safe cards
and walkthroughs to show how a static question follows deterministic evidence
from a source/root surface to endpoint, service, data, package, config,
generated artifact, or stop-condition surfaces.

Site source, route metadata, focused site validation, aggregate site validation
fixtures, and this spec packet were updated. Scanner source, reducer source,
generated scan artifacts, runtime workflows, and core analyzer behavior remain
out of scope.

## Implementation Update - 2026-06-25

- Current branch: `codex/impl-site-proof-path-story-gallery-20260625220151`.
- Target branch: `dev`.
- Work isolation: dedicated temporary worktree outside the primary checkout;
  machine-local absolute worktree path is intentionally not tracked in this
  public-safe spec packet.
- Selected placement: standalone public route `/proof-path-stories/`.
- Rejected placement: `/demo/proof-path-stories/` because the initial stories
  are synthetic concept cards and are not backed by checked-in public-safe demo
  evidence.
- Rejected placement: section on `/demo/proof-upgrades/` because the gallery is
  a story-oriented reading aid, not a compact proof-ledger companion.
- Rejected placement: section on a future proof-source/catalog route because
  the gallery is not the catalog source of truth and the standalone route lets
  the source catalog keep its route-to-source role.
- Rejected placement: folded section on an existing route because this spec
  requires several stable anchors, story cards, walkthroughs, stop conditions,
  and validation markers that are clearer as a standalone concept page.
- Primary navigation remains unchanged. Discovery is via sitemap metadata,
  route-index metadata, and an adjacent link from `/proof-paths/`.
- The page states `Public claim level: concept` and `No public conclusion
  without evidence`.
- All story cards remain `concept`; no card is labeled `demo`.
- All stories are synthetic, public-safe, and concept-level. No checked-in
  public-safe demo evidence was used to upgrade a card.
- The gallery is a story-oriented reading aid. The canonical proof-path overview
  remains `/proof-paths/`; source-family cataloging remains
  `/proof-source-catalog/`; claim gating remains `/review-claim-checklist/`;
  roadmap status remains `/roadmap/`; broad boundaries remain `/limitations/`.
- Required anchors implemented:
  `#story-contract`, `#proof-path-anatomy`,
  `#evidence-packet-references`, `#coverage-and-limitations`,
  `#stop-conditions-and-routing`, `#non-claims-and-forbidden-wording`, and
  `#gallery-validation`.
- Story categories implemented: endpoint/service, data/config,
  package/dependency, generated artifact, and reduced-coverage orientation.
- Walkthrough endings implemented: `evidence-backed static path`,
  `reduced coverage`, `needs owner follow-up`, `internal only`, `hidden`, and
  `stop: no public-safe evidence`.
- Stop conditions implemented: `no public-safe evidence`, `reduced coverage`,
  `semantic gap`, `syntax-only fallback`, `private-only evidence`,
  `hidden detail`, `missing rule ID`, and `requires reducer evidence`.
- Boundary wording is wrapped in a stable public-safety section using
  `data-boundary="rejected-example"` on
  `#non-claims-and-forbidden-wording`.
- Focused validation was added in `site/scripts/proof-path-stories.mjs` with
  regression tests in `site/scripts/proof-path-stories.test.mjs`.
- Aggregate validation now calls the focused validator from
  `site/scripts/validate.mjs`.
- Route metadata was added to `site/src/_site/pages.json` and
  `site/src/_site/discovery.json`.
- An adjacent link was added from `/proof-paths/` without adding the gallery to
  primary navigation.

## Scope Decisions

- Target branch is `dev`.
- Work is isolated in a dedicated temporary worktree outside the primary
  checkout; the machine-local absolute path is intentionally not tracked in
  this public-safe spec packet.
- The future public surface defaults to `Public claim level: concept` because
  this packet does not prove checked-in public-safe generated summaries for
  concrete story cards.
- The preferred future placement is `/proof-path-stories/` unless a future
  implementer proves that `/demo/proof-path-stories/` is backed entirely by
  checked-in public demo evidence.
- The gallery is an explanation layer over evidence packet references, not a
  new evidence source.
- Future implementation must keep each card tied to rule IDs or rule families,
  evidence tiers, coverage labels, supporting IDs when public-safe,
  limitations, stop conditions, and next-owner/next-question routing.

## Boundaries

- No AI/LLM impact-analysis claims.
- No runtime proof, production traffic, endpoint performance, release
  approval, release safety, operational safety, complete coverage, or
  automated approval claims.
- No raw source snippets, raw SQL, config values, secrets, local absolute
  paths, raw repository remotes, private sample names, private labels,
  generated local artifacts, raw `facts.ndjson`, raw SQLite contents, analyzer
  logs, hidden validation details, command output, or credential-like values.
- Do not expose generated local artifact paths or private artifact labels.
- Do not use "impacted" unless future copy is explicitly backed by
  public-safe reducer evidence, status, rule IDs, and limitations.

## Review Results

- Review: `claude-opus-4.8`, 2026-06-25 local / 2026-06-26 UTC, reduced
  coverage, 5 findings: 2 Medium, 3 Low. Artifacts:
  `.tmp/kiro-reviews/site-tracemap-tools-proof-path-story-gallery/2026-06-26T000732-732Z-spec-claude-opus-4.8.clean.md`
  and
  `.tmp/kiro-reviews/site-tracemap-tools-proof-path-story-gallery/2026-06-26T000732-732Z-spec-claude-opus-4.8.meta.json`.
- Review: `claude-sonnet-4.6`, 2026-06-25 local / 2026-06-26 UTC, reduced
  coverage, 4 findings: 1 Medium, 3 Low. Artifacts:
  `.tmp/kiro-reviews/site-tracemap-tools-proof-path-story-gallery/2026-06-26T001033-253Z-spec-claude-sonnet-4.6.clean.md`
  and
  `.tmp/kiro-reviews/site-tracemap-tools-proof-path-story-gallery/2026-06-26T001033-253Z-spec-claude-sonnet-4.6.meta.json`.
- Review coverage note: both model runs completed but reported denied tool
  access through `kiro.review.wrapper.v1`, so coverage is labeled reduced.

### Findings And Dispositions

- Opus M1 (Medium): Requirement 2 said `supporting IDs when available` while
  design/tasks/Requirement 6 require public-safe IDs. Disposition: patched
  Requirement 2 to say `supporting IDs when public-safe`.
- Opus M2 (Medium): Requirement 2 omitted per-card `story category` and
  `claim level`, while design/tasks require them and page/card concept-demo
  control depends on them. Disposition: patched Requirement 2 with explicit
  story category and per-card claim-level criteria.
- Sonnet Finding 1 (Medium): Forbidden-term boundary contexts lacked a concrete
  machine-distinguishable wrapper contract. Disposition: patched Requirement 4,
  Requirement 8, and design Public Safety with an explicit wrapper contract.
- Opus L1 (Low): Hard Boundaries omitted `command output`. Disposition:
  patched Hard Boundaries.
- Opus L2 (Low): Requirements did not anchor the machine-distinguishable
  wrapper mechanism. Disposition: patched with the Sonnet Medium wrapper fix.
- Opus L3 (Low): Cross-linking and role disambiguation appeared only as a task.
  Disposition: patched Requirement 1 with a public-safe related-surface
  cross-link and role-disambiguation criterion.
- Sonnet Finding 2 (Low): `supporting IDs when public-safe` lacked a validation
  definition. Disposition: patched design Evidence Reference Design with an
  opaque supporting-ID definition and public-safety check.
- Sonnet Finding 3 (Low): Version-specific review model names may become
  brittle. Disposition: accepted as-is because the user explicitly requested
  those model commands and the tasks require exact blockers/substitutions to be
  recorded.
- Sonnet Finding 4 (Low): Review Results lacked a completed-entry template.
  Disposition: superseded by these concrete completed review entries and
  artifact references.
- Re-review: `claude-sonnet-4.6`, 2026-06-25 local / 2026-06-26 UTC, reduced
  coverage, 2 Low findings and no Medium+ findings. Artifacts:
  `.tmp/kiro-reviews/site-tracemap-tools-proof-path-story-gallery/2026-06-26T001245-286Z-re-review-claude-sonnet-4.6.clean.md`
  and
  `.tmp/kiro-reviews/site-tracemap-tools-proof-path-story-gallery/2026-06-26T001245-286Z-re-review-claude-sonnet-4.6.meta.json`.
- Re-review L1 (Low): Requirement 6 evidence-reference prohibition list
  omitted `command output`. Disposition: patched Requirement 6.
- Re-review L2 (Low/informational): Requirement 3 conclusion-avoidance list
  used root words but did not name phrase-level categories. Disposition:
  patched Requirement 3 with phrase-level forbidden examples.
- Readiness after review: `ready-for-implementation` because all Medium+
  findings were patched, narrow Low findings were patched, and the bounded
  re-review found no remaining Medium+ findings. Kiro coverage remains reduced
  due to denied tool access.

## Validation Plan

Spec packet validation:

- Run Kiro Opus spec review where available.
- Run Kiro Sonnet spec review where available.
- Patch Medium+ actionable findings and one bounded re-review if patched.
- Run `git diff --check`.
- Run `./scripts/check-private-paths.sh`.
- Confirm diff is limited to this spec folder.

## Validation Results

- Passed: `git diff --check`.
- Passed: `./scripts/check-private-paths.sh`.
- Passed: diff scope check. Changed files are limited to
  `.kiro/specs/site-tracemap-tools-proof-path-story-gallery/`.
- Spec-review phase deferred site `npm test`, `npm run validate`,
  `npm run build`, and browser sanity checks because that phase was spec-only.

Implementation validation:

- Passed: `node --test scripts/proof-path-stories.test.mjs
  scripts/validate.test.mjs`.
- Passed: `npm test` from `site/` (532 tests).
- Passed: `npm run validate` from `site/` (built static site and validated 71
  HTML files, 2444 internal references, and 70 sitemap URLs).
- Passed: `npm run build` from `site/`.
- Passed: desktop browser sanity check for `/proof-path-stories/` at
  1440x1100. DOM check showed no horizontal overflow, 5 story cards, 6
  walkthroughs, and required public markers present.
- Passed: mobile browser sanity check for `/proof-path-stories/` at 390x844.
  DOM check showed no horizontal overflow, 5 story cards, 6 walkthroughs, and
  required public markers present.
- Passed: `git diff --check`.
- Passed: `./scripts/check-private-paths.sh`.
- Pending before commit: `git diff --name-only origin/dev...HEAD` is expected
  to be meaningful after the implementation commit exists; pre-commit uncommitted
  diff was inspected for site/spec-only scope.

## PR Review Feedback

- ACK/Qodo P3 review thread: tracked `implementation-state.md` recorded a
  machine-local worktree path despite the packet's local-path boundary.
  Disposition: patched to describe the dedicated temporary worktree without
  tracking the absolute path.

Implementation PR review loop: pending until the implementation PR is created
and ACK is run.

Future site implementation validation:

- Run `npm test` from `site/`.
- Run `npm run validate` from `site/`.
- Run `npm run build` from `site/`.
- Run desktop and mobile browser sanity checks for layout or interaction
  changes.
- Run root-level `git diff --check` and `./scripts/check-private-paths.sh`.

## Oddities

- The initial file creation patch landed in the original checkout because the
  patch tool does not inherit shell worktree state. Those files were moved into
  the dedicated worktree immediately, and the original checkout was verified
  clean afterward.
- Kiro reviews completed with reduced coverage because the review wrapper
  reported denied tool access (`kiro.review.wrapper.v1`), even though it read
  the packet and produced findings.
- During implementation validation, running `npm test -- proof-path-stories.test.mjs`
  invoked the package glob plus the extra argument, so it behaved as a full
  suite attempt and exposed fixture drift. The focused rerun used
  `node --test scripts/proof-path-stories.test.mjs scripts/validate.test.mjs`
  and passed after fixtures were updated.
- The implementation-state update for this branch was completed in the same
  implementation turn after the first site edit rather than before the first
  site edit. The route decision, placement rationale, scope, validation, and
  follow-ups are now recorded here.

## Follow-Up Items

- After PR creation, record the PR URL, final ACK decision, and any authorized
  review-loop patches or dispositions.
