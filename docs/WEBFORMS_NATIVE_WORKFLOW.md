# Native compiled Web Forms workflow

Status: native preflight, input validation, checkpointed fresh scanning and
immutable compiled-only attachment. Explicit cross-index VB PDB/publish reporting
joins, checkpointed private report orchestration and bounded evidence retrieval
and deterministic receipt partition preparation/pinning are implemented for
declared selected/all-page scopes. Strict fresh synthetic 32/256-page scale
passes with explicitly declared budgets; complete compiled-site coverage,
transient resource instrumentation and real private Windows parity remain pending.

The Core compiled-only producer now has a distinct local attachment context
containing the exact generator, bounded input, original parent manifest/index
hashes and retained source snapshot. It reuses the existing metadata/IL/PDB/
publish policies without source extractors, builds, file discovery or source
fact copies. Retained source bytes are checked before and after extraction.
Parent artifact hashes require independent caller validation; they are not
authenticated signatures. Source analysis/build status stays not run and the
derived scan remains reduced and review-only. Native `attach` now validates the
original parent manifest/index/facts and retained source bytes, then writes a
separate compiled-only scan in a new owned attempt. Context validation checks
local integrity, not authenticity. Validated cross-index report joins are now
consumed by the reporting graph through the explicit combine contract described
below. Native report orchestration produces a private workbench and grouped
lossless handoff; retain the proven wrappers until final workflow parity passes.

## One-command native start

Keep the source and existing compiled website in their original locations. Put
their exact paths, commit, selected/all-page scope and explicit budgets in the
private config once. Then start a new explicitly named durable review folder:

```text
tracemap webforms-review start --config review.json --out review-monday --attest-exact-source-commit <exact-40-character-source-commit>
```

Only supply that attestation if you can declare that the selected primary DLLs
came from that exact clean source commit. `start` does not infer it from the
config, build or copy the compiled site, find the newest TEMP folder, or increase
budgets. It composes the existing preparation, preflight and execution gates.
With an already prepared config containing explicit binding/publish receipts,
omit the attestation and use `start --config prepared.json --out review-monday`.
Missing receipts without explicit attestation are rejected before output creation.

The new root contains `evidence/` when preparation was requested and `run/` with
the pinned manifest, append-only attempts/checkpoints and both document sets.
The command prints the exact workbench, handoff and pinned run paths. Existing
roots, source/publish/parent/receipt overlap and runtime overlap are refused.
Failed owned outputs remain intact. Once `run/run-manifest.json` exists, recover
with `tracemap webforms-review resume --run review-monday/run`; do not repeat
preparation or select a replacement folder by timestamp. The advanced three-step
commands below remain supported. No cleanup or wrapper removal is automatic.

### Bounded publish receipt sets (Core contract)

The scanner's existing `--webforms-publish-receipt` input also accepts a private
`webforms-publish-binding-set.v1` container. Each ordered partition reference
has a safe receipt-relative path and exact SHA-256. The container records
`legacy.webforms.publish-map.v1`, local-only visibility, the operator-declared
review-only claim, exact receipt generator SHA-256, source commit and bounded
input digest. That digest uses UTF-8 framing of the schema, generator, commit
and ordinally sorted `path:sha256` partition rows, each terminated by a newline.
These are byte commitments, not authenticated attestations.

A set has at most 64 non-nested receipts of at most 1 MiB each. Each partition
retains the existing 256-source-file, 64-published-file and 32-page limits;
partitioning does not silently raise them. Conservative repeated artifact
read/hash admission is capped at 8 GiB and retained map virtual paths at 4,096
characters for the set, separately from graph and
rendering budgets. Source and published bytes are rechecked before admission.

The whole set is admitted or withheld. Repeated exact source/assembly membership
is retained once; conflicting hashes/kinds, case-aliased paths, repeated page
identities, invalid partitions and cross-partition map ambiguity are gaps. Every
mapless page must have no matching map in the union of declared maps, and every
mapped page must have exactly one global match. No ambient files are discovered.
An admitted set still covers only its declared inventory, not every page of an
application or its historical build closure.

Explicit `webforms-publish-inventory-partition.v1` members may retain zero pages
to carry additional shared source or DLL/map inventories. They keep the same
nonempty source/published inventories and byte/count limits, cannot contain page
bindings and are accepted only inside a set. A set still requires at least one
normal page-bearing receipt. This separates inventory chunking from page identity
without duplicating pages or upgrading inventory bytes to page/build evidence.

Native `prepare` produces these sets when a declared inventory exceeds a legacy
receipt cap. Preflight pins the root and every exact partition; changed/missing
chunks prevent resume. Never substitute new chunks into a pinned run. This
supports declared page scopes, not full-site or Windows acceptance.

## Native operator-declared receipt preparation

For an authorized existing publish whose primary DLLs you can attest came from
the exact clean source commit, the native preparation command creates receipts
in a **new evidence folder**, without scanning source or copying published bytes:

```text
tracemap webforms-review prepare --config <private-json> --out <new-evidence-root> --attest-exact-source-commit <exact-40-character-commit>
```

This is an explicit operator declaration, not an automatic attestation or proof
of authentic compilation. Do not supply the flag unless you can make that
declaration. Missing/wrong attestation, dirty source, ignored/uncommitted selected
files, absent repository origin, ambiguous maps, unadmitted/duplicate compiled
bytes or effective input/partition limits reject publication of the new evidence folder.
There is no prompt, TEMP search, rebuild, binary copy or existing-folder overwrite.

Use a config without existing receipts or preparation provenance. Declare the
additional source membership through `publishSourceRelativePaths`, for example
`["Pages/Lookup.aspx.vb", "App_Code/PublicData.vb", "Web.config"]`.
Selected page markup is included automatically. These are bounded membership
inputs, **not** the historical compiler's complete source/config/dependency
closure. Assembly/dependency/map/PDB declarations remain explicit. The command
checks committed membership and clean scoped Git state, with optional Git index
writes disabled. Each declared source is also compared with its committed Git
blob, so assume-unchanged flags cannot hide source edits. Exact bytes or supported
Git built-in CRLF normalization are recorded per source; normalized comparisons
pin their attribute/config policy and retain a normalization gap. Custom clean
filters and working-tree encodings are not used to manufacture a commit match;
unsupported transforms fail. Raw source SHA-256 values remain unchanged by this
comparison. Primary DLL bindings record your attestation; dependency DLLs
remain unbound artifact context. Native Core metadata and publish-receipt policy
inspect the generated receipts before admission.

