# Site TraceMap Tools Static Vs Runtime Field Guide Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Define a future public `tracemap.tools` field-guide page or article that
explains the boundary between deterministic static evidence and runtime
telemetry. The page should help engineers, reviewers, managers, and
incident-adjacent teams understand how TraceMap complements logs, traces,
metrics, APM, dashboards, and service-owner interpretation without claiming to
observe production behavior.

This is a spec-only site packet. It does not implement site code, scanner
behavior, reducer behavior, runtime telemetry ingestion, observability vendor
integrations, client-side tracking, generated artifacts, or validation scripts.

## Shared Site Principle

No public conclusion without evidence.

## Claim Level Rationale

Use `Public claim level: concept` because the future field guide is explanatory
positioning and decision support. It may describe TraceMap's deterministic
static evidence model and public-safe artifact families, but it is not a
shipped runtime integration, production monitoring feature, incident dashboard,
performance analysis, release gate, or demo-backed runtime proof path.

Do not upgrade this page to `demo` unless a future phase adds checked-in,
public-safe demo material that supports the exact examples on the page without
publishing raw scanner artifacts, private sample names, runtime payloads,
production identifiers, local paths, raw remotes, secrets, or vendor-specific
integration claims.

## Audience

- Engineers who need to separate repository evidence questions from production
  behavior questions.
- Reviewers who need rule IDs, evidence tiers, file paths, line spans, commit
  SHA, extractor versions, coverage labels, and limitations before assigning a
  follow-up.
- Managers who need bounded language for comparing static evidence with
  operational observability.
- Incident-adjacent teams who need to hand static code context to runtime,
  testing, service-owner, or incident-response owners without overstating what
  TraceMap proves.

## Core Message

TraceMap shows static dependency evidence and limitations; runtime tools show
observed behavior. Neither replaces the other.

TraceMap can identify deterministic facts from a repository snapshot: which
static surfaces were found, which rule produced the fact, what evidence tier
and coverage label apply, where the relevant file and line span live, which
commit was scanned, which extractor version ran, and what limitations or
analysis gaps remain.

Runtime observability tools answer operational questions: which requests ran,
what happened under production traffic, how services behaved, what latency,
errors, logs, traces, metrics, dashboards, alerts, or incident timelines show,
and what service owners conclude from those signals.

The future page must position TraceMap as a companion to runtime observability,
tests, service-owner review, incident response, and release process. It must
not present TraceMap as a replacement for those systems.

## Claim Boundaries

- The future page or article shall visibly say
  `Public claim level: concept`.
- The future page or article shall visibly say
  `No public conclusion without evidence`.
- The future page or article shall include visible public copy equivalent to:
  `TraceMap shows static dependency evidence and limitations; runtime tools
  show observed behavior. Neither replaces the other.`
- The future page may discuss static dependency evidence, rule IDs, evidence
  tiers, file paths, line spans, commit SHA, extractor versions, coverage
  labels, limitations, analysis gaps, reports, proof paths, and public-safe
  artifact families.
- The future page may discuss generic runtime observability categories such as
  logs, traces, metrics, APM, dashboards, alerts, telemetry, incident
  timelines, request behavior, runtime errors, and service-owner
  interpretation as complementary systems.
- The future page must not claim TraceMap proves runtime behavior, production
  traffic, endpoint performance, outage cause, release safety, operational
  safety, production dependency understanding, incident truth, incident root
  cause, service ownership, test sufficiency, complete product coverage, or
  readiness to merge or deploy.
- The future page must not claim TraceMap performs AI impact analysis, LLM
  analysis, prompt-based classification, embedding search, vector search, or
  vector database analysis in the core scanner or reducer.
- The future page must not imply TraceMap replaces logs, traces, metrics, APM,
  telemetry, incident dashboards, production metrics, tests, service-owner
  review, incident response, release approval, governance, or human judgment.
- The future page must not publish raw source snippets, raw SQL, config
  values, secrets, local absolute paths, raw repository remotes, generated scan
  directories, private sample names, raw facts, raw SQLite indexes, analyzer
  logs, raw telemetry payloads, incident timelines, customer data, service
  names, production identifiers, dashboard screenshots, command output, hidden
  validation details, or credential-like values.

