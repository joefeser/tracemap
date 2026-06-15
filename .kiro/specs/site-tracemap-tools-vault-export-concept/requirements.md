# Requirements

## Introduction

TraceMap needs a site-facing concept page for a future optional
Obsidian/vault export demo. The page should explain why a linked Markdown vault
could make static evidence easier to inspect without claiming that the feature
has shipped in the core tool.

## Requirements

### Requirement 1: Future-Facing Framing

**User Story:** As a site visitor, I want to understand the vault export idea
without mistaking it for a currently shipped TraceMap feature.

Acceptance Criteria:

1. WHEN the page describes the vault export THEN it SHALL use future-facing
   language such as "future", "planned", "concept", or "would".
2. WHEN the page mentions Obsidian or vaults THEN it SHALL frame them as an
   optional human exploration layer, not a TraceMap requirement.
3. WHEN the page describes TraceMap evidence THEN it SHALL keep SQLite, facts,
   reports, and the rule catalog as the source of truth.

### Requirement 2: Evidence Boundaries

**User Story:** As a reviewer, I want the concept page to preserve TraceMap's
static-analysis boundaries.

Acceptance Criteria:

1. WHEN the page describes the vault THEN it SHALL NOT claim runtime proof,
   release approval, production usage, or AI impact analysis.
2. WHEN the page describes notes and edges THEN it SHALL say they carry rule IDs,
   evidence tiers, commit SHAs, coverage labels, supporting IDs, and limitations.
3. WHEN the page describes export safety THEN it SHALL say raw source snippets,
   raw SQL, config values, secrets, local absolute paths, and raw repo remotes
   should not be exported.

### Requirement 3: Site Discovery

**User Story:** As someone exploring the public demo, I want to discover the
future vault export story from relevant existing pages.

Acceptance Criteria:

1. WHEN the concept page is added THEN it SHALL have a stable `/vault-export/`
   URL and sitemap entry.
2. WHEN visitors read the demo page THEN they SHALL be able to find the concept
   page without the demo claiming the export exists today.
3. WHEN visitors read the workflows page THEN they SHALL see the vault export as
   a future exploration, not a shipped workflow.
