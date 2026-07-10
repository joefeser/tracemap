# SQL Execution Context Contracts Tasks

## Implementation Plan

### Phase 1: Contract and Rules

- [ ] 1.1 Finalize the `sql-execution-context/v1` sidecar and bounded directive schema.
- [ ] 1.2 Add context declaration, syntax-candidate, and gap rules with documented limitations to the rule catalog.
- [ ] 1.3 Define closed categorical values, capability codes, conflict codes, and coverage labels.
- [ ] 1.4 Document that all context is intended/static and never runtime proof or a safety approval.

### Phase 2: PostgreSQL-First Extraction

- [ ] 2.1 Add deterministic SQL statement boundaries and safe line spans without retaining raw SQL.
- [ ] 2.2 Parse validated sidecars and bounded comment directives.
- [ ] 2.3 Detect conservative context candidates for extension, FDW, grants, replication, validation, and `pg_cron` surfaces.
- [ ] 2.4 Preserve partial results and emit cataloged gaps for malformed, conflicting, or unsupported context.

### Phase 3: Storage and Reporting

- [ ] 3.1 Persist safe context facts and gaps in `facts.ndjson` and `index.sqlite`.
- [ ] 3.2 Add a report section grouped by ordered context boundaries.
- [ ] 3.3 Add manual-client verification prompts and stop/needs-review conditions without generating runnable SQL.
- [ ] 3.4 Verify commit SHA, extractor version, rule ID, tier, span, coverage, and limitation survive all projections.

### Phase 4: Synthetic Fixtures and Tests

- [ ] 4.1 Add public-safe source/admin/archive/validation fixtures with neutral categorical identities.
- [ ] 4.2 Test sidecar/directive precedence, conflicts, unknown context, and adjacent context transitions.
- [ ] 4.3 Test `pg_cron` scheduling evidence without retaining the scheduled SQL body.
- [ ] 4.4 Test wrong-tab/wrong-database risk renders as needs-review, not observed runtime mismatch.
- [ ] 4.5 Add planted-secret, connection-string, hostname, raw-SQL, and absolute-path leak tests across all outputs.
- [ ] 4.6 Test repeated-scan ordering and stable fact/report identities.

### Phase 5: Validation and Documentation

- [ ] 5.1 Update the rule catalog, SQL evidence documentation, and `docs/VALIDATION.md`.
- [ ] 5.2 Run focused unit and integration tests.
- [ ] 5.3 Run `dotnet test src/dotnet/TraceMap.sln`.
- [ ] 5.4 Run a CLI scan against the checked-in synthetic fixture and inspect required artifacts.
- [ ] 5.5 Run `./scripts/check-private-paths.sh` and `git diff --check`.

## Follow-Ups Out of Scope

- Live database validation or pgAdmin integration.
- Runtime role, permission, schema, or connection verification.
- SQL Server, Oracle, or MySQL-specific context inference.
- Script execution, remediation, or safety certification.