## Requirements

### Requirement 1: Publish a bounded concept field guide in a future phase

The future site implementation shall add a public-safe concept-level field
guide page or article that compares deterministic static evidence with runtime
telemetry.

Acceptance criteria:

- The implementation chooses a standalone route such as
  `/static-vs-runtime-field-guide/`, a route under an existing article or
  guide family, or a bounded section on an existing use-case or limitations
  page, then records the selected placement and rejected alternatives in
  `implementation-state.md`.
- Before selecting placement, the implementation confirms the relationship to
  the existing `/static-vs-runtime/` page and the
  `site-tracemap-tools-static-vs-runtime-telemetry` spec, then records whether
  this field guide extends the existing page or justifies a distinct route.
- If a distinct route is selected, the implementation records why a second
  concept-level static-versus-runtime surface is warranted, how the two
  surfaces cross-link, and how duplicate discovery entries are avoided.
- The page or section says `Public claim level: concept`.
- The page or section says `No public conclusion without evidence`.
- The page or section includes the visible core message:
  `TraceMap shows static dependency evidence and limitations; runtime tools
  show observed behavior. Neither replaces the other.`
- The page or section explains that TraceMap provides deterministic static
  repository evidence, not live operational telemetry.
- The page or section introduces no runtime data collection, runtime agent,
  telemetry ingestion, production monitoring, client-side tracking, live
  dashboard, incident automation, observability vendor integration, private
  dataset dependency, or generated local artifact dependency.
- If implemented as a standalone route, route metadata, canonical metadata,
  Open Graph metadata, sitemap metadata, and discovery metadata preserve the
  `concept` claim level.
- The page or section does not enter primary navigation unless a future site
  information-architecture review explicitly selects that placement and records
  why the concept-level field guide belongs there.

### Requirement 2: Separate static evidence questions from runtime questions

The future page shall help readers decide which questions TraceMap can orient
and which questions belong to runtime observability, tests, service owners, or
incident-response systems.

Acceptance criteria:

- The page includes a scannable comparison that distinguishes static evidence
  questions from runtime observability questions.
- Static evidence examples include repository snapshot, commit SHA, rule IDs,
  evidence tiers, file paths, line spans, extractor versions, coverage labels,
  limitations, dependency references, route or endpoint references, contract
  surfaces, package references, configuration or project surfaces, SQL-facing
  references, and analysis gaps only when public-safe evidence supports those
  examples.
- Runtime observability examples include logs, traces, metrics, APM,
  telemetry, dashboards, production alerts, incident timelines, production
  traffic, endpoint performance, request behavior, runtime errors, and
  service-owner interpretation as examples of questions outside TraceMap's
  static evidence.
- The page explains that TraceMap can help prepare for runtime review by
  clarifying static code context, nearby references, dependency paths, and
  known analysis gaps.
- The page explains that TraceMap can support follow-up after runtime review
  by identifying static surfaces to inspect, document, test, or hand to an
  owner.
- The page explains that runtime tools remain necessary for production
  behavior, traffic, performance, outage cause, operational safety,
  incident timelines, request behavior, runtime errors, incident-response
  conclusions, service-owner conclusions, and release decisions.
- The page treats unknowns as first-class: partial, reduced, syntax-only,
  unavailable, or gap-labeled analysis remains visible instead of being turned
  into complete coverage.
- The page avoids saying TraceMap confirms, certifies, guarantees, proves, or
  replaces runtime answers.

### Requirement 3: Preserve operational and runtime non-claims

The future page shall make operational non-claims explicit so future copy does
not turn static evidence into stronger runtime promises.

Acceptance criteria:

- The page does not claim TraceMap proves runtime behavior, production traffic,
  endpoint performance, outage cause, release safety, operational safety,
  production dependency understanding, incident truth, incident priority,
  incident root cause, service ownership, test sufficiency, complete product
  coverage, or readiness to merge or deploy.
- The page does not claim TraceMap performs AI impact analysis, LLM analysis,
  prompt-based classification, embedding search, vector search, or vector
  database analysis in the core scanner or reducer.
