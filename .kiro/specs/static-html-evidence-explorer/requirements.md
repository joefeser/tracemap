# Static HTML Evidence Explorer Requirements

## Introduction

TraceMap already emits machine-readable artifacts and a Markdown report for
deterministic static evidence review. Those outputs are durable and scriptable,
but complex evidence/path exploration can still be hard during demos and human
review because readers must jump across JSON, NDJSON, SQLite, and Markdown.

This spec defines a local static HTML evidence explorer generated from existing
TraceMap output artifacts. The explorer is a generated file set, opened from
disk, with no hosted service, backend, hidden telemetry, LLM, embedding, vector
database, prompt classifier, or runtime code analysis.

Public claim level: concept until implemented and validated with public-safe
fixtures.

## Scope

In scope:

- Generate a local static HTML artifact from existing TraceMap outputs such as
  manifests, JSON reports, combined indexes, facts, reducer output, and related
  generated report artifacts.
- Show sources, coverage, static surfaces, paths, gaps, rule catalog entries,
  limitations, and evidence rows using deterministic rendering.
- Preserve TraceMap evidence boundaries: every displayed conclusion has a rule
  ID, evidence tier, supporting artifact identity, coverage label where
  available, and visible limitations.
- Preserve redaction, no-raw-snippet defaults, and no telemetry.
- Keep raw snippets behind the existing explicit opt-in behavior only; do not
  make HTML output a new bypass for snippets, raw config, SQL, secrets, or
  private values.
- Keep the local generated explorer clearly separate from the public
  `tracemap.tools` static site.
- Design for safe public/demo claim levels and partial-analysis labeling.

Out of scope:

- Hosted explorer service, cloud upload, remote storage, or published demo
  hosting in v1.
- Live backend, live repository connection, live database, service worker
  sync, analytics endpoint, telemetry beacon, or external script dependency.
- Scanner, reducer, adapter, or runtime code-analysis changes except additive
  structured metadata needed to render existing generated artifacts safely.
- New evidence conclusions beyond existing TraceMap artifacts.
- LLM calls, embeddings, vector databases, semantic search services,
  prompt-based classification, or model-generated summaries in TraceMap core.
- Treating RAG answers, vector similarity, user prompts, runtime telemetry, or
  generated prose as evidence for TraceMap conclusions.
- Public `tracemap.tools` site content, navigation, marketing copy, or hosted
  site deployment.
- Publishing raw source snippets, raw SQL, config values, connection strings,
  secrets, local absolute paths, raw remotes, raw endpoint addresses, raw query
  strings, hostnames, private repo names, private sample names, analyzer logs,
  or raw telemetry payloads.

## Requirements

### Requirement 1: Local Static Artifact Boundary

**User Story:** As a reviewer, I want an HTML explorer that I can open locally
from generated TraceMap outputs without running a server or exposing repository
data.

#### Acceptance Criteria

1. WHEN the explorer is generated THEN it SHALL write a local static file set
   that can be opened from disk or served by a generic static file server
   without a TraceMap backend.
2. WHEN the explorer renders data THEN it SHALL read only generated TraceMap
   artifacts selected by the user or produced by the same command invocation.
3. WHEN the explorer includes JavaScript THEN that JavaScript SHALL be bundled
   locally, deterministic, and free of network calls, telemetry beacons,
   analytics SDKs, remote fonts, remote images, remote scripts, and remote CSS.
4. WHEN the explorer is opened THEN it SHALL NOT rescan source code, invoke
   Roslyn or other language adapters, read live repository files, query
   databases, call external services, or derive new impact conclusions.
5. WHEN the explorer output is described in CLI help, docs, manifests, or the
   report THEN it SHALL be labeled as a local generated artifact, not as the
   public `tracemap.tools` website or a hosted service.
6. WHEN generation cannot include required local assets safely THEN TraceMap
   SHALL fail with an evidence-backed generation gap or omit the unsafe feature
   with a visible limitation; it SHALL NOT fall back to remote assets.

### Requirement 2: Supported Inputs And Provenance

**User Story:** As a maintainer, I want the explorer to show exactly which
TraceMap artifacts it was generated from so reviewers can audit provenance.

#### Acceptance Criteria

1. WHEN the explorer is generated THEN it SHALL record the input artifact list,
   artifact types, input artifact content hashes, schema versions, generator
   version, TraceMap version, repo identity policy, commit SHA, generation
   timestamp policy, and claim level in a machine-readable explorer manifest.
   The manifest SHALL NOT attempt to hash its own output file in a
   self-referential way. Allowed values for repo identity policy and generation
   timestamp policy SHALL be defined in the design or versioned explorer
   manifest schema.
