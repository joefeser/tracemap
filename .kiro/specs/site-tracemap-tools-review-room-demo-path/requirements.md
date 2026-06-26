# Site TraceMap Tools Review Room Demo Path Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Define a future `tracemap.tools` guided path that starts from the existing
review-room surfaces and walks a visitor through a demo-safe evidence review
flow: choose a static question, inspect proof paths and an evidence packet,
check limitations and non-claims, route unresolved questions to owners, and
stop when evidence is insufficient.

This is a spec-only site packet. It does not implement site code, validation
scripts, generated output, scanner behavior, reducer behavior, runtime proof,
traffic proof, endpoint performance checks, outage diagnosis, release safety,
or complete product workflow behavior.

## Shared Site Principle

No public conclusion without evidence.

## Claim Level Rationale

The future guided path shall visibly say `Public claim level: concept` because
it describes a future guided reading path across existing public-safe review
surfaces. It does not create a live review room, a production demo, a current
scan, a runtime monitor, an endpoint performance result, an outage finding, a
release decision, or complete coverage.

Do not upgrade this surface to `demo` unless a future spec amendment requires
checked-in public-safe demo evidence for every walkthrough step, proof path,
evidence packet row, limitation, owner route, stop condition, metadata claim,
and validation assertion. Demo-safe examples must still avoid raw facts,
SQLite databases, analyzer logs, source snippets, SQL, config values,
secrets, local paths, raw remotes, generated directories, private sample names,
hidden validation detail, and local-only command output.

## Audience

- Visitors trying to understand how a public TraceMap review conversation
  should proceed from question to evidence boundary.
- Reviewers, architects, managers, and engineers using public-safe pages to
  practice a static evidence review without exposing private material.
- Site authors deciding how future review-room, agenda, proof-path, claim
  checklist, and demo pages should connect.
- Agents preparing public-site copy that must preserve concept-level,
  evidence-backed, non-AI positioning.

## Relationship To Existing Surfaces

The guided path answers: "How should a visitor move through the existing
review-room surfaces during a demo-safe static evidence review?"

It must remain distinct from neighboring pages:

- `/review-room/` remains the concept-level room and bounded static-evidence
  agenda.
- `/review-room/agenda/` remains the meeting agenda and handoff script.
- `/proof-paths/` remains the proof-path index and evidence-route overview.
- `/proof-paths/tour/` remains the guided proof-path reading tour for one
  claim.
- `/review-claim-checklist/` remains the repeat-before-reuse decision
  checklist.
- `/packets/assembly/` remains the human packet assembly checklist.
- `/packets/` and `/packets/examples/` remain evidence-packet orientation and
  examples.
- `/demo/`, `/demo/start-here/`, `/demo/evidence-trail/`,
  `/demo/proof-assets/`, `/demo/result/`, `/demo/runbook/`, and
  `/demo/troubleshooting/` remain demo orientation, result, proof, runbook, and
  troubleshooting surfaces.
- `/limitations/` and `/validation/` remain the broader boundary and
  validation surfaces.
- `/owners/follow-up/` remains the owner handoff destination for unresolved
  questions when present.

The guided path may link to these routes only when they exist at
implementation time. If a route is absent, renamed, or replaced, the
implementation records the omission or substitute in `implementation-state.md`
and does not add a dead link.

## Requirements

### Requirement 1: Choose a bounded public placement

The future implementation shall add the guided path as one public route or one
bounded section using an explicit placement decision.

Acceptance criteria:

- Candidate placements include `/review-room/demo-path/`, a section on
  `/review-room/`, a section on `/review-room/agenda/`, or a section on
  `/demo/start-here/`.
- `/review-room/demo-path/` is the non-binding recommendation because the path
  starts from the review room but should not crowd the base room or meeting
  agenda pages.
- The implementation records the selected placement, rejected alternatives,
  and route-existence check in this spec's `implementation-state.md`.
- The selected page or section says `Public claim level: concept`.
- The selected page or section says `No public conclusion without evidence`.
- The placement does not create a live workspace, review queue, production
  demo, generated packet builder, proof engine, release gate, incident tool,
  stakeholder question index, claim ledger, or duplicate proof-path catalog.
