# Follow-up prompts for Codex

## Prompt for Milestone 2 — C# syntax fallback

Implement Milestone 2 from `MILESTONES.md`: C# syntax fallback extraction without requiring project load or build.

Requirements:

- Parse all non-generated `.cs` files using Roslyn syntax APIs.
- Emit facts for type, method, property, enum declarations.
- Emit facts for attribute names.
- Emit `MemberAccessName` facts for member access expressions, e.g. `profile.PrimaryEmail`.
- Emit `InvocationName` facts for invocation expressions, e.g. `client.GetAsync(...)`.
- Add line-span calculation from syntax nodes.
- Mark these facts `Tier3SyntaxOrTextual` unless semantic evidence is available.
- Add tests using a broken sample project that does not compile.

Do not implement semantic MSBuild analysis yet.

Run `dotnet build` and `dotnet test` before finishing.

## Prompt for Milestone 3 — SQLite index

Implement Milestone 3 from `MILESTONES.md`: SQLite storage.

Requirements:

- Add `index.sqlite` output.
- Add schema from `README_Codex_Handoff.md`.
- Insert scan manifest and all facts.
- Add indexes for fact type, rule, target symbol, contract element, and file.
- Add tests that query facts from SQLite.
- Keep JSONL output unchanged.

Run `dotnet build` and `dotnet test` before finishing.

## Prompt for Milestone 4 — Roslyn semantic extractor

Implement Milestone 4 from `MILESTONES.md`: best-effort Roslyn semantic extraction.

Requirements:

- Use `Microsoft.Build.Locator`, `Microsoft.CodeAnalysis.Workspaces.MSBuild`, and `Microsoft.CodeAnalysis.CSharp.Workspaces`.
- Try to load solutions/projects with MSBuildWorkspace.
- Extract semantic facts for property accesses and method invocations when symbols resolve.
- Emit Tier1Semantic facts only when a symbol is compiler-resolved.
- On project load/compilation failure, log gaps and continue syntax fallback.
- Add a modern sample project that compiles and a broken sample project that does not.

Run `dotnet build` and `dotnet test` before finishing.

## Prompt for Milestone 5 — HTTP/DB/config extractors

Implement Milestone 5 from `MILESTONES.md`.

Requirements:

- Detect HttpClient calls: GetAsync, PostAsync, PutAsync, DeleteAsync, SendAsync.
- Detect common JSON HTTP extension calls: GetFromJsonAsync, ReadFromJsonAsync, PostAsJsonAsync, PutAsJsonAsync.
- Detect IHttpClientFactory.CreateClient name when literal.
- Detect DbContext-like classes, DbSet properties, SaveChanges/SaveChangesAsync calls.
- Detect Dapper-style Query/QueryAsync/Execute/ExecuteAsync calls.
- Detect ADO.NET SqlCommand and SQL-like string literals.
- Parse appsettings*.json, Web.config, App.config, packages.config, and connection strings.
- Emit evidence tiers honestly.

Run `dotnet build` and `dotnet test` before finishing.

## Prompt for Milestone 7 — Basic reducer

Implement Milestone 7 from `MILESTONES.md`: contract delta reduction.

Requirements:

- Parse `samples/contract-delta.example.json` shape.
- Add `tracemap reduce --index <path> --contract-delta <path> --out <path>`.
- Match changed type/property names against facts.
- Classify:
  - DefiniteImpact for Tier1 semantic property/type usage.
  - ProbableImpact for strong structural DTO/HTTP/DB/config usage.
  - NeedsReview for syntax/textual matches.
  - NoEvidenceFullCoverage when no matches and scan coverage is full semantic.
  - NoEvidenceReducedCoverage when no matches but scan coverage is reduced.
  - UnknownAnalysisGap when scan gaps prevent a credible conclusion.
- Emit Markdown impact report with evidence rows.

Run `dotnet build` and `dotnet test` before finishing.
