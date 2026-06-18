# Evidence Export Usability Polish Requirements

## Introduction

TraceMap already has deterministic evidence exports for Markdown vault
navigation and RAG/evidence documentation. The current exports preserve safety
and static evidence boundaries, but the first review experience can still feel
too internal-ID-heavy for humans and too mechanically chunked for downstream
retrieval tools.

This spec defines a public-safe usability polish layer for:

- `tracemap vault export` navigation, index organization, titles, aliases,
  tags, and graph categories;
- `tracemap docs-export` chunk titles, question-oriented chunk families, and
  claim/citation-first records.

The feature remains a deterministic export over TraceMap evidence. TraceMap
core must not call LLMs, create embeddings, write vector databases, or use
prompt-based classification. RAG or vector systems may consume TraceMap
evidence, but their output cannot become evidence for TraceMap conclusions.

Public claim level: concept until implemented and validated with public-safe
fixtures.

## Scope

In scope:

- Better `Start Here` and generated index pages for vault exports.
- Folder and index organization for endpoints, symbols, dependency surfaces,
  routes, gaps, limitations, and rules.
- Safe human-friendly display titles while preserving stable IDs and hashes in
  frontmatter, graph JSON, and chunk metadata.
- Aliases and tags for evidence tiers, coverage labels, surface kinds,
  classifications, needs-review states, gaps, and limitations.
- Safer and more useful graph node and edge category vocabularies.
- Optional important-only or review-friendly graph mode when the selection
  policy is deterministic and rule-backed.
- Preservation of generated-output sentinels, content hashes, idempotent rerun
  behavior, and user-file collision protection.
- Friendlier docs-export chunk titles and section names.
- Question-oriented docs-export chunk families for endpoints, code touching
  data surfaces, packages, snapshot changes, weak evidence, and gaps.
- Claim/citation-first chunk schema carrying rule IDs, evidence tiers,
  supporting fact/report IDs, safe source spans, coverage labels, and
  limitations.
- Hidden/local profile behavior that may redact unsafe values where possible
  instead of hard-failing, while public-safe and demo-safe profiles remain
  strict.
- Tests, docs, and validation guidance proving deterministic output, safety,
  and no overclaiming.

Out of scope:

- Scanner, reducer, adapter, or evidence extraction changes except for
  additive metadata needed by the exporters to render existing evidence.
- Any LLM call, embedding generation, vector database write, retrieval engine,
  prompt classifier, semantic search service, or model-generated decision in
  TraceMap core.
- Treating RAG answers, vector similarity, user prompts, or generated summaries
  as evidence for TraceMap conclusions.
- Runtime proof, production telemetry, ownership inference, vulnerability
  scanning, release approval, service reachability, traffic analysis, or
  business impact claims.
- Publishing raw source snippets, raw SQL, config values, connection strings,
  endpoint URLs, secrets, local absolute paths, raw remotes, private repo names,
  exact private routes, analyzer logs, or private sample identifiers.
- Site work, hosted demos, or marketing copy.

## Requirements

### Requirement 1: Vault Start Here And Top-Level Navigation

**User Story:** As a reviewer opening a generated vault, I want the first page
to explain what evidence exists, what is partial, and where to begin without
requiring me to understand TraceMap internals first.

#### Acceptance Criteria

1. WHEN `tracemap vault export` writes Markdown THEN it SHALL generate a
   `Start Here` note or improve the existing top-level entry note using a
   deterministic file name and generated-file sentinel.
   Existing generated `README.md` or `index.md` entry contracts SHALL be
   preserved or explicitly versioned and cross-linked so current consumers are
   not silently broken.
2. WHEN the start page is generated THEN it SHALL summarize export
   classification, input types, visible source count, non-gap node count, edge
   count, gap count, limitation count, coverage labels, and omitted hidden
   evidence counts when available.
3. WHEN coverage is reduced, partial, unsupported, filtered, or unknown THEN
   the start page SHALL make that status visible near the top and SHALL NOT
   phrase the export as complete.
