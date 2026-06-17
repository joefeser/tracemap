# RAG-Import Evidence Docs Requirements

## Introduction

Some TraceMap users need deterministic evidence documentation that can be
imported into external documentation search, knowledge-base, or RAG systems.
TraceMap already emits scans, combined indexes, dependency reports, route-flow
reports, value-origin evidence, vault exports, evidence packs, release-review
packets, and other bounded reports. This spec defines a downstream
documentation export that chunks those existing artifacts into citation-rich
Markdown and JSONL.

TraceMap remains a deterministic evidence document generator. It does not run
LLM calls, generate embeddings, choose a retriever, write to a vector database,
answer natural-language questions, or classify claims with prompts.

Public claim level: unavailable until a docs export implementation validates
public/demo fixtures and claim-level gates.

## Scope

In scope:

- A dedicated docs/export workflow that emits chunked, deterministic Markdown
  and JSONL suitable for external ingestion.
- A versioned output schema, initially `tracemap-evidence-docs.v1`.
- Stable chunk IDs, deterministic ordering, manifest metadata, input hashes,
  generation settings, and generated-file collision behavior.
- Claim-bearing chunks for sources, endpoints, route-flow, property/value-flow,
  dependency surfaces, data surfaces, package/config/query/SQL-shape evidence,
  legacy evidence families, release-review/impact summaries, gaps, and
  limitations.
- Claim-level filtering for `hidden`, `demo-safe`, and `public-safe` outputs,
  aligned with vault export and evidence-pack concepts.
- Citation requirements for rule IDs, evidence tiers, source labels, commit
  SHAs, coverage labels, file paths and line spans where available, supporting
  fact/edge/report IDs, extractor/version metadata, gaps, and limitations.
- Redaction and generated-output safety gates for Markdown, JSONL, manifest,
  and diagnostics.
- Tests and docs updates for deterministic output, schema shape, chunk identity,
  redaction, claim-level filtering, provenance completeness, and safe collision
  handling.

Out of scope:

- Scanner, reducer, language-adapter, route-flow, value-origin, vault, evidence
  pack, release-review, or combined-report analysis changes.
- Site implementation or site copy.
- Raw source snippets by default.
- LLM calls, embeddings, vector database writes, prompt-based classification,
  retrieval-system integration, natural-language Q&A, or RAG-stack selection in
  TraceMap core.
- Runtime execution, production traffic, deployment, service reachability,
  release approval, vulnerability, license, ownership, or business-impact
  conclusions unless a separate rule-backed input already states a bounded
  static finding.

## Requirements

### Requirement 1: Command and Input Boundaries

**User Story:** As a maintainer, I want a dedicated docs export command so that
external systems can ingest deterministic evidence without TraceMap becoming a
RAG tool.

#### Acceptance Criteria

1. WHEN the user runs `tracemap docs-export --index <index-or-combined.sqlite>
   --out <path>` THEN TraceMap SHALL read the supplied index read-only and emit
   deterministic evidence documentation.
2. WHEN a combined index is supplied THEN the exporter SHALL preserve source
   labels, source index IDs, commit SHAs, coverage labels, fact IDs, edge IDs,
   rule IDs, evidence tiers, extractor metadata, and limitations where safe.
3. WHEN a single-source index is supplied THEN the exporter SHALL emit only the
   evidence families supported by that index and SHALL mark unavailable combined
   views as gaps rather than implying absence.
4. WHEN compatible report inputs are supplied, such as route-flow, paths,
   reverse, vault graph, evidence pack, release-review, or combined report JSON,
   THEN the exporter SHALL consume only documented schema fields and SHALL
   preserve the source report IDs and limitations.
5. WHEN report inputs are omitted THEN the exporter SHALL still produce chunks
   available from the index and SHALL emit not-requested or unavailable gaps for
   requested-but-missing chunk families only.
6. WHEN the required `--index` is absent, unreadable, not a TraceMap index, or
   has zero usable source/fact/schema evidence for docs export THEN the command
   SHALL fail with a sanitized diagnostic naming required input kinds without
   echoing raw paths.
