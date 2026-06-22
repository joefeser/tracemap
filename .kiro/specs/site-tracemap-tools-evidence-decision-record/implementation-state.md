# Site TraceMap Tools Evidence Decision Record Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-evidence-decision-record`

Base: `origin/dev`

Target PR base: `dev`

Worktree: isolated worktree requested by the operator. The machine-local path
is not repeated in this committed spec state to keep repository docs free of
private local path material.

Scope: spec-only packet for a future public-site evidence decision record.
Only `.kiro/specs/site-tracemap-tools-evidence-decision-record/` is in scope.
Site source, generated output, scanner code, reducer code, validation scripts,
and existing specs stay out of scope.

## Current State

Spec packet drafted, reviewed, patched, and validated for spec-only handoff.

No site source, scanner code, reducer code, generated `site/dist`, generated
`site/output`, existing specs, runtime telemetry, AI/LLM analysis, embeddings,
vector databases, prompt classification, decision automation, approval
workflow, or generated evidence artifacts have been changed.

The future surface is a decision-after-evidence record. It should show how a
team records a human owner decision after inspecting TraceMap evidence without
upgrading static evidence into runtime proof, release approval, production
proof, absence-of-impact proof, complete coverage, AI analysis, autonomous
decision-making, or replacement of human judgment and governance.

## Claim-Level Decision

Selected public claim level: `concept`.

Rationale: the future page or section is an explanatory record template and
workflow over existing public-safe evidence surfaces. It does not publish new
demo evidence, prove a new capability, make decisions, approve releases, or
prove runtime or production behavior.

Future implementation must include visible `Public claim level: concept` and
visible `No public conclusion without evidence`.

## Placement Guidance

Candidate placements:

- `/decisions/evidence-record/`
- `/review-room/decision-record/`
- A section on `/review-room/`
- A section on `/packets/assembly/`

No final placement is selected in this spec-only phase. Future implementation
must choose the final placement and record rejected alternatives before
changing site source.

The final placement decision must record why the selected route or section
won. The rejected-alternatives record must separately name the three unchosen
candidate placements from this spec and explain why each was rejected.
Requirement 7 validation should fail if the placement decision record does not
collectively reference all four spec-defined candidate placements.

At implementation time, confirm the candidate routes still exist in the live
site before selecting one. If site information architecture has changed,
record the substitution and rationale in this file.

If `/decisions/evidence-record/` is selected, record whether the new
`/decisions/` namespace is intended to support future sibling decision-record
surfaces or only this single concept route.

Rejected-by-default alternatives unless implementation-state is amended:

- Replacing `/review-room/`
- Replacing `/packets/assembly/`
- Replacing `/review-claim-checklist/`
- Replacing `/manager-packet/`
- Replacing `/questions/objections/`
- Replacing `/proof-paths/tour/`
- Adding the route to primary navigation without a recorded
  information-architecture decision

## Relationship Decisions

The future evidence decision record must distinguish itself from:

- `/review-room/`: meeting agenda for known, partial, and missing evidence.
- `/packets/assembly/`: evidence ingredient assembly workflow before handoff.
- `/review-claim-checklist/`: repeatability ritual for claims and sentences.
- `/manager-packet/`: manager-facing orientation and value explanation.
- `/questions/objections/`: skeptical question handling and owner routing.
- `/proof-paths/tour/`: proof-path education and navigation.

## Scope Decisions

- Required files: `requirements.md`, `design.md`, `tasks.md`,
  `implementation-state.md`, and `review-packet.md`.
- Required future record fields are fixed by the user request and appear in
  requirements, design, and tasks.
- Required future sections are fixed by the user request and appear in
  requirements, design, and tasks.
- The page-level public claim level remains `concept`.
- Record owner fields use public role categories or placeholders, not private
  people or private teams.
- Tone must be calm, professional, and non-blaming.
- No raw artifacts, private material, hidden validation details, local paths,
  raw command output, or credential-like values may appear in public output.
- Future validation must cover required record fields, required links,
  metadata, discovery/sitemap metadata if standalone, forbidden approval or
  decision claims, private/raw material, word count bounds, and desktop/mobile
  browser sanity.

## Review Commands

Planned spec review commands:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-evidence-decision-record --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-evidence-decision-record --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

If a model or tool is unavailable, record the exact command and error here.

## Review Results

