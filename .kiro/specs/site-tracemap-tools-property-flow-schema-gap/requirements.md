# Site TraceMap Tools Property-Flow Schema Gap Requirements

Status: implemented
Readiness: implemented
Public claim level: concept

## Summary

Create a public-safe `tracemap.tools` proof-path page that explains the
property-flow route-flow schema compatibility gap added after PR #376. The
page must help reviewers understand why an incompatible route-flow schema is a
bounded evidence stop, not a silent promotion of endpoint context and not proof
that route-flow evidence is absent.

The implementation is site-only. It does not change scanner, reducer,
property-flow, route-flow, generated artifact, runtime, production, or impact
behavior.

## Shared Site Principle

No public conclusion without evidence.

## Claim Level Rationale

Use `Public claim level: concept`. Current `dev` contains checked-in code,
tests, rule catalog language, and acceptance notes for the narrow behavior:
property-flow distinguishes missing, empty, available, and unsupported
route-flow schema signals; an unsupported schema emits an
`UnsupportedRouteFlowSchema` gap with `property-flow.schema.v1` and
`Tier4Unknown` instead of promoting route-flow context.

The public page may describe that verified source behavior at a bounded level,
but it must not claim runtime behavior, production execution, impact proof,
complete coverage, UI behavior proof, release approval, or AI/LLM analysis.

## Requirements

### Requirement 1: Place The Page As A Proof-Path Compatibility Story

The implementation shall add a public-safe page or section that fits existing
site information architecture.

Acceptance criteria:

- The selected placement is recorded in `implementation-state.md`.
- The selected placement is `/proof-paths/property-flow-schema/` unless the
  implementation records a stronger IA reason for another route.
- The page visibly says `Public claim level: concept`.
- The page visibly says `No public conclusion without evidence`.
- The page is not added to primary navigation.
- If standalone, the page includes title, description, canonical URL, Open
  Graph metadata, sitemap metadata, discovery metadata, and at least one
  adjacent inbound link.

### Requirement 2: Explain The Supported Schema Gap Behavior

The page shall explain the narrow verified behavior using deterministic static
evidence vocabulary.

Acceptance criteria:

- The page distinguishes `RouteFlowUnavailable`, empty route-flow rows,
  `UnsupportedRouteFlowSchema`, and available route-flow schema.
- The page says `UnsupportedRouteFlowSchema` means a route-flow evidence signal
  exists but does not expose a compatible normalized route key contract for
  property-flow.
- The page says property-flow must not silently promote endpoint context when
  route-flow schema is incompatible.
- The page says existing combined path evidence may still be shown when
  available.
- The page says unsupported schema does not prove route-flow evidence is absent.
- The page includes `property-flow.schema.v1`, `Tier4Unknown`,
  `UnknownAnalysisGap`, supporting IDs, commit evidence, observed schema
  context, extractor versions, limitations, and owner follow-up as required
  review fields.
- The page names checked-in evidence surfaces without exposing private paths or
  raw generated artifacts.

### Requirement 3: Keep Public Claims Bounded

The page shall not overstate what the schema gap proves.

Acceptance criteria:

- The page must not claim runtime behavior, runtime request execution,
  runtime binding, production traffic, endpoint performance, outage cause,
  business impact, release safety, operational safety, release approval,
  complete coverage, production execution, impact proof, UI behavior proof, or
  absence of route-flow evidence.
- The page must not describe TraceMap as AI impact analysis, LLM analysis,
  embeddings, vector databases, prompt classification, autonomous approval, or
  a replacement for tests, code review, source review, runtime observability,
  service-owner judgment, or human review.
- The page must not publish raw source snippets, raw SQL, config values,
  secrets, local paths, raw remotes, generated scan directories, raw facts, raw
  SQLite content, analyzer logs, raw command output, hidden validation details,
  private sample names, private route values, or credential-like values.

### Requirement 4: Add Focused Validation

The implementation shall add or update validation following existing site
patterns.

Acceptance criteria:

- Focused validation checks the route, metadata, sitemap/discovery metadata,
  visible claim level, shared principle, required schema terms, adjacent links,
  inbound link, forbidden claims, and forbidden private material.
- Focused tests cover the happy path and at least one regression for missing
  required schema wording, route metadata, missing inbound link, forbidden
  overclaim wording, and private material.
- Aggregate `npm run validate` runs the focused validator.
- `tasks.md` and `implementation-state.md` are updated before completion.

### Requirement 5: Validate And Publish

The site implementation shall be validated before PR handoff.

Acceptance criteria:

- Run `cd site && npm test`.
- Run `cd site && npm run validate`.
- Run `cd site && npm run build`.
- Run `git diff --check`.
- Run `./scripts/check-private-paths.sh`.
- Do desktop and mobile browser sanity for the layout if local tooling is
  available, or record the exact blocker.
- Commit, push, create a PR to `dev`, wait 3 minutes, and run the requested
  ACK loop before final reporting.
