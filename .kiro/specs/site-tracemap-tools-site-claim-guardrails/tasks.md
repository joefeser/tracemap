# Site TraceMap Tools Site Claim Guardrails Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

These tasks track a spec-only public-site phase. Keep future implementation
tasks unchecked in this packet. Do not edit `site/src`, generated output,
scanner code, reducer code, validation scripts, or existing specs in this
phase.

`Status` remains `not-started` for this spec-only packet. A future
implementation may move it only when implementation work actually starts and
the change is recorded in `implementation-state.md`.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8` or record the exact
  unavailable-tool/model error in `implementation-state.md`. Reduced-coverage
  review ran; findings patched or dispositioned in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable-tool/model error in `implementation-state.md`. Reduced-coverage
  review ran; findings patched or dispositioned in `implementation-state.md`.
- [ ] If both `claude-opus-4.8` and `claude-sonnet-4.6` are unavailable,
  record the exact errors, run the review with the best available model, and
  record the substitution and rationale in `implementation-state.md`. Do not
  skip review entirely unless the review harness itself is unavailable. N/A:
  both requested reviews completed with reduced coverage.
- [x] Patch Medium or higher spec findings and rerun re-review where feasible.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Move `Readiness` to `ready-for-implementation` only after Medium or
  higher findings are patched or explicitly dispositioned in
  `implementation-state.md`.

## Future Implementation Tasks

- [ ] Confirm or update this spec-local `implementation-state.md` with branch,
  placement choice, scope decisions, review results, validation plan, and
  initial implementation status before changing site code.
- [ ] Choose the final route or placement:
  `/site-claim-guardrails/`, `/docs/site-claim-guardrails/`, section on
  `/review-claim-checklist/`, contributor-only docs page linked from
  `/docs/`, or a recorded equivalent that preserves the required boundaries.
- [ ] Record the selected placement and rejected alternatives in
  `implementation-state.md`.
- [ ] Explain why the selected placement is a guardrails surface rather than
  the canonical claim checklist, proof source catalog, roadmap, limitations
  page, objections page, or change-risk language guide.
- [ ] Add the concept-level public guardrails page or section, or a hidden
  contributor-only docs page if that placement is selected, using existing
  static-site layout, accessibility, metadata, and validation patterns.
- [ ] Include visible `Public claim level: concept` for public-facing output.
- [ ] Include visible `No public conclusion without evidence`.
- [ ] Keep the page or section out of primary navigation unless
  implementation-state records a matching information-architecture decision.
- [ ] If implemented as a standalone public route, add title, description,
  canonical URL, Open Graph metadata, sitemap metadata, and discovery metadata
  with concept-level wording.
- [ ] If implemented as hidden contributor-only docs, keep it out of public
  sitemap and discovery metadata and record the hidden rationale.
- [ ] If implemented as a section, record host metadata reconciliation and add
  stable unique anchors for every required section.
- [ ] Include required sections: public claim levels, proof-path requirements,
  allowed evidence references, forbidden raw material, non-claim patterns,
  downgrade and hidden rules, validation expectations, and review handoff.
- [ ] Include all required guardrail rows: shipped, demo, concept, hidden, raw
  artifact reference, dev-only feature, reduced coverage, runtime/release
  wording, AI/LLM wording, and private-only support.
- [ ] For every row, include condition, allowed public wording or action,
  required proof path, downgrade or hidden trigger, forbidden implication, and
  review handoff.
- [ ] Keep public claim level values limited to `shipped`, `demo`, `concept`,
  and `hidden`.
- [ ] Ensure `shipped` requires main-true or source-document proof plus
  limitation and does not imply runtime, release, safety, complete coverage,
  or replacement of human review.
- [ ] Ensure `demo` requires checked-in public demo proof or public-safe demo
  summary plus limitation and does not imply shipped or production behavior.
- [ ] Ensure `concept` stays future-facing, explanatory, dev-only, process, or
  not-yet-backed and cannot be presented as shipped or demo-backed.
- [ ] Ensure `hidden` means omitted or abstracted and does not reveal hidden
  capability names, private sample identities, internal route names, counts,
  cadence, sequencing, in-flight status, or private proof details.
- [ ] Ensure raw artifact references are replaced by public-safe summaries or
  owner follow-up.
- [ ] Ensure dev-only features are treated as concept or hidden and do not
  imply public availability.
- [ ] Ensure reduced coverage remains visible and forces downgrade or owner
  follow-up unless the claim is narrowed.
- [ ] Ensure runtime/release wording is removed or rewritten.
- [ ] Ensure AI/LLM wording is removed or rewritten for core scanner/reducer
  behavior.
- [ ] Ensure private-only support stays internal, hidden, or owner follow-up
  until a public-safe summary exists.
- [ ] Link to `/review-claim-checklist/` when it exists and explain that it
  remains the canonical real-claim decision ritual.
- [ ] Link to `/proof-source-catalog/` when it exists and explain that it
  remains route-to-source mapping.
- [ ] Link to `/roadmap/` when it exists and explain that it remains claim
  gate and maturity context without creating timing promises.
- [ ] Link to `/limitations/` when it exists and explain that it remains the
  broader limitation reference.
- [ ] Link to `/questions/objections/` when it exists and explain that it
  remains stakeholder objection context.
- [ ] Link to `/language/change-risk/` when it exists and explain that it
  remains wording-pattern guidance.
- [ ] For any adjacent route that is absent, renamed, or replaced, record the
  route status and substitution, deferral, or omission in
  `implementation-state.md`.
- [ ] Do not publish raw facts, SQLite, analyzer logs, source snippets, SQL,
  config values, secrets, local paths, remotes, generated scan directories,
  private sample names, command output, hidden validation details, or
  credential-like values.
- [ ] Do not reveal internal reviewer identities, private sample identities,
  hidden capability names, in-flight sequencing, confidential customer
  context, private repository details, or local-only evidence.
- [ ] Avoid blame language in page copy, metadata, examples, validation
  messages, and review-packet references.
- [ ] Add focused validation for visible concept claim level or recorded
  hidden contributor-only rationale.
- [ ] Add focused validation for visible shared principle.
- [ ] Add focused validation for required sections and stable anchors.
- [ ] Add focused validation for required guardrail rows and row fields.
- [ ] Add focused validation for adjacent-route link resolution and recorded
  substitutions.
- [ ] Add focused validation for standalone metadata, sitemap metadata, and
  discovery `publicClaimLevel: concept` if a standalone public route is chosen.
- [ ] Add focused validation that hidden contributor-only placement is absent
  from public sitemap and discovery metadata if that placement is chosen.
- [ ] Add focused validation for forbidden product capability, runtime proof,
  release approval, release safety, operational safety, complete coverage,
  AI/LLM analysis, and replacement-of-human-review claims.
- [ ] Add focused validation for forbidden private or raw material across
  rendered text, decoded HTML attributes, raw HTML, metadata, sitemap or
  discovery output, fixtures, tests, validation messages, and review-packet
  references.
- [ ] Add focused validation for no blame language using a recorded advisory
  phrase set.
- [ ] Add focused validation that rendered body word count stays between 700
  and 2200 words while required sections and rows remain present.
- [ ] Wire focused validation into the existing aggregate site validation
  workflow where the existing pattern supports it.
- [ ] Run `npm test` from `site/` after site source is added.
- [ ] Run `npm run validate` from `site/` after site source is added.
- [ ] Run `npm run build` from `site/` after site source is added.
- [ ] Run desktop and mobile browser sanity checks if route, layout, or
  interaction changes are made.
- [ ] Run `git diff --check`.
- [ ] Run `./scripts/check-private-paths.sh`.
- [ ] Update `implementation-state.md` with route decisions, substitutions,
  validation results, review findings, claim-boundary decisions, oddities, and
  follow-up items.
