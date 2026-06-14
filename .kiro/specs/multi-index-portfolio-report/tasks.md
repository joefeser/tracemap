# Multi-Index Portfolio Dependency Report Tasks

## First PR Boundary

Recommended first implementation PR: command shell, manifest parsing, read-only index detection, source identity/coverage summary, rule catalog entries, deterministic Markdown/JSON skeleton, and explicit unavailable/not-requested optional sections. This ships useful portfolio provenance without duplicating combined report, diff, impact, path, or reverse logic.

`--exit-code`, direct mixed manifest/index input, and release-review import are deferred follow-ups. Do not add CLI flags for them in v1.

## Implementation Tasks

- [x] 1. Confirm current architecture and reusable seams. Requirements: 1, 3, 4, 7, 8.
  - [x] Inspect current `report`, `diff`, `impact`, `paths`, and `reverse` implementations.
  - [x] Identify reusable readers for single-language and combined indexes.
  - [x] Identify existing safe path, safe metadata, Markdown escaping, JSON ordering, and output-path helpers.
  - [x] Confirm which optional precision tables can be relied on for MVP.
  - [x] Confirm which workflows can be reused directly and which must render unavailable/deferred.
  - [x] Confirm and record the endpoint matcher extraction seam before endpoint alignment starts.
  - [x] Record architecture conclusions in the implementing PR description or a spec-local note before PR 3 begins.

- [x] 2. Add portfolio command shell and option validation. Requirements: 1, 2, 10, 11.
  - [x] Add `tracemap portfolio --out`.
  - [x] Add repeated `--index <path> --label <label>` input parsing.
  - [x] Add `--manifest`, `--before-manifest`, and `--after-manifest` parsing.
  - [x] Reject direct `--index` plus `--manifest` combinations.
  - [x] Reject unpaired before/after manifests.
  - [x] Parse `--format`, selectors, optional path/reverse context flags, and caps.
  - [x] Reject deferred v1 flags such as `--exit-code`, `--release-review`, and `--allow-mixed-inputs` with sanitized errors if they are encountered.
  - [x] Validate numeric caps.
  - [x] Include bounded optional-section caps: `--max-diff-rows`, `--max-impact-items`, `--max-depth`, and `--max-frontier`.
  - [x] Emit sanitized errors without raw local paths or secret-looking values.
  - [x] Open input indexes read-only.
  - [x] Implement output path behavior for directory, extensionless directory, Markdown file, and JSON file.
  - [x] Fail with a sanitized error when an extensionless output path already exists as a file.

- [x] 3. Add portfolio manifest model and reader. Requirements: 2, 10, 11.
  - [x] Define manifest schema version `1.0`.
  - [x] Parse label, index path, expected repo identity, expected commit SHA, group, and role tags.
  - [x] Resolve relative index paths against the manifest location.
  - [x] Reject unsupported manifest versions.
  - [x] Reject duplicate labels.
  - [x] Validate unreadable index entries with sanitized label-based errors.
  - [x] Ensure manifest paths are not emitted in public output.

- [x] 4. Add report models and section status support. Requirements: 3, 8, 10.
  - [x] Define root model with `reportType = multi-index-portfolio-report` and `version = 1.0`.
  - [x] Define query, input, `PortfolioSnapshot`, source, coverage, summary, section, gap, limitation, and metadata models.
  - [x] Implement section status vocabulary: `available`, `not_requested`, `unavailable`, `deferred`, `truncated`.
  - [x] Implement rollup vocabulary and fixed precedence.
  - [x] Use empty arrays and nulls consistently.
  - [x] Omit generated timestamps for byte-stable output.

- [x] 5. Implement index detection and source expansion. Requirements: 1, 3.
  - [x] Detect TraceMap single-language indexes.
  - [x] Detect combined indexes.
  - [x] Expand combined `index_sources` into portfolio source records.
  - [x] Preserve combined container label and original source label.
  - [x] Extract scan ID, repo identity, commit SHA, language, scanner version, extractor version, analysis level, build status, and manifest gaps.
  - [x] Validate manifest expected repo/commit hints.
  - [x] Emit identity gaps for unknown SHA, mismatches, duplicate labels, ambiguous identity, and duplicate source identity.
  - [x] Exclude duplicate source identities from cross-source endpoint alignment and shared-surface grouping.
  - [x] Mark reduced coverage when build, analysis, schema, commit, language, or manifest gaps require it.

