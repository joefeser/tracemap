# Site TraceMap Tools Evidence Handoff Template Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

These tasks are checked only after the corresponding spec, review, future site
implementation, or validation work is complete.

## Spec-Only Phase

- [x] Create the spec packet with `requirements.md`, `design.md`, `tasks.md`,
  `implementation-state.md`, and `review-packet.md`.
- [x] Keep all tracked changes inside
  `.kiro/specs/site-tracemap-tools-evidence-handoff-template/`.
- [x] Run Kiro spec review with `claude-opus-4.8` if that model is available,
  or record the exact unavailable-model error in `implementation-state.md` and
  treat the task as dispositioned; the `claude-sonnet-4.6` review task below
  is the required fallback.
- [x] Run Kiro spec review with `claude-sonnet-4.6` when available, or record
  the exact command and error in `implementation-state.md`.
- [x] Patch or explicitly disposition all Medium or higher spec-review
  findings.
- [x] Rerun Kiro re-review where feasible after Medium or higher findings are
  patched.
- [x] Move `Readiness` to `ready-for-implementation` only after Medium or
  higher findings are patched or dispositioned.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run focused text checks for the new spec files and record individual
  results in `implementation-state.md` before marking this task complete.
  Checks must cover at minimum:
  - `Status: not-started`, `Readiness: ready-for-implementation`, and
    `Public claim level: concept` present in all five spec files.
  - All 15 required field labels present in `requirements.md` Requirement 3
    and `design.md`.
  - All required sections present in `requirements.md` and `design.md`: when
    to use it, neighbor distinctions, template, filled synthetic example,
    unsafe example, handoff checklist, stop conditions, and non-claims.
  - All required stop conditions present in `requirements.md` and `design.md`.
  - All six neighbor distinctions present in `requirements.md` and
    `design.md`.
  - Forbidden raw/private material absent from the new spec files: absolute
    paths, realistic commit SHAs, named individuals, real organization names,
    command output, and credential-like values.
  - Forbidden-claim boundaries present only in negated or cautionary contexts:
    generated handoff, real org ownership, runtime proof, release approval,
    operational safety, complete coverage, AI/LLM analysis, and human-review
    replacement.
  - Synthetic labeling requirements present in `requirements.md` and
    `design.md`.
  - Word-count expectations documented in `requirements.md` and `design.md`:
    500/300-word minimums for standalone/embedded placement, 1600/900-word
    targets for standalone/embedded placement, and tightening-warning versus
    hard-failure behavior.
- [x] Update `implementation-state.md` with review commands, review findings,
  validation results, oddities, and follow-up items.

## Future Implementation Phase

Do not check any task in this section until `Readiness` has moved to
`ready-for-implementation` and the corresponding future implementation work is
verified complete.

- [x] Confirm this spec is `ready-for-implementation` before changing site
  source.
- [x] Inspect live neighboring routes before selecting final placement from
  `/handoff/template/`, `/team-evidence-handoff/template/`, a section on
  `/team-evidence-handoff/`, or a section on `/packets/assembly/`.
- [x] Record the selected placement, rejected alternatives, route gaps, and
  metadata consequences in `implementation-state.md`.
- [x] Add the concept-level page or section using existing static site layout,
  metadata, accessibility, and navigation patterns.
- [x] Include `Public claim level: concept` and
  `No public conclusion without evidence` on the rendered page or section.
- [x] Publish the required template fields: handoff question, audience, proof
  path, public claim level, rule ID/family, evidence tier, coverage label,
  public-safe path/span, commit SHA, extractor version, limitation, non-claim,
  validation evidence, owner to ask, and stop condition.
- [x] Use only the TraceMap evidence tier vocabulary: `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, and `Tier4Unknown`.
- [x] Keep coverage labels copied from the cited evidence surface or clearly
  labeled concept-only, demo-only, partial, reduced, gap, unknown,
  syntax-only, or another existing public-site label.
- [x] Add required sections for when to use it, neighbor distinctions,
  template, filled synthetic example, unsafe example, handoff checklist, stop
  conditions, and non-claims.
- [x] Label examples as synthetic or already-approved public-safe demo
  summaries.
- [x] Add stop conditions for missing proof path, private-only support, raw or
  private material, unknown or reduced coverage without label, unsupported
  runtime proof wording, unsupported release or safety wording, unsupported
  complete-coverage wording, AI or LLM analysis wording, no validation
  evidence, no owner to ask, and blame language.
- [x] Add explicit non-claims for generated handoff features, real org
  ownership, runtime proof, production traffic, endpoint performance, outage
  cause, release approval, release safety, operational safety, complete
  coverage, AI impact analysis, LLM analysis, autonomous review, and
  replacement of human review.
- [x] State that TraceMap does not replace human review, source review,
  ownership decisions, telemetry, logs, traces, APM, tests, release controls,
  incident response, service-owner judgment, database-owner judgment, security
  review, compliance review, manager judgment, or human judgment.
- [x] Ensure examples and metadata do not include raw facts, SQLite content,
  analyzer logs, source snippets, SQL, config values, secrets, local paths,
  remotes, generated scan directories, private sample names, command output,
  hidden validation details, credential-like values, connection strings,
  tokens, keys, private repository identifiers, named individuals, or personal
  owner names.
- [x] Link to `/team-evidence-handoff/`, `/incident-evidence-handoff/`,
  `/packets/assembly/`, `/reviewer-quickstart/`, `/owners/follow-up/`,
  `/decisions/evidence-record/`, `/proof-paths/`, `/limitations/`, and
  `/validation/` when those routes exist.
- [x] Record substitutions, omissions, or deferred links for adjacent routes
  that do not exist at implementation time.
- [x] Add standalone route metadata, discovery metadata, and sitemap metadata
  if implemented as a standalone route.
- [x] Record section anchor and separate-sitemap notes if implemented as an
  embedded section.
- [x] Add focused validation for required field labels, required sections,
  required links, metadata, discovery metadata, sitemap metadata if
  standalone, forbidden claims, private/raw material, synthetic labeling, and
  word-count bounds.
- [x] Validate rendered text, decoded HTML, raw HTML attributes, and metadata
  for forbidden generated handoff, real ownership, runtime, production,
  release-safety, operational-safety, AI/LLM, autonomous-review,
  complete-coverage, human-review replacement, and blame claims.
- [x] Validate rendered text, decoded HTML, raw HTML attributes, and metadata
  for forbidden raw or private material.
- [x] Run `git diff --check` after implementation.
- [x] Run `./scripts/check-private-paths.sh` after implementation.
- [x] Run `npm test` from `site/` after implementation.
- [x] Run `npm run validate` from `site/` after implementation.
- [x] Run `npm run build` from `site/` after implementation.
- [x] Run desktop and mobile browser sanity checks for layout, link usability,
  text wrapping, overflow, heading order, descriptive link text, and basic
  accessibility.
- [x] Update `implementation-state.md` with route decisions, validation
  results, review findings, claim-boundary decisions, oddities, unresolved
  gaps, and follow-up items.
