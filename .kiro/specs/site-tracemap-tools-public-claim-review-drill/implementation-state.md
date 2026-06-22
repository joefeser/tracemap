# Site TraceMap Tools Public Claim Review Drill Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-public-claim-review-drill`

Implementation branch: future implementation branch not started.

Base: `origin/dev`

Target PR base: `dev`

Worktree: isolated spec worktree for this packet; local absolute path
intentionally omitted from checked-in spec notes.

Scope: spec-only public-site planning for a future claim review drill. Site
source, generated output, scanner code, reducer code, existing specs,
validation scripts, runtime telemetry, review automation, and AI/LLM behavior
remain out of scope for this phase.

## Current State

This packet defines a future concept-level public drill page or section. No
implementation has started. Required files:

- `requirements.md`
- `design.md`
- `tasks.md`
- `implementation-state.md`
- `review-packet.md`

Current readiness is `ready-for-implementation`. Medium or higher Kiro review
findings were patched or explicitly dispositioned, re-review was attempted
where feasible, and local spec validations passed.

`Status` remains `not-started` during this spec-only phase. A future
implementation phase may update it only when implementation work starts and
records the change here.

## Claim-Level Decision

Selected public claim level: `concept`.

Rationale: the future surface is a learning/checklist drill with authored
sample rows and an answer key. It does not publish a new evidence artifact,
scanner finding, reducer result, demo result, runtime observation, release
decision, operational safety conclusion, or shipped workflow.

## Route and Placement Guidance

Candidate placements:

- `/review-claim-checklist/drill/`: recommended default because the drill is a
  practice companion to the public claim checklist.
- `/claims/review-drill/`: allowed if a future claims route family exists.
- Section on `/review-claim-checklist/`: allowed if the drill remains compact.
- Section on `/proof-paths/tour/`: allowed if the exercise is framed as
  proof-path reading practice.

Final route or placement: not selected in this spec-only phase.

Future implementation must record selected placement, rejected alternatives,
route status for adjacent links, metadata decisions, and any substitutions or
deferrals here before marking implementation complete.

## Scope Decisions

- Create a future public claim review drill, not an automated grader.
- Require visible `Public claim level: concept`.
- Require visible `No public conclusion without evidence`.
- Require sections for drill setup, sample public-safe claims, evidence
  checklist, answer key, unsafe answer examples, stop conditions, and
  non-claims.
- Require seven drill rows: supported demo-level claim, concept-only claim,
  reduced-coverage claim, unsafe runtime claim, unsafe release claim,
  private-evidence-only claim, and missing-proof claim.
- Require every row to include claim text, expected claim level, proof path
  needed, evidence fields to check, limitation or non-claim, correct outcome,
  and next action.
- Keep row expected claim levels to `shipped`, `demo`, `concept`, and
  `hidden`.
- Treat the expected claim level `hidden` as a bounded claim-level state; it
  does not authorize public disclosure of hidden capability names, hidden
  validation details, or other private material.
- Keep answer-key outcomes to `repeat with proof`,
  `downgrade before repeating`, `owner follow-up needed`, `do not repeat`, and
  `internal only`.
- Distinguish the drill from `/review-claim-checklist/`,
  `/proof-paths/tour/`, `/proof-paths/faq/`, `/questions/objections/`,
  `/packets/examples/`, and `/language/change-risk/` when those routes exist.
- Forbid automated grading claims, runtime proof, release approval, release
  safety, operational safety, absence-of-impact proof, complete coverage,
  AI/LLM analysis, and replacement of human review.
- Forbid raw facts, SQLite, analyzer logs, source snippets, SQL, config
  values, secrets, local paths, remotes, generated scan directories, private
  sample names, command output, hidden validation details, and
  credential-like values.
- Avoid blame language.

## Review Commands

Planned spec review commands:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-public-claim-review-drill --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-public-claim-review-drill --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

If a requested model or tool is unavailable, record the exact error in this
file. If both requested models are unavailable but the review harness can run a
substitute model, run the best available review and record the substitution.

## Review Outcomes

Review date: 2026-06-22. Last review: final confirmation re-review with
`claude-sonnet-4.6` on 2026-06-22.

