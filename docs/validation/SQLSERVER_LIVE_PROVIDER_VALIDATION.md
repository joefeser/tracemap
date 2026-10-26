# Disposable SQL Server provider validation

This opt-in runtime oracle supplements, not replaces, static SQL evidence tests.
It never runs as part of ordinary unit tests. No work-machine data, private schema,
DNN application, existing database, published port or persistent volume is used.

## Authored expectations

The .NET 10 client in `samples/sqlserver-live-provider` uses Microsoft.Data.SqlClient
6.1.7. Its transitive PKCS dependency is pinned to 10.0.14 because the older floor
failed NuGet audit. Warnings and vulnerability checks remain enabled.

The 19 behavioral assertions plus one denominator assertion cover:

- Reader row, name/ordinal access, case fallback, missing name, invalid ordinal,
  and the single-result boundary.
- Renamed projections, DataTable case fallback, and refusal of the old name.
- DataTable Fill, DataSet default table identity, named selection and source name.
- First/second result sets, result count, separate OUTPUT and RETURN values,
  DataSet multi-result naming and duplicate-column renaming.

Runtime case fallback does not upgrade static unknowns. Multiple results remain
outside the static first-result slice. Type coercion, parameter mismatches,
DNN and private acceptance remain gaps. The linked extension below adds bounded
modern-provider C#/VB extraction and execution coverage.

## Run

Use the exact approved native Linux x86-64 Docker context/endpoint and the pinned
image preflight in `DNN_SQL_VALIDATION.md`. Host, resource, EULA and disposable
approvals remain required. The harness never pulls automatically.

```sh
dotnet publish samples/sqlserver-live-provider/ProviderChecks.csproj \
  -c Release -r linux-x64 --self-contained true -warnaserror \
  -o /tmp/tracemap-provider-publish-new
python3 scripts/dnn-sql-validation/harness.py --activate \
  --context "$REVIEWED_CONTEXT" --expected-endpoint "$REVIEWED_ENDPOINT" \
  --approve-host --approve-resources --accept-eula --approve-disposable \
  --generate-password --provider-directory /tmp/tracemap-provider-publish-new \
  --output /tmp/tracemap-provider-run-new
```

The generated password exists only in process/container environments; Docker
administrators can inspect them. It is never printed, committed or put in receipts.
The client is copied into the owned server container and runs as its non-root user
against fixed loopback and `TraceMapSynthetic908`, with no external network.
No client image, source mount, open port or persistent database is needed.

The publish directory must contain reviewed code, not untrusted input. The harness
rejects links, incomplete and oversized payloads; hashes binaries before/after
execution; and binds them with SQL inputs in its receipt. This is not a general
executable sandbox or signature verifier. The receipt binds generator, image,
SQL bytes, published client bytes and output hashes. Only the matching owned
container is removed; the pinned image stays cached. Never prune unrelated resources.

## Development run — 2026-10-26

Approved remote host: native Linux x86-64, 8 CPUs, ~31 GiB memory, 361 GiB Docker
disk free. Container limits: 2 CPUs, 6 GiB memory/swap, SQL memory 2 GiB, 2 GiB
ephemeral tmpfs, 512 PIDs, network none and no ports.

- SQL Server ProductVersion **16.0.4205.1**, existing digest-pinned image.
- Self-contained linux-x64 Release publish with `-warnaserror`: passed.
- SQL setup, metadata-description and synthetic execution checks: passed.
- Microsoft.Data.SqlClient runtime oracle: **20/20 passed**.
- Cleanup: **owned-container-removed**.
- Python harness guards: **19 passed**.

This was an in-progress implementation run, not a frozen committed-head gate.
Local receipt: `/tmp/tracemap-sql-live-provider-20261026/receipt.json`.
No production scanner code changed; the full TraceMap .NET suite was not rerun.

Initial activation exposed a file-capability conflict with drop-all, hidden by
missing stderr in captured logs. Explicit loopback also corrected localhost
connection failures under network-none. Both defects are now guarded. Failed
attempts were cleaned up; an obsolete retry was interrupted and cleanup verified.

Original next step: connect this runtime oracle to modern-provider C#/VB scanner fixtures and
an independently authored static-versus-runtime expectation matrix. Runtime
success alone does not prove TraceMap recognizes these source shapes.

## Committed-head repeat

Repeated successfully at implementation commit `3970f8fa5` with unchanged source
and published client bytes: SQL setup/metadata/execution passed, **20/20 provider
checks passed**, version **16.0.4205.1**, owned container removed, and no remaining
harness containers. Receipt:
`/tmp/tracemap-sql-live-provider-committed-20261026/receipt.json`.
All **32 SQL + 19 harness Python guards** passed. This documentation-only readback
follows the validated implementation. No full scanner suite or private acceptance
is claimed. The branch is based on the unmerged DataSet implementation and must
not be presented as an independent dev-based patch until that dependency lands.

## Linked static/runtime matrix

`samples/sqlserver-live-provider/matrix.md` defines six cases per language before
execution. The runtime client references the same C# and VB fixture projects that
TraceMap scans. Their modern-provider command/reader/adapter methods therefore
have both static evidence and runtime observations, not merely similar snippets.

The expected combined static denominator is **6 bounded matches, 2 mismatches,
4 unknowns**. Missing columns throw at runtime. Case-only names and later result
tables succeed at runtime but must remain unknown in the bounded static contract.
The first-result declaration is independently authored public JSON; this does not
admit the two-statement procedure through the restricted DDL route.

