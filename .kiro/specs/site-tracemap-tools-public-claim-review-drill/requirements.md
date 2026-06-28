# Site TraceMap Tools Public Claim Review Drill Requirements

Status: implemented
Readiness: implemented
Public claim level: concept

## Summary

Define a future public-safe drill page or section for `tracemap.tools` that
lets readers practice checking whether a public claim is backed by a proof
path, limitation, evidence tier, coverage label, and non-claim. The surface is
a learning exercise and checklist companion, not an automated grader, scoring
engine, release gate, runtime proof, or claim-approval system.

This is a spec-only public-site phase. It does not implement site code,
scanner behavior, reducer behavior, validation scripts, generated output,
runtime telemetry, review automation, AI/LLM behavior, or public copy changes.

The future drill should help reviewers, managers, agents, and engineers slow
down before repeating a claim. It teaches a repeatable evidence check using
safe sample rows, expected outcomes, and an answer key.

## Shared Site Principle

No public conclusion without evidence.

## Claim Level Rationale

Use `Public claim level: concept` because the drill explains a review habit and
uses authored sample rows. It does not publish a new TraceMap finding, scanner
capability, reducer result, proof artifact, demo result, shipped workflow,
runtime observation, or release decision.

Do not upgrade the page to `demo` merely because one sample row describes a
demo-backed claim shape. A future amendment may cite demo-backed surfaces for
a row, but the drill itself remains concept-level unless separately reviewed.

## Claim Boundaries

- The future page or section shall visibly say
  `Public claim level: concept`.
- The future page or section shall visibly say
  `No public conclusion without evidence`.
- The drill may teach claim review, evidence-field checking, answer-key
  outcomes, stop conditions, safe next actions, and non-claims.
- The drill must not claim automated grading, automated approval, runtime
  proof, production traffic proof, endpoint performance proof, release
  approval, release safety, operational safety, absence-of-impact proof,
  complete coverage, AI/LLM analysis, or replacement of human review.
- The drill must not publish raw facts, raw SQLite files, analyzer logs, source
  snippets, raw SQL, config values, secrets, local paths, raw remotes,
  generated scan directories, private sample names, command output, hidden
  validation details, or credential-like values.
- The drill must avoid blame language. Missing or private-only evidence is a
  gap, limitation, downgrade, internal-only state, stop condition, or owner
  follow-up, not a person, team, service, or reviewer failure.
- Public copy must keep TraceMap bounded to deterministic static evidence,
  rule IDs or rule families, evidence tiers, coverage labels, limitations,
  analysis gaps, public-safe proof paths, and next-owner handoffs.

## Requirements

### Requirement 1: Choose route or section placement conservatively

The future implementation shall choose a public route or section placement for
the drill without implying a new claim-verification product surface.

Acceptance criteria:

- The implementation evaluates all candidate placements:
  `/claims/review-drill/`, `/review-claim-checklist/drill/`, section on
  `/review-claim-checklist/`, and section on `/proof-paths/tour/`.
- `/review-claim-checklist/drill/` is the non-binding design recommendation
  because the drill is a practice companion to the checklist rather than the
  canonical checklist itself.
- `/claims/review-drill/` remains allowed if the implementation creates a
  broader claims route family.
- A section on `/review-claim-checklist/` remains allowed if the drill is short
  enough to sit beside the checklist without crowding the canonical ritual.
- A section on `/proof-paths/tour/` remains allowed if the implementation
  treats the drill as a proof-path reading exercise rather than a claim-review
  exercise.
- The implementation records the selected placement, rejected alternatives,
  and short reasons in this spec's `implementation-state.md`.
- The chosen page or section says `Public claim level: concept`.
- The chosen page or section states `No public conclusion without evidence`.
- If implemented as a standalone route, route metadata, sitemap metadata,
  canonical metadata, discovery metadata, and internal-link validation use
  concept-level wording.
- If implemented as a section, the implementation records how the drill's
  visible concept claim level is reconciled with the host route's metadata and
  gives the drill stable anchors unique within the host document.
- The page or section is not added to primary navigation unless a future
  information-architecture review records why a concept-level drill belongs
  there.

### Requirement 2: Distinguish the drill from adjacent public surfaces

The drill shall teach a short exercise without duplicating neighboring pages,
tables, tours, objections, examples, or language-risk guidance.

Acceptance criteria:

- The drill distinguishes itself from `/review-claim-checklist/` by offering
  practice rows and an answer key, while the checklist remains the canonical
  decision ritual for real claims.
- The drill distinguishes itself from `/proof-paths/tour/` by asking readers to
  decide whether several sample claims can be repeated, while the tour remains
  a guided reading flow for proof paths.
