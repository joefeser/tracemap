# Public Demo Workflow Review Prompts

Use these prompts to review the `public-demo-workflow` spec before implementation.

Spec files:

- `.kiro/specs/public-demo-workflow/requirements.md`
- `.kiro/specs/public-demo-workflow/design.md`
- `.kiro/specs/public-demo-workflow/tasks.md`

Related files/specs:

- `AGENTS.md`
- `docs/VALIDATION.md`
- `docs/NEXT_EXECUTION_REPORT.md`
- `scripts/smoke-combined-paths.sh`
- `scripts/smoke-open-source-repos.sh`
- `.kiro/specs/public-combined-path-validation/*`
- `.kiro/specs/multi-index-portfolio-report/*`
- `.kiro/specs/release-review-report/*`
- `.kiro/specs/combined-dependency-reporting/*`
- `.kiro/specs/combined-dependency-paths/*`
- `.kiro/specs/reverse-impact-query/*`
- `.kiro/specs/combined-dependency-diff/*`
- `.kiro/specs/combined-change-impact/*`

TraceMap principles:

- No conclusion without evidence.
- No evidence without a rule ID.
- No rule without documented limitations.
- No scan without repo and commit SHA.
- Partial analysis is useful, but must be labeled partial.
- Do not add LLM calls, embeddings, vector DBs, or prompt-based classification.
- Do not display raw SQL, snippets, literal values, connection strings, raw URLs, secrets, private paths, or local absolute paths in public reports.

## Opus Review Prompt

Review the TraceMap `public-demo-workflow` spec for merge readiness.

This spec defines a one-command public demo workflow for a clean checkout. It must stay a deterministic validation/demo layer over existing TraceMap commands, not a new analyzer, runtime topology engine, service catalog, release approval system, package vulnerability scanner, or AI classifier.

Please inspect:

- `.kiro/specs/public-demo-workflow/requirements.md`
- `.kiro/specs/public-demo-workflow/design.md`
- `.kiro/specs/public-demo-workflow/tasks.md`
- related public validation and reporting specs where needed
- current scripts and docs where needed

Review questions:

1. Is the default demo scope clear, free of external repository/sample-data access, and implementable from checked-in fixtures after local dependencies are restored?
2. Does the spec avoid duplicating `smoke-combined-paths.sh` while making the broader demo value clear?
3. Are scan/combine/report/paths/reverse/diff/impact/portfolio/release-review sections specified honestly enough, including unavailable/deferred states?
4. Are toolchain requirements and missing-tool behavior clear enough for macOS/Linux maintainers?
5. Are semantic assertions strong enough to prove evidence quality rather than file existence only?
6. Does every evidence-bearing assertion require rule IDs, evidence tiers, source labels, commit SHAs, and supporting IDs where available?
7. Are privacy/redaction requirements strong enough for public artifacts?
8. Are deterministic-output expectations realistic, especially when output roots or generated temp paths exist?
9. Are optional OSS repos safely scoped and pinned?
10. Are tasks reviewable and sliced well enough for implementation PRs?
11. What tests or validation commands are missing?

Return:

- Blocking issues with exact file/section references.
- Important non-blocking issues.
- Suggested spec edits.
- Scope cuts or PR slicing changes.
- Missing tests.
- Whether the spec is ready to implement after fixes.

## Sonnet Review Prompt

Review the `public-demo-workflow` spec as an implementation planner for the current TraceMap repo.

Focus on:

- The cleanest script/helper boundary.
- How to reuse existing smoke scripts without brittle duplication.
- Which checked-in samples should run by default.
- Which sections should be required versus `unavailable` or `deferred`.
- How to make JSON assertions deterministic and maintainable.
- How to handle Python/JVM prerequisites without making the default demo flaky.
- How to avoid raw local paths and unsafe fixture values in generated artifacts.
- How to keep the first implementation PR small but useful.

Return:

- Concrete implementation plan.
- Risky assumptions.
- Recommended first PR boundary.
- Files likely to change.
- Missing tests.
- Spec wording that should change before coding.

## Qodo/Gemini Review Prompt

Review the `public-demo-workflow` spec for correctness, safety, and maintainability.

Look for:

- Any public-demo claim that lacks evidence or rule IDs.
- Any hidden private repo/path assumption.
- Any network dependency in the default demo.
- Any overclaiming around runtime execution, production traffic, release approval, package vulnerabilities, ownership, or business impact.
- Any raw SQL, snippets, config values, connection strings, secrets, raw URLs, local paths, or private names that could leak.
- Any non-deterministic output risk from timestamps, filesystem order, temp paths, row order, or unstable IDs.
- Any section that can be silently skipped instead of marked `not_requested`, `unavailable`, or `deferred`.
- Any missing toolchain behavior that would leave users stuck.
- Any task slice that is too large for review.

Return actionable findings with exact section references and suggested fixes.
