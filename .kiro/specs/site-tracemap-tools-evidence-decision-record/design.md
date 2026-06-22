# Site TraceMap Tools Evidence Decision Record Design

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Design Purpose

This design defines the information architecture for a future public-safe
evidence decision record page or section. The future surface should help a
team preserve the line between evidence and human judgment:

1. name the decision question;
2. cite the proof path and evidence metadata;
3. keep the limitation and non-claim attached;
4. record the owner's bounded decision;
5. record the rejected interpretation;
6. assign the next follow-up owner;
7. preserve residual risk.

The design does not implement site code.

## Placement Options

Preferred candidate options:

- `/decisions/evidence-record/`: clearest standalone route if the site needs
  a durable address for decision-record guidance.
- `/review-room/decision-record/`: strong fit if implementation wants the
  record nested beneath the evidence review-room concept.
- Section on `/review-room/`: lowest route surface area when the decision
  record remains a meeting artifact rather than its own reference page.
- Section on `/packets/assembly/`: useful if implementation wants the record
  to appear after evidence packet ingredients are assembled.

Rejected-by-default options unless implementation-state records otherwise:

- Replacing `/review-room/`, because the review room is the agenda for
  inspecting known, partial, and missing evidence.
- Replacing `/packets/assembly/`, because packet assembly gathers ingredients
  before handoff while this record captures a human decision after review.
- Replacing `/review-claim-checklist/`, because the checklist answers whether
  a statement may be repeated while this record captures a bounded owner
  decision.
- Replacing `/manager-packet/`, because manager packet copy explains value and
  public proof surfaces rather than storing decision context.
- Replacing `/questions/objections/`, because objections route skeptical
  questions to evidence and owners rather than recording final owner choices.
- Replacing `/proof-paths/tour/`, because proof-path education is a
  prerequisite for using the record, not the record itself.
- Primary navigation placement without a recorded
  information-architecture decision, because this concept-level surface may
  belong in secondary discovery.

Future implementation must record the final placement and rejected
alternatives before changing site source. That record must include why each
unchosen candidate placement from this spec was rejected, plus any
replace-a-neighbor options considered during implementation.

## Page Model

Recommended sections:

1. Opening boundary: visible `Public claim level: concept`, visible
   `No public conclusion without evidence`, and a short statement that
   TraceMap provides evidence, not the decision.
2. Why record the decision: explain why evidence, limitation, owner judgment,
   rejected interpretation, follow-up, and residual risk must travel together.
3. Record template: show every required field with short field guidance.
4. Example safe record: use synthetic or public-demo placeholders and keep the
   human decision bounded.
5. Unsafe record examples: show how unsupported public copy drops evidence,
   upgrades a claim, exposes private material, or implies approval.
6. Stop conditions: list blockers that prevent public reuse.
7. Follow-up owners: map residual questions to public role categories.
8. Non-claims: centralize boundaries for autonomous decision-making, approval
   workflows, release safety, runtime proof, production proof,
   absence-of-impact proof, complete coverage, AI/LLM analysis, and
   replacement of human judgment or governance.

The record template may render as a table on wide viewports and as stacked
definition-list groups on narrow viewports. Field labels must remain
accessible to screen readers and stable enough for validation.

## Required Record Template

