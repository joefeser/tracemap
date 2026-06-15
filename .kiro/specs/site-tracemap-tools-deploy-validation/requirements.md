# Requirements

## Introduction

The TraceMap site has a local generated-site validation command. Deployment and
pull request automation should run the same validation so broken sitemap URLs,
internal links, or robots metadata do not reach `main` or Amplify unnoticed.

## Requirements

### Requirement 1: Amplify Validation

**User Story:** As a site operator, I want Amplify to run generated-site
validation before publishing artifacts.

Acceptance Criteria:

1. WHEN Amplify builds the site THEN it SHALL run `npm run validate`.
2. WHEN validation succeeds THEN `site/dist` SHALL remain the published artifact
   directory.
3. WHEN validation fails THEN Amplify SHALL fail before publishing stale or
   broken static output.

### Requirement 2: Pull Request Validation

**User Story:** As a maintainer, I want site validation to run on pull requests
so link and sitemap issues are caught before merge.

Acceptance Criteria:

1. WHEN a pull request changes site, Amplify, site spec, or site workflow files
   THEN GitHub Actions SHALL run site tests.
2. WHEN a pull request changes site, Amplify, site spec, or site workflow files
   THEN GitHub Actions SHALL run generated-site validation.
3. WHEN the workflow runs THEN it SHALL use Node.js 20 and `npm ci`.

### Requirement 3: Static Site Boundary

**User Story:** As a contributor, I want deployment validation to preserve the
existing static-site model.

Acceptance Criteria:

1. WHEN deployment validation is configured THEN no backend, runtime service,
   database, auth, or crawler SHALL be added.
2. WHEN validation runs THEN it SHALL use the existing site npm scripts.
3. WHEN docs describe deployment THEN they SHALL identify `npm run validate` as
   the Amplify build command.
