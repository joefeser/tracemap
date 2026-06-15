# Package Upgrade Impact Implementation State

Status: implemented

Branch: `codex/package-upgrade-impact`

## Shipped Scope

- Added `tracemap package-impact --index <index.sqlite> --package-delta <delta.json> --out <path>`.
- Supports single-language and combined indexes.
- Matches existing static `PackageReferenced` package evidence only, across NuGet, npm, Maven/Gradle, and pip where adapters emit deterministic package facts.
- Emits deterministic Markdown and JSON with safe package metadata, rule IDs, evidence tiers, file spans, source labels, scan IDs, commit SHAs, coverage labels, limitations, and analysis gaps.
- Accepts optional delta provenance fields and hashes repository identifiers instead of rendering raw values.
- Adds source/package/ecosystem selectors, finding/gap caps, and `--exit-code`.
- Keeps package upgrade conclusions bounded to static evidence; no registry, vulnerability, compatibility, transitive dependency, runtime loading, LLM, embedding, vector, or prompt-based analysis.

## Scope Decisions

- V1 uses one index plus a package delta. Before/after package diff composition can be layered through release-review later.
- Version values are descriptive and safely rendered or hashed. TraceMap does not evaluate semver or compatibility.
- Missing evidence under reduced coverage emits `UnknownAnalysisGap`; full-coverage no-match emits `NoStaticPackageEvidence`.
- Lockfile matching is supported when adapters emit lockfile rows as `PackageReferenced`; import/usage and package-call matching remains adapter-dependent and requires deterministic package identity facts with documented rule limitations. The current implementation does not infer package usage from raw imports or calls.

## Validation

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter PackageUpgradeImpactTests` passed with 10 tests.
- `dotnet build src/dotnet/TraceMap.sln` passed.
- `dotnet test src/dotnet/TraceMap.sln` passed with 238 tests.
- CLI smoke passed:
  - `dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/modern-sample --out /tmp/tracemap-package-impact-smoke/scan`
  - `dotnet run --project src/dotnet/TraceMap.Cli -- package-impact --index /tmp/tracemap-package-impact-smoke/scan/index.sqlite --package-delta samples/package-deltas/package-delta.example.json --out /tmp/tracemap-package-impact-smoke/report`
- `./scripts/check-private-paths.sh` passed.
- `git diff --check` passed.

## Review

- Attempted local Kiro implementation review with `node scripts/kiro-review.mjs --phase package-upgrade-impact --kind implementation --model auto --fresh --timeout-ms 1800000`.
- The wrapper stalled before spawning `kiro-cli` and was stopped; no Sonnet/Opus review text was produced.
- Completed self-review. One precision issue was found and fixed: delta ecosystem selectors now require matching ecosystem evidence instead of matching missing ecosystem values. Added a regression test.
- PR review loop fixed:
  - sanitized package-impact source rows so raw remotes are not serialized in JSON,
  - scoped coverage warnings to selected sources,
  - stopped treating zero package rows as reduced coverage by itself,
  - limited package-impact matches to real `PackageReferenced` package evidence,
  - normalized delta IDs before duplicate checks,
  - hardened malformed single-index `properties_json` parsing,
  - clarified delta provenance, required ecosystems, and adapter-dependent lockfile/import/call evidence boundaries.

## Follow-Ups

- Add before/after package impact composition in `release-review`.
- Add package usage/import/call evidence when adapter-specific facts can prove those package links.
- Add optional path/reverse context for combined package surfaces.
