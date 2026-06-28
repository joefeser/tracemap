# UI Field Property Lineage Terminal Context Vault Local Navigation Design

## Overview

This design narrows the existing terminal-context consumer runway to one
implementation slice: hidden/local vault navigation for
`terminalContextKind`. It consumes already-emitted property-flow evidence and
projects it into deterministic vault graph and Markdown navigation.

Evidence flow:

```text
combined index / property-flow report
  -> lineage path with selected-property terminal bridge
  -> node.safeMetadata.terminalContextKind
  -> hidden vault terminal-context node or omission gap
  -> graph.json and Markdown navigation with preserved evidence identity
```

The producer gate is not reopened. Docs-export is not implemented here.

## Current Context

This spec was drafted from an isolated worktree created from latest
`origin/dev`:

```text
c37eff84ebc12cc2b4d47bae89fb8af29b35b8bb
```

That commit is merge commit `#405`, `implement-swift-inventory-project-discovery`.

Relevant live-code observations:

- `PropertyFlowReport.cs` writes `terminalContextKind` into node
  `SafeMetadata` only when `includeTerminalContext` is true.
- `includeTerminalContext` is tied to the selected-property bridge gate.
- `StaticTerminalContext` is path-note display text and says the evidence is
  static context, not runtime execution, dependency execution, database
  execution, or impact proof.
- `VaultExport.cs` already supports claim levels, graph nodes/edges/gaps,
  deterministic content hashes, generated-file collision checks, and hidden
  safety classification.
- `VaultExport.cs` does not currently read property-flow report JSON and has no
  existing input seam that carries gated `terminalContextKind`.
- `rules/rule-catalog.yml` already has property-flow rules and vault-export
  safety/gap rules that should be reused before adding new ones.

## Relationship To Existing Specs

| Existing spec | Relationship |
| --- | --- |
| `ui-field-property-lineage-terminal-context-consumers` | Parent runway. This spec implements the PR 2 vault hidden/local lane only. |
| `ui-field-property-lineage-terminal-context` | Producer gate for path-scoped terminal context. This spec consumes, not changes, that output. |
| `evidence-graph-vault-export` | Defines vault graph, Markdown, sentinels, claim levels, and deterministic output. |
| `vault-export-hidden-safety` | Defines hidden/local safety transforms and strict public/demo behavior. |
| `rag-import-evidence-docs` | Adjacent docs-export/RAG work. Out of scope here. |

## Non-Goals

- No docs-export chunks, JSONL, evidence docs, or RAG/vector ingestion.
- No scanner, reducer, language-adapter, or combined-index schema changes.
- No new terminal-context producer mapping.
- No public site copy or product claims.
- No runtime/browser/live HTTP/database validation.
- No impact conclusions.
- No raw source snippets by default.
- No LLM calls, embeddings, vector databases, or prompt-based classification.

## Input Contract

The first implementation SHALL consume terminal-context evidence from an
explicit compatible property-flow report JSON input to vault export. The
implementation may add a narrow `--property-flow-report <property-flow.json>`
option or extend an existing report-input collection only if the CLI contract
stays explicit and documented. Do not infer terminal context from combined
path evidence alone, and do not add a docs-export file-reading seam for this
feature.

This explicit seam is required because the current vault inputs
(`--combined-index`, `--paths-report`, and `--reverse-report`) do not carry the
producer-gated `terminalContextKind` key. Re-deriving terminal context from
`surfaceKind` inside vault export would bypass the selected-property bridge
gate and is out of scope.

Preferred structured fields:

- `reportType: property-flow`
- `version: 1.0` or a compatible successor
- `sources[].sourceIndexId`
- `sources[].scanId`
- `sources[].commitSha`
- `sources[].extractorVersions`
- `lineagePaths[].pathId`
- `lineagePaths[].classification`
- `lineagePaths[].supportingFactIds`
- `lineagePaths[].supportingEdgeIds`
- `lineagePaths[].nodes[].nodeId`
- `lineagePaths[].nodes[].nodeKind`
- `lineagePaths[].nodes[].ruleId`
- `lineagePaths[].nodes[].evidenceTier`
- `lineagePaths[].nodes[].filePath`
- `lineagePaths[].nodes[].startLine`
- `lineagePaths[].nodes[].endLine`
- `lineagePaths[].nodes[].safeMetadata.terminalContextKind`
- `lineagePaths[].edges[].ruleId`
- `lineagePaths[].edges[].evidenceTier`
- `coverageWarnings[]`
- `limitations[]`

`StaticTerminalContext` path notes are display hints. They may be copied after
safety validation, but they must not create terminal-context graph items unless
structured metadata is present. If the property-flow report schema is missing
required fields, unsupported, or lacks stable evidence identity, vault export
SHALL emit a schema gap or reject the report according to existing vault input
compatibility behavior rather than deriving terminal context from prose.

## Output Model

Recommended hidden graph additions:

| Item | Kind | Purpose |
| --- | --- | --- |
| Terminal-context node | `terminal-context` | Path-scoped local navigation for the terminal context kind. |
| Property-flow terminal-context edge | `property-flow-terminal-context` | Connects the property-flow path or terminal source node to the terminal-context node. |
| Terminal-context tag | `tracemap/property-flow/terminal-context` | Local filtering/navigation cue. |
| Claim tag | `tracemap/claim/hidden` | Reinforces hidden-only claim level. |
| Omission gap | `TerminalContextClaimLevelOmitted` classification or successor | Explains demo/public omission or safety omission. |

If the existing graph model cannot represent a property-flow path node cleanly,
the first implementation may link the terminal-context node to the terminal
source/surface node and represent `propertyFlowPathId` through existing safe
graph fields such as `SourceScope`, `SupportingFactIds`, `SupportingEdgeIds`,
or `EvidenceLocations`. It must still preserve path-scoped limitations and
supporting IDs.

Recommended terminal-context node fields, aligned with the current
`VaultGraphNode` record:

```json
{
  "id": "node:terminal-context:<hash>",
  "kind": "terminal-context",
  "claimLevel": "hidden",
  "displayName": "data-surface terminal context",
  "sourceId": "source:...",
  "sourceScope": "property-flow-path:path:...",
  "surfaceKind": "terminal-context",
  "commitSha": "012345...",
  "coverage": ["partial"],
  "ruleIds": ["property-flow.path.v1"],
  "evidenceTiers": ["Tier2Structural"],
  "supportingFactIds": [],
  "supportingEdgeIds": [],
  "limitations": [
    "Static terminal context is path-scoped local navigation, not runtime execution or impact proof."
  ],
  "filePath": "terminal-context/<slug>.md",
  "evidenceLocations": [],
  "surfaceSubtype": "data-surface-terminal-context"
}
```

The exact C# type shape should follow existing `VaultGraphNode` and
`VaultGraphEdge` records. Do not add an ad hoc `safeMetadata` property to
`VaultGraphNode` unless a versioned graph schema change is explicitly designed.
Path ID, terminal node ID, and terminal context kind should be represented via
existing safe fields such as `SourceScope`, `DisplayName`, `SurfaceSubtype`,
supporting IDs, and evidence locations, or through a catalogued schema
extension.

`property-flow.path.v1` in the example is the preserved source evidence rule
for the path. If implementation emits a new terminal-context graph node or
edge kind, the vault packaging behavior needs its own catalogued rule such as
`vault-export.graph.property-flow-terminal-context.v1` before product code
emits that node or edge.

## Stable IDs

Use context-separated hashes. Suggested node input:

```text
node/terminal-context/v1
sourceIndexId
scanId or none
commitSha or unknown
propertyFlowPathId
terminalNodeId
terminalContextKind
ruleId
```

Suggested edge input:

```text
edge/property-flow-terminal-context/v1
fromNodeId
toTerminalContextNodeId
propertyFlowPathId
ruleId
evidenceTier
sorted supportingFactIds
sorted supportingEdgeIds
```

Every component must pass existing vault source-value or identity-component
safety classification before ID construction. If a required component is
unsafe, omit the graph item and emit a safety gap. Do not build fallback IDs
from raw rejected values.

Sort order for `supportingFactIds` and `supportingEdgeIds` SHALL match the
existing vault ID-construction convention: ordinal lexicographic string sort on
the stable ID value. If an implementation discovers a different local
convention in `VaultExport.cs`, it must record that convention in this spec's
`implementation-state.md` before product edits.

## Claim-Level Behavior

Hidden output:

- Render terminal-context navigation when structured metadata is present and
  safe.
- Preserve path/source/rule/gap/limitation links.
- Mark output partial when safety omission or schema gaps affect graph
  interpretation.

Demo/public output:

- Do not render terminal-context navigation from this spec.
- Emit or reuse an omission gap when hidden terminal-context evidence was
  available but filtered.
- Do not allow a source claim catalog to promote this navigation unless a later
  separate reviewed public/demo policy says so.
- Continue to fail when claim-level filtering leaves no visible non-gap graph
  evidence, matching existing vault behavior.

## Rule Plan

Reuse first:

- `property-flow.path.v1`
- `property-flow.edge.v1`
- `property-flow.coverage.v1`
- `property-flow.schema.v1`
- `vault-export.gap.hidden-evidence-omitted.v1` where applicable
- `vault-export.validation.unsafe-value-rejected.v1`
- `vault-export.gap.unsafe-symbol-omitted.v1`
- `vault-export.gap.hidden-safe-context-omitted.v1`
- `vault-export.gap.unsafe-id-component-omitted.v1`

Current vault graph nodes and edges preserve underlying source evidence rule
IDs. There is no reusable generic `vault-export.graph.v1` in the live catalog.
Use source rules for source evidence identity, and add a vault packaging rule
only when introducing a new emitted graph kind requires one.

