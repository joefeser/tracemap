# Legacy Data Model Reporting Integration Design

## Overview

This design integrates legacy data model evidence into TraceMap's user-facing
report and export layers. It is a projection and rendering layer over static
facts and existing graph/report workflows.

Intended evidence chain:

```text
DBML / EDMX / typed DataSet / TableAdapter / NHibernate / unsupported ORM gap
  -> LegacyData* facts and legacy.data.* rule IDs
  -> optional model identity and generated-code linkage metadata
  -> legacy-data dependency surface projection
  -> combined reports, paths, route-flow, reverse, release-review, vault/RAG,
     and static HTML explorer rows
```

Every row remains static repository evidence. TraceMap must not claim runtime
database behavior, live schema proof, SQL execution, production data access,
provider runtime behavior, migration execution, or AI-derived classification.

## Design Goals

- Make extracted legacy data model metadata visible in the reports users already
  inspect.
- Preserve source evidence and report-layer provenance separately.
- Keep selector and surface compatibility by using the existing `legacy-data`
  surface kind.
- Tolerate absent optional facts, absent near-term fields, and missing optional
  combined tables.
- Render safe descriptor labels, hashes, rule IDs, evidence tiers, file spans,
  coverage, and limitations.
- Keep output deterministic, byte-stable, and public/demo safe.

## Non-Goals

- No product-code implementation in this spec PR.
- No new scanner facts in this spec PR.
- No runtime database connection, ORM provider load, query execution, live
  schema validation, migration execution, config transform execution, or
  service activation.
- No proof of runtime data access, runtime reachability, production use,
  business criticality, release approval, vulnerability, or compliance.
- No raw SQL, source snippets, connection strings, config values, remotes,
  hostnames, local absolute paths, private routes, private sample labels, or
  secrets in generated user-facing artifacts.
- No LLM calls, embeddings, vector databases, prompt-based classification, or AI
  impact analysis in TraceMap core.

## Existing Foundation

Source extraction owns these existing fact types:

- `LegacyDataMetadataDeclared`
- `LegacyDataEntityDeclared`
- `LegacyDataStorageObjectDeclared`
- `LegacyDataColumnDeclared`
- `LegacyDataMappingDeclared`
- `LegacyDataProviderConfigDeclared`
- `LegacyDataGeneratedCodeLinked`
- `AnalysisGap`

Existing and near-term rules include:

- `legacy.data.metadata.inventory.v1`
- `legacy.data.dbml.v1`
- `legacy.data.edmx.v1`
- `legacy.data.typed-dataset.v1`
- `legacy.data.config.v1`
- `legacy.data.generated-link.v1`
- `legacy.data.model.identity.v1`
- `legacy.data.model.relationship.v1`
- `legacy.data.orm.nhibernate.v1`
- `legacy.data.orm.unsupported.v1`
- `legacy.data.model.generated-link.v1`
- `legacy.data.model.surface.v1`

`legacy.data.model.surface.v1` is projection-only. It should own derived
report/export rows and projection gaps, not source scan facts. Existing source
rules remain visible on supporting facts.

The combined path graph currently treats `LegacyData*` facts and `legacy.data.*`
rules as possible `legacy-data` surfaces. This spec refines that behavior and
extends it consistently across reports and exports.

## Projection Model

### LegacyDataModelDescriptor

Report/export readers should normalize source facts into an internal descriptor
view before rendering:

```text
descriptorId
surfaceKind = legacy-data
sourceIndexId
sourceLabel
scanId
commitSha
extractorVersion
factId
combinedFactId
sourceRuleId
projectionRuleId
evidenceTier
filePath
startLine
endLine
metadataFormat
sourceArtifactType
modelKind
descriptorRole
stableModelKey
displayName
displayNameHash
containerName
containerHash
storageKind
mappingKind
modelRelationshipKind
sourceMetadataFactId
supportingFactIds
supportingEdgeIds
coverageLabel
limitations
redactions
displayClearance
claimLevelContextId
```

`projectionRuleId` is always `legacy.data.model.surface.v1` when a row is
created by the report/export projection layer. `sourceRuleId` remains the rule
from the source fact. If a command simply displays an existing source fact
without creating a derived conclusion, `projectionRuleId` is omitted or null and
only the source rule ID is cited.

