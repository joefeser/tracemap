# SQL Phase 5: same-compilation internal helper callers across documents
Continue the bounded SQL Server consumer work as a separate branch and PR after the Phase 4
helper slice (branch `codex/sqlserver-consumer-helpers`) is merged. SQL coverage stays the
priority. Test-suite splitting and wall-clock optimization remain deferred: record observed
timings only; do not reorganize test projects, change concurrency or weaken gates.
## Start from current evidence
Read AGENTS.md, docs/VALIDATION.md, the SQLSERVER_DATATABLE_EVIDENCE, SQLSERVER_DATASET_EVIDENCE,
SQLSERVER_HELPER_RESULT_EVIDENCE and SQLSERVER_LIVE_PROVIDER_VALIDATION documents and the complete
`.kiro/specs/sqlserver-consumer-helpers` spec (including implementation-state). Inspect
`SqlServerHelperResults.cs`, `SqlServerHelperResultEvidence.cs`, their tests,
`samples/sqlserver-helper-result-evidence`, `samples/sqlserver-live-provider`, all seven SQL
smoke scripts and `scripts/dnn-sql-validation/{harness,linked_provider}.py` before editing.
Fetch origin/dev and verify the merged Phase 4 PR is an ancestor; if not, stop and report.
Create a clean dedicated `codex/` branch and worktree from the fetched dev head; preserve every
other worktree. New fixtures must be added to `TraceMap.Tests.csproj` as
`ReferenceOutputAssembly="false"` references in the same commit and verified with no
pre-existing fixture `bin/`/`obj/`.
## Scope: internal helpers called from other documents of the same compilation
Phase 4 admits only `private` helpers so every call site is in the Fill's document. Admit
exactly one widening: an `internal` (VB `Friend`) helper with the otherwise unchanged Phase 4
helper-body rules, whose call sites are enumerated across every document of the same
compilation. Author the accepted syntax matrix in a new spec before implementation, deciding at
least:
- how caller entries carry a repo-relative caller path (the extractor's normalized path
  resolver, never an absolute path), and how bindings and digests include it;
- the operation/document bounds for whole-compilation enumeration and the gap when exceeded;
- that `InternalsVisibleTo`, `public`/`protected` helpers, partial types, generated documents,
  linked files and callers in other compilations stay typed unknowns;
- whether caller completeness remains explicitly unclaimed (reflection, delegates) and how
  method-group references in other documents make the helper unknown.
Preserve every Phase 2, Phase 3 and Phase 4 refusal and output. Distinct versioned state (or a
versioned successor of `sqlserver-helper-result/2` with historical records kept), digest and
bindings; never let DataSet, DataTable, reader, parameter or Phase 4 bindings substitute. Bump
extractor versions.
## Evidence and validation gates
Only independently authored public synthetic C#/VB/SQL inputs for the static corpus. The
existing disposable dockercache SQL workflow may extend the linked static/runtime matrix only
with the same isolation, pinned image, limits, transient credentials, ownership-checked cleanup
and failed-run evidence; no private inputs, existing databases, published ports, persistent
volumes, unrelated container changes or work-machine requests. Author expected outcomes before
running the extractor, duplicate them in tests and a smoke with its own denominator, and keep
every retained smoke unchanged: 52-case parameter smokes (8/12/32), result smoke (JSON 36/10/36,
DDL 36/10/58, 20 refusals), table smoke (per language 22/5/44, 24 refusals), DataTable smoke
(per language 54/12/88 over 148 cases, 22 refusals), DataSet smoke (per language 78/12/88 over
169 cases, 24 refusals) and helper smoke (per language 42/15/58 over 115 cases, 24 refusals);
linked matrix 12/4/6 with 42 runtime checks unless deliberately extended. If retained outputs
change, stop and explain before accepting a new denominator. Commit the implementation, freeze
source/head/build, run the full Debug solution with `-warnaserror`, then all smokes sequentially
at the committed head in new directories, Python guards, private-path and whitespace checks, the
pinned C#/VB OSS adapter smoke (report its reduced coverage honestly) and the linked live
harness. Do not change ACK policy, never tag Qodo, do not merge or request paid review; open a
PR only when separately asked.
Final handoff: branch/commit, exact supported syntax matrix and denominator, expected versus
observed outcomes, tests/timings, runtime receipt and cleanup, gaps/non-claims and one
next-slice prompt. Do not claim DNN, private-application, deployment or migration readiness.
