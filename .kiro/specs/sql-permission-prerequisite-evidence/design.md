# SQL Permission Prerequisite Evidence Design

## Overview

The permission lane separates three concepts:

```text
explicit permission statements  -> permission evidence
context-sensitive operations    -> prerequisite candidates
compatible links                -> evidence status + limitations
```

This is not a PostgreSQL privilege simulator. The reducer reports what the
checked-in script set supports or leaves unproven.

## Proposed Rules

Rule names must be reconciled with the live catalog.

| Rule ID | Fact | Tier ceiling | Limitation |
| --- | --- | --- | --- |
| `database.postgres.permission.statement.v1` | `DatabasePermissionDeclared` | `Tier2Structural` | Static statement evidence, not effective live privilege. |
| `database.postgres.permission.prerequisite.v1` | `DatabasePrerequisiteCandidate` | `Tier2Structural` | Candidate mapping requiring owner validation. |
| `database.postgres.permission.coverage.v1` | `DatabasePrerequisiteEvidence` | `Tier2Structural` | Script-set evidence status only. |
| `database.postgres.permission.gap.v1` | `AnalysisGap` | `Tier4Unknown` | Ambiguous, missing, conflicting, or unsupported evidence. |

If existing fact types already carry these semantics, implementation should
reuse them and add only cataloged safe properties.

## Normalized Permission Model

Recommended closed fields:

| Field | Values/examples |
| --- | --- |
| `actionKind` | `grant`, `revoke`, `owner-change`, `default-privilege`, `role-membership` |
| `objectKind` | `database`, `schema`, `table`, `sequence`, `routine`, `foreign-server`, `foreign-wrapper`, `extension-capability` |
| `capabilityCode` | PostgreSQL-specific closed code, not free-form SQL |
| `contextRole` | shared source/archive/admin/scheduled/validation categories |
| `evidenceStatus` | `present-in-scripts`, `missing-evidence`, `conflicting-evidence`, `unknown`, `needs-owner-review` |
| `reasonCode` | cataloged reducer reason |

Object and role identity uses safe public identifiers only when allowed;
otherwise an opaque non-secret identity or unknown identity is retained.

## Candidate Mapping Registry

Maintain a versioned registry mapping operation kinds to capability candidates.
It should encode evidence relationships, not provider documentation as absolute
truth. Each mapping includes operation kind, object kind, candidate capability,
compatible contexts, provider/version qualifier, tier ceiling, and limitation.

Examples include extension creation capability, foreign-server usage, schema
create/usage, table access, publication/subscription ownership/capability, and
scheduler capability. RDS qualifiers never imply the live account has a role.

## Reducer

For every prerequisite candidate, the reducer finds compatible explicit
permission evidence using mechanism, safe identity, context, and declared order.
It then assigns status:

1. conflicting evidence or unsafe ambiguity;
2. compatible explicit statement present;
3. no linked checked-in evidence;
4. unknown because identity/context/order coverage is insufficient.

The reducer never treats absence as runtime denial and never calculates
transitive effective privileges. Cross-file results require declared order;
otherwise they carry an order-unknown limitation.

## Ordering

Within a declared sequence, preserve statement ordinals and flag meaningful
contradictions such as revoke after grant. Do not simulate transactional
rollback, conditional execution, exception paths, role switching, or object
catalog changes. `ALTER DEFAULT PRIVILEGES` remains a declaration with special
scope limitations rather than a grant applied to every object candidate.

## Safety and Output

Render category-level rows with repo-relative spans and evidence references.
Apply the secret-bearing-step allowlist before permission linking. Unsafe role,
object, or infrastructure identity reduces precision rather than leaking.

## Testing

Use synthetic multi-file fixtures with an explicit sidecar order and a parallel
fixture without declared order. Cover each action/object family, contradictions,
unsafe identities, context mismatch, RDS declaration, parser gaps, and leak
sentinels. Unit tests validate registry completeness and rule ownership;
integration tests validate storage/report wording and deterministic reduction.

## Future Work

SQL Server, Oracle, and MySQL need engine-specific candidate registries. A future
explicit validation artifact could report observed privileges, but it must
remain separate from static script evidence and cannot silently change these
statuses.
