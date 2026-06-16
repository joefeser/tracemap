# Site TraceMap Tools Incident Call Use Case Design

Status: not-started
Readiness: ready-for-review
Public claim level: concept

## Overview

This design describes a future public-safe concept page or article for
incident-call readers. The implementation should help someone on a production
incident/P1 call understand what static dependency evidence and proof paths
surround a named endpoint or surface without claiming runtime proof.

## Route and Placement

- Add the stable public route `/incident-call/`.
- Implement the route using the existing static site convention under
  `site/src/`, which maps `/incident-call/` to
  `site/src/incident-call/index.html`.
- Follow the existing hand-authored static HTML page style, metadata shape, and
  shared navigation conventions already used by routes such as
  `/use-cases/incident-review/`, `/proof-paths/`, `/validation/`,
  `/limitations/`, `/docs/`, and `/demo/result/`.
- Add route metadata in `site/src/_site/pages.json` and discovery metadata in
  `site/src/_site/discovery.json`.
- If those metadata files do not exist or their schema has changed by
  implementation time, follow the adjacent metadata convention and document the
  decision in `implementation-state.md`.
- Frame the route as a concept-level use case, not a shipped operational
  workflow.
- Link from relevant public discovery surfaces only when the link text keeps
  the concept boundary clear.
- This route complements `/use-cases/incident-review/` with a narrower
  production incident/P1-call orientation. Future implementation should
  cross-link the routes with disambiguation copy so readers understand the
  difference.
- The top-level route keeps the URL short and shareable during a P1 call, while
  `/use-cases/incident-review/` remains the broader post-incident review
  orientation.

## Page Metadata

The route should include public page metadata consistent with existing static
pages:

- Title: `Incident Call Static Dependency Evidence | TraceMap`.
- Meta description that frames the page as a concept-level static evidence use
  case for incident-call orientation.
- Canonical URL for `https://tracemap.tools/incident-call/`.
- Open Graph type, site name, title, description, and URL.
- Discovery metadata with `publicClaimLevel` set to `concept` and non-claims
  that preserve static-analysis boundaries.

## Content Model

- Lead with the incident-call question: "What static dependencies and proof
  paths surround this endpoint or surface?"
- Show a public-safe flow from endpoint or surface name to nearby static
  dependency evidence.
- Use public-safe generated summaries or demo evidence as examples.
- Include claim-level labels, evidence-tier labels, rule IDs, coverage labels,
  limitations, commit SHA, extractor versions, file paths, and line spans when
  supported by the public-safe example data.

## Proof Path Links

The page should link to public proof-path material that explains:

- Which static rules produced the evidence.
- Which evidence tier supports each visible conclusion.
- Which limitations or analysis gaps apply.
- Which generated public-safe artifacts back the visible summary.

At minimum, the route should link to `/proof-paths/`, `/validation/`,
`/docs/`, `/limitations/`, and `/demo/result/` unless one of those routes has
become unavailable at implementation time. If a route is unavailable, document
the gap in `implementation-state.md`.

Proof path links must not imply live production verification, runtime traffic
analysis, endpoint performance measurement, outage cause, release approval, or
operational safety.

## Public Safety

The implementation must not publish raw `facts.ndjson`, `index.sqlite`,
`.tracemap` paths, analyzer logs, raw source snippets, raw SQL, config values,
secrets, local absolute paths, raw repo remotes, generated scan directories, or
private sample identities. Use authored explanatory copy, synthetic demo
material, or public-safe generated summaries instead.

## Validation Design

Future validation should check:

- The route renders and includes `Public claim level: concept`.
- The shared principle appears: no public conclusion without evidence.
- Required proof-path, limitations, validation, docs, and demo links are present
  when those routes exist.
- Forbidden private artifact text is absent, including raw artifact names,
  `.tracemap` paths, raw SQL patterns, and local absolute path patterns.
- Sitemap and route-index metadata include the route.
- Internal proof-path links resolve in the generated site output.
- Desktop and mobile browser sanity checks show readable layout with no
  overlapping text or broken navigation.
- Site validation and build commands pass before implementation is marked done.
