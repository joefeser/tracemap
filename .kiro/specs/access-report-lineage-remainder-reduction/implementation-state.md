# Implementation State

- Branch: `codex/access-report-lineage-remainder-reduction`
- Base: `origin/dev` at `2633b34bac97aae795d19cc9da2f503197ff0200`
- Starting evidence: 93 report binding remainders from the immutable snapshot used by PR #583.
- Primary cluster: 52 report crosstab output candidates plus 12 domain-field-catalog gaps that reference crosstab outputs.
- Decision: emit existing query-output facts for statically declared crosstab outputs, allowing existing report/domain composition to consume them. Do not create a parallel report-only contract.
- Safety: bounded QueryDef text parsing only; no query execution, row access, recordset opening, report rendering, or runtime availability claim.
- Implementation: the QueryDef reader now emits unique static crosstab row-heading, aggregate-alias, and literal pivot output facts. Existing query-output composition resolves report controls and domain-expression fields without a parallel report-only contract. Report host context identifiers (`Page` and `Pages`) are classified separately from database bindings. The design composer also reconciles a direct control source to an existing hash-only query output only when the report has one exact query record source and the hash resolves to one output identity.
- Versions: `tracemap-access/0.3.0`, `legacy-access/0.3.0`, and `access-design-evidence/0.2.0` identify the changed projection behavior.
- Same-snapshot result: deterministic binding remainders fell from 108 total / 93 report to 39 total / 27 report. Static crosstab-output candidates fell from 55 to zero. The remaining report clusters are 12 DCount expressions whose `[1]` through `[12]` field references do not match the declared `W0`/`W1`-style query outputs, six inline-SQL output mismatches, five ambiguous record sources, two unresolved functions, one ambiguous target, and one partial inline-SQL projection.
- Validation: 127 focused Access projector/composition tests pass; regenerated artifacts pass `validate-adapter-artifacts.py`; the full solution builds with zero warnings/errors; all 1,132 solution tests pass; the private-path guard and `git diff --check` pass.
- Deferred: dynamic pivot outputs, owner obsolescence/spelling confirmations, genuinely ambiguous report sources, DCount field/query mismatch review, and report-layout reconstruction.
