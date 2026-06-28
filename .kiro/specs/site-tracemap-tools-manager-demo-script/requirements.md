# Site TraceMap Tools Manager Demo Script Requirements

Status: implemented
Readiness: implemented
Public claim level: concept

## Summary

Define a future public-site page or section that gives Joe a bounded
manager/teammate demo script for showing `tracemap.tools` without overclaiming
runtime or product completeness. The script should say what to open, what to
say, what proof to point at, what to avoid claiming, when to stop, and what to
hand off afterward.

This is a spec-only site phase. It does not implement site code, scanner
behavior, reducer behavior, generated artifacts, validation scripts, runtime
telemetry, AI/LLM analysis, embeddings, vector databases, prompt
classification, or public copy changes.

## Shared Site Principle

No public conclusion without evidence.

## Claim Level Rationale

The future script starts at `Public claim level: concept` because it is a
bounded presentation guide over existing public pages. It does not publish new
demo evidence and does not itself prove scanner or reducer behavior. Future
implementation must include visible `Public claim level: concept` and visible
`No public conclusion without evidence` text on the rendered surface.

Do not upgrade the script to `demo` merely because some links point to
demo-backed pages. A future claim-level upgrade would require a separate
evidence-backed decision recorded in this spec's `implementation-state.md`.

## Placement Decision

The preferred future placement is `/demo/manager-script/`.

Rationale:

- It is clearly a demonstration aid, not a new manager product claim.
- It can link to `/demo/runbook/` for operator procedure while staying focused
  on what a person says during a short conversation.
- It avoids making the script look like the canonical manager summary, FAQ, or
  packet.

Rejected alternatives:

- `/demo/briefing/`: concise, but less explicit that this is a script with
  stop conditions and safe answer shapes.
- Section on `/demo/runbook/`: lower route surface area, but it risks burying
  the human talking script inside an operator checklist.
- Section on `/manager-brief/`: too close to stakeholder positioning and could
  make the script look like a higher-level product claim.
- Replacing or merging with `/manager-packet/`: the packet explains value; the
  script choreographs a bounded live walkthrough.

Future implementation must verify that the selected route does not collide
with current site information architecture. If implementation chooses a
different candidate placement, it must record the final choice, rejected
alternatives, and rationale in `implementation-state.md`.

## Relationship To Existing Public Surfaces

The manager demo script is a presenter guide. It complements but does not
replace these existing or candidate public pages:

- `/manager-brief/` gives high-level manager framing; this script gives the
  words and route order for a live conversation.
- `/manager-faq/` answers skeptical stakeholder questions; this script gives
  safe answer shapes and points questioners to proof routes.
- `/manager-packet/` packages manager-facing value; this script tells Joe
  what to open, say, avoid, and hand off.
- `/demo/runbook/` is an operator checklist for running and sharing the demo;
  this script is a human conversation guide for an already-public site tour.
- `/questions/` routes questions to evidence surfaces; this script includes a
  small set of likely questions and where to route them during the demo.
- `/use-cases/` explains use-case orientation; this script avoids presenting
  a use case as proof of runtime or release readiness.
- `/capabilities/` explains deterministic capabilities and boundaries; this
  script uses it as one stop in a tour.
- Blog pages can provide narrative context; this script must stay procedural,
  bounded, and evidence-linked.

## Requirements

### Requirement 1: Publish a bounded manager demo script surface

The future implementation shall publish a concept-level public page or section
that Joe can use while showing `tracemap.tools` to a friend, teammate, or
manager.

Acceptance criteria:

- The rendered surface says `Public claim level: concept`.
- The rendered surface states `No public conclusion without evidence`.
- The surface says it is a bounded demo script, not a product capability
  proof, runtime diagnostic, production incident workflow, release approval
  checklist, or completeness claim.
- The surface includes what to say, which existing pages to open, what proof
  to show, what to avoid claiming, and where to stop.
- The surface uses existing static site layout, navigation, metadata, and
  accessibility patterns.
- The surface does not introduce a runtime service, client-side data fetch,
  analytics dependency, form, local scanner invocation, or new generated
  evidence artifact.
- The implementation records the final route or section placement, source
  files, validation, and follow-up items in this spec's
  `implementation-state.md`.

### Requirement 2: Include required script blocks

The script shall include all required blocks so the presenter has a complete
bounded conversation path.

Acceptance criteria:

- Opening context block: explains why the demo exists, what it can help with,
  and the claim boundary before any proof is shown.
- 2-minute tour block: gives a short route sequence and concise talking points
  for a low-context manager or teammate.
- 5-minute proof walkthrough block: gives a slower route sequence that follows
  one claim through evidence surfaces, proof-source mapping, limitations, and
  validation.
- Manager questions and safe answer shapes block: includes common manager
  questions and bounded answers that route to public evidence or limitations.
