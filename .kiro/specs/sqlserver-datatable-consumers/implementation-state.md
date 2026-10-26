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
- The #912 command classification and unsupported-flow predicate were extracted into shared
  helpers (`SqlServerReaderColumns.ClassifyCommand` / `UnsupportedFlow`) in a separate
  behavior-preserving commit; the reader lane (368 tests) passed unchanged before any new code.
## Oddities noticed
- First dev scan of the new fixtures (dirty tree, before any comparison code): 180 of 184
  Fill states matched the authored gaps. Three mismatches were code defects, fixed in code:
  VB reduced extension calls (`AsEnumerable`, `Field(Of T)`) carry the receiver as `Instance`
  rather than as an argument (now classified by `ReducedFrom` in both shapes); one was an
  authoring inconsistency: the documented check order ran the implicit-receiver flow check
  before adapter construction, so `new SqlDataAdapter { SelectCommand = ... }` reported
  `implicit-receiver-unsupported` instead of the matrix's `adapter-initializer-unsupported`.
  Construction checks now precede the implicit-receiver check; the order line in design.md was
  corrected to match. No expectation was changed to fit output.
- The CLI facts input initially reused the 4 MiB public-artifact bound; the new fixture's scan
  `facts.ndjson` is ~7 MiB, so the first dev smoke failed `input-size-limit`. The DataTable
  route now reads facts with an explicit 64 MiB bound; the existing routes are unchanged (their
  fixtures stay below 4 MiB).
- `rows-count+foreach` was the first combined guard spelling; System.Text.Json escapes `+`
  (`\u002B`), so the Python mirror's digest disagreed with Core. Renamed to
  `rows-count-and-foreach` before any acceptance run.
## Development validation (dirty tree at d3bce6012; not acceptance)
- Dev DataTable smoke `/tmp/tm-dt-smoke-dev3`: 148 cases per language, 108 / 24 / 176 (expected =
  observed), 0 gaps, 22/22 refusals each for its intended reason, ~2m18s.
- New test class 59 passed on first run; a deliberate two-row expectation mutation made both
  native theories fail (then restored). Focused lane (new + table + result + DDL + parameter +
  catalog + DNN + Razor + SQL matrix + VB data boundary) 581 passed with `-warnaserror`.
- Python guards: 24 `test_sqlserver_*` (6 new) + 17 DNN passed.
