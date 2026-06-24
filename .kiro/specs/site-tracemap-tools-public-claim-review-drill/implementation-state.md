# Site TraceMap Tools Public Claim Review Drill Implementation State

Status: implemented
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-public-claim-review-drill`

Implementation branch: `codex/impl-site-public-claim-review-drill`

Base: `origin/dev`

Target PR base: `dev`

Worktree: isolated implementation worktree; local absolute path intentionally
omitted from checked-in spec notes.

Scope: public-site implementation for the claim review drill. Changes are
limited to `site/src/`, site validation/test scripts, and this spec-local
bookkeeping. Generated output, scanner code, reducer code, runtime telemetry,
review automation behavior, and AI/LLM behavior remain out of scope.

## Current State

This packet now has a concept-level public drill implemented as a standalone
route at `/review-claim-checklist/drill/`. Required spec files remain:

- `requirements.md`
- `design.md`
- `tasks.md`
- `implementation-state.md`
- `review-packet.md`

Current readiness remains `ready-for-implementation`; implementation work is
complete locally pending PR review-loop outcome.

## Claim-Level Decision

Selected public claim level: `concept`.

Rationale: the implemented surface is a learning/checklist drill with authored
sample rows and an answer key. It does not publish a new evidence artifact,
scanner finding, reducer result, demo result, runtime observation, release
decision, operational safety conclusion, or shipped workflow.

## Route and Placement Decision

Final route: `/review-claim-checklist/drill/`.

Selected because the drill is a practice companion to the canonical
`/review-claim-checklist/` ritual. It needs seven rows plus an answer key,
which would crowd the checklist if embedded as a section.

Rejected alternatives:

- `/claims/review-drill/`: rejected because no broader public claims route
  family exists in the current site.
- Section on `/review-claim-checklist/`: rejected because the checklist should
  remain the canonical real-claim decision ritual, not a long practice set.
- Section on `/proof-paths/tour/`: rejected because this implementation asks
  whether sample claims can be repeated; the tour remains a proof-path reading
  flow.

Navigation decision: the route is not in primary navigation. It is discoverable
through the checklist, proof-path tour, proof-path FAQ, objections guide,
packet examples, language guide, sitemap, and discovery metadata.

Adjacent route status:

- `/review-claim-checklist/`: present; linked as the canonical real-claim
  checklist.
- `/proof-paths/tour/`: present; linked as the guided proof-path reading flow.
- `/proof-paths/faq/`: present; linked as proof-path question-and-answer
  context.
- `/questions/objections/`: present; linked as stakeholder concern handling.
- `/packets/examples/`: present; linked as packet reading examples.
- `/language/change-risk/`: present; linked as wording guidance.

## Scope Decisions

- Created a public claim review drill, not an automated grader.
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

## Spec Validation

Completed spec-phase validation on 2026-06-22:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.

## Implementation Validation

Completed implementation validation:

- `npm test` from `site/`: passed.
- `npm run validate` from `site/`: passed; validated 65 HTML files, 2219
  internal references, 64 sitemap URLs, 1 legacy story safety target, and 13
  legacy modernization evidence-map rows.
- `npm run build` from `site/`: passed.
- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- Desktop browser sanity: passed on `/review-claim-checklist/drill/`; title,
  seven drill rows, seven answer-key rows, and no obvious overflow were
  verified. A desktop viewport screenshot was captured during local QA.
- Mobile browser sanity: passed with a 390 by 844 viewport via DOM layout
  checks; seven drill rows, seven answer-key rows, no unsanctioned overflow,
  and an auto-scrolling table wrapper were verified. Mobile screenshot capture
  timed out twice in the browser tool, so the mobile visual check used DOM
  dimensions and overflow state instead of a screenshot.

Focused validation added:

- required route, sitemap, discovery, and metadata fields;
- visible `Public claim level: concept` and `No public conclusion without
  evidence`;
- required sections;
- all seven drill row scenarios;
- row field completeness;
- discrete evidence field enumeration;
- expected claim-level vocabulary;
- answer-key outcome vocabulary and scenario-to-outcome mapping;
- adjacent route links and inbound links;
- forbidden raw/private proof links and private text;
- forbidden positioning and blame language;
- rendered word count between 500 and 1800 words.

## PR Loop Outcome

PR: pending creation.

Required final command after ready PR creation and the requested wait:

```bash
agent-control pr-loop --repo joefeser/tracemap --pr <PR_NUMBER> --base dev --require-codex-review --quiet --json
```

Authoritative PR-loop outcome must be recorded from the latest command output
after the implementation branch is pushed.

## Oddities

- The drill intentionally overlaps the claim checklist and proof-path tour but
  must remain a practice surface with authored examples and an answer key.
- Unsafe examples may include forbidden terms only inside clearly rejected
  unsafe-example, boundary, limitation, or non-claim regions.
- The supported demo-level row can teach a demo-backed shape without upgrading
  the drill page itself above concept level.
- The implemented page is 1329 rendered words, within the required 500 to 1800
  word range while preserving seven rows and seven row fields.
- Mobile screenshot capture timed out in the browser tool, but mobile DOM
  layout checks passed after setting the viewport to 390 by 844.
- The conditional dual-model-unavailable review task was not triggered because
  both requested models ran through `scripts/kiro-review.mjs`; each run had
  reduced coverage due to Kiro denied write-tool access, and that limitation
  is recorded in the review table.

## Follow-ups

- Create a ready PR to `dev`.
- Wait 3 minutes, then run the required PR loop command.
- Patch only if the PR-loop JSON grants authority; otherwise report the typed
  stop state and residual risk.
