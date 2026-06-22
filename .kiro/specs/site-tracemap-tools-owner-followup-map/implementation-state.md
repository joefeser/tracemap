# Site TraceMap Tools Owner Follow-Up Map Implementation State

Status: implemented
Readiness: ready-for-implementation
Public claim level: concept

## Current Branch And Worktree

- Branch: `codex/impl-site-owner-followup-map`
- Base: `origin/dev`
- PR target: `dev`
- Worktree: `<isolated-implementation-worktree>`
- Root checkout untouched by this implementation phase.

## Scope

Implement the public-site owner follow-up map described by this spec. Scope is
limited to:

- `.kiro/specs/site-tracemap-tools-owner-followup-map/requirements.md`
- `.kiro/specs/site-tracemap-tools-owner-followup-map/design.md`
- `.kiro/specs/site-tracemap-tools-owner-followup-map/tasks.md`
- `.kiro/specs/site-tracemap-tools-owner-followup-map/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-owner-followup-map/review-packet.md`
- `site/src/owners/follow-up/index.html`
- `site/src/_site/pages.json`
- `site/src/_site/discovery.json`
- `site/src/_site/pages.json`
- `site/src/questions/index.html`
- `site/src/team-evidence-handoff/index.html`
- `site/src/styles.css`
- `site/scripts/owner-followup-map.mjs`
- `site/scripts/owner-followup-map.test.mjs`
- `site/scripts/validate.mjs`
- `site/scripts/validate.test.mjs`

Do not edit generated output, scanner code, reducer code, or unrelated specs in
this phase.

## Placement State

Final placement: `/owners/follow-up/` as a standalone concept-level route.

Rationale:

- The route is short, shareable, and describes question-to-owner-category
  routing without implying TraceMap performs real org ownership detection.
- Standalone placement avoids making `/questions/` or
  `/team-evidence-handoff/` carry the full required matrix and boundary copy.
- Discovery and sitemap entries can use `publicClaimLevel: concept` directly
  without changing a host page's route purpose.
- The page remains distinct from an org chart, ownership detector, reviewer
  quickstart, packet assembly guide, manager packet, objection guide, incident
  handoff, release gate, or runtime workflow because each row names only an
  owner category, static evidence boundary, limitation, proof path, and stop
  condition.

Rejected alternatives:

- `/review-room/owners/`: close to review-room language, but it could imply a
  meeting-room owner model rather than broad owner-category follow-up.
- Section on `/team-evidence-handoff/`: that page is receiver-specific packet
  handoff language; adding the full matrix would blur packet handoff with
  question-to-owner routing.
- Section on `/questions/`: that page routes stakeholder questions to evidence
  surfaces; this map routes follow-up questions to owner categories and needs
  its own stop conditions.

Discovery/sitemap handling: add a standalone sitemap entry, route-index
discovery metadata, required links to live neighboring routes, and a focused
validator registered in the aggregate site validation. The page will not be
added to primary navigation; discovery will come from route indexes, sitemap,
and useful contextual links on the new page.

Navigation decision: primary navigation was left unchanged. Contextual inbound
links were added from `/questions/` and `/team-evidence-handoff/` because those
routes naturally lead to owner-category follow-up without bloating the global
navigation.

## Scope Decisions

- The page is concept-level because it routes questions to owner categories,
  not real people, teams, approval chains, or production ownership records.
- The map must preserve proof path, limitation, and stop condition with every
  owner handoff.
- Missing evidence is a follow-up question, not a clean conclusion and not an
  accusation.
- The spec must distinguish this future surface from existing handoff,
  reviewer, question, packet, objection, and manager surfaces.

## Review Log

- Opus spec review complete on 2026-06-22 with `claude-opus-4.8`. Findings:
  M1 embedded-section word-count ceiling conflict, M2 embedded self-host
  relationship/link conflict, L1 placement preference wording, L2 test
  coverage exception mismatch, L3 accessibility validation gap. Patch pass
  completed for M1/M2 and the Low findings in `requirements.md` and
  `design.md`.
