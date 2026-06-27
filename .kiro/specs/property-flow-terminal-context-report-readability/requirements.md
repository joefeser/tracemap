# Property Flow Terminal Context Report Readability Requirements

## Introduction

TraceMap now has property-flow terminal context coverage and a consumer runway
for docs export, vault export, and reporting. This spec narrows the remaining
reporting/documentation closure: make optional human-readable property-flow
terminal context easier to inspect without changing the static evidence model.

This is a hidden, implementation-ready spec for a future .NET reporting/docs
slice. It is not a scanner, reducer, docs-export implementation, vault-local
navigation implementation, public site, or product-claim spec.

Public claim level: hidden.

## Source Material

- `.kiro/specs/ui-field-property-lineage-terminal-context-coverage/`
- `.kiro/specs/ui-field-property-lineage-terminal-context-consumers/`
- `.kiro/specs/ui-field-property-lineage-composition/`
- `.kiro/specs/ui-field-property-lineage-continuation/`
- `.kiro/specs/evidence-export-usability-polish/`
- `.kiro/specs/vault-export-hidden-safety/`
- `src/dotnet/TraceMap.Reporting/PropertyFlowReport.cs`
- `src/dotnet/tests/TraceMap.Tests/PropertyFlowTests.cs`
- `docs/VALIDATION.md`
- `rules/rule-catalog.yml`

## Scope

In scope:

- Optional readability polish for existing property-flow Markdown/report
  rendering of structured `terminalContextKind` and `StaticTerminalContext`
  notes.
- Documentation closure that explains hidden static-only terminal-context
  semantics for local maintainers and implementers.
- Focused tests proving any rendering change remains additive, deterministic,
  evidence-backed, and safely ignorable.
- Rule-catalog updates only if the implementation emits a new rule-backed
  reporting artifact, gap, limitation, or validation finding.

Out of scope:

- New scanner facts, reducer conclusions, impact claims, schema migrations, or
  persisted terminal-context tables.
- Docs-export terminal-context implementation. That belongs to the terminal
  context consumers spec.
- Vault hidden/local terminal-context navigation. That belongs to the terminal
  context consumers and vault-local-navigation work.
- Public site copy or public/demo claim promotion.
- Runtime execution, database execution, browser execution, telemetry,
  authorization, feature-flag, DI runtime target, serializer runtime, branch
  feasibility, traffic, release-safety, ownership, vulnerability, or business
  impact proof.
- LLM calls, embeddings, vector databases, prompt-based classification, or
  natural-language answer generation in TraceMap core.

## Requirement 1: Preserve Static Path-Scoped Semantics

**User Story:** As a maintainer, I want report readability improvements to
keep terminal context attached to the exact property-flow path/node evidence.

### Acceptance Criteria

1. WHEN report rendering displays terminal context THEN it SHALL render only
   from structured `PropertyFlowNode.SafeMetadata["terminalContextKind"]` or
   existing bounded path notes produced from the current scan.
2. WHEN terminal context is rendered THEN it SHALL remain attached to the
   property-flow path or node carrying the metadata, not promoted to a
   source-wide, endpoint-wide, property-wide, package-wide, or impact-wide
   conclusion.
3. WHEN `terminalContextKind` is absent THEN output SHALL NOT claim that no
   terminal surface exists.
4. WHEN terminal context is present THEN wording SHALL say static terminal
   context, static evidence context, or equivalent bounded wording.
5. Report output SHALL NOT say or imply runtime execution, database execution,
   dependency execution, persistence outcome, complete coverage, or impact.

## Requirement 2: Keep Report Contracts Additive

**User Story:** As a report consumer, I want existing property-flow JSON and
Markdown readers to keep working after readability polish.

### Acceptance Criteria

1. IF implementation preserves report version `1.0` THEN changes SHALL be
   additive display-only behavior over existing path notes or safe metadata.
2. IF implementation changes a top-level collection, removes a field, changes
   the meaning of an existing field, or requires consumers to parse a new
   terminal-context structure THEN it SHALL version the affected schema and
   document compatibility.
3. Markdown output ordering SHALL be deterministic by report/source/path/node
   order using ordinal comparisons where names must be sorted.
4. JSON output SHALL remain byte-stable for equivalent inputs.
5. Existing readers that ignore unknown safe metadata SHALL continue to see the
   same evidence identity and path classification.

## Requirement 3: Preserve Evidence Identity

**User Story:** As a reviewer, I want every rendered terminal-context cue to
remain traceable to rule-backed evidence.

### Acceptance Criteria

