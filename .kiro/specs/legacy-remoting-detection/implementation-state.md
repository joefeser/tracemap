# Legacy Remoting Detection Implementation State

Status: implemented
Branch: codex/legacy-remoting-detection
Base: dev at abb174db91112ea0d7a9817cd05a9ccc4f4803ba
Public claim level: hidden

## Scope

Status normalized during spec-state cleanup. The Remoting detector, sample,
rule IDs, reports, focused tests, and validation notes are present on `dev`;
remaining public smoke and richer semantic support are tracked as follow-ups.

Implemented deterministic static .NET Remoting evidence extraction as a sibling
detector to WCF/SVC. The implementation adds Remoting-specific fact types, rule
IDs, extractor versioning, C# syntax fallback, conservative semantic correlation
from existing Roslyn symbol facts, safe XML config extraction, report summaries,
focused tests, and a checked-in synthetic sample.

## Scope Decisions

- Remoting facts remain distinct from WCF/SVC fact families and rules.
- Syntax-only namespace, API, channel, registration, activation, and direct
  `MarshalByRefObject` matches emit `Tier3SyntaxOrTextual` with limitations.
- Compiler-resolved direct `System.MarshalByRefObject` inheritance is promoted
  to `Tier1Semantic`; duplicate syntax rows for the same declaration are
  suppressed.
- `<system.runtime.remoting>` config evidence emits `Tier2Structural` facts
  with line spans where XML line info is available.
- Unsafe endpoint/config values such as URLs, object URIs, ports, application
  names, channel/provider properties, and arbitrary arguments are hashed or
  omitted.
- `Activator.GetObject` is emitted only when same-file Remoting context exists;
  otherwise it becomes a review gap.
- Activated service/client registration calls are visible as
  `AnalysisGap` facts with `activated-type-registration-v1-deferred`.
- No runtime host activation, endpoint probing, exploitability classification,
  production-usage inference, LLM calls, embeddings, vectors, or prompt-based
  classification were added.

## Validation

- `dotnet build src/dotnet/TraceMap.sln`: passed.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyRemotingExtractorTests`: passed.
- `dotnet test src/dotnet/TraceMap.sln`: passed.
- Synthetic sample scan:
  `dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/dotnet-remoting-sample --out /tmp/tracemap-remoting-scan.10Ce2w`
  passed and produced `scan-manifest.json`, `facts.ndjson`, `index.sqlite`,
  `report.md`, `logs/analyzer.log`, and Remoting facts.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- Public/pinned Remoting smoke: not run; no pinned public Remoting baseline
  exists yet. `docs/VALIDATION.md` now documents this explicit deferral and
  requires a separate reviewed baseline task/spec before committing public
  repository baselines.

## Oddities

- The synthetic sample targets `net10.0`; framework Remoting APIs do not
  compile there, so useful output relies on syntax/config fallback while
  existing semantic inheritance facts can still resolve `MarshalByRefObject`.
- Config `type="Namespace.Type, Assembly"` is split into safe `typeName` and
  `assemblyName` properties when possible; unsafe forms are hashed.
- The scanner may produce reduced coverage when MSBuild cannot load legacy
  Remoting projects, but Remoting syntax/config extraction continues.

## Follow-Ups

- Add a separately reviewed public Remoting smoke baseline after redaction and
  claim-level review.
- Consider richer semantic Remoting API symbol extraction if future target
  frameworks or references make `System.Runtime.Remoting` symbols available.
- Consider deterministic support for more activated-registration overload
  details, machine.config, transforms, encrypted sections, and external config
  includes in later specs.
