# Implementation state

## PR #920 integration after #919 merge

Normal merge of `origin/dev` at `c61e764021b39eea65ab6891caa3fba7c6ed33ed`
preserves both histories. The only conflicts were extractor versions and the Razor
version assertion; retain parent C# 0.22.23 / VB 0.10.26. Public packet Field refusal
expectation is now 52 (26 per language), preserving the corrected parent contract.
Focused integration includes initializer, Razor, Field scope/cache and near-limit
regressions before any full-suite rerun: 213 passed, plus 11 public and 59 SQL Python
guards. ACK before integration
reported 10 unresolved threads and a failed Sourcery review check; reviewer batch
and quorum settled but review patch authority was blocked pending owner decision.

Owner explicitly authorized one bounded verified repair/validation/push batch;
this does not override merge readiness, policy, reviewer spending or failed checks.
The current batch binds separate artifact inputs, reconstructs native input and
case/report contracts, validates exact refusal IDs, isolates committed source and
build outputs, and atomically publishes the packet. Linked reader scans were
already included; literal argv with shell disabled was already safe. Tests pin
those two dispositions. Final repair-gate results are recorded below.

Development validation: 20 public acceptance guards pass, including canonical
CRLF/Git-blob identity, transient original-checkout edits and independent build
directories. All four retained native receipts pass the new strict verifier.
An overbroad `test_sql*.py` discovery invocation also selected 32 command-text
tests whose required CLI CONFIG was not initialized; those errored before test
execution. This is a failed invocation, not a passing gate. The targeted standalone
SQL smoke guards are run separately with their supported invocation below.

Branch `codex/sqlserver-public-acceptance`; parent frozen at
`6aa8c40e975b9277418aa3bc4b561073887c7f3e` (`codex/sqlserver-field-reads`).
Stacked package: offline public acceptance replay and tested blockers, not another
consumer feature. No parent worktree/build/output is modified. No runtime lane
was activated during initial development. Final coordinated integration results
are recorded below; the existing disposable regression ran with owned cleanup.

Inspection: DNN's selected operation remains outside concrete reader/parameter
and hydration binding. Helper consumer bindings do not prove parameter bindings;
the packet must not collapse these into one alleged complete application path.

## Proven defect and bounded repair

The new same-execution check found `WithParameter` had a bounded reader match but
unknown parameter state (`nested-alias-or-escape`): the enclosing variable
declarator started before the execution span and misclassified the execution
itself. Exempt only a declarator whose initializer is reference-identical to the
current invocation, before conversion unwrapping. C# 0.22.21 / VB 0.10.24; ten
positive/negative initializer regressions. Focused parameter suite: 91 passed.
Shared Core change requires a final full suite after parent coordination.

## Development failures (not acceptance)

- Initial replay at 54291e017 failed on missing fixture assets. The runner now
  builds the eight selected public fixture projects before their scans.
- Replay at a7e2040f6 passed parameter/reader native gates, then packet inventory
  rejected the intentionally retained symlink in the native negative-input tree.
  Negative inputs under `refusals/` are now excluded from successful evidence;
  their named refusal outcomes remain bound by the hashed native receipt.
- Same-execution development check exposed the parameter defect above; expected
  match/mismatch matrix was not weakened.

## Committed-head checkpoint

Implementation head `78e8f6a4ad5ea8499746d21575df2446786b5a9c`:
- Focused Debug `-warnaserror`: 311 passed, zero failures/skips (parameter, reader,
  Field and DNN). Earlier parameter-only repair gate: 91 passed, ten new cases.
- Python guards: 59 SQL smoke + 20 DNN harness + 13 linked provider + 10 public
  acceptance = 102 passed; no SQL/network in these guards.
- Fresh consolidated native replay passed with source/revision/CLI bytes frozen:
  parameter 8/12/32; reader JSON+DDL 72/20/94; helper 84/30/116;
  Field 78/36/42 (match/mismatch/unknown); 20+24+26 refusal checks passed.
