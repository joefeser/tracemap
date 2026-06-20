# Deterministic Review Priority Scoring Tasks

## Spec Authoring and Review Tasks

- [x] Draft requirements for deterministic evidence-backed review priority scoring.
- [x] Draft design for reusable scoring components, downgrade behavior, and workflow integration.
- [x] Draft implementation task breakdown with implementation tasks left unchecked.
- [x] Run Kiro Opus spec review and patch Medium+ findings.
- [x] Run Kiro Sonnet spec review and patch Medium+ findings.
- [x] Run final Kiro re-review after patches.
- [x] Run spec-only validation commands.

## First PR Boundary

Recommended first implementation PR: release-review opt-in scoring only, including shared scoring models/rules, rule catalog entries, Markdown/JSON output, and focused tests. Do not add scoring to diff, impact, paths, reverse, or portfolio in the first implementation PR unless release-review integration remains small and the shared API is already stable.

The first implementation PR uses ordinal priority only. Numeric `priorityScore` weights are deferred to a future scoring model version.

## Implementation Tasks

- [x] 1. Confirm MVP terminology and option shape. Requirements: 1, 3, 4.
  - [x] Confirm feature name in UI/report copy: deterministic review priority scoring.
  - [x] Confirm first workflow target: `tracemap release-review`.
  - [x] Confirm opt-in flag name, suggested as `--include-priority`.
  - [x] Confirm JSON model version, suggested as `review-priority.v1`.
  - [x] Confirm v1 ordinal-only scoring and `priorityScore: null` JSON behavior.
  - [x] Document ordinal candidates, caps, unknown behavior, and report aggregation in the rule catalog before output code is enabled.
  - [x] Defer numeric scoring and cap-to-score functions to a future scoring model version.
  - [x] Confirm release-review JSON compatibility choice: opt-in sidecar with byte-identical opt-out, or always-present additive `not_requested` section with version/compatibility policy.
  - [x] Confirm closed vocabularies for `severityHint`, `attentionLevel`, and section status.
  - [x] Confirm scoring is not a release gate, runtime risk predictor, security scanner, or business-criticality model.

- [x] 2. Add rule catalog entries before emitting output. Requirements: 2, 4, 5, 8.
  - [x] Add `review.priority.component.v1` or equivalent component rule.
  - [x] Add `review.priority.aggregate.v1` or equivalent aggregation rule.
  - [x] Add downgrade rules for coverage, identity, commit SHA, schema, fallback evidence, and truncation.
  - [x] Add unavailable-workflow and selector rules.
  - [x] Document ordinal precedence, public-surface inputs, cross-repo reach inputs, caps, and unknown behavior.
  - [x] Document inputs, outputs, evidence tiers, downgrade behavior, and limitations for every scoring rule.
  - [x] Document that scoring is deterministic static review prioritization, not runtime probability, AI judgment, release approval, vulnerability scanning, or compliance.

- [x] 3. Add scoring models. Requirements: 2, 3, 4, 7.
  - [x] Define report-level scoring summary model.
  - [x] Define row-level scoring annotation model.
  - [x] Define score component model.
  - [x] Define source evidence reference model.
  - [x] Define section status and completeness fields.
  - [x] Use empty arrays and nulls consistently.
  - [x] Use canonical sorted key/value metadata arrays.
  - [x] Avoid unordered dictionaries in emitted scoring JSON.
  - [x] Model checklist-derived components as references to finding/gap IDs, or as documented metadata-derived components when no tiered source evidence exists.

- [x] 4. Add evidence adapter for release-review. Requirements: 1, 2, 6.
  - [x] Adapt release-review summary, source coverage, top changed surfaces, contract impact, API/DTO changes, SQL/schema impact, package impact, path context, reverse context, gaps, checklist items, and limitations into scorable evidence records.
  - [x] Preserve underlying classifications, rule IDs, evidence tiers, file spans, source labels, commit SHAs, supporting fact IDs, edge IDs, path IDs, and limitations.
  - [x] Preserve section statuses such as `available`, `not_requested`, `unavailable`, `deferred`, and `truncated`.
  - [x] Do not read raw source files or raw input property bags.
  - [x] Do not call scanner or reducer code paths that mutate inputs.

