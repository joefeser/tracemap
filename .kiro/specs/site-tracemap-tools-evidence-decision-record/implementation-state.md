# Site TraceMap Tools Evidence Decision Record Implementation State

Status: implemented
Readiness: implemented
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-evidence-decision-record`

Implementation branch: `codex/impl-site-evidence-decision-record`

Base: `origin/dev`

Target PR base: `dev`

Worktree: isolated worktree requested by the operator. The machine-local path
is not repeated in this committed spec state to keep repository docs free of
private local path material.

Scope: public-site implementation for the evidence decision record, focused on
`site/src/` source, site validation scripts and tests, and this spec-local
bookkeeping. Generated output, scanner code, reducer code, runtime telemetry,
decision automation, approval workflow, and private evidence artifacts stay out
of scope.

## Current State

Implemented as a standalone public route with focused validation and
secondary discovery.

Site source changed under `site/src/`, validation was added under
`site/scripts/`, and the aggregate site validator now exercises the route.
Generated `site/dist`, generated `site/output`, scanner code, reducer code,
runtime telemetry, AI/LLM analysis, embeddings, vector databases, prompt
classification, decision automation, approval workflow, and generated evidence
artifacts have not been changed.

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

## Placement Decision

Selected placement: `/decisions/evidence-record/`

Rationale: a standalone route gives the evidence decision record a durable
address as a decision-after-evidence record. The route captures a human owner
decision, rejected interpretation, follow-up owner, and residual risk after a
proof path is inspected. It does not replace adjacent concept surfaces and it
does not create a release gate, runtime workflow, approval workflow, or
autonomous decision system.

Selected namespace decision: `/decisions/` is introduced for this single
concept-level decision-record surface. It is not being promoted into primary
navigation and does not imply a family of shipped decision automation features.
Future sibling decision-record routes would need separate spec and validation
work.

Candidate placements considered:

- `/decisions/evidence-record/`
- `/review-room/decision-record/`
- A section on `/review-room/`
- A section on `/packets/assembly/`

Rejected alternatives:

- `/review-room/decision-record/`: rejected because nesting under the review
  room makes the artifact sound like part of the meeting agenda. The selected
  page is the post-review record, not the review-room agenda.
- section on `/review-room/`: rejected because `/review-room/` remains the
  meeting agenda for known, partial, and missing evidence. Adding the whole
  template there would blur the line between inspection and the later owner
  decision.
- section on `/packets/assembly/`: rejected because `/packets/assembly/`
  remains the packet assembly checklist before handoff. The evidence decision
  record captures one owner decision and residual risk after evidence review.

Rejected replace-a-neighbor options:

- Replacing `/review-room/`: rejected because the review room is the review-room agenda.
- Replacing `/packets/assembly/`: rejected because packet assembly is the packet assembly checklist.
- Replacing `/review-claim-checklist/`: rejected because that route is the claim checklist and repeatability ritual, not a decision record.
- Replacing `/manager-packet/`: rejected because manager packet copy is orientation, not the compact owner decision artifact.
- Replacing `/questions/objections/`: rejected because that route is the objection guide, while this record logs the owner decision after the question is resolved.
- Replacing `/proof-paths/tour/`: rejected because the proof-path tour teaches how to follow evidence, while this record cites the proof path after inspection.
- Replacing a release gate, runtime workflow, approval workflow, or autonomous decision system: rejected because TraceMap provides evidence, not the decision.
- Adding the route to primary navigation: rejected because this is a concept-level secondary reference; discovery metadata plus inbound links from `/review-room/` and `/packets/assembly/` are sufficient.

Route availability checked during implementation: `/review-room/`,
`/packets/assembly/`, `/review-claim-checklist/`, `/manager-packet/`,
`/questions/objections/`, `/proof-paths/tour/`, `/proof-paths/`,
`/limitations/`, and `/validation/` are present and linked from the selected
route.

Navigation decision: the route is intentionally not in primary navigation.
Inbound links were added from `/review-room/` and `/packets/assembly/` because
those pages naturally precede a recorded owner decision.

## Relationship Decisions

The future evidence decision record must distinguish itself from:

- `/review-room/`: meeting agenda for known, partial, and missing evidence.
- `/packets/assembly/`: evidence ingredient assembly workflow before handoff.
- `/review-claim-checklist/`: repeatability ritual for claims and sentences.
- `/manager-packet/`: manager-facing orientation and value explanation.
- `/questions/objections/`: skeptical question handling and owner routing.
- `/proof-paths/tour/`: proof-path education and navigation.

The implemented page includes an adjacent-surfaces section that distinguishes
itself from those routes using bounded link text.

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
Implementation uses the reference structural marker convention
`data-tracemap-validation-context="<context>"` for allowed boundary contexts.
No substitute marker convention was used.

Focused validation covers required record fields, required sections, required
links, metadata, sitemap metadata, discovery metadata, forbidden approval or
decision claims, private/raw material, word count bounds, route-specific
non-claims, placement-decision state, structural-boundary negative fixtures,
placeholder dates, synthetic commit and extractor examples, public-safe
validation evidence, and template accessibility markers.

Public claim level remains `concept`. No stronger page-level or example-record
claim level was selected.

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

Implementation validation:

```bash
git diff --check
./scripts/check-private-paths.sh
cd site && npm test
cd site && npm run validate
cd site && npm run build
```

Results:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed with `Private path guard passed.`
- `cd site && npm test`: passed, 412 tests.
- `cd site && npm run validate`: passed; generated `site/dist` and validated
  61 HTML files, 2068 internal references, 60 sitemap URLs, 1 legacy story
  safety target, and 13 legacy modernization evidence-map rows.
- `cd site && npm run build`: passed.

Post-review fix validation:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed with `Private path guard passed.`
- `cd site && npm test`: passed, 413 tests.
- `cd site && npm run validate`: initially failed when run in parallel with
  `npm run build` because both commands rewrite `site/dist`; rerun
  sequentially and passed, validating 61 HTML files, 2068 internal references,
  60 sitemap URLs, 1 legacy story safety target, and 13 legacy modernization
  evidence-map rows.
- `cd site && npm run build`: passed.

Browser sanity:

- Started the local site server on port `4174` because port `4173` was already
  in use.
- Desktop viewport `1440x1000`: `/decisions/evidence-record/` loaded with the
  expected title, H1, required anchors, and no document-level horizontal
  overflow.
- Mobile viewport `390x844`: `/decisions/evidence-record/` loaded with the
  expected title, H1, required anchors, and no document-level horizontal
  overflow. The record table is wider than the viewport but contained in the
  existing horizontally scrollable table wrapper used by the site.
- The local server was stopped after browser sanity.

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

Implementation PR-loop status: pending until the implementation PR is created
and the required `agent-control pr-loop --repo joefeser/tracemap --pr
<PR_NUMBER> --base dev --require-codex-review --quiet --json` command
returns a terminal decision.

Implementation PR: `https://github.com/joefeser/tracemap/pull/294`

