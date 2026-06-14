# Requirements

## Introduction

The discovery pages explain TraceMap concepts, but visitors still need concrete
examples of what the artifacts look like. This slice adds representative static
examples for a scan packet and a contract-impact report, plus a use-cases page
that frames where the examples fit.

These examples are illustrative public-site copy. They must preserve TraceMap's
claim boundaries and link back to repository docs as the source of truth.

## Requirements

### Requirement 1: Example Artifact Pages

**User Story:** As a reviewer evaluating TraceMap, I want to see examples of the
files and report rows TraceMap produces before I run the tool.

#### Acceptance Criteria

1. WHEN `/examples/` is rendered THEN it SHALL link to the scan packet and
   contract-impact examples.
2. WHEN `/examples/scan-packet/` is rendered THEN it SHALL show representative
   manifest, facts, and report excerpts.
3. WHEN `/examples/contract-impact/` is rendered THEN it SHALL show a
   representative contract delta and impact output.
4. WHEN example snippets are shown THEN they SHALL avoid raw source snippets,
   local absolute paths, secrets, raw SQL, connection strings, and credential
   bearing URLs.

### Requirement 2: Use Case Framing

**User Story:** As a visitor, I want to understand where TraceMap fits in real
review work.

#### Acceptance Criteria

1. WHEN `/use-cases/` is rendered THEN it SHALL describe contract-change review,
   release review, cross-repo dependency mapping, and legacy partial analysis.
2. WHEN use cases describe TraceMap value THEN they SHALL keep conclusions
   coverage-relative and static-evidence based.

### Requirement 3: Discovery Metadata And Navigation

**User Story:** As a crawler or user navigating the site, I want examples and use
cases to be discoverable from stable URLs.

#### Acceptance Criteria

1. WHEN primary navigation is rendered THEN it SHALL include an `Examples` link.
2. WHEN `sitemap.xml` is fetched THEN it SHALL include `/examples/`,
   `/examples/scan-packet/`, `/examples/contract-impact/`, and `/use-cases/`.
3. WHEN pages are rendered THEN each SHALL include canonical URLs and bounded
   metadata.
