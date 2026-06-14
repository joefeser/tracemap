# TraceMap Validation Guide

This guide defines the repeatable checks used to validate language adapters and cross-index analysis. It complements `docs/ACCEPTANCE.md`: acceptance defines expected behavior, while this file describes the concrete sample and open-source smoke set.

TraceMap validation must stay deterministic and evidence-backed. Do not add LLM calls, embeddings, or prompt-based classification to validation.

## Required Matrix

Every language adapter should have:

| Check | Purpose |
| --- | --- |
| local modern sample | proves full semantic path when compiler/project loading works |
| local broken sample | proves syntax fallback and reduced coverage labels |
| reducer fixture | proves contract delta matching through shared facts/index schema |
| SQLite relationship queries | proves `call_edges`, `object_creations`, `argument_flows`, symbols, and relationship tables are populated when facts exist |
| value-origin flow queries | proves direct parameter forwarding, bounded local aliases, and unique constructor field origins are represented without crossing ambiguous boundaries |
| integration facts | proves HTTP/API, config, SQL/DB, serializer, and package/dependency facts where supported |
| combine/report/paths/reverse/export smoke | proves shared schema compatibility, combined dependency reporting, static dependency path queries, and reverse dependency-surface queries across adapters |
| public OSS smoke | proves larger real-world repos complete without unchecked assumptions |
| private-path guard | proves generated docs/scripts do not leak developer-local paths |

## Required Local Commands

Run these before opening or updating a PR that changes scanner behavior:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
npm run check --prefix src/typescript
JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm test
python3 -m venv /tmp/tracemap-python-venv
/tmp/tracemap-python-venv/bin/python -m pip install -e "src/python[dev]"
/tmp/tracemap-python-venv/bin/python -m pytest src/python/tests
PYTHON_BIN=/tmp/tracemap-python-venv/bin/python ./scripts/smoke-python-endpoints.sh
./scripts/check-private-paths.sh
```

## Public Demo Workflow

Run the public demo when validating the open-source walkthrough or generated public artifacts:

```bash
./scripts/demo-public.sh
./scripts/demo-public.sh .tracemap-demo
```

The default demo uses only checked-in samples. It does not clone public repositories, read private repositories, call external analysis services, query package registries, or run vulnerability/license/compatibility analysis. First-run build restore may still need network access for local toolchains such as NuGet or npm.

Current first-slice behavior:

- checks `git`, `.NET`, `node`, and `npm`
- builds the .NET solution and TypeScript adapter
- scans `samples/modern-sample`
- scans `samples/endpoint-server-aspnet`
- scans `samples/typescript-modern-sample`
- scans `samples/endpoint-client-angular`
- writes `demo-summary.md` and `demo-summary.json`
- runs a generated-output sentinel scan over public-shareable summaries and reports
- marks Python as `not_requested` unless `--include-python` is passed; requested Python scanning is currently `deferred` to a follow-up slice
- marks JVM as `unavailable` when Java 21 is absent
- marks combine/report, paths/reverse, portfolio, diff, impact, and release-review as `deferred` until follow-up demo slices add their assertions

Troubleshooting:

- If the demo refuses an in-repo output directory, use `.tracemap-demo/` or add a generic ignored output path before running the script.
- If .NET or TypeScript build restore fails, run the build/test commands above directly to restore local toolchain dependencies and inspect their native diagnostics.
- Reduced sample scan coverage is expected in the first public-demo slice for samples that intentionally rely on syntax fallback or missing framework packages. The summary reports full and reduced scan counts so follow-up report assertions can stay coverage-aware.
- If the generated public-report sentinel fails, inspect the relative file paths and category it prints. Keep scan manifests, SQLite files, facts, and logs local-only; public summaries and reports must use hashes, labels, or relative paths.

Generated outputs under `scans/**`, SQLite files, facts, manifests, and logs are local-only artifacts and may contain temporary execution details. Public-shareable `demo-summary.*` and future `reports/**/*.md|json` artifacts must not contain raw scripts, SQL, snippets, config values, connection strings, raw URLs with credentials, private paths, or local absolute paths.

Use `.tracemap-demo/` for an in-repo output root; it is ignored by git. Other in-repo output directories are rejected unless `git check-ignore` proves they are ignored.

For JVM CLI smoke, also run:

```bash
JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm installDist
```

For query-pattern report rendering changes, inspect generated scan reports from the affected adapters:

```bash
rg -n "Query Patterns|SQL shape|Query builder|static shape evidence|runtime execution" <scan-output>/report.md
rg -n "fields none" <scan-output>/report.md
```

`fields none` is acceptable for query-builder facts with no extracted field metadata. SQL-shape facts should render derived operation/table/column/source/hash metadata instead, and reports must not render raw SQL text, literal values, unsafe identifiers, or developer-local absolute paths.

For combined dependency report, path-query, reverse-query, diff, contract-diff, or snapshot-diff changes, run a combine/report/paths/reverse/diff/contract-diff/snapshot-diff smoke over any two existing local scan outputs:
For combined change-impact changes, include the `impact` command in the same smoke.
For release-review changes, include `release-review` in the same smoke and verify `release-review.md` plus `release-review.json` are produced.

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- combine \
  --index <first>/index.sqlite --label first \
  --index <second>/index.sqlite --label second \
  --out <tmp>/combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index <tmp>/combined.sqlite --out <tmp>/combined-report
dotnet run --project src/dotnet/TraceMap.Cli -- paths --index <tmp>/combined.sqlite --out <tmp>/combined-paths
dotnet run --project src/dotnet/TraceMap.Cli -- reverse --index <tmp>/combined.sqlite --surface sql-query --to endpoints --out <tmp>/combined-reverse
dotnet run --project src/dotnet/TraceMap.Cli -- diff --before <tmp>/combined.sqlite --after <tmp>/combined.sqlite --out <tmp>/combined-diff
dotnet run --project src/dotnet/TraceMap.Cli -- contract-diff --before <tmp>/combined.sqlite --after <tmp>/combined.sqlite --out <tmp>/contract-diff
dotnet run --project src/dotnet/TraceMap.Cli -- snapshot-diff --before <tmp>/combined.sqlite --after <tmp>/combined.sqlite --out <tmp>/snapshot-diff
dotnet run --project src/dotnet/TraceMap.Cli -- impact --before <tmp>/combined.sqlite --after <tmp>/combined.sqlite --out <tmp>/combined-impact
dotnet run --project src/dotnet/TraceMap.Cli -- release-review --before <tmp>/combined.sqlite --after <tmp>/combined.sqlite --out <tmp>/release-review
test -f <tmp>/combined-report/dependency-report.md
test -f <tmp>/combined-report/dependency-report.json
test -f <tmp>/combined-paths/paths-report.md
test -f <tmp>/combined-paths/paths-report.json
test -f <tmp>/combined-reverse/reverse-report.md
test -f <tmp>/combined-reverse/reverse-report.json
test -f <tmp>/combined-diff/diff-report.md
test -f <tmp>/combined-diff/diff-report.json
test -f <tmp>/contract-diff/contract-diff-report.md
test -f <tmp>/contract-diff/contract-diff-report.json
test -f <tmp>/snapshot-diff/snapshot-diff-report.md
test -f <tmp>/snapshot-diff/snapshot-diff-report.json
test -f <tmp>/combined-impact/impact-report.md
test -f <tmp>/combined-impact/impact-report.json
test -f <tmp>/release-review/release-review.md
test -f <tmp>/release-review/release-review.json
```

