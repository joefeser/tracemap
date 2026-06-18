# Site TraceMap Tools Static Vs Runtime Telemetry Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Define a future public-safe site page or section that explains how TraceMap's
deterministic static evidence complements runtime observability tools such as
logs, traces, APM, telemetry, incident dashboards, and production metrics.

The page is for engineers, reviewers, managers, and incident-adjacent teams who
need to separate code-evidence questions from operational questions before,
during, or after an incident or review workflow. TraceMap can orient static
repository questions. Runtime observability tools remain the source for live
behavior, traffic, performance, incident timelines, and production signals.

This is a site-spec-only packet. It does not implement site code, scanner code,
reducer behavior, telemetry ingestion, runtime integrations, or generated
artifact changes.

## Shared Site Principle

No public conclusion without evidence.

## Claim Level Rationale

The future page should use `Public claim level: concept` because it is an
explanatory positioning page, not a shipped runtime integration or demo with a
checked-in public proof path. The repo has existing public-safe proof surfaces
for deterministic static evidence shapes, artifact families, coverage labels,
and limitations, but this specific static-versus-runtime explanation has not
shipped as a dedicated public route or public demo result.

Do not upgrade this page to `demo` unless a future phase adds checked-in,
public-safe demo material that supports the exact examples on the page without
publishing raw scanner artifacts, private sample names, raw runtime data, or
vendor-specific integration claims.

## Audience

- Engineers who need to ask what code references, contracts, routes, packages,
  configuration surfaces, or SQL-facing surfaces are visible in a repository
  snapshot.
- Reviewers who need rule IDs, evidence tiers, file paths, line spans, commit
  SHA, extractor versions, coverage labels, and limitations before deciding
  what needs human review.
- Managers who need a bounded explanation of what static evidence can answer
  before or after runtime tools answer operational questions.
- Incident-adjacent teams who need to hand static code evidence to runtime,
  service-owner, testing, or incident-response owners without overstating what
  TraceMap proves.

## Core Message

TraceMap and runtime observability answer different classes of questions.
TraceMap can produce deterministic static evidence from a repository snapshot:
what code surfaces were found, which rule produced the fact, what evidence tier
and coverage label apply, where the relevant file and line span live, which
commit was scanned, and what limitations remain.

Runtime observability tools answer operational questions: what happened in
production, which requests ran, how systems behaved under traffic, what
latency, error, metric, trace, log, dashboard, or alert signal exists, and what
the incident timeline shows.

The page should frame TraceMap as a companion to runtime observability, tests,
service-owner review, incident response, and release process. It must never
present TraceMap as a replacement for those systems.

## Requirements

### Requirement 1: Publish a bounded concept explanation in a future phase

The future site implementation shall publish a concept-level page or section
that compares deterministic static evidence with runtime telemetry while
preserving TraceMap's public claim boundaries.

Acceptance criteria:

- The implementation chooses either a standalone route such as
  `/static-vs-runtime/` or a section on an existing use-case or limitations
  page, then records the route or placement decision in this spec's
  `implementation-state.md`.
- The page or section says `Public claim level: concept`.
- The page or section states the shared site principle.
- The page or section explains that TraceMap provides deterministic static
  repository evidence, not live operational telemetry.
- The page or section introduces no runtime data collection, runtime agent,
  telemetry ingestion, live dashboard, incident automation, vendor integration,
  client-side tracking, private dataset dependency, or generated local artifact
  dependency.
- If implemented as a standalone route, the route metadata, sitemap metadata,
  and discovery metadata preserve the `concept` claim level.
- The page or section does not enter primary navigation unless a future site
  information-architecture review explicitly chooses that placement and records
  why the concept-level route belongs there.

### Requirement 2: Separate static evidence questions from runtime questions

The page shall help readers decide which questions TraceMap can orient and
which questions belong to runtime observability or other operational systems.

Acceptance criteria:

- The page includes a scannable comparison that distinguishes static evidence
  questions from runtime observability questions.
- Static evidence examples include repository snapshot, commit SHA, rule IDs,
  evidence tiers, file paths, line spans, extractor versions, coverage labels,
  limitations, endpoint or route references, contract surfaces, package
  references, configuration/project surfaces, SQL-facing references, and
  analysis gaps when public-safe evidence supports those examples.
- Runtime observability examples include logs, traces, metrics, APM,
  telemetry, incident dashboards, production alerts, incident timelines,
  production traffic, endpoint performance, request behavior, runtime errors,
  and service-owner interpretation.
