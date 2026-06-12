# TypeScript Indexer Review Prompts

Use these prompts after the initial spec is written and before implementation starts.

## Opus Product and Evidence Review

```text
You are reviewing the TraceMap TypeScript indexer Kiro spec.

Context:
- TraceMap is a deterministic repository indexer and contract-change reducer.
- Core rule: no conclusion without evidence, no evidence without a rule ID, no raw snippets by default.
- The TypeScript scanner must emit compatible TraceMap artifacts: scan-manifest.json, facts.ndjson, index.sqlite, report.md, logs/analyzer.log.
- It must use deterministic TypeScript compiler/syntax evidence, not LLMs or embeddings.

Files to review:
- .kiro/specs/typescript-indexer/requirements.md
- .kiro/specs/typescript-indexer/design.md
- .kiro/specs/typescript-indexer/tasks.md

Please review for:
- Missing user workflows.
- Requirements that overclaim impact or runtime behavior.
- Places where evidence tier or coverage labeling is ambiguous.
- Scope that is too large for a first TypeScript implementation.
- Missing non-goals or limitations.
- Contract reducer compatibility gaps.

Return:
- Blockers.
- Recommended scope cuts.
- Requirement wording changes.
- Questions that must be answered before implementation.
```

## Sonnet Implementation Review

```text
You are reviewing the TraceMap TypeScript indexer Kiro spec for implementation feasibility.

Context:
- The implementation will live under src/typescript as a sibling to src/dotnet.
- It should borrow techniques from /Users/josephfeser/src/gh-joe/scip-typescript, especially TypeScript project loading, project references, package identity, file-size limits, source caching, and symbol descriptors.
- It should emit TraceMap-compatible facts and SQLite tables.

Files to review:
- .kiro/specs/typescript-indexer/requirements.md
- .kiro/specs/typescript-indexer/design.md
- .kiro/specs/typescript-indexer/tasks.md

Please review for:
- Incorrect TypeScript compiler API assumptions.
- Hard implementation areas that need smaller task slices.
- Missing modules or interfaces in the proposed package structure.
- SQLite/schema compatibility risks.
- Test fixture gaps.
- Any tasks that should move earlier or later.

Return:
- Implementation blockers.
- Proposed task reordering.
- MVP slice recommendation.
- Specific design edits before coding.
```

