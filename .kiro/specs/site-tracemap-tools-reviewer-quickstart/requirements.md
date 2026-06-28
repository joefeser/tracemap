# Site TraceMap Tools Reviewer Quickstart Requirements

Status: implemented
Readiness: implemented
Public claim level: concept

## Summary

Define a future public-site reviewer quickstart page for `tracemap.tools`.
The surface should help a code reviewer land on the public site and inspect a
TraceMap evidence packet in about five minutes without reading the whole site
first.

This is a spec-only site phase. It does not implement site code, scanner
behavior, reducer behavior, generated artifacts, validation scripts, runtime
proof, AI or LLM analysis, embeddings, vector databases, prompt
classification, autonomous approval, or public copy changes.

The future implementation must include visible `Public claim level: concept`
and visible `No public conclusion without evidence`.

## Shared Site Principle

No public conclusion without evidence.

## Placement Decision

Selected future placement: `/reviewer-quickstart/`.

Rationale:

- It gives reviewers a direct low-context landing route.
- It avoids making `/review-room/` carry onboarding copy in addition to the
  meeting agenda.
- It avoids making `/packets/assembly/` look like the first page a reviewer
  must read before inspecting a packet.
- It keeps the quickstart distinct from the claim checklist and proof-path
  tour.

Rejected alternatives:

- `/review-room/quickstart/`: close to the meeting agenda, but it could imply
  the quickstart is only for a review-room session instead of any reviewer
  inspecting a packet.
- Section on `/review-room/`: lower route surface area, but it would blur the
  review-room agenda with first-visit orientation.
- Section on `/packets/assembly/`: close to packet preparation, but the
  quickstart is for inspecting a packet, not assembling one.

Future implementation may revisit placement only if it records the changed
decision and rejected alternatives in this spec's `implementation-state.md`
before site source changes begin.

## Relationship to Existing Site Surfaces

The reviewer quickstart is a first-stop inspection guide. It must remain
distinct from these neighboring public surfaces:

- `/review-room/`: a shared meeting agenda for known, partial, and missing
  evidence. The quickstart is what one reviewer checks before or during the
  meeting.
- `/packets/assembly/`: a human workflow for gathering public-safe packet
  ingredients. The quickstart assumes a packet or public-safe evidence trail
  already exists and teaches how to inspect it.
- `/review-claim-checklist/`: a ritual for deciding whether a sentence may be
  repeated, downgraded, held for owner follow-up, kept internal, or not
  repeated. The quickstart routes to that checklist but does not duplicate all
  outcome rules.
- `/proof-paths/tour/`: a guided reading tour for one public claim's proof
  path. The quickstart is shorter and packet-oriented.
- `/questions/`: a stakeholder question index. The quickstart starts from a
  packet under review, not from a broad question taxonomy.
- `/demo/manager-script/` or `/manager-demo-script/` if present: presenter
  wording for a bounded demo conversation. The quickstart is reviewer-facing
  inspection guidance.
- `/demo/runbook/`: an operator checklist for running and sharing the public
  demo. The quickstart is not an operator runbook and must not include local
  demo execution steps.

## Requirements

### Requirement 1: Publish a bounded quickstart concept

The future implementation shall publish a concept-level reviewer quickstart at
`/reviewer-quickstart/`.

Acceptance criteria:

- The rendered surface says `Public claim level: concept`.
- The rendered surface states `No public conclusion without evidence`.
- The route addresses code reviewers inspecting a TraceMap evidence packet or
  public-safe proof trail.
- The route frames itself as a five-minute inspection guide, not a complete
  review workflow, release gate, test replacement, source review replacement,
  runtime observability replacement, or autonomous approval system.
- The route does not claim TraceMap proves runtime behavior, production
  traffic, endpoint performance, outage cause, release safety, operational
  safety, complete coverage, AI impact analysis, LLM analysis, embeddings,
  vector database analysis, prompt classification, autonomous approval, or
  release approval.
