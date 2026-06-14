# Multi-Index Portfolio Dependency Report Tasks

## First PR Boundary

Recommended first implementation PR: command shell, manifest parsing, read-only index detection, source identity/coverage summary, rule catalog entries, deterministic Markdown/JSON skeleton, and explicit unavailable/not-requested optional sections. This ships useful portfolio provenance without duplicating combined report, diff, impact, path, or reverse logic.

`--exit-code`, direct mixed manifest/index input, and release-review import are deferred follow-ups. Do not add CLI flags for them in v1.

## Implementation Tasks

- [ ] 1. Confirm current architecture and reusable seams. Requirements: 1, 3, 4, 7, 8.
  - [ ] Inspect current `report`, `diff`, `impact`, `paths`, and `reverse` implementations.
  - [ ] Identify reusable readers for single-language and combined indexes.
  - [ ] Identify existing safe path, safe metadata, Markdown escaping, JSON ordering, and output-path helpers.
  - [ ] Confirm which optional precision tables can be relied on for MVP.
  - [ ] Confirm which workflows can be reused directly and which must render unavailable/deferred.
  - [ ] Confirm and record the endpoint matcher extraction seam before endpoint alignment starts.
  - [ ] Record architecture conclusions in the implementing PR description or a spec-local note before PR 3 begins.

- [ ] 2. Add portfolio command shell and option validation. Requirements: 1, 2, 10, 11.
  - [ ] Add `tracemap portfolio --out`.
  - [ ] Add repeated `--index <path> --label <label>` input parsing.
  - [ ] Add `--manifest`, `--before-manifest`, and `--after-manifest` parsing.
  - [ ] Reject direct `--index` plus `--manifest` combinations.
  - [ ] Reject unpaired before/after manifests.
  - [ ] Parse `--format`, selectors, optional path/reverse context flags, and caps.
  - [ ] Reject deferred v1 flags such as `--exit-code`, `--release-review`, and `--allow-mixed-inputs` with sanitized errors if they are encountered.
  - [ ] Validate numeric caps.
  - [ ] Emit sanitized errors without raw local paths or secret-looking values.
  - [ ] Open input indexes read-only.
  - [ ] Implement output path behavior for directory, extensionless, Markdown file, and JSON file.

- [ ] 3. Add portfolio manifest model and reader. Requirements: 2, 10, 11.
  - [ ] Define manifest schema version `1.0`.
  - [ ] Parse label, index path, expected repo identity, expected commit SHA, group, and role tags.
  - [ ] Resolve relative index paths against the manifest location.
  - [ ] Reject unsupported manifest versions.
  - [ ] Reject duplicate labels.
  - [ ] Validate unreadable index entries with sanitized label-based errors.
  - [ ] Ensure manifest paths are not emitted in public output.

- [ ] 4. Add report models and section status support. Requirements: 3, 8, 10.
  - [ ] Define root model with `reportType = multi-index-portfolio-report` and `version = 1.0`.
  - [ ] Define query, input, source, coverage, summary, section, gap, limitation, and metadata models.
  - [ ] Implement section status vocabulary: `available`, `not_requested`, `unavailable`, `deferred`, `truncated`.
  - [ ] Implement rollup vocabulary and fixed precedence.
  - [ ] Use empty arrays and nulls consistently.
  - [ ] Omit generated timestamps for byte-stable output.

- [ ] 5. Implement index detection and source expansion. Requirements: 1, 3.
  - [ ] Detect TraceMap single-language indexes.
  - [ ] Detect combined indexes.
  - [ ] Expand combined `index_sources` into portfolio source records.
  - [ ] Preserve combined container label and original source label.
  - [ ] Extract scan ID, repo identity, commit SHA, language, scanner version, extractor version, analysis level, build status, and manifest gaps.
  - [ ] Validate manifest expected repo/commit hints.
  - [ ] Emit identity gaps for unknown SHA, mismatches, duplicate labels, ambiguous identity, and duplicate source identity.
  - [ ] Exclude duplicate source identities from cross-source endpoint alignment and shared-surface grouping.
  - [ ] Mark reduced coverage when build, analysis, schema, commit, language, or manifest gaps require it.

