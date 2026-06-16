# Evidence Graph Vault Export Requirements

## Introduction

TraceMap already emits rule-backed static evidence through indexes and reports,
but reviewers sometimes need a navigable local view that is easier to explore
than JSON, Markdown tables, or SQLite queries. This spec defines a deterministic
evidence graph/vault export that turns existing TraceMap artifacts into a
directory of Markdown notes plus an optional JSON graph manifest.

The first target should be Obsidian-compatible Markdown because it is useful for
local demos and manager-friendly review, but the feature is an export format,
not a proprietary UI, hosted site, graph database, AI assistant, runtime trace,
or new analysis engine.

The export must preserve TraceMap's evidence model: rule IDs, evidence tiers,
coverage labels, commit SHAs, extractor versions, file spans when safe,
supporting fact/edge IDs, gaps, and documented limitations. It must not infer
runtime behavior, service reachability, production usage, business impact,
release safety, vulnerability status, or reducer impact beyond the source
artifacts' own static evidence.

Public claim level: concept until implemented and validated. Demo or public
claims require public-safe input artifacts and successful export validation.

## Scope

In scope:

- A Kiro spec for exporting existing TraceMap evidence into a deterministic
  Markdown vault and optional JSON graph manifest.
- Input boundaries for combined indexes, portfolio reports, paths/reverse
  reports, release-review reports, and evidence packets where schemas are
  compatible.
- Node and edge vocabularies that cover source, endpoint, surface, package,
  SQL/query, WCF, Remoting, WebForms, legacy, symbol, rule, limitation, and gap
  evidence.
- Obsidian-friendly links, backlinks, tags, and YAML frontmatter only when safe
  and deterministic.
- Redaction, claim-level filtering, generated-output sentinels, deterministic
  ordering, idempotent reruns, and stale-output validation.
- Tests and validation commands for private-path safety, deterministic bytes,
  hidden evidence filtering, stale generated output, and public-safe wording.

Out of scope:

- Scanner, reducer, extractor, alignment, path, reverse, release-review, or
  portfolio inference changes.
- Site pages, hosted demos, screenshots, or marketing copy.
- Runtime topology, telemetry, ownership, traffic, deployment, service catalog,
  vulnerability, license, or release-approval analysis.
- LLM calls, embeddings, vector databases, prompt-based classification, semantic
  search, or model-generated graph decisions.
- Raw source snippets, raw SQL, raw config values, endpoint values, connection
  strings, raw remotes, local absolute paths, private repo names, hostnames,
  usernames, branch names, secrets, tokens, analyzer logs, raw scan artifacts,
  or raw evidence packet internals in generated public/demo outputs.

## Requirements

### Requirement 1: Export Inputs And Boundaries

**User Story:** As a reviewer, I want a graph export from existing TraceMap
artifacts so that I can navigate evidence without rerunning analysis or adding a
new inference engine.

#### Acceptance Criteria

1. WHEN the exporter is implemented THEN it SHALL read existing TraceMap
   artifacts and SHALL NOT mutate input SQLite indexes, reports, evidence
   packets, or source repositories.
2. WHEN a combined index is supplied THEN the exporter SHALL treat the combined
   database as static evidence and SHALL preserve source labels, source index
   IDs, commit SHAs, coverage labels, fact IDs, edge IDs, rule IDs, evidence
   tiers, and limitations where safe.
3. WHEN portfolio, paths, reverse, release-review, or evidence-pack reports are
   supplied THEN the exporter SHALL consume only documented schema fields and
   SHALL emit schema-compatibility gaps for unknown, missing, or incompatible
   report shapes.
4. WHEN multiple inputs describe the same evidence THEN the exporter SHALL use
   stable identities and supporting IDs to merge or cross-link nodes
   deterministically; it SHALL NOT merge by display name alone.
5. WHEN an input has reduced coverage, unknown source identity, duplicate
   identity, schema gaps, selector gaps, or truncation gaps THEN the export
   SHALL preserve those gaps and SHALL NOT upgrade conclusions.
6. WHEN no compatible input is supplied THEN the command SHALL fail with a
   sanitized diagnostic explaining the required input kinds.
