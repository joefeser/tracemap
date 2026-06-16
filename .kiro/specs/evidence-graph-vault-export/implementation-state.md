# Evidence Graph Vault Export Implementation State

Status: implementation-in-progress
Branch: codex/implement-evidence-graph-vault-export
Worktree: requested implementation worktree
Scope: .NET CLI MVP
Public claim level: implementation available; public/demo claims require
public-safe inputs, a source claim catalog, and successful validation.

## Summary

This branch implements a first-class `tracemap vault export` command for the
evidence graph/vault export MVP. The exporter reads existing TraceMap combined
indexes read-only, optionally consumes documented paths and reverse report JSON
fields, projects static evidence into `graph.json`, and renders deterministic
Markdown notes with YAML frontmatter sentinels and content hashes.

The implementation does not add runtime proof, LLM calls, embeddings, vector
databases, graph databases, prompt-based classification, site publishing,
hosted demos, screenshots, scanner inference changes, reducer impact changes,
or release approval claims.

## Scope Decisions

- Implemented in .NET rather than a script fallback.
- CLI shape: `tracemap vault export --combined-index <combined-index> --out
  <vault-output>`.
- MVP input support:
  - combined SQLite index through existing TraceMap reporting graph inventory;
  - `paths-report.json` documented fields;
  - `reverse-report.json` documented fields;
  - `source-claim-catalog.v1` promotion by stable `sourceIndexId` plus proof ID.
- Deferred input support:
  - portfolio report JSON;
  - release-review report JSON;
  - evidence-pack JSON claim promotion until a locked pack schema carries stable
    claim metadata.
- Raw inputs default to `hidden`; demo/public output requires explicit
  `--minimum-claim-level`, source catalog proof, and `--date <yyyy-MM>`.
- Generated output uses deterministic stable IDs, sorted JSON, generated-file
  sentinels, Markdown hashes, graph content hash, and collision checks.
- Non-generated user notes are never overwritten. Stale generated files require
  `--force` and still pass safety validation.
- Promoted unsafe strings are rejected by output validation instead of being
  silently hashed into demo/public output. Hidden evidence remains
  category-labeled.

## Files Changed

- `src/dotnet/TraceMap.Reporting/VaultExport.cs`
- `src/dotnet/TraceMap.Cli/Program.cs`
- `src/dotnet/tests/TraceMap.Tests/VaultExportTests.cs`
- `docs/VAULT_EXPORT.md`
- `rules/rule-catalog.yml`
- `.kiro/specs/evidence-graph-vault-export/tasks.md`
- `.kiro/specs/evidence-graph-vault-export/implementation-state.md`

## Validation

Completed:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln --filter FullyQualifiedName~VaultExportTests
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Pinned smoke checks from `docs/VALIDATION.md` are deferred for this branch
because the change is an exporter over existing artifacts and does not modify a
language adapter or scanner extractor.

## Follow-Ups

- Add portfolio and release-review report readers after compatible schema fields
  are confirmed.
- Add evidence-pack promotion only after a locked v1 pack schema includes stable
  source identity and claim-level metadata.
- Add explicit duplicate/ambiguous stable identity gap coverage for vault node
  and edge projection.
- Broaden fixture coverage for reduced coverage and report-only inputs.
- Consider a richer backlink/index layout after the MVP privacy and
  determinism gates have had review time.
