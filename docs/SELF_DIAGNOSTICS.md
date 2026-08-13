# TraceMap Self-Diagnostics

TraceMap exposes a bounded BCL diagnostics seam for observing the TraceMap process itself. These signals describe scanner, reducer, persistence, and report work. They are not repository evidence, are never written as `CodeFact` rows, and do not participate in reducers, path queries, coverage decisions, or impact claims.

## Providers and instruments

- `ActivitySource`: `TraceMap`, versioned with the TraceMap tool version.
- `Meter`: `TraceMap`, versioned with the same tool version.
- Histogram: `tracemap.operation.duration`, monotonic elapsed milliseconds.
- Counter: `tracemap.operation.count`, completed operations.
- Histogram: `tracemap.operation.items`, aggregate item counts only.

The v0 activities cover command roots, scan discovery, aggregate semantic analysis, aggregate static extraction, scan artifact writes, reduction, and combined-report composition. They deliberately avoid per-file, per-symbol, per-query, and per-fact spans.

Only these low-cardinality tags are allowed:

- `tracemap.command`
- `tracemap.phase`
- `tracemap.outcome`
- `tracemap.analysis_level`
- `tracemap.build_status`
- `tracemap.tool_version`

Unknown command, phase, analysis-level, or build-status values become `other` or `unknown`; they are not copied into telemetry. Repository names, remotes, branches, commit and scan IDs, paths, project names, symbols, contract elements, endpoint and SQL identities, arguments, environment values, exception messages, and source-derived values are never tags.

## Opt-in and cost

There is no listener, exporter, upload, sidecar, or background collector by default. When neither `ActivitySource` nor `Meter` has an enabled listener, the start path returns a shared no-op operation before allocating an activity, tag collection, or stopwatch state. The regression measurement performs 10,000 disabled operations and requires zero bytes allocated on the calling thread. Timing cost is intentionally not used as a CI performance gate because host scheduling and runtime warm-up make a small wall-clock threshold misleading.

When a listener is enabled, operations allocate their bounded tag set and record monotonic duration and aggregate counts. Diagnostics failures are isolated from evidence semantics. A failed, cancelled, or reduced-coverage operation closes with the categorical outcomes `failed`, `cancelled`, or `partial`; no exception message is recorded.

## Completeness and EventPipe boundary

Diagnostic observation is always potentially partial. Late listener attachment, filtering, sampling, buffer loss, cancellation, and process termination can omit spans or measurements. An absent activity, missing metric, or zero count does not prove that work did not execute.

The v0 seam does not start `dotnet-trace`, parse `.nettrace` files, profile application code, or ship an OpenTelemetry SDK/exporter. Maintainers may attach compatible local .NET diagnostics tooling explicitly, but captured artifacts remain local diagnostic material and are not accepted as TraceMap static evidence. Automatic capture, upload, runtime-topology claims, and application telemetry ingestion require separate security and evidence contracts.

No timing fields are added to `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, normal reports, or deterministic IDs.

## Commit-bound scan execution receipt

`tracemap scan` now writes `scan-receipt.json` when Git establishes an exact commit SHA. The versioned `scan-execution-receipt.v1` contract is a bounded operational sidecar, not a `CodeFact`. It records closed stage and outcome codes, monotonic duration, hashed repository and authorized-scope identity, the scan/run ID, commit and source-snapshot digest, coverage transitions, last safe state, mutation/cleanup state, retry guidance, and bounded supporting fact/gap IDs. Receipt and stage IDs exclude duration.

The default receipt omits repository names, remotes, branches, absolute paths, source snippets and values, exception messages and stack traces, credentials, connection strings, and customer identity. A failed commit-bound scan may write only `scan-receipt.json`; it does not fabricate normal scan artifacts. When no exact commit SHA is available, TraceMap does not emit an authoritative receipt.

The receipt proves only what the scanner observed. It does not establish root cause, operator fault, application correctness, runtime reachability, production state, or complete repository coverage. The normative schema is [`contracts/scan-execution-receipt.v1.schema.json`](contracts/scan-execution-receipt.v1.schema.json), and rule `scanner.stage-receipt.v1` documents its limitations.
