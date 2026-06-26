# Site TraceMap Tools Proof Path Story Gallery Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

Last verified: 2026-06-25
Branch: codex/spec-site-proof-path-story-gallery-20260625190306
Source of truth: origin/dev

## Summary

This packet specifies a future `tracemap.tools` proof-path story gallery. The
gallery would use short public-safe cards and walkthroughs to show how a static
question follows deterministic evidence from a source/root surface to endpoint,
service, data, package, config, generated artifact, or stop-condition surfaces.

This is spec-only. No site source, scanner source, reducer source, generated
artifact, demo artifact, validation script, runtime workflow, or existing spec
has been changed.

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
- Deferred: site `npm test`, `npm run validate`, `npm run build`, and browser
  sanity checks because this phase is spec-only and does not change `site/`.

## PR Review Feedback

- ACK/Qodo P3 review thread: tracked `implementation-state.md` recorded a
  machine-local worktree path despite the packet's local-path boundary.
  Disposition: patched to describe the dedicated temporary worktree without
  tracking the absolute path.

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

## Follow-Up Items

- Future implementation must record placement choice, card claim-level
  decisions, category omissions, metadata decisions, and route/link
  substitutions.