Candidate new rules only if reuse is insufficient:

- `vault-export.graph.property-flow-terminal-context.v1`
- `vault-export.gap.terminal-context-omitted.v1`

Candidate `VaultGraphGap.Classification` values:

- `TerminalContextClaimLevelOmitted`
- `TerminalContextMetadataUnsafe`
- `TerminalContextEvidencePartial`
- `TerminalContextStructuredNoteMismatch`
- `PropertyFlowTerminalContextSchemaUnsupported`

Gap-classification mapping:

| Condition | Preferred gap classification | Required rule backing |
| --- | --- | --- |
| Hidden evidence is present but `demo-safe` or `public-safe` filtering removes terminal-context navigation. | `TerminalContextClaimLevelOmitted` | Reuse existing hidden-evidence omission rule if it fits; otherwise add `vault-export.gap.terminal-context-omitted.v1`. |
| Structured `terminalContextKind` is present but is unrecognized by the current vault vocabulary. | `PropertyFlowTerminalContextSchemaUnsupported` | Reuse `property-flow.schema.v1` plus a vault schema gap if available; otherwise add `vault-export.gap.terminal-context-omitted.v1` with `Tier4Unknown`. |
| Structured metadata and `StaticTerminalContext` prose name different terminal contexts. | `TerminalContextStructuredNoteMismatch` | Add or reuse a catalogued schema/consistency gap before product code emits it. If new, use `vault-export.gap.terminal-context-omitted.v1` with `Tier4Unknown`; limitation: the gap reports a structured/prose conflict and treats structured metadata as more authoritative, but does not prove the prose is wrong. |
| Safety validation omits or hashes terminal-context display/evidence components. | `TerminalContextMetadataUnsafe` | Reuse existing vault hidden-safety gaps where their limitations fit. |
| Schema gaps, safety gaps, or claim-level filtering make terminal-context navigation incomplete. | `TerminalContextEvidencePartial` | Reuse existing vault partial graph settings plus the triggering gap rule. |

No product code may emit a candidate rule or gap until
`rules/rule-catalog.yml` documents the emitted artifact, evidence tier, and
limitations.

## Safety Rules

Terminal context kind is a closed safe display vocabulary from the producer.
Known values should pass as `ClosedVocabulary`. Unknown values should be
treated as schema-compatible but unrecognized safe metadata: render a category
label or gap, never infer a stronger kind.

The authoritative current vocabulary is the `TerminalContextKind` switch in
`PropertyFlowReport.cs`. Structured/prose mismatch tests are defensive
malformed-input fixtures because normal producer output derives
`terminalContextKind` and `StaticTerminalContext` from the same mapping.

Evidence fields use their existing contexts:

- file paths: `RepoRelativePath` or `EvidenceLocation`;
- node IDs, path IDs, source IDs: `StableTraceMapId`;
- rule IDs: `RuleId`;
- evidence tiers, claim levels, classifications, terminal-context tags:
  `ClosedVocabulary`;
- display names: hidden-only safe display context.

Hard-fail categories from `vault-export-hidden-safety` remain hard failures in
every mode.

## Markdown Navigation

If terminal-context nodes get notes, recommended body sections are:

1. Summary table with claim level, terminal context kind, property-flow path ID,
   rule ID, evidence tier, and coverage status.
2. Evidence links to source, rule, gaps, limitations, and related graph nodes.
3. Supporting IDs table with bounded counts and safe IDs.
4. Limitations and non-claims copied from property-flow/vault rules.

Avoid standalone proof language. Prefer:

```text
This note is hidden local navigation over static property-flow evidence.
It does not prove runtime execution, database execution, dependency execution,
or impact.
```

## Test Plan

Focused implementation tests should use fixtures rather than rerunning the
producer mapping switch:

- hidden render from structured `terminalContextKind`;
- absent key does not render terminal context and does not claim absence;
- unknown safe value produces a schema/unknown gap or category label;
- structured/prose mismatch prefers structured metadata;
- public/demo filtering emits an omission gap;
- source claim catalog does not promote terminal context;
- unsafe path/display/metadata cannot enter `graph.json` or Markdown;
- generated graph and Markdown are byte-stable across equivalent runs;
- multiple property-flow paths with the same terminal-context kind remain
  path-scoped in counts and are not merged into a stronger claim;
- terminal-context graph nodes or edges are not emitted unless their required
  source and vault packaging rules exist in `rules/rule-catalog.yml`;
- generated-file collision and stale hash behavior remain unchanged;
- output wording excludes runtime, execution, impact, and complete coverage
  claims.

Validation commands:

```bash
git diff --check
./scripts/check-private-paths.sh
dotnet test src/dotnet/TraceMap.sln
```

Implementation PRs may run a narrower focused test set only when
`implementation-state.md` records why the full suite was explicitly deferred.