7. WHEN a requested input is local-only or private THEN the export SHALL remain
   `hidden` unless explicit filtering omits that evidence from a new output set.

### Requirement 2: Vault Storage And Output Shape

**User Story:** As an operator, I want a deterministic directory of notes and a
machine-readable graph manifest that can be regenerated safely.

#### Acceptance Criteria

1. WHEN the exporter writes output THEN it SHALL create a directory containing
   Markdown notes and SHOULD create `graph.json` when JSON output is enabled.
2. WHEN generated Markdown notes are written THEN each generated note SHALL
   include parseable YAML frontmatter at the top of the file with generated-file
   metadata, the export schema version, and a deterministic content hash.
3. WHEN `graph.json` is written THEN it SHALL include schema version
   `evidence-graph-vault-export.v1`, export metadata, input summaries, nodes,
   edges, gaps, limitations, generation settings, and a deterministic graph
   content hash.
4. WHEN the export is rerun with the same inputs and options THEN generated
   Markdown and JSON SHALL be byte-stable.
5. WHEN an existing output directory contains generated notes or `graph.json`
   THEN valid generated files with self-consistent hashes MAY be replaced during
   normal re-export after all safety checks pass, but stale or hand-edited
   generated files SHALL fail unless `--force` is supplied and all safety
   checks for the newly generated content pass.
6. WHEN an existing output directory contains non-generated user notes THEN the
   exporter SHALL NOT overwrite them and SHALL report sanitized collisions.
7. WHEN Markdown links are generated THEN file names, anchors, tags, and
   frontmatter keys SHALL be deterministic, safe, and derived from stable IDs or
   public-safe labels, not raw local paths or raw remotes.
8. WHEN a file path must be represented THEN public/demo output SHALL use safe
   relative paths, labels, or hashes according to existing TraceMap redaction
   rules; local absolute paths SHALL be rejected.
9. WHEN generated Markdown is validated THEN the generated frontmatter SHALL be
   parseable as YAML frontmatter and SHALL remain compatible with plain
   Markdown readers and Obsidian-style vault tools.

### Requirement 3: Node Model

**User Story:** As a reviewer, I want graph nodes that correspond to TraceMap
evidence concepts, not arbitrary visual labels.

#### Acceptance Criteria

1. WHEN nodes are emitted THEN each node SHALL have a stable ID, node kind,
   claim level, source scope when applicable, display name, supporting IDs, rule
   IDs, evidence tiers, coverage labels, limitations, and safe relationships.
2. WHEN sources are emitted THEN source nodes SHALL include safe source labels,
   scan IDs, commit SHA presence or public-safe SHA, scanner/extractor version,
   language, analysis level, build status, and coverage warnings.
3. WHEN endpoint nodes are emitted THEN they SHALL use normalized method/path
   keys and SHALL NOT expose unsafe raw URLs, hostnames, query strings, tokens,
   or local routing values.
4. WHEN dependency surface nodes are emitted THEN the node kind SHALL be
   `surface`, and supported `surfaceKind` values SHALL include `sql-query`,
   `sql-persistence`, `http-route`, `http-client`, `package-config`,
   `dependency-surface`, `wcf-operation`, `remoting-endpoint`,
   `remoting-registration`, `remoting-channel`, `remoting-object`,
   `remoting-api`, `webforms-event`, `legacy-data`, and future additive kinds.
5. WHEN package nodes are emitted THEN package names and versions SHALL be
   treated as dependency metadata only and SHALL NOT imply vulnerability,
   license, or compatibility status unless a separate rule-backed input exists.
6. WHEN symbol nodes are emitted THEN they SHALL be included only when safe
   symbol display names and stable symbol identities are available; otherwise
   the exporter SHALL emit a gap or omit symbols according to the selected
   claim level.
7. WHEN rule, limitation, or gap nodes are emitted THEN they SHALL preserve rule
   IDs, evidence tiers, classifications, and limitation codes so users can see
   why evidence is strong, weak, reduced, or unavailable.
8. WHEN raw snippets, analyzer diagnostics, raw SQL, config values, or secrets
   would be needed to make a node meaningful THEN the exporter SHALL omit or
   category-label the unsafe field and emit a limitation instead of storing the
   raw value. Hashing is allowed only for input categories explicitly permitted
   by the redaction policy.

