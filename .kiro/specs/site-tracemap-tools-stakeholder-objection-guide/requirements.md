# Site TraceMap Tools Stakeholder Objection Guide Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Define a future public-safe objection guide page or section for
`tracemap.tools`. The guide should help managers, reviewers, engineers, and
skeptical stakeholders ask hard questions and get bounded answers: what
TraceMap evidence can support, what it cannot support, and what next owner or
runtime evidence is needed.

This is a spec-only public site phase. It does not implement site source,
scanner behavior, reducer behavior, generated artifacts, validation scripts,
runtime telemetry, AI/LLM analysis, embeddings, vector databases, prompt
classification, autonomous approval, or public copy changes.

The guide is not a defensive FAQ. It should make skepticism useful by turning
objections into evidence checks, stop conditions, owner handoffs, and visible
limitations.

## Shared Site Principle

No public conclusion without evidence.

Future implementation must include visible `Public claim level: concept` and
visible `No public conclusion without evidence`.

## Claim Level Rationale

The future surface starts at `Public claim level: concept` because it is an
orientation and objection-handling guide over existing public evidence
surfaces. It does not publish new demo evidence and does not itself prove a
new scanner, reducer, runtime, release, or operational capability.

Do not upgrade the page or section to `demo` merely because some supporting
links point to demo-backed routes. A future claim-level upgrade requires a
separate evidence-backed decision recorded in this spec's
`implementation-state.md`.

## Candidate Placement

Candidate placements for future implementation are:

- `/objections/`
- `/questions/objections/`
- A section on `/questions/`
- A section on `/manager-faq/`

Future implementation must choose the final placement and record rejected
alternatives in `implementation-state.md` before changing site source.

## Relationship To Existing Public Surfaces

The objection guide is a bounded response surface. It complements, but must
not replace or duplicate, these existing or candidate surfaces when present:

- `/questions/`: routes broad stakeholder questions to evidence surfaces. The
  objection guide answers skeptical challenge shapes and names stop
  conditions.
- `/manager-faq/`: answers common manager questions. The objection guide is
  more operationally structured: objection, evidence to check, stop condition,
  next owner, limitation, and supporting route.
- `/limitations/`: states site-wide non-claims and coverage limits. The
  objection guide applies those limits to concrete objections.
- `/static-vs-runtime/`: separates static evidence from runtime telemetry. The
  objection guide links to it when runtime behavior, traffic, performance, or
  production signals are requested.
- `/review-claim-checklist/`: tells reviewers whether a claim can be repeated.
  The objection guide tells stakeholders what answer shape is safe before a
  claim can be repeated.
- `/proof-paths/tour/`: teaches how to follow a proof path. The objection
  guide sends readers there when the objection requires proof-path literacy.
- `/manager-demo-script/` or `/demo/manager-script/`: guides a live
  conversation. The objection guide is a reference surface for hard questions,
  not a presenter script.

If any route has moved or is unavailable at implementation time, the
implementation must select the closest live public-safe equivalent or defer
the link, then record the decision and rationale in `implementation-state.md`.
Dead links are not acceptable. Supporting routes may extend beyond this
relationship list when a row needs a better proof target, but every
supporting route must be verified or recorded as deferred.

## Claim Boundaries

The future surface may explain deterministic static evidence vocabulary:
rule IDs or rule families, evidence tiers, coverage labels, limitations,
public claim levels, proof paths, generated public-safe summaries, and owner
handoffs.

The future surface must not claim or imply:

- runtime behavior proof;
- production traffic visibility;
- endpoint performance insight;
- outage cause;
- release safety;
- operational safety;
- complete coverage;
- AI analysis, LLM analysis, embeddings, vector databases, or prompt
  classification in the core scanner or reducer;
- autonomous approval;
- release approval;
- absence-of-impact proof;
- replacement of tests, code review, source review, runtime observability, or
  human judgment.

The future surface must not publish raw `facts.ndjson`, raw `index.sqlite`,
analyzer logs, raw source snippets, raw SQL, config values, secrets, local
paths, raw repository remotes, generated scan directories, private sample
names, raw command output, hidden validation details, or credential-like
values.

