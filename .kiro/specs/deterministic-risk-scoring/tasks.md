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

The first implementation PR should prefer ordinal priority if numeric score weights are not fully settled. If numeric scoring is emitted, every component value and aggregation rule must be visible in JSON and covered by tests.

## Implementation Tasks

- [ ] 1. Confirm MVP terminology and option shape. Requirements: 1, 3, 4.
  - [ ] Confirm feature name in UI/report copy: deterministic review priority scoring.
  - [ ] Confirm first workflow target: `tracemap release-review`.
  - [ ] Confirm opt-in flag name, suggested as `--include-priority`.
  - [ ] Confirm JSON model version, suggested as `review-priority.v1`.
  - [ ] Confirm whether v1 emits ordinal-only priority or numeric `priorityScore`.
  - [ ] Treat ordinal-vs-numeric as a hard gate before implementing component values and aggregation.
  - [ ] If numeric scoring is chosen, define and document the cap-to-score function before implementation.
  - [ ] Document the ordinal-vs-numeric decision in the rule catalog before component-value or aggregation code is written.
  - [ ] Confirm release-review JSON compatibility choice: opt-in sidecar with byte-identical opt-out, or always-present additive `not_requested` section with version/compatibility policy.
  - [ ] Confirm closed vocabularies for `severityHint`, `attentionLevel`, and section status.
  - [ ] Confirm scoring is not a release gate, runtime risk predictor, security scanner, or business-criticality model.

- [ ] 2. Add rule catalog entries before emitting output. Requirements: 2, 4, 5, 8.
  - [ ] Add `review.priority.component.v1` or equivalent component rule.
  - [ ] Add `review.priority.aggregate.v1` or equivalent aggregation rule.
  - [ ] Add downgrade rules for coverage, identity, commit SHA, schema, fallback evidence, and truncation.
  - [ ] Add unavailable-workflow and selector rules.
  - [ ] Document ordinal precedence or numeric component weights, including cap and unknown behavior.
  - [ ] Document inputs, outputs, evidence tiers, downgrade behavior, and limitations for every scoring rule.
  - [ ] Document that scoring is deterministic static review prioritization, not runtime probability, AI judgment, release approval, vulnerability scanning, or compliance.

- [ ] 3. Add scoring models. Requirements: 2, 3, 4, 7.
  - [ ] Define report-level scoring summary model.
  - [ ] Define row-level scoring annotation model.
  - [ ] Define score component model.
  - [ ] Define source evidence reference model.
  - [ ] Define section status and completeness fields.
  - [ ] Use empty arrays and nulls consistently.
  - [ ] Use canonical sorted key/value metadata arrays.
  - [ ] Avoid unordered dictionaries in emitted scoring JSON.
  - [ ] Model checklist-derived components as references to finding/gap IDs, or as documented metadata-derived components when no tiered source evidence exists.

- [ ] 4. Add evidence adapter for release-review. Requirements: 1, 2, 6.
  - [ ] Adapt release-review summary, source coverage, top changed surfaces, contract impact, API/DTO changes, SQL/schema impact, package impact, path context, reverse context, gaps, checklist items, and limitations into scorable evidence records.
  - [ ] Preserve underlying classifications, rule IDs, evidence tiers, file spans, source labels, commit SHAs, supporting fact IDs, edge IDs, path IDs, and limitations.
  - [ ] Preserve section statuses such as `available`, `not_requested`, `unavailable`, `deferred`, and `truncated`.
  - [ ] Do not read raw source files or raw input property bags.
  - [ ] Do not call scanner or reducer code paths that mutate inputs.

- [ ] 5. Implement deterministic component rules. Requirements: 2, 4, 5.
  - [ ] Map strong static evidence classifications to base review-priority components.
  - [ ] Map review-tier, syntax-only, textual, hash-only, ambiguous, duplicate, name-only, fallback, and high-fan-out evidence to capped review components.
  - [ ] Map evidence tiers to documented strength or cap components.
  - [ ] Map deterministic fan-out counts to fan-out components.
  - [ ] Map path and reverse context to bounded-context components.
  - [ ] Map coverage, identity, commit SHA, schema, unavailable workflow, selector, and truncation gaps to downgrade or unknown components.
  - [ ] Keep positive evidence and downgrade evidence visible as separate components.
  - [ ] Use release-review's existing required-schema behavior for v1 and optional-schema gaps for precision limitations.

