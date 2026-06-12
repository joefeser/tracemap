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

## Reducer Acceptance

For every successful `tracemap reduce --index <index> --contract-delta <delta> --out <report>` run, verify:

- impact report exists.
- every finding includes the reducer rule ID `contract.delta.reduce.v1`.
- matched findings include evidence rows.
- no-match findings include manifest coverage evidence.
- reduced coverage never produces `NoEvidenceFullCoverage`.

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
| analysis-gap evidence names changed element | `UnknownAnalysisGap` |
| unparsable contract element | `UnknownAnalysisGap` |
| generic member with multiple matches | classification preserved plus generic-name warning |
| high fan-out match set | classification preserved plus fan-out warning |
| syntax invocation | `CallEdge` with containing member and callee name |
| semantic method invocation | Tier1 `CallEdge` with resolved caller and callee symbols |
| syntax object creation | `ObjectCreated` with created type and assigned variable when obvious |
| semantic object creation | Tier1 `ObjectCreated` with created type, constructor, caller, and assembly identity |
| semantic argument passed | Tier1 `ArgumentPassed` with parameter name/type and argument symbol/source location when available |
| calculation expression | `CalculationExpression` with operator, line span, and expression hash |
| retry/backoff method | `RetryPolicyLogic` |
| generated or DI glue file | `InfrastructureBoilerplate` |

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