In JSON output, `projectionRuleId` should be serialized as `null` when no
projection was created so downstream readers can distinguish "not a derived
row" from "field unknown because this schema predates the field." Markdown
tables may render null as a dash or empty cell according to the output writer.

### Source Artifact Types

Closed values should include:

```text
dbml
edmx-csdl
edmx-ssdl
edmx-msl
typed-dataset-xsd
tableadapter-command
generated-data-code
provider-config
nhibernate-hbm
unsupported-orm-descriptor
unknown
```

`unknown` is the closed value for source facts where the artifact type cannot be
determined from available metadata. If an artifact type value appears in an
index but is not in the closed list, that future or unsupported vocabulary value
becomes a schema gap with a rule ID. Raw unknown values should not become
free-form display strings.

### Descriptor Roles

Closed values should include:

```text
model
entity
storage-object
table
view
routine
column
relationship
adapter
generated-code-link
provider-config
unsupported-descriptor-gap
analysis-gap
unknown
```

Rows with `analysis-gap` or `unsupported-descriptor-gap` are rendered in gaps or
limitations sections, not as terminal dependency surfaces.

### Safe Display Selection

Renderers should choose display text in this order:

1. explicitly safe `displayName`;
2. safe role-specific label such as entity, storage object, column, adapter, or
   mapping label;
3. `displayNameHash` or role-specific hash;
4. `stableModelKey` hash fragment;
5. closed descriptor role and source rule ID.

Unsafe or unreviewed values are omitted or represented as hashes. Hash-only rows
must be visibly hash-only so users do not assume the tool recovered a clear
descriptor name.

## Stable Identity

Row/provenance stable IDs use a versioned hash input:

```text
legacy-data-model-reporting/v1
source stable identity, including sourceIndexId
commit SHA stable category
source rule ID
metadata format
descriptor role
stable model key or safe/hash descriptor key
source artifact type
repo-relative file path or path hash
line span
sorted supporting fact IDs
```

The commit SHA stable category is canonical and unconditional:

- `sha:<value>` only when the output profile permits displaying that commit SHA;
- `sha-present-hidden` when a commit SHA exists but the selected profile does
  not permit displaying it;
- `sha-missing` when no commit SHA is available.

For `AnalysisGap` or `unsupported-descriptor-gap` rows that lack
`stableModelKey`, file path, or line span, the stable ID hash must substitute
the source rule ID, metadata format, descriptor role, source index ID, and
available safe fallback fields in place of the missing ones. Each missing
optional field must contribute a canonical absence token such as `field-absent`
rather than being omitted, so partially populated gap rows produce different
stable IDs from fully populated descriptor rows sharing the same source rule.
`sourceIndexId` is mandatory for gap rows even when the broader source stable
identity is otherwise unavailable.

Cross-snapshot descriptor identity keys used for diff matching are separate
from row/provenance IDs. They must not include commit SHA, commit SHA display
category, scan ID, extractor version, profile-specific display clearance, or
supporting fact IDs. Descriptor diff keys should use source stable identity,
source rule ID, metadata format, descriptor role, stable model key or safe/hash
descriptor key, source artifact type, and repo-relative file path or path hash
where those fields are part of the descriptor's static identity. A
row/provenance ID must not be reused as a before/after descriptor match key.

Do not include local absolute paths, raw remotes, timestamps, random values,
SQLite row order, raw SQL, connection strings, config values, unsafe descriptor
names, or raw snippets.

Duplicate stable IDs within the same report/export produce a duplicate-identity
gap and downgrade affected selectors to needs-review or unknown.

## Combined Report Integration

Combined dependency reports should add or refine `legacy-data` surface rows
from `combined_facts`.

Input facts:

- source `LegacyData*` facts;
- source facts under `legacy.data.*.v1` rules;
- model identity fields where available;
- generated-code link facts;
- `AnalysisGap` facts under legacy data rules, rendered as gaps only.

Projection rules:

1. Exclude `AnalysisGap` from terminal surfaces.
2. Prefer `stableModelKey` and descriptor role metadata.
3. Fall back to current safe properties such as metadata kind, mapping kind,
   safe entity/storage/table/column label, text/hash fields, or source fact ID.
