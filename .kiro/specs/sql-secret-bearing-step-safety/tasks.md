# SQL Secret-Bearing Step Safety Tasks

## Implementation Plan

### Phase 1: Threat Model and Rules

- [ ] 1.1 Inventory all SQL-derived fact, SQLite, report, log, export, combine, and exception paths.
- [ ] 1.2 Finalize classification/category codes and catalog owning rules with false-negative limitations.
- [ ] 1.3 Define a category-only allowlist and prohibit raw-secret hashing.
- [ ] 1.4 Define sanitized diagnostic and CLI error codes for high-risk parsing failures.

### Phase 2: Detector and Projection Boundary

- [ ] 2.1 Add PostgreSQL-aware structural detection for user mapping, FDW/dblink, subscription, and related connection inputs.
- [ ] 2.2 Add conservative textual fallback and dynamic/unsupported gaps.
- [ ] 2.3 Construct findings from the safe allowlist before generic fact serialization.
- [ ] 2.4 Apply defensive allowlists to SQLite, reports, logs, exports, combine, diff, and runbook projections.
- [ ] 2.5 Ensure unresolved high-risk statements fail closed for rendering while preserving category/span evidence.

### Phase 3: Runbook Safety Behavior

- [ ] 3.1 Map secret-bearing/reference/possible-secret evidence to stop/owner-review conditions.
- [ ] 3.2 Add language stating that absence of a finding is not proof of secret absence.
- [ ] 3.3 Prohibit runnable secret templates, copy blocks, environment assignments, or secret-handling prescriptions.

### Phase 4: Synthetic Leak Tests

- [ ] 4.1 Add unique public-safe sentinels across every supported high-risk SQL position.
- [ ] 4.2 Assert full and partial sentinels do not appear in NDJSON, SQLite, Markdown, logs, stdout/stderr, exceptions, exports, or combined output.
- [ ] 4.3 Assert raw secret digests are not emitted.
- [ ] 4.4 Test comments, dollar-quoting, dynamic concatenation, malformed SQL, mixed case, false-positive controls, and unsupported dialects.
- [ ] 4.5 Test deterministic category-only facts, gaps, ordering, and stable IDs.

### Phase 5: Validation

- [ ] 5.1 Update security limitations, rule documentation, and `docs/VALIDATION.md`.
- [ ] 5.2 Run focused scanner, serializer, reporter, exporter, combiner, and leak tests.
- [ ] 5.3 Run `dotnet test src/dotnet/TraceMap.sln`.
- [ ] 5.4 Run a CLI scan of the planted-sentinel fixture and inspect all required artifacts.
- [ ] 5.5 Run `./scripts/check-private-paths.sh` and `git diff --check`.

## Follow-Ups Out of Scope

- Secret stores, credential brokering, encryption, rotation, or validation.
- Automatic remediation or runnable SQL generation.
- Proof that a script or repository contains no secrets.
- Live database, cloud, environment, or vault access.