- Engineer questions and proof routes block: includes common engineer
  questions and routes them to proof paths, source catalog, demo result,
  validation, limitations, and static-versus-runtime boundaries.
- Stop conditions block: tells the presenter when to stop showing or stop
  repeating a claim.
- Follow-up handoff block: tells the presenter what public links or summary
  shape to send after the demo.
- Non-claims block: lists claims the presenter must not make.

### Requirement 3: Use only verified existing public pages in the demo route

The script shall define a required route sequence, but the future
implementation must verify each target exists before linking to it.

Acceptance criteria:

- The intended route sequence includes `/`, `/capabilities/`, `/proof-paths/`,
  `/proof-source-catalog/`, `/demo/result/`, `/demo/runbook/`, `/questions/`,
  `/limitations/`, `/validation/`, and `/static-vs-runtime/`.
- The implementation verifies each route resolves in generated site output
  before adding the link.
- If a route is unavailable at implementation time, the implementation either
  blocks the script, selects the closest public equivalent, or removes that
  stop and records the substitution or deferral in `implementation-state.md`.
- Before the script tells a presenter to reference a named evidence field on a
  route, such as rule ID, rule family, evidence tier, coverage label, proof
  path, source mapping, limitation, or validation state, the implementation
  verifies that field is visibly rendered on the target page or softens the
  wording to `where present`.
- The script never links to private routes, local filesystem paths, raw
  generated scan directories, raw artifacts, private remotes, or hidden
  validation details.
- Link anchor text preserves the target page's claim level and does not imply
  runtime proof, production proof, release approval, operational safety, or
  complete coverage.
- The implementation determines each target page's claim level from the
  rendered page label or the site's page/discovery metadata, then records any
  discrepancy in `implementation-state.md` before publishing the link.

### Requirement 4: Explain value without overclaiming

The script may explain why TraceMap is useful for a manager or teammate, but it
must keep value tied to static evidence and explicit limits.

Acceptance criteria:

- The script may describe value as faster orientation, fewer vague review
  conversations, evidence-backed handoff, and clearer limitations.
- The script may say TraceMap helps teams inspect deterministic static
  evidence, rule IDs, evidence tiers, coverage labels, proof paths, generated
  public-safe summaries, and limitations.
- The script may say a demo can help a conversation move from vague concern to
  specific evidence questions.
- The script must not claim production incident diagnosis, runtime proof,
  release approval, complete dependency understanding, endpoint performance
  insight, operational safety, or AI analysis.
- The script must not imply TraceMap replaces managers, engineers, service
  owners, reviewers, architects, tests, telemetry, logs, traces, incident
  process, release process, or human judgment.

### Requirement 5: Preserve language and artifact boundaries

The script shall prevent unsupported claims, blame language, and private or raw
material from appearing in public copy.

Acceptance criteria:

- The script avoids blame language around vendors, consultants, teams,
  service owners, incident participants, or reviewers.
- The script does not claim runtime behavior proof, production traffic,
  endpoint performance, outage cause, release safety, operational safety,
  complete coverage, complete dependency understanding, AI/LLM impact analysis,
  embeddings, vector databases, or prompt classification.
- The script does not publish raw `facts.ndjson`, raw SQLite, raw logs, raw
  source snippets, raw SQL, config values, secrets, local paths, repository
  remotes, generated scan directories, private names, or hidden validation
  details.
- Artifact names such as `facts.ndjson`, `index.sqlite`, `report.md`, and
  `logs/analyzer.log` may appear only as local-only artifact types or scanner
  output families, not as public proof content.
- The script does not use `impacted`, `safe`, `unsafe`, `approved`, `blocked`,
  `root cause`, `validated for release`, or `production proven` as conclusions
  unless they appear only inside explicit non-claim or red-flag guidance.
- The script keeps gaps, partial analysis, reduced coverage, and limitations
  visible rather than smoothing them into clean conclusions.

### Requirement 6: Include safe answer shapes for manager questions

The script shall help a presenter answer manager questions without turning the
demo into unsupported claims.

Acceptance criteria:

- Manager answers use bounded verbs such as `show`, `inspect`, `follow`,
  `compare`, `route`, `handoff`, `ask`, `verify`, and `escalate`.
- Manager answers avoid unsupported verbs such as `prove`, `guarantee`,
  `certify`, `approve`, `resolve`, `diagnose`, and `replace` except when
  explicitly denying those claims.
- Required manager question families include value, trust, completeness,
  release decision, production behavior, incident use, team handoff, and what
  to do next.
- Each answer names the proof or limitation route to open, or tells the
  presenter to stop and not answer from TraceMap alone.
- Answers make clear that managers can use the demo to ask sharper questions,
  not to settle ownership, priority, release, or operational decisions.

### Requirement 7: Include engineer proof routes

