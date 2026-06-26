# Site TraceMap Tools Review Room Demo Path Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

Ordering note: this is a spec-only packet. Keep task status current as review,
patching, validation, commit, PR, and ACK work completes. Do not implement site
code in this phase.

## Spec Packet Tasks

- [x] Create `requirements.md` with concept-level claim boundaries, route or
  section placement acceptance criteria, step contract, link requirements,
  proof-path requirements, stop conditions, owner routing, metadata/discovery,
  forbidden wording, and validation requirements.
- [x] Create `design.md` with the proposed route model, page structure,
  required guided-path steps, proof/packet fields, owner routing, stop model,
  metadata design, and validation design.
- [x] Create `tasks.md` and keep this spec-only checklist current.
- [x] Create `implementation-state.md` with branch, base, scope decisions,
  initial status, validation plan, and follow-up notes.
- [x] Create `review-packet.md` for Kiro spec reviewers.

## Review Tasks

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

## Validation Tasks

- [x] Confirm the diff is limited to
  `.kiro/specs/site-tracemap-tools-review-room-demo-path/`.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Record validation results in `implementation-state.md`.

## Publish Tasks

- [x] Commit the spec-only packet.
- [x] Push the branch.
- [x] Create a ready pull request into `dev`.
- [x] Run ACK with
  `agent-control pr-loop --repo joefeser/tracemap --pr <PR> --base dev --require-codex-review --quiet --json`.
- [x] Follow ACK instructions without merging.
- [x] Record ACK decision, stop reason, and whether Joe can merge.
