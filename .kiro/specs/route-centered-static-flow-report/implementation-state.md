# Route-Centered Static Flow Report Implementation State

Status: implemented-pending-pr-review

## Branch

- Current implementation branch:
  `codex/implement-route-centered-static-flow-report`.

## Scope

This implementation adds the first product slice for the route-centered static
call flow report. It implements `tracemap route-flow` as a deterministic
reporting/query layer over a combined SQLite index. It does not add scanner
extractors, runtime proof, LLM calls, browser execution, database connections,
site changes, or generated public outputs.

The proposed feature is a deterministic reporting/query layer over a combined
TraceMap index. It should compose existing evidence families where possible:

- endpoint alignment;
- HTTP route bindings;
- TypeScript HTTP client calls;
- call edges, object creations, argument flows, and parameter-forward edges;
- `combined_symbol_relationships` and fact-symbol attachments;
- query patterns and SQL-shape evidence;
- data/dependency surfaces;
- object/projection/schema/business-boundary facts;
- combined path and reverse graph inventory;
- evidence graph or vault export safe-rendering helpers.

## Source Material

- GitHub issue #159: `https://github.com/joefeser/tracemap/issues/159`.
- Issue title: `Add route-centered static call flow report`.
- Issue labels observed during spec creation: `enhancement`, `type:spec`,
  `area:flow`, `area:surfaces`, `priority:next`.

The issue describes a public-safe problem statement from private validation:
TraceMap can index route, call, repository, DbSet-like data access,
projection/query-shape, and client-call evidence in pieces, but it does not yet
produce one route-centered report that starts from an HTTP route or client call
and lists touched files, spans, symbols, intermediate calls, transformations,
and dependency/data logic. Private sample names and paths are intentionally not
recorded here.

## Scope Decisions

- The spec proposes a focused `tracemap route-flow` command over a combined
  SQLite index.
- The implementation may still reuse or internally extend `tracemap paths`; the
  new route-flow view should not create a second traversal engine.
- Interface-to-implementation bridging is conservative candidate evidence from
  `combined_symbol_relationships` or successor rule-backed implementation
  evidence. It is not runtime DI target proof.
- Business/data logic rows are static review context, such as projection/object
  shape, branching, validation, query/filter/sort/selection, async boundaries,
  and data/dependency surfaces. They are not runtime execution or business
  impact claims.
- Default outputs are Markdown and JSON, with no source snippets, raw SQL, raw
  URLs, connection strings, raw remotes, local absolute paths, private sample
  labels, or secrets.
- Public-safe validation should use checked-in synthetic/sample fixtures only.

## Current Validation

Implementation validation should run:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlowTests
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
git diff --check
./scripts/check-private-paths.sh
```

Kiro implementation review should be attempted with Opus or Sonnet through
`scripts/kiro-review.mjs` when `kiro-cli` and auth are available locally. If
Kiro review is unavailable, record the exact blocker in this file or the PR
summary and perform self-review.

Focused validation completed so far:

- `dotnet build src/dotnet/TraceMap.Reporting/TraceMap.Reporting.csproj`
- `dotnet build src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj`
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlowTests`
- `dotnet build src/dotnet/TraceMap.sln`
- `dotnet test src/dotnet/TraceMap.sln`
- `./scripts/check-private-paths.sh`
- `git diff --check`

Kiro implementation review completed with full coverage using Sonnet:

```bash
node scripts/kiro-review.mjs --phase route-centered-static-flow-report --kind implementation --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Patched actionable blocking findings from that review:

- replaced the SQLite no-mutation file hash with a read-only logical schema and row-count fingerprint;
- reused shared combined source identity semantics, including git-root identity;
- blocked strong route-flow classifications unless report coverage is full;
- made logic-row truncation single-pass and deterministic;
- emitted reduced-coverage gaps when route-flow detail tables are present but not directly projected in v1;
- blocked cross-source implementation candidate bridges and emitted runtime-binding gaps;
- added implementation-candidate-unavailable gaps for interface-shaped call targets without traversed candidate evidence;
- populated edge-backed evidence source labels from path source nodes where available.

Ran two re-review cycles with Sonnet, both with full coverage. The final
re-review still reported the implementation as not merge-ready because the
larger spec matrix remains incomplete, specifically active
`combined_symbol_relationships` interface-target detection, direct
`combined_fact_symbols` and `combined_argument_flows` readers, a checked-in
paths/reverse shared-helper regression fixture, and broad Req 9 scenario tests.
After the final re-review, patched the small correctness findings around
StrongStaticRouteFlow terminal-surface requirements, NoRouteFlowEvidence gap
coverage, UnknownAnalysisGap rollup precedence, duplicate aligned entry rows,
and identity gap IDs. No third Kiro cycle was run per the requested two-cycle
limit.

PR review loop follow-up patches addressed actionable route-flow findings from
Codex/Gemini review threads:

- null-safe route-flow display-name and label handling;
- route/client selector side filtering so a route-selected report does not
  compose client-rooted path rows, and a client-selected report does not compose
  route-rooted path rows;
- preservation of path-level review downgrades when projecting route-flow rows;
- gap truncation accounting across all route-flow gaps, not only path/schema
  gaps.

Pinned language-adapter smoke checks are deferred for this slice because the
change is a combined reporting/CLI layer over existing facts and does not modify
language adapters, endpoint extraction, dependency-surface extraction, or source
scanner behavior.

Latest local validation after PR review loop patches:

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlowTests`
- `dotnet build src/dotnet/TraceMap.sln`
- `dotnet test src/dotnet/TraceMap.sln`
- `./scripts/check-private-paths.sh`
- `git diff --check`

## Spec Delivery Notes

- Created the spec files only under
  `.kiro/specs/route-centered-static-flow-report/`.
- Did not edit product code, site files, generated outputs, docs outside this
  spec, or rule catalog entries.
- Ran Kiro Opus spec review:
  `node scripts/kiro-review.mjs --phase route-centered-static-flow-report --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`.
  The review completed with reduced coverage because Kiro reported denied tool
  access.
- Ran Kiro Sonnet spec review:
  `node scripts/kiro-review.mjs --phase route-centered-static-flow-report --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`.
  The review completed with full coverage.
- Patched actionable Medium+ findings from the initial reviews, including
  combined schema table names, explicit fact-symbol and argument-flow readers,
  selector semantics, route-flow summary classification, exit-code behavior,
  interface-bridge classification caps, high-fan-out threshold documentation,
  and public/private safety tests.
- Ran two re-review cycles with Opus and Sonnet, each using `--kind re-review`
  and a 10 minute timeout. The final re-reviews completed but both had reduced
  coverage because Kiro reported denied tool access.
- Final local validation passed:
  - `git diff --check`
  - `./scripts/check-private-paths.sh`
  - `node scripts/kiro-review.mjs --self-test`

## Follow-Ups For Implementation

- Broaden public-safe fixture coverage for dynamic URL, optional segments,
  duplicate normalized route keys, missing TypeScript facts, missing route
  facts, reduced coverage, unknown commit SHA, old combined schemas, high
  fan-out, and all cap/truncation paths.
- Read and project richer `combined_fact_symbols` and `combined_argument_flows`
  detail rows directly; the current slice emits schema availability gaps and
  reuses existing path graph evidence.
- Expand conservative interface bridge tests with explicit
  `combined_symbol_relationships` implementation-candidate fixtures.
- Replace the current SQLite no-mutation hash test with a WAL-neutral logical
  schema/row-count/content fingerprint.
- Run pinned combined smoke checks if future work changes shared path traversal,
  endpoint alignment, language adapters, or shared report helpers.