2. WHEN a scan manifest or combined report contains repository and commit SHA
   metadata THEN the explorer SHALL surface safe commit SHA and coverage
   metadata without exposing raw local paths or raw remotes.
3. WHEN an input lacks required repo or commit SHA metadata THEN the explorer
   SHALL label coverage as partial or reduced and emit an `AnalysisGap` or
   explorer-generation gap under a documented rule ID.
4. WHEN multiple artifacts disagree on commit SHA, source identity, schema
   version, or claim level THEN the explorer SHALL stop or mark the affected
   sections as partial with a rule-backed limitation rather than merging them
   silently.
5. WHEN input artifacts have unsupported schema versions THEN the explorer
   SHALL emit a visible unsupported-schema gap and keep supported sections
   usable when safe.
6. WHEN an input artifact is optional and missing THEN the explorer SHALL
   distinguish "not provided", "unsupported", and "no evidence found under
   credible coverage".
7. WHEN an explorer page references an input artifact THEN it SHALL use stable
   artifact IDs or safe generated labels, not absolute paths or raw private
   names.

### Requirement 3: Evidence Overview And Coverage Navigation

**User Story:** As a reviewer, I want the first screen to summarize coverage,
claim level, sources, and the safest places to begin.

#### Acceptance Criteria

1. WHEN `index.html` is opened THEN it SHALL show claim level, coverage labels,
   source count, artifact count, surface count, path count, gap count,
   limitation count, rule count, evidence-row count, and omitted/redacted
   counts where available.
2. WHEN coverage is partial, reduced, unsupported, mixed, stale, or unknown
   THEN that status SHALL be visible near the top and SHALL NOT be described as
   complete analysis.
3. WHEN sections exist for sources, surfaces, paths, gaps, rules, limitations,
   reports, or evidence rows THEN the overview SHALL link to those sections
   using deterministic anchors.
4. WHEN a category has no compatible input THEN the overview SHALL say the
   category is unavailable rather than implying that no evidence exists.
5. WHEN a category has compatible input and credible coverage but no evidence
   rows THEN the overview MAY say no static evidence was found for that
   category, with the applicable rule IDs and coverage labels.
6. WHEN a section summarizes a conclusion THEN it SHALL include rule IDs,
   evidence tiers, support IDs, and limitations or link directly to rows that
   contain them.
7. WHEN the explorer is generated from reducer output THEN the overview SHALL
   distinguish reducer-backed impact classifications from scanner facts,
   paths, gaps, and lower-tier review queues.

### Requirement 4: Source And Artifact Views

**User Story:** As a reviewer, I want to see which sources and generated
artifacts contributed evidence without exposing private environment details.

#### Acceptance Criteria

1. WHEN source rows are rendered THEN each row SHALL show safe source label,
   source kind, claim level, coverage status, commit SHA when safe, extractor
   version when available, input artifact IDs, gap counts, limitation counts,
   and omitted/redacted counts.
2. WHEN file paths are rendered THEN they SHALL be safe repository-relative
   paths that pass the selected safety profile; absolute paths SHALL NOT be
   rendered.
   Safe source labels SHALL be derived from closed vocabularies,
   user-supplied labels that pass safety validation, or stable IDs; they SHALL
   NOT be derived from absolute paths, raw remotes, hostnames, private
   repository names, or generated scan directories.
3. WHEN raw remotes, hostnames, URLs, private repository names, machine names,
   generated scan directories, or local absolute paths are present in inputs
   THEN public/demo output SHALL fail or omit/redact them with rule-backed
   limitations according to existing safety policy.
4. WHEN a source has reduced semantic coverage, failed build, unsupported
   language coverage, missing artifacts, or syntax-only fallback THEN the
   source view SHALL show the reduced coverage label and related gaps.
5. WHEN the explorer links evidence rows back to source spans THEN it SHALL
   show file path, line span, rule ID, tier, support ID, and snippet hash where
   available, not raw source snippets by default.
6. WHEN raw snippets are explicitly enabled by a future option THEN the
   explorer SHALL label the output as hidden/local, record the option in the
   manifest, and keep public/demo output strict.

### Requirement 5: Surfaces, Paths, And Reducer Results

**User Story:** As an engineer, I want to navigate static surfaces and paths
without confusing evidence links with proven runtime impact.

#### Acceptance Criteria

