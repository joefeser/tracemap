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
| integration facts | proves HTTP/API, config, SQL/DB, serializer, and package/dependency facts where supported |
| combine/export smoke | proves shared schema compatibility across adapters |
| public OSS smoke | proves larger real-world repos complete without unchecked assumptions |
| private-path guard | proves generated docs/scripts do not leak developer-local paths |

## Required Local Commands

Run these before opening or updating a PR that changes scanner behavior:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
npm run check --prefix src/typescript
JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm test
./scripts/check-private-paths.sh
```

For JVM CLI smoke, also run:

```bash
JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm installDist
```

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

## Next Language Candidate

Python is the next recommended app-language adapter. Useful early fixtures should cover:

- FastAPI routes and Pydantic DTOs
- Flask routes where syntax can prove them
- SQLAlchemy declared columns and direct SQL literals
- direct SQL literals
- environment/config reads
- requests/httpx client calls

Python should follow the same matrix: modern sample, broken sample, reducer fixture, relationship tables, integration facts, public OSS smoke, and private-path guard.

Python MVP no-match reducer outcomes are expected to be `NoEvidenceReducedCoverage` because MVP scans use reduced AST/package/config coverage, not full type-checker semantic coverage.
