# TraceMap Acceptance Plan

## Purpose

This plan defines how TraceMap proves that scanner and reducer behavior is deterministic, evidence-backed, and honest about coverage.

Concrete sample fixtures, pinned public open-source smoke repositories, and repeatable smoke commands are documented in [Validation guide](VALIDATION.md).

## Required Local Verification

Run before finishing implementation work:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
cd src/typescript
npm run check
cd ../..
JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm test
python3 -m venv /tmp/tracemap-python-venv
/tmp/tracemap-python-venv/bin/python -m pip install -e "src/python[dev]"
/tmp/tracemap-python-venv/bin/python -m pytest src/python/tests
```

Expected result:

- build succeeds with zero errors.
- tests pass.

## Core Artifact Acceptance

For every successful `tracemap scan --repo <repo> --out <out>` run, verify:

- `<out>/scan-manifest.json` exists.
- `<out>/facts.ndjson` exists.
- `<out>/index.sqlite` exists.
- `<out>/report.md` exists.
- `<out>/logs/analyzer.log` exists.
- manifest includes repo name, commit SHA, scanner version, analysis level, and build status.
- facts include rule IDs, evidence tiers, file paths, line spans, commit SHA, and extractor versions.
- `index.sqlite` includes a `call_edges` table when call-edge facts are emitted.
- `index.sqlite` includes an `object_creations` table when object-creation facts are emitted.
- `index.sqlite` includes an `argument_flows` table when argument-flow facts are emitted.
- `index.sqlite` includes a `local_aliases` table when local-alias facts are emitted.
- `index.sqlite` includes a `field_aliases` table when field-alias facts are emitted.
- `index.sqlite` includes a `parameter_forward_edges` table derived from parameter-to-parameter argument-flow facts.
- `parameter_forward_edges` includes direct parameter forwarding, same-method local alias forwarding up to 3 alias hops, and unique constructor field-origin forwarding when evidence exists.
- `parameter_forward_edges` omits ambiguous constructor field origins and alias chains beyond the documented bound instead of inventing a flow path.
- .NET scans that encounter DBML, EDMX, typed DataSet XSD/TableAdapter, data-provider config, or generated legacy data designers emit `LegacyData*` facts or explicit `AnalysisGap` facts with rule IDs and evidence tiers.
- DBML, EDMX, typed DataSet, and TableAdapter descriptor facts include additive normalized model identity metadata such as `metadataFormat`, `modelKind`, `descriptorRole`, and `stableModelKey` while preserving source rule IDs and Tier2 descriptor ceilings.
- DBML associations, EDMX associations, unambiguous EDMX MSL association-set mappings, typed DataSet relations, and resolvable typed DataSet key/keyref constraints emit `LegacyDataMappingDeclared` relationship metadata such as `modelRelationshipKind`, endpoint names or hashes, endpoint coverage, supporting IDs, and limitations without claiming runtime database access.
- Unsupported inherited EDMX model shapes emit an `UnsupportedLegacyOrmMappingShape` gap rather than invented relationship evidence.
- Checked-in NHibernate `.hbm.xml` mapping files emit safe static descriptor facts under `legacy.data.orm.nhibernate.v1` for deterministic class, table, property/id/version, and relationship/collection shapes; unsupported mapping/query shapes emit gaps and raw SQL/formula/filter/query text is not stored.
- Recognized unsupported old ORM descriptors such as LLBLGen, SubSonic, iBATIS.NET/MyBatis.NET, and Castle ActiveRecord emit `AnalysisGap` facts under `legacy.data.orm.unsupported.v1`; they do not emit invented entity, table, column, relationship, generated-code, runtime database, or query-execution evidence.
- Legacy data metadata rows appear in `facts.ndjson`, `index.sqlite`, and the scan report as static design-time metadata evidence, not runtime data access, SQL execution, provider compatibility, database existence, or production usage.
- Legacy data metadata facts and reports do not include raw SQL, connection strings, config values, server/catalog names, URLs, local absolute paths, raw remotes, secrets, source snippets, or private sample identities.
- Unrelated `.xsd` files without typed DataSet/TableAdapter indicators do not become legacy data descriptor facts.

For every successful `tracemap-ts scan --repo <repo> --out <out>` run, verify:

- the same required artifacts are written.
- scans outside a Git checkout with a known commit SHA fail before artifacts are written.
- `scan-manifest.json` uses `Level1SemanticAnalysis` and `buildStatus: "Succeeded"` only when every selected TypeScript project loads semantically with no known gaps and a known commit SHA.
- reduced TypeScript scans use `Level1SemanticAnalysisReduced` or `Level3SyntaxAnalysis`.
- reducer-compatible facts reuse existing fact type strings and matching keys such as `propertyName`, `methodName`, `typeName`, `keyPath`, `name`, `containingType`, and `targetSymbol`.
- TypeScript facts store hashes/spans for source values, not raw source snippets.

For every successful `tracemap-jvm scan --repo <repo> --out <out>` run, verify:

- the same required artifacts are written.
- scans outside a Git checkout with a known commit SHA fail before artifacts are written.
- `scan-manifest.json` uses `Level1SemanticAnalysis` and `buildStatus: "Succeeded"` only when selected Java semantic scopes produce compiler evidence with no known gaps and a known commit SHA.
- Java/Kotlin reduced scans use `Level1SemanticAnalysisReduced` or `Level3SyntaxAnalysis`.
- Kotlin-only MVP scans are `Level3SyntaxAnalysis` because Kotlin semantic extraction is not implemented.
- reducer-compatible facts reuse existing fact type strings and matching keys such as `propertyName`, `methodName`, `typeName`, `keyPath`, `name`, `containingType`, and `targetSymbol`.
- JVM descriptor, group ID, artifact ID, and module metadata are stored as review/export properties and not as reducer-matching names.
- JVM facts store hashes/spans for source values, not raw source snippets.

For every successful `tracemap-py scan --repo <repo> --out <out>` run, verify:

- the same required artifacts are written.
- scans outside a Git checkout with a known commit SHA fail before artifacts are written.
- `scan-manifest.json` uses `Level1SemanticAnalysisReduced` for Python MVP AST/package/config/SQL scans with Python source files.
- scans with only non-Python evidence use `Level3SyntaxAnalysis` or reduced coverage labels, never clean semantic coverage.
- reducer-compatible facts reuse existing fact type strings and matching keys such as `fieldName`, `memberName`, `methodName`, `typeName`, `keyPath`, `name`, `containingType`, and `targetSymbol`.
- Python facts store hashes/spans for source values, not raw source snippets.
- Python scanner does not import user code, execute setup.py, run a type checker, or install project dependencies during scan.

## Reducer Acceptance

For every successful `tracemap reduce --index <index> --contract-delta <delta> --out <report>` run, verify:

- impact report exists.
- every legacy finding includes the reducer rule ID `contract.delta.reduce.v1`, and every v2 finding or gap includes `contract.delta.input.v2`, `contract.delta.impact.v2`, or `contract.delta.context.v2`.
- matched findings include evidence rows.
- no-match findings include manifest coverage evidence.
- reduced coverage never produces `NoEvidenceFullCoverage`.
- v2 directory output writes deterministic `impact-report.md` and `impact-report.json`.
- v2 reports do not render raw SQL, literal values, connection strings, URLs, local absolute paths, or source snippets.
- v2 single-index reduce rejects `--include-paths` and `--include-reverse`; those context flags require a combined index and must emit explicit unavailable gaps when selectors cannot be derived safely.
- v2 combined-index reduce preserves source labels, commit SHA, language, analysis level, build status, and repository identity hashes rather than raw repository URLs.

## Export Acceptance

For every successful `tracemap export --index <index> --out <out> --format json` run, verify:

- export JSON exists.
- export JSON includes scan manifest metadata, fact counts by type/tier/rule, relationship rows, call-edge rows, and object-creation rows when those tables exist.
- export JSON does not include raw source snippets.
- rows are ordered deterministically.

For every successful `tracemap export --index <index> --out <out> --format mermaid` run, verify:

- Mermaid output exists.
- output starts with `flowchart TD`.
- relationship edges use direct `symbol_relationships` evidence.
- call edges use indexed call-edge facts and are bounded.

For TypeScript indexes, `tracemap-ts export --index <index> --out <out> --format <json|mermaid>` should produce equivalent export shapes from the same SQLite tables.

For JVM indexes, existing `.NET` `tracemap export` should read the same SQLite tables and include relationship/call/object rows when emitted.

## Combine Acceptance

For every successful `tracemap combine --index <index> [--index <index>] --out <combined.sqlite>` run, verify:

- combined SQLite output exists.
- `index_sources` contains one row per imported index.
- source labels are unique and deterministic; explicit `--label` values are preserved.
- `index_sources` stores source index path hash, scan ID, repo identity, scan root metadata, language when inferred, scanner version, analysis level, build status, and commit SHA.
- `combined_facts` contains every imported source fact.
- `combined_facts.original_fact_id` and `combined_facts.original_scan_id` preserve the source index identity.
- `combined_facts.combined_fact_id` namespaces facts by source index so equal source fact IDs do not collide.
- combined symbol, relationship, call-edge, object-creation, argument-flow, alias, and parameter-forwarding tables are populated when those source tables exist.
- `combined_dependency_edges` view exposes calls, object creations, relationships, and parameter-forwarding edges with source labels and evidence spans.
- the empty `endpoint_matches` table exists as a placeholder for future derived cross-index rows.
- raw source snippets are not added by combine.
- JVM indexes can be combined with .NET and TypeScript indexes without rewriting source fact IDs.

For every successful `tracemap export --index <combined.sqlite> --out <out> --format <json|mermaid>` run, verify:

- export JSON includes `sources`, `factsByType`, `factsBySourceAndType`, `relationships`, `callEdges`, and `objectCreations`.
- source labels and source index IDs are included on combined dependency rows.
- Mermaid output starts with `flowchart TD` and groups dependency rows by source label.
- export output does not include raw source snippets.

For every successful `tracemap report --index <combined.sqlite> --out <out>` run, verify:

- report rejects single-language indexes with a clear combined-index error.
- directory output writes `dependency-report.md` and `dependency-report.json`.
- source inventory includes labels, language, scan root, commit SHA, analysis level, and build status without local absolute paths.
- reduced coverage and known gaps are labeled as coverage-relative.
- endpoint findings distinguish two-sided pairwise comparisons from one-sided global inventory rows.
- endpoint JSON rows include side-specific scan IDs, commit SHAs, rule IDs, evidence tiers, file spans, and fact IDs.
- HTTP, SQL query/persistence, package/config, and dependency-edge surfaces preserve source labels and evidence spans.
- SQL and dynamic URL rows do not display raw SQL text, raw URLs, source snippets, or local absolute paths.
- `endpoint_matches` is not mutated by report generation.

For every successful `tracemap portfolio --out <out>` run, verify:

- inputs are single-language indexes, combined indexes, or a manifest that references them, and all input indexes are opened read-only.
- directory or extensionless output writes `portfolio-report.md` and `portfolio-report.json`.
- JSON includes `reportType: multi-index-portfolio-report`, required empty arrays, stable query metadata, source snapshots, source coverage, endpoint alignment, dependency surfaces, dependency edges, shared surfaces, optional context sections, portfolio diff/impact sections, gaps, and limitations.
- source rows preserve source labels, combined container labels when applicable, commit SHA, scanner/extractor version, analysis level, build status, coverage status, and repository identity hashes without local absolute paths.
- single-snapshot reports compute endpoint alignment and shared surfaces only from deterministic static evidence and never imply runtime topology, ownership, traffic, deployment, package compatibility, vulnerability, or release approval.
- before/after manifest comparison pairs sources by manifest label plus source identity, emits source changes, and projects safe surface/edge changes into `PortfolioDiffRow` rows with `portfolio.diff.v1`.
- projected surface/edge diff rows use stable safe identities, carry rule IDs, evidence tiers, supporting fact or edge IDs where available, and downgrade to review/partial classifications for Tier3, hash-only, ambiguous, duplicate, or reduced-coverage evidence.
- optional impact, path, reverse, and release-review context is `not_requested`, `unavailable`, or `deferred` until the compatible composition workflows are implemented.
- caps such as `--max-sources`, `--max-surface-rows`, `--max-endpoint-findings`, `--max-shared-surfaces`, `--max-edge-rows`, `--max-diff-rows`, and `--max-gaps` apply deterministically and emit truncation gaps when rows are omitted.
- Markdown and JSON do not include raw SQL text, raw URLs, config values, source snippets, connection strings, secret-looking values, repository remotes, or local absolute paths.

For every successful `tracemap package-impact --index <index.sqlite> --package-delta <delta.json> --out <out>` run, verify:

- the input index is opened read-only and may be either a single-language TraceMap index or a combined index.
- directory or extensionless output writes `package-impact-report.md` and `package-impact-report.json`.
- JSON includes version, report coverage, package delta summary, source snapshots, findings, gaps, and limitations without generated timestamps.
- package-delta input uses `version: package-delta.v1`, optional source provenance, and a non-empty `changes` array with stable change IDs and package names.
- package matching is exact case-insensitive package-name matching, with optional exact case-insensitive ecosystem matching.
- findings carry `package.upgrade.impact.v1`, the original package extractor rule ID, evidence tier, source label, scan ID, commit SHA, file span, fact IDs, and safe package metadata.
- source provenance and repository remotes are hashed or omitted in package-impact JSON; raw remotes are not serialized through reused report source DTOs.
- unsafe version strings, raw URLs, local paths, source snippets, config values, connection strings, and secret-looking values are not rendered.
- no-match package changes under reduced coverage emit `UnknownAnalysisGap`; they are not reported as clean absence.
- no-match package changes under full coverage emit `NoStaticPackageEvidence`; the absence of package rows alone does not downgrade report coverage.
- config-key surfaces must not be matched as package upgrade findings.
- package impact reports do not claim compatibility, transitive dependency resolution, runtime loading, vulnerabilities, licenses, deployment, release approval, or production usage.
- caps such as `--max-findings` and `--max-gaps` apply deterministically and mark truncated output.

For every successful `tracemap paths --index <combined.sqlite> --out <out>` run, verify:

- path reports reject single-language indexes with a clear combined-index error.
- directory output writes `paths-report.md` and `paths-report.json`.
- the command opens the combined database read-only and does not mutate `endpoint_matches` or source evidence tables.
- no-selector mode starts from in-memory endpoint matches and searches to terminal dependency surfaces.
- `--from-endpoint`, `--from-symbol`, `--from-source`, `--to-surface`, `--surface-name`, `--source-pair`, `--max-depth`, `--max-paths`, and `--max-frontier` behave deterministically.
- terminal surfaces are limited to `sql-query`, `sql-persistence`, `http-route`, `http-client`, and `package-config`.
- `DatabaseColumnMapping` terminal surfaces are `sql-persistence` evidence and do not claim query execution.
- path rows include source labels, scan IDs, commit SHAs, rule IDs, evidence tiers, file spans, node IDs, edge IDs, and supporting fact or edge IDs where available.
- classifications use `StrongStaticPath`, `ProbableStaticPath`, `NeedsReviewPath`, `UnknownAnalysisGap`, `NoPathFound`, and `SelectorNoMatch`.
- no-path conclusions are coverage-relative when contributing sources have reduced coverage.
- Markdown and JSON do not include raw SQL text, raw URLs, config values, source snippets, or local absolute paths.

For every successful `tracemap property-flow --index <combined.sqlite> --property <selector> --out <out>` run, verify:

- property-flow rejects single-language indexes and empty/invalid combined indexes with a clear combined-index error.
- `--property` accepts only `field:`, `control:`, `binding:`, `model:`, `dto:`, `symbol:`, and `fact:` selectors, and rejects unsafe local paths, raw URLs, snippets, and secret-like values with sanitized diagnostics.
- directory or extensionless output writes `property-flow-report.md` and `property-flow-report.json`.
- explicit `.md` or `.json` output writes only the compatible selected format.
- the input combined database is opened read-only and source evidence tables are not mutated.
- selected roots include root kind, source label, source index ID, scan ID, commit SHA, combined fact ID, symbol ID where available, rule ID, evidence tier, file path, line span, extractor ID/version, safe display metadata, supporting fact IDs, and limitations.
- selected roots preserve source rule IDs such as `typescript.angular.template-binding.v1`, `typescript.angular.form-binding.v1`, `typescript.angular.event-binding.v1`, `csharp.razor.binding.v1`, and `csharp.razor.form-target.v1`; derived classifications also cite `property-flow.*.v1` gap/path/root rules as applicable.
- `--source` filters source labels by deterministic case-insensitive exact match, and `--framework angular|razor|any` constrains UI roots.
- generic selectors such as `field:status` are allowed but no stronger than `NeedsReviewLineage` unless narrowed by source/type/symbol/fact identity.
- ambiguous selector matches report deterministic top-N roots, total candidate count, and an `AmbiguousSelector` gap instead of choosing a hidden winner.
- selector misses emit `SelectorNoMatch`; reduced coverage emits `UnknownAnalysisGap` rather than proof of no lineage.
- missing optional combined precision tables emit `MissingOptionalSchema` gaps; missing route-flow schema emits `RouteFlowUnavailable` while preserving any available combined path evidence.
- lineage paths and edges include rule IDs, evidence tiers, source labels, file spans, supporting fact IDs, and supporting edge IDs where available.
- Markdown renders a compact `Static terminal context:` cue only when structured path-node `terminalContextKind` metadata is present, keeps it path-scoped, and treats absence as unknown rather than proof that no terminal surface exists.
- classifications are limited to `StrongStaticLineage`, `ProbableStaticLineage`, `NeedsReviewLineage`, `UnknownAnalysisGap`, `NoLineageEvidence`, `SelectorNoMatch`, and `TruncatedByLimit`, with confidence derived deterministically from classification.
- Markdown sections appear in this order: Summary, Query, Sources and Coverage, Selected Roots, Lineage Paths, Gaps, Evidence Inventory, Optional Observed Evidence, Limitations.
- JSON includes `reportType: property-flow`, `version: 1.0`, `reportCoverage`, `coverageWarnings`, `query`, `snapshot`, `summary`, `sources`, `selectedRoots`, `lineagePaths`, `gaps`, `inventory`, `observedEvidence`, and `limitations`.
- reports do not include raw SQL, raw source snippets, raw remotes, local absolute paths, raw URLs, connection strings, secrets, credentials, private data, or unsafe literal values.
- `--observed-evidence <path>`, when supplied, reads a JSON file, accepts only safe demo metadata rows, rejects unsafe keys/values with sanitized diagnostics, labels rows as `ObservedDemoContext`, and cannot upgrade static classifications.

For every successful `tracemap reverse --index <combined.sqlite> --out <out>` run, verify:

- reverse reports reject single-language indexes with a clear combined-index error.
- directory output writes `reverse-report.md` and `reverse-report.json`.
- the command opens the combined database read-only and does not mutate `endpoint_matches` or source evidence tables.
- JSON includes `reportType: combined-reverse-query`, required empty arrays, stable query metadata, source snapshots, selected surfaces, reverse roots, paths, gaps, and limitations.
- selected surfaces are limited to dependency surfaces such as `sql-query`, `sql-persistence`, `http-route`, `http-client`, and `package-config`.
- `--source` matches source labels case-insensitively, `--surface-name` uses exact case-insensitive matching, and `--surface`, `--to`, `--max-depth`, `--max-frontier`, `--max-surfaces`, `--max-roots`, `--max-paths-per-root`, and `--max-gaps` behave deterministically.
- `--to endpoints`, `--to symbols`, `--to sources`, and `--to all` select the requested reverse root families without implying runtime reachability.
- selected surfaces, reverse roots, paths, and gaps carry rule IDs, evidence tiers, source labels, stable keys, file spans, and supporting fact or edge IDs where available.
- classifications use `SelectedSurfaceEvidence`, `NeedsReviewSurfaceEvidence`, `StrongStaticReversePath`, `ProbableStaticReversePath`, `NeedsReviewReversePath`, `UnknownAnalysisGap`, `NoReversePathEvidence`, `SelectorNoMatch`, and `TruncatedByLimit`.
- no-reverse-path conclusions are coverage-relative; reduced coverage emits an `UnknownAnalysisGap` instead of proof that no roots exist.
- `--exit-code` returns a non-zero exit only when requested and reverse roots or paths are present.
- Markdown and JSON do not include raw SQL text, raw URLs, config values, source snippets, connection strings, repository remotes, or local absolute paths.

For every successful `tracemap diff --before <before.sqlite> --after <after.sqlite> --out <out>` run, verify:

- both inputs are combined indexes and are opened read-only.
- directory output writes `diff-report.md` and `diff-report.json`, even when `--format json` is supplied.
- JSON includes `reportType: combined-dependency-diff`, required empty arrays, stable query metadata, source snapshots, diff rows, gaps, and limitations.
- source labels are paired exactly, and known source identity conflicts fail by default unless `--allow-identity-mismatch` is supplied.
- source-only metadata changes do not create endpoint, surface, edge, or path churn.
- endpoint, surface, and edge rows use stable evidence keys, not volatile SQLite row IDs.
- `--scope paths` requires `--include-paths`; path diffing is otherwise explicitly reported as not requested.
- `--endpoint "<METHOD> <PATH_KEY>"`, `--source`, `--surface`, and `--surface-name` selectors behave deterministically.
- `--max-diff-rows` and `--max-gaps` cap output deterministically and emit truncation gaps.
- `--exit-code` returns a non-zero exit only when requested and diff rows are present.
- Markdown and JSON do not include raw SQL text, raw URLs, config values, source snippets, connection strings, or local absolute paths.

For every successful `tracemap snapshot-diff --before <before.sqlite> --after <after.sqlite> --out <out>` run, verify:

- both inputs are the same TraceMap index kind: single-language indexes or combined indexes. Mixed single/combined inputs fail clearly without writing output.
- inputs are opened read-only.
- directory or extensionless output writes `snapshot-diff-report.md` and `snapshot-diff-report.json`.
- JSON includes `reportType: snapshot-diff`, required empty arrays, stable query metadata, before/after snapshots, source diffs, coverage diffs, single-index gap diffs when `AnalysisGap` facts change, extractor-version diffs, gaps, and limitations.
- single-language indexes use the synthetic source label `single`; reports do not render raw repository URLs, raw repository names, raw local roots, or local absolute paths.
- conflicting source identity fails by default and can only continue with `--allow-identity-mismatch`, which emits rule-backed review/unknown gaps.
- reduced coverage and unknown commit SHAs produce gaps instead of clean history-dependent conclusions.
- combined-index endpoint, surface, graph, and opt-in path sections delegate to the combined diff engine and preserve combined rule IDs as supporting evidence.
- single-index endpoint, dependency-surface, and `AnalysisGap` fact changes are projected when present; single-index graph and contract-shape comparison emit explicit availability gaps until their projector slices are implemented.
- `--include-paths` requires combined indexes; `--scope paths` requires `--include-paths`.
- `--max-diff-rows` and `--max-gaps` cap output deterministically.
- `--exit-code` returns a non-zero exit only when requested and diff rows are present.
- Markdown and JSON do not include raw SQL text, raw URLs, config values, source snippets, connection strings, repository remotes, or local absolute paths.

For every successful `tracemap contract-diff --before <before.sqlite> --after <after.sqlite> --out <out>` run, verify:

- both inputs are the same TraceMap index kind: single-language indexes or combined indexes. Mixed single/combined inputs fail clearly without writing output.
- inputs are opened read-only.
- directory or extensionless output writes `contract-diff-report.md` and `contract-diff-report.json`.
- JSON includes `reportType`, `version`, `reportCoverage`, `coverageWarnings`, `query`, before/after snapshots, source pairs, endpoint diffs, DTO type diffs, DTO property diffs, method diffs, request/response diffs, route-shape diffs, gaps, and limitations.
- endpoint rows compare indexed static method/path/handler/route metadata and do not claim runtime traffic, auth, deployment, proxy, or reachability behavior.
- DTO rows compare only indexed type/member metadata and do not infer runtime serializer aliases, generated OpenAPI completeness, or binary compatibility.
- request/response rows are emitted only for explicit endpoint-to-DTO attachment evidence; otherwise `AttachmentEvidenceUnavailable` is a rule-backed gap.
- reduced coverage, unknown commit SHA, source identity conflict, duplicate identity, syntax-only evidence, and generic property-only identity downgrade rows.
- `--scope`, `--source`, `--endpoint`, `--type`, `--property`, `--change-kind`, `--max-diff-rows`, `--max-evidence-rows`, `--max-gaps`, and `--exit-code` behave deterministically.
- `--exit-code` returns a non-zero exit only when requested and `Added`, `Removed`, or `ChangedEvidence` rows exist.
- Markdown and JSON do not include raw SQL text, raw URLs, config values, source snippets, connection strings, repository remotes, or local absolute paths.

For every successful `tracemap impact --before <before.sqlite> --after <after.sqlite> --out <out>` run, verify:

- both inputs are combined indexes and are opened read-only through the shared diff pipeline.
- directory output writes `impact-report.md` and `impact-report.json`, even when `--format json` is supplied.
- JSON includes `reportType: combined-change-impact`, required empty arrays, stable query metadata, source snapshots, impact items, gaps, and limitations.
- every impact item carries an impact rule ID, the delegated diff rule ID, evidence tier, source label, stable key, file span when available, and supporting fact/edge IDs when available.
- `NoImpactEvidence` is emitted as a rule-backed gap when no comparable static impact items exist for the selected snapshots and scopes.
- default scope includes sources, coverage, endpoints, surfaces, and edges; path context is off by default.
- `coverage` scope maps to the delegated diff `sources` scope and filters the resulting report to coverage impact items.
- `--scope paths` requires `--include-paths`; path context runs bounded before/after path queries only for impact items with safe endpoint, surface, or edge selectors.
- source and coverage impact rows do not run path context unless a future rule defines safe mapping semantics.
- path-context classifications distinguish `ReachabilityChanged`, `ReachabilityEvidenceChanged`, `ReachabilityUnchanged`, `PathContextUnavailable`, `NoPathEvidence`, `UnknownAnalysisGap`, and `TruncatedByLimit`.
- reduced coverage or source identity uncertainty downgrades confidence rather than producing strong static impact claims.
- `--source`, `--endpoint`, `--surface`, `--surface-name`, `--max-impact-items`, `--max-paths-per-item`, `--max-path-queries`, `--max-depth`, `--max-frontier`, `--max-gaps`, and `--exit-code` behave deterministically.
- `--exit-code` returns a non-zero exit only when requested and impact items are present.
- Markdown and JSON do not include raw SQL text, raw URLs, config values, source snippets, connection strings, or local absolute paths.

For every successful `tracemap release-review --before <before.sqlite> --after <after.sqlite> --out <out>` run, verify:

- both inputs are valid TraceMap indexes and are opened read-only.
- both inputs are the same mode: single-language indexes use `ReleaseReviewSingleV1`, combined indexes use `ReleaseReviewCombinedV1`, and mixed mode is rejected without raw file paths.
- directory or extensionless output writes `release-review.md` and `release-review.json`, even when `--format json` is supplied.
- JSON includes `reportType: release-review`, required empty arrays, stable query metadata, before/after snapshots, summary, source coverage, all section objects, gaps, checklist items, and limitations.
- release rollups use `ActionableStaticEvidence`, `ReviewRecommended`, `NoActionableEvidence`, `PartialAnalysis`, `SelectorNoMatch`, `UnknownAnalysisGap`, or `TruncatedByLimit`.
- every release-level rollup, checklist item, section gap, selector gap, truncation gap, and source/coverage gap cites a `release.review.*` rule ID and evidence tier.
- contract delta context is included when `--contract-delta` is provided and the reducer workflow is available.
- API/DTO, SQL/schema, and package-upgrade workflows render explicit `unavailable` or `deferred` sections when they are not implemented or not requested; missing sections are never silently treated as clean evidence.
- `--include-paths` and `--include-reverse` are off by default; single-index mode renders requested path/reverse context as unavailable with a rule-backed gap.
- `--include-priority` is off by default; when omitted, release-review Markdown and JSON remain unscored and do not include `reviewPriority` or `reviewPriorityRows`.
- when `--include-priority` is supplied, Markdown includes a Review Priority section and JSON includes top-level `reviewPriority` plus sidecar `reviewPriorityRows` keyed by stable row IDs.
- review priority output uses model version `review-priority.v1`, closed-vocabulary `severityHint`, `attentionLevel`, and status values, and emits `priorityScore: null` plus `componentValue: null` because v1 is ordinal-only.
- every review-priority component cites a `review.priority.*` rule ID, evidence tier, source evidence or metadata, and at least one limitation.
- review priority preserves underlying release-review classifications and rule IDs, treats reduced coverage, identity, commit, schema, workflow, selector, and truncation limits as visible components, and does not claim release approval, runtime risk, production impact, vulnerability, compliance, business criticality, or AI analysis.
- checklist items are derived only from findings and gaps and do not include release approval, readiness, or runtime-risk language.
- caps such as `--max-findings`, `--max-surface-rows`, `--max-paths`, `--max-gaps`, and `--max-checklist-items` apply deterministically and emit truncation gaps when rows are omitted.
- Markdown and JSON do not include raw SQL text, raw URLs, config values, source snippets, connection strings, repository remotes, or local absolute paths.

## Language Adapter Acceptance

New language adapters should satisfy [Language adapter contract](LANGUAGE_ADAPTER_CONTRACT.md) before language-specific depth is considered complete.

Minimum checks:

- required artifacts are written.
- scan manifest includes commit SHA, scanner version, analysis/build status, and scan-root metadata when available.
- facts include rule IDs, evidence tiers, file paths, line spans, commit SHA, and extractor versions.
- reducer-compatible facts reuse existing fact types and matching keys when possible.
- rule catalog entries document every new rule and limitation.
- reduced coverage is labeled whenever compiler/project/dependency gaps exist.
- no raw snippets or raw sensitive values are stored by default.
- legacy WCF metadata facts expose only safe basenames, hashes, service-reference folder labels, and operation identifiers; raw URLs, SOAP actions, schema locations, namespace URIs, local absolute paths, raw schemas, and snippets are hashed or omitted.
- WCF metadata-backed mappings and operation-name normalization remain static evidence and do not prove runtime reachability, deployment, service version compatibility, authorization, binding compatibility, or branch feasibility.
- legacy WebForms facts expose static page/control/event/handler/flow evidence with rule IDs, evidence tiers, supporting fact IDs, coverage labels, and limitations; they do not prove runtime page lifecycle execution, postbacks, event bubbling, service reachability, SQL execution, branch feasibility, deployment, or production usage.
- WebForms generated reports and validation summaries must not include raw source snippets, raw SQL, config values, raw URLs, local absolute paths, raw remotes, private sample identifiers, or secrets.
- legacy WinForms facts expose static form/control/component/resource/event/handler/navigation/callback/handler-flow evidence with rule IDs, evidence tiers, supporting fact IDs, coverage labels, and limitations; they do not prove runtime event firing, form visibility, user reachability, branch feasibility, auth/role outcome, scheduling, service reachability, SQL execution, database existence, deployment, or production usage.
- WinForms generated reports and validation summaries must not include raw source snippets, raw resource values, raw SQL, config values, raw URLs, endpoint addresses, hostnames, local absolute paths, raw remotes, private sample identifiers, or secrets.

## Endpoint Alignment Acceptance

For every successful `tracemap endpoints --client-index <client> --server-index <server> --out <out>` run, verify:

- Markdown and/or JSON output follows the requested output shape.
- output includes client and server scan IDs, commit SHAs, analysis levels, build statuses, labels, scan-root metadata, and index path hashes.
- every finding includes the derived rule ID `endpoint.alignment.v1`.
- matched rows preserve source fact IDs, rule IDs, evidence tiers, file paths, and line spans from both indexes.
- reduced client or server coverage appears in `coverageWarnings`.
- `ClientCallNoServerEndpoint` states it is not proof of a broken call.
- `ServerEndpointNoClientMatch` states it is not proof of dead code or an unused endpoint.
- dynamic client URLs are classified as `DynamicClientUrlNeedsReview` rather than guessed.

Endpoint alignment is static code evidence only. It does not prove runtime routing, middleware behavior, reverse proxies, auth policies, deployment base paths, CORS behavior, feature flags, or whether a route executes.

## Included Sample Repos

### `samples/modern-sample`

Purpose: prove the full semantic path.

Command:

```bash
tracemap scan --repo samples/modern-sample --out <tmp>/modern-sample
tracemap reduce --index <tmp>/modern-sample/index.sqlite --contract-delta samples/contract-deltas/modern-sample.customer-profile.json --out <tmp>/modern-impact.md
```

Expected:

- scan analysis level is `Level1SemanticAnalysis`.
- build status is `Succeeded`.
- `CustomerProfileResponse.primaryEmail` is `DefiniteImpact`.
- evidence includes a Tier1 `PropertyAccessed` fact.
- `CustomerProfileResponse.status` is `NoEvidenceFullCoverage` unless new sample code adds status evidence.

### `samples/broken-sample`

Purpose: prove fallback behavior.

Command:

```bash
tracemap scan --repo samples/broken-sample --out <tmp>/broken-sample
```

Expected:

- scan completes.
- analysis level is reduced or syntax-only.
- build status is not clean success.
- syntax facts are emitted for declarations and member names.
- `AnalysisGap` facts are emitted.

### `samples/typescript-modern-sample`

Purpose: prove the TypeScript full semantic path and reducer compatibility.

Command:

```bash
cd src/typescript
node dist/src/cli.js scan --repo ../../samples/typescript-modern-sample --out <tmp>/typescript-modern-sample
cd ../..
dotnet run --project src/dotnet/TraceMap.Cli -- reduce --index <tmp>/typescript-modern-sample/index.sqlite --contract-delta samples/contract-deltas/typescript-modern.status.json --out <tmp>/typescript-impact.md
```

Expected:

- scan analysis level is `Level1SemanticAnalysis`.
- build status is `Succeeded`.
- `CustomerContract.status` is `DefiniteImpact`.
- evidence includes a Tier1 `PropertyAccessed` fact.
- route, serializer, Zod, Prisma, and `process.env` facts are emitted as bounded integration evidence.

### `samples/typescript-broken-sample`

Purpose: prove TypeScript syntax fallback behavior.

Command:

```bash
cd src/typescript
node dist/src/cli.js scan --repo ../../samples/typescript-broken-sample --out <tmp>/typescript-broken-sample
```

Expected:

- scan completes.
- analysis level is `Level3SyntaxAnalysis`.
- build status is `NotRun`.
- syntax declaration/member facts are emitted.
- `AnalysisGap` facts are emitted.

### `samples/jvm-modern-sample`

Purpose: prove the JVM Java semantic path and reducer compatibility.

Command:

```bash
JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm installDist
src/jvm/build/install/tracemap-jvm/bin/tracemap-jvm scan --repo samples/jvm-modern-sample --out <tmp>/jvm-modern-sample
dotnet run --project src/dotnet/TraceMap.Cli -- reduce --index <tmp>/jvm-modern-sample/index.sqlite --contract-delta samples/contract-deltas/jvm-modern.order-status.json --out <tmp>/jvm-impact.md
```

Expected:

- scan emits the required artifacts.
- Java semantic `PropertyAccessed`, `MethodInvoked`, `CallEdge`, `ObjectCreated`, and `ArgumentPassed` facts are emitted when JDK compiler analysis succeeds.
- `OrderResponse.status` is `DefiniteImpact`.
- route, SQL, JPA, Jackson serializer, and config facts are emitted as bounded integration evidence.
- raw SQL/config/serializer values are hashed or represented as safe normalized keys.

### `samples/jvm-kotlin-sample`

Purpose: prove Kotlin syntax fallback behavior.

Command:

```bash
src/jvm/build/install/tracemap-jvm/bin/tracemap-jvm scan --repo samples/jvm-kotlin-sample --out <tmp>/jvm-kotlin-sample
```

Expected:

- scan completes.
- analysis level is `Level3SyntaxAnalysis`.
- build status is `NotRun`.
- Kotlin declaration, invocation, object creation, and route syntax facts are emitted.
- `KotlinSemanticNotImplemented` is recorded as an analysis gap.

### `samples/jvm-broken-sample`

Purpose: prove JVM reduced fallback behavior.

Command:

```bash
src/jvm/build/install/tracemap-jvm/bin/tracemap-jvm scan --repo samples/jvm-broken-sample --out <tmp>/jvm-broken-sample
```

Expected:

- scan completes.
- analysis level is reduced or syntax-only.
- syntax facts are emitted where recoverable.
- Java compiler diagnostics and/or parser gaps are emitted as `AnalysisGap` facts.

### `samples/python-fastapi-sample`

Purpose: prove Python AST/package/config/SQL extraction and reducer compatibility.

Command:

```bash
/tmp/tracemap-python-venv/bin/python -m tracemap_py.cli scan --repo samples/python-fastapi-sample --out <tmp>/python-fastapi-sample
dotnet run --project src/dotnet/TraceMap.Cli -- reduce --index <tmp>/python-fastapi-sample/index.sqlite --contract-delta samples/contract-deltas/python-fastapi.order-status.json --out <tmp>/python-impact.md
```

Expected:

- scan emits the required artifacts.
- scan analysis level is `Level1SemanticAnalysisReduced`.
- build status is `FailedOrPartial`.
- `OrderResponse.status` is at least `ProbableImpact` through Tier2 serializer/DTO evidence.
- FastAPI route, requests/httpx HTTP call, Pydantic serializer, SQLAlchemy column, SQL text/file, config key, call edge, object creation, argument flow, and symbol relationship facts are emitted where visible.
- raw SQL/config/serializer values are hashed or represented as safe normalized keys.

### `samples/python-flask-sample`

Purpose: prove Flask route extraction, config module assignments, static HTTP calls, and SQL files.

Command:

```bash
/tmp/tracemap-python-venv/bin/python -m tracemap_py.cli scan --repo samples/python-flask-sample --out <tmp>/python-flask-sample
```

Expected:

- scan completes with reduced Python coverage.
- Flask route, httpx call, config key, and SQL file facts are emitted.
- dynamic route registration that cannot be resolved remains lower-tier review evidence or an analysis gap.

### `samples/python-broken-sample`

Purpose: prove Python syntax failure recovery.

Command:

```bash
/tmp/tracemap-python-venv/bin/python -m tracemap_py.cli scan --repo samples/python-broken-sample --out <tmp>/python-broken-sample
```

Expected:

- scan completes.
- analysis level is reduced.
- parse failures and dynamic SQL/import boundaries are recorded as `AnalysisGap` facts.
- recoverable Python files still emit declaration, invocation, and alias facts.

### `samples/endpoint-client-angular` and `samples/endpoint-server-aspnet`

Purpose: prove cross-index endpoint alignment over Angular `HttpClient` and ASP.NET controller route syntax fallback.

Command:

```bash
cd src/typescript
node dist/src/cli.js scan --repo ../../samples/endpoint-client-angular --out <tmp>/endpoint-client
cd ../..
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/endpoint-server-aspnet --out <tmp>/endpoint-server
dotnet run --project src/dotnet/TraceMap.Cli -- endpoints --client-index <tmp>/endpoint-client/index.sqlite --server-index <tmp>/endpoint-server/index.sqlite --client-label endpoint-client --server-label endpoint-server --out <tmp>/endpoint-report
```

Expected:

- Angular scan emits `HttpCallDetected` facts from `typescript.integration.angular-httpclient.v1`.
- ASP.NET scan emits `HttpRouteBinding` facts from `csharp.syntax.aspnetroute.v1`.
- endpoint report contains `MatchedEndpoint`, `OptionalSegmentMatch`, `MethodMismatch`, `DynamicClientUrlNeedsReview`, and `ServerEndpointNoClientMatch`.
- report warnings remain coverage-relative and do not claim runtime reachability.

## External Sample Repos

External repos live outside this repository at a developer-provided path:

```text
<external-csharp-sample-repos>
```

These are opt-in smoke fixtures because they are larger, machine-local, and may depend on SDKs or packages not present on every development machine.

Recommended first-pass repos:

- `ProjectExtensions.Azure.ServiceBus`
- `fluentjdf`

Example command:

```bash
scripts/smoke-sample-repos.sh <external-csharp-sample-repos> <tmp>/sample-smoke
```

Expected:

- scan commands complete.
- scans may report `Level1SemanticAnalysisReduced`.
- reduced scans must label no-evidence findings as `NoEvidenceReducedCoverage`.
- generic member names such as `status` may match unrelated code and should emit warnings when they match multiple facts or a high-fan-out set.

TypeScript external smoke, recorded June 12, 2026:

- Repo: `<external-scip-typescript-repo>`
- Command: `node src/typescript/dist/src/cli.js scan --repo <external-scip-typescript-repo> --out <tmp>/tracemap-ts-scip`
- Result: completed, 13,883 facts, `Level1SemanticAnalysisReduced`, `FailedOrPartial`.
- Interpretation: reduced coverage is expected for external repos with TypeScript diagnostics or dependency/config gaps; no-evidence reducer findings must remain reduced.

Private endpoint smoke, recorded June 12, 2026:

- Client repo: `<private-angular-client-app>`
- Server repo: `<private-aspnet-server-root>`
- Server project: `<private-aspnet-server-project>`
- Result: client scan completed with 34 `HttpCallDetected` facts and `Level1SemanticAnalysisReduced`; server scan completed with 37 `HttpRouteBinding` facts and `Level1SemanticAnalysisReduced`.
- Endpoint report: 38 findings, `ReducedEvidenceForScannedIndexes`, with `MatchedEndpoint`, `OptionalSegmentMatch`, `ServerEndpointNoClientMatch`, and coverage-warning sections.
- Interpretation: reduced coverage is expected because the fixture has dependency/project-load quirks; endpoint no-match findings remain coverage-relative.

Repeatable command: run the local ignored private endpoint smoke helper with explicit client and server paths supplied by the developer environment.

Private endpoint smoke rerun, recorded June 13, 2026 after endpoint review-loop fixes:

- Command: local ignored private endpoint smoke helper with `<private-angular-client-app>`, `<private-aspnet-server-root>`, and `<tmp>/private-endpoint-smoke`.
- Client scan: completed, 25,652 facts, 34 `HttpCallDetected` facts, 3 `AnalysisGap` facts, `Level1SemanticAnalysisReduced` / `FailedOrPartial`.
- Server scan: completed, 7,303 facts, 37 `HttpRouteBinding` facts, 142 `AnalysisGap` facts, `Level1SemanticAnalysisReduced` / `FailedOrPartial`.
- Endpoint report: 38 findings, `ReducedEvidenceForScannedIndexes`.
- Endpoint classifications: 31 `MatchedEndpoint`, 3 `OptionalSegmentMatch`, 4 `ServerEndpointNoClientMatch`.
- Server-only rows:
  - `POST /api/account/change-password`
  - `POST /api/account/log-off`
  - `GET /api/admin/runner/delete`
  - `GET /api/validation/is-club-name-unique`
- Interpretation: all 34 statically discovered client HTTP calls matched server endpoint evidence. The 4 server-only rows remain coverage-relative and are not dead-code proof.

## Repo-Specific Delta Fixtures

Repo-specific deltas live under:

```text
samples/contract-deltas/
```

Current files:

- `modern-sample.customer-profile.json`
- `servicebus.transient-status.json`
- `fluentjdf.status-builder.json`

Each fixture should document:

- target repo.
- changed contract element.
- expected classification.
- expected evidence tier.
- why the fixture exists.

## Regression Matrix

| Scenario | Expected result |
| --- | --- |
| semantic property usage match | `DefiniteImpact` |
| semantic type match | `DefiniteImpact` |
| Tier2 structural DTO/HTTP/DB/config match | `ProbableImpact` |
| syntax-only member match | `NeedsReview` |
| no match with full semantic coverage | `NoEvidenceFullCoverage` |
| no match with reduced coverage | `NoEvidenceReducedCoverage` |
| Angular HttpClient static URL | `HttpCallDetected` with normalized path key |
| ASP.NET controller attribute route with missing framework refs | Tier3 `HttpRouteBinding` from `csharp.syntax.aspnetroute.v1` |
| client/server method and path match | `MatchedEndpoint` |
| server optional segment match | `OptionalSegmentMatch` |
| path match but method differs | `MethodMismatch` |
| dynamic client URL | `DynamicClientUrlNeedsReview` |
| analysis-gap evidence names changed element | `UnknownAnalysisGap` |
| unparsable contract element | `UnknownAnalysisGap` |
| generic member with multiple matches | classification preserved plus generic-name warning |
| high fan-out match set | classification preserved plus fan-out warning |
| syntax invocation | `CallEdge` with containing member and callee name |
| semantic method invocation | Tier1 `CallEdge` with resolved caller and callee symbols |
| syntax object creation | `ObjectCreated` with created type and assigned variable when obvious |
| semantic object creation | Tier1 `ObjectCreated` with created type, constructor, caller, and assembly identity |
| semantic argument passed | Tier1 `ArgumentPassed` with parameter name/type and argument symbol/source location when available |
| semantic symbol identity | resolved semantic facts include stable C# `sourceSymbolId`/`targetSymbolId` properties where Roslyn exposes symbols |
| symbol index tables | `symbols`, `symbol_occurrences`, and `fact_symbols` rows link exact compiler-backed symbols to fact evidence |
| symbol relationship | Tier1 `SymbolRelationship` fact and `symbol_relationships` row for direct inheritance, interface implementation, member override, or interface member implementation |
| semantic local alias | Tier1 `LocalAlias` with alias symbol, origin symbol, rule ID, and evidence span |
| semantic field alias | Tier1 `FieldAlias` with field symbol, origin symbol, rule ID, and evidence span |
| semantic parameter forwarding | `parameter_forward_edges` row with source method/parameter, target method/parameter, rule ID, and evidence span |
| parameter flow report | `tracemap flow` chains direct forwarding, same-method aliases, and unique constructor field initialization while labeling limitations |
| relationship report | `tracemap relate` chains direct symbol relationships while labeling limitations |
| scoped scan | `tracemap scan --project`, `--solution`, `--include`, `--exclude`, `--target-framework`, and explicit `--restore` constrain scan/load behavior deterministically |
| flow boundary | Tier1 semantic boundary fact for DI, deserialization, reflection, dynamic invocation, mutation, branch condition, callback/delegate/event/expression tree, await/await foreach/await using/task scheduling, or iterator yield without claiming runtime flow |
| runtime evidence | Tier1 semantic fact for statically visible DI registration, serializer contract member, reflection target, dynamic dispatch candidate, collection element input, mutation semantics, or simple branch feasibility |
| contract mapping | Tier1 semantic fact for attribute route binding, table/column mapping, or literal configuration section binding |
| calculation expression | `CalculationExpression` with operator, line span, and expression hash |
| retry/backoff method | `RetryPolicyLogic` |
| generated or DI glue file | `InfrastructureBoilerplate` |
| TypeScript semantic property usage match | `DefiniteImpact` through existing .NET reducer |
| TypeScript syntax-only fallback | reduced or syntax-only coverage, never clean |
| TypeScript integration boundary | Tier1/Tier2/Tier3 according to compiler/package/shape evidence |
| TypeScript direct SQL literal | `SqlTextUsed` plus SQL-shape `QueryPatternDetected` under `typescript.integration.sql.v1` when complete static SQL text is visible |
| TypeScript Prisma/Base44 query pattern | `QueryPatternDetected` remains query-builder evidence and does not gain `sqlSourceKind` unless direct SQL text is present |
| Legacy DBML descriptor | `LegacyDataEntityDeclared`, `LegacyDataStorageObjectDeclared`, `LegacyDataColumnDeclared`, and `LegacyDataMappingDeclared` under `legacy.data.dbml.v1` as static design-time metadata only |
| Legacy EDMX simple mapping | `LegacyDataMappingDeclared` under `legacy.data.edmx.v1` when MSL maps one conceptual descriptor to one storage descriptor; unsupported shapes emit `AnalysisGap` |
| Typed DataSet TableAdapter static SQL | `LegacyData*` descriptor facts plus `SqlTextUsed`/SQL-shape `QueryPatternDetected` hashes under `legacy.data.typed-dataset.v1`; raw SQL is not stored |
| Legacy data model identity | Source descriptor facts carry safe `metadataFormat`, `modelKind`, `descriptorRole`, `stableModelKey`, identity-rule provenance, safe display labels or hashes, and source metadata fact IDs without changing source rule ownership |
| Legacy data model surface projection | Combined report, path, route-flow, reverse, diff, and vault outputs render or preserve projected descriptor rows as `surfaceKind = legacy-data` with `surfaceSubtype = data-model`; `AnalysisGap` facts remain gaps/caveats rather than terminal surfaces |
| Legacy data model vault limitation threading | Vault graph nodes for projected `legacy-data` descriptor surfaces preserve descriptor limitation codes such as `formula-redacted`, `filter-redacted`, and `query-redacted`; unsafe NHibernate formula/filter/query/config/provider values, URLs, remotes, local paths, and private labels remain absent from `graph.json` and generated Markdown |
| Legacy data config provider | `LegacyDataProviderConfigDeclared` under `legacy.data.config.v1` with safe names or hashes; raw connection strings and config values are omitted |
| Legacy data generated code explicit file link | `LegacyDataGeneratedCodeLinked` under `legacy.data.generated-link.v1` with `linkKind = explicit-generated-file`, `Tier2Structural`, `coverageLabel = full`, `sourceMetadataFactId`, `supportingFactIds`, `symbolRole`, and `stableModelKey`; descriptor tiers are not upgraded by the link |
| Legacy data generated code syntax fallback | `LegacyDataGeneratedCodeLinked` under `legacy.data.generated-link.v1` with `linkKind = type-name-syntax-fallback`, `Tier3SyntaxOrTextual`, `coverageLabel = reduced`, and static-only limitations; descriptor tiers are not upgraded by the link |
| Legacy data generated code duplicate designer types | `AmbiguousGeneratedCodeLink` gap under `legacy.data.generated-link.v1` anchored to the source descriptor line; no generated-code link is emitted |
| Legacy data generated code missing explicit designer | `MissingGeneratedCode` gap under `legacy.data.generated-link.v1` anchored to the source descriptor line; no generated-code link is emitted |
| Python Pydantic DTO member match | `ProbableImpact` through Tier2 `SerializerContractMember` |
| Python Flask/FastAPI route | `HttpRouteBinding` with normalized route key when static decorator syntax is visible |
| Python SQLAlchemy column | `DatabaseColumnMapping` with table/column/member evidence when declarative syntax is visible |
| Python static SQL pattern | `QueryPatternDetected` with operation, table/column metadata, text hash, and query shape hash; SQL-like `WITH`/CTE may be shape-hash-only |
| Cross-adapter SQL shape fixture | `.NET`, TypeScript, JVM, and Python SQL-shape helpers match `samples/sql-shape-fixtures/sql-shape-v1.json` for v1 text/shape hashes |
| Combined SQL surface identity | SQL surface display prefers `queryShapeHash`, keeps same-table/different-shape surfaces separate, and preserves different `sqlSourceKind` values in diff/reverse identity |
| Combined SQL weak evidence caveat | hash-only SQL evidence emits `HashOnlyEvidence`, fact-hash fallback emits `VolatileIdentity`, and both remain review-tier in diff/impact |
| Python endpoint smoke | `MatchedEndpoint` from public Python client/server sample indexes |
| Python syntax invocation | `CallEdge` with containing function/module and callee syntax name |
| Python broken file | reduced coverage with `AnalysisGap`, while other files continue scanning |

## Performance Smoke Targets

These are sanity checks, not strict benchmarks:

- small sample repo scans in under 10 seconds on a developer machine.
- external sample repos complete without unhandled exceptions.
- reducer runs complete in seconds for existing sample indexes.

## Review Checklist

- Did `dotnet build` pass?
- Did `dotnet test` pass?
- Can the CLI scan at least one sample repo?
- Can the CLI reduce at least one sample delta?
- Are facts deterministic and evidence-backed?
- Did the report avoid saying clean when coverage is reduced?
- Did rule catalog limitations change when reducer behavior changed?