- [x] 6. Add deterministic Markdown and JSON skeleton. Requirements: 9, 10, 11.
  - [x] Render Markdown sections in required order.
  - [x] Render JSON top-level fields in required shape.
  - [x] Render all optional sections as `not_requested`, `unavailable`, or `deferred` instead of omitting them.
  - [x] Sort arrays deterministically.
  - [x] Encode arbitrary metadata as sorted key/value arrays.
  - [x] Normalize dictionary metadata from reused path/reverse/impact/diff models into sorted key/value arrays before rendering.
  - [x] Escape Markdown table and link syntax.
  - [x] Use shared safe path and safe value rendering helpers.
  - [x] Add byte-stability tests for skeleton output.

- [x] 7. Add rule catalog entries before emitting portfolio findings. Requirements: 8, 12.
  - [x] Complete this task before merging tasks 8, 9, 10, or 11; no portfolio findings, groups, or gaps may ship without cataloged rules and limitations.
  - [x] Add `portfolio.identity.v1`.
  - [x] Add `portfolio.coverage.v1`.
  - [x] Add `portfolio.schema.v1`.
  - [x] Add `portfolio.endpoint.alignment.v1`.
  - [x] Add `portfolio.surface.inventory.v1`.
  - [x] Add `portfolio.surface.group.v1`.
  - [x] Add `portfolio.edge.inventory.v1`.
  - [x] Add `portfolio.diff.v1`.
  - [x] Add `portfolio.impact.context.v1`.
  - [x] Add `portfolio.optional-context.v1`.
  - [x] Add `portfolio.selector.v1`.
  - [x] Add `portfolio.truncation.v1`.
  - [x] Add `portfolio.redaction.v1`.
  - [x] Document emitted row/gap types and limitations for every new rule.
  - [x] Include `RedactedValue` and `UnsafePropertyOmitted` behavior and limitations for `portfolio.redaction.v1`.
  - [x] Run `dotnet test src/dotnet/TraceMap.sln` after catalog changes.

- [x] 8. Implement dependency surface inventory. Requirements: 4, 8, 9, 10, 11.
  - [x] Add single-language fact reader support over `facts`, symbols, and fact-symbol tables.
  - [x] Add combined-index reader support over `combined_facts` and related tables.
  - [x] Read HTTP client call facts.
  - [x] Read HTTP route binding facts.
  - [x] Read SQL/query-pattern facts and hash-only SQL evidence.
  - [x] Read database column/persistence mapping facts such as `DatabaseColumnMapping` as `sql-persistence`.
  - [x] Read package, project reference, import, module, config, environment variable, connection string name, resource identifier, and dependency facts as `package-config`.
  - [x] Ensure config/resource values are omitted, hashed, or replaced with closed-set reason codes.
  - [x] Normalize all rows into portfolio source-scoped surface rows.
  - [x] Preserve rule ID, evidence tier, fact ID, commit SHA, file path, and line span.
  - [x] Render missing optional values as `unknown`, `n/a`, `null`, or `[]` consistently.
  - [x] Apply row caps and truncation gaps.
  - [x] Prove unsafe property values do not render.

- [x] 9. Implement dependency edge inventory. Requirements: 4, 8, 9, 10.
  - [x] Add single-language edge reader support where source index edge tables exist.
  - [x] Add combined-index edge reader support over precise combined edge tables.
  - [x] Read call edges.
  - [x] Read object creation edges.
  - [x] Read symbol relationships.
  - [x] Read argument flows.
  - [x] Read parameter-forwarding edges.
  - [x] Use `combined_dependency_edges` as summary fallback when precise tables are absent.
  - [x] Preserve source/target symbol metadata, source label, rule ID, evidence tier, edge ID, fact IDs, file span, and limitations.
  - [x] Apply deterministic sorting and caps.

