# Requirements

## Introduction

The TraceMap site now has static pages, generated blog articles, and queued
concept pages. `sitemap.xml` should be generated from the same source metadata
used by the site build so new public URLs do not require a second manual edit.

## Requirements

### Requirement 1: Generated Sitemap

**User Story:** As a site operator, I want `sitemap.xml` generated during the
normal static build so Amplify publishes the current public URL set.

Acceptance Criteria:

1. WHEN `npm run build` runs THEN `site/dist/sitemap.xml` SHALL be generated.
2. WHEN blog article metadata changes THEN the generated sitemap SHALL include
   the matching `/blog/<slug>/` URLs.
3. WHEN private build input folders are present THEN they SHALL NOT be copied
   into `site/dist`.

### Requirement 2: Metadata Source

**User Story:** As a maintainer, I want public non-blog pages listed in one
metadata file so page URL changes are easy to review.

Acceptance Criteria:

1. WHEN a public static page is part of the site THEN its sitemap path,
   changefreq, and priority SHALL be represented in private site metadata.
2. WHEN site page metadata is missing or malformed THEN the build SHALL fail
   with a clear error.
3. WHEN duplicate sitemap paths exist THEN the build SHALL fail before writing
   ambiguous output.

### Requirement 3: Static Site Boundary

**User Story:** As a contributor, I want sitemap generation to preserve the
current plain-static deployment model.

Acceptance Criteria:

1. WHEN sitemap generation is implemented THEN it SHALL use Node.js standard
   library APIs only.
2. WHEN the site is served locally THEN `/sitemap.xml` SHALL come from generated
   `dist` output.
3. WHEN the sitemap is generated THEN it SHALL use canonical
   `https://tracemap.tools` URLs.