The future surface must not use blame language around vendors, consultants,
teams, service owners, reviewers, or code quality. Objections should be framed
as useful checks, not accusations.

## Requirements

### Requirement 1: Publish a bounded objection guide in a future phase

The future implementation shall publish a concept-level public objection guide
page or section that helps stakeholders challenge TraceMap claims without
overstating static evidence.

Acceptance criteria:

- The rendered surface says `Public claim level: concept`.
- The rendered surface states `No public conclusion without evidence`.
- The implementation chooses one final placement from `/objections/`,
  `/questions/objections/`, a section on `/questions/`, a section on
  `/manager-faq/`, or a recorded equivalent if site information architecture
  has changed.
- The implementation records the final placement, rejected alternatives, and
  rationale in `implementation-state.md`.
- The rationale explains why the guide is an objection-to-evidence handoff,
  not a proof claim, FAQ replacement, limitation replacement, release gate, or
  runtime workflow.
- If implemented as a standalone route, metadata, sitemap metadata, and
  discovery metadata use `publicClaimLevel: concept`.
- If implemented as a section, the host page metadata remains concept-level or
  more conservative and does not imply a new shipped capability.
- The surface uses existing static site layout, navigation, metadata,
  accessibility, and validation patterns.
- The surface introduces no runtime service, telemetry collection, analytics
  dependency, form, local scanner invocation, generated evidence artifact, or
  client-side state requirement.

### Requirement 2: Include the required objection categories

The future surface shall include every required objection category with a
bounded answer and a clear follow-up owner.

Acceptance criteria:

- Include `Does this prove runtime behavior?`
- Include `Can I use this for release approval?`
- Include `Does this show production traffic or endpoint performance?`
- Include `Is this AI analysis?`
- Include `Does missing evidence mean no impact?`
- Include `Can I share raw artifacts?`
- Include `Who owns the next answer?`
- Include `What do we do under reduced coverage?`
- Each objection is phrased as a skeptical stakeholder question, not as a
  TraceMap feature name.
- The guide may include additional objections, but additional rows must follow
  the same row schema and claim boundaries.

### Requirement 3: Require a complete objection row schema

Each objection shall preserve the evidence fields a reader needs before
acting on or repeating an answer.

Acceptance criteria:

- Each objection includes: safe short answer, evidence to check, stop
  condition, next owner, public claim level, limitation/non-claim, and a link
  to a supporting public route.
- `safe short answer` uses bounded language such as `can orient`, `can show
  static evidence`, `needs runtime evidence`, `needs owner review`, `cannot
  support that conclusion`, or `stop and label coverage`.
- `evidence to check` names public-safe evidence categories such as rule ID or
  rule family, evidence tier, coverage label, proof path, limitation,
  scan-manifest status, public-safe generated summary, validation page, or
  linked concept/demo page.
- `stop condition` names the point where TraceMap evidence is insufficient,
  private-only, raw-only, partial, reduced, unavailable, or outside static
  evidence boundaries.
- `next owner` identifies a role category, such as service owner, release
  owner, runtime observability owner, test owner, reviewer, manager, security
  owner, or TraceMap site owner. It does not name private people or teams.
- `public claim level` is `concept` by default. Any stronger row-level claim
  requires exact public-safe evidence and an implementation-state decision.
- `limitation/non-claim` states what the answer must not imply.
- `supporting public route` resolves in generated output or is deferred with a
  recorded rationale; no dead links.

### Requirement 4: Preserve objection category semantics

The future guide shall answer each required objection with the correct
boundary and owner handoff.

Acceptance criteria:

- Runtime behavior objection: answer that TraceMap can orient static
  repository evidence only; runtime behavior needs logs, traces, metrics,
  telemetry, tests, runtime reproduction, or service-owner interpretation.
- Release approval objection: answer that TraceMap evidence may inform review
  questions, but cannot approve, block, certify, or replace release process,
  test results, code review, source review, service-owner judgment, or release
  owner decisions.
