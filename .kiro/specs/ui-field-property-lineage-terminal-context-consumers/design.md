# UI Field Property Lineage Terminal Context Consumers Design

## Overview

PR #400 proved a narrow backend gate: property-flow may add terminal context
only after an existing selected-property bridge reaches a terminal surface
through combined path evidence. This spec does not reopen that gate. It defines
the next consumer layer for docs export, vault export, local report rendering,
and documentation.

Expected evidence flow:

```text
index.sqlite / facts.ndjson / property-flow report / rule catalog
  -> property-flow path with selected-property bridge
  -> node.safeMetadata.terminalContextKind and StaticTerminalContext note
  -> docs/vault/report consumer projection
  -> static, path-scoped navigation metadata with preserved evidence identity
```

Every consumer projection is optional and evidence-preserving. Missing terminal
context remains unknown, not a negative fact.

## Current Context

This spec was drafted from an isolated worktree after fetching `origin/dev`.
The branch starts from:

```text
5e88a10486a1bf0c088ee681f140c643a2635415
```

That commit is `[codex] Add property-flow terminal context gate (#400)`.

Live `PropertyFlowReport.cs` behavior reviewed for this spec:

- `PropertyFlowPath.Notes` can include `StaticTerminalContext` prose.
- `PropertyFlowNode.SafeMetadata` can include `terminalContextKind`.
- `terminalContextKind` is included only when `HasSelectedPropertyBridge`
  allows terminal context for that path.
- Terminal context kinds are derived from terminal surface kinds such as
  `sql-query`, `sql-persistence`, `legacy-data`, `package-config`, message
  surfaces, WCF/ASMX/remoting surfaces, and non-HTTP dependency surfaces.
- HTTP client/route surfaces are not terminal-context kinds in the PR #400
  implementation.
- Report version remains `1.0` because the metadata is additive.

## Relationship To Existing Specs

| Existing spec | Relationship |
| --- | --- |
| `ui-field-property-lineage-terminal-context` | Producer gate implemented by PR #400; this spec consumes its additive path notes and node safe metadata. |
| `ui-field-property-lineage-composition` | Keeps broad endpoint context out of property lineage and establishes static composition boundaries. |
| `evidence-graph-vault-export` | Defines vault graph and Markdown export model that may add terminal-context navigation nodes/edges. |
| `vault-export-hidden-safety` | Defines hidden/local safety handling and public/demo strictness that terminal-context vault output must follow. |
| `evidence-export-usability-polish` | Defines docs/vault usability improvements and downstream RAG boundaries that this spec should reuse. |
| `site-tracemap-tools-vault-export-concept` | Public/demo concept context only; this spec does not touch site files or public claims. |

## Non-Goals

- No product-code implementation in this spec PR.
- No scanner changes, reducer changes, schema migrations, generated outputs, or
  site changes.
- No new source snippets by default.
- No runtime request execution, live HTTP, browser automation, production
  telemetry, DB execution, credential use, or environment probing.
- No authorization, feature-flag, DI runtime target, serializer runtime,
  persistence outcome, branch feasibility, traffic, release safety, or impact
  proof.
- No AI/LLM calls, embeddings, vector databases, or prompt-based
  classification in TraceMap core consumers.
- No public-safe claim promotion unless a separate public/demo spec justifies
  concept rendering.

## Consumer Inputs

Preferred source order:

1. Combined SQLite index and facts as canonical evidence.
2. Property-flow JSON report generated from that index.
3. Rule catalog entries for rule IDs, tiers, emitted artifacts, and limitations.
4. Markdown report notes only as bounded display text, not primary evidence.

Consumers should prefer structured fields:

- `lineagePaths[].pathId`
- `lineagePaths[].classification`
- `lineagePaths[].confidence`
- `lineagePaths[].supportingFactIds`
- `lineagePaths[].supportingEdgeIds`
- `lineagePaths[].nodes[].safeMetadata.terminalContextKind`
- `lineagePaths[].nodes[].ruleId`
- `lineagePaths[].nodes[].evidenceTier`
- `lineagePaths[].nodes[].filePath`
- `lineagePaths[].nodes[].startLine`
- `lineagePaths[].nodes[].endLine`
- `lineagePaths[].edges[].ruleId`
- `lineagePaths[].edges[].evidenceTier`
- `lineagePaths[].edges[].supportingFactIds`
- `lineagePaths[].edges[].supportingEdgeIds`
- `sources[].commitSha`
- `sources[].extractorVersions`
- `coverageWarnings[]`
- `limitations[]`

