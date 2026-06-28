# Site TraceMap Tools Swift V0 Evidence Lane Requirements

Status: not-started
Readiness: backlog
Public claim level: shipped

## Objective

Create a public site story that explains Swift v0 as a shipped TraceMap evidence
lane after PR #425 was promoted to `main`.

Proof anchor: PR #425, merge commit
`e8813daaf763e277e7c5d88a2c0b2ad0b570f25a`.

## Safe Public Claim

TraceMap now has a Swift v0 adapter focused on deterministic static,
evidence-backed discovery.

## Requirements

- The page or section must identify Swift v0 as shipped on `main`.
- The copy must explain that Swift v0 produces static evidence with rule IDs,
  evidence tiers, file paths, line spans, commit SHA, coverage labels, and
  documented limitations.
- The page must link to or name the Swift evidence families without implying
  complete Swift analysis.
- The page must say Swift v0 does not prove runtime behavior, app navigation,
  production usage, build success, deployment state, release safety, or AI
  impact analysis.
- Public examples must use synthetic, category-level, or public-safe generated
  summaries only.

## Acceptance Criteria

- A future implementation has one public route or existing-page section for the
  Swift v0 evidence lane.
- The route or section includes PR #425 as the proof anchor.
- Site validation passes.
- The page copy avoids raw source snippets, raw SQL, secrets, local absolute
  paths, private repo remotes, and stored runtime values.
