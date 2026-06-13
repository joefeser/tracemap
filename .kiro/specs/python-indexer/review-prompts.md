# Python Indexer Review Prompts

Use these prompts after the initial spec is written and before implementation starts.

## Opus Product and Evidence Review

```text
You are reviewing the TraceMap Python indexer Kiro spec.

Context:
- TraceMap is a deterministic repository indexer and contract-change reducer.
- Core rule: no conclusion without evidence, no evidence without a rule ID, no raw snippets by default.
- The Python scanner must emit compatible TraceMap artifacts: scan-manifest.json, facts.ndjson, index.sqlite, report.md, logs/analyzer.log.
- It must use deterministic Python AST/package/config/static evidence, not LLMs or embeddings.
- It must not import target modules, execute decorators, start apps, run tests, install dependencies, or run framework startup.
- The implementation target is src/python as a sibling to src/dotnet, src/typescript, and src/jvm.
- MVP framework scope is FastAPI, Flask, Django route evidence; Pydantic/schema evidence; SQLAlchemy/direct SQL evidence; config/env reads; and common HTTP client calls.
- SQL is treated as shared cross-language data dependency evidence, not the next standalone app language.
- Existing .NET reduce/export/combine/endpoint commands must work against Python indexes.

Files to review:
- .kiro/specs/python-indexer/requirements.md
- .kiro/specs/python-indexer/design.md
- .kiro/specs/python-indexer/tasks.md

Please review for:
- Missing user workflows.
- Requirements that overclaim runtime behavior, imports, decorators, middleware, router inclusion, dependency injection, monkey patching, ORM behavior, serializer mappings, or dynamic dispatch.
- Evidence-tier ambiguity, especially whether AST-only facts should be Tier2 or Tier3.
- Whether `Level1SemanticAnalysis` is too strong for Python AST-only MVP.
- Scope that is too large for a first Python implementation.
- Missing non-goals or Python-specific limitations.
- Contract reducer, export, combine, endpoint-alignment, or SQL-evidence compatibility gaps.
- Whether Python should be the next app-language adapter before a shared SQL parser.
- Which framework integrations should be cut or added for MVP.

Return:
- Blockers.
- Recommended scope cuts.
- Requirement wording changes.
- Evidence-tier corrections.
- Questions that must be answered before implementation.
```

## Sonnet Implementation Review

```text
You are reviewing the TraceMap Python indexer Kiro spec for implementation feasibility.

Context:
- The implementation will live under src/python.
- It should emit TraceMap-compatible facts and SQLite tables that existing .NET reduce/export/combine commands can read.
- It should use Python AST and local metadata first.
- It should not import target modules or execute app code.
- It should keep SQL evidence compatible with other language adapters and defer a full SQL dialect parser to a shared layer.
- The first implementation gates should prove .NET reduce/export/combine compatibility against Python fixture indexes before extractor breadth.

Files to review:
- .kiro/specs/python-indexer/requirements.md
- .kiro/specs/python-indexer/design.md
- .kiro/specs/python-indexer/tasks.md

Please review for:
- Incorrect Python AST assumptions.
- Packaging/project discovery scope that is too broad or too vague.
- Whether type-checker integration should be fully deferred or spiked earlier.
- How to implement stable Python symbol identity without executing imports.
- Whether FastAPI/Flask/Django route extraction is sliced small enough.
- SQLAlchemy and Pydantic extraction risks.
- SQLite/schema compatibility risks.
- Test fixture gaps.
- Tasks that should move earlier or later.

Return:
- Implementation blockers.
- Proposed task reordering.
- MVP slice recommendation.
- Specific design edits before coding.
```

## SQL Layer Review

```text
You are reviewing the SQL portion of the TraceMap Python indexer spec.

Context:
- SQL is not being treated as the next app-language adapter.
- Python should emit SQL evidence into shared facts, compatible with .NET, TypeScript, and JVM.
- A deeper SQL parser/normalizer should be a future shared layer.
- Raw SQL text must not be stored by default.

Files to review:
- .kiro/specs/python-indexer/requirements.md
- .kiro/specs/python-indexer/design.md
- .kiro/specs/python-indexer/tasks.md
- docs/VALIDATION.md
- docs/LANGUAGE_ADAPTER_CONTRACT.md

Please review for:
- Whether Python SQL evidence properties are sufficient for future cross-language SQL matching.
- Whether SQLAlchemy ORM/core facts are overclaiming runtime query/schema behavior.
- Which SQL operation/table/column fields are safe to emit before a real parser exists.
- How to avoid duplicating SQL parser logic inside each language adapter.

Return:
- SQL evidence blockers.
- Suggested shared SQL fact/property contract.
- Scope cuts for Python MVP.
```