7. WHEN multiple inputs describe the same evidence THEN the exporter SHALL link
   chunks through stable supporting IDs and SHALL NOT deduplicate by display name
   alone.
8. WHEN an input has reduced coverage, schema gaps, unknown commit SHA,
   duplicate identity, selector gaps, unsupported optional tables, or truncation
   THEN the exporter SHALL preserve those gaps and SHALL NOT upgrade
   conclusions.
9. WHEN inputs are hidden/local/private by default THEN output SHALL remain
   `hidden` unless explicit claim-level filtering and proof metadata permit
   `demo-safe` or `public-safe`.
10. WHEN the command runs THEN it SHALL NOT mutate input indexes, source
    repositories, report JSON, evidence packs, or vault exports.

### Requirement 2: Output Contract and Storage Shape

**User Story:** As an automation author, I want stable Markdown, JSONL, and a
manifest so ingestion jobs can detect changes deterministically.

#### Acceptance Criteria

1. WHEN docs export writes an output directory THEN it SHALL create
   `manifest.json` for every successful run, plus `chunks.jsonl` and generated
   Markdown files unless `--format markdown`, `--format jsonl`, or
   `--format markdown,jsonl` narrows the selected content formats. `--format`
   SHALL NOT suppress `manifest.json`.
2. WHEN JSONL is emitted THEN each line SHALL be one UTF-8 JSON object using
   schema version `tracemap-evidence-docs.v1`.
3. WHEN chunk Markdown is emitted THEN every generated chunk file SHALL start
   with generated YAML frontmatter containing the schema version, generator name,
   chunk ID, chunk family, claim level, source labels, and deterministic content
   hash.
4. WHEN `manifest.json` is emitted THEN it SHALL include schema version,
   generator metadata, normalized generation settings, input summaries, input
   hashes, output file hashes, chunk counts by family and claim level, omitted
   counts, gaps, limitations, and a deterministic manifest content hash.
5. WHEN generated summary Markdown such as `README.md` or `index.md` is emitted
   THEN each summary file SHALL start with generated YAML frontmatter containing
   schema version, generator name, summary kind, claim level, source labels or
   summary scope, and deterministic content hash; summary files SHALL NOT
   require chunk ID or chunk family fields.
6. WHEN existing `manifest.json` or `chunks.jsonl` is classified as generated
   THEN the exporter SHALL verify a generated manifest marker, generator/schema
   values, manifest self-hash, and output file hashes; a `chunks.jsonl` without
   a valid generated manifest SHALL be treated as a user-file collision.
7. WHEN identical inputs and options are exported twice THEN Markdown, JSONL,
   and manifest bytes SHALL be stable.
8. WHEN output claim level is `demo-safe` or `public-safe` THEN the user SHALL
   provide `--date YYYY-MM`; hidden outputs without `--date` SHALL use the fixed
   generated-date sentinel `local-only` and SHALL remain byte-stable. The
   exporter SHALL NOT use wall-clock time in deterministic outputs.
9. WHEN an existing output directory contains valid generated files with matching
   schema, generator, and self-consistent hashes THEN the exporter MAY replace
   them after the new output passes all safety gates.
10. WHEN an existing generated file is stale, hand-edited, malformed, or has an
   invalid content hash THEN the exporter SHALL fail with
   `GeneratedFileStale` unless `--force` is supplied and the new output passes
   all non-overridable safety gates.
11. WHEN an existing output path contains a non-generated user file THEN the
   exporter SHALL fail with `UserFileCollision` and SHALL NOT overwrite it.
12. WHEN `--force` is supplied THEN it SHALL only bypass stale generated-file
    replacement; it SHALL NOT bypass claim-level, redaction, schema, unsafe
    value, non-generated collision, or private-path gates.
13. WHEN `--dry-run` is supplied THEN the exporter SHALL validate inputs,
    project chunks, run claim-level filtering, report planned files and counts,
    and write no output files.

### Requirement 3: Chunk Identity and Ordering

**User Story:** As a documentation pipeline owner, I want stable chunk IDs and
ordering so downstream indexers can update incrementally.

#### Acceptance Criteria

