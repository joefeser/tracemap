# SQL Schema Change Impact Design

## Overview

SQL Schema Change Impact is a SQL-specific delta adapter for TraceMap's contract-delta impact workflow. It should reuse the shared SQL evidence contract, combined surface projection, path query, reverse query, diff, impact, and contract-delta machinery rather than adding a second SQL graph or a competing impact engine.

```text
sql-schema delta -> safe SQL reference selectors
                 -> contract-delta impact inputs
                 -> single-index facts or combined sql-query/sql-persistence surfaces
                 -> optional path/reverse context
                 -> Markdown/JSON impact report with caveats
```

The command is deterministic and read-only. It does not connect to a database, execute migrations, parse dialect-specific SQL semantics, or infer runtime behavior.

## Core Decisions

- CLI: v1 uses `tracemap reduce --index <index.sqlite> --sql-schema-delta <delta.json> --out <path>`.
- Engine: v1 normalizes SQL/schema deltas into the existing contract-delta impact engine. It is not a standalone impact engine.
- Rule strategy: v1 reuses `contract.delta.input.v2`, `contract.delta.impact.v2`, and `contract.delta.context.v2` once they are documented, unless implementation proves a SQL-specific rule is required. Any new rule must be documented in `rules/rule-catalog.yml` before code merges.
- Change IDs: v1 requires explicit change `id` values. Missing IDs are validation gaps or strict validation failures. The implementation must not derive implicit change IDs in v1.
- Path/reverse context: opt-in only via `--include-paths` and `--include-reverse`.
- SQL persistence: `sql-persistence` is included by default for table, column, mapping, and persistence-surface deltas, but it remains distinct from `sql-query`.
- Input exclusivity: `--sql-schema-delta` and `--contract-delta` are alternate reduce inputs and are mutually exclusive.
- Output naming: active input mode controls default file names; SQL/schema mode uses `sql-impact-report.*` and existing contract-delta mode keeps existing names.

## Relationship To Existing Specs

- `sql-dependency-surfaces` defines `SqlTextUsed`, SQL-shape `QueryPatternDetected`, `DatabaseColumnMapping`, `sql-query`, and `sql-persistence`.
- `contract-delta-impact-v2` defines the broader impact engine and report semantics that this spec reuses.
- `combined-change-impact` defines before/after combined impact over changed evidence.
- `reverse-impact-query` and `combined-dependency-paths` define reachability context.

This spec is narrower than contract-delta impact v2. Its value is the SQL/schema delta input vocabulary, SQL selector normalization, SQL-specific evidence caveats, and focused report labels. The implementation should call into shared impact machinery wherever the existing model can carry the same evidence without loss.

## Goals

- Make schema, table, column, query-shape, SQL resource, and mapping changes first-class deterministic input.
- Match SQL changes against static SQL evidence across languages and combined indexes.
- Distinguish query evidence from mapping evidence and runtime execution claims.
- Surface path/reverse context when stable selectors exist.
- Keep hash-only, schema-only, table-only, mappedName-only, unlinked, reduced-coverage, and identity-conflicted evidence review-tier.
- Emit safe Markdown/JSON reports suitable for release review.

## Non-Goals

- No database connection or schema introspection.
- No migration execution or migration correctness claims.
- No dialect parser or semantic SQL validation.
- No query-plan, permission, tenant, transaction, or data-value inference.
- No generated SQL equivalence claims for ORMs.
- No runtime traffic or production usage claims.
- No LLM, embedding, vector DB, or prompt-based classification.
- No raw SQL text or private path/value rendering.

## Input Model

Recommended file shape:

```json
{
  "version": "sql-schema-delta.v1",
  "source": {
    "name": "release-2026-06-14",
    "kind": "migration-review",
    "commitSha": "optional"
  },
  "changes": [
    {
      "id": "change-001",
      "kind": "column",
      "changeType": "type_changed",
      "reference": {
        "tableName": "orders",
        "columnName": "status"
      },
      "old": {
        "type": "int"
      },
      "new": {
        "type": "varchar"
      }
    }
  ]
}
```