- [ ] 6. Add deterministic Markdown and JSON skeleton. Requirements: 9, 10, 11.
  - [ ] Render Markdown sections in required order.
  - [ ] Render JSON top-level fields in required shape.
  - [ ] Render all optional sections as `not_requested`, `unavailable`, or `deferred` instead of omitting them.
  - [ ] Sort arrays deterministically.
  - [ ] Encode arbitrary metadata as sorted key/value arrays.
  - [ ] Escape Markdown table and link syntax.
  - [ ] Use shared safe path and safe value rendering helpers.
  - [ ] Add byte-stability tests for skeleton output.

- [ ] 7. Add rule catalog entries before emitting portfolio findings. Requirements: 8, 12.
  - [ ] Complete this task before merging tasks 8, 9, 10, or 11; no portfolio findings, groups, or gaps may ship without cataloged rules and limitations.
  - [ ] Add `portfolio.identity.v1`.
  - [ ] Add `portfolio.coverage.v1`.
  - [ ] Add `portfolio.schema.v1`.
  - [ ] Add `portfolio.endpoint.alignment.v1`.
  - [ ] Add `portfolio.surface.inventory.v1`.
  - [ ] Add `portfolio.surface.group.v1`.
  - [ ] Add `portfolio.edge.inventory.v1`.
  - [ ] Add `portfolio.diff.v1`.
  - [ ] Add `portfolio.impact.context.v1`.
  - [ ] Add `portfolio.optional-context.v1`.
  - [ ] Add `portfolio.selector.v1`.
  - [ ] Add `portfolio.truncation.v1`.
  - [ ] Add `portfolio.redaction.v1`.
  - [ ] Document emitted row/gap types and limitations for every new rule.
  - [ ] Include `RedactedValue` and `UnsafePropertyOmitted` behavior and limitations for `portfolio.redaction.v1`.
  - [ ] Run `dotnet test src/dotnet/TraceMap.sln` after catalog changes.

- [ ] 8. Implement dependency surface inventory. Requirements: 4, 8, 9, 10, 11.
  - [ ] Add single-language fact reader support over `facts`, symbols, and fact-symbol tables.
  - [ ] Add combined-index reader support over `combined_facts` and related tables.
  - [ ] Read HTTP client call facts.
  - [ ] Read HTTP route binding facts.
  - [ ] Read SQL/query-pattern facts and hash-only SQL evidence.
  - [ ] Read package, project reference, import, module, and dependency facts.
  - [ ] Read config, environment variable, connection string name, and resource identifier facts.
  - [ ] Normalize all rows into portfolio source-scoped surface rows.
  - [ ] Preserve rule ID, evidence tier, fact ID, commit SHA, file path, and line span.
  - [ ] Render missing optional values as `unknown`, `n/a`, `null`, or `[]` consistently.
  - [ ] Apply row caps and truncation gaps.
  - [ ] Prove unsafe property values do not render.

- [ ] 9. Implement dependency edge inventory. Requirements: 4, 8, 9, 10.
  - [ ] Add single-language edge reader support where source index edge tables exist.
  - [ ] Add combined-index edge reader support over precise combined edge tables.
  - [ ] Read call edges.
  - [ ] Read object creation edges.
  - [ ] Read symbol relationships.
  - [ ] Read argument flows.
  - [ ] Read parameter-forwarding edges.
  - [ ] Use `combined_dependency_edges` as summary fallback when precise tables are absent.
  - [ ] Preserve source/target symbol metadata, source label, rule ID, evidence tier, edge ID, fact IDs, file span, and limitations.
  - [ ] Apply deterministic sorting and caps.