1. WHEN a chunk is emitted THEN it SHALL include a stable `chunkId`, `chunkType`,
   `chunkFamily`, `schemaVersion`, `claimLevel`, `title`, deterministic
   `sortKey`, and bounded content fields.
2. WHEN chunk IDs are derived THEN they SHALL use documented context-separated
   SHA-256 inputs and a documented truncation length.
3. WHEN chunk identity depends on source evidence THEN the ID input SHALL use
   stable source identity, source label, commit SHA presence/value when safe,
   rule IDs, evidence family, normalized safe selector metadata, and supporting
   fact/edge/report IDs.
4. WHEN supporting IDs are missing or unstable THEN the exporter SHALL emit a
   gap and SHALL NOT fall back to raw display names or local paths for identity.
5. WHEN two chunks would produce the same stable ID from different evidence THEN
   the exporter SHALL emit a duplicate-identity gap and SHALL NOT pick an
   arbitrary winner.
6. WHEN chunks are ordered THEN ordering SHALL use closed severity and family
   precedence followed by source label, evidence tier, rule ID, stable ID, file
   path, line span, and title using ordinal comparison.
7. WHEN Markdown files are named THEN names SHALL be derived from stable IDs or
   reviewed safe slugs and SHALL NOT reveal local paths, raw remotes, private
   labels, SQL/config values, source snippets, or secrets.
8. WHEN JSON arrays or manifest maps are emitted THEN they SHALL be serialized
   in canonical deterministic order.

### Requirement 4: Claim-Bearing Chunk Provenance

**User Story:** As a reviewer, I want every claim-bearing chunk to carry enough
evidence to audit the statement.

#### Acceptance Criteria

1. WHEN a chunk makes a claim about source, route, flow, surface, dependency,
   data, package, config, query, SQL shape, release review, impact, gap, or
   limitation evidence THEN the chunk SHALL include at least one rule ID and one
   evidence tier.
2. WHEN source identity is available THEN the chunk SHALL include source label,
   source scope, scan ID where available, commit SHA or approved commit identity,
   coverage label, extractor name, and extractor version where safe.
3. WHEN file-backed evidence is available THEN the chunk SHALL include
   repository-relative file path, start line, and end line where safe; otherwise
   it SHALL include explicit `null`, `unknown`, or redaction/gap metadata.
4. WHEN supporting fact IDs, edge IDs, route-flow IDs, value-flow IDs, path IDs,
   reverse IDs, release-review IDs, evidence-pack IDs, vault node/edge IDs, or
   report section IDs are available THEN the chunk SHALL preserve them in
   deterministic order.
5. WHEN evidence is syntax-only, textual, fallback, ambiguous, high fan-out,
   reduced-coverage, truncated, hidden, or lower-tier THEN the chunk SHALL
   preserve that weaker label and SHALL NOT promote it because it appears in
   documentation.
6. WHEN a chunk summarizes aggregate counts THEN the enclosing summary object
   SHALL cite a summary rule ID, evidence tiers, coverage labels, and limitations
   explaining what the counts can and cannot prove.
7. WHEN a chunk describes no-evidence or absence-like findings THEN it SHALL
   require full credible coverage from the source input; otherwise it SHALL emit
   `UnknownAnalysisGap`.
8. WHEN a chunk contains only gap or limitation text THEN it SHALL still carry a
   docs-export rule ID and evidence tier, usually `Tier4Unknown`.
9. WHEN a chunk emits `UnknownAnalysisGap` THEN the gap SHALL include a stable
   gap ID, `docs-export.gap.unknown-analysis.v1` or an underlying gap rule ID,
   `Tier4Unknown`, a closed reason code, affected chunk family, and supporting
   source/report IDs where available.

### Requirement 5: Chunk Families

**User Story:** As a user importing evidence into a knowledge base, I want
chunks grouped by evidence family so retrieval can cite the right artifact.

#### Acceptance Criteria

1. WHEN source overview evidence exists THEN the exporter SHALL emit
   `source-overview` chunks summarizing source identity, commit SHA, language,
   coverage, build status, extractor versions, generated artifacts, gaps, and
   limitations.