4. Preserve source `ruleId`, `evidenceTier`, `filePath`, line span, source
   label, scan ID, commit SHA, and supporting IDs.
5. Mark precision reduced when only display-name, syntax-only, hash-only, or
   older source fields are available.
6. Sort deterministically by role, format, safe label/hash, source label, and
   stable ID.

If a future combined schema persists derived model surfaces, reports should
prefer persisted derived rows and record the source facts as support. They must
not project the same source evidence twice.

A row is a persisted derived surface only when it is stored in a dedicated
combined derived-surface table defined by the implementation PR and carries
`projectionRuleId = legacy.data.model.surface.v1`. Until that table exists,
readers should treat legacy data model projection as source-fact-derived and
should use source fact IDs plus stable descriptor keys for double-projection
tests.

## Dependency Path Integration

Path graph construction should treat descriptor surfaces as terminal surfaces
only when they are source facts or explicit persisted derived surfaces, not
when they are gaps.

Attachment rules:

- Attach descriptor surfaces to symbols using generated-code links,
  fact-symbol rows, source/target symbols, scoped generated type names, or
  TableAdapter/ORM mapped class fields.
- Avoid global short-name matching across sources.
- Preserve relationship and generated-code uncertainty as edge limitations.
- Classify paths through syntax-only or name-only generated-code linkage as
  needs-review.
- Emit optional-table gaps for missing `combined_fact_symbols`,
  relationship/parameter-forward/argument-flow tables only when those tables
  affect the selected query.

Path output should render the same descriptor fields as combined reports, plus
path node/edge IDs, path classification, and traversal limitations.

## Route-Flow Integration

Route-flow is a specialized composition over route roots. The route-flow
renderer may show two kinds of legacy data model evidence:

- terminal rows: route-flow path reaches a credible `legacy-data` surface;
- supporting rows: descriptor evidence is attached to a generated type,
  adapter, query shape, or ORM mapped class encountered near the route path.

Terminal row fields:

```text
routeFlowRowId
terminalSurfaceId
descriptorId
descriptorRole
metadataFormat
safeDisplayLabelOrHash
classification
routeFlowRuleId
sourceLegacyDataRuleIds
evidenceTier
sourceLabel
commitSha
filePath
lineSpan
supportingFactIds
supportingEdgeIds
coverageLabel
limitations
```

Supporting rows should use separate row IDs and a `supportingContext` label so
users can distinguish nearby descriptor metadata from a terminal static path.

A credible route-to-legacy-data bridge requires at least one of:
`LegacyDataGeneratedCodeLinked`, a fact-symbol attachment linking a route symbol
to a legacy data descriptor, a scoped generated type name matched under
`legacy.data.generated-link.v1` or `legacy.data.model.generated-link.v1`, or a
TableAdapter/ORM mapped class field confirmed under a legacy data source rule.
Name-only, syntax-only, or high-fan-out matches do not constitute a credible
bridge and must produce a supporting row with downgraded confidence, not a
terminal row.

When a credible-bridge evidence record exists but resolves to zero matching
symbols in the combined index, for example because the linked generated-code
symbol has not yet been extracted, route-flow must not emit a terminal row. It
should emit an availability gap citing the bridge evidence record and explaining
that the linked symbol is absent from the combined index, and it should cap the
route-flow classification at `NeedsReviewStaticRouteFlow` or lower for that
route path.

When all reachable legacy data evidence for a route consists solely of
`AnalysisGap` facts, route-flow must not produce a supporting descriptor row
from those gaps. It must emit a scoped availability gap citing the gap rule IDs
and explaining that no credible descriptor evidence was available beyond
analysis gaps.

Route-flow must not say that a route executed SQL, hit a database, selected a
runtime DI binding, or invoked a runtime ORM provider.

## Reverse Query Integration

`tracemap reverse` should support `legacy-data` as a terminal surface kind.
Model-specific selectors can be added after safe identity fields exist.

Selector fields:

```text
--surface legacy-data
--surface-name <safe exact label>
--surface-id <stable descriptor/surface ID>
--surface-hash <display or model key hash>
--metadata-format <closed value>
--descriptor-role <closed value>
```

Only the first three are required for an MVP. Additional selectors must be
closed-vocabulary or hash-based.

