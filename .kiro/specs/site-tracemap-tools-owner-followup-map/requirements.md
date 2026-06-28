# Site TraceMap Tools Owner Follow-Up Map Requirements

Status: implemented
Readiness: implemented
Public claim level: concept

## Summary

Define a future public-site owner follow-up map for `tracemap.tools`. The
surface should help a reader route static-evidence questions to the next
human owner category: code owner, reviewer, test owner, service/runtime
owner, database owner, release reviewer, architect, or manager.

This is a spec-only public site phase. It does not implement site source,
scanner behavior, reducer behavior, generated artifacts, validation scripts,
runtime telemetry, AI/LLM analysis, embeddings, vector databases, prompt
classification, ownership detection, release approval, or public copy
changes.

The future page must not claim that TraceMap knows real organizational
ownership. The map is a bounded handoff aid: static evidence can show why a
question exists, what proof path supports the question, what remains unknown,
and which owner category should answer next.

Future implementation must include visible `Public claim level: concept` and
visible `No public conclusion without evidence`.

## Shared Site Principle

No public conclusion without evidence.

## Claim Level Rationale

Use `Public claim level: concept` because the owner follow-up map is an
orientation surface over deterministic static evidence and human review
roles. It does not publish a new proof claim, identify real teams, prove
production ownership, or validate that a named person, team, service, or
manager owns an answer.

Do not upgrade the page to `demo` merely because linked pages contain
demo-backed proof paths. Any future claim-level upgrade requires an
implementation-state decision naming exact public-safe evidence for the
specific owner-routing claim.

## Candidate Placement

Candidate placements for future implementation are:

- `/owners/follow-up/`
- `/review-room/owners/`
- A section on `/team-evidence-handoff/`
- A section on `/questions/`

Future implementation must choose the final placement and record rejected
alternatives in `implementation-state.md` before changing site source. The
route or section must remain discoverable without implying TraceMap performs
real org ownership detection.

## Relationship To Existing Public Surfaces

The owner follow-up map is a routing surface for the next human owner
category. It must remain distinct from these neighboring surfaces:

- `/team-evidence-handoff/`: describes receiver-specific packet handoff
  language. The owner follow-up map starts from a question shape and routes it
  to an owner category.
- `/incident-evidence-handoff/`: carries incident-adjacent static evidence,
  proof paths, limits, and next owners into a conversation. The owner map is
  not incident-specific and must not imply incident command authority.
- `/reviewer-quickstart/`: helps a code reviewer inspect a packet quickly.
  The owner map helps any reader decide who must answer next when static
  evidence is insufficient or role-specific.
- `/questions/`: routes stakeholder questions to evidence surfaces. The owner
  map routes follow-up questions to owner categories and handoff wording.
- `/questions/objections/`: answers skeptical challenge shapes. The owner map
  is not an objection guide; it names who should resolve each evidence gap or
  decision.
- `/packets/assembly/`: explains how to assemble a public-safe packet. The
  owner map assumes a bounded evidence trail exists or is missing and focuses
  on the next owner handoff.
- `/manager-packet/`: gives manager-facing value and summary framing. The
  owner map includes manager as one possible owner category but does not
  replace manager packet copy.

If any route has moved or is unavailable at implementation time, the
implementation must select the closest live public-safe equivalent, defer the
link, or record the substitution and rationale in `implementation-state.md`.
Dead links are not acceptable.

If the final placement is an embedded section on a candidate host page
(`/team-evidence-handoff/` or `/questions/`), the relationship band must omit
the self-reference to that host and instead frame the distinction as `this
owner follow-up section versus the rest of this page`. The required-links set
must drop the self-link to the host route, and `implementation-state.md` must
record that the self-host relationship row and self-link were intentionally
removed.

## Claim Boundaries

The future surface may explain TraceMap's deterministic static evidence
vocabulary: proof paths, rule IDs or rule families, evidence tiers, coverage
labels, limitations, non-claims, public claim levels, and owner-category
handoff language.

The future surface must not claim or imply:

- real organizational ownership knowledge;
- production ownership proof;
- runtime behavior proof;
- production traffic visibility;
- endpoint performance insight;
- release approval;
- release safety;
- operational safety;
- complete coverage;
- AI analysis, LLM analysis, embeddings, vector databases, or prompt
  classification in the core scanner or reducer;
- autonomous approval;
- replacement of human judgment.