- The drill distinguishes itself from `/proof-paths/faq/` by using exercises
  and outcomes, while the FAQ remains an explanatory question-and-answer
  surface.
- The drill distinguishes itself from `/questions/objections/` by teaching
  evidence review, while objections remain stakeholder concern handling.
- The drill distinguishes itself from `/packets/examples/` by using authored
  public-safe sample rows, while packet examples remain concrete packet
  reading examples when available.
- The drill distinguishes itself from `/language/change-risk/` by reviewing
  public claim support, while language-risk guidance remains wording and risk
  vocabulary guidance.
- For any listed adjacent route that is absent, renamed, or replaced at
  implementation time, the implementation records the route status and uses
  the closest public-safe substitute or defers the link.
- The drill links to adjacent routes where they exist rather than copying their
  full checklists, FAQs, packet examples, proof-path tours, objection
  matrices, or language guidance.

### Requirement 3: Publish the required drill structure

The future page shall present a short public-safe exercise with required
sections and row fields.

Acceptance criteria:

- Include these required sections: drill setup, sample public-safe claims,
  evidence checklist, answer key, unsafe answer examples, stop conditions, and
  non-claims.
- The drill setup states that the reader is practicing evidence review, not
  using an automated grader.
- The evidence checklist asks the reader to inspect proof path, rule ID or
  rule family, evidence tier, coverage label, limitation, non-claim, source
  context, and public/private status.
- The answer key uses bounded outcomes: `repeat with proof`,
  `downgrade before repeating`, `owner follow-up needed`, `do not repeat`, and
  `internal only`.
- Every drill row includes claim text, expected claim level, proof path
  needed, evidence fields to check, limitation or non-claim, correct outcome,
  and next action.
- The `evidence fields to check` row field enumerates the discrete evidence
  the reader inspects: proof path, rule ID or rule family, evidence tier,
  coverage label, limitation, non-claim, source context, and public/private
  status.
- Drill row expected claim levels use only `shipped`, `demo`, `concept`, or
  `hidden`.
- The expected claim level value `hidden` is a bounded claim-level state and
  does not permit public disclosure of hidden capability names, hidden
  validation details, or other private material.
- The answer key does not turn a correct answer into proof that the underlying
  software behavior is shipped, demo-backed, safe, approved, complete, or
  impact-free.
- Unsafe answer examples are visibly rejected patterns and do not become live
  TraceMap claims.
- Non-claims apply both page-wide and row-specific when a sample row could
  otherwise imply runtime, release, safety, operational, AI/LLM, or complete
  coverage conclusions.

### Requirement 4: Include all required drill rows

The future page shall include seven sample rows that exercise the important
claim-review boundaries.

Acceptance criteria:

- Include a supported demo-level claim row.
- Include a concept-only claim row.
- Include a reduced-coverage claim row.
- Include an unsafe runtime claim row.
- Include an unsafe release claim row.
- Include a private-evidence-only claim row.
- Include a missing-proof claim row.
- Each row uses synthetic, authored, or already public-safe phrasing and does
  not reveal real internal claims, private sample names, hidden capability
  names, customer context, internal route names, in-flight sequencing, exact
  counts, cadence, real reviewer names, real owner names, or real review
  dates.
- The supported demo-level claim row requires public-safe demo proof, rule ID
  or rule family, evidence tier, coverage label, limitation, and non-claim
  before the answer key allows `repeat with proof`.
- The concept-only claim row makes clear that future-facing or explanatory
  guidance remains `concept` even when the review exercise is useful. The
  sample row uses draft wording that implies shipped or demo behavior, so its
  canonical answer-key outcome is `downgrade before repeating`. A teaching
  note may explain that concept-bounded wording could only be repeated with
  proof of the concept-level source, but that note is not this row's correct
  outcome and does not introduce a second outcome path for the row.
- The reduced-coverage claim row keeps the reduced, partial, unknown,
  unavailable, or gap-labeled coverage visible and forces downgrade or owner
  follow-up unless a public-safe proof path supports the narrower statement.
- The unsafe runtime claim row rejects runtime behavior, production traffic,
  endpoint performance, and outage-cause conclusions.
- The unsafe release claim row rejects release approval, release safety,
  operational safety, and replacement of release controls.
- The private-evidence-only claim row routes to `internal only` or
  `owner follow-up needed` until a public-safe summary exists.
- The missing-proof claim row routes to `do not repeat` or
  `owner follow-up needed` and states that confidence, seniority, repetition,
  or pressure cannot fill the proof gap.

### Requirement 5: Preserve proof-path and evidence vocabulary

The future drill shall keep every answer attached to TraceMap's deterministic
static evidence vocabulary.

Acceptance criteria:

