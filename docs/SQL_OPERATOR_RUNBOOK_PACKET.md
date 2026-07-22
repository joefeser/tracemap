# SQL Operator Runbook Evidence Packet

TraceMap scans that contain supported SQL evidence emit two additional artifacts:

- `sql-runbook.md` — a human-safe operator handoff packet.
- `sql-runbook.json` — the allowlisted `sql-operator-runbook-packet/v3` summary.

The packet projects cataloged execution-context, archive-link, protected-material,
permission-prerequisite, validation-step, cleanup-candidate, and gap evidence. It
does not copy SQL or generic fact property bags into the packet.

## Reading the Packet

Ordered context groups show the intended categorical engine, server role,
database role, schema role, and execution mode. A context change creates a
transition checkpoint. For manual clients such as pgAdmin, operators must
independently verify the active tab, connection, database, schema, and role.
TraceMap cannot observe those selections.

Milestones use static-only states such as `intended-by-script` and
`validation-step-present`. A validation query is only evidence that a validation
step was checked in; its result is not observed. `validation-evidence-not-provided`
is therefore the v0 observation state.

Permission prerequisites preserve the upstream closed statuses:
`present-in-scripts`, `missing-evidence`, `conflicting-evidence`, `unknown`, and
`needs-owner-review`. Even `present-in-scripts` does not establish effective
authorization.

Protected steps contain categories and owner-handling guidance only. Credential
values, connection material, remote query input, scheduled command bodies, and
private infrastructure identities are omitted.

## Non-Claims

The packet does not execute SQL, connect to a database, observe runtime state,
certify safety, approve changes, prove permissions, validate rollback, or replace
DBA/operator judgment. Missing or conflicting evidence remains a stop condition
or owner question rather than a clean-state conclusion.

## Observed Validation Boundary

Version 3 can ingest an explicit validator-produced `sql-validation-summary/v1`
artifact. Pass `--sql-validation-summary <path>` to `scan` or
`release-review`; the option is repeatable. Pair it with
`--sql-validation-as-of <RFC3339 timestamp>` so expiry decisions are explicit
and deterministic. TraceMap reads the artifact as data and never executes it.

The strict contract is documented at
`contracts/artifacts/sql-validation-summary.v1.schema.json`. It binds the
summary to repository, commit, categorical target context, observation/expiry
timestamps, approved validator/version, closed assertion/status values, and a
canonical SHA-256 digest. Freshness is evaluated against the explicit
`--sql-validation-as-of` instant, making repeated composition deterministic
while allowing validation to occur after the static scan.

Accepted observations appear under `observedValidation`, separate from static
milestones and their evidence tiers. Malformed, tampered, expired, unsupported,
mismatched, duplicate, ambiguous, and conflicting summaries become rule-backed
gaps. No input path, raw JSON, raw output, target name, SQL, credential,
connection detail, or private infrastructure identity is rendered.

An `observed-pass` value remains point-in-time and assertion-specific. It does
not establish continuing state, safe execution, complete procedure success,
release approval, or DBA attestation.

## Synthetic Smoke Test

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- scan \
  --repo samples/sql-operator-runbook \
  --out /tmp/tracemap-sql-runbook-smoke
```

Inspect `sql-runbook.md` and `sql-runbook.json`, then run the private-path guard.
The sample intentionally plants protected values; those values must not appear
in generated artifacts or logs.
