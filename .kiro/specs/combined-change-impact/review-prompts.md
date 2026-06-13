# Combined Change Impact Review Prompts

Use these prompts to review the combined change impact spec before implementation.

Spec files:

- `.kiro/specs/combined-change-impact/requirements.md`
- `.kiro/specs/combined-change-impact/design.md`
- `.kiro/specs/combined-change-impact/tasks.md`

## Opus Review Prompt

Review this TraceMap spec for a deterministic `tracemap impact` command.

Focus on:

- Whether the command has a clear product purpose distinct from `tracemap diff` and `tracemap paths`.
- Whether "impact" is worded safely as static change context rather than runtime proof.
- Whether the MVP can be implemented by reusing existing combined diff and path query code.
- Whether path context should be opt-in, default-on with small caps, or deferred.
- Whether endpoint, surface, edge, coverage, and path impact items are scoped correctly.
- Whether classifications avoid overclaiming under reduced coverage, source identity mismatch, duplicate stable identities, and Tier3 evidence.
- Whether path-context mapping from changed evidence to before/after queries is credible.
- Whether `PathContextUnavailable`, `NoImpactEvidence`, and `UnknownAnalysisGap` are clearly separated.
- Whether the JSON/Markdown contracts are deterministic and safe.
- Whether rule IDs and limitations are complete enough.
- Whether the task list is implementable in reviewable slices.

Please return:

1. Blockers.
2. High-value spec changes before implementation.
3. Scope cuts if the MVP is too large.
4. Missing tests.
5. Wording fixes where the spec overclaims.

## Sonnet Review Prompt

Review this spec as an implementation planner for the current TraceMap .NET codebase.

Focus on:

- The cleanest code seams in `TraceMap.Reporting` for reusing diff and path behavior.
- Whether internal builder APIs should be extracted before adding `impact`.
- Table/schema assumptions that may not match current combined indexes.
- Risks in stable ID construction.
- Risks in path-context planning for changed surfaces and edges.
- Selector parsing and cap semantics.
- Output determinism and private-data leakage risks.
- Minimal useful first PR slice.
- Tests needed to prove behavior without massive fixtures.

Please return a concrete implementation plan, risky assumptions, and suggested first PR boundaries.

## Qodo/Gemini Review Prompt

Review this spec for correctness and maintainability.

Look for:

- Any conclusion without evidence.
- Any evidence row without rule ID or evidence tier.
- Any new rule missing documented limitations.
- Runtime-impact wording that overclaims.
- Non-deterministic output risks.
- Unsafe output of raw SQL, URLs, snippets, config values, connection strings, or local paths.
- Path query explosion risks.
- Misleading `NoImpactEvidence` under reduced coverage.
- Source identity mismatch or checkout-root confusion.
- Duplicate stable identity handling gaps.
- Test gaps that could hide false positives.

Return actionable findings with section references and suggested fixes.