- [ ] 6. Implement aggregation and severity mapping. Requirements: 3, 4, 5.
  - [ ] Aggregate row components deterministically into `severityHint`.
  - [ ] If numeric scoring is emitted, aggregate component values into `priorityScore` with documented fixed weights.
  - [ ] If numeric scoring is emitted, apply cap components through a documented tested function such as `effectiveScore = min(rawScore, capScore)`.
  - [ ] Apply cap rules after positive components.
  - [ ] Emit `unknown` with `priorityScore: null` when limiting gaps prevent a credible score.
  - [ ] Aggregate report-level `attentionLevel` from row severities, section completeness, and gap components.
  - [ ] Mark report-level scoring incomplete when requested evidence is unavailable, reduced, or truncated.
  - [ ] Sort rows, components, evidence references, and limitations deterministically.

- [ ] 7. Add release-review Markdown output. Requirements: 1, 6, 7.
  - [ ] Do not merge Markdown scoring output until Task 2 rule catalog entries are complete.
  - [ ] Add a Review Priority section.
  - [ ] Render status, model version, attention level, completeness, and limiting gap categories.
  - [ ] Render top row priorities with safe evidence references and component summaries.
  - [ ] Render no raw SQL, snippets, literal values, config values, connection strings, raw URLs, hostnames, remotes, local absolute paths, private paths, or secrets.
  - [ ] Escape Markdown table and link delimiters.
  - [ ] Reuse `CombinedReportHelpers.Cell`, `CombinedReportHelpers.SafePath`, and related shared helpers, or extract equivalent shared helpers before adding scoring-specific rendering.
  - [ ] Include limitations that explain static review prioritization boundaries.

- [ ] 8. Add release-review JSON output. Requirements: 1, 3, 4, 7.
  - [ ] Do not merge JSON scoring output until Task 2 rule catalog entries are complete.
  - [ ] Add top-level `reviewPriority` JSON.
  - [ ] Prefer a sidecar `reviewPriorityRows` array keyed by stable row IDs unless nested annotations are explicitly versioned.
  - [ ] Preserve release-review JSON byte identity when `--include-priority` is absent, or bump/document the release-review document version for an always-present additive section.
  - [ ] Include section statuses and completeness.
  - [ ] Include component arrays with rule IDs, evidence tiers, source evidence references, values, directions, and limitations.
  - [ ] Preserve underlying report JSON fields and classifications.
  - [ ] Ensure JSON is byte-stable for identical inputs.

- [ ] 9. Add CLI wiring and compatibility behavior. Requirements: 1, 3, 6.
  - [ ] Parse `--include-priority` for release-review.
  - [ ] Ensure scoring is not run unless requested if v1 is opt-in.
  - [ ] Ensure workflows that receive requested scoring before they have an adapter report scoring as `deferred` rather than silently omitting it.
  - [ ] Keep input indexes read-only.
  - [ ] Keep output path and `--format` behavior unchanged.
  - [ ] Do not change existing exit-code semantics.
  - [ ] Ensure `--include-priority` with `unknown`, `unavailable`, or `deferred` scoring does not change existing `--exit-code` behavior.

- [ ] 10. Add downgrade and unknown tests. Requirements: 5, 9.
  - [ ] Reduced before coverage caps added evidence.
  - [ ] Reduced after coverage caps removed evidence.
  - [ ] Both sides reduced coverage produces unknown or partial priority.
  - [ ] Missing commit SHA emits commit-gap component.
  - [ ] Missing, duplicate, unverified, or conflicting identity emits identity component and caps affected rows.
  - [ ] Missing required schema makes scoring unavailable or fails sanitized.
  - [ ] Missing optional schema caps affected evidence.
  - [ ] Unavailable requested workflow emits unavailable component.
  - [ ] Selector no-match is informational only under credible coverage and unknown under reduced coverage.
  - [ ] Row and gap caps emit truncation component and incomplete report-level scoring.
  - [ ] Row-level `complete: false` is emitted when row components include an unresolvable downgrade or unknown gap.

