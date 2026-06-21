# Site TraceMap Tools Manager Demo Script Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Spec Packet

- [x] Create `requirements.md` for the manager demo script spec.
- [x] Create `design.md` for route placement, page model, script blocks, and
  validation shape.
- [x] Create `tasks.md` with spec-review and future implementation tasks.
- [x] Create `implementation-state.md` with branch, scope, review, validation,
  and follow-up state.
- [x] Create `review-packet.md` for reviewer orientation.
- [x] Run Kiro spec review with `claude-opus-4.8` or record the exact
  command/error if unavailable.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  command/error if unavailable.
- [x] Patch or disposition Medium+ review findings.
- [x] Rerun re-review where feasible.
- [x] Move readiness to `ready-for-implementation` only after Medium+ findings
  are patched or dispositioned.
- [x] Run spec-only validation: `git diff --check`,
  `./scripts/check-private-paths.sh`, and focused text checks.

## Future Site Implementation

- [x] Confirm final placement from `/demo/manager-script/`, `/demo/briefing/`,
  section on `/demo/runbook/`, section on `/manager-brief/`, or a documented
  implementation-time equivalent.
- [x] Record the final placement and rejected alternatives in
  `implementation-state.md`.
- [x] Implement the page or section with visible `Public claim level: concept`.
- [x] Implement visible `No public conclusion without evidence`.
- [x] Include opening context, 2-minute tour, 5-minute proof walkthrough,
  manager questions and safe answer shapes, engineer questions and proof
  routes, stop conditions, follow-up handoff, and non-claims.
- [x] Verify each intended route exists before linking: `/`, `/capabilities/`,
  `/proof-paths/`, `/proof-source-catalog/`, `/demo/result/`,
  `/demo/runbook/`, `/questions/`, `/limitations/`, `/validation/`, and
  `/static-vs-runtime/`.
- [x] Record substitutions, removals, or blocks for unavailable routes.
- [x] Add metadata, sitemap metadata if standalone, and discovery metadata if
  comparable pages use it, all with `publicClaimLevel: concept`.
- [x] Add focused validation for required copy, required links, metadata,
  forbidden claims, private/raw materials, and word count bounds.
- [x] Run `npm test`, `npm run validate`, and `npm run build` from `site/`.
- [x] Run desktop and mobile browser sanity checks for the implemented surface.
- [x] Record validation, oddities, and follow-up items in
  `implementation-state.md`.
