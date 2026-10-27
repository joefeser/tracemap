# Public SQL acceptance design

The runner composes existing native gates without changing their bytes or
denominators. It validates each gate's exact selected-case key set against its
authored plan and verifies the on-disk native reports, scan artifacts and script
identities before writing the aggregate packet. It never interprets a successful
process exit alone as application acceptance.

Evidence legs are deliberately separate:

Additional same-execution check: existing independently authored `WithParameter`
in both reader fixtures adds `@Id Int`, initializes a reader from ExecuteReader,
and accesses Name. Compare that exact fact through distinct parameter and reader
digests. Four independently authored variants per language: good (M/M), parameter
BigInt declaration mismatch (X/M), renamed result (M/X), absent database identity
(U/U). Expected statuses were fixed before execution. This adds eight linked
pairs, not a new application denominator, and uses no helper for the direct route.

| Leg | Public synthetic denominator | Expected bounded match / mismatch / unknown |
| --- | --- | --- |
| Parameter declarations and explicit call bindings | 52 rows | 8 / 12 / 32 |
| Reader + JSON first-result contract | 82 rows | 36 / 10 / 36 |
| Reader + constant DDL first-result contract | 104 rows | 36 / 10 / 58 |
| One private helper + bound caller + result contract | 230 rows | 84 / 30 / 116 |
| Field reads, including internal helper callers | 156 rows | 78 / 36 / 42 |

Each native gate already pins exact case outcomes, source facts, tier/rule/span,
digest-specific bindings, deterministic repeated reports and negative admission
controls. This package adds cross-gate orchestration, retained-byte verification
and explicit real-application boundary reporting; it does not create new proof
that two independent fixture commands are the same execution.

The real DNN denominator remains one operation. Existing source verification
pins wrapper forwarding, qualifier composition, generated `;Exec` translation,
reader hydration and original SQL install-file bytes. Native scan must retain
three incidental nonquery candidates and no selected-reader terminal, with
partial coverage. Four independently reviewed blocker categories remain:
`selected-reader-binding-unavailable`, `qualified-object-identity-unavailable`,
`sql-install-contract-not-admitted`, `hydration-consumer-binding-unavailable`.
These labels describe this inspected slice; they are not new native rules.

The receipt is local-only (not a privacy sanitizer). It hashes only committed
public fixture inputs, public upstream file projections, generated public reports
and tool bytes. It contains no local absolute paths, raw source or credentials.
Separate source/helper/contract/consumer statuses make the unfinished join clear.