- If implemented as a standalone route, the route is added to sitemap
  metadata, discovery metadata, canonical metadata, and internal-link
  validation using existing site patterns.
- If implemented as a section, the implementation records why section
  placement is clearer than a standalone route, adds stable anchors, validates
  anchor uniqueness, and confirms the section does not visually dominate the
  host page.
- The surface is not added to top navigation unless a future
  information-architecture review records why the existing site navigation
  pattern supports it.

### Requirement 2: Publish the step contract

The future page or section shall define a fixed guided-path contract that
visitors can follow without inferring live product completeness.

Acceptance criteria:

- The guided path includes these required steps, in this order: choose a
  static question, open the review room, inspect the agenda, inspect proof
  paths, inspect an evidence packet, run the claim checklist, check
  limitations and non-claims, route unresolved questions to owners, and stop
  when evidence is insufficient.
- Each step includes: step label, visitor action, required evidence field or
  route, allowed outcome, limitation, next owner or next route, and stop
  condition.
- Each required step supplies non-empty text for limitation, stop condition,
  and next owner or next route. Placeholder values such as `-`, `n/a`,
  `see above`, or `none` are not acceptable for those fields. If no stop
  condition applies to a step, the step states explicitly why stopping is not
  triggered instead of leaving the field blank or deferred.
- Required steps are rendered as one contiguous guided-path block. If the
  guided path is implemented as a section on an existing host page, host-page
  content must not interrupt the nine required steps.
- `choose a static question` narrows the flow to one authored public-safe
  question, such as whether a claim has public-safe static evidence, not
  whether a live system is safe or a release can proceed.
- `open the review room` links to `/review-room/` when present and introduces
  concept-level review framing without presenting the room as a shipped
  workflow.
- `inspect the agenda` links to `/review-room/agenda/` when present and uses
  the meeting agenda to preserve question, proof path, evidence tier, coverage
  label, limitation, gap, owner, and handoff fields.
- `inspect proof paths` links to `/proof-paths/` and `/proof-paths/tour/` when
  present and requires proof-path status to stay visible.
- `inspect an evidence packet` requires at least one of `/packets/`,
  `/packets/assembly/`, or `/packets/examples/` to be present and linked at
  implementation time. If none of these routes exists in generated output, the
  step is retained with a visible limitation note and the omission is recorded
  in `implementation-state.md`; the step is not silently removed. The step
  does not claim generated packet-builder behavior.
- `run the claim checklist` links to `/review-claim-checklist/` when present
  and states that the checklist cannot upgrade unsupported evidence.
- `check limitations and non-claims` links to `/limitations/` and
  `/validation/` when present and requires unsupported runtime, production,
  release, safety, AI/LLM, complete-coverage, and private/raw claims to be
  downgraded, removed, or routed to an owner.
- `route unresolved questions to owners` links to `/owners/follow-up/` when
  present, or uses role labels when that route is unavailable.
- `route unresolved questions to owners` uses role-based owner labels only,
  such as evidence owner, site owner, demo owner, source owner, test owner,
  runtime owner, service owner, database owner, release reviewer, validation
  owner, documentation owner, and manager/reviewer owner. It never publishes
  private owner names.
- `route unresolved questions to owners` states that routing a question
  transfers responsibility for the next review step and does not prove,
  approve, diagnose, validate, or clear a claim.
- `stop when evidence is insufficient` appears as a visible final step and
  states that insufficient evidence blocks a public conclusion.

### Requirement 3: Preserve proof-path and packet requirements

The future page or section shall require every review-step conclusion to stay
attached to public-safe proof and packet fields.

Acceptance criteria:

- Required proof fields are proof path, rule ID or rule family, evidence tier,
  coverage label, limitation, non-claim, public claim level, public-safe source
  context, next owner, and validation evidence.
- Public-safe source context may include commit SHA, extractor version, public
  route, checked-in public demo path, report-family name, sanitized file path,
  and line span only when those details are already public-safe.
- If commit SHA, extractor version, file path, or line span is not public-safe
  or not available, the page records a visible limitation instead of inventing
  or omitting the field.
- Evidence tiers use only TraceMap vocabulary: `Tier1Semantic`,
  `Tier2Structural`, `Tier3SyntaxOrTextual`, and `Tier4Unknown`.
