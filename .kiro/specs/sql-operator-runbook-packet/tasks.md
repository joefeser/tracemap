# SQL Operator Runbook Packet Tasks

## Implementation Plan

### Phase 1: Dependency and Schema Gate

- [ ] 1.1 Confirm upstream context, archive-link, secret-safety, and permission fact contracts are implemented and cataloged.
- [ ] 1.2 Define the versioned allowlisted packet JSON schema and Markdown information architecture.
- [ ] 1.3 Define closed milestone states, stop-condition codes, owner-question templates, and limitations.
- [ ] 1.4 Catalog any packet-specific reducer/report rule before emitting conclusions.

### Phase 2: Ordered Packet Reducer

- [ ] 2.1 Group steps by declared order and categorical context with explicit transitions.
- [ ] 2.2 Add manual-client active-context verification checkpoints.
- [ ] 2.3 Project permission evidence and protected-step findings without unsafe properties.
- [ ] 2.4 Reduce intended surfaces, validation-step candidates, and cleanup candidates into non-runtime milestones.
- [ ] 2.5 Convert upstream gaps into deterministic stop conditions and category-level owner questions.
- [ ] 2.6 Preserve failed/partial coverage through every packet section.

### Phase 3: Safe Renderers

- [ ] 3.1 Emit deterministic Markdown with no executable SQL or copy/run controls.
- [ ] 3.2 Emit the compact machine-readable summary without generic fact-property bags.
- [ ] 3.3 Reapply safe-output allowlists and phrase checks at renderer boundaries.
- [ ] 3.4 State all runtime, approval, safety, permission, validation, and rollback limitations clearly.

### Phase 4: Synthetic Fixtures and Tests

- [ ] 4.1 Add a complete public-safe PostgreSQL archive handoff fixture using neutral contexts and identifiers.
- [ ] 4.2 Add wrong-context, missing-permission, missing-validation, unknown-order, contradictory-static-evidence, and partial-parser variants.
- [ ] 4.3 Add deterministic Markdown/JSON golden tests and schema compatibility tests.
- [ ] 4.4 Add planted-secret, connection, infrastructure, path, SQL, validation-output, and ticket-like leak tests.
- [ ] 4.5 Add forbidden runtime/safety claim tests and executable-block absence tests.
- [ ] 4.6 Run the CLI against the synthetic fixture and verify all required scan artifacts plus the packet output.

### Phase 5: Documentation and Validation

- [ ] 5.1 Document packet semantics, limitations, and the future validation-artifact boundary.
- [ ] 5.2 Update `docs/VALIDATION.md` with the synthetic packet smoke workflow.
- [ ] 5.3 Run focused reducer, renderer, schema, safety, phrase, and determinism tests.
- [ ] 5.4 Run `dotnet test src/dotnet/TraceMap.sln`.
- [ ] 5.5 Run `./scripts/check-private-paths.sh` and `git diff --check`.

## Follow-Ups Out of Scope

- `sql-validation-summary/v1` ingestion or live validation.
- SQL execution, copy/run UX, approval workflow, or ticket integration.
- Runtime postmortem conclusions or database-state reconciliation.
- Non-PostgreSQL packet-specific content beyond the shared schema extension points.