`terminalContextKind` is the consumer's primary structured terminal-context
key. Consumers should not derive a terminal-context kind from `surfaceKind`
when `terminalContextKind` is absent. The key is expected to appear only when a
non-null closed-vocabulary value was assigned by the producer.

## Docs Export Design

Docs export should treat terminal context as retrieval metadata.

Suggested implementation shape:

1. Extend the property-flow report-family packaging to detect documented
   property-flow reports.
2. Add optional terminal-context metadata to chunks only when a path node
   carries `safeMetadata.terminalContextKind`.
3. Keep chunk claims at static evidence level, for example
   `property-flow-terminal-context`.
4. Preserve source evidence identity in chunk metadata where safe.
5. Bound and sanitize `StaticTerminalContext` notes before rendering.
6. Update docs to state that downstream RAG/vector consumers do not become
   evidence for TraceMap conclusions.

Suggested safe chunk metadata keys:

- `propertyFlowPathId`
- `terminalContextKind`
- `terminalNodeId`
- `terminalNodeKind`
- `terminalRuleId`
- `terminalEvidenceTier`
- `sourceIndexId`
- `scanId`
- `commitSha`
- `extractorVersion`
- `supportingFactIds`
- `supportingEdgeIds`
- `coverageLabel`
- `partialReason`

If existing docs-export schema cannot accept arrays safely, the first PR may
render terminal context as bounded Markdown text while preserving evidence IDs
in existing metadata fields. Any schema change must be additive or versioned.
Before implementation, `implementation-state.md` must record whether the PR
will ignore terminal context or render it. The matching test set is then
required for that PR.

## Vault Export Design

Vault export should make terminal context navigable only as hidden/local static
evidence unless a separate demo/concept policy allows more.

Suggested graph additions, if implementation chooses to render rather than
ignore:

| Item | Purpose | Required evidence |
| --- | --- | --- |
| `terminal-context` node | Path-scoped summary of terminal context kind | Property-flow path ID plus terminal node safe metadata |
| `property-flow-terminal-context` edge | Link property-flow report/path to terminal-context node | Supporting path edge IDs and rule IDs |
| `terminal-context` tag | Local navigation cue | Closed vocabulary value after claim-level filtering |
| `terminal-context-omitted` gap | Explains hidden/demo/public omission | Claim-level or safety rule |

Graph IDs should use context-separated hashes such as:

```text
node/terminal-context/v1
  sourceIndexId
  scanId, omitted if unavailable
  commitSha
  propertyFlowPathId
  terminalNodeId
  terminalContextKind
```

Do not include local absolute paths, raw URLs, raw SQL, raw config, source
snippets, secrets, raw remotes, or unsafe display values in IDs.

Vault notes should link back to rule, source, gap, limitation, and report pages.
Terminal context pages must not stand alone as proof of execution or impact.
Before implementation, `implementation-state.md` must record whether the PR
will ignore terminal context, render hidden/local graph navigation, or emit
omission gaps. The matching test set is then required for that PR.

## Terminal Context Kind Vocabulary

The PR #400 producer currently emits these closed display values through the
`TerminalContextKind` switch:

- `data-surface terminal context`
- `legacy-data terminal context`
- `package/config terminal context`
- `message-surface terminal context`
- `legacy-communication terminal context`
- `dependency-surface terminal context`

Consumers should test against these values or a versioned successor vocabulary.
Unknown values should be treated as schema-compatible but unrecognized safe
metadata: render a category label or gap if needed, and do not infer a stronger
terminal context kind.

Structured/prose mismatch handling is defensive for malformed, tampered,
hand-edited, or older report fixtures. The current producer derives
`terminalContextKind` and `StaticTerminalContext` from the same surface-kind
mapping, so normal producer output should not disagree. In malformed fixtures,
consumers should treat `safeMetadata["terminalContextKind"]` as the primary
structured key and treat `StaticTerminalContext` prose as bounded display text.

## Reporting Design

Report rendering can remain unchanged if existing Markdown/JSON already exposes
path notes and node safe metadata safely. If an implementation improves
readability, it should:

- Keep report version `1.0` only for additive display changes.
- Add a small path-local terminal-context line or table column only when
  structured metadata exists.