- The page explains that TraceMap evidence can help prepare or follow up on
  runtime investigation by clarifying static code context, nearby references,
  and known analysis gaps.
- The page explains that runtime tools remain necessary for production
  behavior, traffic, performance, outage cause, operational safety, and
  incident-response conclusions.
- The page treats unknowns as first-class: if static evidence is partial,
  reduced, syntax-only, or unavailable, the page labels that state instead of
  implying complete coverage.
- The page avoids saying TraceMap confirms, certifies, guarantees, proves, or
  replaces runtime answers.

### Requirement 3: Preserve runtime and operational non-claims

The page shall make non-claims explicit so readers and future agents do not
turn static evidence into stronger operational promises.

Acceptance criteria:

- The page does not claim TraceMap proves runtime behavior, production traffic,
  endpoint performance, outage cause, release safety, operational safety,
  production dependency understanding, incident root cause, incident priority,
  service ownership, test sufficiency, or complete product coverage.
- The page does not claim TraceMap performs AI impact analysis, LLM analysis,
  prompt-based classification, embedding search, or vector database analysis.
- The page does not imply TraceMap replaces logs, traces, APM, telemetry,
  incident dashboards, production metrics, tests, service-owner review,
  incident response, release approval, governance, or human judgment.
- The page does not name specific observability vendors as shipped
  integrations unless current public repo evidence proves those integrations.
- Generic references to runtime telemetry, APM, logs, traces, metrics, and
  dashboards are allowed when they are framed as complementary systems.
- The page avoids "impacted" wording unless a reducer-backed result with
  public-safe evidence supports it; otherwise use terms such as "static
  reference", "static path", "surface", "nearby evidence", "review input",
  "gap", or "needs review".

### Requirement 4: Tie public examples to public-safe proof paths

The page shall use public-safe examples and proof links without publishing raw
scanner artifacts, private data, or runtime evidence.

Acceptance criteria:

- Public examples link only to checked-in public pages, public-safe generated
  summaries, documentation, rule catalog pages, reports, or demo artifacts that
  are safe to publish.
- The page does not publish raw `facts.ndjson`, raw `index.sqlite`, analyzer
  logs, raw source snippets, raw SQL, config values, secrets, local absolute
  paths, raw repository remotes, generated scan directories, private sample
  identities, or raw telemetry payloads.
- When the source of truth is local-only SQLite, facts, reports, rule catalog
  material, or analyzer logs, the page names the artifact family and links only
  to a public-safe summary or route.
- Rule IDs, evidence tiers, coverage labels, commit SHA, file paths, line
  spans, and extractor versions appear only when they are public-safe and
  backed by an existing public proof path or public-safe summary.
- Any runtime example is conceptual and generic; it does not publish logs,
  traces, dashboards, metrics, incident timelines, customer data, service
  names, or production identifiers.
- Missing proof paths remain visible as limitations rather than being treated
  as evidence-backed examples.

### Requirement 5: Define recommended page structure

The future page shall be concise, scan-friendly, and useful for incident-
adjacent and review workflows.

Acceptance criteria:

- Include a hero or opening section that says the page is a concept-level
  explanation of static evidence beside runtime telemetry.
- Include a "Different Questions" section or equivalent comparison table with
  at least these columns or rows: static evidence question, TraceMap evidence
  shape, runtime question, runtime system owner, and limitation.
- The comparison table uses accessible semantics, including header cells and a
  scannable row or column structure, and remains readable on narrow/mobile
  viewports without losing the static-versus-runtime distinction.
- Include a workflow section that shows how static evidence can be used before
  runtime review, during handoff, and after runtime review without replacing
  logs, traces, metrics, dashboards, tests, or service-owner judgment.
- Include a manager/reviewer section that explains how to read a static
  evidence packet: rule ID, evidence tier, file path, line span, commit SHA,
  extractor version, coverage label, limitation, and follow-up owner.
- Include a non-claims section that lists the operational and AI/LLM
  boundaries from Requirement 3.
- Include a proof-path section that points to existing public-safe evidence,
  documentation, validation, limitations, demo, and output pages when those
  routes exist at implementation time.
- Include a final link set to the most relevant public-safe surfaces confirmed
  to exist at implementation time. Candidate routes include `/docs/`,
  `/validation/`, `/limitations/`, `/outputs/`, `/proof-paths/`,
  `/capabilities/`, `/demo/`, `/demo/result/`, `/static-triage/`,
  `/incident-call/`, and `/use-cases/incident-review/`.
