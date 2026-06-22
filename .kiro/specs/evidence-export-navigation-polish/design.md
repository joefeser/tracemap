# Evidence Export Navigation Polish Design

## Overview

This spec is a follow-up to existing vault and docs-export work:

- `evidence-graph-vault-export` shipped `tracemap vault export`, generated
  Markdown notes, `graph.json`, sentinels, content hashes, and claim-level
  gates.
- `vault-export-hidden-safety` tightened hidden/local safety handling.
- `rag-import-evidence-docs` shipped `tracemap docs-export`.
- `evidence-export-usability-polish` shipped the first usability slice:
  `Start Here.md`, folder indexes, bounded aliases/tags, graph
  `navigationCategory`, and docs-export claim fields.

The next slice should not replace those contracts. It should add a deterministic
navigation model that makes generated evidence easier to browse from human
questions: "what route is this?", "what data/table/package is nearby?", "what
is weak or missing?", and "what can I cite?"

## Design Principles

- Stable IDs are source-of-truth. Names, aliases, tags, and display titles are
  navigation aids only.
- Generated output remains deterministic under row-order changes.
- Public/demo modes stay strict. Hidden/local may be useful, but must be
  visibly local/hidden and safety-classified.
- No arbitrary winners. Duplicates, collisions, and ambiguity produce stable
  disambiguators or gaps.
- No LLM, embedding, vector DB, prompt-based classification, or generated
  summary output inside TraceMap core.

## Existing Code Seams

Likely implementation touch points:

- `src/dotnet/TraceMap.Reporting/VaultExport.cs`
- `src/dotnet/TraceMap.Reporting/EvidenceDocsExport.cs`
- `src/dotnet/tests/TraceMap.Tests/VaultExportTests.cs`
- `src/dotnet/tests/TraceMap.Tests/EvidenceDocsExportTests.cs`
- `docs/VAULT_EXPORT.md`
- `docs/EVIDENCE_DOCS_EXPORT.md`
- `rules/rule-catalog.yml`

The implementation should inspect current helper functions before adding new
abstractions. If vault and docs-export already have equivalent slug/name/safety
logic, prefer a shared helper only when it removes real duplication without
changing behavior.

## Navigation Record Model

Introduce an internal normalized navigation record where useful:

```text
NavigationEntry
  stableId: string
  sourceKind: route | endpoint | symbol | data-surface | package | rule | gap | limitation | report | chunk
  displayTitle: safe string
  slug: safe deterministic string
  aliases: safe string[]
  tags: closed tracemap/... tags
  evidenceTier: optional tier
  coverage: optional label
  ruleIds: sorted string[]
  supportingIds: sorted string[]
  relatedStableIds: sorted string[]
  safetyProfile: hidden | demo-safe | public-safe
  limitations: sorted stable limitation IDs
```

This can be a private implementation detail. It does not need to become a new
public schema unless doing so simplifies compatibility.

## Safe Name Derivation

Name derivation should be a closed pipeline:

1. Gather candidate parts from known-safe fields.
2. Normalize whitespace and punctuation.
3. Remove or category-label unsafe tokens using the existing safety classifier.
4. Apply length limits.
5. Detect case-insensitive and normalized collisions.
6. Append stable evidence-ID-derived hash disambiguators where needed.
7. Preserve stable IDs in metadata/frontmatter/JSON.

Examples:

- `route GET /admin/user/get-all-roles` may render as
  `Route GET admin-user-get-all-roles` only if the normalized path key is
  already safe.
- A package surface may render as `Package nuget Newtonsoft.Json` if package
  identity is safe.
- Unsafe or ambiguous values render as `Route route-<hash>` or
  `Surface data-surface-<hash>`.
- If no safe category can be derived from the evidence family, rule ID, surface
  kind, or report context, the category defaults to `unknown`, producing names
  such as `unknown-<hash>`.

## Vault Navigation

Vault output should prefer additive files and metadata:

- `Start Here.md` remains the entry point.
- Folder-level `index.md` pages remain generated.
- Add or strengthen optional section indexes for:
  - routes/endpoints;
  - symbols;
  - data surfaces;
  - dependency/package surfaces;
  - rules;
  - gaps;
  - limitations;
  - route-flow or property-flow report evidence when supplied.