The new evidence root contains:

```text
preparation-manifest.local.json     input hashes, inspected policy outcomes, gaps and artifact hashes
compiled-binding.local.json         only explicitly attested primary DLLs
publish-receipt.local.json          declared source/DLL/map/page membership
publish-partitions/*.local.json    bounded page or inventory-only chunks, when needed
review-config.local.json            config for preflight/run with this separate receipt root
```

Each derived JSON records exact generator and bounded-input hashes, directly or
through the config's `preparationProvenance`. Single receipts and individual
partitions keep the established source-roster `boundedInputSha256`; a set root
uses the ordered partition commitment described above. `receiptInputSha256` additionally
pins the complete preparation input, metadata inspection and attestation. These
are local integrity/provenance commitments, not authenticated signatures.
Compiler provenance remains unavailable; the legacy `compilerSha256` field holds
the documented unavailable-marker digest, **not a compiler binary hash**.
Failed preparation can retain an unadmitted owned `.webforms-preparation-*`
staging folder; it is not a completed evidence root and is not discovered for reuse.

Exact or uniquely prefixed declared maps are accepted; multiple matching maps
fail. A page with no matching **declared** map can retain the established mapless
source-type candidate if a declared `App_Web_` assembly exists. Physical map
completeness is not inferred. `all` mode covers only the `.aspx` members explicitly
declared in the preparation roster, with an all-pages completeness gap. The
legacy per-receipt caps remain 256 source files, 64 published files and 32 pages;
larger declared inventories are partitioned deterministically into at most 64
chunks. Shared membership is retained without duplicating pages, using explicit
inventory-only chunks when necessary. Core independently validates the complete
set and its global counts before publication.

Existing configs retain their original `maxInputFiles` admission budget. To opt
in to a larger declared page/source/map/partition inventory, set the additive
`budgets.maxPublishInputFiles`, for example `2048` (allowed range 1–20,480).
This separate role budget does not increase compiled-assembly, PDB, project or
binding-receipt admission. Hash-byte, JSON, Core set, metadata/IL, graph and
rendering limits remain independent. Preparation checks that the follow-on run
can also fit its declared input-count and hash-byte budgets, including actual
new receipt/config bytes; it never silently raises a limit.
Git output is bounded to 4,194,304 characters per stream and 15 seconds per check.

Then preflight the generated `review-config.local.json` into a separate new run
folder and use native `run/resume` below. Receipt generation itself does not run
the scanner or finish representative scale/private compiled-site parity. Native
execution generates the checkpointed workbench and handoffs. Keep the proven wrappers.

The proven PowerShell proof/report workflow remains supported. Preflight alone
does not execute scans or replace that workflow:

```text
tracemap webforms-review preflight --config <private-json> --out <new-durable-run-root>
```

The new output must not exist and must be outside source, publish and retained
parent scan roots (and must not contain those roots). It contains only
`run-manifest.json` and `README.md`. There is no timestamp/TEMP search. The
manifest owns this run ID, explicit input hashes, effective budgets, gaps and
pending phase states. Its successful creation is **not a successful scan**.
Retain the external dependencies. Completed native runs support explicit verified
copying and a protect-only retention plan below; cleanup is never automatic.

## Native operational status

```text
tracemap webforms-review status --run <durable-run-root>
tracemap webforms-review status --run <durable-run-root> --json
```

The default view is a short operator summary with fresh/attach operation,
selected/all-page scope, retained phase state, verified
output status, coverage, fact/path/group counts, missing declared input locators,
the workbench path and a next action. It does not search TEMP or rescan. The
local-only JSON adds the retained source commit, generator/input commitments, per-phase observed counts,
configured limits, categorical compiled-gap counts and explicit usage gaps.
Truncation reasons are aggregated separately as cycle/depth/frontier/work/path/
selector categories; other retained reasons remain an explicit category, not
free-form messages. This prevents cycle protection from looking like exhausted
work capacity and does not recommend raising limits automatically.
Unknown work consumption is `null`, never zero or unused capacity. New path
summaries retain the actual search work counter. In native reporting, each page
or compiled graph query shares its path/work limit across all selected roots;
each query's counter is not total report-phase work. Historical summaries
without the field remain unknown. Status identifies this counter's scope as
`page-and-compiled-graph-query-traversal-all-selected-roots` when both counters
are retained. This sum can reach twice the per-query cap; graph-admission work
remains a separate gap. Aggregate artifact bytes are not the renderer's byte
counter. Do not infer elapsed/peak/transient usage from retained file counts.

Completed scan/report artifacts are rehashed before status is reported; compact
report counts come from bounded reads of the checkpointed evidence index, not
whole handoffs. Preflight and failed/interrupted attempts remain distinct from
admitted completion. A present input locator does not establish unchanged bytes,
source snapshot equality, original runtime availability or permission to resume.
The command checks only explicitly pinned file locators, not all current source
or complete snapshot-roster membership. Missing locators direct the operator to
restore original inputs, not select newer folders. Resume keeps its original
tool/runtime and authoritative input gates. A busy run is refused rather than
showing a falsely stable completion state.

Status is a point-in-time private observation, not fresh-source, build, runtime
SQL or full-site acceptance. JSON is capped at 128 KiB and refuses corrupt
checkpoints/artifacts or an unavailable index without a completion output.
Categorical gap kinds and checkpoint gaps have explicit omission counts when
their status projection is bounded; the original handoffs remain lossless.

## Durable completed-run copies and retention planning

Use the explicit native run root, not the newest TEMP folder:

```text
tracemap webforms-review relocate --run <completed-run-root> --out <new-durable-run-root>
tracemap webforms-review retention-plan --run <completed-run-root>
```

