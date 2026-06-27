# UI Field Property Lineage Terminal Context Consumers Requirements

## Introduction

PR #400 merged the property-flow terminal context gate into `dev` as commit
`5e88a10486a1bf0c088ee681f140c643a2635415`. The live `property-flow` report
can now add path notes such as `StaticTerminalContext` and path-scoped node safe
metadata key `terminalContextKind` after an existing selected-property bridge
reaches a terminal surface through combined path evidence.

This follow-up spec defines how deterministic consumers may safely read that
metadata in docs export, vault export, report rendering, rule-catalog
documentation, and local evidence navigation. It does not add new scanner
facts, reducers, runtime analysis, DB execution proof, impact proof, AI/LLM
analysis, public site copy, or public product claims.

SQLite, facts, reports, and the rule catalog remain the source of truth.
Consumer output may summarize or navigate terminal context only when it keeps
the original static evidence IDs, rule IDs, evidence tiers, commit SHAs,
extractor versions, coverage labels, limitations, and path-local scope.

Public claim level: hidden. Any demo/concept rendering must be explicitly
labeled as static concept evidence and must not be promoted to public-safe copy
without a separate reviewed public-claim spec.

## Source Material

- PR #400: `[codex] Add property-flow terminal context gate`.
- `.kiro/specs/ui-field-property-lineage-terminal-context/`
- `.kiro/specs/ui-field-property-lineage-composition/`
- `.kiro/specs/ui-field-property-lineage-continuation/`
- `.kiro/specs/evidence-graph-vault-export/`
- `.kiro/specs/vault-export-hidden-safety/`
- `.kiro/specs/evidence-export-usability-polish/`
- `src/dotnet/TraceMap.Reporting/PropertyFlowReport.cs`
- `src/dotnet/TraceMap.Reporting/EvidenceDocsExport.cs`
- `src/dotnet/TraceMap.Reporting/VaultExport.cs`
- `rules/rule-catalog.yml`
- `docs/EVIDENCE_DOCS_EXPORT.md`
- `docs/VAULT_EXPORT.md`
- `docs/VALIDATION.md`

## Existing Baseline

This spec assumes the `dev` baseline after PR #400 includes:

- `tracemap property-flow` report version `1.0`.
- Additive `PropertyFlowPath.Notes`.
- Additive `PropertyFlowNode.SafeMetadata["terminalContextKind"]` only when
  the existing selected-property bridge gate allows terminal context.
- Terminal context note wording that says the evidence is static context, not
  runtime execution, dependency execution, database execution, or impact proof.
- No new top-level property-flow terminal-context collection.
- No new property-flow terminal-context rule ID or gap code from PR #400.

## Requirement 1: Consume Only Static Path-Scoped Evidence

**User Story:** As a TraceMap maintainer, I want docs, vault, and reporting
consumers to expose terminal context only as path-scoped static evidence.

### Acceptance Criteria

1. WHEN a consumer reads `terminalContextKind` THEN it SHALL read it only from
   documented property-flow JSON, facts, SQLite-backed evidence, or report rows
   produced from the current scan and commit SHA.
2. WHEN a consumer renders terminal context THEN it SHALL keep the context
   attached to the specific property-flow path or node that carried the
   metadata, not promote it to a source-wide, endpoint-wide, field-wide, or
   impact-wide conclusion.
3. WHEN path notes include `StaticTerminalContext` THEN consumers MAY render the
   note after sanitization but SHALL NOT parse the note text as the primary
   evidence source when structured `terminalContextKind`, rule IDs, tiers, and
   supporting IDs are available.
4. WHEN structured metadata and note text disagree THEN consumers SHALL prefer
   structured metadata, emit a schema or consistency gap, and avoid stronger
   conclusions.
5. WHEN terminal context is absent THEN consumers SHALL treat absence as
   unknown or unavailable context, not proof that no terminal surface exists.

## Requirement 2: Preserve Evidence Identity And Limitations

**User Story:** As a reviewer, I want every consumer-rendered terminal context
item to remain traceable to rule-backed evidence.

### Acceptance Criteria

1. WHEN docs export, vault export, or report rendering creates a terminal
   context chunk, node, edge, section, note, or tag THEN it SHALL preserve the
   originating rule ID, evidence tier, source label, source index ID where
   available, scan ID where available, commit SHA, extractor ID/version, file
   path, line span, supporting fact IDs, supporting edge IDs, coverage labels,
   and limitations where the input provides them.
