# Implementation State

- Branch: `codex/access-report-lineage-remainder-reduction`
- Base: `origin/dev` at `2633b34bac97aae795d19cc9da2f503197ff0200`
- Starting evidence: 93 report binding remainders from the immutable snapshot used by PR #583.
- Primary cluster: 52 report crosstab output candidates plus 12 domain-field-catalog gaps that reference crosstab outputs.
- Decision: emit existing query-output facts for statically declared crosstab outputs, allowing existing report/domain composition to consume them. Do not create a parallel report-only contract.
- Safety: bounded QueryDef text parsing only; no query execution, row access, recordset opening, report rendering, or runtime availability claim.
- Implementation: the QueryDef reader now emits unique static crosstab row-heading, aggregate-alias, and literal pivot output facts. Existing query-output composition resolves report controls and domain-expression fields without a parallel report-only contract.
- Versions: `tracemap-access/0.3.0`, `legacy-access/0.3.0`, and `access-design-evidence/0.2.0` identify the changed projection behavior.
- Validation: 124 focused Access projector/composition tests pass; same-snapshot regeneration and full validation pending.
- Deferred: dynamic pivot outputs, owner obsolescence decisions, genuinely ambiguous report sources, and report-layout reconstruction.
