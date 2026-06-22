# Evidence Export Navigation Polish Requirements

## Introduction

TraceMap already ships deterministic vault and docs-export outputs. The next
problem is not evidence extraction; it is making generated evidence easier for a
human reviewer or downstream RAG importer to navigate without weakening the
source-of-truth model. This spec defines a follow-up slice for safer names,
stable cross-links, navigation manifests, route/entity indexes, and
RAG-import-friendly chunk boundaries.

This is not an AI feature. TraceMap may generate artifacts that another RAG
system consumes, but TraceMap evidence remains the deterministic source of
truth.

## Non-Goals

- LLM calls, embeddings, vector database writes, retrieval APIs, or
  prompt-based classification.
- Using RAG answers, vector similarity, browser telemetry, or generated
  summaries as evidence for TraceMap conclusions.
- Raw source snippets by default.
- Raw SQL, config values, credentials, local absolute paths, private sample
  names, raw repository remotes, raw URLs, production data, or secrets in
  public/demo-safe output.
- Runtime proof, production traffic proof, vulnerability scanning, ownership
  assignment, release approval, or absence-of-impact claims.
- Replacing existing vault `graph.json`, docs-export JSONL, or generated
  Markdown schemas in a breaking way.

## Requirement 1: Navigation Profile And Mode Boundaries

**User Story:** As a reviewer, I want generated evidence navigation to be clear
about public/demo/hidden safety so I can browse local outputs without confusing
them with publishable artifacts.

### Acceptance Criteria

1. WHEN vault or docs-export navigation polish is implemented THEN it SHALL keep
   existing public/demo/hidden claim-level behavior and add navigation metadata
   only additively.
2. Public and demo modes SHALL reject unsafe material rather than rendering
   redacted-but-ambiguous proof claims.
3. Hidden/local mode MAY render repo-relative evidence locations or category
   labels only when the hidden safety classifier allows the value for that
   context. Absolute paths, raw remotes, raw URLs, hostnames, connection
   strings, and source snippets SHALL be rejected or omitted even in
   hidden/local mode.
4. Every generated output SHALL visibly preserve or reference rule IDs,
   evidence tiers, coverage labels where available, limitations, source IDs,
   and supporting fact/report IDs.
5. Navigation labels, aliases, tags, file names, slugs, and display names SHALL
   not become evidence. Stable IDs and cited evidence remain authoritative.
6. Vault export and docs-export SHALL use the same public/demo/hidden safety
   mode vocabulary and SHALL document any surface-specific behavior as an
   explicit limitation.

## Requirement 2: Safe Human-Readable Names

**User Story:** As a user opening an exported vault or docs folder, I want note
names and section titles to be readable while still deterministic and safe.

### Acceptance Criteria

1. Generated note and chunk display names SHALL be derived from a closed set of
   safe fields such as route method/path key, surface kind, rule ID,
   classification, source label, evidence tier, or stable hash.
2. Unsafe, ambiguous, excessively long, all-fields-absent, or collision-prone
   names SHALL fall back to deterministic category-plus-hash names; if no safe
   category can be derived, the category SHALL be `unknown`.
3. Name collision handling SHALL be deterministic and SHALL not choose an
   arbitrary winner. All colliding entries SHALL receive stable-ID-derived
   disambiguators rather than allowing one entry to keep the bare display name
   because it happened to sort first. Disambiguator hashes SHALL be derived
   from the stable evidence ID, not the display name.
4. Generated files SHALL include enough metadata or frontmatter to recover the
   stable evidence ID even when the display name is shortened or hashed.
5. Tests SHALL cover collision, truncation, unsafe-token, Unicode/spacing, and
   case-insensitive filesystem behavior.

## Requirement 3: Cross-Link And Index Navigation

**User Story:** As a reviewer, I want to start from a route, endpoint, table,
package, symbol, gap, or rule and find neighboring evidence without remembering
internal IDs.

### Acceptance Criteria

1. Vault output SHOULD include deterministic index pages for route/endpoint,
   symbol, dependency surface, data surface, package, rule, gap, and limitation
   groupings when those evidence families are present.
2. Docs-export output SHOULD include section anchors and backreferences for the
   same groupings when the chunk schema supports them.
