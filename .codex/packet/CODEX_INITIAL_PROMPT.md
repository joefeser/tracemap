# Initial prompt to paste into Codex

You are building an internal deterministic C# repository indexer named `tracemap`. Read `README_Codex_Handoff.md`, `AGENTS.md`, and `MILESTONES.md` before changing code.

Start with Milestone 0 and Milestone 1 only. Do not attempt all milestones in one task.

Build a .NET solution with this structure:

```text
TraceMap.sln
src/TraceMap.Cli
src/TraceMap.Core
src/TraceMap.Storage
src/TraceMap.Reporting
tests/TraceMap.Tests
rules/rule-catalog.yml
samples/tracemap.yml
samples/contract-delta.example.json
```

Implement:

1. A CLI command `tracemap scan --repo <path> --out <path>`.
2. A core model for `ScanManifest`, `CodeFact`, and `EvidenceSpan`.
3. Git metadata detection using `git` commands if available, with graceful fallback when not in a git repo.
4. File inventory for `.sln`, `.csproj`, `.config`, `.json`, `.cs`, `.sql`, `packages.config`, `Web.config`, and `App.config`.
5. Output files:
   - `scan-manifest.json`
   - `facts.ndjson`
   - `report.md`
   - `logs/analyzer.log`
6. Unit tests for manifest creation, JSONL fact writing, and running against a temporary sample directory.

Constraints:

- Do not add LLM calls, embeddings, or vector databases.
- Do not require the target repo to compile in Milestone 1.
- Do not fail if git metadata is unavailable.
- Keep output deterministic and documented.
- Avoid storing source snippets. Store path, line span if available, and snippet hash when implemented later.

When done:

- Run `dotnet build` and `dotnet test`.
- Summarize files changed.
- Include exact commands used.
- List next recommended milestone.
