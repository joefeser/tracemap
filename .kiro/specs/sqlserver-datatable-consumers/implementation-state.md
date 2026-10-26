# Implementation state — Phase 2
Branch `codex/sqlserver-datatable-consumers`, worktree
`~/.codex/worktrees/sqlserver-datatable-consumers/tracemap`, created from fresh `origin/dev` =
`4f664daf5` (#913 merge; Phase 1 implementation `c28dbd3df` verified as ancestor). The user
checkout (`codex/sqlserver-result-column-prompt`) and all other worktrees were not touched.
## Scope decisions
- State lives on the existing Fill DatabaseOperationCandidate (`operationKind`
  `data-adapter-fill`) as `sqlServerDataTableState`; no new fact type. Every Fill fact gets the
  property (unknown when outside the subset), mirroring how reader state covers every
  ExecuteReader fact.
- `new DataTable(name)` is unsupported: TableName feeds the adapter's table-mapping lookup;
  deferred to keep the fresh-table/empty-mapping argument trivial.
- `Field<T>` is unsupported: it lives in System.Data.DataSetExtensions, outside the trusted
  ADO.NET assembly set, and would widen trust.
- Case-only names stay unknown although DataTable lookup falls back case-insensitively at
  runtime. Duplicate shape names make absent-name results unknown because Fill renames
  duplicates (`Name1`).
- New fixture directory so the retained #912/Phase 1 fixtures, denominators and receipts are
  unchanged; shapes are reused from `samples/sqlserver-result-column-evidence`.
## Oddities noticed
(none yet)