- Eight same-execution pairs (four cases in each language) matched their authored
  parameter/reader statuses. Only Good is M/M; the negative variants remain X/M,
  M/X and U/U. Same fact IDs and distinct digest-specific bindings are preserved.
- Fresh pinned public DNN scan: 6,031 facts, three incidental Tier3 nonquery
  candidates, one inspected operation, selected reader join unavailable; all
  original source/license/inventory checks passed. Four blockers remain explicit.
- Receipt local-only: `/tmp/sql-public-acceptance-78e8f6a/public-acceptance.json`,
  SHA-256 `4527af595f2edecdad13ab0cc390385124dc4ef9b36f1e08636264a7f854aaee`.
- Parent ref rechecked after replay: still exact `6aa8c40e975b9277418aa3bc4b561073887c7f3e`;
  no divergence, no parent merge/rebase or parent output changes.

Pending final integration: full Debug suite, remaining retained SQL smokes,
pinned OSS adapters and coordinated live regression. Do not call this merge-ready
or real-DNN/private acceptance. No PR/merge/review spend/ACK edits; no SQL or
container execution. Results documentation is a subsequent docs-only commit.

## Parent integration and first full attempt

Fetched and verified parent final `035f4890d7e2cc48ff5f2d761592d586df5e694a`:
only five docs/tasks/prompt files differ from the original base. Normal merge
`17b9ea9a1d24cd20a667f998ebc5e7032729236b` preserves both histories. The inherited
DataColumn prompt is optional future work, not the next required acceptance gate.

