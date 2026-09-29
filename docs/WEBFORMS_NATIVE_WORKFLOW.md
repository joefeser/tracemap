# Native compiled Web Forms workflow

One explicit configuration, one durable review root, one workbench. Native .NET
commands support fresh source-plus-compiled collection and immutable attachment
to an existing source scan. Keep the proven PowerShell workflow and its inputs
until your authorized Windows comparison passes.

This is static, potentially partial, review-only evidence—not SQL execution,
page activation, authenticated compilation, source-line identity, complete
application coverage or release approval.

## Choose fresh or attach

| Operation | Source of truth | Result |
| --- | --- | --- |
| `fresh` | Explicit source root/commit, published inventory and receipts | New source-plus-compiled scan and reports |
| `attach` | Exact retained parent manifest/index/facts and source snapshot, plus explicit published inputs | New compiled-only scan and combined reports; parent facts stay immutable |
| `resume` | Selected run manifest and append-only checkpoint chain | Verified reuse, or a new owned failed/interrupted attempt; no overwrite |

Do not append DLLs/reports to source or modify an old scan. Source, published
site, receipt evidence, retained parent, installed tool and output locations are
explicit. There is no newest-TEMP discovery, automatic publishing, implicit
attestation or automatic limit increase.

## Configure once

Use private plain JSON, not the legacy PowerShell JSONC schema. Comments,
unknown/duplicate properties and trailing commas are rejected. Forward slashes
simplify Windows paths. Replace every example path, commit and inventory entry
with authorized real inputs; do not run this example unchanged.

```json
{
  "schemaVersion": "webforms-compiled-review-config.v1",
  "operation": "fresh",
  "sourceRoot": "C:/source/application",
  "sourceCommitSha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "projectMode": "projectless",
  "solutionRelativePath": null,
  "projectRelativePaths": [],
  "sourceFolders": ["."],
  "pageMode": "selected",
  "pageRelativePaths": ["Pages/Lookup.aspx"],
  "publishedRoot": "C:/publish/application",
  "primaryAssemblies": ["bin/App_Code.dll", "bin/App_Web_generated.dll"],
  "dependencyAssemblies": ["bin/PublicFramework.dll"],
  "bindingReceipts": [],
  "pdbInputs": [],
  "pageMaps": [],
  "parentScanRoot": null,
  "publishReceiptRelativePath": null,
  "receiptRoot": null,
  "publishSourceRelativePaths": ["Pages/Lookup.aspx.vb", "App_Code/PublicData.vb", "Web.config"],
  "preparationProvenance": null,
  "budgets": {}
}
```

`projectMode` is `projectless`, `solution` with one solution-relative path, or
`projects` with a nonempty project-relative path array. `sourceFolders` restricts
fresh collection; `.` selects the source root. Projectless Web Sites retain
syntax/structural evidence rather than invented semantic compilation.

`pageMode: selected` requires `.aspx` paths. `all` uses an empty page list and
considers the declared preparation roster; it does not establish complete site
or historical compiler-input closure. Declare C# `.aspx.cs` or VB `.aspx.vb`
membership as appropriate in `publishSourceRelativePaths`; selected markup is
included automatically.

Native configured paths are literal values, including commas and relative
source-folder names beginning with `--`. Native selected
page lists are literal path lines, not CSV or comment/header rows; legacy
standalone page-list CSV parsing remains unchanged.

DLLs, portable PDBs and page maps are explicit published-relative lists. Do not
invent missing files or generated assembly names. Missing maps may produce
bounded mapless candidates, not inferred complete map coverage.

For `attach`, change `operation` and supply `parentScanRoot`. Its five standard
scan artifacts are required. Complete retained source membership is checked
before and after compiled extraction. Legacy parents require an exact provable
inventory; missing membership is refused, not inferred.

## Start and recover

Commands below assume the approved `tracemap` executable is on PATH. Use the
[local distribution guide](LOCAL_DISTRIBUTION.md) for an isolated .NET tool
installation; a custom tool directory is not added to PATH automatically.
On Windows an explicit installed `C:/tools/tracemap/tracemap.exe` works too.
Keep that pinned installation and its .NET runtime available for resume.