The future surface must not publish raw facts, SQLite database content,
analyzer logs, source snippets, SQL, configuration values, secrets, local
paths, repository remotes, generated scan directories, private sample names,
command output, hidden validation details, or credential-like values.

The future surface must not use blame language around teams, owners,
reviewers, vendors, consultants, managers, architects, or code quality.
Missing evidence should create a follow-up question, not an accusation.

## Requirements

### Requirement 1: Publish a bounded owner follow-up map in a future phase

The future implementation shall publish a concept-level owner follow-up map
page or section that routes static-evidence questions to human owner
categories without claiming TraceMap knows actual org ownership.

Acceptance criteria:

- The rendered surface says `Public claim level: concept`.
- The rendered surface states `No public conclusion without evidence`.
- The implementation chooses one final placement from
  `/owners/follow-up/`, `/review-room/owners/`, a section on
  `/team-evidence-handoff/`, a section on `/questions/`, or a recorded
  equivalent if site information architecture has changed.
- The implementation records the final placement, rejected alternatives, and
  rationale in `implementation-state.md`.
- The rationale explains why the surface is owner-category routing, not an
  org chart, ownership detector, reviewer quickstart, packet assembly guide,
  manager packet, objection guide, incident handoff, release gate, or runtime
  workflow.
- If implemented as a standalone route, metadata, sitemap metadata, route
  index metadata, and discovery metadata use `publicClaimLevel: concept`.
- If implemented as a section, the host page metadata remains concept-level
  or more conservative and does not imply a new shipped capability.
- The surface uses existing static site layout, navigation, metadata,
  accessibility, and validation patterns.
- The surface introduces no runtime service, telemetry collection, analytics
  dependency, form, local scanner invocation, generated evidence artifact, or
  client-side state requirement.

### Requirement 2: Include the required owner-routing rows

The future surface shall include every required question row with the same
evidence-preserving schema.

Acceptance criteria:

- Include `code path question`.
- Include `test coverage question`.
- Include `runtime behavior question`.
- Include `data/schema question`.
- Include `config/deployment question`.
- Include `release decision question`.
- Include `architecture decision question`.
- Include `evidence gap question`.
- Additional rows may be added only if they follow the same row schema and
  claim boundaries.
- Each row is phrased as a follow-up question or owner-routing need, not as a
  conclusion that TraceMap has already resolved.
- Each row avoids blame language and treats missing evidence as a handoff
  condition.

### Requirement 3: Require the complete row schema

Each owner-routing row shall preserve what static evidence can and cannot
support before naming the next owner category.

Acceptance criteria:

- Each row includes: static evidence trigger, what TraceMap can show, what
  TraceMap cannot show, next owner, handoff wording, proof path, limitation,
  and stop condition.
- `static evidence trigger` names a public-safe evidence category such as a
  rule ID or rule family, evidence tier, coverage label, source span when
  public-safe, generated public-safe summary, validation page, or linked
  concept/demo route.
- `what TraceMap can show` uses bounded static wording such as `can point to`,
  `can list`, `can label`, `can connect to a proof path`, `can surface a
  static reference`, or `can record an analysis gap`.
- `what TraceMap cannot show` states the relevant non-claim without implying
  an absence-of-risk conclusion.
- `next owner` uses only owner categories: code owner, reviewer, test owner,
  service/runtime owner, database owner, release reviewer, architect, or
  manager.
- `handoff wording` is synthetic, public-safe language that names the
  question, evidence boundary, owner category, and next action without source
  snippets, private identifiers, blame, or raw artifacts.
- `proof path` links to or names a public-safe proof route, generated public
  summary, validation/limitation page, or private review location described
  without exposing raw material.
- `limitation` explains what remains partial, private-only, syntax-only,
  unavailable, reduced, unknown, or outside static evidence.
- `stop condition` tells the reader when not to repeat, upgrade, approve, or
  route the claim without further human review.

### Requirement 4: Preserve row semantics by owner category

The future map shall route each required question to the correct owner
category and boundary.

Acceptance criteria:

- Code path question: routes to code owner or reviewer when static evidence
  points to files, symbols, endpoints, handlers, DTOs, call sites, or source
  spans. It does not claim the code owner is the real production owner.
- Test coverage question: routes to test owner or reviewer when evidence
  raises a coverage, fixture, validation, or test-gap question. It does not
  claim tests passed, failed, are sufficient, or provide release safety.