The script shall give engineers concrete proof routes without exposing local or
raw artifacts.

Acceptance criteria:

- Engineer questions include rule IDs, evidence tiers, coverage labels,
  source mapping, demo result status, gaps, static-versus-runtime boundaries,
  validation, and raw artifact boundaries where those fields are visibly
  present on the verified public route.
- Each engineer answer routes to public pages such as `/proof-paths/`,
  `/proof-source-catalog/`, `/demo/result/`, `/validation/`,
  `/limitations/`, or `/static-vs-runtime/` after implementation verifies the
  route exists.
- Engineer answers explain that raw facts, SQLite, logs, source, SQL, config,
  secrets, local paths, remotes, generated scan directories, and private names
  stay out of public copy.
- The proof routes do not imply that public pages contain complete source,
  complete dependency graphs, private repository facts, runtime telemetry, or
  hidden validation details.

### Requirement 8: Include stop conditions and follow-up handoff

The script shall make stopping and follow-up explicit.

Acceptance criteria:

- Stop conditions include missing rule ID or rule family, missing evidence
  tier, missing coverage label, missing limitation, unavailable route,
  unsupported runtime or production question, private/raw material risk, and
  audience pressure to make a release or incident claim.
- The stop guidance says to link to `/limitations/`, `/validation/`, or
  `/static-vs-runtime/` instead of improvising a stronger answer.
- The follow-up handoff includes a short public-link bundle shape rather than
  raw artifacts or local paths.
- The handoff reminds recipients that the script is concept-level and that any
  production, release, incident, or runtime question needs the appropriate
  owner, telemetry, tests, logs, traces, or release process.
- The handoff does not include raw facts, SQLite, logs, source snippets, SQL,
  config, secrets, local paths, remotes, generated scan directories, private
  names, or hidden validation details.

### Requirement 9: Add metadata and discovery safely

The future implementation shall make the script discoverable without implying
product completeness.

Acceptance criteria:

- If implemented as a standalone route, the route appears in the site's page
  metadata with `publicClaimLevel: concept`.
- If comparable public pages appear in sitemap output, the standalone route
  appears in sitemap metadata with concept-level title and description.
- If comparable public pages appear in discovery metadata, the standalone
  route appears in discovery metadata with `publicClaimLevel: concept`.
- The implemented surface has at least one inbound discovery link from
  `/demo/`, `/demo/runbook/`, `/manager-brief/`, or the selected parent page,
  unless implementation records a deliberate direct-navigation-only decision
  with rationale.
- The chosen inbound-link source must be verified in generated site output
  before the link is added, using the same public-route verification discipline
  as the demo route sequence.
- Page title, description, and social metadata describe a bounded manager demo
  script for deterministic static evidence.
- Metadata does not claim runtime proof, production proof, endpoint
  performance, release safety, operational safety, incident diagnosis, AI/LLM
  analysis, complete dependency understanding, or complete coverage.
- Social title should stay at or below 70 characters and social description at
  or below 160 characters unless current site conventions require different
  limits.

### Requirement 10: Validate the future implementation

The future implementation shall run focused validation and record results in
this spec's implementation-state note.

Acceptance criteria:

- Run `git diff --check`.
- Run `./scripts/check-private-paths.sh`.
- Run `npm test` from `site/`.
- Run `npm run validate` from `site/`.
- Run `npm run build` from `site/`.
- Validate rendered copy includes `Public claim level: concept`.
- Validate rendered copy includes `No public conclusion without evidence`.
- Validate every required script block appears.
- Validate required links resolve or that route substitutions are recorded.
- Validate metadata, discovery metadata, and sitemap metadata if standalone.
- Validate forbidden runtime, production, release-safety, operational-safety,
  endpoint-performance, incident-diagnosis, complete-coverage, complete
  dependency, and AI/LLM claims are absent from rendered HTML, metadata,
  discovery output, sitemap output, tests, fixtures, and generated pages,
  except where they appear only inside explicit non-claim or red-flag sections
  of rendered body copy.
- Validate private/raw material is absent from rendered HTML, metadata,
  discovery output, sitemap output, tests, fixtures, and generated pages.
- Validate visible page copy stays between 900 and 2,400 words unless a future
  implementation records why the current site pattern requires a different
  bound.
- Run desktop and mobile browser sanity checks for layout, link visibility,
  text wrapping, and absence of horizontal overflow.

## Spec-Only Validation

This spec-only packet shall be validated with:

- `git diff --check`.
- `./scripts/check-private-paths.sh`.
- Focused text checks over this spec packet for required status labels,
  required files, required route references, forbidden unsupported claims, and
  readiness state.
- Kiro spec review through `scripts/kiro-review.mjs` with
  `claude-opus-4.8` and `claude-sonnet-4.6` when available. If a model or the
  review tool is unavailable, record the exact command and error in
  `implementation-state.md`.
