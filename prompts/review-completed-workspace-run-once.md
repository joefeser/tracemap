# Review the completed workspace run — read-only, once

Ask the owner for ONE folder: the completed workspace run containing
`START-HERE.md` and `workspace-run.local.json`. Do not request source code,
SQL, DLLs, hashes or all setup answers again. The owner has already completed
collection after resolving UTF-16 BOM, database-context and SQL width guards.

Read the checkout's AGENTS.md. This dispatch authorizes a bounded review of
existing LOCAL evidence and a new human-readable review note only. It does NOT
authorize implementation, another scan, workspace verify/collect, wizard repair,
config/source/SQL edits, receipt rewriting, re-attestation, builds/restores,
application execution, database access, PR, commit, push or merge. Read-only
SQLite SELECT/PRAGMA queries are allowed; executing T-SQL is not.

## Locate and preserve the exact run

- If the supplied folder is a workspace root, list only its immediate `runs`
  children and inspect their run records (at most 50). Select automatically only
  when exactly one completed candidate exists. Otherwise ask which run to review;
  do not silently choose by timestamp or reuse an earlier favorable run.
- A native scan/report folder alone is not the enclosing workspace run. Ask for
  its owning workspace run if that cannot be established from recorded links.
- Resolve paths using the run record/report's documented base, not the shell CWD.
  Check existence/type/containment before following links. Do not crawl the disk
  or source repositories. Read an external config only if an explicit recorded
  link identifies it and it is needed to understand scope; otherwise mark unknown.
- Preserve every run, output and configuration. Write any optional local note to
  a new file OUTSIDE the run, with a unique name; no derived JSON/CSV/database or
  uploaded artifact. Private names, source, SQL, paths and fingerprints stay local.

## Review the recorded evidence

Use one pass, at most 30 tool calls/10 minutes. Do not spawn agents or repeatedly
retry. Stream large NDJSON; use bounded queries instead of loading entire graphs.
If a check cannot complete within the budget, label it NOT CHECKED, never passed.

1. Read `START-HERE.md`, `workspace-run.local.json`, `report.md` and the explicitly
   linked source/native/SQL reports. Record final stage and outcome. `stage=completed`
   with `outcome=partial` can establish collection completion with reduced coverage;
   a directory, exit zero or a report's existence alone cannot establish it.
2. Verify required channels are actually present: intended source-repo outputs,
   combined index, recorded native raw scan/workbench/checkpoint/preflight, and SQL
   definitions/report/index. Resolve `compiledReview` and `sqlEvidence` from the
   record; don't assume they are merged into `combined.sqlite`. Missing required
   evidence is a finding, not permission to regenerate it.
3. Compare stored output commitments with the actual completed bytes where their
   mapping is established: `inputs["combined-index"]`, `sql-output:*`, and the
   recorded native manifest/index/checkpoint/preflight identities. Consult the
   checked-out schema/writer implementation if needed. Do not guess a path from an
   arbitrary input key. Report absent/unverifiable commitments explicitly. The run
   generator SHA is a binary fingerprint, not a Git commit; today's checkout/tool
   is not proof of which generator produced historical evidence. Keep per-channel
   generator/extractor/parser identities distinct.
4. Open each relevant SQLite index read-only (e.g. Python sqlite3 URI
   `Path.resolve().as_uri() + "?mode=ro"`, `uri=True`, then `PRAGMA query_only=ON`).
   Inspect `sqlite_master`/table_info before querying; don't invent schema columns.
   Run `PRAGMA quick_check` and aggregate row counts with time/progress bounds.
   Check the combined, native raw and SQL sidecar indexes, plus available source
   indexes. Correlate counts against their own manifests/reports; don't compare
   unrelated channels as if their counts must match.
5. Source scope: report selected repository count/roles, project versus solution
   selection, include/exclude/target/page scope when recorded, analysis level and
   build status per source. Verify whether both intended source repos and primary/
   dependency compiled inputs are represented. If intent isn't recorded, say so;
   don't invent a missing repo or infer whole-solution coverage.
6. The owner selected a `.csproj` because choosing the `.sln` root previously
   failed. `WORKSPACE_PAGE_SCOPE_IS_NOT_REPOSITORY_SCOPE` is a scope warning, not
   by itself a failed collection or proof that selecting the csproj caused it.
   Distinguish source project scope, native source/page scope, compiled admission
   and repository inventory. Read existing failure logs only if explicitly linked.
   Do NOT reproduce the solution-root crash, switch to the sln, broaden scope or
   request a rescan during this review. Log it as a separate unverified follow-up.
7. SQL: report input/export/extractor/parser versions, file/object/parameter/
   dependency counts, complete versus unknown result shapes, gap categories and
   local-only/partial labels. Check source fingerprint/context consistency only
   where the retained evidence establishes it. Do not reparse the original SQL or
   contact a database. A 512-column/conditional synthetic result is not an expected
   private result count. Admitted definitions are not deployed schema, application
   call binding, runtime values or independent owner-declared result schema.
8. Native/IL: summarize retained versus omitted/rejected metadata, coverage and
   major gap categories. Only evaluate lifecycle roots/candidates/state alternatives
   if the run actually requested that route; zero unrequested lifecycle candidates
   is not a failed workspace scan. Retain allocation/execution identity and unknown
   evidence. Do not substitute historical ROOT-A/ROOT-B results for this run.

## Deliver one bounded report and stop

Findings first, severity ordered, with local artifact/table/property evidence.
Distinguish missing/tampered artifacts, scope limitations, analysis gaps and
optional improvements. Give a verdict: collection completed with stated coverage,
incomplete/inconsistent evidence, or unable to verify. Identify the single next
owner action if one is necessary. Do not propose another scan just to silence
partial coverage, and do not claim the whole SQL/application effort complete.

Return at most 35 SANITIZED lines for sharing back:

- Review target: alias RUN-1; final stage/outcome; collection complete yes/no/unknown.
- Identity/recorded output-hash checks: matched/mismatched/not-checkable counts;
  public tool commit only if independently established, otherwise unknown.
- Required artifacts and SQLite checks: per-channel presence/integrity/row counts.
- Source scopes: role aliases SOURCE-1/SOURCE-2, csproj/sln/folder modes, analysis
  levels/build statuses, and warning codes; no private names or paths.
- Native channel: admission/coverage/gap-category counts.
- SQL channel: versions, file/object/parameter/dependency and complete/unknown
  shape counts, gap-category counts; no identifiers, literals or raw fingerprints.
- Scope verdict, discrepancies, blockers and one next action (or none).
- Separate follow-up: solution-root failure remains unverified/not reproduced.
- Explicit NOT CHECKED items and local review-note existence.

No private database/server/repo/project/type/method names, paths, code, SQL,
connection strings, source hashes or receipts in the shared response. Use aliases.
No uploads or outbound messages. Keep detailed evidence private on this machine.
