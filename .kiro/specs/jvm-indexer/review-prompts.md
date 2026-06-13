# JVM Indexer Review Prompts

Use these prompts after the initial spec is written and before implementation starts.

## Opus Product and Evidence Review

```text
You are reviewing the TraceMap JVM indexer Kiro spec.

Context:
- TraceMap is a deterministic repository indexer and contract-change reducer.
- Core rule: no conclusion without evidence, no evidence without a rule ID, no raw snippets by default.
- The JVM scanner must emit compatible TraceMap artifacts: scan-manifest.json, facts.ndjson, index.sqlite, report.md, logs/analyzer.log.
- It must use deterministic Java/Kotlin compiler, build-file, and syntax evidence, not LLMs or embeddings.
- The implementation target is src/jvm and should support Java and Kotlin together.
- Java semantic extraction is expected before Kotlin semantic extraction unless review finds a better first slice.
- The local scip-java repo is a reference for compiler-backed indexing ideas, but TraceMap should emit native facts rather than SCIP as the canonical artifact.

Files to review:
- .kiro/specs/jvm-indexer/requirements.md
- .kiro/specs/jvm-indexer/design.md
- .kiro/specs/jvm-indexer/tasks.md

Please review for:
- Missing user workflows.
- Requirements that overclaim impact, runtime behavior, dependency injection, reflection, annotation processors, generated sources, or serializer mappings.
- Places where evidence tier or coverage labeling is ambiguous.
- Scope that is too large for a first JVM implementation.
- Missing non-goals or JVM-specific limitations.
- Contract reducer, export, or combine compatibility gaps.
- Whether Java and Kotlin should remain together under src/jvm for this phase.

Return:
- Blockers.
- Recommended scope cuts.
- Requirement wording changes.
- Questions that must be answered before implementation.
```

## Sonnet Implementation Review

```text
You are reviewing the TraceMap JVM indexer Kiro spec for implementation feasibility.

Context:
- The implementation will live under src/jvm as a sibling to src/dotnet and src/typescript.
- It should borrow ideas from a local scip-java checkout: compiler-backed Java extraction, clear separation between extraction and aggregation, dependency coordinate mapping, and honest reduced-coverage behavior when builds/classpaths are incomplete.
- It should emit TraceMap-compatible facts and SQLite tables.
- It should parse Maven and Gradle local files, but scan should not run Maven/Gradle target builds or download dependencies.

Files to review:
- .kiro/specs/jvm-indexer/requirements.md
- .kiro/specs/jvm-indexer/design.md
- .kiro/specs/jvm-indexer/tasks.md

Please review for:
- Incorrect Java compiler API assumptions.
- Kotlin compiler/API risks that should be scoped differently.
- Maven/Gradle parser scope that is too broad or too vague.
- Hard implementation areas that need smaller task slices.
- Missing modules or interfaces in the proposed package structure.
- SQLite/schema compatibility risks.
- Test fixture gaps.
- Tasks that should move earlier or later.

Return:
- Implementation blockers.
- Proposed task reordering.
- MVP slice recommendation.
- Specific design edits before coding.
```
