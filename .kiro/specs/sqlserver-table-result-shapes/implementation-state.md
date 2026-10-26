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

## Committed-head acceptance (implementation `c28dbd3df38ee80da684914677829dfee9a35644`)

All gates below ran at the implementation commit with source, HEAD and shared build outputs
frozen (`git status` empty and HEAD unchanged before and after); no competing build, smoke or
commit ran in this checkout during the full suite. Results are recorded by a later
documentation-only commit whose SHA differs.

- Focused Debug `-warnaserror` (new table class + result + DDL + parameter + catalog + DNN):
  **495 passed**, 0 failed/skipped (122 new table tests, 2 catalog). Python guards: 15
  `test_sqlserver_*` (5 new) + 17 DNN passed.
- Full Debug solution `dotnet test src/dotnet/TraceMap.sln -c Debug -warnaserror`:
  **5,077 passed, 0 failed, 3 expected skips** (HttpReportProvenanceInterop receipt env unset,
  Oracle full golden sweep opt-in, Windows-only deep projectless ASP.NET publish); 34m 4s tests,
  2,057 s wall; zero compiler/analyzer warnings.
- Then sequentially, new directories outside Git (`/tmp/tracemap-tr-c28dbd3-*`):
  - JSON 52-case parameter smoke: **8/12/32**, 19 s, generator `16f7b62d…` (unchanged).
  - DDL 52-case parameter smoke reusing those scans: **8/12/32**, 25 s, generator `118db6e0…` (unchanged).
  - Retained result smoke: JSON **36/10/36**, DDL **36/10/58**, **20/20 refusals**, 0 gaps, 67 s,
    generator `ce53c852…` (script unchanged since #912).
  - New table smoke: **64 cases per language, 44/10/88** (per language 22/5/44, expected =
    observed), 0 gaps, **24/24 refusals**, byte-identical JSON+Markdown repeats, 73 s, generator
    `aff4dd7ef885e207923dd52034e36ee58c5f600348397356314ee95de417d080`, bounded input
    `4b7eec018407f86876865670b4726e894a1f9ecf9338140195aeed96394652c6`.
  - Core SHA-256 `dce5cfff0c3f008249ea722b638478594678acbe3ae6071cabb7ad691281a8bd` in every
    receipt; ScriptDom parser unchanged `2dcf224afac347fa1788eeaa6f8db78a17805b82338590b7e51448ab356a54ce`.
- Private-path guard and `git diff --check fc14857a5..HEAD` passed at the implementation commit.
- Pinned OSS C#/VB adapter smoke: deferred, no extractor or adapter changed (versions unchanged);
  those repos contain no SQL calls, so they would not exercise this route anyway. Non-.NET,
  Windows and private/application checks not run (no such code changed).

## Delivery

PR #913 smoke-only review repair at `0d3bd85d3`: reject optimized Python before
helper imports, use explicit validation exceptions (including table report/status
checks), and validate counts/gaps/all 24 refusals before receipt publication.
Regression guards cover -O, -OO, PYTHONOPTIMIZE, incorrect exports/statuses and
failed aggregate receipt publication. All 18 SQL Python guards and 17 DNN guards
pass. Committed-head native table smoke passes unchanged: 64 cases per language,
44/10/88, zero gaps, 24 refusals and deterministic repeats. Existing helper files
and production .NET code are unchanged; the full .NET gate is not rerun for this
script/test-only repair. Prior implementation validation remains historical.

Docs-only results commit on top of `c28dbd3df`, Phase 2 prompt
`prompts/continue-sqlserver-datatable-consumers.md`, branch pushed. No PR, merge, review
request, SQL execution, container or private input. Phase 2 has not started.