- Keep generated-file sentinel behavior. Never overwrite user notes.
- Keep `graph.json` canonical IDs and kinds. Add navigation metadata only as
  optional display fields.

Route-flow navigation must cite the report/fact IDs and limitations. It cannot
claim runtime execution or DI resolution.

Hidden/local evidence locations are repo-relative paths only. Absolute paths,
raw remotes, raw URLs, hostnames, connection strings, and source snippets are
not permitted as evidence-location display values even in hidden/local mode.

## Docs-Export Navigation

Docs-export output should preserve existing JSONL and Markdown behavior while
improving retrieval shape:

- Stable `sectionTitle` and anchors for question-oriented chunks.
- Citation-first sections with rule IDs, tiers, coverage, supporting IDs, and
  limitations near the top of each chunk.
- Backreferences to related chunk IDs or stable evidence IDs when those
  relationships already exist.
- Unsupported additive question families use
  `docs-export.gap.unsupported-question-family.v1`.

Chunk family candidates:

- endpoint review;
- route-flow review;
- touched files/symbols when report evidence is available;
- data/table/query surfaces;
- package/dependency surfaces;
- weak evidence and gaps;
- limitations and safety profile;
- snapshot/change context only when compatible inputs provide stable rows.

Unsupported additive presentation views, including navigation question families
that cannot be produced from the supplied input schema, should reuse
`docs-export.gap.unsupported-question-family.v1`. Canonical CLI chunk-family
failures remain under `docs-export.gap.unsupported-family.v1`. Do not add a
third navigation-specific unsupported-input rule unless implementation discovers
a genuinely new finding class outside both boundaries and documents the
distinction in the rule catalog.

## Compatibility

This spec should prefer additive fields over schema breaks.

Compatibility rules:

- Existing generated sentinel and content hash behavior remains unchanged.
- Existing `graph.json` consumers should ignore new optional fields.
- Existing docs-export JSONL consumers should ignore new optional fields.
- If a schema version bump is required, document old and new behavior and add
  old-format tolerance tests where the previous format is parseable.
- `--force` is only stale-generated-output replacement after new content passes
  safety validation.
- A schema version bump is warranted when chunk ID formats change, existing
  field semantics change, a previously optional field becomes required, or old
  consumers can no longer safely ignore a new field.

## Rules And Limitations

Prefer existing rules:

- `vault-export.*.v1`
- `docs-export.*.v1`
- `docs-export.gap.unsupported-question-family.v1`

Only add new rule IDs if the implementation emits a new class of finding or
gap. Candidate rule IDs, if needed:

- `vault-export.navigation.safe-name.v1`
- `vault-export.gap.navigation-collision.v1`
- `docs-export.navigation.chunk-boundary.v1`

Do not add `docs-export.gap.navigation-unsupported-input.v1` for additive
question-family absence; use `docs-export.gap.unsupported-question-family.v1`
for that case.

Every new rule must include limitations in `rules/rule-catalog.yml`.

## Validation Strategy

Spec PR validation:

```bash
git diff --check
./scripts/check-private-paths.sh
```

Implementation PR validation should add:

```bash
dotnet test src/dotnet/TraceMap.sln --filter "VaultExport|EvidenceDocs"
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
```

Focused tests should cover:

- deterministic safe names under shuffled input order;
- collision disambiguation;
- public/demo unsafe-name rejection;
- hidden/local context-labeled evidence locations;
- hidden/local rejection or omission of absolute paths, raw remotes, raw URLs,
  hostnames, connection strings, and source snippets;
- stable ID preservation in metadata;
- route-flow navigation when compatible report evidence exists;
- unsupported additive question-family gaps;
- Markdown and JSONL parity for chunk titles, citations, anchors, and
  limitations.

## Risks

- **Overclaiming:** navigation can feel like proof. Mitigation: visible
  limitations and evidence IDs.
- **Unsafe names:** friendly labels can leak values. Mitigation: closed-field
  name derivation and classifier tests.
- **Schema churn:** downstream users may parse current JSONL/graph formats.
  Mitigation: additive fields and compatibility notes.
- **Duplicate semantics:** vault and docs-export could drift. Mitigation: shared
  vocabulary and tests, not necessarily shared code.
- **Spec sprawl:** existing usability spec already shipped a large slice.
  Mitigation: this spec is explicitly a follow-up focused on navigation and
  chunk retrieval quality.
