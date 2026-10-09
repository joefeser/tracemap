# Synthetic build-output dependency manifest

`bin/Debug/net8.0/Sample.deps.json` is deliberately committed test input using
synthetic package/project names. It represents an SDK-shaped emitted dependency
graph; it is not a real build receipt or package download. Do not build this fixture.

Run `tracemap scan --repo samples/deps-json-evidence --out <outside-output> --index-deps-json`.
Without the flag no dependency rows are emitted from bin. No build is initiated.

## Evidence contract

- Package facts use `manifestKind=deps.json`, NuGet package names and resolved versions,
  per-file full target keys (including RID when present), and `dependencyRelation`.
- `libraries` must corroborate the exact `name/version` key as type `package`.
  Project libraries are graph nodes only, never package rows.
- Relations describe the emitted target graph relative to its unique project root.
  Missing/ambiguous roots or edges yield unknown relation plus an AnalysisGap.
- `evidenceSource=build-output`, `freshness=unknown`, `buildCommitSha=unknown`:
  the ordinary fact commit is the scan context, not the commit which built the file.
- `manifestPath`, exact `manifestSha256`, JSON-pointer `metadataLocation` and a
  whole-document evidence span bind each observation. `generatorSha256` hashes
  the extractor assembly; `boundedInputSha256` binds bounded observed manifests,
  categorical gaps and limits. These are local raw-input hashes, not a shareable
  privacy projection or NuGet package artifact hashes.
- This format can omit build-only/compile-only dependencies. Absence proves
  neither absence from restore closure nor absence from an application's source.

## Boundaries

Discovery does not follow links, enter .git/obj/node_modules/.nuget/.tracemap, or
read the scan output subtree. Include/exclude file globs and explicit project
directories constrain admitted manifests. Each file is read once with a bounded
buffer; concurrent unreadable/truncated output yields a gap rather than changing
source-snapshot rules. Bytes successfully observed may already be stale, and
no atomic multi-file build snapshot is claimed.

Defaults: 128 manifests, 8 MiB/file, 64 MiB total observed bytes, 100,000 discovery
entries and 20,000 target libraries/file. Limits emit gaps and reduced coverage;
no-file discovery also emits an explicit gap. Core API `DepsJsonLimits` permits
bounded test/operator overrides. `bin` is never added to FileInventory or the
source snapshot. Source changes still fail the existing snapshot guard.

Format references: Microsoft's [DependencyContextJsonReader](https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.DependencyModel/src/DependencyContextJsonReader.cs) and SDK
[DependencyContextBuilder](https://github.com/dotnet/sdk/blob/main/src/Tasks/Microsoft.NET.Build.Tasks/DependencyContextBuilder.cs). Neither provides a top-level direct-dependency list.

TraceMap's package-decision correlation and package-impact reports exclude these
unknown-build observations from current-source findings and report an explicit
freshness/coverage gap. The original package facts remain available to consumers
that preserve build-output provenance. Core limit overrides may lower, but never
raise, the documented resource caps. Opt-in options are init-only properties so
the existing positional ScanOptions constructor and deconstruction remain intact.
