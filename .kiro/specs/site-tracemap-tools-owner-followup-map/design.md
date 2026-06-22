# Site TraceMap Tools Owner Follow-Up Map Design

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Overview

This design describes a future public-safe owner follow-up map for
`tracemap.tools`. The map should help readers route static-evidence questions
to the next owner category without implying TraceMap knows real organizational
ownership.

The implementation is site-only. It does not add scanner behavior, reducer
behavior, generated artifacts, runtime monitoring, production ownership
discovery, release approval, operational safety, AI impact analysis, LLM
analysis, or autonomous decision-making.

Do not edit site source until spec review findings are handled and
`Readiness` is updated to `ready-for-implementation` in all five spec files.

## Placement Model

Candidate placements:

- `/owners/follow-up/`
- `/review-room/owners/`
- Section on `/team-evidence-handoff/`
- Section on `/questions/`

`/owners/follow-up/` is a plausible standalone route because it is short,
shareable, and clear that the page is about follow-up owner categories, but
this spec does not preselect it. The implementation may choose any candidate
placement if current site information architecture supports the choice and
records the rationale before site source changes begin.

Before editing site source, the implementation must record in
`implementation-state.md`:

- selected placement;
- rejected alternatives;
- whether the surface is standalone or embedded;
- sitemap/discovery handling;
- any substituted links for moved or unavailable routes;
- why the chosen placement does not imply real org ownership detection.

## Content Structure

1. Header: name the owner follow-up map, show `Public claim level: concept`,
   and state `No public conclusion without evidence`.
2. Purpose: explain that TraceMap can help preserve a static evidence question
   and route it to a human owner category, but cannot prove real ownership.
3. Relationship band: distinguish the map from `/team-evidence-handoff/`,
   `/incident-evidence-handoff/`, `/reviewer-quickstart/`, `/questions/`,
   `/questions/objections/`, `/packets/assembly/`, and `/manager-packet/`.
4. Owner follow-up matrix: present required rows with static evidence trigger,
   what TraceMap can show, what TraceMap cannot show, next owner, handoff
   wording, proof path, limitation, and stop condition.
5. Ownership boundary: state that actual teams, people, on-call rotations,
   approval chains, service catalogs, database stewardship, and manager
   authority must come from the reader's organization.
6. Public-safety boundary: list private/raw material categories that cannot
   appear in public copy, metadata, examples, or validation fixtures.
7. Link section: send readers to proof paths, limitations, validation, and
   neighboring handoff/review/question pages.

## Required Matrix Rows

The future implementation must render each row with all eight schema fields.
The text below is model content, not final copy; implementers may shorten it
if the fields and boundaries remain intact and validator-checkable.

### Code Path Question

- Static evidence trigger: public-safe file, symbol, endpoint, handler, DTO,
  call-site, or dependency evidence with rule ID or rule family.
- What TraceMap can show: static references and proof paths that explain why
  code review is needed.
- What TraceMap cannot show: real production owner, runtime behavior,
  complete impact, or blame.
- Next owner: code owner or reviewer.
- Handoff wording: `TraceMap can show the static code reference for this
  question. It cannot show real production ownership or runtime behavior.
  Please route this to the code owner or reviewer with the proof path and
  limitation attached. Stop if no proof path exists.`
- Proof path: link or name a public-safe proof route, validation page,
  public-safe summary, or private review location described without raw
  material.
- Limitation: static references only; no runtime proof, no production
  ownership proof, no complete impact conclusion.
- Stop condition: stop if the proof path is missing, the rule ID or rule
  family is absent, or the coverage label is reduced without a visible
  limitation.

### Test Coverage Question

- Static evidence trigger: coverage label, validation summary, fixture
  reference, test-gap note, or reduced-analysis label.
- What TraceMap can show: where evidence suggests a test or validation
  question should be checked.
- What TraceMap cannot show: whether tests are sufficient, release-safe,
  exhaustive, passed, or failed.
