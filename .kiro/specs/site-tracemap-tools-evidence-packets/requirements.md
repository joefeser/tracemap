# site-tracemap-tools-evidence-packets

## Status

Implemented.

## Public claim level

demo

## Summary

Add a public site page that explains TraceMap evidence packets as a demo-level,
reviewer-friendly handoff model. The page should help managers, reviewers,
architects, and engineers understand what generated artifacts are useful for,
how to inspect them, and which claims remain out of bounds.

## Requirements

### Requirement 1: Evidence packet framing

The site shall publish a page at `/packets/` that describes a TraceMap evidence
packet as a bounded set of static-analysis artifacts generated from a repository
and commit.

Acceptance criteria:

- The page says the public claim level is `demo`.
- The page identifies SQLite, facts, reports, logs, and manifests as source
  artifacts rather than replacing them with prose.
- The page explains that the packet helps humans inspect evidence, coverage, and
  limitations.

### Requirement 2: Reader-specific workflow

The page shall describe how different readers use the packet without changing
the underlying evidence.

Acceptance criteria:

- The page includes manager, reviewer, architect, and engineer reader paths.
- Each reader path stays tied to rule-backed static evidence.
- The page does not claim production traffic, runtime behavior, endpoint
  performance, deployment state, or release safety.

### Requirement 3: Claim boundaries

The page shall make public-safe and unsafe wording easy to distinguish.

Acceptance criteria:

- The page includes safe wording examples grounded in static evidence.
- The page includes unsafe wording examples for runtime or production claims.
- The page states that reduced coverage, gaps, and limitations remain part of
  the packet.

### Requirement 4: Discovery and metadata

The page shall be discoverable from existing site surfaces.

Acceptance criteria:

- The homepage, demo page, capabilities page, and docs page link to `/packets/`.
- `/packets/` is included in sitemap metadata.
- The implementation-state note records validation and follow-up items.