2. WHEN any required evidence identity field is missing or unsafe to render
   THEN the consumer SHALL omit, hash, category-label, or gap the unsafe or
   missing field under an existing redaction/schema rule.
3. WHEN a consumer creates a derived ID for navigation THEN the ID SHALL be
   stable, context-separated, sorted deterministically, and based only on safe
   evidence identity.
4. WHEN a consumer collapses multiple paths into an index or graph summary THEN
   it SHALL show counts and partial labels instead of merging terminal context
   into a single stronger claim.
5. WHEN coverage is reduced, schema is unsupported, extractor identity is
   missing, commit SHA is unknown, or traversal caps apply THEN the consumer
   SHALL label the terminal context view as partial.

## Requirement 3: Docs Export Is Retrieval Metadata, Not Analysis

**User Story:** As a docs-export user, I want retrieval chunks to include safe
terminal-context cues without turning them into new TraceMap findings.

### Acceptance Criteria

1. WHEN docs export consumes property-flow terminal context THEN it SHALL
   package it as static evidence metadata or a property-flow chunk family,
   not as an impact result, runtime behavior, vulnerability, ownership, or
   release-safety claim.
2. BEFORE a docs-export implementation edits product code THEN it SHALL record
   in this spec's `implementation-state.md` whether the slice will safely
   ignore terminal context or render it as retrieval metadata; the recorded
   decision SHALL make the matching tests mandatory for that PR.
3. WHEN docs-export JSONL or Markdown schema changes THEN the change SHALL be
   additive or versioned, with deterministic ordering and manifest hashing.
4. WHEN docs export renders `terminalContextKind` THEN values SHALL come from a
   closed safe vocabulary or be hashed/category-labeled under a redaction rule.
5. WHEN docs export forwards path notes THEN note text SHALL be sanitized and
   bounded; unsafe note text SHALL be omitted or hashed without failing public
   strictness unless the existing safety policy requires hard failure.
6. WHEN downstream RAG/vector systems consume docs export THEN documentation
   SHALL state that those systems are consumers of TraceMap evidence and do not
   become evidence for TraceMap conclusions.

## Requirement 4: Vault Export Keeps Hidden/Local Safety Boundaries

**User Story:** As a local vault user, I want terminal context navigation that
is useful but still deterministic, safe, and claim-level bounded.

### Acceptance Criteria

1. WHEN vault export renders terminal context THEN it MAY create hidden/local
   graph nodes, edges, tags, backlinks, index sections, or note summaries only
   from existing property-flow evidence and safe metadata.
2. BEFORE a vault-export implementation edits product code THEN it SHALL record
   in this spec's `implementation-state.md` whether the slice will safely
   ignore terminal context, render hidden/local navigation, or emit omission
   gaps; the recorded decision SHALL make the matching tests mandatory for that
   PR.
3. WHEN `--minimum-claim-level public-safe` or `demo-safe` is selected THEN
   terminal context SHALL remain hidden unless an explicit source claim catalog
   and separate reviewed demo/concept policy allows a bounded static concept
   rendering.
4. WHEN hidden/local terminal context is rendered THEN the output SHALL keep
   existing public/demo strictness for raw URLs, raw SQL, raw config, local
   absolute paths, source snippets, secrets, raw remotes, production data, and
   private sample identifiers.
5. WHEN terminal context nodes or edges are generated THEN they SHALL link back
   to the property-flow path, source, rule, gap, and limitation pages instead
   of standing alone as proof.
6. WHEN terminal context is omitted by claim-level filtering or safety policy
   THEN vault export SHALL emit or reuse a rule-backed omission/safety gap
   rather than silently dropping meaningful graph evidence.

## Requirement 5: Reporting Consumers Stay Additive

**User Story:** As a report consumer, I want existing report readers to remain
compatible while optionally displaying terminal context.

### Acceptance Criteria

1. WHEN implementation touches report rendering THEN Markdown/JSON output SHALL
   remain deterministic for identical inputs.
2. WHEN report version `1.0` is preserved THEN terminal-context consumer
   changes SHALL be additive and safely ignorable by existing readers.
3. WHEN a consumer requires a new top-level collection or changes the meaning
   of existing property-flow path, node, edge, gap, inventory, root, or summary
   fields THEN the implementation SHALL bump or version the consumer schema and
   document compatibility.
4. WHEN rendering path notes or safe metadata THEN wording SHALL say static
   terminal context or static evidence context, not execution, database access,
   persistence, dependency execution, impact, or complete coverage.
