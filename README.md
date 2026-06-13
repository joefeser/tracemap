# TraceMap

TraceMap is a deterministic, evidence-backed repository indexer and contract-change reducer. It scans source repositories into rule-backed facts, writes queryable artifacts, and reduces contract deltas into Markdown impact reports without using LLMs, embeddings, or prompt-based classification in the core scanner/reducer.

The current language scanners are:

- `.NET/C#` under `src/dotnet`, including semantic Roslyn extraction, syntax fallback, flow/export commands, and contract reduction.
- `TypeScript` under `src/typescript`, including compiler-backed facts, syntax fallback, integration facts, and reducer-compatible SQLite output.
- `JVM/Java/Kotlin` under `src/jvm`, including Java compiler-backed facts, Java/Kotlin syntax fallback, Maven/Gradle metadata, integration facts, and reducer-compatible SQLite output.
- `Python` under `src/python`, including AST/package/config/SQL extraction, FastAPI/Flask/Pydantic/SQLAlchemy/httpx/requests integration facts, reduced coverage labeling, and reducer-compatible SQLite output.

TraceMap can also combine multiple indexes into one provenance-preserving SQLite database, generate a combined dependency report, query static dependency paths through the combined graph, diff two combined snapshots, and align client/server endpoint evidence across two existing indexes, such as an Angular client index and an ASP.NET API index.

Start here:

- [Product requirements](docs/PRD.md)
- [Acceptance plan](docs/ACCEPTANCE.md)
- [Validation guide](docs/VALIDATION.md)
- [Decision log](docs/DECISIONS.md)
- [Build milestones](MILESTONES.md)
- [Language adapter contract](docs/LANGUAGE_ADAPTER_CONTRACT.md)
- [Rule catalog](rules/rule-catalog.yml)

TraceMap is not an AI impact-analysis tool. The scanner and reducer do not use LLM calls, embeddings, vector databases, or prompt-based classification.

## Quick Start

Build and test everything:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
cd src/typescript
npm install
npm run check
cd ../..
JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm test
python3 -m venv /tmp/tracemap-python-venv
/tmp/tracemap-python-venv/bin/python -m pip install -e "src/python[dev]"
/tmp/tracemap-python-venv/bin/python -m pytest src/python/tests
```

Run the pinned public open-source smoke set when changing adapter behavior:

```bash
scripts/smoke-open-source-repos.sh /tmp/tracemap-oss-cache /tmp/tracemap-oss-smoke
```

Run the public combined-path smoke when changing `combine`, `report`, `paths`, endpoint extraction, or dependency-surface extraction:

```bash
./scripts/smoke-combined-paths.sh
```

See [Validation guide](docs/VALIDATION.md) for the language matrix, expected artifact/table checks, and pinned repository SHAs.

.NET/C# scan and reduce:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/modern-sample --out .tracemap
dotnet run --project src/dotnet/TraceMap.Cli -- reduce --index .tracemap/index.sqlite --contract-delta samples/contract-deltas/modern-sample.customer-profile.json --out .tracemap/impact-report.md
```

Useful .NET index commands:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- flow --index .tracemap/index.sqlite --symbol request --out .tracemap/flow-report.md
dotnet run --project src/dotnet/TraceMap.Cli -- export --index .tracemap/index.sqlite --out .tracemap/index-export.json --format json
dotnet run --project src/dotnet/TraceMap.Cli -- export --index .tracemap/index.sqlite --out .tracemap/relationships.mmd --format mermaid
```

Combine multiple indexes for cross-repo or cross-language dependency queries:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- combine \
  --index .tracemap/index.sqlite --label dotnet-sample \
  --index .tracemap-ts/index.sqlite --label typescript-sample \
  --out .tracemap-combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index .tracemap-combined.sqlite --out .tracemap-combined-report
dotnet run --project src/dotnet/TraceMap.Cli -- paths --index .tracemap-combined.sqlite --from-endpoint "GET /api/admin/runner/get-by-id/{}" --to-surface sql-query --out .tracemap-combined-paths
dotnet run --project src/dotnet/TraceMap.Cli -- export --index .tracemap-combined.sqlite --out .tracemap-combined.json --format json
```

The combined dependency report writes `dependency-report.md` and `dependency-report.json` when `--out` is a directory. It summarizes source coverage, endpoint alignment, HTTP/SQL/package/config surfaces, dependency edges, needs-review rows, known gaps, and static-analysis limitations without mutating the combined database.

The combined dependency paths command writes `paths-report.md` and `paths-report.json` when `--out` is a directory. It follows static evidence from endpoint, symbol, or source selectors to terminal dependency surfaces such as `sql-query`, `http-client`, `http-route`, and `package-config`. Paths are evidence trails, not runtime traces.

