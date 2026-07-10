# SQL Permission Prerequisite Evidence Tasks

## Implementation Plan

### Phase 1: Rules and Candidate Registry

- [ ] 1.1 Finalize permission action, object, capability, evidence-status, and reason-code vocabulary.
- [ ] 1.2 Add owning rules and limitations to the catalog before emitting facts.
- [ ] 1.3 Implement a versioned PostgreSQL prerequisite candidate registry with provider/version qualifiers.
- [ ] 1.4 Document that registry entries are review guidance, not authorization truth.

### Phase 2: Permission Extraction

- [ ] 2.1 Extract supported grants, revokes, ownership changes, default privileges, and role memberships.
- [ ] 2.2 Normalize schema/table/sequence/routine/foreign-server/wrapper and extension capability targets safely.
- [ ] 2.3 Integrate execution context and secret-bearing-step safety boundaries.
- [ ] 2.4 Emit gaps for dynamic identifiers, unsupported clauses, procedural SQL, and parse ambiguity.

### Phase 3: Prerequisite Reduction

- [ ] 3.1 Map archive-link and scheduled operations to candidate capabilities.
- [ ] 3.2 Link compatible explicit statements by safe identity, context, and declared order.
- [ ] 3.3 Emit present-in-scripts, missing-evidence, conflicting-evidence, unknown, and needs-owner-review statuses.
- [ ] 3.4 Preserve supporting/contradicting facts and propagate reduced coverage.
- [ ] 3.5 Add deterministic ordering review conditions without simulating runtime state.

### Phase 4: Storage, Reports, and Tests

- [ ] 4.1 Persist safe permission and prerequisite evidence through NDJSON and SQLite.
- [ ] 4.2 Add the prerequisite table with owner questions and runtime limitations.
- [ ] 4.3 Add synthetic fixtures for all supported action/object/candidate families.
- [ ] 4.4 Test present, missing, conflicting, unknown identity/order, RDS declaration, and partial coverage.
- [ ] 4.5 Add leak tests for planted roles, infrastructure, credentials, SQL, and paths.
- [ ] 4.6 Test stable reducer results across repeated scans and shuffled input enumeration.

### Phase 5: Validation

- [ ] 5.1 Update rule, SQL evidence, and validation documentation.
- [ ] 5.2 Run focused extractor, registry, reducer, storage, report, safety, and determinism tests.
- [ ] 5.3 Run `dotnet test src/dotnet/TraceMap.sln`.
- [ ] 5.4 Run a CLI scan of the synthetic permission/archive fixture.
- [ ] 5.5 Run `./scripts/check-private-paths.sh` and `git diff --check`.

## Follow-Ups Out of Scope

- Runtime effective privilege or IAM analysis.
- RLS/policy, transactional, branch, or stored-procedure execution simulation.
- Grant generation, execution, or remediation.
- Non-PostgreSQL permission registries.