1. WHEN terminal context is rendered THEN nearby output SHALL preserve or link
   to available rule IDs, evidence tiers, supporting fact IDs, supporting edge
   IDs, file paths, line spans, source labels, commit SHA, extractor versions,
   coverage labels, and limitations.
2. WHEN a required identity field is missing THEN output SHALL label the view
   partial or unknown instead of inventing identity.
3. WHEN coverage is reduced, extractor identity is missing, traversal caps
   apply, schema is unsupported, or commit SHA is unknown THEN rendered
   terminal context SHALL inherit the same partial/reduced coverage posture.
4. WHEN multiple terminal-context paths are summarized THEN summaries SHALL use
   counts and labels, not stronger merged claims.
5. WHEN an implementation emits a new reporting-specific rule, gap, limitation,
   or validation finding THEN `rules/rule-catalog.yml` SHALL document it in
   the same PR before product code emits it.

## Requirement 4: Improve Readability Without New Analysis

**User Story:** As a local report reader, I want static terminal context to be
easier to scan without creating a new analyzer.

### Acceptance Criteria

1. The implementation MAY add a compact path-local Markdown line, table column,
   or node annotation for terminal context.
2. The implementation MAY keep current rendering unchanged if tests prove
   existing path notes and safe metadata are already readable and documented.
3. The implementation SHALL NOT derive terminal context from note prose when
   structured `terminalContextKind` is unavailable.
4. The implementation SHALL NOT infer terminal context from same route, same
   endpoint, same class, same file, same property name, generic names, broad
   dependency reachability, or docs/vault/export consumer metadata.
5. Any display labels SHALL come from a closed safe vocabulary or be rendered
   as unknown/unsupported static metadata without stronger conclusions.

## Requirement 5: Document Hidden Static-Only Semantics

**User Story:** As a future implementer, I want docs to explain how to read
terminal-context report output without overclaiming.

### Acceptance Criteria

1. Documentation updates SHALL state that terminal context is static,
   path-scoped evidence from TraceMap artifacts, not proof of runtime behavior
   or impact.
2. Documentation SHALL state that facts, SQLite, reports, and rule catalog
   entries remain authoritative.
3. Documentation SHALL state that docs-export, vault, RAG, vector, or other
   downstream systems are consumers of TraceMap evidence and do not become
   evidence for TraceMap conclusions.
4. Documentation SHALL keep public claim level hidden unless a separate
   reviewed public/demo spec changes that level.
5. Documentation SHALL avoid site copy and public product claims.

## Requirement 6: Keep Safety And Redaction Boundaries

**User Story:** As a reviewer, I want readability polish to avoid leaking raw
or unsafe values.

### Acceptance Criteria

1. Rendering SHALL NOT store or emit raw source snippets by default.
2. Rendering SHALL NOT introduce raw SQL, raw config, connection strings, raw
   URLs, endpoint addresses, local absolute paths, raw remotes, credentials,
   private sample names, production data, prompt text, embeddings, vector
   database configuration, or natural-language answer templates.
3. Unsafe metadata or note text SHALL be omitted, hashed, category-labeled, or
   gapped using existing safety rules unless a new rule is documented first.
4. Hidden/local semantics SHALL remain hidden; demo/public strictness SHALL NOT
   be weakened by report readability changes.
5. Generated docs SHALL pass private-path validation.

## Requirement 7: Validation Coverage

**User Story:** As an implementer, I want focused tests and validation that
prove the report remains deterministic and evidence-backed.

### Acceptance Criteria

1. Tests SHALL cover a positive fixture with structured `terminalContextKind`.
2. Tests SHALL cover absent `terminalContextKind` and assert no negative
   no-terminal-surface wording.
3. Tests SHALL cover a synthetic malformed or contradictory note prose fixture
   and assert structured metadata wins or the prose is ignored/gapped; normal
   producer output derives prose and structured metadata from the same source.
4. Tests SHALL cover unknown safe terminal-context values and assert unknown or
   unsupported static metadata handling without overclaiming.
5. Tests SHALL cover deterministic Markdown and JSON output for repeated
   equivalent inputs.
6. Tests SHALL assert output does not contain runtime, DB execution,
   dependency execution, impact, complete coverage, release-safety, or public
   claim wording.
7. Tests SHALL assert rule IDs, evidence tiers, supporting IDs, spans, commit
   SHA, extractor versions, coverage labels, and limitations are preserved in
   JSON/report data when available; Markdown cues may rely on nearby existing
   report context rather than duplicate every field.
8. Validation SHALL run focused property-flow/reporting tests, `dotnet test`
   unless explicitly narrowed with a recorded reason, `./scripts/check-private-paths.sh`,
   and `git diff --check`.
