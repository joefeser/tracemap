# Requirements

## Introduction

The static site has repeated header and top-navigation markup across
hand-authored pages and generated blog pages. Recent capabilities work added
validation for navigation drift, but future work should reduce the number of
places agents must edit by hand.

Public claim level: hidden

This is an internal site maintainability phase. It should not create new public
TraceMap product claims.

## Requirements

### Requirement 1: Shared Header Rendering

**User Story:** As a site maintainer, I want generated pages to receive a
canonical header during build so top navigation changes do not require editing
every page by hand.

Acceptance Criteria:

1. WHEN static HTML files are copied into `dist` THEN the build SHALL replace
   the source `<header class="site-header">` block with a canonical generated
   header.
2. WHEN blog pages are generated THEN they SHALL use the same canonical header
   renderer as static pages.
3. WHEN a page path matches a top-navigation link THEN the generated header
   SHALL mark that link with `aria-current="page"`.
4. WHEN a page is nested under a top-navigation section THEN the generated
   header SHALL mark that link with `aria-current="location"`.

### Requirement 2: Shared Navigation Data

**User Story:** As a reviewer, I want the build and validation code to share one
canonical navigation data source so validation cannot drift from generation.

Acceptance Criteria:

1. WHEN validation checks generated top navigation THEN it SHALL use the same
   canonical link list exported by the build script.
2. WHEN a generated page has stale, missing, extra, reordered, or renamed
   navigation links THEN validation SHALL continue to fail with the generated
   file path.