Compare two combined snapshots:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- diff \
  --before .tracemap-before-combined.sqlite \
  --after .tracemap-after-combined.sqlite \
  --out .tracemap-combined-diff
dotnet run --project src/dotnet/TraceMap.Cli -- diff \
  --before .tracemap-before-combined.sqlite \
  --after .tracemap-after-combined.sqlite \
  --include-paths \
  --scope all \
  --out .tracemap-combined-diff-with-paths
```

The combined dependency diff command writes `diff-report.md` and `diff-report.json` when `--out` is a directory. It compares static evidence keys for sources, coverage, endpoints, dependency surfaces, dependency edges, and opt-in path signatures. Added/removed conclusions are coverage-relative; reduced coverage or source identity uncertainty is labeled as a gap or review-tier diff rather than runtime impact.

Summarize static change-impact evidence from two combined snapshots:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- impact \
  --before .tracemap-before-combined.sqlite \
  --after .tracemap-after-combined.sqlite \
  --out .tracemap-combined-impact
dotnet run --project src/dotnet/TraceMap.Cli -- impact \
  --before .tracemap-before-combined.sqlite \
  --after .tracemap-after-combined.sqlite \
  --scope endpoints,surfaces \
  --exit-code \
  --out .tracemap-combined-impact
```

The combined change impact command writes `impact-report.md` and `impact-report.json` when `--out` is a directory. It reuses combined diff evidence to classify changed sources, coverage, endpoints, dependency surfaces, and dependency edges as static impact evidence, probable static impact, needs review, or analysis gaps. This report is not runtime impact analysis; path context is off by default and only opt-in path diff evidence is included with `--include-paths`.

TypeScript scanner:

```bash
cd src/typescript
npm install
npm run build
node dist/src/cli.js scan --repo ../../samples/typescript-modern-sample --out ../../.tracemap-ts
node dist/src/cli.js export --index ../../.tracemap-ts/index.sqlite --out ../../.tracemap-ts/index-export.json --format json
node dist/src/cli.js export --index ../../.tracemap-ts/index.sqlite --out ../../.tracemap-ts/relationships.mmd --format mermaid
cd ../..
dotnet run --project src/dotnet/TraceMap.Cli -- reduce --index .tracemap-ts/index.sqlite --contract-delta samples/contract-deltas/typescript-modern.status.json --out .tracemap-ts/impact-report.md
```

JVM scanner:

```bash
JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm installDist
src/jvm/build/install/tracemap-jvm/bin/tracemap-jvm scan --repo samples/jvm-modern-sample --out .tracemap-jvm
dotnet run --project src/dotnet/TraceMap.Cli -- reduce --index .tracemap-jvm/index.sqlite --contract-delta samples/contract-deltas/jvm-modern.order-status.json --out .tracemap-jvm/impact-report.md
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index .tracemap-jvm/index.sqlite --label jvm-sample --out .tracemap-jvm-combined.sqlite
```

The JVM scanner does not run Maven, Gradle, annotation processors, app code, or dependency restore during scan. Kotlin MVP support is syntax fallback only, so Kotlin-only scans are labeled syntax coverage.

Python scanner:

```bash
python3 -m venv /tmp/tracemap-python-venv
/tmp/tracemap-python-venv/bin/python -m pip install -e "src/python[dev]"
/tmp/tracemap-python-venv/bin/python -m tracemap_py.cli scan --repo samples/python-fastapi-sample --out .tracemap-py
dotnet run --project src/dotnet/TraceMap.Cli -- reduce --index .tracemap-py/index.sqlite --contract-delta samples/contract-deltas/python-fastapi.order-status.json --out .tracemap-py/impact-report.md
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index .tracemap-py/index.sqlite --label python-sample --out .tracemap-py-combined.sqlite
```

The Python scanner does not import user code, execute setup.py, run a type checker, or install project dependencies during scan. MVP coverage is reduced AST/package/config/SQL evidence, so no-match reducer outcomes are coverage-relative.

Endpoint alignment compares two existing indexes instead of scanning multiple apps in one command. This keeps language scanners independent and preserves per-index coverage/provenance:

```bash
node src/typescript/dist/src/cli.js scan --repo /path/to/App.Api/ClientApp --out /tmp/app-client
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo /path/to/App --project App.Api/App.Api.csproj --out /tmp/app-server
dotnet run --project src/dotnet/TraceMap.Cli -- endpoints --client-index /tmp/app-client/index.sqlite --server-index /tmp/app-server/index.sqlite --client-label app-client --server-label app-api --out /tmp/app-endpoints
```

Endpoint findings are static, coverage-relative evidence. A client-only row is not proof of a broken call, and a server-only row is not proof of dead code.

## License

This project is licensed under the Apache License 2.0. See [LICENSE](./LICENSE).



Copyright © 2026 Joe Feser. All rights reserved.
