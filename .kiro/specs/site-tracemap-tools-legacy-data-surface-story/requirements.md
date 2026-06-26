# Site TraceMap Tools Legacy Data Surface Story Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Define a future public-safe `tracemap.tools` page or section about legacy data
surface evidence. The page should help managers, reviewers, architects, and
engineers understand what deterministic static evidence can say about legacy
data surfaces without implying raw data access, database execution, production
behavior, migration success, or complete coverage.

The future page is concept-level only. It describes the story shape for
design-time metadata, data model metadata, ORM or mapping clues,
SQL/query-facing references, storage and persistence context, proof paths, and
limitations. It does not implement scanner behavior, reducer behavior, site
code, data access, database execution, runtime SQL observation, migration
automation, AI impact analysis, or release approval.

## Shared Site Principle

No public conclusion without evidence.

## Requirements

### Requirement 1: Define a bounded concept surface

The future implementation shall publish or add a concept-level public surface
for legacy data surface evidence, preferably as a standalone route at
`/legacy-data-surface/` unless implementation-time route review finds a better
placement under the legacy .NET evidence lane.

Acceptance criteria:

- The page or section says `Public claim level: concept`.
- The page or section states `No public conclusion without evidence`.
- The page is future-facing and uses wording such as "concept", "future",
  "planned", "would", or "could" when describing behavior not already backed
  by public-safe proof.
- The surface is public-safe, manager/reviewer friendly, and bounded to
  deterministic static evidence from repository snapshots and checked-in
  artifacts.
- The surface does not imply shipped coverage, complete coverage, raw data
  access, database connectivity, database execution, query execution, runtime
  SQL behavior, data contents, migration success, endpoint performance,
  production traffic, outage cause, release safety, or operational safety.
- The surface does not claim AI-powered, LLM-powered, AI impact analysis, LLM
  impact analysis, embeddings, vector databases, prompt-based classification,
  or autonomous migration review.
- If implementation uses a section inside an existing route instead of the
  preferred standalone route, the exact placement rationale and link/discovery
  behavior are recorded in `implementation-state.md`.

### Requirement 2: Relate the story to the legacy .NET evidence lane

The future surface shall make clear that legacy data surface evidence is one
bounded lane within the broader legacy .NET evidence story, not a replacement
for the existing legacy lane or a stronger support claim.

Acceptance criteria:

- The page links to `/legacy-dotnet/evidence/` as the broader legacy .NET
  evidence lane.
- The page links to `/legacy-evidence/` for the hidden legacy evidence ledger
  and promotion boundary, and to `/legacy-modernization/evidence-map/` for
  modernization planning context.
- If `/legacy-evidence/` or `/legacy-modernization/evidence-map/` is
  unavailable at implementation time, the implementation records the route gap
  in `implementation-state.md` and omits or placeholders the link rather than
  linking to a missing route.
- The page distinguishes itself from `/legacy-dotnet/evidence/` as the narrower
  data-surface story: data metadata, mappings, SQL/query-facing references, and
  persistence context.
- The page distinguishes itself from `/legacy-evidence/` as a public-safe data
  surface explainer rather than the hidden legacy claim ledger.
- The page distinguishes itself from `/legacy-modernization/evidence-map/` as a
  proof-bounded data evidence story rather than a broad modernization planning
  map.
- The page does not promote hidden legacy .NET rows, private validation notes,
  hidden capability counts, or implementation-branch behavior into public
  claims.
- Any legacy data examples remain generic or public-demo-safe and must be tied
  to concept wording unless public-safe proof exists for stronger wording.

### Requirement 3: Cover the legacy data evidence families

The future page shall describe the static evidence families that may help
reviewers reason about legacy data surfaces while preserving limitations.

Acceptance criteria:

- The page covers design-time metadata, including checked-in model designers,
  descriptor files, generated-designer relationships, and metadata format
  clues.
- The page covers data model metadata, including entity-like descriptors,
  storage-object descriptors, column/property descriptors, relationship clues,
  safe hashes, and normalized identities when public-safe summaries support
  them.
- The page covers ORM or mapping clues, including mapping documents, mapping
  attributes, generated-code links, unsupported ORM metadata gaps, and
  framework-specific mapping families only as deterministic static evidence.
- The page covers SQL/query-facing references, including checked-in query text
  references, query builder calls, stored-procedure references, table or view
  references, and query-facing call sites only when they can be represented
  without raw SQL or private values.
