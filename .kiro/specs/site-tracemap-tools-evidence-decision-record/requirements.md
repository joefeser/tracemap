# Site TraceMap Tools Evidence Decision Record Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Define a future public-safe page or section for an evidence decision record on
`tracemap.tools`. The surface should show how a team records a human decision
after inspecting TraceMap evidence: the question, proof path, evidence tier,
limitation, owner decision, follow-up, and non-claims.

This is a spec-only public site phase. It does not implement site source,
scanner behavior, reducer behavior, generated artifacts, validation scripts,
decision automation, approval workflow, governance workflow, runtime telemetry,
AI or LLM analysis, embeddings, vector databases, prompt classification, or
public copy changes.

TraceMap provides deterministic static evidence. It does not make the owner
decision.

## Shared Site Principle

No public conclusion without evidence.

Future implementation must include visible `Public claim level: concept` and
visible `No public conclusion without evidence`.

## Claim Level Rationale

The future surface starts at `Public claim level: concept` because it is a
recording pattern for human decisions made after evidence inspection. It does
not publish new demo proof, prove a scanner or reducer capability, approve a
release, prove runtime behavior, prove production behavior, or validate an
organization's governance process.

Do not upgrade the page or section to `demo` merely because supporting links
point to demo-backed routes. A future claim-level upgrade requires a separate
evidence-backed decision recorded in this spec's `implementation-state.md`.

## Candidate Placement

Candidate placements for future implementation are:

- `/decisions/evidence-record/`
- `/review-room/decision-record/`
- A section on `/review-room/`
- A section on `/packets/assembly/`

Future implementation must choose the final placement and record rejected
alternatives in `implementation-state.md` before changing site source.

## Relationship To Existing Public Surfaces

The evidence decision record is a decision-after-evidence record. It must
remain distinct from these neighboring surfaces when present:

- `/review-room/`: meeting agenda for known, partial, and missing evidence.
  The decision record captures what the owner decided after the review, not
  the meeting agenda itself.
- `/packets/assembly/`: pre-handoff ingredient checklist. The decision record
  captures one owner decision and residual risk after a proof path is
  inspected, not the whole assembly workflow.
- `/review-claim-checklist/`: repeatability ritual for a sentence or claim.
  The decision record records an owner decision and its limits, not a claim
  approval verdict.
- `/manager-packet/`: manager-facing explanation of TraceMap value and proof
  surfaces. The decision record is a compact evidence-to-decision artifact, not
  a manager summary.
- `/questions/objections/`: skeptical question handling. The decision record
  logs the resolved question, rejected interpretation, follow-up owner, and
  residual risk after evidence review.
- `/proof-paths/tour/`: education for following proof paths. The decision
  record cites a proof path but does not teach the whole proof-path process.

If any route has moved or is unavailable at implementation time, the
implementation must select the closest live public-safe equivalent or defer
the link, then record the decision and rationale in `implementation-state.md`.
Dead links are not acceptable.

## Claim Boundaries

The future surface may explain deterministic static evidence vocabulary:
decision question, decision owner, public claim level, proof path, rule ID or
rule family, evidence tier, coverage label, commit SHA, extractor version,
limitation, non-claim, validation evidence, rejected interpretation,
follow-up owner, review date placeholder, and residual risk.

The future surface must not claim or imply:

- autonomous decision-making by TraceMap;
- approval workflow, governance workflow, release approval, release safety, or
  operational safety;
- runtime behavior proof;
- production traffic proof, production behavior proof, endpoint performance
  proof, or outage cause;
- absence-of-impact proof;
- complete coverage;
- AI analysis, LLM analysis, embeddings, vector databases, or prompt
  classification in the core scanner or reducer;
- replacement of human judgment, governance, code review, source review,
  service ownership, telemetry, tests, or release process.

The future surface must not publish raw `facts.ndjson`, raw `index.sqlite`,
analyzer logs, raw source snippets, raw SQL, config values, secrets, local
paths, raw repository remotes, generated scan directories, private sample
names, raw command output, hidden validation details, or credential-like
values.