`relocate` is a **copy**, not a move or purge. It requires completed native reports,
locks the selected run, verifies every admitted scan/report artifact, and copies
their original bytes together with the manifest, checkpoint chain and optional
README. A new local-only `run-location.local.json` records the exact current
generator, bounded copy roster, original policy root, destination, checkpoint and
artifact commitments. Existing checkpoints, handoffs and evidence identities are
not rewritten. Repeated explicit copies preserve the original policy root and a
previous-location hash. A hand-copied/moved folder with stale or absent location
metadata is not admitted at its new path.

All owned artifact reads resolve beneath the current run root. The original root
is used only to reconstruct existing policy digests, not to redirect file reads;
it need not remain available for a bounded read-only `query`. Embedded private
paths and source/DLL locators remain original evidence, not freshly rewritten
source-line identities. Query still verifies only its admitted evidence index;
relocation and retention planning verify the complete admitted artifact roster.

Source checkout, published DLLs, receipt roots, retained parent scans and original
tool/SDK dependencies are **not copied or revalidated** by relocation. Completed
`resume` retains its stricter original-generator/runtime and external-input gates;
copying does not make an old run executable with a new tool distribution. A
compatible read-only query may use a newer reader without claiming fresh scan
or runtime validation. Preserve the tool installation and proof wrappers.

The destination must be new and separate from the selected run, declared inputs,
previous proof locations and current runtime. Failed/cancelled staging folders
remain inspectable as unadmitted `.pending-*` copies. Unknown files and abandoned
attempts are not copied, and the original run is never removed. Publication uses
a same-filesystem sibling-directory rename after hash verification; it is not
an authenticated backup or a guarantee against hostile concurrent filesystem
replacement.

If interrupted after directory publication, the destination may already contain
a valid completed copy despite a missing success message. Inspect that explicit
destination with `query` or `retention-plan`; do not overwrite it or delete the
original to retry.

`retention-plan` returns JSON on stdout with generator/input commitments, verified
retained-file hashes and protected dependency paths. It is local-only and may
contain private paths/fingerprints. Its mode is `dry-run-protect-only` and its
deletion candidate list is always empty. The emitted plan is capped at 16 MiB;
oversized output is refused rather than silently omitting dependencies. Unknown
folders, old proof runs and unadmitted attempts remain protected. This is a bounded per-run inventory, **not
a machine-wide dependency graph or deletion approval**. Legacy PowerShell proof
folders do not have native checkpoints and are not eligible for these commands.

## Native fresh scan, immutable attachment and resume

After preflight, a `fresh` configuration can execute the normal scanner once,
including explicitly declared managed metadata, receipts, portable PDB and IL
evidence. It does not rebuild or publish the site or create operator attestations:

```text
tracemap webforms-review run --run <durable-run-root>
tracemap webforms-review resume --run <durable-run-root>
```

The same commands execute an `attach` configuration with an explicit
`parentScanRoot`. Attachment never runs the source scanner: it checks current
bytes over the parent's retained snapshot membership before and after compiled
extraction, then writes the five standard scan artifacts to a separate owned
scan directory. The parent, source and published inputs remain read-only. The
derived manifest and Markdown report retain exact parent manifest/index hashes,
source snapshot, Core generator and bounded-input context. Parent source facts
are neither copied nor relabeled. Source analysis and builds remain not run;
the new scan is reduced, local-only and review-only.

The run owns these paths:

```text
run-manifest.json                 immutable preflight/input contract
checkpoints/0001.json              scan-started checkpoint
checkpoints/0002.json              scan-completed/failed/cancelled checkpoint
checkpoints/0003.json              reports-started checkpoint (successful first scan)
checkpoints/0004.json              reports-completed/failed/cancelled checkpoint
attempts/<owned-id>/scan/          five standard scan artifacts; fresh runs also retain snapshot pair
reports/<owned-id>/index.html      single private entry page
reports/<owned-id>/handoff.local.json  full retained page packet, input/manifest provenance and compiled link
reports/<owned-id>/compiled/       grouped method-chain HTML and lossless indexed JSON
reports/<owned-id>/combined.sqlite internal exact combined evidence, not another source scan
reports/<owned-id>/review-evidence.sqlite lossless indexed JSON tokens for bounded read-only retrieval
.native-run.lock                   exclusive process lock, not a discovery hint
```

Checkpoints are append-only, contiguous and hash chained. Every checkpoint pins
the exact CLI generator, preflight bytes and bounded execution input; its separate
canonical payload hash covers status, gaps and derived claims. Completed scans
pin every output file, retain the scanner's actual source snapshot, and pass the
same manifest/index/NDJSON validation used for retained parents. There is no
successful checkpoint for missing, changed, oversized or inconsistent output.
Checkpoint SHA values are local integrity checks, not authenticated signatures.

Native fresh runs explicitly retain `source-snapshot.local.ndjson` and
`source-snapshot-manifest.local.json`. The roster is the scanner's authoritative
snapshot membership, including semantic metadata absent from `FileInventoried`
facts. It contains relative paths, kinds and sizes, never source snippets. The
roster's first line records its own exact CLI generator and the SHA-256 of the
scanner's bounded framed source input. Its small companion manifest pins the
exact CLI/Core generators, original scan-manifest bytes,
snapshot digest, roster hash/count/bytes and declared limits with a bounded-input
SHA-256. The roster is streamed on writing and reading; it is not a giant JSON
array. Both artifacts are private local evidence, not shareable or signed proof.

Normal `scan` output is unchanged unless `--retain-source-snapshot` is requested.
Its explicit retention caps default to 1,000,000 files, 64 GiB raw source bytes
and 64 MiB roster bytes. Native execution supplies its configured parent-fact,
hash-byte and retained-artifact bounds instead. Retention does not bound overall
Roslyn/scanner memory or prove eight-times scale acceptance. Configured limit values
are validated before scanning; actual roster/source usage is admitted during
retention, after scanning. Admission failures retain no completed native checkpoint.
Rosters admit lines up to 32,768 characters, including escaped relative names.
Execution also pins distribution DLL/native/dependency/runtime-config bytes and
the .NET runtime version. Changes reject resume, even if the CLI DLL is unchanged.
This bounded distribution inventory admits 512 code/config files (256 MiB each,
2 GiB total) and at most 4,096 filesystem entries, with no symlink traversal.
External SDK/toolchain bytes are not pinned; that remains an explicit gap, not a
claim of reproducible or authentic compilation. Runs must stay outside the tool
distribution directory.