Allowed `kind` values:

| Kind | Purpose |
| --- | --- |
| `schema` | Schema or database namespace-level change |
| `table` | Table-level change |
| `column` | Column-level change within a table or mapping |
| `query-shape` | Query-shape or SQL-text identity change |
| `sql-file` | SQL resource/file-level change |
| `mapping` | ORM/declarative mapping change |
| `persistence-surface` | Combined `sql-persistence` surface selector |

Allowed `changeType` values:

```text
added removed renamed type_changed nullable_changed constraint_changed
index_changed behavior_changed shape_changed unknown_changed
```

Unknown enum values fail closed in v1. A future compatibility mode can preserve unknown changes as gaps, but it must label them and keep them non-actionable.

`sql-schema-delta.v1` is not a valid `contract-delta-v2` file, and `contract-delta-v2` is not a valid SQL schema delta file. SQL/schema input is normalized internally to contract-delta engine selectors; it is not parsed as a general contract delta.

## SQL-To-Contract Selector Normalization

The SQL/schema input adapter should normalize each SQL kind into the closest contract-delta v2 selector shape before dispatching to shared impact logic:

| SQL/schema kind | Contract-delta v2 kind | Selector fields |
| --- | --- | --- |
| `schema` | `schema` | `schemaName`, `databaseNameHash`, `sourceKind`, `surfaceKind` |
| `table` | `sql-table` | `schemaName`, `tableName`, `tableNames`, `sourceKind`, `surfaceKind` |
| `column` | `sql-column` | `schemaName`, `tableName`, `columnName`, `columnNames`, `mappedName`, `containingType`, `propertyName` |
| `query-shape` | `sql-query` | `queryShapeHash`, `textHash`, `operationName`, `tableName`, `tableNames`, `columnNames`, `sqlSourceKind`, `sourceSymbol` |
| `sql-file` | `sql-query` | `sqlResourceName`, `sqlSourceKind`, `textHash`, `queryShapeHash`; unsafe file paths are matching hints only |
| `mapping` | `dependency-surface` | `surfaceKind=sql-persistence`, `tableName`, `columnName`, `mappedName`, `containingType` |
| `persistence-surface` | `dependency-surface` | `surfaceKind=sql-persistence`, stable surface metadata, source label when provided |

Normalization does not make the input file a contract-delta v2 document. It is an internal adapter step with its own validation gaps and report labels.

## Identity

Input `id` is required and is the stable user-facing change key. Finding IDs should be deterministic and derived from safe stable pieces:

```text
sql-schema-impact:v1
| changeId
| sourceLabelOrSingleIndex
| evidenceCategory
| classification
| stableEvidenceKey
```

`stableEvidenceKey` may include normalized safe schema/table/column names, shape hashes, text hashes, safe surface keys, or stable supporting IDs. It must not include row order, local absolute paths, raw URLs, raw SQL, display labels that can vary by environment, or volatile combined fact IDs unless those IDs are already stable for identical inputs and options.

Volatile identity caveats do not waive byte-stability. Given identical inputs and options, the same volatile caveat and same supporting IDs must render in deterministic order.

For `sql-persistence-mapping` evidence where no shape or text hash is available, `stableEvidenceKey` should use normalized safe table name, normalized safe column name, and normalized safe mapped name. If none of those pass the safe identifier policy, the finding must be marked `VolatileIdentity`, remain review-tier, and use a deterministic SHA-256 fallback truncated to 32 lowercase hex characters without rendering the unsafe value.

## Reference Matching

### Schema

Schema changes match:

- safe `schemaName` metadata on SQL-shape facts
- safe schema metadata on SQL resource facts
- safe schema metadata on `DatabaseColumnMapping` and ORM mapping facts
- combined `sql-query` or `sql-persistence` schema metadata when present

Schema-only matches can be high fan-out. They must be review-tier unless paired with a more stable table, column, shape, mapping, or path/reverse selector.