With an already prepared config containing explicit binding/publish receipts:

```text
tracemap webforms-review start --config review.json --out review-monday
```

If receipts need preparation, only an owner who can declare that the selected
primary DLLs came from the exact clean source commit may use:

```text
tracemap webforms-review start --config review.json --out review-monday --attest-exact-source-commit <exact-source-commit>
```

That declaration is not compiler authenticity. Committed source membership,
supported Git normalization, metadata and global map-ambiguity gates are checked.
Unsupported clean filters/encodings do not manufacture commit equality.
Dependencies remain unbound artifact context. Larger declared inventories are
partitioned deterministically without increasing legacy per-receipt limits.

The output must be new and separate from source, publish, receipts, parent and
executing tool. `start` composes authorized preparation, preflight, collection
and reporting; preflight alone is not completed execution. The root contains
`evidence/` when preparation is requested and `run/` with `run-manifest.json`,
append-only checkpoints, owned scan/report attempts and both document sets.

Use the exact printed workbench/handoff paths and checkpointed report attempt,
not a guessed ID. The workbench navigates application documents and grouped
compiled method paths through one root handoff. Internal links are relative;
source/DLL locations remain original evidence, not rewritten identities.

After failure, preserve the folder and inspect its pinned state:

```text
tracemap webforms-review status --run review-monday/run
tracemap webforms-review resume --run review-monday/run
```

Completed reuse rehashes artifacts and checks original tool/runtime and external
inputs without rescanning source or regenerating completed reports. Failed or
cancelled attempts use new IDs; their partial artifacts are not admitted as
completion. Restore missing original inputs; do not substitute newer folders.

## Evidence and budget review

`status --json` is local-only. It verifies retained artifacts and reports scope,
coverage, counts, configured limits, gaps, truncation and next actions. It does
not validate current source/published bytes or authorize resume. Historical
absent measurements remain unknown, not zero.

Terminal checkpoints retain elapsed attempt time and sampled parent working-set
maxima. Memory sampling is a lower bound, not an exact phase peak or quota;
children, missed spikes and transient disk peak remain unmeasured. Retained
artifact bytes are not transient disk use.

New compiled provenance retains independent metadata/IL logical admission
credits and denied aggregate reservation counts, exposed as `admissionWork`.
These are not CPU instructions, runtime calls or total scan work. Credits can
be consumed before an input later fails; per-input caps/preflight failures remain
separate gaps. Page and compiled traversal counters are separate graph queries,
sharing each query's budget across all roots, not total report work.

| Independent budget | Default |
| --- | ---: |
| Non-publish configured input files | 128 |
| Managed assembly bytes per file | 64 MiB |
| Retained artifact bytes per file / total hash bytes | 16 GiB / 64 GiB |
| Metadata / IL credits | 500,000 / 30,000,000 |
| IL body inventory | 50,000 |
| Graph depth / paths / traversal work per query | 20 / 256 / 2,000,000 |
| Report graph facts / edges / input text | 250,000 / 250,000 / 128 MiB |
| Indexed graph scratch storage | 512 MiB |
| Projection input / rendered output | 256 MiB / 512 MiB |

Additive explicit options include `budgets.maxPublishInputFiles` (1–20,480),
`budgets.ilMaxBodies` (1–1,000,000), and
`budgets.reports.maxGraphStorageBytes` (64 KiB–16 GiB). Other report limits are
fields of `budgets.reports`. Streaming hashing has no universal 2-GiB/Int32
ceiling, but individual collectors/text/projection/scratch remain bounded.
An input refusal is not a reason to raise every limit.

The [strict benchmark](validation/webforms-native-scale-2026-09-29.md) uses
32/256 pages, exact 8× source bytes, 24,000/192,000 sparse methods and explicit
stress budgets. It retains 128/1,024 required static paths, with partial and
cycle-truncated coverage. Synthetic measurements do not prove arbitrary graph,
private-site, complete all-pages or runtime support.

