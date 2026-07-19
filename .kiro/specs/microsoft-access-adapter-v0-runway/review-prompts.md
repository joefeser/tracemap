# Microsoft Access Adapter v0 Runway Review Prompts

## Primary Spec Review Prompt

Review `.kiro/specs/microsoft-access-adapter-v0-runway/` for implementation
readiness. This is a spec-only branch. Do not edit files.

Report findings first in severity order. Focus on whether:

- the adapter remains local, Windows-only, deterministic, Git/commit bound, and
  compatible with the standard TraceMap artifact contract;
- the supervisor/worker/owned-Access-PID design can bound COM hangs without
  terminating unrelated Access processes;
- the controlled-copy, forced macro suppression, AutoExec/startup hostile
  canaries, and cleanup design are concrete enough to prevent accidental input
  mutation or execution claims;
- no row data, query result, raw SQL, connection string, private host/path,
  credential, VBA source, form/report expression, or macro command body can
  enter facts, IPC, reports, logs, or error messages;
- Git `HEAD`, dirty input, Git LFS, path traversal/reparse points, concurrent
  scans, deterministic identity, aggregate limits, and binary `1:1` evidence
  spans have testable contracts;
- schema/query facts reuse shared vocabulary only when semantics match and all
  new facts/rules have explicit tiers and limitations;
- reduced coverage and non-claims avoid SQL/query/macro/VBA execution, runtime
  reachability, linked-source connectivity, record contents, production state,
  permissions, release approval, or “safe to change” claims;
- Phase 0 through Phase 6 form a focused first implementation PR and forms,
  reports, VBA, macros, composition, and site work remain deferred.

Call out Medium+ actionable issues that must be patched before implementation.

## First-Slice Implementation Review Prompt

Review the future Phase 0 through Phase 6 implementation against this runway.
Do not authorize later-phase scope merely because supporting code is nearby.

Prioritize:

- any API path that can open the original, execute a query/macro/VBA/object,
  open a recordset, refresh a link, enumerate unsafe query results, or allow
  startup behavior;
- COM lifetime, timeout, owned-process identity, Job Object, worker crash, and
  temporary-copy cleanup defects;
- raw protected-material leakage through IPC, exception text, logs, test output,
  SQLite properties, reports, or deterministic IDs;
- incorrect Git/LFS attribution, symlink/reparse escape, output deletion, or
  dirty/untracked input acceptance;
- arbitrary COM enumeration order, case-collision merging, unstable IDs, or
  output-path/timestamp/temp-path identity contamination;
- missing rule catalog entries, incorrect evidence tiers, stale task checkboxes,
  or coverage wording that overstates structural evidence;
- tests that assert flags without proving hostile canaries stayed untouched.

Return severity, file/line or section, evidence, and a narrowly scoped fix.

## Re-Review Prompt

Re-review after patches. Confirm prior Medium+ findings are resolved, no new
execution or secret-safety path was introduced, first-slice scope remains
bounded, validation is recorded accurately, and `implementation-state.md` is
honest about Windows/Access integration coverage and deferred work.
