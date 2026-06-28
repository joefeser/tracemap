# UI Field Property Lineage Terminal Context Vault Local Navigation Requirements

## Introduction

This spec is the PR 2 runway from
`.kiro/specs/ui-field-property-lineage-terminal-context-consumers/`: hidden and
local vault navigation for property-flow terminal-context evidence. The input
is existing property-flow path evidence where a path node carries
`safeMetadata["terminalContextKind"]` after the selected-property terminal
context gate. The output is optional vault navigation over that static evidence.

This is not a docs-export implementation spec. Docs-export consumer work,
retrieval chunks, JSONL schemas, RAG/vector ingestion, and Markdown evidence
docs remain out of scope here.

Public claim level: hidden.

## Source Material

- `.kiro/specs/ui-field-property-lineage-terminal-context-consumers/`
- `.kiro/specs/ui-field-property-lineage-terminal-context/`
- `.kiro/specs/evidence-graph-vault-export/`
- `.kiro/specs/vault-export-hidden-safety/`
- `src/dotnet/TraceMap.Reporting/PropertyFlowReport.cs`
- `src/dotnet/TraceMap.Reporting/VaultExport.cs`
- `docs/VAULT_EXPORT.md`
- `rules/rule-catalog.yml`

## Existing Baseline

The `dev` baseline already has:

- Property-flow report version `1.0`.
- Additive path notes such as `StaticTerminalContext`.
- Additive node safe metadata key `terminalContextKind`.
- A closed current terminal-context display vocabulary:
  `data-surface terminal context`, `legacy-data terminal context`,
  `package/config terminal context`, `message-surface terminal context`,
  `legacy-communication terminal context`, and
  `dependency-surface terminal context`.
- Vault export claim levels `hidden`, `demo-safe`, and `public-safe`.
- Vault generated-file sentinels, deterministic content hashes, `graph.json`,
  claim-level filtering, hidden-evidence omission gaps, and safety validation.

## Requirement 1: Consume Only Existing Path-Scoped Evidence

**User Story:** As a TraceMap maintainer, I want vault navigation to expose
terminal context only when current property-flow evidence already carries it.

### Acceptance Criteria

1. WHEN vault export renders terminal-context navigation THEN it SHALL read the
   terminal kind from structured property-flow evidence, preferring
   `lineagePaths[].nodes[].safeMetadata["terminalContextKind"]`.
2. WHEN only `StaticTerminalContext` prose is present THEN vault export MAY
   render the note as bounded display text but SHALL NOT infer a structured
   terminal-context navigation node from the prose alone.
3. WHEN `terminalContextKind` is absent THEN vault export SHALL treat terminal
   context as unknown or unavailable, not as proof that no terminal surface
   exists.
4. WHEN terminal context is rendered THEN it SHALL remain attached to the
   specific property-flow path and terminal node that carried the metadata.
5. WHEN structured metadata and prose disagree THEN vault export SHALL prefer
   structured metadata, emit or reuse a schema/consistency gap, and avoid
   stronger claims.

## Requirement 2: Keep Output Hidden And Local

**User Story:** As a local vault user, I want useful navigation without
promoting terminal context into public or demo claims.

### Acceptance Criteria

1. WHEN the selected output classification is `hidden` THEN vault export MAY
   render terminal-context nodes, edges, tags, index rows, note sections, or
   backlinks that are derived from safe property-flow evidence.
2. WHEN `--minimum-claim-level demo-safe` or `--minimum-claim-level public-safe`
   is selected THEN terminal-context navigation SHALL be omitted unless a later
   separate reviewed public/demo policy explicitly permits static concept
   rendering.
3. WHEN demo/public filtering omits terminal-context evidence THEN vault export
   SHALL emit or reuse a sanitized rule-backed omission gap instead of silently
   dropping meaningful path evidence.
4. WHEN terminal context is hidden-only THEN generated Markdown and `graph.json`
   SHALL not include wording that invites publication, product claims, or
   public-safe promotion.
5. WHEN a source claim catalog promotes other evidence to demo/public THEN that
   promotion SHALL NOT by itself promote terminal-context navigation from this
   spec.

## Requirement 3: Preserve Evidence Identity And Safety

**User Story:** As a reviewer, I want every terminal-context navigation item to
remain traceable and safe.

### Acceptance Criteria

1. WHEN vault export creates a terminal-context navigation item THEN it SHALL
   preserve, where available and safe, the property-flow path ID, terminal node
   ID, rule ID, evidence tier, file path, line span, source index ID, scan ID,
   commit SHA, extractor ID/version, supporting fact IDs, supporting edge IDs,
   coverage labels, limitations, and source claim level.
2. WHEN any evidence identity field is missing, unsafe, or unsupported by the
   input schema THEN vault export SHALL omit, hash, category-label, or gap the
   field under existing vault safety/schema rules.
3. WHEN stable IDs are built for terminal-context nodes or edges THEN the IDs
   SHALL use context-separated deterministic inputs and only safe or approved
   hashed components.
4. WHEN a required stable-ID component is rejected before ID construction THEN
   the node or edge SHALL be omitted and a rule-backed safety gap SHALL be
   emitted.
