# Requirements

Status: implemented
Readiness: implemented
Public claim level: hidden

## Introduction

The initial TraceMap blog runway is implemented, but each article currently
duplicates the same page shell, metadata structure, navigation, and footer. The
site should keep publishing plain static HTML while giving maintainers a smaller
source shape for future articles.

## Requirements

### Requirement 1: Static Output

**User Story:** As a site operator, I want the blog build step to emit normal
static files so Amplify can keep publishing `site/dist` with no runtime service.

Acceptance Criteria:

1. WHEN `npm run build` runs THEN the blog index and article pages SHALL exist in
   `site/dist/blog/`.
2. WHEN the generated blog pages are published THEN their public URLs SHALL stay
   stable.
3. WHEN private blog source files are present THEN they SHALL NOT be copied into
   `site/dist`.

### Requirement 2: Shared Blog Chrome

**User Story:** As a maintainer, I want blog articles to share one generated
shell so navigation, metadata, footer, and article structure do not drift.

Acceptance Criteria:

1. WHEN an article is generated THEN it SHALL use the same primary navigation and
   Blog `aria-current="location"` state as the existing article pages.
2. WHEN the blog index is generated THEN it SHALL use Blog
   `aria-current="page"`.
3. WHEN article metadata changes THEN the generated page SHALL use that metadata
   for title, canonical URL, Open Graph fields, card copy, and published date.

### Requirement 3: No Framework Dependency

**User Story:** As a contributor, I want the build step to stay simple and local
so the site remains easy to run in the repo.

Acceptance Criteria:

1. WHEN the build step is changed THEN it SHALL use Node.js standard library
   APIs only.
2. WHEN `npm run dev` starts THEN it SHALL serve generated static output instead
   of unpublished private source files.
3. WHEN the source data is malformed THEN the build SHALL fail with a clear
   validation error.