### Table

Table changes match:

- `QueryPatternDetected.tableName`
- semicolon-delimited `QueryPatternDetected.tableNames`
- combined `sql-query` surface `TableName`
- `DatabaseColumnMapping.tableName`
- combined `sql-persistence` surface `TableName`
- SQL file/resource facts when safe table metadata exists

Table-only matches are useful but can be high fan-out. Without matching column, shape hash, text hash, or path/reverse context, table-only findings should remain review-tier in combined mode and no stronger than probable/review-tier in single-index mode.

### Column

Column changes match:

- `QueryPatternDetected.columnNames`
- `QueryPatternDetected.fieldNames`
- `DatabaseColumnMapping.columnName`
- `DatabaseColumnMapping.mappedName`
- language property/member facts when they are tied to a database mapping
- combined `sql-query` and `sql-persistence` column metadata

Column-only matches without a table are ambiguous and should be review-tier unless another stable identity is present. `mappedName` without table and column anchors is also review-tier because it can represent language-side naming rather than a proven database column.

### Query Shape

Query-shape changes match:

- `queryShapeHash`
- `textHash`
- `operationName`
- `sqlSourceKind`
- safe table/column metadata
- combined `sql-query` `ShapeHash` and `TextHash`

`queryShapeHash` is stronger than table-only evidence because it represents a deterministic normalized SQL text shape. `textHash` is hash-only text evidence and must carry `HashOnlyEvidence` unless shape metadata also matches.

### Mapping And Persistence

Mapping changes match:

- `DatabaseColumnMapping`
- ORM model/declarative facts when they reference table/column metadata
- combined `sql-persistence` surfaces
- symbols or members tied to mapping facts

Mapping evidence is not query execution evidence. Reports must say this directly.

### SQL File

SQL file changes match:

- `SqlFileDeclared`
- `SqlTextUsed` with `sqlSourceKind=sql-file`
- SQL-shape `QueryPatternDetected` with `sqlSourceKind=sql-file`
- combined `sql-query` surfaces with source kind `sql-file`

File/resource evidence does not prove that the SQL file is executed.

## Evidence Categories

| Category | Evidence | Meaning | Caveat |
| --- | --- | --- | --- |
| `sql-query-shape` | SQL-shape `QueryPatternDetected`, combined `sql-query` with shape hash | Static derived SQL query shape | Not dialect/runtime proof |
| `sql-text-hash` | `SqlTextUsed`, text hash-only combined surface | Static SQL text hash/length evidence | Hash-only review-tier |
| `sql-resource` | `SqlFileDeclared`, SQL resource facts | Static SQL resource inventory | Not execution proof |
| `sql-persistence-mapping` | `DatabaseColumnMapping`, ORM mapping facts, `sql-persistence` | Declarative persistence/schema mapping | Not query execution proof |
| `sql-reachability` | combined paths/reverse rows | Static graph reachability | Coverage/cap dependent |
| `sql-gap` | `AnalysisGap`, identity/schema/truncation gaps | Missing or reduced evidence | Unknown/review-tier |

## Single-Index Flow

Single-index mode should use existing fact tables first:

1. Read `scan_manifest` and coverage.
2. Normalize delta references into safe selectors.
3. Match facts by fact type and safe metadata.
4. Attach symbol/member evidence where facts expose symbol roles.
5. Deduplicate by fact ID.
6. Classify by evidence tier, adapter capabilities, and coverage.
7. Emit Markdown/JSON.

`DefiniteImpact` is only available when a language adapter provides direct Tier1 semantic evidence tying the changed SQL mapping or query boundary to a symbol/member/method. Current cross-language expectations should assume many adapters emit Tier2/Tier3 SQL evidence and therefore classify as probable or review-tier.

Single-index mode should not run combined path/reverse traversal in v1. If a future single-index graph exists, it should be specified separately.

## Combined-Index Flow

Combined mode should use existing combined readers:

1. Read combined source provenance and coverage.
2. Project dependency surfaces using the same logic as `tracemap report`, `paths`, `reverse`, `diff`, and `impact`.
3. Match SQL query changes against `sql-query` surfaces.
4. Match mapping/persistence changes against `sql-persistence` surfaces.
5. Preserve source labels and source identity caveats.
6. Optionally request bounded path/reverse context from existing engines.
7. Classify by evidence strength, identity, coverage, and context.

The implementation should not create an alternate SQL surface projection. If a needed field is missing from existing surface readers, extend the shared reader in an implementation PR and update tests.

Optional combined precision data includes source coverage/provenance tables, dependency edges, symbol relationships, combined SQL surface metadata, path tables, and reverse-query support tables where present. Missing precision data should emit schema or coverage gaps rather than silently lowering evidence quality.

## Classifications

Single-index output may use v1-compatible reducer classes:

| Classification | Confidence | Use |
| --- | --- | --- |
| `DefiniteImpact` | `High` | Direct Tier1 semantic evidence tied to changed mapping/query boundary |
| `ProbableImpact` | `Medium` | Strong Tier2 SQL-shape, ORM mapping, endpoint, or framework evidence |
| `NeedsReview` | `Low` | Tier3, schema-only, name-only, table-only, mappedName-only, hash-only, ambiguous, or high fan-out evidence |
| `NoEvidenceFullCoverage` | `Low` | No match with credible full semantic coverage |
| `NoEvidenceReducedCoverage` | `Low` | No match with reduced coverage |
| `TruncatedByLimit` | `Low` | Output/evidence capped |
| `UnknownAnalysisGap` | `Low` | Gaps prevent a credible conclusion |

Combined output should align with combined impact:

| Classification | Confidence | Use |
| --- | --- | --- |
| `StaticImpactEvidence` | `High` | Strong static surface identity plus credible requested reachability context |
| `ProbableStaticImpact` | `Medium` | Strong SQL-shape/mapping evidence without requested context |
| `NeedsReviewImpact` | `Low` | Hash-only, schema-only, table-only, Tier3, duplicate/volatile identity, or ambiguous evidence |
| `NoImpactEvidence` | `Low` | Report-level no-evidence under credible coverage |
| `SelectorNoMatch` | `Low` | Query selector matched nothing |
| `PathContextUnavailable` | `Low` | Forward path context requested but not seedable |
| `ReverseContextUnavailable` | `Low` | Reverse context requested but not seedable |
| `TruncatedByLimit` | `Low` | Capped output or traversal |
| `UnknownAnalysisGap` | `Low` | Reduced coverage or gaps prevent conclusion |

Confidence is a deterministic mapping from classification, not a model-derived score. The emitted vocabulary should use the `High`/`Medium`/`Low` confidence values from contract-delta impact v2 rather than numeric values.

`DefiniteImpact`, `ProbableImpact`, `NeedsReview`, `NoEvidenceFullCoverage`, and `NoEvidenceReducedCoverage` are single-index classifications. Combined mode should use `StaticImpactEvidence`, `ProbableStaticImpact`, `NeedsReviewImpact`, `NoImpactEvidence`, and the combined gap classifications.

## Path And Reverse Context

Path/reverse context is opt-in.

Selector derivation order:

1. `queryShapeHash` for `sql-query`
2. `textHash` for hash-only `sql-query`, review-tier only
3. safe `tableName` + optional `columnName`
4. `sql-persistence` stable table/column/mapping metadata

Do not seed traversal from raw SQL, unsafe text, local paths, connection strings, or source snippets. Do not seed traversal from table-only or column-only high fan-out matches unless the implementation can cap and label the result as review-tier.

When path/reverse context is requested but cannot be derived, emit a gap rather than silently omitting the context.

When path or reverse context is requested against a single-language index, reject the option with the same clear message used by contract-delta impact v2. SQL/schema mode does not add single-index graph traversal in v1.

