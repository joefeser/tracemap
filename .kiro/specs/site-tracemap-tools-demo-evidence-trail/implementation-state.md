# Site TraceMap Tools Demo Evidence Trail Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: demo

## Branch

Spec branch: `codex/spec-site-demo-evidence-trail`
Base: `origin/dev`

## Scope

This branch adds a spec-only public site phase for a future demo evidence trail.
It does not implement site code, routes, styles, validation scripts, or
generated assets.

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

- Spec files added:
  `.kiro/specs/site-tracemap-tools-demo-evidence-trail/requirements.md`,
  `.kiro/specs/site-tracemap-tools-demo-evidence-trail/tasks.md`,
  `.kiro/specs/site-tracemap-tools-demo-evidence-trail/implementation-state.md`,
  and `.kiro/specs/site-tracemap-tools-demo-evidence-trail/review-packet.md`.
- Tasks remain unchecked because implementation is future work.
- No site source, generated output, scanner code, or reducer code has been
  changed.

## Scope Decisions

- Claim level is `demo`, not concept and not production proof.
- The route or section placement is intentionally left to the future
  implementation, but it must be recorded here before site code changes.
- Proof sources must be checked-in samples, checked-in public demo summaries,
  or public-safe generated summaries. If none exists, implementation should
  stop and record the gap.
- No proof source has been selected or verified yet. The future implementation
  may consider `site/src/_data/demo-public-summary.json` as a candidate, but
  must run the full public-safety checklist before selecting any candidate and
  must record the result of each checklist item in this file before changing
  site code.
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
- Before site implementation starts, record the resolved required target routes
  for `/proof-paths/`, `/evidence/`, `/validation/`, and `/limitations/`, or
  record renamed equivalents or coverage gaps.

## Public-Safety Checklist for Candidate Proof Source

Candidate: `site/src/_data/demo-public-summary.json` may be evaluated at
implementation time, but no candidate is selected until every applicable check
below is completed.

- [ ] File is checked into the repository.
- [ ] Contains no local absolute paths.
- [ ] Contains no raw repository remotes.
- [ ] Contains no connection-string tokens.
- [ ] Contains no private sample names.
- [ ] Contains no secrets.
- [ ] Contains no raw SQL.
- [ ] Contains no raw source snippets.
- [ ] Contains no config values.
- [ ] `./scripts/check-private-paths.sh` passes for the candidate.
- [ ] Candidate passes forbidden-copy patterns in the dedicated dist validator.

Result: _pending; complete before any site code is written._

## Evidence-Sufficiency Check

Complete before selecting a proof source or changing site code.

- [ ] Changed surface present.
- [ ] Endpoint or route present.
- [ ] Static path present, or explicit static-path coverage gap available.
- [ ] Package surface item present with rule ID and evidence tier, or explicit
  package coverage gap available.
- [ ] Config surface item present with rule ID and evidence tier, or explicit
  config coverage gap available.
- [ ] SQL-facing surface item present with rule ID and evidence tier, or
  explicit SQL-facing coverage gap available.
- [ ] Per-surface coverage labels and limitation notes available.
- [ ] Marker scheme chosen for downstream surface types and coverage gaps.

Result: _pending; complete before any site code is written._

## Required Target Route Resolution

Confirm against built `site/dist/` output before site code is written.

- [ ] `/proof-paths/` - source: `site/src/proof-paths/index.html` - resolved
  dist path: _pending_.
- [ ] `/evidence/` - source: `site/src/evidence/index.html` - resolved dist
  path: _pending_.
- [ ] `/validation/` - source: `site/src/validation/index.html` - resolved
  dist path: _pending_.
- [ ] `/limitations/` - source: `site/src/limitations/index.html` - resolved
  dist path: _pending_.

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

- `git diff --check` passed for this spec branch.
- `./scripts/check-private-paths.sh` passed.
- Site implementation validation is future work and is listed in
  `tasks.md`.

## Follow-Ups

- Future implementation must update this file with the selected route or
  section, selected public-safe proof source, validation commands and results,
  review findings, oddities, and follow-up items.
- Future implementation should keep the demo focused on one question so the
  evidence trail remains readable and bounded.
- A partial or interrupted implementation must record, at minimum: the branch
  name, chosen route or section anchor, selected proof source with checklist
  result, completed implementation tasks, validation commands run and their
  outcomes, and any blocking gaps or oddities before stopping.