- [x] 10. Implement cross-source endpoint alignment. Requirements: 5, 8, 11, 12.
  - [x] Extract or wrap the combined report endpoint matcher as a shared internal helper before implementing portfolio alignment.
  - [x] Include same-source client/route pairs when facts differ.
  - [x] Emit `MatchedEndpoint`, `OptionalSegmentMatch`, `MethodMismatch`, `AmbiguousMatch`, `ClientCallNoServerEndpoint`, `ServerEndpointNoClientMatch`, `DynamicClientUrlNeedsReview`, and `UnknownAnalysisGap`.
  - [x] Treat cross-source fan-out as one finding per source, not ambiguity.
  - [x] Treat source-local multiple equivalent server candidates as `AmbiguousMatch`.
  - [x] Use closed-set dynamic reason codes and hashes only.
  - [x] Generate deterministic endpoint finding IDs.
  - [x] Preserve all supporting facts, rule IDs, evidence tiers, file spans, and commit SHAs.
  - [x] Prove duplicate source identities cannot create cross-source endpoint findings.

- [x] 11. Implement shared portfolio surface grouping. Requirements: 5, 8, 10, 11.
  - [x] Group safe HTTP route/client identities.
  - [x] Group safe SQL table/column/query-shape identities.
  - [x] Group package identities.
  - [x] Group config key names or hashes.
  - [ ] Group symbol identities only when stable symbol IDs exist.
  - [x] Preserve all supporting source evidence rows.
  - [x] Emit `allSourcesSame` so same-source-only groups are visible.
  - [x] Mark Tier3, hash-only, dynamic, or coverage-relative groups as review-tier.
  - [x] Document grouping limitations in output.
  - [x] Prove duplicate source identities cannot create shared-surface groups.

- [x] 12. Add selectors and caps. Requirements: 6, 7, 8, 9, 10.
  - [x] Implement `--source`.
  - [x] Implement `--group`.
  - [x] Implement `--surface`.
  - [x] Implement `--surface-name` with safe exact/wildcard semantics consistent with paths/diff where possible.
  - [ ] Record `IgnoredSelector` when a selector applies only to a disabled or unavailable section.
  - [x] Emit `SelectorNoMatch` when selectors match no evidence.
  - [x] Apply source, surface, endpoint, edge, shared-surface, path, root, and gap caps after deterministic ordering.
  - [x] Emit omitted counts and `TruncatedByLimit`.

- [ ] 13. Add before/after manifest comparison. Requirements: 6, 8, 10, 11.
  - [x] Load and validate before and after manifests.
  - [x] Pair sources by manifest label plus repo identity when available.
  - [x] Emit `IdentityAmbiguous` and downgrade to `ReviewRecommended` when labels match but extracted repo identity differs without manifest expected identity.
  - [x] Render added, removed, changed, and unpaired sources.
  - [ ] Project surfaces and edges into stable safe identities.
  - [ ] Compare projected evidence with coverage-relative downgrade rules.
  - [ ] Reuse combined diff semantics where compatible.
  - [ ] Emit unavailable/deferred gaps where combined diff reuse is not possible.
  - [x] Keep local paths and unsafe values out of diff rows.

- [ ] 14. Add optional impact composition. Requirements: 6, 8, 10.
  - [x] Keep impact context off unless `--include-impact` is provided.
  - [ ] Reuse combined change impact engine when before/after compatible combined snapshots are available.
  - [ ] Preserve impact classifications, rule IDs, evidence tiers, supporting IDs, coverage caveats, and limitations.
  - [x] Render `unavailable` or `deferred` when impact cannot run for current inputs.
  - [x] Do not introduce a competing portfolio impact classifier.

- [ ] 15. Add optional path and reverse context. Requirements: 7, 8, 10.
  - [x] Keep path context off unless `--include-paths` is provided.
  - [x] Keep reverse context off unless `--include-reverse` is provided.
  - [ ] Reuse existing bounded path query APIs.
  - [ ] Reuse existing bounded reverse query APIs.
  - [ ] Preserve path/reverse classifications, rule IDs, evidence tiers, supporting facts, edge IDs, and limitations.
  - [x] Render unavailable/deferred gaps for incompatible inputs.
  - [x] Render `releaseReviewContext` as `not_requested` or `deferred`; do not implement release-review import in v1.

