# Site TraceMap Tools Demo Evidence Trail Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: demo

Ordering note: this phase is spec-only. Keep every task unchecked until a
future implementation branch performs the site work and validation.

## Spec Review Tasks

- [ ] Run Kiro spec review with `claude-opus-4.8`; if unavailable, record the
  exact unavailable-tool/model error in `implementation-state.md`.
- [ ] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [ ] Treat a `claude-sonnet-4.6` review as sufficient to unblock
  implementation if `claude-opus-4.8` is unavailable at review time and the
  unavailability is recorded in `implementation-state.md`.
- [ ] Patch Medium or higher spec findings and rerun re-review where feasible.

## Implementation Tasks

- [ ] Confirm or update this spec-local `implementation-state.md` with the
  implementation branch, selected route or section, selected public-safe proof
  source, and scope decisions before changing site code.
- [ ] Run the public-safety proof-source checklist, including
  `./scripts/check-private-paths.sh`, against the candidate proof source and
  record the result in `implementation-state.md` before any site code is
  written.
- [ ] Run the evidence-sufficiency checklist against the candidate proof
  source and record whether it can supply the changed surface, endpoint or
  route, static path or explicit gap, package surface or gap, config surface or
  gap, SQL-facing surface or gap, and per-item rule IDs/evidence tiers.
- [ ] Choose an existing demo route/section or add a new demo route for the
  evidence trail.
- [ ] Add visible `Public claim level: demo` text and the shared site
  principle.
- [ ] Frame exactly one public-safe demo question.
- [ ] Present the trail in this order: changed surface, endpoint or route,
  static path or surface or a coverage gap when no static path evidence exists,
  a downstream dependency-surface step enumerating package evidence, config
  evidence, and SQL-facing evidence, then coverage and limitations.
- [ ] In the downstream dependency-surface step, show evidence for package,
  config, and SQL-facing surface types when present and explicit coverage gaps
  for any of those three surface types that the selected sample lacks.
- [ ] Represent package, config, and SQL-facing surfaces only with public-safe
  names, rule IDs, evidence tiers, coverage labels, counts, hashes, and
  limitations, never raw config keys or values, raw SQL, connection strings, or
  raw package manifests.
- [ ] Keep the key message visible: same evidence packet made easier to
  follow, not a stronger claim.
- [ ] Use only checked-in samples, checked-in public demo summaries, or
  public-safe generated summaries.
- [ ] Include public-safe proof-path links for each major trail segment.
- [ ] Include rule IDs, evidence tiers, coverage labels, and limitations where
  available.
- [ ] Label missing, partial, reduced, or unknown evidence as a coverage gap
  instead of implying complete proof.
- [ ] Avoid saying `impacted` in rendered evidence-trail copy; use bounded
  alternatives such as `referenced`, `connected`, or `shown in the packet`.
- [ ] Add or update route metadata, sitemap metadata, and discovery metadata
  with `publicClaimLevel: demo`.
- [ ] Link to `/proof-paths/`, `/evidence/`, `/validation/`, and
  `/limitations/` or current equivalent public routes.
- [ ] Confirm and record required target route availability in
  `implementation-state.md` before changing site code.
- [ ] Add a dedicated dist validator for the evidence-trail page following the
  existing pattern, or define the pattern first per Requirement 7 if none
  exists.
- [ ] Export a single validator function covering the `impacted` ban and the
  AI/LLM check list, wire it into `site/scripts/validate.mjs`, and add a
  companion test file.
- [ ] Ensure `npm run validate` enforces required labels, trail steps,
  proof-path links, metadata, forbidden private/raw text, forbidden AI/LLM
  positioning, and internal-link resolution for the rendered evidence-trail
  output.
- [ ] Define and record stable machine-detectable markers for each in-scope
  downstream surface type and each coverage gap so the dist validator can
  assert presence or gap deterministically.
- [ ] Keep visible copy, metadata, alt text, tests, and generated output free
  of raw source snippets, raw SQL, config values, secrets, local absolute
  paths, raw remotes, generated scan directories, private sample names, and
  connection-string tokens.
- [ ] Ban `impacted` in rendered evidence-trail output unless a future spec
  amendment defines a reducer-backed citation rule.

## Validation Tasks

- [ ] Run `git diff --check`.
- [ ] Run `npm test` from `site/`.
- [ ] Run `npm run validate` from `site/`.
- [ ] Run `npm run build` from `site/`.
- [ ] Run `./scripts/check-private-paths.sh`.
- [ ] Run desktop and mobile browser sanity checks for layout or interaction
  changes, or record why they were deferred.
- [ ] Update `implementation-state.md` with final validation, review findings,
  oddities, and follow-up notes.