- [ ] 10. Implement cross-source endpoint alignment. Requirements: 5, 8, 11, 12.
  - [ ] Extract or wrap the combined report endpoint matcher as a shared internal helper before implementing portfolio alignment.
  - [ ] Include same-source client/route pairs when facts differ.
  - [ ] Emit `MatchedEndpoint`, `OptionalSegmentMatch`, `MethodMismatch`, `AmbiguousMatch`, `ClientCallNoServerEndpoint`, `ServerEndpointNoClientMatch`, `DynamicClientUrlNeedsReview`, and `UnknownAnalysisGap`.
  - [ ] Treat cross-source fan-out as one finding per source, not ambiguity.
  - [ ] Treat source-local multiple equivalent server candidates as `AmbiguousMatch`.
  - [ ] Use closed-set dynamic reason codes and hashes only.
  - [ ] Generate deterministic endpoint finding IDs.
  - [ ] Preserve all supporting facts, rule IDs, evidence tiers, file spans, and commit SHAs.
  - [ ] Prove duplicate source identities cannot create cross-source endpoint findings.

- [ ] 11. Implement shared portfolio surface grouping. Requirements: 5, 8, 10, 11.
  - [ ] Group safe HTTP route/client identities.
  - [ ] Group safe SQL table/column/query-shape identities.
  - [ ] Group package identities.
  - [ ] Group config key names or hashes.
  - [ ] Group symbol identities only when stable symbol IDs exist.
  - [ ] Preserve all supporting source evidence rows.
  - [ ] Emit `allSourcesSame` so same-source-only groups are visible.
  - [ ] Mark Tier3, hash-only, dynamic, or coverage-relative groups as review-tier.
  - [ ] Document grouping limitations in output.
  - [ ] Prove duplicate source identities cannot create shared-surface groups.

- [ ] 12. Add selectors and caps. Requirements: 6, 7, 8, 9, 10.
  - [ ] Implement `--source`.
  - [ ] Implement `--group`.
  - [ ] Implement `--surface`.
  - [ ] Implement `--surface-name` with safe exact/wildcard semantics consistent with paths/diff where possible.
  - [ ] Record `IgnoredSelector` when a selector applies only to a disabled or unavailable section.
  - [ ] Emit `SelectorNoMatch` when selectors match no evidence.
  - [ ] Apply source, surface, endpoint, edge, shared-surface, path, root, and gap caps after deterministic ordering.
  - [ ] Emit omitted counts and `TruncatedByLimit`.

- [ ] 13. Add before/after manifest comparison. Requirements: 6, 8, 10, 11.
  - [ ] Load and validate before and after manifests.
  - [ ] Pair sources by manifest label plus repo identity when available.
  - [ ] Emit `IdentityAmbiguous` and downgrade to `ReviewRecommended` when labels match but extracted repo identity differs without manifest expected identity.
  - [ ] Render added, removed, changed, and unpaired sources.
  - [ ] Project surfaces and edges into stable safe identities.
  - [ ] Compare projected evidence with coverage-relative downgrade rules.
  - [ ] Reuse combined diff semantics where compatible.
  - [ ] Emit unavailable/deferred gaps where combined diff reuse is not possible.
  - [ ] Keep local paths and unsafe values out of diff rows.

- [ ] 14. Add optional impact composition. Requirements: 6, 8, 10.
  - [ ] Keep impact context off unless `--include-impact` is provided.
  - [ ] Reuse combined change impact engine when before/after compatible combined snapshots are available.
  - [ ] Preserve impact classifications, rule IDs, evidence tiers, supporting IDs, coverage caveats, and limitations.
  - [ ] Render `unavailable` or `deferred` when impact cannot run for current inputs.
  - [ ] Do not introduce a competing portfolio impact classifier.

