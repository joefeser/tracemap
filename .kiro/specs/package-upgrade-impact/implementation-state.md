# Package Upgrade Impact Implementation State

Status: implemented

Branch: `codex/package-upgrade-impact`

## Shipped Scope

- Added `tracemap package-impact --index <index.sqlite> --package-delta <delta.json> --out <path>`.
- Supports single-language and combined indexes.
- Matches existing static `PackageReferenced`/`package-config` evidence only.
- Emits deterministic Markdown and JSON with safe package metadata, rule IDs, evidence tiers, file spans, source labels, scan IDs, commit SHAs, coverage labels, limitations, and analysis gaps.
- Adds source/package/ecosystem selectors, finding/gap caps, and `--exit-code`.
- Keeps package upgrade conclusions bounded to static evidence; no registry, vulnerability, compatibility, transitive dependency, runtime loading, LLM, embedding, vector, or prompt-based analysis.

## Scope Decisions

- V1 uses one index plus a package delta. Before/after package diff composition can be layered through release-review later.
- Version values are descriptive and safely rendered or hashed. TraceMap does not evaluate semver or compatibility.
- Missing evidence under reduced coverage emits `UnknownAnalysisGap`; it is not a clean no-impact result.

## Validation

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter PackageUpgradeImpactTests` passed with 5 tests.
- `dotnet build src/dotnet/TraceMap.sln` passed.
- `dotnet test src/dotnet/TraceMap.sln` passed with 233 tests.
- CLI smoke passed:
  - `dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/modern-sample --out /tmp/tracemap-package-impact-smoke/scan`
  - `dotnet run --project src/dotnet/TraceMap.Cli -- package-impact --index /tmp/tracemap-package-impact-smoke/scan/index.sqlite --package-delta samples/package-deltas/package-delta.example.json --out /tmp/tracemap-package-impact-smoke/report`
- `./scripts/check-private-paths.sh` passed.
- `git diff --check` passed.

## Review

- Attempted local Kiro implementation review with `node scripts/kiro-review.mjs --phase package-upgrade-impact --kind implementation --model auto --fresh --timeout-ms 1800000`.
- The wrapper stalled before spawning `kiro-cli` and was stopped; no Sonnet/Opus review text was produced.
- Completed self-review. One precision issue was found and fixed: delta ecosystem selectors now require matching ecosystem evidence instead of matching missing ecosystem values. Added a regression test.

## Follow-Ups

- Add before/after package impact composition in `release-review`.
- Add lockfile-specific evidence if adapters emit deterministic lockfile facts later.
- Add package usage/import/call evidence once adapter-specific rules can prove those links.
- Add optional path/reverse context for combined package surfaces.