- Next owner: test owner or reviewer.
- Handoff wording: `TraceMap can show the static evidence that raised this
  test question. It cannot show whether test coverage is sufficient. Please
  route this to the test owner or reviewer with the proof path and limitation
  attached. Stop if validation evidence is missing.`
- Proof path: link or name a public-safe validation route, test-summary
  surface, proof path, or private review location described without raw
  command output.
- Limitation: static evidence and public-safe validation summaries only; no
  release safety or exhaustive coverage claim.
- Stop condition: stop if validation evidence, coverage label, limitation, or
  next test owner category is missing.

### Runtime Behavior Question

- Static evidence trigger: static route, dependency, configuration, endpoint,
  or call-path evidence that raises a runtime question.
- What TraceMap can show: which static surface created the runtime follow-up.
- What TraceMap cannot show: logs, traces, metrics, traffic, latency,
  throughput, errors, reproduction, or production behavior.
- Next owner: service/runtime owner.
- Handoff wording: `TraceMap can show the static surface that raised this
  runtime question. It cannot show runtime behavior. Please route this to the
  service/runtime owner with the proof path and limitation attached. Stop if
  runtime evidence is required to answer the question.`
- Proof path: link or name a public-safe static proof route, limitation page,
  or private review location described without telemetry, logs, or traces.
- Limitation: static repository evidence only; runtime evidence must come from
  the service/runtime owner.
- Stop condition: stop if the question asks for runtime behavior, production
  traffic, endpoint performance, outage cause, or operational safety.

### Data/Schema Question

- Static evidence trigger: public-safe schema, migration, persistence,
  data-contract, or SQL-facing reference.
- What TraceMap can show: static data-facing reference and proof path.
- What TraceMap cannot show: raw SQL, production data safety, live schema
  state, connection values, or database ownership proof.
- Next owner: database owner or architect.
- Handoff wording: `TraceMap can show the static data-facing reference for
  this question. It cannot show live schema state or database ownership.
  Please route this to the database owner or architect with the proof path and
  limitation attached. Stop if raw data or configuration values would be
  needed.`
- Proof path: link or name a public-safe proof route, sanitized summary,
  limitation page, or private review location described without raw SQL or
  configuration values.
- Limitation: static data-facing evidence only; no production data, live
  schema, or ownership proof.
- Stop condition: stop if the handoff would require publishing SQL,
  configuration values, secrets, production data, or private identifiers.

### Config/Deployment Question

- Static evidence trigger: public-safe file reference or deployment-facing
  surface label for checked-in configuration, not the configuration value
  itself.
- What TraceMap can show: static config/deployment-facing surface and
  limitation.
- What TraceMap cannot show: live environment values, deployed state,
  operational safety, or production configuration.
- Next owner: service/runtime owner, release reviewer, or architect.
- Handoff wording: `TraceMap can show the static configuration or
  deployment-facing surface. It cannot show live environment state. Please
  route this to the service/runtime owner, release reviewer, or architect with
  the proof path and limitation attached. Stop if production values are needed.`
- Proof path: link or name a public-safe proof route, validation page,
  limitation page, or private review location described without raw
  configuration values.
- Limitation: checked-in static evidence only; no live deployment,
  environment, or operational-safety proof.
- Stop condition: stop if the question depends on live environment values,
  deployment state, secrets, credentials, or operational safety.

### Release Decision Question

- Static evidence trigger: static evidence packet, coverage label, validation
  status, or evidence gap near a release question.
- What TraceMap can show: review inputs and unresolved proof boundaries.
- What TraceMap cannot show: approval, blocking, certification, release
  safety, or replacement for release process.
- Next owner: release reviewer or manager.
- Handoff wording: `TraceMap can show the static review inputs and proof
  boundaries for this release question. It cannot approve or block a release.
  Please route this to the release reviewer or manager with the proof path and
  limitation attached. Stop if release approval is being inferred from static
  evidence.`
- Proof path: link or name a public-safe evidence packet, validation page,
  limitation page, or private release-review location described without raw
  artifacts.
- Limitation: static evidence can inform review questions only; release
  process, tests, code review, source review, and owner judgment remain
  separate.