- Coverage labels are transcribed from the cited public-safe surface and are
  not normalized into stronger wording.
- Packet language describes a human-readable evidence packet or public-safe
  packet surface, not raw `facts.ndjson`, raw SQLite, analyzer logs, raw source
  snippets, SQL, configuration values, secrets, local paths, raw remotes,
  generated directories, private samples, or hidden validation detail.
- A step may end with `continue`, `downgrade`, `owner follow-up`, `internal
  only`, or `stop`; it may not end with unqualified `impacted`, `safe`,
  `approved`, `root cause`, `complete`, or `production proven`.

### Requirement 4: Link only to existing public-safe surfaces

The future implementation shall connect the guided path to adjacent public
surfaces only when generated output proves the links resolve.

Acceptance criteria:

- Required links, when present, are `/review-room/`,
  `/review-room/agenda/`, `/proof-paths/`, `/proof-paths/tour/`,
  `/review-claim-checklist/`, `/packets/`, `/packets/assembly/`,
  `/packets/examples/`, `/limitations/`, `/validation/`, `/demo/`,
  `/demo/start-here/`, `/demo/evidence-trail/`, `/demo/proof-assets/`,
  `/demo/result/`, `/demo/runbook/`, `/demo/troubleshooting/`, and
  `/owners/follow-up/`.
- Optional related links, when present, include `/questions/`,
  `/questions/objections/`, `/reviewer-quickstart/`, `/manager-brief/`,
  `/manager-packet/`, `/team-evidence-handoff/`,
  `/decisions/evidence-record/`, and `/handoff/template/`.
- Link text names the public-safe destination topic, such as `review room`,
  `meeting agenda`, `proof paths`, `claim checklist`, `evidence packet`, or
  `owner follow-up`; it does not use generic text such as `click here`.
- Demo links use demo-safe wording and do not imply shipped product coverage,
  live production proof, current release proof, runtime diagnosis, or complete
  workflow support.
- Validation confirms each required and optional link that is rendered
  resolves in generated site output.
- If an adjacent route does not exist at implementation time, the route is
  omitted or replaced with a documented public-safe substitute and the decision
  is recorded in `implementation-state.md`.

### Requirement 5: Define mandatory stop conditions

The future page or section shall make stop conditions visible,
validator-checkable, and stronger than the desire to finish the demo path.

Acceptance criteria:

- Required stop conditions are missing proof path, missing rule ID or rule
  family, missing evidence tier, missing coverage label, missing limitation,
  missing validation evidence, private-only support without public-safe
  summary, raw artifact leakage, unsupported runtime or production wording,
  unsupported release or safety wording, unsupported AI/LLM wording, no next
  owner, and no public-safe packet route or substitute.
- Missing proof path means the visitor cannot repeat a public conclusion until
  a public-safe proof path exists or the material is explicitly labeled
  private-only.
- Private-only support may guide internal follow-up but must not become public
  copy until a public-safe summary exists.
- Raw artifact leakage includes facts streams, SQLite databases, analyzer
  logs, source snippets, SQL, configuration values, secrets, local paths, raw
  remotes, generated directories, private names, hidden validation detail, and
  credential-like values.
- Unsupported runtime or production wording includes runtime behavior,
  production traffic, endpoint performance, outage cause, live diagnosis,
  production proof, and complete coverage.
- Unsupported release or safety wording includes release approval, release
  safety, operational safety, safe to ship, clean for release, and absence of
  impact.
- Unsupported AI/LLM wording includes AI impact analysis, LLM analysis,
  prompt-based classification, embeddings, vector databases, intelligent
  impact, and smart impact.
- No next owner blocks the path because unresolved questions must be routed
  rather than silently resolved. A role label drawn from the allowed owner
  label vocabulary satisfies the next-owner requirement when
  `/owners/follow-up/` is absent. An implementer may not leave next-owner
  blank or assign a private name that is not in the allowed vocabulary.

### Requirement 6: Preserve public-safe boundaries and forbidden wording

The future page or section shall keep public copy bounded to deterministic
static evidence and human review.

Acceptance criteria:

- The page does not claim runtime proof, production traffic knowledge,
  endpoint performance measurement, outage cause, release approval, release
  safety, operational safety, production proof, complete product coverage,
  live workflow completeness, AI impact analysis, LLM analysis, prompt-based
  proof, embeddings, vector database reasoning, or autonomous review.
- The page does not imply TraceMap replaces source review, ownership
  decisions, tests, telemetry, logs, traces, APM, incident response, release
  controls, manager judgment, service ownership, database ownership, site
  author review, or human validation.
- The page avoids saying a system, route, endpoint, dependency, package,
  database reference, owner, release, or incident is impacted, safe, unsafe,
  approved, blocked, root cause, validated for release, production proven, or
  completely covered unless the phrase appears inside explicit non-claim or
  forbidden-wording guidance.
- Public examples use authored placeholders or checked-in public-safe demo
  material only.
- The page does not publish raw source, SQL, configuration values, secrets,
  local absolute paths, raw repository remotes, generated scan directories,
  private sample names, hidden validation detail, private owner names, raw
  command output, credential-like values, or ignored output paths.

### Requirement 7: Add metadata, discovery, and validation

The future implementation shall make the guided path discoverable while
preserving concept-level boundaries.

Acceptance criteria:

- Standalone route metadata includes title, description, canonical URL, and
  Open Graph fields, with `og:type` following the existing site pattern for
  concept pages. If no concept-page pattern is available, use
  `<meta property="og:type" content="article">`.
- Standalone route sitemap metadata is registered in
  `site/src/_site/pages.json`.
- Standalone route discovery metadata is registered in
  `site/src/_site/discovery.json` with `publicClaimLevel: concept`,
  `hintCategory: use-case`, `sourceType: site-page`,
  `preferredProofPath: /proof-paths/`, limitations, and non-claims. Do not
  introduce a new discovery category for this guided path.
- Section placement records why standalone sitemap and discovery metadata are
  not added and validates the host page metadata does not imply a stronger
  claim for the guided path.
- Validation checks rendered copy for `Public claim level: concept`,
  `No public conclusion without evidence`, required step labels, required
  proof fields, required stop conditions, owner routing language, and
  forbidden wording.
- Validation checks required step labels appearing in the documented order,
  contiguous within the guided-path section, and not interleaved with
  unrelated host-page content. For section placement, the validator confirms
  the nine required steps are rendered as a single contiguous block within a
  stable anchor scope.
- Validation confirms the owner-routing copy uses role-based labels, states
  that routing does not prove, approve, diagnose, validate, or clear a claim,
  and contains no private owner names.
- Validation confirms each step outcome is one of `continue`, `downgrade`,
  `owner follow-up`, `internal only`, or `stop`, and rejects unqualified
  `impacted`, `safe`, `approved`, `root cause`, `complete`, or
  `production proven`.
- Validation checks decoded HTML, rendered text, metadata, and raw attributes
  for forbidden private/raw text and unsupported AI/LLM positioning.
- Validation includes negative tests for missing claim-level copy, missing
  shared principle, missing final stop step, unresolved links, unsupported
  demo claims, raw/private leakage, and unsupported runtime/release/safety
  wording.
- Validation includes `git diff --check`, `npm test` from `site/`,
  `npm run validate` from `site/`, `npm run build` from `site/`,
  `./scripts/check-private-paths.sh`, and desktop/mobile browser sanity checks
  for layout or interaction changes when implementation occurs.

### Requirement 8: Keep this packet spec-only until a later implementation

This phase shall produce only the spec packet and review evidence.

Acceptance criteria:

- This phase changes only files under
  `.kiro/specs/site-tracemap-tools-review-room-demo-path/`.
- It creates `requirements.md`, `design.md`, `tasks.md`,
  `implementation-state.md`, and `review-packet.md`.
- It does not edit `site/src/`, `site/scripts/`, generated outputs, scanner
  code, reducer code, package files, validation scripts, or repository
  metadata outside this spec folder.
- The packet records Kiro review artifacts, findings patched or dispositioned,
  validation commands, and follow-up notes in `implementation-state.md`.
- Readiness starts as `spec-review` and remains there until requested Kiro
  reviews run and Medium or higher actionable findings are patched or
  dispositioned. Readiness may advance to `ready-for-implementation` only
  after that review work is recorded.
