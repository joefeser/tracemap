# Codex Handoff: Impact Index Tool for C# Contract-Change Analysis

## Purpose

Build an internal deterministic CLI tool that indexes C# repositories into a reusable evidence-backed impact index. The tool is not an AI impact guesser. It emits normalized code facts with repo, commit SHA, project, file, line span, rule ID, extractor version, and evidence tier. Later, a reducer compares a contract delta against the index to classify likely impact.

Core statement:

> Map emits evidence. Reduce computes blast radius. AI may summarize evidence, but AI must not invent findings.

## First product target

A .NET CLI tool that can be run in any current repository:

```bash
impact scan --repo . --out .impact-index
impact report --index .impact-index/index.sqlite --out .impact-index/report.md
impact reduce --index .impact-index/index.sqlite --contract-delta contract-delta.json --out .impact-index/impact-report.md
```

## Core principles

1. No conclusion without evidence.
2. No evidence without a rule ID.
3. No rule without documented limitations.
4. No scan without repo and commit SHA.
5. A repo that fails build is not silently considered clean.
6. Full compile gives best evidence; partial/no compile still gives useful syntax, config, SQL, HTTP, DB-pattern, and file-inventory evidence.
7. AI is optional and only summarizes or navigates facts after deterministic extraction.

## Architecture

```text
ImpactIndex/
  src/
    Impact.Cli/
    Impact.Core/
    Impact.CSharp/
    Impact.Extractors.Http/
    Impact.Extractors.Database/
    Impact.Extractors.Config/
    Impact.Storage/
    Impact.Reduce/
    Impact.Reporting/
  tests/
    Impact.Tests/
    SampleRepos/
  rules/
    rule-catalog.yml
  samples/
    impact.yml
    contract-delta.example.json
```

## CLI commands

### scan

```bash
impact scan --repo <path> --out <path> [--config impact.yml] [--no-build] [--include-generated] [--include-snippets]
```

Outputs:

```text
.impact-index/
  scan-manifest.json
  facts.ndjson
  index.sqlite
  report.md
  logs/
    analyzer.log
    restore.log
    build.log
```

### report

```bash
impact report --index .impact-index/index.sqlite --out .impact-index/report.md
```

### reduce

```bash
impact reduce --index .impact-index/index.sqlite --contract-delta contract-delta.json --out impact-report.md
```

## Analysis levels

Every scan and fact should report evidence depth.

```text
Level 5 — Full semantic analysis
  Project loaded, restore/build succeeded enough to produce semantic models.

Level 4 — Partial semantic analysis
  Project loaded and semantic model exists, but compilation has errors or missing references.

Level 3 — Syntax analysis
  C# parsed as syntax trees; declarations, invocations, routes, member access names detected without symbol proof.

Level 2 — Structural/text analysis
  Config, project files, SQL, packages, Web.config, appsettings, obvious framework patterns.

Level 1 — File inventory only
  Files and project/config inventory, but no deep C# parse.

Level 0 — Not analyzed
  Repo inaccessible or scanner crashed.
```

## Evidence tiers

```text
Tier1Semantic
  Compiler-resolved symbol evidence from Roslyn.

Tier2Structural
  Known framework pattern, e.g. HttpClient, EF DbSet, Dapper QueryAsync, controller route, options binding.

Tier3SyntaxOrTextual
  Syntax-only member name, string match, SQL identifier match, JSON property name match.

Tier4Unknown
  Repo/project/file could not be analyzed deeply enough to prove or disprove impact.
```

## Core data model

```csharp
public sealed record ScanManifest(
    string ScanId,
    string RepoName,
    string? RemoteUrl,
    string? Branch,
    string CommitSha,
    string ScannerVersion,
    DateTimeOffset ScannedAt,
    string AnalysisLevel,
    string BuildStatus,
    IReadOnlyList<string> Solutions,
    IReadOnlyList<string> Projects,
    IReadOnlyList<string> TargetFrameworks,
    IReadOnlyList<string> KnownGaps);

public sealed record EvidenceSpan(
    string FilePath,
    int StartLine,
    int EndLine,
    string? SnippetHash,
    string ExtractorId,
    string ExtractorVersion);

public sealed record CodeFact(
    string FactId,
    string ScanId,
    string Repo,
    string CommitSha,
    string? ProjectPath,
    string FactType,
    string RuleId,
    string EvidenceTier,
    string? SourceSymbol,
    string? TargetSymbol,
    string? ContractElement,
    EvidenceSpan Evidence,
    IReadOnlyDictionary<string, string> Properties);
```

## Initial fact types