### Requirement 4: Edge Model

**User Story:** As a maintainer, I want edges that explain static evidence
relationships without pretending to prove runtime flow.

#### Acceptance Criteria

1. WHEN edges are emitted THEN each edge SHALL include stable ID, edge kind,
   source node ID, target node ID, rule ID, evidence tier, supporting fact IDs,
   supporting edge IDs, source scope, classification where applicable, and
   limitations.
2. WHEN call/create/value-flow evidence is available THEN edge kinds SHALL
   support at least `calls`, `creates`, `argument-passed`,
   `parameter-forward`, `value-origin`, and `fact-attached-to-symbol`.
3. WHEN dependency surface evidence is available THEN edge kinds SHALL support
   `surface-evidence`, `endpoint-match`, `symbol-reconciliation`,
   `wcf-service-reference`, `remoting-evidence`, `webforms-event-flow`,
   `legacy-root-selection`, `path-evidence`, `reverse-evidence`,
   `release-review-evidence`, and `portfolio-relationship`.
4. WHEN path, reverse, release-review, diff, impact, or portfolio reports are
   supplied THEN relationships from those reports SHALL be represented as
   report-derived evidence edges with their original rule IDs and limitations.
5. WHEN a relationship cannot be proven from static evidence THEN the exporter
   SHALL emit a gap or omit the edge; it SHALL NOT create speculative edges for
   dynamic dispatch, DI runtime binding, reflection targets, serializer
   contract mapping, branch feasibility, event firing, or collection contents.
6. WHEN an input edge is lower-tier or reduced-coverage evidence THEN the vault
   edge SHALL retain that lower classification and SHALL NOT promote it because
   it appears in a graph.
7. WHEN duplicate or ambiguous edge identity exists THEN the exporter SHALL
   emit a duplicate/ambiguous identity gap and SHALL NOT pick an arbitrary
   winner.

### Requirement 5: Obsidian-Compatible Markdown

**User Story:** As a demo operator, I want the export to work in local Markdown
tools such as Obsidian without making TraceMap depend on those tools.

#### Acceptance Criteria

1. WHEN Markdown is generated THEN note links SHALL use deterministic relative
   links that work in plain Markdown and Obsidian-style readers.
2. WHEN frontmatter is generated THEN it SHALL include only safe scalar or
   bounded-array metadata from closed vocabularies or stable IDs.
3. WHEN tags are generated THEN they SHALL be deterministic, lowercase, safe,
   and limited to closed vocabularies such as evidence tier, node kind, claim
   level, language, and coverage status.
4. WHEN backlinks or index pages are generated THEN they SHALL be derived from
   the exported graph manifest or sorted node/edge lists.
5. WHEN an Obsidian feature is optional or not supported by another Markdown
   reader THEN the output SHALL remain useful as plain Markdown.
6. WHEN generated note names could reveal unsafe identifiers THEN names SHALL
   use stable safe slugs or hashes and put only redacted/safe display text in
   the note body.
7. WHEN a note describes evidence THEN it SHALL include rule IDs, evidence
   tiers, source coverage, supporting IDs, and limitations near the claim.
8. WHEN a note describes a gap or limitation THEN it SHALL make the uncertainty
   visible rather than burying it in metadata only.

### Requirement 6: Claim Levels And Filtering

**User Story:** As a project owner, I want to control whether hidden, demo-safe,
or public-safe evidence is exported.

#### Acceptance Criteria

1. WHEN inputs contain hidden/private/local-only evidence THEN the default export
   SHALL be classified `hidden`.
2. WHEN raw combined indexes or report JSONs are supplied without an explicit
   claim-level catalog or compatible evidence-pack metadata THEN the exporter
   SHALL treat their evidence as `hidden`.
3. WHEN a caller supplies a claim-level catalog THEN the exporter SHALL promote
   evidence only by matching stable source identity, not display name alone.
4. WHEN a claim-level catalog entry cannot be matched to a stable source
   identity THEN the associated evidence SHALL remain `hidden` and the exporter
   SHALL emit a sanitized claim-level gap.