1. WHEN static surfaces are rendered THEN they SHALL be grouped by a closed
   `surfaceKind` or equivalent schema field and SHALL preserve stable IDs,
   rule IDs, tiers, support IDs, classifications, coverage labels, and
   limitations.
2. WHEN dependency or route paths are rendered THEN each path SHALL show
   deterministic hop order, edge kind, rule ID, evidence tier, support IDs,
   coverage label, and visible limitations for each hop.
3. WHEN reducer output is present THEN impact labels SHALL be shown only for
   reducer-backed rows and SHALL include reducer rule ID, evidence tier,
   supporting facts or paths, classification, confidence/needs-review state
   where available, and limitations.
4. WHEN only scanner facts or path evidence exists THEN the explorer SHALL use
   static evidence, candidate, path evidence, nearby evidence, gap, or needs
   review wording; it SHALL NOT say impacted, broken, affected, reachable, used
   in production, safe to deploy, or runtime behavior.
5. WHEN a path uses lower-tier, weak, review-only, ambiguous, partial, or
   syntax-only evidence THEN the path view SHALL make that evidence strength
   visible in row styling, filters, and accessible text.
6. WHEN graph-like navigation is offered THEN it SHALL be a deterministic view
   over existing nodes and edges; it SHALL NOT invent ranking, centrality,
   ownership, runtime criticality, or semantic similarity.
7. WHEN fan-out, duplicate identities, missing identity, unsupported schema, or
   unsafe display values affect interpretation THEN the affected surfaces or
   paths SHALL carry visible gaps or limitations.

### Requirement 6: Gaps, Limitations, Rules, And Evidence Rows

**User Story:** As a reviewer, I want to inspect the rule-backed basis for
claims, gaps, and limitations from one place.

#### Acceptance Criteria

1. WHEN evidence rows are rendered THEN each row SHALL include rule ID,
   evidence tier, evidence kind, support ID, source/artifact ID, file span when
   available, snippet hash when available, coverage label when available,
   extractor version when available, and limitations when available.
2. WHEN gaps are rendered THEN each gap SHALL include gap kind, rule ID,
   evidence tier or `Tier4Unknown`, source/artifact scope, coverage label,
   affected section, and limitation text.
3. WHEN limitations are rendered THEN each limitation SHALL include rule ID,
   limitation kind, affected section, source/artifact scope, and whether the
   limitation affects claim level, completeness, identity, display, or safety.
4. WHEN rule catalog data is available THEN the explorer SHALL render rule ID,
   title, description, evidence tier expectations, limitations, and related
   sections using deterministic order.
5. WHEN rule catalog data is unavailable THEN the explorer SHALL still render
   rule IDs from evidence rows and emit a visible catalog-unavailable gap.
6. WHEN filters or search are provided THEN they SHALL operate only over
   already-rendered safe fields and closed vocabularies; they SHALL NOT query
   source files, send data over the network, or create hidden evidence.
7. WHEN rows are sorted THEN default order SHALL be deterministic and
   documented, with stable tie-breakers based on safe category, rule ID,
   source label, file span, and stable ID.

### Requirement 7: Safety Profiles And Redaction

**User Story:** As a maintainer, I want the explorer to preserve TraceMap's
public/demo safety guarantees while still being useful for hidden/local review.

#### Acceptance Criteria

1. WHEN public/demo output is generated THEN the explorer SHALL reject or omit
   unsafe values using the existing strict safety policy as the source of
   truth and SHALL NOT render raw source snippets, raw SQL, config values,
   connection strings, secrets, raw remotes, raw endpoint addresses, raw query
   strings, hostnames, exact private routes, local absolute paths, private repo
   names, analyzer logs, raw facts, or raw SQLite content. Normalized or
   redacted route templates MAY be rendered only when they pass the existing
   report safety policy for the selected claim level.
2. WHEN hidden/local output is generated THEN the explorer MAY render
   redacted, hashed, category-only, or omitted placeholders, but SHALL label
   the output hidden/local and record redaction counts and affected sections.
3. WHEN redaction changes interpretation THEN the affected section SHALL show a
   rule-backed limitation and reduced or partial coverage where appropriate.
4. WHEN a display title, alias, filter label, data attribute, DOM ID,
   JavaScript state object, embedded JSON blob, CSS custom property, metadata
   tag, title attribute, alt text, or URL fragment would contain unsafe data
   THEN the explorer SHALL use a safe stable ID or redacted placeholder.
