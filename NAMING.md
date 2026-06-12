# TraceMap Naming Decisions

Use these names consistently throughout the implementation.

## Product and repository

- Product name: **TraceMap**
- Suggested repository name: `tracemap`
- CLI command: `tracemap`
- Default output directory: `.tracemap`
- Default config file: `tracemap.yml`

## .NET solution and projects

```text
src/dotnet/TraceMap.sln
src/dotnet/TraceMap.Cli
src/dotnet/TraceMap.Core
src/dotnet/TraceMap.Storage
src/dotnet/TraceMap.Reduction
src/dotnet/TraceMap.Reporting
src/dotnet/tests/TraceMap.Tests
```

## Domain terms that should remain unchanged

Do not rename these just because they contain the word "impact":

```text
impact analysis
impact report
contract impact
DefiniteImpact
ProbableImpact
NoEvidenceFullCoverage
NoEvidenceReducedCoverage
```

The tool name is TraceMap, but the domain is still contract-change impact analysis.
