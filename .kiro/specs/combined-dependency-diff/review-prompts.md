# Combined Dependency Diff Review Prompts

Use these prompts to review the combined dependency diff spec before implementation. This spec is intended to stand on its own even when no external model review is available.

## Opus-Style Architecture Review

```text
You are reviewing a Kiro spec for TraceMap, an open-source deterministic repository indexer and static dependency evidence tool.

Please review:
- .kiro/specs/combined-dependency-diff/requirements.md
- .kiro/specs/combined-dependency-diff/design.md
- .kiro/specs/combined-dependency-diff/tasks.md

TraceMap principles:
- No conclusion without evidence.
- No evidence without a rule ID.
- No rule without documented limitations.
- No scan without repo and commit SHA.
- Partial analysis is useful, but must be labeled partial.
- Do not add LLM calls, embeddings, vector databases, or prompt-based classification.

Focus on:
1. Whether the MVP scope is the right next step after combine/report/paths.
2. Whether path diffing should be opt-in or default.
3. Whether the source pairing rules are safe enough for multi-repo comparison.
4. Whether stable identity keys avoid both false positives from row churn and false negatives from over-normalization.
5. Whether coverage-aware downgrade rules prevent false "removed" or "added" claims.
6. Whether classifications are precise and honest.
7. Whether evidence/rule ID requirements are strong enough.
8. Whether Markdown and JSON contracts are useful for humans and automation.
9. Whether safety rules prevent leaking raw SQL, URLs, config values, source snippets, local absolute paths, or private repo names.
10. Whether implementation tasks are ordered for a reviewable PR.

Please return:
- Blocking issues.
- Important non-blocking issues.
- Suggestions.
- Anything you would remove from MVP.
- Anything missing that would cause implementation ambiguity.
```

## Sonnet-Style Implementation Review

```text
Please review the combined dependency diff spec as an implementation plan.

Look for:
- CLI ambiguity.
- Data model ambiguity.
- Schema assumptions that may not match current combined indexes.
- Places where existing report/path code should be reused instead of duplicated.
- Stable key rules that are too vague to implement deterministically.
- Tests that are missing for edge cases.
- Performance traps, especially around --include-paths.
- Output contract fields that will be hard to keep stable.
- Places where coverage semantics could produce incorrect classifications.

Please be concrete. Reference the exact requirement, design section, or task bullet that needs changes.
```

## Qodo/Gemini-Style Bug Hunt

```text
Act as a skeptical reviewer for the combined dependency diff spec.

Find likely bugs before implementation:
- False Added/Removed results caused by reduced coverage.
- Stable keys that depend on volatile row IDs or local paths.
- Source pairing mistakes when labels match but repos differ.
- Path comparison that claims unchanged paths when --include-paths was not run.
- Leaks of raw SQL, URLs, config values, source snippets, local absolute paths, or private repo names.
- Duplicate identity handling that hides evidence.
- Selector filters that apply to only one snapshot.
- JSON instability from timestamps, dictionary ordering, or unordered sets.
- Markdown injection or broken tables.
- Large graph/path explosions.

Return issues ordered by severity with suggested spec edits.
```

## Self-Review Checklist

- [ ] Does the spec clearly say this is static evidence diffing, not runtime impact analysis?
- [ ] Does every diff claim require a diff rule ID and source evidence rule IDs where available?
- [ ] Are reduced coverage and unknown commit SHAs handled without overclaiming?
- [ ] Are source pairing rules exact and deterministic?
- [ ] Are endpoint, surface, edge, and path identity keys stable enough?
- [ ] Is path diffing explicitly opt-in?
- [ ] Are outputs byte-stable?
- [ ] Are unsafe values excluded by default?
- [ ] Are CLI outputs and file/directory behavior specified?
- [ ] Are tests sufficient for false added/removed results?
- [ ] Is there a clear implementation order?
