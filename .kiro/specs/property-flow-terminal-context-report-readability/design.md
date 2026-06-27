# Property Flow Terminal Context Report Readability Design

## Overview

This spec closes a narrow reporting/docs gap after property-flow terminal
context coverage. The intended implementation is optional readability polish:
make existing terminal-context cues easier to scan in local reports while
preserving the hidden, static-only evidence model.

Expected flow:

```text
facts.ndjson / index.sqlite / property-flow report / rule catalog
  -> property-flow path and node evidence
  -> node.safeMetadata.terminalContextKind and StaticTerminalContext note
  -> optional Markdown/report readability cue
  -> local reviewer interpretation with static-only limitations intact
```

The reporting layer is not allowed to discover new terminal context. It may
only render or document terminal context that already exists in property-flow
report data.

## Current Baseline

The branch was drafted from `origin/dev` after terminal-context coverage and
consumer-runway specs were present. Relevant existing behavior:

- Property-flow report version is currently `1.0`.
- Terminal context is additive safe metadata on path nodes:
  `safeMetadata["terminalContextKind"]`.
- Path notes may include bounded `StaticTerminalContext` prose.
- `PropertyFlowReport.RenderMarkdown` already renders path notes as bullet
  items, so `StaticTerminalContext:` prose is already visible in the Markdown
  report with the built-in static-only disclaimer.
- Terminal context appears only after the selected-property bridge gate.
- Known display values include:
  - `data-surface terminal context`
  - `legacy-data terminal context`
  - `package/config terminal context`
  - `message-surface terminal context`
  - `legacy-communication terminal context`
  - `dependency-surface terminal context`
- HTTP client/route surfaces are not terminal-context kinds in the current
  producer behavior.

## Relationship To Nearby Work

| Existing work | Boundary |
| --- | --- |
| `ui-field-property-lineage-terminal-context-coverage` | Provides producer coverage and selected-property bridge tests. This spec does not add producer mapping coverage. |
| `ui-field-property-lineage-terminal-context-consumers` | Owns docs-export and vault consumer behavior. This spec only covers local report readability and docs closure. |
| Active docs-export implementation | Not touched. Future implementers must not edit `EvidenceDocsExport` for this spec unless the terminal-context-consumers spec authorizes it. If a report-readability implementer observes docs-export safe-metadata coupling, record and defer it rather than importing exporter work here. |
| Active vault-local-navigation spec | Not touched. Future implementers must not add vault graph nodes, edges, backlinks, or local navigation here. |
| `evidence-export-usability-polish` | Supplies wording and safety patterns for downstream consumers. This spec reuses those boundaries without expanding exporter behavior. |

## Non-Goals

- No product-code implementation in this spec-only PR.
- No scanner/reducer logic, migrations, or new persisted artifacts.
- No docs-export chunks, docs-export schema changes, or RAG import changes.
- No vault notes, vault graph, backlinks, tags, or hidden/local navigation.
- No public site or public/demo claim promotion.
- No runtime execution or external probing.
- No LLM, embedding, vector database, prompt, or natural-language answer
  generation in TraceMap core.

## Implementation Decision Point

Before product edits, the implementation PR must update
`implementation-state.md` with one of these decisions:

1. `render`: add a compact property-flow report readability cue.
2. `document-only`: keep existing report rendering unchanged and close the
   docs/test gap.
3. `defer`: record why current report data is insufficient and leave tasks
   unchecked.

The selected decision controls mandatory tests. `render` requires positive,
absent, malformed, unknown-value, safety, and deterministic rendering tests.
`document-only` still requires tests or assertions proving existing rendering
does not overclaim and docs accurately explain the existing output. For
`document-only`, the implementer may record that existing
`StaticTerminalContext:` assertions in `PropertyFlowTests.cs` satisfy the
no-overclaim requirements if the assertions cover the Markdown/report behavior
being relied on.

## Suggested Rendering Shape

If `render` is selected, prefer the smallest additive Markdown change:

```text
Static terminal context: data-surface terminal context
Evidence: property-flow.path.v1, <evidence tier>, path <id>, node <id>
```

Acceptable alternatives:

- A path-local table column in an existing path detail table.
- A bounded node annotation near the terminal node line.
- A compact list item under the existing path notes block.

Current baseline Markdown already renders the producer-generated note as a path
note bullet similar to:

```text
- StaticTerminalContext: selected-property path reached data-surface terminal context through existing combined path evidence; this is static context, not runtime execution, dependency execution, database execution, or impact proof.
```

