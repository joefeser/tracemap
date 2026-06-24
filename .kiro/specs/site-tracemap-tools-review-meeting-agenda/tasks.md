# Site TraceMap Tools Review Meeting Agenda Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

Ordering note: implementation tasks are checked only after the matching site
work or validation is complete.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Patch Medium or higher spec findings from spec review or record an
  evidence-backed not-applicable disposition in `implementation-state.md`.
- [x] Rerun re-review after Medium or higher patches where feasible.
  Iterative re-review rounds and any still-pending final re-review are recorded
  in `implementation-state.md`.
- [x] Move `Readiness` to `ready-for-implementation` only after Medium or
  higher findings are patched or explicitly dispositioned.

## Future Implementation Tasks

- [x] Confirm or update this spec-local `implementation-state.md` with branch,
  target base, selected placement, rejected alternatives, scope decisions, and
  initial implementation status before changing site code.
- [x] Choose `/review-room/agenda/`, `/meetings/evidence-review/`, section
  placement on `/review-room/`, or section placement on
  `/reviewer-quickstart/`; record the selected placement and rejected
  alternatives.
- [x] Add a bounded public concept page or section using existing static-site
  layout, navigation, accessibility, and validation patterns.
- [x] Record that primary navigation was left unchanged, or record the
  information-architecture review that chose primary navigation placement.
- [x] Add the visible `Public claim level: concept` label.
- [x] Add the visible principle `No public conclusion without evidence`.
- [x] Include `Before the meeting`, `Agenda`, `Evidence checks`,
  `Gap capture`, `Owner assignment`, `Decision record handoff`,
  `Stop conditions`, and `Non-claims`.
- [x] Include agenda rows for `question framing`, `proof path check`,
  `evidence tier/coverage check`, `limitation check`,
  `gap register check`, `owner follow-up`, `decision record`, and
  `closeout`.
- [x] Keep the agenda tied to review question, proof path, rule ID or rule
  family, evidence tier, coverage label, limitation, gap, owner, decision
  record, and non-claim vocabulary.
- [x] Ensure the `decision record` agenda row carries limitations in its
  evidence input and handoff output, along with validation evidence category.
- [x] Ensure the `Evidence checks` section requires rule ID or rule family
  before evidence is repeated as meeting support.
- [x] Ensure the `Stop conditions` section enumerates the required stop or
  downgrade triggers, not only the heading.
- [x] Use canonical evidence tier labels: `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, and `Tier4Unknown`.
- [x] State that the agenda is not meeting automation, release approval,
  release safety proof, runtime proof, production traffic proof, endpoint
  performance proof, absence-of-impact proof, complete coverage, AI/LLM
  analysis, or human-governance replacement.
- [x] State that the agenda does not replace tests, code review, source
  review, runtime observability, release review, owner confirmation, or human
  judgment.
- [x] Avoid unsupported `impacted`, `safe`, `unsafe`, `approved`, `blocked`,
  `production proven`, `performance proven`, `complete`, and `root cause`
  wording outside explicit non-claims or stop conditions.
- [x] Avoid blame language around vendors, consultants, teams, owners,
  reviewers, or code.
- [x] Avoid publishing raw facts, SQLite content, analyzer logs, source
  snippets, SQL, config values, secrets, local paths, raw remotes, generated
  scan directories, private sample names, command output, hidden validation
  details, credential-like values, connection strings, tokens, or keys.
- [x] Verify candidate cross-links in generated output before adding them:
  `/proof-paths/`, `/evidence/`, `/validation/`, `/limitations/`,
  `/review-room/`, `/reviewer-quickstart/`, `/packets/assembly/`,
  `/handoff/template/`, `/owners/follow-up/`, and
  `/decisions/evidence-record/`.
- [x] Treat `/proof-paths/`, `/validation/`, and `/limitations/` as required
  core links unless section placement or a documented live route gap records a
  substitute.
- [x] If `/manager-demo-script/` exists, link to it only as a neighboring
  distinction and never as meeting evidence; if the live equivalent is
  different, verify that route before linking or record the mismatch as a
  documented gap.
- [x] Record unresolved, substituted, or deferred cross-links in
  `implementation-state.md`.
- [x] Distinguish the page or section from `/review-room/`,
  `/reviewer-quickstart/`, `/packets/assembly/`, `/handoff/template/`,
  `/owners/follow-up/`, `/decisions/evidence-record/`, and the verified
  manager demo or presentation route if one resolves in generated output;
  otherwise record it as a documented gap.
- [x] If implemented as a standalone route, add title, description, canonical
  URL, Open Graph metadata, route-index metadata, sitemap metadata, and
  discovery metadata with `publicClaimLevel: concept`.
- [x] If implemented as a section, add a stable anchor and extend the host
  page's validator, metadata, route-index, or discovery coverage only where
  the live site pattern supports it. Not applicable: implemented as standalone
  `/review-room/agenda/` route.
- [x] Add focused validation for required copy, required agenda rows, required
  links, metadata, discovery metadata, sitemap metadata when standalone,
  forbidden claims, private/raw material, blame language, unsupported
  certainty language, manual public-safety reviewer signoff when automated
  blame-language detection is not implemented, non-canonical tier terms,
  accessibility, word-count bounds, and desktop/mobile browser sanity.
- [x] Validate rendered main-content word count against a bounded range chosen
  from neighboring concept-page validators, and record the range in
  `implementation-state.md`.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`.
- [x] Run `npm run build` from `site/`.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run desktop and mobile browser sanity checks if route, layout, or
  interaction changes are made, or document why they were deferred.
- [ ] Update `implementation-state.md` with final validation, placement
  choice, review-loop outcomes, oddities, and follow-up notes.
