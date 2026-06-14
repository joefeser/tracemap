# Reverse Impact Query Review Prompts

## Kiro Web Review Prompt

Review the TraceMap reverse-impact-query spec on branch `codex/reverse-impact-query-spec`.

Scope: spec merge readiness only, not implementation.

Files to inspect:

- `.kiro/specs/reverse-impact-query/requirements.md`
- `.kiro/specs/reverse-impact-query/design.md`
- `.kiro/specs/reverse-impact-query/tasks.md`
- `rules/rule-catalog.yml`
- `docs/ACCEPTANCE.md`
- `docs/VALIDATION.md`
- Existing related specs:
  - `.kiro/specs/combined-dependency-paths/*`
  - `.kiro/specs/combined-change-impact/*`

TraceMap principles:

- No conclusion without evidence.
- No evidence without a rule ID.
- No rule without documented limitations.
- No scan without repo and commit SHA.
- Partial analysis is useful, but must be labeled partial.
- Do not add LLM calls, embeddings, vector DBs, or prompt-based classification.

Review questions:

1. Is `tracemap reverse` product-distinct from `tracemap paths` and `tracemap impact`, or does the spec overlap too much?
2. Does the spec avoid overclaiming runtime usage, business impact, SQL execution, or production traffic?
3. Are selectors (`--surface`, exact-case-insensitive `--surface-name`, case-insensitive `--source`, `--to`) precise enough to implement without drift?
4. Are default caps (`--max-depth`, `--max-frontier`, `--max-surfaces`, `--max-roots`, `--max-paths-per-root`, `--max-gaps`) bounded enough for large combined indexes?
5. Are `NoReversePathEvidence`, `UnknownAnalysisGap`, `SelectorNoMatch`, and `TruncatedByLimit` clearly separated?
6. Does reduced coverage correctly prevent strong no-path conclusions?
7. Are stable IDs actually stable, or could they depend on row order, display names, raw paths, or volatile combined IDs?
8. Can raw SQL, URLs, config values, snippets, connection strings, repository remotes, or local absolute paths leak into Markdown/JSON?
9. Are the proposed `combined.reverse.*.v1` rule IDs complete, and are propagated `combined.paths.*.v1` rule IDs handled correctly?
10. Is the implementation task list sliced into reviewable PRs, especially the reusable path graph refactor?
11. Are tests sufficient for the risk level?
12. Should `--to symbols`, `--to sources`, and `--to all` be MVP or follow-up?

Return:

- Blocking spec issues with exact file/line references.
- Important non-blocking issues.
- Suggested concrete edits.
- Whether the spec is mergeable after fixes.
