# Site TraceMap Tools Test Planning Handoff Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

Ordering note: this is a spec-only phase. Do not implement public-site code,
edit `site/src`, generated outputs, scanner code, or existing specs in this
phase. Do not hand-edit `site/dist/`, `site/output/`, or any other generated
output directory.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- If both model-specific reviews are blocked, record the exact errors and note
  that advancement to `ready-for-implementation` requires explicit human
  sign-off.
- [x] Patch Medium or higher spec findings and rerun re-review where feasible.
- [x] Run `git diff --check` and `./scripts/check-private-paths.sh` on the
  spec directory to confirm no private material leaked into spec files.
- [x] Update `Readiness` to `ready-for-implementation` only after review
  findings are patched or exact unavailable-tool/model errors are recorded.

## Future Implementation Tasks

- [x] Confirm or update this spec-local `implementation-state.md` with branch,
  selected placement, scope, public claim level, review status, validation
  plan, oddities, and follow-ups before changing site code.
- [x] Inspect `/reviewer-quickstart/`, `/packets/assembly/`,
  `/review-claim-checklist/`, `/validation/`, `/proof-paths/tour/`, and
  `/questions/objections/` before selecting the final placement.
- [x] Select `/test-planning/`, `/reviewer-quickstart/test-planning/`, a
  section on `/reviewer-quickstart/`, or a section on `/packets/assembly/`,
  then record the rationale in `implementation-state.md`.
- [x] Add or update only the selected public-site surface using existing static
  site layout patterns.
- [x] Include `Public claim level: concept` and `No public conclusion without
  evidence` visibly on the surface.
- [x] Include sections for static evidence input, test-planning questions,
  coverage caveats, examples of safe handoff language, stop conditions, test
  owner handoff, and non-claims.
- [x] Include visible field labels for claim label, proof path, rule
  ID/family, evidence tier, coverage label, changed surface, limitation,
  suggested test question, next owner, validation evidence, and non-claim.
- [x] Explain how proof paths, rule IDs or rule families, evidence tiers,
  coverage labels, changed surfaces, and limitations become human-owned
  test-planning questions.
- [x] Keep examples as safe handoff language and avoid generated tests, test
  sufficiency, runtime behavior proof, production traffic proof, endpoint
  performance proof, release safety, release approval, complete coverage,
  AI/LLM analysis, and QA replacement claims.
- [x] Use role-based next owners rather than named individuals or personal
  owner names in public examples and metadata.
- [x] Choose a standalone or nested route if the complete required content
  cannot fit an embedded section without dropping required labels,
  distinctions, caveats, stop conditions, or non-claims.
- [x] Avoid raw facts, SQLite content, analyzer logs, source snippets, SQL,
  config values, secrets, local paths, raw remotes, generated scan
  directories, private sample names, command output, hidden validation details,
  and credential-like values.
- [x] Distinguish the surface from `/reviewer-quickstart/`,
  `/packets/assembly/`, `/review-claim-checklist/`, `/validation/`,
  `/proof-paths/tour/`, and `/questions/objections/`.
- [x] Add bounded links to the neighboring routes that exist at implementation
  time, documenting route gaps or substitutions in `implementation-state.md`.
- [x] Add title, description, canonical URL, Open Graph metadata, concept
  claim metadata, sitemap metadata, and discovery metadata if the placement is
  standalone.
- [x] If embedded, update host metadata only where needed to make the section
  discoverable without overclaiming. Not applicable: selected standalone
  `/test-planning/`.
- [x] Add focused validation for required copy, required links, route or
  section metadata, discovery/sitemap metadata when standalone, forbidden
  claims, private/raw material, word-count bounds, and desktop/mobile browser
  sanity expectations, including basic accessibility checks.
- [x] Run `git diff --check`.
- [x] Run the relevant site tests, validation, and build commands from `site/`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run desktop and mobile browser sanity checks for the selected surface, or
  document why deferred.
- [x] Update this spec's `implementation-state.md` with final implementation
  scope, validation results, oddities, and follow-up items.