```sh
dotnet build src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj -c Debug -warnaserror
python3 scripts/dnn-sql-validation/linked_provider.py --activate \
  --context "$REVIEWED_CONTEXT" --expected-endpoint "$REVIEWED_ENDPOINT" \
  --approve-host --approve-resources --accept-eula --approve-disposable \
  --output /tmp/tracemap-linked-provider-new
```

The runner publishes the referenced fixture assemblies, scans both projects,
compares every site twice, and then invokes the isolated SQL harness. It refuses
missing/extra sites, unexpected static verdicts, nondeterministic comparisons,
missing runtime counterparts, byte drift and incomplete cleanup. Its local linked
receipt binds generator, source matrix, CLI DLLs, scan/report bytes and the runtime
receipt. The runtime receipt separately binds the published client bytes.

Only a clean committed worktree is admitted; every bounded input is compared with
its exact Git blob before and after execution. Source file, directory and dangling
links are rejected before filtering. Output must be outside the repository tree,
including through symlink aliases. The harness runs in-process with its own
bounded Docker operations so an outer timeout cannot kill ownership-checked cleanup.
Each linked row carries the native comparison provenance and report limitations;
totals are derived from those validated rows.

Failed output directories are intentionally preserved, not atomically discarded:
they may contain the only ownership/cleanup receipt and diagnostic logs after a
remote failure. A failed run has no successful linked receipt. Retry with a fresh
output path; do not delete failed evidence before inspecting cleanup status. This
matches the underlying harness's exclusive-output/evidence-preservation contract.

There are **32 runtime assertions**: the original 20 plus 12 linked observations.
This is a narrow synthetic matrix, not modern-provider parity across all scanner
shapes, DNN acceptance, work-machine acceptance or application readiness. Native
scan/report artifacts are local evidence, not privacy-projected public exports.

### Linked committed-head validation — 2026-10-26

At `88230f187`, after rebuilding the CLI with `-warnaserror`:

- C# and VB scans built successfully; exactly six semantic execution sites each.
- All 12 static verdicts matched: **6 bounded matches / 2 mismatches / 4 unknowns**.
- Repeated JSON and Markdown comparisons were byte-identical.
- All **32 runtime assertions passed** on the pinned SQL Server container.
- Source and CLI bytes remained unchanged; owned-container cleanup completed.
- **56 focused Python guards passed**, including runtime counterpart/hash/cleanup
  refusals. Private-path and whitespace guards passed.

Local linked receipt:
`/tmp/tracemap-linked-provider-committed-20261026/linked-receipt.json`.
The preceding development run also passed. An overly broad unittest discovery
invocation additionally selected an older command-text integration suite without
its required artifact arguments; those 32 setup errors were an invocation error,
not a passing gate. The explicit focused invocation above supersedes that attempt
for this slice. No production scanner code changed and no full .NET test-suite
claim is made.

## Phase 4 helper extension (authored before execution)

`matrix.md` now adds five helper cases per language over two private static
helpers, `ReadTable(connection, procedure)` and `ReadSet(connection, procedure)`,
in the same Microsoft.Data.SqlClient fixture pair that the runtime client
references: HelperTableName and HelperOtherProcedure (the same helper with
`dbo.ReadItems` and `dbo.ReadOther`), HelperSetName, HelperMissingName and
HelperLaterTable. Each helper is one Fill fact; each call site is bound
separately through `sqlserver-helper-result-bindings/1` and compared by the
`sqlserver-helper-result` route. Runtime setup additionally creates
`dbo.ReadOther AS SELECT N'other' AS Title`.

The combined linked denominator is **22 cases = 12 bounded matches / 4 mismatches /
6 unknowns**, and the runtime oracle asserts **42** checks (the original 20 plus 22
linked method observations; `harness.PROVIDER_CHECKS`). HelperLaterTable returns
99 at runtime but must stay a static unknown with `dataset-later-table-unsupported`;
static rows are fixed before the runtime receipt is joined and runtime success
never changes a status. Pinned image, network-none isolation, Developer/EULA
settings, transient credentials, resource limits, ownership-checked cleanup and
failed-run evidence preservation are unchanged.

### Phase 4 linked committed-head validation — 2026-10-26

At implementation head `d39acb8cd` with the CLI built by the frozen full suite, after a
read-only revalidation of the reviewed context (endpoint `ssh://dockercache`, native
linux/x86_64, 8 CPUs, ~30.6 GiB, enforceable memory/swap/CFS/PID limits, 359 GiB free on the
Docker data filesystem, pinned image present by RepoDigest, no pre-existing harness
containers):

- C# and VB scans built; eight semantic execution sites each (six direct, two helper Fills).
- All 22 static verdicts matched: **12 bounded matches / 4 mismatches / 6 unknowns**;
  HelperLaterTable stayed unknown with `dataset-later-table-unsupported`.
- Repeated JSON and Markdown comparisons were byte-identical.
- **42/42 runtime assertions passed** on SQL Server 16.0.4205.1 (2 CPUs, 6 GiB memory/swap,
  SQL 2 GiB, 2 GiB tmpfs, network none, no ports); owned container removed; source and CLI bytes
  unchanged; no credential text in either receipt; 106 s.
- 20 harness + 11 linked-runner Python guards passed.

Local linked receipt: `/tmp/tracemap-linked-provider-hr-d39acb8/linked-receipt.json`
(SHA-256 `ac750b24d02616d515e92b8edf23568d7b48487ca2b348f691046618b976e8c3`). This remains a
narrow synthetic matrix, not modern-provider parity, DNN, private-application or deployment
acceptance.
