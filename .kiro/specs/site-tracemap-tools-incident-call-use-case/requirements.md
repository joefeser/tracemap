# Site TraceMap Tools Incident Call Use Case Requirements

Status: not-started
Readiness: ready-for-review
Public claim level: concept

## Summary

Add a public site concept phase for an incident-call use case page or article.
The planned page should address this situation: "I am on a production
incident/P1 call and need to know what static dependencies and proof paths
surround this endpoint or surface."

This is a static dependency evidence story. It is not a runtime monitor, APM
replacement, outage diagnosis, release approval, release safety claim, or
production operations proof.

## Shared Site Principle

No public conclusion without evidence.

## Requirements

### Requirement 1: Publish a bounded incident-call concept route

The site shall publish a concept-level incident-call page or article at the
stable public route `/incident-call/`.

Acceptance criteria:

- The page says `Public claim level: concept`.
- The page states the shared site principle.
- The primary copy addresses a production incident/P1 call reader who needs to
  understand the static dependencies and proof paths surrounding an endpoint or
  surface.
- The copy uses future-facing concept language and does not imply the page is a
  shipped operational workflow until implementation lands.
- The page labels the content as static dependency evidence, not runtime
  observability.

### Requirement 2: Preserve static evidence boundaries

The page shall keep public claims bounded to deterministic TraceMap evidence
and clearly state what the use case cannot prove.

Acceptance criteria:

- The page does not claim runtime behavior, production traffic, endpoint
  performance, outage cause, Dynatrace/APM replacement, release safety, or
  operational safety.
- The page says TraceMap can help a reader inspect static dependencies,
  coverage labels, rule IDs, evidence tiers, limitations, generated artifacts,
  file paths, line spans, commit SHA, and extractor versions when those are
  available in public-safe summaries.
- The page avoids saying an endpoint or dependency is "impacted" unless a
  reducer-backed result and evidence are present in the public-safe demo
  material.
- The page clearly distinguishes analysis gaps and reduced coverage from clean
  or complete evidence.

### Requirement 3: Link to proof paths and limitations

The incident-call page shall guide readers from the use-case story into
TraceMap's public-safe proof path surfaces.

Acceptance criteria:

- The page links to public proof-path content that shows rule IDs, evidence
  tiers, coverage labels, limitations, and supporting artifact references.
- The page links to `/proof-paths/`, `/validation/`, `/docs/`,
  `/limitations/`, and `/demo/result/` unless one of those routes is
  unavailable at implementation time, in which case the gap is documented in
  `implementation-state.md`.
- Any "proof path" callout describes static source-to-artifact evidence, not
  live production confirmation.
- The page includes a clear path from an endpoint or surface name to nearby
  static dependencies such as routes, handlers, DTOs, config/project surfaces,
  SQL references, package references, or cross-app references when public-safe
  demo evidence supports those examples.
- The page includes disambiguation copy distinguishing `/incident-call/` from
  `/use-cases/incident-review/` as P1-call orientation versus broader
  post-incident review orientation.

### Requirement 4: Publish only public-safe generated summaries

The future implementation shall use public-safe generated summaries and demo
evidence, not raw private scan artifacts.

Acceptance criteria:

- The page and metadata do not publish raw `facts.ndjson`, `index.sqlite`,
  `.tracemap` paths, analyzer logs, raw source snippets, raw SQL, config
  values, secrets, local absolute paths, raw repo remotes, generated scan
  directories, or private sample identities.
- Public examples are derived from demo-safe generated summaries or synthetic
  demo evidence.
- Any snippet-like text is either authored explanatory copy or explicitly
  approved public demo material.
- The page makes clear that private repositories require private scans and
  private review of generated evidence before anything becomes public copy.

### Requirement 5: Add discovery metadata and validation expectations

The site shall make the concept discoverable while preserving its claim level
and public-safety boundaries.

Acceptance criteria:

- Discovery metadata labels the route as `concept`.
- Public page metadata includes a title, description, canonical URL, and Open
  Graph fields for the incident-call route.
- Sitemap and route-index metadata include the incident-call route when
  implemented.
- Existing relevant public pages can link to the route without implying the
  workflow is complete or operationally authoritative.
- Validation checks confirm the route renders, includes the claim-level label
  and shared principle, links to proof paths and limitations, and avoids
  forbidden private artifact text including raw artifact names, `.tracemap`
  paths, raw SQL patterns, and local absolute path patterns.
- Validation checks confirm required internal proof-path links resolve in the
  generated site output.
- Implementation validation includes `git diff --check`, site tests or focused
  route tests when available, `npm run validate` from `site/`, and
  `npm run build` from `site/`.
- If `npm run validate` is not available in `site/package.json`, the
  implementation shall run a focused fallback forbidden-text check over the
  rendered route and document the validation gap in `implementation-state.md`.