- Stop condition: stop if the wording claims approved, blocked, certified,
  release-safe, operationally safe, or manager-approved.

### Architecture Decision Question

- Static evidence trigger: dependency, coupling, boundary, modernization,
  cross-service, or ownership-split evidence.
- What TraceMap can show: static relationship and proof path that can inform
  design review.
- What TraceMap cannot show: complete dependency understanding, runtime
  topology, final architecture authority, or org ownership.
- Next owner: architect or code owner.
- Handoff wording: `TraceMap can show the static relationship that raised this
  architecture question. It cannot decide the architecture or prove complete
  dependency scope. Please route this to the architect or code owner with the
  proof path and limitation attached. Stop if final design authority is being
  inferred.`
- Proof path: link or name a public-safe proof route, relationship summary,
  limitation page, or private design-review location described without private
  topology or org metadata.
- Limitation: static relationship evidence only; no runtime topology,
  complete dependency map, or final architecture decision.
- Stop condition: stop if the row would imply complete coverage, authoritative
  ownership, final design approval, or production dependency proof.

### Evidence Gap Question

- Static evidence trigger: missing proof path, rule ID/rule family, evidence
  tier, coverage label, limitation, validation evidence, or owner category.
- What TraceMap can show: gap label and where the evidence trail stops.
- What TraceMap cannot show: absence of impact, clean conclusion, approval,
  or complete coverage.
- Next owner: reviewer, manager, or relevant domain owner category.
- Handoff wording: `TraceMap can show where the evidence trail stops. It
  cannot turn missing evidence into a clean conclusion. Please route this gap
  to the reviewer, manager, or relevant domain owner with the missing field
  named. Stop until the gap is labeled or resolved.`
- Proof path: link or name the public-safe proof route, validation page, gap
  summary, limitation page, or private review location where the trail stops.
- Limitation: evidence is missing, reduced, syntax-only, private-only,
  unavailable, or outside static analysis.
- Stop condition: stop if proof path, rule ID/rule family, evidence tier,
  coverage label, limitation, validation evidence, or next owner category is
  missing.

## Handoff Wording Pattern

Spec-only template: bracket tokens are placeholders for authored copy. They
must not appear in generated site output, metadata, validation fixtures, or
published examples. The negative-placeholder validation test in Requirement 8
must fail if any token below remains unsubstituted in rendered copy.

Use synthetic, public-safe wording in this shape:

`TraceMap can show [static evidence boundary] for [question]. It cannot show
[non-claim]. Please route this to the [owner category] with [proof path] and
[limitation] attached. Stop if [stop condition].`

The wording must not include source snippets, raw SQL, configuration values,
secrets, private identifiers, local paths, raw remotes, generated scan
directory names, command output, production details, blame language, or
credential-like values.

The stop condition must be a concrete statement, not a literal placeholder.
Omitting the stop condition or leaving `[stop condition]` in published copy is
a validation failure.

## Neighbor Route Relationship

- `/team-evidence-handoff/`: receiver-specific packet handoff language.
- `/incident-evidence-handoff/`: incident-adjacent packet carried into the
  next conversation.
- `/reviewer-quickstart/`: five-minute packet inspection for code reviewers.
- `/questions/`: question-to-evidence-surface orientation.
- `/questions/objections/`: skeptical objection-to-evidence handling.
- `/packets/assembly/`: public-safe packet preparation workflow.
- `/manager-packet/`: manager-facing value and summary packet.
- Owner follow-up map: question-to-owner-category routing with handoff wording
  and stop conditions.

If the map is embedded on `/team-evidence-handoff/` or `/questions/`, the
relationship band must omit the self-reference to the host route and instead
frame the distinction as `this owner follow-up section versus the rest of this
page`. The required-links set must omit the self-link to the host route, and
`implementation-state.md` must record the self-host adjustment.

Cross-links should use bounded anchor text and must not imply runtime proof,
production ownership proof, release approval, operational safety, AI/LLM
analysis, or replacement of human judgment.

## Public Safety

