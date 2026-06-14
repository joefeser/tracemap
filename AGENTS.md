# AGENTS.md — TraceMap Tool

## Mission

Build a deterministic C# repository indexer and contract-change impact reducer. The tool must produce evidence-backed findings with rule IDs, evidence tiers, file paths, line spans, commit SHA, and extractor versions.

This is not an AI impact-analysis tool. Do not add LLM calls, embeddings, vector databases, or prompt-based classification in the core scanner/reducer.

## Non-negotiable principles

- No conclusion without evidence.
- No evidence without a rule ID.
- No rule without documented limitations.
- No scan without repo and commit SHA.
- Failed build is not a clean repo.
- Partial analysis is useful, but must be labeled as partial.
- Prefer deterministic, testable extractors.

## Implementation guidance

- Build as a .NET CLI solution.
- Prefer small, reviewable commits.
- Add or update tests with each meaningful change.
- When implementing a Kiro spec, update that spec's `tasks.md` checkboxes as tasks are completed. Do not leave task status stale when the implementation is done.
- For longer specs, keep a spec-local implementation state note such as `.kiro/specs/<spec-name>/implementation-state.md` with current branch, scope decisions, oddities, validation, and follow-up items so future contexts can resume without guessing.
- Keep the scanner useful even when MSBuild project load fails.
- Use Roslyn semantic analysis where possible, but always include syntax fallback.
- Emit machine-readable `facts.ndjson` and `index.sqlite`.
- Emit human-readable `report.md`.
- Do not store source snippets by default. Store file paths, line spans, and snippet hashes. Only add raw snippets behind an explicit option.
- Preserve stable JSON schemas where possible.

## Suggested package choices

- `Microsoft.CodeAnalysis.CSharp.Workspaces`
- `Microsoft.CodeAnalysis.Workspaces.MSBuild`
- `Microsoft.Build.Locator`
- `Microsoft.Data.Sqlite`
- `xunit` or `NUnit` for tests

## Required outputs for `tracemap scan`

```text
scan-manifest.json
facts.ndjson
index.sqlite
report.md
logs/analyzer.log
```

## Evidence tiers

```text
Tier1Semantic       Compiler-resolved Roslyn symbol evidence.
Tier2Structural     Known framework or project structure pattern.
Tier3SyntaxOrTextual Syntax-only or string/textual evidence.
Tier4Unknown        Analysis gap or unable to prove/disprove.
```

## Analysis behavior

When semantic analysis fails:

1. Continue scanning.
2. Run syntax fallback over `.cs` files.
3. Scan config/project/SQL files.
4. Emit `AnalysisGap` facts.
5. Mark scan coverage as reduced.

## Review checklist before finishing a task

- Does `dotnet test` pass?
- Can the CLI run against at least one sample repo?
- For language-adapter changes, did we follow `docs/VALIDATION.md` and run or explicitly defer the relevant pinned smoke checks?
- If a required local tool appears missing, check Homebrew first (`brew list`, `brew --prefix <formula>`, or `brew info <formula>`) and try the Homebrew path before stopping. Some tools may be installed outside system discovery paths, such as Java under `/opt/homebrew/opt` or Node via `nvm`. If Homebrew/path discovery does not find the tool, stop and ask instead of guessing or rewriting the workflow.
- For Python adapter tests, prefer a temporary virtual environment, for example `python3 -m venv /tmp/tracemap-python-venv && /tmp/tracemap-python-venv/bin/python -m pip install -e "src/python[dev]" && /tmp/tracemap-python-venv/bin/python -m pytest src/python/tests`.
- For JVM adapter tests, Java 21 is required. On macOS, install it with Homebrew if needed and run with `JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home`; `/usr/libexec/java_home -v 21` may not list Homebrew OpenJDK until the system symlink is configured.
- Noisy contract names such as `status` may correctly downgrade reducer output to `NeedsReview` when high fan-out evidence is present; do not force `DefiniteImpact` just to satisfy stale tests.
- Are facts deterministic and evidence-backed?
- Did we avoid saying “impacted” without a reducer and evidence?
- Did we clearly mark partial/failed analysis?
- Did we update docs or rule catalog if a rule changed?