- The page covers storage and persistence context, including provider family,
  connection-name metadata, configuration surface presence, repository/service
  code context, package or assembly clues, and database-owner handoff
  questions.
- Each family has a visible limitation that says what the family does not
  prove.
- The page avoids any statement that a database, schema, table, view, stored
  procedure, row, data value, permission, or migration result exists in a live
  system unless public-safe runtime proof is supplied outside this concept.

### Requirement 4: Provide an evidence-status matrix

The future page shall include an evidence-status matrix that separates visible
static evidence from proof gaps and non-claims.

Acceptance criteria:

- The matrix includes rows for design-time metadata, data model metadata,
  ORM/mapping clues, SQL/query-facing references, storage/persistence context,
  and analysis gaps.
- Each row includes evidence family, possible static evidence, evidence status,
  proof path requirement, limitation, owner follow-up, allowed wording, and
  forbidden wording.
- Evidence status labels use existing site vocabulary where possible, including
  concept, future, demo, hidden, reduced, partial, gap, or unknown.
- The complete allowed vocabulary for evidence status is: concept, future,
  demo, hidden, reduced, partial, gap, and unknown. New labels outside this set
  require a spec amendment.
- Rows with no public-safe proof use future, hidden, gap, unknown, or
  concept-only language rather than support, coverage, or detection claims.
- Rows with reduced semantic evidence label syntax fallback, project-load
  failure, build failure, generated-file uncertainty, unsupported metadata, or
  parser gaps as partial or reduced.
- The matrix includes database-owner and application-owner follow-up questions
  separately from TraceMap static evidence.
- The matrix states that evidence status is not release approval, migration
  readiness, runtime confirmation, production usage proof, or database
  compatibility proof.

### Requirement 5: Require proof paths before public conclusions

The future page shall define what proof is required before a legacy data
surface statement can become public copy.

Acceptance criteria:

- Every visible conclusion must connect to a deterministic rule ID or rule
  family when evidence exists.
- Evidence rows must include an evidence tier, coverage label, limitation, and
  proof path when public-safe proof exists.
