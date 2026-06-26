# Site TraceMap Tools Legacy Data Surface Story Design

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Overview

This design defines a future concept-level public surface for legacy data
surface evidence on `tracemap.tools`. The preferred implementation is a
standalone route at `/legacy-data-surface/`; a future implementation may choose
a subsection of `/legacy-dotnet/evidence/` only if route review shows the
standalone page would duplicate the legacy .NET evidence lane.

The surface should make old data evidence easier to discuss in manager,
reviewer, architect, and engineer conversations while staying bounded to
deterministic static evidence. It must not suggest TraceMap reads databases,
executes SQL, observes runtime SQL behavior, inspects production data, proves
schema compatibility, validates migration success, or ships complete legacy
data coverage.

## Route and Placement

- Preferred route: `/legacy-data-surface/`
- Preferred source file: `site/src/legacy-data-surface/index.html`
- Page type: concept-level public site page or section
- Public claim level: `concept`
- Discovery category: legacy evidence, modernization planning, or use-case
  adjacent, depending on the current discovery schema at implementation time
- Preferred proof path: `/legacy-evidence/` or `/proof-paths/`, with the final
  choice recorded in `implementation-state.md`
- Sitemap: add the route if standalone
- Navigation: do not add to top navigation unless a separate navigation spec
  asks for it

The page should cross-link to `/legacy-dotnet/evidence/`, `/legacy-evidence/`,
`/legacy-modernization/evidence-map/`, `/legacy-validation/`, `/proof-paths/`,
`/validation/`, `/limitations/`, `/outputs/`, and `/docs/` when those routes
exist at implementation time.

## Content Structure

1. Hero: name the concept, show `Public claim level: concept`, and state
   `No public conclusion without evidence`.
2. Placement note: explain that this is the legacy data surface story inside
   the broader legacy .NET evidence lane.
3. Evidence-family overview: introduce design-time metadata, data model
   metadata, ORM/mapping clues, SQL/query-facing references, storage and
   persistence context, and limitations.
4. Evidence-status matrix: map each family to possible static evidence, proof
   path requirement, limitation, owner follow-up, allowed wording, and forbidden
   wording.
5. Proof path section: define what evidence is needed before public copy can
   make a conclusion.
6. Boundary section: list non-claims and public-safety rules.
7. Link section: direct readers back to the legacy .NET lane, legacy evidence
   ledger, modernization map, proof paths, validation, outputs, docs, and
   limitations.

## Required Rendered Copy

The future surface must render these exact phrases:

`Public claim level: concept`

`No public conclusion without evidence`

Both lines may appear in a hero note, metadata band, or evidence boundary
section, but they must be visible body copy, not only metadata.

## Evidence-Family Model

The content model should use these families:

- Design-time metadata: checked-in model designers, descriptor files,
  generated-designer relationships, safe metadata format clues, and explicit
  parser gaps.
- Data model metadata: entity-like descriptors, storage-object descriptors,
  property or column descriptors, relationship clues, normalized identities,
  stable hashes, and safe redaction metadata.
- ORM/mapping clues: mapping documents, mapping attributes, generated-code
  links, unsupported ORM metadata gaps, and framework-specific mapping families
  represented only as static clues.
- SQL/query-facing references: checked-in query text references, query builder
  calls, stored-procedure references, table or view references, and
  query-facing call sites represented without raw SQL or private values.
- Storage/persistence context: provider family, connection-name metadata,
  configuration surface presence, repository/service code context, package or
  assembly clues, and database-owner handoff questions.
- Limitations and gaps: unsupported metadata, unsafe XML rejection, oversized
  metadata, malformed descriptors, missing generated code, failed project load,
  build failure, syntax fallback, and unknown evidence.

The page should not claim a data family is supported, detected, covered, or
complete unless public-safe proof supports that exact wording. Concept examples
should remain generic and should not publish private model, table, column,
connection, schema, stored-procedure, sample, customer, repository, or local
path names.

## Evidence-Status Matrix Design

The matrix should include at least these columns:

- Evidence family
- Possible static evidence
- Evidence status
- Proof path requirement
- Limitation
- Owner follow-up
- Allowed wording
- Forbidden wording

The abbreviated summary below shows the initial evidence status posture per
family. The rendered page matrix must include all eight columns from
Requirement 4: evidence family, possible static evidence, evidence status,
proof path requirement, limitation, owner follow-up, allowed wording, and
forbidden wording. Cells that cannot be filled at concept level should use
bounded placeholder language until public-safe proof is documented in
`implementation-state.md`.

Starting scaffold for the rendered eight-column matrix:

| Evidence family | Possible static evidence | Evidence status | Proof path requirement | Limitation | Owner follow-up | Allowed wording | Forbidden wording |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Design-time metadata | Checked-in model designer or descriptor files may be present. | concept or future | Public-safe descriptor summary with rule family, evidence tier, coverage label, and limitation. | Does not prove tool designer execution, database connectivity, or database state. | Application owner verifies intended model source and database owner verifies live storage questions. | Concept: design-time metadata may frame a review question. | Forbidden example: TraceMap proves design-time models execute against a database. |
| Data model metadata | Entity-like, storage-object, property, column, or relationship descriptors may be summarized safely. | concept or future | Sanitized descriptor summary with safe hashes or normalized identities before stronger wording. | Does not prove live schema, rows, data contents, permissions, or schema compatibility. | Database owner verifies schema and data-state questions; application owner verifies code intent. | Concept: data model metadata may identify static descriptors. | Forbidden example: TraceMap proves live schema compatibility or production data contents. |
| ORM/mapping clues | Mapping documents, attributes, generated-code links, or unsupported mapping metadata may be found. | future or gap | Future until public-safe rule IDs and evidence tiers are confirmed; unsupported ORM formats are labeled gap. | Does not prove ORM runtime behavior, lazy loading, query generation, package compatibility, or persistence success. | Application owner reviews mapping intent; database owner reviews storage implications. | Future: ORM or mapping clues may be extractable as static evidence; unsupported formats are analysis gaps. | Forbidden example: TraceMap proves ORM mappings work at runtime. |
| SQL/query-facing references | Query-facing call sites, query builder calls, stored-procedure references, table/view references, or checked-in query references may be summarized without raw SQL. | future or gap | Future until public-safe rule IDs confirm extraction without raw SQL exposure; unresolvable references are labeled gap. | Does not prove query execution, runtime SQL text, database results, performance, permissions, or production usage. | Application owner reviews call-site intent; database owner reviews live query, schema, and permission questions. | Future: SQL/query-facing references may be identified without raw SQL. | Forbidden example: TraceMap executes SQL or proves runtime SQL behavior. |
| Storage/persistence context | Provider family, connection-name metadata, configuration surface presence, repository/service context, package or assembly clues. | concept, future, or gap | Public-safe configuration or package summary with no raw values before stronger wording. | Does not prove connectivity, credential validity, live database existence, or storage state. | Database owner verifies connectivity and storage; service owner verifies runtime configuration. | Concept: persistence context may provide handoff clues. | Forbidden example: TraceMap connects to your database or proves storage state. |
| Analysis gaps | Failed project load, parser bounds, unsupported metadata, malformed descriptors, missing generated code, build failure, syntax fallback, or unknown evidence. | reduced, partial, unknown, or gap | Gap fact, reduced-coverage label, parser limitation, or validation result for the exact scenario. | Does not prove absence of data surfaces and does not convert failed build or load into clean coverage. | Engineering owner resolves build/load/parser gaps or accepts reduced evidence in review. | Gap or reduced: failed load, parser bounds, unsupported metadata, and syntax fallback remain labeled. | Forbidden example: failed load still proves complete data coverage. |

The concept-level placeholder text is the minimum allowed wording before
public-safe proof upgrades a row. Limitation cells must be non-empty at render
time; an empty limitation cell is a validation failure.

The complete allowed set for the `Evidence status` column is: concept, future,
demo, hidden, reduced, partial, gap, and unknown. Implementation must not
introduce new status labels outside this set without a spec amendment.

The rendered matrix must be an HTML table or equivalent structured element with
one column per required header and one row per evidence family. The focused
validator must locate columns by heading text, not by column index, so column
reordering does not silently break validation.

The matrix should separate application-owner follow-up, database-owner
follow-up, migration-owner follow-up, test-owner follow-up, and TraceMap static
evidence. This distinction keeps reviewer conversations useful without turning
static evidence into runtime or migration proof.

## Proof Path Requirements

Before a visible statement becomes stronger than concept/future language, the
future implementation must verify and document:

- rule ID or rule family;
- evidence tier;
- coverage label;
- limitation;
- public-safe proof path;
- commit SHA or public-safe source provenance when available;
- extractor version when available;
- file path and line span only when public-safe;
- snippet hash or supporting ID instead of raw snippets or raw SQL;
- whether the statement is true on the public main-line site, a public demo, or
  only a target branch.

Output artifact names such as `scan-manifest.json`, `facts.ndjson`,
`index.sqlite`, `report.md`, and `logs/analyzer.log` may be listed as artifact
families. The page must not publish raw contents from those artifacts.

## Relation To Existing Routes

- `/legacy-dotnet/evidence/`: broader legacy .NET evidence lane. The future
  data surface story is narrower and must not promote hidden lane rows.
- `/legacy-evidence/`: hidden legacy evidence ledger and promotion boundary.
  The future page can point to the ledger but must not expose hidden proof.
- `/legacy-modernization/evidence-map/`: broad modernization reviewer map. The
  future page focuses specifically on data surfaces and persistence context.
