# TraceMap

TraceMap is a deterministic, evidence-backed repository indexer and contract-change reducer. It scans source repositories into rule-backed facts, writes queryable artifacts, and reduces contract deltas into Markdown impact reports without using LLMs, embeddings, or prompt-based classification in the core scanner/reducer.

The current language scanners are:

- `.NET/C#` under `src/dotnet`, including semantic Roslyn extraction, syntax fallback, flow/export commands, and contract reduction.
- `TypeScript` under `src/typescript`, including compiler-backed facts, syntax fallback, integration facts, and reducer-compatible SQLite output.
- `JVM/Java/Kotlin` under `src/jvm`, including Java compiler-backed facts, Java/Kotlin syntax fallback, Maven/Gradle metadata, integration facts, and reducer-compatible SQLite output.
- `Python` under `src/python`, including AST/package/config/SQL extraction, FastAPI/Flask/Pydantic/SQLAlchemy/httpx/requests integration facts, reduced coverage labeling, and reducer-compatible SQLite output.

TraceMap can also combine multiple indexes into one provenance-preserving SQLite database, generate a combined dependency report, query static dependency paths through the combined graph, produce route-centered static flow reports, diff combined snapshots, compare API/DTO contract evidence, compare single or combined snapshots by source/coverage/extractor metadata, and align client/server endpoint evidence across two existing indexes, such as an Angular client index and an ASP.NET API index.

Start here:

- [Product requirements](docs/PRD.md)
- [Acceptance plan](docs/ACCEPTANCE.md)
- [Validation guide](docs/VALIDATION.md)
- [Decision log](docs/DECISIONS.md)
- [Build milestones](MILESTONES.md)
- [Language adapter contract](docs/LANGUAGE_ADAPTER_CONTRACT.md)
- [PR review loop](docs/PR_REVIEW_LOOP.md)
- [Static HTML evidence explorer](docs/STATIC_HTML_EVIDENCE_EXPLORER.md)
- [Rule catalog](rules/rule-catalog.yml)

TraceMap is not an AI impact-analysis tool. The scanner and reducer do not use LLM calls, embeddings, vector databases, or prompt-based classification.

## Quick Start

Run the public demo from a clean checkout:

```bash
./scripts/demo-public.sh
./scripts/demo-public.sh .tracemap-demo
```

The demo scans checked-in .NET and TypeScript samples, including a synthetic before/after fixture pair, combines endpoint, mixed-stack, and before/after indexes with deterministic labels, runs dependency, path, reverse, diff, impact, portfolio, and release-review reports, then writes `demo-summary.md` plus `demo-summary.json`. Generated scan artifacts are local-only; public-shareable summaries and reports use relative paths or hashes and run a generated-output sentinel scan.

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

`tracemap reduce` also accepts structured contract delta v2 input for type, property, method, endpoint, package, schema, SQL, and dependency-surface references, and directory outputs write both Markdown and deterministic JSON:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- reduce --index .tracemap/index.sqlite --contract-delta samples/contract-deltas/contract-delta-v2.example.json --out .tracemap/impact --format json
```

Useful .NET index commands:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- flow --index .tracemap/index.sqlite --symbol request --out .tracemap/flow-report.md
dotnet run --project src/dotnet/TraceMap.Cli -- export --index .tracemap/index.sqlite --out .tracemap/index-export.json --format json
dotnet run --project src/dotnet/TraceMap.Cli -- export --index .tracemap/index.sqlite --out .tracemap/relationships.mmd --format mermaid
dotnet run --project src/dotnet/TraceMap.Cli -- explorer generate --input .tracemap --out .tracemap-explorer
```

`tracemap explorer generate` writes a local static HTML evidence explorer from
existing generated TraceMap artifacts. It is separate from the public
`tracemap.tools` site, uses bundled local assets, and does not rescan source
code or derive new impact conclusions.

Combine multiple indexes for cross-repo or cross-language dependency queries:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- combine \
  --index .tracemap/index.sqlite --label dotnet-sample \
  --index .tracemap-ts/index.sqlite --label typescript-sample \
  --out .tracemap-combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index .tracemap-combined.sqlite --out .tracemap-combined-report
dotnet run --project src/dotnet/TraceMap.Cli -- paths --index .tracemap-combined.sqlite --from-endpoint "GET /api/admin/runner/get-by-id/{}" --to-surface sql-query --out .tracemap-combined-paths
dotnet run --project src/dotnet/TraceMap.Cli -- route-flow --index .tracemap-combined.sqlite --route "GET /api/admin/runner/get-by-id/{}" --to-surface sql-query --out .tracemap-route-flow
dotnet run --project src/dotnet/TraceMap.Cli -- reverse --index .tracemap-combined.sqlite --surface sql-query --surface-name ClubMemberships --to endpoints --out .tracemap-combined-reverse
dotnet run --project src/dotnet/TraceMap.Cli -- export --index .tracemap-combined.sqlite --out .tracemap-combined.json --format json
```

`tracemap route-flow` writes `route-flow-report.md` and `route-flow-report.json` for directory outputs. It is a route-centered static evidence view over a combined index: it preserves rule IDs, evidence tiers, source labels, commit SHAs, file spans, supporting fact/edge IDs, coverage labels, gaps, and limitations. It does not prove runtime execution, dependency-injection target selection, SQL execution, traffic, auth behavior, deployment, or production use.

The combined dependency report writes `dependency-report.md` and `dependency-report.json` when `--out` is a directory. It summarizes source coverage, endpoint alignment, HTTP/SQL/package/config surfaces, dependency edges, needs-review rows, known gaps, and static-analysis limitations without mutating the combined database.

Summarize a portfolio of single-language and/or combined indexes:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- portfolio \
  --index .tracemap-ts/index.sqlite --label web-client \
  --index .tracemap/index.sqlite --label orders-api \
  --out .tracemap-portfolio
dotnet run --project src/dotnet/TraceMap.Cli -- portfolio \
  --manifest samples/portfolio.example.json \
  --out .tracemap-portfolio
dotnet run --project src/dotnet/TraceMap.Cli -- portfolio \
  --before-manifest .tracemap-before/portfolio.json \
  --after-manifest .tracemap-after/portfolio.json \
  --out .tracemap-portfolio-comparison
```