The future surface must not use blame language around teams, vendors,
consultants, service owners, reviewers, prior implementers, or code quality.

## Requirements

### Requirement 1: Publish a bounded evidence decision record surface

The future implementation shall publish a concept-level public page or section
that explains how to record a human owner decision after inspecting TraceMap
evidence.

Acceptance criteria:

- The rendered surface says `Public claim level: concept`.
- The rendered surface states `No public conclusion without evidence`.
- The surface says TraceMap provides evidence, not the decision.
- The implementation chooses one final placement from
  `/decisions/evidence-record/`, `/review-room/decision-record/`, a section on
  `/review-room/`, a section on `/packets/assembly/`, or a recorded equivalent
  if site information architecture has changed.
- The implementation records the final placement, rejected alternatives, and
  rationale in `implementation-state.md`.
- The placement decision record collectively references all four candidate
  placements from this spec: the selected placement with rationale, the three
  unchosen placements as rejected alternatives, and any rejected
  replace-a-neighbor options considered during implementation.
- The rationale explains why the surface is a decision-after-evidence record,
  not a review-room agenda, packet assembly checklist, claim checklist,
  manager packet, objection guide, proof-path tour, release gate, runtime
  workflow, approval workflow, or autonomous decision system.
- If implemented as a standalone route, metadata, sitemap metadata, and
  discovery metadata use `publicClaimLevel: concept`.
- If implemented as a section, the host page metadata remains concept-level or
  more conservative and does not imply a new shipped capability.
- The surface uses existing static site layout, navigation, metadata,
  accessibility, and validation patterns.
- The surface introduces no runtime service, telemetry collection, analytics
  dependency, form submission, local scanner invocation, generated evidence
  artifact, client-side state requirement, or decision automation.

### Requirement 2: Include the required record fields

The future surface shall define a complete decision record template with every
field needed to keep the owner decision attached to evidence and limitations.

Acceptance criteria:

- The required record fields are: decision question, decision owner, public
  claim level, proof path, rule ID/family, evidence tier, coverage label,
  commit SHA, extractor version, limitation, non-claim, validation evidence,
  rejected interpretation, follow-up owner, review date placeholder, and
  residual risk.
- The template explains that any missing field must be labeled as unavailable,
  private-only, not public-safe, or pending, rather than silently removed.
- `decision question` names the exact question being answered and must not
  expand into broader release, runtime, production, or safety claims.
- `decision owner` is a public role category or placeholder, not a private
  person's name or private team name.
- `public claim level` is `concept` by default for the page and for example
  records unless a future implementation records a separate evidence-backed
  upgrade decision.
- `proof path` points to public-safe pages, public-safe summaries,
  documentation, report-family summaries, rule catalog material, or private
  review locations named without exposing raw material.
- `rule ID/family` is required so every evidence-backed record stays attached
  to a documented rule and limitation. If a specific rule ID cannot be named
  publicly, the record uses a rule family and states the limitation.