`run` refuses an already-started run; use `resume`. Failed/cancelled/interrupted
attempts remain on disk and a retry uses a new owned ID. A completed resume checks
input/tool/output hashes and does **not** rerun source scanning. It reports
`sourceRescanned=false`: current unpinned source-file edits
do not turn a retained snapshot into current-source validation. A changed config,
selected page, declared DLL/receipt/PDB/map or tool rejects reuse; make a new
preflight run for new inputs. A changed Git identity also rejects execution.
Completed attachment resume additionally revalidates the original parent and
current bytes over its retained snapshot membership, plus derived attachment
context. It does not rerun compiled extraction. Changed retained source bytes,
parent sidecars or artifact bytes, extra outputs, or changed context reject reuse.
This is retained-membership validation, not discovery of newly added files.

Source folders restrict direct file inventory. Projectless mode excludes project and
solution files; explicit solution/project modes pass their selected paths to the
existing scanner and explicitly admit those selection files even outside the
source folders. Literal source folders containing `*` are refused rather than
interpreted as expanded globs. Selected/all-page mode remains the future report-selection
contract, not a claim that all-page compiled path integration is complete.
Explicit project/solution scans retain the existing compiler-membership behavior;
their additional semantic inputs participate in the scanner's source snapshot.
These modes have argument-admission coverage here, not full compiled-site parity.

The scan first records `scan-completed-reports-pending`. Operator `run/resume`
then admits an explicit combined index and renders the existing page packet plus
separate compiled method paths. Success records `reports-completed-review-only`
and prints `webFormsWorkbench` and `webFormsHandoff` paths. The immutable manifest
and contiguous journal are the source of truth; no newest-folder discovery occurs.
Report failure/cancellation keeps the scan checkpoint and partial report bytes.
Resume allocates a fresh report ID without scanning or overwriting old evidence.
Completed resume verifies scan and report hashes without regenerating reports.
Generation-complete is not complete coverage, runtime proof or full-site acceptance.

The main handoff embeds the full retained page packet, original rules, tiers,
verdicts and gaps, input inventory and scan manifests. Its compiled link is pinned
by exact bytes. The grouped JSON retains all original variants and full identities;
compact HTML names are display-only. Page-chain verdicts are never upgraded by
the compiled supplement. Raw page maps are not promoted to a receipt automatically.

An optional `budgets.reports` object configures graph admission (`maxInputFacts`,
`maxInputEdges`, `maxInputTextBytes`, optional `maxGraphStorageBytes`), packet limits (`maxSurfaces`, `maxEventChains`,
`maxGaps`), handler/frontier limits (`maxCompiledRoots`, `maxFrontier`), and projection
limits (`maxProjectionInputBytes`, `maxOutputBytes`, `maxProjectionRecords`,
`maxProjectionReferences`). Defaults are 250,000 facts/edges, 128 MiB graph text,
1,000 surfaces/chains/roots, 10,000 gaps/frontier, 256 MiB canonical projection
input, 512 MiB aggregate rendered output, 500,000 records and 2,000,000 references.
The internal combined SQLite uses retained-artifact/hash bounds, not the rendered
output cap. Receipt partitioning is implemented for declared inventory; complete
compiled-site coverage and representative eight-times graph memory remain separate
gates. Bounded combined reports use the indexed scratch graph described below;
type-specific semantic ambiguity indexes still use managed memory. Admitted fact
rows use the private indexed storage described below, not a complete managed list.

The bounded combined reader now projects the same audited graph-consumed
properties used by the single-index reader in SQLite, before allocating .NET
strings. It retains **every combined fact row**, all source/overload/dispatch
competitors and attachment links. Unknown fact types, legacy evidence and
declared surfaces retain their full properties. Source symbol IDs and VB
member/body-span/namespace fields needed by compiled bridges are preserved.
Duplicate keys and non-flat/non-string property shapes stay on the full parser
path, preserving each reader's last-key and malformed-input behavior rather than inventing
identity fields from SQLite's different JSON lookup semantics.
Original fact JSON remains unchanged in the retained index; the projection is
an internal read optimization, not a replacement handoff or evidence deletion.
Fact/edge text is admitted before allocation. Raw combined admission ceilings
are not raised.