For value-origin flow changes, also inspect the source `parameter_forward_edges` table from a semantic .NET sample or focused fixture:

```bash
sqlite3 <out>/index.sqlite "select source_method_symbol, source_parameter_symbol, target_method_symbol, target_parameter_name, rule_id from parameter_forward_edges order by source_method_symbol, target_method_symbol;"
```

Expected behavior: direct parameter forwarding is present, same-method aliases are bounded to 3 hops, and ambiguous constructor/member origins are omitted or represented as gaps by future reporting slices rather than being promoted to forwarding edges.

For changes to `combine`, `report`, `paths`, `reverse`, endpoint extraction, call edges, SQL/query extraction, or dependency-surface projection, run the public combined-path smoke:

```bash
./scripts/smoke-combined-paths.sh
```

The smoke is sample-only and does not clone repositories or read external application paths. It scans `samples/endpoint-client-angular` and `samples/endpoint-server-aspnet`, combines them as `sample-client` and `sample-server`, runs `report`, runs default and targeted `paths` queries, runs a reverse SQL-surface query, and verifies:

- required scan, combined, report, and paths artifacts exist
- the combined report has exactly `sample-client` and `sample-server`
- the sample endpoint `/api/admin/runner/get-by-id/{}` has endpoint alignment evidence; duplicate syntax/semantic server route facts may classify this as review-tier `AmbiguousMatch`
- a targeted path reaches a `sql-query` terminal from the client through an endpoint match, server call edge, source-local symbol reconciliation edge, and surface evidence edge
- `DatabaseColumnMapping` facts, when present, are selectable as `sql-persistence` terminal surfaces rather than `sql-query` terminal surfaces
- path edges and gaps carry rule IDs and evidence tiers
- a reverse SQL-surface query finds endpoint roots and path evidence with rule IDs and evidence tiers
- a bogus endpoint selector returns a valid zero-path report with a rule-backed gap
- repeated targeted `paths` JSON output is byte-stable
- generated Markdown does not render the synthetic SQL sentinel or developer-local absolute paths