2. WHEN endpoint evidence exists THEN the exporter SHALL emit `endpoint` chunks
   for HTTP routes, HTTP client calls, endpoint alignment, route templates, and
   selector gaps using normalized safe route keys.
3. WHEN route-flow reports or compatible combined path evidence exist THEN the
   exporter SHALL emit `route-flow` chunks preserving route-flow
   classifications, entry evidence, static path rows, business/data logic rows,
   dependency surfaces, gaps, and limitations.
4. WHEN value-origin or property-flow evidence exists THEN the exporter SHALL
   emit `property-flow` chunks preserving argument, parameter, origin, alias,
   constructor/member, callback, async, boundary, and dependency-surface
   evidence without claiming full taint or runtime value flow.
5. WHEN dependency or data surfaces exist THEN the exporter SHALL emit
   `dependency-surface` and `data-surface` chunks for SQL/query shape,
   persistence, HTTP, package/config, event/message, WCF, remoting, WebForms,
   WinForms, and legacy data evidence.
6. WHEN package, config, query, or SQL-shape evidence exists THEN chunks SHALL
   render only safe metadata such as package identifiers, ecosystem, version
   metadata, query shape, table/column metadata, source kind, operation, and
   hashes allowed by the redaction policy; raw values are forbidden by default.
7. WHEN legacy evidence families exist THEN the exporter SHALL include chunks
   for WebForms, WCF/service-reference, ASMX/SOAP, Remoting, WinForms, build
   diagnostics, legacy environment, and baseline/evidence-pack summaries where
   compatible inputs expose safe fields.
8. WHEN release-review or impact summaries exist THEN the exporter SHALL emit
   `release-review` and `impact-summary` chunks preserving the underlying
   static classifications, reviewer checklist provenance, gap status, and
   limitations; it SHALL NOT turn those sections into release approval.
9. WHEN evidence gaps or limitations exist THEN the exporter SHALL emit
   `gap` and `limitation` chunks and link claim-bearing chunks to those records.
10. WHEN a chunk family is requested but unsupported by the input schema THEN the
    exporter SHALL emit a schema or unavailable-family gap rather than silently
    omitting the family.
11. WHEN `--families` selects a supported subset THEN unselected families SHALL
    be recorded as `not_requested` in manifest counts and SHALL NOT emit gaps;
    requested families with unavailable or incompatible evidence SHALL emit
    unavailable-family or schema gaps.

### Requirement 6: Claim Levels and Safety Model

**User Story:** As an operator, I want public/demo/hidden claim-level controls
so docs can be shared without leaking local or private evidence.

#### Acceptance Criteria

1. WHEN raw indexes or report JSON are exported without reviewed claim metadata
   THEN chunks SHALL default to `hidden`.
2. WHEN `--minimum-claim-level demo-safe` or `--minimum-claim-level public-safe`
   is supplied THEN the exporter SHALL filter chunks, citations, metadata,
   links, gaps, and summaries to evidence at or above the requested level.
3. WHEN source claim catalogs, vault graph metadata, or evidence-pack metadata
   are used to promote evidence THEN promotion SHALL match stable source identity
   and reviewed proof metadata; display names alone SHALL NOT promote evidence.
4. WHEN evidence-pack metadata is used THEN evidence-pack `local-only` SHALL map
   to docs-export `hidden`; only `demo-safe` or `public-safe` evidence-pack
   metadata matched by stable source identity MAY promote docs-export chunks.
5. WHEN filtering removes hidden evidence that affects interpretation THEN the
   exporter SHALL emit sanitized hidden-evidence omission gaps and recompute
   output classification and counts.
6. WHEN public/demo filtering leaves no visible claim-bearing non-gap chunks
   THEN the exporter SHALL fail rather than emit an apparently empty clean
   output, and this failure SHALL be unconditional rather than gated by an
   `--exit-code` option.
7. WHEN a chunk mixes evidence from different claim levels THEN the chunk SHALL
   be capped at the lowest safe claim level, or split into separate chunks if
   that preserves provenance more clearly.
