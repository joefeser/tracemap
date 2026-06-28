# Requirements

Status: implemented
Readiness: implemented
Public claim level: concept

## Overview

Create a concept-level public site page at `/legacy-modernization/review-handoff/` that bridges existing legacy evidence surfaces into a modernization review handoff. The page must help managers, reviewers, architects, and engineers move from deterministic static evidence to owner questions without implying runtime proof, migration success, release approval, database execution, or complete coverage.

## Requirements

### Requirement 1: Public-Safe Route

**User story:** As a modernization reviewer, I want a bounded handoff route so I can carry TraceMap static evidence into owner questions without overstating what the evidence proves.

Acceptance criteria:

1. The route is implemented at `site/src/legacy-modernization/review-handoff/index.html`.
2. The visible copy includes `Public claim level: concept`.
3. The visible copy includes `No public conclusion without evidence`.
4. The page explains the handoff boundary between static evidence and modernization decisions.
5. The page stays out of primary navigation unless a strong reason is recorded.

### Requirement 2: Handoff Matrix

**User story:** As a reviewer, I want a structured matrix so each modernization question carries evidence, proof fields, limitations, owners, allowed wording, and stop conditions together.

Acceptance criteria:

1. The matrix includes columns for `Review question`, `Static evidence to bring`, `Required proof field`, `Limitation to keep attached`, `Owner to involve`, `Allowed wording`, and `Stop condition`.
2. Matrix rows cover framework/runtime age, route/API, data surface, package/dependency, config/deployment clue, validation/reduced coverage, and migration/test planning questions.
3. Rows remain bounded to rule IDs or rule families, evidence tiers, coverage labels, limitations, proof paths, and owner follow-up.

### Requirement 3: Non-Claims and Boundaries

**User story:** As a public reader, I want explicit non-claims so I do not confuse static evidence with runtime proof, migration approval, or release readiness.

Acceptance criteria:

1. The page distinguishes TraceMap static evidence from modernization decisions, runtime telemetry, migration tooling, service ownership, and release approval.
2. The page lists non-claims and stop conditions.
3. The page does not claim runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, migration success, schema compatibility, database execution or connectivity, raw data access, AI/LLM impact analysis, embeddings, vector databases, prompt classification, or complete coverage.
4. The page does not publish raw source snippets, raw SQL, raw config values, secrets, tokens, connection strings, database contents, raw facts, raw SQLite content, analyzer logs, raw remotes, local absolute paths, generated scan directories, private sample names, hidden validation details, raw command output, private URLs, or credential-like values.

### Requirement 4: Metadata and Validation

**User story:** As a site maintainer, I want route metadata and route-specific validation so regressions in public claim boundaries are caught locally.

Acceptance criteria:

1. Add a `pages.json` sitemap entry for `/legacy-modernization/review-handoff/`.
2. Add a `discovery.json` entry with `publicClaimLevel: concept`, `sourceType: site-page`, hint category, preferred proof path, limitations, and non-claims.
3. Add `site/scripts/legacy-modernization-review-handoff.mjs`.
4. Add `site/scripts/legacy-modernization-review-handoff.test.mjs`.
5. Wire the validator into `site/scripts/validate.mjs`.
6. The validator checks required visible markers, matrix headers and rows, adjacent links, route metadata, sitemap metadata, forbidden claims, forbidden private/raw material, word count bounds, and no primary-nav addition.
7. Negative tests cover missing required copy, metadata regression, forbidden runtime or modernization claims, private/raw material, and missing matrix rows or fields.
