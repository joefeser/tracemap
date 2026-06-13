# Python Indexer Review Prompts

Use these prompts after spec updates and before implementation starts.

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
- Real Python MVP scans must not claim DefiniteImpact from AST-only evidence. DefiniteImpact is proved only by a synthetic Tier1 fixture for reducer compatibility.
- MVP framework scope is FastAPI, Flask, Pydantic, dataclasses, SQLAlchemy declared columns/direct SQL, env/config reads, and requests/httpx clients.
- Django, aiohttp, urllib, attrs, TypedDict, lockfiles, .pyi, type-checker semantics, and rich logic-shape facts are post-MVP.
- SQL is treated as shared cross-language data dependency evidence, not the next standalone app language.
- Existing .NET reduce/export/combine/endpoint commands must work against Python indexes.

Files to review:
- .kiro/specs/python-indexer/requirements.md
- .kiro/specs/python-indexer/design.md
- .kiro/specs/python-indexer/tasks.md

Please review for:
- Missing user workflows.
- Requirements that overclaim runtime behavior, imports, decorators, middleware, router inclusion, dependency injection, monkey patching, ORM behavior, serializer mappings, or dynamic dispatch.
- Whether real Python MVP no-match results should always be NoEvidenceReducedCoverage.
- Whether buildStatus = FailedOrPartial is acceptable as the compatibility convention for "no full semantic Python pass".
- Evidence-tier ambiguity, especially whether each Tier2 case has import/package/framework/file-role evidence.
- Scope that remains too large for a first Python implementation.
- Missing non-goals or Python-specific limitations.
- Contract reducer, export, combine, endpoint-alignment, or SQL-evidence compatibility gaps.
- Whether endpoint alignment should remain post-MVP validation or become an MVP acceptance gate.

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
- Real AST-only Python MVP extraction should not emit MethodInvoked or PropertyAccessed because receiver types are not proven.

Files to review:
- .kiro/specs/python-indexer/requirements.md
- .kiro/specs/python-indexer/design.md
- .kiro/specs/python-indexer/tasks.md

Please review for:
- Incorrect Python AST assumptions.
- Packaging/project discovery scope that is too broad or too vague.
- Whether type-checker integration should stay fully deferred.
- How to implement stable Python symbol identity without executing imports.
- Whether targetSymbol as dotted display plus sourceSymbolId/targetSymbolId as stable IDs is implementable and reducer-friendly.
- Whether FastAPI/Flask route extraction is sliced small enough.
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
- Raw SQL text must not be stored by default.
- Current shared hash convention is SHA-256 over the exact UTF-8 string bytes, truncated to 32 lowercase hex chars.
- Before a shared SQL parser exists, Python must not emit guessed tableName, columnName, or operationKind.
- Dynamic SQL from f-strings, concatenation, format strings, templates, or ORM builders should be a boundary/gap fact.

Files to review:
- .kiro/specs/python-indexer/requirements.md
- .kiro/specs/python-indexer/design.md
- .kiro/specs/python-indexer/tasks.md
- docs/VALIDATION.md
- docs/LANGUAGE_ADAPTER_CONTRACT.md

Please review for:
- Whether Python SQL evidence properties are sufficient for future cross-language SQL matching.
- Whether SQLAlchemy ORM/core facts still overclaim runtime query/schema behavior.
- Whether operationName from only literal leading SQL verbs is safe.
- Whether sqlSourceKind values are sufficient.
- How to avoid duplicating SQL parser logic inside each language adapter.

Return:
- SQL evidence blockers.
- Suggested shared SQL fact/property contract.
- Scope cuts for Python MVP.
```