Bounded combined admitted facts, graph nodes, edges and outgoing adjacency reside
in a private temporary SQLite database, with an 8 MiB page cache and a default
512 MiB logical database ceiling. Native configuration can explicitly declare
`budgets.reports.maxGraphStorageBytes` between 64 KiB and 16 GiB. Omission preserves
the historical default and configuration serialization; an explicit value is
hash-bound, displayed by status, and applied to both packet and compiled-path
graphs. It does not increase input, traversal work, path or output limits, or
guarantee that the machine has sufficient disk or memory.
This is not an emitted run artifact or a reusable checkpoint:
SQLite owns the temporary file and disposes it with the connection. The header
pins the exact Reporting DLL SHA-256 and full input-index SHA-256; input bytes are
streamed again before a report can be returned. No input index is written.
Source/overload/dispatch competitors are not pruned. Symbol reconciliation uses
an indexed alias roster and loads one global alias group at a time; traversal
reads outgoing edges through an indexed list, using .NET ordinal ordering even
for Unicode. A private order roster pins global and per-node positions. Random
access loads at most 64 records with a 512 KiB serialized page target; one
already-admitted larger record is retained alone. Reverse traversal uses this
same list without materializing a reversed whole-node array.
The private v2 store losslessly frames each raw UTF-8 JSON payload with its byte
length. Decode requires the exact declared length; invalid modes,
oversize declarations, truncated or trailing bytes are rejected. The 512 KiB
outgoing page target still measures JSON bytes, excluding the framing header.
IDs, alias keys, ordering columns and every admitted row remain unchanged.
The experimental Brotli encoding is not active: it slowed the smaller stress
case and still failed to admit the larger graph. Framing validates row bounds;
it is not a compression or representative-scale claim.
Fact admission streams each compact projected row directly into this database.
Published-page bridge joins build each complete typed inventory once and use
source/file lookups rather than re-decoding the entire scratch fact table per
page and handler. The qualified method index keeps all bound name/assembly/type
competitors and opaque fallback identities; the original receipt, type, span,
name and boundness predicates still gate each edge. Mapless type counts retain
unbound competitors, and duplicate page/binding/declaration rows remain ambiguous.
These type-specific inventories still allocate managed memory; measured larger
workflow throughput and peak-memory acceptance remain required.
Legacy terminal fan-out classification uses a global incoming-edge index and
reads at most five index references per threshold check, without decoding the
entire edge inventory for each retained path. Every admitted edge still
contributes, including unrelated candidate edges to that exact terminal. This
preserves the existing threshold/classification, not distinct-root or runtime
caller counts. Local graph diagnostics retain incoming-query/reference counts
and global edge-payload reads separately from traversal work.
Exact combined-ID and source/original-ID lookups read only the requested row;
global identity-order traversal uses the index's .NET ordinal collation. All
admitted rows remain available, including unrelated competitors. Surface
projection is a repeatable lazy list rather than another full fact-input array.
Storage refusal discards the graph and returns the existing reduced-coverage
`GraphInputLimitReached` gap with reason `graph-storage-bytes`, not partial paths.
For local capacity diagnosis, the explicit retained-index storage test accepts
`TRACEMAP_GRAPH_DIAGNOSTIC_INDEX` and `TRACEMAP_GRAPH_DIAGNOSTIC_OUT` together.
The latter must be a new owned directory. Optional
`TRACEMAP_GRAPH_DIAGNOSTIC_STORAGE_BYTES` explicitly declares the scratch budget
within the same 64 KiB to 16 GiB range; omission uses 512 MiB.
Optional `TRACEMAP_GRAPH_DIAGNOSTIC_TEXT_BYTES` declares serialized input text
from 1 byte through Int32.MaxValue; omission uses 512 MiB. This is a separate
diagnostic input admission bound, not a row, scratch or production-default change.
Its local-only receipt binds exact
test/Reporting generators, full input bytes, actual query options and budgets;
the input is rehashed unchanged after read-only replay. Allocation snapshots are
sampled before successful scratch writes. On refusal, these prior samples and
successful-write counters are unvalidated partial observations, not an admitted
graph. No damaged scratch store is inspected after storage-full; unavailable
allocation/dbstat data remains unknown. Graph-stage observations measure wall-clock
intervals between fixed stage boundaries, including waits and GC, not CPU time or
complete CLI phase durations. They contain no method or source-path labels.
This does not measure OS memory or
temporary sorter disk, and is not a replacement for the native end-to-end gate.
The local diagnostic also flushes at most 32 fixed-name stage entries to
`graph-stages.ndjson` (at most 64 KiB). Every entry carries the exact test and
Reporting generator hashes plus bounded input commitment. Entries describe an
incomplete attempt, never admission; the completed receipt hashes this trace.
This permits checking progress without uploading private methods or source paths.

The storage ceiling includes fact storage and the order roster, excludes SQLite
sorter scratch files, and is not an OS working-set guarantee. Type-specific
compiled binding/dispatch metadata, alias-group
fan-out and selected traversal/output records still have managed allocations.
The 32/256-page public parity fixture proves an eight-times page-count ratio,
not representative source or compiled-graph disk/time/peak-memory acceptance.

### Bounded retained evidence retrieval

New native reports additionally index both complete handoff documents as a
lossless JSON-token tree in `review-evidence.sqlite`. Metadata binds the exact
CLI generator, run ID, input handoff hashes, node count and parser/storage bounds.
The completed checkpoint pins the actual database bytes. This index is private,
not another source scan, classification engine or privacy projection.

```text
tracemap webforms-review query --run <durable-run-root>
tracemap webforms-review query --run <durable-run-root> --document application --pointer /packet/surfaces --offset 0 --limit 5 --depth 2
tracemap webforms-review query --run <durable-run-root> --document compiled --pointer /chains/0 --depth 2 --limit 10
```

The closed query grammar accepts only `application` or `compiled`, an exact JSON
Pointer, nonnegative offset, limit 1–50 and depth 0–8. It never accepts SQL, globs,
short-name matches or a source path. JSON Pointer escapes `~` as `~0` and `/` as
`~1`. Object order and array ordinals remain original; child pointers select exact
records. Containers report total/returned/omitted children. Depth-limited or
paginated omission is not empty evidence, nor packet or graph truncation.

Each JSON response carries the actual query generator, preflight/checkpoint/index
hashes, original index generator/input hashes and its own bounded-input digest.
Output is buffered and capped at 128 KiB and 2,048 nodes; oversized responses emit
no partial stdout. Refine a refused query by choosing a child pointer or reducing
depth/limit. Query preserves complete leaf values, never truncates their text.
Index construction streams both JSON inputs, allows at most 2,000,000 nodes,
64 JSON levels, 4,096-character property names and a 1 MiB pending token buffer.
Input aggregate bytes use the report output budget; SQLite bytes use the retained
artifact budget. Failed indexing retains an unadmitted report attempt and never
publishes report completion.

Query verifies the completed journal and index before and after retrieval, rejects
SQLite sidecars and uses immutable read-only access under the existing run lock.
It does not require current source/publish/parent availability and does not read
those external inputs or full JSON handoffs. It therefore proves retained index
integrity, not fresh source/build/runtime validity or every other report file's
current bytes. Whole-index hash verification still streams the complete index;
this is a bounded-memory read, not a constant-time retrieval or scale benchmark.
Older native reports without this index are refused, never automatically repaired.
See the [agent handoff](WEBFORMS_AGENT_HANDOFF.md) and updated prompt for the
bounded review sequence. Existing PowerShell launch wrappers remain legacy-only.