The portfolio command writes `portfolio-report.md` and `portfolio-report.json` when `--out` is a directory. It expands combined indexes into source records, reads single-language indexes directly, and reports source coverage, endpoint alignment, dependency surfaces, dependency edges, shared static surfaces, gaps, and limitations across many repositories. Before/after portfolio comparison reports source changes plus projected safe surface and edge changes from manifest-paired snapshots. Portfolio reports are static evidence inventories; they do not infer runtime topology, ownership, deployment, production traffic, package compatibility, vulnerabilities, or release approval.

Summarize static package-upgrade evidence from a single-language or combined index:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- package-impact \
  --index .tracemap-combined.sqlite \
  --package-delta samples/package-deltas/package-delta.example.json \
  --out .tracemap-package-impact
```

The package impact command writes `package-impact-report.md` and `package-impact-report.json` when `--out` is a directory. It matches `package-delta.v1` changes against indexed `PackageReferenced`/`package-config` evidence and reports source labels, commit SHAs, rule IDs, evidence tiers, file spans, safe version metadata, gaps, and limitations. Package impact reports are static evidence inventories; they do not infer compatibility, transitive dependency resolution, runtime loading, vulnerabilities, licenses, deployment, or release approval.

The combined dependency paths command writes `paths-report.md` and `paths-report.json` when `--out` is a directory. It follows static evidence from endpoint, symbol, or source selectors to terminal dependency surfaces such as `sql-query`, `http-client`, `http-route`, and `package-config`. Paths are evidence trails, not runtime traces.

The combined reverse query command writes `reverse-report.md` and `reverse-report.json` when `--out` is a directory. It starts from dependency surfaces and walks static evidence backward to endpoints, symbols, sources, or all supported roots. Reverse paths answer "what static roots can reach this dependency evidence?" and remain coverage-relative rather than runtime usage proof.

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

Compare two single-language or combined snapshots by source, coverage, and extractor metadata:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- snapshot-diff \
  --before .tracemap-before/index.sqlite \
  --after .tracemap-after/index.sqlite \
  --out .tracemap-snapshot-diff
```

The snapshot diff command writes `snapshot-diff-report.md` and `snapshot-diff-report.json` when `--out` is a directory. It validates same-kind inputs, source identity, commit SHA availability, coverage, and extractor-version changes. Combined indexes delegate endpoint, surface, graph, and opt-in path comparison to the combined diff engine; single-language indexes compare endpoint, dependency-surface, and `AnalysisGap` fact changes. Single-index graph and contract-shape comparison remain explicit availability gaps until deeper evidence readers are implemented.

Compare API/DTO static contract evidence between two single-language or combined snapshots:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- contract-diff \
  --before .tracemap-before/index.sqlite \
  --after .tracemap-after/index.sqlite \
  --out .tracemap-contract-diff
dotnet run --project src/dotnet/TraceMap.Cli -- contract-diff \
  --before .tracemap-before/index.sqlite \
  --after .tracemap-after/index.sqlite \
  --scope endpoints,dto-properties \
  --endpoint "GET /api/orders/{}" \
  --exit-code \
  --out .tracemap-contract-diff
```

The contract diff command writes `contract-diff-report.md` and `contract-diff-report.json` when `--out` is a directory. It compares indexed endpoint routes, route shapes, DTO/type declarations, DTO property/member declarations, method signatures, and explicit request/response attachment facts when available. Missing attachment evidence is reported as a gap rather than inferred as no change. The report is static evidence only; it is not OpenAPI completeness, runtime serializer mapping proof, binary compatibility, traffic analysis, deployment reachability, or auth behavior proof.

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

The combined change impact command writes `impact-report.md` and `impact-report.json` when `--out` is a directory. It reuses combined diff evidence to classify changed sources, coverage, endpoints, dependency surfaces, and dependency edges as static impact evidence, probable static impact, needs review, or analysis gaps. This report is not runtime impact analysis; path context is off by default and `--include-paths` adds bounded before/after static path context for changed endpoints, surfaces, and edges when safe selectors can be derived.

Assemble a release-oriented evidence packet from two snapshots:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- release-review \
  --before .tracemap-before-combined.sqlite \
  --after .tracemap-after-combined.sqlite \
  --contract-delta samples/contract-deltas/contract-delta-v2.example.json \
  --out .tracemap-release-review
```

The release review command writes `release-review.md` and `release-review.json` when `--out` is a directory. It composes available TraceMap evidence from source coverage, combined change impact, contract delta impact, optional path/reverse context, section gaps, and a deterministic reviewer checklist. It is a static evidence packet, not release approval, CI policy, runtime risk prediction, deployment verification, or production usage proof. API/DTO, SQL/schema, and package-upgrade sections are rendered from available deterministic workflows or as explicit unavailable/deferred sections when compatible inputs are not supplied.

Add `--include-priority` to include deterministic review priority scoring in the release-review Markdown and JSON. The opt-in JSON adds `reviewPriority` and sidecar `reviewPriorityRows` fields with closed-vocabulary `attentionLevel` and `severityHint` values. V1 is ordinal-only, so `priorityScore` is always `null`; it uses no numeric weights and does not change the underlying release-review classifications or exit-code behavior.

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
