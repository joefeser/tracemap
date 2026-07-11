# SQL Secret-Bearing Step Safety Tasks

## Implementation Plan

### Phase 1: Threat Model and Rules

- [x] 1.1 Inventory all SQL-derived fact, SQLite, report, log, export, combine, and exception paths.
- [x] 1.2 Finalize classification/category codes and catalog owning rules with false-negative limitations.
- [x] 1.3 Define a category-only allowlist and prohibit raw-secret hashing.
- [x] 1.4 Define sanitized diagnostic and CLI error codes for high-risk parsing failures.

### Phase 2: Detector and Projection Boundary

- [x] 2.1 Add PostgreSQL-aware structural detection for user mapping, FDW/dblink, subscription, and related connection inputs.
- [x] 2.2 Add conservative textual fallback and dynamic/unsupported gaps.
- [x] 2.3 Construct findings from the safe allowlist before generic fact serialization.
- [x] 2.4 Apply defensive allowlists to SQLite, reports, logs, exports, combine, diff, and runbook projections.
- [x] 2.5 Ensure unresolved high-risk statements fail closed for rendering while preserving category/span evidence.

### Phase 3: Runbook Safety Behavior

- [x] 3.1 Map secret-bearing/reference/possible-secret evidence to stop/owner-review conditions.
- [x] 3.2 Add language stating that absence of a finding is not proof of secret absence.
- [x] 3.3 Prohibit runnable secret templates, copy blocks, environment assignments, or secret-handling prescriptions.

### Phase 4: Synthetic Leak Tests

- [x] 4.1 Add unique public-safe sentinels across every supported high-risk SQL position.
- [x] 4.2 Assert full and partial sentinels do not appear in NDJSON, SQLite, Markdown, logs, stdout/stderr, exceptions, exports, or combined output.
- [x] 4.3 Assert raw secret digests are not emitted.
- [x] 4.4 Test comments, dollar-quoting, dynamic concatenation, malformed SQL, mixed case, false-positive controls, and unsupported dialects.
- [x] 4.5 Test deterministic category-only facts, gaps, ordering, and stable IDs.

### Phase 5: Validation

- [x] 5.1 Update security limitations, rule documentation, and `docs/VALIDATION.md`.
- [x] 5.2 Run focused scanner, serializer, reporter, exporter, combiner, and leak tests.
- [x] 5.3 Run `dotnet test src/dotnet/TraceMap.sln`.
- [x] 5.4 Run a CLI scan of the planted-sentinel fixture and inspect all required artifacts.
- [x] 5.5 Run `./scripts/check-private-paths.sh` and `git diff --check`.

## Follow-Ups Out of Scope

- Secret stores, credential brokering, encryption, rotation, or validation.
- Automatic remediation or runnable SQL generation.
- Proof that a script or repository contains no secrets.
- Live database, cloud, environment, or vault access.
