# Requirements

## Introduction

The static site repeats top navigation across hand-authored pages and generated
blog pages. TraceMap should catch navigation drift during `npm run validate`
instead of relying on PR reviewers to notice missing links.

Public claim level: hidden

This is an internal site quality gate. It should not create new public product
claims.

## Requirements

### Requirement 1: Canonical Top Navigation

**User Story:** As a site maintainer, I want validation to fail when a generated
HTML page has stale top navigation so public pages remain consistently
discoverable.

Acceptance Criteria:

1. WHEN `npm run validate` checks generated HTML THEN it SHALL verify every
   page with the canonical top navigation links.
2. WHEN a page is missing `<nav class="top-nav">` THEN validation SHALL fail
   with the generated file path.
3. WHEN a page has missing, extra, reordered, or renamed top-navigation links
   THEN validation SHALL fail with the generated file path and expected links.
4. WHEN current-page markers such as `aria-current` are present THEN validation
   SHALL ignore those markers and compare link text plus `href`.

### Requirement 2: Generated Blog Layout

**User Story:** As a reader, I want blog article pages to expose the same top
navigation as the rest of the site.

Acceptance Criteria:

1. WHEN blog pages are generated THEN their top navigation SHALL include
   `Capabilities` and `Docs`.
2. WHEN validation runs against generated blog pages THEN they SHALL pass the
   same canonical navigation check as hand-authored pages.