4. WHEN the export includes endpoints, routes, symbols, dependency surfaces,
   packages, rules, gaps, or limitations THEN the start page SHALL link to
   deterministic index pages for those categories.
5. WHEN no evidence exists for a category THEN the start page MAY list the
   category as unavailable, but SHALL distinguish no compatible input from no
   static evidence under credible coverage.
6. WHEN public/demo output is generated THEN the start page SHALL include only
   public/demo-safe values and SHALL fail under existing strict validation if an
   unsafe value would be rendered.
7. WHEN hidden/local output is generated THEN the start page MAY include
   redacted, hashed, or category-only placeholders, but SHALL label the export
   as hidden/local and partial when redaction affects interpretation.

### Requirement 2: Vault Folder And Index Organization

**User Story:** As a maintainer, I want vault folders and indexes to group
evidence by user-review concepts rather than a flat set of hashed notes.

#### Acceptance Criteria

1. WHEN vault notes are generated THEN the output layout SHALL include or
   preserve deterministic folders for endpoints, symbols, dependency surfaces,
   packages, rules, gaps, limitations, reports, and sources where those concepts
   exist. A `routes/` folder is a net-new additive surface and SHALL be added
   only when route-specific evidence or route indexes can be generated without
   breaking existing output contracts.
2. WHEN a folder contains generated notes THEN it SHALL have an index note with
   deterministic sorted links, counts, coverage labels, and visible limitations
   for that folder.
3. WHEN dependency surfaces are indexed THEN the index SHALL group by
   `surfaceKind` from a closed vocabulary and SHALL NOT invent surface kinds
   from display names.
4. WHEN route evidence is present THEN route index pages SHALL distinguish
   endpoint routes, route-flow evidence, static path evidence, and route gaps
   using rule IDs and evidence tiers.
5. WHEN symbol evidence is present THEN symbol indexes SHALL preserve source
   scope and stable symbol identity where safe; unsafe symbol names SHALL be
   redacted, hashed, or omitted according to claim level.
6. WHEN rule, gap, or limitation index pages are generated THEN they SHALL link
   back to supporting nodes, edges, chunks, or report sections where safe.
7. WHEN the exporter reruns with identical input and options THEN folder names,
   note names, index ordering, links, anchors, frontmatter, and content hashes
   SHALL be byte-stable.
8. WHEN non-generated user-authored notes exist in the output tree THEN the
   exporter SHALL preserve existing collision safety and SHALL NOT overwrite
   them.

### Requirement 3: Safe Display Titles, Aliases, And Metadata

**User Story:** As a human reviewer, I want readable note titles and aliases
without sacrificing stable IDs, hashes, or public-safety guarantees.

#### Acceptance Criteria

1. WHEN a generated note or chunk has a safe human-readable name THEN the
   exporter SHALL use it as display text and SHALL preserve the canonical
   stable ID, supporting hashes, and source identity in metadata/frontmatter.
2. WHEN a safe human-readable name is unavailable THEN the exporter SHALL use a
   deterministic fallback title based on category and stable ID, not an unsafe
   raw value.
3. WHEN display titles are generated THEN they SHALL be derived from documented
   safe fields such as rule ID, evidence tier, public-safe source label,
   surface kind, classification, coverage label, safe relative path, or stable
   TraceMap ID.
4. WHEN aliases are emitted in frontmatter THEN aliases SHALL be bounded arrays
   from safe display titles, closed vocabularies, or stable IDs; aliases SHALL
   NOT contain raw local paths, raw remotes, endpoint URLs, SQL, config values,
   source snippets, secrets, private names, or exact private routes.
5. WHEN titles or aliases are redacted, hashed, category-labeled, or omitted
   THEN the note/chunk SHALL include a rule-backed limitation or redaction
   record unless the omission is cosmetic and does not affect interpretation.
6. WHEN public/demo output is selected THEN unsafe-looking display names SHALL
   fail or be filtered under strict policy; hidden/local redaction behavior
   SHALL NOT make public/demo output pass.