- `evidence tier` uses only TraceMap vocabulary: `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, or `Tier4Unknown`.
- `coverage label` is transcribed from the cited evidence surface and is not
  normalized into stronger wording.
- `commit SHA` and `extractor version` identify the scan context behind the
  cited evidence when a public-safe summary exposes those values. If they are
  unavailable or not public-safe, the record keeps the field and records the
  limitation.
- `limitation` names the rule, coverage, data, or publication boundary that
  travels with the decision.
- `non-claim` states what the decision must not imply, including runtime,
  production, release, safety, approval, absence-of-impact, complete-coverage,
  AI/LLM, or autonomous-decision claims when relevant.
- `validation evidence` names public-safe validation results, review evidence,
  command families, test summaries, or manual review evidence without
  publishing raw command output or hidden validation details.
- `rejected interpretation` records the tempting conclusion the owner did not
  make because evidence was insufficient, private-only, reduced, outside
  static analysis, or unsupported.
- `follow-up owner` names the role category responsible for the next answer.
- `review date placeholder` uses a placeholder or example date, not real
  internal cadence or private review date values in examples.
- `residual risk` records what remains unknown, reduced, private-only,
  runtime-only, owner-pending, or outside TraceMap evidence.

### Requirement 3: Publish the required sections

The future surface shall present the decision record as a compact, scannable
workflow with visible examples and boundaries.

Acceptance criteria:

- The required sections are: why record the decision, record template, example
  safe record, unsafe record examples, stop conditions, follow-up owners, and
  non-claims.
- `why record the decision` explains that teams need to preserve the evidence
  trail, limitation, rejected interpretation, owner decision, follow-up owner,
  and residual risk so later readers do not repeat a stronger conclusion.
- `record template` contains every required field from Requirement 2.
- `example safe record` uses synthetic or already-public/demo-sourced
  placeholders only and labels itself as an example.
- `unsafe record examples` show patterns that must be downgraded, blocked, or
  kept internal, such as dropping the proof path, claiming release approval,
  implying runtime proof, hiding reduced coverage, omitting the owner, or
  sharing raw/private material.
- `stop conditions` lists blockers that prevent public reuse until corrected
  or explicitly labeled.
- `follow-up owners` maps remaining questions to public role categories such
  as service owner, runtime observability owner, release owner, test owner,
  reviewer, security owner, repository owner, manager, or TraceMap owner.
- `non-claims` centralizes boundaries for autonomous decisions, approval
  workflows, release safety, runtime proof, production proof,
  absence-of-impact proof, complete coverage, AI/LLM analysis, and replacement
  of human judgment or governance.

### Requirement 4: Define safe and unsafe record semantics

The future surface shall distinguish a safe decision record from unsafe public
copy.

Acceptance criteria:

- A safe record states the decision question, cites a proof path, keeps rule
  ID/family, evidence tier, coverage label, commit SHA, extractor version,
  limitation, non-claim, validation evidence, rejected interpretation,
  follow-up owner, review date placeholder, and residual risk visible.
- A safe record says the human owner made the decision after inspecting
  evidence; it does not say TraceMap decided, approved, certified, validated,
  blocked, released, or proved production behavior.
- A safe record does not say a system, route, endpoint, dependency, package,
  database reference, team, vendor, service, or release is impacted, safe,
  unsafe, approved, blocked, root cause, production proven, or validated for
  release unless the phrase appears inside explicit non-claim, limitation,
  stop-condition, rejected-interpretation, residual-risk, or unsafe-example
  wording.
- Unsafe examples must be clearly labeled as unsafe examples and must not be
  written in a way that a snippet or metadata description could be mistaken
  for an actual public claim.
- Unsafe examples include missing proof path, missing evidence tier, missing
  coverage label, private-only support, raw artifact leakage, hidden validation
  details, runtime proof wording, release approval wording, production proof
  wording, absence-of-impact wording, complete-coverage wording, AI/LLM impact
  wording, autonomous-decision wording, no follow-up owner, no validation
  evidence, and blame language.

### Requirement 5: Preserve public-safe artifacts and publication boundaries

The future surface shall publish only public-safe copy, public-safe links, and
synthetic or already-public examples.

Acceptance criteria:

- The page and metadata do not publish raw facts, SQLite content, analyzer
  logs, source snippets, SQL, config values, secrets, local paths, raw remotes,
  generated scan directories, private sample names, command output, hidden
  validation details, or credential-like values.
- Public examples are synthetic or derived from existing public-safe demo
  summaries without copying raw evidence.
- The page may name artifact families such as fact streams, SQLite indexes,
  reports, rule catalog entries, scan manifests, commit metadata, coverage
  labels, and documented limitations as local source-of-truth categories, but
  raw artifacts are not public page content.
- Private repository evidence may be described only as requiring private
  review and public-safe summarization before any public decision record can
  cite it.
- The page avoids local commands that expose ignored output paths, raw
  artifact names as links, machine-local paths, branch names not meant for
  publication, or private validation details.
- The page does not include real internal reviewer names, real owner or
  assignee identities, private team names, real private review dates, internal
  cadence, private route names, hidden feature names, or private sample
  identities.
- The page avoids blame language and treats rejected interpretations as
  evidence boundaries, not people failures.

### Requirement 6: Differentiate from adjacent pages and link safely

The future implementation shall make the evidence decision record's job clear
relative to neighboring public surfaces and link only to verified public-safe
routes.

Acceptance criteria:

- The page links to `/review-room/` as the meeting agenda, not as the decision
  record itself.
- The page links to `/packets/assembly/` when present as the evidence
  ingredient assembly workflow, not as owner decision proof.
- The page links to `/review-claim-checklist/` when present as the claim
  repeatability ritual, not as approval.
- The page links to `/manager-packet/` when present as manager-facing
  orientation, not as the source of a decision.
- The page links to `/questions/objections/` when present as skeptical
  question handling, not as a decision record.
- The page links to `/proof-paths/tour/` when present as proof-path
  education, not as a decision.
- The page links to `/proof-paths/`, `/limitations/`, and `/validation/` when
  present as supporting proof, boundary, and validation surfaces.
- If a required adjacent route does not exist at implementation time, the
  implementation records the omission, substitution, or deferred link in
  `implementation-state.md` instead of adding a dead link.
- Cross-links use bounded anchor text that does not imply runtime proof,
  production proof, release safety, operational safety, approval, autonomous
  decision-making, absence-of-impact proof, AI/LLM analysis, or complete
  coverage.
- The route or section is not added to top navigation unless
  `implementation-state.md` records a matching information-architecture
  decision.

### Requirement 7: Validate record fields, metadata, links, and safety

The future implementation shall add focused validation for the evidence
decision record surface.

Acceptance criteria:

- Validation checks visible required text: `Public claim level: concept` and
  `No public conclusion without evidence`.
- Validation checks the exact required record fields: decision question,
  decision owner, public claim level, proof path, rule ID/family, evidence
  tier, coverage label, commit SHA, extractor version, limitation, non-claim,
  validation evidence, rejected interpretation, follow-up owner, review date
  placeholder, and residual risk.
- Validation checks required sections: why record the decision, record
  template, example safe record, unsafe record examples, stop conditions,
  follow-up owners, and non-claims.
- Validation checks required links to adjacent public-safe surfaces that exist
  at implementation time, including `/review-room/`, `/packets/assembly/`,
  `/review-claim-checklist/`, `/manager-packet/`, `/questions/objections/`,
  `/proof-paths/tour/`, `/proof-paths/`, `/limitations/`, and `/validation/`
  when those routes are present.
- If implemented as a standalone route, page metadata includes title,
  description, canonical URL, Open Graph fields, and `og:type` consistent with
  neighboring concept pages.
- If implemented as a standalone route, sitemap metadata and discovery
  metadata are added using existing site patterns and discovery metadata uses
  `publicClaimLevel: concept`, the existing closed vocabulary for source type
  and hint category, limitations, and non-claims for autonomous decisions,
  approval workflows, release safety, runtime proof, production proof,
  absence-of-impact proof, complete coverage, AI or LLM analysis, and
  replacement of human judgment or governance.
- If implemented as a section, validation checks a stable in-page anchor and
  host-page metadata that remains concept-level or more conservative.
- Validation checks selected placement and rejected alternatives are recorded
  in `implementation-state.md`.
- Validation checks the placement decision record collectively references all
  four spec-defined candidate placements: the selected placement with
  rationale plus the three unchosen placements as rejected alternatives.
- Validation checks rendered text, decoded HTML, raw HTML attributes, alt
  text, captions, link anchor text, link title attributes, metadata, sitemap
  output, discovery output, generated pages, passing validation fixtures, and
  public-output fixtures for forbidden approval, decision, release, safety,
  runtime, production, absence-of-impact, complete-coverage, AI/LLM, embedding,
  vector database,
  prompt-classification, autonomous-decision, and
  replacement-of-human-judgment claims. The check must allow those terms only
  inside explicit non-claim, limitation, stop-condition,
  rejected-interpretation, residual-risk, or unsafe-example contexts.
- Validation identifies those allowed contexts by structural markers, such as
  stable section anchors, field-level containers, or validator-owned data
  attributes, rather than by broad prose inference. The structural convention
  must be documented in the implementation and tested so positive unsupported
  claims cannot pass merely because they appear inside a wrapper labeled as an
  unsafe example.
- A conforming implementation may use a concrete convention such as
  `data-tracemap-validation-context="unsafe-example"`,
  `data-tracemap-validation-context="non-claim"`,
  `data-tracemap-validation-context="rejected-interpretation"`, and
  `data-tracemap-validation-context="residual-risk"` on the smallest relevant
  region. If implementation chooses different markers, it records the
  substitution and rationale in `implementation-state.md`.
- Negative tests include a fixture that places a forbidden positive claim
  inside a `data-tracemap-validation-context="unsafe-example"` wrapper located
  outside any structurally allowed unsafe-example region, and asserts
  validation fails. This fixture proves the validator checks structural
  boundaries, not only marker names.
- Negative fixtures may contain the forbidden claim text or private/raw tokens
  they are designed to reject, but only as isolated failing inputs. They must
  not be included in public-output absence sweeps or passing fixture sweeps.
- Validation checks rendered text, decoded HTML, raw HTML attributes, alt
  text, captions, metadata, sitemap output, discovery output, generated pages,
  passing validation fixtures, and public-output fixtures for forbidden
  private/raw material and credential-like values.
- Validation enforces rendered word count bounds of 700 to 2500 words unless
  amended in `implementation-state.md`. The 700-word floor ensures all
  required sections and fields are substantively present, and the 2500-word
  ceiling keeps the surface a bounded record template rather than a broad
  governance page. The count includes rendered body prose and record field
  values while excluding page-level navigation, breadcrumbs, site headers, site
  footers, metadata blocks, and repeated field-label headers.
- Field labels count once in the record template; repeated field labels in the
  example safe record and unsafe examples are excluded from the rendered word
  count.
- Any implementation-state amendment to word count bounds must include a
  rationale, keep the floor at or above 400 rendered words, and keep the
  ceiling at or below 2500 rendered words.
- Validation checks example review date placeholders use an explicit
  placeholder pattern such as `YYYY-MM-DD`, not real private review dates or
  internal cadence.
- Validation checks example commit SHA and extractor version values use
  synthetic placeholders, redacted placeholders, or explicit public-demo
  provenance, not real private scan identifiers.
- Validation checks example validation evidence values are synthetic,
  public-safe, or explicit public-demo summaries, and do not include raw
  command output, private CI/job identifiers, hidden validation details, or
  credential-like values.
- Validation includes an accessibility check for the responsive record
  template, including semantic table headers or definition-list labels and
  screen-reader-accessible field names.
- Validation includes negative tests for missing required fields, missing
  required sections, missing required links, unsupported approval or decision
  claims, private/raw material leakage, and out-of-bounds word counts.
- Validation is wired into the existing aggregate site validation workflow so
  `npm run validate` exercises the selected route or host section.
- If the aggregate `npm run validate` workflow requires explicit route or
  section registration, the implementation must add that registration,
  document the registration pattern, and record the decision in
  `implementation-state.md`.
- If implementation-state word count amendment is needed because required
  sections and fields exceed 2500 words, trim-eligible content is introductory
  connective prose, repeated field-guidance wording, and navigation context
  sentences. Required field names, required section headings, required example
  content, and required non-claim statements are not trim-eligible.
- Implementation validation includes `git diff --check`,
  `./scripts/check-private-paths.sh`, `npm test` from `site/`,
  `npm run validate` from `site/`, `npm run build` from `site/`, and desktop
  and mobile browser sanity checks for the selected route or host page,
  including scroll-to-section sanity for section-only implementations.