- [x] 5. Implement deterministic component rules. Requirements: 2, 4, 5.
  - [x] Map strong static evidence classifications to base review-priority components.
  - [x] Map existing public-surface evidence to `public_surface` components without inferring runtime exposure.
  - [x] Map existing combined/portfolio cross-source evidence to `cross_repo_reach` components without inferring ownership, deployment, or business reach.
  - [x] Map review-tier, syntax-only, textual, hash-only, ambiguous, duplicate, name-only, fallback, and high-fan-out evidence to capped review components.
  - [x] Map evidence tiers to documented strength or cap components.
  - [x] Map deterministic fan-out counts to fan-out components.
  - [x] Map path and reverse context to bounded-context components.
  - [x] Map coverage, identity, commit SHA, schema, unavailable workflow, selector, and truncation gaps to downgrade or unknown components.
  - [x] Keep positive evidence and downgrade evidence visible as separate components.
  - [x] Use release-review's existing required-schema behavior for v1 and optional-schema gaps for precision limitations.

- [x] 6. Implement aggregation and severity mapping. Requirements: 3, 4, 5.
  - [x] Aggregate row components deterministically into `severityHint`.
  - [x] Emit `priorityScore: null` for v1 ordinal scoring where the schema includes the field.
  - [x] Do not emit numeric component weights or numeric score bands in v1.
  - [x] Apply cap rules after positive components.
  - [x] Emit `unknown` with `priorityScore: null` when limiting gaps prevent a credible score.
  - [x] Aggregate report-level `attentionLevel` from row severities, section completeness, and gap components.
  - [x] Mark report-level scoring incomplete when requested evidence is unavailable, reduced, or truncated.
  - [x] Sort rows, components, evidence references, and limitations deterministically.

- [x] 7. Add release-review Markdown output. Requirements: 1, 6, 7.
  - [x] Do not merge Markdown scoring output until Task 2 rule catalog entries are complete.
  - [x] Add a Review Priority section.
  - [x] Render status, model version, attention level, completeness, and limiting gap categories.
  - [x] Render top row priorities with safe evidence references and component summaries.
  - [x] Render no raw SQL, snippets, literal values, config values, connection strings, raw URLs, hostnames, remotes, local absolute paths, private paths, or secrets.
  - [x] Escape Markdown table and link delimiters.
  - [x] Reuse `CombinedReportHelpers.Cell`, `CombinedReportHelpers.SafePath`, and related shared helpers, or extract equivalent shared helpers before adding scoring-specific rendering.
  - [x] Include limitations that explain static review prioritization boundaries.

- [x] 8. Add release-review JSON output. Requirements: 1, 3, 4, 7.
  - [x] Do not merge JSON scoring output until Task 2 rule catalog entries are complete.
  - [x] Add top-level `reviewPriority` JSON.
  - [x] Prefer a sidecar `reviewPriorityRows` array keyed by stable row IDs unless nested annotations are explicitly versioned.
  - [x] Preserve release-review JSON byte identity when `--include-priority` is absent, or bump/document the release-review document version for an always-present additive section.
  - [x] Include section statuses and completeness.
  - [x] Include component arrays with rule IDs, evidence tiers, source evidence references, values, directions, and limitations.
  - [x] Preserve underlying report JSON fields and classifications.
  - [x] Ensure JSON is byte-stable for identical inputs.

- [x] 9. Add CLI wiring and compatibility behavior. Requirements: 1, 3, 6.
  - [x] Parse `--include-priority` for release-review.
  - [x] Ensure scoring is not run unless requested if v1 is opt-in.
  - [x] Ensure workflows that receive requested scoring before they have an adapter report scoring as `deferred` rather than silently omitting it.
  - [x] Keep input indexes read-only.
  - [x] Keep output path and `--format` behavior unchanged.
  - [x] Do not change existing exit-code semantics.
  - [x] Ensure `--include-priority` with `unknown`, `unavailable`, or `deferred` scoring does not change existing `--exit-code` behavior.

