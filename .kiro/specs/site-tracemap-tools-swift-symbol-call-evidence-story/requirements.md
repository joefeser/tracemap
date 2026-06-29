# Site TraceMap Tools Swift Symbol And Call Evidence Story Requirements

Status: implemented
Readiness: implemented
Public claim level: shipped

## Objective

Create a site story for Swift declarations, basic call/construction candidates,
and symbol relationship evidence where syntax/static evidence supports them.

Proof anchor: PR #425, merge commit
`e8813daaf763e277e7c5d88a2c0b2ad0b570f25a`.

## Safe Public Claim

TraceMap emits syntax-backed Swift declaration, call candidate, construction
candidate, and direct relationship evidence where static evidence supports it.

## Requirements

- Public copy must say this is static/syntax-backed evidence.
- Public copy must not claim compiler semantic resolution, runtime dispatch,
  protocol witness proof, dependency injection proof, or SwiftUI runtime view
  reachability.
- The story must explain that weaker evidence tiers and reduced coverage labels
  are part of the product behavior, not a failure to hide.
- The story must avoid saying a Swift path is impacted without reducer-backed
  evidence.

## Acceptance Criteria

- Future public copy separates syntax-backed candidates from proven runtime
  targets.
- The copy includes rule/evidence language and explicit limitations.
- Site validation passes after implementation.
