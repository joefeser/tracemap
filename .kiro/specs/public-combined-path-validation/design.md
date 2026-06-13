# Public Combined Path Validation Design

## Overview

Add a public validation and demo workflow for TraceMap's combined dependency path analysis.

The workflow should be easy to run:

```bash
./scripts/smoke-combined-paths.sh /tmp/tracemap-combined-paths-smoke
```

The script should produce:

```text
<out>/
  client/
    scan-manifest.json
    facts.ndjson
    index.sqlite
    report.md
    logs/analyzer.log
  server/
    scan-manifest.json
    facts.ndjson
    index.sqlite
    report.md
    logs/analyzer.log
  combined.sqlite
  dependency-report/
    dependency-report.md
    dependency-report.json
  paths/
    paths-report.md
    paths-report.json
```

The output directory is generated and should not be committed.

The first implementation is sample-only. It should not reach the network, clone OSS repositories, or read external application paths.

## Goals

- Prove `scan -> combine -> report -> paths` end to end.
- Demonstrate cross-index endpoint-to-surface paths with public fixtures.
- Keep validation deterministic and review-friendly.
- Keep the open-source repository free of private paths and private repo names.
- Make reviewer loops easier by giving bots and humans a single high-signal command.

## Non-Goals

- No new analyzer features unless the validation exposes a blocker.
- No runtime traffic capture.
- No committed SQLite/report artifacts.
- No private repo-specific script defaults.
- No CI workflow unless explicitly added in a follow-up.
- No graph visualization in this slice.
- No optional external repo mode in this slice.
- No OSS network smoke inside this script.

## Proposed Files

```text
scripts/
  smoke-combined-paths.sh

docs/
  VALIDATION.md

README.md

samples/
  endpoint-client-angular/
  endpoint-server-aspnet/
```

The existing endpoint samples should be used, but the server sample must be extended before implementation because it currently has route actions with no downstream service/repository/SQL evidence. The minimum fixture target is endpoint-to-`sql-query`. Reachable `package-config` is useful but deferred unless the fixture naturally supports it.

## Smoke Script Shape

Command:

```bash
./scripts/smoke-combined-paths.sh [out_dir]
```

Environment variables:

```text
none for the first implementation
```

Default behavior:

- Use checked-in samples only.
- Write to `mktemp -d` when no output directory is supplied.
- Build TypeScript CLI if needed.
- Use `dotnet run --project src/dotnet/TraceMap.Cli`.
- Fail fast on missing artifacts or failed assertions.
- Perform semantic JSON assertions with Node.js, which is already required by the TypeScript adapter.
- Do not require `jq`.
- Do not access the network.

Reserved follow-up environment variables:

```text
TRACEMAP_EXTERNAL_CLIENT_REPO=<path>
TRACEMAP_EXTERNAL_SERVER_REPO=<path>
TRACEMAP_EXTERNAL_SERVER_PROJECT=<path>
```

If a later slice implements these, diagnostics must print only labels, basenames, or hashes for external paths, never full absolute paths.

## Detailed Flow

1. Resolve repo root.
2. Resolve output root.
3. Build TypeScript CLI.
4. Build .NET CLI or rely on `dotnet run`.
5. Scan `samples/endpoint-client-angular` into `<out>/client`.
6. Scan `samples/endpoint-server-aspnet` into `<out>/server`.
7. Assert scan artifacts exist for both indexes.
8. Run:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- combine \
  --index <out>/client/index.sqlite --label sample-client \
  --index <out>/server/index.sqlite --label sample-server \
  --out <out>/combined.sqlite
```

9. Run:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- report \
  --index <out>/combined.sqlite \
  --out <out>/dependency-report
```

10. Run a default paths query:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- paths \
  --index <out>/combined.sqlite \
  --out <out>/paths
```

11. Run targeted paths queries when fixture evidence supports them:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- paths \
  --index <out>/combined.sqlite \
  --from-endpoint "GET /api/admin/runner/get-by-id/{}" \
  --to-surface sql-query \
  --out <out>/paths-runner-sql
```

12. Run a deliberately bogus selector query and assert it produces a valid zero-path report with a rule-backed gap.
13. Re-run the successful targeted paths query into a second output directory and compare `paths-report.json` bytes for deterministic output.
14. Inspect JSON with Node.js.
15. Print summary.

## Assertions

Artifact assertions:

- scan manifest exists
- facts NDJSON exists
- SQLite index exists
- human report exists
- analyzer log exists
- combined SQLite exists
- dependency report Markdown and JSON exist
- paths report Markdown and JSON exist

