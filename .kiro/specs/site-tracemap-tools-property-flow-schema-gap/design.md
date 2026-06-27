# Site TraceMap Tools Property-Flow Schema Gap Design

Status: implemented
Readiness: implemented
Public claim level: concept

## Design Purpose

This design adds a public-safe explanation page for the property-flow
route-flow schema compatibility gap. The page teaches reviewers how to read an
unsupported route-flow schema as an evidence-backed stop condition.

The page is not a product behavior demo, runtime trace, impact report, release
gate, or UI behavior proof.

## Placement

Selected route: `/proof-paths/property-flow-schema/`.

This placement keeps the topic next to proof-path reading surfaces. The topic
is narrower than a use-case page: it is about one compatibility stop condition
inside the property-flow proof path.

Rejected placements:

- `/use-cases/property-flow-schema/`: too use-case-like for a schema
  compatibility proof-path page.
- `/property-flow-schema/`: too broad and first-class for a concept-level
  compatibility page.
- Section on `/proof-paths/`: too small a host for the rule, tier, schema
  status, and validation story.
- Section on `/evidence/gaps/`: useful adjacent surface, but this topic needs
  property-flow and route-flow context together.

## Page Model

Recommended sections:

1. Opening boundary with visible `Public claim level: concept` and
   `No public conclusion without evidence`.
2. Why schema compatibility matters for property-flow review.
3. The four route-flow schema statuses: unavailable, empty, unsupported, and
   available.
4. The required gap fields for `UnsupportedRouteFlowSchema`.
5. What reviewers may and may not conclude.
6. Adjacent surfaces and owner follow-up.

## Verified Source Evidence

Current `dev` evidence supports these narrow statements:

- `PropertyFlowReport.cs` classifies a present route-flow edge table or view
  with no compatible normalized route key column as `unsupported`.
- The unsupported signal emits an `UnsupportedRouteFlowSchema` gap under
  `property-flow.schema.v1` with `Tier4Unknown` and `UnknownAnalysisGap`.
- The emitted gap includes selected support, source IDs, commit evidence, file
  path and line span from the selected anchor when available, and observed
  route-flow schema columns.
- Focused `PropertyFlowTests` assert the unsupported route-flow schema signal,
  rule ID, classification, message context, commit evidence, supporting fact
  IDs, file path, line span, and absence of a fallback `RouteFlowUnavailable`
  gap.
- The rule catalog says unsupported route-flow schema means property-flow
  could not find the normalized route key contract it needs for route-flow
  context, not that route-flow evidence is absent.

## Copy Boundaries

Safe wording:

- `property-flow labels the incompatible schema as an evidence gap`.
- `reviewers can see why route-flow context stopped`.
- `existing combined path evidence may still be shown when available`.
- `unsupported schema is not absence proof`.

Unsafe wording:

- `TraceMap proves runtime behavior`.
- `TraceMap proves UI behavior`.
- `TraceMap proves impact`.
- `TraceMap proves complete coverage`.
- `TraceMap proves route-flow evidence is absent`.
- `TraceMap automatically approves a release`.
- `TraceMap performs AI or LLM impact analysis`.

## Validation Design

The focused validator should check:

- route output and sitemap entry;
- route metadata in `routes-index.json`;
- required page metadata;
- visible claim-level and shared-principle text;
- required schema statuses and evidence terms;
- adjacent outbound links and inbound link from `/proof-paths/`;
- forbidden runtime, impact, absence-proof, release, complete-coverage, AI, and
  LLM claims;
- hard private material and raw artifact publication;
- implementation-state records selected placement, verified source evidence,
  and browser sanity status.
