# Deterministic Review Priority Scoring Implementation State

Status: implemented-release-review-v1

Post-promotion note: PR #227 implemented opt-in deterministic release-review
priority scoring and PR #247 promoted it to `main`.

Branch/PR: `codex/implement-deterministic-risk-scoring` / https://github.com/joefeser/tracemap/pull/227

Issue: `#30`

## Current Scope

This branch implements the first PR boundary: release-review opt-in deterministic review priority scoring only.

Implemented:

- Shared review-priority models, closed vocabularies, constants, and release-review scorer under `TraceMap.Reporting/ReviewPriority`.
- `tracemap release-review --include-priority` CLI flag.
- Opt-in Markdown Review Priority section.
- Opt-in JSON sidecar fields: `reviewPriority` and `reviewPriorityRows`.
- Ordinal-only v1 behavior with `priorityScore: null` and component `componentValue: null`.
- Rule catalog entries for `review.priority.*.v1` scoring rules.
- Focused release-review tests for opt-in output, opt-out compatibility, evidence discipline, reduced coverage/truncation, safety, read-only inputs, CLI flag parsing, and rule catalog coverage.

Not implemented in this PR:

- Scoring for diff, impact, paths, reverse, portfolio, or standalone score commands.
- Numeric scoring, weights, or score bands.
- New scanner facts or source rescanning for scoring.

## Scope Decisions

- The folder and branch keep the issue-requested `deterministic-risk-scoring` slug, but product-facing terminology should be "deterministic review priority scoring" with fields such as `reviewPriority`, `severityHint`, and `attentionLevel`.
- Preferred terminology is `reviewPriority`, `severityHint`, and `attentionLevel`.
- The feature should prioritize review attention over existing TraceMap evidence rather than claim runtime, production, security, compliance, or business risk.
- Release-review is the recommended first implementation target because it already composes diff, impact, path/reverse, portfolio-adjacent context, gaps, and checklist evidence.
- The first implementation slice uses explicit opt-in flag `--include-priority`.
- V1 scoring is ordinal-only. Numeric `priorityScore` weights are deferred to a future scoring model version.
- V1 emits `priorityScore: null` where the schema includes that field and emits component `componentValue: null`.
- Release-review scoring uses opt-in sidecar JSON so `--include-priority` opt-out output remains byte-identical with pre-feature output.
- Scoring must emit downgrade and unknown components rather than hiding uncertainty.
- Public-surface and cross-repo reach components must derive only from existing static TraceMap evidence and must not infer runtime exposure, deployment topology, ownership, or business reach.
- Release-review v1 should use the existing status vocabulary and defer `not_supported` until a workflow has a real unsupported-scoring path.
- Scoring output should reuse shared helpers such as `CombinedReportHelpers.Cell`, `SafePath`, and `SortedMetadata` or refactored equivalents.

## Oddities and Constraints

- Implementation task checkboxes are marked complete for the first PR boundary after product code and focused tests landed.
- Do not include raw local paths, raw remotes, private paths, snippets, raw SQL/config values, URLs, hostnames, or secrets in committed spec artifacts or PR text.
- Do not add LLM, embedding, vector database, or prompt-based classification behavior to TraceMap core.

## Validation

- Kiro Opus spec review: completed on the spec-only branch with reduced coverage because the wrapper reported denied shell access; Medium+ findings patched there.
- Kiro Sonnet spec review: completed on the spec-only branch with full coverage; blocking/important ambiguity findings patched there.
- Final Kiro spec re-review after patches: completed with full coverage; no blocking issues.
- Product implementation Kiro review: completed with reduced coverage because Kiro reported denied tool access. Artifacts saved under `.tmp/kiro-reviews/deterministic-risk-scoring/2026-06-20T172625-480Z-implementation-claude-sonnet-4.6.*`.
- Product implementation Kiro re-review cycle 1: completed with full coverage. Blocking findings patched: checklist `must_review` mapping, opt-out compatibility test coverage, attention aggregation cleanup, rule-catalog component-kind wording.
- Product implementation Kiro re-review cycle 2: completed with reduced coverage because Kiro reported denied tool access. Reported remaining test-coverage/cap/selector concerns were patched after the final allowed cycle: scored byte-stability, Markdown escaping through scoring renderer, closed vocabularies/model version, cap semantics, selector no-match under reduced coverage, and reserved fan-out documentation.
- `dotnet build src/dotnet/TraceMap.sln`: passed.
- `dotnet test src/dotnet/TraceMap.sln`: passed, 534 tests.
- Focused release-review tests: passed, 23 tests.
- Release-review CLI/sample smoke: passed by scanning `samples/modern-sample` twice into `/tmp/tracemap-review-priority-smoke` and running `release-review --include-priority`; verified `release-review.md`, `release-review.json`, `reviewPriority`, `reviewPriorityRows`, and `## Review Priority`.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.

## Follow-Up Items

- Run the repo-local Agent Control PR review loop and patch only still-actionable findings.
