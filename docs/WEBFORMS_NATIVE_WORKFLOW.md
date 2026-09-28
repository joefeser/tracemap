# Native compiled Web Forms workflow

Status: native preflight, input validation, checkpointed fresh scanning and
immutable compiled-only attachment. Explicit cross-index VB PDB/publish reporting
joins are implemented; native report orchestration and unified review reports
are still pending.

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
below. Native report orchestration is not implemented yet; the proven report
wrappers remain required.

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
bytes or legacy receipt limits reject publication of the new evidence folder.
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
review-config.local.json            config for preflight/run with this separate receipt root
```

Each derived JSON records exact generator and bounded-input hashes, directly or
through the config's `preparationProvenance`. The publish receipt keeps its
established source-roster `boundedInputSha256`; `receiptInputSha256` additionally
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
larger sets require future explicit partitioning, never an automatic limit raise.
Git output is bounded to 4,194,304 characters per stream and 15 seconds per check.

Then preflight the generated `review-config.local.json` into a separate new run
folder and use native `run/resume` below. Receipt generation does not finish
grouped workbench/handoff integration, all-pages partitioning or
representative scale/private compiled-site parity. Keep the proven wrappers.

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
Retain the external dependencies; relocation and cleanup are not supported yet.

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
checkpoints/0002.json              completed/failed/cancelled checkpoint
attempts/<owned-id>/scan/          five standard scan artifacts; fresh runs also retain snapshot pair
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
`retainedSnapshot=true;sourceRescanned=false`: current unpinned source-file edits
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

This milestone ends at `scan-completed-reports-pending`. The scan's normal
`report.md` is available, but no unified workbench, grouped compiled-path handoff
or graph-query phase is produced here. Raw page maps are not promoted to a
publish receipt automatically. Attachment completion additionally retains
`CrossIndexParentJoinsPending`: a valid derived index is not yet a joined
source-to-compiled workbench or a replacement for the proven reports.

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
with configured artifact count/file size. Output admission is bounded separately
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
execution enforces metadata/IL budgets; graph traversal is not yet executed.
No budget is automatically raised.
The input canonicalization is UTF-8 camel-case indented JSON of config SHA-256
and ordered input records (role then physical path). The manifest records its
exact CLI-assembly generator hash and actual bounded input hash. Run ID is not
part of the deterministic input digest. This artifact contains private paths,
commit identities and file hashes; it is local-only, never a shareable projection.

Source commit preflight checks current Git HEAD; it does not independently attest
cleanliness or freeze source bytes. Later source-snapshot/index and binding gates
must pass before execution. Input hashes are rechecked immediately before the
new run is published, and an existing output is never overwritten.

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