- `/legacy-validation/`: validation concept for legacy codebases and reduced
  coverage. The future page should point here for validation posture.
- `/proof-paths/`, `/validation/`, `/limitations/`, `/outputs/`, `/docs/`:
  proof, artifact, and limitation context for readers who need to inspect the
  evidence model.

Cross-links should use role-specific anchor text such as legacy .NET evidence
lane, hidden legacy evidence ledger, modernization evidence map, proof paths,
validation, output artifacts, and limitations. Link text must not imply
coverage, support, database execution, runtime proof, or migration readiness.
If `/legacy-evidence/` or `/legacy-modernization/evidence-map/` is unavailable
at implementation time, record the route gap in `implementation-state.md` and
omit or placeholder the link rather than linking to a missing route.

## Public Safety

The page, metadata, discovery entry, tests, and fixtures must not publish raw
source snippets, raw SQL, raw config values, secrets, credentials, tokens,
connection strings, database contents, table dumps, raw fact streams, raw
SQLite content, analyzer logs, raw repository remotes, local absolute paths,
generated scan directories, private sample names, hidden validation details,
raw command output, private URLs, or credential-like values.

The page must also avoid raw model names, table names, column names,
stored-procedure names, connection names, provider values, schema names, query
strings, and file-system locations unless a future public-safe demo explicitly
approves those values.

## Validation Design

Future implementation should add a focused validator following the neighboring
concept route patterns. The validator should check:

- rendered route or section contains `Public claim level: concept` and
  `No public conclusion without evidence`;
- rendered text contains all required evidence-family labels;
- evidence-status matrix contains the required columns and required family rows;
- required links are present and resolve in generated output;
- required link validation covers `/legacy-dotnet/evidence/`,
  `/legacy-evidence/`, `/legacy-modernization/evidence-map/`,
  `/legacy-validation/`, `/proof-paths/`, `/validation/`, `/limitations/`,
  `/outputs/`, and `/docs/`, unless a route is unavailable at implementation
  time and the documented gap is recorded in `implementation-state.md`;
- discovery metadata has `publicClaimLevel: concept`, preferred proof path,
  limitations, non-claims, and neighboring route hints;
- sitemap metadata includes the route if standalone;
- canonical, title, description, Open Graph fields, and `og:type` are present;
- rendered word count is between 450 and 1800 visible body words, excluding
  navigation, footer, metadata, alt text, and hidden elements;
- forbidden overclaim phrases are rejected in normalized rendered text,
  decoded HTML attributes, route metadata, discovery metadata, sitemap metadata,
  and route-scoped fixtures;
- route-scoped public-safety checks reject private/raw artifact wording without
  scanning unrelated site pages, spec files, validator comments, or neighboring
  route copy as if they were rendered public copy for this surface.
- The overclaim and private/raw denylists must catch affirmative overclaims and
  raw disclosures while excluding the surface's own required forbidden-wording
  matrix column, non-claim list, and negated limitation copy.
- Affirmative-assertion matching means a denylist phrase triggers a failure only
  when it appears as the direct verb phrase or predicate of a sentence whose
  subject is the page, the tool, or TraceMap, for example `TraceMap executes
  SQL`, and does not appear inside a negated clause, such as `does not execute
  SQL`, a limitation sentence, such as `this does not prove runtime SQL`, an
  owner follow-up question, or a cell in the `Forbidden wording` column of the
  evidence-status matrix. Record the chosen assertion-matching strategy in
  `implementation-state.md`. The strategy must use subject-verb pattern
  matching at the sentence level, not bare substring matching. A denylist phrase
  triggers a failure only when the grammatical subject of its containing clause
  is the page, the tool, or TraceMap and the clause is affirmative; bare
  substring matching alone does not satisfy this requirement.
- Denylist phrases in the matrix `forbidden wording` column are allowed only
  when the column heading or cell text clearly labels them as examples of what
  the page must not say. A bare affirmative sentence in that column cell is
  still a validation failure.

Suggested overclaim denylist phrases include: proves database behavior, proves
query behavior, proves runtime SQL, reads production data, executes SQL,
connects to your database, validates migration success, proves schema
compatibility, complete data coverage, runtime data lineage, production data
understanding, AI-powered impact analysis, and LLM-powered migration analysis.

Suggested private/raw denylist phrases include: raw source snippet, raw SQL,
raw config value, connection string, credential, token, database contents, table
dump, raw fact stream, raw SQLite content, analyzer log, raw remote, local
absolute path, generated scan directory, private sample name, hidden validation
detail, raw command output, and private URL.

Implementation validation should run the standard site validation/build checks,
private-path guard, and desktop/mobile browser sanity checks because this is a
public site surface. Spec-only authoring does not run site build or browser
checks.