5. WHEN report consumers sort terminal context rows, notes, graph items, or
   chunks THEN ordering SHALL be ordinal and stable.

## Requirement 6: Rule Catalog And Docs Are Source-Of-Truth Updates

**User Story:** As an implementer, I want consumer behavior and public-facing
limitations documented before output claims expand.

### Acceptance Criteria

1. WHEN a consumer emits a new chunk family, graph node kind, graph edge kind,
   note kind, gap code, limitation, or redaction category THEN
   `rules/rule-catalog.yml` SHALL include the rule, emitted artifact,
   evidence tier, and limitations in the same implementation PR.
2. WHEN existing rules already cover the consumer output THEN implementation
   SHALL reuse them and document the mapping in the relevant docs or tests.
3. WHEN docs are updated THEN they SHALL state that SQLite/facts/reports/rule
   catalog remain authoritative and that exports are deterministic consumers.
4. WHEN public/demo concept wording is introduced THEN it SHALL explain why the
   rendering is demo/concept only, identify the reviewed inputs that permit it,
   and avoid product claims beyond static evidence navigation.
5. WHEN no public/demo justification exists THEN public claim level SHALL remain
   hidden and no site copy SHALL be touched.

## Requirement 7: Explicit Non-Claims

**User Story:** As a TraceMap reviewer, I want consumer output to avoid
overstating what terminal-context evidence proves.

### Acceptance Criteria

1. Consumers SHALL NOT claim runtime behavior, browser visibility, user
   interaction, authorization behavior, feature-flag state, dependency-injection
   runtime target selection, serializer runtime behavior, branch feasibility,
   database execution, persistence outcome, traffic, release safety, business
   impact, or complete coverage.
2. Consumers SHALL NOT say a property, endpoint, dependency, database, or source
   is "impacted" unless a separate reducer with rule-backed evidence produced
   that exact impact claim.
3. Consumers SHALL NOT use LLM calls, embeddings, vector databases, or
   prompt-based classification in TraceMap scanner, reducer, report, docs
   export, vault export, or terminal-context consumer logic.
4. Consumers SHALL NOT store raw source snippets by default. Any future raw
   snippet option must be explicit, local, hidden, documented, and outside this
   first consumer spec slice.
5. Consumers SHALL NOT infer terminal context from path-note prose alone, same
   route, same endpoint, same class, same file, same property name, or broad
   dependency reachability.

## Requirement 8: Validation Coverage

**User Story:** As an implementer, I want tests that prove terminal-context
consumer behavior is deterministic, safe, and compatibility-preserving.

### Acceptance Criteria

1. Tests SHALL prove docs export either safely ignores or safely renders
   additive `terminalContextKind` metadata and `StaticTerminalContext` notes.
2. Tests SHALL prove vault export either safely ignores or safely renders
   property-flow terminal context while preserving claim-level filters and
   hidden/local safety.
3. Tests SHALL match the implementation-state decision record for each touched
   consumer; a render, ignore, or omission-gap decision makes that behavior
   mandatory for the implementation PR.
4. Tests SHALL prove consumer output preserves rule IDs, tiers, supporting IDs,
   commit SHA, extractor versions, spans, and limitations when input provides
   them.
5. Tests SHALL prove terminal context is not promoted to impact, runtime, DB
   execution, dependency execution, or complete coverage language.
6. Tests SHALL prove unsafe metadata and note text are omitted, hashed,
   category-labeled, or gapped according to existing safety rules.
7. Tests SHALL prove deterministic output for repeated equivalent inputs.
8. Tests SHALL prove unsupported or older property-flow schema produces a
   schema/compatibility gap instead of a stronger conclusion.
9. Tests SHALL prove a malformed fixture with structured `terminalContextKind`
   and contradictory `StaticTerminalContext` prose prefers structured metadata,
   emits or reuses a consistency/schema gap, and avoids stronger conclusions.
10. Tests SHALL prove absent `terminalContextKind` produces no negative
   no-surface language.
11. Tests SHALL prove claim-level filtering that removes meaningful terminal
   context emits or reuses a rule-backed omission gap instead of silently
   dropping it.
12. Tests SHALL prove unknown additive safe metadata is ignored by existing
   readers unless the implementation explicitly renders that metadata.
13. Implementation validation SHALL include focused reporting/export tests,
   `dotnet test src/dotnet/TraceMap.sln` unless explicitly narrowed with a
   recorded reason, `./scripts/check-private-paths.sh`, and `git diff --check`.
