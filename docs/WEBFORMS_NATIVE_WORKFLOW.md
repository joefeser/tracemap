# Native compiled Web Forms workflow

Status: native preflight, input validation and checkpointed fresh scanning.
Immutable attachment and unified review reports are still pending.

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

## Native fresh scan and resume

After preflight, a `fresh` configuration can execute the normal scanner once,
including explicitly declared managed metadata, receipts, portable PDB and IL
evidence. It does not rebuild or publish the site or create operator attestations:

```text
tracemap webforms-review run --run <durable-run-root>
tracemap webforms-review resume --run <durable-run-root>
```

The run owns these paths:

```text
run-manifest.json                 immutable preflight/input contract
checkpoints/0001.json              scan-started checkpoint
checkpoints/0002.json              completed/failed/cancelled checkpoint
attempts/<owned-id>/scan/          normal five scan artifacts plus scan receipt
.native-run.lock                   exclusive process lock, not a discovery hint
```

Checkpoints are append-only, contiguous and hash chained. Every checkpoint pins
the exact CLI generator, preflight bytes and bounded execution input; its separate
canonical payload hash covers status, gaps and derived claims. Completed scans
pin every output file, retain the scanner's actual source snapshot, and pass the
same manifest/index/NDJSON validation used for retained parents. There is no
successful checkpoint for missing, changed, oversized or inconsistent output.
Checkpoint SHA values are local integrity checks, not authenticated signatures.
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
`report.md` is available, but no unified workbench, grouped compiled-path handoff,
publish-map execution or graph-query phase is produced here. Raw page maps are
pinned, not promoted to a publish receipt. `attach` is explicitly refused before
execution; it must not be approximated by a fresh scan of the parent's source.

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
  the execution gate can validate parent repository/index identity, but attachment
  production and source-to-compiled cross-index joins remain pending.
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
- PDB/map bytes are hashed, not semantically validated. Missing optional inputs
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

All preflight input hashes are rechecked before and after validation. A retained
parent snapshot is **not** a validation of current source bytes; fresh scans must
establish their own actual source snapshot. Public gate tests cover receipt
identity/locator/ambiguity/staleness, parent tampering and count/row limits, escaped
filesystem names, cancellation and byte-for-byte parent immutability. They do not
establish private Windows or representative scale acceptance. Fresh execution
and resume are now implemented; attachment and reporting integration remain
outstanding.

## Next slices and acceptance

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
