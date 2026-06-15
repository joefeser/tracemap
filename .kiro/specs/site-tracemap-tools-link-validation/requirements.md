# Requirements

## Introduction

The TraceMap site now has generated blog pages and a generated sitemap. The
site should have a local validation step that proves public URLs and internal
links resolve in generated `dist` output before Amplify publishes the site.

## Requirements

### Requirement 1: Generated Output Validation

**User Story:** As a site operator, I want a local validation command that
checks the built site so stale sitemap or navigation links are caught before
deployment.

Acceptance Criteria:

1. WHEN `npm run validate` runs THEN it SHALL build the site before checking
   generated output.
2. WHEN `sitemap.xml` contains a TraceMap URL THEN the corresponding generated
   file SHALL exist in `site/dist`.
3. WHEN `robots.txt` is generated THEN it SHALL point at the canonical
   `https://tracemap.tools/sitemap.xml` URL.

### Requirement 2: Internal Link Safety

**User Story:** As a maintainer, I want internal HTML links checked so static
pages, generated blog pages, and copied assets do not drift apart.

Acceptance Criteria:

1. WHEN an HTML page references an internal `href` or `src` THEN the referenced
   generated file SHALL exist.
2. WHEN an HTML page links to an external URL THEN validation SHALL NOT require
   network access.
3. WHEN a missing internal target is found THEN validation SHALL fail with the
   source file and missing reference.

### Requirement 3: Static Site Boundary

**User Story:** As a contributor, I want validation to preserve the simple
static-site toolchain.

Acceptance Criteria:

1. WHEN validation is implemented THEN it SHALL use Node.js standard library
   APIs only.
2. WHEN validation fails THEN it SHALL report clear diagnostics.
3. WHEN validation passes THEN it SHALL report how many HTML files, internal
   references, and sitemap URLs were checked.
