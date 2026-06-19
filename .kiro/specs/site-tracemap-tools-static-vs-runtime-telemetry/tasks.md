# Site TraceMap Tools Static Vs Runtime Telemetry Tasks

Status: completed
Readiness: ready-for-implementation
Public claim level: concept

Note: validation tasks must pass before future implementation tasks are checked
complete.

- [x] Confirm or update this spec-local `implementation-state.md` with branch,
  scope, route or placement decision, validation results, review findings,
  oddities, and follow-up items before changing site code.
- [x] Choose the future page placement: standalone `/static-vs-runtime/` route,
  an existing use-case page section, or an existing limitations/orientation
  page section.
- [x] Record the selected placement and rejected alternates in
  `implementation-state.md`, including why the chosen placement does not imply
  a shipped runtime or observability capability.
- [x] Confirm the page or section is not added to primary navigation unless a
  future site information-architecture review explicitly chooses that placement
  and records the rationale in `implementation-state.md`.
- [x] Add the concept-level page or section using existing static-site layout,
  typography, metadata, and validation patterns.
- [x] Include visible `Public claim level: concept` copy and the shared site
  principle.
- [x] Explain that TraceMap provides deterministic static repository evidence,
  not live operational telemetry.
- [x] Add a scannable comparison of static evidence questions and runtime
  observability questions.
- [x] Include static evidence examples such as repository snapshot, commit SHA,
  rule IDs, evidence tiers, file paths, line spans, extractor versions,
  coverage labels, limitations, routes/endpoints, contracts, packages,
  config/project surfaces, SQL-facing references, and analysis gaps only when
  those examples are public-safe.
- [x] Include generic runtime observability examples such as logs, traces,
  metrics, APM, telemetry, incident dashboards, alerts, incident timelines,
  production traffic, endpoint performance, request behavior, runtime errors,
  and service-owner interpretation.
- [x] Add a workflow section for before runtime review, during handoff, and
  after runtime review that keeps runtime owners and service owners responsible
  for operational conclusions.
- [x] Add manager/reviewer guidance for reading rule IDs, evidence tiers, file
  paths, line spans, commit SHA, extractor versions, coverage labels,
  limitations, and follow-up owners.
- [x] Add explicit non-claims for runtime behavior, production traffic,
  endpoint performance, outage cause, release safety, operational safety,
  incident root cause, service ownership, production dependency understanding,
  AI impact analysis, LLM analysis, and complete product coverage.
- [x] Ensure copy does not imply TraceMap replaces logs, traces, APM,
  telemetry, incident dashboards, production metrics, tests, service-owner
  review, incident response, release approval, governance, or human judgment.
- [x] Avoid naming specific observability vendors as shipped integrations
  unless current public repo evidence proves those integrations.
- [x] Avoid saying a surface is impacted unless a reducer-backed result with
  public-safe evidence supports the wording.
- [x] Link proof paths only to public-safe routes, generated summaries,
  documentation, rule catalog pages, reports, or demo artifacts.
- [x] For local-only SQLite, facts, reports, rule catalog material, or analyzer
  logs, name only the artifact family and link only to a public-safe summary or
  route.
- [x] Ensure no page copy, metadata, alt text, captions, or generated discovery
  output publishes raw source snippets, raw SQL, config values, secrets, local
  absolute paths, raw remotes, generated scan directories, private sample
  names, raw facts, raw SQLite indexes, analyzer logs, raw telemetry payloads,
  incident timelines, customer data, service names, or production identifiers.
- [x] Add stable section anchors for static questions, runtime questions,
  handoff workflow, proof paths, limitations, and non-claims if a standalone
  route is added.
- [x] If the content is implemented as a section on an existing page, ensure
  the host page title, description, social metadata, sitemap entry, and any LLM
  or bot-oriented discovery entry stay concept-level and do not imply a shipped
  runtime, telemetry, or observability capability.
- [x] If the content is implemented as a section, add a stable in-page anchor
  so cross-links and discovery entries can deep-link without implying a
  standalone shipped route.
- [x] Add page title, description, canonical URL, Open Graph fields, sitemap
  metadata, and discovery metadata if a standalone route is added.
- [x] Ensure discovery metadata and any LLM or bot-oriented discovery entry
  preserve the concept claim level and operational non-claims.
- [x] Use the existing `site/src/_site/discovery.json` `site-page` field shape
  when that schema is still current, including `path`, `title`, `summary`,
  `publicClaimLevel`, `sourceType`, `hintCategory`, `preferredProofPath`,
  `limitations`, and `nonClaims`.
- [x] Add safe cross-links from relevant public pages only where the link text
  reinforces static evidence boundaries and does not imply runtime proof or
  observability replacement.
- [x] Link to relevant public-safe surfaces available at implementation time,
  such as `/docs/`, `/validation/`, `/limitations/`, `/outputs/`,
  `/proof-paths/`, `/capabilities/`, `/demo/`, `/demo/result/`,
  `/static-triage/`, `/incident-call/`, and `/use-cases/incident-review/`.
- [x] Record any moved or unavailable target route in `implementation-state.md`
  with the chosen mapping or reason for deferral.
- [x] Add focused validation for required claim-level text, shared principle,
  static-versus-runtime distinction, required links, route metadata, forbidden
  operational claims, and forbidden private/raw material.
- [x] Add focused validation that rendered copy avoids unsupported "impacted"
  wording unless a reducer-backed result with public-safe evidence supports it.
- [x] If a section placement was chosen, validate that host-page title,
  description, social metadata, sitemap, and LLM/bot discovery entries remain
  concept-level and do not imply a shipped runtime, telemetry, or
  observability capability.
- [x] Wire focused validation into the existing site validation and test
  workflow rather than relying only on manual review.
- [x] Run `git diff --check`.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`, or record a gap in
  `implementation-state.md` only if `site/scripts/validate.mjs` no longer
  exists at implementation time.
- [x] Run `npm run build` from `site/`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run desktop and mobile browser sanity checks if layout or interaction
  changes are made.
- [x] Update `implementation-state.md` with route decisions, validation
  results, review findings, claim-boundary decisions, oddities, partial-work
  labels if applicable, and follow-up items.
- [x] Keep `implementation-state.md` free of local absolute paths, raw remotes,
  secrets, raw facts, raw SQLite index paths, analyzer log content, raw runtime
  telemetry, and private sample names.
