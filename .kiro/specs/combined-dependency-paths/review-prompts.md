# Combined Dependency Paths Review Prompts

Use these prompts to review the spec before implementation.

Spec files:

- `.kiro/specs/combined-dependency-paths/requirements.md`
- `.kiro/specs/combined-dependency-paths/design.md`
- `.kiro/specs/combined-dependency-paths/tasks.md`

## Opus Review Prompt

Review this TraceMap spec for combined dependency path queries.

Focus on:

- Whether the MVP scope is implementable without scanner rewrites.
- Whether `tracemap paths` has a clear product purpose distinct from `tracemap report`.
- Whether the evidence graph model is deterministic and provenance-preserving.
- Whether `endpoint_matches` being reserved/unused while report and paths share one in-memory matcher is the right MVP ownership decision.
- Whether `CombinedEndpointMatcher` in `TraceMap.Reporting` is the right extraction target for combined N-way matching.
- Whether source-local symbol keys are sufficiently precise and collision behavior is honest.
- Whether limiting cross-source traversal to `EndpointMatch` is clear enough.
- Whether endpoint start nodes are correctly excluded from terminal `http-route`/`http-client` surface matches in default queries.
- Whether the contributing-source rule for `NoPathFound` vs `UnknownAnalysisGap` is deterministic.
- Whether endpoint-to-symbol-to-surface linking rules are too ambitious, too weak, or missing important evidence sources.
- Whether the selector set is the right first version.
- Whether path classifications map cleanly to TraceMap evidence tiers.
- Whether the design avoids overclaiming runtime behavior.
- Whether JSON/Markdown outputs are stable, safe, and useful.
- Whether the task list is ordered so implementation can be reviewed in slices.

Please return:

1. Blockers.
2. High-value changes before implementation.
3. Scope cuts if the spec is too large.
4. Missing tests.
5. Wording fixes where the spec overclaims.

## Sonnet Review Prompt

Review this spec as an implementation planner.

Focus on:

- Likely code seams in the current .NET solution.
- What should be refactored from `CombinedDependencyReport` before adding paths.
- Whether the behavior-preserving refactor slice is correctly isolated.
- Table/column assumptions that may not match the current combined SQLite schema.
- Edge cases in selector parsing and deterministic BFS.
- Risks in source-local symbol display-name joins.
- Risks in endpoint-match parity between `tracemap report` and `tracemap paths`.
- Risks in `maxFrontier` semantics and truncation reporting.
- Test fixtures needed to prove the feature.
- Risks around output determinism and private-data leakage.

Please return a concrete implementation plan, any risky assumptions, and a recommended first PR slice.

## Qodo/Gemini Review Prompt

Review this spec for correctness and maintainability risks.

Look for:

- Non-deterministic output risks.
- Path search false positives.
- Missing reduced-coverage caveats.
- Unsafe rendering of raw SQL, URLs, snippets, config values, connection strings, or local paths.
- Graph traversal cycle or explosion risks.
- Ambiguous selector behavior.
- Accidental cross-source symbol stitching.
- Divergence between endpoint matching in report and paths.
- JSON schema instability.
- Unclear ownership between report and paths code.

Please provide actionable findings with file/section references and suggested fixes.