- The page does not imply TraceMap replaces logs, traces, APM, telemetry,
  incident dashboards, production metrics, tests, service-owner review,
  incident response, release approval, governance, or human judgment.
- The page does not name specific observability vendors as shipped
  integrations unless current public repo evidence proves those integrations.
- Generic references to runtime telemetry, APM, logs, traces, metrics, and
  dashboards are allowed when they are framed as complementary systems.
- The page avoids `impacted`, `safe`, `unsafe`, `no impact`, `production is
  unaffected`, `runtime confirmed`, `TraceMap proved`, `ready to release`, and
  `approved to merge` wording unless the phrase appears only inside an
  explicitly marked forbidden-wording example or non-claim context.
- When reducer-backed public-safe evidence is unavailable, use terms such as
  `static reference`, `static path`, `dependency evidence`, `surface`, `nearby
  evidence`, `review input`, `gap`, or `needs review`.

### Requirement 4: Tie examples to public-safe proof paths

The future page shall use only public-safe examples and proof links.

Acceptance criteria:

- Public examples link only to checked-in public pages, public-safe generated
  summaries, documentation, rule catalog pages, reports, demo summaries, or
  validation pages that are safe to publish.
- The page does not publish raw `facts.ndjson`, raw `index.sqlite`, analyzer
  logs, raw source snippets, raw SQL, config values, secrets, local absolute
  paths, raw repository remotes, generated scan directories, private sample
  identities, raw telemetry payloads, incident timelines, customer data,
  service names, production identifiers, dashboard screenshots, command
  output, hidden validation details, or credential-like values.
- When the source of truth is local-only SQLite, facts, reports, rule catalog
  material, analyzer logs, or runtime data, the page names only the artifact
  family or complementary runtime category and links only to a public-safe
  summary or route.
- Rule IDs, evidence tiers, coverage labels, commit SHA, file paths, line
  spans, and extractor versions appear only when they are public-safe and
  backed by an existing public proof path or public-safe summary.
- Any runtime example is conceptual and generic. It does not publish logs,
  traces, metrics, dashboards, payloads, incident timelines, customer data,
  service names, production identifiers, or vendor integration claims.
- Missing proof paths remain visible as limitations, gaps, deferred links, or
  owner handoffs rather than being treated as evidence-backed examples.

### Requirement 5: Define the recommended page structure

The future page shall be concise, scan-friendly, and useful for review and
incident-adjacent handoff workflows.

Acceptance criteria:

- Include an opening section that identifies the surface as a concept-level
  field guide for reading static evidence beside runtime telemetry.
- Include a `Different questions` section or equivalent comparison table with
  these fields: static question, TraceMap evidence shape, runtime question,
  runtime owner or system, limitation, and handoff.
- The comparison table uses accessible semantics with header cells and a
  scannable row or column structure. It remains readable on narrow/mobile
  viewports without losing the static-versus-runtime distinction.
- Include a `How to use both` workflow section covering before runtime review,
  during handoff, and after runtime review without replacing logs, traces,
  metrics, dashboards, tests, incident response, or service-owner judgment.
- Include a `Reading a static evidence packet` section that explains rule ID,
  evidence tier, file path, line span, commit SHA, extractor version, coverage
  label, limitation, and follow-up owner.
- Include a `Where runtime tools remain authoritative` section with stable
  anchor `#runtime-authority` that keeps production behavior, traffic,
  endpoint performance, outage cause, operational safety, incident timelines,
  runtime errors, incident-response conclusions, service-owner conclusions, and
  release decisions outside TraceMap's static evidence.
- Include a `Non-claims` section that lists the runtime, operational, release,
  AI/LLM, and replacement-of-human-judgment boundaries from Requirement 3.
- Include a `Proof paths and limitations` section that points to existing
  public-safe evidence, documentation, validation, limitation, demo, output,
  or proof-path pages when those routes exist at implementation time.