- [ ] 15. Add optional path and reverse context. Requirements: 7, 8, 10.
  - [ ] Keep path context off unless `--include-paths` is provided.
  - [ ] Keep reverse context off unless `--include-reverse` is provided.
  - [ ] Reuse existing bounded path query APIs.
  - [ ] Reuse existing bounded reverse query APIs.
  - [ ] Preserve path/reverse classifications, rule IDs, evidence tiers, supporting facts, edge IDs, and limitations.
  - [ ] Render unavailable/deferred gaps for incompatible inputs.
  - [ ] Render `releaseReviewContext` as `not_requested` or `deferred`; do not implement release-review import in v1.

- [ ] 16. Add safety and redaction tests. Requirements: 9, 10, 11, 12.
  - [ ] Prove no raw SQL renders.
  - [ ] Prove no snippets render.
  - [ ] Prove no config values or connection strings render.
  - [ ] Prove no raw URLs render.
  - [ ] Prove no secret-looking values render.
  - [ ] Prove no local absolute paths render in Markdown, JSON, or stderr.
  - [ ] Prove Markdown escaping for table delimiters and link-like syntax.
  - [ ] Prove manifest `portfolioId`, `snapshotId`, `label`, `group`, and `roleTags` cannot inject Markdown or unsafe JSON display strings.
  - [ ] Prove JSON metadata ordering.

- [ ] 17. Add integration tests. Requirements: 1-12.
  - [ ] Repeated `--index --label` input.
  - [ ] Manifest input.
  - [ ] Mixed single-language and combined inputs.
  - [ ] Combined input expansion into multiple portfolio sources.
  - [ ] Duplicate label rejection.
  - [ ] Expected repo/commit mismatch gaps.
  - [ ] Unknown commit SHA partial coverage.
  - [ ] Duplicate source identity across combined and single-language inputs does not create cross-source matches or groups.
  - [ ] Cross-source endpoint alignment across at least three sources.
  - [ ] Same-source endpoint findings and cross-source fan-out behavior.
  - [ ] SQL, package, config, call edge, object creation, and parameter-forwarding rendering.
  - [ ] Shared surface grouping with `allSourcesSame`.
  - [ ] Before/after manifest comparison, including identity-ambiguous labels.
  - [ ] Optional path/reverse sections not requested by default.
  - [ ] Optional path/reverse unavailable for incompatible inputs.
  - [ ] `releaseReviewContext` v1 deferred/not-requested behavior.
  - [ ] Relative manifest paths resolve from the manifest location.
  - [ ] Rollup precedence.
  - [ ] Truncation omitted counts.
  - [ ] No generated timestamps or stored scan/import timestamps render.
  - [ ] Input databases are not mutated.
  - [ ] Repeated output is byte-stable.

- [ ] 18. Update docs and examples. Requirements: 1, 2, 8, 12.
  - [ ] Add README or docs quickstart for portfolio reports.
  - [ ] Add sample portfolio manifest.
  - [ ] Document command options and output files.
  - [ ] Document static-analysis limitations and redaction policy.
  - [ ] Update `docs/ACCEPTANCE.md` if public acceptance workflow changes.
  - [ ] Update `docs/VALIDATION.md` if smoke validation changes.

- [ ] 19. Validate implementation PRs. Requirements: 12.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`
  - [ ] `dotnet test src/dotnet/TraceMap.sln`
  - [ ] `./scripts/check-private-paths.sh`
  - [ ] `git diff --check`
  - [ ] Relevant pinned smoke checks from `docs/VALIDATION.md` when implementation touches language adapters, combined report, diff, impact, paths, reverse, or release-review behavior.

## Suggested PR Slices

- [ ] PR 1: Command shell, manifest reader, read-only index detection, source identity/coverage summary, rule catalog entries, skeleton writers, unavailable optional sections.
- [ ] PR 2: Dependency surface inventory with redaction and deterministic output.
- [ ] PR 3: Dependency edge inventory with redaction and deterministic output.
- [ ] PR 4: Cross-source endpoint alignment and shared-surface grouping.
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