5. WHEN the caller requests `--minimum-claim-level demo-safe` THEN the exporter
   SHALL create a new output set containing only `demo-safe` and `public-safe`
   nodes/edges/gaps, recompute export classification, and preserve sanitized
   omission gap nodes when hidden relationship removal affects interpretation.
   Summary counts MAY supplement those gaps but SHALL NOT replace them.
   Demo-safe export SHALL fail when no non-gap visible graph nodes remain after
   filtering.
6. WHEN the caller requests `--minimum-claim-level public-safe` THEN the exporter
   SHALL include only public-safe evidence and SHALL fail if no visible graph
   nodes remain after filtering; summary counts alone SHALL NOT satisfy this
   threshold.
7. WHEN a relationship crosses from visible evidence to hidden evidence THEN the
   exporter SHALL omit the hidden target and emit a sanitized hidden-evidence
   gap if that omission affects graph interpretation.
8. WHEN evidence comes from local-only generated artifacts or operator-only
   samples THEN it SHALL remain hidden unless a separate public/demo-safe
   evidence pack or catalog entry proves it is safe.
9. WHEN claim-level filtering removes evidence THEN generated Markdown and JSON
   SHALL clearly state that the export is partial.
10. WHEN the top-level export classification is computed THEN it SHALL be no
   higher than the least-safe included node, edge, or gap.

### Requirement 7: Redaction And Safety Validation

**User Story:** As a reviewer, I want generated vaults to be safe to share when
classified as demo-safe or public-safe.

#### Acceptance Criteria

1. WHEN demo-safe or public-safe output is generated THEN validation SHALL reject
   local absolute paths, home fragments, raw remotes, raw SQL, source snippets,
   config values, connection strings, endpoint values, credentials, secrets,
   tokens, analyzer diagnostics, stack traces, raw private names, unsafe
   Markdown, and private source labels.
2. WHEN diagnostics report a rejection THEN they SHALL include sanitized
   category and location but SHALL NOT echo the unsafe value.
3. WHEN hashing is used THEN hash inputs SHALL be context-separated,
   deterministic, and documented; secret-like, credential-like, low-entropy,
   and enumerable private values SHALL be omitted or category-only rather than
   hashed for tracked or public/demo output. High-entropy stable IDs may be
   hashed only when the input category is documented as safe or has explicit
   public/demo claim proof.
4. WHEN generated Markdown is validated THEN every generated file SHALL be
   scanned, including index pages and frontmatter.
5. WHEN `graph.json` is validated THEN every string-valued JSON leaf SHALL be
   scanned, including IDs, labels, rule IDs, tags, and relationship metadata.
6. WHEN generated output is committed accidentally THEN
   `./scripts/check-private-paths.sh` SHALL still reject private path leaks in
   tracked files.
7. WHEN raw input artifacts contain unsafe values THEN the exporter SHALL either
   use existing redacted fields or reject the export; it SHALL NOT copy unsafe
   raw fields into notes.

### Requirement 8: Determinism And Generated Output Validation

**User Story:** As a maintainer, I want vault export diffs to be reviewable and
reproducible.

#### Acceptance Criteria

1. WHEN nodes, edges, links, tags, sections, and arrays are emitted THEN they
   SHALL be sorted by documented ordinal stable keys.
2. WHEN Markdown is generated from `graph.json` THEN a stale or hand-edited note
   or graph manifest SHALL fail validation unless regenerated.
3. WHEN `graph.json` is generated THEN canonical JSON SHALL use UTF-8 without
   BOM, LF line endings, two-space indentation, final newline, ordinal key
   sorting, and schema-defined array ordering.
4. WHEN generated note content hashes are computed THEN they SHALL exclude the
   mutable hash field itself and include canonical note content after
   normalization.
5. WHEN an export date is needed for public/demo output THEN it SHALL be an
   explicit `YYYY-MM` option or fixture value, not wall-clock time.
6. WHEN `--dry-run` is supplied THEN the exporter SHALL run all validations that
   do not require writing files and SHALL report planned files without creating
   or replacing output.
7. WHEN `--force` is supplied THEN it SHALL only allow overwriting generated
   files after validation; it SHALL NOT bypass claim-level, redaction, stale
   sentinel, or private-path gates.

