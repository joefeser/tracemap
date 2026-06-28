# Site TraceMap Tools Swift Adapter Story Requirements

Status: implemented
Readiness: implemented
Public claim level: shipped

## Objective

Create the public narrative story for the shipped Swift v0 adapter. The page
should explain why Swift support matters, how it fits the TraceMap evidence
model, and how readers should interpret the shipped `/swift/` evidence lane.

Proof anchor: PR #425, merge commit
`e8813daaf763e277e7c5d88a2c0b2ad0b570f25a`.

## Safe Public Claim

TraceMap now has a shipped Swift v0 adapter focused on deterministic static
evidence. The story may describe what the adapter helps reviewers inspect, but
it must keep runtime, build, production, release, and AI-analysis non-claims
explicit.

## Requirements

- The story must identify Swift v0 as shipped on `main` and link to the `/swift/`
  evidence lane.
- The story must explain why the adapter exists in human terms: mobile and Swift
  repositories need the same rule-backed evidence contract as other stacks.
- The copy must describe static inventory, symbols/calls, surfaces, storage/data,
  and safety as evidence families, not complete Swift understanding.
- The copy must explain how managers, reviewers, architects, and engineers can
  use the page without reading raw artifacts.
- The copy must include explicit non-claims for runtime behavior, app
  navigation, production usage, build success, deployment state, release safety,
  stored values, query execution, and AI impact analysis.
- The page must not publish raw source snippets, raw SQL, secrets, local paths,
  raw remotes, credentials, stored values, private scan artifacts, or hidden
  validation details.

## Acceptance Criteria

- `/swift/story/` exists as the public story route.
- The route links to `/swift/`, `/capabilities/`, `/validation/`,
  `/limitations/`, `/limitations/reduced-coverage/`, and `/static-vs-runtime/`.
- The route has sitemap and discovery metadata with `publicClaimLevel:
  "shipped"`.
- Site validation and build pass.