An optional `publishReceiptRelativePath` names an existing local-only
`webforms-publish-binding.v1` receipt under `receiptRoot`, if declared, or the
explicit published root by default. `bindingReceipts` use the same receipt root;
assembly, map and PDB paths always remain relative to `publishedRoot`. The optional
receipt root is an explicit existing absolute directory, not discovered or created
by preflight. It is pinned in the private configuration and protected from output
overlap just like source/publish roots. Changed or missing receipt bytes reject
resume without rescanning. This permits owned evidence to live separately from the
read-only site; it does not yet generate that evidence or attest its ownership. Receipts
can live in a subdirectory: native execution passes the published root separately
instead of copying the DLLs/maps beside the receipt. Preflight pins receipt source
file hashes and requires every receipt DLL/map to be a declared assembly/map with
the same hash. Duplicate/unsafe paths, changed bytes, wrong commits or missing
selected pages reject the preflight. The existing Core policy then independently
validates source membership and page-map content during the scan; bad map content
remains reduced coverage, not a source/compiled join. Mapless receipts retain their
existing declared-map-inventory limitations. None of this creates an operator
attestation, validates a historical compiler or proves the DLLs came from this
source. Native preparation is a separate explicit operator-declaration command.

Normal `scan` also supports `--webforms-published-root <absolute-path>` together
with `--webforms-publish-receipt`. If absent, the established receipt-directory
root and receipt-byte digest behavior are unchanged. An explicit root participates
in the publish bounded-input digest through its path hash and is never written.

Execution uses configured metadata/IL budgets and normal portable-PDB defaults
with configured artifact count/file size. Optional `budgets.ilMaxBodies` accepts 1 through 1,000,000
and is hash-bound, passed to `scan --il-max-bodies`, and shown in native status.
Omission preserves the scanner's 50,000-body default and historical JSON shape.
A metadata-admitted assembly can still be refused by the IL body-count limit;
graph storage or work increases do not repair missing IL evidence. The stress
profile explicitly declares 250,000 bodies, not a larger production default.
Output admission is bounded separately
to 256 filesystem entries, configured per-artifact/total hash bytes, and 256
checkpoints. Source scanning and graph scale acceptance remain unproven; streaming
artifact hashes do not make the scanner's fact collection memory-bounded.

## Private configuration

This is a public-safe shape example, not a runnable private configuration. Replace
the roots and commit with actual values. Root paths are absolute; every declared
file is relative to its configured root. No assembly discovery or upload occurs.

```json
{
  "schemaVersion": "webforms-compiled-review-config.v1",
  "operation": "fresh",
  "sourceRoot": "/authorized/source",
  "sourceCommitSha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "projectMode": "projectless",
  "solutionRelativePath": null,
  "projectRelativePaths": [],
  "sourceFolders": ["."],
  "pageMode": "selected",
  "pageRelativePaths": ["Pages/Lookup.aspx"],
  "publishedRoot": "/authorized/published",
  "primaryAssemblies": ["bin/App_Code.dll", "bin/App_Web_public.dll"],
  "dependencyAssemblies": ["bin/PublicFramework.dll"],
  "bindingReceipts": [],
  "pdbInputs": [],
  "pageMaps": [],
  "parentScanRoot": null,
  "publishReceiptRelativePath": null,
  "receiptRoot": null,
  "publishSourceRelativePaths": null,
  "preparationProvenance": null,
  "budgets": {
    "maxInputFiles": 128,
    "maxAssemblyBytes": 67108864,
    "maxRetainedArtifactBytes": 17179869184,
    "maxTotalHashBytes": 68719476736,
    "ilMaxWork": 30000000,
    "graphMaxDepth": 20,
    "graphMaxPaths": 256,
    "graphMaxWork": 2000000,
    "metadataMaxWork": 500000,
    "metadataMaxText": 8192,
    "ilMaxText": 16384,
    "maxParentFacts": 5000000,
    "maxFactLineChars": 1048576
  }
}
```

- `fresh` inventories inputs for the native source-plus-compiled scan.
- `attach` requires `parentScanRoot`. Its five required scan artifacts are hashed
  using streaming reads. Source commit and parent manifest commit must match;
  the execution gate validates parent repository/index/fact identity and retained
  source bytes before producing a separate compiled-only scan. Source-to-compiled
  cross-index joins remain pending.
  A derived run never appends files to or modifies the parent scan.
- `projectMode` is exactly `projectless`, `solution` with one solution path, or
  `projects` with a nonempty native JSON path array. Selected files must exist.
- `sourceFolders` is a bounded array of source-relative directories; `.` explicitly
  selects the source root. These declarations restrict native fresh scan admission.
- `pageMode` is `selected` with nonempty `.aspx` paths, or `all` with an empty page
  list. This preflight does not claim that all-page extraction/reporting works.
- Assemblies, receipts, PDBs and maps are explicit lists under the published root.
  Scattered/external DLLs are deliberately not discovered; explicit external
  locator support belongs to a later configuration slice.
- The managed PE header is inspected without loading/executing the assembly.
  Architecture-specific dependencies do not require a runtime load for this check.
- Receipt schema and artifact-hash candidates are inspected, not admitted as
  bindings. Missing/ambiguous candidates remain explicit gaps. Authoritative
  identity, safe-locator, repository, build and commit-relation validation remains
  pending under the existing scanner policy. A candidate never establishes source
  ownership or source-line identity.
- Preflight hashes PDB/map bytes without semantic validation. A declared publish
  receipt is checked semantically only in the actual scan. Missing optional inputs
  and deferred validations are visible gaps, not inferred clean coverage.

## Limits and provenance

File count includes config, selected pages/projects and parent artifacts, not only
DLLs. Parent artifact size has a separate configured limit from assembly size;
there is no `Int32`/2-GiB hashing ceiling. Total hashing is bounded independently.
Config/receipt JSON is capped at 1 MiB, selected files and parent manifest at
4 MiB. All hashing is streaming; the managed metadata header uses a bounded PE
reader. These facts are not an eight-times corpus performance acceptance claim.

Preflight records IL and graph work limits without consuming them. Native fresh
execution enforces metadata/IL budgets; the separately checkpointed report phase
enforces graph traversal and projection budgets.
No budget is automatically raised.
Native report traversal accepts an explicitly configured `budgets.graphMaxPaths`
from 1 through 4,096; the default remains 256. Increasing this path-retention
budget does not increase depth, work, graph admission, projection or rendered
output limits, and does not promise that all paths will fit. The effective value
is pinned in the run configuration and report policy; refusal and truncation
remain explicit review gaps.
The input canonicalization is UTF-8 camel-case indented JSON of config SHA-256
and ordered input records (role then physical path). The manifest records its
exact CLI-assembly generator hash and actual bounded input hash. Run ID is not
part of the deterministic input digest. This artifact contains private paths,
commit identities and file hashes; it is local-only, never a shareable projection.

