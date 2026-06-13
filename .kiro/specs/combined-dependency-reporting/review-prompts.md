# Combined Dependency Reporting Review Prompts

Use this spec review prompt after reading:

- `.kiro/specs/combined-dependency-reporting/requirements.md`
- `.kiro/specs/combined-dependency-reporting/design.md`
- `.kiro/specs/combined-dependency-reporting/tasks.md`
- Existing related docs:
  - `docs/LANGUAGE_ADAPTER_CONTRACT.md`
  - `docs/ACCEPTANCE.md`
  - `docs/VALIDATION.md`
- Existing implementation:
  - `src/dotnet/TraceMap.Combine/CombinedIndexBuilder.cs`
  - `src/dotnet/TraceMap.Storage/IndexExporter.cs`
  - `src/dotnet/TraceMap.EndpointAlignment/*`
  - `src/dotnet/TraceMap.Cli/Program.cs`

## Prompt For Opus

Please review this TraceMap spec for a combined dependency reporting slice.

Context:

- TraceMap is deterministic static analysis. No LLMs, embeddings, or runtime tracing belong in core scanner/reporter behavior.
- Existing scanners emit evidence-backed facts with rule IDs, evidence tiers, file spans, commit SHA, and extractor versions.
- `tracemap combine` already creates a combined SQLite database with provenance-preserving combined facts, symbols, dependency tables, `combined_dependency_edges`, and an empty `endpoint_matches` table.
- `tracemap endpoints` already exists for exactly one client index and one server index.
- `tracemap report` currently exists only as a CLI skeleton.

Review goals:

1. Identify overclaims or places where the report could imply runtime behavior from static evidence.
2. Check whether the proposed `tracemap report --index <combined.sqlite>` command is the right surface, or whether a separate command would be safer.
3. Check if `endpoint_matches` persistence during report generation is wise, or whether MVP should default to read-only and make persistence explicit.
4. Verify the endpoint matching classifications are enough for N-way combined indexes.
5. Verify the report model has enough provenance for audits and future diffing.
6. Identify schema assumptions that are not true in the current combined database.
7. Suggest test cases that would catch likely false positives, false negatives, or provenance leaks.

Please prioritize concrete blocking issues first, then nice-to-have refinements.

## Prompt For Sonnet

Please review this TraceMap combined dependency reporting spec for implementability.

Focus on:

1. Whether the implementation tasks are sized correctly for a single PR.
2. Whether the reader/matcher/writer package layout fits the existing .NET solution.
3. Whether JSON and Markdown output contracts are specific enough to test.
4. Whether row caps and deterministic ordering are specified enough.
5. Whether the acceptance criteria can be validated with small fixtures instead of private repos.
6. Whether any scanner work accidentally leaked into this reporting-only slice.
7. Whether docs updates are scoped appropriately.

Assume the implementation should pass:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Return:

- Blockers
- Important refinements
- Optional follow-ups
- Recommended first implementation cut