The page and metadata must not publish raw facts, SQLite database content,
analyzer logs, source snippets, SQL, configuration values, secrets, local
paths, repository remotes, generated scan directories, private sample names,
command output, hidden validation details, credential-like values, production
service names, private team names, or individual names.

The page may publish authored concept copy, synthetic handoff wording, and
links to existing public-safe pages. Any private repository evidence must
remain private until reviewed and sanitized for public use.

## Validation Design

Future implementation should add a focused validator following neighboring
concept-page validation patterns. The validator should check:

- selected route or host page output exists;
- rendered text contains the claim-level label and shared principle;
- every required row appears;
- every row includes static evidence trigger, what TraceMap can show, what
  TraceMap cannot show, next owner, handoff wording, proof path, limitation,
  and stop condition;
- owner categories are limited to code owner, reviewer, test owner,
  service/runtime owner, database owner, release reviewer, architect, and
  manager unless implementation-state records an approved addition;
- metadata includes title, description, canonical URL, Open Graph fields, and
  `publicClaimLevel: concept` for standalone routes;
- discovery, route-index, and sitemap metadata include the selected route when
  standalone;
- section placement has equivalent discovery handling or a documented
  rationale for not adding section-level discovery;
- required links resolve in generated site output or have recorded
  substitutions/deferments;
- rendered body word count is 600 to 1700 words for standalone routes and
  600 to 1700 words for embedded sections because required rows and boundary
  statements remain mandatory in either placement, with the embedded ceiling
  treated as a derived maximum that cannot be set below the content needed for
  all required rows, row fields, and boundary statements;
- if embedded placement cannot fit all required rows, row fields, and
  boundary statements within the chosen ceiling, the standalone route must be
  selected and the conflict recorded in `implementation-state.md`. Required
  rows and boundary statements must remain independently checked regardless
  of word count;
- if the full set of required rows, row fields, and boundary statements
  cannot be rendered within 1700 words even in standalone placement, mandatory
  content takes precedence. The word-count ceiling becomes a soft target only
  for mandatory-content overflow, the overflow and cause must be recorded in
  `implementation-state.md`, the 600-word floor still applies, and validation
  must flag overflow not explained by mandatory content;
- accessibility sanity checks follow existing site patterns for heading order,
  meaningful link text, keyboard-safe navigation for any interactive controls,
  and accessible table or list structure for the owner map;
- owner-category checks allow only the eight named categories, compound lists
  made entirely from those categories, and the explicit meta-phrase `relevant
  domain owner category` used for evidence-gap routing. Any new named owner
  category requires an `implementation-state.md` entry;
- no literal handoff-wording placeholder token remains in rendered copy or
  metadata, including `[static evidence boundary]`, `[question]`,
  `[non-claim]`, `[owner category]`, `[proof path]`, `[limitation]`, and
  `[stop condition]`;
- forbidden claim checks cover normalized rendered text, decoded HTML
  attributes, route metadata, discovery metadata, and sitemap text;
- forbidden-claim and blame-language checks distinguish prohibited assertive
  wording from allowed negated or non-claim usage, such as `cannot approve`,
  `does not prove`, and `what TraceMap cannot show`, and from explicit
  forbidden-wording examples, so legitimate non-claim copy neither produces
  false positives nor causes the check to be disabled wholesale;
- private/raw material checks cover raw HTML, decoded HTML attributes,
  metadata, discovery entries, tests, and visible text;
- blame-language checks cover visible text and metadata;
- negative tests fail for missing rows, missing row fields, missing required
  labels, missing links, missing metadata, forbidden claims, private/raw
  material, blame language, AI/LLM positioning, unsubstituted handoff
  placeholders, word count drift, and an embedded ceiling set below the
  content minimum for all required rows, row fields, and boundary statements
  when standalone fallback is not recorded.

Future implementation should run `npm run build` from `site/`, the focused
validator or full site validation entrypoint, `git diff --check`,
`./scripts/check-private-paths.sh`, and desktop/mobile browser sanity checks
for the selected route or host page.
