# Deterministic Review Priority Scoring Implementation State

Status: spec-ready

Branch/PR: `codex/spec-deterministic-risk-scoring`

Issue: `#30`

## Current Scope

This branch is spec-only. It defines requirements, design, tasks, and review state for deterministic evidence-backed review priority scoring. It does not implement product code.

## Scope Decisions

- The folder and branch keep the issue-requested `deterministic-risk-scoring` slug, but product-facing terminology should be "deterministic review priority scoring" with fields such as `reviewPriority`, `severityHint`, and `attentionLevel`.
- Preferred terminology is `reviewPriority`, `severityHint`, and `attentionLevel`.
- The feature should prioritize review attention over existing TraceMap evidence rather than claim runtime, production, security, compliance, or business risk.
- Release-review is the recommended first implementation target because it already composes diff, impact, path/reverse, portfolio-adjacent context, gaps, and checklist evidence.
- The first implementation slice should use an explicit opt-in flag unless implementation review decides default scoring is safer and fully versioned.
- Numeric scoring is allowed only if every component and aggregation rule is visible, documented, deterministic, and tested. Ordinal-only priority is acceptable for v1.
- The ordinal-vs-numeric choice is a hard implementation gate before component values or aggregation are coded.
- Release-review scoring should prefer opt-in sidecar JSON so `--include-priority` opt-out output can remain byte-identical; an always-present additive section requires an explicit version or compatibility decision.
- Scoring must emit downgrade and unknown components rather than hiding uncertainty.
- Release-review v1 should use the existing status vocabulary and defer `not_supported` until a workflow has a real unsupported-scoring path.
- Scoring output should reuse shared helpers such as `CombinedReportHelpers.Cell`, `SafePath`, and `SortedMetadata` or refactored equivalents.

## Oddities and Constraints

- Keep implementation tasks unchecked until product code lands.
- Mark only spec authoring and completed review/validation tasks checked.
- Do not include raw local paths, raw remotes, private paths, snippets, raw SQL/config values, URLs, hostnames, or secrets in committed spec artifacts or PR text.
- Do not add LLM, embedding, vector database, or prompt-based classification behavior to TraceMap core.

## Validation

- Kiro Opus spec review: completed with reduced coverage because the wrapper reported denied shell access; Medium+ findings patched in this branch.
- Kiro Sonnet spec review: completed with full coverage; blocking/important ambiguity findings patched in this branch.
- Final Kiro re-review after patches: completed with full coverage; no blocking issues.
- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.

## Follow-Up Items

- Address final re-review low-risk implementation notes during Task 1 before product implementation begins, including the `priorityScore` null-vs-absent decision and ordinal-mode Markdown score-column rendering.
- Open a ready PR to `dev` with `Refs #30`.
