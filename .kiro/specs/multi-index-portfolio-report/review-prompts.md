# Multi-Index Portfolio Dependency Report Review Prompts

Use these prompts to review the multi-index portfolio report spec before implementation.

Spec files:

- `.kiro/specs/multi-index-portfolio-report/requirements.md`
- `.kiro/specs/multi-index-portfolio-report/design.md`
- `.kiro/specs/multi-index-portfolio-report/tasks.md`

Related specs:

- `.kiro/specs/combined-dependency-reporting/*`
- `.kiro/specs/combined-dependency-diff/*`
- `.kiro/specs/combined-change-impact/*`
- `.kiro/specs/combined-dependency-paths/*`
- `.kiro/specs/reverse-impact-query/*`
- `.kiro/specs/release-review-report/*`
- `.kiro/specs/sql-dependency-surfaces/*`

TraceMap principles:

- No conclusion without evidence.
- No evidence without a rule ID.
- No rule without documented limitations.
- No scan without repo and commit SHA.
- Failed build is not a clean repo.
- Partial analysis is useful, but must be labeled partial.
- Prefer deterministic, testable extractors and reducers.
- Do not add LLM calls, embeddings, vector databases, or prompt-based classification.
- Do not display raw SQL, snippets, literal values, config values, connection strings, raw URLs, raw secrets, private paths, or local absolute paths in public reports.

## Opus Review Prompt

Review the TraceMap `multi-index-portfolio-report` spec for merge readiness.

This spec defines a deterministic portfolio dependency report across many TraceMap indexes and combined indexes. It must remain a composition/reporting layer over existing evidence, not a runtime topology engine, service catalog ownership inference tool, release approval system, package compatibility analyzer, vulnerability analyzer, or AI classifier.

Please inspect:

- `.kiro/specs/multi-index-portfolio-report/requirements.md`
- `.kiro/specs/multi-index-portfolio-report/design.md`
- `.kiro/specs/multi-index-portfolio-report/tasks.md`
- related combined report, diff, impact, path, reverse, and release-review specs
- current CLI/reporting code where needed

Review questions:

1. Is the portfolio report distinct enough from `combine`, `report`, `diff`, `impact`, `paths`, `reverse`, and `release-review` while clearly reusing them?
2. Does the MVP scope avoid building a new scanner, graph database, runtime topology system, service catalog, or impact classifier?
3. Are single-language indexes, combined indexes, and manifest-driven inputs specified clearly enough?
4. Are source identity, commit SHA, scanner version, build status, coverage, and extractor provenance requirements strong enough?
5. Are cross-source endpoint alignment and shared-surface grouping safe, deterministic, and limited to static evidence?
6. Does before/after portfolio comparison avoid arbitrary source pairing and coverage-overclaiming?
7. Are optional path, reverse, impact, and release-review context sections bounded and clearly unavailable/deferred when incompatible?
8. Are rule ID expectations and documented limitations complete before implementation?
9. Are Markdown and JSON contracts deterministic, byte-stable, and safe from private-data leaks?
10. Are row caps, selector behavior, truncation, and rollup precedence clear enough?
11. Are PR slices reviewable and small enough?
12. What tests are missing?

Return:

- Blocking issues with file/section references.
- Important non-blocking issues.
- Suggested spec edits.
- Scope cuts or PR slicing changes.
- Missing tests.
- Whether the spec is ready to implement after fixes.

## Sonnet Review Prompt

Review the `multi-index-portfolio-report` spec as an implementation planner for the current TraceMap .NET codebase.

Focus on:

- The cleanest code seams in `TraceMap.Reporting` for reusing combined report, diff, impact, path, reverse, and release-review behavior.
- Whether a manifest-first MVP is the right boundary.
- How to support both single-language and combined indexes without duplicating readers.
- How to expand combined `index_sources` while preserving container/source provenance.
- Whether endpoint matching can be extracted cleanly from combined reporting.
- How to represent shared portfolio surfaces without implying runtime coupling.
- How to model unavailable/deferred optional sections without fake evidence.
- Stable ID construction for sources, surfaces, endpoint findings, shared groups, gaps, and rollups.
- Deterministic JSON and Markdown writing.
- Safe rendering helper reuse for paths, URLs, SQL, config, secrets, and Markdown escaping.
- Minimal first PR with high value and low blast radius.
- Tests likely to catch overclaiming, byte churn, source identity bugs, and private-data leakage.
- Validation commands and smoke checks likely to fail.

Return:

- Concrete implementation plan.
- Risky assumptions.
- Recommended first PR boundary.
- Reusable APIs to extract first.
- Missing tests.
- Any spec wording that should change before coding.

## Qodo/Gemini Review Prompt

Review the `multi-index-portfolio-report` spec for correctness, safety, and maintainability.

Look for:

- Any conclusion without evidence.
- Any evidence row, group, gap, rollup, or checklist item without a rule ID or evidence tier.
- Any new rule missing documented limitations.
- Runtime topology, production traffic, deployment, ownership, auth, SQL execution, package compatibility, vulnerability, release approval, or business impact overclaims.
- LLM/generated narrative, embedding/vector, prompt classification, or hidden risk-score creep.
- Unclear handling of single-language versus combined indexes.
- Ambiguous source pairing in before/after manifests.
- Grouping that could be mistaken for runtime dependency proof.
- Misleading `NoActionableEvidence` or `NoPortfolioChangeEvidence` under reduced coverage.
- Path/reverse/impact/release-review optional sections silently omitted instead of `not_requested`, `unavailable`, or `deferred`.
- Raw SQL, snippets, literal values, config values, connection strings, raw URLs, raw secrets, local paths, or private paths leaking to Markdown, JSON, or stderr.
- Non-deterministic output risks from timestamps, unordered dictionaries, filesystem order, row order, or unstable IDs.
- Caps that omit rows without truncation gaps and omitted counts.
- Missing tests that could hide false confidence.

Return actionable findings with exact section references and suggested fixes.