7. WHEN frontmatter metadata is generated THEN it SHALL remain parseable using
   the existing narrow YAML profile and SHALL keep generated sentinel and
   content hash semantics intact.

### Requirement 4: Tags And Closed Navigation Vocabularies

**User Story:** As a reviewer using Markdown tooling, I want tags that help me
filter evidence by strength, coverage, and review state without introducing
unsafe or unstable labels.

#### Acceptance Criteria

1. WHEN tags are emitted THEN every tag SHALL come from a documented closed
   vocabulary or a stable safe ID category and SHALL be lowercase,
   deterministic, bounded, and public-safe.
2. Tags SHALL cover evidence tiers, coverage labels, claim levels, node kinds,
   edge kinds, surface kinds, classifications, needs-review states, gap kinds,
   and limitation kinds where those values exist.
3. WHEN a tag would require an unsafe raw value THEN the exporter SHALL emit a
   safe category tag or omit the tag with a limitation; it SHALL NOT render the
   raw value.
4. WHEN tags are emitted in Markdown frontmatter or body text THEN tag ordering
   SHALL be deterministic, byte-stable across reruns, and documented in
   exporter design or user-facing docs.
5. WHEN a vocabulary changes during implementation THEN `rules/rule-catalog.yml`
   or exporter documentation SHALL describe the new rule IDs, limitations, and
   compatibility expectations.
6. WHEN a tag indicates `needs-review`, `gap`, `partial`, or reduced coverage
   THEN generated pages SHALL avoid wording that implies definite impact,
   runtime behavior, or absence of dependency.

### Requirement 5: Graph Node And Edge Category Polish

**User Story:** As a graph viewer, I want node and edge categories that are
useful for review and safe to browse without overclaiming static evidence.

#### Acceptance Criteria

1. WHEN `graph.json` is generated THEN each node and edge SHALL keep stable IDs,
   rule IDs, evidence tiers, supporting fact/report IDs, coverage labels,
   classification where applicable, limitations, and claim level.
2. WHEN graph node categories are generated THEN the exporter SHALL use a closed
   vocabulary that distinguishes source, endpoint, route, symbol, dependency
   surface, package, rule, gap, limitation, and report nodes where applicable.
   Chunk/reference nodes are net-new additive surfaces and SHALL be emitted only
   when docs-export and vault outputs are explicitly linked by stable IDs.
3. WHEN graph edge categories are generated THEN the exporter SHALL distinguish
   static evidence relationships such as `describes`, `supports`,
   `links-to-rule`, `has-limitation`, `has-gap`, `static-path-evidence`,
   `route-flow-evidence`, `surface-evidence`, `symbol-evidence`, and
   `report-evidence` without claiming runtime execution.
4. WHEN an edge comes from lower-tier, reduced, weak, or review-only evidence
   THEN its category, evidence tier, and classification SHALL preserve that
   weakness and SHALL NOT be promoted by graph inclusion.
5. WHEN duplicate, ambiguous, unsafe, or unsupported graph identity exists THEN
   the exporter SHALL emit a rule-backed gap rather than choosing a display-name
   match.
6. WHEN graph categories are used for Obsidian or another Markdown graph view
   THEN the output SHALL remain plain JSON and Markdown and SHALL NOT require
   proprietary tooling.
7. WHEN a relationship cannot be proven from existing static evidence THEN the
   exporter SHALL omit it or emit a gap; it SHALL NOT create speculative
   runtime, ownership, vulnerability, deployment, release, traffic, or business
   impact edges.

### Requirement 6: Optional Deterministic Review-Friendly Graph Mode

**User Story:** As a reviewer preparing a walkthrough, I want an optional graph
mode that emphasizes important evidence without relying on subjective ranking.

#### Acceptance Criteria

1. IF the implementation includes an important-only or review-friendly graph
   mode THEN it SHALL be opt-in and deterministic.