- [ ] 11. Add evidence-quality tests. Requirements: 2, 3, 4, 9.
  - [ ] Tier1 semantic evidence can rank stronger only under credible coverage and identity.
  - [ ] Tier2 structural evidence maps to documented review priority.
  - [ ] Tier3 syntax/textual evidence is capped at review-tier.
  - [ ] Hash-only evidence is capped and cites limitations.
  - [ ] Ambiguous and duplicate identities are capped.
  - [ ] High fan-out noisy contract names stay `medium_review` or lower unless another rule applies.
  - [ ] Existing reducer/report classifications and rule IDs are preserved.
  - [ ] Every score component has a scoring rule ID, evidence tier, evidence reference or metadata source, and limitation.
  - [ ] No-upgrade assertion: review-tier source rows cannot become stronger than documented static-evidence rules permit.
  - [ ] A `NeedsReview` row cannot become `critical_review` without a separate documented non-noisy rule-backed component, and the allowed multi-component path is tested when such a rule exists.
  - [ ] Rule catalog tests parse `rules/rule-catalog.yml` and verify every emitted scoring rule ID is documented.

- [ ] 12. Add determinism and safety tests. Requirements: 7, 9.
  - [ ] Identical inputs produce byte-stable Markdown.
  - [ ] Identical inputs produce byte-stable JSON.
  - [ ] Row ordering tie-breaks by severity, score, classification, evidence tier, source label, stable key, file path, line span, and ID.
  - [ ] Component ordering is deterministic.
  - [ ] Closed-vocabulary exhaustiveness for every emitted `severityHint`, `attentionLevel`, and section status.
  - [ ] Opt-out output remains byte-identical when `--include-priority` is absent, unless an always-present additive section is explicitly versioned.
  - [ ] Priority score type consistency: fixtures either emit `priorityScore: null` consistently for ordinal mode or integer scores consistently for numeric mode.
  - [ ] `ComponentValue` is `null` for ordinal-only components or integer for numeric components according to the documented model.
  - [ ] Sidecar `rowId` values are stable across repeated identical inputs and do not use volatile database row IDs.
  - [ ] Scoring `modelVersion` is asserted against a known constant.
  - [ ] Markdown delimiter escaping covers pipes, line endings, brackets, parentheses, backticks, angle brackets, and allowed user-controlled labels or limitations.
  - [ ] Markdown and JSON omit raw SQL, source snippets, literal values, config values, connection strings, raw URLs, hostnames, raw remotes, local absolute paths, private paths, and secrets.
  - [ ] Input indexes are not mutated.

- [ ] 13. Add docs and acceptance updates. Requirements: 1, 3, 8, 9.
  - [ ] Update `docs/ACCEPTANCE.md` for scored release-review output.
  - [ ] Update `docs/VALIDATION.md` only if validation workflow changes.
  - [ ] Update README or command help with careful terminology.
  - [ ] Document output schema additions and status vocabulary.
  - [ ] Document release-review document version or additive compatibility behavior.
  - [ ] Document scoring limitations and non-goals.

- [ ] 14. Validate implementation PR. Requirements: 9.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`
  - [ ] `dotnet test src/dotnet/TraceMap.sln`
  - [ ] `./scripts/check-private-paths.sh`
  - [ ] `git diff --check`
  - [ ] Relevant pinned smoke checks from `docs/VALIDATION.md` if shared report, path, reverse, reducer, or adapter internals change.

## Deferred Follow-Ups

- Add opt-in scoring to combined diff.
- Add opt-in scoring to combined impact.
- Add opt-in scoring to route-flow and path reports.
- Add opt-in scoring to reverse reports.
- Add opt-in scoring to portfolio reports.
- Add a standalone `tracemap score` command only if report-integrated scoring proves insufficient.
- Add structured user-provided criticality metadata only through an explicit schema with safe rendering and no inference from names, paths, or prose.
