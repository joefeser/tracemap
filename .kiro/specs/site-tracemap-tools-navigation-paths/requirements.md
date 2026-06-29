# Site TraceMap Tools Navigation Paths Requirements

Status: implemented
Readiness: implemented

Public claim level: demo

## Summary

Add a small information-architecture pass that makes the existing public site
paths easier to choose by reader role. This phase should connect the recently
added manager packet, proof assets, proof upgrades, evidence packet guide,
capability matrix, and public demo without expanding the primary top
navigation.

## Requirements

### Requirement 1: Add role-based entry paths

The site shall make it clear where a manager, engineer, reviewer, or demo
reader should start.

Acceptance criteria:

- The homepage includes a role-based path section using existing site styles.
- Each path links to existing public pages instead of creating a new route.
- The path copy stays evidence-oriented and avoids marketing-only language.
- The section does not add new primary top-navigation items.

### Requirement 2: Connect the evidence journey

The site shall make `/manager-packet/`, `/demo/proof-assets/`,
`/demo/proof-upgrades/`, `/packets/`, `/capabilities/`, and `/demo/` feel like
one intentional journey.

Acceptance criteria:

- The homepage first-look path references manager, reviewer, proof-asset, and
demo reader flows.
- `/packets/` points readers to the role-based homepage path.
- `/use-cases/` links managers and incident-adjacent readers to the bounded
manager packet and proof assets.
- Existing proof pages remain bounded to demo-level static evidence.

### Requirement 3: Preserve public claim boundaries

The pathing copy shall not upgrade static evidence into runtime, production, or
release-safety claims.

Acceptance criteria:

- The copy does not claim runtime behavior, production traffic, deployment
state, endpoint performance, release safety, incident root cause, or AI impact
analysis.
- The copy keeps limitations, partial analysis, static evidence, and human
review visible where relevant.
- No raw source snippets, raw SQL, config values, secrets, local absolute paths,
raw repository remotes, raw facts, SQLite files, generated scan directories, or
analyzer logs are published.

### Requirement 4: Validate the site

The implementation shall preserve static-site validation and responsive layout.

Acceptance criteria:

- Run `git diff --check`.
- Run `npm test` from `site/`.
- Run `npm run validate` from `site/`.
- Run desktop and mobile browser sanity checks for the homepage and at least
  one linked evidence page touched by this phase.
