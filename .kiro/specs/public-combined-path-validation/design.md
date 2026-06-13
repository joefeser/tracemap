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

The existing endpoint samples should be used first. If they do not currently emit enough evidence for SQL/config/package path assertions, extend them in small, realistic ways.

## Smoke Script Shape

Command:

```bash
./scripts/smoke-combined-paths.sh [out_dir]
```

Environment variables:

```text
TRACEMAP_INCLUDE_OSS=0|1
TRACEMAP_OSS_CACHE=<path>
TRACEMAP_EXTERNAL_CLIENT_REPO=<path>
TRACEMAP_EXTERNAL_SERVER_REPO=<path>
TRACEMAP_EXTERNAL_SERVER_PROJECT=<path>
```

Default behavior:

- Use checked-in samples only.
- Write to `mktemp -d` when no output directory is supplied.
- Build TypeScript CLI if needed.
- Use `dotnet run --project src/dotnet/TraceMap.Cli`.
- Fail fast on missing artifacts or failed assertions.

Optional behavior:

- If `TRACEMAP_INCLUDE_OSS=1`, run or invoke pinned OSS smoke in a separate cache/output area.
- If external repo env vars are supplied, scan those repos using generic labels such as `external-client` and `external-server`.
- Optional external validation must never be required for default success.

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
  --from-endpoint "GET /api/orders/{}" \
  --to-surface sql-query \
  --out <out>/paths-orders-sql
```

12. Inspect JSON with Node.js or another already-required local runtime.
13. Print summary.

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
- at least one terminal node has `surfaceKind = "sql-query"` when SQL fixture evidence exists
- at least one terminal node has `surfaceKind = "package-config"` when config/package fixture evidence exists
- every path edge has a non-empty `ruleId`
- every path edge has a non-empty `evidenceTier`
- every gap has a non-empty `ruleId`
- every gap has a non-empty `evidenceTier`
- Markdown contains `source transition:`
- Markdown does not contain raw SQL text, raw config values, or developer-local absolute paths

If a fixture cannot support one of these assertions yet, the implementation should either extend the fixture or document that assertion as deferred. It should not leave a weak file-existence-only smoke.

## Fixture Guidance

The checked-in full-stack fixture should be intentionally small:

- Angular/TypeScript client service calls a static API URL.
- ASP.NET server exposes a matching route.
- Controller calls service/repository code through static evidence.
- Repository emits SQL/query facts using hash-only rendering.
- Startup/config code emits config or package facts.

This fixture should be boring on purpose. It is a validation target, not a product sample app.

## OSS Strategy

Pinned OSS repositories are useful for breadth, but they are not guaranteed to contain a clean client/server pair that proves path queries. Treat OSS scans as confidence checks:

- scan completes,
- artifacts exist,
- coverage is honestly labeled,
- important tables are populated where evidence exists,
- combine/report/paths do not crash on larger real indexes.

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

Generated output directories should be ignored if they are predictable. Prefer `mktemp` defaults to avoid new ignore rules.

## Documentation Updates

README should get a short public workflow:

```bash
./scripts/smoke-combined-paths.sh
```

and a manual command sketch:

```bash
tracemap combine --index client/index.sqlite --label client --index api/index.sqlite --label api --out combined.sqlite
tracemap report --index combined.sqlite --out dependency-report
tracemap paths --index combined.sqlite --from-endpoint "GET /api/orders/{}" --to-surface sql-query --out paths
```

`docs/VALIDATION.md` should describe:

- when to run the smoke,
- prerequisites,
- expected outputs,
- expected assertions,
- how to run optional OSS smoke,
- how to run optional generic external validation,
- how to interpret reduced coverage.

## Risks

| Risk | Mitigation |
| --- | --- |
| Fixture does not currently emit enough SQL/config path evidence | Extend checked-in samples with small deterministic evidence. |
| Smoke becomes too slow | Keep default sample-only; make OSS optional. |
| Docs accidentally mention private paths | Run private path guard and use generic env var names only. |
| Path assertions become brittle | Assert semantic JSON fields and counts, not exact full Markdown bytes. |
| Toolchain prerequisites differ by machine | Fail with clear messages and document prerequisites. |
| Generated outputs are accidentally committed | Use `mktemp` by default and generic `.gitignore` patterns only if needed. |

## Open Questions for Review

- Should the default smoke require both `sql-query` and `package-config` paths, or should one be optional until the sample fixture is extended?
- Should the smoke script call the existing endpoint smoke script or stay independent for clearer output?
- Should optional OSS mode live in this script or remain delegated to `scripts/smoke-open-source-repos.sh`?
- Should the README include command output snippets, or only commands and output file descriptions?
