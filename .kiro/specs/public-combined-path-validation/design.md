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

src/dotnet/
  TraceMap.Reporting/
  tests/TraceMap.Tests/

rules/
  rule-catalog.yml
```

The existing endpoint samples should be used, but the server sample must be extended before implementation because it currently has route actions with no downstream service/repository/SQL evidence. The minimum fixture target is endpoint-to-`sql-query`. Reachable `package-config` is useful but deferred unless the fixture naturally supports it.

## Implementation Slices

This spec should be implemented as two PRs if the linkage spike confirms the expected symbol mismatch:

1. Path-graph linkage PR: reproduce the scanned-symbol naming mismatch with focused tests, add deterministic source-local symbol reconciliation, and document any new derived rule IDs.
2. Public smoke PR: extend the public server fixture, add `scripts/smoke-combined-paths.sh`, and update README/validation docs.

If the spike unexpectedly shows real scanned endpoint-to-SQL paths already work after only the sample extension, the path-graph linkage PR can be skipped.

## Linkage Spike

Before writing the smoke script, run the existing samples through the real scanners:

```bash
node src/typescript/dist/src/cli.js scan --repo samples/endpoint-client-angular --out <out>/client
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/endpoint-server-aspnet --out <out>/server
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index <out>/client/index.sqlite --label sample-client --index <out>/server/index.sqlite --label sample-server --out <out>/combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index <out>/combined.sqlite --out <out>/dependency-report
dotnet run --project src/dotnet/TraceMap.Cli -- paths --index <out>/combined.sqlite --out <out>/paths
```

Record:

- matched endpoint keys,
- route fact source symbols,
- call-edge source and target symbols,
- SQL/query fact source symbols after the fixture extension,
- whether SQL appears as a reachable terminal or only as an `UnlinkedSurface` gap.

The spike output is diagnostic only and should not be committed.

## Symbol Reconciliation Design

The path graph currently keys symbol nodes by source-local display string. Real scanner output can contain compatible but non-identical strings for the same method: full method signatures, type-only route symbols, controller-method short names, and bare syntax member names.

If this prevents scanned endpoint-to-SQL paths, add a conservative reconciliation layer in `TraceMap.Reporting`:

- Scope all aliasing to a single `sourceIndexId`.
- Prefer exact symbol IDs and exact display names first.
- Consider short-name aliases only when source-local evidence makes the match credible.
- Treat syntax/name-only reconciliation as review-tier evidence; resulting paths should be `NeedsReviewPath` unless stronger evidence exists for every hop.
- Add a derived edge kind such as `symbol-alias` or a clearly named attachment edge only with a documented rule ID and limitations.
- Include supporting fact IDs or combined edge IDs where available.
- Do not use runtime assumptions, DI, reflection, serializer behavior, or branch feasibility to create aliases.
- If multiple candidates share the same short name, emit a gap or review-tier ambiguity instead of choosing one silently.

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

- at least one endpoint finding for `/api/admin/runner/get-by-id/{}` classified as `MatchedEndpoint` or review-tier `AmbiguousMatch`
- report coverage is present
- source labels are `sample-client` and `sample-server`

Path report assertions:

- `summary.pathCount > 0` for default or targeted query
- at least one path contains an `endpoint-match` edge
- at least one path crosses from `sample-client` to `sample-server`
- at least one terminal node has `surfaceKind = "sql-query"`
- the SQL path includes at least one code traversal or documented reconciliation edge between the server route and the SQL terminal
- SQL evidence reported only as `UnlinkedSurface` does not count as success
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

Scan manifests and analyzer logs may legitimately contain absolute paths to the checked-in sample directories or temporary output roots. Those files are generated artifacts and must not be committed; public Markdown and docs remain sanitized.

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
| Route/call/query facts do not share symbol strings in real scans | Add source-local, rule-backed symbol reconciliation before requiring the smoke assertion. |
| Fixture does not emit reachable package/config evidence | Defer package-config assertion unless the extension naturally creates it. |
| Smoke becomes too slow | Keep default sample-only and keep OSS in the existing separate script. |
| Docs accidentally mention private paths | Run private path guard and avoid external path support in this slice. |
| Path assertions become brittle | Assert semantic JSON fields and counts, not exact full Markdown bytes. |
| Toolchain prerequisites differ by machine | Fail with clear messages and document prerequisites. |
| Generated outputs are accidentally committed | Use `mktemp` by default and generic `.gitignore` patterns only if needed. |

## Decisions From Review

- The default smoke requires endpoint-to-`sql-query` after extending the public fixture.
- The linkage spike happens before the script; if it exposes a symbol mismatch, the engine fix is a prerequisite.
- `package-config` reachability is deferred unless the fixture naturally proves it.
- The new script stays independent from `scripts/demo-public.sh`; docs explain when to run each.
- OSS remains delegated to `scripts/smoke-open-source-repos.sh`.
- README should include commands, expected artifacts, and at most one clearly labeled illustrative snippet, not captured full reports.
