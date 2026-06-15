# Requirements

## Introduction

TraceMap has enough implemented scanner, reducer, combined-index, reporting, and
language-adapter capability that the public site needs a concise capability map.
The page should help managers, reviewers, bots, and future agents understand
what is real today while preserving maturity labels and explicit non-claims.

Public claim level: demo

This page may describe TraceMap capabilities demonstrated by checked-in samples
and public-safe generated summaries. Capabilities that exist only on dev must be
labeled as dev-only or omitted until dev-to-main promotion lands.

## Requirements

### Requirement 1: Capability Map

**User Story:** As a reviewer or manager, I want to see what TraceMap can do
today so I can decide where to start.

Acceptance Criteria:

1. WHEN a visitor opens `/capabilities/` THEN they SHALL see status-labeled
   workflow groups for scan, reduce, combine/report, paths/reverse, diff/impact,
   release-review, portfolio/endpoints, and inspect/export support.
2. WHEN a capability row is shown THEN it SHALL include a main, dev, demo, or
   future status and a proof path.
3. WHEN command names are shown THEN they SHALL be framed as static evidence
   workflows, not runtime proof.
4. WHEN outputs are described THEN they SHALL preserve the distinction between
   public-shareable summaries and local-only private analysis artifacts.

### Requirement 2: Adapter Maturity

**User Story:** As a maintainer, I want language support described with maturity
labels so the site does not overclaim parity.

Acceptance Criteria:

1. WHEN `.NET/C#` support is described THEN it SHALL be framed as the strongest
   adapter.
2. WHEN TypeScript, JVM, or Python support is described THEN it SHALL use MVP or
   reduced-coverage language where appropriate.
3. WHEN an adapter row is shown THEN it SHALL include maturity status and a proof
   path.
4. WHEN Kotlin or Python runtime-sensitive behavior is mentioned THEN the page
   SHALL avoid implying full semantic parity or runtime execution.

### Requirement 3: Discovery And Boundaries

**User Story:** As a site operator, I want the capability map discoverable and
validated.

Acceptance Criteria:

1. WHEN the homepage source-of-truth link grid is shown THEN it SHALL include
   the capability map.
2. WHEN the docs map is shown THEN it SHALL link to the capability map.
3. WHEN the sitemap is generated THEN it SHALL include `/capabilities/`.
4. WHEN validation runs THEN it SHALL pass without adding dependencies or
   runtime services.