- Production traffic or endpoint performance objection: answer that TraceMap
  does not show production traffic, live request paths, latency, throughput,
  error rates, or endpoint performance; runtime observability owners must
  provide that evidence.
- AI analysis objection: answer that core TraceMap scanner/reducer claims are
  deterministic and rule-backed, not LLM, embedding, vector database, or
  prompt-classification analysis.
- Missing evidence objection: answer that missing evidence is a gap or
  unknown, not proof of no impact or no dependency; the row must route to
  coverage labels, limitations, and next owner review.
- Raw artifact sharing objection: answer that public pages may link to
  public-safe summaries and proof routes, but raw facts, SQLite, logs,
  snippets, SQL, configs, secrets, local paths, remotes, generated scan
  directories, private sample names, raw command output, hidden validation
  details, and credential-like values must not be published.
- Next answer ownership objection: answer that TraceMap can identify the kind
  of follow-up owner but does not assign accountability, service ownership,
  incident command, release authority, or organization decisions.
- Reduced coverage objection: answer that reduced or partial coverage must
  stay visible; conclusions must be downgraded, blocked, or routed to owners
  until adequate evidence exists.

### Requirement 5: Keep tone calm, professional, and non-defensive

The future guide shall make skepticism useful without blaming people or
systems.

Acceptance criteria:

- The guide uses calm, professional wording and avoids defensive, dismissive,
  salesy, fear-based, or blame-oriented language.
- Objections are treated as valid review inputs.
- Safe answers acknowledge limits before giving a proof route.
- The guide does not shame stakeholders for asking release, runtime,
  production, AI, or coverage questions.
- The guide avoids vendor, consultant, team, owner, reviewer, or code-quality
  blame.
- The guide avoids competitive positioning and does not name observability,
  AI, release, or code-review vendors unless a public-safe route requires a
  generic comparison. Generic categories such as telemetry, logs, traces,
  tests, code review, and release process are allowed.

### Requirement 6: Preserve public-safe artifacts and links

The future guide shall publish only public-safe copy and public-safe links.

Acceptance criteria:

- Supporting routes point only to public pages, public-safe generated
  summaries, documentation, validation pages, limitation pages, demo pages, or
  proof-path pages.
- Supporting routes do not link directly to raw `facts.ndjson`, raw
  `index.sqlite`, analyzer logs, raw source snippets, raw SQL, config values,
  secrets, local paths, raw remotes, generated scan directories, private
  sample names, raw command output, hidden validation details, or
  credential-like values.
- Artifact names may appear only as local-output categories or non-shareable
  artifact examples, not as public proof content.
- Rule IDs, evidence tiers, file paths, line spans, commit SHA, extractor
  versions, coverage labels, and snippet hashes appear only when public-safe
  and backed by an existing public proof path or public-safe summary.
- The guide never asks a public reader to inspect local-only files or private
  samples.
- Link anchor text must not imply runtime proof, production proof, endpoint
  performance proof, release approval, operational safety, complete coverage,
  AI analysis, or absence-of-impact proof.

### Requirement 7: Add discovery and sitemap metadata safely

The future implementation shall make a standalone route discoverable without
inflating the claim level.

Acceptance criteria:

- If standalone, route metadata includes title, description, canonical path,
  Open Graph fields, and `publicClaimLevel: concept` using existing site
  patterns.
- If standalone, sitemap and route-index metadata include the route when
  comparable public pages are indexed there.
- If standalone, discovery metadata includes the route using the current
  discovery schema at implementation time.
- Discovery metadata summary, limitations, non-claims, preferred proof path,
  and hint category preserve concept-level boundaries.
- If implemented as a section, add a stable in-page anchor and record whether
  discovery metadata can safely deep-link to that anchor under the current
  schema.
- Metadata and discovery copy include no forbidden runtime, production,
  endpoint-performance, release-safety, operational-safety, AI/LLM,
  complete-coverage, autonomous-approval, absence-of-impact, or raw/private
  artifact claims.

### Requirement 8: Validate the future implementation

