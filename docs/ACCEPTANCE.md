# TraceMap Acceptance Plan

## Purpose

This plan defines how TraceMap proves that scanner and reducer behavior is deterministic, evidence-backed, and honest about coverage.

## Required Local Verification

Run before finishing implementation work:

```bash
dotnet build
dotnet test
```

Expected result:

- build succeeds with zero errors.
- tests pass.

## Core Artifact Acceptance

For every successful `tracemap scan --repo <repo> --out <out>` run, verify:

- `<out>/scan-manifest.json` exists.
- `<out>/facts.ndjson` exists.
- `<out>/index.sqlite` exists.
- `<out>/report.md` exists.
- `<out>/logs/analyzer.log` exists.
- manifest includes repo name, commit SHA, scanner version, analysis level, and build status.
- facts include rule IDs, evidence tiers, file paths, line spans, commit SHA, and extractor versions.
- `index.sqlite` includes a `call_edges` table when call-edge facts are emitted.
- `index.sqlite` includes an `object_creations` table when object-creation facts are emitted.
- `index.sqlite` includes an `argument_flows` table when argument-flow facts are emitted.
- `index.sqlite` includes a `local_aliases` table when local-alias facts are emitted.
- `index.sqlite` includes a `field_aliases` table when field-alias facts are emitted.
- `index.sqlite` includes a `parameter_forward_edges` table derived from parameter-to-parameter argument-flow facts.

For every successful `tracemap-ts scan --repo <repo> --out <out>` run, verify:

- the same required artifacts are written.
- scans outside a Git checkout with a known commit SHA fail before artifacts are written.
- `scan-manifest.json` uses `Level1SemanticAnalysis` and `buildStatus: "Succeeded"` only when every selected TypeScript project loads semantically with no known gaps and a known commit SHA.
- reduced TypeScript scans use `Level1SemanticAnalysisReduced` or `Level3SyntaxAnalysis`.
- reducer-compatible facts reuse existing fact type strings and matching keys such as `propertyName`, `methodName`, `typeName`, `keyPath`, `name`, `containingType`, and `targetSymbol`.
- TypeScript facts store hashes/spans for source values, not raw source snippets.

## Reducer Acceptance

For every successful `tracemap reduce --index <index> --contract-delta <delta> --out <report>` run, verify:

- impact report exists.
- every finding includes the reducer rule ID `contract.delta.reduce.v1`.
- matched findings include evidence rows.
- no-match findings include manifest coverage evidence.
- reduced coverage never produces `NoEvidenceFullCoverage`.

## Export Acceptance

For every successful `tracemap export --index <index> --out <out> --format json` run, verify:

- export JSON exists.
- export JSON includes scan manifest metadata, fact counts by type/tier/rule, relationship rows, call-edge rows, and object-creation rows when those tables exist.
- export JSON does not include raw source snippets.
- rows are ordered deterministically.

For every successful `tracemap export --index <index> --out <out> --format mermaid` run, verify:

- Mermaid output exists.
- output starts with `flowchart TD`.
- relationship edges use direct `symbol_relationships` evidence.
- call edges use indexed call-edge facts and are bounded.

For TypeScript indexes, `tracemap-ts export --index <index> --out <out> --format <json|mermaid>` should produce equivalent export shapes from the same SQLite tables.

## Endpoint Alignment Acceptance

For every successful `tracemap endpoints --client-index <client> --server-index <server> --out <out>` run, verify:

- Markdown and/or JSON output follows the requested output shape.
- output includes client and server scan IDs, commit SHAs, analysis levels, build statuses, labels, scan-root metadata, and index path hashes.
- every finding includes the derived rule ID `endpoint.alignment.v1`.
- matched rows preserve source fact IDs, rule IDs, evidence tiers, file paths, and line spans from both indexes.
- reduced client or server coverage appears in `coverageWarnings`.
- `ClientCallNoServerEndpoint` states it is not proof of a broken call.
- `ServerEndpointNoClientMatch` states it is not proof of dead code or an unused endpoint.
- dynamic client URLs are classified as `DynamicClientUrlNeedsReview` rather than guessed.

Endpoint alignment is static code evidence only. It does not prove runtime routing, middleware behavior, reverse proxies, auth policies, deployment base paths, CORS behavior, feature flags, or whether a route executes.

## Included Sample Repos

### `samples/modern-sample`

Purpose: prove the full semantic path.

Command:

```bash
tracemap scan --repo samples/modern-sample --out <tmp>/modern-sample
tracemap reduce --index <tmp>/modern-sample/index.sqlite --contract-delta samples/contract-deltas/modern-sample.customer-profile.json --out <tmp>/modern-impact.md
```

Expected:

- scan analysis level is `Level1SemanticAnalysis`.
- build status is `Succeeded`.
- `CustomerProfileResponse.primaryEmail` is `DefiniteImpact`.
- evidence includes a Tier1 `PropertyAccessed` fact.
- `CustomerProfileResponse.status` is `NoEvidenceFullCoverage` unless new sample code adds status evidence.

