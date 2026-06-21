# Site TraceMap Tools Review Packet Assembly Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Define a future public-safe page or section for assembling a TraceMap review
packet from existing evidence surfaces. The surface should help a human prepare
review handoff by collecting the claim being reviewed, the proof path, static
evidence metadata, limitations, non-claims, validation evidence, unresolved
gaps, and the next owner before repeating a conclusion.

This is a spec-only site phase. It does not implement site code, scanner
behavior, reducer behavior, generated artifacts, validation scripts, or public
copy changes. It also does not define a generated packet-builder feature. The
future public surface is a human checklist and workflow for preparing a
public-safe review handoff from evidence that already exists elsewhere.

## Shared Site Principle

No public conclusion without evidence.

## Claim Boundaries

- The future page or section must say `Public claim level: concept`.
- The future page or section must say `No public conclusion without evidence`.
- The workflow may explain how a person assembles public-safe handoff material
  from existing TraceMap proof surfaces, but it must not claim that TraceMap
  generates review packets, proves runtime behavior, monitors production
  traffic, measures endpoint performance, identifies outage cause, grants
  release approval or safety, provides operational safety, performs AI or LLM
  analysis, conducts autonomous review, or provides complete coverage.
- The workflow must not publish raw facts, SQLite databases, analyzer logs,
  source snippets, SQL, configuration values, secrets, local absolute paths,
  raw repository remotes, generated scan directories, private sample names, or
  hidden validation details.

## Relationship to Existing Site Surfaces

The review packet assembly surface is a human preparation workflow. It
complements but must remain distinct from these neighboring surfaces:

- `/packets/` explains the general evidence packet artifact model. Review
  packet assembly explains how a human chooses which public-safe evidence from
  existing surfaces travels into a specific review handoff.
- `/manager-packet/` prepares manager-facing summaries. Review packet assembly
  prepares an evidence checklist for any review audience and does not optimize
  solely for management language.
- `/team-evidence-handoff/` focuses on receiver-specific handoff language.
  Review packet assembly focuses on the pre-handoff assembly steps and stop
  conditions.
- `/incident-evidence-handoff/` scopes evidence transfer to
  incident-adjacent conversations. Review packet assembly is not
  incident-specific and must not imply outage proof.
- `/review-room/` frames a shared meeting agenda for known, partial, and
  missing evidence. Review packet assembly is the human pre-meeting or
  pre-handoff checklist for gathering bounded material.
- `/review-claim-checklist/` answers whether a sentence may be repeated.
  Review packet assembly gathers the evidence ingredients and handoff notes
  that make that checklist possible.
- `/proof-source-catalog/` maps routes and claims to source material. Review
  packet assembly consumes those public-safe proof paths without becoming a
  second proof-source catalog.
- `/use-cases/change-review/` frames change review with static evidence.
  Review packet assembly is narrower: it prepares the ingredient checklist and
  stop-condition notes for one review handoff.
- `/questions/` organizes stakeholder questions. Review packet assembly
  answers what must travel with one packet, not a general question index.
- The claim-ledger concept governs claim vocabulary and upgrade/downgrade
  rules. Review packet assembly consumes that vocabulary without becoming a
  competing claim ledger.

## Requirements

### Requirement 1: Define route and placement decision

The future implementation shall choose a public placement for the review packet
assembly workflow and record the decision before implementation starts.

Acceptance criteria:

- Candidate placements include `/packets/assembly/`, `/review-packet/`, a
  section on `/packets/`, or a section on `/review-room/`.
- The implementation records the final placement and rejected alternatives in
  this spec's `implementation-state.md`.
- The selected page or section says `Public claim level: concept`.
- The selected page or section states `No public conclusion without evidence`.
- The selected placement does not create a competing packet taxonomy, proof
  source catalog, claim ledger, stakeholder question index, change-review
  brief, or review-room agenda.
- If implemented as a standalone route, the route is added to sitemap metadata
  and discovery metadata using existing site patterns.
- If implemented as a section on an existing page, the implementation records
  why section placement is clearer than a standalone route and how readers can
  link directly to the section.
- The surface is not added to top navigation unless the implementation records
  why the existing site navigation pattern supports it.

### Requirement 2: Publish the required packet ingredients

The future page or section shall define the required ingredients for a
public-safe review packet.