The smoke writes generated manifests, logs, SQLite files, and reports under a caller-provided directory or `mktemp -d`. Generated manifests/logs may contain absolute paths to the checked-in samples or temporary output roots; they must not be committed.

## Public OSS Smoke

Use `scripts/smoke-open-source-repos.sh` to clone pinned public repositories into a cache directory and scan them into a separate output directory:

```bash
scripts/smoke-open-source-repos.sh /tmp/tracemap-oss-cache /tmp/tracemap-oss-smoke
```

The script uses exact commit SHAs so results are comparable over time.

| Label | Language | URL | Commit SHA | Expected coverage |
| --- | --- | --- | --- | --- |
| `ProjectExtensions.Azure.ServiceBus` | C# | `https://github.com/ProjectExtensions/ProjectExtensions.Azure.ServiceBus.git` | `2a8e72c8f5680edf2096b05ac08c39d47a95cef8` | usually `Level1SemanticAnalysisReduced` |
| `fluentjdf` | C# | `https://github.com/joefeser/fluentjdf.git` | `9490e699a89bb21f4aabf198173fc6382f84a53f` | usually `Level1SemanticAnalysisReduced` |
| `scip-typescript` | TypeScript | `https://github.com/sourcegraph/scip-typescript.git` | `891eb4293709a6a587bf4468dfa1b45a85182fd9` | usually `Level1SemanticAnalysisReduced` |
| `scip-java` | JVM | `https://github.com/sourcegraph/scip-java.git` | `825463cb15d540d45c680593aad1f634330435cf` | usually `Level1SemanticAnalysisReduced` |
| `spring-petclinic` | JVM | `https://github.com/spring-projects/spring-petclinic.git` | `a2c2ef994340d3970eb6db51247456a51bb161f8` | usually `Level1SemanticAnalysisReduced` |
| `okio` | JVM/Kotlin | `https://github.com/square/okio.git` | `cad7ff1057307142149b1a28dfcb49117e89b0d3` | usually reduced or syntax fallback for Kotlin-heavy areas |
| `full-stack-fastapi-template` | Python | `https://github.com/fastapi/full-stack-fastapi-template.git` | `1c1175eb5045e6e8fca3bcbc4134630f3ae640ba` | `Level1SemanticAnalysisReduced` |
| `microblog` | Python | `https://github.com/miguelgrinberg/microblog.git` | `a975ef64864354867c88e0ed3a17ba7d17dca752` | `Level1SemanticAnalysisReduced` |
| `sqlalchemy` | Python | `https://github.com/sqlalchemy/sqlalchemy.git` | `bfe559a7e4d69e5699c390ac9cafd2a5a2d38078` | `Level1SemanticAnalysisReduced` |

Reduced coverage is acceptable for OSS smoke when project/dependency/classpath gaps are recorded as `AnalysisGap` facts. A successful smoke means the scan completes, artifacts exist, the manifest is honest about coverage, and important relationship tables can be queried.

## JVM Smoke Expectations

The JVM modern sample is the minimum high-signal fixture. It should produce:

- `Level1SemanticAnalysis`
- `buildStatus = "Succeeded"`
- exactly one Java route binding: `GET /api/orders/{id}` mapped to `com.example.orders.OrderController.getOrder`
- semantic call edges from `OrderController.getOrder` to `OrderResponse.setStatus` and `OrderService.calculateTotal`
- object creation rows for `OrderService`, `OrderResponse`, and `OrderRepository`
- argument-flow rows from controller/service calls into callee parameters
- SQL facts for the JDBC `prepareStatement` literal and `schema.sql`
- config key facts for `application.properties`
- a reducer `DefiniteImpact` finding for `OrderResponse.status`

Example query set:

```bash
sqlite3 <out>/index.sqlite "select fact_type, count(*) from facts group by fact_type order by fact_type;"
sqlite3 <out>/index.sqlite "select count(*) from call_edges;"
sqlite3 <out>/index.sqlite "select count(*) from object_creations;"
sqlite3 <out>/index.sqlite "select count(*) from argument_flows;"
sqlite3 <out>/index.sqlite "select target_symbol, properties_json from facts where fact_type='HttpRouteBinding';"
```

## What SQL Means Here

SQL should be treated as a first-class cross-language data dependency surface, not as the next application language adapter. The SQL layer should eventually parse:

- query text from application code
- `.sql` files and migration files
- tables, columns, joins, projections, predicates, and write operations
- stored procedures, views, and function calls where dialect support exists
- query-to-schema relationships across app indexes and database artifacts

SQL validation should therefore plug into every app-language adapter, because C#, TypeScript, JVM, and Python can all emit SQL evidence.

## Python Adapter

Python validation fixtures should cover:

- FastAPI routes and Pydantic DTOs
- Flask routes where syntax can prove them
- SQLAlchemy declared columns and direct SQL literals
- direct SQL literals
- environment/config reads
- requests/httpx client calls

Python follows the same matrix: modern sample, broken sample, reducer fixture, relationship tables, integration facts, public OSS smoke, and private-path guard.

Python MVP no-match reducer outcomes are expected to be `NoEvidenceReducedCoverage` because MVP scans use reduced AST/package/config coverage, not full type-checker semantic coverage.

## Python Smoke Expectations

The Python FastAPI sample is the minimum high-signal fixture. It should produce:

- `Level1SemanticAnalysisReduced`
- `buildStatus = "FailedOrPartial"`
- route facts for FastAPI/Flask decorators when static decorator syntax is visible
- serializer contract member facts for Pydantic and dataclass-like DTO fields
- SQLAlchemy column mapping facts for declarative mapped columns
- SQL file and direct SQL literal facts with hashed SQL text
- query-pattern facts with operation, table, column, text hash, and query shape hash metadata when simple static SQL is visible
- config key facts for config module assignments and static `os.getenv` or `os.environ[...]` reads
- HTTP client facts for `requests` and `httpx` static URL calls
- endpoint alignment smoke from `samples/python-client-sample` to `samples/python-fastapi-sample` produces at least one `MatchedEndpoint`
- shared SQLite rows for `call_edges`, `object_creations`, `argument_flows`, `symbol_relationships`, and `symbols`
- a reducer `ProbableImpact` or stronger structural finding for `OrderResponse.status`

Example query set:

```bash
sqlite3 <out>/index.sqlite "select fact_type, count(*) from facts group by fact_type order by fact_type;"
sqlite3 <out>/index.sqlite "select count(*) from call_edges;"
sqlite3 <out>/index.sqlite "select count(*) from object_creations;"
sqlite3 <out>/index.sqlite "select count(*) from argument_flows;"
sqlite3 <out>/index.sqlite "select target_symbol, properties_json from facts where fact_type='HttpRouteBinding';"
sqlite3 <out>/index.sqlite "select target_symbol, properties_json from facts where fact_type='DatabaseColumnMapping';"
sqlite3 <out>/index.sqlite "select target_symbol, properties_json from facts where fact_type='QueryPatternDetected';"
sqlite3 <out>/index.sqlite "select rule_id, json_extract(properties_json, '$.sqlSourceKind'), json_extract(properties_json, '$.queryShapeHash') from facts where fact_type='QueryPatternDetected' and json_extract(properties_json, '$.sqlSourceKind') is not null order by rule_id, fact_id;"
sqlite3 <combined>/combined.sqlite "select sources.label, facts.fact_type, json_extract(facts.properties_json, '$.sqlSourceKind'), json_extract(facts.properties_json, '$.queryShapeHash'), json_extract(facts.properties_json, '$.textHash') from combined_facts facts join combined_sources sources on sources.source_index_id = facts.source_index_id where facts.fact_type in ('SqlTextUsed','QueryPatternDetected','DatabaseColumnMapping','DapperCallDetected','SqlCommandDetected') order by sources.label, facts.combined_fact_id;"
grep "orm-text" <out>/report.md
grep "orders" <out>/report.md
```

For SQL dependency-surface changes, also inspect hash-only and weak-identity behavior:

```bash
sqlite3 <combined>/combined.sqlite "select sources.label, facts.fact_type, facts.properties_json from combined_facts facts join combined_sources sources on sources.source_index_id = facts.source_index_id where facts.fact_type in ('SqlTextUsed','QueryPatternDetected') order by sources.label, facts.combined_fact_id;"
dotnet run --project src/dotnet/TraceMap.Cli -- diff --before <before-combined.sqlite> --after <after-combined.sqlite> --out <tmp>/sql-diff --scope surfaces --surface sql-query --format json
grep -E "HashOnlyEvidence|VolatileIdentity" <tmp>/sql-diff/diff-report.json
```

When checking mapping-only persistence evidence, use `--to-surface sql-persistence`, `--surface sql-persistence`, or `--scope surfaces --surface sql-persistence`; these surfaces do not claim that a SQL query executes.