2. The mode SHALL include a node or edge when any documented inclusion
   predicate is true: evidence tier is `Tier3SyntaxOrTextual` or
   `Tier4Unknown`; classification contains `NeedsReview` or another documented
   weak/static review label; coverage label is reduced, partial, unsupported,
   or unknown; node kind is `gap` or `limitation`; a supported report input
   marks the item unstable or unresolved; or rule ID belongs to a documented
   gap, omission, or safety rule family. Snapshot-change membership SHALL NOT
   be used by vault graph review mode unless a compatible future diff or
   release-review input is explicitly added to the vault exporter.
3. The mode SHALL record selection predicates, selected counts, omitted counts,
   and partial/export classification in `graph.json`, manifest metadata, and
   relevant index pages.
4. The mode SHALL NOT use LLMs, embeddings, vector similarity, prompt scoring,
   runtime telemetry, popularity, ownership, production status, or external
   service calls to decide importance.
5. The full generated output SHALL remain available unless the caller
   explicitly requests a filtered-only output set, and filtered-only output
   SHALL be labeled partial.
6. IF deterministic selection cannot be specified clearly for the first
   implementation slice THEN the mode SHALL be deferred rather than
   implemented heuristically.
7. IF implementation cannot define crisp boolean logic for every review-mode
   predicate by the end of the first graph-mode implementation PR THEN the
   feature SHALL be explicitly deferred and recorded as a rule-backed gap or
   limitation.

### Requirement 7: Docs-Export Friendly Chunk Titles And Sections

**User Story:** As a documentation or retrieval-system user, I want chunk titles
and sections that describe evidence questions directly while preserving exact
citations.

#### Acceptance Criteria

1. WHEN docs-export emits Markdown or JSONL chunks THEN each chunk SHALL include
   a safe, human-friendly `title` and a deterministic `sectionTitle`.
2. Titles SHALL be derived from question-oriented evidence categories, safe
   public labels, rule IDs, surface kinds, evidence tiers, coverage labels, or
   stable IDs; titles SHALL NOT require raw private values.
3. WHEN a chunk title is redacted or fallback-generated THEN the chunk SHALL
   preserve the stable chunk ID and include a redaction or limitation record
   where interpretation is affected.
4. WHEN Markdown chunks are generated THEN headings SHALL be deterministic,
   bounded, and safe for downstream citation.
5. WHEN JSONL chunks are generated THEN `title`, `sectionTitle`, `chunkFamily`,
   `claim`, `citations`, `limitations`, and `redactions` SHALL be explicit
   fields, not only prose inside `bodyMarkdown`.
6. WHEN public/demo docs are generated THEN unsafe title inputs SHALL be
   rejected, filtered, redacted, or category-labeled according to strict
   public/demo rules.

### Requirement 8: Question-Oriented Docs-Export Chunk Families

**User Story:** As someone importing TraceMap docs into a RAG system, I want
chunks grouped around likely review questions so retrieval can cite evidence
without reinterpreting raw artifacts.

#### Acceptance Criteria

1. WHEN docs-export chunk families are configured THEN the exporter SHALL
   support or plan additive families for endpoint evidence, code touching data
   surfaces, packages/dependencies, snapshot changes, weak evidence, gaps, and
   limitations. Question-oriented families MAY be additive `questionFamilies`
   views over existing canonical `chunkFamily` values rather than new physical
   chunk records. A chunk MAY belong to multiple question-family views when a
   primary question and one or more cross-cutting review views apply.
2. Endpoint-oriented chunks SHALL answer static questions such as "what evidence
   describes this endpoint or route?" and SHALL include rule IDs, tiers,
   supporting IDs, coverage labels, safe spans, and limitations.
3. Data-surface-oriented chunks SHALL answer static questions such as "what
   code has static evidence of touching this data surface?" without rendering
   raw SQL, connection strings, config values, or snippets.
4. Package-oriented chunks SHALL describe dependency metadata and related static
   evidence without claiming vulnerability, license, compatibility, ownership,
   or runtime usage unless a separate rule-backed input exists.