```text
RepoScanned
SolutionDeclared
ProjectDeclared
PackageReferenced
TargetFrameworkDeclared
TypeDeclared
MethodDeclared
PropertyDeclared
EnumDeclared
AttributeUsed
TypeReferenced
PropertyAccessed
MemberAccessName
MethodInvoked
InvocationName
ControllerRouteDeclared
MinimalApiRouteDeclared
HttpCallDetected
HttpClientCreated
DbContextDeclared
DbSetDeclared
EfSaveChangesCalled
DapperQueryCalled
AdoNetCommandDetected
SqlTextUsed
ConfigKeyDeclared
ConfigKeyUsed
ConnectionStringDeclared
OptionsTypeBound
BuildStatus
AnalyzerWarning
AnalysisGap
```

## Extractors to build in order

1. RepoManifestExtractor
2. FileInventoryExtractor
3. ProjectFileExtractor for `.sln`, `.csproj`, `packages.config`
4. ConfigExtractor for `appsettings*.json`, `Web.config`, `App.config`
5. CSharpSyntaxExtractor fallback using syntax parse only
6. RoslynSemanticExtractor using MSBuildWorkspace where possible
7. PropertyAccessExtractor
8. MethodInvocationExtractor
9. HttpExtractor for HttpClient and common HTTP extension methods
10. DatabaseExtractor for EF Core/classic EF patterns, Dapper, ADO.NET, SQL strings
11. MarkdownReportWriter
12. SqliteIndexWriter
13. BasicContractReducer

## Legacy .NET Framework behavior

The scanner must handle .NET Framework 4.x and old-style csproj as best-effort inputs.

Requirements:

1. Do not fail the whole scan because restore/build/project load fails.
2. If MSBuild/Roslyn semantic load fails, run syntax fallback over all `.cs` files.
3. Include `Web.config`, `App.config`, `packages.config`, `.svcmap`, `Reference.cs`, `.edmx`, `.sql`, and generated service reference files when present.
4. Emit `AnalysisGap` facts for missing reference assemblies, missing packages, missing HintPath DLLs, unsupported project types, or failed generated-code steps.
5. A repo with reduced coverage must never be reported as clean without caveat.

## SQLite schema MVP

```sql
create table scan_manifest (
  scan_id text primary key,
  repo text not null,
  commit_sha text not null,
  scanner_version text not null,
  scanned_at text not null,
  analysis_level text not null,
  build_status text not null,
  manifest_json text not null
);

create table facts (
  fact_id text primary key,
  scan_id text not null,
  repo text not null,
  commit_sha text not null,
  project_path text,
  fact_type text not null,
  rule_id text not null,
  evidence_tier text not null,
  source_symbol text,
  target_symbol text,
  contract_element text,
  file_path text not null,
  start_line integer not null,
  end_line integer not null,
  snippet_hash text,
  properties_json text not null
);

create index ix_facts_type on facts(fact_type);
create index ix_facts_rule on facts(rule_id);
create index ix_facts_target_symbol on facts(target_symbol);
create index ix_facts_contract_element on facts(contract_element);
create index ix_facts_file on facts(file_path);
```

## Contract delta shape

```json
{
  "contract": "CustomerProfileResponse",
  "source": "contracts/customer-profile-v2.json",
  "changes": [
    {
      "element": "CustomerProfileResponse.primaryEmail",
      "changeType": "removed",
      "oldType": "string",
      "newType": null
    },
    {
      "element": "CustomerProfileResponse.status",
      "changeType": "enum_value_added",
      "value": "Suspended"
    }
  ]
}
```

## Reducer classification

```text
DefiniteImpact
  Semantic property/type/method usage tied to changed contract element.

ProbableImpact
  Strong structural evidence such as DTO serialized/deserialized, endpoint returns type, HTTP response type, DB mapping.

NeedsReview
  Syntax/textual evidence, SQL identifier, JSON property string, dynamic/reflection-ish patterns.

NoEvidenceFullCoverage
  No matching evidence and semantic scan succeeded.

NoEvidenceReducedCoverage
  No matching evidence, but semantic coverage was incomplete.

UnknownAnalysisGap
  Build/project load/parse was insufficient to make a claim.
```

## Initial acceptance criteria

1. `dotnet test` passes.
2. `impact scan --repo <sample-modern> --out <tmp>` produces `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, and `report.md`.
3. A sample project with `profile.PrimaryEmail` emits a Tier1 semantic `PropertyAccessed` fact if semantic analysis works.
4. A broken sample project still emits syntax-level `MemberAccessName` facts and an `AnalysisGap` fact.
5. The report has sections for repo summary, analysis coverage, facts by category, HTTP calls, DB calls, config keys, and known gaps.
6. The tool does not use an LLM or embeddings.
7. Generated snippets are not stored by default; store file/line and snippet hash. Add `--include-snippets` later if explicitly enabled.

