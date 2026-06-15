# Requirements

## Introduction

The public site needs a guided first-run path that turns the existing demo,
examples, capabilities, outputs, evidence model, and limitations pages into one
inspectable workflow. The page should help developers, reviewers, managers, and
future agents understand what to run, what to inspect, and what not to claim.

Public claim level: demo

This page may describe reproducible behavior from checked-in samples and
public-safe generated summaries. It must not present private-repo analysis,
runtime behavior, production traffic, deployment state, release approval, or AI
impact analysis as TraceMap output.

## Requirements

### Requirement 1: Guided First Run

**User Story:** As a first-time visitor, I want a step-by-step demo path so I
can understand TraceMap without guessing which page to open next.

Acceptance Criteria:

1. WHEN a visitor opens `/demo/start-here/` THEN they SHALL see a clear
   first-run workflow.
2. WHEN command snippets are shown THEN they SHALL use runnable `tracemap` or
   checked-in script framing where appropriate.
3. WHEN the demo output is described THEN it SHALL distinguish public-safe
   summaries from local-only artifacts.
4. WHEN next links are shown THEN they SHALL connect the walkthrough to demo,
   examples, capabilities, outputs, evidence, limitations, docs, and source.

### Requirement 2: Claim Boundaries

**User Story:** As a site maintainer, I want the walkthrough to preserve
TraceMap's evidence boundaries under incident or review pressure.

Acceptance Criteria:

1. WHEN runtime tools are mentioned THEN TraceMap SHALL be framed beside them,
   not as a replacement.
2. WHEN endpoint or dependency inspection is described THEN it SHALL be framed
   as static repository evidence, not production truth.
3. WHEN limitations are described THEN the page SHALL explicitly reject claims
   about runtime behavior, production traffic, deployment state, endpoint
   performance, endpoint usage, or release safety.

### Requirement 3: Discovery And Validation

**User Story:** As a public site operator, I want the walkthrough discoverable
and validated.

Acceptance Criteria:

1. WHEN `/demo/` is shown THEN it SHALL link to `/demo/start-here/`.
2. WHEN the homepage source-of-truth links are shown THEN they SHALL include the
   demo walkthrough.
3. WHEN the sitemap is generated THEN `/demo/start-here/` SHALL be included.
4. WHEN validation runs THEN it SHALL pass without adding dependencies or
   runtime services.