- Proof path entries link only to public-safe pages, public-safe generated
  summaries, documentation, rule catalog pages, reports, sanctioned demo
  artifacts, proof-path entries, checklist pages, or review-packet surfaces.
- Proof path entries do not link directly to raw facts, raw SQLite files,
  analyzer logs, raw source snippets, raw SQL, config values, secrets, local
  absolute paths, raw remotes, generated scan directories, private sample
  names, command output, hidden validation details, or credential-like values.
- Rule IDs are required when public-safe and specific; otherwise the row uses a
  rule-family label plus a limitation explaining why a specific rule ID is not
  public-safe or not available.
- Evidence tiers use only `Tier1Semantic`, `Tier2Structural`,
  `Tier3SyntaxOrTextual`, and `Tier4Unknown`.
- Coverage labels are copied from the cited public-safe artifact or summary
  and are not silently normalized into stronger wording.
- Source context is public-safe and may describe `main`, checked-in public
  demo evidence, public documentation, future-only, hidden, or local-only
  status without exposing private paths or remotes.
- A row with no proof path, no rule ID or rule family, no evidence tier, no
  coverage label, or private-only evidence cannot be upgraded by the drill.

### Requirement 6: Define implementation validation expectations

The future implementation shall add focused validation so the drill remains
bounded and public-safe.

Acceptance criteria:

- Validation checks that all seven required drill rows are present.
- Validation checks that all required sections are present: drill setup,
  sample public-safe claims, evidence checklist, answer key, unsafe answer
  examples, stop conditions, and non-claims.
- Validation checks that every row includes claim text, expected claim level,
  proof path needed, evidence fields to check, limitation or non-claim,
  correct outcome, and next action.
- Validation checks that drill-row expected claim levels use only `shipped`,
  `demo`, `concept`, and `hidden`.
- Validation checks answer-key outcomes and allows only
  `repeat with proof`, `downgrade before repeating`,
  `owner follow-up needed`, `do not repeat`, and `internal only`.
- Validation checks that any row whose correct outcome is
  `repeat with proof` exposes a discrete rule ID or rule family, evidence tier,
  and coverage label within its evidence fields rather than a single
  unstructured value.
- Validation checks that each required row's correct outcome falls within that
  scenario's allowed answer-key outcomes: the supported demo-level row resolves
  to `repeat with proof`; the concept-only row resolves to
  `downgrade before repeating`; the reduced-coverage row resolves to
  `downgrade before repeating` or `owner follow-up needed`; the unsafe runtime
  row and unsafe release row resolve to `do not repeat`; the
  private-evidence-only row resolves to `internal only` or
  `owner follow-up needed`; and the missing-proof row resolves to
  `do not repeat` or `owner follow-up needed`. This prevents an answer-key
  regression from teaching an unsafe or incorrect outcome while still passing
  the outcome-vocabulary membership check.
- Validation checks visible `Public claim level: concept` and
  `No public conclusion without evidence`.
- Validation checks required links to adjacent surfaces that exist at
  implementation time and records substitutions or deferrals for absent
  surfaces in `implementation-state.md`.
- If standalone, validation checks route metadata, canonical metadata,
  sitemap metadata, discovery metadata, and concept-level
  `publicClaimLevel`.
- Validation checks forbidden automated-grading, runtime-proof, release-safety,
  operational-safety, absence-of-impact, complete-coverage, AI/LLM, and
  replacement-of-human-review claims across rendered text, decoded HTML,
  attributes, metadata, discovery surfaces, sitemap output, tests, and
  fixtures. Rejected terms may appear only inside explicit unsafe-example,
  limitation, boundary, or non-claim regions.
- Validation checks private/raw material exposure across raw HTML, decoded
  HTML attributes, metadata, rendered body text, discovery surfaces, sitemap
  output, tests, fixtures, and bot-oriented discovery surfaces.
- Validation checks no blame language in visible copy, metadata, examples,
  validation messages, and review-packet references using a recorded advisory
  phrase set such as `fault`, `to blame`, `negligent`, `careless`, or
  attributing missing, reduced, or conflicting evidence to a named person,
  team, service, customer, or reviewer. The advisory phrase set should avoid
  common technical status terms that appear in ordinary validation output.
- Validation checks rendered body word count after stripping navigation,
  footer, metadata, and global chrome. The target range is 500 to 1800 words;
  required sections and row fields remain mandatory even when trimming copy.
- Validation checks desktop and mobile browser sanity after layout or
  interaction changes.
- The implementation runs `npm test`, `npm run validate`, and
  `npm run build` from `site/` after site source is added, plus
  `git diff --check` and `./scripts/check-private-paths.sh` from the repo
  root.