The future implementation shall run focused public-site validation and record
results in this spec's implementation-state note.

Acceptance criteria:

- Run `git diff --check`.
- Run `./scripts/check-private-paths.sh`.
- Run `npm test` from `site/`.
- Run `npm run validate` from `site/`.
- Run `npm run build` from `site/`.
- Run desktop and mobile browser sanity checks for any route, layout, or
  interaction change.
- Validate that rendered copy includes `Public claim level: concept`.
- Validate that rendered copy includes `No public conclusion without
  evidence`.
- Validate all eight required objection categories are present.
- Validate every objection row includes safe short answer, evidence to check,
  stop condition, next owner, public claim level, limitation/non-claim, and a
  supporting public route.
- Validate supporting links resolve in generated output, or recorded deferrals
  exist for unavailable candidate routes.
- Validate standalone route metadata, sitemap metadata, and discovery metadata
  when a standalone route is chosen.
- Validate section anchor and host-page metadata behavior when a section is
  chosen.
- Validate rendered body copy stays within 900 to 2,400 visible words unless a
  spec amendment changes the bound. Visible word count means rendered body
  prose and objection row cell content, excluding page-level navigation,
  breadcrumbs, site headers, site footers, metadata blocks, and row-field
  label headers. The lower bound ensures all eight objection rows have room
  for complete fields; the upper bound keeps the guide reference-grade rather
  than exhaustive.
- Validate that forbidden private or raw material listed in this spec does not
  appear in rendered copy, metadata, sitemap output, discovery output, tests,
  fixtures, or generated pages except inside explicit non-shareable artifact
  examples.
- Validate forbidden runtime, production, endpoint-performance, outage-cause,
  release-safety, operational-safety, complete-coverage, AI/LLM, embedding,
  vector database, prompt-classification, autonomous-approval, and
  absence-of-impact wording does not appear as a claim.
- A suitable starting forbidden-positioning pattern is
  `/\b(AI[- ]?powered|AI impact analysis|LLM[- ]?powered|LLM analysis|machine learning impact analysis|artificial intelligence impact analysis|embedding[- ]?based impact|vector database impact|prompt[- ]?classification|prompt[- ]?classified impact|intelligent impact|automated release approval|release approval|operational assurance|runtime proof|production proven|absence of impact|no impact proven)\b/i`.
  Apply this pattern outside explicit objection titles, safe-answer,
  stop-condition, non-claim, limitation, and other objection-boundary contexts
  so the required objection questions themselves do not fail validation.
- A suitable starting overclaim pattern is
  `/\b(proves?|guarantees?|certifies?|approves?|blocks?|safe to release|validated for release|approved for release|deployment[- ]safe|production[- ]traffic|endpoint[- ]performance|runtime[- ]behavior|outage[- ]cause|root[- ]cause|complete[- ]coverage|no impact|not impacted|autonomous[- ]approval)\b/i`,
  applied to rendered guide body copy outside explicit non-claim, limitation,
  or objection-boundary contexts.
- Focused validation should explicitly allow the eight verbatim required
  objection titles while still rejecting the same phrases when they appear as
  unsupported claims elsewhere.
- Manual review of allowed boundary wording is recorded in
  `implementation-state.md` before merging any implementation PR.

### Requirement 9: Keep spec and implementation state current

The future implementation shall keep this spec's task and state files aligned
with actual work.

Acceptance criteria:

- `tasks.md` implementation tasks remain unchecked until implementation work
  begins.
- Implementation tasks are checked only after the corresponding site source,
  validation, metadata, and browser sanity work is complete.
- Spec review tasks are checked only after the requested review command ran
  or the exact unavailable-tool/model error is recorded.
- `implementation-state.md` records current branch, target base, scope
  decisions, placement decisions, rejected alternatives, validation results,
  review results, PR-loop outcomes, oddities, residual risk, and follow-up
  items.
- If a Kiro review identifies Medium or higher findings, the spec either
  patches them and records the rerun result, or records an evidence-backed
  disposition explaining why no patch is needed.
