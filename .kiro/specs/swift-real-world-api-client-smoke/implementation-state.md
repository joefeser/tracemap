# Swift Real-World API-Client Smoke Implementation State

Status: implemented
Issue: #431
Branch: `codex/issue-431-swift-real-world-smoke`
Public claim level: demo after generated summaries are reviewed for
publication.

## Shipped Scope

- Added `scripts/smoke-swift-real-world.sh`.
- Added pinned real-world Swift sample set:
  - `Dimillian/IceCubesApp` at
    `9c05a720597b3ff13de2e241bf58d3fba0863c09`.
  - `mastodon/mastodon-ios` at
    `95ac4a6d726ebf9fa867036dbf9d72f0a4b5f534`.
  - `kickstarter/ios-oss` at
    `203971bdf40f3a3a5071ce0c1fbc4eb3cad5b094`.
- The script scans selected repos with `tracemap-swift`, verifies required
  artifacts, and writes sanitized per-sample `summary.json` files plus
  `swift-real-world-smoke-summary.md`.
- Focused runs reject unknown labels and build Markdown summaries only from
  samples scanned in the current invocation, not stale summaries left in the
  output root.
- `TRACEMAP_SWIFT_REAL_WORLD_OFFLINE=1` supports cache-only reruns when pinned
  commits are already present; missing commits fail explicitly instead of
  silently requiring network access.
- Cached repositories verify their `origin` URL before reuse.
- SQLite summary queries fail loudly if the generated index cannot be read.
- The generated summaries use public repo slugs, pinned SHAs, artifact labels,
  counts, rule IDs, coverage labels, and limitations. They intentionally omit
  local absolute paths, clone URLs, raw remotes, source snippets, raw SQL,
  credentials, config values, hostnames, private labels, and runtime
  observations.
- Updated `README.md` and `docs/VALIDATION.md` with the command, sample table,
  focused-selection environment variable, and static-evidence limitations.

## Scope Decisions

- This is a validation harness only. It does not change Swift extractor
  behavior or claim stronger Swift analysis.
- The first sample set is intentionally small enough for an opt-in smoke while
  still covering real API-client/mobile app structures.
- Generated outputs stay under caller-provided local output roots and are not
  committed.
- `TRACEMAP_SWIFT_REAL_WORLD_REPOS` supports focused runs such as
  `TRACEMAP_SWIFT_REAL_WORLD_REPOS=icecubesapp`.

## Validation

- `bash -n scripts/smoke-swift-real-world.sh` passed.
- `swift build --package-path src/swift` passed.
- `swift run --package-path src/swift tracemap-swift-smoke-tests` passed.
- Unknown focused selection validation passed:
  `TRACEMAP_SWIFT_REAL_WORLD_REPOS=does-not-exist TRACEMAP_SKIP_BUILD=1 scripts/smoke-swift-real-world.sh ...`
  failed before scanning and printed the known labels.
- `TRACEMAP_SWIFT_REAL_WORLD_REPOS=icecubesapp TRACEMAP_SKIP_BUILD=1 scripts/smoke-swift-real-world.sh /tmp/tracemap-swift-real-world-cache /tmp/tracemap-swift-real-world-smoke` passed.
  - `icecubesapp` scan produced 29,686 facts.
  - Summary reported 4 HTTP/API facts, 601 UI surface facts, 20 storage/data
    facts, 74 package/dependency facts, and 2,411 analysis gaps.
  - The generated summary stayed path-safe and limitation-forward.
- `TRACEMAP_SWIFT_REAL_WORLD_REPOS=icecubesapp TRACEMAP_SWIFT_REAL_WORLD_OFFLINE=1 TRACEMAP_SKIP_BUILD=1 scripts/smoke-swift-real-world.sh /tmp/tracemap-swift-real-world-cache /tmp/tracemap-swift-real-world-smoke-review` passed.
  - The offline rerun reused only a verified local cache with the pinned commit.
- Focused summary isolation passed against a reused output root containing stale
  full-run artifacts; the generated Markdown included only `icecubesapp`.
- `TRACEMAP_SKIP_BUILD=1 scripts/smoke-swift-real-world.sh /tmp/tracemap-swift-real-world-cache /tmp/tracemap-swift-real-world-smoke-full` passed.
  - `icecubesapp`: 29,686 facts, 2,411 gaps, 4 HTTP/API, 601 UI,
    20 storage/data, 74 package/dependency.
  - `mastodon-ios`: 62,579 facts, 5,793 gaps, 9 HTTP/API, 586 UI,
    2,324 storage/data, 20 package/dependency.
  - `kickstarter-ios`: 156,892 facts, 11,349 gaps, 41 HTTP/API, 1,104 UI,
    264 storage/data, 106 package/dependency.
  - All three generated `0` duplicate fact IDs after Swift fact normalization.
- `dotnet build src/dotnet/TraceMap.sln` passed.
- `dotnet test src/dotnet/TraceMap.sln` passed: 697 tests.
- `git diff --check` passed.
- `./scripts/check-private-paths.sh` passed.

## Implementation Hardening

The first real-world smoke found duplicate identical Swift analysis-gap facts
that caused SQLite `facts.fact_id` uniqueness failures before `index.sqlite`
could be written. This slice now normalizes Swift facts before artifact writes:

- exact duplicate facts are removed while preserving original fact order;
- non-identical fact-id collisions keep a deterministic representative and emit
  a `swift-fact-id-collision` reduced-coverage `AnalysisGap`;
- collision gaps record both discarded occurrence count and distinct collision
  shape count;
- NDJSON, SQLite, reports, analyzer logs, and returned scan results all consume
  the same normalized fact list.

## Follow-Ups

- Add or rotate pinned repos if a sample stops providing useful static evidence.
- Add public-safe generated summary artifacts only after reviewing output for
  publication.
- Consider optional `wordpress-ios`, `wikipedia-ios`, or `firefox-ios` samples
  if we need larger app validation later.