5. WHEN source snippets, raw SQL, raw config values, raw URLs, raw remotes,
   credentials, production data, local absolute paths, or private sample
   identifiers would enter output THEN vault export SHALL reject or omit them
   before writing generated files.

## Requirement 4: Use Rule-Backed Vault Semantics

**User Story:** As an implementer, I want any new navigation shape to be backed
by documented rules and limitations.

### Acceptance Criteria

1. WHEN existing vault rules cover the behavior THEN implementation SHALL reuse
   those rules and document the mapping in tests or docs.
2. WHEN implementation emits a new terminal-context graph node kind, edge kind,
   tag, omission gap, schema gap, limitation, or redaction category THEN
   `rules/rule-catalog.yml` SHALL document the rule ID, emitted artifact,
   evidence tier, limitations, and non-claims before product code emits it.
3. WHEN a new graph rule is needed THEN prefer the naming
   `vault-export.graph.property-flow-terminal-context.v1`.
4. WHEN a new omission gap is needed THEN prefer the naming
   `vault-export.gap.terminal-context-omitted.v1`.
5. WHEN hidden/local safety transforms are needed THEN reuse existing
   `vault-export.validation.unsafe-value-rejected.v1`,
   `vault-export.gap.unsafe-symbol-omitted.v1`,
   `vault-export.gap.hidden-safe-context-omitted.v1`,
   `vault-export.gap.unsafe-id-component-omitted.v1` where their limitations
   fit. If evidence-location category-only behavior is required, add a
   catalogued rule before emitting it instead of treating it as existing.

## Requirement 5: Render Deterministic Local Navigation

**User Story:** As a vault user, I want byte-stable Markdown and graph output
that makes terminal context easy to inspect locally.

### Acceptance Criteria

1. WHEN hidden terminal-context navigation is enabled THEN graph nodes and edges
   SHALL sort ordinally and produce byte-stable `graph.json` for equivalent
   inputs.
2. WHEN terminal-context Markdown is rendered THEN generated notes SHALL have
   deterministic frontmatter, deterministic body ordering, and self-consistent
   content hashes.
3. WHEN index pages or folder pages include terminal context THEN counts SHALL
   be path-scoped and SHALL NOT merge multiple paths into one stronger claim.
4. WHEN tags are generated THEN they SHALL come from a closed safe vocabulary
   such as `tracemap/property-flow/terminal-context` and
   `tracemap/claim/hidden`.
5. WHEN generated navigation links are created THEN they SHALL link back to
   property-flow paths, source/rule/gap/limitation notes, or safe graph nodes
   rather than standing alone as proof.

## Requirement 6: Keep Non-Claims Explicit

**User Story:** As a TraceMap reviewer, I want vault output to stay aligned
with TraceMap's deterministic evidence model.

### Acceptance Criteria

1. Vault output SHALL NOT claim runtime behavior, browser visibility, user
   interaction, authorization behavior, feature-flag state, dependency
   injection runtime target selection, serializer runtime behavior, branch
   feasibility, database execution, persistence outcome, traffic, release
   safety, business impact, or complete coverage.
2. Vault output SHALL NOT say a property, endpoint, dependency, database, or
   source is "impacted" unless a separate reducer produced that exact
   rule-backed impact claim.
3. Vault implementation SHALL NOT add LLM calls, embeddings, vector databases,
   or prompt-based classification.
4. Vault implementation SHALL NOT store raw source snippets by default.
5. Vault implementation SHALL NOT infer terminal context from same route, same
   endpoint, same class, same file, same property name, broad dependency
   reachability, or path note prose alone.

## Requirement 7: Validation Coverage

**User Story:** As an implementer, I want tests proving the vault slice is
deterministic, hidden-only, and safe.

### Acceptance Criteria

1. Tests SHALL prove hidden/local terminal-context navigation can render from
   structured `terminalContextKind` metadata without overclaiming.
2. Tests SHALL prove demo/public exports omit or gap terminal-context
   navigation and do not promote it through unrelated source claim catalog
   entries.
3. Tests SHALL prove absent `terminalContextKind` does not produce negative
   no-surface language.
4. Tests SHALL prove structured/prose mismatch prefers structured metadata and
   emits or reuses a schema/consistency gap.
5. Tests SHALL prove unrecognized safe `terminalContextKind` values are
   category-labeled or gap-backed and never upgraded to a stronger known kind.
6. Tests SHALL prove unsafe metadata, unsafe display names, local absolute
   paths, raw URLs, raw SQL, raw config values, source snippets, credentials,
   raw remotes, production data, and private identifiers do not reach generated
   output.
7. Tests SHALL prove deterministic Markdown, `graph.json`, generated-file
   collision behavior, content-hash behavior, and claim-level filtering.
8. Tests SHALL prove terminal-context nodes or edges are emitted only after any
   required vault graph/omission rule is present in `rules/rule-catalog.yml`.
9. Validation SHALL include `git diff --check`,
   `./scripts/check-private-paths.sh`, focused vault tests, and
   `dotnet test src/dotnet/TraceMap.sln` unless the implementation records a
   narrow reason for a smaller test set.