5. WHEN assets are embedded or copied into the output directory THEN generated
   files SHALL not contain private local paths, raw remotes, source snippets,
   raw config/SQL values, secrets, raw endpoint addresses, raw query strings,
   hostnames, or private identifiers in comments, source maps, metadata, or
   bundled constants.
6. WHEN the explorer includes downloadable JSON or data blobs THEN they SHALL
   follow the same selected safety profile and SHALL NOT expose any value that
   is less redacted or otherwise more sensitive than the corresponding visible
   UI value.
7. WHEN safety validation finds unsafe material after generation THEN the
   command SHALL fail and identify the safety rule and affected generated
   artifact without printing the unsafe raw value.

### Requirement 8: Determinism, Accessibility, And Usability

**User Story:** As a reviewer, I want repeatable output that is accessible and
easy to navigate during review.

#### Acceptance Criteria

1. WHEN explorer generation runs twice with identical inputs and options THEN
   generated HTML, CSS, JavaScript, manifest, embedded data, IDs, anchors,
   sort order, and asset file names SHALL be byte-stable except for explicitly
   documented timestamp fields.
2. WHEN generation writes timestamps THEN it SHALL either use deterministic
   source timestamps from input manifests or clearly mark nondeterministic
   fields outside byte-stable comparison scope.
3. WHEN pages contain tables, filters, tabs, collapsible groups, graph views,
   or keyboard navigation THEN they SHALL be accessible with semantic HTML,
   visible focus states, labels, headings, and screen-reader text that does
   not overclaim evidence strength.
4. WHEN JavaScript is disabled THEN the explorer SHALL still show the overview,
   section links, gap tables, limitation tables, rule IDs, rule catalog rows
   when available, and a documented deterministic baseline of evidence rows.
   Large datasets MAY cap no-JavaScript evidence rows at a documented threshold
   only when counts, omitted-row labels, and JavaScript-enhanced access to the
   remaining safe rows are provided.
5. WHEN JavaScript is enabled THEN filters, sorting, section toggles, copy
   buttons, or graph interactions SHALL operate entirely locally over safe
   embedded data.
6. WHEN datasets are large THEN the implementation MAY add deterministic
   pagination, virtualized tables, or precomputed indexes, but SHALL preserve
   stable ordering, no-network behavior, and no hidden evidence.
7. WHEN copy/download affordances exist THEN copied text and downloaded files
   SHALL preserve redaction and safety profile constraints and SHALL NOT expose
   any value that is less redacted or otherwise more sensitive than the
   corresponding visible UI value.

### Requirement 9: Output Contracts, Validation, And Docs

**User Story:** As a future implementer, I want clear output contracts and
tests so the explorer can evolve without silently breaking consumers.

#### Acceptance Criteria

1. WHEN implementation adds explorer output THEN it SHALL document the CLI
   command or option, output directory layout, generated-file sentinels,
   manifest schema, supported input schemas, safety profiles, deterministic
   ordering, and compatibility expectations.
2. WHEN implementation adds or changes explorer-specific rule IDs THEN
   `rules/rule-catalog.yml` or equivalent rule documentation SHALL include the
   rule, evidence tier expectations, and documented limitations before
   explorer output ships. Initial implementation PRs MAY add catalog stubs, but
   generated explorer output SHALL NOT be considered implementation-complete
   until the rule catalog entries are present.
3. WHEN implementation adds explorer output THEN tests SHALL cover deterministic
   reruns, no-network assets, strict public/demo safety, hidden/local redaction
   labeling, unsupported input schemas, partial coverage labels, no-snippet
   defaults, reducer-backed impact wording, scanner-only non-impact wording,
   accessibility-relevant markup, manifest provenance, the three absence states
   of not-provided versus unsupported versus no-evidence-under-credible-coverage,
   existing safety-policy parity, downloadable-data safety parity,
   safety-failure message hygiene, embedded JSON safety, and line-ending
   normalization.
4. WHEN the explorer is validated for demos THEN validation SHALL include a
   public-safe fixture or generated artifact and SHALL fail on unsafe public
   claims or private material.
5. WHEN browser sanity checks are run THEN they SHALL use local generated files
   or a local static server and SHALL not require external network access.
6. WHEN docs mention the explorer THEN they SHALL explicitly separate the local
   generated artifact from the public `tracemap.tools` site and from hosted
   services.
   Generated explorer artifacts SHALL NOT be emitted into public site source or
   generated site output directories.
7. WHEN implementation remains partial THEN docs, manifests, and the UI SHALL
   label unsupported sections as partial, unavailable, or gaps rather than
   implying complete coverage.
