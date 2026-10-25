# Implementation state

Branch `codex/sqlserver-result-column-evidence`, worktree
`~/.codex/worktrees/sqlserver-result-column-evidence/tracemap`, created from fresh
`origin/dev` = `730610582660fe32354479b35cf80eb2aa954a62` (#911 merge; ancestor
verified). The user checkout stayed on `codex/sqlserver-result-column-prompt`
(clean, handoff prompt only) and other agents' worktrees were not touched.
Live #908/#870 read for context only; both open and broader than this slice.

## Scope decisions

- Source evidence is a new property `sqlServerReaderState` (`sqlserver-reader/1`)
  on ExecuteReader-family candidates, produced by a separate walker. The parameter
  walker rejects all loops/conditionals, so reusing it would make every Read()
  loop unknown; the reader walker re-implements the command checks and adds the
  reader/ordinal classification. Extractor bumps: C# 0.22.10, VB 0.10.13.
- Rows are per access; call-level problems give exactly one unknown row so
  unknowns survive. Every DatabaseOperationCandidate gets at least one row.
- JSON duplicates are admitted (legal SQL) and compared unknown; DDL duplicate or
  case-only aliases make the whole shape unknown (spec requires unique aliases).
- DDL result route reuses the whole parameter admission core through an internal
  visitor; therefore procedures with unsupported parameter types still reject the
  whole result input. Distinct manifest version `sqlserver-ddl-result-input/1`.
- New bindings contract with `readerEvidenceSha256`; parameter `CallDigest`
  bindings cannot bind reader evidence.
- `PublishNewDirectory` gained an optional `requiredFiles` argument (default
  unchanged); DDL source reading/bounded reads were extracted unchanged into
  internal helpers for reuse.
- Only ASCII identifier column names are retained from source constants; other
  constants become `column-name-outside-identifier-vocabulary` gaps.

## Oddities noticed (not changed)

- `database.sqlserver.ddl-parameters.v1` from #911 has no rule-catalog entry.
  Out of scope here; recorded for the follow-up.
- Report rows are ordered by fact ID (hash), mirroring the parameter route;
  deterministic but not source order.

## Development validation (dirty tree at 730610582; not acceptance)

- Fixture builds (C#/VB, -warnaserror) clean. Dev native scans: 34 sites each,
  reader states matched the independently authored expectations on first run.
- Dev result smoke `/tmp/rc-smoke-dev1`: first attempt failed my own denominator
  guard (EXPECTED_TOTALS was written as two-language totals; corrected to
  per-language). Rerun passed: JSON 36/10/36, DDL 36/10/58, 20/20 refusals.
- First new-class test run: 138/140; two wrong test expectations (xml parses as
  XmlDataTypeReference → `alias-clr-or-table-type-unsupported`; a 50,000-column
  token test hit the file-size bound first). Corrected the tests, not the code.
- Broad focused lane (SQL/semantic/VB/snapshot/migration/index classes): 506 passed.
- Final precommit focused Debug -warnaserror (new + DDL + parameter + DNN):
  **348 passed**, 0 failed/skipped (142 new + 206 retained). Rule-catalog tests 76
  passed. Python guards: 9 `test_sqlserver_*` (5 new + 4 reuse) and 17 DNN passed.
- Private-path guard and `git diff --check` passed before the implementation commit.

## Remaining protocol

Commit implementation; freeze source/HEAD/build outputs for full Debug
-warnaserror; then sequential committed-head JSON, DDL and result smokes; pinned
adapter smoke decision; guards; docs-only results commit; follow-up prompt; push.