- Reuse `StaticTerminalContext` wording or stricter wording.
- Keep ordering stable by path ID, node order, then ordinal metadata key.
- Add compatibility tests for consumers that ignore unknown safe metadata.

Do not create a new top-level `terminalContext` report section in the first PR
unless schema versioning and consumer compatibility are handled in the same PR.

## Rule And Gap Plan

Reuse first:

- `property-flow.path.v1`
- `property-flow.edge.v1`
- `property-flow.coverage.v1`
- `property-flow.schema.v1`
- existing docs-export packaging/redaction/schema rules
- existing vault-export graph/redaction/schema/claim-level rules
- existing combined path surface rules carried by path nodes and edges

Candidate new consumer rules, only if reuse is insufficient:

- `docs-export.chunk.property-flow-terminal-context.v1`
- `vault-export.property-flow-terminal-context.v1`
- `vault-export.terminal-context-omitted.v1`

Candidate gap kinds, only after catalog update:

- `PropertyFlowTerminalContextSchemaUnsupported`
- `TerminalContextMetadataUnsafe`
- `TerminalContextClaimLevelOmitted`
- `TerminalContextEvidencePartial`
- `TerminalContextStructuredNoteMismatch`

No implementation may emit these candidates until `rules/rule-catalog.yml`
documents the rule behavior, emitted artifact, evidence tier, and limitations
in the same PR.

## Public Claim Boundary

Default public claim level is hidden.

Demo/concept output may be justified only when all are true:

- Inputs are already reviewed for demo-safe use.
- The output says static terminal context, not execution or impact.
- The terminal-context value comes from closed safe metadata, not prose parsing.
- Claim-level filtering preserves hidden/private evidence omissions.
- Docs explicitly describe the result as a deterministic consumer projection.

No public site copy is in scope for this spec.

## Implementation Slices

### PR 1: Compatibility And Docs Export

- Add property-flow terminal-context recognition in docs export or prove
  current docs export safely ignores it.
- Preserve evidence IDs, rule IDs, tiers, spans, commit SHA, extractor version,
  coverage, and limitations where available.
- Add docs-export tests for safe render/ignore, unsafe metadata handling,
  deterministic output, and no runtime/impact wording.
- Update docs/rule catalog only if emitted artifacts change.

### PR 2: Vault Hidden/Local Navigation

- Add hidden/local vault graph rendering or explicit omission gaps.
- Preserve public/demo strictness and source claim filtering.
- Add graph/note deterministic tests, claim-level omission tests, and unsafe
  value tests.
- Update vault docs and rule catalog if new node/edge/gap kinds are emitted.

### PR 3: Reporting Readability Polish

- Optionally improve property-flow Markdown rendering of structured terminal
  context.
- Keep output additive or versioned.
- Add compatibility and determinism tests.

The implementer may split further if the first code seam is smaller.

## Validation Strategy

Spec-only PR validation:

- Kiro Opus review when available.
- Kiro Sonnet review when available.
- Patch Medium+ actionable findings.
- `git diff --check`.
- `./scripts/check-private-paths.sh`.
- Confirm diff is limited to this spec folder.

Implementation PR validation:

- Focused docs-export tests if docs export changes.
- Focused vault-export tests if vault export changes.
- Focused property-flow/reporting tests if report rendering changes.
- Rule catalog tests or checks if new rules/gaps are emitted.
- `dotnet test src/dotnet/TraceMap.sln` unless explicitly narrowed with a
  recorded reason.
- `./scripts/check-private-paths.sh`.
- `git diff --check`.
- Relevant `docs/VALIDATION.md` checks if language adapters or scanner
  behavior are touched. This spec expects no adapter/scanner changes.

## Risks And Mitigations

| Risk | Mitigation |
| --- | --- |
| Consumers parse note prose and overclaim. | Prefer structured safe metadata; treat note text as display only. |
| Vault graph makes terminal context look source-wide. | Scope IDs and links to property-flow path and terminal node. |
| Public/demo exports leak hidden context. | Keep default claim level hidden and require claim catalog plus separate policy for demo/concept rendering. |
| New emitted artifacts lack catalog entries. | Catalog-first task gate before implementation emits new node/edge/gap/chunk kinds. |
| Existing report readers break on schema drift. | Keep PR 1 additive or versioned with compatibility tests. |
| Unsafe metadata enters generated docs. | Reuse existing redaction/safety policy; hash, omit, category-label, or gap unsafe values. |