Source commit preflight checks current Git HEAD; it does not independently attest
cleanliness or freeze source bytes. Later source-snapshot/index and binding gates
must pass before execution. Input hashes are rechecked immediately before the
new run is published, and an existing output is never overwritten.

## Public subprocess scale diagnostic

`WebFormsNativeScaleTests` runs the real CLI in separate processes for receipt
preparation, preflight, execution and immutable completed resume. Its ordinary
CI smoke uses 1/8 pages. An explicit opt-in uses 32/256 pages with exactly eight
times the source bytes, cross-assembly calls, an overload competitor and four
database-API branches per page. Generated PE/IL is never executed and is not an
ASP.NET compilation claim. Both declared assemblies are explicitly byte-bound
operator candidates; missing compiler authenticity remains a gap.

For the larger diagnostic, set `TRACEMAP_WEBFORMS_NATIVE_SCALE=1` and
`TRACEMAP_WEBFORMS_SCALE_OUT` to a new absolute owned directory, then run
`dotnet test` with filter `FullyQualifiedName~WebFormsNativeScaleTests`. Existing
directories are refused. Explicit outputs, including failures, are retained.
The receipt pins the test/CLI generators and bounded corpus input hashes, records
elapsed time and actual OS peak resident usage per CLI process (`time -l` on
macOS or `time -v` on Linux), retained disk bytes, facts, paths, coverage and
truncation. It also retains measured page/compiled traversal work and the shared
per-query cap; these are deterministic search counters, not CPU or runtime calls.
Retained disk excludes transient graph/sorter disk peak; these public
synthetic observations do not establish arbitrary graph or private Windows scale.

For a sparse compiled-graph stress diagnostic, use
`TRACEMAP_WEBFORMS_NATIVE_SCALE=graph` with a new explicit output root. It retains
the same 32/256-page, exact-eight-times-source cases while adding 24,000/192,000
distinct unreachable compiled methods. This exercises full global inventory,
not just a larger reachable route. The diagnostic config explicitly uses two
million metadata work units, 250,000 IL bodies, and report admission of one million facts, 500,000
edges, 1 GiB text and 4 GiB scratch storage; production defaults and path/work
caps are unchanged. The receipt records the declared scratch budget, sparse
methods, observed graph nodes/edges and categorical input refusals. A refused
graph must classify zero paths and remain partial; that is evidence of a ceiling,
not a successful large-graph acceptance. Every input and failed output is retained.
The first retained graph diagnostic, using the default 512 MiB scratch budget,
admitted the 24,000-method case (49,228 graph
nodes and 128 compiled variants), but the 192,000-method case hit the unchanged
`graph-storage-bytes` ceiling. All 192,000 sparse methods were collected;
the refused graph correctly emitted zero compiled paths. That historical refusal
is not large-graph support. The subsequent strict fresh run on 2026-09-29 admits
the complete global inventory under the explicit 4 GiB scratch / 1 GiB text /
250,000-body profile, retains 128/1,024 paths and 32/256 surfaces, and verifies
immutable completed resume. Native run time was 32.97/304.76 seconds with OS
peak resident usage 941,457,408/4,185,030,656 bytes and retained run disk
629,400,563/5,034,029,161 bytes. Coverage remains partial and cycle-truncated.
These measured synthetic bounds do not imply arbitrary or private-site support;
production defaults remain unchanged. See
[strict fresh scale evidence](validation/webforms-native-scale-2026-09-29.md).
Set `TRACEMAP_WEBFORMS_REQUIRE_GRAPH_ADMISSION=1` alongside the graph profile
when validating a storage improvement. That acceptance run must admit both
graphs and retain all four branches per page; a correct refusal fails the test.
The earlier 256-page diagnostic retained all four compiled branches per page
but hit `ProjectlessPublishMemberWorkLimit` for source member joins. Qualified
candidate indexing now clears that limit in the same declared corpus while
preserving global name/assembly counts, opaque fallback rows and the existing
100,000 work cap. The rerun retains all branches and restores candidate edges;
cycle truncation remains explicit. A false truncation flag never clears other
coverage gaps. See the
[bounded benchmark observations](validation/webforms-native-scale-2026-09-28.md).

## Internal execution validation gate

Native execution calls the tested input gate before scanning. Inventory-only
`preflight` does not call it and remains a separate planning boundary.

Managed inspection reuses the exact scanner readers, receipt classification,
dependency resolution and global gap policy through `ManagedMetadataExtractor.InspectInputs`.
Hash matches alone never become bindings. Receipt-based `bound` still does not
prove source ownership, authentic compilation, runtime dispatch or SQL execution.
Metadata work/text bounds are separate from IL work/text bounds.

Parent validation checks current Git/root identity against the retained manifest,
its snapshot identity against the complete embedded SQLite manifest, SQLite
integrity, and every streamed NDJSON fact against its indexed identity, rule,
tier, symbols, spans, extractor and properties. Duplicate/missing rows fail.
Facts and SQLite row projections are bounded; duplicate detection uses SQLite's
file-backed temporary store with a bounded cache instead of an unbounded managed
set. Active WAL/SHM/journal sidecars are rejected. A sidecar-free checkpointed WAL
index is opened immutably without creating files in its parent folder. Native
SQLite checks can be interrupted on cancellation.

