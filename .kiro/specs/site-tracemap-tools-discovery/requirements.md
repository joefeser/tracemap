# Requirements

## Introduction

The first `tracemap.tools` launch published a single static homepage. This slice
turns the site into a crawlable discovery surface with stable URLs for the core
concepts that humans, search engines, and documentation agents need to find:
evidence tiers, scan outputs, workflows, validation, limitations, and the public
demo.

The site remains a publishing layer over the TraceMap repository. It must not
become the source of truth for scanner behavior, reducer behavior, schemas, or
rule definitions.

## Requirements

### Requirement 1: Stable Crawlable Pages

**User Story:** As a reviewer discovering TraceMap from the web, I want stable
topic pages so I can link directly to the concept I need.

#### Acceptance Criteria

1. WHEN the site is built THEN `/evidence/`, `/outputs/`, `/workflows/`,
   `/validation/`, `/limitations/`, and `/demo/` SHALL exist as static HTML
   pages.
2. WHEN a page is rendered THEN it SHALL include a canonical URL for
   `https://tracemap.tools/<page>/`.
3. WHEN the homepage is rendered THEN primary navigation SHALL use the stable
   pages instead of only hash anchors.

### Requirement 2: Bounded Product Claims

**User Story:** As an engineer evaluating TraceMap, I want the public site to
describe what the tool can prove and what it cannot prove.

#### Acceptance Criteria

1. WHEN evidence pages describe findings THEN they SHALL mention rule IDs,
   evidence tiers, source spans, commit SHA, and extractor versions.
2. WHEN limitations are described THEN the site SHALL state that TraceMap is
   static analysis and does not prove runtime traffic, production usage,
   deployment state, release approval, or absence of impact under reduced
   coverage.
3. WHEN AI is mentioned THEN the site SHALL state that the scanner and reducer
   do not use LLM calls, embeddings, vector databases, or prompt-based
   classification.

### Requirement 3: Discovery Metadata

**User Story:** As a crawler or documentation agent, I want sitemap and robots
metadata that exposes the public pages.

#### Acceptance Criteria

1. WHEN `sitemap.xml` is fetched THEN it SHALL list the homepage and all new
   stable topic pages.
2. WHEN `robots.txt` is fetched THEN it SHALL allow crawling and reference the
   sitemap.
3. WHEN the site is built THEN a static 404 page SHALL exist.

### Requirement 4: Deployment Notes

**User Story:** As a maintainer configuring hosting, I want the deployment
settings recorded near the site code.

#### Acceptance Criteria

1. WHEN a maintainer opens `site/README.md` THEN it SHALL document the Amplify
   app root, build command, output directory, and validation URLs.
2. WHEN future agents start site work THEN they SHALL be able to see that site
   slices should use `site-*` specs and separate worktrees.
