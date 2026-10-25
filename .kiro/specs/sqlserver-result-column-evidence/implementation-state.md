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

## Committed-head acceptance (implementation `1660a5a8cb0107abb3792788f3eed637987ac30b`)

All gates below ran at the implementation commit with source, HEAD and shared
build outputs frozen (`git status` empty and HEAD unchanged before and after);
no competing build, smoke or commit ran during the full suite. Results are
recorded by a later documentation-only commit whose SHA differs.

- Full Debug solution `dotnet test src/dotnet/TraceMap.sln -c Debug -warnaserror`:
  **4,930 passed, 0 failed, 3 expected skips** (HttpReportProvenanceInterop receipt
  env unset, Oracle full golden sweep opt-in, Windows-only deep projectless
  ASP.NET publish); 29m 37s tests, 1,787 s wall. Zero compiler/analyzer warnings.
- Then sequentially, new directories outside Git:
  - JSON 52-case smoke `/tmp/tracemap-rc-1660a5a-json`: 52/52, **8/12/32**,
    generator `16f7b62d92d3e8e34573d09f46fdaf6e6437db5553cc85f6578f63ce58548c46`,
    bounded input `20ee5a3cdb517448b59768060d890f332742e02568d308de9a1c28e51f41efd8`.
  - DDL 52-case smoke reusing those scans `/tmp/tracemap-rc-1660a5a-ddl`: 52/52,
    **8/12/32**, five scan artifacts per language re-verified, generator
    `118db6e0d3ab192ea463f6378a1c6b2922c0ef16f9a13a2f19cfdcb4ffefd334`, bounded input
    `5676094d22d667cab3f5b563b75ab95a5dc7eca7c1491cf08301681a4878d904`.
  - New result smoke `/tmp/tracemap-rc-1660a5a-result`: 158 case reports, JSON
    **36/10/36**, DDL **36/10/58** (expected = observed), 0 gaps, **20/20 refusals**,
    byte-identical JSON+Markdown repeats per case, generator
    `af02e6722879edf4b8d0205d63297b9af87895ba2d6ca9bd2ed6810886aaf205`, bounded input
    `2081b429d67aa09870c583513b2a3d3d96e8a579a8695f0468bfbee4698296e7`.
  - Core SHA-256 `430b0ee3151b3831fe72c6c3e77a4ec3ce955a87ba29fa3e88e62f10a15b9a88`
    (all three receipts and every export/report); ScriptDom parser unchanged
    `2dcf224afac347fa1788eeaa6f8db78a17805b82338590b7e51448ab356a54ce`.
- Pinned OSS adapter smoke (`TRACEMAP_SKIP_BUILD=1
  TRACEMAP_OSS_SMOKE_REPOS=ProjectExtensions.Azure.ServiceBus,community-visual-basic`,
  public pinned clones): exit 0, all five artifacts each; C# `csharp-semantic/0.22.10`
  12,949 facts, VB `vb-semantic/0.10.13` 114,806 facts, both
  `Level1SemanticAnalysisReduced` / `FailedOrPartial` (documented expected partial
  coverage). Neither repo has a DatabaseOperationCandidate, so this only shows the
  new property does not disturb unrelated extraction; it exercises no reader path.
  Non-.NET adapters, fluentjdf and Windows/private application checks deferred:
  no non-.NET code changed; reader evidence is covered by the synthetic corpus only.
- Private-path guard and `git diff --check` passed again after the gates.

Core differs from the development smoke's Core because the assembly embeds the
commit; source was unchanged between the final focused run and the commit.

## Delivery

Docs-only results commit on top of `1660a5a8c`; follow-up prompt
`prompts/continue-sqlserver-result-column-evidence.md`; branch pushed. No PR,
merge, review request, SQL execution, container or private input. #908/#870 open.
