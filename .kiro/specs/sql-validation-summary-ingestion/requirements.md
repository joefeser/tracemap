# SQL Validation Summary Ingestion Requirements

## Goal

TraceMap SHALL compose narrowly observed, validator-produced SQL validation
outcomes beside its existing static SQL runbook evidence without connecting to
a database, executing SQL, or accepting operator prose as evidence.

Related issue: [#508](https://github.com/joefeser/tracemap/issues/508).

## Requirements

### 1. Versioned safe input

1. The input schema SHALL be `sql-validation-summary/v1` and reject unknown
   fields, free-form notes, raw output, SQL, rows, credentials, connection
   strings, target names, paths, and infrastructure identities.
2. The summary SHALL contain a safe artifact ID, repository identity, full
   commit SHA, observation and expiry timestamps, categorical target context,
   approved validator identity/version, SHA-256 digest, claim level, limitations,
   and closed assertion/result values.
3. CLI ingestion SHALL be explicit and repeatable through
   `--sql-validation-summary <path>` on `scan` and `release-review`, paired
   with an explicit RFC3339 `--sql-validation-as-of` instant.

### 2. Provenance and freshness

1. Repository and commit SHALL match the selected scan source exactly.
2. Target engine, server role, database role, schema role, and execution mode
   SHALL match a statically cataloged runbook context.
3. Freshness SHALL be evaluated against the explicit consumer-supplied
   `--sql-validation-as-of` instant so the same inputs produce the same result
   regardless of wall-clock time.
4. Observation time SHALL not be after expiry or after the deterministic
   evaluation time; expiry before evaluation produces an expired-summary gap.
5. The artifact digest SHALL cover canonical JSON with the digest value omitted.

### 3. Closed trust boundary

1. V1 SHALL accept only the documented validator ID and supported version.
2. Assertion codes and statuses SHALL use closed vocabularies.
3. Unknown validator, validator version, assertion, status, schema, claim level,
   or context value SHALL produce a structured gap and no accepted observation
   from the affected summary.
4. A typed `observed-pass` is a narrow observation only. It SHALL NOT mean a
   script is safe, a release is approved, permissions remain effective,
   production is healthy, rollback works, or the whole procedure succeeded.

### 4. Duplicate and conflict handling

1. Exact duplicate artifact IDs and digests SHALL be deterministically deduped
   and produce a duplicate-summary gap.
2. Reused artifact IDs with different digests, or different summaries for the
   same repository/commit/context/assertion identity, SHALL produce conflict
   gaps and contribute no observation for the conflicted identity.
3. Malformed, tampered, expired, mismatched, or unsupported summaries SHALL
   produce safe gaps rather than crash or silently disappear.

### 5. Composition

1. `sql-runbook.json` and Markdown SHALL render static evidence and accepted
   observed validation in separate sections.
2. Static facts SHALL preserve their rule IDs, evidence tiers, spans, commit,
   extractor versions, supporting fact IDs, coverage, and limitations.
3. Observations SHALL preserve validator, artifact, digest, timestamps, target
   context, assertion, result, rule ID, and limitations without pretending they
   have a static evidence tier.
4. Release review SHALL expose observations separately from static SQL evidence
   and copy ingestion failures into packet-level `ReleaseReviewGap` entries.
5. Gaps SHALL remain gap entries, never status values.

### 6. Safety and tests

1. Synthetic tests SHALL cover valid, expired, commit mismatch, context
   mismatch, unknown validator, unknown assertion, conflict, duplicate, tamper,
   malformed, and planted-secret inputs.
2. Ordering and stable IDs SHALL be deterministic.
3. Output SHALL never contain input paths, raw input JSON, planted secrets,
   private names, SQL text, connection details, or validator diagnostics.
4. TraceMap SHALL retain explicit non-claims: no SQL execution, runtime
   reachability, production database state, release approval, or safe-to-run
   conclusion.