8. WHEN an unsafe value would be needed to explain a claim THEN the exporter
   SHALL omit, category-label, or mark the value as a gap; it SHALL NOT include
   the raw value and SHALL NOT hash forbidden low-entropy, enumerable, secret,
   credential-like, snippet-derived, or production-data values.
9. WHEN generated public/demo files are validated THEN they SHALL pass the same
   private-path and unsafe-value categories expected by vault export and
   evidence-pack workflows.

### Requirement 7: Redaction and Prohibited Content

**User Story:** As a maintainer, I want automated guards that prevent unsafe
content from entering generated documentation.

#### Acceptance Criteria

1. WHEN Markdown, JSONL, manifest, frontmatter, file names, links, tags,
   diagnostics, or logs are generated THEN the exporter SHALL reject local
   absolute paths, home-directory fragments, raw remotes, raw SQL, raw config
   values, connection strings, raw URLs, endpoint addresses, captured
   credentials, tokens, secret-like strings, analyzer diagnostics, stack traces,
   source snippets, private sample identifiers, production data, and unsafe
   Markdown.
2. WHEN unsafe content is detected THEN diagnostics SHALL include category,
   output file, line or JSON pointer where available, and rule ID; diagnostics
   SHALL NOT echo the unsafe value.
3. WHEN Markdown text is rendered THEN user-controlled table cells, headings,
   frontmatter values, link labels, paths, and inline code fields SHALL be
   escaped or omitted.
4. WHEN JSONL text fields are rendered THEN all free-text fields SHALL come from
   bounded deterministic templates or already-safe source fields.
5. WHEN JSONL includes `bodyMarkdown` THEN that field SHALL be subject to the
   same Markdown escaping, prohibited-claim wording, and unsafe-value gates as
   the corresponding generated `.md` file.
6. WHEN raw source snippets are requested by a future explicit option THEN that
   option SHALL be out of v1 scope or SHALL require a separate spec update with
   claim-level, redaction, hashing, and validation rules.
7. WHEN a value resembles production data or a private identifier even after
   hashing THEN it SHALL be omitted or category-labeled for demo/public outputs.
8. WHEN docs export creates logs THEN logs SHALL use sanitized categories and
   output-relative paths only.
9. WHEN generated text includes wording that claims runtime execution,
   production traffic, release approval, vulnerability, ownership, deployment,
   service reachability, or business impact without an explicit supporting
   rule-backed input THEN validation SHALL reject the output with
   `docs-export.validation.prohibited-claim-wording.v1` and a sanitized
   docs-export validation finding.

### Requirement 8: External Ingestion Boundary

**User Story:** As a product owner, I want TraceMap docs to be easy to import
without TraceMap embedding itself into external RAG infrastructure.

#### Acceptance Criteria

1. WHEN docs export completes THEN it MAY print generated file counts and paths,
   but SHALL NOT call an embedding model, retriever, hosted vector database,
   search API, or LLM.
2. WHEN output metadata describes ingestion intent THEN it SHALL use neutral
   labels such as `external-ingestion-ready` and SHALL NOT name or configure a
   specific vendor or RAG stack.
3. WHEN chunks are emitted THEN they SHALL include citation metadata that an
   external system can index, but TraceMap SHALL NOT generate prompt text,
   question/answer pairs, chain-of-thought, relevance scores, or natural
   language answer templates.
4. WHEN documentation mentions downstream use THEN it SHALL say the external
   system is responsible for retrieval, embeddings, ranking, answer generation,
   access controls, and data retention.
5. WHEN users need a connector to a specific external system THEN that SHALL be
   a separate integration outside TraceMap core or a future explicitly scoped
   adapter that still consumes generated files rather than changing evidence
   meaning.

### Requirement 9: Rules, Documentation, and Limitations

**User Story:** As a contributor, I want every docs-export claim and gap tied to
documented rules and limitations.

#### Acceptance Criteria

1. WHEN docs export emits derived chunks, summaries, gaps, limitations,
   validation findings, or generated-file diagnostics THEN rule catalog entries
   SHALL exist before implementation emits those IDs.
2. WHEN a chunk cites underlying evidence THEN it SHALL preserve the original
   rule IDs and evidence tiers and MAY add a docs-export packaging rule ID.