| Field | Guidance |
| --- | --- |
| decision question | The exact question being answered; do not expand it into release, runtime, production, safety, or governance claims. |
| decision owner | Public role category or placeholder responsible for the human decision. |
| public claim level | `concept` for this page and examples unless a future evidence-backed upgrade is recorded. |
| proof path | Public-safe route, summary, documentation, report-family summary, or private review location named without raw material. |
| rule ID/family | Specific rule ID when public-safe, or rule family plus limitation when the specific ID cannot be named. |
| evidence tier | One of `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, or `Tier4Unknown`. |
| coverage label | The coverage label transcribed from cited evidence; do not strengthen the wording. |
| commit SHA | Public-safe scan commit identifier when available, otherwise visible limitation. |
| extractor version | Public-safe extractor version when available, otherwise visible limitation. |
| limitation | Rule, coverage, data, publication, or analysis boundary that travels with the decision. |
| non-claim | What the decision must not imply. |
| validation evidence | Public-safe validation summary, review evidence, command family, test summary, or manual review evidence. |
| rejected interpretation | Stronger or tempting conclusion the owner did not make. |
| follow-up owner | Role category responsible for the next answer. |
| review date placeholder | Placeholder or example date for the record format. |
| residual risk | Unknown, reduced, private-only, runtime-only, owner-pending, or out-of-scope risk that remains. |

## Example Safe Record Shape

Use synthetic or public-demo values only. The example should look like this in
spirit, with implementation copy free to adjust wording:

- Decision question: Can this public summary say reviewers have static
  evidence for a dependency question?
- Decision owner: reviewer role placeholder.
- Public claim level: concept.
- Proof path: public-safe proof path or demo summary link.
- Rule ID/family: public-safe rule family with documented limitation.
- Evidence tier: `Tier2Structural`.
- Coverage label: reduced or public-demo coverage label as cited.
- Commit SHA: public-safe example commit value or unavailable limitation.
- Extractor version: public-safe example version or unavailable limitation.
- Limitation: static evidence only; reduced coverage remains visible.
- Non-claim: does not prove runtime behavior, release safety, production
  traffic, or absence of impact.
- Validation evidence: public-safe validation summary or manual review note.
- Rejected interpretation: do not say the dependency is safe, unsafe,
  production-proven, or approved for release.
- Follow-up owner: service owner or runtime observability owner role.
- Review date placeholder: `YYYY-MM-DD`.
- Residual risk: runtime behavior and release decision remain outside
  TraceMap evidence.

## Unsafe Record Patterns

Unsafe examples should be short, clearly labeled, and written so validation can
differentiate them from positive claims. Include at least these patterns:

- Missing proof path: the record repeats a decision but drops its evidence.
- Missing evidence tier or coverage label: the record hides reduced or unknown
  analysis strength.
- Release approval wording: the record says evidence approves, certifies, or
  validates a release.
- Runtime or production proof wording: the record treats static evidence as
  runtime behavior, production traffic, endpoint performance, or outage cause.
- Absence-of-impact wording: the record turns missing evidence into no-impact
  proof.
- Autonomous decision wording: the record says TraceMap decided, approved, or
  blocked.
- Raw/private leakage: the record publishes raw facts, SQLite, analyzer logs,
  snippets, SQL, config values, secrets, local paths, remotes, generated scan
  directories, private sample names, raw command output, hidden validation
  details, or credential-like values.
- Blame wording: the record frames a limitation or rejected interpretation as
  fault by a person, team, vendor, reviewer, or codebase.

Forbidden terms may appear inside explicitly bounded non-claim, limitation,
stop-condition, rejected-interpretation, residual-risk, or unsafe-example
contexts when the sentence says TraceMap does not make that claim.

## Stop Conditions

The future page or section should list these stop conditions:

- Missing proof path.
- Missing rule ID/family.
- Missing evidence tier.
- Missing coverage label.
- Missing limitation or non-claim.
- Missing decision owner or follow-up owner.
- Missing validation evidence.
- Private-only support without public-safe summary.
- Raw artifact or private material leakage.
- Unsupported autonomous decision, approval workflow, release safety, runtime,
  production, absence-of-impact, complete-coverage, or AI/LLM wording.
- Hidden validation details or raw command output.
- Blame language.

Stop conditions block public reuse until corrected, downgraded, or explicitly
labeled as unavailable, private-only, reduced, pending, or outside TraceMap's
static evidence boundary.

## Follow-Up Owner Guidance

Use public role categories, not private people or teams:

- `service owner`: owns service behavior interpretation and code-level
  follow-up.
- `runtime observability owner`: owns logs, traces, metrics, dashboards,
  production traffic, endpoint performance, and runtime evidence.
- `release owner`: owns release gates, deployment policy, and final release
  decisions.
- `test owner`: owns test evidence, reproduction, regression coverage, and
  verification strategy.
- `reviewer`: owns claim checking, proof-path review, and repeatability of
  public or internal statements.
- `TraceMap owner`: owns scanner/reducer evidence boundaries, rule
  documentation, public site copy, validation, and implementation gaps.
- `security owner` or `repository owner`: owns raw artifact sharing, secrets,
  private paths, remotes, and publication decisions.
- `manager`: owns prioritization and coordination after evidence and owner
  inputs are identified.

The page must not imply that TraceMap assigns accountability, service
ownership, incident command, release authority, staffing, priority, or
organizational decisions.

## Copy Rules

Use bounded wording:

- `record the owner decision`
- `after inspecting evidence`
- `cite the proof path`
- `keep the limitation visible`
- `name the rejected interpretation`
- `route runtime questions to the runtime observability owner`
- `record residual risk`
- `TraceMap provides evidence, not the decision`

Avoid unsupported conclusion wording as claims:

- `TraceMap decided`
- `approved`
- `certified`
- `safe to release`
- `production proven`
- `runtime proven`
- `complete coverage`
- `no impact`
- `not impacted`
- `AI-powered decision`
- `autonomous approval`
- `replaces governance`

Avoid blame wording:

- `bad owner`
- `failed team`
- `vendor fault`
- `broken codebase`
- `careless reviewer`

The future page should use calm, practical copy. Rejected interpretations are
evidence boundaries, not accusations.

## Validation Design

Focused validation should use structured HTML parsing where possible. It
should verify:

- visible claim label and shared principle;
- selected placement and rejected alternatives recorded in implementation
  state;
- all required record fields;
- all required sections;
- supporting route resolution or recorded route deferral;
- placement decision coverage for all four spec-defined candidate placements,
  with the selected placement rationale separated from the three unchosen
  rejected alternatives;
- standalone route metadata, sitemap metadata, and discovery metadata when
  standalone, using the site's existing closed vocabulary for discovery source
  type and hint category;
- stable in-page anchor and conservative host-page metadata when sectioned;
- exact TraceMap evidence tier vocabulary;
- owner values are public role categories or placeholders;
- word count bounds of 700 to 2500 rendered words unless amended, where the
  floor ensures all required sections and fields are substantively present and
  the ceiling keeps the surface a bounded record template;
- word count amendments recorded in implementation state, if any, include a
  rationale, keep the floor at or above 400 rendered words, and keep the
  ceiling at or below 2500 rendered words;
- forbidden autonomous decision, approval workflow, release-safety,
  operational-safety, runtime-proof, production-proof, endpoint-performance,
  outage-cause, absence-of-impact, complete-coverage, AI/LLM, embedding,
  vector database, prompt-classification, and replacement-of-human-judgment
  wording across rendered text, decoded HTML, raw HTML attributes, alt text,
  captions, link anchor text, link title attributes, metadata, fixtures,
  tests, sitemap output, discovery output, and generated pages;
- forbidden terms are allowed only inside bounded non-claim, limitation,
  stop-condition, rejected-interpretation, residual-risk, or unsafe-example
  contexts;
- allowed forbidden-term contexts are identified structurally, for example by
  stable section anchors, field-level containers, or validator-owned data
  attributes. The validator should not rely on broad text proximity alone, and
  tests should prove that unsupported positive claims still fail when they are
  placed in the wrong structural region or hidden inside a mislabeled wrapper;
- a reference marker convention is
  `data-tracemap-validation-context="<context>"` on the smallest relevant
  region, with context values such as `unsafe-example`, `non-claim`,
  `limitation`, `stop-condition`, `rejected-interpretation`, and
  `residual-risk`. Implementation may substitute equivalent anchors, classes,
  or data attributes only with a recorded rationale;
- the structural-boundary negative fixture should deliberately put a forbidden
  positive claim inside `data-tracemap-validation-context="unsafe-example"` but
  outside the route's allowed unsafe-example section or field container, then
  assert validation fails. A companion positive fixture may place the same
  wording inside the allowed unsafe-example region with explicit non-claim
  framing and assert validation passes;
- forbidden private/raw material and credential-like values are absent from
  public output except inside explicit non-shareable artifact category
  examples;
- example review date placeholders use a visible placeholder pattern such as
  `YYYY-MM-DD`, not real private review dates or internal cadence;
- example commit SHA and extractor version values use synthetic placeholders,
  redacted placeholders, or explicit public-demo provenance rather than real
  private scan identifiers;
- example validation evidence values use synthetic, public-safe, or explicit
  public-demo summaries and exclude raw command output, private CI/job
  identifiers, hidden validation details, and credential-like values;
- field labels count once in the record template; repeated field labels in the
  example safe record and unsafe examples are excluded from the rendered word
  count;
- unsafe examples are clearly labeled and cannot be mistaken for supported
  public claims;
- accessibility checks cover the responsive record template, including
  semantic table headers or definition-list labels and screen-reader-accessible
  field names;
- desktop and mobile browser sanity for the selected route or host page,
  including scroll-to-section sanity for section-only implementations.
- explicit route or section registration in the aggregate `npm run validate`
  workflow when existing validation patterns require it, with the registration
  pattern recorded in implementation state.

The implementation should wire focused validation into the existing aggregate
site validation command so `npm run validate` exercises the evidence decision
record.
