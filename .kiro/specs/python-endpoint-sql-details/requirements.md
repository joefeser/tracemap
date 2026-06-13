# Python Endpoint and SQL Details Requirements

## Purpose

TraceMap's Python adapter already emits endpoint, SQL text, and relationship facts. This slice makes those facts more useful for cross-index analysis by proving that Python HTTP client calls can align with Python server route bindings, and by emitting deterministic SQL query-pattern metadata without storing raw SQL snippets.

## Requirements

### 1. Python endpoint alignment compatibility

1. WHEN Python scans emit `HttpCallDetected` and `HttpRouteBinding` facts THEN they SHALL populate shared endpoint properties consumed by `tracemap endpoints`: `httpMethod`, `normalizedPathTemplate`, `normalizedPathKey`, `methodName`, and `targetSymbol` where statically visible.
2. WHEN a Python client URL cannot be statically normalized THEN the scanner SHALL emit `HttpCallDetected` with `urlKind = dynamic` and a closed, deterministic `dynamicReason`.
3. WHEN public in-repo Python client and server samples are scanned THEN `tracemap endpoints` SHALL produce at least one `MatchedEndpoint` finding from those indexes.

### 2. SQL query pattern facts

1. WHEN direct SQL text is found in Python DB-API, SQLAlchemy `text(...)`, migration `execute(...)`, or `.sql` files THEN the scanner SHALL continue emitting `SqlTextUsed` with `textHash` and `textLength`.
2. WHEN direct SQL text has a statically visible operation/table/column shape THEN the scanner SHALL also emit `QueryPatternDetected`.
3. `QueryPatternDetected` facts SHALL store only derived metadata such as `operationName`, `tableName`, `tableNames`, `columnNames`, `textHash`, `queryShapeHash`, and `sqlSourceKind`.
4. The scanner SHALL NOT store raw SQL text or literal values in query-pattern facts.
5. Tier2 `QueryPatternDetected` facts MAY be used as structural reducer/report evidence; Tier3 query patterns remain review-routing evidence only.

### 3. Documentation and validation

1. The rule catalog SHALL document `python.integration.sql.v1` as emitting `QueryPatternDetected`.
2. Validation docs SHALL include the public Python endpoint smoke path.
3. Tests SHALL cover deterministic SQL shape extraction and emitted Python query-pattern facts.