- The route does not use blame language around vendors, consultants, teams, or
  code quality.

### Requirement 2: Include required quickstart sections

The future page shall use a scannable structure that lets a reviewer inspect
the evidence packet quickly.

Acceptance criteria:

- The required rendered sections are `Start Here`, `Five-Minute Review`,
  `Evidence Fields`, `Stop Conditions`, `Safe Review Language`,
  `Escalation Owners`, and `Non-Claims`.
- `Start Here` explains that the reviewer begins with the claim and stops if
  there is no evidence trail.
- `Five-Minute Review` presents the required inspection steps in order.
- `Evidence Fields` defines the public-safe packet fields a reviewer should
  expect to see.
- `Stop Conditions` lists blockers that prevent repeating or upgrading a
  claim.
- `Safe Review Language` gives bounded wording patterns and forbidden wording
  categories without publishing private examples.
- `Escalation Owners` names owner categories for missing evidence, validation,
  runtime questions, release decisions, source review, tests, telemetry, or
  service ownership.
- `Non-Claims` lists what the quickstart and TraceMap evidence packet do not
  prove.

### Requirement 3: Publish the required five-minute review steps

The future page shall present the review steps as a fixed quickstart checklist.

Acceptance criteria:

- The required quickstart steps are:
  `identify the claim`, `find the proof path`, `check public claim level`,
  `read rule ID/family`, `inspect evidence tier and coverage label`,
  `check commit/extractor context`, `read limitations/non-claims`,
  `name next owner`, and `stop on missing evidence`.
- `identify the claim` tells the reviewer to name the exact claim before
  reading supporting material.
- `find the proof path` requires a public-safe route, summary, documented
  source trail, or private review location named without exposing raw material.
- `check public claim level` keeps concept, demo, shipped, hidden, or other
  supported site vocabulary from being upgraded by confidence or repetition.
- `read rule ID/family` requires a deterministic rule identifier or documented
  rule family before evidence is repeated.