- Public-safe proof paths may name output artifact families such as
  `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and
  `logs/analyzer.log` as artifact types, but they must not publish raw private
  contents from those artifacts.
- Public-safe proof may mention commit SHA, extractor versions, file paths, line
  spans, supporting IDs, and snippet hashes only when those details have been
  reviewed for public safety.
- The page links to `/proof-paths/`, `/validation/`, `/limitations/`,
  `/outputs/`, `/docs/`, `/legacy-validation/`, and the broader legacy pages
  unless implementation-time route review records a documented gap.
- The page explains that static evidence can narrow reviewer questions for
  humans, but it cannot prove live database state, data contents, query
  execution, runtime behavior, or migration success.
- The page does not say a surface is impacted unless a reducer-backed finding
  and public-safe supporting evidence are present.

### Requirement 6: Preserve public-safety and forbidden wording boundaries

The future page, metadata, discovery entry, tests, and validation fixtures shall
exclude raw or private material and overclaiming language.

Acceptance criteria:

- The page does not publish raw source snippets, raw SQL, raw config values,
  secrets, credentials, tokens, connection strings, database contents, table
  dumps, raw fact streams, raw SQLite content, analyzer logs, raw repository
  remotes, local absolute paths, generated scan directories, private sample
  names, hidden validation details, raw command output, private URLs, or
  credential-like values.
- The page does not publish raw model names, raw table names, raw column names,
  raw stored-procedure names, raw connection names, raw provider values, raw
  schema names, raw query strings, or raw file-system locations unless a future
  public-safe demo explicitly approves those values.
- The page may describe categories such as table reference, column descriptor,
  provider family, or connection-name metadata only as authored public-safe
  concepts or sanitized summaries.
- The page rejects overclaim phrases such as "proves database behavior",
  "proves query behavior", "proves runtime SQL", "reads production data",
  "executes SQL", "connects to your database", "validates migration success",
  "proves schema compatibility", "complete data coverage", "runtime data
  lineage", "production data understanding", "AI-powered impact analysis", and
  "LLM-powered migration analysis".
- Boundary statements may use category words such as raw SQL or connection
  strings only in forbidden-material lists or limitation copy.

### Requirement 7: Add metadata, discovery, and hidden/future/dev wording

The future implementation shall make the surface discoverable while preserving
concept-level claim boundaries and branch-specific wording.

Acceptance criteria:

- Discovery metadata labels the surface with `publicClaimLevel: concept`.
- Discovery metadata includes a preferred proof path, limitations, non-claims,
  neighboring route hints, and relation to the legacy .NET evidence lane.
- Metadata includes title, description, canonical URL, Open Graph fields, and
  `og:type` consistent with neighboring concept pages.
- Sitemap metadata includes the route if implementation chooses a standalone
  route.
- The page or section uses hidden/future/dev wording consistently: hidden for
  non-public or proof-gated material, future for unimplemented or unpromoted
  behavior, and dev only for behavior proven on a target branch but not claimed
  on the public main-line site.
- Dev wording requires evidence from a merged branch with passing validation and
  public-safe proof; it does not permit speculative, aspirational, or
  reviewable-but-not-merged branch claims on the deployed public main-line site.
- When a future item is implemented, merged, and validated with public-safe
  proof, the future label must be removed or replaced with dev-qualified or
  plain wording before the surface ships; leaving future on shipped and
  validated copy is a wording error.
- When a dev-labeled item is deployed to the public main-line site, the dev
  label must be removed or replaced with plain wording before or at deploy time;
  leaving dev on deployed main-line copy is a wording error equivalent to
  leaving future on shipped copy.
- The page does not expose branch-only implementation behavior as shipped
  public capability.
- If implementation adds cross-links from existing pages, link text uses
  concept or evidence-story wording and does not imply coverage or support.

### Requirement 8: Validate the future surface

The future implementation shall add focused validation for required copy,
route/discovery metadata, public safety, and proof-boundary language.

Acceptance criteria:

- Focused validation checks that the rendered surface contains `Public claim
  level: concept` and `No public conclusion without evidence`.
- Focused validation checks required evidence-family labels: design-time
  metadata, data model metadata, ORM/mapping clues, SQL/query-facing
  references, storage/persistence context, limitations, and analysis gaps.
- Focused validation checks that the evidence-status matrix contains each
  required column from Requirement 4.
- Focused validation checks that each evidence-status matrix row has a
  non-empty limitation cell.
- Focused validation checks required links and generated internal-link
  resolution.
- Focused validation checks discovery metadata, sitemap metadata when
  standalone, canonical metadata, Open Graph metadata, and preferred proof path.
- Focused validation rejects forbidden runtime, database-execution, data-access,
  migration-success, complete-coverage, AI/LLM, private/raw-artifact, and hidden
  implementation wording.
- Focused validation distinguishes forbidden wording the page asserts as its
  own claim from forbidden wording the page is required to render as a negated
  boundary statement, limitation, allowed/forbidden wording example, or
  evidence-status matrix `forbidden wording` column. The validator must not flag
  the page's own required boundary, limitation, non-claim, or forbidden-wording
  example copy while still rejecting affirmative overclaims and raw/private
  disclosures elsewhere on the surface. The reconciliation strategy must use
  affirmative-assertion matching: a denylist phrase triggers a failure only when
  it appears as the direct verb phrase or predicate of a sentence whose subject
  is the page, the tool, or TraceMap, for example `TraceMap executes SQL`, and
  does not appear inside a negated clause, such as `does not execute SQL`, a
  limitation sentence, such as `this does not prove runtime SQL`, an owner
  follow-up question, or a cell in the `Forbidden wording` column of the
  evidence-status matrix. Region-exclusion of entire table sections is not
  sufficient. Record the final strategy in `implementation-state.md` at
  implementation time. The recorded strategy must use subject-verb pattern
  matching at the sentence level: a denylist phrase triggers a failure only when
  the grammatical subject of its containing clause is the page, the tool, or
  TraceMap and the clause is affirmative. Substring matching alone does not
  satisfy this requirement.
- Denylist phrases are permitted in the evidence-status matrix `forbidden
  wording` column only when the column heading or cell text clearly marks the
  phrase as an example of what the page must not say. A bare affirmative
  sentence in that column cell is still a validation failure.
- Focused validation uses route-scoped checks for the new rendered surface and
  metadata so neighboring pages, spec files, validator comments, and fixtures do
  not create false positives.
- Focused validation enforces a bounded rendered word count between 450 and
  1800 visible body words, excluding navigation, footer, metadata, alt text,
  and hidden elements.
- Future implementation runs `npm run build` from `site/` and performs desktop
  and mobile browser sanity checks for layout or interaction changes.
- Spec-only work does not run site build or browser checks unless site source is
  changed.