### `samples/broken-sample`

Purpose: prove fallback behavior.

Command:

```bash
tracemap scan --repo samples/broken-sample --out <tmp>/broken-sample
```

Expected:

- scan completes.
- analysis level is reduced or syntax-only.
- build status is not clean success.
- syntax facts are emitted for declarations and member names.
- `AnalysisGap` facts are emitted.

### `samples/typescript-modern-sample`

Purpose: prove the TypeScript full semantic path and reducer compatibility.

Command:

```bash
cd src/typescript
node dist/src/cli.js scan --repo ../../samples/typescript-modern-sample --out <tmp>/typescript-modern-sample
cd ../..
dotnet run --project src/dotnet/TraceMap.Cli -- reduce --index <tmp>/typescript-modern-sample/index.sqlite --contract-delta samples/contract-deltas/typescript-modern.status.json --out <tmp>/typescript-impact.md
```

Expected:

- scan analysis level is `Level1SemanticAnalysis`.
- build status is `Succeeded`.
- `CustomerContract.status` is `DefiniteImpact`.
- evidence includes a Tier1 `PropertyAccessed` fact.
- route, serializer, Zod, Prisma, and `process.env` facts are emitted as bounded integration evidence.

### `samples/typescript-broken-sample`

Purpose: prove TypeScript syntax fallback behavior.

Command:

```bash
cd src/typescript
node dist/src/cli.js scan --repo ../../samples/typescript-broken-sample --out <tmp>/typescript-broken-sample
```

Expected:

- scan completes.
- analysis level is `Level3SyntaxAnalysis`.
- build status is `NotRun`.
- syntax declaration/member facts are emitted.
- `AnalysisGap` facts are emitted.

### `samples/endpoint-client-angular` and `samples/endpoint-server-aspnet`

Purpose: prove cross-index endpoint alignment over Angular `HttpClient` and ASP.NET controller route syntax fallback.

Command:

```bash
cd src/typescript
node dist/src/cli.js scan --repo ../../samples/endpoint-client-angular --out <tmp>/endpoint-client
cd ../..
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/endpoint-server-aspnet --out <tmp>/endpoint-server
dotnet run --project src/dotnet/TraceMap.Cli -- endpoints --client-index <tmp>/endpoint-client/index.sqlite --server-index <tmp>/endpoint-server/index.sqlite --client-label endpoint-client --server-label endpoint-server --out <tmp>/endpoint-report
```

Expected:

- Angular scan emits `HttpCallDetected` facts from `typescript.integration.angular-httpclient.v1`.
- ASP.NET scan emits `HttpRouteBinding` facts from `csharp.syntax.aspnetroute.v1`.
- endpoint report contains `MatchedEndpoint`, `OptionalSegmentMatch`, `MethodMismatch`, `DynamicClientUrlNeedsReview`, and `ServerEndpointNoClientMatch`.
- report warnings remain coverage-relative and do not claim runtime reachability.

## External Sample Repos

External repos live outside this repository at:

```text
/Users/josephfeser/src/gh-joe/c-sharp-sample-repos
```

These are opt-in smoke fixtures because they are larger, machine-local, and may depend on SDKs or packages not present on every development machine.

Recommended first-pass repos:

- `ProjectExtensions.Azure.ServiceBus`
- `fluentjdf`

Example command:

```bash
scripts/smoke-sample-repos.sh /Users/josephfeser/src/gh-joe/c-sharp-sample-repos <tmp>/sample-smoke
```

Expected:

- scan commands complete.
- scans may report `Level1SemanticAnalysisReduced`.
- reduced scans must label no-evidence findings as `NoEvidenceReducedCoverage`.
- generic member names such as `status` may match unrelated code and should emit warnings when they match multiple facts or a high-fan-out set.

TypeScript external smoke, recorded June 12, 2026:

- Repo: `/Users/josephfeser/src/gh-joe/scip-typescript`
- Command: `node src/typescript/dist/src/cli.js scan --repo /Users/josephfeser/src/gh-joe/scip-typescript --out /tmp/tracemap-ts-scip`
- Result: completed, 13,883 facts, `Level1SemanticAnalysisReduced`, `FailedOrPartial`.
- Interpretation: reduced coverage is expected for external repos with TypeScript diagnostics or dependency/config gaps; no-evidence reducer findings must remain reduced.

FFP endpoint smoke, recorded June 12, 2026:

- Client repo: `/Users/josephfeser/src/ffp/FFP%20Platform%20v2/backend/FFPRunningClub/FFPRunningClub.Api/ClientApp`
- Server repo: `/Users/josephfeser/src/ffp/FFP%20Platform%20v2/backend/FFPRunningClub`
- Server project: `FFPRunningClub.Api/FFPRunningClub.Api.csproj`
- Result: client scan completed with 34 `HttpCallDetected` facts and `Level1SemanticAnalysisReduced`; server scan completed with 37 `HttpRouteBinding` facts and `Level1SemanticAnalysisReduced`.
- Endpoint report: 38 findings, `ReducedEvidenceForScannedIndexes`, with `MatchedEndpoint`, `OptionalSegmentMatch`, `ServerEndpointNoClientMatch`, and coverage-warning sections.
- Interpretation: reduced coverage is expected because the fixture has dependency/project-load quirks; endpoint no-match findings remain coverage-relative.