- Runtime behavior question: routes to service/runtime owner when evidence
  asks for logs, traces, metrics, reproduction, traffic, latency, throughput,
  errors, or runtime interpretation. It states TraceMap cannot prove runtime
  behavior.
- Data/schema question: routes to database owner or architect when evidence
  references schema, migration, SQL-facing, persistence, or data-contract
  surfaces. It does not publish raw SQL, connection details, configuration
  values, or claim data safety.
- Config/deployment question: routes to service/runtime owner, release
  reviewer, or architect when evidence points to checked-in configuration or
  deployment-facing surfaces. It does not prove live deployment state,
  environment values, operational safety, or production configuration.
- Release decision question: routes to release reviewer or manager when a
  reader asks whether evidence supports a release decision. It states TraceMap
  may inform review questions but cannot approve, block, certify, or replace
  release process, tests, code review, source review, service-owner judgment,
  or manager judgment.
- Architecture decision question: routes to architect or code owner when
  evidence raises coupling, dependency, boundary, modernization, ownership
  split, or design-decision questions. It does not claim complete dependency
  understanding or final architecture authority.
- Evidence gap question: routes to reviewer, manager, or the relevant domain
  owner when proof path, rule ID/rule family, evidence tier, coverage label,
  limitation, validation evidence, or owner category is missing. It states
  missing evidence means stop, label the gap, and ask the right human owner;
  it does not mean no impact or a clean conclusion.

### Requirement 5: Distinguish TraceMap evidence from ownership proof

The future page shall make real ownership boundaries visible wherever owner
categories are named.

Acceptance criteria:

- The page states that TraceMap can help route a question to an owner
  category, not identify real teams, people, approval chains, on-call
  rotations, escalation policies, service catalogs, database stewardship, or
  production ownership.
- The page does not use private team names, individual names, private service
  names, private sample names, raw repository names, raw remotes, local paths,
  or hidden org metadata.
- The page does not imply owner categories are complete, authoritative,
  current, or sufficient for approval.
- The page tells readers to confirm actual ownership through their team's
  source of truth before assigning work, approving a release, or escalating an
  operational question.
- The page does not include blame-oriented wording such as `who broke it`,
  `at fault`, `culprit`, `bad owner`, `bad team`, `guilty`, or equivalent
  accusatory phrasing.

### Requirement 6: Preserve public-safe material boundaries

The future surface shall publish only public-safe explanatory copy, metadata,
links, and synthetic handoff examples.

Acceptance criteria:

- The page, metadata, discovery entries, tests, validation fixtures, and
  examples do not publish raw facts, SQLite database content, analyzer logs,
  source snippets, SQL, configuration values, secrets, local paths, repository
  remotes, generated scan directories, private sample names, command output,
  hidden validation details, or credential-like values.
- Public examples are synthetic, authored, or derived from already public-safe
  demo summaries.
- Private repository evidence is described as requiring private review before
  any summary becomes public copy.
- The page does not include local commands that expose private output paths,
  private repository paths, private remotes, ignored generated directories, or
  raw artifact locations.
- Snippet-like handoff wording contains no source code, SQL, configuration
  values, secrets, local paths, private identifiers, or production details.

### Requirement 7: Preserve forbidden-copy boundaries

The future surface shall keep non-claims visible and avoid wording that
upgrades static evidence into unsupported ownership, runtime, release, or
safety conclusions.

Acceptance criteria:

- The page does not claim real org ownership, production ownership proof,
  runtime behavior, production traffic, endpoint performance, release
  approval, release safety, operational safety, AI impact analysis, LLM
  analysis, or complete coverage.
- The page does not imply TraceMap replaces owners, reviewers, managers,
  architects, tests, telemetry, logs, traces, source review, code review,
  release process, incident response, database stewardship, service catalogs,
  on-call systems, or human judgment.
- The page avoids saying a change, endpoint, service, owner, team, release,
  dependency, database reference, configuration, deployment, packet, or
  question is safe, unsafe, approved, blocked, root cause, production-proven,
  complete, ownership-proven, or impacted unless the wording is inside an
  explicit non-claim or forbidden-wording example.
- The page does not describe TraceMap as AI-powered, LLM-powered,
  intelligent impact analysis, automated ownership detection, automated
  release approval, operational assurance, or a production observability tool.
- The page states that owner follow-up should preserve what is known, what is
  partial, what is missing, and what a human owner must decide next.