All preflight input hashes are rechecked before and after validation. Attachment
input validation additionally streams the retained `FileInventoried` roster from
the immutable index and recomputes current bytes with the scanner's exact snapshot
framing, twice around validation. The original digest must match exactly. The
ordered stream is bounded by `maxParentFacts` and the remaining `maxTotalHashBytes`
per hash pass; it rejects unsafe, duplicate, linked, missing or changed members.
It does not discover extra files or rerun semantic extraction. When the complete
retention pair is present, preflight pins both files, and validation checks their
scan identity, original manifest hash, roster hash, input digest and actual
membership count/bytes before accepting snapshot equality. A missing member of
the pair is an error, never an implicit downgrade to the older inventory path.
An older parent without the pair still uses only retained `FileInventoried` rows.
If those rows omitted semantic metadata inputs, it fails with
`PARENT_SOURCE_SNAPSHOT_MISMATCH_OR_INCOMPLETE_INVENTORY`; those members are not
guessed. An exact match establishes only the retained snapshot's declared scope,
not full current-repository coverage, clean Git state, historical build identity
or source-line identity. Fresh scans still establish their own snapshot.
Public gate tests cover receipt
identity/locator/ambiguity/staleness, parent tampering and count/row limits, escaped
filesystem names, cancellation and byte-for-byte parent immutability. They do not
establish private Windows or representative scale acceptance. Fresh execution
and immutable attachment/resume are implemented. Reporting can consume explicit
attachment links for retained VB PDB/publish candidates; native report execution,
complete compiled-site parity and unified workbench integration remain outstanding.

## Next slices and acceptance

### Explicit attachment combination contract

The .NET `CombineOptions.CompiledAttachments` API accepts explicit parent and
attachment index/manifest paths. It never discovers manifests next to an index
or grants join authority from matching labels or commits. Ordinary combine
behavior does not create attachment links. This is a .NET API contract consumed
by reporting, not a new operator command or completed native report phase.

Each declared pair must be present among the combine inputs. Admission streams
and pins actual index/manifest bytes, rejects SQLite sidecars and symbolic input
paths, compares the complete external and embedded manifests, validates the
compiled-only context, and matches exact parent manifest/index hashes, snapshot
and repository identity. Use physically resolved paths. Manifests are capped at
4 MiB; index and aggregate hashing caps are explicit API settings (default 16 GiB
per index and 32 GiB total), not scanner memory or scale acceptance claims.

Only a new output is admitted for attachment combination. Inputs are attached
through immutable read-only SQLite URIs and rehashed before link publication.
The additive `compiled_attachment_links` table records exact Combine generator
and bounded-input hashes, parent/child source IDs, scan IDs, content hashes,
snapshot, external/embedded manifest hashes and attachment-context hash. Original source namespaces and fact IDs
remain intact. Its digest is local integrity evidence, not an authenticated
signature or runtime proof. The normal native parent gate remains responsible
for complete fact/index/NDJSON and current retained-source admission.

Reporting validates the complete link, recorded embedded manifest bytes and
source metadata before using it. PDB and publish candidate bridges look up source
declarations only in the explicitly linked parent; metadata/IL facts stay in the
attachment index. Unique ownership, checksum, binding and signature policies are
unchanged. Cross-index candidate edges record `compiledAttachmentLinkSha256`;
local path JSON includes the exact `compiledAttachmentLinks` records. Ordinary
reports without links retain their previous JSON shape. Earlier draft link rows
without embedded-manifest hashes stop with `RECOMBINE_REQUIRED`, never an inferred
upgrade. Native execution still retains `CrossIndexParentJoinsPending` until its
report phase actually consumes and pins this contract.

Public synthetic tests establish PDB/publish VB method-chain, rule/tier/span and
classification parity with the single-index baseline, bidirectional member
candidates, original supporting fact namespaces, withheld ambiguous joins and
rejection of changed links/embedded manifests/source identity. These tests do not
establish native end-to-end workbench acceptance, Windows compiled-site parity,
all-pages coverage, new semantic source reconciliation or representative scale.

### Lossless grouped report projection

`GroupedCompiledPathHandoffBuilder` provides a private .NET projection of an
already admitted dependency-path report. It indexes exact full node/edge records
by content hash and keeps ordered references for every original path variant and
inventory occurrence. The retained header includes all coverage, gaps, sources,
query, classifications, limitations, traversal and attachment-link provenance.
Restoration verifies context, record/reference integrity, complete report digest
and exact chain membership; it reconstructs the original canonical report without
dropping evidence. Existing path JSON schemas and compatibility scripts are unchanged.

Chains are grouped by ordered node kind, full symbol identity, source index,
scan and commit. Short labels are for display only; overloads and source identities
remain separate. This is static presentation grouping, not runtime deduplication.
The `webforms-compiled-grouped-handoff.v1` artifact records the exact Reporting
assembly SHA-256 and a bounded-input digest over the independently admitted index
hash, canonical report hash, generator and configured projection limits. The
canonical report hash is not a hash of an arbitrary input JSON file's whitespace.
The caller must validate the actual index bytes separately.

Default projection caps are 256 MiB canonical input, 512 MiB aggregate HTML/JSON
output, 100,000 paths, 500,000 distinct records and 2,000,000 node/edge references.
These are explicit projection limits, not upstream graph-memory or eight-times
scale acceptance. Bounds stop publication rather than silently truncate variants.
`GroupedCompiledPathReportWriter` writes only new `compiled-paths.local.html` and
`compiled-paths.handoff.local.json` files and returns actual artifact hashes/bytes.
Failures leave unadmitted partial bytes for the owning workflow. The caller owns
output isolation and checkpoint admission. This API is not yet a native operator
report command or the unified application workbench phase.

All identities and input fingerprints remain local/private; there is no shareable
projection. The HTML shows each chain once, retains expandable exact identities,
transition evidence and every path variant, and links to the lossless handoff.
It does not upgrade page-chain verdicts or claim runtime SQL, source-line identity,
compiled build authenticity, all-page coverage or final Windows acceptance.

1. Authoritative source/parent/binding validation and resumable .NET phase execution.
2. Integration of grouped method chains into normal workbench navigation and an
   additive chain index in lossless JSON, without changing page verdicts.
3. Evidence-first Claude handoff updates and bounded retrieval over larger inputs.
4. Representative scale benchmarks, relocation and dry-run dependency cleanup.

Do not retire compatibility wrappers or the original working proof until public
parity tests and an authorized private Windows run validate the new flow.

Public tests: `WebFormsReviewPreflightTests`, `WebFormsReviewInputValidationTests`
and `WebFormsReviewExecutionTests`. The first slice validates real managed
headers, typed input failures, explicit gaps, parent immutability, deterministic
input hashing, cancellation, output separation, bounded JSON configuration,
and a native CLI invocation against a disposable public Git repository.