`document-only` leaves that note shape unchanged and documents how to read it.
`render` adds a compact structured cue beside the existing note; it must not
replace the existing note unless compatibility and tests explicitly prove the
change remains additive.

Avoid:

- New top-level terminal-context report sections.
- Source-wide or endpoint-wide terminal-context summaries.
- New JSON structures unless versioning is intentional.
- Parsing `StaticTerminalContext` prose to recover meaning.

## Structured Metadata First

Rendering should use this priority:

1. `PropertyFlowNode.SafeMetadata["terminalContextKind"]`
2. Existing path/node identity fields and rule-backed evidence fields
3. Bounded path note display text as secondary context only

`terminalContextKind` is populated in `SafeMetadata` only when the
selected-property bridge gate allows terminal context. When the bridge is
absent, when the producer suppresses a surface such as HTTP route/client, or
when the value is otherwise unavailable, the key is absent. Rendering must
check key presence and treat absence as unknown/unavailable, not as proof that
no terminal surface exists.

If structured metadata and prose disagree, structured metadata wins. The prose
should be ignored for classification and may be omitted, bounded, or gapped if
the reporting layer already has an appropriate schema/safety gap.

If structured metadata is absent, report rendering must not infer terminal
context from path notes, surface kind, route-flow context, docs-export chunks,
vault metadata, same-file proximity, or name matching.
Rendering may display producer-generated path-note text as text, but
classification and any compact label must come only from structured
`terminalContextKind`.

## Evidence Identity

Readability cues should remain close to existing evidence identifiers:

- rule ID
- evidence tier
- path ID
- node ID where available
- supporting fact IDs
- supporting edge IDs
- file path and line span
- commit SHA
- extractor version
- coverage label
- limitations

If the current Markdown report already renders some of these near the path,
the terminal-context cue may rely on nearby context rather than duplicate every
field. Tests should still assert the identity is preserved in JSON/report
fixtures.

## Documentation Closure

Docs should describe terminal-context report output as hidden, static,
path-scoped evidence. Suitable docs targets may include:

- property-flow reporting docs if present;
- `docs/VALIDATION.md` if validation guidance changes;
- a short local maintainer note if no user-facing property-flow doc exists.

Docs should not modify `site/`.

Required wording concepts:

- Static evidence context, not runtime behavior.
- Absence is unknown, not proof of no terminal surface.
- Downstream docs/vault/RAG/vector systems consume TraceMap evidence; they do
  not create TraceMap evidence.
- Report readability does not produce impact claims.

## Rule Catalog Plan

Prefer no new rule IDs. Report readability should normally reuse existing
property-flow path/node/source rules and the rule IDs already carried by the
path evidence.

Add a rule only if the implementation emits a new machine-readable reporting
artifact, gap, validation finding, or limitation. Candidate names, if reuse is
insufficient:

- `property-flow.report.terminal-context-readability.v1`
- `property-flow.gap.terminal-context-metadata-unsafe.v1`
- `property-flow.gap.terminal-context-note-mismatch.v1`

No candidate may be emitted until `rules/rule-catalog.yml` documents emitted
artifacts, evidence tier, behavior, and limitations in the same PR.

## Compatibility

The preferred implementation keeps property-flow report version `1.0` because
the rendering is additive and based on existing notes/safe metadata. A version
bump is required if implementation:

- changes the meaning of an existing JSON field;
- adds a required top-level collection;
- removes or renames existing report fields;
- makes existing readers parse terminal context to understand path identity;
- changes machine-readable schema compatibility.

Markdown readability alone should not require a version bump.

## Validation Plan

For the implementation PR:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter PropertyFlowTests
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

If docs-only implementation does not touch Markdown writer code, record the
narrowed test rationale in `implementation-state.md` and still run focused
property-flow tests plus safety/whitespace checks.

`./scripts/check-private-paths.sh` applies to every touched file, including
spec docs and future report/docs files, and should catch accidental private
paths in generated wording or fixtures.

## Risks And Mitigations

| Risk | Mitigation |
| --- | --- |
| Readability wording becomes an impact claim. | Ban impact/runtime/database-execution terms and assert in tests. |
| Report layer rediscovers terminal context from prose or proximity. | Require structured `terminalContextKind` as the only classification source. |
| This overlaps docs-export implementation. | Keep `EvidenceDocsExport` out of scope and reference the consumers spec. |
| This overlaps vault local navigation. | Keep vault graph/note/backlink work out of scope. |
| Unknown future terminal-context values are overinterpreted. | Render unknown safe metadata as unknown/unsupported static metadata only. |
| Hidden evidence leaks into public/demo output. | Preserve hidden public claim level and run private-path validation. |