- [ ] 16. Add safety and redaction tests. Requirements: 9, 10, 11, 12.
  - [ ] Prove no raw SQL renders.
  - [ ] Prove no snippets render.
  - [x] Prove no config values or connection strings render.
  - [ ] Prove no raw URLs render.
  - [ ] Prove no secret-looking values render.
  - [x] Prove no local absolute paths render in Markdown, JSON, or stderr.
  - [x] Prove Markdown escaping for table delimiters and link-like syntax.
  - [ ] Prove manifest `portfolioId`, `snapshotId`, `label`, `group`, and `roleTags` cannot inject Markdown or unsafe JSON display strings.
  - [x] Prove JSON metadata ordering.

- [ ] 17. Add integration tests. Requirements: 1-12.
  - [x] Repeated `--index --label` input.
  - [x] Manifest input.
  - [ ] Mixed single-language and combined inputs.
  - [x] Combined input expansion into multiple portfolio sources.
  - [ ] Duplicate label rejection.
  - [x] Expected repo/commit mismatch gaps.
  - [ ] Unknown commit SHA partial coverage.
  - [ ] Duplicate source identity across combined and single-language inputs does not create cross-source matches or groups.
  - [ ] Cross-source endpoint alignment across at least three sources.
  - [ ] Same-source endpoint findings and cross-source fan-out behavior.
  - [ ] SQL, package, config, call edge, object creation, and parameter-forwarding rendering.
  - [x] Shared surface grouping with `allSourcesSame`.
  - [x] Before/after manifest comparison, including identity-ambiguous labels.
  - [x] Optional path/reverse sections not requested by default.
  - [ ] Optional path/reverse unavailable for incompatible inputs.
  - [x] `releaseReviewContext` v1 deferred/not-requested behavior.
  - [x] Relative manifest paths resolve from the manifest location.
  - [x] Extensionless output path behavior, including existing extensionless-file failure.
  - [ ] Rollup precedence.
  - [ ] Truncation omitted counts.
  - [ ] Optional section caps for diff, impact, path depth, and path frontier are honored.
  - [ ] Reused reverse/path dictionary metadata is normalized to sorted key/value arrays.
  - [x] No generated timestamps or stored scan/import timestamps render.
  - [x] Input databases are not mutated.
  - [x] Repeated output is byte-stable.

- [ ] 18. Update docs and examples. Requirements: 1, 2, 8, 12.
  - [x] Add README or docs quickstart for portfolio reports.
  - [x] Add sample portfolio manifest.
  - [x] Document command options and output files.
  - [x] Document static-analysis limitations and redaction policy.
  - [ ] Update `docs/ACCEPTANCE.md` if public acceptance workflow changes.
  - [ ] Update `docs/VALIDATION.md` if smoke validation changes.

- [x] 19. Validate implementation PRs. Requirements: 12.
  - [x] `dotnet build src/dotnet/TraceMap.sln`
  - [x] `dotnet test src/dotnet/TraceMap.sln`
  - [x] `./scripts/check-private-paths.sh`
  - [x] `git diff --check`
  - [x] Relevant pinned smoke checks from `docs/VALIDATION.md` when implementation touches language adapters, combined report, diff, impact, paths, reverse, or release-review behavior.

## Suggested PR Slices

- [x] PR 1: Command shell, manifest reader, read-only index detection, source identity/coverage summary, rule catalog entries, skeleton writers, unavailable optional sections.
- [x] PR 2: Dependency surface inventory with redaction and deterministic output.
- [x] PR 3: Dependency edge inventory with redaction and deterministic output.
- [x] PR 4: Cross-source endpoint alignment and shared-surface grouping.
- [ ] PR 5: Before/after manifest comparison and optional impact composition.
- [ ] PR 6: Optional path/reverse context, examples, and smoke workflow.

## Deferred Follow-Ups

- Persisted portfolio database behind explicit `--write-derived`.
- HTML portfolio explorer.
- Service catalog import as explicitly labeled external metadata.
- Batch query file support.
- `--exit-code` with an explicit deterministic CI policy.
- `--allow-mixed-inputs` for direct mixed manifest/index input.
- Release-review report import after release-review report workflow exists.
- Package vulnerability/license overlays in a separate spec.
- Runtime telemetry import as external evidence with separate provenance and limitations.
- CI annotations or dashboard publishing.

## Out of Scope for TraceMap Core

- Runtime topology discovery.
- Production traffic analysis.
- Service ownership inference.
- Package compatibility, vulnerability, or license claims.
- Release approval, merge recommendation, or CI gate policy.
- LLM-generated portfolio narratives or prompt-based classification.