### Requirement 8: Add discovery metadata and validation expectations

The future implementation shall make the surface discoverable and validate
its claim boundaries.

Acceptance criteria:

- If standalone, the route metadata includes title, description, canonical
  URL, Open Graph fields, and `publicClaimLevel: concept`.
- If standalone, generated route-index metadata and discovery metadata use
  `publicClaimLevel: concept`, and sitemap metadata includes the selected
  route.
- If embedded as a section, the host page discovery metadata either references
  the owner follow-up map or records why section-level discovery is not used.
- Discovery metadata includes bounded limitations and non-claims for real
  org ownership, production ownership, runtime behavior, production traffic,
  endpoint performance, release approval, release safety, operational safety,
  AI/LLM, complete coverage, and replacement of human judgment.
- Required links include current live equivalents for `/team-evidence-handoff/`,
  `/incident-evidence-handoff/`, `/reviewer-quickstart/`, `/questions/`,
  `/questions/objections/`, `/packets/assembly/`, `/manager-packet/`,
  `/proof-paths/`, `/limitations/`, and `/validation/`, except that the
  self-link to an embedded host route is omitted, unless unavailable routes
  are deferred or substituted with rationale in `implementation-state.md`.
- Validation confirms the rendered surface contains `Public claim level:
  concept`, `No public conclusion without evidence`, every required row,
  every required row field, required links, route metadata, discovery/sitemap
  metadata when standalone, and public-safe owner-category wording.
- Validation confirms required internal links resolve in generated site
  output.
- Validation confirms no literal handoff-wording placeholder token remains in
  rendered copy or metadata, including `[static evidence boundary]`,
  `[question]`, `[non-claim]`, `[owner category]`, `[proof path]`,
  `[limitation]`, and `[stop condition]`.
- Validation enforces a rendered body word count between 600 and 1700 words
  for a standalone route. For an embedded section, validation also enforces a
  rendered section word count between 600 and 1700 words because the required
  rows and boundary statements remain mandatory in either placement. The
  section ceiling
  must never be set below the count needed to render all required rows, all
  required row fields, and all boundary statements. If a future placement
  cannot satisfy both the full row/field/boundary requirements and a chosen
  ceiling, the standalone route must be selected and the conflict recorded in
  `implementation-state.md`. Required rows and boundary statements must
  remain independently checked regardless of word count.
- If the full set of required rows, row fields, and boundary statements
  cannot be rendered within 1700 words even in the standalone placement, the
  mandatory content takes precedence. The word-count ceiling is then treated
  as a soft target: the rendered body may exceed 1700 words only to the
  extent required to render all mandatory rows, row fields, and boundary
  statements, and the overflow plus its cause must be recorded in
  `implementation-state.md`. The 600-word floor and all forbidden-claim and
  private-material checks still apply, and validation must flag overflow that
  is not explained by mandatory content.
- Validation checks normalized rendered text, decoded HTML attributes, and
  public metadata for forbidden claims, private/raw material categories,
  credential-like values, local paths, raw remotes, blame language, and
  AI/LLM positioning.
- Forbidden-claim and blame-language checks must distinguish prohibited
  assertive wording from allowed negated or non-claim usage, such as `cannot
  approve`, `does not prove`, and `what TraceMap cannot show`, and from
  explicit forbidden-wording examples. Legitimate non-claim copy must neither
  produce false positives nor cause the check to be disabled wholesale.
- Validation includes an accessibility sanity check consistent with existing
  site patterns, including heading order, meaningful link text, keyboard-safe
  navigation for any interactive controls, and accessible table or list
  structure for the owner map.
- Validation includes negative tests for missing required rows, missing row
  fields, missing public claim label, missing shared principle, missing route
  metadata, missing discovery/sitemap metadata when standalone, unresolved
  required links, forbidden claims, private/raw material, blame language, and
  word count violations.
- Validation includes a negative test that fails when any handoff-wording
  placeholder token remains unsubstituted in rendered copy or metadata.
- Validation includes a negative test for an embedded ceiling set below the
  content minimum required for all rows, row fields, and boundary statements
  when standalone fallback selection is not recorded.
- Future implementation runs `npm run build` from `site/`, focused
  validation, `git diff --check`, `./scripts/check-private-paths.sh`, and
  desktop and mobile browser sanity checks for the selected route or host
  page.