Acceptance criteria:

- The required ingredients are: claim being reviewed, audience, proof path,
  public claim level, rule ID or rule family, evidence tier, coverage label,
  commit SHA, extractor version, public-safe file path and line span,
  limitations, non-claims, next owner, validation evidence, and unresolved
  gaps.
- The page explains that a packet is incomplete when any required ingredient is
  missing or intentionally unavailable without a visible limitation.
- `proof path` points to public-safe pages, public-safe summaries,
  documentation, rule catalog material, report-family summaries, or private
  review locations that are named without exposing raw material.
- `public claim level` uses the site claim vocabulary appropriate for the
  surface, with this concept page remaining concept-level until a later spec
  changes it.
- `rule ID or rule family` is required so each evidence item has a documented
  rule and limitation. If a specific rule ID cannot be named publicly, the
  packet uses a rule family and states the limitation.
- `evidence tier` uses TraceMap vocabulary:
  `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, or
  `Tier4Unknown`.
- `coverage label` is transcribed from the cited evidence surface and is not
  normalized into stronger wording.
- `commit SHA` and `extractor version` identify the scan context behind the
  cited evidence when a public-safe summary exposes those values. If the
  values are not public-safe or are unavailable for the proof path, the packet
  records an explicit limitation instead of dropping the fields silently.
- `public-safe file path and line span` may name checked-in public demo paths
  or sanitized source references, but must not expose local absolute paths,
  private repository paths, hidden branch names, private remotes, or source
  snippets. If no public-safe path or span is available, the packet records an
  explicit limitation instead of inventing a stronger proof path.
- `validation evidence` names the public-safe validation result, command
  family, review evidence, or test summary supporting the handoff without
  publishing private logs or hidden validation details.
- `unresolved gaps` remains visible when coverage is unknown, reduced,
  syntax-only, private-only, or pending human review.

### Requirement 3: Publish the assembly workflow

The future page or section shall present a scannable workflow for assembling a
review packet from existing evidence surfaces.

Acceptance criteria:

- The workflow sections are: choose the question, collect public-safe
  evidence, attach limitations, name next owners, run claim checklist, stop
  conditions, and handoff notes.
- `choose the question` narrows the packet to the claim under review and the
  intended audience before evidence is gathered.
- `collect public-safe evidence` instructs the human assembler to use existing
  public-safe TraceMap surfaces and to keep proof paths attached.
- `attach limitations` requires coverage labels, evidence tiers, reduced or
  unknown coverage labels, and rule limitations to travel with the claim.
- `name next owners` records who owns the next human, telemetry, test, release,
  service, database, code, or review question.
- `run claim checklist` links or refers to the review claim checklist surface
  when it exists and states that process cannot upgrade unsupported evidence.
- `stop conditions` lists the mandatory blockers from Requirement 4.
- `handoff notes` capture audience, non-claims, validation evidence,
  unresolved gaps, and next owner without introducing raw or private material.

### Requirement 4: Define mandatory stop conditions

The future page or section shall make stop conditions visible and
validator-checkable.

Acceptance criteria:

- The required stop conditions are missing proof path, private-only support,
  raw artifact leakage, unknown or reduced coverage without label,
  unsupported runtime, release, or safety wording, no next owner, and no
  validation evidence.
- Missing proof path means the packet cannot be repeated publicly until a
  public-safe proof path or explicit private-only limitation exists.
- Private-only support means the packet may guide internal follow-up but must
  not become public copy until a public-safe summary exists.
- Raw artifact leakage includes raw facts, SQLite databases, analyzer logs,
  source snippets, SQL, config values, secrets, local paths, raw remotes,
  generated scan directories, private names, and hidden validation details.
- Unknown or reduced coverage without label blocks the handoff until the
  coverage label is restored.
- Unsupported runtime, release, or safety wording includes claims about
  runtime behavior, production traffic, endpoint performance, outage cause,
  release approval or safety, operational safety, production proof, or complete
  coverage.
- No next owner blocks handoff because the packet must identify who handles
  the remaining human or non-static question.
- No validation evidence blocks handoff because the packet must identify what
  was checked or which validation remains missing.

### Requirement 5: Preserve public-safe boundaries and non-claims

The future page or section shall keep review packet assembly bounded to
deterministic static evidence and human review.

Acceptance criteria:

- The page does not claim generated packet-builder behavior, runtime behavior
  proof, production traffic knowledge, endpoint performance measurement,
  outage cause, release approval, release safety, operational safety, complete
  coverage, AI impact analysis, LLM analysis, or autonomous review.
- The page does not imply TraceMap replaces human review, source review,
  ownership decisions, telemetry, logs, traces, APM, tests, release controls,
  incident response, manager judgment, service ownership, or database
  ownership.
- The page avoids saying a system, route, endpoint, dependency, package,
  database reference, or release is impacted, safe, unsafe, approved, blocked,
  root cause, validated for release, or production proven unless the phrase is
  inside explicit non-claim wording.
- The page may say the workflow helps humans prepare bounded review handoff
  material from existing static evidence surfaces.
- The page may name public-safe report families, rule IDs, evidence tiers,
  coverage labels, limitations, commit SHA, extractor versions, file paths,
  and line spans only when those details are public-safe.

### Requirement 6: Validate copy, metadata, links, and safety

The future implementation shall add focused validation for the review packet
assembly surface.

Acceptance criteria:

- Validation checks required rendered copy: `Public claim level: concept`,
  `No public conclusion without evidence`, the required packet ingredients,
  required workflow sections, and required stop conditions.
- Validation checks required links to adjacent public-safe surfaces that exist
  at implementation time, including `/packets/`, `/manager-packet/`,
  `/team-evidence-handoff/`, `/incident-evidence-handoff/`, `/review-room/`,
  `/review-claim-checklist/`, `/proof-source-catalog/`, `/proof-paths/`,
  `/limitations/`, and `/validation/` when those routes are present.
- If an adjacent route does not exist at implementation time, the
  implementation records the omission, substitution, or deferred link in
  `implementation-state.md` instead of adding a dead link.
- Standalone route metadata includes title, description, canonical URL, Open
  Graph fields, and `og:type` consistent with neighboring concept pages.
- Discovery metadata uses `publicClaimLevel: concept`, a bounded source type,
  a bounded hint category, limitations, and non-claims for runtime,
  production, endpoint performance, outage cause, release safety, operational
  safety, AI or LLM analysis, autonomous review, and complete coverage.
- Standalone routes are added to sitemap metadata using existing site
  patterns. Section placement records why sitemap metadata is not separately
  required.
- Validation checks rendered text, decoded HTML, raw HTML attributes, and
  metadata for forbidden runtime, production, release-safety,
  operational-safety, AI or LLM positioning, generated packet-builder feature
  claims, and autonomous-review claims. The check must be scoped so the
  required non-claims and boundary sections may contain those terms only in
  explicit negated wording. Validation may strip sanctioned non-claim regions
  before positive-claim scans, or require a negation context for those terms;
  it must not either block required non-claims or allow positive claims to pass
  because the same terms appear in a non-claim section.
- Validation checks rendered text, decoded HTML, raw HTML attributes, and
  metadata for forbidden private or raw material.
- Validation enforces a rendered word count between 400 and 1500 words unless
  section placement records a stricter existing-page constraint.
- Implementation validation includes `git diff --check`,
  `./scripts/check-private-paths.sh`, `npm test` from `site/`,
  `npm run validate` from `site/`, `npm run build` from `site/`, and desktop
  and mobile browser sanity checks for layout or interaction changes.

### Requirement 7: Keep implementation state and tasks accurate

The future implementation shall keep this spec packet aligned with completed
work.

Acceptance criteria:

- This spec-only phase keeps `Status: not-started` and moves `Readiness` to
  `ready-for-implementation` only after Medium or higher spec-review findings
  are patched or explicitly dispositioned.
- Future implementation tasks in `tasks.md` remain unchecked until future site
  implementation work begins.
- Implementation tasks are checked only after the corresponding implementation
  and validation work is complete.
- `implementation-state.md` records branch, target base, scope decisions,
  public claim level, route decision, review commands and results, validation
  commands and results, oddities, follow-up items, and unresolved gaps.
- If Kiro review is unavailable, `implementation-state.md` records the exact
  command and error.
- If a Kiro review identifies Medium or higher findings, the spec either
  patches them and records a rerun result or records why a rerun was not
  feasible.
