# site-tracemap-tools-public-demo-result

## Status

Implemented.

## Public claim level

demo

## Summary

Add a public site page that explains the current public demo result shape. The
page should help visitors understand what the first demo slice produces today,
which sections are available, which sections are intentionally deferred, and how
to read the generated summaries without overclaiming static evidence.

## Requirements

### Requirement 1: Public demo result page

The site shall publish a page at `/demo/result/` that describes the current
public demo result.

Acceptance criteria:

- The page says the public claim level is `demo`.
- The page identifies the checked-in sample scan set used by the current demo.
- The page describes `demo-summary.md` and `demo-summary.json`.
- The page links back to the demo script source.

### Requirement 2: Available and deferred sections

The page shall make available and deferred sections explicit.

Acceptance criteria:

- The page identifies toolchain checks, build, and sample scans as available
  sections.
- The page identifies Python default behavior as `not_requested`.
- The page describes JVM status as environment-dependent unless Java 21 is
  required.
- The page identifies combine/report, paths/reverse, portfolio, diff, impact,
  and release-review as deferred in the current public demo slice.
- Deferred sections include reasons copied from the demo workflow intent.

### Requirement 3: Safety and claim boundaries

The page shall keep generated artifact safety boundaries visible.

Acceptance criteria:

- The page says public summaries and reports are shareable only after sentinel
  checks.
- The page says raw scan artifacts, SQLite indexes, fact streams, manifests, and
  logs remain local-only.
- The page does not claim runtime behavior, production traffic, deployment
  state, endpoint performance, release safety, or AI impact analysis.

### Requirement 4: Discovery

The page shall be discoverable from existing site surfaces.

Acceptance criteria:

- `/demo/`, `/demo/start-here/`, `/packets/`, `/capabilities/`, and
  `/examples/` link to `/demo/result/`.
- `/demo/result/` is included in sitemap metadata.
- The implementation-state note records scope, validation, and follow-ups.

