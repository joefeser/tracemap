# Implementation state — Phase 1

Branch `codex/sqlserver-table-result-shapes`, worktree
`~/.codex/worktrees/sqlserver-table-result-shapes/tracemap`, created from fresh `origin/dev` =
`fc14857a54044e8edfaea170dab3b01141c0842a` (#912 merge; ancestor verified). The user checkout
stayed on `codex/sqlserver-result-column-prompt`; the handoff prompt branch
`codex/sql-result-next-phases` (`e7a6bd11a`, prompt only) was read and not used as a base.
Other worktrees were not touched. #864/#865/#870/#908 read live as context; all open.

## Scope decisions

- New route/manifest/export/rule rather than widening `sqlserver-ddl-result`: the constant route
  and the parameter route still reject CREATE TABLE byte-for-byte as before.
- The shared admission core gets an optional table visitor; tables and procedures share one
  case-insensitive identity set (SQL Server schema-scoped object namespace).
- Unsupported table definitions are typed unknown (Tier4, no columns) rather than whole-input
  rejections, so a procedure over them is an explicit `table-definition-unsupported` shape.
  Malformed SQL, non-two-part/temp table names and other statements still reject everything.
- WHERE and every other clause stay unknown in this phase even though they would not change the
  column list; widening is a separate, deliberate step.
- Constant SELECTs without FROM reuse the #912 constant analysis inside the new route.
- No source extractor change: Phase 1 reuses `sqlserver-reader/1` and `sqlserver-result-bindings/1`.
  C# 0.22.13 / VB 0.10.16 unchanged; pinned OSS adapter smoke deferred for that reason.
- New smoke script imports the #912 result smoke helpers without modifying them, so the
  retained result receipt generator hash and denominators are unchanged.

## Oddities noticed

- A fresh worktree has no `obj/` for the synthetic fixture projects; scans then report
  `FailedOrPartial` and the smokes' build assertion fails. `dotnet restore` of the four
  `samples/sqlserver-{parameter,result-column}-evidence/{csharp,vb}` projects fixes it (ignored
  outputs, no tracked change).
- T-SQL accepts a trailing comma in a CREATE TABLE column list; my first "malformed" refusal
  example used it and was (correctly) admitted. Replaced with `PRIMARY` without `KEY`.
- ScriptDom sets `UniqueConstraintDefinition.IndexType` for CLUSTERED/NONCLUSTERED and exposes
  `FromClause.PredictTableReference` as a never-null list; both were initially mishandled in
  development and fixed before commit (tests caught both).
- xUnit display names truncate theory arguments at 50 characters; distinct rows still run and
  count separately (verified by a deliberately wrong row).
- One str_replace edit to the new test file did not persist; re-applied and verified by grep.

## Development validation (dirty tree at fc14857a5; not acceptance)

- First dev table smoke: all 142 rows matched the authored matrix; one refusal (trailing comma)
  was a wrong expectation. Rerun `/tmp/tm-table-dev2`: 64 cases/language, 44/10/88, 24/24
  refusals, ~70 s.
- New test class: 122 passed. Focused SQL lane (new + result + DDL + parameter + catalog + DNN):
  493 passed before the last two theory rows were added. Python guards: 15 `test_sqlserver_*`
  (5 new) + 17 DNN passed.

## Committed-head acceptance

Pending.