Path and reverse context are evaluated per finding. If only some findings can derive stable selectors, emit per-finding `PathContextUnavailable` or `ReverseContextUnavailable` gaps for the others and mark the summary context coverage partial.

## Report Shape

Markdown sections:

1. Summary
2. Query
3. Index Sources
4. Schema Changes
5. Findings
6. Path Context
7. Reverse Context
8. Gaps
9. Limitations

JSON top-level shape:

```json
{
  "reportType": "SqlSchemaChangeImpactSingleV1",
  "version": "1",
  "mode": "single",
  "input": {},
  "query": {},
  "index": {},
  "summary": {},
  "findings": [],
  "gaps": [],
  "coverageWarnings": [],
  "limitations": []
}
```

SQL/schema JSON uses `SqlSchemaChangeImpactSingleV1` for single-index reports and `SqlSchemaChangeImpactCombinedV1` for combined-index reports. `version` is the JSON schema version for the chosen report type, while `mode` is `single` or `combined`. This is a net-new JSON report model for SQL/schema mode. It does not retrofit the existing Markdown-only `ImpactReport` record used by the current reducer. Implementations should introduce the JSON model alongside existing reducer output and keep existing `--contract-delta` output behavior byte-stable.

Safe display metadata may include:

- operation name
- schema name
- table name
- column name
- mapped name
- source kind
- surface kind
- query shape hash
- text hash
- source label
- safe symbol display name
- database name hash

Unsafe metadata must be omitted or hashed.

## Rules

V1 should reuse existing rule IDs:

| Rule ID | Purpose |
| --- | --- |
| `contract.delta.input.v2` | SQL/schema delta input validation gaps |
| `contract.delta.impact.v2` | SQL/schema finding classification and evidence rows |
| `contract.delta.context.v2` | Path/reverse context unavailable or traversal gaps |
| Existing truncation rule used by impact reports | SQL/schema impact caps |

Supporting fact rows must preserve original adapter rule IDs such as `database.sql.shape.v1`, `database.sql.text.v1`, `python.integration.sql.v1`, `typescript.integration.sql.v1`, `jvm.integration.sql.v1`, ORM mapping rules, and `DatabaseColumnMapping` producer rules. Combined context must preserve `combined.paths.*`, `combined.reverse.*`, `combined.impact.*`, and `combined.diff.*` rule IDs where reused.

If the reused contract-delta v2 rules are not present in `rules/rule-catalog.yml` when implementation starts, the first implementation PR must add `contract.delta.input.v2`, `contract.delta.impact.v2`, and `contract.delta.context.v2` with documented limitations before emitting reports that cite them. No finding or gap may cite a missing rule. If implementation introduces `sql.schema.*` rules for precision, those rules must also be documented in `rules/rule-catalog.yml` with limitations before the implementation merges. A spec-only PR does not add those rules.

## Safety

The report must not render:

- raw SQL text
- SQL predicate literal values
- source snippets
- connection strings
- config values
- raw URLs
- local absolute paths
- private repository paths

Identifiers are renderable only if they pass the existing safe identifier policy from SQL reporting specs. Hash values should be lowercase SHA-256 truncations with documented lengths.

Raw database names are never rendered. If database identity is needed for matching, use `databaseNameHash` as SHA-256 truncated to 16 lowercase hex characters.

Unsafe reference handling:

1. Safe fields are normalized and matched.
2. Unsafe fields are omitted from output.
3. If a change has no safe selector after unsafe fields are removed, emit a documented input gap and do not create findings for that change.
4. Unknown enum values fail strict validation or become non-actionable input gaps.

## Review Boundaries

Implementation should land in small slices:

1. Input model, validation, SQL-to-contract selector normalization, and report skeleton.
2. Single-index matching.
3. Combined `sql-query` and `sql-persistence` matching.
4. Optional path/reverse context.
5. Report polish, byte-stability, and sample validation.

The first implementation PR should avoid path/reverse traversal if it threatens reviewability; the input adapter and static SQL evidence matching are already valuable.