- Sonnet spec review complete on 2026-06-22 with `claude-sonnet-4.6`.
  Findings: H1 design matrix incomplete, M1 embedded ceiling fallback missing
  from design, M2 implementation gate absent from design, M3 review log not
  updated, L1 concrete stop-condition note, L3 vague link-risk note. Patch
  pass completed for H1/M1/M2/M3 and Low L1/L3 in `design.md` and
  `implementation-state.md`.
- Sonnet re-review complete on 2026-06-22 with `claude-sonnet-4.6`.
  Findings: M1 review-packet review-log pointer absent, M2 embedded ceiling
  negative-test case missing, plus Low findings for file lists, config trigger
  wording, re-review model specificity, and link-risk reminder completeness.
  Patch pass completed in `requirements.md`, `design.md`, `tasks.md`,
  `implementation-state.md`, and `review-packet.md`.
- Opus re-review complete on 2026-06-22 with `claude-opus-4.8`. Findings:
  M1 unsubstituted handoff placeholder validation absent, M2 non-claim
  exception semantics absent from forbidden-claim validation, plus Low
  findings for link-risk reminder completeness, word-count floor alignment,
  and standalone claim-level metadata alignment. Patch pass completed in
  `requirements.md`, `design.md`, and `implementation-state.md`.
- Sonnet re-review complete on 2026-06-22 with `claude-sonnet-4.6`.
  Findings: M1 tasks checklist missing embedded-ceiling negative test, M2
  tasks checklist missing unsubstituted-placeholder negative test, plus Low
  findings for current review-log update and review-packet focus coverage.
  Patch pass completed in `tasks.md`, `implementation-state.md`, and
  `review-packet.md`.
- Sonnet re-review complete on 2026-06-22 with `claude-sonnet-4.6`.
  Findings: M1 bracket-token pattern needed a spec-only template signal, M2
  completed review cycles were not reflected in `tasks.md`, plus Low findings
  for current review-log update and review-packet focus coverage. Patch pass
  completed in `design.md`, `tasks.md`, `implementation-state.md`, and
  `review-packet.md`.
- Sonnet re-review complete on 2026-06-22 with `claude-sonnet-4.6`.
  Findings: M1 patch/re-review task checkbox still pending, M2 validation
  checkboxes still pending. Patch pass completed by running validation,
  updating `tasks.md`, and recording validation results in
  `implementation-state.md`.
- Opus re-review complete on 2026-06-22 with `claude-opus-4.8`. Findings:
  M1 standalone word-count ceiling needed mandatory-content precedence, M2
  readiness conflicted with pending Opus re-review log state, plus Low
  findings for stale review-packet readiness wording and owner-category
  allow-list precision. Patch pass completed in `requirements.md`,
  `design.md`, `implementation-state.md`, and `review-packet.md`.
- Sonnet re-review complete on 2026-06-22 with `claude-sonnet-4.6`.
  Findings: no content or validation-spec defects; only review-log pending
  state remained to record.
- Opus final re-review complete on 2026-06-22 with `claude-opus-4.8`. No
  Medium or higher content findings after the mandatory-content precedence
  patch. The precedence patch was additive and conservative: it relaxes the
  1700-word ceiling only for mandatory rows, row fields, and boundary
  statements, while leaving the 600-word floor and all forbidden-claim and
  private-material checks intact. Readiness remains
  `ready-for-implementation`.

## Validation Log

- Passed on 2026-06-22: `git diff --check`.
- Passed on 2026-06-22: `./scripts/check-private-paths.sh`.
- Passed on 2026-06-22: `cd site && npm test`.
- Passed on 2026-06-22: `cd site && npm run validate`
  (`Validated 59 HTML files, 1993 internal references, 58 sitemap URLs, 1
  legacy story safety targets, and 13 legacy modernization evidence-map
  rows.`).
- Passed on 2026-06-22: `cd site && npm run build`.
- Passed on 2026-06-22 after review fix: `git diff --check`.
- Passed on 2026-06-22 after review fix:
  `./scripts/check-private-paths.sh`.
