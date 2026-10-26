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
outside the static first-result slice. Type coercion, parameter mismatches, VB
execution, modern-provider scanner fixtures, DNN and private acceptance remain gaps.

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

Next: connect this runtime oracle to modern-provider C#/VB scanner fixtures and
an independently authored static-versus-runtime expectation matrix. Runtime
success alone does not prove TraceMap recognizes these source shapes.