- [x] 10. Add downgrade and unknown tests. Requirements: 5, 9.
  - [x] Reduced before coverage caps added evidence.
  - [x] Reduced after coverage caps removed evidence.
  - [x] Both sides reduced coverage produces unknown or partial priority.
  - [x] Missing commit SHA emits commit-gap component.
  - [x] Missing, duplicate, unverified, or conflicting identity emits identity component and caps affected rows.
  - [x] Missing required schema makes scoring unavailable or fails sanitized.
  - [x] Missing optional schema caps affected evidence.
  - [x] Unavailable requested workflow emits unavailable component.
  - [x] Selector no-match is informational only under credible coverage and unknown under reduced coverage.
  - [x] Row and gap caps emit truncation component and incomplete report-level scoring.
  - [x] Row-level `complete: false` is emitted when row components include an unresolvable downgrade or unknown gap.

- [x] 11. Add evidence-quality tests. Requirements: 2, 3, 4, 9.
  - [x] Tier1 semantic evidence can rank stronger only under credible coverage and identity.
  - [x] Tier2 structural evidence maps to documented review priority.
  - [x] Tier3 syntax/textual evidence is capped at review-tier.
  - [x] Hash-only evidence is capped and cites limitations.
  - [x] Ambiguous and duplicate identities are capped.
  - [x] High fan-out noisy contract names stay `medium_review` or lower unless another rule applies.
  - [x] Existing reducer/report classifications and rule IDs are preserved.
  - [x] Every score component has a scoring rule ID, evidence tier, evidence reference or metadata source, and limitation.
  - [x] No-upgrade assertion: review-tier source rows cannot become stronger than documented static-evidence rules permit.
  - [x] A `NeedsReview` row cannot become `critical_review` without a separate documented non-noisy rule-backed component, and the allowed multi-component path is tested when such a rule exists.
  - [x] Rule catalog tests parse `rules/rule-catalog.yml` and verify every emitted scoring rule ID is documented.

- [x] 12. Add determinism and safety tests. Requirements: 7, 9.
  - [x] Identical inputs produce byte-stable Markdown.
  - [x] Identical inputs produce byte-stable JSON.
  - [x] Row ordering tie-breaks by severity, score, classification, evidence tier, source label, stable key, file path, line span, and ID.
  - [x] Component ordering is deterministic.
  - [x] Closed-vocabulary exhaustiveness for every emitted `severityHint`, `attentionLevel`, and section status.
  - [x] Opt-out output remains byte-identical when `--include-priority` is absent, unless an always-present additive section is explicitly versioned.
  - [x] Priority score type consistency: v1 fixtures emit `priorityScore: null` consistently where the field is present.
  - [x] `ComponentValue` is `null` for v1 ordinal-only components.
  - [x] Sidecar `rowId` values are stable across repeated identical inputs and do not use volatile database row IDs.
  - [x] Scoring `modelVersion` is asserted against a known constant.
  - [x] Markdown delimiter escaping covers pipes, line endings, brackets, parentheses, backticks, angle brackets, and allowed user-controlled labels or limitations.
  - [x] Markdown and JSON omit raw SQL, source snippets, literal values, config values, connection strings, raw URLs, hostnames, raw remotes, local absolute paths, private paths, and secrets.
  - [x] Input indexes are not mutated.

- [x] 13. Add docs and acceptance updates. Requirements: 1, 3, 8, 9.
  - [x] Update `docs/ACCEPTANCE.md` for scored release-review output.
  - [x] Update `docs/VALIDATION.md` only if validation workflow changes.
  - [x] Update README or command help with careful terminology.
  - [x] Document output schema additions and status vocabulary.
  - [x] Document release-review document version or additive compatibility behavior.
  - [x] Document scoring limitations and non-goals.

- [x] 14. Validate implementation PR. Requirements: 9.
  - [x] `dotnet build src/dotnet/TraceMap.sln`
  - [x] `dotnet test src/dotnet/TraceMap.sln`
  - [x] `./scripts/check-private-paths.sh`
  - [x] `git diff --check`
  - [x] Relevant pinned smoke checks from `docs/VALIDATION.md` if shared report, path, reverse, reducer, or adapter internals change.

## Deferred Follow-Ups

- Add opt-in scoring to combined diff.
- Add opt-in scoring to combined impact.
- Add opt-in scoring to route-flow and path reports.
- Add opt-in scoring to reverse reports.
- Add opt-in scoring to portfolio reports.
- Add a standalone `tracemap score` command only if report-integrated scoring proves insufficient.
- Add structured user-provided criticality metadata only through an explicit schema with safe rendering and no inference from names, paths, or prose.