### Requirement 9: Command Shape And Documentation

**User Story:** As an operator, I want a clear command and docs that explain how
to generate and validate a vault export.

#### Acceptance Criteria

1. WHEN implemented as a first-class CLI THEN the command SHOULD be shaped like
   `tracemap vault export --out <dir> [inputs/options]` so it does not conflict
   with the existing `tracemap export --index <path> --format <format>` command.
2. WHEN first-class CLI integration is too invasive for the first slice THEN a
   script fallback MAY be used, but the implementation state SHALL document the
   blocker and migration path.
3. WHEN command templates are documented THEN they SHALL use placeholders such
   as `<combined-index>`, `<portfolio-report>`, `<paths-report>`,
   `<release-review-report>`, `<evidence-pack>`, and `<vault-output>`.
4. WHEN documentation describes the export THEN it SHALL say it is a local
   navigation aid over static evidence, not a proof of runtime behavior.
5. WHEN documentation mentions Obsidian THEN it SHALL describe compatibility and
   local usage only; TraceMap SHALL NOT require Obsidian or implement site
   publishing in this spec.
6. WHEN site relationships are documented THEN they SHALL state that future site
   pages may consume public-safe summaries, but this spec does not create site
   files or public copy.
7. WHEN validation commands are documented THEN they SHALL include redaction,
   deterministic rerun, stale generated output, and private-path gates.
8. WHEN command options are implemented THEN the option set SHALL include
   `--minimum-claim-level`, `--date`, `--format`, `--dry-run`, and `--force`;
   there SHALL NOT be a separate `--claim-level` option because claim level is
   computed from inputs and filters.

### Requirement 10: Tests And Validation

**User Story:** As a maintainer, I want tests that make graph export safe to
ship later.

#### Acceptance Criteria

1. WHEN implementation finishes THEN focused tests SHALL cover deterministic
   JSON and Markdown output.
2. WHEN implementation finishes THEN tests SHALL prove hidden evidence is
   filtered or blocks public/demo export according to claim-level options.
3. WHEN implementation finishes THEN tests SHALL reject unsafe planted values in
   Markdown, frontmatter, tags, JSON leaves, IDs, links, and diagnostics without
   echoing the unsafe value.
4. WHEN implementation finishes THEN tests SHALL prove stale generated
   sentinels fail validation and regenerated files become valid.
5. WHEN implementation finishes THEN tests SHALL prove existing non-generated
   user notes are not overwritten.
6. WHEN implementation finishes THEN tests SHALL prove lower-tier and
   reduced-coverage evidence is not promoted in graph edges.
7. WHEN implementation finishes THEN tests SHALL prove duplicate identity and
   incompatible schema inputs emit gaps instead of arbitrary merges.
8. WHEN implementation finishes THEN `./scripts/check-private-paths.sh` and
   `git diff --check` SHALL pass.
9. WHEN implementation touches .NET CLI code THEN `dotnet build
   src/dotnet/TraceMap.sln` and `dotnet test src/dotnet/TraceMap.sln` SHALL
   pass.
10. WHEN implementation is script-only THEN focused script tests SHALL pass and
   any deferred .NET tests SHALL be documented with a reason.
11. WHEN implementation finishes THEN tests SHALL prove generated Markdown
    frontmatter is parseable and remains the first content in the file.
12. WHEN implementation finishes THEN tests SHALL prove `graph.json` content
    hash validation catches stale or hand-edited manifests.
13. WHEN implementation finishes THEN tests SHALL prove stable output bytes are
    independent of the output directory and local checkout root.
14. WHEN implementation finishes THEN tests SHALL prove an explicit `--date`
    option is the only date source in public/demo output.
15. WHEN implementation finishes THEN tests SHALL prove `--force` cannot bypass
    claim-level, redaction, stale generated file, or private-path gates.

## Deferred Follow-Ups

- Hosted HTML graph viewer.
- Site pages that consume public-safe exported summaries.
- Graph layout coordinates or screenshot generation.
- Optional import into specific Obsidian plugins.
- Runtime telemetry overlays as externally labeled evidence.
- Ownership, service catalog, vulnerability, or package compliance overlays.
- Bidirectional sync from user-edited notes back into TraceMap.