| Review | Model | Saved clean output | Coverage | Findings summary | Disposition |
| --- | --- | --- | --- | --- | --- |
| Initial spec review | `claude-opus-4.8` | `.tmp/kiro-reviews/site-tracemap-tools-public-claim-review-drill/2026-06-22T224426-698Z-spec-claude-opus-4.8.clean.md` | Reduced; Kiro reported denied write-tool access after reading all five files | 0 High, 4 Medium, 3 Low. Medium findings: Requirement 6 omitted required-section validation; Requirement 6 omitted expected-claim-level vocabulary validation; concept-only row had branching correct outcome; discrete rule ID or rule family, evidence tier, and coverage label were not individually enforced for repeat-with-proof rows. Low notes covered `Status` gate, hidden claim-level wording, and word-count tightness. | Patched all four Medium findings. Section-presence validation, expected-claim-level validation, repeat-with-proof discrete evidence validation, `Status` gate, and hidden claim-level clarification were added. Concept-only row wording was tightened again after Sonnet review. Word-count note deferred as implementation advisory. |
| Second spec review | `claude-sonnet-4.6` | `.tmp/kiro-reviews/site-tracemap-tools-public-claim-review-drill/2026-06-22T224804-085Z-spec-claude-sonnet-4.6.clean.md` | Reduced; Kiro reported denied write-tool access after reading all five files | 0 High, 3 Medium, 3 Low. Medium findings: concept-only row still read as conditional; Requirement 6 missing required-section validation; Requirement 6 missing expected-claim-level validation. Low notes: per-finding disposition detail, completed review checkbox state, and evidence-field wording alignment. | Patched concept-only row wording. The section-presence and expected-claim-level validation bullets were already present in `requirements.md` before this review result was inspected; verified by local `rg`/`sed`, so those two findings were dispositioned as stale reviewer misses. Completed review task checkboxes were updated. Evidence-field wording is substantively aligned and left unchanged. |
| Targeted re-review | `claude-opus-4.8` | `.tmp/kiro-reviews/site-tracemap-tools-public-claim-review-drill/2026-06-22T225050-680Z-re-review-claude-opus-4.8.clean.md` | Reduced; Kiro reported denied write-tool access after reading all five files and checking adjacent routes with allowed read tools | 0 High, 1 Medium, 3 Low. Medium finding: validation did not bind each required row scenario to its canonical allowed answer-key outcome set. Low notes: `/language/change-risk/` and `/claims/` were absent in the current site tree but covered by absent-route clauses; 1800-word ceiling may be tight; review checkbox wording should stay aligned with completed re-review state. | Patched Medium finding by adding scenario-to-allowed-outcome validation in requirements, design, and future validation tasks. Low absent-route note is already handled by required implementation-state route-status recording. Word-count note deferred as implementation advisory. |
| Final confirmation re-review | `claude-sonnet-4.6` | `.tmp/kiro-reviews/site-tracemap-tools-public-claim-review-drill/2026-06-22T225608-449Z-re-review-claude-sonnet-4.6.clean.md` | Reduced; Kiro reported denied write-tool access after reading all five files | 0 High, 2 Medium, 3 Low. Medium findings were bookkeeping only: stale review date and checked patch/re-review task needing a note that final confirmation remained pending at review time. Low notes: review-date scalar ambiguity, word-count deferral rationale, and conditional unavailable-model task not applicable because both models ran. Content criteria were confirmed satisfied, including required metadata, implementation-free scope, candidate placements, adjacent-surface distinctions, required sections, row fields, discrete evidence fields, scenario outcome mapping, forbidden claims, private/raw material, and no-blame coverage. | Patched stale review date, added task note for completed reduced-coverage re-reviews, recorded word-count and unavailable-model notes here, and prepared readiness upgrade after validation. |

Medium or higher findings requiring patches before readiness upgrade: none
remaining after the final confirmation re-review patch pass.

Readiness upgraded to `ready-for-implementation`: 2026-06-22, after Medium or
higher review findings were patched or dispositioned and final spec validation
passed.

## Validation

Spec-phase validation planned:

- `git diff --check`
- `./scripts/check-private-paths.sh`

Future implementation validation must include:

- focused validator for required drill rows and row fields;
- focused validator for answer-key outcomes;
- focused validator for visible concept claim level and shared principle;
- focused validator for required links and recorded substitutions;
- focused validator for standalone route metadata, sitemap metadata, and
  discovery metadata if standalone;
- focused validator for forbidden automated-grading, runtime, release,
  safety, operational, absence-of-impact, complete-coverage, AI/LLM, and
  human-review-replacement claims;
- focused validator for private/raw material exposure;
- focused validator for no blame language;
- focused validator for rendered body word count between 500 and 1800 words;
- `npm test`, `npm run validate`, and `npm run build` from `site/`;
- desktop and mobile browser sanity checks after layout or interaction
  changes;
- `git diff --check`;
- `./scripts/check-private-paths.sh`.

Completed spec-phase validation on 2026-06-22:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.

## PR Loop Outcome

PR: not opened yet.

Required final command after ready PR creation and the requested wait:

```bash
agent-control pr-loop --repo joefeser/tracemap --pr <PR_NUMBER> --base dev --require-codex-review --quiet --json
```

Final PR-loop decision: pending.

## Oddities

- The drill intentionally overlaps the claim checklist and proof-path tour but
  must remain a practice surface with authored examples and an answer key.
- Unsafe examples may include forbidden terms only inside clearly rejected
  unsafe-example, boundary, limitation, or non-claim regions.
- The supported demo-level row can teach a demo-backed shape without upgrading
  the drill page itself above concept level.
- The 500 to 1800 rendered-word validation bound may be tight for seven rows
  with seven row fields. Future implementation should use compact field labels
  and concise row text, but must not trim required sections, row scenarios, or
  row fields to satisfy the word-count ceiling.
- The conditional dual-model-unavailable review task was not triggered because
  both requested models ran through `scripts/kiro-review.mjs`; each run had
  reduced coverage due to Kiro denied write-tool access, and that limitation
  is recorded in the review table.

## Follow-ups

- Run the requested Kiro spec reviews.
- Patch Medium or higher findings and rerun re-review where feasible.
- Move readiness to `ready-for-implementation` only after findings are handled.
- Open the PR to `dev`, wait 3 minutes, then run the required PR loop.