First merged-head full run failed the Razor version-contract assertion (expected
C# 0.22.20, actual repaired 0.22.21). Updated that sole stale test pin; no expected
behavior or denominator weakened. The owned run was cancelled after verifying its
PID/child-worktree provenance; cancellation exit 1, not a completed full result.
This attempt remains historical failed/cancelled evidence. A fresh committed-head
full run is required after the touched focused gate.

## Final coordinated validation

Frozen implementation `9d486e208f43d307c342add74e04cc9d382e615b` includes
the parent final merge and the exact Razor version-contract repair. The touched
parameter/Razor gate passed 96 tests. The fresh full Debug `-warnaserror` gate
passed **5,412 tests, 3 expected skips, 0 failures**, duration 29m 2s; its TRX is
`/tmp/sql-public-acceptance-full-repaired/public-acceptance-full.trx`.
The earlier failed/cancelled attempt above is not erased or counted as a pass.

Sequential committed-head checks:
- Consolidated parameter/reader/helper/Field replay and eight same-execution
  pairs passed with every original denominator/refusal unchanged. Fresh pinned
  DNN inspection retained the same four blockers and 6,031 facts.
- Other retained smokes passed: DDL parameter 8/12/32; table 44/10/88 (24
  refusals); DataTable 108/24/176 (22); DataSet 156/24/176 (24); internal helper
  72/24/60 (24). Native repeated reports remained byte-identical.
- Pinned C#/VB OSS scans passed at 12,949 / 114,806 facts, respectively, with
  expected reduced coverage. Neither contains SQL calls; this is adapter
  regression evidence, not real SQL application coverage.
- Python 59 + 20 + 13 + 10 = 102 guards passed; private-path and whitespace
  guards passed.
- Existing linked-provider regression passed: 34 cases, 20/6/8, 54 runtime checks.
  Revalidated the established SSH endpoint, native amd64 daemon, resource-limit
  capabilities, pinned local image, free disk and absent owned container before
  execution. No provisioning or matrix expansion. Owned container removed;
  post-run owned-label inventory empty. Static unknowns were not upgraded.

Receipt roots: `/tmp/sql-public-acceptance-9d486e2`,
`/tmp/sql-public-9d486e2-{ddl,table,datatable,dataset,internal,oss}`, and
`/tmp/tracemap-linked-provider-public-9d486e2`. Public packet SHA-256:
`d4aff4e54b6b829431ecec81f4265efa28da6d125a6d44c80fe1b865835e94ad`.
Its fullSuite/liveGate fields remain pending by design: the offline runner does
not verify those separate gates. This record binds their completed external runs
to the same frozen implementation, rather than editing the generated packet.

HEAD/source and CLI/Core/Reporting DLL hashes stayed unchanged through all gates.
The following results commit is docs/tasks only. No DNN runtime, private inputs,
application/migration acceptance, ACK policy edits, review spend or merge.

## PR #920 bounded review repair validation

Frozen implementation `66bde05ee75ca0355d18ba6c0e4cc5c7e1a482bb` includes the
normal dev merge and the owner-authorized evidence-integrity repairs. Full Debug
`-warnaserror`: **5,419 passed, 3 expected skips, zero failures**, 36m 55s;
TRX `/tmp/pr920-full-66bde05/pr920-final.trx`. Python guards: 20 public acceptance,
59 retained SQL, 20 DNN and 13 linked-provider = **112 passed**. Sixteen additional
in-memory mutations of the fresh native receipts (digest, case, fact and report
substitution in each route) were all rejected without modifying the receipts.

The first isolated replay at `e60ed948f` failed while building the CLI (17 missing
SQLite type errors). Canonicalizing the macOS temporary checkout path removed
the `/var` versus `/private/var` alias from the build inputs. The subsequent fresh
run at `66bde05ee` built successfully and passed all gates. The failed attempt is
retained at `/tmp/pr920-public-repaired-20261027.log`, not counted as acceptance.

Fresh isolated replay `/tmp/pr920-public-repaired-20261027b`:
- Parameter 52 cases, 8/12/32; reader 158 cases, 72/20/94 and 20 refusals;
  helper 230 cases, 84/30/116 and 24 refusals; Field 150 cases, 78/36/42 and
  **52 refusals (26 per language)**. Exact case/report/input reconstruction passed.
- Eight same-execution pairs passed. Fresh pinned DNN inspection retained the
  same four explicit blockers; no SQL or upstream application execution.
- Packet SHA-256 `c54604d04e8aabab83aa2b83168679c5cc2515ff885135c94650153599cb9b41`.

After the full suite, original-checkout smokes ran sequentially under
`/tmp/pr920-66bde05-*`: fresh parameter and DDL parameter 8/12/32 each;
table 44/10/88 (24 refusals); DataTable 108/24/176 (22); DataSet 156/24/176 (24);
internal helper 72/24/60 (24). Pinned OSS C#/VB scans passed at 12,949/114,806
facts with reduced coverage and no SQL calls. Original-checkout DLL hashes stayed
unchanged through those smokes. The isolated replay's binaries are a separate
build, with their own recorded hashes, at the same exact source commit; do not
claim byte identity between the two builds.

No new live SQL/container run occurred in this repair-only batch. Earlier live
results remain historical evidence, not a fresh-head runtime claim. DNN/private
acceptance and migration readiness remain unproven. The following results commit
changes only documentation/tasks; no policy edits, extra review spend or merge.

## Independent acceptance-oracle follow-up

Two later exact-head Baz findings identified shared test-oracle data and a mocked
reconstruction boundary. The bounded repair adds `acceptance_matrix.py`, an
independently maintained literal contract transcribed from the authored design
matrices. Native scripts supply source serialization only, not case plans,
expected verdicts, names, report paths or refusal identities.

Four offline integration tests execute real reconstruction for all four routes:
case/report paths, count-preserving verdict swaps with regenerated report hashes,
native-oracle corruption, and source/base/template input-digest changes. The
existing isolated unit tests remain explicitly unit-scoped. Focused Python gates:
24 public + 59 retained SQL + 20 DNN + 13 linked-provider = **116 passed**.
No .NET source or extractor changes; the full 5,419-pass gate at `66bde05ee`
continues to describe the unchanged core, not a newly executed full suite.
Fresh committed-head public replay is pending at this implementation checkpoint.