- Passed on 2026-06-22 after review fix:
  `cd site && node --test scripts/owner-followup-map.test.mjs`.
- Passed on 2026-06-22 after review fix: `cd site && npm test`.
- Passed on 2026-06-22 after review fix: `cd site && npm run validate`
  (`Validated 59 HTML files, 1993 internal references, 58 sitemap URLs, 1
  legacy story safety targets, and 13 legacy modernization evidence-map
  rows.`).
- Passed on 2026-06-22 after review fix: `cd site && npm run build`.
- Passed on 2026-06-22 after test-hardening fix: `git diff --check`.
- Passed on 2026-06-22 after test-hardening fix:
  `./scripts/check-private-paths.sh`.
- Passed on 2026-06-22 after test-hardening fix:
  `cd site && node --test scripts/owner-followup-map.test.mjs`.
- Passed on 2026-06-22 after test-hardening fix: `cd site && npm test`.
- Passed on 2026-06-22 after test-hardening fix:
  `cd site && npm run validate` (`Validated 59 HTML files, 1993 internal
  references, 58 sitemap URLs, 1 legacy story safety targets, and 13 legacy
  modernization evidence-map rows.`).
- Passed on 2026-06-22 after test-hardening fix: `cd site && npm run build`.
- Passed on 2026-06-22: desktop browser sanity for `/owners/follow-up/` at
  1280x900. The page rendered title `Owner Follow-Up Map | TraceMap`, 8 owner
  rows, visible `Public claim level: concept. No public conclusion without
  evidence.`, no horizontal overflow, and no browser console warnings/errors.
- Passed on 2026-06-22: mobile browser sanity for `/owners/follow-up/` at
  390x844. The page rendered 8 owner rows, visible claim-level note, no
  horizontal overflow, and no browser console warnings/errors.

Implementation validation included the full required site test, validation,
build, private-path, diff, and browser sanity checks.

## PR Loop State

- PR loop initial outcome on PR #290 head
  `56033f6de5ef16d281d3e00d9b5b59f766cbc37b`: first run stopped on
  `ACTIONABLE_BOT_FINDINGS` while `nextAction` was
  `wait_for_required_reviewers` because the required Codex review request was
  still active. No patch was made until the review batch settled.
- PR loop second actionable outcome on the same head:
  `UNRESOLVED_REVIEW_THREADS`, `nextAction: patch_actionable_findings`.
  Actionable finding was Codex/Qodo review feedback on
  `site/scripts/owner-followup-map.mjs` requiring every forbidden-claim
  occurrence to be checked, not only the first match.
- Follow-up fix: `validateForbiddenText` now checks every
  forbidden-claim match with a global clone of each regex and keeps the
  negated-context guard per occurrence. Added regression coverage for a safe
  negated occurrence before a later unsafe occurrence.
- PR loop follow-up outcome on head
  `5bdb52f7d1a6a5a110ce1e24758a0721d7d1a255`: Qodo reported brittle test
  fixture mutation in `site/scripts/owner-followup-map.test.mjs` because the
  negative test depended on exact serialized HTML spacing and copy.
- Follow-up fix: the owner-followup test now removes required fixture
  fragments through attribute-targeted patterns and asserts that each fixture
  mutation actually changed the page before validating the expected error.
- Latest PR loop outcome: pending rerun after the test-hardening commit is
  pushed.

## Oddities

- The future page must visibly name owner categories while repeatedly stating
  that TraceMap does not know real org ownership.
- All required public links were live at implementation time. `/manager-packet/`
  existed as a route but did not have discovery metadata, so this
  implementation added bounded demo-level discovery metadata for the existing
  manager packet route rather than weakening owner-map validation.
- The standalone page fit within the 600-to-1700 rendered word-count target;
  no mandatory-content overflow exception was needed.

## Follow-Up Items

- Push the test-hardening follow-up commit, rerun the required PR loop, and
  record the final exact decision/stop reason here before final handoff.