## Bounded agent handoff

Keep the root handoff, lossless grouped compiled handoff and evidence index
together. Grouping removes repeated presentation, not variants. Exact identities,
source/index namespaces, rules, tiers, source commit, DLL hashes, collector/input
commitments and gaps remain retained. Compiled paths supplement page chains and
never upgrade their verdicts.

Use the [agent handoff](WEBFORMS_AGENT_HANDOFF.md) and
[evidence-review prompt](../prompts/review-webforms-modernization-evidence.md).
Start with status, then bounded indexed slices instead of whole large handoffs:

```text
tracemap webforms-review query --run review-monday/run
tracemap webforms-review query --run review-monday/run --document compiled --pointer /chains --offset 0 --limit 5 --depth 2
```

Responses are capped at 128 KiB and 2,048 nodes. Omitted children are explicit;
retrieve another slice rather than inferring absence. Evidence review does not
authorize source access, fixes, rescanning, execution or tickets.

Concurrent evidence queries share a read lock; native writers still require an
exclusive lock. `WEBFORMS_EVIDENCE_QUERY_RUN_BUSY_OR_LOCK_UNAVAILABLE` indicates
lock contention or lock I/O failure, not a finding that the evidence index is
corrupt. Retry after the conflicting operation finishes; do not regenerate it.

## Durable completed-run copies and retention planning

```text
tracemap webforms-review relocate --run review-monday/run --out review-archive/run
tracemap webforms-review retention-plan --run review-monday/run
tracemap webforms-review retain-tool --run review-monday/run --out review-monday/tool
```

`relocate` copies verified completed artifacts/checkpoints to a new root,
preserving bytes and policy anchors. External inputs/tool/SDK are not relocated.
Query can use a compatible reader; resume still needs original generator/runtime
and external-input gates.

`retention-plan` emits a private protect-only inventory with **no deletion
candidates**. Unknown folders, old proofs, failed attempts and previous locations
stay protected. This is not machine-wide reference discovery or cleanup approval.

New checkpoints retain original tool/runtime locator declarations. `retain-tool`
copies the pinned distribution selection into a new folder, checks the entry DLL
against the exact run generator and writes a hashed local manifest without
changing the run or executing copied code. Selection is bounded to 512
DLL/executable/native/deps/runtimeconfig files, 256 MiB per file, 2 GiB total
and 4,096 filesystem entries. Keep unrelated data out of tool directories.
External .NET runtime, SDK and adaptors are not packaged/pinned; this is not a
self-contained or portable toolchain.

The copied original tool can be explicitly invoked with its required runtime:

```text
dotnet review-monday/tool/tracemap.dll webforms-review resume --run review-monday/run
```

Historical missing tool locators stay unknown. Missing/changed originals,
runtime-version mismatch, overlap and existing destinations are refused. Failed
staging is retained. Tool copies are not automatically included in run relocation;
keep their folders/manifests separately. Nothing authorizes deleting originals.

## Private Windows acceptance gate

The [completion audit](validation/webforms-native-completion-audit-2026-09-29.md)
maps the native requirements to their public evidence and final operator checks.

Keep prior PowerShell outputs and their exact source/DLL/receipt/parent inputs.
Run fresh and attach only against authorized inputs. Compare both document sets
and lossless handoffs at full identity, rule/tier, provenance and coverage scope;
matching total counts alone is not parity. Verify failed/resumed and completed
reuse preserve inputs and parent bytes. Record tool head and collector hashes.

Use the single `start --config ... --out ...` command above with prepared
receipts. If source-to-DLL declaration is unavailable, stop and ask the owner;
do not attest merely to make a test run. Do not upload private paths, DLLs,
source, handoffs or private-input fingerprints. Return safe counts/status/gap
kinds only. Private acceptance is required before retiring the old workflow or
removing dependencies.

Historical detail and failed experiments are preserved in the
[implementation notes](history/WEBFORMS_NATIVE_IMPLEMENTATION_NOTES_2026-09-29.md).