3. WHEN docs export introduces new rule IDs THEN each rule SHALL document
   emitted rows/chunks, evidence tier semantics, and limitations.
4. WHEN docs export documentation is added THEN it SHALL explain command usage,
   input support, schema version, chunk identity, redaction, claim levels,
   collision behavior, generated artifacts, and external ingestion boundaries.
5. WHEN docs mention RAG, knowledge-base, or documentation-search use THEN they
   SHALL keep the boundary clear: TraceMap emits deterministic evidence docs;
   external systems ingest them.
6. WHEN docs export reuses vault, evidence-pack, route-flow, value-origin,
   release-review, or combined-report concepts THEN docs SHALL link or reference
   those workflows without duplicating their analysis logic.

### Requirement 10: Tests and Validation

**User Story:** As a maintainer, I want tests that prove docs export is stable,
safe, and complete enough for external ingestion.

#### Acceptance Criteria

1. Tests SHALL prove deterministic Markdown, JSONL, and manifest byte stability
   for identical inputs and options.
2. Tests SHALL prove hidden outputs without `--date` use the fixed `local-only`
   date sentinel and remain byte-stable across reruns.
3. Tests SHALL validate `tracemap-evidence-docs.v1` schema shape for manifest
   and JSONL chunks.
4. Tests SHALL prove stable chunk IDs and chunk ordering across reruns, output
   directories, and input row permutations.
5. Tests SHALL prove duplicate or ambiguous chunk identity emits gaps rather
   than arbitrary merges.
6. Tests SHALL prove every claim-bearing chunk includes rule IDs, evidence
   tiers, source provenance, coverage labels, commit identity where available,
   supporting IDs, extractor/version metadata where available, gaps, and
   limitations.
7. Tests SHALL prove lower-tier, reduced-coverage, hidden, fallback,
   high-fan-out, and syntax/textual evidence is not promoted.
8. Tests SHALL cover source overview, endpoint, route-flow, property-flow,
   dependency/data surface, package/config/query/SQL-shape, legacy,
   release-review/impact, gap, and limitation chunk families using synthetic or
   public-safe fixtures.
9. Tests SHALL prove claim-level filtering, source-claim promotion,
   evidence-pack `local-only` to `hidden` mapping, hidden omission gaps,
   mixed-claim chunk splitting or capping, and public/demo no-visible-evidence
   failures.
10. Tests SHALL prove redaction rejects planted local paths, raw remotes, raw SQL,
   config values, connection strings, raw URLs, endpoints, credentials, tokens,
   snippets, analyzer diagnostics, private identifiers, production data, unsafe
   Markdown, and unsafe file names without echoing unsafe values.
11. Tests SHALL prove `bodyMarkdown` in JSONL is scanned with the same Markdown
    and unsafe-value gates as generated `.md` files.
12. Tests SHALL prove prohibited-claim wording validation rejects unsupported
    runtime, production, approval, vulnerability, ownership, deployment,
    reachability, and business-impact claims.
13. Tests SHALL prove generated-file collision handling for valid generated
    files, stale generated files, malformed sentinels, non-generated user files,
    and `--force` boundaries, including that `--force` does not bypass
    non-generated user file collisions.
14. Tests SHALL prove missing report-derived stable IDs emit identity gaps
    rather than unstable chunks.
15. Tests SHALL prove requested unsupported `--families` values emit gaps and
    unselected supported families are recorded as `not_requested`.
16. Tests SHALL prove manifest `contentHash` blanks its hash field and remains
    stable across input ordering permutations.
17. Tests SHALL prove no LLM calls, embedding calls, vector database writes,
    prompt-based classification hooks, or network RAG integration code paths are
    introduced in TraceMap core.
18. Validation SHALL include `git diff --check`,
    `./scripts/check-private-paths.sh`, focused docs-export tests, and
    `dotnet build`/`dotnet test` when implementation code changes.
19. Validation SHALL run or explicitly defer pinned smoke checks from
    `docs/VALIDATION.md` when shared report, combined-index, route-flow,
    release-review, evidence-pack, or vault behavior changes.