Dependency report assertions:

- at least one `MatchedEndpoint`
- report coverage is present
- source labels are `sample-client` and `sample-server`

Path report assertions:

- `summary.pathCount > 0` for default or targeted query
- at least one path contains an `endpoint-match` edge
- at least one path crosses from `sample-client` to `sample-server`
- at least one terminal node has `surfaceKind = "sql-query"`
- at least one returned path has classification `StrongStaticPath`, `ProbableStaticPath`, or `NeedsReviewPath`
- every path edge has a non-empty `ruleId`
- every path edge has a non-empty `evidenceTier`
- every gap has a non-empty `ruleId`
- every gap has a non-empty `evidenceTier`
- the same normalized path key appears in client and server endpoint evidence for the matched route
- a bogus endpoint selector produces a valid report with zero paths and a rule-backed gap
- repeated targeted `paths` output is byte-stable
- Markdown does not contain fixture sentinel tokens, raw SQL text, raw config values, or developer-local absolute paths

Reachable `package-config` path assertion is deferred. If the sample extension naturally creates a reachable config/package surface, the implementation may add a non-blocking assertion or promote it with reviewer agreement.

## Fixture Guidance

The checked-in full-stack fixture should be intentionally small:

- Angular/TypeScript client service calls a static API URL.
- ASP.NET server exposes a matching route.
- Controller calls service/repository code through static evidence.
- Repository emits SQL/query facts using hash-only rendering.
- SQL text includes a synthetic sentinel token that the smoke can assert does not appear in Markdown.
- Optional config text includes a synthetic sentinel token if config rendering safety is asserted.

This fixture should be boring on purpose. It is a validation target, not a product sample app.

## OSS Strategy

Pinned OSS repositories are useful for breadth, but they are not guaranteed to contain a clean client/server pair that proves path queries. Treat OSS scans as confidence checks and keep them in `scripts/smoke-open-source-repos.sh`:

- scan completes,
- artifacts exist,
- coverage is honestly labeled,
- important tables are populated where evidence exists,
- combine/report/paths do not crash on larger real indexes when that separate smoke is extended to run them.

`docs/VALIDATION.md` should keep the current pinned table and add a short section explaining which repos were used for broad smoke versus path-specific assertions.

## Output Safety

The smoke must not render or commit:

- raw SQL text,
- raw config values,
- connection strings,
- raw source snippets,
- developer-local absolute paths,
- private repository names.

Use existing privacy checks:

```bash
./scripts/check-private-paths.sh
git diff --check
```

Generated output directories should be ignored if they are predictable. This script should use `mktemp` by default and should not need new ignore rules.

## Documentation Updates

README should get a short public workflow:

```bash
./scripts/smoke-combined-paths.sh
```

and a manual command sketch:

```bash
tracemap combine --index client/index.sqlite --label client --index api/index.sqlite --label api --out combined.sqlite
tracemap report --index combined.sqlite --out dependency-report
tracemap paths --index combined.sqlite --from-endpoint "GET /api/admin/runner/get-by-id/{}" --to-surface sql-query --out paths
```

`docs/VALIDATION.md` should describe:

- when to run the smoke,
- prerequisites,
- expected outputs,
- expected assertions,
- how the separate OSS smoke relates to this sample-only smoke,
- which external validation env var names are reserved for a follow-up,
- how to interpret reduced coverage.

## Risks

| Risk | Mitigation |
| --- | --- |
| Fixture does not currently emit enough SQL path evidence | Extend checked-in samples with small deterministic evidence before adding the script. |
| Fixture does not emit reachable package/config evidence | Defer package-config assertion unless the extension naturally creates it. |
| Smoke becomes too slow | Keep default sample-only and keep OSS in the existing separate script. |
| Docs accidentally mention private paths | Run private path guard and avoid external path support in this slice. |
| Path assertions become brittle | Assert semantic JSON fields and counts, not exact full Markdown bytes. |
| Toolchain prerequisites differ by machine | Fail with clear messages and document prerequisites. |
| Generated outputs are accidentally committed | Use `mktemp` by default and generic `.gitignore` patterns only if needed. |

## Decisions From Review

- The default smoke requires endpoint-to-`sql-query` after extending the public fixture.
- `package-config` reachability is deferred unless the fixture naturally proves it.
- The new script stays independent from `scripts/smoke-typescript-endpoints.sh`; docs explain when to run each.
- OSS remains delegated to `scripts/smoke-open-source-repos.sh`.
- README should include commands, expected artifacts, and at most one clearly labeled illustrative snippet, not captured full reports.