## Repo-Specific Delta Fixtures

Repo-specific deltas live under:

```text
samples/contract-deltas/
```

Current files:

- `modern-sample.customer-profile.json`
- `servicebus.transient-status.json`
- `fluentjdf.status-builder.json`

Each fixture should document:

- target repo.
- changed contract element.
- expected classification.
- expected evidence tier.
- why the fixture exists.

## Regression Matrix

| Scenario | Expected result |
| --- | --- |
| semantic property usage match | `DefiniteImpact` |
| semantic type match | `DefiniteImpact` |
| Tier2 structural DTO/HTTP/DB/config match | `ProbableImpact` |
| syntax-only member match | `NeedsReview` |
| no match with full semantic coverage | `NoEvidenceFullCoverage` |
| no match with reduced coverage | `NoEvidenceReducedCoverage` |
| Angular HttpClient static URL | `HttpCallDetected` with normalized path key |
| ASP.NET controller attribute route with missing framework refs | Tier3 `HttpRouteBinding` from `csharp.syntax.aspnetroute.v1` |
| client/server method and path match | `MatchedEndpoint` |
| server optional segment match | `OptionalSegmentMatch` |
| path match but method differs | `MethodMismatch` |
| dynamic client URL | `DynamicClientUrlNeedsReview` |
| analysis-gap evidence names changed element | `UnknownAnalysisGap` |
| unparsable contract element | `UnknownAnalysisGap` |
| generic member with multiple matches | classification preserved plus generic-name warning |
| high fan-out match set | classification preserved plus fan-out warning |
| syntax invocation | `CallEdge` with containing member and callee name |
| semantic method invocation | Tier1 `CallEdge` with resolved caller and callee symbols |
| syntax object creation | `ObjectCreated` with created type and assigned variable when obvious |
| semantic object creation | Tier1 `ObjectCreated` with created type, constructor, caller, and assembly identity |
| semantic argument passed | Tier1 `ArgumentPassed` with parameter name/type and argument symbol/source location when available |
| semantic symbol identity | resolved semantic facts include stable C# `sourceSymbolId`/`targetSymbolId` properties where Roslyn exposes symbols |
| symbol index tables | `symbols`, `symbol_occurrences`, and `fact_symbols` rows link exact compiler-backed symbols to fact evidence |
| symbol relationship | Tier1 `SymbolRelationship` fact and `symbol_relationships` row for direct inheritance, interface implementation, member override, or interface member implementation |
| semantic local alias | Tier1 `LocalAlias` with alias symbol, origin symbol, rule ID, and evidence span |
| semantic field alias | Tier1 `FieldAlias` with field symbol, origin symbol, rule ID, and evidence span |
| semantic parameter forwarding | `parameter_forward_edges` row with source method/parameter, target method/parameter, rule ID, and evidence span |
| parameter flow report | `tracemap flow` chains direct forwarding, same-method aliases, and unique constructor field initialization while labeling limitations |
| relationship report | `tracemap relate` chains direct symbol relationships while labeling limitations |
| scoped scan | `tracemap scan --project`, `--solution`, `--include`, `--exclude`, `--target-framework`, and explicit `--restore` constrain scan/load behavior deterministically |
| flow boundary | Tier1 semantic boundary fact for DI, deserialization, reflection, dynamic invocation, mutation, or branch condition without claiming runtime flow |
| runtime evidence | Tier1 semantic fact for statically visible DI registration, serializer contract member, reflection target, dynamic dispatch candidate, collection element input, mutation semantics, or simple branch feasibility |
| contract mapping | Tier1 semantic fact for attribute route binding, table/column mapping, or literal configuration section binding |
| calculation expression | `CalculationExpression` with operator, line span, and expression hash |
| retry/backoff method | `RetryPolicyLogic` |
| generated or DI glue file | `InfrastructureBoilerplate` |
| TypeScript semantic property usage match | `DefiniteImpact` through existing .NET reducer |
| TypeScript syntax-only fallback | reduced or syntax-only coverage, never clean |
| TypeScript integration boundary | Tier1/Tier2/Tier3 according to compiler/package/shape evidence |

## Performance Smoke Targets

These are sanity checks, not strict benchmarks:

- small sample repo scans in under 10 seconds on a developer machine.
- external sample repos complete without unhandled exceptions.
- reducer runs complete in seconds for existing sample indexes.

## Review Checklist

- Did `dotnet build` pass?
- Did `dotnet test` pass?
- Can the CLI scan at least one sample repo?
- Can the CLI reduce at least one sample delta?
- Are facts deterministic and evidence-backed?
- Did the report avoid saying clean when coverage is reduced?
- Did rule catalog limitations change when reducer behavior changed?