- Include a final related-links set to the most relevant public-safe surfaces
  confirmed to exist at implementation time. Candidate routes include
  `/docs/`, `/validation/`, `/limitations/`, `/outputs/`, `/proof-paths/`,
  `/capabilities/`, `/demo/`, `/demo/result/`, `/static-vs-runtime/`,
  `/static-triage/`, `/incident-call/`, `/use-cases/incident-review/`,
  `/language/change-risk/`, and `/site-claim-guardrails/`.
- Before linking to any route, verify it resolves in generated output. Record
  unresolved, moved, or unavailable routes as gaps in `implementation-state.md`
  rather than linking to them.

### Requirement 6: Add discovery metadata without inflating the claim

The future implementation shall make the field guide discoverable while
preserving concept-level and non-runtime wording.

Acceptance criteria:

- Standalone implementations add page title, description, canonical URL, Open
  Graph metadata, sitemap metadata, and discovery metadata.
- Metadata copy describes the page as a static-versus-runtime field guide or
  concept guide, not as a shipped runtime, telemetry, monitoring, APM, incident
  automation, release-safety, or production-performance capability.
- Discovery metadata uses the current site schema at implementation time. If
  the existing `site-page` shape remains current, include `path`, `title`,
  `summary`, `publicClaimLevel`, `sourceType`, `hintCategory`,
  `preferredProofPath`, `limitations`, and `nonClaims`.
- `publicClaimLevel` remains `concept`.
- `sourceType` remains a site or documentation page type, not runtime data,
  telemetry data, generated scan artifact, or demo result unless a future
  implementation has public-safe proof for that stronger source type.
  The current discovery schema allows only `site-page` and `repo-doc`; use
  `site-page` unless the schema is extended at implementation time.
- `hintCategory` uses an existing allowed value from the current site
  discovery schema at implementation time. The known allowed set at spec time
  is `start`, `evidence`, `limitations`, `demo`, `repo-doc`, `roadmap`, and
  `use-case`; `use-case` or `evidence` are likely fits. The selected value and
  rationale are recorded in `implementation-state.md`.
- `preferredProofPath` points only to an existing public-safe proof, docs,
  limitations, or outputs route verified at implementation time.
- `limitations` and `nonClaims` explicitly include that TraceMap does not prove
  runtime behavior, production traffic, endpoint performance, outage cause,
  release safety, operational safety, complete coverage, AI/LLM analysis, or
  replacement of runtime tools and human review.
- If implemented as a section on an existing page, record how the host page
  title, description, social metadata, sitemap entry, and discovery entry stay
  compatible with concept-level static-versus-runtime wording.

### Requirement 7: Define validation expectations for future implementation

The future implementation shall add focused validation that protects the
static-versus-runtime boundary.

Acceptance criteria:

- Add validation for required visible copy: `Public claim level: concept`,
  `No public conclusion without evidence`, and the core message about static
  dependency evidence, runtime observed behavior, and neither replacing the
  other.
- Add validation for required sections, stable anchors, comparison table
  structure, related links, route metadata, sitemap metadata, and discovery
  metadata when a standalone route is selected.
- Add validation that candidate links resolve in generated output before they
  are published.
- Add validation for forbidden affirmative runtime claims, including runtime
  behavior proof, production traffic knowledge, endpoint performance proof,
  outage cause, incident truth, release safety, operational safety, complete
  coverage, AI/LLM analysis, and replacement of runtime tools or human review.
- Add validation for forbidden private or raw material across rendered text,
  decoded HTML attributes, raw HTML, route metadata, sitemap metadata,
  discovery metadata, tests, and validation messages.
- Add validation that `impacted`, `safe`, `unsafe`, `no impact`, `production
  is unaffected`, `runtime confirmed`, `TraceMap proved`, `ready to release`,
  and `approved to merge` are absent from affirmative copy and appear only in
  machine-distinguishable forbidden-wording examples or non-claim contexts.
- Add validation that generic runtime telemetry terms are allowed only when
  framed as complementary systems or non-claims.
- Run `npm test` from `site/`, `npm run validate` from `site/`, `npm run
  build` from `site/`, `git diff --check`, `./scripts/check-private-paths.sh`,
  and desktop/mobile browser sanity checks when layout, route, or interaction
  changes are made.