- Before linking to any route, verify it resolves in generated output. Record
  unresolved, moved, or unavailable routes as gaps in `implementation-state.md`
  rather than linking to them.

### Requirement 6: Add discovery metadata without inflating the claim

The future implementation shall make the page discoverable while preserving
concept-level wording and machine-readable boundaries.

Acceptance criteria:

- Standalone route metadata includes title, description, canonical path, Open
  Graph fields, and public claim level `concept` using existing site patterns.
- Sitemap and route-index metadata include the standalone route if one is
  added.
- If the content is implemented as a section on an existing page rather than a
  standalone route, the host page's existing title, description, social
  metadata, sitemap entry, and any LLM or bot-oriented discovery entry must not
  be inflated by the added section. They must continue to carry concept-level
  wording for the static-versus-runtime content and must not imply a shipped
  runtime, telemetry, or observability capability.
- If the content is implemented as a section, add a stable in-page anchor so
  human-facing cross-links can deep-link to the concept explanation without
  implying a standalone shipped route. Machine-readable discovery entries may
  deep-link to that anchor only if the current discovery schema and validator
  support in-page anchors; otherwise keep discovery entries on the validated
  host-page path and record the anchor-only limitation in implementation state.
- Discovery metadata, search metadata, social metadata, alt text, captions,
  and generated route summaries carry the same concept-level claim boundaries
  as visible page copy.
- Standalone route or section discovery metadata follows the existing
  `site/src/_site/discovery.json` `site-page` shape when that schema is still
  current, including `path`, `title`, `summary`, `publicClaimLevel`,
  `sourceType`, `hintCategory`, `preferredProofPath`, `limitations`, and
  `nonClaims`.
- Any LLM or bot-oriented discovery entry marks the route as concept-level and
  includes non-claims that forbid runtime proof, production traffic, endpoint
  performance, outage cause, release safety, operational safety, AI impact
  analysis, LLM analysis, complete product coverage, incident root cause,
  service ownership, and test sufficiency.
- Link text from existing public pages describes learning, comparison,
  boundaries, static evidence handoff, or review orientation. It must not imply
  runtime proof, production coverage, operational safety, release approval, or
  observability replacement.
- Stable section anchors exist for static questions, runtime questions,
  handoff workflow, proof paths, limitations, and non-claims if a standalone
  page is added.

### Requirement 7: Validate the future implementation

The future implementation shall run normal site validation and record results
in this spec's implementation state.

Acceptance criteria:

- Run `git diff --check`.
- Run `npm test` from `site/`.
- Run `npm run validate` from `site/`, or record a gap in
  `implementation-state.md` only if `site/scripts/validate.mjs` no longer
  exists at implementation time.
- Run `npm run build` from `site/`.
- Run `./scripts/check-private-paths.sh`.
- For layout or interaction changes, run desktop and mobile browser sanity
  checks.
- Validate rendered copy contains `Public claim level: concept` and the shared
  site principle.
- Validate rendered copy distinguishes static repository evidence from runtime
  logs, traces, APM, telemetry, incident dashboards, and production metrics.
- Validate rendered copy avoids forbidden claims about runtime proof,
  production traffic, endpoint performance, outage cause, release safety,
  operational safety, AI impact analysis, LLM analysis, and complete product
  coverage.
- Validate rendered copy avoids unsupported "impacted" wording unless a
  reducer-backed result with public-safe evidence supports it; otherwise verify
  the copy uses terms such as "static reference", "static path", "surface",
  "nearby evidence", "review input", "gap", or "needs review".
- Validate rendered copy and metadata avoid raw source snippets, raw SQL,
  config values, secrets, local absolute paths, raw remotes, generated scan
  directories, private sample names, raw `facts.ndjson`, raw `index.sqlite`,
  analyzer logs, raw telemetry payloads, incident timelines, customer data,
  service names, and production identifiers.
- If the content is implemented as a section on an existing page, validate
  that the host page's title, description, social metadata, sitemap entry, and
  any LLM or bot-oriented discovery entry have not been inflated to imply a
  shipped runtime, telemetry, or observability capability.
- Validate internal links resolve in generated output.
- Record any intentionally deferred validation, route mapping, or route gap in
  `implementation-state.md`.

## Validation Plan For This Spec Packet

- Run the repo-supported Kiro spec review with `claude-opus-4.8` if available.
- Run the repo-supported Kiro spec review with `claude-sonnet-4.6` if
  available.
- Patch Medium or higher findings and rerun re-review where feasible.
- Run `git diff --check`.
- Run `./scripts/check-private-paths.sh`.