Reverse traversal starts from selected descriptor surfaces and walks static
graph evidence toward endpoints, symbols, or sources. It renders paths in
root-to-surface order. No-path results are credible only under full relevant
coverage; otherwise the command emits an unknown gap.

No-path gaps must cite a registered rule ID. Use `combined.reverse.path.v1` only
if its documented scope and limitations already cover the legacy-data-specific
gap context. If legacy-data reverse has a distinct gap cause, such as missing
generated-code links, unsupported ORM descriptors, or absent relationship
tables, the implementation PR must add a new rule catalog entry before emitting
that rule ID. The tasks checklist must be updated to record which rule was
chosen or registered so the decision is not deferred past implementation.

## Diff, Impact, And Release Review

Snapshot comparison should use descriptor stable identity. Diff rows for legacy
data model evidence should include:

- diff row ID;
- before/after descriptor IDs;
- change kind;
- metadata format;
- descriptor role;
- safe label/hash;
- source labels and commit SHAs;
- source and diff rule IDs;
- evidence tiers;
- supporting IDs;
- coverage and limitations.

When before/after descriptor stable identities cannot be uniquely resolved, for
example when two rows share all identity fields except a volatile row ID, the
diff row must be marked `ambiguous-identity`, the matching candidates must be
listed as supporting rows, and the change must be classified as needs-review
rather than a definite add or remove.

Impact and release-review may consume changed descriptor rows as static inputs.
They must not claim schema compatibility, runtime database behavior, migration
success, or production impact. Any downstream classification is capped by
underlying diff/impact/reverse/path classifications and coverage.

Review priority scoring may use closed static features:

- evidence tier;
- full/reduced coverage;
- added/removed/changed descriptor role;
- unsupported ORM gaps;
- duplicate identity;
- generated-code uncertainty;
- truncation;
- path/reverse context presence;
- high fan-out.

It may not use raw descriptor names, private business terms, production
telemetry, vulnerability labels, LLM output, embeddings, or prompt results.

## Vault, RAG, And Evidence Graph Export

Vault/RAG exports should project descriptor surfaces as graph `surface` nodes:

```json
{
  "kind": "surface",
  "surfaceKind": "legacy-data",
  "descriptorRole": "entity",
  "metadataFormat": "dbml",
  "sourceIndexId": "source-index-id-placeholder",
  "sourceLabel": "source-label-placeholder",
  "scanId": "scan-id-placeholder",
  "commitSha": null,
  "displayName": null,
  "displayNameHash": "hash:abcd1234",
  "ruleIds": ["legacy.data.dbml.v1", "legacy.data.model.surface.v1"],
  "evidenceTiers": ["Tier2Structural"],
  "supportingFactIds": [],
  "supportingEdgeIds": [],
  "limitations": []
}
```

The example is synthetic and shows shape only. Placeholder values must be
replaced with actual index-sourced provenance at export time, and the field list
is non-exhaustive; implementations must carry all provenance fields required by
the requirements. `displayName` is null when claim level policy prohibits
display; the safe hash is carried separately.
Implementations should preserve source rule IDs and add exporter-specific rule
IDs only for exporter-created gaps, omitted evidence, validation failures, or
duplicate stable identities.

RAG export remains a deterministic evidence artifact. It must not generate
embeddings, vector stores, prompt summaries, or AI impact classifications in
TraceMap core.

Claim-level behavior:

- hidden/local input may show local-safe hashes and category labels;
- public/demo output displays descriptor names only when a synthetic fixture or
  reviewed claim catalog explicitly permits display;
- hidden rows omitted by claim filtering produce omission gaps and counts.

## Static HTML Explorer Integration

The static explorer should render legacy data model evidence in existing
sections:

- Surfaces
- Paths
- Reducer Results
- Gaps
- Limitations
- Rules
- Evidence Rows

Suggested filters:

- `surfaceKind`
- `metadataFormat`
- `descriptorRole`
- `sourceArtifactType`
- `ruleId`
- `evidenceTier`
- `coverageLabel`
- `gapKind`

The explorer must operate from generated TraceMap artifacts only. It must not
scan repository files, inspect raw SQLite content in the browser, contact a
network service, run AI classification, or search hidden raw facts.