5. Snapshot-change chunks SHALL summarize added, removed, changed, unchanged,
   unstable, or unknown evidence only from a documented compatible input such
   as `--release-review-report` or a future explicitly supported diff/snapshot
   report. When no compatible input is supplied, docs-export SHALL emit the
   unsupported-family gap required by this section, with rule ID
   `docs-export.gap.unsupported-question-family.v1`, instead of synthesizing
   change evidence. The existing `docs-export.gap.unsupported-family.v1` rule
   remains responsible for unsupported canonical CLI chunk families; the new
   question-family rule applies only to additive question-oriented views.
6. Weak-evidence and gap chunks SHALL make lower-tier, ambiguous, unsupported,
   reduced-coverage, and needs-review evidence easy to retrieve and SHALL NOT
   hide limitations in metadata only.
7. WHEN a requested canonical chunk family cannot be supported by the input
   schema THEN docs-export SHALL emit the existing
   `docs-export.gap.unsupported-family.v1` gap and record the family as
   unsupported or unavailable, not silently omit it. WHEN a requested additive
   `questionFamilies` view cannot be supported by the input schema THEN
   docs-export SHALL emit `docs-export.gap.unsupported-question-family.v1`.
8. WHEN family names or chunk types are added THEN schema changes SHALL be
   additive and documented.

### Requirement 9: Claim/Citation-First Docs-Export Schema

**User Story:** As a reviewer reading imported evidence, I want every chunk to
state the claim, cite the supporting TraceMap evidence, and show limitations
before any narrative text.

#### Acceptance Criteria

1. WHEN docs-export emits a chunk THEN the chunk SHALL include a concise
   evidence claim or gap statement that is directly backed by TraceMap fact,
   edge, report, or graph IDs.
2. Every chunk SHALL carry rule IDs, evidence tiers, supporting fact IDs,
   supporting edge IDs, supporting report IDs, source identities where safe,
   safe source spans where available, coverage labels, claim level, redaction
   records, and limitations.
3. WHEN a claim uses lower-tier, reduced, weak, inferred-from-structure, or
   syntax-only evidence THEN the chunk SHALL label that weakness near the claim
   and SHALL NOT overstate certainty.
4. WHEN a chunk has no positive evidence because it describes a gap THEN the
   claim SHALL be a gap statement with `Tier4Unknown` or another documented tier
   and a rule ID.
5. WHEN `bodyMarkdown` is emitted THEN it SHALL be derived from the structured
   claim/citation fields, not the other way around.
6. WHEN downstream RAG import consumes chunks THEN the structured fields SHALL
   be sufficient to preserve TraceMap evidence boundaries without requiring
   the importer to parse prose.
7. WHEN a vector database or LLM uses these chunks externally THEN that external
   system SHALL remain a consumer only and SHALL NOT be represented as source
   evidence in TraceMap outputs.

### Requirement 10: Hidden/Local Redaction Profile For Docs And Vault

**User Story:** As an operator working locally, I want hidden/local exports to
remain useful when values are unsafe for public output, without weakening
public/demo strictness.

#### Acceptance Criteria

1. WHEN output classification is hidden/local THEN vault export and docs-export
   MAY redact, hash, category-label, or omit unsafe-looking display values
   where possible instead of failing the entire export.
2. Hidden/local redaction SHALL never render raw secrets, credentials, tokens,
   private keys, authorization headers, connection strings, raw SQL, config
   values, raw URLs, raw remotes, local absolute paths, exact private routes,
   private repo names, source snippets, analyzer logs, stack traces, production
   data, or private sample identifiers.
3. WHEN hidden/local redaction changes a title, alias, tag, node, edge, chunk,
   or citation location in a way that affects interpretation THEN the exporter
   SHALL emit a rule-backed redaction record, limitation, or gap.
4. WHEN public-safe or demo-safe output is selected THEN strict validation SHALL
   remain the default and hidden/local redaction SHALL NOT be used to downgrade
   unsafe public/demo failures into acceptable output.
