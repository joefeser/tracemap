# Site TraceMap Tools Demo Evidence Trail Tasks

Status: implemented
Readiness: implemented
Public claim level: demo

Ordering note: implementation is complete on codex/impl-site-demo-evidence-trail; tasks are checked to reflect the delivered site work and validation.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8`; if unavailable, record the
  exact unavailable-tool/model error in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Treat a `claude-sonnet-4.6` review as sufficient to unblock
  implementation if `claude-opus-4.8` is unavailable at review time and the
  unavailability is recorded in `implementation-state.md`.
- [x] Patch Medium or higher spec findings and rerun re-review where feasible.

## Implementation Tasks

- [x] Confirm or update this spec-local `implementation-state.md` with the
  implementation branch, selected route or section, selected public-safe proof
  source, and scope decisions before changing site code.
- [x] Run the public-safety proof-source checklist, including
  `./scripts/check-private-paths.sh`, against the candidate proof source and
  record the result in `implementation-state.md` before any site code is
  written.
- [x] Run the evidence-sufficiency checklist against the candidate proof
  source and record whether it can supply the changed surface, endpoint or
  route, static path or explicit gap, package surface or gap, config surface or
  gap, SQL-facing surface or gap, and per-item rule IDs/evidence tiers.
- [x] Choose an existing demo route/section or add a new demo route for the
  evidence trail.
- [x] Add visible `Public claim level: demo` text and the shared site
  principle.
- [x] Frame exactly one public-safe demo question.
- [x] Present the trail in this order: changed surface, endpoint or route,
  static path or surface or a coverage gap when no static path evidence exists,
  a downstream dependency-surface step enumerating package evidence, config
  evidence, and SQL-facing evidence, then coverage and limitations.
- [x] In the downstream dependency-surface step, show evidence for package,
  config, and SQL-facing surface types when present and explicit coverage gaps
  for any of those three surface types that the selected sample lacks.
- [x] Represent package, config, and SQL-facing surfaces only with public-safe
  names, rule IDs, evidence tiers, coverage labels, counts, hashes, and
  limitations, never raw config keys or values, raw SQL, connection strings, or
  raw package manifests.
- [x] Keep the key message visible: same evidence packet made easier to
  follow, not a stronger claim.
- [x] Use only checked-in samples, checked-in public demo summaries, or
  public-safe generated summaries.
- [x] Include public-safe proof-path links for each major trail segment.
- [x] Include rule IDs, evidence tiers, coverage labels, and limitations where
  available.
- [x] Label missing, partial, reduced, or unknown evidence as a coverage gap
  instead of implying complete proof.
- [x] Avoid saying `impacted` in rendered evidence-trail copy; use bounded
  alternatives such as `referenced`, `connected`, or `shown in the packet`.
- [x] Add or update route metadata, sitemap metadata, and discovery metadata
  with `publicClaimLevel: demo`.
- [x] Link to `/proof-paths/`, `/evidence/`, `/validation/`, and
  `/limitations/` or current equivalent public routes.
- [x] Confirm and record required target route availability in
  `implementation-state.md` before changing site code.
- [x] Add a dedicated dist validator for the evidence-trail page following the
  existing pattern, or define the pattern first per Requirement 7 if none
  exists.
- [x] Export a single validator function covering the `impacted` ban and the
  AI/LLM check list, wire it into `site/scripts/validate.mjs`, and add a
  companion test file.
- [x] Ensure `npm run validate` enforces required labels, trail steps,
  proof-path links, metadata, forbidden private/raw text, forbidden AI/LLM
  positioning, and internal-link resolution for the rendered evidence-trail
  output.
- [x] Define and record stable machine-detectable markers for each in-scope
  downstream surface type and each coverage gap so the dist validator can
  assert presence or gap deterministically.
- [x] Keep visible copy, metadata, alt text, tests, and generated output free
  of raw source snippets, raw SQL, config values, secrets, local absolute
  paths, raw remotes, generated scan directories, private sample names, and
  connection-string tokens.
- [x] Ban `impacted` in rendered evidence-trail output unless a future spec
  amendment defines a reducer-backed citation rule.

## Validation Tasks

- [x] Run `git diff --check`.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`.
- [x] Run `npm run build` from `site/`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run desktop and mobile browser sanity checks for layout or interaction
  changes, or record why they were deferred.
- [x] Update `implementation-state.md` with final validation, review findings,
  oddities, and follow-up notes.
