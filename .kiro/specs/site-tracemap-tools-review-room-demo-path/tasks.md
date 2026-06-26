# Site TraceMap Tools Review Room Demo Path Tasks

Status: implemented
Readiness: ready-for-pr
Public claim level: concept

Keep task status current as implementation, validation, commit, PR, and ACK
work completes. Check only work that has actually landed.

## Spec Packet Tasks

- [x] Create `requirements.md` with concept-level claim boundaries, route or
  section placement acceptance criteria, step contract, link requirements,
  proof-path requirements, stop conditions, owner routing, metadata/discovery,
  forbidden wording, and validation requirements.
- [x] Create `design.md` with the proposed route model, page structure,
  required guided-path steps, proof/packet fields, owner routing, stop model,
  metadata design, and validation design.
- [x] Create `tasks.md` and keep this checklist current.
- [x] Create `implementation-state.md` with branch, base, scope decisions,
  status, validation plan, oddities, and follow-up notes.
- [x] Create `review-packet.md` for Kiro spec reviewers.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8` or record the exact blocker
  in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  blocker in `implementation-state.md`.
- [x] Patch Medium or higher actionable spec findings, if any.
- [x] Patch Low findings only when narrow and safe.
- [x] Run one bounded Kiro re-review if findings were patched, or record why
  re-review was unnecessary.
- [x] Patch Medium findings from bounded re-review.
- [x] Record review artifacts, findings, patches, and dispositions in
  `implementation-state.md`.

## Implementation Tasks

- [x] Create dedicated implementation worktree and branch from latest
  `origin/dev`.
- [x] Re-check adjacent public route existence before adding links.
- [x] Select `/review-room/demo-path/` as the standalone public route and
  record rejected placement alternatives.
- [x] Add visible `Public claim level: concept` and `No public conclusion
  without evidence`.
- [x] Render the nine guided-path steps in order as one contiguous block.
- [x] Give each guided-path step non-empty limitation, stop condition, and
  next owner or route text.
- [x] Link the evidence-packet step to present packet routes.
- [x] Use public role-label owner routing only.
- [x] Add sitemap metadata, discovery metadata, canonical metadata, and
  Open Graph metadata.
- [x] Keep the route out of primary navigation.
- [x] Add focused site validation for required markers, ordered steps,
  non-empty step fields, packet-route guard, final stop step, forbidden
  wording, private/raw material, metadata/discovery/sitemap, and valid links.

## Validation Tasks

- [x] Run `cd site && npm test`.
- [x] Run `cd site && npm run validate`.
- [x] Run `cd site && npm run build`.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Perform desktop browser sanity check for `/review-room/demo-path/`, or
  record the exact blocker.
- [x] Perform mobile browser sanity check for `/review-room/demo-path/`, or
  record the exact blocker.
- [x] Run `git diff --name-only origin/dev...HEAD` and confirm the diff is
  limited to site source/test/spec files required by this spec.
- [x] Record validation results and browser check notes in
  `implementation-state.md`.

## Publish And ACK Tasks

- [x] Commit the implementation.
- [x] Push the branch.
- [x] Create a ready pull request into `dev`.
- [x] Wait about 3 minutes after PR creation.
- [x] Run ACK with
  `agent-control pr-loop --repo joefeser/tracemap --pr <PR> --base dev --require-codex-review --quiet --json`.
- [x] Follow ACK instructions without merging.
- [x] Record ACK decision, stop reason, next action, checks state,
  unresolved threads count, actionable findings count, reviewer state, and
  owner answer in `implementation-state.md`.