3. Cross-links SHALL use stable IDs or generated safe slugs, not raw paths or
   raw endpoint URLs.
4. Missing neighbors inside a present evidence family SHALL be represented as
   rule-backed gaps or visible absence states rather than empty conclusions.
5. Wholly absent evidence families SHALL either be omitted from generated
   navigation or rendered as an explicit family-level absence state; the
   implementation SHALL choose one behavior per output surface and test it.
6. Index pages SHALL remain deterministic across input row order changes.

## Requirement 4: Route And Flow-Oriented Navigation

**User Story:** As a reviewer, I want exported evidence to help me follow a
route-centered static flow from entrypoint evidence to service/data/package
neighbors.

### Acceptance Criteria

1. When route-flow report evidence is present, exports SHOULD expose route-flow
   navigation entries that cite the report/fact IDs, rule IDs, path coverage,
   and limitations.
2. When property-flow report evidence is present, exports SHOULD expose
   property-flow navigation entries that cite report/fact IDs, rule IDs,
   property/source-target evidence, coverage labels where available, and
   limitations.
3. Route-flow and property-flow navigation SHALL not claim runtime execution,
   DI resolution, branch feasibility, auth behavior, object identity, mutation
   effects, or production traffic.
4. Related service, data, SQL-shape, package, event/message, and legacy data
   descriptors MAY be linked only when existing evidence already supplies the
   relationship or shared stable identity.
5. Ambiguous or duplicate route-flow or property-flow neighbors SHALL be
   rendered as NeedsReview/unknown/gap context, not definitive flow.

## Requirement 5: RAG-Import Chunk Boundaries

**User Story:** As a downstream RAG builder, I want chunks with stable titles,
citations, and boundaries so retrieval can cite TraceMap evidence without
turning RAG output into TraceMap evidence.

### Acceptance Criteria

1. Docs-export chunks SHALL keep deterministic stable chunk IDs and include
   claim/citation-first sections.
2. Chunk boundaries SHOULD align to likely review questions such as endpoint
   summary, touched files, data surfaces, package surfaces, weak evidence,
   gaps, limitations, and snapshot/change context when supported inputs exist.
3. Every chunk SHALL include rule IDs, evidence tiers, coverage labels where
   available, supporting IDs, and limitations.
4. Unsupported additive question families SHALL emit
   `docs-export.gap.unsupported-question-family.v1` rather than fabricating
   synthetic answers.
5. Chunk text SHALL avoid raw snippets and unsafe values by default.
6. Identical inputs SHALL yield identical chunk boundaries and chunk ordering
   even when source rows are supplied in a different order.
7. Chunk text SHALL be bounded by a documented maximum size or sectioning
   policy so RAG import does not depend on unbounded generated Markdown.

## Requirement 6: Compatibility And Migration

**User Story:** As a user with existing generated exports, I want navigation
polish to be additive and safe to adopt.

### Acceptance Criteria

1. Existing generated-file sentinel and content-hash validation behavior SHALL
   be preserved.
2. Existing vault `graph.json` and docs-export JSONL consumers SHALL continue to
   parse old fields.
3. New fields SHALL be optional/additive unless the implementation creates a
   new documented schema version.
4. Migration notes SHALL explain which fields are stable identity fields and
   which are display/navigation helpers.
5. Stale generated output SHALL require existing force behavior and SHALL pass
   safety validation before replacement.

## Requirement 7: Validation And Review Evidence

**User Story:** As a maintainer, I want tests proving navigation polish is
deterministic, safe, and useful before merging.

### Acceptance Criteria

1. Focused tests SHALL cover safe naming, collisions, deterministic ordering,
   hidden-only evidence locations, public/demo rejection, graph/index
   backreferences, and RAG chunk boundaries.
2. Validation SHALL include `git diff --check` and
   `./scripts/check-private-paths.sh`.
3. Product-code implementation PRs SHALL run focused vault/docs-export tests
   and the full required .NET validation unless explicitly scoped otherwise.
4. Spec-only PRs SHALL not run full .NET tests unless product code changes.
5. Kiro review state SHALL be recorded in `implementation-state.md`.
