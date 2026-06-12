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
TraceMap.sln
src/TraceMap.Cli
src/TraceMap.Core
src/TraceMap.CSharp
src/TraceMap.Extractors.Http
src/TraceMap.Extractors.Database
src/TraceMap.Extractors.Config
src/TraceMap.Storage
src/TraceMap.Reduce
src/TraceMap.Reporting
src/tests/TraceMap.Tests
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