The client-side search index and filter logic must operate only on the safe
rendered fields listed above. It must not index or expose raw fact content, SQL
text, config values, connection strings, file contents, or hidden descriptor
names.

## Optional Evidence And Compatibility

Readers should use these compatibility rules:

| Situation | Behavior |
| --- | --- |
| Current `LegacyData*` facts, no model fields | Render current safe fields; mark model precision reduced. |
| Model identity fields present | Prefer `stableModelKey` and descriptor role fields. |
| Generated-code links absent | Render descriptors; emit linkage gap only when a workflow needs a code bridge. |
| Relationship fields absent | Render entity/storage/column descriptors; omit relationship rows or emit scoped availability gap. |
| Unsupported ORM gap present | Render gap; do not fabricate descriptors. |
| Optional combined table missing | Continue unrelated sections; emit table-specific availability gap where relevant. |
| Unknown `legacy.data.*` rule | Preserve rule ID; render safe known fields; emit unknown-vocabulary gap for unknown roles/formats under `legacy.data.model.surface.v1` unless a narrower rule is registered. |
| Derived surface row already persisted | Use persisted row; do not re-project same source fact. |

## Safety And Redaction

All report/export layers should reuse existing safe rendering primitives. The
safety pass must reject or omit:

- raw SQL;
- raw source snippets;
- literal values;
- config values;
- connection strings;
- secrets and secret-looking values;
- local absolute paths;
- raw remotes;
- hostnames;
- raw endpoint addresses;
- raw private routes;
- private repository or sample names;
- analyzer logs;
- source maps with local paths.

Markdown table cells and HTML text must escape user-controlled syntax. JSON
should omit unsafe raw values rather than storing them for downstream tools.

Claim-level clearance for descriptor display names must be passed to rendering
helpers through an explicit claim-level context object, such as a
`ClaimLevelContext` or output-profile parameter. It must not be inferred from an
output path, caller assembly, or environment variable. Rendering helpers may
accept display clearance from a `source-claim-catalog.v1` entry matching stable
source identity or from a synthetic fixture marker on the source fact. Both
paths must produce a deterministic boolean recorded with the rendered row.
Renderers without a claim-level context must default to hash-only display.

## Rule Catalog Expectations

No new rule IDs are required by this spec if implementations can reuse:

- source legacy data rules for source evidence;
- `legacy.data.model.surface.v1` for report/export projection rows;
- existing combined route-flow, path, reverse, release-review, vault, and
  explorer rules for workflow-specific rows and gaps.

If an implementation emits a new gap, limitation, validation failure, selector
row, or exporter-created decision, it must add a rule catalog entry before
emitting that rule ID. Each rule entry must document limitations and evidence
tier expectations.

## Implementation Slices

### First PR Boundary

Recommended first product-code PR:

1. Add a shared legacy data model descriptor projection helper in reporting.
2. Update combined report/path and reverse surface readers to use the helper.
3. Update route-flow rendering only where it already sees legacy-data surfaces.
4. Add focused tests for safe rendering, optional-field absence,
   `AnalysisGap` exclusion, deterministic ordering, duplicate identity, and
   no raw unsafe values.
5. Leave extractor changes, persisted derived tables, broad release-review
   scoring expansion, vault/RAG expansion, and explorer UI enhancement for
   follow-up slices unless the code change stays small.

### Follow-Up Slices

- Add model-specific reverse selectors.
- Add release-review review-priority feature inputs.
- Add vault/RAG graph node specialization and claim-level tests.
- Add static explorer filters for descriptor role and metadata format.
- Add persisted derived surface rows only after ownership and double-counting
  rules are tested.

## Validation Plan

Spec PR validation:

- `git diff --check`
- `./scripts/check-private-paths.sh`
- existing spec lint/check if present
- Kiro spec review with Opus and Sonnet when available

Implementation PR validation:

- focused .NET tests for touched reporting/export layers;
- route-flow, combined report/path, reverse, release-review, vault/export, and
  explorer tests when those layers change;
- byte-stability tests for JSON/Markdown/HTML outputs;
- public/demo safety tests;
- CLI smoke with synthetic or public-safe fixtures;
- `git diff --check`;
- `./scripts/check-private-paths.sh`.