- `inspect evidence tier and coverage label` uses TraceMap tier vocabulary
  such as `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, and
  `Tier4Unknown` when examples are shown.
- `check commit/extractor context` requires source revision and extractor
  context when the cited public-safe evidence exposes those values; otherwise
  the packet records an explicit limitation.
- `read limitations/non-claims` requires the reviewer to carry limitations
  with the claim instead of treating them as footnotes.
- `name next owner` assigns follow-up to a human owner category instead of
  letting missing evidence imply a stronger conclusion.
- `stop on missing evidence` tells the reviewer not to repeat, upgrade, or
  approve a claim when required evidence is missing.

### Requirement 4: Define expected evidence fields

The future page shall define the evidence fields that belong in a public-safe
reviewer quickstart.

Acceptance criteria:

- Required fields are claim, proof path, public claim level, rule ID or rule
  family, evidence tier, coverage label, commit SHA or source revision
  context, extractor version or extractor family, file path and line span when
  public-safe, limitation, non-claim, validation evidence, unresolved gap, and
  next owner.
- Public-safe file paths and line spans may name checked-in public demo paths
  or sanitized source references. They must not include local absolute paths,
  private repository paths, raw remotes, generated scan directories, or source
  snippets.
- Commit and extractor context may be omitted only when the page or packet
  says why the context is unavailable or not public-safe.
- Validation evidence names public-safe command families, review summaries,
  test summaries, or validation status without exposing raw command output or
  hidden validation details.
- Unresolved gaps remain visible when evidence is reduced, syntax-only,
  unknown, private-only, or pending owner review.

### Requirement 5: Define stop conditions and safe language

The future page shall make unsupported review conclusions easy to stop.

Acceptance criteria:

- Stop conditions include missing proof path, missing rule ID or rule family,
  missing evidence tier, missing coverage label, missing limitation, missing
  claim level, missing commit or extractor context without an explicit
  limitation, no validation evidence, no next owner, private-only support
  presented as public proof, raw artifact leakage, and runtime, release,
  safety, production, AI/LLM, or complete-coverage wording.
- Safe language uses bounded verbs such as `inspect`, `check`, `follow`,
  `review`, `compare`, `label`, `record`, `route`, `escalate`, or `cannot
  conclude from this packet`.
- Safe language does not say a change, endpoint, service, vendor system,
  team, release, dependency, database reference, or packet is safe, unsafe,
  approved, blocked, root cause, production-proven, complete, or impacted
  unless the wording is inside an explicit non-claim or forbidden-wording
  example.
- The page says missing evidence creates a follow-up owner question, not a
  clean conclusion.

### Requirement 6: Preserve public-safe material boundaries

The future page shall publish only public-safe guidance and metadata.

Acceptance criteria:

- The page, metadata, discovery entries, tests, and validation fixtures do not
  publish raw facts, raw SQLite content, analyzer logs, raw source snippets,
  raw SQL, config values, secrets, local paths, raw remotes, generated scan
  directories, private sample names, raw command output, hidden validation
  details, or credential-like values.
- Public examples are synthetic, authored, or derived from already public-safe
  demo summaries.
- The page may name artifact families only to explain boundaries and stop
  conditions, not to expose actual artifact content.
- The page does not include local commands that reveal ignored output paths,
  machine-specific locations, raw remotes, or private repository names.
- The page does not add analytics, forms, uploads, client-side data fetches,
  private artifact viewers, runtime services, agent integrations, or
  autonomous review flows.

### Requirement 7: Link to adjacent proof and review surfaces

The future page shall help reviewers move from the quickstart to the deeper
public-safe pages.

Acceptance criteria:

- Required links include `/review-room/`, `/packets/assembly/`,
  `/review-claim-checklist/`, `/proof-paths/tour/`, `/proof-paths/`,
  `/questions/`, `/demo/runbook/`, `/limitations/`, and `/validation/` when
  those routes exist at implementation time.
- If `/demo/manager-script/` exists, link to it as presenter guidance; if the
  live route is named differently, record the substitution or deferral in
  `implementation-state.md`.
- Link text preserves each target's scope and does not imply runtime proof,
  production proof, release approval, operational safety, AI/LLM analysis,
  autonomous review, or complete coverage.
- Missing or renamed adjacent routes are recorded in `implementation-state.md`
  with public-safe substitutes or deferred-link rationale.

### Requirement 8: Add metadata, discovery, and validation

The future implementation shall make the quickstart discoverable and enforce
its public claim boundaries.

Acceptance criteria:

- Standalone route metadata includes title, description, canonical URL, and
  Open Graph metadata.
- Sitemap metadata includes `/reviewer-quickstart/`.
- Discovery metadata labels `/reviewer-quickstart/` with
  `publicClaimLevel: concept`, a suitable existing `hintCategory`, source type
  matching existing site patterns, preferred proof path `/proof-paths/`, and
  bounded limitations and non-claims.
- Validation checks required copy, required links, route metadata, sitemap
  metadata, discovery metadata, forbidden claims, private or raw material, and
  rendered word count.
- Validation checks the required exact phrases `Public claim level: concept`
  and `No public conclusion without evidence`.
- Validation checks the required section headings and quickstart step labels.
- Validation enforces a rendered word count between 500 and 1400 words unless
  implementation records a tighter bound with rationale.
- Validation scans rendered text, decoded HTML, raw HTML attributes, metadata,
  discovery entries, and route fixtures for forbidden public claims and
  private or raw material.
- Validation includes negative tests for missing required copy, missing links,
  missing metadata, forbidden runtime/release/safety/AI claims, private or raw
  material, and word count outside bounds.
- Implementation validation includes `git diff --check`,
  `./scripts/check-private-paths.sh`, focused text checks, `npm test` from
  `site/`, `npm run validate` from `site/`, `npm run build` from `site/`, and
  desktop and mobile browser sanity checks.