5. WHEN a value cannot be safely redacted, hashed, category-labeled, or omitted
   without losing required evidence identity THEN the exporter SHALL fail with
   a sanitized diagnostic.
6. WHEN redaction rules are implemented THEN tests SHALL prove both hidden/local
   success and public/demo rejection for representative unsafe-looking values.
7. Implementation SHALL document concrete examples of redactable, hashable,
   category-only, omitted, and hard-fail value categories for hidden/local
   profiles in vault export and docs-export user documentation before merging
   the first PR that implements new hidden/local redaction behavior.

### Requirement 11: Generated Output Safety And Collision Preservation

**User Story:** As a vault or docs user, I want usability changes to preserve
existing generated-file safety guarantees.

#### Acceptance Criteria

1. WHEN generated Markdown, JSON, or JSONL is written THEN existing generated
   sentinel, schema version, generator ID, content hash, and canonical
   serialization behavior SHALL be preserved or versioned explicitly.
2. WHEN existing generated files have valid sentinels and content hashes THEN
   they MAY be replaced only after all newly generated content passes safety
   validation.
3. WHEN existing generated files are stale, malformed, hand-edited, or
   hash-invalid THEN export SHALL fail unless `--force` is supplied and the new
   content passes every validation gate.
4. `--force` SHALL NOT bypass claim-level gates, public/demo strictness,
   hidden/local hard-fail categories, schema validation, user-file collision
   checks, or private-path guards.
5. WHEN non-generated files collide with planned generated output THEN the
   exporter SHALL fail with sanitized collision diagnostics and SHALL NOT
   overwrite user notes or imported files.
6. WHEN output validation fails after planning but before writing THEN no
   partial output files SHALL be written.
7. WHEN diagnostics are emitted THEN they SHALL include rule ID, evidence tier,
   sanitized category, and output-relative location; diagnostics SHALL NOT echo
   unsafe raw values.

### Requirement 12: Validation And Documentation

**User Story:** As a maintainer, I want implementation tasks and validation to
prove the polish is deterministic, public-safe, and compatible with existing
exports.

#### Acceptance Criteria

1. Implementation SHALL update user-facing docs for vault export and
   docs-export to describe new navigation, chunk, tag, alias, graph-mode,
   redaction, and limitation behavior.
2. Implementation SHALL update `rules/rule-catalog.yml` for new exporter rule
   IDs, gap IDs, redaction IDs, and documented limitations.
3. Tests SHALL cover deterministic byte stability for Markdown, `graph.json`,
   `chunks.jsonl`, and manifest/index files affected by the polish.
4. Tests SHALL cover strict public/demo rejection and hidden/local
   redaction/omission behavior.
5. Tests SHALL cover user-file collision, stale generated file, and `--force`
   behavior for the changed export surfaces.
6. Tests SHALL cover safe titles, aliases, tags, chunk families, graph
   categories, and claim/citation-first schema fields.
7. Tests SHALL prove no raw private paths, private repo names, exact private
   routes, raw SQL, raw config values, secrets, raw remotes, or source snippets
   appear in public/demo generated outputs.
8. Tests SHALL prove canonical unsupported-family gaps and additive
   unsupported-question-family gaps do not both fire for the same unsupported
   input condition.
9. Tests SHALL prove question-family metadata is additive over existing
   canonical chunk families, supports multiple memberships where applicable,
   and does not change existing chunk identities.
10. Tests SHALL prove existing vault entry-note contracts such as generated
   `README.md` or `index.md` are preserved or explicitly versioned.
11. Tests SHALL prove weak-evidence, gap, and limitation question views return
   the same underlying chunk IDs, evidence tiers, and limitations as their
   canonical chunks.
12. Validation SHALL include relevant focused exporter tests, `dotnet build
   src/dotnet/TraceMap.sln`, `dotnet test src/dotnet/TraceMap.sln`,
   `./scripts/check-private-paths.sh`, and `git diff --check`, unless explicitly
   deferred with a documented reason.