Initial Kiro review commands:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-evidence-decision-record --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-evidence-decision-record --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- `claude-opus-4.8`: command completed with reduced coverage because Kiro
  reported denied tool access. Artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-decision-record/2026-06-22T040059-355Z-spec-claude-opus-4.8.clean.md`.
  Meta recorded `toolDenied: true`, `reviewComplete: true`, and
  `reviewCoverage: "Reduced"`.
- `claude-sonnet-4.6`: command completed with reduced coverage because Kiro
  reported denied tool access. Artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-decision-record/2026-06-22T040059-454Z-spec-claude-sonnet-4.6.clean.md`.
  Meta recorded `toolDenied: true`, `reviewComplete: true`, and
  `reviewCoverage: "Reduced"`.

Medium findings patched across review and re-review:

- Allowed forbidden-term contexts now include `rejected-interpretation` and
  `residual-risk` so the required safe record can name rejected conclusions
  without false positives.
- `tasks.md` now includes the blocked/unavailable review-command fallback.
- Placement guidance now requires live site IA confirmation and records
  `/decisions/` namespace intent if that route is selected.
- Validation now requires structural markers rather than broad prose
  inference for allowed forbidden-term contexts.
- Validation now scans link anchor text and link title attributes.
- Word-count bounds are now `700` to `2500` rendered words by default, with
  bounded amendment rules and trim-eligible content guidance.
- Validation now checks review date placeholders, synthetic/public-safe commit
  SHA and extractor version examples, synthetic/public-safe validation
  evidence examples, accessibility for the responsive template, and
  scroll-to-section sanity for section implementations.
- A reference marker convention,
  `data-tracemap-validation-context="<context>"`, is documented for
  allowed forbidden-term contexts.
- Structural-boundary negative fixture requirements now explicitly place a
  forbidden positive claim inside
  `data-tracemap-validation-context="unsafe-example"` but outside any allowed
  unsafe-example section or field container, then assert validation fails.
- Placement decision validation now separates selected placement rationale
  from the three unchosen rejected alternatives.
- Validation wiring now includes a fallback for aggregate `npm run validate`
  workflows that require explicit route or section registration.

Re-review commands were rerun multiple times with both requested models after
patches. Final re-review artifacts:

- `claude-opus-4.8`:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-decision-record/2026-06-22T041632-333Z-re-review-claude-opus-4.8.clean.md`
  and matching `.meta.json`. Meta recorded `toolDenied: true`,
  `reviewComplete: false`, and `reviewCoverage: "Reduced"`.
- `claude-sonnet-4.6`:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-decision-record/2026-06-22T041632-422Z-re-review-claude-sonnet-4.6.clean.md`
  and matching `.meta.json`. Meta recorded `toolDenied: true`,
  `reviewComplete: false`, and `reviewCoverage: "Reduced"`.

Remaining review state:

- No High or Critical findings were reported.
- Medium findings from review output have been patched or converted into
  explicit implementation requirements.
- Remaining Low or residual risks: route existence must be verified at future
  implementation time; word-count trim guidance must be followed if the future
  page approaches the ceiling; automated review coverage was reduced because
  Kiro reported denied tool access.
- Readiness decision: `ready-for-implementation` for the future spec. This is
  a spec-only branch and does not implement site source.

## Validation

Spec-only validation:

```bash
git diff --check
./scripts/check-private-paths.sh
```

Future implementation validation is listed in `requirements.md` and
`tasks.md`.

Results:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- Focused text sanity confirmed no machine-local worktree path is committed in
  the spec packet and no readiness header remains at `spec-review`.

## PR Loop

PR: `https://github.com/joefeser/tracemap/pull/281`

PR-loop command:

```bash
agent-control pr-loop --repo joefeser/tracemap --pr 281 --base dev --require-codex-review --quiet --json
```

Observed PR-loop history:

- Initial run after PR creation posted or observed the required Codex review
  request and stopped with `decision: actionable_findings`,
  `stopReason: ACTIONABLE_BOT_FINDINGS`, and `nextAction:
  wait_for_required_reviewers`; no patch was made until the required reviewer
  lock cleared.
- Follow-up run after Codex returned stopped with `decision:
  actionable_findings`, `stopReason: UNRESOLVED_REVIEW_THREADS`, and
  `nextAction: patch_actionable_findings` for a Codex review thread about
  negative fixtures being included in forbidden-claim sweeps.
- The negative-fixture validation wording was patched in follow-up commit
  `598b7e7a`.
- Post-patch PR-loop stopped with `decision: actionable_findings` because the
  earlier Qodo top-level stale-status finding still applied to this section.
  This section was updated to remove stale pending text.

## Residual Risk

Known residual risks:

- Kiro review coverage was reduced because Kiro reported denied tool access
  during automated spec review and re-review runs.
- Required review freshness and final PR-loop status must be rechecked after
  this implementation-state update lands on the PR.
- This branch remains spec-only. Future implementation still needs route or
  section selection, site-source changes, focused validation, and browser
  sanity checks.