Implementation PR-loop history:

- Initial implementation PR-loop command:
  `agent-control pr-loop --repo joefeser/tracemap --pr 294 --base dev --require-codex-review --quiet --json`.
- First terminal result stopped with `decision: actionable_findings`,
  `stopReason: UNRESOLVED_REVIEW_THREADS`, and `nextAction:
  wait_for_required_reviewers` because the required Codex request was active.
  No patch was made at that point.
- After waiting and rerunning the same command, required reviewers reached a
  terminal batch state: Codex `review_completed`, Qodo
  `actionable_findings`, `requiredReviewBatch.patchAuthorized: true`, and
  `nextAction: patch_actionable_findings`.
- Combined findings patched:
  unused `lowerPageText` removal; test fixture implementation-state path kept
  inside the temporary fixture root; allowed validation-context stripping
  scoped to approved record-template and safe-example containers; negation
  handling tightened so unrelated negated clauses cannot mask a later positive
  forbidden claim; regression coverage added.
- PR-loop after the post-review fix commit stopped with `decision:
  actionable_findings`, `stopReason: UNRESOLVED_REVIEW_THREADS`, `nextAction:
  patch_actionable_findings`, `reviewFreshnessPosture: hard_blocker`,
  `residualRiskLevel: high`, and `canMerge: false`.
- Current pushed head at that readback:
  `364aed94edaad3031ff15855fd9a1435cfd09ba5`.
- Checks at that readback: no pending checks and no failed checks.
- Remaining unresolved review threads at that readback:
  3 threads. They are the patched temp-fixture state-path thread, the patched
  validation-context stripping thread, and the patched Codex
  validation-context stripping thread. I did not manually resolve bot review
  threads or post disposition comments because that is a GitHub write action
  outside the normal patch/validate/push loop.
- Required reviewer state at that readback: Codex reviewed
  `8b7fcbdc807a5e20a7aec5be5314fa2c91a64c45`; current head was
  `364aed94edaad3031ff15855fd9a1435cfd09ba5`, so Codex review freshness was
  stale after the fix commit. Qodo still reported actionable findings because
  the unresolved Qodo thread remained open.
- Owner handoff at that readback: not merge-ready. Joe should not merge unless
  the remaining review threads are resolved or explicitly dispositioned, and
  the stale required-review risk is accepted for the exact current head or a
  fresh review returns clean.

## Residual Risk

Known residual risks:

- Kiro review coverage was reduced because Kiro reported denied tool access
  during automated spec review and re-review runs.
- Required reviewer freshness and final PR-loop status must be checked after
  this implementation branch opens a PR.
- The page remains concept-level public copy. It is a record template and does
  not prove runtime behavior, production behavior, release safety, absence of
  impact, complete coverage, autonomous decisions, approval workflow behavior,
  AI/LLM analysis, or replacement of human judgment.
