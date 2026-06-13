# Python Endpoint and SQL Details Design

## Overview

This is an additive scanner slice. It does not change the shared SQLite schema, endpoint matcher, or reducer classifications. Instead, the Python adapter emits richer existing fact types so downstream tools can use the same database contract across C#, TypeScript, JVM, and Python.

## Endpoint alignment

Python already emits:

- `HttpRouteBinding` from FastAPI and Flask decorator syntax.
- `HttpCallDetected` from `requests` and `httpx` call syntax.

The endpoint matcher reads these facts from `index.sqlite` and normalizes by shared `normalizedPathKey`. A public sample client is added so the smoke path can scan one Python client index and one Python server index, then run:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- endpoints \
  --client-index <client>/index.sqlite \
  --server-index <server>/index.sqlite \
  --out <report>
```

The fixture intentionally uses a static route-template-shaped URL so matching is deterministic and does not claim runtime URL construction.

## SQL query pattern detection

The SQL helper remains lightweight and deterministic. It strips comments/whitespace for hashing and extracts:

- operation name
- primary table and table list
- visible column list for simple `SELECT`, `INSERT`, `UPDATE`, and `CREATE TABLE`
- source kind, text hash, and query shape hash

It is not a dialect parser. Unsupported or complex SQL still receives `SqlTextUsed`, and dynamic SQL remains an `AnalysisGap`.

## Evidence tiers

- `.sql` files: `Tier2Structural`
- SQLAlchemy `text(...)` with SQLAlchemy import evidence: `Tier2Structural`
- DB-API `.execute(<literal>)` and bare SQL-like literals: `Tier3SyntaxOrTextual`
- dynamic SQL expressions: `Tier4Unknown` `AnalysisGap`

## Limitations

- No raw SQL text or literal values are stored.
- Query shape extraction does not prove runtime execution, schema existence, dialect validity, generated SQL, or branch feasibility.
- Table and column extraction is best-effort for simple static SQL only.
- Endpoint alignment remains static and coverage-relative.
